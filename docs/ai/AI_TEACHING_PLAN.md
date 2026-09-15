# AI Teaching Plan

This document is for AI. It is a teaching plan, not an implementation plan. Do not turn any item here into code unless the user explicitly asks.

## Operating Rule

- User plans and supervises; AI implements, tests, and explains.
- Do not ask the user to write code. Discuss a slice, then AI edits files.
- Tests are batched: `docs/ai/06-when-to-test.md`. Roles: `docs/human/07-roles.md`.
- Every slice must have objective evidence: a file, a command result, or a test run.
- Keep implementations small enough to review. Do not build the whole city at once.
- Record the user's design thoughts in Simplified Chinese by default.

## Current Design Track

Focus the next teaching discussions on road/building separation:

- Building placement uses world coordinates, not a fixed road grid.
- Roads should be represented as graph data for routing, while visual road geometry and movement detail can be richer.
- Building entrances should attach to road segments through a local relationship instead of turning every building into a permanent global routing node.
- Global pathfinding can solve the road-network route; deterministic local approach logic can solve the final road-to-building distance.
- Ask the user to explain these relationships before asking them to code.

## Evidence Map

| Evidence | What it supports | Local action for AI |
|---|---|---|
| `docs/human/00-learning-contract.md` | The project is learning-first and error-driven. | Keep tasks small; make the user predict and observe. |
| `docs/human/02-architecture.md` | Core rules stay independent from Unity. | Put guidance around `CitySim.Core`, `CitySim.Headless`, and tests. |
| `docs/ai/design-notes/2026-09-01-road-network-and-building-access.md` | User's current road graph and building access reasoning. | Preserve continuity; use it for questions, not immediate implementation. |
| `tools/verify.ps1` | The repo has a repeatable build/test/run check. | Use it as the default session-close verification. |
| Microsoft .NET CLI docs for `dotnet build`, `dotnet test`, and `dotnet run` | Build, test, and command-line run are standard .NET workflows. | Teach compile/test/run feedback loops before game complexity. |
| Unity Manual: event function execution order | Unity gameplay depends on lifecycle timing such as initialization and update order. | Teach tick/update concepts in headless C# before real Unity scenes. |
| Unity Manual: assembly definitions | Unity projects can separate code into assemblies. | Preserve the current split between core rules and Unity-facing layers. |
| Unity Manual: ScriptableObject | Authored configuration can later live outside runtime scene objects. | Delay config assets until the user has hardcoded rules they understand. |
| Cities: Skylines user manual | Roads, zoning, buildings, services, budgets, and transport are real city-builder system categories. | Use these as curriculum categories, not as copied design answers. |
| TM:PE network documentation | Cities: Skylines-style networks can be discussed as nodes, segments, and lanes. | Use the vocabulary carefully: nodes are not roads; segments connect nodes; lanes carry movement. |
| OSRM nearest service and GraphHopper map matching | Real routing systems often snap free coordinates to a road network before routing. | Use as analogy for building entrance to road attachment. |
| Valhalla routing documentation | Large routing systems use tiled/hierarchical data structures. | Mention as future scaling inspiration only; do not teach it before the user can model one segment. |
| Unity Netcode for GameObjects docs | Multiplayer introduces ownership/authority concerns. | Defer networking until simulation commands, state, and replay checks exist. |

## Curriculum Shape

### 0. Environment Literacy

- User task: run the project and explain what each project folder is for.
- AI role: help with command errors only.
- Evidence: `README.md`, `tools/verify.ps1`, successful `dotnet build/test/run`.
- Exit check: user can run verification and identify where core code, tests, docs, and runner live.

### 1. First Data Object

- User task: choose one tiny object: coordinate, tile, or tick.
- AI role: ask for expected behavior before code; after the user's attempt, AI writes the matching test and runs it.
- Evidence: local test result and user-written source file.
- Exit check: one user-authored object compiles and has one passing test.

### 2. First Rule

- User task: add one rule to the chosen object.
- AI role: force input/output clarity; review edge cases without providing a full system.
- Evidence: failing test before fix or explicit prediction before run.
- Exit check: user can explain why the rule passes and what input would break it.

### 3. Headless Observation

