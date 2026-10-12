using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

namespace VRVolley.XR
{
    /// <summary>
    /// Smooth thumbstick locomotion and snap turning for VR Origin on the volleyball court.
    /// Supports Meta Quest Touch controllers (Left stick: move, Right stick: snap turn)
    /// plus PC keyboard fallback (WASD / Arrows to move, Q/E to snap turn).
    /// Strictly ZERO managed allocations in per-frame loops.
    /// </summary>
    public sealed class VRPlayerLocomotion : MonoBehaviour
    {
        [Header("Movement Settings")]
        [Tooltip("Max movement speed in meters per second.")]
        [SerializeField] private float _moveSpeed = 3.8f;
        [Tooltip("Deadzone radius for thumbstick input.")]
        [SerializeField] private float _deadzone = 0.18f;

        [Header("Snap Turn Settings")]
        [Tooltip("Degrees rotated per snap turn flick.")]
        [SerializeField] private float _snapTurnAngle = 45.0f;
        [Tooltip("Threshold on right thumbstick X axis to trigger snap turn.")]
        [SerializeField] private float _snapThreshold = 0.65f;
        [Tooltip("Minimum time (seconds) between snap turns.")]
        [SerializeField] private float _snapCooldown = 0.30f;

        [Header("References")]
        [SerializeField] private Transform _headTransform;

        private InputDevice _leftController;
        private InputDevice _rightController;
        private readonly List<InputDevice> _deviceBuffer = new List<InputDevice>(4);

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

            RefreshDevices();
        }

        private void OnEnable()
        {
            InputDevices.deviceConnected += OnDeviceConnected;
        }

        private void OnDisable()
        {
            InputDevices.deviceConnected -= OnDeviceConnected;
        }

        private void OnDeviceConnected(InputDevice dev)
        {
            RefreshDevices();
        }

        private void RefreshDevices()
        {
            _deviceBuffer.Clear();
            InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.Controller | InputDeviceCharacteristics.Left, _deviceBuffer);
            if (_deviceBuffer.Count > 0)
            {
                _leftController = _deviceBuffer[0];
            }

            _deviceBuffer.Clear();
            InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.Controller | InputDeviceCharacteristics.Right, _deviceBuffer);
            if (_deviceBuffer.Count > 0)
            {
                _rightController = _deviceBuffer[0];
            }
        }

        private void Update()
        {
            float dt = Time.deltaTime;

            if (_snapCooldownTimer > 0f)
            {
                _snapCooldownTimer -= dt;
            }

            // 1. Read Movement Input (Left Stick or Keyboard WASD)
            Vector2 moveInput = ReadMoveInput();

            if (moveInput.sqrMagnitude > _deadzone * _deadzone)
            {
                ApplyMovement(moveInput, dt);
            }

            // 2. Read Turn Input (Right Stick or Keyboard Q/E)
            float turnInput = ReadTurnInput();
            HandleSnapTurn(turnInput);
        }

        private Vector2 ReadMoveInput()
        {
            Vector2 input = Vector2.zero;

            // VR Left Controller Thumbstick
            if (_leftController.isValid && _leftController.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 stick))
            {
                input = stick;
            }

            // Keyboard WASD Fallback
            if (UnityEngine.InputSystem.Keyboard.current != null)
            {
                var kb = UnityEngine.InputSystem.Keyboard.current;
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) input.y += 1.0f;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) input.y -= 1.0f;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) input.x += 1.0f;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) input.x -= 1.0f;
            }

            if (input.sqrMagnitude > 1.0f)
            {
                input.Normalize();
            }

            return input;
        }

        private float ReadTurnInput()
        {
            float turn = 0f;

            // VR Right Controller Thumbstick X
            if (_rightController.isValid && _rightController.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 stick))
            {
                turn = stick.x;
            }

            // Keyboard Q/E Fallback
            if (UnityEngine.InputSystem.Keyboard.current != null)
            {
                var kb = UnityEngine.InputSystem.Keyboard.current;
                if (kb.eKey.wasPressedThisFrame) turn += 1.0f;
                if (kb.qKey.wasPressedThisFrame) turn -= 1.0f;
            }

            return turn;
        }

        private void ApplyMovement(Vector2 moveInput, float dt)
        {
            // Move relative to horizontal forward direction of the head/camera
            Vector3 forward = _headTransform != null ? _headTransform.forward : transform.forward;
            forward.y = 0f;
            forward.Normalize();

            Vector3 right = _headTransform != null ? _headTransform.right : transform.right;
            right.y = 0f;
            right.Normalize();

            Vector3 moveDir = (forward * moveInput.y + right * moveInput.x);
            transform.position += moveDir * (_moveSpeed * dt);
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

            // Rotate XR Origin around the head position on the floor
            Vector3 pivot = _headTransform != null ? _headTransform.position : transform.position;
            pivot.y = transform.position.y;

            transform.RotateAround(pivot, Vector3.up, angle);

            _snapTurnEngaged = true;
            _snapCooldownTimer = _snapCooldown;
        }
    }
}
