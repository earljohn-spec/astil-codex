# Astil Codex

> An original live 3D AI desktop assistant for Windows—designed to converse, organize, code, create, and act through explicit user-approved tools.

[![Foundation checks](https://github.com/earljohn-spec/astil-codex/actions/workflows/core-simulator.yml/badge.svg)](https://github.com/earljohn-spec/astil-codex/actions/workflows/core-simulator.yml)
![Status](https://img.shields.io/badge/status-pre--alpha-8b5cf6)
![Platform](https://img.shields.io/badge/platform-Windows-3b82f6)
![Unity](https://img.shields.io/badge/Unity-6000.3.19f1-111827)
![.NET](https://img.shields.io/badge/.NET-8.0-512bd4)

Astil Codex combines an original animated avatar, a local .NET agent core, persistent SQLite memory, and a Unity desktop interface. Its long-term purpose is to provide one character-driven assistant for everyday tasks, software development, Blender workflows, and eventually controlled AI/ML experiments.

The project prioritizes privacy, visible plans, narrow permissions, reversible actions, and clear user control. It does **not** silently grant an AI unrestricted access to the computer.

## Current status

Astil Codex is a working **pre-alpha foundation**. The first standalone Windows development build has been compiled and tested successfully.

### Working now

- Standalone Windows Unity application
- Animated procedural Astil placeholder and Codex Shards
- Runtime loading of a properly licensed local VRM model
- Companion, Assistant, Focus, Developer, and Creator mode controls
- Privacy-policy selection
- Local .NET 8 core host
- Current-user named-pipe IPC with versioned messages
- Streamed chat text and avatar-state events
- Emergency task cancellation
- Persistent SQLite conversation history
- Build-safe custom placeholder shader
- OpenAI-compatible local/cloud streaming provider adapter
- Native Anthropic Claude Messages API streaming adapter
- Windows DPAPI credential storage and provider setup utility
- Provider health/model checks and mock fallback
- Automated routing, permission, memory, IPC, provider, and cancellation tests
- GitHub Actions foundation validation

### Not connected yet

- A configured real model by default—provider adapters exist, but a user profile and compatible service/model are still required
- In-application provider settings UI—the current setup utility is a local console application
- Speech recognition, text-to-speech, lip sync, or wake word
- Final original Astil VRM character and expression mapping
- File, Git, terminal, calendar, email, or Blender tools
- Transparent always-on-top desktop-companion window
- Production installer, code signing, updater, or encrypted memory

Without an eligible configured profile, Astil Codex falls back to the offline mock provider. Configured providers receive only policy-approved conversation context; neither mock nor real providers receive direct tool authority.

## Architecture

```text
┌─────────────────────────────────────────────────────────┐
│ Unity 6.3 Windows Client                                │
│ Avatar · Chat · Modes · Privacy · Approvals · Stop      │
└──────────────────────────┬──────────────────────────────┘
                           │ named pipe: astil-codex-core-v1
┌──────────────────────────▼──────────────────────────────┐
│ .NET 8 Core Host                                        │
│ Conversation · Routing · Permissions · Cancellation     │
├──────────────────────┬───────────────────┬──────────────┤
│ Provider adapters    │ SQLite memory     │ Tool registry │
│ Mock + compatible AI │ Local persistence │ Planned       │
└──────────────────────┴───────────────────┴──────────────┘
```

Reasoning and execution are separated. Model output is treated as untrusted input until deterministic policy and permission checks approve an operation.

## Assistant modes

| Mode | Intended behavior |
|---|---|
| Companion | Conversation, check-ins, and expressive character interaction |
| Assistant | Planning, reminders, documents, and everyday organization |
| Focus | Concise responses and reduced interruptions |
| Developer | Code analysis, diffs, builds, tests, and approved workspace tools |
| Creator | Blender-based 3D workflows and future AI/ML projects |

The interface exposes all five modes now; their advanced tool capabilities remain roadmap work.

## Requirements

### Core development

- Windows 10/11
- Git
- .NET 8 SDK
- Python 3.11 or newer for reference validators

### Unity client

- Unity Hub
- Unity `6000.3.19f1`
- Windows Build Support; IL2CPP is recommended for future release builds
- Git access for Unity Package Manager
- UniVRM `v0.131.1` is pinned in `Packages/manifest.json`
- Visual Studio Community with **Game development with Unity** is recommended, not required to run the Editor

## Quick start: .NET foundation

```powershell
git clone https://github.com/earljohn-spec/astil-codex.git
cd astil-codex

dotnet restore AstilCodex.sln
dotnet build AstilCodex.sln --configuration Release --no-restore
dotnet run --project tests/AstilCodex.Core.SelfTest --configuration Release --no-build
dotnet run --project src/AstilCodex.Core.Cli --configuration Release --no-build
```

Useful CLI commands:

```text
/mode companion|assistant|focus|developer|creator
/policy auto|local|cloud|ask
/history
/memory clear
/quit
```

## Configure a real AI provider

Astil Codex supports OpenAI-compatible local/cloud endpoints and the native Anthropic Claude Messages API. On Windows, run:

```powershell
dotnet run --project src/AstilCodex.ProviderSetup.Cli --configuration Release
```

The utility can configure a loopback OpenAI-compatible local model, an OpenAI-compatible HTTPS cloud service, or native Anthropic Claude. It stores non-secret settings under `%LOCALAPPDATA%\AstilCodex\config` and encrypts API keys separately with current-user Windows DPAPI. Remote endpoints must use HTTPS; plain HTTP is allowed only for loopback local-model servers. Restart the core host after changing profiles.

No API key is required to build, test, or run Astil Codex in mock mode. Never paste credentials into source files, GitHub, logs, or chat messages.

See [Real AI provider foundation](docs/development/PROVIDER_INTEGRATION.md).

## Quick start: Unity client

Build the core host first:

```powershell
dotnet build src/AstilCodex.Core.Host/AstilCodex.Core.Host.csproj --configuration Release
```

Then:

1. Add `src/AstilCodex.UnityClient` to Unity Hub.
2. Open it with Unity `6000.3.19f1`.
3. Open `Assets/AstilCodex/Scenes/Main.unity`.
4. Enter Play Mode.
5. Select **Connect Core**.
6. Send a message to verify streamed mock chat.

If the main scene is missing, use **Astil Codex → Create or Refresh Main Scene**.

## Build the Windows application

In Unity, stop Play Mode and select:

```text
Astil Codex → Build Windows Development Client
```

The generated application is written to:

```text
src/AstilCodex.UnityClient/Builds/Windows/
├── AstilCodex.exe
├── AstilCodex_Data/
└── Core/
```

The build helper copies the Release .NET core host and SQLite dependencies into `Core`. Generated builds are intentionally excluded from Git.

## VRM avatar testing

No third-party character model is bundled. Place a VRM model you have the right to use at:

```text
%LOCALAPPDATA%\AstilCodex\avatars\astil.vrm
```

Then select **Load default VRM** in the application. Imported `.vrm` files are limited to 256 MiB and remain outside the repository by default.

## Local data

Conversation memory is stored at:

```text
%LOCALAPPDATA%\AstilCodex\data\astil-codex.db
```

The current database is local but not encrypted at rest. Never store passwords, access tokens, private keys, or raw credentials in conversation memory.

## Reclaim development disk space

The tracked repository is intentionally small—approximately 1–2 MiB without generated caches. Most local space is consumed by Unity `Library`, Windows builds, and .NET `bin`/`obj` directories.

Preview safe cleanup candidates:

```powershell
.\scripts\clean-development.ps1
```

Delete standard generated outputs:

```powershell
.\scripts\clean-development.ps1 -Apply
```

Include Unity `Library` for maximum recovery:

```powershell
.\scripts\clean-development.ps1 -Deep -Apply
```

Close Unity, Astil Codex, and the core host before applying cleanup. `-Deep` forces a full Unity package and asset reimport on the next launch. The script never touches source, Git history, `ProjectSettings`, `Packages`, local conversation memory, or avatars.

See [Development storage and cleanup](docs/development/STORAGE_AND_CLEANUP.md).

## Repository layout

```text
astil-codex/
├── contracts/                         JSON contract schemas and examples
├── docs/                              Architecture and development guides
├── prototypes/
│   ├── core_simulator/                Dependency-free Python policy reference
│   └── ui-prototype/                  Offline clickable interface concept
├── scripts/                           Development maintenance tools
├── src/
│   ├── AstilCodex.Contracts/          Shared .NET contracts
│   ├── AstilCodex.Core/               Routing, permissions, and orchestration
│   ├── AstilCodex.Core.Cli/           Offline development shell
│   ├── AstilCodex.Core.Host/          IPC host process
│   ├── AstilCodex.Ipc/                Named-pipe protocol and transport
│   ├── AstilCodex.Memory/             SQLite persistence
│   ├── AstilCodex.Providers/          Compatible AI providers and DPAPI secrets
│   ├── AstilCodex.ProviderSetup.Cli/  Local provider configuration utility
│   └── AstilCodex.UnityClient/        Unity/VRM Windows application
├── tests/AstilCodex.Core.SelfTest/    Executable production-core tests
├── PROJECT_SPEC.md                    Approved product specification
├── SECURITY.md                        Security boundaries and reporting
└── THIRD_PARTY_NOTICES.md             Dependency and license notices
```

## Roadmap

1. Unity provider-settings panel, test connection, and profile reload
2. Provider usage limits, context preview, and cost/telemetry controls
3. Original Astil character bible and final VRM production
4. Speech recognition, licensed TTS, lip sync, and interruption
5. Low-risk everyday tools and approval UI
6. Developer workspace, Git, build, and test tools
7. Blender 3D Studio connector
8. AI Laboratory with isolated environments
9. Transparent companion window, installer, signing, and updates

Detailed plans are in [PROJECT_SPEC.md](PROJECT_SPEC.md) and the [development documentation](docs/development/).

## Security and contribution

- Never commit API keys, passwords, tokens, private data, or unlicensed assets.
- Imported avatars and voices require clear usage and redistribution rights.
- Sensitive, external, destructive, and paid actions require explicit approval.
- See [SECURITY.md](SECURITY.md) and [CONTRIBUTING.md](CONTRIBUTING.md).

## Licensing

No open-source license has been selected yet. Unless a license is added, no permission is granted to copy, redistribute, or modify Astil Codex source code or original character assets. Code, model, voice, music, and third-party dependencies are evaluated separately. See [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).
