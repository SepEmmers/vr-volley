using UnityEngine;
using VRVolley.Physics;

namespace VRVolley.XR
{
    public enum ContactVolumeType
    {
        LeftPalm,
        RightPalm,
        LeftForearm,
        RightForearm,
        DualForearmPlatform
    }

    /// <summary>
    /// Manages the kinematic contact capsules for player hands and forearms.
    /// Dynamically evaluates whether the two wrists are paired in a forearm bump passing platform.
    /// Strictly ZERO managed allocations.
    /// </summary>
    public sealed class BodyContactVolumes : MonoBehaviour
    {
        [Header("Tracked Anchors")]
        [SerializeField] private TrackedVelocityEstimator _leftEstimator;
        [SerializeField] private TrackedVelocityEstimator _rightEstimator;

        [Header("Capsule Dimensions (Meters)")]
        [SerializeField] private float _palmRadius = 0.045f;
        [SerializeField] private float _forearmRadius = 0.042f;
        [SerializeField] private float _forearmLength = 0.24f;

        [Header("Platform Criteria")]
        [SerializeField] private float _maxWristDistanceForPlatform = 0.18f;
        [SerializeField] private float _maxForearmAngleDeg = 40.0f;

        [Header("Control Mode")]
        [SerializeField] private bool _isOneHandMode = false;
        [SerializeField] private bool _dominantIsRight = true;
        [SerializeField] private float _dualPlatformSpan = 0.09f;
        [SerializeField] private VRTrackedController _dominantController;

        // Local offsets relative to OpenXR controller grip pose
        private readonly Vector3 _localWrist = new Vector3(0f, 0f, -0.05f);
        private readonly Vector3 _localPalm = new Vector3(0f, -0.02f, 0.06f);
        private readonly Vector3 _localForearmEnd = new Vector3(0f, -0.06f, -0.28f);

