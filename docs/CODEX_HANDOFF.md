# Codex Handoff

This document is for Codex agents, not tutorial prose. Keep it compact and update it at the end of each work/home session.

## Current State

- Last updated: 2026-08-31 11:59 +08:00
- Last known location: work
- Root: `E:\Myself\CitySimLab`
- Branch: `main`
- Remote: `https://github.com/SANJIUwei/CitySimLab.git`
- Tracking: `main -> origin/main`
- Remote latest observed before compaction: `ac0192e9f8264e353f4c6f36f2b491cd695178e0`
- Local pending push: compacted Git handoff docs; push is pending because current network cannot reach GitHub.

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
- Verification script: `tools\verify.ps1`

## Git Sync Rules

- Do not use GitHub Desktop.
- Start session: `git status --short --branch`, then `git pull --ff-only` if clean.
- End session: update this file, run `tools\verify.ps1`, commit, then `git push`.
- If normal Git auth fails, use VS Code built-in Git/GitHub UI or ask the user for a project-specific auth method.
- Keep old auth failures out of this document unless they are still actionable.

## Last Verification

- `tools\verify.ps1`: passed on 2026-08-31 11:54 +08:00.
- `git ls-remote --heads origin main`: confirmed remote `main` on 2026-08-31 11:40 +08:00.
- `git push`: failed on 2026-08-31 11:59 +08:00 because this machine could not reach `github.com:443`; retry when network is available.

## Next Recommended Action

If the next session is sync/setup work:

1. Pull with `git pull --ff-only`.
2. Verify with `tools\verify.ps1`.
3. Update this handoff only with current, actionable state.

If the next session is gameplay learning:

1. Ask the user to choose one tiny first object: coordinate, tile, or tick.
2. Let the user attempt the code first.
3. Help them add the smallest useful test.
4. Keep implementation scope small.

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
