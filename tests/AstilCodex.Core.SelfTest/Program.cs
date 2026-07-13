using System.Text;
using AstilCodex.Contracts;
using AstilCodex.Core.Conversation;
using AstilCodex.Core.Permissions;
using AstilCodex.Core.Providers;
using AstilCodex.Core.Routing;
using AstilCodex.Core.Host;
using AstilCodex.Ipc;
using AstilCodex.Memory;

namespace AstilCodex.Core.SelfTest;

internal static class Program
{
    private static readonly PermissionBroker PermissionBroker = new();
    private static readonly TaskRouter Router = new(PermissionBroker);
    private static readonly HardwareProfile Hybrid = new(
        LocalModelAvailable: true,
        CloudProviderAvailable: true,
        LocalHighComplexityCapable: false);

    private static async Task<int> Main()
    {
        var tests = new (string Name, Func<Task> Run)[]
        {
            ("public high-complexity task uses cloud in Auto mode", PublicTaskUsesCloud),
            ("confidential task remains local", ConfidentialTaskRemainsLocal),
            ("secret cannot fall back to cloud", SecretCannotUseCloud),
            ("personal high-complexity task asks first", PersonalTaskAsksFirst),
            ("workspace write requires confirmation", WorkspaceWriteRequiresConfirmation),
            ("self-granted permission is denied", SelfGrantIsDenied),
            ("mock provider streams a response", MockProviderStreams),
            ("orchestrator emits state and manifest", OrchestratorProducesResult),
            ("SQLite memory persists and deletes a session", SqliteMemoryPersists),
            ("IPC frame round-trips a versioned envelope", IpcFrameRoundTrips),
            ("named-pipe health check responds", NamedPipeHealthResponds),
            ("IPC chat streams and persists locally", IpcChatStreamsAndPersists),
            ("IPC cancellation stops an active chat", IpcCancellationStopsChat)
        };

        var failures = 0;
        foreach (var test in tests)
        {
            try
            {
                await test.Run().ConfigureAwait(false);
                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write("PASS");
            }
            catch (Exception exception)
            {
                failures++;
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Write("FAIL");
                Console.ResetColor();
                Console.WriteLine($"  {test.Name}\n      {exception.Message}");
                continue;
            }

            Console.ResetColor();
            Console.WriteLine($"  {test.Name}");
        }

        Console.WriteLine($"\n{tests.Length - failures}/{tests.Length} production-core self-tests passed.");
        return failures == 0 ? 0 : 1;
    }

    private static Task PublicTaskUsesCloud()
    {
        var task = NewTask(
            "research.web",
            TaskComplexity.High,
            DataSensitivity.Public,
            internetRequired: true);
        var result = Router.Route(task, ProcessingPolicy.AutoPrivacyFirst, Hybrid);
        AssertEqual(ReasoningLocation.Cloud, result.ReasoningLocation);
        AssertTrue(result.CloudContextAllowed, "Public cloud context should be allowed.");
        return Task.CompletedTask;
    }

    private static Task ConfidentialTaskRemainsLocal()
    {
        var task = NewTask(
            "code.modify_project",
            TaskComplexity.High,
            DataSensitivity.Confidential,
            tools: ["files.read", "files.write"]);
        var result = Router.Route(task, ProcessingPolicy.CloudPreferred, Hybrid);
        AssertEqual(ReasoningLocation.Local, result.ReasoningLocation);
        AssertTrue(result.ConfirmationRequired, "Workspace write should require confirmation.");
        AssertFalse(result.CloudContextAllowed, "Confidential context must remain local.");
        return Task.CompletedTask;
    }

    private static Task SecretCannotUseCloud()
    {
        var cloudOnly = new HardwareProfile(false, true, false);
        var task = NewTask(
            "credentials.inspect",
            TaskComplexity.Low,
            DataSensitivity.Secret);
        var result = Router.Route(task, ProcessingPolicy.CloudPreferred, cloudOnly);
        AssertEqual(ReasoningLocation.Unavailable, result.ReasoningLocation);
        AssertEqual(ExecutionLocation.None, result.ExecutionLocation);
        AssertFalse(result.CloudContextAllowed, "Secret context must not be cloud eligible.");
        return Task.CompletedTask;
    }

