---
name: standalone-quest-profiler
description: >-
  Use this skill when creating or reviewing shaders, materials, lighting, URP
  renderer/pipeline assets, quality settings, VFX, instantiation/pooling logic,
  or any per-frame code meant to run on Meta Quest. Also use it when the user
  reports dropped frames, stutter, reprojection, overheating, or GC spikes,
  and before every on-device build or performance review. Covers budgets, the
  settings checklist, ADB/OVR Metrics/Unity Profiler workflows, and how to
  diagnose CPU- vs GPU-bound frames.
---

# Standalone Quest Profiler

Quest is a mobile SoC rendering two eyes at 72 to 120 Hz. A missed frame is felt physically as
judder or reprojection, so performance is a correctness requirement.

## 1. Budgets (hard limits)
| Metric | Budget | How to check |
| :-- | :-- | :-- |
| Frame time @72 Hz / 90 Hz | **< 13.8 ms / < 11.1 ms** (CPU *and* GPU) | OVR Metrics Tool, `VrApi` logcat |
| Managed allocations in Update/FixedUpdate/LateUpdate/hot events | **0 B** | Profiler → CPU → GC Alloc column; `ProfilerRecorder("GC Allocated In Frame")` |
| Draw calls (SetPass + batches) | < 150 | Frame Debugger, Profiler Rendering module |
| Visible triangles | < 350k (target 200k) | Profiler Rendering module |
| Real-time lights | 1 directional, **no real-time shadows** (baked/blob shadows) | URP asset |
| Transparent overdraw | Minimal; no full-screen transparent quads | RenderDoc Meta Fork overdraw view |
| Texture memory | < 1 GB total, ASTC compression | Memory Profiler |

The ball and player hands are the exceptions to "no shadows". Use a cheap **blob shadow** (projected
quad/decal) under the ball. It is the depth cue players rely on most to judge trajectory.

## 2. Project settings checklist (run before every perf build)
- [ ] Graphics API: **Vulkan** only (remove GLES3 once Vulkan is stable on device)
- [ ] Stereo: OpenXR render mode **Single Pass Instanced / Multiview**
- [ ] Scripting backend **IL2CPP**, target **ARM64** only, managed stripping Medium+
- [ ] Texture compression **ASTC**
- [ ] URP asset (Quest): HDR **off**, MSAA **4x**, Render Scale 1.0, Depth Texture **off**,
      Opaque Texture **off**, SRP Batcher **on**, Main Light shadows **off**, Additional lights **off/per-vertex**
- [ ] URP renderer: no post-processing, no SSAO, no Decal Renderer Feature using depth (use mesh decals)
- [ ] Meta features: Fixed Foveated Rendering (Meta XR Foveation feature) high on the court environment;
      optimize buffer discards on; consider Application SpaceWarp only after the base frame rate is solid
- [ ] `Time.fixedDeltaTime` = 1 / display refresh rate (see the physics skill)
- [ ] Lighting: baked lightmaps + light probes; Mixed lights → Baked Indirect or fully baked
- [ ] Development Build **off** for any measurement you report as "final"

## 3. Code rules for hot paths
- Pool everything that spawns during a rally (balls, sand puffs, decals, audio sources, UI popups).
  `Instantiate`/`Destroy` during play is a bug.
- `renderer.material` clones the material and leaks a draw call batch. Use `sharedMaterial` or
  `MaterialPropertyBlock` (allocate the block once in `Awake`).
- Cache `WaitForSeconds` instances; never `new` them inside coroutines that loop.
- No `Debug.Log` in per-frame paths in builds. Wrap it in `[Conditional("VRVOLLEY_DEBUG")]` helpers.
- Strings: no concatenation or interpolation per frame. For score UI use
  `TMP_Text.SetText("{0}", value)`, which doesn't allocate.
- Physics queries: only the `*NonAlloc` variants, with explicit `layerMask` and
  `QueryTriggerInteraction.Ignore`.

## 4. Device workflow
Use the helper script [quest-adb.ps1](scripts/quest-adb.ps1). It locates Unity's bundled `adb.exe`
automatically, so no global Android SDK is needed.

```powershell
# from the project root
.\.agents\skills\standalone-quest-profiler\scripts\quest-adb.ps1 devices
.\.agents\skills\standalone-quest-profiler\scripts\quest-adb.ps1 install .\Builds\VRVolley.apk
.\.agents\skills\standalone-quest-profiler\scripts\quest-adb.ps1 launch
.\.agents\skills\standalone-quest-profiler\scripts\quest-adb.ps1 fps         # VrApi FPS/latency lines
.\.agents\skills\standalone-quest-profiler\scripts\quest-adb.ps1 logcat      # Unity + crash logs
.\.agents\skills\standalone-quest-profiler\scripts\quest-adb.ps1 refresh 90  # test-only refresh override
```

### Reading a `VrApi` line
`FPS=72/72,Prd=38ms,Tear=0,Early=0,Stale=0,...,CPU4/GPU=4/4,...,GPU%=0.85,CPU%=0.60(W0.70)`
- `FPS` lower than target → you are missing frames.
- `Stale` > 0 → a frame was reused (reprojection), which is visible judder.
- `GPU%` > 0.9 → GPU bound. Check overdraw, shader cost, resolution, MSAA, triangle count.
- `CPU%` (or the worst-thread `W` value) > 0.9 → CPU bound. Check scripts, physics, draw-call
  submission, and GC.

## 5. Profiling procedure
1. Build a **Development Build** with **Autoconnect Profiler** and **Deep Profiling off**.
2. Connect the Unity Profiler over USB (`adb forward` is set up automatically in Unity 6).
3. Record 300+ frames of an *actual rally* (ball launcher drill), not an idle scene.
4. Check in this order:
   - **GC Alloc** column in the `PlayerLoop` hierarchy: any non-zero entry in Update/FixedUpdate is a bug.
   - **FixedUpdate.PhysicsFixedUpdate** + our `BallBody`/`HandBallContactSolver` under 1.5 ms combined.
   - **Gfx.WaitForPresent / WaitForGPU** dominates → GPU bound → go to RenderDoc Meta Fork.
5. Confirm with the OVR Metrics Tool overlay in-headset (no Development Build) for true numbers.
6. Record the results in the perf log table in `docs/perf-log.md` (date, build, scene, CPU ms, GPU ms,
   draw calls, tris, GC/frame).

## 6. Quick triage table
| Symptom | Likely cause | First fix |
| :-- | :-- | :-- |
| Periodic hitch every few seconds | GC collection | Find the allocation in the GC Alloc column |
| Judder only when looking at the crowd/stands | GPU overdraw / too many tris | LODs, imposters, merge meshes, fewer transparent sprites |
| Ball stutters but the world is smooth | Visual ball moved in FixedUpdate without interpolation | Interpolate in Update (physics skill) |
| Hands lag behind real controllers | Pose read too early / wrong update type | TrackedPoseDriver update type "Update and Before Render" |
| Device gets hot, FPS drops after 10 min | Thermal throttling | Lower CPU/GPU levels, reduce GPU load, enable FFR |

## Validation
A change is "perf-safe" when: Profiler shows 0 B GC in hot paths across a 300-frame rally capture,
the `VrApi` FPS equals the target with `Stale=0` for 60 s, and draw calls and triangles stay within budget.