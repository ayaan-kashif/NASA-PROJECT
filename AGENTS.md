# Instructions for Codex, Antigravity (and any other agent)

1. Read `CLAUDE.md` (the project brief and architecture rules — they apply to you too).
2. Read `docs/COORDINATION.md` (who owns what, how to drive the open Unity Editor), `docs/PROGRESS.md` (what exists) and `docs/AGENT_PROMPTS.md` (your task).
3. The prompt pack is `docs/PLAN.md` (Prompts 0–18). Do one prompt at a time, verify with `tools/agent/ucompile.sh` and `tools/agent/utest.sh`, then **commit locally**.
4. **Do NOT push to GitHub** (no `git push`, no `gh`). Never rebase, reset or rewrite history. Claude audits your work and pushes it.
5. Only edit files you own per `docs/AGENT_PROMPTS.md`; stage only your own files (never `git add -A`).
