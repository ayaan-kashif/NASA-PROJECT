# Prompts for three parallel agents (2026-11-13)

Each agent works in its **own clone + branch + Unity window**. Claude merges the branches in the final audit.

| Agent | Folder | Branch | Work |
|---|---|---|---|
| Codex | `Desktop\NASA PROJECT` (existing) | `main` | Fix P11, then P12 (golden path: flight → operations → debrief) |
| Antigravity | `Desktop\NASA-PROJECT-houston` (new clone) | `houston` | P13 + P14 (HOUSTON advisor + LLM proxy) |
| Grok | `Desktop\NASA-PROJECT-ship` (new clone) | `ship` | P16 (Blender import pipeline), README + credits, design-code compare |

Setup for the two new clones (run once, in Git Bash):
```
cd ~/OneDrive/Desktop
git clone https://github.com/ayaan-kashif/NASA-PROJECT.git NASA-PROJECT-houston
git clone https://github.com/ayaan-kashif/NASA-PROJECT.git NASA-PROJECT-ship
```
Open each folder in Unity Hub (Add project from disk), let it import, and keep that Unity window open while its agent works.

---

## Common rules (included in every prompt below)

```
Before anything: read AGENTS.md, CLAUDE.md, docs/COORDINATION.md, docs/PROGRESS.md and the prompt(s) you own in docs/PLAN.md.
- Work only in YOUR folder and YOUR branch. Never push to another agent's branch. Only Codex pushes to main.
- Only edit the files/folders you own (listed in your prompt). If you must touch Scripts/Core, Resources/Data or an asmdef, keep it small and additive and write one line under "Shared changes log" in docs/COORDINATION.md.
- Never `git add -A`; stage only your files. Never force-push. Commit + push after every passing step, prompt number in the message.
- Unity must be open on YOUR folder. Verify with: bash tools/agent/ucompile.sh  (no "error CS"), bash tools/agent/utest.sh (all PASS). For play tests: bash tools/agent/uplay.sh <script> and look at Logs/agent-shots/*.png.
- If the bridge stops reacting, Unity is probably stuck in Play mode: run  printf 'Tools/T-Minus/Agent/Stop Play Mode' > Temp/agent_menu.flag  (keep the Unity window in front for a few seconds). Never leave Play mode running when you finish.
- Never run Unity in batch mode on a folder whose Editor is open.
- No API keys in code or git. No paid assets. No NASA logos.
- When done, append 3 lines per prompt to docs/PROGRESS.md and mark DONE in docs/COORDINATION.md (Claims).
```

---

## 1. Codex (main folder, branch `main`)

```
You are continuing T-MINUS (NASA Space Apps 2026, Unity 6000.6.8f1) in C:\Users\AYAAN KASHIF\OneDrive\Desktop\NASA PROJECT on branch main. <paste the Common rules here>

Your job, in order:
1. Finish Prompt 11 (Flight scene + HUD). It is committed as WIP. Known bug: in play mode, `TMinus.Flight.FlightDebug.Launch` throws a NullReferenceException right after `FlightDebug.Reload` (FlightController.Instance, its hud or Sim is null after the scene reload; also check the IGNITION button path). Repro: bash tools/agent/uplay.sh tools/agent/example_flight.txt. Fix the root cause, then make the acceptance checks of Prompt 11 pass: the perfect demo build (BriefingDebug.Demo perfect) reaches orbit and deploys the CubeSat; the "joint" demo build fails at max-Q with joint_failure; an unwired separation cable gives stuck_stage. Look at the screenshots and fix anything that looks wrong (camera framing, Earth, exhaust, HUD layout).
   Also add pad hold-down to FlightSim: while on the pad with thrust < weight the rocket must not fall through the ground.
2. Prompt 12 (operations, scoring, debrief) — scene Debrief, namespace TMinus.Mission (files Scripts/Mission/Ops*, Scoring*, Debrief*). Inputs: GameState.Flight (FlightRecord), GameState.Spacecraft + SpacecraftBudget.Compute(...) (power, battery, data, pointing, GSD), GameState.Launch (known failures, NO-GO rounds for decision quality), GameState.Settings.hintsUsed, the EventLog, missions.json scoring weights and seeded random events. Science reward: the Lake Manchar Landsat images — if they are not in the repo yet, show a placeholder with the credit line from codex.json and leave a clear TODO. Write a scene builder (Tools > T-Minus > Build Debrief) and debug hooks like the other scenes.
3. Keep main compiling and green at all times; the other agents will merge into it tomorrow.
```

