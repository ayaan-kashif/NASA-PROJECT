# T-MINUS — brief for AI coding agents (read before every task)

The full plan and the prompt pack (Prompts 0–18) live in `docs/PLAN.md`. Progress lives in `docs/PROGRESS.md`. Read both at the start of a session.

## What we are building
T-MINUS is a 3D educational space-mission game for the NASA Space Apps Challenge 2026, challenge "Space Mission Design Game". Inspired by PC Building Simulator: players build a 3U CubeSat and its rocket part by part (bolts, cables, connectors), launch it with real physics, operate the satellite, and get a debrief that explains every success and failure. HOUSTON, an in-game AI assistant, recommends where each bolt, cable and part goes and explains why, and shows how a different choice changes the outcome.

## Core loop
Mission Briefing -> Integration Bay (build + wire the CubeSat) -> VAB (assemble, bolt and wire the rocket; mount the payload) -> pre-launch checklist + GO/NO-GO poll (1-4 players with roles) -> Flight (physics ascent, staging, deploy) -> Operations (power, data, downlink over mission days) -> Debrief (score + causes) -> Redesign -> fly again.

## Audience
Students aged 12+ and first-year university students. Every number on screen has a one-line "why". Friendly and clear, never childish. English UI; HOUSTON can also answer in Urdu.

## Tech stack (ask before changing)
- Unity 6000.6.8f1 (Unity 6), URP, C#, new Input System, UI Toolkit (UXML + USS) for all UI, Newtonsoft JSON (com.unity.nuget.newtonsoft-json).
- No paid assets. Ask before adding any package.
- Targets: Windows x64 (main demo) and WebGL (stretch).
- Root folder Assets/_TMinus/. Namespaces: TMinus.Core, TMinus.Assembly, TMinus.Wiring, TMinus.Spacecraft, TMinus.Flight, TMinus.Mission, TMinus.Houston, TMinus.UI, TMinus.EditorTools. One .asmdef per Scripts subfolder; tests in Assets/_TMinus/Tests/EditMode.
- Blender 5.3 for models (FBX export into Assets/_TMinus/Art/Models/Parts/, file name = part id).
- Unity MCP: Unity's own relay (`~/.unity/relay/relay_win.exe --mcp`, from com.unity.ai.assistant) is configured as the `unity-mcp` server.

## Architecture rules
1. Data-driven: parts, ports, bolt patterns, rockets, missions, rules and codex text live in JSON in Assets/_TMinus/Resources/Data/. Never hardcode stats in code.
2. Sockets by name: child transforms named SNAP_*, BOLT_##, PORT_<TYPE>_<NAME> and PIPE_* are auto-registered as snap nodes, bolt holes, ports and pipe ends.
3. Scenes: Boot, MainMenu, MissionBriefing, IntegrationBay, VAB, Flight, Debrief. One owner per scene; never edit another track's scene, only call its public C# API. Cross-scene state lives in GameState (the only singleton).
4. Every gameplay event goes to EventLog (time, system, code, message, causeId, severity). The debrief and HOUSTON read it.
5. Flight physics is pure C#, double precision, fixed timestep, 2D plane over a round rotating Earth. Never use Rigidbody for flight. Unity transforms only display the result (floating origin).
6. Editor automation: any scene or prefab setup gets a menu command under Tools/T-Minus/ that builds it idempotently. Never ask the user to hand-wire more than 3 Inspector fields.
7. Placeholders first: if a model is missing, generate a procedural one with the same socket names.
8. Keep scripts under 300 lines. If you change a public API another track uses, note it in docs/PROGRESS.md.
9. No API keys in code or in git. HOUSTON's LLM calls go to a configurable proxy URL (Assets/StreamingAssets/houston.config.json, git-ignored).
10. Colour is never the only signal: cables and states also use patterns, icons and text.

## Scoring (from the challenge)
Mission performance, scientific value, efficient resource use, decision quality (hints used, faults fixed before launch), team collaboration (crew roles).

## Working agreement
- Before coding: restate the task in 3 bullets and list the files you will create or change.
- After coding: list changed files, give exact Unity test steps, and append a 3-line entry to docs/PROGRESS.md.
- If something is ambiguous, pick the simplest option that keeps the demo working, and say what you picked.
- If Unity MCP tools are available, use them to create objects, inspect scenes and read the Console. Otherwise write Editor menu builders and check compile errors in `%LOCALAPPDATA%/Unity/Editor/Editor.log`.
- The project sits inside OneDrive: if Unity reports locked files, pause OneDrive sync.
