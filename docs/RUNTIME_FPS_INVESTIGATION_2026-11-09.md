# Runtime FPS Investigation (2026-11-09)

**Scope:** Scene 1 (`Scene1_Opening`), Scene 2 (`World_Master` + `Scene2_Evening` additive), `World_Master` alone, and the ending composite (`World_Master` + `Scene2_Evening` + `Scene1_Opening_Repeat`).
**Method:** short Play Mode samples in the Editor (10 s after ~4-5 s warm-up), Game View maximized to 1593x896, PC quality profile untouched, vSync off. No build, no full Profiler session, no asset reimport.

## 1. Final state of the shipped change

Only one runtime change remains: `Assets/Scripts/World/VegetationCuller.cs` caps **vegetation shadow casting at 45 m** (6 m hysteresis). It never disables a renderer, so every grass, fern and tree instance is still drawn at every distance. There is no distance hide and no grass removal.

An earlier version of this component also hid small ground scatter past 90 m. That was wrong: it removed grass the user could see, and later measurements showed it bought nothing. It was deleted, the authored states were preserved through a renderer-keyed cache, and the scene was re-verified with **22,859 of 22,859 renderers active** and grass present at the spawn point.

## 2. Measured results

All numbers are in-Editor, 1593x896, PC quality, render scale 1.5, 4x MSAA, shadow distance 120 m. `main` = CPU main thread (FrameTiming), `gpu` = GPU frame time.

| Probe | FPS | main ms | gpu ms | Renderers | Shadow casters |
|---|---|---|---|---|---|
| Scene 2, before culler | 30.65 | 24.20 | 24.69 | 22,859 | 18,404 |
| Scene 2, before culler | 31.81 | 22.23 | 21.71 | 22,859 | 18,404 |
| Scene 2, before culler | 28.60 | 31.50 | 17.30 | 22,859 | 18,404 |
| Scene 2, before culler | 27.81 | 36.43 | 28.94 | 22,859 | 18,404 |
| **Scene 2, culler on** | **34.81** | 19.04 | 17.67 | 22,859 | 2,848 |
| **Scene 2, culler on** | **33.12** | 21.12 | 18.23 | 22,859 | 3,196 |
| Scene 2, ALL vegetation deactivated | 34.48 | 19.83 | 17.64 | 1,461 | 1,326 |
| `World_Master` alone | 35.93 | 27.26 | 18.26 | 22,674 | 3,051 |
| Scene 1 (`Scene1_Opening`) | 57.96 | 7.41 | 2.07 | 473 | 455 |
| Ending composite | 34.70 | 19.04 | 12.18 | 5,722 | 1,781 |

Baseline averages 29.7 FPS / 33.8 ms; with the shadow cap 33.9-34.8 FPS / ~29.6 ms, GPU ~20.4 → ~15.9 ms, shadow casters 11,337 → ~3,000-3,500, triangles 107.9M → 29.8M. The difference is real and repeatable in GPU time; the frame-rate gain in the Editor is limited because the main thread, not the GPU, is the ceiling (§3).

## 3. What the follow-up tests proved

- **Grass is not the problem.** Deactivating the entire `Vegetation` root (21,398 renderers, all grass included) gave 34.48 FPS versus 34.81 FPS with all of it drawn. Removing grass cannot raise FPS; it only removes visuals. Grass is restored.
- **Scene 2's story manager is not the problem.** `World_Master` loaded alone (22,674 renderers, 100 lights, no `Scene2_Evening`) costs the same as the full Scene 2 (35.93 vs ~34.8 FPS, main 27.3 vs ~19-21 ms, same GPU). `Scene2_Evening` adds only ~185 renderers; its scripting was separately measured at ~0.3 ms (`BehaviourUpdate`).
- **The GPU is not the ceiling in the Editor.** Main thread (19-27 ms) is consistently above GPU (15-18 ms). Cutting GPU work therefore cannot raise in-Editor FPS, which is why the SSAO experiment below showed nothing.
- **SSAO off did not help.** Measured 18.66 FPS / main 65.7 ms under heavy external CPU load, GPU 15.13 vs 15.26 ms baseline: no GPU saving worth the lost contact shading. Reverted to on (verified `isActive = True`).
- **Scene 1 is healthy.** ~58-64 FPS, main 7.4-8.6 ms, GPU 2-8 ms, 473 renderers. The vegetation culler is inert there.
- **The Editor floor.** Emptying Scene 2 of vegetation still leaves ~34.5 FPS at ~20 ms main thread, and Scene 1 at 473 renderers runs 58 FPS. The remaining cost tracks the world's object/light count (22.6k renderers, ~100 point lights) and is Editor-side per-frame work, not scripts, physics or the shadow pass (the shadow pass was already reduced 69%).

