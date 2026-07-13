# Astil Codex

**Astil Codex** is an original live 3D AI desktop assistant for Windows. The project combines a VRM character, real-time conversation, privacy-first local/cloud AI routing, everyday assistance, permission-controlled coding tools, Blender integration, and a future AI/ML laboratory.

> **Status:** Pre-alpha. The .NET core, persistent local memory, versioned IPC host, automated tests, and interactive UI concept are operational. The Unity/VRM Windows client is not yet implemented.

## Design principles

- **Original identity:** Original character, model, personality, and properly licensed voice.
- **Privacy first:** Personal data remains local by default.
- **Visible agency:** Astil Codex shows its plan, requested permissions, and current actions.
- **Local execution:** Consequential operations run through controlled local tools.
- **Human approval:** Sensitive, destructive, external, and paid actions always require confirmation.
- **Provider neutrality:** Local and cloud AI providers are replaceable adapters.
- **Reversibility:** Code and file changes use diffs, snapshots, Git, or another rollback mechanism.

## Planned modes

- **Companion:** Conversation and character interaction
- **Assistant:** Reminders, documents, files, calendar, and everyday tasks
- **Focus:** Concise responses and reduced distractions
- **Developer:** Coding, Git, builds, tests, and debugging in approved workspaces
- **Creator:** Blender-based 3D Studio and a later AI Laboratory

## Repository map

```text
astil-codex/
├── contracts/                 Versioned JSON contracts
├── docs/                      Architecture and security documentation
├── prototypes/
│   ├── core_simulator/        Testable task-routing reference prototype
│   └── ui-prototype/          Clickable interface concept
├── src/                       Production .NET core, memory, IPC, and future Unity source
├── tests/                     Executable production-core integration tests
├── PROJECT_SPEC.md            Approved product specification
├── SECURITY.md
└── CONTRIBUTING.md
```

## Run the production core

Requires the .NET 8 SDK.

```powershell
dotnet restore AstilCodex.sln
dotnet build AstilCodex.sln --configuration Release --no-restore
dotnet run --project tests/AstilCodex.Core.SelfTest --configuration Release --no-build
dotnet run --project src/AstilCodex.Core.Cli
```

The CLI provides offline streaming conversation through a mock provider. It demonstrates modes, privacy routing, permission decisions, persistent local history, cancellation, and avatar-state events. Use `/history` to inspect its session and `/memory clear` to remove local conversation data.

Run the named-pipe host for a future Unity client:

```powershell
dotnet run --project src/AstilCodex.Core.Host
```

Neither executable can access files, execute commands, contact AI services, or use a microphone. See [docs/development/CORE_FOUNDATION.md](docs/development/CORE_FOUNDATION.md) and [docs/development/LOCAL_MEMORY_AND_IPC.md](docs/development/LOCAL_MEMORY_AND_IPC.md).

## Run the Python reference simulator

Requires Python 3.11 or newer and has no third-party dependencies.

```bash
python -m unittest discover -s prototypes/core_simulator/tests -v
python prototypes/core_simulator/demo.py
```

The Python simulator remains a cross-check for deterministic routing, permissions, and local storage rules.

## Planned production stack

- Unity/C# desktop and VRM avatar client
- .NET AI core service
- SQLite local data store
- Windows Credential Manager or DPAPI-backed secret storage
- Local and cloud AI provider adapters
- Controlled Blender add-on and Python worker
- Isolated AI/ML project environments

See [PROJECT_SPEC.md](PROJECT_SPEC.md) for the roadmap and acceptance criteria. Runtime dependencies and their licenses are listed in [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).

## Security

Never commit API keys, passwords, private keys, tokens, private user data, or production credentials. See [SECURITY.md](SECURITY.md).

## Licensing

No open-source license has been selected yet. Unless a license is added, no permission is granted to copy, redistribute, or modify the source or original character assets. Code and character/voice assets will be licensed separately if the project is opened to contributors or public distribution.
