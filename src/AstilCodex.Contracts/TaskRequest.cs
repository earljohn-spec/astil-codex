namespace AstilCodex.Contracts;

public sealed record TaskRequest(
    string TaskId,
    string TaskType,
    TaskComplexity Complexity,
    DataSensitivity Sensitivity,
    bool InternetRequired,
    IReadOnlyList<string> RequiredTools,
    bool AuthorizedRemoteTarget = false)
{
    public static TaskRequest Conversation(string taskId) => new(
        taskId,
        "conversation.reply",
        TaskComplexity.Low,
        DataSensitivity.Personal,
        InternetRequired: false,
        RequiredTools: Array.Empty<string>());
}
