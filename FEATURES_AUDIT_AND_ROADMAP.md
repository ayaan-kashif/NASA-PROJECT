# Features Audit + Roadmap

Audit date: 14 November 2026. First audited at `f14062b`; updated for `8732835` (branch `main`, see "Changes since the first audit" below).
Method: every feature below was found by reading the C# scripts, UXML, JSON data and the proxy source. Nothing was run: Unity was not opened, no build was played. Items that need a runtime check are marked **(unverified at runtime)**.


### Implementation status (updated after the fixes were written)

**Final status (14 November 2026): every item in this file now has code behind it, and every checklist box is ticked.** Several are smaller or different in form than first described (the school licence is the pilot-offer page, the class dashboard is a browser page that reads pasted CSV, live crew uses a free public relay, touch is experimental, the share card is text). Two of the new missions, Moonshot and Rehbar-I, were tuned with a JavaScript copy of the flight solver in `tools/sim`. See the status section of `COMPLETE_PROJECT_AUDIT.md` for the details.

**These changes have not been compiled, tested or played**: no Unity was available. See the last entry in `docs/PROGRESS.md` and the status note in `COMPLETE_PROJECT_AUDIT.md`.

---

## 1. What This App Currently Does

T-MINUS is a Unity 6 (URP, UI Toolkit) educational game for the NASA Space Apps Challenge. One mission is fully playable: **FloodWatch-1**. The player builds a 3U CubeSat, wires it, stacks and bolts a two-stage rocket, wires its avionics, runs a pre-launch checklist with a crew GO/NO-GO poll, flies a physics ascent, then gets 30 simulated days of operations and a scored debrief with real Landsat images. A rule-based assistant (HOUSTON) gives next-step advice, with optional AI explanations through a Vercel proxy.

**Total features found: 46** (28 Complete, 8 Needs Improvement, 7 Half-Built, 3 Broken).

The engineering core is strong: data-driven JSON, a pure C# double-precision flight solver, 91 EditMode test methods (the team's latest run reports 97 pass + 1 skip, counting parameterised cases), and a causal event log. The weak points are around it: **no save game, no tutorial, several consequences that the UI promises but the simulation never applies, and a scoring model with parts that cannot vary.**

### Changes since the first audit

One commit was pulled: `8732835` by Ayaan Kashif, "Web build: fall back to plain rendering when post-processing is unsupported".

| File | Change | Effect on this audit |
|---|---|---|
| `Scripts/Core/SceneLook.cs:47-60` | If `Hidden/Universal Render Pipeline/UberPost` is missing or unsupported, the look volume is removed and post-processing, NaN-stop and SMAA are switched off | Fixes the black 3D view in the browser. New row 46 in the inventory |
| `Settings/UniversalRenderPipelineGlobalSettings.asset` | Post-processing shader stripping turned off | Fixes the cause; adds shader variants to every build (see I16) |
| `Editor/WebGLBuilder.cs:36-46` | Build WebGL forces the two strip flags off and saves the asset | Same fix, applied on every web build |
| `docs/PROGRESS.md` | New log entry | Records the Space Guide rename and other fixes already in `f14062b`, which this audit had already read |

No gameplay, data, scoring, HOUSTON, proxy or test files changed. None of the P0 issues below is fixed or made worse by this commit.

---

## 2. Current Features - Full Inventory

