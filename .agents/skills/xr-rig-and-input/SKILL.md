---
name: xr-rig-and-input
description: >-
  Use this skill when setting up or changing the XR camera rig, controller
  tracking, input actions, haptics, locomotion, jumping, player height/arm
  calibration, refresh-rate control, or Meta-specific XR features; and when
  performing or verifying the Unity 2022.3 → Unity 6 LTS / XRI 2.x → XRI 3.x
  upgrade. Covers XR Origin structure, tracking-space rules, Touch controller
  bindings, haptics, comfort locomotion for a court larger than a play space,
  and jump detection.
---

# XR Rig & Input

## 1. Stack decision (locked)
- Unity 6 LTS + URP 17 + **OpenXR** plugin + **XR Interaction Toolkit 3.x** + Input System.
- **Meta XR Core SDK** stays installed and runs *on top of* OpenXR (the "Meta XR Feature" in the OpenXR settings).
  Use it for: display refresh rate, foveation, boundary/guardian, ASW, and platform services.
- Do **not** add the legacy "Oculus XR Plugin" loader. OpenXR is the only XR loader on Android.
- Input: **Touch controllers only** (Oculus Touch + Touch Plus profiles). Hand tracking is out of scope for v1.

## 2. Unity 6 / XRI 3 upgrade runbook
1. Commit everything (`git commit -am "pre-upgrade snapshot"`). Close the Editor.
2. Install the latest **Unity 6 LTS** in Unity Hub with **Android Build Support (OpenJDK + SDK/NDK)**.
3. Open the project with Unity 6 and accept the upgrade. Let the import finish.
4. Package Manager: update `com.unity.xr.interaction.toolkit` to the latest 3.x verified for the editor,
   plus OpenXR, URP, and the Input System to their verified versions. Remove `com.unity.visualscripting`
   and `com.unity.collab-proxy` (unused).
5. Update the Meta XR Core SDK to the version that lists your Unity 6 stream as supported.
   Then run **Meta → Tools → Project Setup Tool → Fix All / Apply All**.
6. XRI 3 samples: re-import **Starter Assets** (the 2.x sample in `Assets/XRI` is obsolete; delete it after the new
   import compiles). XRI 3 replaces `ActionBasedController` with `TrackedPoseDriver` + input readers. Old rigs must
   be rebuilt from the new `XR Origin (XR Rig)` prefab, not migrated by hand.
7. Project Settings → XR Plug-in Management → Android: OpenXR ✔. OpenXR features: Meta XR Feature ✔,
   Meta Quest Touch Plus/Oculus Touch profiles ✔. Render mode **Single Pass Instanced / Multi-view**.
8. Player: Active Input Handling = **Input System Package (New)** once no legacy `Input.*` calls remain
   (currently "Both" for the transition).
9. Verify: an empty scene with the XR Origin builds and runs on Quest, hands track, and `VrApi` FPS = target.
   Commit as "Unity 6 upgrade".

## 3. Rig structure
```
XR Origin (XR Rig)            ← moved by locomotion; NEVER moved by gameplay physics
└── Camera Offset             ← floor offset (Floor tracking origin mode)
    ├── Main Camera           ← TrackedPoseDriver (centerEye), Update + BeforeRender
    ├── Left Controller       ← TrackedPoseDriver (grip pose) + TrackedVelocityEstimator + HapticImpulsePlayer
    │   └── BodyVolumes/      ← palm, wrist, forearm capsule anchors (calibrated offsets)
    └── Right Controller      ← same
PlayerBody (not parented to the camera) ← estimated torso/feet position for rules (foot faults, net crossing)
```
- **Tracking origin mode: Floor.** Player height comes from real head height. That is essential for
  realistic reach and block height.
- All gameplay reads **world-space** poses from these transforms. Never use raw `InputDevice`
  positions (they're in tracking space) without transforming them through `Camera Offset`.
- `Camera.main` may be used for UI facing only, never as the origin for gameplay math.

## 4. Input bindings (Input Actions asset `VRVolley.inputactions`)
| Action | Binding | Use |
| :-- | :-- | :-- |
| `Move` | Left thumbstick | Smooth locomotion on the court |
| `Turn` | Right thumbstick X | Snap turn 30° (comfort) or smooth (option) |
| `Jump` | A/X button **or** detected physical jump | Spikes, blocks, jump serves |
| `TossBall` | Left trigger (hold & release) | Serve toss from the off hand |
| `CallBall` | B/Y | "Mine!" voice line + AI teammate yields |
| `Menu` | Menu button | Pause |
- Hitting the ball needs **no button**. Contact is purely physical (physics skill).

## 5. Haptics
- Use XRI 3 `HapticImpulsePlayer` on each controller. Cache the reference in `Awake`.
- Amplitude = `clamp01(impulse / impulseForMaxHaptic)`, duration 20–60 ms; spikes get a sharper/shorter pulse,
  bumps a longer, softer one. Fire from the contact event; never poll per frame.

## 6. Locomotion (the court is bigger than a living room)
A beach half-court is 8 × 8 m. Most play spaces are ~2 × 2 m. Options, all available in settings:
1. **Smooth thumbstick locomotion** (default) with acceleration capped to athlete-realistic values
   (≈ 5 m/s max, ≈ 8 m/s² accel) and an optional comfort vignette.
2. **Assisted positioning**: when the ball is predicted to land in your zone, a subtle "magnet"
   drifts the rig toward the optimal contact point (strength slider, off in Pro realism).
3. **Room-scale** movement always works on top of 1 and 2.
- Locomotion moves the `XR Origin`, and body volume velocities must include rig velocity
  (the velocity estimator samples world positions, so this comes for free; don't subtract it).

## 7. Jump detection
- Physical jump: head vertical velocity > 1.2 m/s upward **and** head height > standing height + 0.08 m.
  Calibrate standing height at session start (and re-calibrate when the head stays steady for 2 s).
- Virtual jump (button or detected) applies a vertical rig offset following the ballistic curve of a real
  jump (beach ≈ 0.5–0.7 m vertical, ~0.6–0.75 s airtime), scaled by a "jump assist" setting.
- While airborne, locomotion input is ignored (no air control). Hand velocity still includes rig velocity.

## 8. Calibration flow (first launch)
1. Stand straight → record head height.
2. Arms out sideways (T-pose) → record arm span → derive forearm length and reach.
3. Hold a forearm platform → record the controller-to-forearm offset rotation used for the platform capsules.
Store in `PlayerCalibration` (JSON in `Application.persistentDataPath`, loaded once at boot).

## 9. Refresh rate & timing
- Request 90 Hz at boot if the device supports it (Meta XR display frequency API), fall back to 72.
- On every refresh change: `Time.fixedDeltaTime = 1f / hz;`. Log once (outside hot paths).

## Validation
- In-headset checklist: controllers visually match real hands with no lag at fast swings; the platform capsule
  gizmo lines up with real forearms after calibration; a physical jump registers in > 95 % of 20 attempts with
  0 false positives while walking/ducking; haptics fire on every contact.
- Profiler: rig + estimator + body volumes < 0.3 ms CPU, 0 B GC.
