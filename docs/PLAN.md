# T-MINUS — Rocket & Mission Builder: Full Plan + AI Prompt Pack (NASA Space Apps 2026)

Nov 12, 2026 · @Coalition

## The idea in one minute

T-MINUS is a 3D "PC Building Simulator for space missions": you build a satellite and its rocket bolt by bolt and wire by wire, launch it with real physics, and learn exactly why it worked or failed. HOUSTON, an in-game AI assistant, recommends where every bolt, cable and part goes, and explains why.

**Pitch line:** Build it bolt by bolt. Wire it pin by pin. Launch it for real.

**The core loop**

1. Pick a mission, for example: put a flood-watch CubeSat in orbit over Sindh.
2. Build and wire the satellite in the Integration Bay (boards, battery, radio, camera, solar panels).
3. Assemble the rocket in the Vehicle Assembly Building (VAB): stack stages, torque the bolts, wire the avionics.
4. Run the pre-launch checklist and a GO/NO-GO poll (1 to 4 players, one role each).
5. Launch with real equations: thrust, drag, gravity, staging.
6. Operate the satellite for 30 days (power, data, downlink), then read a debrief that traces every failure to its cause.
7. Redesign and fly again.

**What changed from your original idea, and why**

- **Kept:** a library of real rockets, seeing how each one is built, taking it apart, connecting single wires and boards, a full simulation, education first.
- **Added: a mission and a small satellite payload.** The gaming challenge you picked ("Space Mission Design Game") asks for mission objectives, spacecraft and payload design, launch vehicle and orbit choice, resource management and a scored simulation. A rocket-only builder covers only the launch-vehicle part.
- **Added: HOUSTON** (your AI recommendation feature) and a **Failure Lab** built on real NASA incidents.
- **Why it can win:** the public entries I looked at for this challenge choose parts from menus or drag blocks and check numbers, for example [Delta-V: Mission Architect](https://github.com/MahiAlAraf/NASA) and [TrajectIQ](https://github.com/Raj2003-bubt/TrajectIQ). None of them makes you torque a bolt or plug a cable and then shows that exact choice deciding the mission.

**About your video:** I couldn't open the YouTube link (YouTube rate-limited both attempts). The design follows PC Building Simulator's well-known loop: pick parts, screw them in, plug the cables, power on, diagnose faults. If the video shows a specific feature you want copied, tell me and I'll add it.

## Fit to the challenge and the rules

Enter T-MINUS under the official **Space Mission Design Game** challenge, because its statement asks for exactly this build, simulate and evaluate loop. Avoid "Create Your Own Challenge": it is not eligible for Global Judging ([judging guide](https://www.spaceappschallenge.org/resources/judging-awards-guide)).

The challenge asks for a game where 1 to 4 players define a mission, design the spacecraft and payload, select the launch vehicle and orbit, manage budget, mass, power, communications, data and duration, then simulate and evaluate the result ([statement copy on GitHub](https://github.com/NASA-SPACEAPP-2026/Space-Mission-Design-Game); check it against the official page on your team dashboard).

| What the challenge asks | T-MINUS feature | Priority |
| --- | --- | --- |
| Define the mission objective and requirements | Mission Briefing cards: objective, budget, orbit, success criteria | P0 |
| Design the spacecraft and scientific payload | Integration Bay: CubeSat boards, battery, radio, camera, wiring | P0 |
| Select the launch vehicle and orbit | VAB rocket library + orbit picker (400 / 550 / 800 km) | P0 |
| Manage budget, mass, power, comms, data, duration | Live budget bars; battery vs eclipse; downlink per day | P0 |
| Simulate the mission and evaluate the result | Physics ascent + 30-day operations + scored debrief | P0 |
| Clear feedback on why it succeeded or failed | Causal debrief timeline + HOUSTON explanations | P0 |
| Interactive vehicle visualizations | 3D exploded and X-ray views | P0 |
| Single-player and/or multiplayer up to 4 | Crew mode: 1 to 4 roles, hot-seat GO/NO-GO poll | P1 |
| Random events | Solar storm, ground-station outage, Apollo 12 lightning | P1 |
| Save, share and compare designs | Copy-paste design codes + side-by-side compare | P1 |
| Leaderboard; sandbox | Local top 10; free build without a budget | P1 |

**Rules and dates (Karachi time, PKT)**

- **Don't build before Saturday Nov 14, 9:00 AM.** The official Team Formation Guide says participants should not begin working on their project until the hackathon starts. Learning, tool setup, research and planning are fine ([Team Formation Guide](https://www.spaceappschallenge.org/resources/team-formation-guide/)).
- **Submit by Sunday Nov 15, 11:59 PM** local time ([Team Formation Guide](https://www.spaceappschallenge.org/resources/team-formation-guide/)). Aim to submit by 8:00 PM and update after.
- **Resources drop Friday Nov 13.** Each challenge's datasets and resources are published then ([NASA Space Apps on X](https://x.com/SpaceApps/status/2115481217965371893)). Use at least one of them in the game.
- **Judging:** Impact (worded as Influence in the current guide), Creativity, Validity, Relevance and Presentation ([submission guide](https://www.spaceappschallenge.org/resources/project-submission-guide/)).
- **Team size:** six members at most.
- **Branding:** keep NASA logos out of the game and its icon; credit every NASA model and image you use.

That leaves 39 hours of build time, sleep included.

## Scope: what to build in 39 hours

Build one complete golden path first (one mission, two rockets, one satellite) and add P1 items only after that loop runs end to end. Target: full loop playable by Sunday 2:00 AM.

| Item | Priority | Track | Notes |
| --- | --- | --- | --- |
| Mission FloodWatch-1: 3U CubeSat images the Indus floodplain for 30 days | P0 | C | Science reward = real NASA Landsat images of the 2022 Lake Manchar floods |
| Integration Bay: 16 CubeSat parts, 4 cable types, live budgets | P0 | B | Includes the Remove-Before-Flight pin and deployment switch |
| Pathfinder training rocket, fully buildable: stages, bolts, avionics wiring | P0 | A + B | Fictional and labelled "training rocket" |
| Saturn V: inspect, exploded and X-ray views, real stage data, flyable | P0 | A + C | Uses NASA's free 3D model |
| Bolts: star-pattern order, 3 torque passes, joint integrity | P0 | A |  |
| HOUSTON: rule-based advisor + LLM explanations | P0 | D | Works offline |
| Pre-launch checklist + GO/NO-GO poll | P0 | B |  |
| Ascent physics, telemetry HUD, staging, failures caused by your build | P0 | C |  |
| Operations, scoring, causal debrief, redesign loop | P0 | C |  |
| Judge Mode: scripted 3-minute demo, no network needed | P0 | D | Your safety net on stage |
| Crew mode: 1 to 4 roles, hot-seat | P1 | B |  |
| Failure Lab: "Broken Pathfinder" repair job + Apollo 12 lightning strike | P1 | D |  |
| Rehbar-I mission: Pakistan's first rocket (7 June 1962, Sonmiani near Karachi), a two-stage Nike-Cajun flown with NASA support | P1 | C | Strong hook for Karachi judges ([Rehbar](<https://en.wikipedia.org/wiki/Rehbar_(rocket_family)>)) |
| Saturn V Moon shot: reach parking orbit with enough Δv left for trans-lunar injection | P1 | C |  |
| Design codes, compare, leaderboard, sandbox | P1 | C |  |
| HOUSTON answers in Urdu | P1 | D | One setting in the LLM call |
| Propellant plumbing (2 pipes) built on the cable system | P1 | B |  |
| SLS/Artemis, engine lab, Apollo 13 investigation, classroom dashboard, online multiplayer, VR | P2 | — | Roadmap slide only |

**Cut list: don't build these this weekend**

- Detailed rockets modelled from scratch in Blender. Use NASA's free Saturn V model plus simple procedural parts.
- More than two playable rockets.
- Full 3D orbits and inclination changes. A 2D flight plane over a round, rotating Earth is enough, and easy to explain.
- Unity Rigidbody physics for flight. Single-precision floats jitter at orbital distances; write your own double-precision integrator.
- Fluid or combustion simulation, photoreal graphics, online multiplayer, accounts and logins, voice chat.
- NASA logos.

**If you are only two people:** keep Saturn V inspect-only, skip crew mode and the Failure Lab, and ship HOUSTON's rule layer first (LLM if time allows).

## The game, screen by screen

&#91;embedded content: game loop · 7 screens, a redesign loop, the HOUSTON overlay\]

HOUSTON advises in the three build steps and narrates the debrief; every failure sends the player back through the redesign loop to fix the exact bolt, cable or part that caused it.

## HOUSTON: the AI that tells you where everything connects

HOUSTON has two layers: a rule engine that always knows the correct bolt, port and part from the game's own data, and an LLM that explains the why in plain language. The rules decide and the LLM only explains, so HOUSTON is never wrong about a connection and keeps working without internet.

| Feature | What the player sees | How it works |
| --- | --- | --- |
| Where does this go? | Pick up the battery: the EPS "BATT" port glows green and a ghost battery shows where it fits | Filters free, compatible targets from rules.json; required connections first |
| What's next? | "Next: slide the EPS board into slot U1" | Walks the assembly dependency order, skipping finished steps |
| Bolt coach | Bolt 7 lights up after bolt 1 (star pattern); the gauge shows a 45 N·m target, pass 2 of 3 | Star-order generator + torque spec per joint |
| Scan my build | "3 problems: 4 of 12 bolts missing on Joint 1; RBF pin still in; antenna not connected", each with a Show me button | Validators over the build state, sorted by severity |
| Why? | "Why a star pattern?" → it clamps the joint evenly so the flange doesn't tilt or leave a gap | LLM fed only the codex facts and the current state |
| Debrief | "Stage 2 never lit: the separation pyro channel was never wired" | Event log → rule-based cause → LLM writes the story |

**Hint ladder.** Level 1 is a text nudge (free). Level 2 highlights the target (small score cost). Level 3, Show me, animates a ghost doing the step (bigger cost). A teacher can switch HOUSTON off for a test.

**Grounding rules for the LLM**

- It receives a small JSON packet: mission, current step, the advisor's recommendation, top faults, the relevant codex entries, recent events and the player's question.
- It may not contradict the advisor or invent parts, numbers or ports. At most 70 words, student-friendly, English with an Urdu option.
- If no answer arrives within 8 seconds, the codex text is shown instead. Answers are cached, and Judge Mode's answers are pre-cached.
- The API key never ships in the game: calls go through a tiny Vercel function that holds the key.

**Why judges won't call it a chatbot wrapper:** every decision comes from simulation data and rules; the AI is a tutor sitting on top of them.

## Tech architecture

Use Unity 6.3 LTS with the Universal 3D (URP) template, C#, UI Toolkit, all content in JSON, and one scene per teammate so nobody edits the same file. Unity 6.3 is the latest LTS release ([Unity](https://unity.com/releases/unity-6-releases)); if you already installed another Unity 6 version, keep it rather than downloading again.

**Stack**

- Packages: Input System, UI Toolkit (built in), Newtonsoft JSON, Splines (optional, for cable tubes), Test Framework.
- MCP for Unity (CoplayDev, free, MIT licence, v10.3.0) lets Claude Code create objects, edit scripts, run tests and build inside the open Editor. It needs Python 3.10+ and uv ([GitHub](https://github.com/CoplayDev/unity-mcp)).
- Git + GitHub, the standard Unity .gitignore, and Git LFS for .fbx, .png and .wav files.
- HOUSTON proxy: one Vercel serverless function that holds the LLM key.

**Scenes and owners**

| Scene | Owner track | What lives there |
| --- | --- | --- |
| Boot, MainMenu | C | Services start, main menu |
| MissionBriefing | B | Mission cards, orbit and vehicle choice, checklist, GO/NO-GO |
| IntegrationBay | B | CubeSat build, wiring, budgets |
| VAB | A | Rocket library, assembly, bolts, avionics bay view |
| Flight | C | Countdown, ascent, HUD, payload deploy |
| Debrief | C | Operations results, score, causes, image reward |
| HOUSTON overlay (prefab) | D | Panel + advisor, loaded into every scene |

**Folder layout**

```
Assets/_TMinus/
  Scripts/Core/        GameState, DataRegistry, EventLog, SceneFlow
  Scripts/Assembly/    Part, SnapNode, AssemblyGraph, BoltJoint
  Scripts/Wiring/      Device, Port, Cable, WiringValidator
  Scripts/Spacecraft/  Budgets: mass, power, data, cost
  Scripts/Flight/      FlightSim (double precision), Guidance, Telemetry
  Scripts/Mission/     MissionRunner, OpsSim, Scoring, Debrief
  Scripts/Houston/     AssemblyAdvisor, HoustonPanel, HoustonLLMClient
  UI/                  UXML + USS
  Editor/              Scene and prefab builders (Tools > T-Minus menu)
  Resources/Data/      parts.json, rockets.json, missions.json, rules.json, codex.json
  Art/Models/Parts/    FBX files from Blender, named by part id
  Tests/EditMode/      Physics and rules tests
proxy/                 Vercel function for HOUSTON (outside Assets)
```

**Five decisions that save the weekend**

1. Data-driven: parts, ports, bolt patterns, rockets, missions, rules and codex text live in JSON. Content changes need no code, and HOUSTON reads the same data.
2. Sockets by name: child objects named SNAP\_*, BOLT\_## and PORT\_* become snap points, bolt holes and ports automatically, from Blender or from procedural placeholders.
3. Flight physics is plain C# in double precision on a fixed timestep. Unity only draws the result around a floating origin.
4. Editor builders: the AI writes Tools > T-Minus menu commands that build scenes and prefabs, so nobody hand-wires dozens of Inspector fields.
5. One event log: every assembly, flight and operations event is logged with its cause, which feeds the debrief and HOUSTON.

**Physics model (the equations judges will ask about)**

Rocket equation: the Δv a stage can deliver.

```latex
\Delta v = I_{sp}\, g_0 \ln\frac{m_0}{m_f}
```

Thrust changes with air pressure, at constant mass flow.

```latex
F(h) = \dot m\, g_0\left[I_{sp,vac} - \left(I_{sp,vac} - I_{sp,sl}\right)\frac{p(h)}{p_0}\right], \qquad \dot m = \frac{F_{vac}}{g_0\, I_{sp,vac}}
```

Drag in an exponential atmosphere with an 8.5 km scale height.

```latex
D = \tfrac{1}{2}\,\rho(h)\,v_{rel}^2\,C_d A, \qquad \rho(h) = 1.225\,e^{-h/8500}\ \mathrm{kg/m^3}
```

Gravity over a round Earth, and the orbit check from position and speed.

```latex
g = \frac{\mu}{r^2}, \qquad \varepsilon = \frac{v^2}{2} - \frac{\mu}{r}, \qquad a = -\frac{\mu}{2\varepsilon}, \qquad \mu = 3.986\times10^{14}\ \mathrm{m^3/s^2}
```

Launching east from Sonmiani (about 25°N) gives about 420 m/s for free from Earth's rotation (ω × R × cos latitude): a good teaching moment.

## Blender: the six-step pipeline

Use Blender for only two jobs: preparing NASA's free Saturn V model, and making simple modular parts. Mark every attach point with a named Empty so Unity wires it up automatically.

1. **Scene units:** Metric, Unit Scale 1.0, so 1 Blender unit = 1 metre, the same as Unity.
2. **Model low-poly:** rockets are mostly cylinders, cones and lathe shapes (Spin or Screw modifier for engine bells). Add a small bevel so edges catch light. Keep each part under about 5,000 triangles.
3. **Pivot = attach point:** put the origin at the part's bottom centre or mounting face (Object > Set Origin > Origin to 3D Cursor).
4. **Sockets as Empties** (Add > Empty > Plain Axes), parented to the part, local axis pointing along the connection:
   - `SNAP_Top`, `SNAP_Bottom` for stacking
   - `BOLT_01` to `BOLT_12` around a joint flange
   - `PORT_PWR_IN`, `PORT_PWR_OUT_1`, `PORT_DATA_1`, `PORT_RF`, `PORT_PYRO_1` for cable connectors
   - `PIPE_LOX`, `PIPE_FUEL` for P1 plumbing
5. **Materials:** one plain Principled material per colour, named `Body_White`, `Band_Black`, `Insulation_Orange`, `Metal_Grey`. No textures needed; Unity remaps them by name.
6. **Export FBX per part:** File > Export > FBX with Limit to Selected Objects, Object Types Mesh + Empty, Apply Scalings "FBX All", Forward "-Z Forward", Up "Y Up", Apply Transform on, Apply Modifiers on. Save as `Assets/_TMinus/Art/Models/Parts/<part_id>.fbx`; the file name must equal the part's id in parts.json.

**NASA's Saturn V model**

- Download `Saturn V.glb` (905 KB, credit NASA/Michael D. Carbajal) from [NASA 3D Resources](https://science.nasa.gov/3d-resources/saturn-v/).
- In Blender: File > Import > glTF 2.0. Split it into S-IC, S-II, S-IVB, Instrument Unit and spacecraft (Edit Mode, select a stage, P > Selection).
- Set each stage's origin at its bottom, add `SNAP_` Empties at the joints, and export each stage as its own FBX (`saturnv_sic`, `saturnv_sii`, `saturnv_sivb`, `saturnv_iu`, `saturnv_spacecraft`).

**Placeholders first.** Prompt 4 generates procedural parts with the same socket names, so the code never waits for art. When an FBX arrives, the importer from Prompt 16 swaps it in automatically.

**Before Saturday:** practise the export once with a test cylinder in a throwaway project. Make the real assets during the event.

## Team plan and the 39-hour schedule

&#91;embedded content: build schedule · 6 phases, 4 tracks, 3 gates\]

Sleep in two shifts (2–6 AM and 4–8 AM) so someone is always awake to fix a broken build.

**Git rules that prevent Unity merge disasters**

- One scene and one Scripts folder per track. Never save another track's scene.
- Shared code (Core, Data) changes only through the integrator, announced in your team chat first.
- Pull before every prompt; commit after every passing prompt with the prompt number in the message ("P6: wiring validator"); push at least hourly.
- Git LFS for models, textures and audio. Never commit Library/, Temp/, Builds/ or houston.config.json.
- If a .unity scene file conflicts, don't hand-merge it: keep the owner's version and redo the other change.
- Tag a known-good commit at each gate (`git tag gate-1`) so you always have something to demo.

## Before Saturday 9:00 AM: allowed prep

Spend Thursday and Friday on tools, skills and resources, not on project code, so Saturday starts with a working toolchain and nothing to install.

- [ ] Confirm your team is registered for the Karachi local event under "Space Mission Design Game", and read the full statement on the official site.
- [ ] Friday: open the challenge Resources tab, list every NASA dataset it gives, and pick at least one to use in the game.
- [ ] Ask your Karachi local lead whether pre-made art assets are allowed. Plans and research are fine; code is not.
- [ ] Install Unity 6.3 LTS with Windows Build Support and WebGL Build Support.
- [ ] Make a throwaway practice project (delete it afterwards): a C# script that rotates a cube, a prefab, a UI Toolkit button, a mouse-click raycast.
- [ ] Install Python 3.10+, uv, Git and Git LFS. Install MCP for Unity in the practice project and confirm Claude Code can create a cube in the open Editor.
- [ ] Blender: export a test cylinder with two Empties as FBX into the practice project; check the Empties appear as child objects.
- [ ] Download resources: NASA's Saturn V model, the three Landsat images of Lake Manchar (June, August, September 2022) from [NASA Earth Observatory](https://science.nasa.gov/earth/earth-observatory/lake-manchar-is-overflowing-150306/), and [NASA's CubeSat 101 guide](https://www.nasa.gov/wp-content/uploads/2017/03/nasa_csli_cubesat_101_508.pdf).
- [ ] Get an LLM API key and a Vercel account for the HOUSTON proxy; test one API call with curl.
- [ ] Assign tracks A to D, the owner of each scene, and one integrator who merges to main.
- [ ] Read this whole prompt pack once; rename "T-Minus" now if you want another name.
- [ ] Sleep properly on Friday night.

## How to run the prompts

Run one prompt at a time, test it in Unity, commit, then move on. Never paste three prompts at once: the agent will cut corners and you won't know which step broke.

1. Saturday 9:00 AM: the integrator creates the Unity project (Universal 3D template), pushes it to GitHub, and everyone clones it.
2. Save **Prompt 0** as `CLAUDE.md` in the project root. Claude Code (VS Code extension) reads it automatically every session. In Antigravity, save it as `PROJECT_BRIEF.md` and start each session with "Read PROJECT\_BRIEF.md first".
3. Paste the next prompt for your track. When the agent says it's done, switch to Unity, wait for the compile, and open the Console.
4. Errors? Paste the full Console text with the Fix-it prompt below.
5. Run the prompt's acceptance checks yourself. All pass → commit → push → next prompt.
6. Always `git pull` before a prompt, and only touch your own scene and folders.

**MCP for Unity (optional, faster):** Package Manager > Add package from git URL > `https://github.com/CoplayDev/unity-mcp.git?path=/MCPForUnity#main`, then Window > MCP for Unity > Configure All Detected Clients ([GitHub](https://github.com/CoplayDev/unity-mcp)). Without MCP, the prompts already ask the agent for Editor menu builders.

**Order and parallel tracks**

| Track | Person | Prompts in order |
| --- | --- | --- |
| Integrator (first hour, then joins A) | Strongest coder | 0, 1, 2 |
| A: VAB and assembly | Dev A | 3, 4, 5, then 16, 17 |
| B: Integration Bay and wiring | Dev B | 6, 7, 8, 9, then plumbing (P1) |
| C: Mission, flight and debrief | Dev C | 10, 11, 12 |
| D: HOUSTON, content, pitch | Dev D | 13, 14, 18, then 15 |

**Fix-it prompt**

```
Unity shows the errors below after your last change. Fix the root cause, not the symptom. Do not delete or stub out features to make errors disappear. Explain the cause in one line, then fix it, then tell me exactly what to re-test.

<paste the full Console errors here>
```

**Review prompt (after each step)**

```
Review what you built in this step against its acceptance checks. Mark each check PASS or FAIL with a reason. Fix every FAIL. Then give me the exact manual test steps in Unity, and append a 3-line entry to docs/PROGRESS.md.
```

**New-session prompt**

```
Read CLAUDE.md and docs/PROGRESS.md. Summarise what exists, what's next for Track <A/B/C/D>, and any risks or broken parts. Do not write code yet.
```

**Emergency scope-cut prompt**

```
We are behind schedule. It is <time>. The demo must run the golden path (briefing → bay → VAB → checklist → launch → debrief) by <time>. List what to cut or fake, ranked by hours saved, and the smallest code changes to keep Judge Mode working. Don't change anything until I pick.
```

## Prompt pack, part 1: foundation (Prompts 0–2)

Prompt 0 is not run; it is saved as `CLAUDE.md` so every later prompt inherits it. Prompts 1 and 2 are run once by the integrator before the tracks split.

### Prompt 0 — Master brief (save as CLAUDE.md)

```
# T-MINUS — brief for AI coding agents (read before every task)

## What we are building
T-MINUS is a 3D educational space-mission game for the NASA Space Apps Challenge 2026, challenge "Space Mission Design Game". Inspired by PC Building Simulator: players build a 3U CubeSat and its rocket part by part (bolts, cables, connectors), launch it with real physics, operate the satellite, and get a debrief that explains every success and failure. HOUSTON, an in-game AI assistant, recommends where each bolt, cable and part goes and explains why.

## Core loop
Mission Briefing -> Integration Bay (build + wire the CubeSat) -> VAB (assemble, bolt and wire the rocket; mount the payload) -> pre-launch checklist + GO/NO-GO poll (1-4 players with roles) -> Flight (physics ascent, staging, deploy) -> Operations (power, data, downlink over mission days) -> Debrief (score + causes) -> Redesign -> fly again.

## Audience
Students aged 12+ and first-year university students. Every number on screen has a one-line "why". Friendly and clear, never childish. English UI; HOUSTON can also answer in Urdu.

## Tech stack (ask before changing)
- Unity 6.3 LTS, URP (Universal 3D template), C#, new Input System, UI Toolkit (UXML + USS) for all UI, Newtonsoft JSON.
- No paid assets. Ask before adding any package.
- Targets: Windows x64 (main demo) and WebGL (stretch).
- Root folder Assets/_TMinus/. Namespaces: TMinus.Core, TMinus.Assembly, TMinus.Wiring, TMinus.Spacecraft, TMinus.Flight, TMinus.Mission, TMinus.Houston, TMinus.UI, TMinus.EditorTools. One .asmdef per Scripts subfolder; tests in Assets/_TMinus/Tests/EditMode.

## Architecture rules
1. Data-driven: parts, ports, bolt patterns, rockets, missions, rules and codex text live in JSON in Assets/_TMinus/Resources/Data/. Never hardcode stats in code.
2. Sockets by name: child transforms named SNAP_*, BOLT_##, PORT_<TYPE>_<NAME> and PIPE_* are auto-registered as snap nodes, bolt holes, ports and pipe ends.
3. Scenes: Boot, MainMenu, MissionBriefing, IntegrationBay, VAB, Flight, Debrief. One owner per scene; never edit another track's scene, only call its public C# API. Cross-scene state lives in GameState (the only singleton).
4. Every gameplay event goes to EventLog (time, system, code, message, causeId, severity). The debrief and HOUSTON read it.
5. Flight physics is pure C#, double precision, fixed timestep, 2D plane over a round rotating Earth. Never use Rigidbody for flight. Unity transforms only display the result (floating origin).
6. Editor automation: any scene or prefab setup gets a menu command under Tools/T-Minus/ that builds it idempotently. Never ask me to hand-wire more than 3 Inspector fields.
7. Placeholders first: if a model is missing, generate a procedural one with the same socket names.
8. Keep scripts under 300 lines. If you change a public API another track uses, note it in docs/PROGRESS.md.
9. No API keys in code or in git. HOUSTON's LLM calls go to a configurable proxy URL.
10. Colour is never the only signal: cables and states also use patterns, icons and text.

## Scoring (from the challenge)
Mission performance, scientific value, efficient resource use, decision quality (hints used, faults fixed before launch), team collaboration (crew roles).

## Working agreement
- Before coding: restate the task in 3 bullets and list the files you will create or change.
- After coding: list changed files, give exact Unity test steps, and append a 3-line entry to docs/PROGRESS.md.
- If something is ambiguous, pick the simplest option that keeps the demo working, and say what you picked.
- If MCP for Unity tools are available, use them to create objects, inspect scenes and read the Console. Otherwise write Editor menu builders.
```

### Prompt 1 — Bootstrap the project · Integrator · about 1 h · P0

```
Read CLAUDE.md first. Task: bootstrap the T-MINUS Unity project.

1. Create the folders from CLAUDE.md under Assets/_TMinus/ (Scripts/Core, Assembly, Wiring, Spacecraft, Flight, Mission, Houston, UI; UI/UXML; UI/USS; Editor; Resources/Data; Art/Models/Parts; Art/Materials; Prefabs/Parts; Scenes; Tests/EditMode). Add an .asmdef per Scripts subfolder (all reference Core), an Editor .asmdef and a Tests .asmdef.
2. Add packages: com.unity.nuget.newtonsoft-json, com.unity.inputsystem (if missing), com.unity.test-framework, com.unity.splines. Report any that fail.
3. Core services (TMinus.Core):
   - GameState: current mission id; crew (1-4 players: name + role); current SpacecraftDesign and VehicleDesign (serializable); settings (HOUSTON mode On / Hints only / Off, language en|ur, hint level used).
   - DataRegistry: loads every JSON in Resources/Data at startup and exposes GetPart(id), GetRocket(id), GetMission(id), GetRules(), GetCodex(id). Clear error message for a missing or invalid file.
   - EventLog: Add(system, code, message, causeId, severity); Query(system, fromTime); Clear() per run.
   - SceneFlow: Load(sceneName) with a 0.3 s fade; Next() follows Briefing -> IntegrationBay -> VAB -> Flight -> Debrief.
4. Scenes: Boot, MainMenu, MissionBriefing, IntegrationBay, VAB, Flight, Debrief, each with a camera, a light and a label showing its name. Add them to Build Settings with Boot first. Boot starts the services and loads MainMenu.
5. Main menu (UI Toolkit): title "T-MINUS", buttons Missions, Sandbox, Codex, Settings, Quit. Missions -> MissionBriefing. Style: dark navy background, white text, one orange accent (#FF7A1A), rounded buttons, large readable font. No NASA logos.
6. Editor menu: Tools/T-Minus/Rebuild Scenes recreates scenes and Build Settings idempotently.
7. Create a standard Unity .gitignore, a .gitattributes with Git LFS for *.fbx *.png *.jpg *.wav *.mp3, and docs/PROGRESS.md.

Acceptance checks:
- Play from Boot: the main menu appears; Missions opens MissionBriefing; no Console errors.
- DataRegistry logs "Loaded 0 parts, 0 rockets, 0 missions" (data arrives in Prompt 2).
- EditMode test: DataRegistry handles an empty Data folder without exceptions.
```

### Prompt 2 — Game data · Integrator or Track D · about 1.5 h · P0

```
Read CLAUDE.md. Create the game data in Assets/_TMinus/Resources/Data/ and typed C# classes in TMinus.Core.Data that DataRegistry loads. Use the values exactly as given. Anything you add yourself must carry "illustrative": true.

1) rockets.json
- "pathfinder" (fictional training rocket, illustrative):
  Stage 1: dry 1,000 kg, propellant 9,500 kg, 9 engines of 25 kN each at sea level / 27 kN in vacuum, Isp 300 s sea level / 330 s vacuum, diameter 1.2 m, length 12 m.
  Interstage 60 kg, 1.0 m. Stage 2: dry 250 kg, propellant 2,000 kg, 1 vacuum engine of 26 kN, Isp 345 s, diameter 1.2 m, length 2.4 m.
  Avionics ring 25 kg. Payload adapter + CubeSat dispenser 15 kg. Fairing 50 kg (two halves), jettisoned at 110 km.
  Capacity about 150 kg to 550 km (tuned in Prompt 10). Launch cost 7.5 M USD (game value).
- "saturn_v" (real; source https://en.wikipedia.org/wiki/Saturn_V stage table):
  S-IC: dry 137,000 kg, gross 2,214,000 kg, 5 x F-1, 33,000 kN at sea level (stage total), Isp 263 s sea level / 304 s vacuum (F-1 engine figures, mark needsReview), burn about 150 s, diameter 10 m, length 42 m, LOX/RP-1.
  S-II: dry 43,000 kg, gross 470,000 kg, 5 x J-2, 4,400 kN vacuum, Isp 424 s, burn 395 s, diameter 10 m, length 24.87 m, LOX/LH2.
  S-IVB: dry 15,200 kg, gross 120,500 kg, 1 x J-2, 1,000 kN vacuum, Isp 424 s, two burns (165 s + 312 s), diameter 6.6 m, length 17.86 m, LOX/LH2.
  Instrument Unit + interstages + escape tower lumped as 15,000 kg. Apollo spacecraft payload 43,500 kg.
  Whole vehicle: height 111 m; liftoff mass must fall between 2,822,171 and 2,965,241 kg; payload 140,000 kg to LEO and 43,500 kg to trans-lunar injection.
  Stage fields: id, name, dryMassKg, propMassKg, engineCount, engineName, thrustSlKN, thrustVacKN, ispSl, ispVac, diameterM, lengthM, propellants, separationChannel, ignitionChannel, source, illustrative. If only one thrust is given, derive the other from the Isp ratio (same mass flow).

2) parts.json (CubeSat parts; all illustrative game-balance values)
id | name | mass kg | cost k$ | power | ports and notes
frame_3u | 3U structure | 0.35 | 6 | - | slots U1-U3, 4 long faces, 2 end faces
eps | Electrical power system board | 0.10 | 12 | 0.15 W | SOLAR_IN x2, BATT, PWR_OUT x4 (10 W each), DATA
batt_20 | Battery 20 Wh | 0.18 | 4 | - | BATT
batt_40 | Battery 40 Wh | 0.33 | 7 | - | BATT
obc | Flight computer, 2,000 MB storage | 0.10 | 10 | 0.5 W | PWR_IN, DATA x4 (bus)
radio_uhf | UHF radio 9.6 kbps | 0.09 | 8 | 1 W idle / 4 W transmit | PWR_IN, DATA, RF
radio_sband | S-band radio 1,000 kbps | 0.20 | 25 | 1.5 W idle / 10 W transmit | PWR_IN, DATA, RF
ant_uhf | UHF deployable antenna | 0.05 | 3 | - | RF
ant_patch | S-band patch antenna | 0.06 | 6 | - | RF
adcs_mag | ADCS: magnetorquers + sun sensors, +/-10 deg | 0.15 | 15 | 0.4 W | PWR_IN, DATA
adcs_rw | ADCS: reaction wheels + star tracker, +/-0.1 deg | 0.70 | 60 | 2.5 W | PWR_IN, DATA
cam_wide | Wide camera, 100 m/pixel at 550 km, 4 MB/image | 0.20 | 10 | 2 W while imaging | PWR_IN, DATA
cam_hd | Imaging camera, 5 m/pixel at 550 km, 25 MB/image, needs adcs_rw | 0.60 | 45 | 4.5 W while imaging | PWR_IN, DATA
solar_body | Body-mounted panels, 8 W peak | 0.20 | 20 | - | SOLAR_OUT x2, pointing factor 0.6
solar_wings | Deployable wings, 20 W peak | 0.35 | 45 | - | SOLAR_OUT x2, pointing factor 0.8
dep_switch | Deployment switch | 0.01 | 1 | - | inline between battery and EPS; keeps the satellite off inside the dispenser
rbf_pin | Remove-Before-Flight pin | 0.01 | 0 | - | must be pulled before launch
Rocket parts: pf_stage1, pf_interstage, pf_stage2, pf_avionics_ring, pf_payload_adapter, pf_fairing_left, pf_fairing_right.
Avionics devices: av_battery (28 V), av_pdu, av_flight_computer (PYRO_OUT_1-4, DATA x3), av_imu, av_telemetry, av_antenna, av_igniter_s1, av_igniter_s2, av_stage_sep, av_fairing_sep, av_safe_arm.
Saturn V parts: saturnv_sic, saturnv_sii, saturnv_sivb, saturnv_iu, saturnv_spacecraft (inspect and fly; no bolt mini-game).

3) rules.json
- Cable types: power (PWR_OUT, SOLAR_OUT, BATT -> PWR_IN, SOLAR_IN, BATT; polarity must match), data (DATA <-> DATA; OBC ports accept several), rf (RF <-> RF), pyro (PYRO_OUT -> PYRO_IN).
- Forbidden: PWR_OUT -> PWR_OUT (short circuit); any cable into a port of another type. Warning: battery wired straight to a load (bypasses EPS protection).
- Required connections: radio = PWR_IN + DATA + RF; camera = PWR_IN + DATA; adcs = PWR_IN + DATA; obc = PWR_IN; eps = at least 1 SOLAR_IN + BATT; antenna = RF; dep_switch inline between battery and EPS.
- Assembly order. CubeSat: frame, eps, battery, dep_switch, obc, radio, antenna, adcs, camera, panels; pull rbf_pin at the pad. Pathfinder: stage1, interstage, stage2, avionics ring, payload adapter, payload, fairing halves.
- Bolt joints (game values): stage1-interstage 12 x M10 at 45 N.m +/-10%; interstage-stage2 12 x M10 at 45 N.m; payload adapter 8 x M6 at 12 N.m; fairing 8 x M6 at 10 N.m; maxQkPa 45 at full integrity.
- Pyro channels: 1 = S1 ignition, 2 = stage separation, 3 = S2 ignition, 4 = fairing separation. Safe/Arm plug must be SAFE in the VAB and ARMED only at the pad.

4) missions.json
- "floodwatch_1" (P0, beginner/intermediate): objective "Image the Indus floodplain for 30 days and downlink at least 20 usable images"; budget 7.72 M USD (launch 7.5 M + 220 k for the spacecraft, so the premium HD build only just fits); 3U mass limit 4.0 kg (game value); orbit options 400 / 550 / 800 km; launch site Sonmiani (25.2 N) or Cape Canaveral (28.5 N); 4 ground contacts per day x 8 min; 1 imaging pass over the target per day (up to 3 images per pass); seeded random events: solar storm on day 12 (computer reboot, 1 day lost), ground-station outage on day 20; scoring weights success 40, science 25, budget 10, margins 10, decision quality 10, collaboration 5; reward images: the three Landsat Lake Manchar images of June 25, August 28 and September 5, 2022.
- "moonshot" (P1): Saturn V; reach a 185 km parking orbit with at least 3,100 m/s left in the S-IVB for trans-lunar injection (game threshold). Drop the 15,000 kg lumped mass with the S-II, keeping 2,000 kg for the Instrument Unit, and tune the threshold after a test flight.
- "rehbar_1962" (P1): suborbital from Sonmiani; two-stage solid sounding rocket; target apogee about 130 km (historical); sodium-vapour payload; story card about Pakistan's first launch, flown with NASA support.
- "failure_apollo12" (P1): scripted lightning-strike scenario (Prompt 15).
- "sandbox" (P1): no budget, no limits.

5) codex.json: at most 30 entries, each { id, title, short (max 40 words), long (max 120 words), sourceUrl, needsReview }. Topics: star pattern, torque, staging, thrust-to-weight, delta-v and the rocket equation, specific impulse, max-Q, gravity turn, launching east, orbit and periapsis, eclipse, battery depth of discharge, EPS, deployment switch, RBF pin, pyro separation, safe/arm, telemetry, ADCS and pointing, ground sample distance, downlink and ground passes, data budget, Saturn V, F-1 engine, J-2 engine, Rehbar-I 1962, Apollo 12 lightning, Landsat. Use only textbook facts and the numbers in this prompt; set needsReview: true on anything else. Landsat credit line: "NASA Earth Observatory images by Joshua Stevens, using Landsat data from the U.S. Geological Survey."

6) EditMode test: load every file; ids unique; every referenced id exists; every device's required ports exist; Saturn V liftoff mass falls inside the range above.
```

## Prompt pack, part 2: VAB, assembly and bolts (Prompts 3–5)

Track A owns these. They deliver the PC-Building-Simulator feel: pick a part, slide it home, torque it down.

### Prompt 3 — VAB hangar, camera and rocket library · Track A · about 2 h · P0

```
Read CLAUDE.md. Track A. Scene: VAB.

1. Environment: a large assembly hall (procedural boxes are fine): concrete floor with a faint grid, tall walls, a launch-stand platform in the centre, a gantry tower beside it, 3-point lighting with soft blue ambient. URP post-processing: mild bloom, ACES tonemapping, vignette 0.2.
2. Orbit camera (no Cinemachine): right-drag orbits the focus, middle-drag pans, scroll zooms (clamped), F or double-click focuses the selected part, smooth damping; Alt + left-drag orbits for touchpads.
3. Selection: hover tints a part (emission boost or a simple outline shader you write), click selects, Esc deselects.
4. Part info card (UI Toolkit, right side): name, category, mass, cost, key specs from DataRegistry, a 2-line codex summary, "Learn more" (opens the codex entry), "Ask HOUSTON" (raises an event; wired in Prompt 13).
5. Rocket library (UI Toolkit, bottom carousel): a card per rocket in rockets.json with name, Real/Training badge, height, liftoff mass, payload to LEO. Selecting a card spawns that rocket assembled on the launch stand, from prefabs if present, else procedural placeholders (if Prompt 4 isn't done, a stub: one cylinder per stage scaled by length and diameter, a cone on top).
6. Top toolbar: Assemble / Inspect mode, Exploded view, X-ray, Reset camera.
7. Editor menu: Tools/T-Minus/Build VAB Scene (idempotent).

Acceptance checks:
- Play the VAB scene: the Pathfinder appears; orbit, pan and zoom are smooth at 60 fps.
- Hover or click any stage: the info card shows its real numbers from rockets.json.
- Pick Saturn V in the carousel: it replaces the Pathfinder and the camera reframes to its 111 m height.
```

### Prompt 4 — Parts, snapping, exploded and X-ray views · Track A · about 3 h · P0

```
Read CLAUDE.md. Track A. Namespace TMinus.Assembly.

1. Part component: partId (from parts.json), runtime state (attached, joint id). On Awake it registers child transforms: SNAP_* -> SnapNode (kind top/bottom/side, diameterM from data), BOLT_## -> BoltHole (Prompt 5), PORT_* -> Port (Prompt 6), PIPE_* -> PipeEnd (P1).
2. ProceduralPartFactory: builds a placeholder for any part id without a prefab: stage = cylinder (length, diameter); engine = frustum + bell lathed from a profile; interstage = short cylinder; fairing = two ogive half-shells; payload adapter = cone; avionics ring = thin torus. It creates SNAP, BOLT and PORT children at the right positions (bolts evenly spaced on the joint circle). Materials: Body_White, Band_Black, Insulation_Orange, Metal_Grey (URP Lit).
3. AssemblyGraph: nodes = parts, edges = snap connections with a joint id. Validation: diameters must match within 5% (else an adapter is needed), stack order from rules.json (stage 2 can't go below stage 1), one part per snap node. Events: PartAttached, PartDetached, GraphChanged.
4. Drag and drop from a parts bin (left panel, UI Toolkit list filtered by category) or off the rocket. While dragging, show a translucent ghost. When a ghost snap node is within 0.5 m (scaled by rocket size) and aligned within 15 degrees of a compatible free node, snap with a click sound and a green flash. Invalid targets flash red with a reason ("Diameter mismatch: 1.2 m vs 3.0 m").
5. Detach: allowed only if nothing depends on the part (otherwise list what must come off first) and its joint bolts are removed (Prompt 5 hook; allow for now).
6. Undo/redo (Ctrl+Z / Ctrl+Y) for attach and detach.
7. Exploded view: animate parts apart along the stack axis, spacing proportional to size, with labels on leader lines; toggle back. X-ray: outer shells (stages, fairing) fade to 15% alpha so tanks, engines, the avionics ring and the payload show through.
8. Two start states for the Pathfinder: Disassembled (parts laid out on the floor) for missions, Assembled for inspection.

Acceptance checks:
- From Disassembled, build the Pathfinder by dragging its 7 parts; a wrong order gives a clear reason.
- Exploded view on Saturn V separates S-IC, S-II, S-IVB, Instrument Unit and spacecraft with labels.
- EditMode tests: AssemblyGraph rejects a diameter mismatch and a wrong order; undo restores the graph exactly.
```

### Prompt 5 — Bolts and torque · Track A · about 2 h · P0

```
Read CLAUDE.md. Track A. Namespace TMinus.Assembly.

1. Every snap connection with BOLT_## holes creates a BoltJoint with N holes; data from rules.json: boltCount, size label (M10), torqueNm, tolerancePct, maxQkPa.
2. Bolt states: Missing -> Inserted (finger-tight) -> Torqued (within tolerance) / UnderTorqued / OverTorqued (stripped: must be replaced).
3. Interaction: hover a hole to highlight it; click to animate a bolt in (state Inserted). Hold left mouse on a bolt to fill a circular torque gauge with a green target band; release to set the torque; a ratchet click every 10% and a distinct click at target. Right-click removes a bolt.
4. Star pattern: StarOrder(N) returns the tightening order. If N is divisible by 4: for k from 0 to N/4 - 1 add k, k + N/2, k + N/4, k + 3N/4 (0-based). That gives 1-5-3-7-2-6-4-8 for N = 8 and 1-7-4-10-2-8-5-11-3-9-6-12 for N = 12 (1-based). Otherwise alternate opposite holes.
5. Three passes: 30%, then 70%, then 100% of target torque. Fully torquing one bolt while its neighbours are still loose raises a "Joint warped" warning.
6. JointIntegrity from 0 to 1: share of bolts correctly torqued, minus penalties for out-of-order full torque and over-torque. BoltJoint.GetStatus() returns integrity, missing, under, over, sequenceErrors.
7. Flight hook: FlightSim (Prompt 10) reads each joint; a joint fails when dynamic pressure exceeds maxQkPa x integrity. Log the cause to EventLog with the joint id and the bolt numbers involved.
8. Visuals: bolt numbers 1..N on hover; state shown by colour + icon (grey missing, yellow inserted, green torqued, red over/under).

Acceptance checks:
- Bolting stage 1 to the interstage in star order over three passes gives integrity 1.0. Going clockwise at full torque on pass 1 shows the warped-joint warning and integrity below 1.
- EditMode tests: StarOrder(8) and StarOrder(12) match the sequences above; integrity maths is correct.
```

## Prompt pack, part 3: wiring, satellite and avionics (Prompts 6–8)

Track B owns these. This is your "single wire, single board" feature: one wiring system, used twice (the CubeSat and the rocket's avionics ring), where every cable changes what happens in flight.

### Prompt 6 — Wiring system + Integration Bay · Track B · about 4 h · P0

```
Read CLAUDE.md. Track B. Scene: IntegrationBay. Namespaces TMinus.Wiring (generic) and TMinus.Spacecraft (the bay).

Part A - generic wiring system (reused by the rocket avionics in Prompt 8):
1. Port: id, type (PWR_OUT, PWR_IN, BATT, SOLAR_OUT, SOLAR_IN, DATA, RF, PYRO_OUT, PYRO_IN, ARM), polarity (+/- for power types), maxPowerW (outputs), label. Created from child transforms named PORT_<TYPE>_<NAME>.
2. Cable types from rules.json: power (red/black pair, solid), data (blue, dashed), RF coax (grey, thick, ringed), pyro (yellow with black stripes). Each has allowed port pairs and a max length.
3. Interaction: click a port and a plug follows the mouse on a sagging cable (cubic Bezier, sag grows with length; LineRenderer, or a SplineExtrude tube if Splines works). Over a compatible port the plug snaps and glows green; over an incompatible one it shows red with a reason ("RF coax can't plug into a DATA port"). Click to connect (plug sound); right-click a cable to unplug. Route cables slightly above the board to avoid flicker.
4. WiringGraph + WiringValidator rules: compatible types only; power polarity must match; PWR_OUT to PWR_OUT is a short circuit (spark effect + fault); one cable per port except bus ports; each power rail's load must stay under maxPowerW; required connections per device from rules.json; battery straight to a load gives a warning (bypasses the EPS).
5. Tools: Multimeter (click two ports: beep + "Connected via 1 cable" or "Open circuit"); Label view (hover a cable to see from -> to).
6. Events CableConnected, CableRemoved, FaultDetected, all logged to EventLog.

Part B - Integration Bay scene (3U CubeSat):
1. A clean-room table with an anti-static mat and a 3U CubeSat frame (procedural if no model: a 10 x 10 x 34 cm frame with 4 corner rails), internal slots U1-U3, 4 long faces and 2 end faces outside.
2. Parts bin (left panel) with every cubesat part in parts.json.
3. Boards slide into slots along the rails with a click (PC-build style); panels snap onto faces; antennas onto end faces.
4. Exploded view and a cutaway toggle to see the stack.
5. The RBF pin starts inserted and must be pulled (click, pull animation) before launch. The deployment switch must sit between battery and EPS: it keeps the satellite off inside the dispenser, as on real CubeSats.
6. Editor menu: Tools/T-Minus/Build Integration Bay.

Acceptance checks:
- Build a working CubeSat (EPS, 20 Wh battery, deployment switch, OBC, UHF radio + antenna, magnetorquer ADCS, wide camera, body panels), fully wired: validator shows 0 faults.
- Wrong plugs give clear reasons; a short circuit raises a fault; the multimeter works.
- EditMode tests for the validator: polarity, short circuit, rail overload, missing required connection.
```

### Prompt 7 — Spacecraft budgets · Track B · about 2 h · P0

```
Read CLAUDE.md. Track B. Namespace TMinus.Spacecraft.

Add live budget bars to the Integration Bay (right panel, green / yellow / red, each with a "why?" tooltip):
1. Mass: sum of parts vs the mission's 3U limit (4.0 kg game value) and vs the chosen rocket's capacity (checked again in the VAB).
2. Cost: parts + launch vs the mission budget.
3. Power, orbit average, at the mission's target altitude h:
   r = 6,371 km + h; period T = 2*pi*sqrt(r^3 / mu), mu = 3.986e14 m^3/s^2.
   Eclipse fraction fe = asin(6,371 km / r) / pi (worst case: Sun in the orbit plane); sunlit fraction = 1 - fe. At 550 km: T is about 95.5 min and eclipse about 35 min.
   Generation = sum of panel peak W x sunlit fraction x pointing factor (body 0.6, wings 0.8).
   Consumption = always-on loads + duty-cycled loads x duty (camera imaging duty from the mission plan; radio transmit only during ground contacts: 4 x 8 min per day).
   Margin = generation - consumption; must be positive; warn under 10%.
4. Battery: eclipse energy = consumption x eclipse time; required capacity = that / 0.3 (max depth of discharge); compare with the battery's Wh.
5. Data per day: generated = images/day x MB/image; downlink = radio rate x 1,920 s of contact; storage 2,000 MB. Show days until storage is full when downlink < generated.
6. Pointing: the HD camera needs reaction-wheel ADCS; with magnetorquers only, image quality = 0.3.
7. Resolution preview: GSD = camera GSD at 550 km x h / 550; show "about X m per pixel".
8. Expose SpacecraftReport (all numbers + issues with codex ids) for HOUSTON, the pre-launch checklist and the operations sim.

Acceptance checks:
- Default wide-camera build: positive power margin (about 3.0 W generated vs about 2.1 W used), battery OK, about 2.3 MB/day downlink on UHF. That is only about 17 wide images in 30 days, so the data bar warns that the 20-image goal needs the S-band radio.
- Swap to the HD camera without changing ADCS or panels: power turns red and a pointing warning appears, each with a reason.
- EditMode test: at 550 km, period 95.5 min and eclipse 35 min, each within 1 min.
```

### Prompt 8 — Rocket avionics bay · Track B (or A) · about 2 h · P0

```
Read CLAUDE.md. Reuse TMinus.Wiring. Scene: a new additive scene AvionicsBay that you own (never edit Track A's VAB scene), loaded as a close-up "Bay view" of the Pathfinder's avionics ring.

1. Devices on a ring panel (procedural boards are fine): flight battery (28 V), power distribution unit (PDU), flight computer (PYRO_OUT_1-4, DATA x3), IMU, telemetry radio + antenna, stage 1 igniter, stage 2 igniter, stage separation system (PYRO_IN), fairing separation (PYRO_IN), Safe/Arm plug (ARM).
2. Required wiring (rules.json): battery -> PDU; PDU -> flight computer, IMU, telemetry; flight computer DATA <-> IMU and telemetry; telemetry RF -> antenna; PYRO_OUT_1 -> S1 igniter, PYRO_OUT_2 -> stage separation, PYRO_OUT_3 -> S2 igniter, PYRO_OUT_4 -> fairing separation. The Safe/Arm plug stays SAFE in the VAB; working on wiring while ARMED is a fault. It is switched to ARMED at the pad.
3. Consequences, exposed as AvionicsReport for FlightSim:
   - S1 igniter not wired: no ignition at T-0, automatic abort.
   - Stage separation not wired: stage 1 stays attached, stage 2 can't fire, mission lost.
   - S2 igniter not wired: the upper stage coasts without thrust.
   - IMU not wired: guidance has no attitude; the rocket slowly drifts off course.
   - Telemetry or antenna not wired: the flight HUD shows "LOS - NO TELEMETRY"; the flight still happens and the debrief notes it.
   - Fairing separation not wired: the fairing stays on, costing performance, and the satellite can't deploy.
   Every consequence is logged with its cause id.
4. A "Bay view" button on the avionics ring in the VAB opens the close-up through a public AvionicsBay.Open() call that Track A wires to their button; Back returns.

Acceptance checks: full wiring gives 0 faults; removing each cable produces its matching consequence in AvionicsReport; EditMode tests cover every rule.
```

## Prompt pack, part 4: mission, flight and debrief (Prompts 9–12)

Track C owns Prompts 10–12; Track B takes Prompt 9 after its wiring work. Prompt 10 is pure maths with tests, so it can start at 10 AM Saturday without any art or scenes.

### Prompt 9 — Mission flow, crew roles and GO/NO-GO · Track B · about 3 h · P0 (crew mode P1)

```
Read CLAUDE.md. Track B. Scene: MissionBriefing (+ shared flow). Namespace TMinus.Mission.

1. Mission select: a card per mission in missions.json (title, difficulty, one-line objective, budget, rocket options, "Start here" badge on FloodWatch-1). P1 missions show "Coming next" when locked.
2. Briefing screen: objective; success criteria as checkboxes that tick in the debrief; constraints (budget, mass limit, duration); orbit options with one-line trade-offs (400 km: sharper images, shorter life; 550 km: balanced; 800 km: long life, blurrier images); launch vehicles with capacity, cost and a fits / doesn't-fit badge; launch site (Sonmiani 25.2 N or Cape Canaveral 28.5 N) with its Earth-rotation bonus (omega x R x cos(latitude), about 420 m/s at Sonmiani).
3. Crew setup (scaffold now, finish in P1): 1-4 players; roles Flight Director, Propulsion & Structures, Spacecraft & Comms, Science & Budget. With fewer players, roles merge. Each role owns its checklist items.
4. Flow buttons: Build Spacecraft -> IntegrationBay; Build Rocket -> VAB; Go to Pad -> pre-launch.
5. Pre-launch checklist (modal), filled automatically from SpacecraftReport, AssemblyGraph and BoltJoint status, both WiringValidators, AvionicsReport, and payload mass vs rocket capacity. Each item is PASS or FAIL with a Fix button that opens the right scene and focuses the part. Then the GO/NO-GO poll: each role in turn clicks GO or NO-GO (hot-seat). Any NO-GO returns to fixing; all GO starts the countdown. Launching with known FAILs is allowed (it's a lesson) but lowers decision quality.
6. Design codes (P1): serialise SpacecraftDesign + VehicleDesign + mission id -> JSON -> GZip -> Base64. Copy / Load design code; compare two designs side by side (mass, cost, power margin, delta-v).

Acceptance checks: click through from mission select to countdown (stub scenes are fine); the checklist reflects the real build state; a design code round-trips exactly (EditMode test).
```

### Prompt 10 — Flight physics core · Track C · about 3 h · P0

```
Read CLAUDE.md. Track C. Namespace TMinus.Flight. Pure C#: no UnityEngine types except in thin adapters. Read build state only through two interfaces you define in TMinus.Core (IAvionicsReport, IJointStatus) with default all-OK stubs, so this prompt runs before Prompts 5 and 8 are finished. Double precision, fixed timestep.

1. Constants: mu = 3.986004418e14 m^3/s^2, R_E = 6,371,000 m, omega_E = 7.2921159e-5 rad/s, g0 = 9.80665 m/s^2, p0 = 101,325 Pa.
2. State in a 2D Earth-centred inertial plane: position (x, y) m, velocity (vx, vy) m/s, mass kg, time s. Start on the surface at the launch site with eastward velocity omega_E x R_E x cos(latitude).
3. Forces each step:
   - Gravity: a = -mu * r_hat / r^2.
   - Atmosphere: rho = 1.225 * exp(-h / 8500) kg/m^3; p = p0 * exp(-h / 8500); treat both as zero above 150 km.
   - Drag: D = 0.5 * rho * v_rel^2 * Cd * A, opposite v_rel, where v_rel = v - omega_E x r (the air turns with Earth; in this 2D plane use omega_E x cos(latitude) as the rotation rate, so v_rel is zero on the pad). Cd = 0.4, or 0.6 between Mach 0.8 and 1.2 (speed of sound 340 m/s). A = pi * d^2 / 4 of the widest attached stage.
   - Thrust per active stage: mdot = F_vac / (g0 * Isp_vac); Isp(h) = Isp_vac - (Isp_vac - Isp_sl) * p / p0; F = mdot * g0 * Isp(h) * throttle. Mass drops by mdot * dt until that stage's propellant is gone.
   - Dynamic pressure q = 0.5 * rho * v_rel^2; record max-Q.
4. Integrator: RK4 at dt = 0.05 s. Time warp x1 / x2 / x4 inside the atmosphere and up to x100 when coasting in space, always via substeps, never a bigger dt in the atmosphere.
5. Autopilot: vertical rise to 100 m/s; pitch kick (player slider 1-6 degrees, default 3); gravity turn (thrust along v_rel) up to 45 km; then keep raising apoapsis toward the target; cut off when apoapsis >= target; coast to apoapsis; burn prograde until periapsis >= target - 10 km. Manual mode: A/D pitch, Shift/Ctrl throttle, Space stages.
6. Orbit elements from state: energy e = v^2/2 - mu/r; a = -mu / (2e); h = x*vy - y*vx; ecc = sqrt(1 + 2*e*h^2 / mu^2); r_apo = a(1 + ecc); r_peri = a(1 - ecc). Orbit achieved when periapsis altitude > 140 km.
7. Staging (on burnout or Space): check AvionicsReport (separation wired and armed?) and joint integrity; drop the spent stage's mass; ignite the next stage only if its igniter is wired; jettison the fairing at 110 km if fairing separation is wired.
8. Failures from the build: ignition failure; separation failure; joint failure when q > maxQkPa x integrity; guidance drift without the IMU (pitch error grows 0.5 degrees per second). Log each to EventLog with its cause id.
9. Telemetry per step: t, altitude, downrange, surface and orbital speed, acceleration in g, q, mass, propellant % per stage, thrust-to-weight, delta-v remaining (rocket equation over remaining stages), apoapsis, periapsis, stage, events.

EditMode tests (must pass):
- Saturn V liftoff thrust-to-weight between 1.1 and 1.25 with the data in rockets.json.
- Rocket equation: delta-v = Isp * g0 * ln(m0 / mf) matches a hand-computed case within 0.1%.
- A satellite placed in a 550 km circular orbit stays within +/-2 km altitude for 10 orbits (no thrust, no drag above 150 km).
- The default Pathfinder with 150 kg of payload reaches an orbit of at least 500 km on autopilot. If not, tune the pitch-kick default first, then Pathfinder data, and report the final numbers. Then record that flight's max-Q and set every joint's maxQkPa in rules.json to 1.3 x that value, so a perfect joint always survives and a joint below about 0.75 integrity fails.
```

### Prompt 11 — Flight scene and telemetry HUD · Track C · about 3 h · P0

```
Read CLAUDE.md. Track C. Scene: Flight.

1. World: Earth sphere shown at 1:1000 scale with a simple land/ocean look (use NASA's Blue Marble texture if the team added one, else a gradient), an atmosphere rim glow, a star skybox and a sun light. Floating origin: the rocket stays near (0,0,0) and the world moves.
2. Rocket: the assembled rocket from VehicleDesign follows FlightSim position and attitude. Exhaust particles scale with thrust and widen as the air thins. Cameras: chase, side tracking, ground view from the pad (first 20 s), and an orbit map (2D view: Earth circle, trajectory, apoapsis and periapsis markers).
3. Countdown T-10 to T-0 with checklist callouts. At T-0 check ignition; release only if thrust-to-weight > 1, else abort with "Not enough thrust to lift off: TWR 0.92".
4. Events with animation: max-Q callout, stage 1 cutoff, stage separation (the spent stage tumbles away), stage 2 ignition, fairing halves peel away, stage 2 cutoff, orbit achieved, CubeSat springs out of the dispenser. Failure animations: joint break with debris, ignition failure smoke, stuck stage.
5. Telemetry HUD (UI Toolkit, mission-control style): altitude, speed, acceleration (g), dynamic pressure (kPa), mass, propellant bars per stage, thrust-to-weight, delta-v remaining, apoapsis / periapsis, mission clock, event feed. Two live charts drawn with Painter2D: altitude vs time and speed vs time. If telemetry isn't wired, show "LOS - NO TELEMETRY" and only the clock.
6. Controls: time-warp buttons, autopilot on/off, pitch-kick slider before launch, manual keys, abort.
7. On success or failure, store the EventLog and 1 Hz telemetry samples in GameState and load Debrief.

Acceptance checks: the default Pathfinder reaches orbit and deploys the CubeSat in under 10 real minutes at max warp; an unwired separation cable produces the stuck-stage failure; 60 fps on a mid-range laptop.
```

### Prompt 12 — Operations, scoring and debrief · Track C · about 3 h · P0

```
Read CLAUDE.md. Track C. Namespace TMinus.Mission. Scene: Debrief (with an Operations panel before it).

1. Operations sim after deploy, one step per orbit for the mission duration (FloodWatch-1: 30 days, about 450 orbits; runs in a few seconds with a progress timeline):
   - Sun and eclipse per orbit from altitude (as in Prompt 7); battery state of charge starts at 100%; loads from SpacecraftReport.
   - Imaging: one daylight pass over the target per day, up to 3 images, only if state of charge > 40%.
   - Downlink: 4 contacts per day x 8 min at the radio's rate; storage limit 2,000 MB.
   - Image quality from pointing (ADCS); resolution from altitude.
   - RBF pin left in, or deployment switch miswired: the satellite never powers on (logged at deploy).
   - Seeded random events from missions.json (so Judge Mode repeats exactly): solar storm (computer reboot, 1 day lost), ground-station outage (no downlink for 1 day), battery ageing (-10% capacity).
2. Outputs: state-of-charge timeline, data stored vs downlinked, images taken vs downlinked, failures with causes.
3. Score with the mission's weights (default: success 40, science 25, budget 10, margins 10, decision quality 10, collaboration 5) and give 1-3 stars.
4. Debrief screen: stars + score breakdown; "What happened", a causal timeline from EventLog ("T+142 s stage separation failed -> cause: PYRO_OUT_2 not wired"); "What to change", the top 3 fixes from the rules; science reward: the real NASA Landsat images of Lake Manchar (2022 floods), blurred to your design's resolution and cropped to the share of images you downlinked, with the credit line from codex.json.
5. Buttons: Redesign (back to the bays, same mission and design), Fly again, Save design code, Leaderboard (local top 10: name, score, design code).

Acceptance checks: three scripted runs (perfect; separation cable missing; HD camera without enough power) give sensible scores and causes, and the image reward changes with the design.
```

## Prompt pack, part 5: HOUSTON (Prompts 13–14)

Track D owns these. Prompt 13 makes the recommendations (offline, always correct); Prompt 14 adds the AI voice that explains them. Ship 13 first.

### Prompt 13 — HOUSTON advisor: where bolts, wires and parts go · Track D · about 3 h · P0

```
Read CLAUDE.md. Track D. Namespace TMinus.Houston. HOUSTON is a prefab overlay used in IntegrationBay, VAB, pre-launch and Debrief.

Build the deterministic AssemblyAdvisor (no network, never wrong):
1. Inputs: the scene's AssemblyGraph, BoltJoints, WiringGraph + WiringValidator results, SpacecraftReport, AvionicsReport, mission requirements, and rules.json (required connections, assembly order, port compatibility, bolt patterns).
2. API:
   - WhereDoesThisGo(heldItem): ranked valid targets (snap node, port or bolt hole) with reason codes. The top target gets a ghost preview and a pulsing green highlight; incompatible targets dim.
   - NextStep(): the next action in dependency order ("Slide the EPS board into slot U1", "Bolt 7 next (star pattern)", "Connect radio RF to the antenna"), skipping finished steps and putting required items and faults first.
   - Scan(): prioritised faults, each with severity (blocker / warning / tip), what, where (object reference), why (codex id) and a fix action.
   - BoltCoach(joint): next bolt number from StarOrder, current pass (30 / 70 / 100%), target torque.
3. Hint ladder: Level 1 text nudge ("Power has to reach the radio somehow..."), Level 2 highlight the target, Level 3 "Show me" (a ghost performs the step and the player confirms). Count hints per level in GameState for decision-quality scoring. Modes: On / Hints only / Off (exam mode).
4. HOUSTON panel (UI Toolkit, bottom-right, collapsible): a simple headset icon (no NASA logo), message list, quick buttons "What's next?", "Where does this go?", "Scan my build", "Why?", and a text box for free questions (wired to the LLM in Prompt 14; until then answered from the codex). Keys: H toggles the panel, N = next step, Shift+S = scan.
5. Proactive tips, at most one every 20 s and dismissible: when the player picks up an item, when a fault appears, when they try to launch with blockers.
6. Every recommendation carries reasonCode + codexId + objectRef, so the LLM can explain it and a Show me button can frame the object with the camera.

Acceptance checks:
- On an empty CubeSat, pressing "What's next?" repeatedly yields a complete, valid build order.
- Holding the battery highlights the EPS BATT port.
- On the Pathfinder's 12-bolt joint, BoltCoach follows 1-7-4-10-2-8-5-11-3-9-6-12.
- EditMode test: Scan finds all 5 faults seeded into a test build.
```

### Prompt 14 — HOUSTON's AI explanations + key-safe proxy · Track D · about 2 h · P0

```
Read CLAUDE.md. Track D. Namespace TMinus.Houston.

1. HoustonLLMClient: async (UnityWebRequest), POST to a configurable endpoint from StreamingAssets/houston.config.json (add it to .gitignore and commit a houston.config.example.json): { endpointUrl, timeoutSec: 8, language: "en" | "ur", enabled }. No API key anywhere in the Unity project.
2. Request body: { packet, question }. The packet is JSON: mission (id, objective), scene, playerRole, currentStep, advisorRecommendation (from AssemblyAdvisor), faults (top 5), codex entries for the ids involved (title + short + long), last 10 events. Keep it under 6 KB.
3. System prompt, stored in /proxy/houston_system_prompt.txt and added by the proxy:
   "You are HOUSTON, a calm, encouraging flight-engineering mentor inside the T-MINUS education game. Explain the advisor's recommendation and the faults using ONLY the facts in the packet. Never contradict advisorRecommendation, and never invent part names, numbers, ports or rules. If the packet doesn't contain the answer, say so and name the codex entry to read. Audience: students aged 12 and up. At most 70 words, plain language, one idea per sentence, metric units. End with one short question that checks understanding. If language is 'ur', answer in simple Urdu script and keep technical terms in English."
4. Fallback: on timeout, error or disabled, compose the answer from the codex short text + the advisor's reason, and show a small "offline" tag. Cache answers per session by (question + step + faults) hash. Judge Mode uses pre-cached answers only.
5. Debrief narration: send the causal timeline + score breakdown and get back a 3-sentence story of the mission + 3 "next time" bullets; fallback = the rule text.
6. Proxy in /proxy (outside Assets): a Vercel serverless function (TypeScript, Node 20) at /api/houston. It reads the LLM API key, provider and model from environment variables, adds the system prompt, forwards the request, caps output at 300 tokens, rate-limits per IP (simple in-memory), sends CORS headers so the WebGL build can call it, and returns { text }. Include a README with deploy steps (vercel deploy, set env vars) and a curl test.
7. UI: typing indicator; thumbs up / down on each answer, logged locally (useful in the pitch: "X% of answers rated helpful in testing").

Acceptance checks: with the proxy deployed, "Why a star pattern?" returns an answer of 70 words or fewer that agrees with the advisor; with Wi-Fi off, the fallback answers instantly; a search of the repo finds no API key.
```

## Prompt pack, part 6: Failure Lab, art pipeline, polish and ship (Prompts 15–18)

Prompts 16 and 18 are P0 (art swap-in and Judge Mode). Prompts 15 and 17 are P1: start them only after the golden path runs end to end.

### Prompt 15 — Failure Lab: repair jobs from real NASA history · Track D · about 3 h · P1

```
Read CLAUDE.md. Track D. Namespace TMinus.Mission. P1: only after the golden path works end to end.

Failure Lab = repair jobs, like PC Building Simulator's broken-PC jobs, built on real NASA history.
1. Job board UI: cards with a short client note, difficulty and reward.
2. Job "Broken Pathfinder": a pre-built rocket with 3 seeded faults (4 bolts missing on Joint 2; pyro channel 3 unplugged; the IMU data cable in the wrong port). The player finds them with the tools (inspection camera, multimeter, torque readout) before launch. HOUSTON can help, at a points cost.
3. Job "Apollo 12, 1969: lightning" (Saturn V ascent): at T+36.5 s and T+52 s the vehicle is struck by lightning. Spacecraft power drops, warning lights flood the panel and telemetry turns to garbage. In the EECOM seat, the player has 20 seconds to pick the fix from a console of switches. The correct call is "SCE to AUX" (switch the Signal Conditioning Equipment to auxiliary power), which restores telemetry; then reset the fuel cells. Afterwards, explain that the rocket's own guidance kept flying on its separate systems: a lesson in redundancy. Credit flight controller John Aaron for the real call.
4. Job debrief: what was broken, how it was found, the real-world lesson.
5. Data-driven: jobs live in missions.json with seededFaults and scriptedEvents, so new jobs need no code.

Acceptance checks: someone who knows the answer solves each job in under 3 minutes; every wrong fix gets an explanation.
```

### Prompt 16 — Blender import pipeline · Track A or D · about 1.5 h · P0

```
Read CLAUDE.md. Namespace TMinus.EditorTools.

1. AssetPostprocessor for Assets/_TMinus/Art/Models/**/*.fbx: scale factor 1, no animation import, keep empties, add colliders (convex mesh colliders up to 255 triangles, else a box collider).
2. After import, create or update Assets/_TMinus/Prefabs/Parts/<file name>.prefab: add Part with partId = file name; convert children named SNAP_*, BOLT_##, PORT_*, PIPE_* into components exactly like procedural parts; map materials by name (Body_White, Band_Black, Insulation_Orange, Metal_Grey) to shared URP Lit materials.
3. ProceduralPartFactory first looks for a prefab with the same partId and falls back to procedural.
4. Tools/T-Minus/Validate Models: a window listing every part id in parts.json: model present? sockets found vs expected (BOLT count, PORT types)? size sane (within 50% of the data's length and diameter)? Show what to fix.

Acceptance check: drop in pathfinder_interstage.fbx with 12 BOLT and 2 SNAP empties; the prefab is created, the VAB uses it automatically, and the validator shows green.
```

### Prompt 17 — Polish, tutorial and accessibility · Tracks A + B · about 3 h · P1

```
Read CLAUDE.md. Polish pass. Do not change gameplay rules.
1. First-time tutorial on FloodWatch-1: 8 short HOUSTON-led steps (pick a part, slide in a board, plug a cable, torque bolts in order, read the budgets, run the checklist, launch, read the debrief). Skippable; remembers completion.
2. Audio (free or CC0 only, listed in CREDITS.md): UI clicks, plug-in, ratchet, bolt seated, countdown beeps, engine rumble low-pass filtered with altitude, staging bangs, clean-room and hangar ambience.
3. Feel: camera shake at ignition scaled by thrust, sparks on short circuits, a satisfying green flash on every correct connection.
4. Accessibility: colour-blind-safe cable palette plus patterns and labels; UI scale 100 / 125 / 150%; every action reachable by keyboard; pause anywhere; subtitles for voice callouts.
5. Settings: HOUSTON mode, HOUSTON language (English / Urdu), graphics quality (Low for weak laptops), volumes.
6. Performance: 60 fps on integrated graphics at Low (no post-processing, fewer particles); GPU instancing for bolts.

Acceptance checks: the tutorial runs start to finish; with a greyscale filter, the cable types are still distinguishable.
```

### Prompt 18 — Judge Mode, builds and README · Track D · about 2 h · P0

```
Read CLAUDE.md. Final ship prompt.

1. Judge Mode (main-menu button and F9): a scripted 3-minute run of FloodWatch-1 with a fixed random seed.
   a. A nearly finished CubeSat with one fault (RF cable missing). HOUSTON points to it; the presenter fixes it.
   b. The Pathfinder with Joint 1 bolted out of order and 3 bolts missing. Scan shows it; the presenter launches anyway; the joint fails at max-Q; the debrief explains why.
   c. "Fix and fly again" applies the fixes: orbit, CubeSat deploy, operations, a 3-star debrief and the Landsat flood-image reveal.
   Space skips any step. Nothing needs the network (HOUSTON answers are pre-cached for this script).
2. Builds: Windows x64 (primary) and WebGL (stretch: Brotli compression with Decompression Fallback on, Low quality default, HOUSTON's LLM off unless a proxy URL is set). Tools/T-Minus/Build All writes to /Builds.
3. README.md: what it is; the challenge; how to play; controls; team; tech; NASA data and resources used, with links; credits (NASA 3D Resources Saturn V by NASA/Michael D. Carbajal; NASA Earth Observatory Landsat images; sounds); AI tools used and how; known limits (2D flight plane, simple atmosphere, illustrative part values).
4. Final checks: no Console errors in any scene; Judge Mode runs identically 3 times in a row; the build runs on a second laptop.
```

## Demo and pitch: 3 minutes

Tell one story: a single badly bolted joint loses the mission, HOUSTON explains why, you fix it, and the satellite photographs the 2022 Sindh floods.

| Time | On screen | What you say |
| --- | --- | --- |
| 0:00–0:20 | Title slide | "In 1962, Pakistan launched its first rocket, Rehbar-I, from Sonmiani near Karachi, with NASA's help. We built a game so every student here can build one." |
| 0:20–0:40 | Problem slide | "Missions fail at the connections: Apollo 13's oxygen-tank failure began with damaged wire insulation. Yet learning tools usually teach orbits, rockets or subsystems separately; the challenge statement says so itself." |
| 0:40–2:10 | Judge Mode, live | Fix the missing RF cable with HOUSTON → launch the badly bolted rocket → the joint fails at max-Q → the debrief shows the cause → fix and fly → orbit, deploy → Landsat flood image |
| 2:10–2:35 | Validity slide | "Real equations: rocket equation, thrust vs air pressure, drag, round rotating Earth. Real data: Saturn V stage figures. Tested: our Saturn V lifts off at a thrust-to-weight of about 1.2, computed from that data." |
| 2:35–2:50 | Impact slide | "It runs offline on a school laptop, HOUSTON explains in English or Urdu, and it's free." |
| 2:50–3:00 | Roadmap slide | "Next: SLS and Artemis, an engine lab, and a classroom mode for teachers." |

**Questions judges will likely ask**

- *How is this different from Kerbal Space Program?* KSP is a sandbox of fictional parts. T-MINUS teaches real component-level integration with guided missions, explained failures and NASA data.
- *Is the physics real?* Show the four equations and the passing tests.
- *Does the AI make things up?* No: the rules decide, the AI only explains the facts it is given, and everything works offline.
- *Who will use it?* Students 12 and up in school computer labs, on low-end laptops, in English or Urdu.

**Submission checklist**

- [ ] Read the 2026 Project Submission Guide when it is released on Friday Nov 13 and match its fields and video rules.
- [ ] Project page: title, short summary, challenge = Space Mission Design Game, link to the playable build or demo video, GitHub link.
- [ ] Demo video: screen-record Judge Mode with a voice-over.
- [ ] List every NASA dataset and resource you used, with links.
- [ ] Say which AI tools you used and how.
- [ ] Upload the Windows build as a zip and, if it works, the WebGL build.
- [ ] Rehearse the pitch twice with a timer: one person drives, one talks.
- [ ] Submit by 8:00 PM Sunday; the hard deadline is 11:59 PM.

## Sources

- [Space Mission Design Game: challenge statement (copy on GitHub)](https://github.com/NASA-SPACEAPP-2026/Space-Mission-Design-Game)
- [NASA Space Apps: Team Formation Guide (start time, deadline, team size)](https://www.spaceappschallenge.org/resources/team-formation-guide/)
- [NASA Space Apps: Judging & Awards Guide](https://www.spaceappschallenge.org/resources/judging-awards-guide)
- [NASA Space Apps: Project Submission Guide](https://www.spaceappschallenge.org/resources/project-submission-guide/)
- [NASA Space Apps on X: statements live, resources on Nov 13](https://x.com/SpaceApps/status/2115481217965371893)
- [Unity 6 releases (6.3 LTS)](https://unity.com/releases/unity-6-releases)
- [MCP for Unity (CoplayDev)](https://github.com/CoplayDev/unity-mcp)
- [NASA 3D Resources: Saturn V](https://science.nasa.gov/3d-resources/saturn-v/)
- [Saturn V stage data (Wikipedia)](https://en.wikipedia.org/wiki/Saturn_V)
- [NASA Earth Observatory: Lake Manchar Is Overflowing (Landsat, 2022)](https://science.nasa.gov/earth/earth-observatory/lake-manchar-is-overflowing-150306/)
- [NASA CubeSat 101](https://www.nasa.gov/wp-content/uploads/2017/03/nasa_csli_cubesat_101_508.pdf)
- [Rehbar rocket family (Wikipedia)](<https://en.wikipedia.org/wiki/Rehbar_(rocket_family)>)
- Other entries for this challenge: [Delta-V: Mission Architect](https://github.com/MahiAlAraf/NASA), [TrajectIQ](https://github.com/Raj2003-bubt/TrajectIQ)

Not opened: the YouTube video you sent (rate-limited).
