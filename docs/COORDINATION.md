# Two-agent coordination (Claude Code + Codex)

Both agents work in this same folder and push to `main`. The rules below keep us from overwriting each other.

## Ownership (who may edit what)

| Area | Owner | Folders / files |
|---|---|---|
| Prompts 8, 9 (avionics bay, mission briefing + checklist + GO/NO-GO) | **Claude** | `Scripts/Mission/Briefing*`, `Scripts/Wiring/Avionics*`, scenes `MissionBriefing`, `AvionicsBay` |
| Prompts 10, 11, 12 (flight physics, Flight scene + HUD, operations + scoring + debrief) | **Codex** | `Scripts/Flight/**`, `Scripts/Mission/Ops*`, `Scripts/Mission/Scoring*`, `Scripts/Mission/Debrief*`, scenes `Flight`, `Debrief`, `Tests/EditMode/Flight*`, `Tests/EditMode/Ops*` |
| Prompts 13, 14 (HOUSTON advisor + LLM proxy) | whoever finishes first; claim it below before starting | `Scripts/Houston/**`, `proxy/**` |
| Shared: `Scripts/Core/**`, `Resources/Data/*.json`, `CLAUDE.md`, `AGENTS.md`, asmdefs | **ask first** | Add a line under "Shared changes" below, keep edits small and additive, never rename public APIs |

If Claude stops (weekly limit), Codex may take over Claude's rows: write "Codex took over P8/P9" under Claims first.

## Claims (append one line before starting a prompt, mark DONE after pushing)
- Claude: P0–P7 DONE.
- Claude: P8 DONE (2026-11-12).
- P9 (mission briefing, checklist, GO/NO-GO): owned by Claude but NOT started. Codex may take it if Claude is out of quota: write "Codex took over P9" here first.
- Codex: P10–P12 claimed if you agree — write your line here.

## Shared changes log
- (append: who, file, what, why)
- Claude, Core/AvionicsReport.cs (new) + SceneNames.AvionicsBay: avionics consequences for FlightSim. Use `AvionicsReport.Compute(GameState.Instance.Vehicle, rules)`; `AvionicsReport.Perfect()` for Saturn V.
- Claude, Scripts/Wiring asmdef now references TMinus.Assembly (to reuse OrbitCamera).
- Note: screenshots in uplay.sh need the Unity window in front; if another app holds focus the shots are skipped. Just rerun.

## Rules
1. `git pull --rebase` before each prompt; commit + push after each passing prompt ("P10: flight physics core"). Never force-push.
2. Never edit a Unity scene you don't own. Scenes are rebuilt by Editor menu builders (`Tools/T-Minus/...`), so write a builder instead of hand-editing.
3. If a `.unity`, `.asset` or `.meta` file conflicts: keep the owner's version and rerun the builder.
4. Only one agent drives the open Unity Editor at a time (flag files below). Check `Logs/agent-status.txt`; if a script is running (`Logs/agent-play.txt` without "finished"), wait.
5. Log each prompt in `docs/PROGRESS.md` (3 lines).

## Driving Unity without MCP (works for both agents)
The Editor is open on this project. `Assets/_TMinus/Editor/AgentBridge.cs` and `AgentPlayScript.cs` watch flag files:
- `bash tools/agent/ucompile.sh` — refresh + compile, prints `error CS` lines (log: `Logs/Editor.log`).
- `bash tools/agent/utest.sh` — runs all EditMode tests, prints PASS/FAIL (`Logs/agent-tests.txt`).
- `bash tools/agent/umenu.sh "Tools/T-Minus/Build VAB Scene"` — runs a menu item.
- `bash tools/agent/uplay.sh tools/agent/example_vab_build.txt` — plays a scene and runs a script (`scene`, `wait`, `shot <name>`, `call Namespace.Type.StaticMethod arg`, `stop`). Screenshots land in `Logs/agent-shots/` (Unity window must be focused; the script focuses it). Add static hooks like `VabDebug` / `BayDebug` for your scene.

## Where things stand
See `docs/PROGRESS.md`. Key APIs for flight/ops:
- `BoltJointState.AllStatuses(rules, GameState.Instance.Vehicle)` → per-joint integrity and `FailureQkPa`.
- `SpacecraftBudget.Compute(design, data, mission, rocket)` → `SpacecraftReport` (power, battery, data, pointing, GSD).
- `GameState.Instance.Spacecraft.cables` / `Vehicle.avionicsCables` + `WiringGraph`/`WiringValidator` for wiring state.
- `rules.json` `avionicsWiring[].consequence` codes (no_ignition, stuck_stage, s2_no_thrust, guidance_drift, no_telemetry, fairing_stuck) are what FlightSim should react to; Prompt 8 will expose them as `AvionicsReport` in `TMinus.Core`.
