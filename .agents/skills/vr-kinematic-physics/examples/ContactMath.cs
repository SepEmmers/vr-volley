// Reference implementation for the vr-kinematic-physics skill.
// Copy into Assets/_Project/Scripts/Runtime/Physics/ when implementing.
// Pure math: no UnityEngine.Object access, no allocations, deterministic → EditMode-testable.
using UnityEngine;

namespace VRVolley.Physics
{
    public struct ContactParams
    {
        public float Restitution;        // e, normal coefficient of restitution
        public float EffectiveMass;      // M_eff of the hitting body segment (kg)
        public float Friction;           // Coulomb μ for tangential impulse (spin transfer)
    }

    public static class ContactMath
    {
        /// <summary>Hollow-sphere inertia factor k in I = k·m·r².</summary>
        public const float HollowSphereInertiaFactor = 2f / 3f;

        public static Vector3 ClosestPointOnSegment(Vector3 a, Vector3 b, Vector3 p)
        {
            Vector3 ab = b - a;
            float lenSq = Vector3.Dot(ab, ab);
            if (lenSq < 1e-12f)
            {
                return a;
            }
            float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / lenSq);
            return a + ab * t;
        }

        /// <summary>
        /// Sub-stepped swept test of a moving sphere against a moving capsule over one physics step.
        /// Sub-step count is derived from the relative displacement so nothing tunnels.
        /// Returns the earliest overlapping sub-step.
        /// </summary>
        public static bool SweepSphereVsMovingCapsule(
            Vector3 ballStart, Vector3 ballEnd, float ballRadius,
            Vector3 capAStart, Vector3 capBStart, Vector3 capAEnd, Vector3 capBEnd, float capRadius,
            out float hitFraction, out Vector3 normal, out Vector3 contactPoint)
        {
            float radiusSum = ballRadius + capRadius;
            float radiusSumSq = radiusSum * radiusSum;

            // Relative displacement of the ball w.r.t. the capsule mid-point decides resolution.
            Vector3 capMidStart = 0.5f * (capAStart + capBStart);
            Vector3 capMidEnd = 0.5f * (capAEnd + capBEnd);
            float relDisp = ((ballEnd - ballStart) - (capMidEnd - capMidStart)).magnitude;
            int subSteps = Mathf.Clamp(Mathf.CeilToInt(relDisp / (0.5f * ballRadius)), 1, 16);

            for (int i = 0; i <= subSteps; i++)
            {
                float t = (float)i / subSteps;
                Vector3 c = Vector3.LerpUnclamped(ballStart, ballEnd, t);
                Vector3 a = Vector3.LerpUnclamped(capAStart, capAEnd, t);
                Vector3 b = Vector3.LerpUnclamped(capBStart, capBEnd, t);
                Vector3 closest = ClosestPointOnSegment(a, b, c);
                Vector3 d = c - closest;
                float distSq = d.sqrMagnitude;
                if (distSq <= radiusSumSq)
                {
                    float dist = Mathf.Sqrt(distSq);
                    // Degenerate: ball center on the segment → push opposite to relative motion.
                    normal = dist > 1e-6f ? d / dist : -((ballEnd - ballStart) - (capMidEnd - capMidStart)).normalized;
                    contactPoint = closest + normal * capRadius;
                    hitFraction = t;
                    return true;
                }
            }

            hitFraction = 1f;
            normal = Vector3.zero;
            contactPoint = Vector3.zero;
            return false;
        }

        /// <summary>
        /// Impulse response of the ball against a moving body surface.
        /// surfaceVelocity must already include the swing (v_hand + ω_hand × r). Do NOT add swing again.
        /// normal points from the surface toward the ball center.
        /// </summary>
        public static void ResolveImpulse(
            ref Vector3 ballVelocity, ref Vector3 ballAngularVelocity,
            float ballMass, float ballRadius,
            Vector3 surfaceVelocity, Vector3 normal, in ContactParams p)
        {
            Vector3 vRel = ballVelocity - surfaceVelocity;
            float vn = Vector3.Dot(vRel, normal);
            if (vn >= 0f)
            {
                return; // already separating
            }

            // Normal: Δv_n = −(1 + e)·v_n · M/(M + m)
            float massFactor = p.EffectiveMass / (p.EffectiveMass + ballMass);
            float dvN = -(1f + p.Restitution) * vn * massFactor;
            Vector3 deltaV = dvN * normal;

            // Tangential: slip velocity at the contact point (includes ball spin).
            Vector3 r = -normal * ballRadius; // ball center → contact point
            Vector3 vContact = vRel + Vector3.Cross(ballAngularVelocity, r);
            Vector3 vt = vContact - Vector3.Dot(vContact, normal) * normal;
            float vtMag = vt.magnitude;
            if (vtMag > 1e-5f)
            {
                // Max tangential Δv that brings slip to zero: vt / (1 + 1/k)
                float k = HollowSphereInertiaFactor;
                float stickLimit = vtMag / (1f + 1f / k);
                float dvT = Mathf.Min(p.Friction * dvN, stickLimit);
                Vector3 deltaVt = -vt * (dvT / vtMag);
                deltaV += deltaVt;

                // Δω = (r × m·Δv_t) / (k·m·r²)
                ballAngularVelocity += Vector3.Cross(r, deltaVt) / (k * ballRadius * ballRadius);
            }

            ballVelocity += deltaV;
        }
    }
}
