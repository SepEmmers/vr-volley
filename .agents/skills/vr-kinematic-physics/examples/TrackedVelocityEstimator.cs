// Reference implementation for the vr-kinematic-physics skill.
// Copy into Assets/_Project/Scripts/Runtime/XR/ when implementing. Zero allocations after Awake.
using UnityEngine;

namespace VRVolley.XR
{
    /// <summary>
    /// Estimates linear and angular velocity of a tracked transform (controller grip pose)
    /// using a least-squares fit over a rolling window of timestamped samples.
    /// Runs before gameplay physics so contact solvers read fresh values in FixedUpdate.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class TrackedVelocityEstimator : MonoBehaviour
    {
        [SerializeField] private Transform _trackedPoint;
        [SerializeField, Range(3, 16)] private int _windowSize = 5;
        [Tooltip("Samples whose position moved less than this are treated as duplicates (no new tracking data).")]
        [SerializeField] private float _duplicateEpsilon = 1e-6f;

        private Vector3[] _positions;
        private double[] _times;
        private int _head;    // index of the most recent sample
        private int _count;   // number of valid samples

        private Quaternion _previousRotation;
        private double _previousRotationTime;

        public Vector3 Velocity { get; private set; }
        public Vector3 AngularVelocity { get; private set; }     // rad/s, convention v_surface = ω × r
        public Vector3 CurrentPosition { get; private set; }
        public Vector3 PreviousPosition { get; private set; }    // position at the previous physics step
        public Quaternion CurrentRotation { get; private set; }
        public Quaternion PreviousRotation { get; private set; }

        private void Awake()
        {
            if (_trackedPoint == null)
            {
                _trackedPoint = transform;
            }

            _positions = new Vector3[_windowSize];
            _times = new double[_windowSize];
            CurrentPosition = PreviousPosition = _trackedPoint.position;
            CurrentRotation = PreviousRotation = _previousRotation = _trackedPoint.rotation;
        }

        private void OnEnable()
        {
            _count = 0;
            _head = 0;
        }

        private void FixedUpdate()
        {
            double now = Time.fixedTimeAsDouble;
            Vector3 position = _trackedPoint.position;
            Quaternion rotation = _trackedPoint.rotation;

            PreviousPosition = CurrentPosition;
            PreviousRotation = CurrentRotation;
            CurrentPosition = position;
            CurrentRotation = rotation;

            // Tracking poses update once per rendered frame. If physics stepped twice in one frame,
            // the pose is identical. Skip it, otherwise the velocity estimate collapses toward zero.
            if (_count > 0 && (position - _positions[_head]).sqrMagnitude < _duplicateEpsilon * _duplicateEpsilon)
            {
                return;
            }

            _head = (_head + 1) % _windowSize;
            _positions[_head] = position;
            _times[_head] = now;
            if (_count < _windowSize)
            {
                _count++;
            }

            Velocity = ComputeLeastSquaresVelocity();
            AngularVelocity = ComputeAngularVelocity(rotation, now);
        }

        /// <summary>Velocity of a point rigidly attached to the tracked transform (e.g., palm center).</summary>
        public Vector3 GetPointVelocity(Vector3 worldPoint)
        {
            return Velocity + Vector3.Cross(AngularVelocity, worldPoint - CurrentPosition);
        }

        private Vector3 ComputeLeastSquaresVelocity()
        {
            if (_count < 2)
            {
                return Vector3.zero;
            }

            // Mean time and position over the window (relative to newest sample for float precision).
            double tRef = _times[_head];
            double tMean = 0.0;
            Vector3 pMean = Vector3.zero;
            for (int i = 0; i < _count; i++)
            {
                int idx = (_head - i + _windowSize) % _windowSize;
                tMean += _times[idx] - tRef;
                pMean += _positions[idx];
            }
            tMean /= _count;
            pMean /= _count;

            double denom = 0.0;
            Vector3 numer = Vector3.zero;
            for (int i = 0; i < _count; i++)
            {
                int idx = (_head - i + _windowSize) % _windowSize;
                float dt = (float)(_times[idx] - tRef - tMean);
                numer += dt * (_positions[idx] - pMean);
                denom += dt * dt;
            }

            return denom > 1e-12 ? numer / (float)denom : Vector3.zero;
        }

        private Vector3 ComputeAngularVelocity(Quaternion rotation, double now)
        {
            float dt = (float)(now - _previousRotationTime);
            Quaternion previous = _previousRotation;
            _previousRotation = rotation;
            _previousRotationTime = now;

            if (dt <= 1e-5f)
            {
                return AngularVelocity;
            }

            Quaternion delta = rotation * Quaternion.Inverse(previous);
            delta.ToAngleAxis(out float angleDeg, out Vector3 axis);
            if (float.IsInfinity(axis.x) || float.IsNaN(axis.x))
            {
                return Vector3.zero;
            }
            if (angleDeg > 180f)
            {
                angleDeg -= 360f;
            }

            // Unity's quaternion convention matches Vector3.Cross: Euler(0,90,0) * forward == right == Cross(up, forward).
            // So axis * angle is directly ω in the convention v_surface = ω × r used by ContactMath
            // (and by Rigidbody.angularVelocity).
            return axis * (angleDeg * Mathf.Deg2Rad / dt);
        }
    }
}
