# Instructions for Codex (and any other agent)

1. Read `CLAUDE.md` (the project brief and architecture rules — they apply to you too).
2. Read `docs/COORDINATION.md` (who owns what, how to claim work, how to drive the open Unity Editor) and `docs/PROGRESS.md` (what exists).
3. The prompt pack is `docs/PLAN.md` (Prompts 0–18). Do one prompt at a time, verify with `tools/agent/ucompile.sh` and `tools/agent/utest.sh`, then commit and push.
4. Another agent (Claude Code) works in this folder at the same time. Only edit files you own per `docs/COORDINATION.md`; `git pull --rebase` before you start and before you push.

5. Until the user says otherwise: do NOT push to GitHub. Commit locally only; Claude audits and pushes.