Invalid samples that must not be quoted: any run with the Profiler enabled (5.9 FPS / 142 ms — enabling it distorts the Editor), the "directional shadows off" run and the "no shadows" run (GPU 0.01-0.02 ms, FPS does not rise — rendering broke, as with the earlier cyan-frame case), and every run timestamped 19:33-20:23 where CPU was 84-100% from external processes.

## 4. The Scene 1 → Scene 2 handoff (2026-11-10)

The opening already preloads the whole world behind the YESTERDAY title card. `Scene1OpeningManager.PrepareScene2Transition` loads `World_Master` then `Scene2_Evening` additively while the card is on screen, holds activation at 90%, and only activates during the blackout. A probe that played the real opening and recorded every frame found the loads are already hidden:

- Opening phase (card on screen): 20.6 ms average, but six frames of 1.15-1.92 s — those are the world and scene loads, all behind the card.
- First 10 s of Scene 2: 40.2 ms average, 81 of 276 frames over 33 ms, spread evenly across the ten seconds rather than decaying.

A second, repeated run (fast handoff, CPU 70-97% from external load) gave per-second averages of 32-41 ms for all ten seconds, again flat. So Scene 2 does not start slow because it "loads at once"; the frame cost after the handoff is the same steady-state cost measured in §2, just sampled while the machine was busy.

One real gap was fixed: the opening scene's fade overlay dies with the scene, so the reveal landed exactly on the opening-scene teardown. `Assets/Scripts/UI/SceneHandoffCover.cs` keeps a persistent black cover across activation and unload and fades it out after Scene 2 has rendered a few frames (0.45 s, 10 s timeout so a failed unload cannot leave the player on black). Verified in Play Mode: the cover was present during the handoff, was destroyed afterwards, and no errors were logged. This changes what the player sees during the handoff, not the frame rate.

## 5. The "very low FPS" windows are machine contention

The Editor runs ~34-36 FPS when the machine is quiet and drops to 18-25 FPS when the CPU is saturated. During the low samples, `Resource Monitor`/`Get-Counter` showed total CPU 84-100% from processes outside Unity — a training workload (`scripts/train_python_specialization.py --dry-run`, ~95-98% CPU in two processes), two `faceless-shorts/scripts/words.py` processes (~140-180%), Windows Defender `MsMpEng` (~87%), and `ServerProgram`. GPU stayed at 5-25%. In those windows the main thread balloons from ~20 ms to 37-80 ms while the GPU stays at 15-22 ms.

Those processes are outside this project and were deliberately not touched: stopping, pausing, reprioritizing or reconfiguring them was prohibited. The game itself does not consume that CPU up to a fixed cap; it is slowed by the contention.

## 6. What would actually raise FPS

1. **A standalone player build.** The in-Editor number is a lower bound, not a prediction: Play Mode adds editor-only per-frame work and runs at a 1593x896 maximized Game View with 1.5 render scale (≈2390x1344 internal) and 4x MSAA. Builds were disallowed in this environment, so this is unverified. It is the single highest-value next measurement.
2. **Freeing machine CPU.** The observed 18-25 FPS windows correlate with external saturation, not with anything in the scene.
3. **Further GPU-only settings** (render scale, MSAA, shadow distance) would not help the Editor's main-thread ceiling and would all be visible quality regressions, so they were left untouched. `QualitySettings.shadows` has no effect on URP; only per-renderer `shadowCastingMode`, which the culler already uses, works.

Not done on purpose: builds, intensive Profiler sessions, asset reimports, stress runs. No graphics setting was changed (MSAA 4, render scale 1.5, shadow distance 120 m, SSAO on, volume profile unchanged).

## 7. Reproducing

1. Open the project, leave the Editor out of Play Mode.
2. `Tools/Perf Probe/Scene 2 Evening (+World_Master)` for the hot path, `Scene 1 Opening (single)`, `World_Master only`, `Repeat Ending Composite`.
3. Each run writes `Temp/frame_probe_<mode>_<timestamp>.json` and exits Play Mode. `Scene 2 - Screenshot Baseline` also writes a PNG.

Editor-only diagnostics: `Assets/Scripts/Diagnostics/FrameProbe.cs` and `Assets/Editor/FrameProbeMenu.cs`. Both are `#if UNITY_EDITOR` / `Assets/Editor` and never ship in a player build. Diagnostic-only menu entries that produced no usable data: No Shadows, Real No Shadows, No Point Lights, Vegetation Root Off, Directional Shadows Off, Post Volumes Off, Profiler Counters.
