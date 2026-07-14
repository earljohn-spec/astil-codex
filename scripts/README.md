# Development Scripts

## `clean-development.ps1`

Reports or removes generated development output without touching source code, Git history, Unity project settings, local conversation memory, or avatars.

```powershell
# Preview
.\scripts\clean-development.ps1

# Standard generated output
.\scripts\clean-development.ps1 -Apply

# Preserve Windows builds
.\scripts\clean-development.ps1 -Apply -KeepBuilds

# Maximum project-local recovery; forces a full Unity reimport
.\scripts\clean-development.ps1 -Deep -Apply
```

Close Unity, the standalone client, and the core host before applying cleanup. See [`docs/development/STORAGE_AND_CLEANUP.md`](../docs/development/STORAGE_AND_CLEANUP.md).

## `validate_repository.py`

Checks that generated output, local databases, crash files, and unexpectedly large files have not entered source control. GitHub Actions runs it automatically.

```powershell
python .\scripts\validate_repository.py
```
