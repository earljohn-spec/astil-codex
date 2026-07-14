namespace AstilCodex.Providers.Configuration;

public enum ProviderLocation
{
    Local,
    Cloud
}

public sealed record ProviderProfile(
    string ProfileId,
    string DisplayName,
    ProviderLocation Location,
    string ChatCompletionsEndpoint,
    string Model,
    string? SecretId,
    int MaxOutputTokens = 1024,
    int TimeoutSeconds = 90,
    bool Enabled = true);

public sealed record ProviderSettingsDocument(
    int SchemaVersion,
    IReadOnlyList<ProviderProfile> Profiles)
{
    public const int CurrentSchemaVersion = 1;

    public static ProviderSettingsDocument Empty =>
        new(CurrentSchemaVersion, Array.Empty<ProviderProfile>());
}
