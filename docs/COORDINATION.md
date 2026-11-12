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
- Claude: P9 DONE (2026-11-13).
- P11 (Flight scene + HUD): WORK IN PROGRESS, committed so it isn't lost. Builds and compiles; scene built via Tools > T-Minus > Build Flight Scene. KNOWN BUG to fix first: in play mode `FlightDebug.Launch` throws a NullReferenceException right after `FlightDebug.Reload` (FlightController.Instance or its hud is null after the scene reload; the IGNITION button path is untested too). Repro: `bash tools/agent/uplay.sh tools/agent/example_flight.txt`. Then check screenshots in Logs/agent-shots/.
- Open after P11: P12 (operations + scoring + debrief, reads GameState.Flight / FlightRecord), P13-P18.
- 2026-11-13 split (see docs/AGENT_PROMPTS.md): Codex = P11 fix + P12 + README/credits (+ P18 if time) on main; Antigravity = P13 + P14 + P16 on branch houston (own clone). Agents commit locally only and never push; Claude audits, merges houston and pushes.
- Antigravity: P13, P14, P16 DONE (2026-11-13).
- Codex: P10 DONE (implemented by Codex, verified + committed by Claude 2026-11-13).

## Shared changes log
- (append: who, file, what, why)
- Codex, Core/FlightContracts.cs (IAvionicsReport, IJointStatus + Perfect stubs), AvionicsReport/JointStatus implement them; rockets.json stage 2 = 50 kN; rules.json joint maxQkPa = 52.7931 (1.3 x measured max-Q 40.61 kPa).
- Claude, Core: DesignCode.cs, LaunchRecord (Designs.cs) + GameState.Launch. Editor: AgentBridge menu items Tools/T-Minus/Agent/Report Editor State + Stop Play Mode (a play test left running blocks every flag).
- Claude, Core/AvionicsReport.cs (new) + SceneNames.AvionicsBay: avionics consequences for FlightSim. Use `AvionicsReport.Compute(GameState.Instance.Vehicle, rules)`; `AvionicsReport.Perfect()` for Saturn V.
- Claude, Scripts/Wiring asmdef now references TMinus.Assembly (to reuse OrbitCamera).
- Codex, Core/FlightContracts.cs plus AvionicsReport/BoltMath implementations: additive flight-facing build-state contracts for avionics and integrity.
- Codex, Resources/Data/rockets.json and rules.json: Pathfinder stage 2 tuned to 50 kN; joint limits set to 1.3 x measured 40.610 kPa max-Q.
- Antigravity, Tests/EditMode/TMinus.Tests.EditMode.asmdef: added TMinus.Houston and TMinus.EditorTools references for Houston and Import tests.
- Note: screenshots in uplay.sh need the Unity window in front; if another app holds focus the shots are skipped. Just rerun.

## Rules
1. Until the final audit: commit locally after each passing prompt, NEVER push (Claude pushes after the audit). Never force-push or rewrite history.
2. Never edit a Unity scene you don't own. Scenes are rebuilt by Editor menu builders (`Tools/T-Minus/...`), so write a builder instead of hand-editing.
3. If a `.unity`, `.asset` or `.meta` file conflicts: keep the owner's version and rerun the builder.
4. Only one agent drives the open Unity Editor at a time (flag files below). Check `Logs/agent-status.txt`; if a script is running (`Logs/agent-play.txt` without "finished"), wait.
5. Log each prompt in `docs/PROGRESS.md` (3 lines).
6. **We share ONE working tree.** Never `git add -A` / `git add .` / `git commit -a`: stage only the files you wrote (`git add <paths>`), check `git diff --cached --stat` before committing. Never `git checkout --`, `git stash`, `git reset` or `git clean` files you didn't write.
7. Unity compiles the whole project, so the other agent's half-written files can break the build for a minute. If `ucompile.sh` shows errors only in the other agent's files, wait and retry; don't edit their files. Commit your own files only when the project compiles.

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
