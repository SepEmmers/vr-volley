---
name: volleyball-rules
description: >-
  Use this skill when implementing or reviewing anything that depends on the
  official rules of beach or indoor volleyball: the referee/rally state
  machine, touch counting, fault detection (double contact, lift, four hits,
  net touch, foot fault, antenna, out), scoring, set/match flow, side
  switches, serving order, rotation, court/net dimensions, AI decision logic
  that must respect the rules, or tutorial text that explains rules.
---

# Volleyball Rules (FIVB-based)

Beach 2v2 ships first, indoor 6v6 second. The rules engine must support both through a
`RuleSet` ScriptableObject (`BeachRuleSet`, `IndoorRuleSet`), never through `if (isBeach)` scattered
through code.

## 1. Court and equipment
| | Beach | Indoor |
| :-- | :-- | :-- |
| Court | 16 × 8 m (two 8 × 8 halves) | 18 × 9 m (two 9 × 9 halves) |
| Lines | 5–8 cm wide, part of the court ("on the line = in") | 5 cm, part of the court |
| Attack line | none | 3 m from the center line |
| Net height | Men 2.43 m, Women 2.24 m | Men 2.43 m, Women 2.24 m |
| Net size | 8.5 m × 1 m | 9.5–10 m × 1 m |
| Antennas | 1.80 m above the net, on the sideline edges | same |
| Free zone | ≥ 3 m (5–6 m at events) | ≥ 3 m |
| Players | 2, no substitutes | 6 on court (+ libero) |

Use these as `CourtDefinition` ScriptableObject values; the court mesh, line colliders, and AI
positioning all read from it.

## 2. Scoring
| | Beach | Indoor |
| :-- | :-- | :-- |
| System | Rally point: every rally scores a point | same |
| Sets | Best of 3 | Best of 5 |
| Set length | Sets 1–2 to **21**, set 3 to **15**, win by 2, no cap | Sets 1–4 to **25**, set 5 to **15**, win by 2 |
| Side switch | Every **7** points combined (sets 1–2), every **5** (set 3) | After each set; in set 5 at 8 points |
| Technical timeout | At a combined score of 21 (sets 1–2) | Competition-dependent: model as a `RuleSet` flag, default off |
| Serve time limit | 5 s after the whistle | 8 s after the whistle |

## 3. Touches
- Max **3 team touches** before the ball crosses the net. The 4th touch is a fault.
- A player may not touch the ball **twice in a row**, except:
  - after a block (beach: the blocker may take the next touch, **but the block counts as touch 1**;
    indoor: the block does **not** count as a team touch);
  - on the team's **first touch**, one player may make consecutive contacts in a single action
    (e.g., the ball deflects off both forearms during a hard-driven reception).
- **Simultaneous touch** by two teammates = 2 touches in both formats (except during blocking).
- The touch counter resets when the ball crosses the net plane (between the antennas) or touches the opponent.

## 4. Contact faults (the realism core)
| Fault | Definition | Detection in game |
| :-- | :-- | :-- |
| **Catch / lift** | Ball is held or thrown rather than rebounding | Soft-capture duration > `RuleSet.MaxHoldTime` (beach stricter: ~0.06 s vs ~0.10 s indoor) |
| **Double contact** | Ball touches two body parts separately in one action | Two contacts by the same player < `DoubleContactWindow` (0.05–0.08 s) apart **and** not the first team touch |
| Beach **open-hand tip** | Attacking the ball with open-hand fingertips (dink) | Contact type `Set`/fingertips with intent "attack" sending the ball over → fault |
| Beach **overhand set over the net** | An overhand finger pass sending the ball over must go perpendicular to the shoulder line | Angle between ball direction and torso forward > 15° → fault |
| Assisted hit | Using a teammate or a structure to reach the ball | Not applicable in VR (ignore) |

Make the strictness configurable (`Realism: Casual / Standard / Pro`). Casual relaxes the lift and
double-contact windows. Pro uses the real limits.

## 5. Net and line faults
- **Net touch**: touching the net between the antennas while playing the ball is a fault.
  Detect when a hand/forearm capsule overlaps the net volume while the player is "in action"
  (within ~1 s of a touch or block attempt).
- **Center line**: indoor: crossing it with a whole foot = fault. Beach: no center line; interfering
  with the opponent under the net = fault.
- **Ball out**: lands entirely outside the lines, touches an antenna, crosses outside the antennas,
  or touches an object outside the court. **Any part of the ball on a line = in**. Check it using the
  ball's contact footprint radius, not its center.
- **Crossing space**: the ball must cross the net within the crossing space (between the antennas). A ball
  that crosses outside may be played back on the attacking team's own side (except on the third touch).

## 6. Serving
- Server stands behind the end line, anywhere along its width; feet may not touch the line or court
  until after the contact (foot fault).
- Toss and hit with one hand or arm. A toss that drops without being hit: model as `RuleSet.DroppedTossIsFault`
  (default true; check the current FIVB rulebook for the edition you target).
- **Let serve** (ball touches the net and goes over) is **legal** and in play.
- Beach serving order: alternates between players of the serving team each time the team regains serve.
  Indoor: rotation order.
- Screening is a fault (indoor) and is not enforced in VR v1.

## 7. Indoor-only (phase 2)
- **Rotation**: 6 positions, rotate clockwise when side-out is won. Overlap fault at the moment of serve contact.
- **Back-row attack**: back-row players may not complete an attack above net height from in front of the attack line.
- **Libero**: back-row only, can't attack above the net or block; unlimited replacements. Overhand set from the
  front zone by the libero can't be attacked above the net.
- Block doesn't count as a team touch.

## 8. Rally state machine (reference)
```
PreServe ──whistle──► Serving ──contact──► InPlay ──fault/ball dead──► RallyEnd ──► (score, rotate/switch) ──► PreServe
   ▲                    │ timeout (5s/8s) → fault                                        │
   └────────────────────┴───────────────────────────────── set/match over ─► SetEnd/MatchEnd
```
- `InPlay` listens to `BallTouched`, `BallCrossedNetPlane`, `BallHitSurface`, `NetTouched`, `AntennaTouched` events.
- Every fault produces a `RallyResult { WinningTeam, FaultType, FaultingPlayer, Position }` for UI, the referee
  whistle/hand-signal animation, replays, and analytics.
- Dead-ball decisions need a short hold (≈ 0.3 s) after a surface hit before scoring, so the line-call
  replay and audio can play.

## Validation
EditMode tests feed synthetic event sequences into the rules engine and assert the resulting `RallyResult`. At minimum:
4 touches → fault; same player twice → fault; block then same player (beach) → legal and counts as touch 2;
ball on line → in; let serve → in play; 21–20 → set continues; 22–20 → set over; combined score 7 → side switch (beach).
