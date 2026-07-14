namespace AstilCodex.Providers.Configuration;

public interface IProviderSettingsStore
{
    string SettingsPath { get; }

    Task<ProviderSettingsDocument> LoadAsync(
        CancellationToken cancellationToken = default);

    Task SaveAsync(
        ProviderSettingsDocument settings,
        CancellationToken cancellationToken = default);
}
