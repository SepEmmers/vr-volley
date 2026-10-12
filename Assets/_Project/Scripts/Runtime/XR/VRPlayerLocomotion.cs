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

        [Header("References")]
        [SerializeField] private Transform _headTransform;

        private InputAction _moveAction;
        private InputAction _turnAction;

        private Vector3 _currentVelocity;
        private float _snapCooldownTimer;
        private bool _snapTurnEngaged;

        private void Awake()
        {
            if (_headTransform == null)
            {
                Camera cam = GetComponentInChildren<Camera>();
                if (cam != null)
                {
                    _headTransform = cam.transform;
                }
            }

            _moveAction = new InputAction("Move", InputActionType.Value, "<XRController>{LeftHand}/thumbstick");
            _turnAction = new InputAction("Turn", InputActionType.Value, "<XRController>{RightHand}/thumbstick");
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
