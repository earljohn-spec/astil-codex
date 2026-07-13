namespace AstilCodex.Contracts;

public sealed record PermissionResult(
    PermissionDecision Decision,
    IReadOnlyList<string> Reasons);
