using System.Text;
using AstilCodex.Contracts;
using AstilCodex.Core.Conversation;
using AstilCodex.Core.Permissions;
using AstilCodex.Core.Providers;
using AstilCodex.Core.Routing;

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
            ("orchestrator emits state and manifest", OrchestratorProducesResult)
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
            stateChanged: state => states.Add(state.State));

        AssertEqual(ReasoningLocation.Local, result.Manifest.ReasoningLocation);
        AssertTrue(result.Text.Length > 20, "Expected orchestrated response text.");
        AssertTrue(states.Contains(AvatarState.Thinking), "Thinking state was not emitted.");
        AssertTrue(states.Contains(AvatarState.Speaking), "Speaking state was not emitted.");
        AssertEqual(AvatarState.Ready, states[^1]);
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