## 2. Antigravity (clone NASA-PROJECT-houston, branch `houston`)

```
You are working on T-MINUS (NASA Space Apps 2026, Unity 6000.6.8f1) in C:\Users\AYAAN KASHIF\OneDrive\Desktop\NASA-PROJECT-houston. First: git checkout -b houston, and push that branch. <paste the Common rules here>

You own: Assets/_TMinus/Scripts/Houston/** (namespace TMinus.Houston), a HOUSTON overlay prefab + its UXML/USS (UI/UXML/Houston*, UI/USS/Houston*), proxy/** (outside Assets), Tests/EditMode/Houston*. Do not edit other scenes; add HOUSTON to them only through a runtime spawner (e.g. [RuntimeInitializeOnLoadMethod] that creates the overlay in IntegrationBay, VAB, AvionicsBay, MissionBriefing, Debrief).

Do Prompt 13 then Prompt 14 from docs/PLAN.md:
- P13 AssemblyAdvisor (deterministic, offline, never wrong). Use what already exists: AssemblyGraph/AssemblyRules (TMinus.Assembly), BoltJointState.NextBolt()/BoltMath.StarOrder (Core), WiringGraph/WiringValidator (TMinus.Wiring), SpacecraftBudget/SpacecraftReport, AvionicsReport, PrelaunchChecklist (TMinus.Mission), rules.json, codex.json. The scenes already raise `HoustonBus.Ask(partId, context)` (Core) from "Ask HOUSTON" buttons — subscribe to it. Implement WhereDoesThisGo, NextStep, Scan, BoltCoach, the hint ladder (count hints in GameState.Settings.hintsUsed), the panel (H toggles, N next step, Shift+S scan), proactive tips max 1 per 20 s. To find live scene objects use the existing singletons: VabController.Instance, BayController.Instance, AvionicsBayController.Instance, BriefingUI.Instance.
- P14 HoustonLLMClient + Vercel proxy exactly as in the plan (endpoint from StreamingAssets/houston.config.json, which is git-ignored; houston.config.example.json already exists). Offline fallback from codex text with an "offline" tag. Never put a key in the repo.
Acceptance checks are in the plan. Commit and push to branch houston only.
```

## 3. Grok (clone NASA-PROJECT-ship, branch `ship`)

```
You are working on T-MINUS (NASA Space Apps 2026, Unity 6000.6.8f1) in C:\Users\AYAAN KASHIF\OneDrive\Desktop\NASA-PROJECT-ship. First: git checkout -b ship, and push that branch. <paste the Common rules here>

You own: Assets/_TMinus/Editor/Import*.cs, Assets/_TMinus/Editor/ModelValidator*.cs, Assets/_TMinus/Art/Models/**, README.md, CREDITS.md, docs/SUBMISSION.md, Tests/EditMode/Import*. You may add one small hook: ProceduralPartFactory.PrefabProvider already exists (TMinus.Assembly) — set it from a runtime script you own (e.g. Scripts/Assembly/PrefabPartProvider.cs) instead of editing the factory.

Do:
1. Prompt 16 (Blender import pipeline) from docs/PLAN.md: AssetPostprocessor for Art/Models/**/*.fbx (scale 1, keep empties, colliders), auto prefab in Prefabs/Parts/<part id>.prefab with Part + socket registration (names SNAP_*, BOLT_##, PORT_*, PIPE_*), material remap by name to Resources/TMinusMaterials, PrefabPartProvider so the VAB uses a prefab when one exists, and Tools > T-Minus > Validate Models window. Test it with a tiny generated test FBX or a mesh you create in code if no Blender file exists yet.
2. README.md for judges (what it is, the challenge "Space Mission Design Game", how to play, controls, scenes, tech, NASA data used with links, credits incl. "NASA Earth Observatory images by Joshua Stevens, using Landsat data from the U.S. Geological Survey." and "Saturn V model: NASA/Michael D. Carbajal" if used, AI tools used and how, known limits: 2D flight plane, simple atmosphere, illustrative part values). CREDITS.md. docs/SUBMISSION.md = checklist from the "Demo and pitch" section of docs/PLAN.md.
3. If time remains: design-code compare panel (Prompt 9 P1 item) as a separate script in Scripts/Mission/DesignCompare*.cs using Core/DesignCode + SpacecraftBudget.
Commit and push to branch ship only.
```