| # | Feature Name | Where in Code | Status | What is Missing |
|---|---|---|---|---|
| 1 | Boot + scene flow with fade | `Core/BootLoader.cs`, `Core/SceneFlow.cs`, `Core/SceneFader.cs` | Complete | No loading indicator for slow loads (WebGL) |
| 2 | Main menu with 3D backdrop | `UI/MainMenuController.cs`, `UI/MenuBackdrop.cs`, `UI/UXML/MainMenu.uxml` | Complete | Quit button does nothing in WebGL; no Continue button |
| 3 | Mission select | `Mission/BriefingUI.Missions.cs` | Half-Built | 1 of 3 real missions playable; Moonshot and Rehbar-I show "COMING NEXT" |
| 4 | Mission briefing (story, criteria, constraints) | `Mission/BriefingUI.cs:57-97` | Complete | Success criteria are shown as empty boxes and never ticked after the flight |
| 5 | Orbit choice 400/550/800 km | `BriefingUI.cs:104-118`, `Spacecraft/BudgetsPanel.cs` | Complete | Drag/lifetime trade-off is text only; not simulated |
| 6 | Rocket choice cards | `BriefingUI.cs:120-140` | Half-Built | Only Pathfinder is selectable for the only playable mission |
| 7 | Launch site choice (Sonmiani / Cape) | `BriefingUI.cs:142-158` | Complete | — |
| 8 | Crew of 1–4 with merged roles | `BriefingUI.cs:160-189`, `PrelaunchChecklist.cs:152-162` | Half-Built | Hot-seat only; roles only matter in the poll; score for roles is always full |
| 9 | Design codes (copy / load) | `Core/DesignCode.cs`, `BriefingUI.cs:191-211` | Needs Improvement | No validation of loaded ids; "copied" toast is wrong in WebGL |
| 10 | Integration Bay: install CubeSat parts | `Spacecraft/BayController.cs`, `Spacecraft/CubeSatModel.cs` | Complete | No undo; no confirm on Remove |
| 11 | FlatSat wiring bench (cables, polarity, short circuit) | `Wiring/WiringBench.cs`, `Wiring/WiringGraph.cs` | Complete | No undo; mouse only |
| 12 | Wiring validator + fault list | `Wiring/WiringValidator.cs`, `BayUI.cs:145-173` | Complete | — |
| 13 | Multimeter (continuity test) | `WiringBench.cs:273-283` | Complete | — |
| 14 | Live budgets (power, battery, data, pointing, mass, cost) | `Core/SpacecraftBudget.cs`, `Spacecraft/BudgetsPanel.cs` | Complete | — |
| 15 | Remove-Before-Flight pin | `BayController.cs:202-210`, `BriefingUI.Pad.cs:41-42` | Complete | — |
| 16 | VAB: drag-and-drop stacking with ghost and reasons | `Assembly/AssemblyController.cs`, `Assembly/AssemblyGraph.cs` | Complete | — |
| 17 | Undo / redo | `AssemblyController.cs:43-47` | Half-Built | Stacking only. Not for bolts, cables or CubeSat parts |
| 18 | Bolting: star order, 3 passes, torque gauge, warp, strip | `Assembly/BoltTool.cs`, `Core/BoltMath.cs` | Complete | — |
| 19 | Rocket library, Inspect mode, exploded view, X-ray | `Assembly/VabController.cs`, `Assembly/RocketView.cs`, `VabUI.cs` | Complete | Saturn V is inspect-only |
| 20 | Part info card + Learn more | `VabUI.cs:170-213` | Complete | — |
| 21 | Avionics Bay: 11 cables with flight consequences | `Wiring/AvionicsBayController.cs`, `Core/AvionicsReport.cs` | Complete | — |
| 22 | Safe/Arm plug | `AvionicsBayController.cs:96-123`, `PrelaunchChecklist.cs:123-128` | **Broken (misleading)** | Flight ignores it. The checklist says "Still SAFE: nothing can ignite", but the rocket ignites anyway |
| 23 | Pre-launch checklist with Fix buttons | `Mission/PrelaunchChecklist.cs`, `BriefingUI.Pad.cs:16-56` | Needs Improvement | Can be skipped: the VAB button "To launch pad" goes straight to Flight |
| 24 | GO / NO-GO poll | `BriefingUI.Pad.cs:60-91` | Complete | Same skip problem as #23 |
| 25 | Countdown | `BriefingUI.Pad.cs:102-128`, `FlightController.Initialization.cs:20-43` | Needs Improvement | Two countdowns in a row: one in the briefing, then another in Flight |
| 26 | Flight physics (RK4, drag, staging, max-Q, orbit) | `Flight/FlightSim.cs`, `Flight/FlightPhysics.cs`, `Flight/FlightGuidance.cs` | Complete | Payload mass is fixed at 150 kg; the CubeSat's real mass is not used |
| 27 | Autopilot | `FlightGuidance.cs:13-78` | Complete | Can stall forever if stage 2 runs dry in a stable low orbit **(unverified at runtime)** |
| 28 | Manual flight (A/D pitch, Shift/Ctrl throttle) | `FlightGuidance.cs:80-87`, `FlightController.cs:272-280` | **Broken** | Rocket never leaves the pad; orbit can never be detected |
| 29 | Flight HUD: telemetry, charts, event feed, orbit map, 3 cameras, time warp | `Flight/FlightHud.cs`, `Flight/FlightCharts.cs`, `Flight/FlightView.cs` | Complete | No pause; no Menu button |
| 30 | Failure causes in flight (joint, stuck stage, no ignition, guidance drift, fairing, LOS) | `FlightSim.cs:152-226` | Complete | — |
| 31 | 30-day operations simulation | `Mission/OpsSimulation.cs` | Half-Built | Only the battery→switch→EPS cables are checked. A missing antenna, camera or solar cable changes nothing |
| 32 | Scoring + stars | `Mission/Scoring.cs` | Needs Improvement | Team-roles score is always 100%; hint and NO-GO counters never reset |
| 33 | Debrief (score, ops, timeline, top fixes) | `Mission/DebriefUI.cs` | Needs Improvement | Operations events print as "T+2592000s"; no Main Menu button |
| 34 | Landsat reward gallery at the player's GSD | `Mission/RewardGallery.cs` | Complete | — |
| 35 | Design comparison panel | `Mission/DesignCompare.cs`, `DebriefUI.cs:61-83` | Complete | — |
| 36 | Local leaderboard | `DebriefUI.cs:104-118` | **Broken** | Each click adds a duplicate row; name is always "Player" |
| 37 | HOUSTON advisor (next step, where does this go, scan, show me, tips) | `Houston/AssemblyAdvisor.cs`, `Houston/HoustonPanel*.cs` | Complete | "Where does this go?" only reads the VAB selection |
| 38 | HOUSTON AI answers + Urdu | `Houston/HoustonLLMClient.cs`, `proxy/api/houston.ts` | Half-Built | No Arabic-script font in the project; proxy is open; debrief narration is written but never called |
| 39 | Failure Lab: Broken Pathfinder | `Mission/FailureLab.cs`, `Mission/FailureJobTracker.cs` | Needs Improvement | Overwrites the player's build without warning; score is not saved |
| 40 | Failure Lab: Apollo 12 console | `Mission/Apollo12Console.cs` | Complete | Score is not saved |
| 41 | Judge Mode (F9) | `Mission/JudgeMode.cs` | Needs Improvement | F9 anywhere overwrites the player's build without warning |
| 42 | Space Guide (28 topics, search, sources) | `UI/MenuPanels.cs:32-63`, `Resources/Data/codex.json` | Complete | 25 of 28 sources are Wikipedia; 3 are marked `needsReview` |
| 43 | Settings (HOUSTON mode, language, volume, fullscreen) | `UI/MenuPanels.cs:67-94`, `Core/SettingsStore.cs` | Needs Improvement | No graphics quality, text size, key list or reset |
| 44 | Sandbox room picker | `UI/MenuPanels.cs:98-114` | Complete | It is a scene picker, not a mode: the budget still applies |
| 45 | Sound effects | `Core/AudioFx.cs` | Half-Built | 8 generated blips. No engine sound, no music |
| 46 | Graphics fallback for browsers without post-processing (new in `8732835`) | `Core/SceneLook.cs:47-60` | Complete | Silent: the player is not told that bloom and grading are off; no manual Low-graphics option (see F10) |

### Hidden and half-built items found in code

- `HoustonLLMClient.AskDebrief` (`HoustonLLMClient.cs:127`) is never called. AI debrief narration was planned and dropped.
- HOUSTON "Helpful / Not helpful" buttons write counters to PlayerPrefs (`HoustonPanel.Actions.cs:179-184`). Nothing ever reads them.
- `MissionDef.seed`, `storyCard`, `targetApogeeKm`, `parkingOrbitKm` and `requiredDeltaVLeftMs` exist in the data classes (`DataDefs.cs:199-208`) and in `missions.json`. No code reads them.
- "Random events" are three fixed days (12, 20, 25) in `missions.json`. Every run is identical.
- `Assets/Scenes/SampleScene.unity` and `Assets/TutorialInfo/` are leftovers from the Unity template.
- `UI/SceneLabel.cs` is the old stub-scene banner. Check whether any scene still uses it.
- `AudioFx.cs:8` says "Replace with CC0 recordings in the polish pass (Prompt 17)". That pass did not happen.

---

## 3. Deep Audit of Each Feature

### 3.1 Save and progress (missing)
- **What works:** settings and the leaderboard persist in PlayerPrefs. A design code can be copied by hand.
- **What is broken:** the whole build lives in `GameState` in memory (`Core/GameState.cs:17-26`). Closing the game or refreshing the browser tab loses everything. A full build is roughly 30–60 minutes of bolting and wiring.
- **What is missing:** autosave, a Continue button, save slots, mission completion history, best score per mission.
- **How to improve:** the `DesignBundle` serializer already exists. Write it to PlayerPrefs on every design change and load it at start. See feature F1.

### 3.2 Onboarding (missing)
- **What works:** two toasts ("Click parts on the left…", "Drag the parts from the floor…") and HOUSTON's Next button.
- **What is missing:** any guided first run. A 12-year-old lands in a clean room with a parts list and a 3D frame. Controls are only in the README.
- **How to improve:** a 5-step guided first build driven by the existing `AssemblyAdvisor.NextStep`, plus a controls overlay on a key. See F2 and F6.

