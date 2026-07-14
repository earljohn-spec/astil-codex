using System.Net;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using AstilCodex.Contracts;
using AstilCodex.Core.Providers;
using AstilCodex.Providers.Configuration;
using AstilCodex.Providers.Security;

namespace AstilCodex.Providers.OpenAICompatible;

public sealed class OpenAICompatibleChatProvider : IChatProvider, IDisposable
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly ProviderProfile _profile;
    private readonly ISecretStore _secretStore;
    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;
    private readonly Uri _chatEndpoint;

    public OpenAICompatibleChatProvider(
        ProviderProfile profile,
        ISecretStore secretStore,
        HttpClient? httpClient = null)
    {
        _profile = profile ?? throw new ArgumentNullException(nameof(profile));
        _secretStore = secretStore ?? throw new ArgumentNullException(nameof(secretStore));
        _chatEndpoint = ProviderProfileValidator.ValidateAndGetEndpoint(profile);
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

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, _chatEndpoint);
        await ApplyAuthenticationAsync(httpRequest, timeout.Token).ConfigureAwait(false);
        httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        httpRequest.Headers.UserAgent.ParseAdd("AstilCodex/0.1-prealpha");

        var body = new ChatCompletionRequest(
            _profile.Model,
            BuildMessages(request),
            Stream: true,
            MaxTokens: _profile.MaxOutputTokens);
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
            throw new ProviderException(
                "provider_timeout",
                $"Provider '{_profile.DisplayName}' did not respond within {_profile.TimeoutSeconds} seconds.");
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

                    if (string.Equals(data, "[DONE]", StringComparison.Ordinal))
                    {
                        break;
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
        var endpoint = ProviderProfileValidator.GetModelsEndpoint(_chatEndpoint);
        using var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
        await ApplyAuthenticationAsync(request, cancellationToken).ConfigureAwait(false);
        request.Headers.UserAgent.ParseAdd("AstilCodex/0.1-prealpha");

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            return new ProviderHealthResult(
                false,
                "unreachable",
                false,
                Array.Empty<string>());
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

    private async ValueTask ApplyAuthenticationAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_profile.SecretId))
        {
            return;
        }

        var secret = await _secretStore.GetAsync(_profile.SecretId, cancellationToken)
            .ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(secret))
        {
            throw new ProviderException(
                "missing_provider_secret",
                $"No credential is stored for provider '{_profile.DisplayName}'.");
        }

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", secret);
    }

    private static IReadOnlyList<ChatCompletionMessage> BuildMessages(ChatRequest request)
    {
        var messages = new List<ChatCompletionMessage>
        {
            new(
                "system",
                "You are Astil Codex, an original privacy-conscious desktop assistant. " +
                "Be accurate, concise, and explicit about uncertainty. Never claim a tool action occurred unless the core confirms it.")
        };

        foreach (var message in request.History.TakeLast(24))
        {
            var role = message.Role.ToLowerInvariant();
            if (role is "user" or "assistant" or "system")
            {
                messages.Add(new ChatCompletionMessage(role, message.Content));
            }
        }

        messages.Add(new ChatCompletionMessage("user", request.UserText));
        return messages;
    }

    private static string? ParseStreamChunk(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (!root.TryGetProperty("choices", out var choices) ||
                choices.ValueKind != JsonValueKind.Array ||
                choices.GetArrayLength() == 0)
            {
                return null;
            }

            var choice = choices[0];
            if (!choice.TryGetProperty("delta", out var delta) ||
                !delta.TryGetProperty("content", out var content) ||
                content.ValueKind != JsonValueKind.String)
            {
                return null;
            }

            return content.GetString();
        }
        catch (JsonException exception)
        {
            throw new ProviderException(
                "malformed_provider_stream",
                "Provider returned malformed streaming JSON.",
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
            _ when (int)response.StatusCode >= 500 => "provider_server_error",
            _ => "provider_request_failed"
        };
        var message = response.StatusCode switch
        {
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden =>
                "Provider authentication failed. Check the locally stored credential.",
            HttpStatusCode.TooManyRequests =>
                "Provider rate limit was reached. Wait before retrying.",
            HttpStatusCode.NotFound =>
                "Provider endpoint or model was not found.",
            _ => $"Provider returned HTTP {(int)response.StatusCode}."
        };
        return new ProviderException(code, message, response.StatusCode);
    }

    private sealed record ChatCompletionRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("messages")] IReadOnlyList<ChatCompletionMessage> Messages,
        [property: JsonPropertyName("stream")] bool Stream,
        [property: JsonPropertyName("max_tokens")] int MaxTokens);

    private sealed record ChatCompletionMessage(
        [property: JsonPropertyName("role")] string Role,
        [property: JsonPropertyName("content")] string Content);
}
