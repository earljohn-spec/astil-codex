# Production Source

The .NET 8 production foundation is now present.

```text
src/
├── AstilCodex.Contracts/       Shared versioned messages and enums
├── AstilCodex.Core/            Routing, permissions, providers, orchestration
├── AstilCodex.Core.Cli/        Offline interactive development shell
├── AstilCodex.Memory/          SQLite persistence (next milestone)
├── AstilCodex.Providers/       Real AI/STT/TTS adapters (planned)
├── AstilCodex.Tools/           Permission-controlled tools (planned)
└── AstilCodex.UnityClient/     Unity/VRM client (planned)
```

`AstilCodex.Core.Cli` currently uses a streaming mock provider and cannot access computer resources. Run the production self-test project before starting the CLI. See `docs/development/CORE_FOUNDATION.md`.
