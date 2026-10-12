using NUnit.Framework;
using UnityEngine;
using VRVolley.Physics;

namespace VRVolley.Tests.EditMode
{
    [TestFixture]
    public class BallIntegratorTests
    {
        [Test]
        public void FreeFall_WithoutAirDrag_MatchesAnalyticalFreeFallTime()
        {
            var profile = BallProfileData.DefaultBeach;
            profile.AirDensity = 0f; // Vacuum free-fall test

            float startHeight = 2.0f;
            var state = new BallState(new Vector3(0f, startHeight, 0f), Vector3.zero, Vector3.zero);
            float dt = 1.0f / 72.0f;

            float expectedTime = Mathf.Sqrt(2f * startHeight / 9.81f); // ~0.63855 s
            float elapsed = 0f;

            while (state.Position.y > 0f && elapsed < 2.0f)
            {
                BallIntegrator.Step(ref state, in profile, dt, BallIntegrator.StandardGravity);
                elapsed += dt;
            }

            Assert.AreEqual(expectedTime, elapsed, dt * 1.5f, "Vacuum drop time must match analytical t = sqrt(2h/g)");
        }

        [Test]
        public void DragCrisis_CoefficientDecreasesAtHighVelocity()
        {
            var profile = BallProfileData.DefaultBeach;

            float cdLow = BallIntegrator.ComputeDragCoefficient(10f, in profile);
            float cdMid = BallIntegrator.ComputeDragCoefficient(18.5f, in profile);
            float cdHigh = BallIntegrator.ComputeDragCoefficient(25f, in profile);

            Assert.AreEqual(profile.DragSubcritical, cdLow, 1e-4f);
            Assert.AreEqual(profile.DragSupercritical, cdHigh, 1e-4f);
            Assert.IsTrue(cdMid < cdLow && cdMid > cdHigh, "Mid-crisis drag must be smoothly interpolated between subcritical and supercritical");
        }

        [Test]
        public void AerodynamicDrag_ReducesForwardTravelDistance()
        {
            var profileDrag = BallProfileData.DefaultBeach;
            var profileVacuum = BallProfileData.DefaultBeach;
            profileVacuum.AirDensity = 0f;

            var stateDrag = new BallState(Vector3.up * 2f, new Vector3(0f, 0f, 20f), Vector3.zero);
            var stateVacuum = new BallState(Vector3.up * 2f, new Vector3(0f, 0f, 20f), Vector3.zero);

            float dt = 1f / 72f;
            for (int i = 0; i < 72; i++) // 1 second flight
            {
                BallIntegrator.Step(ref stateDrag, in profileDrag, dt, BallIntegrator.StandardGravity);
                BallIntegrator.Step(ref stateVacuum, in profileVacuum, dt, BallIntegrator.StandardGravity);
            }

            Assert.Less(stateDrag.Position.z, stateVacuum.Position.z, "Drag must retard forward travel distance");
            Assert.Less(stateDrag.Velocity.z, stateVacuum.Velocity.z, "Drag must reduce forward velocity");
        }

        [Test]
        public void MagnusEffect_TopspinProducesDownwardDive()
        {
            var profile = BallProfileData.DefaultBeach;
            float dt = 1f / 72f;

            // Forward flight along +Z. Topspin: ω = (+X) * 50 rad/s.
            // Using v_surface = ω × r convention: ω × v = (+X) × (+Z) = -Y (downward lift force)
            var stateTopspin = new BallState(new Vector3(0f, 3f, 0f), new Vector3(0f, 0f, 22f), new Vector3(50f, 0f, 0f));
            var stateNoSpin = new BallState(new Vector3(0f, 3f, 0f), new Vector3(0f, 0f, 22f), Vector3.zero);

            for (int i = 0; i < 36; i++) // 0.5s of flight
            {
                BallIntegrator.Step(ref stateTopspin, in profile, dt, BallIntegrator.StandardGravity);
                BallIntegrator.Step(ref stateNoSpin, in profile, dt, BallIntegrator.StandardGravity);
            }

            Assert.Less(stateTopspin.Position.y, stateNoSpin.Position.y, "Topspin must pull the volleyball downward (diving spike effect)");
        }

        [Test]
        public void MagnusEffect_BackspinProducesUpwardFloat()
        {
            var profile = BallProfileData.DefaultBeach;
            float dt = 1f / 72f;

            // Forward flight along +Z. Backspin: ω = (-X) * 50 rad/s.
            // ω × v = (-X) × (+Z) = +Y (upward lift force)
            var stateBackspin = new BallState(new Vector3(0f, 3f, 0f), new Vector3(0f, 0f, 22f), new Vector3(-50f, 0f, 0f));
            var stateNoSpin = new BallState(new Vector3(0f, 3f, 0f), new Vector3(0f, 0f, 22f), Vector3.zero);

            for (int i = 0; i < 36; i++)
            {
                BallIntegrator.Step(ref stateBackspin, in profile, dt, BallIntegrator.StandardGravity);
                BallIntegrator.Step(ref stateNoSpin, in profile, dt, BallIntegrator.StandardGravity);
            }

            Assert.Greater(stateBackspin.Position.y, stateNoSpin.Position.y, "Backspin must generate upward lift counteracting gravity");
        }

        [Test]
        public void SpinDecay_FollowsExponentialDecayConstant()
        {
            var profile = BallProfileData.DefaultBeach;
            profile.AirDensity = 0f; // isolate spin decay
            profile.SpinDecayHalfLife = 4.0f;

            float initialSpin = 60f;
            var state = new BallState(Vector3.zero, Vector3.zero, new Vector3(initialSpin, 0f, 0f));

            float dt = 1f / 72f;
            int totalSteps = Mathf.RoundToInt(4.0f / dt);
            for (int i = 0; i < totalSteps; i++)
            {
                BallIntegrator.Step(ref state, in profile, dt, Vector3.zero);
            }

            float expectedSpin = initialSpin * Mathf.Exp(-1.0f); // ~22.07 rad/s
            Assert.AreEqual(expectedSpin, state.AngularVelocity.x, 0.1f, "Spin must decay by 1/e after tau seconds");
        }

        [Test]
        public void FloatKnuckle_ProducesDeterministicLateralWobble()
        {
            var profile = BallProfileData.DefaultBeach;
            float dt = 1f / 72f;

            // Ball flying at float serve speed (17 m/s) with very low spin (2 rad/s)
            var stateFloat1 = new BallState(Vector3.up * 2.5f, new Vector3(0f, 0f, 17f), new Vector3(0f, 2f, 0f));
            var stateFloat2 = new BallState(Vector3.up * 2.5f, new Vector3(0f, 0f, 17f), new Vector3(0f, 2f, 0f));

            float flightTime = 0f;
            for (int i = 0; i < 50; i++)
            {
                BallIntegrator.Step(ref stateFloat1, in profile, dt, BallIntegrator.StandardGravity, floatServeSeed: 42, flightTime: flightTime);
                BallIntegrator.Step(ref stateFloat2, in profile, dt, BallIntegrator.StandardGravity, floatServeSeed: 42, flightTime: flightTime);
                flightTime += dt;
            }

            // Must produce lateral horizontal deflection
            Assert.AreNotEqual(0f, stateFloat1.Position.x, "Float serve must induce lateral deflection");
            // Must be deterministic across clients with identical seed
            Assert.AreEqual(stateFloat1.Position.x, stateFloat2.Position.x, 1e-6f, "Float serve wobble must be deterministic with identical seed");
        }
    }
}
