# Production Source

The production implementation will be added here after the versioned contracts and core simulator stabilize.

Planned components:

```text
src/
├── AstilCodex.Contracts/       Shared versioned messages
├── AstilCodex.Core/            Task router and orchestration
├── AstilCodex.Memory/          SQLite persistence and retention rules
├── AstilCodex.Providers/       Local/cloud AI, STT, TTS, and vision adapters
├── AstilCodex.Tools/           Permission-controlled tool registry
└── AstilCodex.UnityClient/     Unity project or documented Unity subtree
```

The production core is planned for .NET. A dependency-free Python simulator under `prototypes/core_simulator` validates rules and contracts in environments where the .NET and Unity toolchains are unavailable.