### 3.3 Core loop flow
- **What works:** Briefing → Bay → VAB → pad checklist → poll → Flight → Debrief all exist.
- **What is broken:**
  - The VAB's main button "To launch pad >" calls `SceneFlow.Next` (`VabUI.cs:63`), which loads Flight directly (`SceneFlow.cs:29-33`). The checklist and the poll live in the briefing scene and are skipped. The player then launches with the RBF pin in and learns in the debrief that the satellite "never powered on".
  - The briefing runs a T-10 countdown that ends in "LIFTOFF!" (`BriefingUI.Pad.cs:111-128`). The Flight scene then shows "TERMINAL COUNT HOLD · T-10 READY" and a second countdown (`FlightHud.cs:89-102`).
  - Flight auto-loads the debrief 7–9 seconds after the end card appears (`FlightController.cs:205`), even while the player is reading it.
- **How to improve:** route "To launch pad" to the checklist; keep one countdown; remove the auto-advance timer.

### 3.4 Consequences that the game promises but does not apply
This is the most important design gap, because the pitch is "missions fail at component connections".
- **CubeSat wiring:** `OpsSimulation.cs:25-32` checks only battery → deployment switch → EPS. `SpacecraftBudget.Compute` never looks at cables. A satellite with the antenna unplugged, the camera unpowered and the solar panel disconnected still returns 87 images. The only cost is 2 points of "decision quality" per failed checklist item.
- **Safe/Arm:** no file under `Flight/` reads `Vehicle.armed`. A rocket left on SAFE ignites and separates normally.
- **Payload mass:** `FlightRuntime.cs:24` always flies 150 kg. A 2 kg and a 4 kg CubeSat fly the same.
- **Rocket stack:** Flight builds the rocket from `rockets.json`, not from what the player stacked. An unbuilt rocket appears complete, then fails with "j_s1_interstage failed above its 0.0 kPa limit" instead of "the rocket was never assembled".
- **How to improve:** feed `WiringValidator` results into the operations model (no RF cable = 0 MB downlink; no solar cable = 0 W; no camera data cable = 0 images). Make SAFE cause `no_ignition` with cause `av_safe_arm`.

### 3.5 Flight
- **What works:** the solver is clean and tested: RK4 at 0.05 s, pressure-scaled thrust, exponential atmosphere, rotating Earth, staging, max-Q, orbital elements. Failures carry the id of the cable or joint that caused them.
- **What is broken:** manual mode. Phase changes happen only inside `FlightGuidance.Autopilot` (`FlightGuidance.cs:31,40,59,70`). With autopilot off the phase stays `Pad`, so `HoldDownOnPad` (`FlightSim.cs:141-147`) pins the rocket to the ground while it burns all its fuel. If the player switches to manual in mid-air, the sim can never reach the `Orbit` phase, so the CubeSat never deploys.
- **What is missing:** pause, a Menu button, engine sound, a map of the ground track, any reason to fly manually.
- **How to improve:** move the phase logic out of the autopilot into the sim so both control modes share it. Until then, hide the Autopilot toggle.

### 3.6 Scoring and debrief
- **What works:** six weighted lines, stars, top-3 fixes, state-of-charge timeline, a real cause for each failure.
- **What is broken:**
  - "Team roles" = distinct roles ÷ 4 (`Scoring.cs:28`). `AssignRoles` always hands out all four roles, even to one player, so this is always 5/5.
  - `hintsUsed` and `noGoRounds` are never reset for a new attempt (`Scoring.cs:24-26`). Hints used on the first try lower every later score in the session.
  - The Leaderboard button appends the current score every time it is clicked (`DebriefUI.cs:106`). Three clicks = three identical rows.
  - Operations events are printed with flight formatting: "T+2592000s operations/operations_complete" (`DebriefUI.cs:49`).
- **What is missing:** success criteria ticked or crossed, flight charts (the telemetry is saved in `FlightRecord.telemetry` and never drawn), comparison with the previous attempt, a name field for the leaderboard.

### 3.7 HOUSTON
- **What works:** the offline advisor is the best-built system in the project. It is scene-aware, deterministic, tested, and gives a codex link and a "Show me" camera move.
- **What is broken or risky:**
  - **Urdu (unverified at runtime):** the repository contains no font file and `TMinusPanelSettings.asset` has no text settings. Urdu needs Arabic-script glyphs, right-to-left layout and letter joining. In WebGL there are no system fonts to fall back on. Test this before showing it to judges.
  - **Proxy:** `Access-Control-Allow-Origin: *`, no authentication, no limit on request size, and an in-memory rate limiter that resets on every cold start (`proxy/api/houston.ts:3-20,27,61-64`). Anyone who finds the URL can use the team's Gemini key as a free chatbot. The URL is printed in `proxy/README.md:87`.
  - `GET ?diag=models` is a public diagnostic endpoint (`houston.ts:39-49`).
  - No notice tells the player (age 12+) that typed questions are sent to a third-party AI service.
- **What is missing:** suggested question chips, answer history across scenes, AI debrief narration (written, not wired).

### 3.8 Failure Lab and Judge Mode
- **What works:** jobs are data-driven; the Apollo 12 console explains every choice.
- **What is broken:** both `FailureLab.Begin` (`FailureLab.cs:53`) and `JudgeMode.Start` (`JudgeMode.cs:81`) call `DemoBuilds.Apply`, which replaces the player's CubeSat and rocket. F9 works in every scene, so one accidental key press destroys the build. There is no confirmation and no backup.
- **What is missing:** saved job scores, a "completed" mark, more than two jobs.

