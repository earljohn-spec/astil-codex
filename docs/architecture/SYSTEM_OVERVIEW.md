# System Overview

## Runtime boundaries

Astil Codex is intentionally split into processes so the animated interface is not also the security boundary.

```text
Unity Client
  ↕ versioned local IPC
.NET Core Service
  ├─ Task router
  ├─ Permission broker
  ├─ Provider router
  ├─ Memory service
  └─ Tool registry
       ├─ Restricted file/Git/command tools
       ├─ Blender connector
       ├─ Authorized account connectors
       └─ Isolated AI/ML worker
```

## Trust model

- The **Unity client** presents plans, approvals, status, and results. It does not grant itself operating-system authority.
- The **core service** evaluates policy and issues short-lived capabilities to registered tools.
- **AI providers** propose text, plans, and structured calls. Provider output is untrusted until validated.
- **Tool workers** accept only schema-valid operations covered by a current capability.
- The **user** remains the authority for sensitive and consequential actions.

## Reasoning versus execution

Reasoning may be local or cloud-based. Execution remains local unless a task explicitly targets an authorized remote service or approved compute environment. Cloud model output never becomes a shell command without validation, policy evaluation, and any required user confirmation.

## Communication

Initial local IPC will use versioned JSON messages over named pipes. Each message includes:

- Contract version
- Correlation ID
- Timestamp
- Message type
- Task or session ID
- Payload

Long operations emit progress events and support cancellation. Contracts must be forward-compatible where practical and reject unknown security-sensitive values.

## Failure behavior

- Permission denial stops the relevant action without ending the conversation.
- Provider failure can fall back according to user policy.
- Tool crashes cannot grant broader access on restart.
- Interrupted file operations use staging, atomic replacement, or snapshots where possible.
- The UI must distinguish planning, waiting for approval, executing, completed, failed, and cancelled states.
