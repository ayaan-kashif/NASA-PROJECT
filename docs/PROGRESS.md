# T-MINUS progress log

Newest entries at the bottom. Each entry: what was done, how to test it, what changed for other tracks.

## P0 + P1 — Brief and project bootstrap (2026-11-12)
- `CLAUDE.md` (Prompt 0) added; full plan copied to `docs/PLAN.md`. Folders, one .asmdef per Scripts subfolder, Editor + EditMode test assemblies. Packages added: Newtonsoft JSON 3.2.2, Splines 2.9.1, Test Framework 1.8.0.
- Core (TMinus.Core): `GameState` (self-creating singleton), `DataRegistry` (tolerant JSON loader), `EventLog`, `SceneFlow` + `SceneFader` (0.3 s fade), `UiDocs` (runtime UIDocument helper). Main menu in UI Toolkit; other scenes are stubs with a name banner and Menu / Next buttons (`SceneLabel`, remove it when your scene gets real UI).
- Test: Tools > T-Minus > Rebuild Scenes (runs automatically the first time), open `Assets/_TMinus/Scenes/Boot.unity`, Play. Menu shows; Missions -> MissionBriefing; Next walks the loop. Console: "Loaded 0 parts, 0 rockets, 0 missions". Window > General > Test Runner > EditMode: all green.

### Notes for every track
- Do not add an EventSystem to your scene: `InputBootstrap` creates one persistent EventSystem (Input System UI module). The project uses the new Input System only.
- Make UI with `UiDocs.Create(...)` or a UIDocument using `Resources/UI/TMinusPanelSettings`.
- `GameState.Instance.Data` / `.Events` are available in any scene, even when you press Play directly in it.

## P2 — Game data (2026-11-12)
- `Resources/Data/`: rockets.json (Pathfinder training rocket + real Saturn V stages), parts.json (16 CubeSat parts, 7 Pathfinder parts, 11 avionics devices, 5 Saturn V parts, every port listed), rules.json (cable types, forbidden/required connections, assembly order, 5 bolt joints, pyro channels 1-4, Safe/Arm, 11 required avionics cables with flight consequences, launch sites), missions.json (FloodWatch-1 + 4 P1 missions), codex.json (28 entries). Game-balance values carry `"illustrative": true`; uncertain facts carry `needsReview`.
- Typed classes in `TMinus.Core.Data` (DataDefs.cs, RulesDefs.cs). Ports: id like `PWR_OUT_1`, type like `PWR_OUT`; the Blender/procedural child is `PORT_<id>`. Rocket thrust values are stage totals. Saturn V liftoff = 2,863,000 kg, TWR 1.18.
- Tests: DataIntegrityTests (references, ports, Saturn V mass, codex limits) — 14/14 pass. Agent helper `AgentBridge` (Editor): Temp/agent_refresh.flag, agent_tests.flag, agent_menu.flag drive the open Editor; results in Logs/agent-*.txt.
