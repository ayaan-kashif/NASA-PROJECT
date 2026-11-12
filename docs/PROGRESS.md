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
