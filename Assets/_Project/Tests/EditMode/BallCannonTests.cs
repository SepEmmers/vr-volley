using NUnit.Framework;
using UnityEngine;
using VRVolley.Gameplay;
using VRVolley.Physics;

namespace VRVolley.Tests.EditMode
{
    [TestFixture]
    public class BallCannonTests
    {
        [Test]
        public void CalculateLaunchVelocity_ReachesTargetInSpecifiedDuration()
        {
            Vector3 start = new Vector3(0f, 1.8f, 7.5f);
            Vector3 target = new Vector3(1.5f, 0.5f, -6.0f);
            float duration = 1.2f;
            float dt = 1f / 100f;

            Vector3 v0 = BallCannon.CalculateLaunchVelocity(start, target, duration, dt);

            // Integrate trajectory in vacuum
            var profile = BallProfileData.DefaultBeach;
            profile.AirDensity = 0f;

            var state = new BallState(start, v0, Vector3.zero);
            int steps = Mathf.RoundToInt(duration / dt);

            for (int i = 0; i < steps; i++)
            {
                BallIntegrator.Step(ref state, in profile, dt, BallIntegrator.StandardGravity);
            }

            Assert.AreEqual(target.x, state.Position.x, 0.05f);
            Assert.AreEqual(target.y, state.Position.y, 0.05f);
            Assert.AreEqual(target.z, state.Position.z, 0.05f);
        }

        [Test]
        public void ServeToss_RisesSmoothlyToApex_AndFallsUnderGravity()
        {
            Vector3 releasePos = new Vector3(0.2f, 1.4f, -7.0f);
            Vector3 tossVel = new Vector3(0.1f, 4.0f, 0.5f);
            var profile = BallProfileData.DefaultBeach;
            var state = new BallState(releasePos, tossVel, Vector3.zero);

            float dt = 1f / 72f;
            float maxApexY = releasePos.y;
            float flightTime = 0f;

            // Step for 1.2 seconds of flight
            for (int i = 0; i < 86; i++)
            {
                BallIntegrator.Step(ref state, in profile, dt, BallIntegrator.StandardGravity);
                flightTime += dt;
                if (state.Position.y > maxApexY)
                {
                    maxApexY = state.Position.y;
                }
            }

            // At 4 m/s upward, apex is roughly 1.4 + 4^2 / (2 * 9.81) ≈ 2.21m
            Assert.Greater(maxApexY, 2.10f);
            Assert.Less(maxApexY, 2.30f);
            // After 1.2s, ball should have fallen past release height
            Assert.Less(state.Position.y, releasePos.y);
        }
    }
}
