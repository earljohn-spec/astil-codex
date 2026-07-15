# Production Source

The .NET 8 production foundation is now present.

```text
src/
├── AstilCodex.Contracts/       Shared versioned messages and enums
├── AstilCodex.Core/            Routing, permissions, providers, orchestration
├── AstilCodex.Core.Cli/        Offline interactive shell with local history
├── AstilCodex.Core.Host/       Versioned named-pipe host for the UI client
├── AstilCodex.Ipc/             Framing, serialization, server, and client
├── AstilCodex.Memory/          SQLite conversation persistence
├── AstilCodex.Providers/       OpenAI-compatible + Anthropic streaming and secrets
├── AstilCodex.ProviderSetup.Cli/ Windows DPAPI provider setup utility
├── AstilCodex.Tools/           Permission-controlled tools (planned)
└── AstilCodex.UnityClient/     Unity 6.3/VRM desktop-client foundation
```

`AstilCodex.Core.Cli` and `AstilCodex.Core.Host` currently use a streaming mock provider and cannot access computer resources. The CLI persists conversation history locally; the host exposes health, chat streaming, avatar-state, completion, error, and cancellation messages over a current-user named pipe. Run the production self-test project before starting either executable.
