using UnityEngine;
using UnityEngine.InputSystem;

namespace VRVolley.XR
{
    /// <summary>
    /// Dual-pipeline head tracking for Main Camera under XR Origin.
    /// Ensures head pose is accurately updated in New Input System.
    /// Strictly ZERO managed allocations in per-frame loops.
    /// </summary>
    [DefaultExecutionOrder(-200)]
    public sealed class VRTrackedHead : MonoBehaviour
    {
        private InputAction _posAction;
        private InputAction _rotAction;
        private UnityEngine.XR.InputDevice _hmdDevice;

        private void Awake()
        {
            _posAction = new InputAction("HeadPos", InputActionType.Value, "<XRHMD>/centerEyePosition");
            _rotAction = new InputAction("HeadRot", InputActionType.Value, "<XRHMD>/centerEyeRotation");
        }

        private void OnEnable()
        {
            if (_posAction != null) _posAction.Enable();
            if (_rotAction != null) _rotAction.Enable();

            Application.onBeforeRender += OnBeforeRender;
        }

        private void OnDisable()
        {
            if (_posAction != null) _posAction.Disable();
            if (_rotAction != null) _rotAction.Disable();

            Application.onBeforeRender -= OnBeforeRender;
        }

        private void OnDestroy()
        {
            if (_posAction != null) _posAction.Dispose();
            if (_rotAction != null) _rotAction.Dispose();
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
            if (_posAction != null && _posAction.enabled)
            {
                Vector3 pos = _posAction.ReadValue<Vector3>();
                Quaternion rot = _rotAction != null && _rotAction.enabled ? _rotAction.ReadValue<Quaternion>() : Quaternion.identity;

                if (pos.sqrMagnitude > 1e-6f)
                {
                    transform.localPosition = pos;
                }

                float qNormSq = rot.w * rot.w + rot.x * rot.x + rot.y * rot.y + rot.z * rot.z;
                if (Mathf.Abs(qNormSq - 1.0f) < 0.15f)
                {
                    transform.localRotation = rot;
                }
            }
        }
    }
}
