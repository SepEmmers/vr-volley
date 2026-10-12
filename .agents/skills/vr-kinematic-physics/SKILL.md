---
name: vr-kinematic-physics
description: >-
  Use this skill when writing, reviewing, or debugging any code that touches the
  volleyball's motion or a player's body touching the ball: controller/forearm
  velocity tracking, hand-ball contact detection, bump/set/spike/serve/block
  impulse responses, ball flight (drag, Magnus spin, float-serve knuckle),
  ball-vs-floor/sand/net/antenna collisions, haptics on contact, or physics
  unit tests. Also use it when the ball "tunnels", jitters, feels floaty, or
  goes the wrong direction.
---

# VR Kinematic Physics (Volleyball)

The ball and every player-to-ball contact are simulated by **our own code**,
not by PhysX contact resolution. PhysX is only used for scene queries
(non-alloc casts/overlaps) and, optionally, trigger notifications.

## Why custom physics
- At 72 Hz a spiking hand (15 m/s) moves ~0.21 m per frame, the full ball diameter.
  A 30 m/s ball moves ~0.42 m. Discrete PhysX contacts miss or half-resolve these.
- PhysX bounciness is per-material, has no notion of arm stiffness, swing
  direction, or spin transfer, and is non-deterministic across platforms. That
  last point blocks the multiplayer roadmap.
- Real volleyball feel comes from **swing velocity + contact angle + spin**. We need
  explicit control of each term.

## Architecture (mandatory)
```
VRVolley.Physics (pure C#, no UnityEngine.Object dependencies except math types)
├── BallState            struct: position, velocity, angularVelocity
├── BallProfile          ScriptableObject: mass, radius, Cd curve, Magnus, restitution per surface
├── BallIntegrator       static: Step(ref BallState, in BallProfile, in Environment, float dt)
├── ContactMath          static: swept sphere-vs-capsule, impulse resolution
└── SurfaceProfile       ScriptableObject: restitution, friction, (sand) energy absorption

VRVolley.XR / VRVolley.Gameplay (MonoBehaviours, thin)
├── TrackedVelocityEstimator  samples controller poses into a ring buffer (order -100)
├── BodyContactVolumes        hands + forearm platform capsules derived from controller poses
├── BallBody                  owns BallState, calls integrator in FixedUpdate, interpolates in Update
└── HandBallContactSolver     sweeps volumes vs ball each FixedUpdate, applies ContactMath
```
- Keep the pure part deterministic: no `Time.*`, no `Random` without an explicit seed,
  no `Physics.*` calls inside `BallIntegrator`. Pass `dt` and environment data in.
  Then EditMode tests can run it, and later netcode can resimulate it.
- The ball GameObject uses a **kinematic** `Rigidbody` (for triggers and the
  interpolation hook) and moves only through `BallBody`.

## Step-by-step: implementing a new contact type
1. **Sample**: make sure `TrackedVelocityEstimator` runs before the solver
   (`[DefaultExecutionOrder(-100)]`). See [TrackedVelocityEstimator.cs](examples/TrackedVelocityEstimator.cs).
2. **Shape**: define the contact volume as a capsule (segment + radius) from the
   controller pose. Forearm platform = two forearm capsules, valid only while the wrists
   are < ~0.15 m apart and the palms face roughly up/forward.
3. **Sweep**: test the ball's motion this step against the volume's motion this step,
   using sub-stepped sphere-vs-capsule, with sub-steps chosen from the *relative* displacement.
   See [ContactMath.cs](examples/ContactMath.cs) `SweepSphereVsMovingCapsule`.
4. **Resolve**: compute the surface point velocity `v_s = v_hand + ω_hand × r`, then call
   `ContactMath.ResolveImpulse` with the contact type's restitution, effective arm mass,
   and friction (table in [ball-physics-reference.md](references/ball-physics-reference.md)).
5. **Separate**: push the ball out along the normal to `rBall + rCapsule + skin` and start a
   per-volume cooldown (~0.1 s). Without these, one swing registers as 3 to 4 hits.
6. **Report**: raise a `BallTouched` event (struct payload, no allocations) with player, contact
   type, impulse magnitude, and timestamp. The rules system uses it to count touches and
   detect double contacts.
7. **Feedback**: send a haptic impulse scaled by impulse magnitude, plus a contact sound with
   pitch/volume taken from the same value.
8. **Test**: add an EditMode test that feeds a scripted hand trajectory and asserts the outgoing
   ball velocity range (see "Validation" below).

## Do NOT double-count swing velocity
AGENTS.md says "add the user's swing velocity to the ball". That happens *through* `v_s` in the
impulse equation. Never add hand velocity on top of the resolved result.

