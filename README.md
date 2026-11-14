# T-MINUS

**T-MINUS: Rocket & Mission Builder** is an educational Unity game for the NASA Space Apps Challenge 2026 challenge **"Space Mission Design Game."** Players build a 3U CubeSat and its rocket part by part (bolts, cables, connectors), launch it with real physics, operate the satellite for 30 days, and get a debrief that explains every success and failure. HOUSTON, the in-game assistant, says where each part, bolt and cable goes and why. The game is for students aged 12+ and first-year university students, and it works fully offline.

## Play it

**In your browser:** https://ayaan-kashif.github.io/NASA-PROJECT/ (desktop Chrome, Edge or Firefox; about 26 MB to load).

## Run it

**Windows build:** open `Builds/Windows/T-MINUS.exe`.

**Update the web version:** Tools → T-Minus → Build WebGL, then `bash tools/publish_web.sh`. To make the build yourself, use **Tools → T-Minus → Build Windows** in Unity.

**In Unity:** open the project in **Unity 6000.6.8f1**. Open the `Boot` scene (**Tools → T-Minus → Open Boot Scene**) and press Play. If a scene is missing, run **Tools → T-Minus → Rebuild Scenes**.

## Judge Mode (3-minute demo)

Press **F9** anywhere, or choose **Judge Mode** in the main menu. It runs the whole story in six steps:
1. A build with a hidden fault.
2. HOUSTON finds the fault.
3. A launch that fails because of it, with the cause.
4. The fix.
5. A launch that reaches orbit.
6. 30 days of operations and the debrief with the Landsat reward.

**Space** goes to the next step and **Esc** exits.

## The loop

| Stage | What you do |
|---|---|
| Mission Briefing | Read the FloodWatch-1 brief (map the 2022 Pakistan floods), budget 7.72 M USD, success criteria. |
| Integration Bay | Install EPS, battery, deployment switch, computer, radio, antenna, ADCS, camera and solar panels; wire every power and data cable. Live power, data and mass budgets. |
| VAB | Stack Pathfinder (2 stages + fairing), fit and torque every bolt in star order over 3 passes, mount the CubeSat. Saturn V can be inspected. |
| Avionics Bay | Wire the flight computer, batteries, pyro separation, Safe/Arm plug and telemetry (11 cables). |
| Pad | Pre-launch checklist, pull the Remove-Before-Flight pin, arm, GO/NO-GO poll with 1–4 crew roles. |
| Flight | Double-precision physics ascent: gravity turn, max-Q, staging, fairing, orbit insertion, CubeSat deploy. |
| Operations + Debrief | 30 simulated days of power, storage and downlink; score with the cause of every failure; real Landsat images of Lake Manchar shown at your camera's resolution. |

**Failure Lab** has repair jobs from real history:
- *Broken Pathfinder:* find 3 seeded faults (missing bolts, an unplugged pyro cable, a cable in the wrong port), fix them, and hand the rocket back for a job debrief.
- *Apollo 12, 1969:* you are EECOM after the lightning strikes. Make the right calls in 20 seconds ("SCE to AUX", then reset the fuel cells). Every wrong call is explained.

**Main menu extras:**
- **Space Guide:** 28 searchable science topics, each with its source.
- **Settings:** HOUSTON on, hints only or off; HOUSTON answers in Urdu; volume; fullscreen.
- **Sandbox:** open any workshop directly.

## Controls

| Where | Keys and mouse |
|---|---|
| Everywhere | **H** HOUSTON panel · **N** next step · **Shift+S** scan the build · **F9** Judge Mode |
| VAB | Drag parts onto or off the stack · right-drag orbit · middle-drag pan · scroll zoom · **F** focus selected part · **Esc** deselect · **Ctrl+Z / Ctrl+Y** undo/redo |
| Bolting | Click a hole to fit a bolt, then torque it · follow the star order · **Esc** leaves the bolt tool |
| Wiring benches | Click a port, then another port, to run a cable · **Esc** cancels |
| Flight | **C** cycle camera (chase / ground / map) · **1 2 3 4** time warp x1/x2/x4/x100 · with autopilot off: **A/D** pitch, **Left Shift/Left Ctrl** throttle |

## Technology

- Unity 6000.6.8f1 with URP, C#, UI Toolkit, the new Input System and Newtonsoft JSON. No paid assets.
- **Data-driven:** parts, rockets, bolt patterns, missions, rules and the Space Guide (codex.json) live in JSON under `Assets/_TMinus/Resources/Data/`.
- **Flight physics:** a pure C#, double-precision, fixed-step solver. It flies in a 2D plane around a round, rotating Earth. Unity only displays the result.
- **Tests:** EditMode tests live in `Assets/_TMinus/Tests/EditMode/`. Run them in Window → General → Test Runner.
- **Scene builders:** every scene is built by an idempotent menu command under **Tools → T-Minus**.
- **HOUSTON's advice:** deterministic, and it works offline. Optional AI explanations go through a small proxy (`proxy/`), configured in `Assets/StreamingAssets/houston.config.json`. That file is git-ignored, and no API keys are stored in this repository.

## NASA and science data

- **Landsat / Lake Manchar (2022 floods):**
  - The debrief shows the three NASA Earth Observatory images (25 June, 28 August and 5 September 2022), downsampled to the player's camera GSD.
  - Each third of the image goal returned unlocks one more date.
  - Credit: "NASA Earth Observatory images by Joshua Stevens, using Landsat data from the U.S. Geological Survey."
  - Source: [Lake Manchar Is Overflowing](https://science.nasa.gov/earth/earth-observatory/lake-manchar-is-overflowing-150306/).
- **Earth from orbit:** NASA Blue Marble: Next Generation (credit "NASA Earth Observatory"). The globe is oriented so the real launch site sits under the pad.
- **Saturn V:**
  - Stage figures in the data files cite [Wikipedia's Saturn V article](https://en.wikipedia.org/wiki/Saturn_V) and are being cross-checked against the NASA Saturn V News Reference (the old link to it returned "404 Not Found" on 14 November 2026, so it is not linked). Entries marked "being fact-checked" in the game are not yet verified.
  - The model is a procedural representation.
- **Rehbar-I:** the history is in the in-game Space Guide. Verify it against a mission-specific source before public submission.

## Credits and AI use

See [CREDITS.md](CREDITS.md). AI coding assistants (Claude Code, OpenAI Codex and Antigravity) were used to draft and implement code, documentation and tests. The human team reviewed and tested the changes.

## Known limits

- Flight runs in a 2D plane with a simplified atmosphere.
- Near the ground, a real-scale launch site is drawn. From altitude, the globe is shown at 1/1000 scale.
- Part performance, costs and some thresholds are illustrative game values, not flight-qualified data.
- The leaderboard is local to the machine.
- The rocket always carries a fixed 150 kg payload mass in flight (a tuned game value), whatever CubeSat you build.
- Your build is saved automatically on this computer (in the browser's storage on the web version) and offered as **Continue** on the main menu. Clearing site data erases it; design codes are the backup.
- Urdu answers are experimental: they need the AI service. The game ships Noto Naskh Arabic (SIL Open Font License) for them, but the shaping has not been checked in a build yet.
- Windows x64 is the main target. WebGL is a stretch target.
