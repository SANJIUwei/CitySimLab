# Codex Handoff

This document is for Codex agents, not tutorial prose. Keep it compact and update it at the end of each work/home session.

## Current State

- Last updated: 2026-09-01 18:37 +08:00
- Last known location: work
- Root: `E:\Myself\CitySimLab`
- Branch: `main`
- Remote: `https://github.com/SANJIUwei/CitySimLab.git`
- Tracking: `main -> origin/main`
- Sync status: clean and pushed to `origin/main` at session close; run `git status --short --branch` after pull to confirm on the next machine
- Working tree should be clean after each session close

## Project Intent

- Beginner learning project for building a city simulation game from zero.
- Codex should scaffold, debug, verify, and guide.
- Do not implement gameplay systems unless the user explicitly asks.
- Let the user write first when the task is gameplay learning.

## Environment

- Solution: `CitySimLab.slnx`
- Core rules project: `src/CitySim.Core`
- No-Unity runner: `src/CitySim.Headless`
- Unity concept mock layer: `src/CitySim.UnityMock`
- Tests: `tests/CitySim.Tests`
- Docs: `docs`
- Design notes: `docs/design-notes`
- Verification script: `tools\verify.ps1`
- Codex teaching plan: `docs/CODEX_TEACHING_PLAN.md`

## Git Sync Rules

- Do not use GitHub Desktop.
- Start session: `git status --short --branch`, then `git pull --ff-only` if clean.
- End session: update this file, run `tools\verify.ps1`, commit, then `git push`.
- If normal Git auth fails, use VS Code built-in Git/GitHub UI or ask the user for a project-specific auth method.
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

## Latest Work

- Added `docs/design-notes/` for Codex-only design continuity.
- Recorded the user's 2026-09-01 reasoning about road graphs, nodes/segments, free building world positions, road attachments, and the "last 100 meters" local approach.
- Updated `docs/CODEX_TEACHING_PLAN.md` so the near-term teaching track focuses on building entrance to road attachment, not full gameplay implementation.
- Reinforced in `AGENTS.md` that this is a private practice project: do not use game-development skills or write gameplay code unless the user explicitly asks in the current turn.
- User requested that design-thinking notes be written in Chinese because English is tiring to read; `docs/design-notes/` was converted to Chinese and future user-thought notes should default to Simplified Chinese.
- Local `src/CitySim.Core/WorldPosition.cs` exists and appears to represent the user's coordinate-practice thread; do not modify it unless asked.

## Next Recommended Action

If the next session is sync/setup work:

1. Pull with `git pull --ff-only`.
2. Verify with `tools\verify.ps1`.
3. Update this handoff only with current, actionable state.

If the next session is gameplay learning:

1. Read `docs/design-notes/2026-09-01-road-network-and-building-access.md`.
2. Ask the user to explain building position, building entrance, road segment, and road attachment in their own words.
3. If the user asks to code, guide them toward one tiny calculation: nearest point on a single segment and attachment `t`.
4. Let the user attempt the code first; Codex reviews, debugs, and verifies.

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