- User task: print a small experiment from `CitySim.Headless`.
- AI role: keep output boring and readable; no UI work.
- Evidence: command-line output captured by `dotnet run`.
- Exit check: user can compare predicted output with actual output.

### 4. Map Representation

- User task: represent a small map using the objects already learned.
- AI role: ask about bounds, indexing, and invalid positions; avoid giving a finished map API.
- Evidence: tests for inside/outside positions and a simple headless print.
- Exit check: user can describe how data is stored and why one indexing choice was made.

### 5. Roads And Connectivity

- User task: introduce roads as data, then ask one connectivity question.
- AI role: guide toward adjacency and road attachment before pathfinding.
- Evidence: city-builder manuals treat roads as foundational infrastructure; local tests show road queries.
- Exit check: user can distinguish road node, road segment, building entrance, and attachment point.

### 5A. Building Entrance To Road Attachment

- User task: describe how one building entrance attaches to one nearby road segment.
- AI role: ask for the user's expected data before code; review the reasoning; do not provide a finished system.
- Evidence: `docs/ai/design-notes/2026-09-01-road-network-and-building-access.md`, TM:PE nodes/segments/lanes vocabulary, OSRM/GraphHopper coordinate-to-road matching analogies.
- Exit check: user can explain why the global road graph should not permanently include every building by default.

### 6. Zoning And Growth

- User task: model one zone type and one growth condition.
- AI role: keep the rule deliberately fake but testable; avoid balancing numbers for the user.
- Evidence: Cities: Skylines uses zoning/building categories; local tests define this project's simplified behavior.
- Exit check: user can separate "game design guess" from "tested rule."

### 7. Services And Economy

- User task: add one resource or service pressure, such as money, power, or maintenance.
- AI role: insist on one cause/effect loop and one visible failure state.
- Evidence: city-builder manuals include services and budgets as system categories.
- Exit check: user can point to a test showing both normal and failing conditions.

### 8. Traffic And Pathfinding

- User task: only after roads and map are stable, attempt a tiny route query.
- AI role: start with BFS or simple graph reasoning; delay A* until the user can explain the simpler version. Keep the "last 100 meters" as local logic, not global graph bloat.
- Evidence: local graph tests; pathfinding is a later system because it depends on map and roads.
- Exit check: user can explain nodes, edges, start, goal, and no-path behavior.

### 9. Persistence And Replay

- User task: save/load a tiny state or replay a tiny command list.
- AI role: make determinism visible; prepare for future networking without adding a server.
- Evidence: local snapshot files and repeated command output.
- Exit check: same input produces same state twice.

### 10. Unity Presentation Layer

- User task: only after core rules exist, map one core state to a Unity-style view concept.
- AI role: keep MonoBehaviour-like code thin; teach lifecycle boundaries.
- Evidence: Unity execution-order and assembly-definition docs.
- Exit check: core tests still pass without Unity.

### 11. Networking Preparation

- User task: convert user actions into explicit commands.
- AI role: explain authority/ownership concepts, but do not build server code yet.
- Evidence: Unity Netcode docs and local replay/determinism checks.
- Exit check: a command can be replayed locally before any network transport exists.

## Source Links For AI

- Microsoft .NET CLI overview: https://learn.microsoft.com/en-us/dotnet/core/tools/
- Unity event execution order: https://docs.unity3d.com/Manual/execution-order.html
- Unity assembly definitions: https://docs.unity.cn/Manual/ScriptCompilationAssemblyDefinitionFiles.html
- Unity ScriptableObject: https://docs.unity.cn/Manual/class-ScriptableObject.html
- Cities: Skylines user manual: https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/255710/manuals/CitiesSkylines-UserManual.pdf
- TM:PE nodes, segments, and lanes: https://doc.tmpe.me/nodes-segments-lanes.html
- Colossal Order traffic AI diary: https://colossalorder.fi/news/development-diary-2-traffic-ai/
- OSRM nearest service: https://project-osrm.org/docs/v5.5.1/api/#nearest-service
- GraphHopper map matching: https://github.com/graphhopper/graphhopper/tree/master/map-matching
- Valhalla routing docs: https://valhalla.github.io/valhalla/
- Unity Netcode ownership and authority: https://docs.unity.cn/Packages/com.unity.netcode.gameobjects%402.13/manual/terms-concepts/ownership.html
