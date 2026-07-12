from __future__ import annotations

from .domain import (
    Complexity,
    DataSensitivity,
    ExecutionLocation,
    HardwareProfile,
    ProcessingPolicy,
    ReasoningLocation,
    RoutingDecision,
    TaskRequest,
)
from .permissions import PermissionBroker, PermissionDecision


class TaskRouter:
    def __init__(self, permission_broker: PermissionBroker | None = None) -> None:
        self.permission_broker = permission_broker or PermissionBroker()

    def route(
        self,
        task: TaskRequest,
        policy: ProcessingPolicy,
        hardware: HardwareProfile,
    ) -> RoutingDecision:
        permission = self.permission_broker.evaluate(task.required_tools)
        confirmation_required = permission.decision == PermissionDecision.REQUIRE_CONFIRMATION

        if permission.decision == PermissionDecision.DENY:
            return RoutingDecision(
                ReasoningLocation.UNAVAILABLE,
                ExecutionLocation.NONE,
                False,
                False,
                "; ".join(permission.reasons),
            )

        execution = (
            ExecutionLocation.AUTHORIZED_REMOTE
            if task.authorized_remote_target
            else ExecutionLocation.LOCAL
            if task.required_tools
            else ExecutionLocation.NONE
        )

        # Raw secrets never enter model prompts, regardless of selected policy.
        if task.sensitivity == DataSensitivity.SECRET:
            location = (
                ReasoningLocation.LOCAL
                if hardware.local_model_available
                else ReasoningLocation.UNAVAILABLE
            )
            return RoutingDecision(
                location,
                execution if location != ReasoningLocation.UNAVAILABLE else ExecutionLocation.NONE,
                confirmation_required,
                False,
                "Secret data cannot be sent to an AI provider; use filtered local processing only.",
            )

        if policy == ProcessingPolicy.LOCAL_ONLY:
            location = self._local_or_unavailable(hardware)
            return self._decision(
                location,
                execution,
                confirmation_required,
                False,
                "Local Only policy selected.",
            )

        if policy == ProcessingPolicy.ASK_EVERY_TIME:
            if hardware.cloud_provider_available:
                return self._decision(
                    ReasoningLocation.ASK,
                    execution,
                    True,
                    task.sensitivity in (DataSensitivity.PUBLIC, DataSensitivity.PERSONAL),
                    "User selection is required before choosing local or cloud reasoning.",
                )
            return self._decision(
                self._local_or_unavailable(hardware),
                execution,
                confirmation_required,
                False,
                "No cloud provider is configured; local processing selected.",
            )

        # Confidential context remains local unless a separate per-task override is added.
        if task.sensitivity == DataSensitivity.CONFIDENTIAL:
            location = self._local_or_unavailable(hardware)
            return self._decision(
                location,
                execution,
                confirmation_required,
                False,
                "Confidential context remains local under the baseline policy.",
            )

        if policy == ProcessingPolicy.CLOUD_PREFERRED and hardware.cloud_provider_available:
            return self._decision(
                ReasoningLocation.CLOUD,
                execution,
                confirmation_required,
                True,
                "Cloud Preferred policy selected for non-confidential context.",
            )

        # Auto — Privacy First
        cloud_would_help = task.internet_required or task.complexity == Complexity.HIGH
        if (
            task.sensitivity == DataSensitivity.PUBLIC
            and cloud_would_help
            and hardware.cloud_provider_available
        ):
            return self._decision(
                ReasoningLocation.CLOUD,
                execution,
                confirmation_required,
                True,
                "Public high-complexity or online task routed to cloud reasoning.",
            )

        if (
            task.sensitivity == DataSensitivity.PERSONAL
            and task.complexity == Complexity.HIGH
            and hardware.cloud_provider_available
            and not hardware.local_high_complexity_capable
        ):
            return self._decision(
                ReasoningLocation.ASK,
                execution,
                True,
                True,
                "Cloud reasoning may help, but personal context requires user approval.",
            )

        location = self._local_or_cloud_fallback(hardware, task.sensitivity)
        return self._decision(
            location,
            execution,
            confirmation_required,
            location == ReasoningLocation.CLOUD,
            "Privacy-first routing selected the best available permitted provider.",
        )

    @staticmethod
    def _local_or_unavailable(hardware: HardwareProfile) -> ReasoningLocation:
        return (
            ReasoningLocation.LOCAL
            if hardware.local_model_available
            else ReasoningLocation.UNAVAILABLE
        )

    @staticmethod
    def _local_or_cloud_fallback(
        hardware: HardwareProfile,
        sensitivity: DataSensitivity,
    ) -> ReasoningLocation:
        if hardware.local_model_available:
            return ReasoningLocation.LOCAL
        if hardware.cloud_provider_available and sensitivity == DataSensitivity.PUBLIC:
            return ReasoningLocation.CLOUD
        return ReasoningLocation.UNAVAILABLE

    @staticmethod
    def _decision(
        location: ReasoningLocation,
        execution: ExecutionLocation,
        confirmation: bool,
        cloud_allowed: bool,
        explanation: str,
    ) -> RoutingDecision:
        if location == ReasoningLocation.UNAVAILABLE:
            execution = ExecutionLocation.NONE
            explanation += " No permitted provider is available."
        return RoutingDecision(location, execution, confirmation, cloud_allowed, explanation)
