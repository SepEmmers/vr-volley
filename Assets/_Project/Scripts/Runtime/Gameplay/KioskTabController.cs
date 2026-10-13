using UnityEngine;

namespace VRVolley.Gameplay
{
    public enum KioskTab
    {
        Drills,
        Calibration
    }

    /// <summary>
    /// Manages tab navigation on the interactive 3D kiosk.
    /// Strictly ZERO managed allocations in per-frame loops.
    /// </summary>
    public sealed class KioskTabController : MonoBehaviour
    {
        [SerializeField] private GameObject _drillsPage;
        [SerializeField] private GameObject _calibrationPage;
        [SerializeField] private MeshRenderer _tabDrillsRenderer;
        [SerializeField] private MeshRenderer _tabCalibrationRenderer;

        private MaterialPropertyBlock _propBlock;
        private KioskTab _activeTab = KioskTab.Drills;

        private static readonly Color ActiveColor = new Color(0.15f, 0.65f, 1.0f);
        private static readonly Color InactiveColor = new Color(0.28f, 0.32f, 0.38f);

        public KioskTab ActiveTab => _activeTab;

        public void Configure(
            GameObject drillsPage,
            GameObject calibrationPage,
            MeshRenderer drillsRenderer,
            MeshRenderer calibrationRenderer)
        {
            _drillsPage = drillsPage;
            _calibrationPage = calibrationPage;
            _tabDrillsRenderer = drillsRenderer;
            _tabCalibrationRenderer = calibrationRenderer;
        }

        private void Awake()
        {
            _propBlock = new MaterialPropertyBlock();
            SetTab(KioskTab.Drills);
        }

        public void SetTab(KioskTab tab)
        {
            _activeTab = tab;
            if (_drillsPage != null)
            {
                _drillsPage.SetActive(tab == KioskTab.Drills);
            }
            if (_calibrationPage != null)
            {
                _calibrationPage.SetActive(tab == KioskTab.Calibration);
            }

            UpdateTabVisuals();
        }

        private void UpdateTabVisuals()
        {
            if (_propBlock == null)
            {
                _propBlock = new MaterialPropertyBlock();
            }

            if (_tabDrillsRenderer != null)
            {
                _tabDrillsRenderer.GetPropertyBlock(_propBlock);
                Color c = _activeTab == KioskTab.Drills ? ActiveColor : InactiveColor;
                _propBlock.SetColor("_BaseColor", c);
                _propBlock.SetColor("_Color", c);
                _tabDrillsRenderer.SetPropertyBlock(_propBlock);
            }

            if (_tabCalibrationRenderer != null)
            {
                _tabCalibrationRenderer.GetPropertyBlock(_propBlock);
                Color c = _activeTab == KioskTab.Calibration ? ActiveColor : InactiveColor;
                _propBlock.SetColor("_BaseColor", c);
                _propBlock.SetColor("_Color", c);
                _tabCalibrationRenderer.SetPropertyBlock(_propBlock);
            }
        }
    }
}
