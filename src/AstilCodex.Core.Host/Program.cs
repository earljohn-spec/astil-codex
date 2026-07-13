using AstilCodex.Contracts;
using AstilCodex.Core.Conversation;
using AstilCodex.Core.Permissions;
using AstilCodex.Core.Providers;
using AstilCodex.Core.Routing;
using AstilCodex.Ipc;
using AstilCodex.Memory;

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

        var permissionBroker = new PermissionBroker();
        var orchestrator = new ConversationOrchestrator(
            new MockChatProvider(),
            new TaskRouter(permissionBroker),
            new TaskClassifier());
        var hardware = new HardwareProfile(
            LocalModelAvailable: true,
            CloudProviderAvailable: false,
            LocalHighComplexityCapable: false);

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("Astil Codex Core Host · IPC milestone");
        Console.ResetColor();
        Console.WriteLine($"Pipe: {IpcDefaults.PipeName}");
        Console.WriteLine($"Memory: {store.DatabasePath}");
        Console.WriteLine("Provider: mock.local (offline simulation)");
        Console.WriteLine("Press Ctrl+C to stop.\n");

        while (!shutdown.IsCancellationRequested)
        {
            var server = new NamedPipeIpcServer(IpcDefaults.PipeName);
            var service = new CoreIpcService(orchestrator, store, hardware);
            try
            {
                Console.WriteLine("Waiting for a local client...");
                await server.RunSingleClientAsync(
                    service.RunClientAsync,
                    shutdown.Token).ConfigureAwait(false);
                Console.WriteLine("Client disconnected.");
            }
            catch (OperationCanceledException) when (shutdown.IsCancellationRequested)
            {
                break;
            }
            catch (IOException exception)
            {
                Console.Error.WriteLine($"IPC error: {exception.Message}");
            }
        }

        Console.WriteLine("Astil Codex Core Host stopped.");
        return 0;
    }
}
