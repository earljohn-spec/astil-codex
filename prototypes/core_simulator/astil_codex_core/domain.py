from __future__ import annotations

from dataclasses import dataclass, field
from enum import StrEnum


class ProcessingPolicy(StrEnum):
    AUTO_PRIVACY_FIRST = "auto_privacy_first"
    LOCAL_ONLY = "local_only"
    CLOUD_PREFERRED = "cloud_preferred"
    ASK_EVERY_TIME = "ask_every_time"


class Complexity(StrEnum):
    LOW = "low"
    MEDIUM = "medium"
    HIGH = "high"


class DataSensitivity(StrEnum):
    PUBLIC = "public"
    PERSONAL = "personal"
    CONFIDENTIAL = "confidential"
    SECRET = "secret"


class ReasoningLocation(StrEnum):
    LOCAL = "local"
    CLOUD = "cloud"
    ASK = "ask"
    UNAVAILABLE = "unavailable"


class ExecutionLocation(StrEnum):
    LOCAL = "local"
    AUTHORIZED_REMOTE = "authorized_remote"
    NONE = "none"


@dataclass(frozen=True, slots=True)
class HardwareProfile:
    local_model_available: bool = True
    cloud_provider_available: bool = False
    local_high_complexity_capable: bool = False


@dataclass(frozen=True, slots=True)
class TaskRequest:
    task_id: str
    task_type: str
    complexity: Complexity
    sensitivity: DataSensitivity
    internet_required: bool = False
    required_tools: tuple[str, ...] = field(default_factory=tuple)
    authorized_remote_target: bool = False

    def __post_init__(self) -> None:
        if not self.task_id.strip():
            raise ValueError("task_id must not be empty")
        if "." not in self.task_type:
            raise ValueError("task_type must use a namespaced value such as 'code.modify'")


@dataclass(frozen=True, slots=True)
class RoutingDecision:
    reasoning_location: ReasoningLocation
    execution_location: ExecutionLocation
    confirmation_required: bool
    cloud_context_allowed: bool
    explanation: str
