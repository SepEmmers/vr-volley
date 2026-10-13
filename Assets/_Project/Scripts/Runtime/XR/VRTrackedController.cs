using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;

namespace VRVolley.XR
{
    /// <summary>
    /// Dual-pipeline 6DOF tracked controller for Meta Quest Touch and Touch Plus controllers.
    /// Tracks via Unity New Input System (<XRController>/devicePosition) with automatic InputDevices fallback.
    /// Updates in Update(), FixedUpdate() (for kinematic contact physics), and OnBeforeRender (for VR refresh rate).
    /// Strictly ZERO managed allocations in per-frame loops.
    /// </summary>
    [DefaultExecutionOrder(-200)]
    public sealed class VRTrackedController : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private bool _isRightHand;
        [SerializeField] private GameObject _visualRoot;
        [SerializeField] private GameObject _receivingVisualRoot;
        [SerializeField] private GameObject _pointerVisual;
        [SerializeField] private GameObject _claspedVisual;
        [SerializeField] private BodyContactVolumes _contactVolumes;

        [Header("Calibration Offsets")]
        [SerializeField] private Vector3 _rotationOffset = new Vector3(31.0f, -38.0f, 22.0f);
        [SerializeField] private Vector3 _positionOffset = Vector3.zero;

        private InputAction _posAction;
        private InputAction _rotAction;
        private InputAction _gripAction;
        private UnityEngine.XR.InputDevice _xrDevice;
        private bool _hasReceivedPose;
        private bool _isGripPressed;

        public bool IsRightHand => _isRightHand;
        public bool IsTracked => _hasReceivedPose;
        public bool IsGripPressed => _isGripPressed;

        public Vector3 RotationOffset
        {
            get => _rotationOffset;
            set => _rotationOffset = value;
        }

        public Vector3 PositionOffset
        {
            get => _positionOffset;
            set => _positionOffset = value;
        }

        public void Configure(
            bool isRightHand,
            GameObject receivingVisualRoot,
            GameObject pointerVisual,
            GameObject claspedVisual = null,
            BodyContactVolumes contactVolumes = null)
        {
            _isRightHand = isRightHand;
            _receivingVisualRoot = receivingVisualRoot;
            _pointerVisual = pointerVisual;
            _claspedVisual = claspedVisual;
            _contactVolumes = contactVolumes;
            _rotationOffset = isRightHand ? new Vector3(31.0f, -38.0f, 22.0f) : new Vector3(31.0f, 38.0f, -22.0f);
        }

        private void Awake()
        {
            string handPath = _isRightHand ? "<XRController>{RightHand}" : "<XRController>{LeftHand}";
            _posAction = new InputAction(_isRightHand ? "RightPos" : "LeftPos", InputActionType.Value, $"{handPath}/devicePosition");
            _rotAction = new InputAction(_isRightHand ? "RightRot" : "LeftRot", InputActionType.Value, $"{handPath}/deviceRotation");
            _gripAction = new InputAction(_isRightHand ? "RightGrip" : "LeftGrip", InputActionType.Button, $"{handPath}/gripPressed");

            if (!_isRightHand && _rotationOffset.y < 0f)
            {
                _rotationOffset = new Vector3(_rotationOffset.x, -_rotationOffset.y, -_rotationOffset.z);
            }

            if (_contactVolumes == null)
            {
                _contactVolumes = FindFirstObjectByType<BodyContactVolumes>();
            }

            RefreshXRDevice();
        }

        private void OnEnable()
        {
            if (_posAction != null) _posAction.Enable();
            if (_rotAction != null) _rotAction.Enable();
            if (_gripAction != null) _gripAction.Enable();

            Application.onBeforeRender += OnBeforeRender;
            UnityEngine.XR.InputDevices.deviceConnected += OnDeviceConnected;
        }

        private void OnDisable()
        {
            if (_posAction != null) _posAction.Disable();
            if (_rotAction != null) _rotAction.Disable();
            if (_gripAction != null) _gripAction.Disable();

            Application.onBeforeRender -= OnBeforeRender;
            UnityEngine.XR.InputDevices.deviceConnected -= OnDeviceConnected;
        }

        private void OnDestroy()
        {
            if (_posAction != null) _posAction.Dispose();
            if (_rotAction != null) _rotAction.Dispose();
            if (_gripAction != null) _gripAction.Dispose();
        }

