using System.Net;
using System.Net.Http.Headers;
using System.Text;
using AstilCodex.Contracts;
using AstilCodex.Core.Conversation;
using AstilCodex.Core.Permissions;
using AstilCodex.Core.Providers;
using AstilCodex.Core.Routing;
using AstilCodex.Core.Host;
using AstilCodex.Ipc;
using AstilCodex.Memory;
using AstilCodex.Providers;
using AstilCodex.Providers.Configuration;
using AstilCodex.Providers.OpenAICompatible;
using AstilCodex.Providers.Security;

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
            ("IPC cancellation stops an active chat", IpcCancellationStopsChat),
            ("provider endpoints enforce transport security", ProviderEndpointSecurityRules),
            ("provider settings persist without credentials", ProviderSettingsRoundTrip),
            ("OpenAI-compatible provider streams text", OpenAiProviderStreams),
            ("provider health lists configured model", ProviderHealthListsModel),
            ("provider resolver selects local and cloud profiles", ProviderResolverSelectsProfiles),
            ("provider streaming honors cancellation", ProviderStreamingHonorsCancellation),
            ("DPAPI secret storage round-trips on Windows", DpapiSecretRoundTrip)
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
        AssertEqual("mock.local", result.ProviderId);
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
            AssertEqual("mock.local", completed.ProviderId);
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
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try
        {
            var store = new SqliteConversationStore(databasePath);
            await store.InitializeAsync(timeout.Token).ConfigureAwait(false);

            for (var iteration = 0; iteration < 1; iteration++)
            {
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
                    var requestId = $"ipc-cancel-target-{iteration}";
                    await client.SendAsync(
                        IpcSerializer.CreateEnvelope(
                            IpcMessageTypes.ChatRequest,
                            new ChatIpcRequest(
                                $"ipc-cancel-test-{iteration}",
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
        }
        finally
        {
            DeleteDatabaseFiles(databasePath);
        }
    }

    private static Task ProviderEndpointSecurityRules()
    {
        var local = NewProviderProfile(
            "local.test",
            ProviderLocation.Local,
            "http://127.0.0.1:11434/v1/chat/completions");
        var cloud = NewProviderProfile(
            "cloud.test",
            ProviderLocation.Cloud,
            "https://provider.example/v1/chat/completions");
        ProviderProfileValidator.ValidateAndGetEndpoint(local);
        ProviderProfileValidator.ValidateAndGetEndpoint(cloud);

        var insecureRemote = NewProviderProfile(
            "cloud.insecure",
            ProviderLocation.Cloud,
            "http://provider.example/v1/chat/completions");
        var rejected = false;
        try
        {
            ProviderProfileValidator.ValidateAndGetEndpoint(insecureRemote);
        }
        catch (ArgumentException)
        {
            rejected = true;
        }

        AssertTrue(rejected, "Remote HTTP provider endpoint should be rejected.");
        return Task.CompletedTask;
    }

    private static async Task ProviderSettingsRoundTrip()
    {
        var directory = NewTemporaryDirectory();
        try
        {
            var path = Path.Combine(directory, "providers.json");
            var store = new JsonProviderSettingsStore(path);
            var profile = NewProviderProfile(
                "cloud.test",
                ProviderLocation.Cloud,
                "https://provider.example/v1/chat/completions") with
            {
                SecretId = "provider:cloud.test"
            };
            var document = new ProviderSettingsDocument(
                ProviderSettingsDocument.CurrentSchemaVersion,
                [profile]);
            await store.SaveAsync(document).ConfigureAwait(false);
            var reloaded = await store.LoadAsync().ConfigureAwait(false);
            AssertEqual(1, reloaded.Profiles.Count);
            AssertEqual(profile, reloaded.Profiles[0]);

            var raw = await File.ReadAllTextAsync(path).ConfigureAwait(false);
            AssertFalse(raw.Contains("secret-value", StringComparison.Ordinal),
                "Provider settings must not contain API key material.");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static async Task OpenAiProviderStreams()
    {
        var secretStore = new InMemorySecretStore();
        await secretStore.SetAsync("provider:cloud.test", "secret-value");
        string? capturedBody = null;
        var handler = new DelegateHttpMessageHandler(async (request, cancellationToken) =>
        {
            AssertEqual(HttpMethod.Post, request.Method);
            AssertEqual(
                new AuthenticationHeaderValue("Bearer", "secret-value"),
                request.Headers.Authorization!);
            capturedBody = await request.Content!.ReadAsStringAsync(cancellationToken)
                .ConfigureAwait(false);
            var content = new StringContent(
                "data: {\"choices\":[{\"delta\":{\"content\":\"Hello\"}}]}\n\n" +
                "data: {\"choices\":[{\"delta\":{\"content\":\" world\"}}]}\n\n" +
                "data: [DONE]\n\n",
                Encoding.UTF8,
                "text/event-stream");
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
        });
        using var client = new HttpClient(handler);
        using var provider = new OpenAICompatibleChatProvider(
            NewProviderProfile(
                "cloud.test",
                ProviderLocation.Cloud,
                "https://provider.example/v1/chat/completions") with
            {
                SecretId = "provider:cloud.test"
            },
            secretStore,
            client);
        var output = new StringBuilder();
        await foreach (var chunk in provider.StreamReplyAsync(NewChatRequest()))
        {
            output.Append(chunk);
        }

        AssertEqual("Hello world", output.ToString());
        AssertTrue(capturedBody is not null && capturedBody.Contains("test-model", StringComparison.Ordinal),
            "Provider request did not include the configured model.");
        AssertFalse(capturedBody!.Contains("secret-value", StringComparison.Ordinal),
            "Provider credential must not appear in the JSON body.");
    }

    private static async Task ProviderHealthListsModel()
    {
        var handler = new DelegateHttpMessageHandler((request, _) =>
        {
            AssertEqual(HttpMethod.Get, request.Method);
            AssertEqual("/v1/models", request.RequestUri!.AbsolutePath);
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    "{\"data\":[{\"id\":\"test-model\"},{\"id\":\"other-model\"}]}",
                    Encoding.UTF8,
                    "application/json")
            };
            return Task.FromResult(response);
        });
        using var client = new HttpClient(handler);
        using var provider = new OpenAICompatibleChatProvider(
            NewProviderProfile(
                "local.test",
                ProviderLocation.Local,
                "http://127.0.0.1:11434/v1/chat/completions"),
            new InMemorySecretStore(),
            client);
        var health = await provider.CheckHealthAsync().ConfigureAwait(false);
        AssertTrue(health.IsHealthy, "Provider should be healthy.");
        AssertTrue(health.ConfiguredModelFound, "Configured model should be present.");
        AssertEqual(2, health.AvailableModels.Count);
    }

    private static async Task ProviderResolverSelectsProfiles()
    {
        var directory = NewTemporaryDirectory();
        try
        {
            var settingsStore = new JsonProviderSettingsStore(
                Path.Combine(directory, "providers.json"));
            var local = NewProviderProfile(
                "local.test",
                ProviderLocation.Local,
                "http://127.0.0.1:11434/v1/chat/completions");
            var cloud = NewProviderProfile(
                "cloud.test",
                ProviderLocation.Cloud,
                "https://provider.example/v1/chat/completions");
            await settingsStore.SaveAsync(new ProviderSettingsDocument(
                ProviderSettingsDocument.CurrentSchemaVersion,
                [local, cloud])).ConfigureAwait(false);

            using var resolver = await ConfiguredChatProviderResolver.CreateAsync(
                settingsStore,
                new InMemorySecretStore(),
                new MockChatProvider(TimeSpan.Zero),
                _ => new HttpClient(new DelegateHttpMessageHandler((_, _) =>
                    Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)))))
                .ConfigureAwait(false);
            var localProvider = await resolver.ResolveAsync(ReasoningLocation.Local);
            var cloudProvider = await resolver.ResolveAsync(ReasoningLocation.Cloud);
            AssertEqual("local.test", localProvider.ProviderId);
            AssertEqual("cloud.test", cloudProvider.ProviderId);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static async Task ProviderStreamingHonorsCancellation()
    {
        var handler = new DelegateHttpMessageHandler(async (_, cancellationToken) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        using var client = new HttpClient(handler);
        using var provider = new OpenAICompatibleChatProvider(
            NewProviderProfile(
                "local.cancel",
                ProviderLocation.Local,
                "http://127.0.0.1:11434/v1/chat/completions"),
            new InMemorySecretStore(),
            client);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        var cancelled = false;
        try
        {
            await foreach (var _ in provider.StreamReplyAsync(
                NewChatRequest(),
                cancellation.Token))
            {
            }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            cancelled = true;
        }

        AssertTrue(cancelled, "Provider streaming should honor caller cancellation.");
    }

    private static async Task DpapiSecretRoundTrip()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var directory = NewTemporaryDirectory();
        try
        {
            var store = new DpapiFileSecretStore(directory);
            await store.SetAsync("provider:test", "secret-value");
            AssertEqual("secret-value", (await store.GetAsync("provider:test"))!);
            AssertTrue(await store.DeleteAsync("provider:test"), "Secret should be deleted.");
            AssertTrue(await store.GetAsync("provider:test") is null, "Deleted secret should be absent.");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static ProviderProfile NewProviderProfile(
        string profileId,
        ProviderLocation location,
        string endpoint) =>
        new(
            profileId,
            profileId,
            location,
            endpoint,
            "test-model",
            SecretId: null,
            MaxOutputTokens: 256,
            TimeoutSeconds: 10,
            Enabled: true);

    private static ChatRequest NewChatRequest() =>
        new(
            "provider-test-session",
            AssistantMode.Companion,
            "Hello provider",
            Array.Empty<ChatMessage>());

    private static string NewTemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"astil-codex-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private sealed class DelegateHttpMessageHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
        : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handler =
            handler ?? throw new ArgumentNullException(nameof(handler));

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            _handler(request, cancellationToken);
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
