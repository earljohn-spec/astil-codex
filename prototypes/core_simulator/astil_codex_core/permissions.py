from __future__ import annotations

from dataclasses import dataclass
from enum import StrEnum
from typing import Iterable


class PermissionDecision(StrEnum):
    ALLOW = "allow"
    REQUIRE_CONFIRMATION = "require_confirmation"
    DENY = "deny"


@dataclass(frozen=True, slots=True)
class PermissionResult:
    decision: PermissionDecision
    reasons: tuple[str, ...]


class PermissionBroker:
    """Deterministic reference rules; production rules will use scoped capabilities."""

    ALWAYS_CONFIRM_PREFIXES = (
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
        "cloud.start_paid_compute",
    )

    DENIED_PREFIXES = (
        "credentials.reveal",
        "security.disable_audit",
        "permissions.self_grant",
    )

    PLAN_CONFIRM_PREFIXES = (
        "files.write",
        "git.commit",
        "terminal.workspace",
        "blender.execute",
        "ml.train",
    )

    def evaluate(self, tools: Iterable[str]) -> PermissionResult:
        requested = tuple(sorted(set(tools)))
        denied = self._matching(requested, self.DENIED_PREFIXES)
        if denied:
            return PermissionResult(
                PermissionDecision.DENY,
                tuple(f"Denied capability: {name}" for name in denied),
            )

        immediate = self._matching(requested, self.ALWAYS_CONFIRM_PREFIXES)
        if immediate:
            return PermissionResult(
                PermissionDecision.REQUIRE_CONFIRMATION,
                tuple(f"Sensitive action requires confirmation: {name}" for name in immediate),
            )

        planned = self._matching(requested, self.PLAN_CONFIRM_PREFIXES)
        if planned:
            return PermissionResult(
                PermissionDecision.REQUIRE_CONFIRMATION,
                tuple(f"Plan approval required: {name}" for name in planned),
            )

        return PermissionResult(PermissionDecision.ALLOW, ("No elevated capability requested.",))

    @staticmethod
    def _matching(requested: tuple[str, ...], prefixes: tuple[str, ...]) -> tuple[str, ...]:
        return tuple(
            name
            for name in requested
            if any(name == prefix or name.startswith(prefix + ".") for prefix in prefixes)
        )
