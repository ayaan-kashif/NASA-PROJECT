# Prompts for two parallel agents: Codex + Antigravity (2026-11-13)

Each agent works in its **own folder + branch + Unity window**. **Nobody pushes to GitHub.** Agents only commit locally; Claude audits both folders, merges `houston` into `main`, and pushes after the audit.

| Agent | Folder | Branch | Work |
|---|---|---|---|
| Codex | `Desktop\NASA PROJECT` (existing) | `main` | Fix P11, then P12 (flight → operations → debrief), then README + credits + submission checklist |
| Antigravity | `Desktop\NASA-PROJECT-houston` (new clone) | `houston` | P13 + P14 (HOUSTON advisor + LLM proxy), then P16 (Blender import pipeline) |

Setup for the new clone (run once, in Git Bash):
```
cd ~/OneDrive/Desktop
git clone https://github.com/ayaan-kashif/NASA-PROJECT.git NASA-PROJECT-houston
```
Open that folder in Unity Hub (Add project from disk), let it import, and keep its Unity window open while Antigravity works.

---

## Common rules (paste into both prompts)

```
Before anything: read AGENTS.md, CLAUDE.md, docs/COORDINATION.md, docs/PROGRESS.md and the prompt(s) you own in docs/PLAN.md.
- Work only in YOUR folder and YOUR branch. Do NOT push to GitHub (no git push, no gh). Commit locally only; Claude reviews and pushes after the audit.
- Only edit the files/folders you own (listed in your prompt). If you must touch Scripts/Core, Resources/Data or an asmdef, keep it small and additive and write one line under "Shared changes log" in docs/COORDINATION.md.
- Never `git add -A`; stage only your files. Commit locally after every passing step, prompt number in the message. Never push, rebase, reset, or rewrite history.
- Unity must be open on YOUR folder. Verify with: bash tools/agent/ucompile.sh (no "error CS"), bash tools/agent/utest.sh (all PASS). Play tests: bash tools/agent/uplay.sh <script>, then look at Logs/agent-shots/*.png.
- If the bridge stops reacting, Unity is probably stuck in Play mode: run  printf 'Tools/T-Minus/Agent/Stop Play Mode' > Temp/agent_menu.flag  (keep the Unity window in front for a few seconds). Never leave Play mode running when you finish.
- Never run Unity in batch mode on a folder whose Editor is open.
- No API keys in code or git. No paid assets. No NASA logos.
- When done, append 3 lines per prompt to docs/PROGRESS.md and mark DONE in docs/COORDINATION.md (Claims).
```

---

## 1. Codex (folder `NASA PROJECT`, branch `main`)

```
You are continuing T-MINUS (NASA Space Apps 2026, Unity 6000.6.8f1) in C:\Users\AYAAN KASHIF\OneDrive\Desktop\NASA PROJECT on branch main. <paste the Common rules here>

You own: Scripts/Flight/**, Scripts/Mission/Ops*, Scoring*, Debrief*, DesignCompare*, scenes Flight + Debrief, Editor/FlightSceneBuilder.cs, Editor/Debrief*.cs, UI/USS/Flight.uss + Debrief*, Tests/EditMode/Flight/**, Tests/EditMode/Ops*, README.md, CREDITS.md, docs/SUBMISSION.md.

Your job, in order:
1. Finish Prompt 11 (Flight scene + HUD). It is committed as WIP. Known bug: in play mode, `TMinus.Flight.FlightDebug.Launch` throws a NullReferenceException right after `FlightDebug.Reload` (FlightController.Instance, its hud or Sim is null after the scene reload; also check the IGNITION button path). Repro: bash tools/agent/uplay.sh tools/agent/example_flight.txt. Fix the root cause, then make Prompt 11's acceptance checks pass: the perfect demo build (BriefingDebug.Demo perfect) reaches orbit and deploys the CubeSat; the "joint" demo build fails at max-Q with joint_failure; an unwired separation cable gives stuck_stage. Look at the screenshots and fix anything that looks wrong (camera framing, Earth, exhaust, HUD layout). Also add pad hold-down to FlightSim: while on the pad with thrust < weight the rocket must not fall through the ground.
2. Prompt 12 (operations, scoring, debrief), scene Debrief, namespace TMinus.Mission. Inputs: GameState.Flight (FlightRecord), GameState.Spacecraft + SpacecraftBudget.Compute(...) (power, battery, data, pointing, GSD), GameState.Launch (known failures, NO-GO rounds -> decision quality), GameState.Settings.hintsUsed, the EventLog, missions.json scoring weights and seeded random events. Science reward: the Lake Manchar Landsat images; if they are not in the repo, show a placeholder with the credit line from codex.json and leave a clear TODO. Write Tools > T-Minus > Build Debrief and debug hooks like the other scenes. Debrief buttons: Redesign (MissionBriefing), Fly again (Flight), Save design code, Leaderboard (local top 10).
3. README.md for judges (what it is, the challenge "Space Mission Design Game", how to play, controls, scenes, tech, NASA data used with links, credits incl. "NASA Earth Observatory images by Joshua Stevens, using Landsat data from the U.S. Geological Survey." and "Saturn V model: NASA/Michael D. Carbajal" if used, AI tools used and how, known limits: 2D flight plane, simple atmosphere, illustrative part values). CREDITS.md. docs/SUBMISSION.md = checklist from the "Demo and pitch" section of docs/PLAN.md.
4. If time remains: Prompt 18 Judge Mode (use Mission.DemoBuilds.Apply with missingRf / badJoint), then a design-code compare panel (Scripts/Mission/DesignCompare*.cs using Core/DesignCode + SpacecraftBudget).
Keep main compiling and green at all times. Commit locally; do not push.
```

