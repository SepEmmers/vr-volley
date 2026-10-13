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
        [SerializeField] private BallTossGrabbable _tossGrabbable;

        [Header("Player Metadata")]
        [SerializeField] private int _playerId = 1;
        [SerializeField] private Rules.TeamId _teamId = Rules.TeamId.TeamA;

        [Header("Platform Energy & Bounciness Calibration")]
        [Tooltip("Restitution of the receiving platform (0.2 to 1.5). Default 0.85f ensures energetic FIVB rebound.")]
        [SerializeField] private float _platformRestitution = 0.85f;

        [Tooltip("Effective arm mass (kg). Default 10.0kg.")]
        [SerializeField] private float _platformEffectiveMass = 10.0f;

        [Tooltip("Bounciness multiplier / energy return factor (0.5 to 2.0). 1.0 = standard.")]
        [SerializeField] private float _bounceMultiplier = 1.0f;

        [Header("Spike & Serve Power Calibration")]
        [Tooltip("Velocity amplification for open-palm spike and serve strikes. In VR, controller swing speeds (4-6 m/s) require a boost to match real-life spike velocity (18-25 m/s).")]
        [SerializeField] private float _spikePowerMultiplier = 2.0f;

        [Header("Cooldown Settings")]
        [Tooltip("Minimum time (seconds) between registered contacts on the same player to prevent multiple collisions in one swing.")]
        [SerializeField] private float _contactCooldown = 0.12f;

        private float _cooldownTimer;
        private float _tossImmunityTimer;
        private bool _tossHandIsRight;

        public void NotifyTossReleased(bool isTossingHandRight)
        {
            _tossImmunityTimer = 0.45f;
            _tossHandIsRight = isTossingHandRight;
            _cooldownTimer = 0.20f;
        }

        private const string PrefKeyBounceMultiplier = "VRVolley_PlatformBounce";
        private const string PrefKeyRestitution = "VRVolley_PlatformRestitution";
        private const string PrefKeySpikePower = "VRVolley_SpikePower";

        public float PlatformRestitution
        {
            get => _platformRestitution;
            set
            {
                _platformRestitution = Mathf.Clamp(value, 0.20f, 1.50f);
                PlayerPrefs.SetFloat(PrefKeyRestitution, _platformRestitution);
                PlayerPrefs.Save();
            }
        }

        public float BounceMultiplier
        {
            get => _bounceMultiplier;
            set
            {
                _bounceMultiplier = Mathf.Clamp(value, 0.50f, 2.00f);
                PlayerPrefs.SetFloat(PrefKeyBounceMultiplier, _bounceMultiplier);
                PlayerPrefs.Save();
            }
        }

        public float SpikePowerMultiplier
        {
            get => _spikePowerMultiplier;
            set
            {
                _spikePowerMultiplier = Mathf.Clamp(value, 1.0f, 3.5f);
                PlayerPrefs.SetFloat(PrefKeySpikePower, _spikePowerMultiplier);
                PlayerPrefs.Save();
            }
        }

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
            if (_tossGrabbable == null)
            {
                _tossGrabbable = FindFirstObjectByType<BallTossGrabbable>();
            }

            _platformRestitution = PlayerPrefs.GetFloat(PrefKeyRestitution, 0.85f);
            _bounceMultiplier = PlayerPrefs.GetFloat(PrefKeyBounceMultiplier, 1.0f);
            _spikePowerMultiplier = PlayerPrefs.GetFloat(PrefKeySpikePower, 2.0f);
        }

        private void FixedUpdate()
        {
            if (_cooldownTimer > 0f)
            {
                _cooldownTimer -= Time.fixedDeltaTime;
                return;
            }

            if (_tossImmunityTimer > 0f)
            {
                _tossImmunityTimer -= Time.fixedDeltaTime;
            }

            // If ball is suspended waiting to be grabbed, or is currently held, hands do not hit it!
            if (_ballBody == null || _contactVolumes == null || _ballBody.IsSuspended)
            {
                return;
            }

            if (_tossGrabbable != null && _tossGrabbable.IsGrabbed)
            {
                return;
            }

            Vector3 ballStart = _ballBody.PreviousState.Position;
            Vector3 ballEnd = _ballBody.CurrentState.Position;
            float ballRadius = _ballBody.Radius;

            // 1. Dual-Forearm Bump Platform Priority (ignored during toss release immunity)
            if (_tossImmunityTimer <= 0f && _contactVolumes.IsPlatformActive)
            {
                if (CheckPlatformContact(ballStart, ballEnd, ballRadius))
                {
                    _cooldownTimer = _contactCooldown;
                    return;
                }
            }

            // 2. Right Hand (Palm & Forearm) - immune if right hand just tossed the ball
            bool skipRight = (_tossImmunityTimer > 0f && _tossHandIsRight);
            if (!skipRight && CheckSingleArmContact(true, ballStart, ballEnd, ballRadius))
            {
                _cooldownTimer = _contactCooldown;
                return;
            }

            // 3. Left Hand (Palm & Forearm) - immune if left hand just tossed the ball
            bool skipLeft = (_tossImmunityTimer > 0f && !_tossHandIsRight);
            if (!skipLeft && CheckSingleArmContact(false, ballStart, ballEnd, ballRadius))
            {
                _cooldownTimer = _contactCooldown;
                return;
            }
        }

        private bool CheckPlatformContact(Vector3 ballStart, Vector3 ballEnd, float ballRadius)
        {
            _contactVolumes.GetForearmCapsule(false, out Vector3 leftStart, out Vector3 leftEnd, out float capRadiusL);
            _contactVolumes.GetForearmCapsule(true, out Vector3 rightStart, out Vector3 rightEnd, out float capRadiusR);
            _contactVolumes.GetHandCapsule(false, out Vector3 leftHandStart, out Vector3 leftHandEnd, out float capHandRadiusL);
            _contactVolumes.GetHandCapsule(true, out Vector3 rightHandStart, out Vector3 rightHandEnd, out float capHandRadiusR);

            // Test swept collision against dual forearm and clasped hand geometry
            bool hitL = ContactMath.SweepSphereVsMovingCapsule(
                ballStart, ballEnd, ballRadius,
                leftStart, leftEnd, leftStart, leftEnd, capRadiusL,
                out float hitFracL, out Vector3 normL, out Vector3 ptL);

            bool hitR = ContactMath.SweepSphereVsMovingCapsule(
                ballStart, ballEnd, ballRadius,
                rightStart, rightEnd, rightStart, rightEnd, capRadiusR,
                out float hitFracR, out Vector3 normR, out Vector3 ptR);

            bool hitHandL = ContactMath.SweepSphereVsMovingCapsule(
                ballStart, ballEnd, ballRadius,
                leftHandStart, leftHandEnd, leftHandStart, leftHandEnd, capHandRadiusL,
                out float hitFracHL, out Vector3 normHL, out Vector3 ptHL);

            bool hitHandR = ContactMath.SweepSphereVsMovingCapsule(
                ballStart, ballEnd, ballRadius,
                rightHandStart, rightHandEnd, rightHandStart, rightHandEnd, capHandRadiusR,
                out float hitFracHR, out Vector3 normHR, out Vector3 ptHR);

            if (hitL || hitR || hitHandL || hitHandR)
            {
                Vector3 contactPoint = hitL ? ptL : (hitR ? ptR : (hitHandL ? ptHL : ptHR));
                Vector3 normal = _contactVolumes.PlatformNormal;

                // Effective surface velocity (in 1-Hand mode, derived from dominant controller; in 2-Hand mode, averaged)
                Vector3 surfaceVelocity;
                if (_contactVolumes.IsOneHandMode)
                {
                    TrackedVelocityEstimator dominant = _contactVolumes.DominantIsRight ? _contactVolumes.RightEstimator : _contactVolumes.LeftEstimator;
                    surfaceVelocity = dominant != null ? dominant.GetPointVelocity(contactPoint) : Vector3.zero;
                }
                else
                {
                    Vector3 vLeft = _contactVolumes.LeftEstimator != null ? _contactVolumes.LeftEstimator.GetPointVelocity(contactPoint) : Vector3.zero;
                    Vector3 vRight = _contactVolumes.RightEstimator != null ? _contactVolumes.RightEstimator.GetPointVelocity(contactPoint) : Vector3.zero;
                    surfaceVelocity = 0.5f * (vLeft + vRight);
                }

                Vector3 ballVel = _ballBody.CurrentState.Velocity;
                Vector3 ballSpin = _ballBody.CurrentState.AngularVelocity;
                Vector3 oldVel = ballVel;

                ContactParams p = new ContactParams(
                    Mathf.Clamp01(_platformRestitution * _bounceMultiplier),
                    _platformEffectiveMass * _bounceMultiplier,
                    0.30f);

                ContactMath.ResolveImpulse(
                    ref ballVel, ref ballSpin,
                    _ballBody.Mass, _ballBody.Radius,
                    surfaceVelocity, normal, p);

                _ballBody.SetVelocity(ballVel, ballSpin);

                float impulseMag = (ballVel - oldVel).magnitude * _ballBody.Mass;
                if (_haptics != null)
                {
                    if (_contactVolumes.IsOneHandMode)
                    {
                        _haptics.PlayContactHaptic(_contactVolumes.DominantIsRight, impulseMag, 0.05f);
                    }
                    else
                    {
                        _haptics.PlayDualHandHaptic(impulseMag, 0.05f);
                    }
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
                ContactParams pDig = new ContactParams(
                    Mathf.Clamp01(0.70f * _bounceMultiplier),
                    6.0f * _bounceMultiplier,
                    0.30f);
                ApplyArmContact(estimator, isRight, ptA, normA, pDig,
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
            bool isPalmStrike = volumeType == ContactVolumeType.RightPalm || volumeType == ContactVolumeType.LeftPalm;
            if (isPalmStrike)
            {
                surfaceVelocity *= _spikePowerMultiplier;
                contactParams.Restitution = Mathf.Max(contactParams.Restitution, 0.85f);
                contactParams.EffectiveMass = Mathf.Max(contactParams.EffectiveMass, 8.0f);
            }

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
