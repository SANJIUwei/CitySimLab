# AI Operating Notes

First file after clone or pull. Engineering rules: `docs/ai/AGENT_PROTOCOL.md`. Doc style: `docs/ai/DOCUMENTATION_STYLE.md`. Index: `docs/README.md`.

## Roles

- User: direction, decisions, review, stop or change requirements.
- AI: architecture, implementation, tests, verification, docs, coupling checks, roadmap navigation.
- Do not ask the user to write or paste code.
- Record design thoughts in Simplified Chinese.
- Batch tests per `docs/ai/06-when-to-test.md`. Roles for the user: `docs/human/分工.md`.

## Discuss vs Execute

Analysis only: 看看 / 分析一下 / 你觉得呢 / 研究一下 / 给建议 / 解释一下.

Change the repo only when asked: 实现 / 修改 / 重构 / 修复 / 加上 / 删除 / 提交 / 推送.

## Session Start

1. `git status --short --branch`.
2. If the tree is clean and a remote exists, `git pull --ff-only`.
3. Read `docs/ai/AI_HANDOFF.md`.
4. Teaching: `docs/ai/AI_TEACHING_PLAN.md`. Design continuity: `docs/ai/design-notes/`.
5. Read only the files needed for the current request.
6. Judge: current state → user goal → missing dependencies → affected modules → smallest change.
7. Run `tools\verify.ps1` when changing code.

## Session Close

Update `docs/ai/AI_HANDOFF.md` with location, completed work, files changed, commands, blockers, next step.

If code changed, run `tools\verify.ps1`. Then `git status --short`.

For requested implementation work, use the standing backup authorization in the Git section. Do not create empty commits or include unrelated changes.

## Architecture

- Core gameplay belongs in `src/CitySim.Core`. Core must not depend on Unity.
- `src/CitySim.Headless` is the no-Unity runner. `src/CitySim.UnityMock` is lifecycle learning only.
- `tests/CitySim.Tests` is the first validation layer.
- Business objects do not own threads, GPU, or scheduler internals.
- Do not add a real Unity project, server, or networking stack until the user asks.

## Git

- Remote: `https://github.com/SANJIUwei/CitySimLab.git`.
- Prefer `main`. Never reset, checkout, or delete changes you did not make unless asked.
- Keep build outputs, secrets, and Unity Library out of Git.
- The user authorizes a scoped commit and push to `origin/main` after each completed implementation work chunk, without asking again. Analysis-only tasks and empty changes produce no commit.
- Stage only files changed for the task. Stop and report if unrelated changes exist, the remote has diverged, or authentication fails. Never push secrets or generated outputs.
- This is a per-work-chunk backup cadence; no background timer or scheduled runner is configured.
