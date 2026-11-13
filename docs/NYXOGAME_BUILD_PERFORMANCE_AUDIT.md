# NyxoGame Unity Build Performance Audit

**Audit date:** 2026-11-07  
**Mode:** Read-only inspection; no build, clean build, Build And Run, shader benchmark, profiler session, package change, or scene load was performed.

## Executive findings

- Unity MCP connected to the loaded **NyxoGame** project at `C:/Users/Antplay/NyxoGame`, Unity **6000.6.4f1**, Windows Editor. The active scene was `Assets/Scenes/MainMenu.unity`; it was not dirty and Unity was not compiling or in Play Mode.
- **Largest serialized/build scene candidate:** `World_Master.unity` (18.02 MiB, 2,124 GameObjects, 14,010 serialized Unity records). This is a scene-size/structure ranking, not proof it dominates build time.
- **Largest renderer/physics candidate:** `Scene1_Opening.unity` and `Scene1_Opening_Repeat.unity` (each has 472 MeshRenderers, 189 MeshColliders, and 198 BoxColliders in serialized scene data). These are stronger runtime draw-submission and physics candidates than their 1.56 MiB file size suggests.
- **Heaviest cinematic candidate:** the code-driven liminal ending sequence in `Scene1OpeningManager` (`PlayEndingVideoThenLiminalSequence`). It uses a VideoPlayer/RenderTexture and instantiates a configured environment-model array. The project search found no Timeline assets and no PlayableDirector in the inspected scenes. This sequence was not run or profiled.
- **Cause of reported build CPU saturation/crashes: NOT CONFIRMED.** No previous build log or crash dump was inspected, and no build was run. Unity build work such as C# compilation, shader-variant compilation, asset processing, serialization, linking, and compression is primarily CPU/disk work; low GPU use during a build is therefore plausible and does not by itself identify a fault.
- The latest host snapshot during this audit was **52.5% CPU**, **5,859 MB available of 24,536 MB RAM**, and **4% GPU**, with **1,936 MiB used / 9,919 MiB free of 12,227 MiB VRAM**. Earlier short snapshots ranged from 52.5–79.2% CPU and 4–9% GPU. These were idle-editor/audit snapshots, not measurements during a build.
- The training process identity and its current per-process resource use could not be confirmed. The first process snapshot showed a `python.exe` process, but did not establish that it was the training job; NVIDIA's compute-process query did not reveal some process names due to insufficient permissions. No process was stopped, suspended, restarted, or modified.

## 1. Unity MCP and project verification

Unity MCP listed one connected instance: `NyxoGame@867f2434fa9e32f3`. Project info reported:

- Project root: `C:/Users/Antplay/NyxoGame`
- Unity: `6000.6.4f1`
- Editor platform: Windows; project build platform: `StandaloneWindows64`
- Active scene: `Assets/Scenes/MainMenu.unity`, build index 0, clean, loaded, 3 root objects
- Play Mode: off; compilation: off; asset update: off

The live `MainMenu` hierarchy contains three active roots and no children: `Main Menu` (`MainMenuController`), `Main Camera` (`Camera`, `AudioListener`, `UniversalAdditionalCameraData`), and `Directional Light` (`Light`, `UniversalAdditionalLightData`). The live camera query reports a 60-degree field of view and no Cinemachine camera/Brain.

Unity MCP's hierarchy query for an unopened scene path returned the active `MainMenu` hierarchy again. I did not load or switch scenes to force hierarchy inspection. For the four unopened build scenes, the report uses bounded, read-only metadata from those five exact scene files plus Unity MCP build/asset queries; full live hierarchies, enabled states, and component properties remain unverified.

## 2. Build scene inventory

Unity MCP Build Settings reports all five scenes enabled and in this order. The active Windows profile does not override the global scene list.