## 2. Antigravity (folder `NASA-PROJECT-houston`, branch `houston`)

```
You are working on T-MINUS (NASA Space Apps 2026, Unity 6000.6.8f1) in C:\Users\AYAAN KASHIF\OneDrive\Desktop\NASA-PROJECT-houston. First: git checkout -b houston (local branch only, do not push). <paste the Common rules here>

You own: Assets/_TMinus/Scripts/Houston/** (namespace TMinus.Houston), a HOUSTON overlay + its UXML/USS (UI/UXML/Houston*, UI/USS/Houston*), proxy/** (outside Assets), Tests/EditMode/Houston*; for P16 also Editor/Import*.cs, Editor/ModelValidator*.cs, Art/Models/**, Prefabs/Parts/**, Scripts/Assembly/PrefabPartProvider.cs, Tests/EditMode/Import*. Do not edit other scenes; add HOUSTON to them only through a runtime spawner (e.g. [RuntimeInitializeOnLoadMethod] that creates the overlay in IntegrationBay, VAB, AvionicsBay, MissionBriefing and Debrief).

Do, in order:
1. Prompt 13 AssemblyAdvisor (deterministic, offline, never wrong). Reuse what exists: AssemblyGraph/AssemblyRules (TMinus.Assembly), BoltJointState.NextBolt() / BoltMath.StarOrder (Core), WiringGraph/WiringValidator (TMinus.Wiring), SpacecraftBudget/SpacecraftReport, AvionicsReport, PrelaunchChecklist (TMinus.Mission), rules.json, codex.json. The scenes already raise `HoustonBus.Ask(partId, context)` (Core) from "Ask HOUSTON" buttons: subscribe to it. Implement WhereDoesThisGo, NextStep, Scan, BoltCoach, the hint ladder (count hints in GameState.Settings.hintsUsed), the panel (H toggles, N next step, Shift+S scan), proactive tips at most one per 20 s. Find live scene objects through the existing singletons: VabController.Instance, BayController.Instance, AvionicsBayController.Instance, BriefingUI.Instance.
2. Prompt 14 HoustonLLMClient + Vercel proxy exactly as in the plan (endpoint from StreamingAssets/houston.config.json, which is git-ignored; houston.config.example.json already exists). Offline fallback from codex text with an "offline" tag. Never put a key in the repo.
3. Prompt 16 (Blender import pipeline): AssetPostprocessor for Art/Models/**/*.fbx (scale 1, keep empties, colliders), auto prefab in Prefabs/Parts/<part id>.prefab with Part + socket registration (SNAP_*, BOLT_##, PORT_*, PIPE_*), material remap by name to Resources/TMinusMaterials, a runtime Scripts/Assembly/PrefabPartProvider.cs that sets the existing ProceduralPartFactory.PrefabProvider hook (do not edit the factory itself), and Tools > T-Minus > Validate Models. Test it with a tiny test model if no Blender file exists yet.
Acceptance checks are in the plan. Commit locally on branch houston only; do not push.
```
