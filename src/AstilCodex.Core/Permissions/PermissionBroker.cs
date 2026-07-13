using AstilCodex.Contracts;

namespace AstilCodex.Core.Permissions;

public sealed class PermissionBroker : IPermissionBroker
{
    private static readonly string[] AlwaysConfirmPrefixes =
    [
        "files.delete",
        "files.overwrite_unversioned",
        "terminal.outside_workspace",
        "packages.install",
        "software.install",
        "email.send",
        "calendar.modify",
        "external.publish",
        "account.modify",
        "system.modify",
        "cloud.upload_private",
        "cloud.start_paid_compute"
    ];

    private static readonly string[] DeniedPrefixes =
    [
        "credentials.reveal",
        "security.disable_audit",
        "permissions.self_grant"
    ];

    private static readonly string[] PlanConfirmationPrefixes =
    [
        "files.write",
        "git.commit",
        "terminal.workspace",
        "blender.execute",
        "ml.train"
    ];

    public PermissionResult Evaluate(IEnumerable<string> requestedTools)
    {
        ArgumentNullException.ThrowIfNull(requestedTools);
        var tools = requestedTools
            .Where(static tool => !string.IsNullOrWhiteSpace(tool))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

        var denied = Matching(tools, DeniedPrefixes);
        if (denied.Length > 0)
        {
            return new PermissionResult(
                PermissionDecision.Deny,
                denied.Select(static name => $"Denied capability: {name}").ToArray());
        }

        var immediate = Matching(tools, AlwaysConfirmPrefixes);
        if (immediate.Length > 0)
        {
            return new PermissionResult(
                PermissionDecision.RequireConfirmation,
                immediate.Select(static name => $"Sensitive action requires confirmation: {name}").ToArray());
        }

        var planned = Matching(tools, PlanConfirmationPrefixes);
        if (planned.Length > 0)
        {
            return new PermissionResult(
                PermissionDecision.RequireConfirmation,
                planned.Select(static name => $"Plan approval required: {name}").ToArray());
        }

        return new PermissionResult(
            PermissionDecision.Allow,
            ["No elevated capability requested."]);
    }

    private static string[] Matching(
        IEnumerable<string> requested,
        IEnumerable<string> prefixes) =>
        requested.Where(name => prefixes.Any(prefix =>
                string.Equals(name, prefix, StringComparison.Ordinal) ||
                name.StartsWith(prefix + ".", StringComparison.Ordinal)))
            .ToArray();
}
