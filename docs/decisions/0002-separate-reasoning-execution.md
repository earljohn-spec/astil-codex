# ADR-0002: Separate Reasoning from Execution

- **Status:** Accepted
- **Date:** 2026-07-12

## Decision

Language and multimodal models propose responses, plans, and structured tool requests. A deterministic local policy layer validates those requests and grants narrowly scoped capabilities to local tool workers. Model output is never treated as authority by itself.

## Consequences

- Cloud providers cannot directly control the terminal or desktop.
- Tool arguments require schema and policy validation.
- Sensitive actions pause for user confirmation.
- The interface must display plans, actions, progress, and outcomes.
- Tool execution remains testable without a live AI provider.
