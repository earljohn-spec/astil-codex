namespace AstilCodex.Providers.Configuration;

public static class ProviderPaths
{
    public static string GetApplicationDataRoot()
    {
        var localData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(localData))
        {
            localData = AppContext.BaseDirectory;
        }

        return Path.Combine(localData, "AstilCodex");
    }

    public static string GetDefaultSettingsPath() =>
        Path.Combine(GetApplicationDataRoot(), "config", "providers.json");

    public static string GetDefaultSecretsDirectory() =>
        Path.Combine(GetApplicationDataRoot(), "secrets");
}
