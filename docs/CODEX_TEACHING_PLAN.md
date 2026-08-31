# Codex Teaching Plan

This document is for Codex agents. It is a teaching plan, not an implementation plan. Do not turn any item here into code unless the user explicitly asks.

## Operating Rule

- User writes gameplay first; Codex guides, reviews, debugs, tests, and explains.
- Every lesson must have objective evidence: a local file, a command result, an official reference, or observed user output.
- Prefer questions and tiny exercises over finished answers.
- Never prebuild the city simulation for the user.

## Evidence Map

| Evidence | What it supports | Local action for Codex |
|---|---|---|
| `docs/00-learning-contract.md` | The project is learning-first and error-driven. | Keep tasks small; make the user predict and observe. |
| `docs/02-architecture.md` | Core rules stay independent from Unity. | Put guidance around `CitySim.Core`, `CitySim.Headless`, and tests. |
| `tools/verify.ps1` | The repo has a repeatable build/test/run check. | Use it as the default session-close verification. |
| Microsoft .NET CLI docs for `dotnet build`, `dotnet test`, and `dotnet run` | Build, test, and command-line run are standard .NET workflows. | Teach compile/test/run feedback loops before game complexity. |
| Unity Manual: event function execution order | Unity gameplay depends on lifecycle timing such as initialization and update order. | Teach tick/update concepts in headless C# before real Unity scenes. |
| Unity Manual: assembly definitions | Unity projects can separate code into assemblies. | Preserve the current split between core rules and Unity-facing layers. |
| Unity Manual: ScriptableObject | Authored configuration can later live outside runtime scene objects. | Delay config assets until the user has hardcoded rules they understand. |
| Cities: Skylines user manual | Roads, zoning, buildings, services, budgets, and transport are real city-builder system categories. | Use these as curriculum categories, not as copied design answers. |
| Unity Netcode for GameObjects docs | Multiplayer introduces ownership/authority concerns. | Defer networking until simulation commands, state, and replay checks exist. |

## Curriculum Shape

### 0. Environment Literacy

- User task: run the project and explain what each project folder is for.
- Codex role: help with command errors only.
- Evidence: `README.md`, `tools/verify.ps1`, successful `dotnet build/test/run`.
- Exit check: user can run verification and identify where core code, tests, docs, and runner live.

### 1. First Data Object

- User task: choose one tiny object: coordinate, tile, or tick.
- Codex role: ask for expected behavior before code; help write one test after the user's attempt.
- Evidence: local test result and user-written source file.
- Exit check: one user-authored object compiles and has one passing test.

### 2. First Rule

- User task: add one rule to the chosen object.
- Codex role: force input/output clarity; review edge cases without providing a full system.
- Evidence: failing test before fix or explicit prediction before run.
- Exit check: user can explain why the rule passes and what input would break it.

### 3. Headless Observation

- User task: print a small experiment from `CitySim.Headless`.
- Codex role: keep output boring and readable; no UI work.
- Evidence: command-line output captured by `dotnet run`.
- Exit check: user can compare predicted output with actual output.

### 4. Map Representation

- User task: represent a small map using the objects already learned.
- Codex role: ask about bounds, indexing, and invalid positions; avoid giving a finished map API.
- Evidence: tests for inside/outside positions and a simple headless print.
- Exit check: user can describe how data is stored and why one indexing choice was made.

### 5. Roads And Connectivity

- User task: introduce roads as data, then ask one connectivity question.
- Codex role: guide toward adjacency before pathfinding.
- Evidence: city-builder manuals treat roads as foundational infrastructure; local tests show road queries.
- Exit check: user can answer "is this tile connected enough for the next rule?" without full traffic simulation.

### 6. Zoning And Growth

- User task: model one zone type and one growth condition.
- Codex role: keep the rule deliberately fake but testable; avoid balancing numbers for the user.
- Evidence: Cities: Skylines uses zoning/building categories; local tests define this project's simplified behavior.
- Exit check: user can separate "game design guess" from "tested rule."

### 7. Services And Economy

- User task: add one resource or service pressure, such as money, power, or maintenance.
- Codex role: insist on one cause/effect loop and one visible failure state.
- Evidence: city-builder manuals include services and budgets as system categories.
- Exit check: user can point to a test showing both normal and failing conditions.

### 8. Traffic And Pathfinding

- User task: only after roads and map are stable, attempt a tiny route query.
- Codex role: start with BFS or simple graph reasoning; delay A* until the user can explain the simpler version.
- Evidence: local graph tests; pathfinding is a later system because it depends on map and roads.
- Exit check: user can explain nodes, edges, start, goal, and no-path behavior.

### 9. Persistence And Replay

- User task: save/load a tiny state or replay a tiny command list.
- Codex role: make determinism visible; prepare for future networking without adding a server.
- Evidence: local snapshot files and repeated command output.
- Exit check: same input produces same state twice.

### 10. Unity Presentation Layer

- User task: only after core rules exist, map one core state to a Unity-style view concept.
- Codex role: keep MonoBehaviour-like code thin; teach lifecycle boundaries.
- Evidence: Unity execution-order and assembly-definition docs.
- Exit check: core tests still pass without Unity.

### 11. Networking Preparation

- User task: convert user actions into explicit commands.
- Codex role: explain authority/ownership concepts, but do not build server code yet.
- Evidence: Unity Netcode docs and local replay/determinism checks.
- Exit check: a command can be replayed locally before any network transport exists.

## Source Links For Codex

- Microsoft .NET CLI overview: https://learn.microsoft.com/en-us/dotnet/core/tools/
- Unity event execution order: https://docs.unity3d.com/Manual/execution-order.html
- Unity assembly definitions: https://docs.unity.cn/Manual/ScriptCompilationAssemblyDefinitionFiles.html
- Unity ScriptableObject: https://docs.unity.cn/Manual/class-ScriptableObject.html
- Cities: Skylines user manual: https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/255710/manuals/CitiesSkylines-UserManual.pdf
- Unity Netcode ownership and authority: https://docs.unity.cn/Packages/com.unity.netcode.gameobjects%402.13/manual/terms-concepts/ownership.html
