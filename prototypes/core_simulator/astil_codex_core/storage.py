from __future__ import annotations

import json
import sqlite3
from pathlib import Path
from typing import Any


SCHEMA_VERSION = 1


class LocalStore:
    def __init__(self, path: str | Path = ":memory:") -> None:
        self.connection = sqlite3.connect(path)
        self.connection.row_factory = sqlite3.Row
        self._migrate()

    def _migrate(self) -> None:
        with self.connection:
            self.connection.executescript(
                """
                CREATE TABLE IF NOT EXISTS schema_info (
                    version INTEGER NOT NULL
                );

                CREATE TABLE IF NOT EXISTS conversations (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    session_id TEXT NOT NULL,
                    role TEXT NOT NULL CHECK(role IN ('user', 'assistant', 'system', 'tool')),
                    content TEXT NOT NULL,
                    created_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
                );

                CREATE TABLE IF NOT EXISTS task_events (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    task_id TEXT NOT NULL,
                    event_type TEXT NOT NULL,
                    payload_json TEXT NOT NULL,
                    created_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
                );
                """
            )
            count = self.connection.execute("SELECT COUNT(*) FROM schema_info").fetchone()[0]
            if count == 0:
                self.connection.execute(
                    "INSERT INTO schema_info(version) VALUES (?)", (SCHEMA_VERSION,)
                )

    def add_message(self, session_id: str, role: str, content: str) -> int:
        if role not in {"user", "assistant", "system", "tool"}:
            raise ValueError("unsupported conversation role")
        with self.connection:
            cursor = self.connection.execute(
                "INSERT INTO conversations(session_id, role, content) VALUES (?, ?, ?)",
                (session_id, role, content),
            )
        return int(cursor.lastrowid)

    def add_task_event(self, task_id: str, event_type: str, payload: dict[str, Any]) -> int:
        with self.connection:
            cursor = self.connection.execute(
                "INSERT INTO task_events(task_id, event_type, payload_json) VALUES (?, ?, ?)",
                (task_id, event_type, json.dumps(payload, sort_keys=True)),
            )
        return int(cursor.lastrowid)

    def messages(self, session_id: str) -> list[dict[str, Any]]:
        rows = self.connection.execute(
            "SELECT role, content, created_at FROM conversations WHERE session_id = ? ORDER BY id",
            (session_id,),
        ).fetchall()
        return [dict(row) for row in rows]

    def close(self) -> None:
        self.connection.close()
