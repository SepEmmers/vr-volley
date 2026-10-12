using UnityEngine;
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
            _propBlock = new MaterialPropertyBlock();
            SetColor(_normalColor);
        }

        private void Update()
        {
            if (_cooldownTimer > 0f)
            {
                _cooldownTimer -= Time.deltaTime;
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_cooldownTimer > 0f)
            {
                return;
            }

            // Triggered by hand/controller contact capsule or ray
            if (other.GetComponentInParent<TrackedVelocityEstimator>() != null ||
                other.GetComponentInParent<BodyContactVolumes>() != null ||
                other.CompareTag("Player") || other.CompareTag("GameController"))
            {
                PressButton();
            }
        }

        [ContextMenu("Press Button")]
        public void PressButton()
        {
            _cooldownTimer = _cooldown;
            SetColor(_hoverColor);

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

            var haptics = FindFirstObjectByType<VRVolleyHaptics>();
            if (haptics != null)
            {
                haptics.PlayContactHaptic(true, 10.0f, 0.05f);
                haptics.PlayContactHaptic(false, 10.0f, 0.05f);
            }

            Invoke(nameof(ResetColor), 0.25f);
        }

        private void ResetColor()
        {
            SetColor(_normalColor);
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