| Build index | Scene | File size | Serialized records | GameObjects | MeshRenderers | MeshColliders | BoxColliders | Lights | Cameras | Other notable components |
|---:|---|---:|---:|---:|---:|---:|---:|---:|---:|---|
| 0 | `MainMenu` | 0.01 MiB | 17 | 3 | 0 | 0 | 0 | 1 | 1 | 3 MonoBehaviours; all 3 roots active in live MCP hierarchy |
| 1 | `Scene1_Opening` | 1.56 MiB | 2,534 | 566 | 472 | 189 | 198 | 8 | 2 | 4 reflection probes; 23 MonoBehaviours; 516 GameObjects have nonzero static flags |
| 2 | `World_Master` | 18.02 MiB | 14,010 | 2,124 | 94 | 64 | 52 | 12 | 1 | 11 Animators; 2 Rigidbody; 2 reflection probes; 156 MonoBehaviours |
| 3 | `Scene2_Evening` | 0.01 MiB | 8 | 1 | 0 | 0 | 0 | 0 | 0 | One root named `Scene2_Content`, with `Scene2EveningManager` |
| 4 | `Scene1_Opening_Repeat` | 1.56 MiB | 2,534 | 566 | 472 | 189 | 198 | 8 | 2 | 4 reflection probes; 23 MonoBehaviours; 516 GameObjects have nonzero static flags |

“Serialized records” counts Unity YAML object records, including settings/root records; it is not a runtime object count or triangle count. Component counts come from the serialized scene files. For the unopened scenes they do not establish which objects are enabled at runtime. The MainMenu active state is verified directly through Unity MCP.

No ParticleSystem, Terrain component, Timeline asset, or `PlayableDirector` was found in these five scenes. The bounded asset queries returned zero `*.playable` or `TimelineAsset` results. This does not rule out runtime-created effects or assets stored outside the searched asset types.

## 3. Sequence and Timeline inventory

There is no Unity Timeline asset inventory to rank from the available search results. The cinematic logic is implemented in `Assets/Scripts/Scene1/Scene1OpeningManager.cs` as coroutines and runtime setup. The script contains the opening, repeat confrontation, bedroom dream, ending-video/liminal environment, and coma-ending sequences, plus an asynchronous transition that loads `World_Master` and `Scene2_Evening`.

The strongest candidate for the most resource-heavy cinematic is `PlayEndingVideoThenLiminalSequence`: the script assigns a `VideoClip` to a VideoPlayer using RenderTexture output and builds a liminal environment by instantiating configured models. That can add runtime video decode, memory bandwidth, renderer, and asset-load work. Exact video resolution/codec, model count/triangle count, and GPU decode behavior were not verified.

The coma ending is another sequence to watch: the script loads a doctor model and uses `AnimationClipPlayable`/`PlayableGraph` for idle animation. That is a Playables graph, not evidence of a Timeline asset. Neither sequence was executed during this audit, so their actual frame cost is unknown.

## 4. Scene and asset complexity assessment

### Scene ranking

1. **World_Master — largest serialized scene and most GameObjects.** Its file is about 11.5 times the size of either opening scene and has 2,124 GameObjects plus 156 MonoBehaviours. This makes it the leading scene-serialization, load/activation, and object-management candidate. Its 94 renderers are fewer than either opening scene.
2. **Scene1_Opening / Scene1_Opening_Repeat — strongest rendering and physics candidates.** Each has 472 MeshRenderers/MeshFilters, 387 MeshCollider plus BoxCollider components, 8 lights, and 4 reflection probes. Many GameObjects carry static flags. Actual draw calls, triangles, material count, collider complexity, and shadow settings were not available.
3. **Scene2_Evening — tiny serialized scene, but do not infer tiny runtime cost.** It has a single manager root; that manager can drive gameplay and dynamic activity that is not represented by scene object count alone.
4. **MainMenu — lightest inspected scene by structure.** Three roots, one camera, one directional light, and the menu controller.

The scene-file GUID token counts were not used as dependency counts because raw serialized GUIDs can include built-in or non-asset references. No project-wide texture/model size scan was run. Individual mesh triangle counts, texture dimensions/memory, shader variants, material counts, dependency sizes, VFX Graphs, and prefab-expanded hierarchies are **NOT TESTED**.

### Runtime CPU candidates

- Opening scenes: collider volume (189 mesh + 198 box colliders each) and many scene objects are candidates for physics broadphase/contact and scene traversal work. The counts alone do not prove expensive physics; static/non-interactive colliders may be cheap.
- World_Master: 2,124 GameObjects and 156 MonoBehaviours make initialization, object lookup, activation, and script scheduling worth measuring. It also has 11 Animators.
- `Scene1OpeningManager` performs scene-wide object searches during setup/transition paths and has coroutine-driven camera, subtitle, audio, lighting, video, and environment setup. This is a runtime concern, not proof of a build-time CPU bottleneck.

