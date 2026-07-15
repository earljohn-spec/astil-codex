namespace AstilCodex.Providers.Configuration;

public static class ProviderProfileValidator
{
    public static Uri ValidateAndGetEndpoint(ProviderProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentException.ThrowIfNullOrWhiteSpace(profile.ProfileId);
        ArgumentException.ThrowIfNullOrWhiteSpace(profile.DisplayName);
        ArgumentException.ThrowIfNullOrWhiteSpace(profile.ChatCompletionsEndpoint);
        ArgumentException.ThrowIfNullOrWhiteSpace(profile.Model);

        if (profile.ProfileId.Length > 64 ||
            profile.ProfileId.Any(character =>
                !(char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or '.')))
        {
            throw new ArgumentException(
                "Profile ID must use 1-64 letters, digits, dots, dashes, or underscores.",
                nameof(profile));
        }

        if (!Uri.TryCreate(profile.ChatCompletionsEndpoint, UriKind.Absolute, out var endpoint) ||
            endpoint.Scheme is not ("http" or "https"))
        {
            throw new ArgumentException("Provider endpoint must be an absolute HTTP(S) URL.", nameof(profile));
        }

        if (!string.IsNullOrEmpty(endpoint.UserInfo))
        {
            throw new ArgumentException("Provider credentials must not be embedded in the URL.", nameof(profile));
        }

        if (!string.IsNullOrEmpty(endpoint.Fragment))
        {
            throw new ArgumentException("Provider endpoint must not contain a URL fragment.", nameof(profile));
        }

        if (endpoint.Scheme == "http" && !IsLoopback(endpoint))
        {
            throw new ArgumentException(
                "Plain HTTP is permitted only for loopback local-model endpoints. Use HTTPS remotely.",
                nameof(profile));
        }

        if (profile.Location == ProviderLocation.Cloud && endpoint.Scheme != "https")
        {
            throw new ArgumentException("Cloud providers require HTTPS.", nameof(profile));
        }

        if (profile.Protocol == ProviderProtocol.AnthropicMessages)
        {
            if (profile.Location != ProviderLocation.Cloud || endpoint.Scheme != "https")
            {
                throw new ArgumentException(
                    "Native Anthropic Messages profiles must be cloud profiles using HTTPS.",
                    nameof(profile));
            }

            if (!endpoint.AbsolutePath.TrimEnd('/').EndsWith(
                "/v1/messages",
                StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException(
                    "Native Anthropic endpoint must end with /v1/messages.",
                    nameof(profile));
            }
        }

        if (profile.MaxOutputTokens is < 1 or > 32768)
        {
            throw new ArgumentOutOfRangeException(
                nameof(profile),
                "Max output tokens must be between 1 and 32768.");
        }

        if (profile.TimeoutSeconds is < 5 or > 600)
        {
            throw new ArgumentOutOfRangeException(
                nameof(profile),
                "Timeout must be between 5 and 600 seconds.");
        }

        return endpoint;
    }

    public static Uri GetModelsEndpoint(Uri chatCompletionsEndpoint)
    {
        ArgumentNullException.ThrowIfNull(chatCompletionsEndpoint);
        var path = chatCompletionsEndpoint.AbsolutePath.TrimEnd('/');
        const string chatSuffix = "/chat/completions";
        const string messagesSuffix = "/messages";
        path = path.EndsWith(chatSuffix, StringComparison.OrdinalIgnoreCase)
            ? path[..^chatSuffix.Length] + "/models"
            : path.EndsWith(messagesSuffix, StringComparison.OrdinalIgnoreCase)
                ? path[..^messagesSuffix.Length] + "/models"
                : path + "/models";
        var builder = new UriBuilder(chatCompletionsEndpoint)
        {
            Path = path,
            Query = string.Empty,
            Fragment = string.Empty
        };
        return builder.Uri;
    }

    private static bool IsLoopback(Uri endpoint)
    {
        if (string.Equals(endpoint.Host, "localhost", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return System.Net.IPAddress.TryParse(endpoint.Host, out var address) &&
            System.Net.IPAddress.IsLoopback(address);
    }
}
