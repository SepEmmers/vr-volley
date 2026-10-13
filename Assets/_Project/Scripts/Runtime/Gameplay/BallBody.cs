using System;
using UnityEngine;
using VRVolley.Physics;

namespace VRVolley.Gameplay
{
    /// <summary>
    /// Runtime MonoBehaviour adaptor for the volleyball.
    /// Drives BallState integration in FixedUpdate, handles non-allocating environment collisions,
    /// performs smooth visual transform interpolation in Update(), and projects a depth-cue blob shadow.
    /// Strictly ZERO managed allocations in per-frame loops.
    /// </summary>
    public sealed class BallBody : MonoBehaviour
    {
        [Header("Profile & Configuration")]
        [SerializeField] private bool _useBeachProfile = true;
        [SerializeField] private LayerMask _environmentLayers;
        [SerializeField] private Transform _visualBallTransform;
        [SerializeField] private Transform _blobShadowTransform;
        [SerializeField] private float _maxBlobShadowHeight = 6.0f;

        [Header("State (Inspector Read-Only)")]
        [SerializeField] private bool _isSuspended;
        [SerializeField] private Vector3 _debugVelocity;
        [SerializeField] private float _debugSpeed;

        private BallProfileData _profile;
        private BallState _currentState;
        private BallState _previousState;
        private Quaternion _visualRotation;

        private RaycastHit[] _sweepHits;
        private const int MaxSweepHits = 4;

        public event Action<Vector3, Vector3, bool> OnEnvironmentCollision; // contactPoint, normal, isGround

        public BallState CurrentState => _currentState;
        public BallState PreviousState => _previousState;
        public BallProfileData Profile => _profile;
        public float Radius => _profile.Radius;
        public float Mass => _profile.Mass;
        public bool IsSuspended => _isSuspended;

        private void Awake()
        {
            _profile = _useBeachProfile ? BallProfileData.DefaultBeach : BallProfileData.DefaultIndoor;
            _sweepHits = new RaycastHit[MaxSweepHits];

            if (_environmentLayers.value == 0)
            {
                _environmentLayers = LayerMask.GetMask("Default");
            }

            if (_visualBallTransform == null)
            {
                _visualBallTransform = transform;
            }

            _visualRotation = _visualBallTransform.rotation;
            _currentState = new BallState(transform.position, Vector3.zero, Vector3.zero, Time.fixedTimeAsDouble);
            _previousState = _currentState;
        }

        private void OnEnable()
        {
            ResetToPosition(transform.position, Vector3.zero, Vector3.zero);
        }

        public void SuspendAt(Vector3 position)
        {
            _isSuspended = true;
            _currentState = new BallState(position, Vector3.zero, Vector3.zero, Time.fixedTimeAsDouble);
            _previousState = _currentState;
            transform.position = position;
            if (_visualBallTransform != null)
            {
                _visualBallTransform.position = position;
            }
        }

        public void ResetToPosition(Vector3 position, Vector3 velocity, Vector3 angularVelocity)
        {
            _isSuspended = false;
            _currentState = new BallState(position, velocity, angularVelocity, Time.fixedTimeAsDouble);
            _previousState = _currentState;
            transform.position = position;
            if (_visualBallTransform != null)
            {
                _visualBallTransform.position = position;
            }
        }

        public void ApplyImpulse(Vector3 deltaV, Vector3 deltaOmega)
        {
            _isSuspended = false;
            _currentState.Velocity += deltaV;
            _currentState.AngularVelocity += deltaOmega;
        }

        public void SetVelocity(Vector3 velocity, Vector3 angularVelocity)
        {
            _isSuspended = false;
            _currentState.Velocity = velocity;
            _currentState.AngularVelocity = angularVelocity;
        }

