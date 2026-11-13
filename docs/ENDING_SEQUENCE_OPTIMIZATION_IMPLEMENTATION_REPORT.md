# Ending Sequence Optimization — Phase A Implementation Report

**Project:** NyxoGame — Unity 6000.6.4f1  
**Target:** `Assets/Scripts/Scene1/Scene1OpeningManager.cs`  
**Date:** 2026-11-08  
**Scope:** Phase A only. No collider, model-loading, rendering, video, scene, asset, or project-setting changes.

## 1. Original issue

The liminal ending's `UpdateLiminalEnding` performed `FindFirstObjectByType<Scene2EveningManager>(FindObjectsInactive.Include)` on every active frame. This repeated scene-wide search is unnecessary while the same Scene2 manager remains loaded and is a CPU-side cost. The audited sequence also has other possible performance costs, but no runtime FPS baseline was available to prove their impact.

## 2. Changes implemented

### Manager cache

Added a cached `Scene2EveningManager` reference and a null-safe resolver in `Scene1OpeningManager.cs`.

- The resolver reuses a valid cached manager.
- It invalidates the cache if the manager's Unity object is destroyed or its owning scene is invalid/unloaded.
- Call sites that establish a sequence context can force a fresh lookup, preserving the prior selection behavior at those transitions.
- If no manager is present, lookup retries are throttled to once every 0.5 seconds instead of once per frame.
- Existing `FindObjectsInactive.Include` behavior is preserved.

Updated the debug ending entry, bedroom cutscene setup, liminal objective assignment, per-frame liminal update, and coma-ending setup to use the resolver. In particular, the active per-frame update no longer calls `FindFirstObjectByType` directly.

### Other per-frame work

Reviewed `UpdateLiminalEnding`, `KeepPlayerInsideLiminalShell`, and `UpdateLiminalShellVisibility`. Kept the distance thresholds, boundary enforcement, renderer-state checks, input timing, and door prompt updates intact because changing them without runtime equivalence evidence could alter behavior. Boundary controller lookup already runs only when the player crosses the boundary; shell renderers are already cached.

## 3. Files and methods

Modified for this phase:

- `Assets/Scripts/Scene1/Scene1OpeningManager.cs`
  - Added `cachedLiminalScene2Manager` and `nextLiminalScene2ManagerLookupTime` fields.
  - Added `ResolveLiminalScene2Manager(bool forceRefresh = false)`.
  - Updated manager lookup call sites in `DebugStartLiminalEndingTest`, `PlayBedroomDreamSequence`, `PlayEndingVideoThenLiminalSequence`, `UpdateLiminalEnding`, and `PlayComaEndingSequence`.
- `docs/ENDING_SEQUENCE_OPTIMIZATION_IMPLEMENTATION_REPORT.md` (this report).

No scene, model, material, texture, VideoPlayer, collider, rendering, or Project Settings files were changed.

## 4. Collider investigation status

The current code adds MeshColliders to qualifying MeshFilters under the door (`model_0`) and terrain/path models (`model_2`–`model_4`). The environment separately creates a 24-panel boundary using BoxColliders. It performs two `Physics.SyncTransforms` calls during environment construction. The shell (`model_1`) is handled visually and is not included in that MeshCollider-addition branch.

The previous static inspection found terrain OBJ source files of about 18.4 MB, 19.1 MB, and 18.3 MB, with model_2 importer mesh compression set to 0 and collider import disabled. Exact MeshFilter/MeshCollider, vertex, triangle, and submesh counts were not safely available through the read-only asset metadata endpoint; this phase did not instantiate or reimport assets to obtain them. No collider layout recommendation is approved or implemented. Grounding, boundary, door interaction, and collision changes remain pending the separate collision approval gate.

## 5. Model-loading investigation status

The current `BuildLiminalEndingEnvironment` synchronously instantiates the five assigned model assets and traverses their mesh/render components after video playback while the screen is black. It adds collision components, activates the environment, synchronizes physics transforms twice, computes placement, creates boundary colliders, applies global sky/ambient settings, and calls `DynamicGI.UpdateEnvironment` before revealing the set.

No per-model duration or total construction time has been measured. The per-model vertex/triangle/renderer/submesh counts are unknown. Staged creation, asynchronous loading, pooling, collider simplification, and component-traversal changes were not implemented. A controlled creation plan should follow only after a safe focused measurement and the separate approval gate.

## 6. Rendering investigation status

The liminal code assigns shared runtime materials for the terrain and door, and changes global skybox/ambient/fog settings, restoring the captured settings at the bedroom transition. It does not create additional realtime lights in this sequence. The earlier idle-editor rendering snapshot was not a liminal gameplay measurement, so draw calls, visible triangles, shadow casters, reflection-probe cost, GPU frame time, and liminal VRAM usage remain unknown.

No LOD, mesh simplification, instancing, shadow, reflection, shader, or texture-quality changes were made. Visual composition and atmosphere remain untouched.

## 7. VideoPlayer and memory findings

The prior read-only metadata inspection found the ending clip is H.264/AAC, 1920×1080, 30 fps, about 7.13 seconds, and about 12.8 MB on disk. The sequence creates a matching 1920×1080 ARGB32 RenderTexture, prepares the VideoPlayer asynchronously, then stops it and releases/destroys the temporary player, Canvas, and target before building the liminal environment.

The uncompressed color storage for that target is about 7.9 MiB before backend overhead. Hardware decode selection and decode frame time were not observable. The clip and target were left unchanged. The liminal environment remains cached on the manager after creation; its lifetime was not changed.

## 8. Validation and safety

- Pre-edit lightweight snapshot: Unity MCP connected to NyxoGame / Unity 6000.6.4f1; Editor idle in `MainMenu`, not in Play Mode; about 10.2 GB RAM free; CPU snapshot 52%; Python processes were present. No process was stopped, suspended, restarted, reprioritized, or otherwise modified.
- `git diff --check` for the target script passed.
- Static brace-count check on the full script: 390 opening and 390 closing braces.
- Unity MCP basic script validation: **0 errors, 0 warnings**.
- Reviewed the resulting diff and confirmed the active per-frame method uses the cache resolver; the only remaining `FindFirstObjectByType<Scene2EveningManager>` call in the target is inside that resolver.
- No build, Clean Build, Build And Run, stress test, benchmark, intensive Profiler capture, or gameplay sequence launch was performed.
- No FPS improvement is claimed. Runtime gameplay behavior remains unverified.

## 9. Risks and limitations

The cache relies on Unity's destroyed-object null semantics and validates the cached manager's scene load state before reuse. If the manager is absent, it may take up to 0.5 seconds for a later retry to find one; the existing sequence normally has the additive Scene2 manager available, and critical one-off setup points force a fresh lookup. No runtime scene-transition test was run because builds and heavier testing are prohibited while the AI training workload is active.

## 10. Next phase

**Phase A is complete. Stop here for approval.** The next safe step is to present a collision-only plan with exact imported mesh/collider counts and explain grounding, boundary, and door interaction implications. Do not modify collider configuration until separately approved. Model-loading and rendering proposals also remain approval-gated.
