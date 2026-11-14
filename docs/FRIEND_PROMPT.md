# Prompt for the Unity machine's Claude Code (copy everything below the line)

---

You are working on T-MINUS, a Unity 6 (6000.6.8f1, URP) educational space-mission game. Read `CLAUDE.md`, `docs/PLAN.md`, `docs/PROGRESS.md`, `AGENTS.md`, `FEATURES_AUDIT_AND_ROADMAP.md` and `COMPLETE_PROJECT_AUDIT.md` first.

## Situation
The other developer has no Unity or Blender. For one long session another Claude wrote a lot of C#, JSON, USS and web-page code **without ever compiling or running it**. You are the first to open it in Unity. Your job is to make it compile, prove it works, measure it, and then make the graphics better on weak GPUs. Fix what is broken; do not redesign. Do not change anything major, and never leave the project in a state that does not compile.

Rules from CLAUDE.md still apply: data-driven JSON, scripts under 300 lines, namespaces and asmdefs as they are, no new packages without asking, any scene or prefab setup is an idempotent `Tools/T-Minus/...` menu command, no API keys in git. Commit locally in small steps (`git add` only what you changed, message ends with the attribution lines your harness gives you). **Do not push without the user saying so.**

## What was built without being compiled (all unverified)
- Gameplay: VAB button to checklist, single countdown, no forced debrief, manual flight, `FlightSim.Advance` fractional-step fix (x1/x2 never advanced at 60 fps before), Safe/Arm gates ignition, `OpsWiring`, scoring and team-score fixes, `MissionGoals`, `MissionLog`, `DebriefNarration`, "Show me why" (`WhyTarget`/`FocusRequest`), save/continue (`SaveService`/`AutoSave`), `DesignValidator`, undo (`UndoStack`), first-build guide, Space Guide quiz, accessibility settings, "What if?" panel, pause menu, engine sound.
- Missions: Moonshot (flight-only, Saturn V with a guidance profile in `rockets.json`) and Rehbar-I (new rocket `rehbar_nike_cajun`, apogee ending). Their numbers came from a JavaScript port of the solver in `tools/sim/`.
- Replay: slow-motion "Watch the failure again", plus a time slider (`FlightController.ScrubTo`, `ReplayScrubber`) that flies a fresh copy of the design to any second with a throwaway `EventLog`.
- Crew and class: remote crew by pad and vote codes (`CrewCodes`, `BriefingUI.Crew.cs`), live room over ntfy.sh (`CrewRelay`; the host's pad code is sent through the room too), live class room (debrief "Send result to my class" and `tools/web/teacher.html`), real NASA imagery regions (`LiveImagery`), "Save picture" button (`ShareCard`, `Plugins/WebGL/Download.jslib`).
- Text: `Strings`/`strings.json` (about 115 entries, `Strings.Format` for interpolated text, made by `tools/extract_strings.py`), Urdu font and USS.
- Web/release: `tools/build_release.sh`, `tools/publish_web.sh`, pages in `tools/web/`, the HOUSTON proxy in `proxy/` (origin allow-list, caps).
- Speed-ups already committed in `d93710a`: the flight HUD rewrites numbers 10 times a second (`FlightHud.TextInterval`; set it to 0 to go back), cached max thrust, cached VAB camera, vsync on in desktop builds.
- Graphics tool just added: `Assets/_TMinus/Editor/GraphicsTuner.cs` (Tools > T-Minus > Graphics).

## Do these in order, and report after each step
1. **Pull and open.** `git pull` (Git LFS is used: run `git lfs pull`). Open the project in Unity 6000.6.8f1. Let it import. Pause OneDrive sync if files are locked.
2. **Compile.** Read the Console (or `%LOCALAPPDATA%/Unity/Editor/Editor.log`). Fix every compile error with the smallest change. Known risk areas: `FlightController.Scrub.cs`, `ReplayScrubber.cs`, `ShareCard.cs`, `CrewRelay.cs`, `BriefingUI.Crew.cs`, `DebriefUI.cs`, `GraphicsTuner.cs`, `Strings.Format` call sites, `LiveImagery.cs`. Commit the fixes.
3. **Run the tests.** Test Runner, EditMode then PlayMode. Fix failures by finding which side is wrong (code or test). The key ones: `FlightPhysicsTests.SaturnVReachesTheParkingOrbit...` and `RehbarRocketClimbs...` (if they fail, the C# flight code and `tools/sim/*.js` disagree: re-tune the data, not the physics), `ScrubbingARewoundFlightShowsTheSameMomentAsTheOriginal`, `StringsTests`, `CrewRelayTests`, `LiveImageryTests`, `FlightPhysicsTests` generally.
4. **Play the golden path:** Main menu, mission briefing, Integration Bay (build and wire the CubeSat), VAB (assemble the rocket, bolt, wire), pre-launch checklist and GO/NO-GO, countdown, flight at x1/x2/x4, debrief, "Show me why", "Watch the failure again" and drag the replay slider, Fly again. Also play Judge Mode, a Failure Lab repair job, Moonshot and Rehbar-I. Note anything that errors in the Console or looks wrong, fix the small things, list the big ones.
5. **Measure speed.** Profiler on the Flight scene: record "GC Alloc" per frame and frame time with the speed-up commit `d93710a` and with its parent `d93710a~1` (use `git worktree add ../t-minus-before d93710a~1` and open that copy). Also run **Tools > T-Minus > Build WebGL** and read `Logs/build-webgl.txt` (size and time). Report numbers, not guesses.
6. **Graphics, step 1 (faster, same look).** Run **Tools > T-Minus > Graphics > Make Faster (same look)** (drops MSAA 4x because SMAA is already on every camera, 2 shadow cascades, Medium soft shadows, half-size SSAO). Take before/after screenshots of Bay, VAB, Flight and Main menu, and the frame time. If it looks worse, run **Restore Original**.
7. **Graphics, step 2 (better look, still cheap for weak GPUs).** Goal: a more professional look without a strong graphics card. Use only free built-in URP features:
   - Bake lighting for the static indoor scenes (Integration Bay, VAB): mark static geometry Contribute GI, bake lightmaps at modest resolution, add light probes (or Adaptive Probe Volumes) for the moving parts, then switch SSAO off in those scenes if the baked result already shades the corners.
   - Emissive materials for LEDs, screens and warning strips (with the existing bloom), a strong key light plus a coloured rim light per scene, light fog in the VAB, a colour-grading look per scene (warm pad, cool clean room, deep space), better camera framing, small particle plumes.
   - Do it as idempotent `Tools/T-Minus/...` menu commands, keep lightmaps small (the web build downloads them), keep a Low/Medium/High option in mind (`SettingsStore.LowGraphics` exists), and avoid real-time reflection probes, volumetric fog, SSR, depth of field, TAA plus MSAA, and many real-time shadowed lights.
   - Compare screenshots and frame time after each change. Keep what is clearly better.
8. **Web build and link.** Build WebGL, test it in a browser (the 3D view must not be black: a previous black-screen bug was fixed by keeping URP post-processing shaders; see `SceneLook.cs` and `WebGLBuilder.cs`). Then `tools/publish_web.sh` only if the user says to publish. The GitHub Pages link returned 404 at last check; ask the user to enable Pages for the `gh-pages` branch.
9. **HOUSTON proxy.** Tell the user to redeploy `proxy/` and set `ALLOWED_ORIGINS` (and optionally `HOUSTON_CLIENT_TOKEN`). Never put keys in git.
10. **Write back.** Append a short entry to `docs/PROGRESS.md` for what you fixed and measured. Then give the user a report: what compiled, which tests passed or failed, the speed numbers, the screenshots, and what is still broken. Ask before pushing.

## Not built on purpose
F18 billing (needs a payment provider and licence terms; the roadmap says run a school pilot first), removing the AI packages from the dev project (it would break the Unity MCP setup; `tools/build_release.sh` builds without them), and billing or server-backed class accounts. Do not build these unless the user asks.
