using System.Net;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using AstilCodex.Contracts;
using AstilCodex.Core.Providers;
using AstilCodex.Providers.Configuration;
using AstilCodex.Providers.OpenAICompatible;
using AstilCodex.Providers.Security;

namespace AstilCodex.Providers.Anthropic;

public sealed class AnthropicMessagesChatProvider :
    IChatProvider,
    IProviderHealthCheck,
    IDisposable
{
    private const string AnthropicVersion = "2023-06-01";
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly ProviderProfile _profile;
    private readonly ISecretStore _secretStore;
    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;
    private readonly Uri _messagesEndpoint;

    public AnthropicMessagesChatProvider(
        ProviderProfile profile,
        ISecretStore secretStore,
        HttpClient? httpClient = null)
    {
        _profile = profile ?? throw new ArgumentNullException(nameof(profile));
        _secretStore = secretStore ?? throw new ArgumentNullException(nameof(secretStore));
        if (profile.Protocol != ProviderProtocol.AnthropicMessages)
        {
            throw new ArgumentException(
                "Anthropic provider requires the AnthropicMessages protocol.",
                nameof(profile));
        }

        _messagesEndpoint = ProviderProfileValidator.ValidateAndGetEndpoint(profile);
        _httpClient = httpClient ?? new HttpClient();
        _ownsHttpClient = httpClient is null;
    }

    public string ProviderId => _profile.ProfileId;

    public async IAsyncEnumerable<string> StreamReplyAsync(
        ChatRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(_profile.TimeoutSeconds));

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, _messagesEndpoint);
        await ApplyHeadersAsync(httpRequest, timeout.Token).ConfigureAwait(false);
        httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));

        var body = new AnthropicMessageRequest(
            _profile.Model,
            _profile.MaxOutputTokens,
            Stream: true,
            SystemPrompt:
                "You are Astil Codex, an original privacy-conscious desktop assistant. " +
                "Be accurate, concise, and explicit about uncertainty. Never claim a tool action occurred unless the core confirms it.",
            Messages: BuildMessages(request));
        httpRequest.Content = new StringContent(
            JsonSerializer.Serialize(body, SerializerOptions),
            Encoding.UTF8,
            "application/json");

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(
                httpRequest,
                HttpCompletionOption.ResponseHeadersRead,
                timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw CreateTimeoutException();
        }
        catch (HttpRequestException exception)
        {
            throw new ProviderException(
                "provider_unreachable",
                $"Unable to reach provider '{_profile.DisplayName}'.",
                innerException: exception);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                throw CreateHttpError(response);
            }

            Stream stream;
            try
            {
                stream = await response.Content.ReadAsStreamAsync(timeout.Token)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw CreateTimeoutException();
            }

            await using (stream)
            using (var reader = new StreamReader(stream, Encoding.UTF8))
            {
                var emitted = false;
                while (true)
                {
                    string? line;
                    try
                    {
                        line = await reader.ReadLineAsync(timeout.Token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                    {
                        throw CreateTimeoutException();
                    }

                    if (line is null)
                    {
                        break;
                    }

                    if (!line.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var data = line[5..].Trim();
                    if (data.Length == 0)
                    {
                        continue;
                    }

                    var chunk = ParseStreamChunk(data);
                    if (!string.IsNullOrEmpty(chunk))
                    {
                        emitted = true;
                        yield return chunk;
                    }
                }

                if (!emitted)
                {
                    throw new ProviderException(
                        "empty_provider_response",
                        $"Provider '{_profile.DisplayName}' returned no response text.");
                }
            }
        }
    }

    public async Task<ProviderHealthResult> CheckHealthAsync(
        CancellationToken cancellationToken = default)
    {
        var endpoint = ProviderProfileValidator.GetModelsEndpoint(_messagesEndpoint);
        using var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
        await ApplyHeadersAsync(request, cancellationToken).ConfigureAwait(false);

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            return new ProviderHealthResult(false, "unreachable", false, Array.Empty<string>());
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                return new ProviderHealthResult(
                    false,
                    $"http_{(int)response.StatusCode}",
                    false,
                    Array.Empty<string>());
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var models = ParseModels(json);
            return new ProviderHealthResult(
                true,
                "ready",
                models.Contains(_profile.Model, StringComparer.Ordinal),
                models);
        }
    }

    public void Dispose()
    {
        if (_ownsHttpClient)
        {
            _httpClient.Dispose();
        }
    }

    private async ValueTask ApplyHeadersAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_profile.SecretId))
        {
            throw new ProviderException(
                "missing_provider_secret",
                $"No credential identifier is configured for provider '{_profile.DisplayName}'.");
        }

        var secret = await _secretStore.GetAsync(_profile.SecretId, cancellationToken)
            .ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(secret))
        {
            throw new ProviderException(
                "missing_provider_secret",
                $"No credential is stored for provider '{_profile.DisplayName}'.");
        }

        request.Headers.Add("x-api-key", secret);
        request.Headers.Add("anthropic-version", AnthropicVersion);
        request.Headers.UserAgent.ParseAdd("AstilCodex/0.1-prealpha");
    }

    private static IReadOnlyList<AnthropicMessage> BuildMessages(ChatRequest request)
    {
        var messages = new List<AnthropicMessage>();
        foreach (var message in request.History.TakeLast(24))
        {
            var role = message.Role.ToLowerInvariant();
            if (role is "user" or "assistant")
            {
                messages.Add(new AnthropicMessage(role, message.Content));
            }
        }

        messages.Add(new AnthropicMessage("user", request.UserText));
        return messages;
    }

    private static string? ParseStreamChunk(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (!root.TryGetProperty("type", out var eventType))
            {
                return null;
            }

            if (string.Equals(eventType.GetString(), "error", StringComparison.Ordinal))
            {
                throw new ProviderException(
                    "provider_stream_error",
                    "Anthropic reported an error while streaming the response.");
            }

            if (!string.Equals(
                eventType.GetString(),
                "content_block_delta",
                StringComparison.Ordinal))
            {
                return null;
            }

            if (!root.TryGetProperty("delta", out var delta) ||
                !delta.TryGetProperty("type", out var deltaType) ||
                !string.Equals(deltaType.GetString(), "text_delta", StringComparison.Ordinal) ||
                !delta.TryGetProperty("text", out var text) ||
                text.ValueKind != JsonValueKind.String)
            {
                return null;
            }

            return text.GetString();
        }
        catch (JsonException exception)
        {
            throw new ProviderException(
                "malformed_provider_stream",
                "Anthropic returned malformed streaming JSON.",
                innerException: exception);
        }
    }

    private static IReadOnlyList<string> ParseModels(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty("data", out var data) ||
                data.ValueKind != JsonValueKind.Array)
            {
                return Array.Empty<string>();
            }

            return data.EnumerateArray()
                .Where(item => item.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.String)
                .Select(item => item.GetProperty("id").GetString())
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Cast<string>()
                .ToArray();
        }
        catch (JsonException)
        {
            return Array.Empty<string>();
        }
    }

    private ProviderException CreateTimeoutException() =>
        new(
            "provider_timeout",
            $"Provider '{_profile.DisplayName}' did not complete within {_profile.TimeoutSeconds} seconds.");

    private static ProviderException CreateHttpError(HttpResponseMessage response)
    {
        var code = response.StatusCode switch
        {
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => "provider_authentication_failed",
            HttpStatusCode.TooManyRequests => "provider_rate_limited",
            HttpStatusCode.NotFound => "provider_endpoint_or_model_not_found",
            (HttpStatusCode)529 => "provider_overloaded",
            _ when (int)response.StatusCode >= 500 => "provider_server_error",
            _ => "provider_request_failed"
        };
        var message = response.StatusCode switch
        {
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden =>
                "Anthropic authentication failed. Check the locally stored API key.",
            HttpStatusCode.TooManyRequests =>
                "Anthropic rate limit was reached. Wait before retrying.",
            HttpStatusCode.NotFound =>
                "Anthropic endpoint or model was not found.",
            (HttpStatusCode)529 =>
                "Anthropic is temporarily overloaded.",
            _ => $"Anthropic returned HTTP {(int)response.StatusCode}."
        };
        return new ProviderException(code, message, response.StatusCode);
    }

    private sealed record AnthropicMessageRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("max_tokens")] int MaxTokens,
        [property: JsonPropertyName("stream")] bool Stream,
        [property: JsonPropertyName("system")] string SystemPrompt,
        [property: JsonPropertyName("messages")] IReadOnlyList<AnthropicMessage> Messages);

    private sealed record AnthropicMessage(
        [property: JsonPropertyName("role")] string Role,
        [property: JsonPropertyName("content")] string Content);
}
