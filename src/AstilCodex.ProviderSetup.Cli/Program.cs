using System.Text;
using AstilCodex.Providers;
using AstilCodex.Providers.Configuration;
using AstilCodex.Providers.OpenAICompatible;
using AstilCodex.Providers.Security;

namespace AstilCodex.ProviderSetup.Cli;

internal static class Program
{
    private static async Task<int> Main()
    {
        if (!OperatingSystem.IsWindows())
        {
            Console.Error.WriteLine(
                "Provider setup currently requires Windows because credentials use DPAPI.");
            return 1;
        }

        var settingsStore = new JsonProviderSettingsStore(
            ProviderPaths.GetDefaultSettingsPath());
        var secretStore = new DpapiFileSecretStore(
            ProviderPaths.GetDefaultSecretsDirectory());

        while (true)
        {
            var settings = await settingsStore.LoadAsync().ConfigureAwait(false);
            PrintHeader(settings, settingsStore);
            Console.WriteLine("1. Configure local OpenAI-compatible provider");
            Console.WriteLine("2. Configure cloud OpenAI-compatible provider");
            Console.WriteLine("3. Configure native Anthropic Claude provider");
            Console.WriteLine("4. Test configured provider");
            Console.WriteLine("5. Remove configured provider");
            Console.WriteLine("0. Exit");
            Console.Write("Selection: ");
            var selection = Console.ReadLine()?.Trim();
            Console.WriteLine();

            switch (selection)
            {
                case "1":
                    await ConfigureAsync(
                        ProviderLocation.Local,
                        ProviderProtocol.OpenAICompatible,
                        settings,
                        settingsStore,
                        secretStore).ConfigureAwait(false);
                    break;
                case "2":
                    await ConfigureAsync(
                        ProviderLocation.Cloud,
                        ProviderProtocol.OpenAICompatible,
                        settings,
                        settingsStore,
                        secretStore).ConfigureAwait(false);
                    break;
                case "3":
                    await ConfigureAsync(
                        ProviderLocation.Cloud,
                        ProviderProtocol.AnthropicMessages,
                        settings,
                        settingsStore,
                        secretStore).ConfigureAwait(false);
                    break;
                case "4":
                    await TestAsync(settings, secretStore).ConfigureAwait(false);
                    break;
                case "5":
                    await RemoveAsync(settings, settingsStore, secretStore).ConfigureAwait(false);
                    break;
                case "0":
                    return 0;
                default:
                    Console.WriteLine("Unknown selection.");
                    Pause();
                    break;
            }
        }
    }

    private static async Task ConfigureAsync(
        ProviderLocation location,
        ProviderProtocol protocol,
        ProviderSettingsDocument settings,
        IProviderSettingsStore settingsStore,
        ISecretStore secretStore)
    {
        var replacedProfile = settings.Profiles.FirstOrDefault(profile => profile.Location == location);
        var existing = replacedProfile?.Protocol == protocol ? replacedProfile : null;
        var isLocal = location == ProviderLocation.Local;
        var isAnthropic = protocol == ProviderProtocol.AnthropicMessages;
        var profileId = isLocal
            ? "local.default"
            : isAnthropic
                ? "cloud.anthropic"
                : "cloud.openai";
        var endpointDefault = existing?.ChatCompletionsEndpoint ??
            (isLocal
                ? "http://127.0.0.1:11434/v1/chat/completions"
                : isAnthropic
                    ? "https://api.anthropic.com/v1/messages"
                    : "https://api.openai.com/v1/chat/completions");
        var modelDefault = existing?.Model ?? (isAnthropic ? "claude-sonnet-5" : string.Empty);

        Console.WriteLine($"Configure {location} provider ({protocol})");
        var displayName = ReadWithDefault(
            "Display name",
            existing?.DisplayName ??
                (isLocal ? "Local Model" : isAnthropic ? "Anthropic Claude" : "Cloud Model"));
        var endpoint = ReadWithDefault(
            isAnthropic ? "Messages endpoint" : "Chat-completions endpoint",
            endpointDefault);
        var model = ReadWithDefault("Model ID", modelDefault);
        if (string.IsNullOrWhiteSpace(model))
        {
            Console.WriteLine("Model ID is required.");
            Pause();
            return;
        }

        var secretId = existing?.SecretId ?? $"provider:{profileId}";
        var currentSecret = await secretStore.GetAsync(secretId).ConfigureAwait(false);
        Console.Write(
            currentSecret is null
                ? isLocal
                    ? "API key (optional for local providers; input is hidden): "
                    : "API key (required; input is hidden): "
                : "New API key (leave empty to keep stored key; input is hidden): ");
        var secret = ReadSecret();
        Console.WriteLine();
        if (!isLocal && string.IsNullOrEmpty(secret) && currentSecret is null)
        {
            Console.WriteLine("A cloud API key is required.");
            Pause();
            return;
        }

        var profile = new ProviderProfile(
            profileId,
            displayName,
            location,
            endpoint,
            model,
            string.IsNullOrEmpty(secret) && currentSecret is null ? null : secretId,
            Protocol: protocol,
            MaxOutputTokens: existing?.MaxOutputTokens ?? 1024,
            TimeoutSeconds: existing?.TimeoutSeconds ?? 90,
            Enabled: true);

        try
        {
            ProviderProfileValidator.ValidateAndGetEndpoint(profile);
        }
        catch (Exception exception) when (exception is ArgumentException)
        {
            Console.WriteLine("Configuration rejected: " + exception.Message);
            Pause();
            return;
        }

        if (!string.IsNullOrEmpty(secret))
        {
            await secretStore.SetAsync(secretId, secret).ConfigureAwait(false);
        }

        var profiles = settings.Profiles
            .Where(item => item.Location != location)
            .Append(profile)
            .OrderBy(item => item.Location)
            .ToArray();
        await settingsStore.SaveAsync(
            new ProviderSettingsDocument(ProviderSettingsDocument.CurrentSchemaVersion, profiles))
            .ConfigureAwait(false);

        if (replacedProfile?.SecretId is not null &&
            !string.Equals(replacedProfile.SecretId, secretId, StringComparison.Ordinal))
        {
            await secretStore.DeleteAsync(replacedProfile.SecretId).ConfigureAwait(false);
        }

        Console.WriteLine("Provider configuration saved. API keys are not stored in providers.json.");
        Pause();
    }

