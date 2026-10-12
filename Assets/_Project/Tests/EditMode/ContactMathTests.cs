using NUnit.Framework;
using UnityEngine;
using VRVolley.Physics;

namespace VRVolley.Tests.EditMode
{
    [TestFixture]
    public class ContactMathTests
    {
        private const float BallRadius = 0.107f;
        private const float BallMass = 0.270f;
        private const float HandRadius = 0.045f;

        [Test]
        public void SweepSphereVsMovingCapsule_PreventsHighVelocityTunneling()
        {
            // Ball is stationary at origin
            Vector3 ballStart = Vector3.zero;
            Vector3 ballEnd = Vector3.zero;

            // Hand moves 0.5m in a single frame directly through the ball (from -0.25m to +0.25m)
            // Discrete collision check at start and end misses, but continuous sweep must hit!
            Vector3 handAStart = new Vector3(0f, -0.25f, -0.05f);
            Vector3 handBStart = new Vector3(0f, -0.25f, 0.05f);

            Vector3 handAEnd = new Vector3(0f, 0.25f, -0.05f);
            Vector3 handBEnd = new Vector3(0f, 0.25f, 0.05f);

            bool hit = ContactMath.SweepSphereVsMovingCapsule(
                ballStart, ballEnd, BallRadius,
                handAStart, handBStart, handAEnd, handBEnd, HandRadius,
                out float hitFraction, out Vector3 normal, out Vector3 contactPoint);

            Assert.IsTrue(hit, "High-velocity hand sweep must detect contact without tunneling");
            Assert.IsTrue(hitFraction >= 0f && hitFraction <= 1f, "Hit fraction must be within [0, 1]");
            Assert.AreNotEqual(Vector3.zero, normal, "Hit normal must be non-zero");
        }

        [Test]
        public void SweepSphereVsMovingCapsule_MissReturnsFalse()
        {
            Vector3 ballStart = Vector3.zero;
            Vector3 ballEnd = Vector3.zero;

            // Hand passes 1.0m to the right
            Vector3 handAStart = new Vector3(1f, -0.25f, -0.05f);
            Vector3 handBStart = new Vector3(1f, -0.25f, 0.05f);

            Vector3 handAEnd = new Vector3(1f, 0.25f, -0.05f);
            Vector3 handBEnd = new Vector3(1f, 0.25f, 0.05f);

            bool hit = ContactMath.SweepSphereVsMovingCapsule(
                ballStart, ballEnd, BallRadius,
                handAStart, handBStart, handAEnd, handBEnd, HandRadius,
                out float hitFraction, out Vector3 normal, out Vector3 contactPoint);

            Assert.IsFalse(hit, "Distant hand sweep must return false");
        }

        [Test]
        public void ResolveImpulse_SpikeSwingTransfersVelocityAccurately()
        {
            // Falling ball (-5 m/s Y)
            Vector3 ballVel = new Vector3(0f, -5f, 0f);
            Vector3 ballSpin = Vector3.zero;

            // Hand swings forward at 15 m/s along +Z and downward at -2 m/s along -Y
            Vector3 handPointVel = new Vector3(0f, -2f, 15f);

            // Normal pointing from hand palm to ball center (angled forward/downward)
            Vector3 normal = new Vector3(0f, 0.3f, 0.95f).normalized;

            ContactMath.ResolveImpulse(
                ref ballVel, ref ballSpin,
                BallMass, BallRadius,
                handPointVel, normal, in ContactParams.SpikePalm);

            // Outgoing ball must have strong positive forward velocity and be within authentic spike range (20 - 30 m/s)
            float outgoingSpeed = ballVel.magnitude;
            Assert.Greater(ballVel.z, 15f, "Spiked ball must acquire forward momentum");
            Assert.IsTrue(outgoingSpeed >= 18f && outgoingSpeed <= 32f, $"Spike outgoing speed ({outgoingSpeed} m/s) must be in authentic range [18, 32] m/s");
        }

        [Test]
        public void ResolveImpulse_SeparatingBodiesDoNotApplyImpulse()
        {
            Vector3 ballVel = new Vector3(0f, 10f, 0f); // moving upward
            Vector3 ballSpin = Vector3.zero;
            Vector3 handVel = Vector3.zero;
            Vector3 normal = Vector3.up; // points from hand to ball

            // Relative velocity is separating (ball moving away from hand along normal)
            ContactMath.ResolveImpulse(
                ref ballVel, ref ballSpin,
                BallMass, BallRadius,
                handVel, normal, in ContactParams.ForearmPlatform);

            Assert.AreEqual(new Vector3(0f, 10f, 0f), ballVel, "Separating contact must not modify velocity");
        }

        [Test]
        public void ResolveImpulse_FrictionTransfersTopspinOnTangentialHit()
        {
            // Ball falling vertically
            Vector3 ballVel = new Vector3(0f, -5f, 0f);
            Vector3 ballSpin = Vector3.zero;

            // Hand brush upward and forward
            Vector3 handVel = new Vector3(0f, 10f, 5f);
            Vector3 normal = Vector3.up; // normal is vertical, tangential velocity is forward

            ContactMath.ResolveImpulse(
                ref ballVel, ref ballSpin,
                BallMass, BallRadius,
                handVel, normal, in ContactParams.SpikePalm);

            // Tangential friction must induce spin (non-zero angular velocity)
            Assert.AreNotEqual(Vector3.zero, ballSpin, "Tangential friction must impart spin to the ball");
        }
    }
}
