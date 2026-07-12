from pathlib import Path
import sys
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from astil_codex_core.storage import LocalStore  # noqa: E402


class LocalStoreTests(unittest.TestCase):
    def test_messages_are_stored_in_order(self) -> None:
        store = LocalStore()
        self.addCleanup(store.close)
        store.add_message("session-1", "user", "Hello")
        store.add_message("session-1", "assistant", "Welcome")
        messages = store.messages("session-1")
        self.assertEqual(["Hello", "Welcome"], [item["content"] for item in messages])

    def test_invalid_role_is_rejected(self) -> None:
        store = LocalStore()
        self.addCleanup(store.close)
        with self.assertRaises(ValueError):
            store.add_message("session-1", "unknown", "content")


if __name__ == "__main__":
    unittest.main()
