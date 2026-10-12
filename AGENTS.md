# AGENTS.md — Standalone VR Project Guidelines: VR Volley

## Project Context & Target Architecture
- **Target Platform**: Standalone Meta Quest 2 / 3 / 3S / Pro (Android ARM64 / Meta Horizon OS)
- **Application ID**: `com.belvok.volleyvr` (Company: Belvok, Product: VR volley)
- **Target Engine**: Unity 6 LTS (URP) [Transitioning from current 2022.3 baseline]
- **XR Standard**: Unity OpenXR Plugin + XR Interaction Toolkit (XRI 3.x) + Meta XR Core SDK
- **Input Method**: Meta Quest Touch / Touch Plus controllers only (hand tracking disabled for fast athletic spike velocity accuracy)
- **Game Modes**: Beach 2v2 first (simpler team dynamics, intuitive sand physics), Indoor 6v6 second. Single-player vs AI first, built on deterministic simulation architecture for future multiplayer.
- **Host OS**: Windows 11

## Architecture & Code Organization
1. **Decoupled Simulation Core (Pure C#)**:
   - Core gameplay physics (`VRVolley.Physics`) and referee rules (`VRVolley.Rules`) must be pure C# (using only basic math structs like `Vector3`, `Quaternion`, `Mathf`).
   - ZERO direct references to `MonoBehaviour`, `GameObject`, or `UnityEngine.Object` in core simulation logic.
   - MonoBehaviours in `VRVolley.Runtime` serve strictly as thin visual/audio/input adaptors.
   - All runtime code belongs in `VRVolley.*` assembly definitions (`.asmdef`). No custom scripts in default `Assembly-CSharp`.
2. **Deterministic & Test-Driven Development**:
   - Every contact solver, trajectory integration (drag, Magnus lift, knuckle wobble), and rally state transition must be validated with NUnit EditMode unit tests in `Assets/_Project/Tests/EditMode/`.
   - Never consider a physics or rules task complete without passing automated tests.

## Performance & Hardware Budgets (Meta Quest Standalone)
1. **Zero-Allocation Hot Paths**:
   - Strictly ZERO managed allocations (`GC.Alloc = 0 B`) in `Update()`, `FixedUpdate()`, `LateUpdate()`, and recurring event handlers.
   - Use non-allocating physics: `Physics.SphereCastNonAlloc`, `Physics.OverlapSphereNonAlloc`. Pre-allocate all buffer arrays and ring buffers in `Awake()`.
   - Banned in per-frame methods: LINQ, string concatenation/formatting/interpolations (`$"..."`), closures/lambdas, `new` collections, `GetComponent*`, `Find*`, and `renderer.material` (use `sharedMaterial` or `MaterialPropertyBlock`).
   - Automated check: Pre-invocation hook `.agents/hooks/hotpath_lint.py` enforces these rules automatically.
2. **Timing & Cadence**:
   - Target refresh rate: 72 Hz (`Time.fixedDeltaTime = 1.0f / 72.0f` ≈ 0.01389s) or 90 Hz (`1.0f / 90.0f` ≈ 0.01111s).
   - Set `Maximum Allowed Timestep` to 0.05s to prevent physics death spirals during temporary hitches.
   - Draw calls per frame must remain under 150; active triangle counts under 350,000 (target ~200,000).
3. **Rendering & Materials**:
   - Stereo Rendering: OpenXR Single-Pass Instanced (Multiview) rendering only.
   - URP Settings: No real-time shadow cascades. Single directional light with baked shadows/lightmaps. Use a dedicated blob shadow decal under the ball for crucial depth perception.
   - Vulkan graphics API, ASTC texture compression, 4x MSAA, HDR disabled.

## XR Interaction & Ball Physics Standards
1. **Transform Spaces & Tracking**:
   - Never treat `Camera.main.transform` as root world coordinates. Controller and eye positions must resolve relative to the `XROrigin.CameraFloorOffsetObject` or local tracking anchor.
   - Tracking Origin Mode is strictly `Floor`. Real player height determines reach and net clearances.
2. **Kinematic Hand-Ball Contact Model**:
   - **DO NOT** use PhysX `PhysicMaterial.bounciness` on controller or ball colliders. High-speed hands cause tunneling, jitter, and non-deterministic bounces.
   - Hand and forearm velocity history is tracked via least-squares regression over a rolling sample window (`TrackedVelocityEstimator`).
   - Contacts are evaluated with sub-stepped swept sphere-vs-capsule queries (`SweepSphereVsMovingCapsule`).
   - Impulse resolution calculates surface point velocity $v_s = v_{hand} + \omega_{hand} \times r$. Relative velocity is $v_{rel} = v_{ball} - v_s$.
   - **CRITICAL**: Hand swing velocity is folded into $v_s$ within the kinematic impulse formula—**NEVER** add the controller velocity a second time on top of the resolved ball velocity.
3. **Ball Aerodynamics**:
   - Ball flight is evaluated in pure C# (`BallIntegrator`) factoring in gravity (-9.81 m/s²), speed-dependent drag $C_d(Re)$ (drag crisis at 18–21 m/s), Magnus lift for topspin/backspin, and seeded smooth noise knuckle-wobble for float serves.
   - Visual ball interpolation runs in `Update()`, while physics advances strictly in `FixedUpdate()`.

## Coding Style & Unity Conventions
- Use PascalCase for public fields, properties, and methods; camelCase with `_` prefix for private fields (`_rigidBody`).
- Explicitly declare `[SerializeField] private` instead of `public` variables for inspector references.
- Always check component nullability with explicit reference caching in `Awake()`.
- Wrap editor-only code and tools with `#if UNITY_EDITOR ... #endif` blocks or place them in Editor `.asmdef`.
- Reference project skills in `.agents/skills/` for domain-specific runbooks:
  - `vr-kinematic-physics`: Ball integration, impulse equations, and contact profiles.
  - `volleyball-rules`: FIVB Beach 2v2 and Indoor 6v6 rally state machines and fault criteria.
  - `xr-rig-and-input`: Rig layout, locomotion, jump detection, calibration, and input actions.
  - `standalone-quest-profiler`: Profiling tools, `quest-adb.ps1`, and VrApi diagnosis.
  - `unity-editor-automation`: MCP for Unity integration and `unity-batch.ps1` fallback.