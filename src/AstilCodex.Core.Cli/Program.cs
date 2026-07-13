using AstilCodex.Contracts;
using AstilCodex.Core.Conversation;
using AstilCodex.Core.Permissions;
using AstilCodex.Core.Providers;
using AstilCodex.Core.Routing;

namespace AstilCodex.Core.Cli;

internal static class Program
{
    private static async Task<int> Main()
    {
        var permissionBroker = new PermissionBroker();
        var router = new TaskRouter(permissionBroker);
        var classifier = new TaskClassifier();
        var provider = new MockChatProvider();
        var orchestrator = new ConversationOrchestrator(provider, router, classifier);
        var history = new List<ChatMessage>();
        var mode = AssistantMode.Companion;
        var policy = ProcessingPolicy.AutoPrivacyFirst;
        var hardware = HardwareProfile.Development;
        CancellationTokenSource? currentRequest = null;

        Console.CancelKeyPress += (_, eventArgs) =>
        {
            if (currentRequest is null)
            {
                return;
            }

            eventArgs.Cancel = true;
            currentRequest.Cancel();
        };

        PrintHeader(provider.ProviderId);

        while (true)
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write($"[{mode} · {policy}] ");
            Console.ForegroundColor = ConsoleColor.White;
            Console.Write("You: ");
            Console.ResetColor();

            var input = Console.ReadLine();
            if (input is null || string.Equals(input.Trim(), "/quit", StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            if (string.IsNullOrWhiteSpace(input))
            {
                continue;
            }

            if (TryHandleCommand(input, ref mode, ref policy))
            {
                continue;
            }

            var request = new ChatRequest(
                SessionId: "cli-development",
                Mode: mode,
                UserText: input,
                History: history.ToArray());

            history.Add(new ChatMessage("user", input, DateTimeOffset.UtcNow));
            currentRequest = new CancellationTokenSource();
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.Write("Astil: ");
            Console.ResetColor();

            try
            {
                var result = await orchestrator.ReplyAsync(
                    request,
                    policy,
                    hardware,
                    stateChanged: state => WriteState(state),
                    chunkReceived: chunk => Console.Write(chunk),
                    cancellationToken: currentRequest.Token);

                if (result.Manifest.ReasoningLocation == ReasoningLocation.Ask ||
                    result.Manifest.ReasoningLocation == ReasoningLocation.Unavailable)
                {
                    Console.Write(result.Text);
                }

                Console.WriteLine();
                WriteManifest(result.Manifest);
                history.Add(new ChatMessage("assistant", result.Text, DateTimeOffset.UtcNow));
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("\nRequest cancelled.");
            }
            finally
            {
                currentRequest.Dispose();
                currentRequest = null;
            }
        }

        Console.WriteLine("Session closed. Local mock data was not persisted.");
        return 0;
    }

    private static bool TryHandleCommand(
        string input,
        ref AssistantMode mode,
        ref ProcessingPolicy policy)
    {
        var parts = input.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        var command = parts[0].ToLowerInvariant();

        if (command == "/help")
        {
            Console.WriteLine("Commands: /mode <name>, /policy <name>, /help, /quit");
            Console.WriteLine("Modes: companion, assistant, focus, developer, creator");
            Console.WriteLine("Policies: auto, local, cloud, ask");
            return true;
        }

        if (command == "/mode")
        {
            if (parts.Length == 2 && Enum.TryParse<AssistantMode>(parts[1], true, out var parsedMode))
            {
                mode = parsedMode;
                Console.WriteLine($"Mode changed to {mode}.");
            }
            else
            {
                Console.WriteLine("Usage: /mode companion|assistant|focus|developer|creator");
            }

            return true;
        }

        if (command == "/policy")
        {
            if (parts.Length != 2)
            {
                Console.WriteLine("Usage: /policy auto|local|cloud|ask");
                return true;
            }

            var parsed = parts[1].ToLowerInvariant() switch
            {
                "auto" => ProcessingPolicy.AutoPrivacyFirst,
                "local" => ProcessingPolicy.LocalOnly,
                "cloud" => ProcessingPolicy.CloudPreferred,
                "ask" => ProcessingPolicy.AskEveryTime,
                _ => (ProcessingPolicy?)null
            };

            if (parsed is null)
            {
                Console.WriteLine("Usage: /policy auto|local|cloud|ask");
            }
            else
            {
                policy = parsed.Value;
                Console.WriteLine($"Policy changed to {policy}.");
            }

            return true;
        }

        return command.StartsWith("/", StringComparison.Ordinal)
            ? PrintUnknownCommand()
            : false;
    }

    private static bool PrintUnknownCommand()
    {
        Console.WriteLine("Unknown command. Enter /help for available commands.");
        return true;
    }

    private static void PrintHeader(string providerId)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("Astil Codex Core · executable foundation");
        Console.ResetColor();
        Console.WriteLine($"Provider: {providerId} (offline simulation)");
        Console.WriteLine("No files, cloud services, microphones, or computer tools are connected.");
        Console.WriteLine("Enter /help for commands or /quit to close.\n");
    }

    private static void WriteState(AvatarStateEvent state)
    {
        if (state.State is not (AvatarState.Thinking or AvatarState.Speaking))
        {
            return;
        }

        Console.Title = $"Astil Codex · {state.State}";
    }

    private static void WriteManifest(TaskManifest manifest)
    {
        Console.ForegroundColor = manifest.ConfirmationRequired
            ? ConsoleColor.Yellow
            : ConsoleColor.DarkGray;
        Console.WriteLine(
            $"  route={manifest.ReasoningLocation.ToString().ToLowerInvariant()} " +
            $"execution={manifest.ExecutionLocation.ToString().ToLowerInvariant()} " +
            $"approval={manifest.ConfirmationRequired.ToString().ToLowerInvariant()}");
        Console.ResetColor();
    }
}
