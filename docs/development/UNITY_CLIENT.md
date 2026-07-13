# Unity/VRM Client Foundation

## Version baseline

- Unity: `6000.3.18f1` from the Unity 6.3 LTS line
- UniVRM: `v0.131.1`
- Target: Windows x64
- UI: runtime-generated uGUI
- Core transport: `astil-codex-core-v1` named pipe, contract `1.0`

Unity 6.3 LTS is used to keep the desktop client on a supported production line. UniVRM is pinned by Git tag in `Packages/manifest.json` for reproducibility.

## Runtime flow

```text
AstilRuntimeBootstrap
├── creates camera, lighting, and placeholder avatar
├── adds AstilIpcClient
├── adds CoreHostLauncher
└── adds AstilAppController
      ├── creates desktop UI
      ├── selects mode and privacy policy
      ├── starts/connects to the .NET core
      ├── streams chat output
      ├── maps avatar-state events
      └── sends emergency cancellation
```

## IPC behavior

The Unity client reproduces the core's length-prefixed protocol:

1. Serialize a camel-case JSON envelope.
2. Encode it as UTF-8.
3. Write a four-byte little-endian payload length.
4. Write the JSON payload.
5. Read and dispatch correlated response envelopes on a background task.
6. Queue Unity object and UI updates to the main thread.

The client rejects frames over 4 MiB and reports contract-version mismatches.

## Avatar behavior

The procedural placeholder changes color and motion for:

- ready
- listening
- thinking
- speaking
- acting
- success
- error
- cancelled

`VrmAvatarLoader` loads a local VRM through UniVRM. A loaded model replaces the visible placeholder. Expression, look-at, gesture, and lip-sync mappings are deferred until the final character rig is defined.

## Asset policy

No third-party character is included. The project must ship only original, commissioned, public-domain, or properly licensed models. User-imported files remain outside the repository by default.

## Build flow

The Unity editor build helper creates a Windows development build and copies the already-built .NET core host beside it. A production installer, code signing, auto-update system, and single-instance process management remain deferred.
