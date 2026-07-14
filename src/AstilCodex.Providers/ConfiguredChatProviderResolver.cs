using AstilCodex.Contracts;
using AstilCodex.Core.Providers;
using AstilCodex.Providers.Configuration;
using AstilCodex.Providers.OpenAICompatible;
using AstilCodex.Providers.Security;

namespace AstilCodex.Providers;

public sealed class ConfiguredChatProviderResolver : IChatProviderResolver, IDisposable
{
    private readonly IChatProvider _mockProvider;
    private readonly IReadOnlyDictionary<ProviderLocation, OpenAICompatibleChatProvider> _providers;

    private ConfiguredChatProviderResolver(
        IChatProvider mockProvider,
        IReadOnlyDictionary<ProviderLocation, OpenAICompatibleChatProvider> providers)
    {
        _mockProvider = mockProvider;
        _providers = providers;
    }

    public bool HasLocalProvider => _providers.ContainsKey(ProviderLocation.Local);

    public bool HasCloudProvider => _providers.ContainsKey(ProviderLocation.Cloud);

    public IReadOnlyCollection<string> ConfiguredProviderIds =>
        _providers.Values.Select(provider => provider.ProviderId).ToArray();

    public static async Task<ConfiguredChatProviderResolver> CreateAsync(
        IProviderSettingsStore settingsStore,
        ISecretStore secretStore,
        IChatProvider mockProvider,
        Func<ProviderProfile, HttpClient>? httpClientFactory = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settingsStore);
        ArgumentNullException.ThrowIfNull(secretStore);
        ArgumentNullException.ThrowIfNull(mockProvider);
        var settings = await settingsStore.LoadAsync(cancellationToken).ConfigureAwait(false);
        var providers = new Dictionary<ProviderLocation, OpenAICompatibleChatProvider>();

        foreach (var profile in settings.Profiles.Where(profile => profile.Enabled))
        {
            ProviderProfileValidator.ValidateAndGetEndpoint(profile);
            if (providers.ContainsKey(profile.Location))
            {
                throw new InvalidDataException(
                    $"Only one enabled {profile.Location} provider profile is supported in this milestone.");
            }

            var client = httpClientFactory?.Invoke(profile);
            providers[profile.Location] = new OpenAICompatibleChatProvider(
                profile,
                secretStore,
                client);
        }

        return new ConfiguredChatProviderResolver(mockProvider, providers);
    }

    public ValueTask<IChatProvider> ResolveAsync(
        ReasoningLocation reasoningLocation,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var location = reasoningLocation switch
        {
            ReasoningLocation.Local => ProviderLocation.Local,
            ReasoningLocation.Cloud => ProviderLocation.Cloud,
            _ => (ProviderLocation?)null
        };

        if (location is not null && _providers.TryGetValue(location.Value, out var provider))
        {
            return ValueTask.FromResult<IChatProvider>(provider);
        }

        return ValueTask.FromResult(_mockProvider);
    }

    public void Dispose()
    {
        foreach (var provider in _providers.Values)
        {
            provider.Dispose();
        }
    }
}