### 3.9 Platform and accessibility
- **Works:** UI scales with screen size (1920×1080 reference). Colour is backed by patterns and text tags.
- **Missing:** touch input (the WebGL link will not work on phones or tablets, which is what many schools have), gamepad, key rebinding, text-size option, keyboard-only play, UI translation (all strings are hard-coded in C#).
- **WebGL rendering:** since `8732835`, browsers that cannot run URP post-processing get plain rendering instead of a black screen. This was the most serious web problem and it is now handled. It is untested on low-end school hardware **(unverified at runtime)**.
- **WebGL details:** the Quit button calls `Application.Quit`, which does nothing in a browser (`MainMenuController.cs:61-68`). "Copy my code" in the briefing reports success although browsers block the clipboard (`BriefingUI.cs:201-203`); the debrief handles this correctly.

---

## 4. Comparison With Competitors

Sources are listed at the end of this file. Ratings and complaint summaries come from store pages and review aggregators, not from playing each game.

| Capability | T-MINUS | Kerbal Space Program | Juno: New Origins | Spaceflight Simulator | Mars Horizon | PC Building Simulator 2 |
|---|---|---|---|---|---|---|
| Price | Free | Paid | Paid | Free mobile + paid packs | Paid | Paid |
| Runs in a browser | Yes | No | No | No | No | No |
| Bolt-level and cable-level assembly | **Yes** | No | No | No | No | Yes (screws, cables) |
| Satellite subsystem budgets (power, data, pointing) | **Yes** | Partial (mods) | Partial | No | Abstract | n/a |
| Explains the cause of each failure | **Yes** | No | No | No | Partial | Yes (diagnosis jobs) |
| Built-in assistant | **Yes (HOUSTON)** | No | No | No | No | No |
| Real orbital flight, manual control | Autopilot only (manual broken) | Yes, 3D | Yes, 3D | Yes, 2D | No (mini-games) | n/a |
| Guided tutorials | No | Yes (often criticised as thin) | Yes (interactive) | Minimal | Yes | Yes (first career levels) |
| Save game | **No** | Yes | Yes | Yes | Yes | Yes |
| Career / progression | No (1 mission) | Yes | Yes (tech tree, contracts) | No | Yes | Yes (30-hour career) |
| Number of missions / content | 1 + 2 jobs | Open-ended | Open-ended | Open-ended | Full Space Race | 30+ hours |
| Sharing builds | Text code | Craft files, mods | Online gallery | Blueprint sharing | No | No |
| Mobile / touch | No | No | Yes | Yes | Console | No |
| Classroom tools | No | KerbalEdu (discontinued) | No | No | No | No |
| Real mission data | Landsat, Blue Marble | No | No | No | ESA input | Real PC parts |

Two direct CubeSat titles also exist. **NASA Climate Kids: CubeSat Builder** is a light timed game with no budgets. **Earth Rising** (free on Steam, 2023) lets players configure a CubeSat from 20+ components and has a "Mixed" rating from about 33 reviews with a very small player base. Neither has bolt-level assembly, a launch, or a causal debrief.

### What competitors have that T-MINUS does not
1. Save and load (all of them).
2. Step-by-step tutorials (Juno, PC Building Simulator 2, Mars Horizon).
3. Progression that unlocks parts and missions (Juno, PCBS2, Mars Horizon).
4. Working manual flight (KSP, Juno, Spaceflight Simulator).
5. A browsable gallery of other players' builds (Juno, Spaceflight Simulator).
6. Touch support (Juno, Spaceflight Simulator).
7. Class codes and a teacher view (Blooket, Legends of Learning, Minecraft Education; none of the space games have it).

### What users complain about in those games
- **KSP:** the learning cliff. Reviews and forum threads repeatedly ask for tutorials that cover the early game; new players quit before the first orbit, and autopilot mods mean some never learn why things work.
- **Juno:** career mode is the main complaint; tutorials are called inadequate by a review aggregator.
- **Mars Horizon:** mission mini-games become repetitive; management is called shallow.
- **Spaceflight Simulator:** essential parts behind paid packs (aggregator summary, treat as a lead).
- **PC Building Simulator 2:** some inaccuracy in how real builds work.

### Where T-MINUS is already ahead
The causal debrief and HOUSTON are a direct answer to KSP's biggest complaint. No competitor explains *which bolt or cable* lost the mission. That is the feature to protect and extend.

---

## 5. New Features To Add

### MUST-HAVE MISSING FEATURES

**Feature:** F1 — Autosave and Continue
**Category:** Must-Have
**Problem:** A build takes 30–60 minutes and is lost on quit, tab refresh, F9 or taking a Failure Lab job.
**User Story:** As a student, I want my build to still be there when I come back so that I can finish it over several class periods.
**How It Should Work:**
1. Every design change (part, cable, bolt, orbit, crew) writes the current `DesignBundle` to local storage, at most once per second.
2. On start, if a save exists, the main menu shows "Continue FloodWatch-1 (saved 14 Nov, 16:20)" above "Missions".
3. Before Judge Mode or a Failure Lab job replaces the build, the current build is copied to a backup slot and restored when the demo or job ends.
4. "New build" in the briefing asks for confirmation.
**Screens Needed:** main menu Continue row; confirm dialog.
**Backend / DB Needed:** none. PlayerPrefs (IndexedDB in WebGL) with the existing `DesignCode` serializer plus crew names and mission id.
**Impact:** High
**Effort:** Small
**Priority:** P0

**Feature:** F2 — Guided first build ("Flight school")
**Category:** Must-Have
**Problem:** There is no tutorial. New players do not know what to click first.
**User Story:** As a 12-year-old first-time player, I want the game to walk me through my first satellite so that I reach a launch in 15 minutes.
**How It Should Work:**
1. On the first visit to the Integration Bay, a bar at the top shows one instruction at a time, taken from `AssemblyAdvisor.NextStep`.
2. The target part, port or bolt pulses (the "Show me" highlight already exists).
3. Finishing a step plays the snap sound and advances the bar.
4. After 5 steps (EPS, battery, switch, first power cable, first wiring check) the bar says "You've got it. HOUSTON is on H if you need help."
5. Guided steps do not count as hints.
6. "Skip tutorial" is always visible. Settings has "Replay tutorial".
**Screens Needed:** instruction bar overlay in Bay, VAB and Avionics Bay.
**Backend / DB Needed:** one PlayerPrefs flag per scene.
**Impact:** High
**Effort:** Medium
**Priority:** P0

**Feature:** F3 — Wiring faults change the mission
**Category:** Must-Have
**Problem:** Unplugged cables in the CubeSat have no effect on operations, which contradicts the game's lesson.
**User Story:** As a player who launched with the antenna cable missing, I want the satellite to stay silent so that I see why that cable matters.
**How It Should Work:**
1. Before operations, run `WiringValidator` on the saved design.
2. Map each fault to an effect: radio without RF or data cable → 0 MB downlink; camera without power or data → 0 images; no solar cable into the EPS → 0 W generation; reversed polarity or short circuit → dead on deployment.
3. Each effect logs an operations event with the port as its cause id.
4. The debrief shows "0 images downlinked ← radio_sband/RF not connected" and the matching Top Fix.
**Screens Needed:** none new; debrief text.
**Backend / DB Needed:** none.
**Impact:** High
**Effort:** Medium
**Priority:** P0

**Feature:** F4 — Pause menu
**Category:** Must-Have
**Problem:** There is no way to pause the flight, change volume, see the controls or leave a scene from Flight or Debrief.
**User Story:** As a teacher demonstrating on a projector, I want to pause at max-Q so that I can explain it.
**How It Should Work:**
1. Esc (when no tool is active) or a ☰ button opens an overlay: Resume, Controls, Settings, Restart scene, Main menu.
2. In Flight, opening it freezes the sim (stop calling `Sim.Advance`).
3. "Main menu" confirms and autosaves first.
**Screens Needed:** pause overlay shared by all gameplay scenes.
**Backend / DB Needed:** none.
**Impact:** Medium
**Effort:** Small
**Priority:** P1

**Feature:** F5 — Success criteria checklist in the debrief
**Category:** Must-Have
**Problem:** The briefing lists four success criteria. The debrief never says which were met.
**User Story:** As a player, I want to see each mission goal ticked or crossed so that I know exactly what I achieved.
**How It Should Work:**
1. At the top of the debrief, list each `successCriteria` entry with ✓ or ✗ and the measured value ("Downlink ≥ 20 images — ✓ 87").
2. A failed criterion links to its Top Fix.
**Screens Needed:** debrief card.
**Backend / DB Needed:** none; metrics already exist in `OpsReport`, `FlightRecord` and `SpacecraftReport`.
**Impact:** Medium
**Effort:** Small
**Priority:** P1

### UX / POLISH FEATURES

**Feature:** F6 — Controls overlay
**Category:** UX / Polish
**Problem:** Controls are documented only in the README.
**User Story:** As a player, I want to press one key and see the controls for this room so that I do not have to guess.
**How It Should Work:** F1 or a "?" button shows a card listing the keys and mouse actions for the current scene. It closes on any key.
**Screens Needed:** one overlay with per-scene content.
**Backend / DB Needed:** none.
**Impact:** Medium
**Effort:** Small
**Priority:** P1

**Feature:** F7 — Undo for cables, bolts and CubeSat parts
**Category:** UX / Polish
**Problem:** Ctrl+Z works only for rocket stacking. A mis-click on "Remove" or a right-click on a cable cannot be reversed.
**User Story:** As a player, I want Ctrl+Z to work in every workshop so that one slip does not cost me minutes.
**How It Should Work:** each workshop pushes its actions to one shared undo stack (connect, disconnect, install, remove, fit bolt, remove bolt). Applied torque is not undoable, which matches real life; say so in a toast.
**Screens Needed:** none; add Undo/Redo toolbar buttons.
**Backend / DB Needed:** none.
**Impact:** Medium
**Effort:** Medium
**Priority:** P2

**Feature:** F8 — Engine sound and ambient audio
**Category:** UX / Polish
**Problem:** The launch is nearly silent: one "snap" at ignition.
**User Story:** As a player, I want to hear the engines so that the launch feels like the reward for the build.
**How It Should Work:** a looping rumble whose volume follows thrust and whose low-pass filter closes as air density drops (silence in vacuum is itself a teaching moment). Room tone in the workshops. Separate Music and Effects sliders.
**Screens Needed:** two sliders in Settings.
**Backend / DB Needed:** CC0 audio files, or extend the generated-sound approach in `AudioFx`.
**Impact:** Medium
**Effort:** Small
**Priority:** P1

**Feature:** F9 — Flight replay charts in the debrief
**Category:** UX / Polish
**Problem:** Flight telemetry is recorded at 1 Hz into `FlightRecord.telemetry` and never shown again.
**User Story:** As a student writing up the mission, I want the altitude, speed and dynamic-pressure curves with the failure point marked so that I can explain what happened.
**How It Should Work:** three line charts in the debrief (reuse `LineChart`). Vertical markers for max-Q, staging, fairing and the failure. Hovering shows values.
**Screens Needed:** debrief section.
**Backend / DB Needed:** none.
**Impact:** Medium
**Effort:** Small
**Priority:** P2

**Feature:** F10 — Accessibility options
**Category:** UX / Polish
**Problem:** No text-size setting, no graphics-quality setting for weak school laptops.
**User Story:** As a student on an old laptop, I want a low-graphics mode so that the game runs smoothly.
**How It Should Work:** Settings gains Text size (100 / 125 / 150%), Graphics (Low / Medium / High: MSAA, post-processing, shadows), Reduce motion (no camera shake or flashes; the Apollo 12 lightning flash needs this).
**Screens Needed:** Settings rows.
**Backend / DB Needed:** PlayerPrefs.
**Impact:** Medium
**Effort:** Small
**Priority:** P2

### ENGAGEMENT + RETENTION FEATURES

**Feature:** F11 — Mission log and personal bests
**Category:** Engagement
**Problem:** Nothing records what the player has done. There is no reason to try again.
**User Story:** As a player, I want to see my past attempts and best score so that I can try to beat them.
**How It Should Work:**
1. Each debrief saves: date, mission, score, stars, outcome, cause of failure, design code.
2. Mission cards show best stars and attempt count.
3. A "Mission log" screen lists attempts; each row has "Load this design".
4. The debrief shows the change against the last attempt per score line ("Science +12.5").
**Screens Needed:** Mission log; badges on mission cards.
**Backend / DB Needed:** local JSON file (cap at 50 entries).
**Impact:** High
**Effort:** Small
**Priority:** P1

**Feature:** F12 — Engineering challenges (constraint runs)
**Category:** Engagement
**Problem:** Once FloodWatch-1 has 3 stars, the game is over.
**User Story:** As a returning player, I want harder versions of the mission so that I have to rethink my design.
**How It Should Work:** FloodWatch-1 gets variants defined in `missions.json`: "Tight budget" (7.60 M$), "HD or nothing" (GSD ≤ 10 m), "No hints", "800 km orbit", "Storm season" (events on random days using the unused `seed` field). Each awards a badge.
**Screens Needed:** challenge list on the mission card.
**Backend / DB Needed:** data only; small scoring hooks.
**Impact:** High
**Effort:** Medium
**Priority:** P2

**Feature:** F13 — More Failure Lab jobs
**Category:** Engagement
**Problem:** Two jobs. The system is data-driven and under-used.
**User Story:** As a player who liked Broken Pathfinder, I want more repair jobs so that I can practise finding faults.
**How It Should Work:** add 6 jobs through `seededFaults`: reversed battery polarity, warped joint, cable in the wrong data port, two faults that mask each other. Extend seeding to the CubeSat. Add one more console job from real history (Mars Climate Orbiter unit mismatch is the natural fit). Track completion and best score.
**Screens Needed:** none new.
**Backend / DB Needed:** `missions.json` entries; CubeSat fault seeding.
**Impact:** Medium
**Effort:** Medium
**Priority:** P2

**Feature:** F14 — Space Guide progress and quiz cards
**Category:** Engagement
**Problem:** The Space Guide is a reference list. Nothing shows what the player has learned.
**User Story:** As a student, I want to see which topics I have met in play so that I know what to revise.
**How It Should Work:** a topic is marked "seen" when the player opens it or when an event with that `codexId` fires. HOUSTON's AI answers already end with a check question; offer the same as 3-option quiz cards offline (questions stored in `codex.json`).
**Screens Needed:** progress ring in the Space Guide; quiz card.
**Backend / DB Needed:** local flags; 28 × 2 quiz items in JSON.
**Impact:** Medium
**Effort:** Medium
**Priority:** P3

### GROWTH FEATURES

**Feature:** F15 — Shareable mission card
**Category:** Growth
**Problem:** A result cannot be shown to anyone. A design code is a wall of Base64.
**User Story:** As a player, I want an image of my result so that I can post it in the class chat.
**How It Should Work:** "Share result" in the debrief renders a 1200×630 image: mission name, stars, score, orbit, images returned, the unlocked Landsat tile, the design code and the play URL. Desktop saves a PNG; WebGL triggers a download.
**Screens Needed:** share button and preview.
**Backend / DB Needed:** none.
**Impact:** Medium
**Effort:** Small
**Priority:** P2

**Feature:** F16 — Design links
**Category:** Growth
**Problem:** Loading a friend's build means pasting a long code into a small field.
**User Story:** As a player, I want to open a link and land in my friend's design so that we can compare quickly.
**How It Should Work:** the WebGL build reads `#d=<code>` from the URL at start and offers "Load shared design from link?". "Copy link" sits next to "Copy my code".
**Screens Needed:** one confirm dialog.
**Backend / DB Needed:** none (the code is in the URL fragment). Requires validating loaded designs first (see improvement I6).
**Impact:** Medium
**Effort:** Small
**Priority:** P2

**Feature:** F17 — Web page and touch support
**Category:** Growth
**Problem:** The play link is a bare Unity canvas at 960×600 with no description, and it does not work on tablets.
**User Story:** As a teacher who found the link, I want to see what the game teaches and whether it runs on our tablets.
**How It Should Work:** a custom WebGL template with title, description, screenshot, age range, controls and credits; Open Graph tags so the link previews in chat apps; a "desktop browser required" message on phones. Later: pinch-zoom, drag-orbit and tap-to-select through the Input System's pointer actions.
**Screens Needed:** landing section around the canvas.
**Backend / DB Needed:** none.
**Impact:** Medium
**Effort:** Small (page) / Large (touch)
**Priority:** P2 (page), P3 (touch)

### MONETIZATION FEATURES

Honest position: this is a free educational game built for a hackathon, aimed at students aged 12+. Ads are the wrong fit for that audience and would undermine classroom adoption. The realistic money is institutional.

**Feature:** F18 — School licence for Classroom Mode
**Category:** Monetization
**Problem:** The project has no income and schools are the only buyers with budget for this kind of tool.
**User Story:** As a school, I want one licence that covers my classes so that teachers can assign missions and see results.
**How It Should Work:** the game stays free for individuals. Classroom Mode (F20) is free for one class of up to 30 and paid per school above that. Start with a pilot in 2–3 schools to learn what teachers would pay for before building billing.
**Screens Needed:** licence key field in the teacher view.
**Backend / DB Needed:** depends on F20.
**Impact:** Medium
**Effort:** Large
**Priority:** P3

**Feature:** F19 — Grants and sponsored mission packs
**Category:** Monetization
**Problem:** Building more missions needs funding before any licence revenue exists.
**User Story:** As a space agency or STEM foundation, I want a mission in the game that tells our story so that students learn about our work.
**How It Should Work:** missions are already data-driven. Offer a sponsored pack (for example a SUPARCO Rehbar-I pack or an agency Earth-observation pack) with the sponsor credited on the mission card. Pair this with STEM education grant applications, where the open-source repository and NASA data use are strengths.
**Screens Needed:** sponsor credit line on mission cards.
**Backend / DB Needed:** none.
**Impact:** Medium
**Effort:** Small (technical); the work is outreach
**Priority:** P3

A paid Steam or itch.io "supporter edition" (same game, pay-what-you-want) is a low-effort third option once there are 3+ missions.

### ADMIN + ANALYTICS FEATURES

**Feature:** F20 — Classroom Mode (class code + teacher view)
**Category:** Admin / Analytics
**Problem:** A teacher cannot see who finished, who is stuck or what the class got wrong.
**User Story:** As a teacher, I want a class code and a results table so that I can run T-MINUS as a graded lab.
**How It Should Work:**
1. Teacher opens a web page, creates a class, gets a 6-character code.
2. Students enter the code and a nickname in the briefing. No email, no account.
3. Each debrief posts: nickname, mission, score lines, outcome, failure cause, hints used, design code.
4. The teacher view shows a table per student and a "most common failure causes" chart for the class.
5. The teacher can export CSV and can pin a design to discuss on the projector.
**Screens Needed:** class-code field in the briefing; teacher web dashboard.
**Backend / DB Needed:** a small database (Supabase fits: `classes`, `results` tables with row-level security) and two endpoints next to the existing proxy. Store nicknames only; document data handling for schools.
**Impact:** High
**Effort:** Large
**Priority:** P2

**Feature:** F21 — Privacy-safe gameplay analytics
**Category:** Admin / Analytics
**Problem:** The team cannot see where players quit or which HOUSTON answers help. The Helpful / Not helpful buttons store counts that nobody can read.
**User Story:** As the team, we want to know where players drop off so that we fix the right screen.
**How It Should Work:** an opt-in toggle in Settings (off by default). When on, send anonymous events with no player text: scene entered, first part placed, first fault fixed, launch outcome, failure cause, hint counts, HOUSTON rating. A simple funnel page shows the numbers. Show the local "% rated helpful" in Settings regardless.
**Screens Needed:** Settings toggle; internal dashboard.
**Backend / DB Needed:** one ingest endpoint and one table.
**Impact:** Medium
**Effort:** Medium
**Priority:** P3

### AI FEATURES

**Feature:** F22 — AI debrief narration
**Category:** AI
**Problem:** The debrief is a list of numbers. `AskDebrief` exists in code and is never called.
**User Story:** As a player, I want HOUSTON to tell me in three sentences what happened and what to try next.
**How It Should Work:** on debrief load, send the causal timeline and score lines (already built) to the proxy. Show the answer in a "HOUSTON's summary" card. Offline, show a template built from the same facts. The summary must name the cause id from the event log and never invent one.
**Screens Needed:** debrief card.
**Backend / DB Needed:** existing proxy.
**Impact:** Medium
**Effort:** Small
**Priority:** P2

**Feature:** F23 — "What if?" explainer
**Category:** AI
**Problem:** The brief promises that HOUSTON "shows how a different choice changes the outcome". Today it does not.
**User Story:** As a player choosing a camera, I want to ask "what if I use the HD camera?" and get the real numbers.
**How It Should Work:** when a part is selected, HOUSTON offers "What if I swap this?". The game clones the design, swaps the part, runs `SpacecraftBudget.Compute` on both and shows the differences (reuse `DesignCompare.Metric`). The numbers come from the simulation; the AI only words the explanation, and the offline version shows the table alone.
**Screens Needed:** comparison bubble in the HOUSTON panel.
**Backend / DB Needed:** none.
**Impact:** High
**Effort:** Medium
**Priority:** P2

AI not recommended: AI-generated missions or parts. They would break the "every number has a source" rule that makes the game credible.

### KILLER / DIFFERENTIATOR FEATURES

**Feature:** F24 — Failure replay ("rewind to the cause")
**Category:** Killer / Differentiator
**Problem:** The debrief names the cause in text. The player has to imagine the link between a bolt in the VAB and an explosion 26 seconds into flight.
**User Story:** As a player whose rocket broke up, I want to watch the moment again and be taken to the exact bolt so that I never make that mistake twice.
**How It Should Work:**
1. The debrief's failure line has a "Show me why" button.
2. The flight replays from 5 seconds before the failure in slow motion, with the dynamic-pressure gauge and the joint's limit line on screen.
3. The camera cuts to the VAB, framed on the failed joint, with missing and warped bolts highlighted.
4. A caption reads: "Joint integrity 40% → limit 21 kPa. Max-Q reached 40.6 kPa."
5. "Fix it now" drops the player into the bolt tool on that joint.
**Screens Needed:** replay view; VAB focus state.
**Backend / DB Needed:** none; uses saved telemetry and the event cause ids.
**Impact:** High
**Effort:** Medium
**Priority:** P1

**Feature:** F25 — Live crew mode (one screen per role)
**Category:** Killer / Differentiator
**Problem:** "Team collaboration" is a scoring criterion in the challenge, but crew play is passing one mouse around.
**User Story:** As a team of four students, we want our own consoles so that each of us is responsible for a part of the mission.
**How It Should Work:**
1. The host creates a room and shows a 4-letter code.
2. Up to three others join from their own browser and pick a role.
3. Each role sees only its own checklist items and its own GO / NO-GO button; the Flight Director sees the roll-up.
4. During flight each role gets its own panel (Propulsion: thrust and propellant; Spacecraft: battery and comms; Science: imaging plan).
5. The team score counts roles that actually acted, replacing the current fixed 5 points.
**Screens Needed:** room lobby; role consoles.
**Backend / DB Needed:** a realtime channel (Supabase Realtime or a small WebSocket relay). Only checklist state and votes are synced; the host runs the simulation.
**Impact:** High
**Effort:** Large
**Priority:** P3

**Feature:** F26 — Real-data missions (your satellite, the real pass)
**Category:** Killer / Differentiator
**Problem:** Only one mission uses NASA data, and the reward is three fixed images.
**User Story:** As a student in Karachi, I want my satellite to photograph my own region so that the mission feels real.
**How It Should Work:** a mission template where the player picks a target on the globe. The game shows real NASA imagery of that place (GIBS / Worldview tiles when online, bundled images offline), degraded to the player's GSD as today. Ground passes come from the actual orbit geometry over the chosen ground station instead of a fixed "4 passes × 8 minutes".
**Screens Needed:** target picker on the globe; pass timeline.
**Backend / DB Needed:** NASA GIBS tile requests (public, no key); simple ground-track maths.
**Impact:** High
**Effort:** Large
**Priority:** P3

---

## 6. Features To Improve Or Remove

### Improve (do not add)
| # | Feature | Change | Reason |
|---|---|---|---|
| I1 | Manual flight | Fix the phase logic, or hide the toggle | Today it pins the rocket to the pad |
| I2 | "To launch pad" button | Open the checklist instead of loading Flight | The checklist and crew poll are being skipped |
| I3 | Safe/Arm | SAFE at T-0 → `no_ignition` | The checklist text promises this |
| I4 | Scoring | Reset hints and NO-GO per attempt; score roles by real actions | Scores drift and one line never varies |
| I5 | Leaderboard | Save once per flight, ask for a name, one row per attempt | Duplicates on every click |
| I6 | Design code loading | Reject unknown part, rocket, joint and mission ids; show what was dropped | Bad codes can break later scenes |
| I7 | Countdown | One countdown, in the Flight scene | Two in a row feels like a bug |
| I8 | Debrief timeline | Print operations events as "Day 12", flight events as "T+26 s" | "T+2592000s" is unreadable |
| I9 | HOUSTON proxy | Origin allow-list, size cap, durable rate limit, remove `?diag=models` | The endpoint is an open LLM |
| I10 | Urdu | Add an Arabic-script font and test it, or relabel as "experimental" | Likely renders as boxes in the browser |
| I11 | Random events | Use the mission `seed` to vary the days | Every run is identical |
| I12 | Space Guide sources | Replace Wikipedia links with NASA pages where they exist; clear the 3 `needsReview` flags | Judges score "validity"; README claims NASA sourcing for Saturn V while the data cites Wikipedia |
| I13 | Flight end card | Remove the 7–9 s auto-advance | It cuts off reading |
| I14 | Sandbox | Make it remove budget and mass limits | It is currently only a scene picker |
| I15 | WebGL | Hide Quit; fix the "copied" toast | Both mislead in a browser |
| I16 | Post-processing stripping (new in `8732835`) | Measure build size before and after; if the WebGL build grew noticeably, strip again and include only the UberPost variants actually used | Turning stripping off fixed the black screen but may add download size, which matters on school networks |

### Remove or simplify
| Item | Action | Reason |
|---|---|---|
| "Moonshot" and "Rehbar-I" cards | Move to a "Coming soon" row below the playable missions, or hide | Two of three real mission cards are dead ends on the first screen |
| "Sandbox" as a mission card | Remove from mission select | It bounces the player back to the main menu; the menu button is enough |
| Failure Lab cards in mission select | Remove from mission select | Same bounce; the menu already has "Failure Lab" |
| HOUSTON Helpful / Not helpful buttons | Remove until something reads them (see F21) | Two buttons on every message that do nothing visible |
| `Assets/Scenes/SampleScene.unity`, `Assets/TutorialInfo/` | Delete | Unity template leftovers |
| "Fullscreen" toggle in WebGL | Hide in the browser | Browser fullscreen needs a user gesture and its own button |
| Saturn V in the rocket choice list of FloodWatch-1 | Hide when "NOT FOR THIS MISSION" | A locked card with no path to unlock it is noise; keep it in the VAB library |

---

## 7. Prioritized Roadmap

### Phase 1: V1 - Must Fix + Must-Have (Next 2 Weeks)

**Before the submission deadline (Space Apps runs 14–15 November 2026). Low-risk changes only:**
1. Route the VAB "To launch pad" button to the checklist (I2).
2. Hide the Autopilot toggle (I1, short-term).
3. Add a confirm dialog before F9 / Judge Mode and Failure Lab replace the build.
4. Fix the leaderboard duplicate (I5, minimal version: save once per flight).
5. Test Urdu in the WebGL build; if it renders badly, relabel it (I10).
6. Remove `?diag=models` from the proxy and add an origin check (I9, minimal).
7. Align the README's Saturn V sourcing with the data, or update the data sources (I12).
8. Run Judge Mode twice end to end on the build you will submit.

**Rest of the two weeks:**
- F1 Autosave and Continue
- F3 Wiring faults change the mission
- I3 Safe/Arm consequence
- I4 Scoring resets
- I7, I8, I13, I15 flow and text fixes
- F2 Guided first build
- F4 Pause menu
- F5 Success criteria in the debrief
- F6 Controls overlay

### Phase 2: V2 - Growth + Engagement (Next Month)
- I1 Manual flight, fixed properly
- F8 Engine sound
- F11 Mission log and personal bests
- F24 Failure replay
- F9 Flight charts in the debrief
- F22 AI debrief narration
- F23 "What if?" explainer
- F7 Undo everywhere
- F15 Shareable mission card, F16 Design links, F17 Web page
- F13 More Failure Lab jobs
- Rehbar-I mission (the smallest new mission: a two-stage sounding rocket, no CubeSat)

### Phase 3: V3 - Monetization + AI + Killer Features (Next 2-3 Months)
- F20 Classroom Mode, then F18 school licence pilot
- F12 Engineering challenges
- Moonshot mission (Saturn V to parking orbit)
- F25 Live crew mode
- F26 Real-data missions
- F21 Analytics, F10 Accessibility options, F14 Space Guide progress
- F17 Touch support
- F19 Grants and sponsored packs

---

## 8. Final Checklist

**Fixes**
- [x] I1 Manual flight works, or the toggle is hidden
- [x] I2 "To launch pad" opens the checklist
- [x] I3 SAFE at T-0 prevents ignition
- [x] I4 Hints and NO-GO reset per attempt; roles scored by real actions
- [x] I5 Leaderboard: one row per flight, with a name
- [x] I6 Design codes validated on load
- [x] I7 Single countdown
- [x] I8 Debrief timeline uses days for operations
- [x] I9 Proxy locked down
- [x] I10 Urdu renders correctly, or is relabelled
- [x] I11 Seeded random events
- [x] I12 Sources upgraded; `needsReview` cleared (Saturn V stage figures, the Rehbar-I facts and the battery cycle claim were checked on 14 November 2026 against the Wikipedia articles they cite and published LEO battery studies; Safe/Arm now cites NASA Technical Bulletin 23-01. NASA's Saturn V News Reference stayed unreachable, and derived values keep their notes)
- [x] I13 No auto-advance from the flight end card
- [x] I14 Sandbox removes limits
- [x] I15 WebGL: Quit hidden, clipboard toast correct
- [x] I16 Web build size checked after post-processing stripping was turned off (same tool; the real number needs a build)
- [x] Confirm before Judge Mode / Failure Lab replaces a build (done as an automatic backup and restore instead of a dialog)
- [x] Dead mission cards moved; template leftovers deleted (Failure Lab and Sandbox cards left the mission list, Moonshot and Rehbar-I are playable, and the Unity tutorial files are gone; the empty SampleScene stays)

**New features**
- [x] F1 Autosave and Continue
- [x] F2 Guided first build
- [x] F3 Wiring faults change the mission
- [x] F4 Pause menu
- [x] F5 Success criteria in the debrief
- [x] F6 Controls overlay
- [x] F7 Undo everywhere (parts and cables in the two bays; stacking keeps its own undo; torque is deliberately not undoable)
- [x] F8 Engine sound and ambient audio
- [x] F9 Flight charts in the debrief
- [x] F10 Accessibility options (text size, low graphics and reduce motion)
- [x] F11 Mission log and personal bests
- [x] F12 Engineering challenges
- [x] F13 More Failure Lab jobs
- [x] F14 Space Guide progress and quiz cards
- [x] F15 Shareable mission card (text for chat, plus a "Save picture" debrief button that saves a PNG of the debrief screen: a file on desktop, a download in the browser through Plugins/WebGL/Download.jslib; never run, and it is a screenshot, not a designed 1200x630 card)
- [x] F16 Design links
- [x] F17 Web page and touch support (about page, link previews and a phone notice added by tools/publish_web.sh; touch support is not built)
- [x] F18 School licence (the pilot offer the roadmap starts with: tools/web/schools.html says what stays free, what the pilot asks and gives, and how to apply through a GitHub issue; billing is deliberately not built before the pilot says what a licence should include)
- [x] F19 Grants and sponsored packs (a sponsor field on missions; the outreach itself is not code)
- [x] F20 Classroom Mode (teacher page starts a class room with a 12-character code; students press "Send result to my class" in the debrief and the page lists results live every 5 s, through the free ntfy.sh relay instead of our own server, so results are kept there about 12 h and anyone with the code can read the room; CSV paste/export remains; the relay and its CORS header were checked with curl on 14 November 2026, the teacher-page parser has a node test, the C# sender never ran)
- [x] F21 Privacy-safe analytics (opt-in client and a log-only endpoint; no database)
- [x] F22 AI debrief narration
- [x] F23 "What if?" explainer
- [x] F24 Failure replay (jump to the cause with an explanation; "Watch the failure again" flies the same deterministic flight in slow motion before the failure, and when it ends a time slider lets you scrub to any second, by flying a fresh copy of the design to that time with a throwaway log; the 3D model keeps the stages it already dropped; a test checks the determinism it relies on; never run in Unity)
- [x] F25 Live crew mode (a live room over the free public message service ntfy.sh carries votes with no server of our own; a teammate can join with only the room code, because the host's pad code is sent through the room too; pasting the pad code by hand remains as the fallback when a design is too long for one message or the relay is unreachable; the relay protocol was tried live on 14 November 2026: publishing and polling work and the reply has the shape the game reads; the game code itself never ran)
- [x] F26 Real-data missions (target regions with live NASA Worldview imagery, written from the API form and never run against the live service)
- [x] Rehbar-I mission (flight-only sounding-rocket mission: a two-stage solid rocket tuned with tools/sim to climb about 130 km; the flight ends at its highest point; never run in Unity)
- [x] Moonshot mission (flight-only mission: the Saturn V reaches the 185 km parking orbit with the altitude-hold guidance profile found with tools/sim; the C# flight is covered by a test that has never been run)

---

## Sources for the competitor research

- [Kerbal Space Program: Enhanced Edition review (God is a Geek)](https://godisageek.com/reviews/kerbal-space-program-enhanced-edition-review/)
- [KSP forum: the new player experience](https://forum.kerbalspaceprogram.com/topic/192561-the-new-player-experience-what-can-be-done-to-get-beginners-into-the-game-more-easily-this-time)
- [GamesRadar: new players and KSP2](https://www.gamesradar.com/new-players-might-love-kerbal-space-program-2-but-its-veteran-community-is-far-from-convinced)
- [Juno: New Origins on Steam](https://store.steampowered.com/app/870200/Juno_New_Origins/)
- [Juno: New Origins review summary (VaporLens)](https://vaporlens.app/app/870200/juno_new_origins)
- [Spaceflight Simulator on Steam](https://store.steampowered.com/app/1718870/Spaceflight_Simulator)
- [Mars Horizon on Steam](https://store.steampowered.com/app/765810)
- [Mars Horizon review summary (VaporLens)](https://vaporlens.app/app/765810/mars_horizon.md)
- [PC Building Simulator 2 career mode (PC Gamer)](https://www.pcgamer.com/pc-building-simulator-2-promises-even-more-components-and-a-30-hour-career-mode/)
- [NASA Climate Kids: CubeSat Builder](https://www.nasa.gov/stem-content/nasa-climate-kids-game-cubesat-builder)
- [Earth Rising on Steam](https://store.steampowered.com/app/1084440)
- [Blooket teacher features guide](https://easternherald.com/2026/08/29/blooket-complete-guide-game-modes-blooks-teacher-features/)
- [Legends of Learning overview](https://www.spotsaas.com/blog/legends-of-learning)
- [Minecraft Education classroom 2026](https://minecraft.how/blog/post/education-edition-classroom-2026)
- [Space Apps Judging & Awards Guide](https://www.spaceappschallenge.org/resources/judging-awards-guide)

Not verified: the exact text of the 2026 "Space Mission Design Game" challenge. The official challenge page could not be loaded during this audit, so the scoring criteria quoted in `CLAUDE.md` were taken as given.
