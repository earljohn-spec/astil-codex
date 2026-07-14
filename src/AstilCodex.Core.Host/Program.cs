using System.Text.Json;
using AstilCodex.Contracts;
using AstilCodex.Core.Conversation;
using AstilCodex.Core.Permissions;
using AstilCodex.Core.Providers;
using AstilCodex.Core.Routing;
using AstilCodex.Ipc;
using AstilCodex.Memory;
using AstilCodex.Providers;
using AstilCodex.Providers.Configuration;
using AstilCodex.Providers.Security;

namespace AstilCodex.Core.Host;

internal static class Program
{
    private static async Task<int> Main()
    {
        using var shutdown = new CancellationTokenSource();
        Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            shutdown.Cancel();
        };

        var store = new SqliteConversationStore(
            SqliteConversationStore.GetDefaultDatabasePath());
        await store.InitializeAsync(shutdown.Token).ConfigureAwait(false);

        var mockProvider = new MockChatProvider();
        var settingsStore = new JsonProviderSettingsStore(
            ProviderPaths.GetDefaultSettingsPath());
        ISecretStore secretStore;
        if (OperatingSystem.IsWindows())
        {
            secretStore = new DpapiFileSecretStore(
                ProviderPaths.GetDefaultSecretsDirectory());
        }
        else
        {
            secretStore = new InMemorySecretStore();
        }

        ConfiguredChatProviderResolver? configuredResolver = null;
        IChatProviderResolver providerResolver;
        try
        {
            configuredResolver = await ConfiguredChatProviderResolver.CreateAsync(
                settingsStore,
                secretStore,
                mockProvider,
                cancellationToken: shutdown.Token).ConfigureAwait(false);
            providerResolver = configuredResolver;
        }
        catch (Exception exception) when (
            exception is InvalidDataException or ArgumentException or JsonException)
        {
            Console.Error.WriteLine(
                "Provider configuration is invalid; using offline mock fallback: " +
                exception.Message);
            providerResolver = new FixedChatProviderResolver(mockProvider);
        }

        using (configuredResolver)
        {
            var permissionBroker = new PermissionBroker();
            var orchestrator = new ConversationOrchestrator(
                providerResolver,
                new TaskRouter(permissionBroker),
                new TaskClassifier());
            var hardware = new HardwareProfile(
                LocalModelAvailable: true,
                CloudProviderAvailable: configuredResolver?.HasCloudProvider ?? false,
                LocalHighComplexityCapable: false);

            PrintStartup(store, settingsStore, configuredResolver);
            await RunServerAsync(orchestrator, store, hardware, shutdown.Token)
                .ConfigureAwait(false);
        }

        Console.WriteLine("Astil Codex Core Host stopped.");
        return 0;
    }

    private static async Task RunServerAsync(
        ConversationOrchestrator orchestrator,
        IConversationStore store,
        HardwareProfile hardware,
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var server = new NamedPipeIpcServer(IpcDefaults.PipeName);
            var service = new CoreIpcService(orchestrator, store, hardware);
            try
            {
                Console.WriteLine("Waiting for a local client...");
                await server.RunSingleClientAsync(
                    service.RunClientAsync,
                    cancellationToken).ConfigureAwait(false);
                Console.WriteLine("Client disconnected.");
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (IOException exception)
            {
                Console.Error.WriteLine($"IPC error: {exception.Message}");
            }
        }
    }

    private static void PrintStartup(
        IConversationStore store,
        IProviderSettingsStore settingsStore,
        ConfiguredChatProviderResolver? configuredResolver)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("Astil Codex Core Host · provider foundation");
        Console.ResetColor();
        Console.WriteLine($"Pipe: {IpcDefaults.PipeName}");
        Console.WriteLine($"Memory: {store.DatabasePath}");
        Console.WriteLine($"Provider settings: {settingsStore.SettingsPath}");
        var providers = configuredResolver?.ConfiguredProviderIds ?? Array.Empty<string>();
        Console.WriteLine(providers.Count == 0
            ? "Providers: mock.local (offline fallback)"
            : "Providers: " + string.Join(", ", providers) + " · mock.local fallback");
        Console.WriteLine("Press Ctrl+C to stop.\n");
    }
}
