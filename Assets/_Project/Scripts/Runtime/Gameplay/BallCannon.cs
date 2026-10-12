using UnityEngine;
using VRVolley.Physics;

namespace VRVolley.Gameplay
{
    public enum DrillShotType
    {
        FloatServe,
        TopspinJumpServe,
        SpikeAtPlayer,
        SetterHighBall,
        GentleLob
    }

    /// <summary>
    /// Training ball cannon machine for solo drills (digging, float serve reception, and spike timing).
    /// Solves ballistic trajectories targeting court zones and launches the volleyball.
    /// Strictly ZERO managed allocations in per-frame loops.
    /// </summary>
    public sealed class BallCannon : MonoBehaviour
    {
        [Header("Targeting & Physics")]
        [SerializeField] private BallBody _ballBody;
        [SerializeField] private Transform _targetZone;
        [SerializeField] private DrillShotType _shotType = DrillShotType.FloatServe;

        [Header("Cadence & Automation")]
        [SerializeField] private bool _autoFire = false;
        [SerializeField] private float _fireInterval = 5.0f;

        private float _fireTimer;

        private void Awake()
        {
            if (_ballBody == null)
            {
                _ballBody = FindFirstObjectByType<BallBody>();
            }
        }

        private void Update()
        {
            if (!_autoFire)
            {
                return;
            }

            _fireTimer += Time.deltaTime;
            if (_fireTimer >= _fireInterval)
            {
                _fireTimer = 0f;
                FireDrillBall();
            }
        }

        [ContextMenu("Fire Drill Ball")]
        public void FireDrillBall()
        {
            if (_ballBody == null)
            {
                return;
            }

            Vector3 startPos = transform.position;
            Vector3 targetPos = _targetZone != null ? _targetZone.position : new Vector3(0f, 0.5f, -5.0f);

            Vector3 initialVelocity;
            Vector3 initialSpin;

            switch (_shotType)
            {
                case DrillShotType.FloatServe:
                    // 16 m/s float serve with low spin (~1 rad/s) for maximum knuckle wobble
                    initialVelocity = CalculateLaunchVelocity(startPos, targetPos, 1.1f);
                    initialSpin = new Vector3(0f, 1.5f, 0f); // very low spin
                    break;

                case DrillShotType.TopspinJumpServe:
                    // 25 m/s aggressive topspin dive: heavy +X spin relative to forward direction
                    initialVelocity = CalculateLaunchVelocity(startPos, targetPos, 0.75f);
                    Vector3 forwardDir = (targetPos - startPos).normalized;
                    Vector3 rightDir = Vector3.Cross(Vector3.up, forwardDir).normalized;
                    initialSpin = rightDir * 55.0f; // 55 rad/s topspin
                    break;

                case DrillShotType.SpikeAtPlayer:
                    // Fast downward-angled attack (0.6s flight)
                    initialVelocity = CalculateLaunchVelocity(startPos, targetPos, 0.6f);
                    initialSpin = Vector3.right * 40.0f;
                    break;

                case DrillShotType.SetterHighBall:
                    // High arched setter ball near the net for spike approach training
                    initialVelocity = CalculateLaunchVelocity(startPos, targetPos, 1.8f);
                    initialSpin = Vector3.zero;
                    break;

                case DrillShotType.GentleLob:
                default:
                    // Easy arc for platform bump warmup
                    initialVelocity = CalculateLaunchVelocity(startPos, targetPos, 2.0f);
                    initialSpin = Vector3.zero;
                    break;
            }

            _ballBody.ResetToPosition(startPos, initialVelocity, initialSpin);
        }

        /// <summary>
        /// Solves the required launch velocity to reach targetPos in durationSeconds under gravity.
        /// (v0 = (target - start)/t - 0.5 * g * t)
        /// </summary>
        public static Vector3 CalculateLaunchVelocity(Vector3 start, Vector3 target, float durationSeconds, float dt = 0f)
        {
            if (durationSeconds <= 0.05f)
            {
                durationSeconds = 0.05f;
            }

            Vector3 displacement = target - start;
            Vector3 gravity = BallIntegrator.StandardGravity;
            // Accounts for discrete semi-implicit Euler integration: x(t) = v0*t + 0.5*g*t*(t + dt)
            Vector3 v0 = (displacement / durationSeconds) - (0.5f * gravity * (durationSeconds + dt));
            return v0;
        }
    }
}