        public bool IsOneHandMode
        {
            get => _isOneHandMode;
            set
            {
                _isOneHandMode = value;
                PlayerPrefs.SetInt("VRVolley_OneHandMode", _isOneHandMode ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        public bool DominantIsRight => _dominantIsRight;
        public float DualPlatformSpan => _dualPlatformSpan;

        public bool IsPlatformActive { get; private set; }
        public Vector3 PlatformNormal { get; private set; }

        public float PalmRadius => _palmRadius;
        public float ForearmRadius => _forearmRadius;

        public TrackedVelocityEstimator LeftEstimator => _leftEstimator;
        public TrackedVelocityEstimator RightEstimator => _rightEstimator;

        private void Awake()
        {
            _isOneHandMode = false;
            PlayerPrefs.SetInt("VRVolley_OneHandMode", 0);
            if (_dominantController == null)
            {
                var controllers = FindObjectsByType<VRTrackedController>(FindObjectsSortMode.None);
                foreach (var c in controllers)
                {
                    if (c.IsRightHand == _dominantIsRight)
                    {
                        _dominantController = c;
                        break;
                    }
                }
            }
        }

        private void Update()
        {
            EvaluateForearmPlatform();
        }

        private void EvaluateForearmPlatform()
        {
            if (_isOneHandMode)
            {
                TrackedVelocityEstimator dominant = _dominantIsRight ? _rightEstimator : _leftEstimator;
                if (dominant == null)
                {
                    IsPlatformActive = false;
                    return;
                }

                if (_dominantController == null)
                {
                    _dominantController = dominant.GetComponent<VRTrackedController>();
                }

                bool isGripHeld = _dominantController != null && _dominantController.IsGripPressed;
                if (!isGripHeld)
                {
                    IsPlatformActive = false;
                    return;
                }

                Transform t = dominant.transform;
                Vector3 wrist = t.TransformPoint(_localWrist);
                Vector3 elbow = t.TransformPoint(_localForearmEnd);
                Vector3 forearmDir = (wrist - elbow).normalized;
                Vector3 lateralDir = _dominantIsRight ? -t.right : t.right;

                Vector3 normal = Vector3.Cross(forearmDir, lateralDir).normalized;
                if (Vector3.Dot(normal, Vector3.up) < 0f)
                {
                    normal = -normal;
                }

                PlatformNormal = normal;
                IsPlatformActive = true;
                return;
            }

            // 2-Hand Dual Controller Evaluation
            if (_leftEstimator == null || _rightEstimator == null)
            {
                IsPlatformActive = false;
                return;
            }

            Vector3 leftWrist = _leftEstimator.transform.TransformPoint(_localWrist);
            Vector3 rightWrist = _rightEstimator.transform.TransformPoint(_localWrist);

            float wristDistSq = (rightWrist - leftWrist).sqrMagnitude;
            if (wristDistSq > _maxWristDistanceForPlatform * _maxWristDistanceForPlatform)
            {
                IsPlatformActive = false;
                return;
            }

            Vector3 leftElbow = _leftEstimator.transform.TransformPoint(_localForearmEnd);
            Vector3 rightElbow = _rightEstimator.transform.TransformPoint(_localForearmEnd);

            Vector3 dirL = (leftWrist - leftElbow).normalized;
            Vector3 dirR = (rightWrist - rightElbow).normalized;

            float dot = Vector3.Dot(dirL, dirR);
            float minDot = Mathf.Cos(_maxForearmAngleDeg * Mathf.Deg2Rad);
            if (dot < minDot)
            {
                IsPlatformActive = false;
                return;
            }

            // Normal orthogonal to the forearm plane and wrist span
            Vector3 wristSpan = rightWrist - leftWrist;
            Vector3 avgForearm = dirL + dirR;
            Vector3 norm = Vector3.Cross(avgForearm, wristSpan).normalized;

            // Orient normal upward/forward
            if (Vector3.Dot(norm, Vector3.up) < 0f)
            {
                norm = -norm;
            }

            PlatformNormal = norm;
            IsPlatformActive = true;
        }

        public void GetHandCapsule(bool isRight, out Vector3 start, out Vector3 end, out float radius)
        {
            if (_isOneHandMode)
            {
                TrackedVelocityEstimator dominant = _dominantIsRight ? _rightEstimator : _leftEstimator;
                if (dominant != null)
                {
                    Transform t = dominant.transform;
                    Vector3 baseStart = t.TransformPoint(_localWrist);
                    Vector3 baseEnd = t.TransformPoint(_localPalm);
                    bool isSynthetic = isRight != _dominantIsRight;
                    if (isSynthetic)
                    {
                        Vector3 offset = (_dominantIsRight ? -t.right : t.right) * _dualPlatformSpan;
                        start = baseStart + offset;
                        end = baseEnd + offset;
                    }
                    else
                    {
                        start = baseStart;
                        end = baseEnd;
                    }
                }
                else
                {
                    start = end = Vector3.zero;
                }
                radius = _palmRadius;
                return;
            }

            TrackedVelocityEstimator estimator = isRight ? _rightEstimator : _leftEstimator;
            if (estimator != null)
            {
                Transform t = estimator.transform;
                start = t.TransformPoint(_localWrist);
                end = t.TransformPoint(_localPalm);
            }
            else
            {
                start = end = Vector3.zero;
            }
            radius = _palmRadius;
        }

        public void GetForearmCapsule(bool isRight, out Vector3 start, out Vector3 end, out float radius)
        {
            if (_isOneHandMode)
            {
                TrackedVelocityEstimator dominant = _dominantIsRight ? _rightEstimator : _leftEstimator;
                if (dominant != null)
                {
                    Transform t = dominant.transform;
                    Vector3 baseStart = t.TransformPoint(_localWrist);
                    Vector3 baseEnd = t.TransformPoint(_localForearmEnd);
                    bool isSynthetic = isRight != _dominantIsRight;
                    if (isSynthetic)
                    {
                        Vector3 offset = (_dominantIsRight ? -t.right : t.right) * _dualPlatformSpan;
                        start = baseStart + offset;
                        end = baseEnd + offset;
                    }
                    else
                    {
                        start = baseStart;
                        end = baseEnd;
                    }
                }
                else
                {
                    start = end = Vector3.zero;
                }
                radius = _forearmRadius;
                return;
            }

            TrackedVelocityEstimator estimator = isRight ? _rightEstimator : _leftEstimator;
            if (estimator != null)
            {
                Transform t = estimator.transform;
                start = t.TransformPoint(_localWrist);
                end = t.TransformPoint(_localForearmEnd);
            }
            else
            {
                start = end = Vector3.zero;
            }
            radius = _forearmRadius;
        }
    }
}
