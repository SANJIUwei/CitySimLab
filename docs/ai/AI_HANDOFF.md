# AI Handoff

This document is for AI, not tutorial prose. Keep it compact and update it at the end of each work/home session.

## Current State

- Last updated: 2026-09-15
- Last known location: work
- Root: `E:\Myself\CitySimLab`
- Home root (next session): `E:\code\code home\city`
- Branch: `main`
- Remote: `https://github.com/SANJIUwei/CitySimLab.git`
- Tracking: `main -> origin/main`
- Protocol: `docs/ai/AGENT_PROTOCOL.md`
- Doc style: `docs/ai/DOCUMENTATION_STYLE.md`
- Doc index: `docs/README.md`
- Progress map: `docs/human/路网思维导图.md`
- Current problems: `docs/ai/design-notes/2026-09-11-current-problems.md`

## Project Intent

- Beginner city-simulation project. User plans and supervises; AI implements (2026-09-10).
- Do not ask the user to write code. Do not commit or push unless asked.
- Batch tests per `docs/ai/06-when-to-test.md`. Roles: `docs/human/07-roles.md`.

## Environment

- Solution: `CitySimLab.slnx`
- Core: `src/CitySim.Core`
- Headless: `src/CitySim.Headless`
- Unity mock: `src/CitySim.UnityMock`
- Tests: `tests/CitySim.Tests`
- Verify: `powershell -NoProfile -ExecutionPolicy Bypass -File tools\verify.ps1`

## Git Sync Rules

- Git: plain Git, VS Code, or GitHub Desktop.
- Start: `git status --short --branch`. Pull only if the tree is clean.
- End: update this file. Verify if code changed. Commit/push only when asked.
- Work machine GitHub credential is `SANJIUwei`. Home machine `HDyouyita` had 403 write failure before.

## Latest Work (2026-09-15)

- Docs live under `docs/human` (user) and `docs/ai` (AI).
- Added `docs/ai/AGENT_PROTOCOL.md` and `docs/ai/DOCUMENTATION_STYLE.md`.
- `AttachNearest(network, maxDistance)`: too far stays unattached.
- Scheduler uses a single `IComputeBackend`; default `CpuParallelBackend`. GPU backend is optional and not wired.
- Verified: `dotnet test` 36 passed; Headless ran.

Working tree should be clean after this upload.

## Next Recommended Action

Gameplay next knives, in order:

1. Merge lane direction into graph topology (`_adj` is still bidirectional).
2. Move `Thing` by distance/tick, not waypoint jumps. Clock does not exist yet.

Do not add turning, vehicle types, Unity presentation, or GPU backend wiring unless asked.