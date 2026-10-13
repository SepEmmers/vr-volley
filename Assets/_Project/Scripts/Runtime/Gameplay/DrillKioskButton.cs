using UnityEngine;
using UnityEngine.InputSystem;
using VRVolley.XR;

namespace VRVolley.Gameplay
{
    /// <summary>
    /// Physical 3D button placed on court-side kiosk in VR.
    /// Can be tapped with controller hands or ray interactor to trigger a drill situation.
    /// Strictly ZERO managed allocations in per-frame loops.
    /// </summary>
    public sealed class DrillKioskButton : MonoBehaviour
    {
        [SerializeField] private DrillSituation _situation;
        [SerializeField] private bool _isRepeatButton;
        [SerializeField] private DrillSituationManager _manager;
        [SerializeField] private MeshRenderer _buttonRenderer;
        [SerializeField] private Color _normalColor = new Color(0.18f, 0.55f, 0.95f);
        [SerializeField] private Color _hoverColor = new Color(0.35f, 0.75f, 1.0f);
        [SerializeField] private float _cooldown = 0.5f;

        private float _cooldownTimer;
        private MaterialPropertyBlock _propBlock;
        private InputAction _rightTriggerAction;
        private InputAction _leftTriggerAction;
        private bool _isHovered;
        private VRVolleyHaptics _haptics;

        public void Configure(DrillSituation situation, DrillSituationManager manager, MeshRenderer renderer, Color normalColor)
        {
            _situation = situation;
            _isRepeatButton = false;
            _manager = manager;
            _buttonRenderer = renderer;
            _normalColor = normalColor;
        }

        public void ConfigureRepeat(DrillSituationManager manager, MeshRenderer renderer, Color normalColor)
        {
            _isRepeatButton = true;
            _manager = manager;
            _buttonRenderer = renderer;
            _normalColor = normalColor;
        }

        private void Awake()
        {
            if (_manager == null)
            {
                _manager = FindFirstObjectByType<DrillSituationManager>();
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
                    PressButton();
                }
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.GetComponentInParent<TrackedVelocityEstimator>() != null ||
                other.GetComponentInParent<VRTrackedController>() != null ||
                other.GetComponentInParent<BodyContactVolumes>() != null ||
                other.CompareTag("Player") || other.CompareTag("GameController"))
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
                other.GetComponentInParent<BodyContactVolumes>() != null ||
                other.CompareTag("Player") || other.CompareTag("GameController"))
            {
                _isHovered = false;
                SetColor(_normalColor);
            }
        }

        [ContextMenu("Press Button")]
        public void PressButton()
        {
            _cooldownTimer = _cooldown;
            SetColor(Color.white);

            if (_manager != null)
            {
                if (_isRepeatButton)
                {
                    _manager.RepeatActiveSituation();
                }
                else
                {
                    _manager.TriggerSituation(_situation);
                }
            }

            if (_haptics != null)
            {
                _haptics.PlayContactHaptic(true, 12.0f, 0.06f);
                _haptics.PlayContactHaptic(false, 12.0f, 0.06f);
            }

            Invoke(nameof(ResetColorAfterPress), 0.20f);
        }

        private void ResetColorAfterPress()
        {
            SetColor(_isHovered ? _hoverColor : _normalColor);
        }

        private void SetColor(Color col)
        {
            if (_buttonRenderer != null)
            {
                _buttonRenderer.GetPropertyBlock(_propBlock);
                _propBlock.SetColor("_BaseColor", col);
                _propBlock.SetColor("_Color", col);
                _buttonRenderer.SetPropertyBlock(_propBlock);
            }
        }
    }
}