## Contact-type cheat sheet
| Contact | Volume | Model | Notes |
| :-- | :-- | :-- | :-- |
| Bump / forearm pass | 2 forearm capsules → averaged platform normal | Impulse, low e, high effective mass | Platform angle dominates direction. Speed comes mostly from incoming ball, not swing |
| Overhead set | Both hands above head, ball between palms | **Soft capture**: spring the ball to the hands' midpoint for ≤ 0.08 s, release with hands' mean velocity × gain | Over the hold limit → "lift/catch" fault event. Hands far apart in time → "double contact" |
| Spike | Single hand capsule (palm sphere at wrist + 0.08 m) | Impulse, high e, medium effective mass, high friction → topspin | Needs a jump (see the XR skill). Swing speed 10 to 20 m/s |
| Serve (float / topspin) | Same as spike, ball released from off-hand toss | Impulse. Float = contact through the center (low spin); topspin = high friction + upward wrist snap | Float serve triggers the knuckle model |
| Block | Both hands, passive | Impulse with near-zero hand velocity, e ≈ 0.4 | Beach: block counts as a team touch |
| Dig / one-arm | Single forearm capsule | Same as bump with lower effective mass | Usually emergency touches |

## Ball flight
Per FixedUpdate (semi-implicit Euler is fine at 72/90 Hz; use RK2 if trajectories drift in tests):
- Gravity: `-9.81 m/s²`
- Drag: `F = -½ ρ C_d(Re) A |v| v`. C_d drops sharply (drag crisis) around 18 to 21 m/s.
- Magnus: `F = ½ ρ C_L A |v|² · normalize(ω × v)`, with `C_L = min(k·(rω/|v|), C_Lmax)`
- Spin decay: `ω *= exp(-dt / τ_spin)`
- Float knuckle: when `|ω| < ω_float` and speed is in the critical range, add a lateral force from
  **seeded** smooth noise. Seed it per serve so replays and netcode match.

All coefficients, with sources and starting values: [ball-physics-reference.md](references/ball-physics-reference.md).

## Environment collisions
- Ball vs world: `Physics.SphereCastNonAlloc` along the step displacement against the
  `Environment` layer mask only, with a buffer allocated in `Awake`.
- Floor (indoor): e ≈ 0.75 to 0.8, low friction. Sand: e ≈ 0.1 to 0.25, very high friction,
  so the ball "dies" and leaves a mark. That gives the in/out decal for line calls.
- Net body: compliant, e ≈ 0.1 to 0.2, strong tangential damping, so the ball drops.
  Net tape (top 5 cm) is a cylinder with e ≈ 0.4, which gives the "let cord" dribble.
- Antennas: plain capsules. Touch = ball out (rules event).
- Rendering at higher refresh than physics: interpolate the visual transform in `Update` between
  the previous and current `BallState`. Never move the visual from `FixedUpdate` directly.

## Hard rules (also in AGENTS.md)
- Zero GC in all of the above. Pre-allocate hit buffers and ring buffers in `Awake`, use
  struct events, and never use LINQ or lambdas capturing locals in hot paths.
- No `MeshCollider` on hands. No `Rigidbody` on hands for ball contact purposes.
- No PhysX `PhysicMaterial.bounciness` anywhere on the ball/hand interaction path.
- SI units everywhere (m, kg, s, rad). Angles in the inspector can be degrees but get
  converted once in `OnValidate`/`Awake`.

## Validation
1. **EditMode tests** (pure C#), in `Assets/_Project/Tests/EditMode/Physics/`:
   - Drop test: ball from 2 m with no drag lands at `t = sqrt(2h/g) ± 1 frame`.
   - Drag test: a 20 m/s serve over 18 m must land inside the reference envelope from the reference doc.
   - Magnus sign test: using the convention `v_surface = ω × r`, a ball travelling +Z with topspin
     has ω = +X. `ω × v` points −Y, so the ball must dive compared with a no-spin ball.
   - Contact tests: a static platform at 45° reflects a falling ball to the expected direction ±3°;
     a 15 m/s hand swing gives an outgoing spike speed in the 20 to 30 m/s band.
   - Tunneling test: a hand capsule moving 0.5 m in one step through a stationary ball must register a hit.
2. **In-headset**: enable the `PhysicsDebugOverlay` (draws contact normals, swing vectors, and
   predicted trajectories) and check the Profiler shows `GC Alloc = 0` in `FixedUpdate`.
3. If behavior feels wrong, log one contact with the full state (`v_ball`, `v_s`, `n`, `e`, `M_eff`)
   and recompute by hand before you tweak constants.

## References
- [ball-physics-reference.md](references/ball-physics-reference.md): constants, equations, real-world speed ranges
- [ContactMath.cs](examples/ContactMath.cs): sweep + impulse math (zero alloc)
- [TrackedVelocityEstimator.cs](examples/TrackedVelocityEstimator.cs): least-squares ring-buffer velocity