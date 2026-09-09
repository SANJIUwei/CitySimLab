# VS Code GitHub Sync Notes

This document is for AI. Keep it short.

## Current Setup

- Remote: `https://github.com/SANJIUwei/CitySimLab.git`
- Branch: `main`
- Tracking: `main -> origin/main`
- First publish was completed through VS Code's built-in Git/GitHub UI.
- Future sync should normally use plain Git commands.

## Normal Flow

```powershell
git status --short --branch
git pull --ff-only
tools\verify.ps1
git push
```

## Rules

- Git may be managed with plain Git commands, VS Code Git/GitHub UI, or GitHub Desktop.
- Prefer the client that already has write access to this repository.
- If auth fails, try another allowed Git client, then ask the user for a project-specific auth method.
