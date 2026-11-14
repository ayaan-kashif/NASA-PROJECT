# Demo graphics: measured settings and demo-day checklist

Measured on the team's laptop (HP EliteBook 840 G5, Radeon RX 540 with 2 GB). Unity chose the Radeon by itself
(`SystemInfo.graphicsDeviceName` reads "Radeon RX 540 Series"). The development exe was run with `-perfprobe -judgeauto`,
which plays the whole of Judge Mode and times every room (see below). Measured on 2026-11-15.

## Presets (Tools > T-Minus > Graphics)

| Preset | MSAA | Shadow cascades | Shadow map | Soft shadows | SSAO | Render scale | Use |
|---|---|---|---|---|---|---|---|
| Restore Original | 4x | 4 | 2048 | High | full size | 1.0 | the project as it was |
| Make Faster (same look) | off | 2 | 2048 | Medium | half size | 1.0 | earlier, kept |
| **Demo Live** | off | 2 | 1024 | Medium | half size | 0.8 | **the live demo (set now)** |
| Demo Video | 4x | 4 | 4096 | High | full size | 1.0 | recording only (not yet timed) |

SMAA is still switched on for every camera by `SceneLook`, so Demo Live keeps smooth edges.

## Frame time, development exe at 1920x1080 (fps)

Each room is timed for 5 s while Judge Mode runs. The rows are the average over the room's samples.

| Room | Original | Demo Live |
|---|---|---|
| Main menu | 54 - 59 | 59 - 60 |
| Integration Bay | 30 - 31 | 39 - 43 |
| VAB | 36 | 57 |
| Flight | 57 - 58 | 55 - 59 |
| Debrief | 58 | 58 |

Single-frame spikes of 0.5 - 1.3 s appear at scene loads in the Bay, not during play.

## Editor check at 3840x2160 (the monitor's native size, a harder test)

| Room | Original | Demo Live |
|---|---|---|
| Main menu | 15.8 fps (later 18.2) | 37.8 fps |
| Integration Bay | 5.8 fps (later 6.0) | 18.5 fps |
| VAB | 10.0 fps (later 10.2) | 21.8 fps |
| Flight | 13.2 fps | 25.3 fps |

Only the exe at 1080p is the real target; the editor at 4K is a stress check.

## Look

Before and after screenshots of the Bay, VAB and Avionics Bay were compared. The layout, colours, labels and lighting are
the same. Shadows are a little softer, which is not visible at normal play distance.

## What was added

- `Tools > T-Minus > Graphics > Demo Live` and `Demo Video` (absolute values, safe to run twice).
- `Core/PerfProbe.cs` (development and editor builds only): the `PerfProbe.Measure` call, and `-perfprobe` for automatic timing per room.
- `Mission/JudgeMode.cs`: `-judgeauto` (development builds only) runs the demo by itself, so no keyboard is needed.
- `Tools > T-Minus > Build Windows (development, for performance tests)`: the same game with the probe, to `Builds/WindowsDev`.

## Demo-day checklist

1. **Graphics for the exe:** Windows Settings > System > Display > Graphics > Browse > the game's `T-MINUS.exe` > Options > High performance. (For the dev exe I set this in the registry at `HKCU\Software\Microsoft\DirectX\UserGpuPreferences`; check it there.)
2. **Preset:** Demo Live (the project default now). Run `Tools > T-Minus > Graphics > Report current values` before building; it should say MSAA 1, cascades 2, shadow map 1024, render scale 0.8.
3. **Build:** `Tools > T-Minus > Build Windows` (Demo Live is in the project), then run `Builds\Windows\T-MINUS.exe`.
4. **Plug in the power cable** and close other apps (browsers, video calls).
5. **Full screen 1920x1080,** Alt+Enter to switch to a window if needed.
6. **Test once before the judges arrive:** press F9 to play Judge Mode through to the end.
7. **Web build:** check the browser in a clean window. A black 3D view means the browser cannot run the post effects; the fallback then shows plain rendering.

## Not done yet

- **Art pass (LEDs, lighting per room, colour grade per scene, engine plume, smoke):** not started.
- **Unity Recorder and the demo video:** not started. The package has not been added.
- **Demo Video timing:** the preset exists but has not been measured.
- **Blender models for the stages and the gantry:** not started.
- **The exe measurement was with the probe on (development build), so it is a little slower than the real release build.**
