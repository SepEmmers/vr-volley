using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

namespace VRVolley.XR
{
    /// <summary>
    /// Lightweight non-allocating haptics manager for Meta Quest Touch controllers.
    /// Fires scaled impulses based on contact normal impulse magnitude.
    /// </summary>
    public sealed class VRVolleyHaptics : MonoBehaviour
    {
        [SerializeField] private float _minImpulseThreshold = 1.0f;
        [SerializeField] private float _maxImpulseForFullHaptic = 25.0f;

        private InputDevice _leftDevice;
        private InputDevice _rightDevice;
        private readonly List<InputDevice> _deviceBuffer = new List<InputDevice>(4);

        private void Awake()
        {
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

        private void OnDeviceConnected(InputDevice device)
        {
            RefreshDevices();
        }

        private void RefreshDevices()
        {
            _deviceBuffer.Clear();
            InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.Controller | InputDeviceCharacteristics.Left, _deviceBuffer);
            if (_deviceBuffer.Count > 0)
            {
                _leftDevice = _deviceBuffer[0];
            }

            _deviceBuffer.Clear();
            InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.Controller | InputDeviceCharacteristics.Right, _deviceBuffer);
            if (_deviceBuffer.Count > 0)
            {
                _rightDevice = _deviceBuffer[0];
            }
        }

        /// <summary>
        /// Sends a tactile impulse scaled by the physical collision impulse magnitude.
        /// </summary>
        public void PlayContactHaptic(bool isRightHand, float impulseMagnitude, float baseDuration = 0.035f)
        {
            if (impulseMagnitude < _minImpulseThreshold)
            {
                return;
            }

            float amplitude = Mathf.Clamp01(impulseMagnitude / _maxImpulseForFullHaptic);
            InputDevice target = isRightHand ? _rightDevice : _leftDevice;

            if (target.isValid)
            {
                target.SendHapticImpulse(0u, amplitude, baseDuration);
            }
        }

        /// <summary>
        /// Dual-hand pulse for forearm platform bumps and overhead sets.
        /// </summary>
        public void PlayDualHandHaptic(float impulseMagnitude, float duration = 0.05f)
        {
            PlayContactHaptic(false, impulseMagnitude, duration);
            PlayContactHaptic(true, impulseMagnitude, duration);
        }
    }
}
