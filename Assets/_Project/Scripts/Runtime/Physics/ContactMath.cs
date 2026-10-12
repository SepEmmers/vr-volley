using UnityEngine;

namespace VRVolley.Physics
{
    /// <summary>
    /// Pure C# contact solver: sub-stepped continuous swept collision queries and
    /// kinematic impulse resolution with effective arm mass, restitution, and friction spin transfer.
    /// Strictly ZERO managed allocations.
    /// </summary>
    public static class ContactMath
    {
        public const float HollowSphereInertiaFactor = 2f / 3f;

        /// <summary>
        /// Finds the closest point on segment AB to point P.
        /// </summary>
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
        /// Sub-stepped swept query of a moving sphere against a moving capsule over one timestep.
        /// Sub-steps are dynamically scaled by relative displacement to prevent high-velocity tunneling.
        /// </summary>
        public static bool SweepSphereVsMovingCapsule(
            Vector3 ballStart, Vector3 ballEnd, float ballRadius,
            Vector3 capAStart, Vector3 capBStart, Vector3 capAEnd, Vector3 capBEnd, float capRadius,
            out float hitFraction, out Vector3 normal, out Vector3 contactPoint)
        {
            float radiusSum = ballRadius + capRadius;
            float radiusSumSq = radiusSum * radiusSum;

            Vector3 capMidStart = 0.5f * (capAStart + capBStart);
            Vector3 capMidEnd = 0.5f * (capAEnd + capBEnd);
            Vector3 relDisp = (ballEnd - ballStart) - (capMidEnd - capMidStart);
            float relDist = relDisp.magnitude;

            // Dynamically scale sub-steps so relative motion per step is less than half a ball radius
            int subSteps = Mathf.Clamp(Mathf.CeilToInt(relDist / (0.5f * ballRadius)), 1, 16);

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
                    normal = dist > 1e-6f ? d / dist : -relDisp.normalized;
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
        /// Resolves kinematic impulse between the ball and a moving body surface.
        /// surfaceVelocity must represent the point velocity of the striking body (v_hand + ω_hand × r).
        /// Note: Hand swing velocity is ALREADY folded into surfaceVelocity; NEVER add hand velocity a second time.
        /// Normal must point from the surface toward the ball center.
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
                return; // Bodies are already separating
            }

            // Normal impulse with effective inertial arm mass:
            // Δv_n = −(1 + e) · v_n · M_eff / (M_eff + m_ball)
            float massFactor = p.EffectiveMass / (p.EffectiveMass + ballMass);
            float dvN = -(1f + p.Restitution) * vn * massFactor;
            Vector3 deltaV = dvN * normal;

            // Tangential slip velocity at contact point (folds in ball angular velocity)
            Vector3 r = -normal * ballRadius; // vector from ball center to contact point
            Vector3 vContact = vRel + Vector3.Cross(ballAngularVelocity, r);
            Vector3 vt = vContact - Vector3.Dot(vContact, normal) * normal;
            float vtMag = vt.magnitude;
            if (vtMag > 1e-5f)
            {
                // Maximum tangential impulse that reduces relative slip to zero
                float k = HollowSphereInertiaFactor;
                float stickLimit = vtMag / (1f + 1f / k);
                float dvT = Mathf.Min(p.Friction * dvN, stickLimit);
                Vector3 deltaVt = -vt * (dvT / vtMag);
                deltaV += deltaVt;

                // Angular impulse: Δω = (r × m·Δv_t) / (k·m·r²)
                ballAngularVelocity += Vector3.Cross(r, deltaVt) / (k * ballRadius * ballRadius);
            }

            ballVelocity += deltaV;
        }
    }
}
