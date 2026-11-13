# Ending Liminal Sequence Performance Optimization Plan

**Project:** NyxoGame — Unity 6000.6.4f1  
**Target:** `PlayEndingVideoThenLiminalSequence` in `Assets/Scripts/Scene1/Scene1OpeningManager.cs`  
**Inspection date:** 2026-11-08  
**Scope:** Read-only inspection. No scripts, scenes, assets, prefabs, or project settings were changed. No build, benchmark, stress test, or intensive Profiler capture was run.

## Executive summary

The source contains two plausible costs that deserve focused measurement: the liminal environment is created synchronously from five imported OBJ models (including three terrain/path OBJ files whose source files are about 18–19 MB each), and runtime `MeshCollider`s are attached to every `MeshFilter` under the terrain and door models. While the liminal ending is active, `FindFirstObjectByType<Scene2EveningManager>` also runs once per frame.

These are **code-level candidates, not proven causes of the reported FPS decrease**. The Unity Editor was not in Play Mode and its active scene was `MainMenu`. A rendering-stat snapshot showed 25 render textures totaling 150,898,164 bytes, but zero draw calls/triangles and no active game rendering. That aggregate editor snapshot cannot be attributed to this sequence. No ending-scene frame-time baseline was available, and no safe runtime measurement was taken.

The H.264/AAC video is 1920×1080, 30 fps, 7.13 seconds, and 12.8 MB on disk. The sequence allocates a matching 1920×1080 ARGB32 RenderTexture (about 7.9 MiB of uncompressed color storage, excluding implementation overhead). Unity's actual decode path/hardware acceleration was not observable from the importer metadata or this idle Editor session.

## 1. Sequence architecture traced

The repeat-scene setup calls `PlayEndingVideoThenLiminalSequence` from the normal flow and the debug ending entry point. The coroutine:

1. Disables player controls, clears subtitles, and fades to black.
2. Creates a 1920×1080 ARGB32 RenderTexture, an overlay Canvas/RawImage, and a runtime VideoPlayer; assigns the clip and target, enables the clip's audio track, calls `Prepare`, and yields frames while waiting (up to 15 seconds) for preparation.
3. Starts playback while black, waits for the first decoded frame, fades in, then yields while video plays. A 12-second minimum playback timeout bounds the wait. It stops playback and fades back to black.
4. Destroys the temporary VideoPlayer and Canvas and releases/destroys the RenderTexture.
5. Calls `BuildLiminalEndingEnvironment` synchronously. The environment GameObject is placed at (4096, 0, 4096), initially inactive. It creates runtime materials and a ground plane, instantiates each non-null assigned model, applies ground/door materials, and adds MeshColliders to model_0 plus model_2 through model_4 where mesh filters exist. It activates the root, calls `Physics.SyncTransforms`, computes placement and bounds, creates 24 boundary wall colliders, calls `Physics.SyncTransforms` again, assigns the liminal sky and ambient settings, calls `DynamicGI.UpdateEnvironment`, and disables fog.
6. Teleports the player/camera to the liminal road, fades in, shows “What’s that?”, then sets the “Go to the door” objective and restores player controls.
7. While the ending is active, `Update` calls `UpdateLiminalEnding` each frame. That maintains the shell boundary and visibility, calculates distance to the door, looks up the Scene2 manager, updates the door prompt, and checks input. `KeepPlayerInsideLiminalShell` only obtains the character-controller array if the player crosses the shell boundary. Shell visibility iterates the renderers cached during construction.
8. The door interaction starts the coma-ending coroutine, which restores the previously captured render settings before returning the camera/player to the bedroom.

The assigned scene references are visible in `Assets/Scenes/Scene1_Opening_Repeat.unity`: one video, five model GameObjects, a sky panorama, and the moss/path texture. `model_2`, `model_3`, and `model_4` are the terrain/path meshes; model_1 is the shell; model_0 is the door.

## 2. Findings by system

