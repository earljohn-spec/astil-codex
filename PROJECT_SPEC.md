# Astil Codex — Original 3D AI Desktop Assistant

**Project name:** **Astil Codex**  
**Internal identifier / namespace:** `AstilCodex`  
**Project type:** Standalone Windows desktop application  
**Status:** Baseline architecture approved  
**Target platform:** Windows 11 first, with Windows 10 compatibility evaluated during packaging

---

## 1. Product Vision

Create an original live 3D AI character that acts as a conversational companion and a permission-controlled desktop agent. It should be able to help with ordinary tasks, software development, Blender-based 3D creation, and—later—AI/ML development.

The assistant must remain understandable and controllable: users can see what it plans to do, grant only the permissions needed, inspect changes, interrupt execution, and undo supported actions.

---

## 2. Locked Decisions

### Character and interface

- Original anime-style 3D character; no third-party character identity or cloned voice.
- VRM-compatible humanoid model with facial blendshapes, visemes, expressions, and full-body animation.
- Unity/C# desktop client.
- Transparent always-on-top companion mode plus a full task workspace window.
- Companion, Assistant, Focus, Developer, and Creator modes.

### AI operation

- Hybrid local/cloud architecture.
- Default policy: **Auto — Privacy First**.
- Local-only, Cloud-preferred, and Ask-every-time policies are also available.
- Speech, memory, file operations, credentials, tool execution, and permission enforcement remain local by default.
- Difficult reasoning can be escalated to a cloud model only under the active privacy policy.
- Consequential actions always execute through permission-controlled local tools.

### Creation capabilities

Both creation categories will be supported as separate modules:

1. **3D Studio** — Blender-based modeling, materials, scenes, animation assistance, and export.
2. **AI Laboratory** — isolated AI/ML projects for dataset preparation, experiments, training, evaluation, and export.

Development priority is 3D Studio first and AI Laboratory later.

---

## 3. Main User Modes

### Companion Mode

- Casual conversation
- Character reactions and emotional expression
- Optional user preferences and relationship memory
- No autonomous desktop actions

### Assistant Mode

- Reminders, alarms, to-do lists, and planning
- Document summaries
- Local file search
- Application launching
- Calendar and email integration through authorized accounts

### Focus Mode

- Minimal animation and personality
- Short, direct answers
- Reduced notifications
- Optional Pomodoro and work-session tracking

### Developer Mode

- Project analysis
- Code creation and editing
- Terminal commands in approved workspaces
- Git branches, diffs, tests, builds, and rollback
- Explicit approval for dependency installation and destructive operations

### Creator Mode

Creator Mode contains two independent workspaces:

- **3D Studio:** Blender automation and asset workflows
- **AI Laboratory:** Isolated machine-learning projects

---

## 4. High-Level Architecture

```text
┌────────────────────────────────────────────────────────┐
│                  Unity Desktop Client                  │
│                                                        │
│  VRM Avatar   Chat/Voice UI   Task Plan   Approvals    │
│  Animation    Workspace UI    Activity Log             │
└──────────────────────────┬─────────────────────────────┘
                           │ Local IPC
┌──────────────────────────▼─────────────────────────────┐
│                 .NET AI Core Service                   │
│                                                        │
│  Task Router       Conversation      Persona Layer     │
│  Permission Broker Memory            Provider Router   │
│  Tool Registry     Audit/Undo        Safety Policy     │
└──────────────┬──────────────┬────────────────┬──────────┘
               │              │                │
       ┌───────▼──────┐ ┌─────▼──────┐ ┌──────▼─────────┐
       │ AI Providers │ │ Local Tools│ │ Tool Workers   │
       │              │ │            │ │                │
       │ Local LLM    │ │ Files      │ │ Blender add-on │
       │ Cloud LLM    │ │ Git        │ │ Python/ML      │
       │ STT/TTS      │ │ Terminal   │ │ Browser/API    │
       │ Vision       │ │ Apps       │ │ Document tools │
       └──────────────┘ └────────────┘ └────────────────┘
```

### Technology choices

- **Avatar/UI:** Unity LTS and C#
- **Avatar format:** VRM
- **Core/orchestrator:** .NET service
- **Local communication:** Named pipes initially; gRPC if cross-process contracts become complex
- **Memory and task records:** SQLite
- **Secrets:** Windows Credential Manager and/or DPAPI-backed encrypted storage
- **Local AI runtime:** Provider adapter for a local model server/runtime
- **Cloud AI:** Provider-neutral adapters; no hard dependency on one vendor
- **Blender:** Local Blender add-on plus controlled Python scripts
- **AI/ML:** Isolated Python environments; WSL2 or containers may be offered as advanced options

