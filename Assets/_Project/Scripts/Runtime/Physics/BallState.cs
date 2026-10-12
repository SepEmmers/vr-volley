using System;
using UnityEngine;

namespace VRVolley.Physics
{
    /// <summary>
    /// Pure C# value type representing the instantaneous kinematic state of the volleyball.
    /// Used for deterministic simulation, prediction, and network synchronization.
    /// </summary>
    [Serializable]
    public struct BallState
    {
        public Vector3 Position;
        public Vector3 Velocity;
        public Vector3 AngularVelocity; // rad/s (convention: v_surface = ω × r)
        public double Timestamp;

        public BallState(Vector3 position, Vector3 velocity, Vector3 angularVelocity, double timestamp = 0.0)
        {
            Position = position;
            Velocity = velocity;
            AngularVelocity = angularVelocity;
            Timestamp = timestamp;
        }

        public static BallState Zero => new BallState(Vector3.zero, Vector3.zero, Vector3.zero, 0.0);
    }
}
