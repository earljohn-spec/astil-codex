using System.Text.Json;
using System.Text.Json.Serialization;

namespace AstilCodex.Providers.Configuration;

public sealed class JsonProviderSettingsStore : IProviderSettingsStore
{
    private static readonly JsonSerializerOptions SerializerOptions = CreateSerializerOptions();

    public JsonProviderSettingsStore(string settingsPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(settingsPath);
        SettingsPath = Path.GetFullPath(settingsPath);
    }

    public string SettingsPath { get; }

    public async Task<ProviderSettingsDocument> LoadAsync(
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(SettingsPath))
        {
            return ProviderSettingsDocument.Empty;
        }

        await using var stream = new FileStream(
            SettingsPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 4096,
            useAsync: true);
        var settings = await JsonSerializer.DeserializeAsync<ProviderSettingsDocument>(
            stream,
            SerializerOptions,
            cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidDataException("Provider settings file is empty or invalid.");
        if (settings.SchemaVersion != ProviderSettingsDocument.CurrentSchemaVersion)
        {
            throw new InvalidDataException(
                $"Unsupported provider settings schema: {settings.SchemaVersion}.");
        }

        return settings;
    }

    public async Task SaveAsync(
        ProviderSettingsDocument settings,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (settings.SchemaVersion != ProviderSettingsDocument.CurrentSchemaVersion)
        {
            throw new ArgumentException("Provider settings schema version is invalid.", nameof(settings));
        }

        var directory = Path.GetDirectoryName(SettingsPath)
            ?? throw new InvalidOperationException("Provider settings directory is unavailable.");
        Directory.CreateDirectory(directory);
        var temporaryPath = SettingsPath + ".tmp";

        await using (var stream = new FileStream(
            temporaryPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 4096,
            useAsync: true))
        {
            await JsonSerializer.SerializeAsync(
                stream,
                settings,
                SerializerOptions,
                cancellationToken).ConfigureAwait(false);
            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }

        File.Move(temporaryPath, SettingsPath, overwrite: true);
    }

    private static JsonSerializerOptions CreateSerializerOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = false,
            WriteIndented = true
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        return options;
    }
}
