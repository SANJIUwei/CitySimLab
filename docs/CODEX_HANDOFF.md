# Codex Handoff

This document is written for Codex, not for human-facing tutorial prose. Update it at the end of every work/home session before committing and pushing.

## Sync State

- Last updated: 2026-08-31 11:25 +08:00
- Last known location: work
- Git branch: main
- GitHub remote: not configured
- GitHub auth: prefer VS Code built-in Git/GitHub UI; do not use GitHub Desktop
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
- Do not use GitHub Desktop for this project. The user clarified that GitHub Desktop belongs to company/work usage and must not be touched for CitySimLab.
- VS Code 1.135.0 is installed and includes built-in Git/GitHub support. Local inspection found built-in extensions `git`, `git-base`, `github`, `github-authentication`, and `microsoft-authentication`.
- VS Code's built-in GitHub extension contributes command `github.publish`.
- Codex tried `code --command git.publish` earlier, but this Code CLI does not expose `--command`; it only passed the argument through to Electron/Chromium. Therefore publishing must be initiated from the VS Code UI unless another automation path is added.
- GitHub CLI is installed but not authenticated.
- `gh auth login --web` was attempted. It displayed a device code, then failed while exchanging the OAuth token because the connection to GitHub timed out.
- `git credential-manager github list` returned no reusable GitHub account.
- `code --list-extensions --show-versions` shows C# / Unity / EditorConfig extensions only; no command-line-accessible GitHub publish extension was found.
- VS Code sign-in must not be treated as reusable Git credentials.
- Need the user to approve VS Code's "Publish to GitHub" UI, or provide an existing GitHub repository URL / working `gh auth login` / project-specific token flow.

## Next Recommended Codex Action

If the user asks to finish GitHub sync:

1. Do not use GitHub Desktop.
2. Prefer VS Code built-in Git/GitHub UI. Open `E:\Myself\CitySimLab` in VS Code.
3. Ask the user to run Command Palette `GitHub: Publish to GitHub` or click Source Control `Publish Branch` / `Publish to GitHub`.
4. Ask the user to choose private unless they explicitly want public.
5. After VS Code publishes, return to terminal and run `git remote -v` plus `git status --short`.
6. If a remote exists but branch is not tracking, run `git push -u origin main`.
7. Re-run clone/pull instructions mentally for a second machine.
8. Update this handoff with the remote URL and latest commit hash.

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
