# Astil Codex Unity Client

Unity/VRM desktop-client foundation for Astil Codex.

## Required software

- Unity Hub
- Unity `6000.3.18f1` (Unity 6.3 LTS) with Windows Build Support (IL2CPP is optional for this milestone)
- Git, used by Unity Package Manager to retrieve UniVRM
- .NET 8 SDK, used to build the local core host

## First setup

From the repository root, build the core host:

```powershell
dotnet build src/AstilCodex.Core.Host/AstilCodex.Core.Host.csproj --configuration Release
```

In Unity Hub:

1. Select **Add > Add project from disk**.
2. Choose `src/AstilCodex.UnityClient`.
3. Open it with Unity `6000.3.18f1` or a newer Unity 6.3 LTS patch.
4. Wait for Package Manager and script compilation to finish.
5. Select **Astil Codex > Create or Refresh Main Scene**.
6. Enter Play Mode.
7. Click **Connect Core**.

The Connect button attempts to start the Release core-host build and then connects to the named pipe `astil-codex-core-v1`.

## VRM avatar

The client includes a procedural synthetic placeholder so it can run without copyrighted or unlicensed assets. To test a VRM model, place a model you have the right to use at:

```text
%LOCALAPPDATA%\AstilCodex\avatars\astil.vrm
```

Then click **Load default VRM**. The project uses UniVRM `v0.131.1` and supports VRM 1.0 plus permitted VRM 0.x migration.

Do not commit user-imported or third-party VRM files unless redistribution rights are documented.

## Windows development build

1. Build the .NET core host in Release mode.
2. In Unity, select **Astil Codex > Build Windows Development Client**.
3. The output is written to `Builds/Windows` inside the Unity project.
4. The build script copies the local core host into the build's `Core` folder.

Generated builds are excluded from Git.

## Implemented

- Runtime-created desktop interface
- Companion, Assistant, Focus, Developer, and Creator controls
- Privacy-policy selector
- Core-host process launcher for Editor and Windows builds
- Versioned named-pipe client
- Health requests
- Streamed chat text
- Avatar-state handling
- Error and completion handling
- Emergency task cancellation
- Procedural animated placeholder avatar
- Runtime VRM loading from a known local path
- Windows development-build helper

## Limitations

- No final original Astil VRM asset is bundled.
- Loaded VRM expressions are not yet mapped to avatar-state events.
- The window is not transparent or always-on-top yet.
- Speech recognition, text-to-speech, and lip sync are not connected.
- The core still uses an offline mock chat provider.
- Rapid repeated task cancellation on one long-lived IPC connection needs a dedicated soak-test milestone.
