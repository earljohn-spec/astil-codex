# Security Policy

Astil Codex is designed to operate on personal files, development projects, microphones, external accounts, and optional cloud AI services. Security boundaries are therefore product requirements, not optional polish.

## Never commit

- API keys or access tokens
- Passwords or private keys
- OAuth refresh tokens
- User conversations or private documents
- Real production configuration
- Credential exports
- Proprietary training datasets

Use redacted examples and `.env.example` files containing placeholder names only. Production secrets must use Windows Credential Manager or DPAPI-backed encrypted storage.

## Required execution rules

- Tools receive the minimum capability needed for one task.
- File access is restricted to user-approved roots.
- Cloud models never receive direct shell or desktop authority.
- Destructive, external, security-sensitive, and paid actions require immediate user confirmation.
- Commands and file modifications are logged.
- Supported modifications must have a rollback path.
- Secrets must be filtered before any model prompt is constructed.

## Local memory

Conversation memory is stored in a local SQLite database. The current pre-alpha database is not encrypted at rest and must never contain passwords, access tokens, private keys, or raw credentials. Users can clear all CLI memory with `/memory clear`; production UI controls will provide session-level deletion and retention settings.

## Local IPC

The current IPC host uses a versioned, length-prefixed named-pipe protocol. On Windows, the server requests current-user-only access. Frames are limited to 4 MiB, message types are allowlisted, contract versions are checked, and active chat requests can be cancelled. IPC payloads remain untrusted input and require typed deserialization and policy validation.

## Unity client and avatar imports

The Unity client launches only a core-host executable resolved from the known development/build location or the explicit `ASTIL_CODEX_CORE_HOST` environment variable, with shell execution disabled. Runtime avatar loading accepts only `.vrm` files from an explicit local path and rejects files over 256 MiB. Imported models remain untrusted content and must have documented usage rights before redistribution.

## Reporting a vulnerability

Do not publish exploitable details in a public issue. Until a dedicated security contact is configured, use GitHub's private vulnerability reporting feature if it is enabled for the repository owner. Include the affected component, reproduction steps, impact, and any suggested mitigation.

## Supported versions

The project is pre-alpha and has no supported production release. Security behavior may change before the first release.
