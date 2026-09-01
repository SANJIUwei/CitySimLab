# Codex Operating Notes

This file is for Codex agents working on CitySimLab. Treat it as the first file to read after cloning or pulling the repo.

## Human Goal

The user is a beginner learning how to build a city simulation game from zero. The project may later approach systems found in Cities: Skylines 1, including networking, but the learning goal matters more than fast completion.

Do not implement gameplay systems unless the user explicitly asks for implementation help. Prefer environment setup, debugging, Socratic guidance, tests, and small reviewable steps.

## Session Start Protocol

1. Run `git status --short`.
2. If a remote exists, run `git pull --ff-only` before editing.
3. Read `docs/CODEX_HANDOFF.md`.
4. For teaching tasks, read `docs/CODEX_TEACHING_PLAN.md`.
5. For design-continuity tasks, read the relevant note under `docs/design-notes/`.
6. Read only the docs or source files needed for the current request.
7. Verify the environment with `tools\verify.ps1` when practical.

## Session Close Protocol

Before ending a work session, update `docs/CODEX_HANDOFF.md` with:

- current location context, if known: work, home, or unknown
- latest completed work
- files changed
- commands run and results
- known blockers
- recommended next step for the next Codex session

Then run:

```powershell
tools\verify.ps1
git status --short
```

If verification passes and changes are intentional, commit with a short message. If GitHub remote/auth is available, push. If not, leave the repo in a clean local committed state and record the blocker in `docs/CODEX_HANDOFF.md`.

## Teaching Style

- Let the user write first when the task is gameplay learning.
- Give hints before full answers.
- Ask the user to predict behavior before running experiments.
- When fixing an error, explain the smallest cause and the smallest verification.
- Keep code changes tiny unless the user asks for a larger setup step.
- For this private practice project, do not use game-development skills or write gameplay code unless the user explicitly asks in the current turn.

## Architecture Rules

- Core gameplay logic belongs in `src/CitySim.Core`.
- `src/CitySim.Core` must not depend on Unity.
- `src/CitySim.Headless` is the no-Unity command-line experiment runner.
- `src/CitySim.UnityMock` may contain tiny Unity-like types for learning lifecycle ideas only.
- `tests/CitySim.Tests` is the first validation layer.
- Do not add a real server, real Unity project, networking stack, or large framework until the user asks.

## Git Rules

- Protect the user's work. Never reset, checkout, or delete changes you did not make unless explicitly asked.
- Prefer `main` as the long-lived branch.
- Keep generated build outputs out of Git.
- Avoid committing secrets, tokens, `.env` files, Unity Library folders, or local machine paths except documented setup paths.

## Sync Tool Notes

- Remote: `https://github.com/SANJIUwei/CitySimLab.git`.
- Normal sync loop: `git pull --ff-only` at start, `tools\verify.ps1`, commit, then `git push` at close.
- VS Code's built-in Git/GitHub UI was used for first publish/auth. Use it only if normal Git auth breaks.
- Do not use, open, inspect, or rely on the user's GitHub Desktop for this project.
- If GitHub auth fails, stop and ask the user for a project-specific auth method.
