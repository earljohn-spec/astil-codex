from pathlib import Path
import sys
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from astil_codex_core.permissions import (  # noqa: E402
    PermissionBroker,
    PermissionDecision,
)


class PermissionBrokerTests(unittest.TestCase):
    def setUp(self) -> None:
        self.broker = PermissionBroker()

    def test_read_only_tool_is_allowed(self) -> None:
        result = self.broker.evaluate(("files.read",))
        self.assertEqual(PermissionDecision.ALLOW, result.decision)

    def test_workspace_command_requires_plan_confirmation(self) -> None:
        result = self.broker.evaluate(("terminal.workspace",))
        self.assertEqual(PermissionDecision.REQUIRE_CONFIRMATION, result.decision)

    def test_external_send_requires_confirmation(self) -> None:
        result = self.broker.evaluate(("email.send",))
        self.assertEqual(PermissionDecision.REQUIRE_CONFIRMATION, result.decision)

    def test_self_grant_is_denied(self) -> None:
        result = self.broker.evaluate(("permissions.self_grant",))
        self.assertEqual(PermissionDecision.DENY, result.decision)


if __name__ == "__main__":
    unittest.main()
