using UnityEngine;
using UnityEngine.InputSystem;

namespace VRVolley.XR
{
    /// <summary>
    /// Smooth thumbstick locomotion and snap turning using Unity's New Input System.
    /// Reads <XRController>{LeftHand}/thumbstick for movement and <XRController>{RightHand}/thumbstick for snap turning.
    /// Includes athletic acceleration/deceleration damping and keyboard fallback (WASD / Q/E).
    /// Strictly ZERO managed allocations in per-frame loops.
    /// </summary>
    public sealed class VRPlayerLocomotion : MonoBehaviour
    {
        [Header("Movement Settings")]
        [SerializeField] private float _maxSpeed = 3.8f;
        [SerializeField] private float _acceleration = 12.0f;
        [SerializeField] private float _deceleration = 16.0f;
        [SerializeField] private float _deadzone = 0.15f;

        [Header("Snap Turn Settings")]
        [SerializeField] private float _snapTurnAngle = 45.0f;
        [SerializeField] private float _snapThreshold = 0.65f;
        [SerializeField] private float _snapCooldown = 0.30f;

        [Header("Jump Settings")]
        [SerializeField] private float _jumpVelocity = 3.8f;
        [SerializeField] private float _gravity = -11.0f;
        [SerializeField] private float _armSwingThreshold = 1.7f;

        [Header("References")]
        [SerializeField] private Transform _headTransform;
        [SerializeField] private TrackedVelocityEstimator _leftEstimator;
        [SerializeField] private TrackedVelocityEstimator _rightEstimator;
        [SerializeField] private VRVolleyHaptics _haptics;

        private InputAction _moveAction;
        private InputAction _turnAction;

        private const string PrefKeyJumpThreshold = "VRVolley_JumpSwingThreshold";

        public float ArmSwingThreshold
        {
            get => _armSwingThreshold;
            set
            {
                _armSwingThreshold = Mathf.Clamp(value, 0.8f, 3.0f);
                PlayerPrefs.SetFloat(PrefKeyJumpThreshold, _armSwingThreshold);
                PlayerPrefs.Save();
            }
        }

        private Vector3 _currentVelocity;
        private float _snapCooldownTimer;
        private bool _snapTurnEngaged;
        private HandCalibrationTool _calibrationTool;

        private bool _isGrounded = true;
        private float _verticalVelocity;
        private float _baseFloorY;

        public bool IsGrounded => _isGrounded;
        public float JumpVelocity => _verticalVelocity;

        public void ConfigureEstimators(TrackedVelocityEstimator left, TrackedVelocityEstimator right, VRVolleyHaptics haptics)
        {
            _leftEstimator = left;
            _rightEstimator = right;
            _haptics = haptics;
        }

        private void Awake()
        {
            _baseFloorY = transform.position.y;

            if (_headTransform == null)
            {
                Camera cam = GetComponentInChildren<Camera>();
                if (cam != null)
                {
                    _headTransform = cam.transform;
                }
            }

            _calibrationTool = GetComponent<HandCalibrationTool>();
            if (_haptics == null) _haptics = GetComponent<VRVolleyHaptics>();

            _armSwingThreshold = PlayerPrefs.GetFloat(PrefKeyJumpThreshold, 1.7f);

            _moveAction = new InputAction("Move", InputActionType.Value, "<XRController>{LeftHand}/thumbstick");
            _turnAction = new InputAction("Turn", InputActionType.Value, "<XRController>{RightHand}/thumbstick");
            // Jump is triggered strictly by physical dual arm swing (no controller button mapped per player design)
        }

        private void OnEnable()
        {
            if (_moveAction != null) _moveAction.Enable();
            if (_turnAction != null) _turnAction.Enable();
        }

        private void OnDisable()
        {
            if (_moveAction != null) _moveAction.Disable();
            if (_turnAction != null) _turnAction.Disable();
        }

        private void OnDestroy()
        {
            if (_moveAction != null) _moveAction.Dispose();
            if (_turnAction != null) _turnAction.Dispose();
        }

