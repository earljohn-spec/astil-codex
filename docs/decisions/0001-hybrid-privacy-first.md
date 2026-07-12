# ADR-0001: Hybrid, Privacy-First Processing

- **Status:** Accepted
- **Date:** 2026-07-12

## Decision

Astil Codex will support local and cloud AI providers. The default policy is **Auto — Privacy First**. Local processing is preferred for personal data, files, memory, credentials, and tool execution. Cloud reasoning may be selected for public or sufficiently minimized context when it materially improves task quality.

Users can override the default with Local Only, Cloud Preferred, or Ask Every Time policies.

## Consequences

- Provider contracts must be vendor-neutral.
- Tasks require sensitivity classification before provider selection.
- Cloud transmissions need visible disclosure.
- Secrets must never enter prompts.
- A task may use cloud reasoning while retaining local execution.
- Offline fallback quality depends on available hardware and installed local models.
