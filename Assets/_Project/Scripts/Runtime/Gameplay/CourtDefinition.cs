using UnityEngine;
using VRVolley.Rules;

namespace VRVolley.Gameplay
{
    /// <summary>
    /// Definitive FIVB court geometry and in/out line call evaluator.
    /// Strictly ZERO managed allocations.
    /// </summary>
    [CreateAssetMenu(fileName = "CourtDefinition", menuName = "VR Volley/Court Definition")]
    public sealed class CourtDefinition : ScriptableObject
    {
        [Header("Court Dimensions (Meters)")]
        [Tooltip("Full court length along Z axis (Beach: 16m, Indoor: 18m)")]
        [SerializeField] private float _courtLength = 16.0f;

        [Tooltip("Full court width along X axis (Beach: 8m, Indoor: 9m)")]
        [SerializeField] private float _courtWidth = 8.0f;

        [Tooltip("Line tape width (FIVB: 0.05m to 0.08m)")]
        [SerializeField] private float _lineWidth = 0.05f;

        [Header("Net & Antennas (Meters)")]
        [Tooltip("Net height at center (Men: 2.43m, Women: 2.24m)")]
        [SerializeField] private float _netHeight = 2.43f;

        [Tooltip("Net horizontal width along X axis (FIVB: 8.5m)")]
        [SerializeField] private float _netWidth = 8.50f;

        [Tooltip("Antenna height above the net (FIVB: 1.80m)")]
        [SerializeField] private float _antennaHeightAboveNet = 1.80f;

        public float CourtLength => _courtLength;
        public float CourtWidth => _courtWidth;
        public float HalfLength => _courtLength * 0.5f;
        public float HalfWidth => _courtWidth * 0.5f;
        public float LineWidth => _lineWidth;
        public float NetHeight => _netHeight;
        public float NetWidth => _netWidth;
        public float AntennaHeightAboveNet => _antennaHeightAboveNet;
        public float AntennaTopY => _netHeight + _antennaHeightAboveNet;

        /// <summary>
        /// Evaluates whether a ball impact point on the ground is IN bounds according to FIVB rules.
        /// Accounts for line thickness and ball contact radius (ball on the line is IN).
        /// </summary>
        public bool IsInBounds(Vector3 groundContactPoint, float ballRadius, out TeamId defendingCourtSide)
        {
            // Z < 0 is Team A half-court [-HalfLength, 0]; Z > 0 is Team B half-court [0, HalfLength]
            defendingCourtSide = groundContactPoint.z <= 0f ? TeamId.TeamA : TeamId.TeamB;

            // Ball touches line if distance from center is <= HalfDimension + (LineWidth / 2) + (ball imprint radius)
            float effectiveHalfWidth = HalfWidth + (_lineWidth * 0.5f) + (ballRadius * 0.5f);
            float effectiveHalfLength = HalfLength + (_lineWidth * 0.5f) + (ballRadius * 0.5f);

            bool withinWidth = Mathf.Abs(groundContactPoint.x) <= effectiveHalfWidth;
            bool withinLength = Mathf.Abs(groundContactPoint.z) <= effectiveHalfLength;

            return withinWidth && withinLength;
        }

        /// <summary>
        /// Returns the horizontal crossing space boundaries between the two antennas.
        /// </summary>
        public void GetAntennaPositions(out Vector3 antennaLeft, out Vector3 antennaRight)
        {
            // Antennas are placed directly on the outer edge of each sideline at the net plane (Z = 0)
            antennaLeft = new Vector3(-HalfWidth, _netHeight, 0f);
            antennaRight = new Vector3(HalfWidth, _netHeight, 0f);
        }
    }
}