---

## 5. Task Routing Policy

Every request is classified before execution.

```json
{
  "task_type": "code.modify_project",
  "complexity": "high",
  "data_sensitivity": "private",
  "internet_required": false,
  "reasoning_location": "local",
  "execution_location": "local",
  "required_tools": ["files", "git", "terminal"],
  "confirmation_required": true
}
```

The router evaluates:

1. User-selected processing policy
2. Data sensitivity
3. Internet requirement
4. Task complexity
5. Available CPU, RAM, GPU, and storage
6. Expected cloud cost
7. Required tools and permissions
8. Reversibility of each action

### Data classes

- **Public:** May use cloud reasoning under Auto mode.
- **Personal:** Local by default; cloud requires disclosure and policy approval.
- **Confidential:** Local only unless the user explicitly overrides for that task.
- **Secrets:** Passwords, tokens, private keys, and raw credentials must never be placed in LLM prompts.

### Recommended processing by category

| Task | Reasoning | Execution |
|---|---|---|
| Casual conversation | Local first | Local |
| Reminders and local planning | Local | Local |
| Private document summary | Local | Local |
| Current web research | Cloud/online | Local presentation |
| Simple code edits | Local | Local sandbox/workspace |
| Difficult coding | Local or cloud, based on privacy | Local workspace |
| Blender creation | Local/cloud plan | Local Blender |
| Small ML experiment | Local | Local isolated environment |
| Large ML training | Cloud compute after cost approval | Selected compute target |
| Email/calendar | Local planner plus authorized API | Authorized service |

---

## 6. Permission and Safety Model

### Permission levels

1. **Conversation only** — no tool use
2. **Read selected data** — inspect user-selected files or folders
3. **Modify approved workspace** — create and edit files only inside allowed roots
4. **Run approved commands** — build, test, render, or execute project commands
5. **Control selected applications** — use explicit connectors for Blender and supported programs
6. **Sensitive action** — always prompt immediately before execution

### Actions that always require confirmation

- Deleting or overwriting user data without a versioned backup
- Installing or removing software and dependencies
- Sending email or posting externally
- Purchases or financial actions
- Account, security, registry, firewall, or system-setting changes
- Uploading personal/confidential files
- Starting paid cloud compute
- Running commands outside an approved workspace

### Required controls

- Emergency stop button
- Microphone mute and visible listening indicator
- Per-tool permission settings
- Plan preview before multi-step tasks
- Command and file-diff previews
- Action audit log
- Git or snapshot rollback where applicable
- Memory viewer, editing, export, and deletion
- Cloud transmission summary

---

## 7. Voice and Avatar Pipeline

```text
Microphone
 → Echo cancellation
 → Voice activity detection
 → Streaming speech recognition
 → Conversation/task router
 → Streaming response generation
 → Streaming speech synthesis
 → Visemes, emotion, gesture, and animation
```

Required behavior:

- Push-to-talk in the first release
- Optional wake word later
- User can interrupt speech (barge-in)
- Assistant must not hear its own output
- Visible Listening, Thinking, Acting, and Speaking states
- Character animation must never hide permission or error messages

The AI core produces semantic presentation metadata:

```json
{
  "speech": "The build failed because two packages target incompatible versions.",
  "mode": "developer",
  "emotion": "concerned",
  "intensity": 0.45,
  "gesture": "thinking",
  "action_status": "awaiting_approval"
}
```

Personality affects delivery but must not alter technical facts, safety warnings, code, or permission requirements.

---

## 8. Developer Mode Workflow

1. User selects a project workspace.
2. Assistant performs read-only analysis.
3. Assistant proposes a structured plan.
4. User approves the plan and requested permissions.
5. Assistant creates a Git branch or snapshot.
6. Local tools edit files and run approved commands.
7. Assistant runs builds and tests.
8. UI displays logs and diffs.
9. User accepts, requests revision, or rolls back.

Cloud reasoning, when enabled, receives only the minimum selected context. It never receives direct terminal authority.

---

## 9. 3D Studio Workflow

