# Codex Handoff

This document is written for Codex, not for human-facing tutorial prose. Update it at the end of every work/home session before committing and pushing.

## Sync State

- Last updated: 2026-08-31 11:40 +08:00
- Last known location: work
- Git branch: main
- GitHub remote: `https://github.com/SANJIUwei/CitySimLab.git`
- GitHub auth: VS Code/Git binding completed by user; do not use GitHub Desktop
- Environment root: `E:\Myself\CitySimLab`

## Project Intent

The user wants to learn by building the project themselves. Codex should provide scaffolding, debugging, verification, and learning guidance. Do not prebuild city simulation gameplay systems unless the user explicitly asks.

## Current Repository State

- Solution exists: `CitySimLab.slnx`
- Core library exists: `src/CitySim.Core`
- Headless runner exists: `src/CitySim.Headless`
- Unity mock helper library exists: `src/CitySim.UnityMock`
- Test project exists: `tests/CitySim.Tests`
- Documentation folder exists: `docs`
- Save folder placeholder exists: `saves/.gitkeep`

## Latest Completed Work

- Created the C# solution and project skeleton under `E:\Myself\CitySimLab`.
- Added a minimal smoke test so `dotnet test` confirms project references are wired.
- Added a minimal headless runner that prints environment readiness only.
- Added learning docs without implementing gameplay mechanics.
- Removed earlier over-scoped gameplay prototype files after the user clarified that gameplay should remain their work.
- Published the repository to GitHub through VS Code/Git, with local `main` tracking `origin/main`.

## Verification Log

- `dotnet build --no-restore`: passed on 2026-08-31.
- `dotnet test --no-build`: passed on 2026-08-31, 1 test.
- `dotnet run --project src\CitySim.Headless`: passed on 2026-08-31.
- `tools\verify.ps1`: passed on 2026-08-31 11:40 +08:00.
- `git ls-remote --heads origin main`: passed on 2026-08-31 11:40 +08:00, remote `main` pointed at `c86baa0d8588ed5fabebbbf6fb4d98e132e3b759`.

## Open Blockers

- No current GitHub remote blocker.
- Do not use GitHub Desktop for this project. The user clarified that GitHub Desktop belongs to company/work usage and must not be touched for CitySimLab.
- GitHub CLI may still be unauthenticated; this is not a blocker as long as normal Git push/pull works through the configured VS Code/Git credential path.

## Next Recommended Codex Action

If the user asks to continue GitHub sync:

1. Do not use GitHub Desktop.
2. Run `git status --short --branch`.
3. If working tree is clean, run `git pull --ff-only`.
4. After changes are complete, run `tools\verify.ps1`.
5. Commit intentional changes.
6. Push with `git push`.
7. Update this handoff with the latest commit hash and verification results.

If the user asks for gameplay learning:

1. Ask them to choose one tiny first object: coordinate, tile, or tick.
2. Let them attempt the code first.
3. Help them write the smallest useful test.
4. Keep implementation scope deliberately small.

## Files Codex Should Read First

- `AGENTS.md`
- `docs/CODEX_HANDOFF.md`
- `README.md`
- `docs/04-your-first-task.md`

## Session Update Template

Copy this block when closing a future session:

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
