using AstilCodex.Contracts;

namespace AstilCodex.Core.Routing;

public sealed class TaskClassifier
{
    private static readonly string[] ActionWords =
    [
        "build", "change", "create", "edit", "install", "modify", "refactor", "render", "run", "test", "write"
    ];

    public TaskRequest Classify(string text, AssistantMode mode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        var taskId = $"task-{Guid.NewGuid():N}";
        var actionRequested = ActionWords.Any(word =>
            text.Contains(word, StringComparison.OrdinalIgnoreCase));

        return mode switch
        {
            AssistantMode.Developer => new TaskRequest(
                taskId,
                actionRequested ? "code.modify_project" : "code.analyze_project",
                TaskComplexity.High,
                DataSensitivity.Confidential,
                InternetRequired: false,
                RequiredTools: actionRequested
                    ? ["files.read", "files.write", "git", "terminal.workspace"]
                    : ["files.read"]),

            AssistantMode.Creator => new TaskRequest(
                taskId,
                actionRequested ? "blender.prepare_scene" : "creator.plan",
                TaskComplexity.High,
                DataSensitivity.Personal,
                InternetRequired: false,
                RequiredTools: actionRequested
                    ? ["blender.execute", "files.write"]
                    : Array.Empty<string>()),

            AssistantMode.Assistant => new TaskRequest(
                taskId,
                "assistant.plan",
                TaskComplexity.Medium,
                DataSensitivity.Personal,
                InternetRequired: false,
                RequiredTools: Array.Empty<string>()),

            _ => TaskRequest.Conversation(taskId)
        };
    }
}
