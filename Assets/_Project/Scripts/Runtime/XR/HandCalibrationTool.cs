using UnityEngine;
using UnityEngine.InputSystem;

namespace VRVolley.XR
{
    /// <summary>
    /// Interactive in-game tool to adjust and calibrate the hand & forearm controller angle.
    /// Supports:
    /// 1. Grip + Thumbstick live adjustment (Hold Right Grip, move stick to tilt hands in real-time).
    /// 2. Physical 3D buttons on court kiosk.
    /// 3. PC keyboard shortcuts ([ and ] for Pitch, ; and ' for Yaw).
    /// Saves calibrated angles persistently in PlayerPrefs.
    /// Strictly ZERO managed allocations in per-frame loops.
    /// </summary>
    public sealed class HandCalibrationTool : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private VRTrackedController _leftTracker;
        [SerializeField] private VRTrackedController _rightTracker;
        [SerializeField] private TextMesh _readoutTextMesh;
        [SerializeField] private VRVolleyHaptics _haptics;

        [Header("Current Angle Offsets (Degrees)")]
        [SerializeField] private float _pitch = 31.0f;
        [SerializeField] private float _yaw = -38.0f;
        [SerializeField] private float _roll = 22.0f;

        private InputAction _rightGripAction;
        private InputAction _rightStickAction;
        private InputAction _leftStickAction;

        private float _stickCooldownTimer;
        private const float StickCooldown = 0.12f;

        private const string PrefKeyPitch = "VRVolley_HandPitch";
        private const string PrefKeyYaw = "VRVolley_HandYaw";
        private const string PrefKeyRoll = "VRVolley_HandRoll";

        public float Pitch => _pitch;
        public float Yaw => _yaw;
        public float Roll => _roll;
        public bool IsCalibrating { get; private set; }

        private void Awake()
        {
            if (_leftTracker == null || _rightTracker == null)
            {
                VRTrackedController[] trackers = FindObjectsByType<VRTrackedController>(FindObjectsSortMode.None);
                foreach (var t in trackers)
                {
                    if (t.IsRightHand) _rightTracker = t;
                    else _leftTracker = t;
                }
            }

            if (_haptics == null)
            {
                _haptics = FindFirstObjectByType<VRVolleyHaptics>();
            }

            _pitch = PlayerPrefs.GetFloat(PrefKeyPitch, 31.0f);
            _yaw = PlayerPrefs.GetFloat(PrefKeyYaw, -38.0f);
            _roll = PlayerPrefs.GetFloat(PrefKeyRoll, 22.0f);

            _rightGripAction = new InputAction("RightGrip", InputActionType.Button, "<XRController>{RightHand}/gripPressed");
            _rightStickAction = new InputAction("RightStick", InputActionType.Value, "<XRController>{RightHand}/thumbstick");
            _leftStickAction = new InputAction("LeftStick", InputActionType.Value, "<XRController>{LeftHand}/thumbstick");

            ApplyOffsets();
        }

        private void OnEnable()
        {
            if (_rightGripAction != null) _rightGripAction.Enable();
            if (_rightStickAction != null) _rightStickAction.Enable();
            if (_leftStickAction != null) _leftStickAction.Enable();
        }

        private void OnDisable()
        {
            if (_rightGripAction != null) _rightGripAction.Disable();
            if (_rightStickAction != null) _rightStickAction.Disable();
            if (_leftStickAction != null) _leftStickAction.Disable();
        }

