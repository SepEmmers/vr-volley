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
        private UnityEngine.InputSystem.InputAction _aBtnAction;
        private UnityEngine.InputSystem.InputAction _bBtnAction;
        private UnityEngine.InputSystem.InputAction _xBtnAction;
        private UnityEngine.InputSystem.InputAction _yBtnAction;

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

            _aBtnAction = new UnityEngine.InputSystem.InputAction("A", UnityEngine.InputSystem.InputActionType.Button, "<XRController>{RightHand}/primaryButton");
            _bBtnAction = new UnityEngine.InputSystem.InputAction("B", UnityEngine.InputSystem.InputActionType.Button, "<XRController>{RightHand}/secondaryButton");
            _xBtnAction = new UnityEngine.InputSystem.InputAction("X", UnityEngine.InputSystem.InputActionType.Button, "<XRController>{LeftHand}/primaryButton");
            _yBtnAction = new UnityEngine.InputSystem.InputAction("Y", UnityEngine.InputSystem.InputActionType.Button, "<XRController>{LeftHand}/secondaryButton");
        }

        private void OnEnable()
        {
            if (_aBtnAction != null) _aBtnAction.Enable();
            if (_bBtnAction != null) _bBtnAction.Enable();
            if (_xBtnAction != null) _xBtnAction.Enable();
            if (_yBtnAction != null) _yBtnAction.Enable();
        }

        private void OnDisable()
        {
            if (_aBtnAction != null) _aBtnAction.Disable();
            if (_bBtnAction != null) _bBtnAction.Disable();
            if (_xBtnAction != null) _xBtnAction.Disable();
            if (_yBtnAction != null) _yBtnAction.Disable();
        }

        private void OnDestroy()
        {
            if (_aBtnAction != null) _aBtnAction.Dispose();
            if (_bBtnAction != null) _bBtnAction.Dispose();
            if (_xBtnAction != null) _xBtnAction.Dispose();
            if (_yBtnAction != null) _yBtnAction.Dispose();
        }

        private void Start()
        {
            RepeatActiveSituation();
        }

        private void Update()
        {
            // 1. Controller Button Shortcuts (A/X = Repeat, B = Next, Y = Summon Kiosk)
            if ((_aBtnAction != null && _aBtnAction.WasPressedThisFrame()) ||
                (_xBtnAction != null && _xBtnAction.WasPressedThisFrame()))
            {
                RepeatActiveSituation();
            }

            if (_bBtnAction != null && _bBtnAction.WasPressedThisFrame())
            {
                CycleNextSituation();
            }

            if (_yBtnAction != null && _yBtnAction.WasPressedThisFrame())
            {
                SummonKioskNearPlayer();
            }

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

        public void SummonKioskNearPlayer()
        {
            if (_playerRigTransform != null && _kioskTransform != null)
            {
                _kioskTransform.position = _playerRigTransform.position + new Vector3(-0.9f, 0f, 0.3f);
                _kioskTransform.rotation = Quaternion.Euler(0f, 45f, 0f);
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
        /// 1. DIG FLOAT SERVE: Opponent serves a float serve from far baseline (Z = +7.5m)
        /// over the net tape directly into defender's forearm sweet spot at Z = -4.8m, Y = 1.25m.
        /// </summary>
        public void SetupDigFloatServe()
        {
            MovePlayerTo(new Vector3(0f, 0f, -5.0f));

            Vector3 startPos = new Vector3(0.5f, 2.3f, 7.5f);
            Vector3 launchVel = new Vector3(-0.45f, 5.80f, -12.80f);
            Vector3 floatSpin = new Vector3(0f, 0.8f, 0f);

            _ballBody.ResetToPosition(startPos, launchVel, floatSpin);
        }

        /// <summary>
        /// 2. DIG HARD SPIKE: Opponent attacker spikes hard from the net (Z = +0.6m, Y = 2.95m)
        /// downward directly at defender.
        /// </summary>
        public void SetupDigHardSpike()
        {
            MovePlayerTo(new Vector3(0f, 0f, -5.0f));

            Vector3 startPos = new Vector3(-0.5f, 2.95f, 0.6f);
            Vector3 launchVel = new Vector3(1.20f, -4.80f, -18.50f);
            Vector3 topspin = Vector3.right * 35.0f;

            _ballBody.ResetToPosition(startPos, launchVel, topspin);
        }

        /// <summary>
        /// 3. SETTER PRACTICE: Player is at the net setter zone (Z = -1.2m, X = +0.8m).
        /// Friendly reception arcs from backcourt to forehead height (Y = 2.25m).
        /// </summary>
        public void SetupSetterPractice()
        {
            MovePlayerTo(new Vector3(0.8f, 0f, -1.2f));

            Vector3 startPos = new Vector3(-1.0f, 1.1f, -5.5f);
            Vector3 launchVel = new Vector3(1.40f, 6.40f, 5.00f);

            _ballBody.ResetToPosition(startPos, launchVel, Vector3.zero);
        }

        /// <summary>
        /// 4. SPIKE APPROACH PRACTICE: Player starts back at Z = -3.5m.
        /// Setter ball arcs high right along the net tape (Z = -0.6m, Y = 3.1m, X = -1.5m).
        /// </summary>
        public void SetupSpikeApproachPractice()
        {
            MovePlayerTo(new Vector3(-1.5f, 0f, -3.5f));

            Vector3 startPos = new Vector3(0.8f, 1.8f, -1.2f);
            Vector3 launchVel = new Vector3(-2.00f, 7.90f, 0.50f);

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