    private static Task PersonalTaskAsksFirst()
    {
        var task = NewTask(
            "document.analyze",
            TaskComplexity.High,
            DataSensitivity.Personal);
        var result = Router.Route(task, ProcessingPolicy.AutoPrivacyFirst, Hybrid);
        AssertEqual(ReasoningLocation.Ask, result.ReasoningLocation);
        AssertTrue(result.ConfirmationRequired, "Provider approval should be required.");
        return Task.CompletedTask;
    }

    private static Task WorkspaceWriteRequiresConfirmation()
    {
        var result = PermissionBroker.Evaluate(["files.read", "files.write"]);
        AssertEqual(PermissionDecision.RequireConfirmation, result.Decision);
        return Task.CompletedTask;
    }

    private static Task SelfGrantIsDenied()
    {
        var result = PermissionBroker.Evaluate(["permissions.self_grant"]);
        AssertEqual(PermissionDecision.Deny, result.Decision);
        return Task.CompletedTask;
    }

    private static async Task MockProviderStreams()
    {
        var provider = new MockChatProvider(TimeSpan.Zero);
        var request = new ChatRequest(
            "self-test",
            AssistantMode.Companion,
            "Hello",
            Array.Empty<ChatMessage>());
        var output = new StringBuilder();
        await foreach (var chunk in provider.StreamReplyAsync(request))
        {
            output.Append(chunk);
        }

        AssertTrue(output.Length > 20, "Expected a non-empty streamed mock response.");
    }

    private static async Task OrchestratorProducesResult()
    {
        var states = new List<AvatarState>();
        var provider = new MockChatProvider(TimeSpan.Zero);
        var orchestrator = new ConversationOrchestrator(
            provider,
            Router,
            new TaskClassifier());
        var request = new ChatRequest(
            "self-test",
            AssistantMode.Developer,
            "Analyze this project without changing it",
            Array.Empty<ChatMessage>());

        var result = await orchestrator.ReplyAsync(
            request,
            ProcessingPolicy.AutoPrivacyFirst,
            Hybrid,
            stateChanged: (state, _) =>
            {
                states.Add(state.State);
                return ValueTask.CompletedTask;
            });

        AssertEqual(ReasoningLocation.Local, result.Manifest.ReasoningLocation);
        AssertTrue(result.Text.Length > 20, "Expected orchestrated response text.");
        AssertTrue(states.Contains(AvatarState.Thinking), "Thinking state was not emitted.");
        AssertTrue(states.Contains(AvatarState.Speaking), "Speaking state was not emitted.");
        AssertEqual(AvatarState.Ready, states[^1]);
    }

    private static async Task SqliteMemoryPersists()
    {
        var databasePath = NewTemporaryDatabasePath();
        try
        {
            var firstStore = new SqliteConversationStore(databasePath);
            await firstStore.InitializeAsync().ConfigureAwait(false);
            await firstStore.UpsertSessionAsync("memory-test", AssistantMode.Companion)
                .ConfigureAwait(false);
            await firstStore.AddMessageAsync("memory-test", "user", "Hello")
                .ConfigureAwait(false);
            await firstStore.AddMessageAsync("memory-test", "assistant", "Welcome")
                .ConfigureAwait(false);

            var reopenedStore = new SqliteConversationStore(databasePath);
            await reopenedStore.InitializeAsync().ConfigureAwait(false);
            var messages = await reopenedStore.GetMessagesAsync("memory-test")
                .ConfigureAwait(false);
            AssertEqual(2, messages.Count);
            AssertEqual("Hello", messages[0].Content);
            AssertEqual("Welcome", messages[1].Content);

            AssertTrue(
                await reopenedStore.DeleteSessionAsync("memory-test").ConfigureAwait(false),
                "Expected the stored session to be deleted.");
            AssertEqual(
                0,
                (await reopenedStore.GetMessagesAsync("memory-test").ConfigureAwait(false)).Count);
        }
        finally
        {
            DeleteDatabaseFiles(databasePath);
        }
    }

    private static async Task IpcFrameRoundTrips()
    {
        var envelope = IpcSerializer.CreateEnvelope(
            IpcMessageTypes.HealthRequest,
            new HealthRequest("self-test"),
            messageId: "frame-test");
        await using var stream = new MemoryStream();
        await IpcFrameCodec.WriteAsync(stream, envelope).ConfigureAwait(false);
        stream.Position = 0;
        var decoded = await IpcFrameCodec.ReadAsync(stream).ConfigureAwait(false);
        AssertTrue(decoded is not null, "Expected a decoded IPC envelope.");
        AssertEqual(IpcMessageTypes.HealthRequest, decoded!.MessageType);
        AssertEqual(Protocol.Version, decoded.ContractVersion);
        AssertEqual("self-test", IpcSerializer.GetPayload<HealthRequest>(decoded).ClientName);
    }