### Runtime GPU candidates

- Opening scenes: 472 mesh renderers, 8 lights, and 4 reflection probes each; World_Master has 94 mesh renderers, 12 lights, and 2 reflection probes. Whether lights/probes use realtime shadows or costly resolutions was not verified.
- No particle systems or terrain were found in the serialized scene records, so there is no measured particle/terrain workload to move to GPU.
- Unity MCP's rendering snapshot during the unfocused editor reported 0 draw calls/triangles and 23 render textures (~141 MiB). This is not a representative gameplay render sample and is not used to rank runtime scenes.

## 5. Build profile and pipeline findings

The active profile is `Assets/Settings/Build Profiles/Windows.asset`. The profile data reports Development Build enabled, deep profiling disabled, and the global Build Settings scene list in use. Unity MCP reports the `mono` scripting backend, `x86_64` architecture, and the `UNITY_POST_PROCESSING_STACK_V2` define. The raw compression enum in the profile was not interpreted.

Likely CPU/disk-heavy build stages (stage times unavailable):

- C# script compilation and editor/domain work when scripts change.
- Shader variant compilation. Shader compilers generally consume CPU; a low GPU reading is normal while they compile.
- Asset import/processing for textures, meshes, audio, and video; import and compression can be CPU- and disk-intensive.
- Scene/asset serialization, dependency processing, stripping, player data generation, linking, and output compression.

The project currently uses Mono, so an IL2CPP conversion/link stage is not indicated by the inspected profile. Build and Clean Build can both stress CPU, RAM, and disk; Clean Build may also discard reusable generated data and force more work. Force Skip Data Build does not establish which other stage is expensive. No Unity Editor.log, previous build report, shader compiler log, disk telemetry, or crash dump was examined, so the cause of the reported 100% CPU or freezes cannot be assigned to a stage.

## 6. CPU, GPU, RAM, VRAM, and training-process observations

These are brief host snapshots taken during read-only audit activity, with no build running:

| Snapshot | CPU | Available system RAM | GPU utilization | GPU memory used / free / total |
|---|---:|---:|---:|---:|
| Latest | 52.5% | 5,859 / 24,536 MB | 4% | 1,936 / 9,919 / 12,227 MiB |
| Earlier | 65.4–79.2% | 4,659–4,846 / 24,536 MB | 7–9% | 2,103–2,241 / 9,614–9,752 / 12,227 MiB |

These readings are snapshots, not build measurements. They do not confirm or disprove the historical crash cause. The GPU was not saturated at audit time; ordinary Unity build stages usually do not render the game or use the GPU as a general-purpose build accelerator. GPU under-utilization is therefore expected and is not evidence that the CPU is the only crash cause.

**Training process status: NOT VERIFIED.** A first process listing included `python.exe` PID 21608 at about 1,255 MiB working set and 116 cumulative CPU seconds, but this did not identify it as the training process or measure its instantaneous CPU share. NVIDIA's compute-process listing returned insufficient permissions for some process names. No process controls or changes were issued. Training-specific CPU/GPU/RAM use and the identity of the running training process remain unknown.

## 7. Potential crash causes (not confirmed)

- **Combined resource pressure:** Unity build work plus the separate training workload could compete for CPU, memory, and disk. The limited available RAM in audit snapshots makes memory pressure/pagefile activity a plausible factor, but no build-time memory or paging evidence exists.
- **CPU-heavy build stages:** script/shader compilation, import, serialization, compression, or multiple workers could keep cores busy and make the cloud desktop unresponsive. The exact stage is unknown.
- **Disk/temp contention:** import caches, Library, temp output, and build output can contend with training data and paging. Disk active time/latency was not measured.
- **Cloud host or hardware factors:** VM scheduling, thermal/power behavior, storage issues, or driver faults remain possibilities. No event logs, thermal data, or crash dump were inspected.

CPU at 100% is an observed condition from the owner, not a confirmed root cause. GPU at 5–6% does not rule in or rule out memory, disk, driver, or cloud-host causes.

