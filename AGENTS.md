# AI Operating Notes

This file is for AI working on CitySimLab. Treat it as the first file to read after cloning or pulling the repo.

## Human Goal

The user is a beginner building a city simulation from zero. From 2026-09-10 the standing split is: the user plans and supervises; AI implements.

AI writes feature code, tests, and headless experiments. Do not ask the user to write or paste code. Present results for review. Keep steps small enough to supervise. Record design thoughts in Simplified Chinese.

## Session Start Protocol

1. Run `git status --short`.
2. If a remote exists, run `git pull --ff-only` before editing.
3. Read `docs/AI_HANDOFF.md`.
4. For teaching tasks, read `docs/AI_TEACHING_PLAN.md`.
5. For design-continuity tasks, read the relevant note under `docs/design-notes/`.
6. Read only the docs or source files needed for the current request.
7. Verify the environment with `tools\verify.ps1` when practical.

## Session Close Protocol

Before ending a work session, update `docs/AI_HANDOFF.md` with:

- current location context, if known: work, home, or unknown
- latest completed work
- files changed
- commands run and results
- known blockers
- recommended next step for the next AI session

Then run:

```powershell
tools\verify.ps1
git status --short
```

If verification passes and changes are intentional, commit with a short message. If GitHub remote/auth is available, push. If not, leave the repo in a clean local committed state and record the blocker in `docs/AI_HANDOFF.md`.

## Roles

- User: planning and supervision. Direction, decisions, review, stop or change requirements.
- AI: full implementation. Feature code in `src/CitySim.Core` and `src/CitySim.Headless`, tests in `tests/CitySim.Tests`.
- Do not ask the user to write, copy, or paste code.
- Batch tests per `docs/06-when-to-test.md`. See also `docs/07-roles.md`.
- Record the user's design thoughts in Simplified Chinese by default.

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
- Git may be managed with plain Git commands, VS Code's built-in Git/GitHub UI, or GitHub Desktop.
- Prefer the method that already has write access to `SANJIUwei/CitySimLab`.
- If GitHub auth fails, try another allowed Git client, then ask the user for a project-specific auth method.
