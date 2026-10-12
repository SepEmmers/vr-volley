using System;
using UnityEngine;
using VRVolley.Physics;
using VRVolley.XR;

namespace VRVolley.Gameplay
{
    /// <summary>
    /// Sweeps player hand and forearm contact capsules against the volleyball every physics frame.
    /// Eliminates tunneling via ContactMath.SweepSphereVsMovingCapsule, applies kinematic impulse resolution,
    /// triggers tactile haptic feedback, and invokes touch events for the FIVB referee state machine.
    /// Strictly ZERO managed allocations in per-frame loops.
    /// </summary>
    public sealed class HandBallContactSolver : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private BallBody _ballBody;
        [SerializeField] private BodyContactVolumes _contactVolumes;
        [SerializeField] private VRVolleyHaptics _haptics;

        [Header("Player Metadata")]
        [SerializeField] private int _playerId = 1;
        [SerializeField] private Rules.TeamId _teamId = Rules.TeamId.TeamA;

        [Header("Cooldown Settings")]
        [Tooltip("Minimum time (seconds) between registered contacts on the same player to prevent multiple collisions in one swing.")]
        [SerializeField] private float _contactCooldown = 0.12f;

        private float _cooldownTimer;

        public event Action<int, Rules.TeamId, ContactVolumeType, float> OnBallContact;

        private void Awake()
        {
            if (_ballBody == null)
            {
                _ballBody = FindFirstObjectByType<BallBody>();
            }
            if (_contactVolumes == null)
            {
                _contactVolumes = FindFirstObjectByType<BodyContactVolumes>();
            }
            if (_haptics == null)
            {
                _haptics = FindFirstObjectByType<VRVolleyHaptics>();
            }
        }

        private void FixedUpdate()
        {
            if (_cooldownTimer > 0f)
            {
                _cooldownTimer -= Time.fixedDeltaTime;
                return;
            }

            if (_ballBody == null || _contactVolumes == null)
            {
                return;
            }

            Vector3 ballStart = _ballBody.PreviousState.Position;
            Vector3 ballEnd = _ballBody.CurrentState.Position;
            float ballRadius = _ballBody.Radius;

            // 1. Dual-Forearm Bump Platform Priority
            if (_contactVolumes.IsPlatformActive)
            {
                if (CheckPlatformContact(ballStart, ballEnd, ballRadius))
                {
                    _cooldownTimer = _contactCooldown;
                    return;
                }
            }

            // 2. Right Hand (Palm & Forearm)
            if (CheckSingleArmContact(true, ballStart, ballEnd, ballRadius))
            {
                _cooldownTimer = _contactCooldown;
                return;
            }

            // 3. Left Hand (Palm & Forearm)
            if (CheckSingleArmContact(false, ballStart, ballEnd, ballRadius))
            {
                _cooldownTimer = _contactCooldown;
                return;
            }
        }