## 8. GPU acceleration opportunities and realistic impact

| Opportunity | Likely benefit | Build-time impact | Risk/limit |
|---|---|---|---|
| Reduce unused shader variants/keywords and avoid compiling unused graphics targets | Less shader compilation and smaller player data | Can reduce CPU shader-compiler work; does not move compilation to GPU | Incorrect stripping can cause missing shaders at runtime |
| Unity Accelerator/cache for imported assets; retain a valid Library/import cache between incremental builds | Avoids repeated asset import work | Can reduce CPU and disk work on cache hits; no first-build GPU gain | Cache storage/compatibility and invalidation need care |
| Consolidate repeated compatible meshes/materials and use instancing where verified | Lower runtime CPU draw submission; GPU performs the repeated rendering | Little or no general player-build acceleration | Needs identical compatible assets; may raise GPU load or complicate batching |
| Reduce realtime lights/shadows and reflection-probe cost where quality allows | Lower runtime GPU rendering work | No meaningful general build acceleration | Visual/lighting tradeoff; current shadow modes/resolutions are unknown |
| Simplify/remove decorative or non-interactive MeshColliders; use simpler collision shapes where equivalent | Lower runtime CPU physics work and some scene data | Usually small build-time benefit | Can change collision/gameplay behavior; requires gameplay validation |
| Compute Shaders or GPU-based VFX for a proven large data-parallel runtime simulation | Moves that simulation from CPU to GPU | Does not accelerate normal Unity build stages | No particle/terrain workload was found here; GPU/VRAM cost rises |

Texture/mesh import, C# compilation, serialization, and normal build packaging should not be assumed to gain GPU acceleration. Current inspection found no supported evidence for a GPU-heavy particle workload. A GPU feature should only be considered after identifying a specific runtime CPU workload and confirming GPU/VRAM headroom.

## 9. Recommended roadmap (recommendations only; no changes made)

1. **First, identify the build stage from existing evidence:** preserve and inspect the Editor.log and any build report from the last failed/slow attempt; look for script compilation, shader compilation, asset import, player data, serialization/compression, memory/paging, and disk waits. Do not start another build for this audit.
2. **Keep the training workload protected during diagnosis.** Before any future measurement, confirm which process is the training job and record its baseline CPU/RAM/GPU use. Do not change its priority, affinity, state, or files.
3. **If logs show repeated import work,** assess cache/Accelerator reuse and the largest referenced assets; if logs show shader work, review variant/keyword breadth; if logs show script compilation, inspect assembly boundaries and unnecessary recompiles. Choose only the fix supported by stage evidence.
4. **For runtime CPU/GPU work,** profile Scene1 and World_Master separately after the machine is safe and the owner approves a measurement. Start with the opening scenes' collider count and renderer/material/shadow/probe details, then World_Master's object and Animator count. Do not infer speedups from file size alone.
5. **Do not convert the player to IL2CPP as a CPU-saturation fix.** The active profile is Mono; changing backend can add build work and has no evidence-based benefit for the reported freeze.
6. **Only after review and explicit approval** consider any project edits or controlled build validation, preferably in a copy or on a machine that is not running the training job.

Expected benefits are qualitative only. No timing improvement can be responsibly estimated without stage timings and a controlled baseline. Runtime optimizations may improve play performance but are unlikely to materially reduce build time unless the build log identifies the same asset/scene work as a bottleneck.

## 10. Not verified / limitations

- No build or Clean Build was run; no profiling, stress test, shader benchmark, or crash reproduction was attempted.
- No previous Editor.log/build report/crash dump, disk telemetry, pagefile/commit data, temperature, or cloud-host telemetry was collected.
- The training process could not be positively identified; per-process GPU names were restricted.
- Unity MCP could not retrieve unopened-scene hierarchies by path. Their scene files were not loaded into Unity; active/inactive hierarchy state and serialized component properties remain incomplete.
- Full asset dependency sizes, model triangles, texture dimensions/memory, material/shader variants, shadow settings, and VFX Graph inventory are **NOT TESTED**.
- The ranking separates serialized scene size from likely runtime rendering/physics cost; neither is a measured build-time contribution.

## Approval boundary

This report is the end of the audit phase. No optimization or project setting change was made. The next phase requires the owner's explicit approval.
