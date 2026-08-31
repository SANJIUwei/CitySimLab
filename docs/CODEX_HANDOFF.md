# Codex Handoff

This document is written for Codex, not for human-facing tutorial prose. Update it at the end of every work/home session before committing and pushing.

## Sync State

- Last updated: 2026-08-31 11:09 +08:00
- Last known location: work
- Git branch: main
- GitHub remote: not configured
- GitHub auth: VS Code is reportedly signed in, but `gh` and Git Credential Manager cannot currently reuse that login
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

## Verification Log

- `dotnet build --no-restore`: passed on 2026-08-31.
- `dotnet test --no-build`: passed on 2026-08-31, 1 test.
- `dotnet run --project src\CitySim.Headless`: passed on 2026-08-31.

## Open Blockers

- GitHub remote is not configured.
- GitHub CLI is installed but not authenticated.
- `gh auth login --web` was attempted. It displayed a device code, then failed while exchanging the OAuth token because the connection to GitHub timed out.
- `git credential-manager github list` returned no reusable GitHub account.
- `code --list-extensions --show-versions` shows C# / Unity / EditorConfig extensions only; no command-line-accessible GitHub publish extension was found.
- VS Code UI may still be able to publish using its internal GitHub authentication, but Codex cannot safely read or reuse that token directly.
- Need either a completed VS Code UI "Publish to GitHub" action, an existing GitHub repository URL, or a working `gh auth login` / token flow.

## Next Recommended Codex Action

If the user asks to finish GitHub sync:

1. First try the VS Code UI path if the user is present: open `E:\Myself\CitySimLab` in VS Code, Source Control, Publish Branch / Publish to GitHub, choose private unless the user says otherwise.
2. If VS Code creates the repo, return to terminal and run `git remote -v` plus `git push -u origin main` if needed.
3. If VS Code UI cannot publish, ask for an existing GitHub repository URL or ask the user to complete `gh auth login`.
4. Configure `origin`.
5. Push `main`.
6. Re-run clone/pull instructions mentally for a second machine.
7. Update this handoff with the remote URL and latest commit hash.

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
