using UnityEngine;
using UnityEngine.InputSystem;
using VRVolley.XR;

namespace VRVolley.Gameplay
{
    /// <summary>
    /// Interactive button that switches tabs on the Kiosk.
    /// Requires Trigger press while hovering to activate.
    /// Strictly ZERO managed allocations in per-frame loops.
    /// </summary>
    public sealed class KioskTabButton : MonoBehaviour
    {
        [SerializeField] private KioskTab _targetTab;
        [SerializeField] private KioskTabController _controller;
        [SerializeField] private MeshRenderer _renderer;
        [SerializeField] private VRVolleyHaptics _haptics;

        private bool _isHovered;
        private InputAction _rightTriggerAction;
        private InputAction _leftTriggerAction;

        public void Configure(KioskTab tab, KioskTabController controller, MeshRenderer mr)
        {
            _targetTab = tab;
            _controller = controller;
            _renderer = mr;
        }

        private void Awake()
        {
            if (_controller == null)
            {
                _controller = GetComponentInParent<KioskTabController>();
            }
            if (_haptics == null)
            {
                _haptics = FindFirstObjectByType<VRVolleyHaptics>();
            }

            _rightTriggerAction = new InputAction("RightTriggerTab", InputActionType.Button, "<XRController>{RightHand}/triggerPressed");
            _leftTriggerAction = new InputAction("LeftTriggerTab", InputActionType.Button, "<XRController>{LeftHand}/triggerPressed");
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
            if (_isHovered)
            {
                bool triggerPressed = false;
                if (_rightTriggerAction != null && _rightTriggerAction.WasPressedThisFrame()) triggerPressed = true;
                if (_leftTriggerAction != null && _leftTriggerAction.WasPressedThisFrame()) triggerPressed = true;
                if (Keyboard.current != null && (Keyboard.current.spaceKey.wasPressedThisFrame || Keyboard.current.digit1Key.wasPressedThisFrame)) triggerPressed = true;
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
            }
        }

        [ContextMenu("Press")]
        public void Press()
        {
            if (_controller != null)
            {
                _controller.SetTab(_targetTab);
            }

            if (_haptics != null)
            {
                _haptics.PlayContactHaptic(true, 10.0f, 0.04f);
                _haptics.PlayContactHaptic(false, 10.0f, 0.04f);
            }
        }
    }
}
