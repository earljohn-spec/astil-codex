"""Fail CI when generated or unexpectedly large files enter source control."""

from __future__ import annotations

import subprocess
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
MAX_TRACKED_BYTES = 5 * 1024 * 1024
FORBIDDEN_PARTS = {
    "__pycache__",
    ".pytest_cache",
    ".mypy_cache",
    ".ruff_cache",
    ".vs",
    "bin",
    "build",
    "builds",
    "library",
    "logs",
    "node_modules",
    "obj",
    "temp",
    "usersettings",
}
FORBIDDEN_SUFFIXES = {
    ".db",
    ".db-shm",
    ".db-wal",
    ".dmp",
    ".sqlite",
    ".sqlite3",
    ".stacktrace",
}


def tracked_files() -> list[Path]:
    output = subprocess.check_output(
        ["git", "ls-files", "-z"],
        cwd=ROOT,
    )
    return [ROOT / item.decode("utf-8") for item in output.split(b"\0") if item]


def main() -> None:
    files = tracked_files()
    generated: list[str] = []
    oversized: list[str] = []
    total = 0

    for path in files:
        relative = path.relative_to(ROOT)
        lower_parts = {part.lower() for part in relative.parts[:-1]}
        suffix = "".join(path.suffixes[-2:]).lower() if len(path.suffixes) >= 2 else path.suffix.lower()

        if lower_parts.intersection(FORBIDDEN_PARTS) or suffix in FORBIDDEN_SUFFIXES:
            generated.append(relative.as_posix())

        size = path.stat().st_size
        total += size
        if size > MAX_TRACKED_BYTES:
            oversized.append(f"{relative.as_posix()} ({size:,} bytes)")

    if generated:
        raise SystemExit(
            "Generated/runtime files must not be tracked:\n- " + "\n- ".join(sorted(generated))
        )
    if oversized:
        raise SystemExit(
            "Tracked files over 5 MiB require explicit artifact/LFS review:\n- "
            + "\n- ".join(sorted(oversized))
        )

    readme = (ROOT / "README.md").read_text(encoding="utf-8")
    for marker in ("6000.3.19f1", ".NET 8", "MockChatProvider", "clean-development.ps1"):
        if marker not in readme:
            raise SystemExit(f"README is missing current project marker: {marker}")

    print(
        f"Repository hygiene valid: {len(files)} tracked files, "
        f"{total / (1024 * 1024):.2f} MiB total, no generated output"
    )


if __name__ == "__main__":
    main()
