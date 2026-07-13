using AstilCodex.Contracts;
using AstilCodex.Core.Conversation;
using AstilCodex.Core.Permissions;
using AstilCodex.Core.Providers;
using AstilCodex.Core.Routing;
using AstilCodex.Memory;

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
        const string sessionId = "cli-development";
        var store = new SqliteConversationStore(
            SqliteConversationStore.GetDefaultDatabasePath());
        await store.InitializeAsync().ConfigureAwait(false);
        var storedMessages = await store.GetMessagesAsync(sessionId).ConfigureAwait(false);
        var history = storedMessages
            .Select(message => new ChatMessage(message.Role, message.Content, message.CreatedAt))
            .ToList();
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

        PrintHeader(provider.ProviderId, store.DatabasePath, history.Count);

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

            if (string.Equals(input.Trim(), "/history", StringComparison.OrdinalIgnoreCase))
            {
                PrintHistory(history);
                continue;
            }

            if (string.Equals(input.Trim(), "/memory clear", StringComparison.OrdinalIgnoreCase))
            {
                await store.ClearAllAsync().ConfigureAwait(false);
                history.Clear();
                Console.WriteLine("Local conversation memory cleared.");
                continue;
            }

            if (TryHandleCommand(input, ref mode, ref policy))
            {
                continue;
            }

            await store.UpsertSessionAsync(sessionId, mode).ConfigureAwait(false);
            var request = new ChatRequest(
                SessionId: sessionId,
                Mode: mode,
                UserText: input,
                History: history.ToArray());

            history.Add(new ChatMessage("user", input, DateTimeOffset.UtcNow));
            await store.AddMessageAsync(sessionId, "user", input).ConfigureAwait(false);
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
                    stateChanged: (state, _) =>
                    {
                        WriteState(state);
                        return ValueTask.CompletedTask;
                    },
                    chunkReceived: (chunk, _) =>
                    {
                        Console.Write(chunk);
                        return ValueTask.CompletedTask;
                    },
                    cancellationToken: currentRequest.Token);

                if (result.Manifest.ReasoningLocation == ReasoningLocation.Ask ||
                    result.Manifest.ReasoningLocation == ReasoningLocation.Unavailable)
                {
                    Console.Write(result.Text);
                }

                Console.WriteLine();
                WriteManifest(result.Manifest);
                history.Add(new ChatMessage("assistant", result.Text, DateTimeOffset.UtcNow));
                await store.AddMessageAsync(sessionId, "assistant", result.Text)
                    .ConfigureAwait(false);
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

        Console.WriteLine("Session closed. Conversation memory remains stored locally.");
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
            Console.WriteLine("Commands: /mode <name>, /policy <name>, /history, /memory clear, /help, /quit");
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

    private static void PrintHeader(string providerId, string databasePath, int historyCount)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("Astil Codex Core · local memory milestone");
        Console.ResetColor();
        Console.WriteLine($"Provider: {providerId} (offline simulation)");
        Console.WriteLine($"Memory: {databasePath}");
        Console.WriteLine($"Loaded messages: {historyCount}");
        Console.WriteLine("No files, cloud services, microphones, or computer tools are connected.");
        Console.WriteLine("Enter /help for commands or /quit to close.\n");
    }

    private static void PrintHistory(IReadOnlyList<ChatMessage> history)
    {
        if (history.Count == 0)
        {
            Console.WriteLine("No messages are stored in this session.");
            return;
        }

        Console.WriteLine("Recent local conversation:");
        foreach (var message in history.TakeLast(10))
        {
            Console.WriteLine($"  {message.Role}: {message.Content}");
        }
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