    private static async Task TestAsync(
        ProviderSettingsDocument settings,
        ISecretStore secretStore)
    {
        var profile = SelectProfile(settings);
        if (profile is null)
        {
            Pause();
            return;
        }

        var provider = ProviderFactory.Create(profile, secretStore);
        try
        {
            var healthProvider = provider as IProviderHealthCheck
                ?? throw new InvalidOperationException("Provider does not implement a health check.");
            Console.WriteLine("Testing provider without sending a chat prompt...");
            var health = await healthProvider.CheckHealthAsync().ConfigureAwait(false);
            Console.WriteLine($"Status: {health.Status}");
            Console.WriteLine($"Reachable: {health.IsHealthy}");
            Console.WriteLine($"Configured model found: {health.ConfiguredModelFound}");
            if (health.AvailableModels.Count > 0)
            {
                Console.WriteLine("Available models (first 10):");
                foreach (var model in health.AvailableModels.Take(10))
                {
                    Console.WriteLine("  " + model);
                }
            }
        }
        catch (Exception exception) when (
            exception is ProviderException or HttpRequestException or TaskCanceledException)
        {
            Console.WriteLine("Provider test failed: " + exception.Message);
        }
        finally
        {
            (provider as IDisposable)?.Dispose();
        }

        Pause();
    }

    private static async Task RemoveAsync(
        ProviderSettingsDocument settings,
        IProviderSettingsStore settingsStore,
        ISecretStore secretStore)
    {
        var profile = SelectProfile(settings);
        if (profile is null)
        {
            Pause();
            return;
        }

        var profiles = settings.Profiles
            .Where(item => !string.Equals(item.ProfileId, profile.ProfileId, StringComparison.Ordinal))
            .ToArray();
        await settingsStore.SaveAsync(
            new ProviderSettingsDocument(ProviderSettingsDocument.CurrentSchemaVersion, profiles))
            .ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(profile.SecretId))
        {
            await secretStore.DeleteAsync(profile.SecretId).ConfigureAwait(false);
        }

        Console.WriteLine($"Removed provider '{profile.DisplayName}' and its stored credential.");
        Pause();
    }

    private static ProviderProfile? SelectProfile(ProviderSettingsDocument settings)
    {
        if (settings.Profiles.Count == 0)
        {
            Console.WriteLine("No provider profiles are configured.");
            return null;
        }

        for (var index = 0; index < settings.Profiles.Count; index++)
        {
            var profile = settings.Profiles[index];
            Console.WriteLine(
                $"{index + 1}. {profile.DisplayName} ({profile.Location}, {profile.Protocol})");
        }

        Console.Write("Provider number: ");
        return int.TryParse(Console.ReadLine(), out var selected) &&
            selected >= 1 && selected <= settings.Profiles.Count
            ? settings.Profiles[selected - 1]
            : null;
    }

    private static string ReadWithDefault(string label, string defaultValue)
    {
        Console.Write(string.IsNullOrWhiteSpace(defaultValue)
            ? label + ": "
            : $"{label} [{defaultValue}]: ");
        var value = Console.ReadLine()?.Trim();
        return string.IsNullOrWhiteSpace(value) ? defaultValue : value;
    }

    private static string ReadSecret()
    {
        var builder = new StringBuilder();
        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter)
            {
                return builder.ToString();
            }

            if (key.Key == ConsoleKey.Backspace && builder.Length > 0)
            {
                builder.Length--;
                continue;
            }

            if (!char.IsControl(key.KeyChar))
            {
                builder.Append(key.KeyChar);
            }
        }
    }

    private static void PrintHeader(
        ProviderSettingsDocument settings,
        IProviderSettingsStore settingsStore)
    {
        Console.Clear();
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("Astil Codex Provider Setup");
        Console.ResetColor();
        Console.WriteLine("Credentials: Windows DPAPI, current user");
        Console.WriteLine("Settings: " + settingsStore.SettingsPath);
        Console.WriteLine("Configured profiles: " + settings.Profiles.Count);
        Console.WriteLine();
    }

    private static void Pause()
    {
        Console.WriteLine();
        Console.Write("Press Enter to continue...");
        Console.ReadLine();
    }
}
