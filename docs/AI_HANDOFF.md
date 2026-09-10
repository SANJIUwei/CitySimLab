# AI Handoff

This document is for AI, not tutorial prose. Keep it compact and update it at the end of each work/home session.

## Current State

- Last updated: 2026-09-10
- Last known location: home
- Root: `E:\code\code home\city`
- Branch: `main`
- Remote: `https://github.com/SANJIUwei/CitySimLab.git`
- Tracking: `main -> origin/main`
- Sync status: local last-meters LocalApproach added; commit after verify
- Working tree should be clean after each session close

## Project Intent

- Beginner city-simulation project. User plans and supervises; AI implements (2026-09-10).
- AI writes feature code, tests, and headless experiments. Do not ask the user to write code.
- Batch tests per `docs/06-when-to-test.md`. Roles: `docs/07-roles.md`.

## Environment

- Solution: `CitySimLab.slnx`
- Core rules project: `src/CitySim.Core`
- No-Unity runner: `src/CitySim.Headless`
- Unity concept mock layer: `src/CitySim.UnityMock`
- Tests: `tests/CitySim.Tests`
- Docs: `docs`
- Design notes: `docs/design-notes`
- Verification script: `tools\verify.ps1`
- AI teaching plan: `docs/AI_TEACHING_PLAN.md`

## Git Sync Rules

- Git may be managed with plain Git commands, VS Code Git/GitHub UI, or GitHub Desktop.
- Start session: `git status --short --branch`, then `git pull --ff-only` if clean.
- End session: update this file, run `tools\verify.ps1`, commit, then `git push`.
- If normal Git auth fails, use VS Code Git/GitHub UI, GitHub Desktop, or ask the user for a project-specific auth method.
- Keep old auth failures out of this document unless they are still actionable.

## Last Verification

- `tools\verify.ps1`: passed on 2026-08-31 14:01 +08:00.
- `git ls-remote --heads origin main`: confirmed remote `main` on 2026-08-31 11:40 +08:00.
- `git push`: passed on 2026-08-31 12:13 +08:00.
- `git rebase origin/main`: passed on 2026-09-01 16:17 +08:00; preserved local `WorldPosition.cs`.
- Direct `tools\verify.ps1`: blocked on 2026-09-01 by PowerShell execution policy.
- `powershell -NoProfile -ExecutionPolicy Bypass -File tools\verify.ps1`: passed on 2026-09-01 16:22 +08:00; build passed with 0 warnings/0 errors, tests passed 1/1, headless environment check passed.
- `powershell -NoProfile -ExecutionPolicy Bypass -File tools\verify.ps1`: passed on 2026-09-01 18:36 +08:00 after converting design notes to Chinese; build passed with 0 warnings/0 errors, tests passed 1/1, headless environment check passed.
- `git push origin main`: passed on 2026-09-01 16:28 +08:00 for design notes update.
- `git pull --ff-only`: passed on 2026-09-02 at home; repository was already up to date.
- `powershell -NoProfile -ExecutionPolicy Bypass -File tools\verify.ps1`: passed on 2026-09-02 at home; build passed with 0 warnings/0 errors, tests passed 1/1, and the headless runner completed successfully.
- VS Code opened `E:\code\code home\city`; source control is available through the built-in Git panel.
- `git push origin main`: blocked on 2026-09-02 with GitHub HTTP 403; credential account `HDyouyita` does not have write permission to `SANJIUwei/CitySimLab`.

## Latest Work

- Home session pulled `e0101e2`, then added `PlanTrip` and the last-meters `LocalApproach`.
- Last meters is a fixed local sequence, not a graph search: ExitNode (cross-segment only) → cached `t` on the target segment → building Entrance.
- Same-segment trips skip ExitNode; along-segment cost is already in the on-road `t→t` cost.
- Still not implemented: turning, signs, path-request queue, vehicles, nearest-point attachment.

## Next Recommended Action

1. Wait for user review of `LocalApproach`.
2. Do not add turning, signs, A*, or a path queue unless the user asks.
3. Read `docs/design-notes/2026-09-10-pathfinding-pipeline.md` before changing routing.

## Close Template

```text
Date:
Location:
Branch:
Latest commit:
Files changed:
Commands run:
Results:
Blockers:
Next suggested action:
```
