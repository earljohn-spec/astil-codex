# Local Memory and IPC Milestone

This milestone adds persistent SQLite conversation memory and a versioned named-pipe connection for the future Unity client.

## Local memory

`AstilCodex.Memory` provides `IConversationStore` and `SqliteConversationStore`.

Stored data:

- Session identifier
- Assistant mode
- User and assistant message text
- UTC creation and update timestamps
- Schema migration version

Default Windows location:

```text
%LOCALAPPDATA%\AstilCodex\data\astil-codex.db
```

Supported controls:

- Create or update a session
- Append a parameterized message
- Load bounded recent history
- Delete one session and its messages
- Prune sessions older than a cutoff
- Clear all conversation memory

The development CLI now reloads its previous session. Use `/history` to inspect recent messages and `/memory clear` to delete all local conversation memory.

### Security limitation

The current SQLite database is local but not encrypted at rest. It must not store passwords, tokens, private keys, or raw credentials. Database encryption and Windows-protected key management are deferred to a dedicated security milestone.

## IPC transport

`AstilCodex.Ipc` uses asynchronous named pipes and length-prefixed UTF-8 JSON frames.

- Pipe name: `astil-codex-core-v1`
- Contract version: `1.0`
- Maximum frame payload: 4 MiB
- Byte-stream framing: 4-byte little-endian payload length followed by JSON
- Windows server restriction: current user only
- Connection scope: one local UI client per host instance in this milestone

Supported messages:

```text
health.request      → health.response
chat.request        → chat.started
                    → avatar.state (zero or more)
                    → chat.chunk (zero or more)
                    → chat.completed

task.cancel         → task.cancelled
invalid request     → error
```

Every envelope includes a message ID, optional correlation ID, contract version, message type, timestamp, and typed JSON payload.

## Core host

Run the local host:

```powershell
dotnet run --project src/AstilCodex.Core.Host
```

It initializes local memory and waits for a client on the named pipe. The current host uses `MockChatProvider`; it does not contact an AI service or execute computer tools.

## Tests

The production self-test executable now verifies:

- Privacy routing and permission policy
- Streaming mock provider and avatar states
- SQLite persistence across store instances
- Session deletion and cascading message deletion
- IPC serialization and framing
- Named-pipe health request/response
- Streamed IPC chat with local persistence
- Cancellation of an active IPC chat

```powershell
dotnet run --project tests/AstilCodex.Core.SelfTest --configuration Release
```

## Next integration

The Unity client will connect as an IPC client and translate:

- `avatar.state` into animations
- `chat.chunk` into streamed text
- `chat.completed` into final task and permission details
- `error` into visible failures
- emergency stop into `task.cancel`
