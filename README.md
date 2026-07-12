# Astil Codex

**Astil Codex** is an original live 3D AI desktop assistant for Windows. The project combines a VRM character, real-time conversation, privacy-first local/cloud AI routing, everyday assistance, permission-controlled coding tools, Blender integration, and a future AI/ML laboratory.

> **Status:** Pre-alpha. Architecture and foundation prototype are under development. There is no production Windows build yet.

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
├── src/                       Future production .NET and Unity source
├── tests/                     Cross-component tests (planned)
├── PROJECT_SPEC.md            Approved product specification
├── SECURITY.md
└── CONTRIBUTING.md
```

## Try the core simulator

Requires Python 3.11 or newer and has no third-party dependencies.

```bash
python -m unittest discover -s prototypes/core_simulator/tests -v
python prototypes/core_simulator/demo.py
```

The simulator validates task classification, privacy-aware local/cloud routing, and permission decisions. It is a reference harness; the production orchestrator remains planned as a .NET service.

## Planned production stack

- Unity/C# desktop and VRM avatar client
- .NET AI core service
- SQLite local data store
- Windows Credential Manager or DPAPI-backed secret storage
- Local and cloud AI provider adapters
- Controlled Blender add-on and Python worker
- Isolated AI/ML project environments

See [PROJECT_SPEC.md](PROJECT_SPEC.md) for the roadmap and acceptance criteria.

## Security

Never commit API keys, passwords, private keys, tokens, private user data, or production credentials. See [SECURITY.md](SECURITY.md).

## Licensing

No open-source license has been selected yet. Unless a license is added, no permission is granted to copy, redistribute, or modify the source or original character assets. Code and character/voice assets will be licensed separately if the project is opened to contributors or public distribution.
