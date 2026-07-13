namespace AstilCodex.Contracts;

public sealed record TaskManifest(
    string ContractVersion,
    string TaskId,
    string TaskType,
    TaskComplexity Complexity,
    DataSensitivity DataSensitivity,
    bool InternetRequired,
    ReasoningLocation ReasoningLocation,
    ExecutionLocation ExecutionLocation,
    IReadOnlyList<string> RequiredTools,
    bool ConfirmationRequired,
    bool CloudContextAllowed,
    string Explanation);
