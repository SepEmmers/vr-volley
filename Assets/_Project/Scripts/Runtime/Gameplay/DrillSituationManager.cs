using UnityEngine;
using VRVolley.Physics;
using VRVolley.XR;

namespace VRVolley.Gameplay
{
    public enum DrillSituation
    {
        DigFloatServe,
        DigHardSpike,
        SetterPractice,
        SpikeApproachPractice,
        SelfServe
    }

    /// <summary>
    /// Manages interactive volleyball drill situations (digging over the net, setting, spiking, self-serve).
    /// Can be triggered via in-world 3D buttons, controller button inputs, or inspector context menus.
    /// Strictly ZERO managed allocations in per-frame loops.
    /// </summary>
    public sealed class DrillSituationManager : MonoBehaviour
    {
        [Header("Dependencies")]
        [SerializeField] private BallBody _ballBody;
        [SerializeField] private Transform _playerRigTransform;
        [SerializeField] private Transform _kioskTransform;

        [Header("Current Situation")]
        [SerializeField] private DrillSituation _activeSituation = DrillSituation.DigFloatServe;
        [SerializeField] private bool _autoRepeat = false;
        [SerializeField] private float _repeatDelay = 6.0f;

        private float _repeatTimer;
        private UnityEngine.XR.InputDevice _leftDevice;
        private UnityEngine.XR.InputDevice _rightDevice;
        private readonly System.Collections.Generic.List<UnityEngine.XR.InputDevice> _deviceBuffer =
            new System.Collections.Generic.List<UnityEngine.XR.InputDevice>(4);

        private bool _wasPrimaryPressed;
        private bool _wasSecondaryPressed;

        public DrillSituation ActiveSituation => _activeSituation;

        private void Awake()
        {
            if (_ballBody == null)
            {
                _ballBody = FindFirstObjectByType<BallBody>();
            }
            if (_kioskTransform == null)
            {
                GameObject kiosk = GameObject.Find("DrillKiosk_Menu");
                if (kiosk != null) _kioskTransform = kiosk.transform;
            }
            RefreshDevices();
        }

        private void OnEnable()
        {
            UnityEngine.XR.InputDevices.deviceConnected += OnDeviceConnected;
        }

        private void OnDisable()
        {
            UnityEngine.XR.InputDevices.deviceConnected -= OnDeviceConnected;
        }

        private void OnDeviceConnected(UnityEngine.XR.InputDevice dev)
        {
            RefreshDevices();
        }

        private void RefreshDevices()
        {
            _deviceBuffer.Clear();
            UnityEngine.XR.InputDevices.GetDevicesWithCharacteristics(
                UnityEngine.XR.InputDeviceCharacteristics.Controller | UnityEngine.XR.InputDeviceCharacteristics.Left, _deviceBuffer);
            if (_deviceBuffer.Count > 0) _leftDevice = _deviceBuffer[0];

            _deviceBuffer.Clear();
            UnityEngine.XR.InputDevices.GetDevicesWithCharacteristics(
                UnityEngine.XR.InputDeviceCharacteristics.Controller | UnityEngine.XR.InputDeviceCharacteristics.Right, _deviceBuffer);
            if (_deviceBuffer.Count > 0) _rightDevice = _deviceBuffer[0];
        }

        private void Start()
        {
            RepeatActiveSituation();
        }

        private void Update()
        {
            // 1. Controller Button Shortcuts (A/X = Repeat, B/Y = Next Situation)
            bool isPrimary = false;
            bool isSecondary = false;

            if (_rightDevice.isValid)
            {
                if (_rightDevice.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primaryButton, out bool aBtn) && aBtn) isPrimary = true;
                if (_rightDevice.TryGetFeatureValue(UnityEngine.XR.CommonUsages.secondaryButton, out bool bBtn) && bBtn) isSecondary = true;
            }
            if (_leftDevice.isValid)
            {
                if (_leftDevice.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primaryButton, out bool xBtn) && xBtn) isPrimary = true;
                if (_leftDevice.TryGetFeatureValue(UnityEngine.XR.CommonUsages.secondaryButton, out bool yBtn) && yBtn) isSecondary = true;
            }

