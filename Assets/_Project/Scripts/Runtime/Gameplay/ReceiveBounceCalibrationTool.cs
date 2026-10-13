using UnityEngine;

namespace VRVolley.Gameplay
{
    /// <summary>
    /// Interactive tool to calibrate and test receiving platform bounciness and energy return.
    /// Eliminates "dead ball absorption" by allowing the player to tune restitution and impulse pop.
    /// Strictly ZERO managed allocations in per-frame loops.
    /// </summary>
    public sealed class ReceiveBounceCalibrationTool : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private HandBallContactSolver _solver;
        [SerializeField] private XR.BodyContactVolumes _contactVolumes;
        [SerializeField] private TextMesh _readoutTextMesh;
        [SerializeField] private XR.VRVolleyHaptics _haptics;
        [SerializeField] private BallTossGrabbable _tossGrabbable;
        [SerializeField] private XR.VRPlayerLocomotion _locomotion;

        private float _bounceMultiplier = 1.0f;

        public float BounceMultiplier => _bounceMultiplier;

        private void Awake()
        {
            if (_solver == null)
            {
                _solver = FindFirstObjectByType<HandBallContactSolver>();
            }
            if (_contactVolumes == null)
            {
                _contactVolumes = FindFirstObjectByType<XR.BodyContactVolumes>();
            }
            if (_haptics == null)
            {
                _haptics = FindFirstObjectByType<XR.VRVolleyHaptics>();
            }
            if (_tossGrabbable == null)
            {
                _tossGrabbable = FindFirstObjectByType<BallTossGrabbable>();
            }
            if (_locomotion == null)
            {
                _locomotion = FindFirstObjectByType<XR.VRPlayerLocomotion>();
            }

            if (_solver != null)
            {
                _bounceMultiplier = _solver.BounceMultiplier;
            }
            else
            {
                _bounceMultiplier = PlayerPrefs.GetFloat("VRVolley_PlatformBounce", 1.0f);
            }

            UpdateReadout();
        }

        public void SetReadoutTextMesh(TextMesh tm)
        {
            _readoutTextMesh = tm;
            UpdateReadout();
        }

        public void AdjustBounce(float delta)
        {
            _bounceMultiplier = Mathf.Clamp(_bounceMultiplier + delta, 0.50f, 2.00f);
            ApplyBounce();
        }

        public void ToggleOneHandMode()
        {
            if (_contactVolumes != null)
            {
                _contactVolumes.IsOneHandMode = !_contactVolumes.IsOneHandMode;
            }
            if (_haptics != null)
            {
                _haptics.PlayContactHaptic(true, 10.0f, 0.05f);
            }
            UpdateReadout();
        }

        public void ToggleTossMode()
        {
            if (_tossGrabbable == null)
            {
                _tossGrabbable = FindFirstObjectByType<BallTossGrabbable>();
            }
            if (_tossGrabbable != null)
            {
                _tossGrabbable.ToggleTossMode();
            }
            if (_haptics != null)
            {
                _haptics.PlayContactHaptic(true, 10.0f, 0.05f);
                _haptics.PlayContactHaptic(false, 10.0f, 0.05f);
            }
            UpdateReadout();
        }

        public void SetBouncePreset(float multiplier)
        {
            _bounceMultiplier = Mathf.Clamp(multiplier, 0.50f, 2.00f);
            ApplyBounce();
        }

        public void ResetDefault()
        {
            _bounceMultiplier = 1.0f;
            ApplyBounce();
        }

        private void ApplyBounce()
        {
            if (_solver != null)
            {
                _solver.BounceMultiplier = _bounceMultiplier;
            }

            PlayerPrefs.SetFloat("VRVolley_PlatformBounce", _bounceMultiplier);
            PlayerPrefs.Save();

            PlayFeedbackHaptic();
            UpdateReadout();
        }

        public void AdjustSpikePower(float delta)
        {
            if (_solver == null) _solver = FindFirstObjectByType<HandBallContactSolver>();
            if (_solver != null)
            {
                _solver.SpikePowerMultiplier = Mathf.Clamp(_solver.SpikePowerMultiplier + delta, 1.0f, 3.5f);
            }
            PlayFeedbackHaptic();
            UpdateReadout();
        }

        public void ResetSpikePower()
        {
            if (_solver == null) _solver = FindFirstObjectByType<HandBallContactSolver>();
            if (_solver != null)
            {
                _solver.SpikePowerMultiplier = 2.0f;
            }
            PlayFeedbackHaptic();
            UpdateReadout();
        }

        public void AdjustJumpSensitivity(float delta)
        {
            if (_locomotion == null) _locomotion = FindFirstObjectByType<XR.VRPlayerLocomotion>();
            if (_locomotion != null)
            {
                _locomotion.ArmSwingThreshold = Mathf.Clamp(_locomotion.ArmSwingThreshold + delta, 0.8f, 3.0f);
            }
            PlayFeedbackHaptic();
            UpdateReadout();
        }

        public void ResetJumpSensitivity()
        {
            if (_locomotion == null) _locomotion = FindFirstObjectByType<XR.VRPlayerLocomotion>();
            if (_locomotion != null)
            {
                _locomotion.ArmSwingThreshold = 1.7f;
            }
            PlayFeedbackHaptic();
            UpdateReadout();
        }

        private void PlayFeedbackHaptic()
        {
            if (_haptics != null)
            {
                _haptics.PlayContactHaptic(true, 8.0f, 0.04f);
                _haptics.PlayContactHaptic(false, 8.0f, 0.04f);
            }
        }

        private void UpdateReadout()
        {
            if (_readoutTextMesh != null)
            {
                int bouncePercent = Mathf.RoundToInt(_bounceMultiplier * 100f);
                string feelLabel;
                if (bouncePercent < 85) feelLabel = "Soft Touch";
                else if (bouncePercent <= 110) feelLabel = "Crisp FIVB Pop";
                else feelLabel = "High Spring Pop";

                if (_tossGrabbable == null)
                {
                    _tossGrabbable = FindFirstObjectByType<BallTossGrabbable>();
                }
                string tossText = _tossGrabbable != null && _tossGrabbable.CurrentTossMode == TossMode.RawPhysics
                    ? "Raw 1:1"
                    : "Stabilized";

                if (_solver == null) _solver = FindFirstObjectByType<HandBallContactSolver>();
                float spikePower = _solver != null ? _solver.SpikePowerMultiplier : 2.0f;

                if (_locomotion == null) _locomotion = FindFirstObjectByType<XR.VRPlayerLocomotion>();
                float jumpThresh = _locomotion != null ? _locomotion.ArmSwingThreshold : 1.7f;

                _readoutTextMesh.text = $"BOUNCE: {bouncePercent}% ({feelLabel}) | TOSS: {tossText}\n" +
                                        $"SERVE POWER: {spikePower:F1}x Amplification (Default 2.0x)\n" +
                                        $"JUMP THRESHOLD: {jumpThresh:F1} m/s (Lower = Easier, Default 1.7)";
            }
        }
    }
}
