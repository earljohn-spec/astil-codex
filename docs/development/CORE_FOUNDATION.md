# Production Core Foundation

The first production-code milestone is a dependency-free .NET 8 foundation. It can compile and run before Unity, SQLite packages, AI credentials, microphones, or computer tools are introduced.

## Projects

```text
AstilCodex.sln
├── src/AstilCodex.Contracts       Versioned records and enums
├── src/AstilCodex.Core            Routing, permissions, providers, orchestration
├── src/AstilCodex.Core.Cli        Interactive offline development shell
└── tests/AstilCodex.Core.SelfTest Framework-free executable checks
```

## Build

Install the .NET 8 SDK, then run from the repository root:

```powershell
dotnet restore AstilCodex.sln
dotnet build AstilCodex.sln --configuration Release --no-restore
dotnet run --project tests/AstilCodex.Core.SelfTest --configuration Release --no-build
dotnet run --project src/AstilCodex.Core.Cli
```

## CLI commands

```text
/help
/mode companion|assistant|focus|developer|creator
/policy auto|local|cloud|ask
/quit
```

The CLI uses `MockChatProvider`. It never contacts a cloud provider or local AI runtime and cannot access files, execute commands, use a microphone, or modify Windows. Developer and Creator requests only produce task manifests.

## Current guarantees

- Raw secret-classified tasks cannot route to cloud reasoning.
- Confidential tasks remain local under the baseline policy.
- Model output is not an execution authority.
- Restricted capabilities such as self-granting permissions are denied.
- Workspace writes, commands, Blender execution, and other consequential operations require approval.
- Mock responses stream in chunks and emit avatar-state events for a future Unity client.

## Deliberately deferred

- SQLite production package and migrations
- Named-pipe IPC
- Real local/cloud providers
- Unity client
- Speech recognition and synthesis
- File, Git, terminal, and Blender tool workers

These are added only after the foundation compiles and its security rules pass on Windows and GitHub Actions.
