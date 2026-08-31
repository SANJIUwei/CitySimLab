# VS Code GitHub Sync Notes

This document is for Codex agents. It records the intended GitHub sync route for this project.

## Rule

Use VS Code's built-in Git/GitHub integration as the primary UI path. Do not use GitHub Desktop.

## Verified Local Facts

- VS Code is installed at `F:\Microsoft VS Code`.
- `code --version` returned `1.135.0`.
- Built-in extension directory exists under `F:\Microsoft VS Code\08d4889f9e\resources\app\extensions`.
- Built-in extensions include `git`, `git-base`, `github`, `github-authentication`, and `microsoft-authentication`.
- The built-in `github` extension contributes command `github.publish`.
- The local `code` CLI does not expose a reliable `--command` option for invoking `github.publish` headlessly.

## Expected Publish Flow

1. Open the repository in VS Code:

```powershell
code E:\Myself\CitySimLab
```

2. In VS Code, use one of:

- Source Control panel: `Publish Branch` / `Publish to GitHub`
- Command Palette: `GitHub: Publish to GitHub`

3. User approves GitHub authentication and visibility. Default recommendation: private repository.
4. After publish, Codex checks:

```powershell
git remote -v
git status --short
git branch -vv
```

5. If needed:

```powershell
git push -u origin main
```

## Fallbacks

- Existing empty GitHub repo URL from the user.
- Project-specific token from the user.
- Working `gh auth login` flow.

Do not reuse, open, or inspect GitHub Desktop.
