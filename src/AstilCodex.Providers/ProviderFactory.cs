using AstilCodex.Core.Providers;
using AstilCodex.Providers.Anthropic;
using AstilCodex.Providers.Configuration;
using AstilCodex.Providers.OpenAICompatible;
using AstilCodex.Providers.Security;

namespace AstilCodex.Providers;

public static class ProviderFactory
{
    public static IChatProvider Create(
        ProviderProfile profile,
        ISecretStore secretStore,
        HttpClient? httpClient = null)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(secretStore);
        return profile.Protocol switch
        {
            ProviderProtocol.OpenAICompatible =>
                new OpenAICompatibleChatProvider(profile, secretStore, httpClient),
            ProviderProtocol.AnthropicMessages =>
                new AnthropicMessagesChatProvider(profile, secretStore, httpClient),
            _ => throw new ArgumentOutOfRangeException(
                nameof(profile),
                profile.Protocol,
                "Unsupported provider protocol.")
        };
    }
}