        private void Update()
        {
            float dt = Time.deltaTime;

            if (_calibrationTool != null && _calibrationTool.IsCalibrating)
            {
                _currentVelocity = Vector3.MoveTowards(_currentVelocity, Vector3.zero, _deceleration * dt);
                return;
            }

            if (_snapCooldownTimer > 0f)
            {
                _snapCooldownTimer -= dt;
            }

            // 1. Read Movement Input
            Vector2 input = Vector2.zero;
            if (_moveAction != null && _moveAction.enabled)
            {
                input = _moveAction.ReadValue<Vector2>();
            }

            // Keyboard WASD Fallback
            if (Keyboard.current != null)
            {
                var kb = Keyboard.current;
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) input.y += 1.0f;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) input.y -= 1.0f;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) input.x += 1.0f;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) input.x -= 1.0f;
            }

            // Apply deadzone
            if (input.sqrMagnitude < _deadzone * _deadzone)
            {
                input = Vector2.zero;
            }
            else if (input.sqrMagnitude > 1.0f)
            {
                input.Normalize();
            }

            // Target world velocity relative to headset view
            Vector3 forward = _headTransform != null ? _headTransform.forward : transform.forward;
            forward.y = 0f;
            forward.Normalize();

            Vector3 right = _headTransform != null ? _headTransform.right : transform.right;
            right.y = 0f;
            right.Normalize();

            Vector3 targetVelocity = (forward * input.y + right * input.x) * _maxSpeed;

            // Smooth athletic acceleration / deceleration
            float rate = targetVelocity.sqrMagnitude > 0.01f ? _acceleration : _deceleration;
            _currentVelocity = Vector3.MoveTowards(_currentVelocity, targetVelocity, rate * dt);

            if (_currentVelocity.sqrMagnitude > 1e-4f)
            {
                transform.position += _currentVelocity * dt;
            }

            // 2. Jump Handling (Arm swing upward OR Button/Space)
            if (_isGrounded)
            {
                bool armsSwungUp = false;
                if (_leftEstimator != null && _rightEstimator != null)
                {
                    float vyL = _leftEstimator.Velocity.y;
                    float vyR = _rightEstimator.Velocity.y;
                    if (vyL > _armSwingThreshold && vyR > _armSwingThreshold)
                    {
                        armsSwungUp = true;
                    }
                }

                bool buttonPressed = (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame);

                if (armsSwungUp || buttonPressed)
                {
                    _isGrounded = false;
                    float armBonus = armsSwungUp ? Mathf.Clamp01((_leftEstimator.Velocity.y + _rightEstimator.Velocity.y - (_armSwingThreshold * 2f)) * 0.15f) : 0f;
                    _verticalVelocity = _jumpVelocity * (1.0f + armBonus);
                    if (_haptics != null)
                    {
                        _haptics.PlayContactHaptic(true, 12.0f, 0.05f);
                        _haptics.PlayContactHaptic(false, 12.0f, 0.05f);
                    }
                }
            }
            else
            {
                _verticalVelocity += _gravity * dt;
                Vector3 pos = transform.position;
                pos.y += _verticalVelocity * dt;
                if (pos.y <= _baseFloorY)
                {
                    pos.y = _baseFloorY;
                    _verticalVelocity = 0f;
                    _isGrounded = true;
                    if (_haptics != null)
                    {
                        _haptics.PlayContactHaptic(true, 8.0f, 0.04f);
                        _haptics.PlayContactHaptic(false, 8.0f, 0.04f);
                    }
                }
                transform.position = pos;
            }

            // 2. Read Turn Input
            float turnInput = 0f;
            if (_turnAction != null && _turnAction.enabled)
            {
                turnInput = _turnAction.ReadValue<Vector2>().x;
            }

            // Keyboard Q/E Fallback
            if (Keyboard.current != null)
            {
                var kb = Keyboard.current;
                if (kb.eKey.wasPressedThisFrame) turnInput += 1.0f;
                if (kb.qKey.wasPressedThisFrame) turnInput -= 1.0f;
            }

            HandleSnapTurn(turnInput);
        }

        private void HandleSnapTurn(float turnInput)
        {
            if (Mathf.Abs(turnInput) < _snapThreshold)
            {
                _snapTurnEngaged = false;
                return;
            }

            if (_snapTurnEngaged || _snapCooldownTimer > 0f)
            {
                return;
            }

            float sign = Mathf.Sign(turnInput);
            float angle = sign * _snapTurnAngle;

            Vector3 pivot = _headTransform != null ? _headTransform.position : transform.position;
            pivot.y = transform.position.y;

            transform.RotateAround(pivot, Vector3.up, angle);

            _snapTurnEngaged = true;
            _snapCooldownTimer = _snapCooldown;
        }
    }
}
