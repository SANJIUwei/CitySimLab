# AI Handoff

This document is for AI, not tutorial prose. Keep it compact and update it at the end of each work/home session.

## Current State

- Last updated: 2026-09-10
- Last known location: work
- Root: `E:\Myself\CitySimLab`
- Branch: `main`
- Remote: `https://github.com/SANJIUwei/CitySimLab.git`
- Tracking: `main -> origin/main`
- Sync status: local working tree has a new design note; not committed unless the user asks
- Working tree should be clean after each session close

## Project Intent

- Beginner learning project for building a city simulation game from zero.
- AI should scaffold, debug, verify, and guide.
- Do not implement gameplay systems unless the user explicitly asks.
- Let the user write first when the task is gameplay learning.
- Standing split: user writes feature code; AI writes comments/tests in `tests/CitySim.Tests` and runs them. Do not ask the user for tests.

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

- Recorded the user's 2026-09-10 placement discussion in `docs/design-notes/2026-09-10-placement-logic-vs-visual-naturalness.md`.
- User compared Cities: Skylines road-attached buildings with Transport Fever free placement plus auto-connect.
- User currently judges Skylines-style attachment as logically clearer for nodes and entrances, but visually fake/unnatural (art and detail, not code). Topic is parked until the user understands roads and pathfinding more deeply.
- User is still only at `WorldPosition.cs` plus `Distance`. Do not jump to road architecture.
- User asked on 2026-09-10 that AI own tests going forward: write them in sync with feature changes, add comments, run `dotnet test`. User will not write tests.
- Current tests: smoke + 3-4-5 distance + identical-points distance 0.
- No gameplay code was changed by AI this turn.

## Next Recommended Action

If the next session is sync/setup work:

1. Pull with `git pull --ff-only`.
2. Verify with `tools\verify.ps1`.
3. Update this handoff only with current, actionable state.

If the next session is gameplay learning:

1. Stay with `WorldPosition` unless the user asks to move on.
2. Read `docs/design-notes/2026-09-01-road-network-and-building-access.md` and `docs/design-notes/2026-09-10-placement-logic-vs-visual-naturalness.md` before reopening placement/pathfinding.
3. Do not resume the Skylines-vs-Transport-Fever choice unless the user brings it up.
4. Let the user write feature code first; AI writes/runs matching tests and reviews.

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
