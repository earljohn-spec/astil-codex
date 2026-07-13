using AstilCodex.Contracts;
using AstilCodex.Core.Permissions;

namespace AstilCodex.Core.Routing;

public sealed class TaskRouter(IPermissionBroker permissionBroker) : ITaskRouter
{
    private readonly IPermissionBroker _permissionBroker =
        permissionBroker ?? throw new ArgumentNullException(nameof(permissionBroker));

    public TaskManifest Route(
        TaskRequest task,
        ProcessingPolicy policy,
        HardwareProfile hardware)
    {
        ArgumentNullException.ThrowIfNull(task);
        ArgumentNullException.ThrowIfNull(hardware);
        Validate(task);

        var permission = _permissionBroker.Evaluate(task.RequiredTools);
        var confirmationRequired = permission.Decision == PermissionDecision.RequireConfirmation;

        if (permission.Decision == PermissionDecision.Deny)
        {
            return Create(
                task,
                ReasoningLocation.Unavailable,
                ExecutionLocation.None,
                confirmationRequired: false,
                cloudContextAllowed: false,
                string.Join("; ", permission.Reasons));
        }

        var execution = task.AuthorizedRemoteTarget
            ? ExecutionLocation.AuthorizedRemote
            : task.RequiredTools.Count > 0
                ? ExecutionLocation.Local
                : ExecutionLocation.None;

        if (task.Sensitivity == DataSensitivity.Secret)
        {
            var location = hardware.LocalModelAvailable
                ? ReasoningLocation.Local
                : ReasoningLocation.Unavailable;
            return Create(
                task,
                location,
                location == ReasoningLocation.Unavailable ? ExecutionLocation.None : execution,
                confirmationRequired,
                cloudContextAllowed: false,
                "Secret data cannot be sent to an AI provider; use filtered local processing only.");
        }

        if (policy == ProcessingPolicy.LocalOnly)
        {
            return FinalizeDecision(
                task,
                LocalOrUnavailable(hardware),
                execution,
                confirmationRequired,
                cloudContextAllowed: false,
                "Local Only policy selected.");
        }

        if (policy == ProcessingPolicy.AskEveryTime)
        {
            if (hardware.CloudProviderAvailable)
            {
                return FinalizeDecision(
                    task,
                    ReasoningLocation.Ask,
                    execution,
                    confirmationRequired: true,
                    cloudContextAllowed: task.Sensitivity is DataSensitivity.Public or DataSensitivity.Personal,
                    "User selection is required before choosing local or cloud reasoning.");
            }

            return FinalizeDecision(
                task,
                LocalOrUnavailable(hardware),
                execution,
                confirmationRequired,
                cloudContextAllowed: false,
                "No cloud provider is configured; local processing selected.");
        }

        if (task.Sensitivity == DataSensitivity.Confidential)
        {
            return FinalizeDecision(
                task,
                LocalOrUnavailable(hardware),
                execution,
                confirmationRequired,
                cloudContextAllowed: false,
                "Confidential context remains local under the baseline policy.");
        }

        if (policy == ProcessingPolicy.CloudPreferred && hardware.CloudProviderAvailable)
        {
            return FinalizeDecision(
                task,
                ReasoningLocation.Cloud,
                execution,
                confirmationRequired,
                cloudContextAllowed: true,
                "Cloud Preferred policy selected for non-confidential context.");
        }

        var cloudWouldHelp = task.InternetRequired || task.Complexity == TaskComplexity.High;
        if (task.Sensitivity == DataSensitivity.Public &&
            cloudWouldHelp &&
            hardware.CloudProviderAvailable)
        {
            return FinalizeDecision(
                task,
                ReasoningLocation.Cloud,
                execution,
                confirmationRequired,
                cloudContextAllowed: true,
                "Public high-complexity or online task routed to cloud reasoning.");
        }

        if (task.Sensitivity == DataSensitivity.Personal &&
            task.Complexity == TaskComplexity.High &&
            hardware.CloudProviderAvailable &&
            !hardware.LocalHighComplexityCapable)
        {
            return FinalizeDecision(
                task,
                ReasoningLocation.Ask,
                execution,
                confirmationRequired: true,
                cloudContextAllowed: true,
                "Cloud reasoning may help, but personal context requires user approval.");
        }

        var fallback = LocalOrPublicCloudFallback(hardware, task.Sensitivity);
        return FinalizeDecision(
            task,
            fallback,
            execution,
            confirmationRequired,
            cloudContextAllowed: fallback == ReasoningLocation.Cloud,
            "Privacy-first routing selected the best available permitted provider.");
    }

    private static void Validate(TaskRequest task)
    {
        if (string.IsNullOrWhiteSpace(task.TaskId))
        {
            throw new ArgumentException("Task ID must not be empty.", nameof(task));
        }

        if (string.IsNullOrWhiteSpace(task.TaskType) || !task.TaskType.Contains('.', StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Task type must be namespaced, for example 'code.modify_project'.",
                nameof(task));
        }
    }

    private static ReasoningLocation LocalOrUnavailable(HardwareProfile hardware) =>
        hardware.LocalModelAvailable ? ReasoningLocation.Local : ReasoningLocation.Unavailable;

    private static ReasoningLocation LocalOrPublicCloudFallback(
        HardwareProfile hardware,
        DataSensitivity sensitivity)
    {
        if (hardware.LocalModelAvailable)
        {
            return ReasoningLocation.Local;
        }

        return hardware.CloudProviderAvailable && sensitivity == DataSensitivity.Public
            ? ReasoningLocation.Cloud
            : ReasoningLocation.Unavailable;
    }

    private static TaskManifest FinalizeDecision(
        TaskRequest task,
        ReasoningLocation location,
        ExecutionLocation execution,
        bool confirmationRequired,
        bool cloudContextAllowed,
        string explanation)
    {
        if (location == ReasoningLocation.Unavailable)
        {
            execution = ExecutionLocation.None;
            explanation += " No permitted provider is available.";
        }

        return Create(
            task,
            location,
            execution,
            confirmationRequired,
            cloudContextAllowed,
            explanation);
    }

    private static TaskManifest Create(
        TaskRequest task,
        ReasoningLocation reasoning,
        ExecutionLocation execution,
        bool confirmationRequired,
        bool cloudContextAllowed,
        string explanation) =>
        new(
            Protocol.Version,
            task.TaskId,
            task.TaskType,
            task.Complexity,
            task.Sensitivity,
            task.InternetRequired,
            reasoning,
            execution,
            task.RequiredTools,
            confirmationRequired,
            cloudContextAllowed,
            explanation);
}
