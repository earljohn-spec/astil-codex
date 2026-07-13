namespace AstilCodex.Contracts;

public enum AssistantMode
{
    Companion,
    Assistant,
    Focus,
    Developer,
    Creator
}

public enum ProcessingPolicy
{
    AutoPrivacyFirst,
    LocalOnly,
    CloudPreferred,
    AskEveryTime
}

public enum TaskComplexity
{
    Low,
    Medium,
    High
}

public enum DataSensitivity
{
    Public,
    Personal,
    Confidential,
    Secret
}

public enum ReasoningLocation
{
    Local,
    Cloud,
    Ask,
    Unavailable
}

public enum ExecutionLocation
{
    Local,
    AuthorizedRemote,
    None
}

public enum PermissionDecision
{
    Allow,
    RequireConfirmation,
    Deny
}

public enum AvatarState
{
    Ready,
    Listening,
    Thinking,
    Speaking,
    Acting,
    Success,
    Error,
    Cancelled
}