### VideoPlayer and RenderTexture

- **Observed:** `VIDEO 1.mp4` is H.264 video plus AAC audio, 1920×1080 at 30 fps, 7.133 seconds, 12,812,907 bytes. The VideoClip importer has audio import enabled and no platform `targetSettings` entries in its small metadata file.
- **Observed:** The coroutine creates one non-mipmapped 1920×1080 `ARGB32` RenderTexture and explicitly releases it after the clip. It uses `Prepare` and yields while waiting rather than synchronously waiting for the video to prepare.
- **Not observed:** Whether Unity chooses hardware decoding on the target runtime, whether the backend performs extra conversions/copies, or the actual video frame time. Those depend on the runtime/platform and could not be safely measured here.
- **Assessment:** Video decoding and the full-resolution target can consume resources during the 7.13-second clip. They are less likely to explain a sustained FPS drop after playback because the player and target are stopped and destroyed before the liminal environment is built. No measured benefit can be assigned.

### Models, materials, and colliders

- **Observed:** The scene assigns five model GameObjects. The three ground/path OBJ sources are 18,388,917; 19,058,585; and 18,327,229 bytes (about 55.0 MB total on disk). The door OBJ is 336,074 bytes and shell OBJ is 70,178 bytes. These are source file sizes, not imported mesh-memory sizes or triangle counts.
- **Observed:** The OBJ importer metadata for `model_2` has mesh compression set to 0 and collider import disabled. The script adds a MeshCollider to each qualifying `MeshFilter` for terrain/path and door at runtime. Exact MeshFilter, submesh, vertex, triangle, and collider counts were not available from the safe inspection performed.
- **Observed:** All assigned models are instantiated one after another in one synchronous method. Mesh component traversal/material assignment and collider setup therefore occur together after the video, while the screen is black. Physics transforms are synchronized twice before the scene fades back in.
- **Assessment:** This is the strongest **one-time CPU/load-hitch candidate**. Runtime mesh collider cooking and synchronization can add CPU time and memory; the large source models and their renderers can also raise ongoing GPU vertex/shading work. The actual magnitudes and whether they cause the observed FPS change are unmeasured.
- **Materials:** The script creates one shared runtime ground material and one shared runtime door material and assigns them through renderer material arrays. No separate light objects are created in this method. No reliable draw-call count for the liminal scene was available.

### Textures, lighting, and global rendering

- **Observed:** The assigned sky panorama source is 650,743 bytes. The moss/path source texture is 10,781,994 bytes. Their importer metadata caps platform texture size at 2048 and enables texture compression. Final platform GPU allocations were not measured.
- **Observed:** The script captures `RenderSettings`, sets a liminal skybox and ambient colors, calls `DynamicGI.UpdateEnvironment` once, disables fog, and restores the saved settings at the bedroom transition. This is global render state, not a separate isolated camera volume.
- **Not observed:** Actual shadow-caster count, reflection-probe cost, post-processing, shader overdraw, GPU memory for this scene, or per-frame GPU time. No ending runtime frame was active for those measurements.
- **Assessment:** `DynamicGI.UpdateEnvironment` is a possible one-time transition spike and should be timed if an actual hitch coincides with the fade. The current code does not add per-frame realtime lights. The available aggregate RenderTexture memory (about 143.8 MiB) was captured in the idle Editor and is not evidence of ending-specific VRAM use.

### CPU, memory, and cleanup

- **Observed:** The liminal root is cached and `BuildLiminalEndingEnvironment` returns early if it already exists, so the same manager does not rebuild a second copy on repeat invocation. The video player, video Canvas, and target are explicitly destroyed/released after playback. The liminal environment and its runtime materials persist until the manager/scene is unloaded; the coma ending restores render settings but this path does not destroy the liminal environment.
- **Observed:** `FindFirstObjectByType<Scene2EveningManager>` is called every frame by `UpdateLiminalEnding`. CharacterController component lookup happens only when the player crosses the boundary, not every frame. The shell renderer array is cached.
- **Assessment:** The per-frame scene lookup is an avoidable CPU cost with a straightforward low-risk cache opportunity. It is unlikely, on source evidence alone, to explain a large GPU-bound FPS drop. No repeated allocations or garbage-collection spike has been measured. The persistent environment may be intentional for the sequence; unloading it must be weighed against whether the ending can be re-entered.