        private void OnDeviceConnected(UnityEngine.XR.InputDevice dev)
        {
            RefreshXRDevice();
        }

        private void RefreshXRDevice()
        {
            _xrDevice = UnityEngine.XR.InputDevices.GetDeviceAtXRNode(
                _isRightHand ? UnityEngine.XR.XRNode.RightHand : UnityEngine.XR.XRNode.LeftHand);
        }

        private void Update()
        {
            UpdatePose();
        }

        private void FixedUpdate()
        {
            UpdatePose();
        }

        private void OnBeforeRender()
        {
            UpdatePose();
        }

        private void UpdatePose()
        {
            bool poseUpdated = false;

            // Pipeline 1: New Input System Actions
            if (_posAction != null && _posAction.enabled)
            {
                Vector3 pos = _posAction.ReadValue<Vector3>();
                Quaternion rot = _rotAction != null && _rotAction.enabled ? _rotAction.ReadValue<Quaternion>() : Quaternion.identity;

                if (pos.sqrMagnitude > 1e-6f || Mathf.Abs(rot.w) > 0.01f || Mathf.Abs(rot.x) > 0.01f)
                {
                    float qNormSq = rot.w * rot.w + rot.x * rot.x + rot.y * rot.y + rot.z * rot.z;
                    if (Mathf.Abs(qNormSq - 1.0f) < 0.15f)
                    {
                        transform.localRotation = rot * Quaternion.Euler(_rotationOffset);
                    }
                    transform.localPosition = pos + rot * _positionOffset;
                    poseUpdated = true;
                }
            }

            // Pipeline 2: InputDevices Fallback
            if (!poseUpdated)
            {
                if (!_xrDevice.isValid)
                {
                    RefreshXRDevice();
                }

                if (_xrDevice.isValid)
                {
                    if (_xrDevice.TryGetFeatureValue(UnityEngine.XR.CommonUsages.deviceRotation, out Quaternion xrRot))
                    {
                        transform.localRotation = xrRot * Quaternion.Euler(_rotationOffset);
                        poseUpdated = true;
                    }
                    if (_xrDevice.TryGetFeatureValue(UnityEngine.XR.CommonUsages.devicePosition, out Vector3 xrPos))
                    {
                        transform.localPosition = xrPos + (poseUpdated ? transform.localRotation : Quaternion.identity) * _positionOffset;
                        poseUpdated = true;
                    }
                }
            }

            if (poseUpdated)
            {
                _hasReceivedPose = true;
            }

            // Check if Grip is pressed for receiving mode
            bool isGripHeld = false;
            if (_gripAction != null && _gripAction.enabled)
            {
                isGripHeld = _gripAction.IsPressed();
            }
            if (!isGripHeld && _xrDevice.isValid)
            {
                if (_xrDevice.TryGetFeatureValue(UnityEngine.XR.CommonUsages.gripButton, out bool gripVal))
                {
                    isGripHeld = gripVal;
                }
            }
            if (Keyboard.current != null && Keyboard.current.gKey.isPressed)
            {
                isGripHeld = true;
            }

            _isGripPressed = isGripHeld;

            // Only show receiving hands/forearm when receiving (Grip held)
            if (_receivingVisualRoot != null)
            {
                bool showReceiving = _hasReceivedPose && isGripHeld;
                if (_receivingVisualRoot.activeSelf != showReceiving)
                {
                    _receivingVisualRoot.SetActive(showReceiving);
                }
            }

            if (_claspedVisual != null)
            {
                bool showClasped = _hasReceivedPose && isGripHeld && (_contactVolumes == null || _contactVolumes.IsOneHandMode);
                if (_claspedVisual.activeSelf != showClasped)
                {
                    _claspedVisual.SetActive(showClasped);
                }
            }

            // Fingertip pointer remains visible when tracked to allow interacting with the kiosk
            if (_pointerVisual != null)
            {
                if (_pointerVisual.activeSelf != _hasReceivedPose)
                {
                    _pointerVisual.SetActive(_hasReceivedPose);
                }
            }

            if (_visualRoot != null)
            {
                if (_visualRoot.activeSelf != _hasReceivedPose)
                {
                    _visualRoot.SetActive(_hasReceivedPose);
                }
            }
        }
    }
}