            if (isPrimary && !_wasPrimaryPressed)
            {
                RepeatActiveSituation();
            }
            _wasPrimaryPressed = isPrimary;

            if (isSecondary && !_wasSecondaryPressed)
            {
                CycleNextSituation();
            }
            _wasSecondaryPressed = isSecondary;

            // 2. Keyboard Shortcuts (PC Testing)
            if (UnityEngine.InputSystem.Keyboard.current != null)
            {
                var kb = UnityEngine.InputSystem.Keyboard.current;
                if (kb.digit1Key.wasPressedThisFrame) TriggerSituation(DrillSituation.DigFloatServe);
                if (kb.digit2Key.wasPressedThisFrame) TriggerSituation(DrillSituation.DigHardSpike);
                if (kb.digit3Key.wasPressedThisFrame) TriggerSituation(DrillSituation.SetterPractice);
                if (kb.digit4Key.wasPressedThisFrame) TriggerSituation(DrillSituation.SpikeApproachPractice);
                if (kb.digit5Key.wasPressedThisFrame) TriggerSituation(DrillSituation.SelfServe);
                if (kb.rKey.wasPressedThisFrame) RepeatActiveSituation();
                if (kb.tabKey.wasPressedThisFrame) CycleNextSituation();
            }

            if (_autoRepeat && _activeSituation != DrillSituation.SelfServe)
            {
                _repeatTimer += Time.deltaTime;
                if (_repeatTimer >= _repeatDelay)
                {
                    _repeatTimer = 0f;
                    RepeatActiveSituation();
                }
            }
        }

        public void CycleNextSituation()
        {
            int next = ((int)_activeSituation + 1) % 5;
            TriggerSituation((DrillSituation)next);
        }

        public void TriggerSituation(DrillSituation situation)
        {
            _activeSituation = situation;
            _repeatTimer = 0f;
            RepeatActiveSituation();
        }

        [ContextMenu("Repeat Active Situation")]
        public void RepeatActiveSituation()
        {
            if (_ballBody == null)
            {
                return;
            }

            switch (_activeSituation)
            {
                case DrillSituation.DigFloatServe:
                    SetupDigFloatServe();
                    break;

                case DrillSituation.DigHardSpike:
                    SetupDigHardSpike();
                    break;

                case DrillSituation.SetterPractice:
                    SetupSetterPractice();
                    break;

                case DrillSituation.SpikeApproachPractice:
                    SetupSpikeApproachPractice();
                    break;

                case DrillSituation.SelfServe:
                    SetupSelfServe();
                    break;
            }
        }

        /// <summary>
        /// 1. DIG FLOAT SERVE: Opponent serves a 16 m/s float serve from far baseline (Z = +7.5m)
        /// over the net into the player's defensive court (Z = -5.0m).
        /// </summary>
        public void SetupDigFloatServe()
        {
            MovePlayerTo(new Vector3(0f, 0f, -5.0f));

            Vector3 startPos = new Vector3(1.0f, 2.2f, 7.5f);
            Vector3 targetPos = new Vector3(0.5f, 1.1f, -4.8f);
            float duration = 1.15f;

            Vector3 launchVel = BallCannon.CalculateLaunchVelocity(startPos, targetPos, duration, Time.fixedDeltaTime);
            Vector3 floatSpin = new Vector3(0f, 1.2f, 0f); // Low knuckle spin

            _ballBody.ResetToPosition(startPos, launchVel, floatSpin);
        }

        /// <summary>
        /// 2. DIG HARD SPIKE: Opponent attacker spikes hard from the net (Z = +0.8m, Y = 3.1m)
        /// downward at 24 m/s directly toward the defender.
        /// </summary>
        public void SetupDigHardSpike()
        {
            MovePlayerTo(new Vector3(0f, 0f, -5.0f));

            Vector3 startPos = new Vector3(-0.8f, 3.1f, 0.8f);
            Vector3 targetPos = new Vector3(0f, 0.8f, -4.8f);
            float duration = 0.55f; // Fast attack reaction

            Vector3 launchVel = BallCannon.CalculateLaunchVelocity(startPos, targetPos, duration, Time.fixedDeltaTime);
            Vector3 topspin = Vector3.right * 45.0f;

            _ballBody.ResetToPosition(startPos, launchVel, topspin);
        }

        /// <summary>
        /// 3. SETTER PRACTICE: Player is at the net setter zone (Z = -1.2m, X = +1.0m).
        /// A friendly reception ball arcs from backcourt (Z = -6.0m) to forehead height (Y = 2.2m).
        /// </summary>
        public void SetupSetterPractice()
        {
            MovePlayerTo(new Vector3(1.0f, 0f, -1.2f));

            Vector3 startPos = new Vector3(-1.0f, 1.0f, -6.0f);
            Vector3 targetPos = new Vector3(1.0f, 2.3f, -1.2f);
            float duration = 1.6f;

            Vector3 launchVel = BallCannon.CalculateLaunchVelocity(startPos, targetPos, duration, Time.fixedDeltaTime);
            _ballBody.ResetToPosition(startPos, launchVel, Vector3.zero);
        }

        /// <summary>
        /// 4. SPIKE APPROACH PRACTICE: Player starts back at Z = -3.5m.
        /// A setter ball arcs high right near the net tape (Z = -0.5m, Y = 3.2m, X = -1.5m).
        /// </summary>
        public void SetupSpikeApproachPractice()
        {
            MovePlayerTo(new Vector3(-1.5f, 0f, -3.5f));

            Vector3 startPos = new Vector3(1.0f, 1.8f, -1.2f);
            Vector3 targetPos = new Vector3(-1.5f, 3.2f, -0.6f);
            float duration = 1.8f;

            Vector3 launchVel = BallCannon.CalculateLaunchVelocity(startPos, targetPos, duration, Time.fixedDeltaTime);
            _ballBody.ResetToPosition(startPos, launchVel, Vector3.zero);
        }

        /// <summary>
        /// 5. SELF SERVE: Player stands behind baseline at Z = -8.5m.
        /// Ball hovers weightlessly slightly behind the player at shoulder height (Z = -8.85m, Y = 1.30m)
        /// ready to be grabbed, tossed, or struck over the net!
        /// </summary>
        public void SetupSelfServe()
        {
            MovePlayerTo(new Vector3(0f, 0f, -8.5f));

            // Position ball slightly behind / to the side of the player within easy arm's reach
            Vector3 hoverPos = new Vector3(-0.30f, 1.30f, -8.85f);
            _ballBody.SuspendAt(hoverPos);
        }

        private void MovePlayerTo(Vector3 position)
        {
            if (_playerRigTransform != null)
            {
                _playerRigTransform.position = position;
            }
            if (_kioskTransform != null)
            {
                _kioskTransform.position = position + new Vector3(-1.0f, 0f, 0.2f);
                _kioskTransform.rotation = Quaternion.Euler(0f, 45f, 0f);
            }
        }

        private void OnGUI()
        {
            // On-screen debug menu for PC testing
            GUILayout.BeginArea(new Rect(10, 10, 220, 260), "VR Volley Drills", GUI.skin.window);

            if (GUILayout.Button("1. Dig Float Serve")) TriggerSituation(DrillSituation.DigFloatServe);
            if (GUILayout.Button("2. Dig Hard Spike")) TriggerSituation(DrillSituation.DigHardSpike);
            if (GUILayout.Button("3. Setter Practice")) TriggerSituation(DrillSituation.SetterPractice);
            if (GUILayout.Button("4. Spike Practice")) TriggerSituation(DrillSituation.SpikeApproachPractice);
            if (GUILayout.Button("5. Self Serve (Ball Hovers)")) TriggerSituation(DrillSituation.SelfServe);
            GUILayout.Space(6);
            if (GUILayout.Button("Repeat Active Situation")) RepeatActiveSituation();

            GUILayout.EndArea();
        }
    }
}