        private void OnDestroy()
        {
            if (_rightGripAction != null) _rightGripAction.Dispose();
            if (_rightStickAction != null) _rightStickAction.Dispose();
            if (_leftStickAction != null) _leftStickAction.Dispose();
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (_stickCooldownTimer > 0f)
            {
                _stickCooldownTimer -= dt;
            }

            // Method 1: Hold Right Grip + move thumbsticks to calibrate in real-time
            bool isGripHeld = _rightGripAction != null && _rightGripAction.IsPressed();

            // Keyboard shortcut modifier (Hold G on PC)
            if (Keyboard.current != null && Keyboard.current.gKey.isPressed)
            {
                isGripHeld = true;
            }

            IsCalibrating = isGripHeld;

            if (isGripHeld && _stickCooldownTimer <= 0f)
            {
                Vector2 rStick = _rightStickAction != null ? _rightStickAction.ReadValue<Vector2>() : Vector2.zero;
                Vector2 lStick = _leftStickAction != null ? _leftStickAction.ReadValue<Vector2>() : Vector2.zero;

                bool changed = false;

                // Right Stick Up/Down -> Pitch (+/- 2 deg)
                if (rStick.y > 0.5f) { _pitch += 2.0f; changed = true; }
                else if (rStick.y < -0.5f) { _pitch -= 2.0f; changed = true; }

                // Right Stick Left/Right -> Yaw (+/- 2 deg)
                if (rStick.x > 0.5f) { _yaw += 2.0f; changed = true; }
                else if (rStick.x < -0.5f) { _yaw -= 2.0f; changed = true; }

                // Left Stick Left/Right -> Roll (+/- 2 deg)
                if (lStick.x > 0.5f) { _roll += 2.0f; changed = true; }
                else if (lStick.x < -0.5f) { _roll -= 2.0f; changed = true; }

                if (changed)
                {
                    _stickCooldownTimer = StickCooldown;
                    ApplyOffsets();
                    if (_haptics != null)
                    {
                        _haptics.PlayContactHaptic(true, 5.0f, 0.02f);
                    }
                }
            }

            // Keyboard direct adjustments
            if (Keyboard.current != null)
            {
                var kb = Keyboard.current;
                if (kb.rightBracketKey.wasPressedThisFrame) AdjustPitch(5.0f);
                if (kb.leftBracketKey.wasPressedThisFrame) AdjustPitch(-5.0f);
                if (kb.quoteKey.wasPressedThisFrame) AdjustYaw(5.0f);
                if (kb.semicolonKey.wasPressedThisFrame) AdjustYaw(-5.0f);
                if (kb.pKey.wasPressedThisFrame) ResetDefaults();
            }
        }

        public void AdjustPitch(float delta)
        {
            _pitch += delta;
            ApplyOffsets();
        }

        public void AdjustYaw(float delta)
        {
            _yaw += delta;
            ApplyOffsets();
        }

        public void AdjustRoll(float delta)
        {
            _roll += delta;
            ApplyOffsets();
        }

        public void ResetDefaults()
        {
            _pitch = 31.0f;
            _yaw = -38.0f;
            _roll = 22.0f;
            ApplyOffsets();
        }

        private void ApplyOffsets()
        {
            _pitch = Mathf.Clamp(_pitch, -80.0f, 80.0f);
            _yaw = Mathf.Clamp(_yaw, -45.0f, 45.0f);
            _roll = Mathf.Clamp(_roll, -45.0f, 45.0f);

            // Right hand uses (Pitch, Yaw, Roll)
            if (_rightTracker != null)
            {
                _rightTracker.RotationOffset = new Vector3(_pitch, _yaw, _roll);
            }

            // Left hand mirrors Yaw and Roll for symmetrical anatomy
            if (_leftTracker != null)
            {
                _leftTracker.RotationOffset = new Vector3(_pitch, -_yaw, -_roll);
            }

            PlayerPrefs.SetFloat(PrefKeyPitch, _pitch);
            PlayerPrefs.SetFloat(PrefKeyYaw, _yaw);
            PlayerPrefs.SetFloat(PrefKeyRoll, _roll);
            PlayerPrefs.Save();

            UpdateReadout();
        }

        public void SetReadoutTextMesh(TextMesh tm)
        {
            _readoutTextMesh = tm;
            UpdateReadout();
        }

        private void UpdateReadout()
        {
            if (_readoutTextMesh != null)
            {
                _readoutTextMesh.text = $"HAND ANGLE: Pitch: {_pitch:+0.0;-0.0;0.0}\u00b0 | Yaw: {_yaw:+0.0;-0.0;0.0}\u00b0 | Roll: {_roll:+0.0;-0.0;0.0}\u00b0\n(Hold Right Grip + Move Stick to Adjust)";
            }
        }
    }
}
