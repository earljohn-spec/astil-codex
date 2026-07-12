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

## Reporting a vulnerability

Do not publish exploitable details in a public issue. Until a dedicated security contact is configured, use GitHub's private vulnerability reporting feature if it is enabled for the repository owner. Include the affected component, reproduction steps, impact, and any suggested mitigation.

## Supported versions

The project is pre-alpha and has no supported production release. Security behavior may change before the first release.
