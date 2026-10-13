using System;
using UnityEngine;
using UnityEngine.InputSystem;
using VRVolley.XR;

namespace VRVolley.Gameplay
{
    public enum TossMode
    {
        Stabilized,
        RawPhysics
    }

    /// <summary>
    /// Handles grabbing and tossing the volleyball for serving.
    /// Supports both Stabilized (guided upward arc) and RawPhysics (pure 1:1 release velocity) modes.
    /// Zero managed allocations in per-frame loops.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public sealed class BallTossGrabbable : MonoBehaviour
    {
        [Header("Dependencies")]
        [SerializeField] private BallBody _ballBody;
        [SerializeField] private DrillSituationManager _drillManager;
        [SerializeField] private VRVolleyHaptics _haptics;
        [SerializeField] private HandBallContactSolver _solver;

        [Header("Tracked Controllers")]
        [SerializeField] private TrackedVelocityEstimator _leftEstimator;
        [SerializeField] private TrackedVelocityEstimator _rightEstimator;
        [SerializeField] private VRTrackedController _leftController;
        [SerializeField] private VRTrackedController _rightController;

        [Header("Grab Settings")]
        [SerializeField] private float _grabRadius = 0.35f;
        [SerializeField] private TossMode _tossMode = TossMode.RawPhysics;
        [SerializeField] private float _resetDelayAfterGroundHit = 2.5f;

        private bool _isGrabbed;
        private bool _grabbedByRight;
        private Vector3 _grabLocalOffset = new Vector3(0f, 0.05f, 0.12f);

        private readonly Vector3[] _velocityBuffer = new Vector3[6];
        private int _bufferHead;
        private int _bufferCount;

        private bool _wasServed;
        private float _resetTimer = -1f;

        private InputAction _leftGripAction;
        private InputAction _rightGripAction;

        public event Action<TossMode, Vector3> OnBallTossed;

        public bool IsGrabbed => _isGrabbed;

        public TossMode CurrentTossMode
        {
            get => _tossMode;
            set
            {
                _tossMode = value;
                PlayerPrefs.SetInt("VRVolley_TossMode", (int)_tossMode);
                PlayerPrefs.Save();
            }
        }

        public void Configure(
            BallBody ballBody,
            DrillSituationManager drillManager,
            VRVolleyHaptics haptics,
            TrackedVelocityEstimator leftEst,
            TrackedVelocityEstimator rightEst,
            VRTrackedController leftCtrl,
            VRTrackedController rightCtrl,
            HandBallContactSolver solver = null)
        {
            _ballBody = ballBody;
            _drillManager = drillManager;
            _haptics = haptics;
            _leftEstimator = leftEst;
            _rightEstimator = rightEst;
            _leftController = leftCtrl;
            _rightController = rightCtrl;
            if (solver != null) _solver = solver;
        }

        private void Awake()
        {
            if (_ballBody == null) _ballBody = GetComponent<BallBody>();
            if (_drillManager == null) _drillManager = FindFirstObjectByType<DrillSituationManager>();
            if (_haptics == null) _haptics = FindFirstObjectByType<VRVolleyHaptics>();
            if (_solver == null) _solver = FindFirstObjectByType<HandBallContactSolver>();

            _tossMode = (TossMode)PlayerPrefs.GetInt("VRVolley_TossMode", (int)TossMode.RawPhysics);

            _leftGripAction = new InputAction("GrabLeftGrip", InputActionType.Button, "<XRController>{LeftHand}/gripPressed");
            _rightGripAction = new InputAction("GrabRightGrip", InputActionType.Button, "<XRController>{RightHand}/gripPressed");
        }

        private void OnEnable()
        {
            if (_leftGripAction != null) _leftGripAction.Enable();
            if (_rightGripAction != null) _rightGripAction.Enable();

            if (_ballBody != null)
            {
                _ballBody.OnEnvironmentCollision += HandleBallEnvironmentCollision;
            }

            if (_solver == null) _solver = FindFirstObjectByType<HandBallContactSolver>();
            if (_solver != null)
            {
                _solver.OnBallContact += HandleBallStruck;
            }
        }

        private void OnDisable()
        {
            if (_leftGripAction != null) _leftGripAction.Disable();
            if (_rightGripAction != null) _rightGripAction.Disable();

            if (_ballBody != null)
            {
                _ballBody.OnEnvironmentCollision -= HandleBallEnvironmentCollision;
            }

            if (_solver != null)
            {
                _solver.OnBallContact -= HandleBallStruck;
            }
        }

        private void OnDestroy()
        {
            if (_leftGripAction != null) _leftGripAction.Dispose();
            if (_rightGripAction != null) _rightGripAction.Dispose();
        }

        private void Update()
        {
            if (_drillManager != null && _drillManager.ActiveSituation != DrillSituation.SelfServe)
            {
                return;
            }

            // Handle Reset Timer after serve lands
            if (_resetTimer > 0f)
            {
                _resetTimer -= Time.deltaTime;
                if (_resetTimer <= 0f)
                {
                    _resetTimer = -1f;
                    _wasServed = false;
                    _isGrabbed = false;
                    if (_drillManager != null)
                    {
                        _drillManager.SetupSelfServe();
                    }
                }
            }

            if (_isGrabbed)
            {
                TrackActiveGrab();
            }
            else if (_ballBody != null && _ballBody.IsSuspended)
            {
                CheckGrabInitiation();
            }
        }

        private void CheckGrabInitiation()
        {
            Vector3 ballPos = _ballBody.transform.position;

            // Check Left Controller
            if (_leftController != null)
            {
                float distLeft = Vector3.Distance(_leftController.transform.position, ballPos);
                if (distLeft <= _grabRadius)
                {
                    bool gripPressed = (_leftGripAction != null && _leftGripAction.IsPressed()) ||
                                       (Keyboard.current != null && Keyboard.current.gKey.isPressed);
                    if (gripPressed)
                    {
                        Grab(false);
                        return;
                    }
                }
            }

            // Check Right Controller
            if (_rightController != null)
            {
                float distRight = Vector3.Distance(_rightController.transform.position, ballPos);
                if (distRight <= _grabRadius)
                {
                    bool gripPressed = (_rightGripAction != null && _rightGripAction.IsPressed()) ||
                                       (Keyboard.current != null && Keyboard.current.hKey.isPressed);
                    if (gripPressed)
                    {
                        Grab(true);
                        return;
                    }
                }
            }
        }

        private void Grab(bool isRight)
        {
            _isGrabbed = true;
            _grabbedByRight = isRight;
            _wasServed = false;
            _resetTimer = -1f;

            _bufferHead = 0;
            _bufferCount = 0;
            for (int i = 0; i < _velocityBuffer.Length; i++)
            {
                _velocityBuffer[i] = Vector3.zero;
            }

            if (_haptics != null)
            {
                _haptics.PlayContactHaptic(isRight, 6.0f, 0.04f);
            }
        }

        private void TrackActiveGrab()
        {
            VRTrackedController ctrl = _grabbedByRight ? _rightController : _leftController;
            TrackedVelocityEstimator est = _grabbedByRight ? _rightEstimator : _leftEstimator;
            InputAction gripAction = _grabbedByRight ? _rightGripAction : _leftGripAction;

            if (ctrl == null)
            {
                _isGrabbed = false;
                return;
            }

            // Keep ball anchored to hand
            Vector3 targetPos = ctrl.transform.position + ctrl.transform.TransformDirection(_grabLocalOffset);
            _ballBody.SuspendAt(targetPos);

            // Record velocity samples in ring buffer (captures linear hand movement + wrist angular swing)
            if (est != null)
            {
                Vector3 ptVel = est.GetPointVelocity(targetPos);
                _velocityBuffer[_bufferHead] = ptVel;
                _bufferHead = (_bufferHead + 1) % _velocityBuffer.Length;
                if (_bufferCount < _velocityBuffer.Length)
                {
                    _bufferCount++;
                }
            }

            // Check Release
            bool isGripHeld = (gripAction != null && gripAction.IsPressed()) ||
                              (Keyboard.current != null && (_grabbedByRight ? Keyboard.current.hKey.isPressed : Keyboard.current.gKey.isPressed));

            if (!isGripHeld)
            {
                ReleaseToss(est, ctrl);
            }
        }

        private void ReleaseToss(TrackedVelocityEstimator est, VRTrackedController ctrl)
        {
            _isGrabbed = false;

            // Notify contact solver immediately to grant release immunity to tossing hand
            // and activate a short global contact cooldown so the ball never collides with the hand on release!
            if (_solver != null)
            {
                _solver.NotifyTossReleased(_grabbedByRight);
            }

            // Extract peak upward velocity from recent ring buffer to overcome Quest grip mechanical release latency
            Vector3 bestVel = est != null ? est.Velocity : Vector3.zero;
            if (_bufferCount > 0)
            {
                float maxVy = float.MinValue;
                for (int i = 0; i < _bufferCount; i++)
                {
                    Vector3 sample = _velocityBuffer[i];
                    if (sample.y > maxVy)
                    {
                        maxVy = sample.y;
                        bestVel = sample;
                    }
                }
            }

            Vector3 finalVel;
            if (_tossMode == TossMode.Stabilized)
            {
                // Stabilized Toss: clean upward parabolic launch with subtle forward guidance into the strike zone
                float vy = Mathf.Clamp(bestVel.y, 2.5f, 5.0f);
                float vz = Mathf.Clamp(bestVel.z, 0.3f, 1.6f);
                float vx = Mathf.Clamp(bestVel.x, -0.6f, 0.6f);
                finalVel = new Vector3(vx, vy, vz);
            }
            else
            {
                // Pure 1:1 Raw Physics Toss: exact hand velocity at release
                if (bestVel.sqrMagnitude < 0.25f)
                {
                    // Gentle default toss if released with stationary hand
                    finalVel = ctrl.transform.forward * 0.35f + Vector3.up * 2.8f;
                }
                else
                {
                    // Clamp extreme tracking spikes while honoring natural 1:1 swing physics
                    float vy = Mathf.Clamp(bestVel.y, 1.8f, 6.8f);
                    float vx = Mathf.Clamp(bestVel.x, -1.2f, 1.2f);
                    float vz = Mathf.Clamp(bestVel.z, -0.6f, 2.4f);
                    finalVel = new Vector3(vx, vy, vz);
                }
            }

            _ballBody.SetVelocity(finalVel, Vector3.zero);

            if (_haptics != null)
            {
                _haptics.PlayContactHaptic(_grabbedByRight, 8.0f, 0.05f);
            }

            OnBallTossed?.Invoke(_tossMode, finalVel);
        }

        private void HandleBallStruck(int playerId, VRVolley.Rules.TeamId teamId, ContactVolumeType contactType, float impulse)
        {
            if (_drillManager != null && _drillManager.ActiveSituation == DrillSituation.SelfServe)
            {
                _wasServed = true;
            }
        }

        private void HandleBallEnvironmentCollision(Vector3 contactPoint, Vector3 normal, bool isGround)
        {
            if (_drillManager != null && _drillManager.ActiveSituation == DrillSituation.SelfServe)
            {
                if (isGround || _wasServed)
                {
                    if (_resetTimer < 0f)
                    {
                        _resetTimer = _resetDelayAfterGroundHit;
                    }
                }
            }
        }

        public void ToggleTossMode()
        {
            CurrentTossMode = _tossMode == TossMode.Stabilized ? TossMode.RawPhysics : TossMode.Stabilized;
            if (_haptics != null)
            {
                _haptics.PlayContactHaptic(true, 10.0f, 0.05f);
            }
        }
    }
}
