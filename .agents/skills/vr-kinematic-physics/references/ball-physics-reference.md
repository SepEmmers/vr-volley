# Volleyball Physics Reference

All values are **starting points**. Tune them against reference trajectories in EditMode tests, then
lock them into `BallProfile` / `SurfaceProfile` ScriptableObjects. Never hard-code them in scripts.

## 1. Ball specifications (FIVB)
| Property | Indoor | Beach |
| :-- | :-- | :-- |
| Circumference | 65–67 cm | 66–68 cm |
| Radius (use) | 0.105 m | 0.107 m |
| Mass | 260–280 g (use 0.270 kg) | 260–280 g (use 0.270 kg) |
| Internal pressure | 0.30–0.325 kg/cm² | 0.175–0.225 kg/cm² (softer → lower e, more "grab") |
| Cross-section A = πr² | 0.0346 m² | 0.0360 m² |
| Moment of inertia (thin hollow sphere) | I = ⅔ m r² ≈ 1.98e-3 kg·m² | ≈ 2.06e-3 kg·m² |

## 2. Air
- Air density ρ = 1.20 kg/m³ (20 °C, sea level). Beach maps may use 1.18 (hot).
- Dynamic viscosity μ = 1.81e-5 Pa·s.
- Reynolds number: `Re = ρ |v| D / μ ≈ 14 000 · |v|` (D = 0.21 m).

## 3. Drag coefficient C_d(Re)
Volleyballs (panelled, slightly rough) show a drag crisis around Re ≈ 2.5–3.0e5 → **18–21 m/s**.

| Speed | C_d (start value) |
| :-- | :-- |
| < 15 m/s | 0.47 |
| 15 → 21 m/s | lerp 0.47 → 0.18 (smoothstep) |
| > 21 m/s | 0.18–0.22 |

Implement as an `AnimationCurve` sampled by speed, or as a 3-point smoothstep. Sampling an
`AnimationCurve` is allocation-free and fine in FixedUpdate.

**Why it matters for feel**: float serves are hit at 15–20 m/s, right inside the crisis. Small changes in
the boundary layer cause big side forces → the "knuckle" wobble that defines a real float serve.

## 4. Magnus (spin) lift
- Spin parameter `S = r|ω| / |v|`
- `C_L = min(1.0 · S, 0.30)` (start: linear slope 1.0, cap 0.30)
- `F_magnus = ½ ρ C_L A |v|² · normalize(ω × v)`
- Typical spins: topspin jump serve 5–10 rev/s (30–60 rad/s); spike 5–15 rev/s; float serve < 1 rev/s.
- Spin decay: `τ_spin ≈ 4 s` → `ω *= exp(-dt / 4)`.

## 5. Float-serve knuckle model
Apply only when `|ω| < 6 rad/s` and `13 m/s < |v| < 22 m/s`:
- `F_side = ½ ρ A |v|² · C_side(t)`, direction perpendicular to v, rotating slowly
- `C_side(t) = amp · (PerlinNoise(seed, t·f) · 2 − 1)`, amp ≈ 0.05–0.10, f ≈ 1.5–3 Hz
- Seed = serve index (deterministic across clients).
- Fade amplitude in/out over 1 m/s at the speed range edges to avoid pops.

## 6. Real-world speed ranges (use for test assertions)
| Action | Ball speed after contact |
| :-- | :-- |
| Underhand serve (beginner) | 8–12 m/s |
| Float serve | 14–20 m/s |
| Jump topspin serve (elite) | 24–33 m/s |
| Forearm pass, good reception | 4–8 m/s, peak height 1.5–3 m above contact |
| Overhead set | 5–9 m/s, apex 1–3 m above net |
| Spike (recreational) | 15–22 m/s |
| Spike (elite men) | 25–36 m/s |
| Attacker hand speed at contact | 10–20 m/s |

## 7. Contact parameters
`M_eff` is the effective mass of the hitting body segment. It folds "arm give" into the impulse:
`Δv_n = −(1 + e) · v_rel·n · M_eff / (M_eff + m_ball)`.

| Contact | Restitution e | M_eff (kg) | Friction μ | Cooldown |
| :-- | :-- | :-- | :-- | :-- |
| Forearm platform (bump) | 0.45–0.55 | 6–10 (rigid platform) | 0.3 | 0.15 s |
| Single forearm (dig) | 0.40–0.50 | 3–5 | 0.3 | 0.15 s |
| Open hand spike / serve | 0.60–0.70 | 2–4 | 0.6–0.8 (topspin transfer) | 0.10 s |
| Block (hands firm) | 0.35–0.45 | 4–8 | 0.4 | 0.10 s |
| Overhead set (soft capture) | n/a: release velocity = mean hand velocity × 1.1–1.4 | n/a | n/a | 0.20 s |

## 8. Surfaces
| Surface | e (normal) | Friction μ | Notes |
| :-- | :-- | :-- | :-- |
| Indoor sports floor | 0.75–0.80 | 0.5 | Ball keeps rolling |
| Sand | 0.10–0.25 | 0.9 | Ball dies; spawn imprint decal for line calls |
| Net mesh | 0.10–0.20 | 0.8 (strong tangential damping) | Ball drops nearly vertically |
| Net tape (top band, 5 cm) | 0.40 | 0.3 | Cylinder; "let" serves roll over |
| Antenna | 0.50 | 0.3 | Any touch = out |
| Poles / referee stand | 0.50 | 0.4 | Out of play |

## 9. Hand / forearm geometry from Touch controllers
Offsets are in the controller's **grip pose** local space (OpenXR grip). Calibrate per user with an arm-length step.
- Palm center: `(0, -0.02, 0.06)`, hand capsule radius 0.045 m
- Wrist: `(0, 0, -0.05)`
- Forearm capsule: from wrist along local `-Z` (rotated ~15° toward local `-Y`), length 0.24 m, radius 0.04 m
- Platform valid: wrist distance < 0.15 m AND angle between forearms < 35°
- Platform normal = normalize(cross(forearmDirL + forearmDirR, wristR − wristL)), flipped to face the ball

## 10. Timing
- 72 Hz → dt = 0.013889 s; 90 Hz → 0.011111 s; 120 Hz → 0.008333 s.
- `Time.fixedDeltaTime` must equal the active display refresh interval. When the refresh rate
  changes at runtime (OVRManager / OpenXR display refresh feature), update `fixedDeltaTime`.
- Set `Maximum Allowed Timestep` to ~0.05 s so a hitch can't trigger a physics death spiral.

## 11. Sanity equations
- Flight time of a lob with apex h above release: `t ≈ 2·sqrt(2h/g)`; h = 3 m → 1.56 s.
- Without drag, a 20 m/s serve at 10° covers 18 m in about 0.9 s; with drag it takes ~1.0–1.1 s and lands 1–2 m shorter.
