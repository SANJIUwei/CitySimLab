# VS Code GitHub Sync Notes

This document is for Codex agents. Keep it short.

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

- Do not use GitHub Desktop.
- Use VS Code Git/GitHub UI only if normal Git auth breaks.
- If auth fails, ask the user for a project-specific auth method.
