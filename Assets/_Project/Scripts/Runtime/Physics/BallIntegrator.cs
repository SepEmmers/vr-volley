using UnityEngine;

namespace VRVolley.Physics
{
    /// <summary>
    /// Pure C# aerodynamic numerical integrator for the volleyball.
    /// Evaluates gravity, speed-dependent drag crisis, Magnus spin lift, float knuckle wobble,
    /// and spin decay. Zero managed allocations.
    /// </summary>
    public static class BallIntegrator
    {
        public static readonly Vector3 StandardGravity = new Vector3(0f, -9.81f, 0f);

        /// <summary>
        /// Computes the drag coefficient Cd taking into account the volleyball drag crisis.
        /// </summary>
        public static float ComputeDragCoefficient(float speed, in BallProfileData profile)
        {
            if (speed <= profile.DragCrisisSpeedMin)
            {
                return profile.DragSubcritical;
            }
            if (speed >= profile.DragCrisisSpeedMax)
            {
                return profile.DragSupercritical;
            }

            float t = (speed - profile.DragCrisisSpeedMin) / (profile.DragCrisisSpeedMax - profile.DragCrisisSpeedMin);
            // Smoothstep transition through the boundary layer detachment zone
            float smoothT = t * t * (3f - 2f * t);
            return Mathf.Lerp(profile.DragSubcritical, profile.DragSupercritical, smoothT);
        }

        /// <summary>
        /// Computes Magnus lift coefficient from dimensionless spin parameter S = (r * ω) / v.
        /// </summary>
        public static float ComputeMagnusLiftCoefficient(float speed, float spinRate, in BallProfileData profile)
        {
            if (speed < 0.1f)
            {
                return 0f;
            }
            float spinParam = (profile.Radius * spinRate) / speed;
            return Mathf.Min(profile.MagnusCoefficientSlope * spinParam, profile.MagnusMaxLift);
        }

        /// <summary>
        /// Advances ball kinematics by dt using semi-implicit Euler integration.
        /// </summary>
        public static void Step(
            ref BallState state,
            in BallProfileData profile,
            float dt,
            Vector3 gravity,
            int floatServeSeed = 0,
            float flightTime = 0f)
        {
            float speed = state.Velocity.magnitude;
            float spin = state.AngularVelocity.magnitude;

            // 1. Gravity Force
            Vector3 totalForce = profile.Mass * gravity;

            if (speed > 1e-4f)
            {
                Vector3 vDir = state.Velocity / speed;
                float dynamicPressure = 0.5f * profile.AirDensity * profile.CrossSectionalArea * speed * speed;

                // 2. Aerodynamic Drag Force (with drag crisis)
                float cd = ComputeDragCoefficient(speed, in profile);
                Vector3 fDrag = -cd * dynamicPressure * vDir;
                totalForce += fDrag;

                // 3. Magnus Lift Force: F_magnus = (1/2) * ρ * C_L * A * |v|^2 * normalize(ω × v)
                if (spin > 1e-4f)
                {
                    Vector3 spinAxisCrossV = Vector3.Cross(state.AngularVelocity, state.Velocity);
                    float crossMag = spinAxisCrossV.magnitude;
                    if (crossMag > 1e-5f)
                    {
                        Vector3 liftDir = spinAxisCrossV / crossMag;
                        float cl = ComputeMagnusLiftCoefficient(speed, spin, in profile);
                        Vector3 fMagnus = cl * dynamicPressure * liftDir;
                        totalForce += fMagnus;
                    }
                }

                // 4. Knuckle Wobble Force (Float Serve)
                if (spin <= profile.KnuckleMaxSpin &&
                    speed >= profile.KnuckleSpeedMin &&
                    speed <= profile.KnuckleSpeedMax &&
                    floatServeSeed != 0)
                {
                    // Find lateral horizontal unit vector orthogonal to velocity
                    Vector3 lateral = Vector3.Cross(vDir, Vector3.up);
                    if (lateral.sqrMagnitude < 1e-4f)
                    {
                        lateral = Vector3.Cross(vDir, Vector3.right);
                    }
                    lateral.Normalize();

                    // Smooth deterministic noise wobble
                    float noiseSample = Mathf.PerlinNoise(floatServeSeed * 0.137f, flightTime * profile.KnuckleFrequency);
                    float wobbleNormalized = (noiseSample * 2f) - 1f; // range [-1, 1]

                    // Window fade at boundaries of knuckle speed
                    float speedFade = 1f;
                    if (speed < profile.KnuckleSpeedMin + 1.0f)
                    {
                        speedFade = Mathf.Clamp01(speed - profile.KnuckleSpeedMin);
                    }
                    else if (speed > profile.KnuckleSpeedMax - 1.0f)
                    {
                        speedFade = Mathf.Clamp01(profile.KnuckleSpeedMax - speed);
                    }

                    Vector3 fKnuckle = lateral * (wobbleNormalized * profile.KnuckleSideForceCoeff * dynamicPressure * speedFade);
                    totalForce += fKnuckle;
                }
            }

            // 5. Spin Decay: ω *= exp(-dt / τ)
            if (profile.SpinDecayHalfLife > 0f)
            {
                float decayFactor = Mathf.Exp(-dt / profile.SpinDecayHalfLife);
                state.AngularVelocity *= decayFactor;
            }

            // 6. Kinematic State Update
            Vector3 acceleration = totalForce / profile.Mass;
            state.Velocity += acceleration * dt;
            state.Position += state.Velocity * dt;
            state.Timestamp += dt;
        }
    }
}
