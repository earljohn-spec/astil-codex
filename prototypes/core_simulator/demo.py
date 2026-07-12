from __future__ import annotations

import json
from dataclasses import asdict
from pathlib import Path
import sys

sys.path.insert(0, str(Path(__file__).resolve().parent))

from astil_codex_core import (  # noqa: E402
    Complexity,
    DataSensitivity,
    HardwareProfile,
    ProcessingPolicy,
    TaskRequest,
    TaskRouter,
)


def main() -> None:
    router = TaskRouter()
    hardware = HardwareProfile(
        local_model_available=True,
        cloud_provider_available=True,
        local_high_complexity_capable=False,
    )

    tasks = [
        TaskRequest(
            "demo-1",
            "conversation.reply",
            Complexity.LOW,
            DataSensitivity.PERSONAL,
        ),
        TaskRequest(
            "demo-2",
            "research.web",
            Complexity.HIGH,
            DataSensitivity.PUBLIC,
            internet_required=True,
            required_tools=("web.search",),
        ),
        TaskRequest(
            "demo-3",
            "code.modify_project",
            Complexity.HIGH,
            DataSensitivity.CONFIDENTIAL,
            required_tools=("files.read", "files.write", "git", "terminal.workspace"),
        ),
    ]

    print("Astil Codex core routing simulator\n")
    for task in tasks:
        decision = router.route(task, ProcessingPolicy.AUTO_PRIVACY_FIRST, hardware)
        output = {
            "task": task.task_type,
            **{
                key: value.value if hasattr(value, "value") else value
                for key, value in asdict(decision).items()
            },
        }
        print(json.dumps(output, indent=2))


if __name__ == "__main__":
    main()
