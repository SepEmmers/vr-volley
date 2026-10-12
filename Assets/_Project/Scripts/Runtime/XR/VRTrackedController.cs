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

        private InputAction _posAction;
        private InputAction _rotAction;
        private UnityEngine.XR.InputDevice _xrDevice;
        private bool _hasReceivedPose;

        public bool IsRightHand => _isRightHand;
        public bool IsTracked => _hasReceivedPose;

        public void Configure(bool isRightHand, GameObject visualRoot)
        {
            _isRightHand = isRightHand;
            _visualRoot = visualRoot;
        }

        private void Awake()
        {
            string handPath = _isRightHand ? "<XRController>{RightHand}" : "<XRController>{LeftHand}";
            _posAction = new InputAction(_isRightHand ? "RightPos" : "LeftPos", InputActionType.Value, $"{handPath}/devicePosition");
            _rotAction = new InputAction(_isRightHand ? "RightRot" : "LeftRot", InputActionType.Value, $"{handPath}/deviceRotation");

            RefreshXRDevice();
        }

        private void OnEnable()
        {
            if (_posAction != null) _posAction.Enable();
            if (_rotAction != null) _rotAction.Enable();

            Application.onBeforeRender += OnBeforeRender;
            UnityEngine.XR.InputDevices.deviceConnected += OnDeviceConnected;
        }

        private void OnDisable()
        {
            if (_posAction != null) _posAction.Disable();
            if (_rotAction != null) _rotAction.Disable();

            Application.onBeforeRender -= OnBeforeRender;
            UnityEngine.XR.InputDevices.deviceConnected -= OnDeviceConnected;
        }

        private void OnDestroy()
        {
            if (_posAction != null) _posAction.Dispose();
            if (_rotAction != null) _rotAction.Dispose();
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
                    transform.localPosition = pos;
                    float qNormSq = rot.w * rot.w + rot.x * rot.x + rot.y * rot.y + rot.z * rot.z;
                    if (Mathf.Abs(qNormSq - 1.0f) < 0.15f)
                    {
                        transform.localRotation = rot;
                    }
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
                    if (_xrDevice.TryGetFeatureValue(UnityEngine.XR.CommonUsages.devicePosition, out Vector3 xrPos))
                    {
                        transform.localPosition = xrPos;
                        poseUpdated = true;
                    }
                    if (_xrDevice.TryGetFeatureValue(UnityEngine.XR.CommonUsages.deviceRotation, out Quaternion xrRot))
                    {
                        transform.localRotation = xrRot;
                        poseUpdated = true;
                    }
                }
            }

            if (poseUpdated)
            {
                _hasReceivedPose = true;
            }

            // Show visual root once controller pose is received
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
