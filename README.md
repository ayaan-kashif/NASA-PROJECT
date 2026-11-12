# T-MINUS

**T-MINUS: Rocket & Mission Builder** is an educational Unity game for the NASA Space Apps Challenge 2026 challenge **“Space Mission Design Game.”** Players integrate a CubeSat, assemble and wire its launch vehicle, fly an ascent, operate the satellite, and learn from a causal mission debrief. The game is aimed at students aged 12 and up and is designed to work offline.

## How to play

Open the Unity project in Unity **6000.6.8f1**. Run **Tools → T-Minus → Rebuild Scenes** if the editor asks for the project scenes, then open `Boot` and press Play. Choose the FloodWatch-1 mission, build and wire the CubeSat in the Integration Bay, assemble and bolt the Pathfinder in the VAB, complete the pad checklist and GO/NO-GO poll, then launch. The Debrief summarizes flight events, operations, scoring and suggested fixes.

### Controls

- VAB: drag parts onto the stack or off it to detach; right-drag to orbit the camera, middle-drag to pan, scroll to zoom. `Alt` + left-drag also orbits. `Ctrl+Z` / `Ctrl+Y` undo and redo assembly changes.
- Integration Bay: select components and ports in the interface to install and wire them; use the on-screen cable guidance and wiring check.
- Flight: use the HUD buttons for ignition/countdown, camera, autopilot, abort and time warp. Keyboard shortcuts: `C` changes camera; `1`/`2`/`3`/`4` select x1/x2/x4/x100 warp.
- Bolts and cables: follow the highlighted bolt sequence and connection prompts; inspect the checklist before launch.

## Scenes and technology

MissionBriefing → IntegrationBay → VAB → Flight → Debrief. Unity UI Toolkit builds the interfaces; flight dynamics use a fixed-step, double-precision C# solver in a 2D flight plane around a rotating Earth. Buildable scenes have idempotent `Tools → T-Minus` Editor menu builders. Mission and part definitions are data-driven JSON under `Assets/_TMinus/Resources/Data/`.

## NASA and science data

- **Landsat / Lake Manchar (2022 floods):** [NASA Earth Observatory — Lake Manchar Is Overflowing](https://science.nasa.gov/earth/earth-observatory/lake-manchar-is-overflowing-150306/). The debrief currently uses a design-scaled placeholder; the image files have not been added to the repository.
- **Saturn V:** stage data and history cite the [NASA Saturn V News Reference](https://www.nasa.gov/wp-content/uploads/2023/04/saturnvnewsreference.pdf) and [NASA's Saturn V page](https://www.nasa.gov/history/afj/ap11fj/launch.html). The project uses a procedural Saturn V representation; it does not currently bundle the NASA model.
- **Rehbar-I:** mission history and launch context are documented in the in-game codex; see the [NASA History overview of Pakistan's space cooperation](https://history.nasa.gov/). Verify and replace this general reference with a mission-specific source before public submission.

## Credits and AI use

See [CREDITS.md](CREDITS.md) for image credit, project acknowledgements, sound/model notes and AI-tool disclosure. AI coding assistants (Claude Code, OpenAI Codex and Antigravity) were used to draft and implement code, documentation and test support. Human team members reviewed and tested changes. HOUSTON provides deterministic in-game engineering guidance; optional AI explanations use a configurable proxy and are not required for gameplay. No provider API keys are stored in this repository.

## Known limits

- Flight is a 2D plane with a simplified atmosphere, drag and ground track.
- Part performance, spacecraft costs, mass budgets and some engineering thresholds are illustrative game values; check the data notes before treating them as flight-qualified design data.
- The Lake Manchar reward is a placeholder until licensed/credited Landsat image assets are added and visually processed.
- The local leaderboard is stored on the current machine and is not an online service.
- Windows x64 is the primary target; WebGL is a stretch target.

## License and sources

Project code and original assets are provided for the NASA Space Apps Challenge project demonstration. NASA imagery and referenced factual data remain subject to their source terms and credits. See [CREDITS.md](CREDITS.md) and the JSON `source` / `sourceUrl` fields for item-level attribution.
