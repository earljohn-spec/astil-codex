# Contributing to Astil Codex

Astil Codex is currently in pre-alpha foundation development.

## Workflow

1. Create a branch from `main`.
2. Keep each change focused and reviewable.
3. Add or update tests for behavior changes.
4. Do not commit generated builds, credentials, private data, or unlicensed assets.
5. Submit a pull request describing behavior, risks, permissions, and test results.

Suggested branch names:

- `feature/task-router`
- `feature/developer-mode`
- `feature/avatar-client`
- `fix/permission-check`
- `docs/security-model`

Suggested commit prefixes:

- `feat:` new behavior
- `fix:` defect correction
- `docs:` documentation only
- `test:` test changes
- `refactor:` internal change without intentional behavior change
- `chore:` repository maintenance

## Security-sensitive changes

Changes involving file access, commands, credentials, cloud transmission, microphone input, account integrations, updates, or plugins must document their threat model and failure behavior. A friendly character response must never obscure a warning or permission request.

## Assets

Only submit assets that are original or properly licensed for the intended use. Record the creator, source, license, and allowed uses. Character, voice, music, font, animation, and model licenses are evaluated separately from source code.
