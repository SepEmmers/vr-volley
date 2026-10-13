using UnityEngine;
using UnityEngine.InputSystem;
using VRVolley.XR;

namespace VRVolley.Gameplay
{
    public enum ReceiveBounceAction
    {
        BouncePlus10,
        BounceMinus10,
        BouncePlus1,
        BounceMinus1,
        PresetSoft,
        PresetCrisp,
        PresetMaxPop,
        ResetDefault,
        ToggleOneHandMode,
        ToggleTossMode,
        SpikePowerPlus,
        SpikePowerMinus,
        SpikePowerReset,
        JumpSensitivityPlus,
        JumpSensitivityMinus,
        JumpSensitivityReset
    }

    /// <summary>
    /// Physical 3D button on the kiosk to adjust receive bounciness.
    /// Highlights on hover and requires the controller Trigger to be pulled to activate.
    /// Strictly ZERO managed allocations in per-frame loops.
    /// </summary>
    public sealed class ReceiveBounceButton : MonoBehaviour
    {
        [SerializeField] private ReceiveBounceAction _action;
        [SerializeField] private ReceiveBounceCalibrationTool _tool;
        [SerializeField] private MeshRenderer _renderer;
        [SerializeField] private Color _normalColor = new Color(0.20f, 0.70f, 0.65f);
        [SerializeField] private Color _hoverColor = new Color(0.40f, 0.95f, 0.85f);
        [SerializeField] private float _cooldown = 0.25f;

        private float _cooldownTimer;
        private bool _isHovered;
        private MaterialPropertyBlock _propBlock;
        private InputAction _rightTriggerAction;
        private InputAction _leftTriggerAction;
        private VRVolleyHaptics _haptics;

        public void Configure(ReceiveBounceAction action, ReceiveBounceCalibrationTool tool, MeshRenderer mr, Color normalColor)
        {
            _action = action;
            _tool = tool;
            _renderer = mr;
            _normalColor = normalColor;
        }

        private void Awake()
        {
            if (_tool == null)
            {
                _tool = FindFirstObjectByType<ReceiveBounceCalibrationTool>();
            }
            if (_haptics == null)
            {
                _haptics = FindFirstObjectByType<VRVolleyHaptics>();
            }

            _propBlock = new MaterialPropertyBlock();
            SetColor(_normalColor);

            _rightTriggerAction = new InputAction("RightTrigger", InputActionType.Button, "<XRController>{RightHand}/triggerPressed");
            _leftTriggerAction = new InputAction("LeftTrigger", InputActionType.Button, "<XRController>{LeftHand}/triggerPressed");
        }

        private void OnEnable()
        {
            if (_rightTriggerAction != null) _rightTriggerAction.Enable();
            if (_leftTriggerAction != null) _leftTriggerAction.Enable();
        }

        private void OnDisable()
        {
            if (_rightTriggerAction != null) _rightTriggerAction.Disable();
            if (_leftTriggerAction != null) _leftTriggerAction.Disable();
            _isHovered = false;
        }

        private void OnDestroy()
        {
            if (_rightTriggerAction != null) _rightTriggerAction.Dispose();
            if (_leftTriggerAction != null) _leftTriggerAction.Dispose();
        }

        private void Update()
        {
            if (_cooldownTimer > 0f)
            {
                _cooldownTimer -= Time.deltaTime;
            }

            if (_isHovered && _cooldownTimer <= 0f)
            {
                bool triggerPressed = false;
                if (_rightTriggerAction != null && _rightTriggerAction.WasPressedThisFrame()) triggerPressed = true;
                if (_leftTriggerAction != null && _leftTriggerAction.WasPressedThisFrame()) triggerPressed = true;
                if (Keyboard.current != null && (Keyboard.current.spaceKey.wasPressedThisFrame || Keyboard.current.enterKey.wasPressedThisFrame)) triggerPressed = true;
                if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) triggerPressed = true;

                if (triggerPressed)
                {
                    Press();
                }
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.GetComponentInParent<TrackedVelocityEstimator>() != null ||
                other.GetComponentInParent<VRTrackedController>() != null ||
                other.CompareTag("GameController") || other.CompareTag("Player"))
            {
                _isHovered = true;
                SetColor(_hoverColor);
                if (_haptics != null)
                {
                    _haptics.PlayContactHaptic(true, 3.0f, 0.02f);
                    _haptics.PlayContactHaptic(false, 3.0f, 0.02f);
                }
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.GetComponentInParent<TrackedVelocityEstimator>() != null ||
                other.GetComponentInParent<VRTrackedController>() != null ||
                other.CompareTag("GameController") || other.CompareTag("Player"))
            {
                _isHovered = false;
                SetColor(_normalColor);
            }
        }

        [ContextMenu("Press")]
        public void Press()
        {
            _cooldownTimer = _cooldown;
            SetColor(Color.white);

            if (_tool != null)
            {
                switch (_action)
                {
                    case ReceiveBounceAction.BouncePlus10: _tool.AdjustBounce(0.10f); break;
                    case ReceiveBounceAction.BounceMinus10: _tool.AdjustBounce(-0.10f); break;
                    case ReceiveBounceAction.BouncePlus1: _tool.AdjustBounce(0.01f); break;
                    case ReceiveBounceAction.BounceMinus1: _tool.AdjustBounce(-0.01f); break;
                    case ReceiveBounceAction.PresetSoft: _tool.SetBouncePreset(0.75f); break;
                    case ReceiveBounceAction.PresetCrisp: _tool.SetBouncePreset(1.00f); break;
                    case ReceiveBounceAction.PresetMaxPop: _tool.SetBouncePreset(1.25f); break;
                    case ReceiveBounceAction.ResetDefault: _tool.ResetDefault(); break;
                    case ReceiveBounceAction.ToggleOneHandMode: _tool.ToggleOneHandMode(); break;
                    case ReceiveBounceAction.ToggleTossMode: _tool.ToggleTossMode(); break;
                    case ReceiveBounceAction.SpikePowerPlus: _tool.AdjustSpikePower(0.1f); break;
                    case ReceiveBounceAction.SpikePowerMinus: _tool.AdjustSpikePower(-0.1f); break;
                    case ReceiveBounceAction.SpikePowerReset: _tool.ResetSpikePower(); break;
                    case ReceiveBounceAction.JumpSensitivityPlus: _tool.AdjustJumpSensitivity(0.1f); break;
                    case ReceiveBounceAction.JumpSensitivityMinus: _tool.AdjustJumpSensitivity(-0.1f); break;
                    case ReceiveBounceAction.JumpSensitivityReset: _tool.ResetJumpSensitivity(); break;
                }
            }

            if (_haptics != null)
            {
                _haptics.PlayContactHaptic(true, 12.0f, 0.05f);
                _haptics.PlayContactHaptic(false, 12.0f, 0.05f);
            }

            Invoke(nameof(ResetColorAfterPress), 0.18f);
        }

        private void ResetColorAfterPress()
        {
            SetColor(_isHovered ? _hoverColor : _normalColor);
        }

        private void SetColor(Color col)
        {
            if (_renderer != null)
            {
                _renderer.GetPropertyBlock(_propBlock);
                _propBlock.SetColor("_BaseColor", col);
                _propBlock.SetColor("_Color", col);
                _renderer.SetPropertyBlock(_propBlock);
            }
        }
    }
}