        private void FixedUpdate()
        {
            if (_isSuspended)
            {
                _previousState = _currentState;
                transform.position = _currentState.Position;
                if (_visualBallTransform != null)
                {
                    _visualBallTransform.position = _currentState.Position;
                }
                return;
            }

            _previousState = _currentState;
            float dt = Time.fixedDeltaTime;

            Vector3 startPos = _currentState.Position;

            // 1. Advance pure aerodynamic simulation
            BallIntegrator.Step(ref _currentState, in _profile, dt, BallIntegrator.StandardGravity);

            Vector3 endPos = _currentState.Position;
            Vector3 disp = endPos - startPos;
            float dispDist = disp.magnitude;

            // 2. Continuous environment sweep (floors, sand, net, antennas, poles)
            if (dispDist > 1e-5f)
            {
                Vector3 dispDir = disp / dispDist;
                int hitCount = UnityEngine.Physics.SphereCastNonAlloc(
                    startPos,
                    _profile.Radius,
                    dispDir,
                    _sweepHits,
                    dispDist,
                    _environmentLayers,
                    QueryTriggerInteraction.Ignore);

                if (hitCount > 0)
                {
                    // Find closest valid hit
                    int closestIdx = -1;
                    float closestDist = float.MaxValue;
                    for (int i = 0; i < hitCount; i++)
                    {
                        if (_sweepHits[i].distance < closestDist)
                        {
                            closestDist = _sweepHits[i].distance;
                            closestIdx = i;
                        }
                    }

                    if (closestIdx >= 0)
                    {
                        RaycastHit hit = _sweepHits[closestIdx];
                        ResolveEnvironmentHit(hit, startPos);
                    }
                }
            }

            // Sync root transform position to current physics state
            transform.position = _currentState.Position;

            _debugVelocity = _currentState.Velocity;
            _debugSpeed = _currentState.Velocity.magnitude;
        }

        private void ResolveEnvironmentHit(in RaycastHit hit, Vector3 startPos)
        {
            Vector3 normal = hit.normal;
            float vn = Vector3.Dot(_currentState.Velocity, normal);

            if (vn < 0f)
            {
                // Surface collision response (sand / court floor)
                ContactParams surfaceParams = _useBeachProfile ? ContactParams.SandGround : ContactParams.IndoorGround;
                ContactMath.ResolveImpulse(
                    ref _currentState.Velocity,
                    ref _currentState.AngularVelocity,
                    _profile.Mass,
                    _profile.Radius,
                    Vector3.zero,
                    normal,
                    surfaceParams);

                // Displace ball out along normal to prevent penetration
                _currentState.Position = hit.point + normal * (_profile.Radius + 0.002f);

                bool isGround = Vector3.Dot(normal, Vector3.up) > 0.6f;
                OnEnvironmentCollision?.Invoke(hit.point, normal, isGround);
            }
        }

        private void Update()
        {
            // Visual interpolation between previous and current physics steps
            // Eliminates visual stutter on high-refresh Quest headsets
            float fixedDt = Time.fixedDeltaTime;
            float alpha = fixedDt > 0f ? Mathf.Clamp01((Time.time - Time.fixedTime) / fixedDt) : 1f;

            Vector3 interpolatedPos = Vector3.Lerp(_previousState.Position, _currentState.Position, alpha);
            if (_visualBallTransform != null)
            {
                _visualBallTransform.position = interpolatedPos;

                // Visual ball spin integration
                Vector3 angularVel = _currentState.AngularVelocity;
                float spinSpeed = angularVel.magnitude;
                if (spinSpeed > 1e-4f)
                {
                    float angleDeg = spinSpeed * Mathf.Rad2Deg * Time.deltaTime;
                    Vector3 axis = angularVel / spinSpeed;
                    _visualRotation = Quaternion.AngleAxis(angleDeg, axis) * _visualRotation;
                    _visualBallTransform.rotation = _visualRotation;
                }
            }

            // Depth-cue blob shadow projection directly under the ball
            if (_blobShadowTransform != null)
            {
                float heightAboveGround = Mathf.Max(0f, interpolatedPos.y);
                _blobShadowTransform.position = new Vector3(interpolatedPos.x, 0.005f, interpolatedPos.z);

                float heightFactor = Mathf.Clamp01(1f - (heightAboveGround / _maxBlobShadowHeight));
                float shadowScale = Mathf.Lerp(0.18f, 0.40f, 1f - heightFactor);
                _blobShadowTransform.localScale = new Vector3(shadowScale, shadowScale, shadowScale);
            }
        }
    }
}
