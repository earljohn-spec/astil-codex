from pathlib import Path
import sys
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from astil_codex_core import (  # noqa: E402
    Complexity,
    DataSensitivity,
    HardwareProfile,
    ProcessingPolicy,
    TaskRequest,
    TaskRouter,
)
from astil_codex_core.domain import ExecutionLocation, ReasoningLocation  # noqa: E402


class TaskRouterTests(unittest.TestCase):
    def setUp(self) -> None:
        self.router = TaskRouter()
        self.hybrid = HardwareProfile(True, True, False)

    def test_public_high_complexity_task_uses_cloud_in_auto_mode(self) -> None:
        task = TaskRequest(
            "t1", "research.web", Complexity.HIGH, DataSensitivity.PUBLIC, True
        )
        result = self.router.route(task, ProcessingPolicy.AUTO_PRIVACY_FIRST, self.hybrid)
        self.assertEqual(ReasoningLocation.CLOUD, result.reasoning_location)
        self.assertTrue(result.cloud_context_allowed)

    def test_confidential_task_remains_local_even_when_cloud_preferred(self) -> None:
        task = TaskRequest(
            "t2",
            "code.modify_project",
            Complexity.HIGH,
            DataSensitivity.CONFIDENTIAL,
            required_tools=("files.read", "files.write"),
        )
        result = self.router.route(task, ProcessingPolicy.CLOUD_PREFERRED, self.hybrid)
        self.assertEqual(ReasoningLocation.LOCAL, result.reasoning_location)
        self.assertFalse(result.cloud_context_allowed)
        self.assertTrue(result.confirmation_required)

    def test_secret_cannot_fall_back_to_cloud(self) -> None:
        cloud_only = HardwareProfile(False, True, False)
        task = TaskRequest(
            "t3", "credentials.inspect", Complexity.LOW, DataSensitivity.SECRET
        )
        result = self.router.route(task, ProcessingPolicy.CLOUD_PREFERRED, cloud_only)
        self.assertEqual(ReasoningLocation.UNAVAILABLE, result.reasoning_location)
        self.assertFalse(result.cloud_context_allowed)
        self.assertEqual(ExecutionLocation.NONE, result.execution_location)

    def test_personal_high_complexity_task_asks_before_cloud(self) -> None:
        task = TaskRequest(
            "t4", "document.analyze", Complexity.HIGH, DataSensitivity.PERSONAL
        )
        result = self.router.route(task, ProcessingPolicy.AUTO_PRIVACY_FIRST, self.hybrid)
        self.assertEqual(ReasoningLocation.ASK, result.reasoning_location)
        self.assertTrue(result.confirmation_required)

    def test_denied_capability_blocks_task(self) -> None:
        task = TaskRequest(
            "t5",
            "security.change_policy",
            Complexity.LOW,
            DataSensitivity.PUBLIC,
            required_tools=("permissions.self_grant",),
        )
        result = self.router.route(task, ProcessingPolicy.LOCAL_ONLY, self.hybrid)
        self.assertEqual(ReasoningLocation.UNAVAILABLE, result.reasoning_location)
        self.assertEqual(ExecutionLocation.NONE, result.execution_location)


if __name__ == "__main__":
    unittest.main()
