"""Dependency-free structural validation for the Unity client scaffold."""

from __future__ import annotations

import json
from pathlib import Path


ROOT = Path(__file__).resolve().parent
REQUIRED_FILES = {
    "ProjectSettings/ProjectVersion.txt",
    "Packages/manifest.json",
    "Assets/AstilCodex/Runtime/AstilRuntimeBootstrap.cs",
    "Assets/AstilCodex/Runtime/AstilAppController.cs",
    "Assets/AstilCodex/Runtime/Ipc/AstilIpcClient.cs",
    "Assets/AstilCodex/Runtime/Ipc/IpcProtocolModels.cs",
    "Assets/AstilCodex/Runtime/Avatar/PlaceholderAvatarController.cs",
    "Assets/AstilCodex/Runtime/Avatar/VrmAvatarLoader.cs",
    "Assets/AstilCodex/Runtime/UI/AstilUiFactory.cs",
    "Assets/AstilCodex/Editor/AstilProjectSetup.cs",
    "Assets/AstilCodex/Editor/AstilWindowsBuild.cs",
}


def main() -> None:
    missing = sorted(path for path in REQUIRED_FILES if not (ROOT / path).is_file())
    if missing:
        raise SystemExit("Missing Unity client files: " + ", ".join(missing))

    version = (ROOT / "ProjectSettings/ProjectVersion.txt").read_text(encoding="utf-8")
    if "6000.3.18f1" not in version:
        raise SystemExit("Unity project is not pinned to the approved 6000.3.18f1 baseline")

    manifest = json.loads((ROOT / "Packages/manifest.json").read_text(encoding="utf-8"))
    dependencies = manifest.get("dependencies", {})
    expected_git_dependencies = {
        "com.vrmc.gltf": "/Packages/UniGLTF#v0.131.1",
        "com.vrmc.vrm": "/Packages/VRM10#v0.131.1",
    }
    for package, suffix in expected_git_dependencies.items():
        value = dependencies.get(package, "")
        if not value.startswith("https://github.com/vrm-c/UniVRM.git?path=") or not value.endswith(suffix):
            raise SystemExit(f"Unexpected or unpinned {package} dependency: {value!r}")

    runtime_root = ROOT / "Assets/AstilCodex/Runtime"
    runtime_editor_references = []
    for source in runtime_root.rglob("*.cs"):
        text = source.read_text(encoding="utf-8")
        if "using UnityEditor" in text:
            runtime_editor_references.append(str(source.relative_to(ROOT)))
    if runtime_editor_references:
        raise SystemExit(
            "Runtime code contains UnityEditor dependencies: "
            + ", ".join(runtime_editor_references)
        )

    bundled_vrm = sorted(str(path.relative_to(ROOT)) for path in ROOT.rglob("*.vrm"))
    if bundled_vrm:
        raise SystemExit("Unreviewed VRM assets must not be committed: " + ", ".join(bundled_vrm))

    ipc_source = (runtime_root / "Ipc/IpcProtocolModels.cs").read_text(encoding="utf-8")
    for value in ("1.0", "astil-codex-core-v1", "4 * 1024 * 1024"):
        if value not in ipc_source:
            raise SystemExit(f"Unity IPC protocol is missing required value: {value}")

    sources = list((ROOT / "Assets/AstilCodex").rglob("*.cs"))
    print(
        f"Unity client scaffold valid: {len(sources)} C# files, "
        "pinned UniVRM dependencies, no bundled VRM assets"
    )


if __name__ == "__main__":
    main()
