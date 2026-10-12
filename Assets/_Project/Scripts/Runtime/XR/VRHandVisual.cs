using UnityEngine;
using UnityEngine.InputSystem;

namespace VRVolley.XR
{
    /// <summary>
    /// Renders an athletic hand and forearm visual that only displays when the controller is actively tracked.
    /// Prevents frozen geometry from floating in front of the headset when controllers are idle.
    /// Strictly ZERO managed allocations in per-frame loops.
    /// </summary>
    public sealed class VRHandVisual : MonoBehaviour
    {
        [SerializeField] private bool _isRightHand;
        [SerializeField] private GameObject _visualRoot;

        private InputAction _trackingAction;

        public void Configure(bool isRight, GameObject visualRoot)
        {
            _isRightHand = isRight;
            _visualRoot = visualRoot;
        }

        private void Awake()
        {
            string handPath = _isRightHand ? "<XRController>{RightHand}" : "<XRController>{LeftHand}";
            _trackingAction = new InputAction(
                _isRightHand ? "RightIsTracked" : "LeftIsTracked",
                InputActionType.Button,
                $"{handPath}/isTracked");
            _trackingAction.Enable();

            if (_visualRoot != null)
            {
                _visualRoot.SetActive(false); // Start hidden until tracking confirms valid pose
            }
        }

        private void OnDestroy()
        {
            if (_trackingAction != null)
            {
                _trackingAction.Disable();
                _trackingAction.Dispose();
            }
        }

        private void Update()
        {
            if (_visualRoot == null)
            {
                return;
            }

            // Show visual only when controller is actively sending tracked poses
            bool isTracked = _trackingAction.IsPressed();

            // Fallback check: if transform is no longer at exact zero/origin, it has received poses
            if (!isTracked)
            {
                isTracked = transform.localPosition.sqrMagnitude > 0.001f;
            }

            if (_visualRoot.activeSelf != isTracked)
            {
                _visualRoot.SetActive(isTracked);
            }
        }
    }
}