    private static async Task NamedPipeHealthResponds()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var server = new NamedPipeIpcServer(NewPipeName());
        var serverTask = server.RunSingleClientAsync(
            async (connection, token) =>
            {
                var request = await connection.ReceiveAsync(token).ConfigureAwait(false);
                AssertTrue(request is not null, "Server did not receive a health request.");
                await connection.SendAsync(
                    IpcSerializer.CreateEnvelope(
                        IpcMessageTypes.HealthResponse,
                        new HealthResponse("Astil Codex Core", Protocol.Version, "ready", DateTimeOffset.UtcNow),
                        request!.MessageId),
                    token).ConfigureAwait(false);
            },
            timeout.Token);

        await server.Ready.WaitAsync(timeout.Token).ConfigureAwait(false);
        await using var client = await NamedPipeIpcClient.ConnectAsync(
            server.PipeName,
            TimeSpan.FromSeconds(5),
            timeout.Token).ConfigureAwait(false);
        var requestEnvelope = IpcSerializer.CreateEnvelope(
            IpcMessageTypes.HealthRequest,
            new HealthRequest("self-test-client"),
            messageId: "health-test");
        await client.SendAsync(requestEnvelope, timeout.Token).ConfigureAwait(false);
        var responseEnvelope = await client.ReceiveAsync(timeout.Token).ConfigureAwait(false);
        AssertTrue(responseEnvelope is not null, "Client did not receive a health response.");
        AssertEqual(IpcMessageTypes.HealthResponse, responseEnvelope!.MessageType);
        AssertEqual("ready", IpcSerializer.GetPayload<HealthResponse>(responseEnvelope).Status);
        await serverTask.ConfigureAwait(false);
    }

    private static async Task IpcChatStreamsAndPersists()
    {
        var databasePath = NewTemporaryDatabasePath();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        try
        {
            var store = new SqliteConversationStore(databasePath);
            await store.InitializeAsync(timeout.Token).ConfigureAwait(false);
            var server = new NamedPipeIpcServer(NewPipeName());
            var service = CreateIpcService(store, TimeSpan.Zero);
            var serverTask = server.RunSingleClientAsync(
                service.RunClientAsync,
                timeout.Token);
            await server.Ready.WaitAsync(timeout.Token).ConfigureAwait(false);

            ChatCompletedEvent? completed = null;
            var streamed = new StringBuilder();
            await using (var client = await NamedPipeIpcClient.ConnectAsync(
                server.PipeName,
                TimeSpan.FromSeconds(5),
                timeout.Token).ConfigureAwait(false))
            {
                var request = IpcSerializer.CreateEnvelope(
                    IpcMessageTypes.ChatRequest,
                    new ChatIpcRequest(
                        "ipc-chat-test",
                        AssistantMode.Companion,
                        "Hello from IPC",
                        ProcessingPolicy.AutoPrivacyFirst),
                    messageId: "ipc-chat-request");
                await client.SendAsync(request, timeout.Token).ConfigureAwait(false);

                while (completed is null)
                {
                    var message = await client.ReceiveAsync(timeout.Token).ConfigureAwait(false)
                        ?? throw new InvalidOperationException("IPC connection ended before chat completion.");
                    if (message.MessageType == IpcMessageTypes.ChatChunk)
                    {
                        streamed.Append(IpcSerializer.GetPayload<ChatChunkEvent>(message).Text);
                    }
                    else if (message.MessageType == IpcMessageTypes.ChatCompleted)
                    {
                        completed = IpcSerializer.GetPayload<ChatCompletedEvent>(message);
                    }
                    else if (message.MessageType == IpcMessageTypes.Error)
                    {
                        throw new InvalidOperationException(
                            IpcSerializer.GetPayload<ErrorEvent>(message).Message);
                    }
                }
            }

            await serverTask.ConfigureAwait(false);
            AssertTrue(streamed.Length > 20, "Expected streamed chat chunks.");
            AssertEqual(streamed.ToString(), completed.Text);
            var stored = await store.GetMessagesAsync("ipc-chat-test", cancellationToken: timeout.Token)
                .ConfigureAwait(false);
            AssertEqual(2, stored.Count);
            AssertEqual("user", stored[0].Role);
            AssertEqual("assistant", stored[1].Role);
        }
        finally
        {
            DeleteDatabaseFiles(databasePath);
        }
    }

    private static async Task IpcCancellationStopsChat()
    {
        var databasePath = NewTemporaryDatabasePath();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        try
        {
            var store = new SqliteConversationStore(databasePath);
            await store.InitializeAsync(timeout.Token).ConfigureAwait(false);
            var server = new NamedPipeIpcServer(NewPipeName());
            var service = CreateIpcService(store, TimeSpan.FromMilliseconds(100));
            var serverTask = server.RunSingleClientAsync(
                service.RunClientAsync,
                timeout.Token);
            await server.Ready.WaitAsync(timeout.Token).ConfigureAwait(false);

            var cancelled = false;
            await using (var client = await NamedPipeIpcClient.ConnectAsync(
                server.PipeName,
                TimeSpan.FromSeconds(5),
                timeout.Token).ConfigureAwait(false))
            {
                const string requestId = "ipc-cancel-target";
                await client.SendAsync(
                    IpcSerializer.CreateEnvelope(
                        IpcMessageTypes.ChatRequest,
                        new ChatIpcRequest(
                            "ipc-cancel-test",
                            AssistantMode.Companion,
                            "Generate a response that will be cancelled",
                            ProcessingPolicy.AutoPrivacyFirst),
                        messageId: requestId),
                    timeout.Token).ConfigureAwait(false);

                IpcEnvelope? started;
                do
                {
                    started = await client.ReceiveAsync(timeout.Token).ConfigureAwait(false);
                }
                while (started is not null && started.MessageType != IpcMessageTypes.ChatStarted);
                AssertTrue(started is not null, "Chat did not start before cancellation.");

                await client.SendAsync(
                    IpcSerializer.CreateEnvelope(
                        IpcMessageTypes.CancelRequest,
                        new CancelTaskRequest(requestId)),
                    timeout.Token).ConfigureAwait(false);

                while (!cancelled)
                {
                    var message = await client.ReceiveAsync(timeout.Token).ConfigureAwait(false)
                        ?? throw new InvalidOperationException("IPC connection ended before cancellation acknowledgement.");
                    cancelled = message.MessageType == IpcMessageTypes.TaskCancelled;
                    if (message.MessageType == IpcMessageTypes.Error)
                    {
                        throw new InvalidOperationException(
                            IpcSerializer.GetPayload<ErrorEvent>(message).Message);
                    }
                }
            }

            await serverTask.ConfigureAwait(false);
            AssertTrue(cancelled, "Expected a task-cancelled IPC event.");
        }
        finally
        {
            DeleteDatabaseFiles(databasePath);
        }
    }

    private static CoreIpcService CreateIpcService(
        IConversationStore store,
        TimeSpan delay)
    {
        var orchestrator = new ConversationOrchestrator(
            new MockChatProvider(delay),
            Router,
            new TaskClassifier());
        return new CoreIpcService(
            orchestrator,
            store,
            new HardwareProfile(true, false, false));
    }

    private static string NewPipeName() => $"astil-codex-test-{Guid.NewGuid():N}";

    private static string NewTemporaryDatabasePath() =>
        Path.Combine(Path.GetTempPath(), $"astil-codex-{Guid.NewGuid():N}.db");

    private static void DeleteDatabaseFiles(string databasePath)
    {
        foreach (var suffix in new[] { string.Empty, "-wal", "-shm" })
        {
            var path = databasePath + suffix;
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    private static TaskRequest NewTask(
        string type,
        TaskComplexity complexity,
        DataSensitivity sensitivity,
        bool internetRequired = false,
        IReadOnlyList<string>? tools = null) =>
        new(
            $"test-{Guid.NewGuid():N}",
            type,
            complexity,
            sensitivity,
            internetRequired,
            tools ?? Array.Empty<string>());

    private static void AssertTrue(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static void AssertFalse(bool condition, string message) =>
        AssertTrue(!condition, message);

    private static void AssertEqual<T>(T expected, T actual)
        where T : notnull
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException($"Expected {expected}; received {actual}.");
        }
    }
}