1. User describes an asset or scene.
2. Assistant asks for missing constraints such as dimensions, style, and export format.
3. Assistant creates a modeling plan.
4. Blender connector translates the plan into controlled operations or a reviewable Python script.
5. User approves execution.
6. Blender performs the work locally.
7. Assistant renders previews and reports warnings.
8. User requests revisions.
9. Approved result is exported as `.blend`, `.vrm`, `.glb`, `.fbx`, or another supported format.

Initial scope should emphasize procedural props, scene assembly, materials, conversion, and automation. Production-quality character sculpting and automatic rigging remain assisted workflows requiring human review.

---

## 10. AI Laboratory Workflow

AI Laboratory is not part of the first MVP. When added, it will:

- Create one isolated environment per project
- Inspect but not silently modify datasets
- Generate reproducible configuration files
- Track package versions and random seeds
- Require approval before downloads or paid compute
- Monitor training and display metrics
- Store checkpoints in the project workspace
- Evaluate results before export
- Record dataset and model-license metadata

Global Python package installation is prohibited by default.

---

## 11. Delivery Roadmap

### Milestone 0 — Foundation

- Repository and solution structure
- Unity-to-core IPC contract
- SQLite schema
- Logging and configuration
- Permission broker skeleton
- Provider and tool interfaces

### Milestone 1 — Live Character MVP

- Placeholder VRM model
- Chat interface
- Original persona configuration
- Basic idle, speaking, and thinking animations
- Text-based local/cloud provider routing

### Milestone 2 — Real-Time Voice

- Push-to-talk
- Speech recognition provider
- Streaming TTS provider
- Lip synchronization and expressions
- Barge-in and audio-state handling

### Milestone 3 — Everyday Assistant

- Reminders and tasks
- Local document reading
- Approved-folder search
- Application launcher
- Memory-management UI

### Milestone 4 — Developer Mode

- Workspace permissions
- File tools
- Git integration
- Terminal runner
- Build/test workflows
- Diff review and rollback

### Milestone 5 — 3D Studio

- Blender add-on and authenticated local connection
- Script review and execution
- Preview rendering
- Export workflows

### Milestone 6 — AI Laboratory

- Isolated Python environments
- Dataset inspection
- Experiment templates
- Training monitoring
- Evaluation and export

### Milestone 7 — Windows Release

- Installer and updater
- First-run hardware assessment
- Local/cloud policy setup
- Crash recovery
- Security review
- Performance and accessibility pass

---

## 12. MVP Acceptance Criteria

The first usable MVP is complete when the user can:

- Launch the application from Windows normally
- See and interact with a live original 3D avatar
- Type or speak a request
- Interrupt the avatar while it is speaking
- Switch between Companion and Focus modes
- Use either a configured cloud provider or supported local model
- View and delete stored conversation memory
- Select an approved folder
- Ask the assistant to read files in that folder
- See a clear permission request before any modification
- Review an activity log
- Stop an active task immediately

Coding, Blender, and AI Laboratory are delivered after this baseline is stable.

---

## 13. Character Asset Requirements

The final original model should provide:

- VRM humanoid rig
- Neutral, happy, sad, angry, surprised, confused, and thinking expressions
- A/I/U/E/O or equivalent visemes
- Blink and eye-look controls
- Hair and clothing physics
- Idle, listen, speak, think, celebrate, warning, and error animations
- Commercial-use rights for the application and promotional materials
- A licensed original or non-imitative synthetic voice

A placeholder VRM model will be used during engineering so character production does not block the software architecture.

---

## 14. First Implementation Sprint

1. Create Unity client and .NET core solution structure.
2. Define versioned IPC messages.
3. Add connection health checks.
4. Implement text chat without tools.
5. Add provider-neutral `IChatProvider` interface.
6. Add SQLite conversation storage.
7. Implement a simple persona/presentation layer.
8. Load a placeholder VRM avatar.
9. Map Thinking, Speaking, Success, and Error states to animations.
10. Add a nonfunctional permission dialog and activity-log shell for early UI validation.

This sprint proves the architecture before speech, desktop automation, or Blender access is introduced.

---

## 15. Deferred Content Decisions

These do not block engineering:

- Exact visual design and color palette
- Character biography and voice style
- Final avatar artist/modeler
- Cloud AI provider
- Local model runtime and model selection
- Premium/offline packaging strategy

All provider choices remain modular so they can be changed without rewriting the avatar application or tool system.