## 3. Bottleneck determination

| Candidate | Evidence available | Current conclusion |
|---|---|---|
| Video decode / RenderTexture | Clip metadata and source code only | Possible brief cost during video; hardware decode and frame cost unknown |
| Model instantiation | Five synchronous instantiations; ~55 MB of terrain OBJ source files | Strong one-time CPU/loading hitch candidate; no time measurement |
| MeshCollider creation/cooking | Runtime colliders added to every qualifying terrain/door MeshFilter | Strong one-time CPU/memory candidate; exact mesh and collider counts unknown |
| Ongoing mesh rendering | Large terrain meshes rendered after reveal | Possible sustained GPU/vertex cost; no triangles, batches, or GPU time captured in the ending scene |
| Materials/shaders/overdraw | Runtime materials are assigned; scene runtime not active | Not enough evidence to rank as a cause |
| Lighting / shadows / reflections | Sky/ambient settings and one GI update are visible in source | One-time GI spike possible; ongoing costs and shadow/probe counts unmeasured |
| Per-frame CPU management | Manager lookup each active frame; other loops cached or boundary-triggered | Easy low-risk improvement, likely smaller than mesh/collider work |
| RAM/VRAM pressure | Editor snapshot reports aggregate RT bytes only | Cannot attribute memory or infer pressure from this snapshot |
| Garbage collection | Static source inspection only | No GC data; do not claim a GC bottleneck |

**Conclusion:** There is not enough safe runtime evidence to declare one component the actual FPS bottleneck. Static evidence ranks synchronous model/collider setup as the leading transition-hitch candidate, with mesh rendering the leading candidate for sustained cost after the environment appears. These remain hypotheses until a short, focused, non-stress runtime capture records the same scene and camera before/after the sequence.

## 4. Ranked optimization opportunities

Benefits below are qualitative until measured; none is represented as a measured FPS gain.

