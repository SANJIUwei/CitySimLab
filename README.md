# CitySimLab

CitySimLab is a C# learning sandbox for building city-simulation systems with a Unity-style project workflow.

The repository currently contains only the development environment scaffold: solution structure, a headless runner, a test project, documentation folders, and lightweight Unity concept mocks. Gameplay systems are intentionally not implemented yet.

## Status

- Language/runtime: C# / .NET
- Unity dependency: none
- Current focus: environment, verification, and learning workflow
- Future direction: map simulation, zoning, traffic, economy, services, saves, and networking

## Repository Layout

```text
src/CitySim.Core        Core simulation code
src/CitySim.Headless    Command-line runner for non-Unity experiments
src/CitySim.UnityMock   Minimal Unity-like concepts for learning lifecycle patterns
tests/CitySim.Tests     Automated tests
docs/                   Design notes, learning notes, and Codex handoff docs
saves/                  Placeholder for future saves or simulation snapshots
tools/                  Utility scripts
```

## Verification

```powershell
cd E:\Myself\CitySimLab
tools\verify.ps1
```

Equivalent manual commands:

```powershell
dotnet build
dotnet test
dotnet run --project src\CitySim.Headless
```

## Design Boundaries

- Keep core simulation logic independent from Unity.
- Use the headless runner to verify behavior before adding presentation layers.
- Add tests for small rules as they are introduced.
- Keep generated build outputs, secrets, and local machine state out of Git.
