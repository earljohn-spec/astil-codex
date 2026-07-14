# Development Storage and Cleanup

## Repository audit

A clean clone of Astil Codex currently contains approximately 139 tracked files and occupies roughly 1–2 MiB excluding Git metadata. No large generated binaries, Unity caches, downloaded AI models, user avatars, SQLite databases, or Windows builds are tracked.

Tracked files that should remain:

- Unity `Assets`, `Packages`, and `ProjectSettings`
- `packages-lock.json` files for reproducible dependencies
- Unity `.meta` files for stable asset GUIDs
- The HTML UI prototype as a design and interaction reference
- The Python simulator as an independent policy reference
- Documentation, schemas, tests, and third-party notices

Removing these would save very little space and would reduce reproducibility or project clarity.

## Main local space consumers

| Location | Safe to remove? | Consequence |
|---|---|---|
| `src/AstilCodex.UnityClient/Builds` | Yes | Rebuild the standalone application when needed |
| Unity `Library` | Yes, with `-Deep` | Full package resolution, shader compilation, and asset reimport |
| Unity `Temp`, `Obj`, `Logs` | Yes while Unity is closed | Regenerated automatically |
| .NET `bin` and `obj` | Yes | Rebuild with `dotnet build` |
| Python `__pycache__` | Yes | Regenerated on next run |
| Unity installation | No, if it is the active editor | Project requires Unity 6000.3.19f1 |
| Visual Studio workloads | Only through Visual Studio Installer | Removing C#/Unity/C++ workloads reduces development capability |
| `%LOCALAPPDATA%\AstilCodex\data` | Only by user choice | Deletes local conversation memory |
| `%LOCALAPPDATA%\AstilCodex\avatars` | Only by user choice | Deletes user-provided avatars |

Unity `Library` is usually the largest project-local directory. Keeping it makes the Editor open much faster; deleting it is appropriate when disk space is more important than import time.

## Cleanup script

Run from the repository root.

### Dry run

```powershell
.\scripts\clean-development.ps1
```

Lists standard generated outputs and their sizes without deleting anything.

### Standard cleanup

```powershell
.\scripts\clean-development.ps1 -Apply
```

Removes:

- Unity development builds
- Unity temporary output and logs
- .NET `bin` and `obj`
- Python caches

Keep standalone builds with:

```powershell
.\scripts\clean-development.ps1 -Apply -KeepBuilds
```

### Deep Unity cleanup

```powershell
.\scripts\clean-development.ps1 -Deep -Apply
```

Also removes Unity `Library` and `UserSettings`. The next Editor launch will take longer while Unity downloads/resolves packages and reimports assets.

## Safety rules

- Close Unity, `AstilCodex.exe`, and `astil-core-host.exe` before applying cleanup.
- The script refuses paths outside the repository.
- Source, Git history, `Assets`, `Packages`, `ProjectSettings`, memory, avatars, provider settings, and DPAPI credentials are excluded.
- Do not manually delete files under `.git`.
- Do not commit generated `Builds`, `Library`, `bin`, or `obj` folders.

## Global caches

Global NuGet and Unity caches can also consume space, but they are shared by other projects and are intentionally outside the cleanup script.

Inspect NuGet caches:

```powershell
dotnet nuget locals all --list
```

Clear all NuGet caches only when necessary:

```powershell
dotnet nuget locals all --clear
```

Use Unity Hub to remove obsolete Editor versions and modules. Keep Unity `6000.3.19f1` while it remains the Astil Codex project baseline.
