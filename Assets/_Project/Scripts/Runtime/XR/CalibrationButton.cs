using UnityEngine;

namespace VRVolley.XR
{
    public enum CalibrationAction
    {
        PitchPlus5,
        PitchMinus5,
        YawPlus5,
        YawMinus5,
        RollPlus5,
        RollMinus5,
        ResetDefault
    }

    /// <summary>
    /// Physical 3D button on the kiosk to adjust hand angle calibration.
    /// Triggered by controller hand contact.
    /// Strictly ZERO managed allocations in per-frame loops.
    /// </summary>
    public sealed class CalibrationButton : MonoBehaviour
    {
        [SerializeField] private CalibrationAction _action;
        [SerializeField] private HandCalibrationTool _tool;
        [SerializeField] private MeshRenderer _renderer;
        [SerializeField] private Color _normalColor = new Color(0.25f, 0.55f, 0.85f);
        [SerializeField] private Color _hoverColor = new Color(0.40f, 0.80f, 1.0f);
        [SerializeField] private float _cooldown = 0.35f;

        private float _cooldownTimer;
        private MaterialPropertyBlock _propBlock;

        public void Configure(CalibrationAction action, HandCalibrationTool tool, MeshRenderer mr, Color normalColor)
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
                _tool = FindFirstObjectByType<HandCalibrationTool>();
            }
            _propBlock = new MaterialPropertyBlock();
            SetColor(_normalColor);
        }

        private void Update()
        {
            if (_cooldownTimer > 0f)
            {
                _cooldownTimer -= Time.deltaTime;
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_cooldownTimer > 0f)
            {
                return;
            }

            if (other.GetComponentInParent<TrackedVelocityEstimator>() != null ||
                other.GetComponentInParent<VRTrackedController>() != null ||
                other.CompareTag("GameController") || other.CompareTag("Player"))
            {
                Press();
            }
        }

        [ContextMenu("Press")]
        public void Press()
        {
            _cooldownTimer = _cooldown;
            SetColor(_hoverColor);

            if (_tool != null)
            {
                switch (_action)
                {
                    case CalibrationAction.PitchPlus5: _tool.AdjustPitch(5.0f); break;
                    case CalibrationAction.PitchMinus5: _tool.AdjustPitch(-5.0f); break;
                    case CalibrationAction.YawPlus5: _tool.AdjustYaw(5.0f); break;
                    case CalibrationAction.YawMinus5: _tool.AdjustYaw(-5.0f); break;
                    case CalibrationAction.RollPlus5: _tool.AdjustRoll(5.0f); break;
                    case CalibrationAction.RollMinus5: _tool.AdjustRoll(-5.0f); break;
                    case CalibrationAction.ResetDefault: _tool.ResetDefaults(); break;
                }
            }

            var haptics = FindFirstObjectByType<VRVolleyHaptics>();
            if (haptics != null)
            {
                haptics.PlayContactHaptic(true, 8.0f, 0.04f);
                haptics.PlayContactHaptic(false, 8.0f, 0.04f);
            }

            Invoke(nameof(ResetColor), 0.20f);
        }

        private void ResetColor()
        {
            SetColor(_normalColor);
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
