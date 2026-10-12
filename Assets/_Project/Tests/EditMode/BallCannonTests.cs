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
    }
}