| Rank | Current implementation | Recommended next step after approval | Expected CPU | Expected GPU | Expected RAM/VRAM | Risk and benefit estimate |
|---:|---|---|---|---|---|---|
| 1 | MeshColliders are added to every MeshFilter in terrain/path models and door model; Physics.SyncTransforms is called twice | First inspect imported mesh/submesh/collider counts. If the gameplay path permits it, replace detailed terrain collision with a small set of simplified collision proxies, retaining a reliable ground raycast and boundary. Avoid colliders on non-walkable/detail-only pieces. | Potentially large reduction in mesh-cooking/physics-sync work at transition; lower physics query cost | No direct rendering change | Lower duplicate cooked-collider memory | Low-to-medium gameplay risk: test walking, grounding, door trigger/raycast, and boundary. Likely best transition-hitch opportunity if collider count/complexity is high; no numeric estimate yet. |
| 2 | Five imported models are instantiated and traversed synchronously after video; all terrain meshes become visible together | Measure the one-time build duration and after-reveal frame time separately. If load hitch dominates, preload or instantiate the environment in controlled stages behind the existing black/video interval, avoiding overlap that worsens decode pressure. If sustained GPU cost dominates, consider LOD or offline mesh simplification for distant terrain while preserving the path silhouette. | Staging can hide/limit one-time main-thread work; simplification can reduce setup | LOD/simplification can reduce vertex work | Staging may temporarily raise peak memory; simpler meshes reduce mesh memory | Medium visual risk for silhouette/path and timing. Do not lower quality without image comparison. Potentially high if measured mesh cost is dominant. |
| 3 | `FindFirstObjectByType<Scene2EveningManager>` runs every liminal frame | Cache the manager once when the ending environment is prepared; refresh only if a scene transition invalidates it. Keep null-safe behavior. | Small positive reduction in per-frame CPU/search work | None expected | Negligible | Very low behavior risk; likely small benefit, but most certain low-cost code improvement. |
| 4 | `DynamicGI.UpdateEnvironment` runs after setting the liminal sky | Measure transition frame time. If it causes a hitch, prepare/update the environment lighting before reveal or use a precomputed/fixed ambient setup that matches the intended sky; preserve the captured/restored settings. | May reduce one-time GI CPU/setup spike | May reduce one-time environment-map update work | May reduce transient environment cubemap allocation | Medium visual risk: ambient lighting must still match the liminal look and restore correctly. Only do if the spike is observed. |
| 5 | Video uses a 1920×1080 ARGB32 target and H.264 1080p clip | Keep the current path unless a capture shows video/decode pressure. If confirmed, test a 1280×720 target/clip on the same display and compare image quality and decode time. Verify Unity runtime decode behavior on the target build platform. | Possible lower decode/texture transfer cost | Target storage falls from ~7.9 MiB to ~3.5 MiB; decode/render work may fall | ~4.4 MiB less base color target storage, before overhead | Visible video softness is the main risk. Expected benefit only during playback and unmeasured. |
| 6 | Runtime material arrays and temporary video objects are allocated; cleanup is explicit for video but environment remains until scene unload | Preserve explicit video cleanup. Review persistent environment lifetime only if re-entry or memory snapshots show retained memory. Avoid pooling unless repeated sequence entry is confirmed. | Minor allocation/setup savings possible | None expected | Could free liminal assets earlier if safe | Low performance priority; destroying/reloading can create a later hitch or break re-entry. |

## 5. Recommended implementation and validation order

1. After explicit approval, capture a short, focused Profiler sample of only the ending sequence on the actual target runtime. Record CPU frame time, GPU frame time, main-thread spikes, physics/mesh cooking, GC allocations, batches/triangles, and memory before video, during video, during the black transition, and after liminal reveal. This should be a normal playthrough sample, not a stress test.
2. Inspect imported mesh statistics for only the five assigned models and exact MeshFilter/MeshCollider counts. Do not broadly scan or reimport assets.
3. If the transition hitch correlates with colliders, prototype simplified terrain collision first and verify ground raycast/player movement. If the sustained cost correlates with rendering, test an LOD/simplified terrain representation while comparing the road silhouette and door framing.
4. Cache the Scene2 manager reference as a small isolated change; compare the same capture.
5. Address the GI update or video target only when the capture attributes cost there. Keep resolution, timing, sound sync, dialogue, camera composition, lighting mood, and scene transition intact unless a measured optimization requires a narrowly scoped visual tradeoff approved by the user.
6. Re-run only the same lightweight sequence capture and check visuals, dialogue/audio sync, ground collision, door interaction, and restoration to the bedroom. No full build is needed for this diagnostic/optimization loop while the training process is active.

## 6. Safety and verification record

- Unity Editor state checked through Unity MCP: Unity 6000.6.4f1, active scene `Assets/Scenes/MainMenu.unity`, Play Mode off, Editor idle.
- Rendering snapshot: zero draw calls, batches, triangles, and vertices at capture; 25 aggregate RenderTextures, 150,898,164 bytes. This is not a liminal runtime measurement.
- Video metadata read without transcoding or decoding the clip: H.264/AAC, 1920×1080, 30 fps, 7.133 seconds, 12.8 MB.
- Source files and relevant importer metadata were read narrowly. No model was opened/reimported, and no full asset scan was performed.
- No build, benchmark, stress test, intensive Profiler capture, script/scene/project-setting edit, or background-process action was performed.
- The requested optimization plan is complete. **Implementation has not started and awaits explicit approval.**