        private bool CheckPlatformContact(Vector3 ballStart, Vector3 ballEnd, float ballRadius)
        {
            _contactVolumes.GetForearmCapsule(false, out Vector3 leftStart, out Vector3 leftEnd, out float capRadiusL);
            _contactVolumes.GetForearmCapsule(true, out Vector3 rightStart, out Vector3 rightEnd, out float capRadiusR);

            // Test swept collision against dual forearm geometry
            bool hitL = ContactMath.SweepSphereVsMovingCapsule(
                ballStart, ballEnd, ballRadius,
                leftStart, leftEnd, leftStart, leftEnd, capRadiusL,
                out float hitFracL, out Vector3 normL, out Vector3 ptL);

            bool hitR = ContactMath.SweepSphereVsMovingCapsule(
                ballStart, ballEnd, ballRadius,
                rightStart, rightEnd, rightStart, rightEnd, capRadiusR,
                out float hitFracR, out Vector3 normR, out Vector3 ptR);

            if (hitL || hitR)
            {
                Vector3 contactPoint = hitL ? ptL : ptR;
                Vector3 normal = _contactVolumes.PlatformNormal;

                // Effective surface velocity is the average velocity of both wrists
                Vector3 vLeft = _contactVolumes.LeftEstimator.GetPointVelocity(contactPoint);
                Vector3 vRight = _contactVolumes.RightEstimator.GetPointVelocity(contactPoint);
                Vector3 surfaceVelocity = 0.5f * (vLeft + vRight);

                Vector3 ballVel = _ballBody.CurrentState.Velocity;
                Vector3 ballSpin = _ballBody.CurrentState.AngularVelocity;
                Vector3 oldVel = ballVel;

                ContactMath.ResolveImpulse(
                    ref ballVel, ref ballSpin,
                    _ballBody.Mass, _ballBody.Radius,
                    surfaceVelocity, normal, ContactParams.ForearmPlatform);

                _ballBody.SetVelocity(ballVel, ballSpin);

                float impulseMag = (ballVel - oldVel).magnitude * _ballBody.Mass;
                if (_haptics != null)
                {
                    _haptics.PlayDualHandHaptic(impulseMag, 0.05f);
                }

                OnBallContact?.Invoke(_playerId, _teamId, ContactVolumeType.DualForearmPlatform, impulseMag);
                return true;
            }

            return false;
        }

        private bool CheckSingleArmContact(bool isRight, Vector3 ballStart, Vector3 ballEnd, float ballRadius)
        {
            TrackedVelocityEstimator estimator = isRight ? _contactVolumes.RightEstimator : _contactVolumes.LeftEstimator;
            if (estimator == null)
            {
                return false;
            }

            // Test Palm Capsule (Spike / Open-hand hit)
            _contactVolumes.GetHandCapsule(isRight, out Vector3 handA, out Vector3 handB, out float palmRadius);
            bool hitPalm = ContactMath.SweepSphereVsMovingCapsule(
                ballStart, ballEnd, ballRadius,
                handA, handB, handA, handB, palmRadius,
                out float hitFracP, out Vector3 normP, out Vector3 ptP);

            if (hitPalm)
            {
                ApplyArmContact(estimator, isRight, ptP, normP, ContactParams.SpikePalm,
                    isRight ? ContactVolumeType.RightPalm : ContactVolumeType.LeftPalm);
                return true;
            }

            // Test Single Forearm Capsule (Emergency Dig)
            _contactVolumes.GetForearmCapsule(isRight, out Vector3 armA, out Vector3 armB, out float armRadius);
            bool hitArm = ContactMath.SweepSphereVsMovingCapsule(
                ballStart, ballEnd, ballRadius,
                armA, armB, armA, armB, armRadius,
                out float hitFracA, out Vector3 normA, out Vector3 ptA);

            if (hitArm)
            {
                ApplyArmContact(estimator, isRight, ptA, normA, ContactParams.SingleForearmDig,
                    isRight ? ContactVolumeType.RightForearm : ContactVolumeType.LeftForearm);
                return true;
            }

            return false;
        }

        private void ApplyArmContact(
            TrackedVelocityEstimator estimator,
            bool isRight,
            Vector3 contactPoint,
            Vector3 normal,
            ContactParams contactParams,
            ContactVolumeType volumeType)
        {
            Vector3 surfaceVelocity = estimator.GetPointVelocity(contactPoint);

            Vector3 ballVel = _ballBody.CurrentState.Velocity;
            Vector3 ballSpin = _ballBody.CurrentState.AngularVelocity;
            Vector3 oldVel = ballVel;

            ContactMath.ResolveImpulse(
                ref ballVel, ref ballSpin,
                _ballBody.Mass, _ballBody.Radius,
                surfaceVelocity, normal, contactParams);

            _ballBody.SetVelocity(ballVel, ballSpin);

            float impulseMag = (ballVel - oldVel).magnitude * _ballBody.Mass;
            if (_haptics != null)
            {
                _haptics.PlayContactHaptic(isRight, impulseMag, 0.035f);
            }

            OnBallContact?.Invoke(_playerId, _teamId, volumeType, impulseMag);
        }
    }
}
