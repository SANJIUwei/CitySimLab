# AI Handoff

This document is for AI, not tutorial prose. Keep it compact and update it at the end of each work/home session.

## Current State

- Last updated: 2026-09-11
- Last known location: work
- Root: `E:\Myself\CitySimLab`
- Home root (next session): `E:\code\code home\city`
- Branch: `main`
- Remote: `https://github.com/SANJIUwei/CitySimLab.git`
- Tracking: `main -> origin/main`
- Working tree should be clean after each session close
- User-facing recap: `docs/design-notes/2026-09-11-session-work.md`
- Progress map: `docs/路网思维导图.md`

## Project Intent

- Beginner city-simulation project. User plans and supervises; AI implements (2026-09-10).
- AI writes feature code, tests, and headless experiments. Do not ask the user to write code.
- Batch tests per `docs/06-when-to-test.md`. Roles: `docs/07-roles.md`.

## Environment

- Solution: `CitySimLab.slnx`
- Core: `src/CitySim.Core`
- Headless: `src/CitySim.Headless`
- Unity mock: `src/CitySim.UnityMock`
- Tests: `tests/CitySim.Tests`
- Verify: `powershell -NoProfile -ExecutionPolicy Bypass -File tools\verify.ps1`

## Git Sync Rules

- Git: plain Git, VS Code, or GitHub Desktop.
- Start: `git status --short --branch`, then `git pull --ff-only` if clean.
- End: update this file, verify, commit, `git push`.
- Work machine GitHub credential is `SANJIUwei`. Home machine `HDyouyita` had 403 write failure before.

## Latest Work (2026-09-11 work)

- TripStarter owns trip start; Building does not IssueTo; unattached requests fail.
- Trip/TripCommand use RouteEnd, not Building.
- Thing.Advance walks logical waypoints.
- ComputeScheduler is a world allocator: Category, Lane, PriorityScore 0-100, vruntime + aging. Pathfinder only submits `path` jobs.
- Dedicated pinned scheduler core was removed. No core affinity. Virtual scheduling only.
- GPU backend still unavailable; GPU jobs wait.
- Scale: 160x160 nodes, 20k parallel FindPath ~2.7s on 20 threads.

## Next Recommended Action

At home: `git pull --ff-only`, read `docs/design-notes/2026-09-11-session-work.md`.

If continuing gameplay:

1. Max attach distance for buildings.
2. Merge lane direction into graph topology.
3. Move Thing by distance/tick, not waypoint jumps.
4. Put node-to-node Request on the same queue.

Do not add turning, vehicle types, or Unity presentation unless asked.
