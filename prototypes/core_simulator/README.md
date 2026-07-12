# Core Simulator

This dependency-free Python package validates Astil Codex routing, permissions, and local storage behavior before the production .NET implementation is introduced.

Run from the repository root:

```bash
python -m unittest discover -s prototypes/core_simulator/tests -v
python prototypes/core_simulator/demo.py
```

The simulator is not an AI model and does not contact local or cloud providers. Its purpose is to make deterministic security and routing rules executable and testable.
