using System;
using UnityEngine;

namespace VRVolley.Physics
{
    /// <summary>
    /// Pure C# aerodynamic and mass profile parameters for the volleyball.
    /// Can be initialized directly or wrapped in a ScriptableObject.
    /// </summary>
    [Serializable]
    public struct BallProfileData
    {
        public float Radius;               // meters (Beach: 0.107m, Indoor: 0.105m)
        public float Mass;                 // kg (FIVB: 0.270kg)
        public float AirDensity;            // kg/m^3 (approx 1.20kg/m^3 at sea level)

        // Drag parameters (C_d vs speed)
        public float DragSubcritical;       // C_d below drag crisis (~0.47)
        public float DragSupercritical;     // C_d above drag crisis (~0.20)
        public float DragCrisisSpeedMin;    // m/s (~16.0 m/s)
        public float DragCrisisSpeedMax;    // m/s (~21.0 m/s)

        // Magnus lift parameters
        public float MagnusCoefficientSlope;// linear lift slope k (~1.0)
        public float MagnusMaxLift;         // C_L cap (~0.30)
        public float SpinDecayHalfLife;     // seconds (~4.0s)

        // Float serve knuckle wobble parameters
        public float KnuckleSpeedMin;       // m/s (~13.0 m/s)
        public float KnuckleSpeedMax;       // m/s (~22.0 m/s)
        public float KnuckleMaxSpin;        // rad/s (~6.0 rad/s)
        public float KnuckleSideForceCoeff; // ~0.08
        public float KnuckleFrequency;      // Hz (~2.0 Hz)

        public float CrossSectionalArea => Mathf.PI * Radius * Radius;
        public float MomentOfInertia => (2f / 3f) * Mass * Radius * Radius;

        public static BallProfileData DefaultBeach => new BallProfileData
        {
            Radius = 0.107f,
            Mass = 0.270f,
            AirDensity = 1.20f,

            DragSubcritical = 0.47f,
            DragSupercritical = 0.20f,
            DragCrisisSpeedMin = 16.0f,
            DragCrisisSpeedMax = 21.0f,

            MagnusCoefficientSlope = 1.0f,
            MagnusMaxLift = 0.30f,
            SpinDecayHalfLife = 4.0f,

            KnuckleSpeedMin = 13.0f,
            KnuckleSpeedMax = 22.0f,
            KnuckleMaxSpin = 6.0f,
            KnuckleSideForceCoeff = 0.08f,
            KnuckleFrequency = 2.0f
        };

        public static BallProfileData DefaultIndoor => new BallProfileData
        {
            Radius = 0.105f,
            Mass = 0.270f,
            AirDensity = 1.20f,

            DragSubcritical = 0.46f,
            DragSupercritical = 0.19f,
            DragCrisisSpeedMin = 17.0f,
            DragCrisisSpeedMax = 22.0f,

            MagnusCoefficientSlope = 1.0f,
            MagnusMaxLift = 0.30f,
            SpinDecayHalfLife = 4.0f,

            KnuckleSpeedMin = 14.0f,
            KnuckleSpeedMax = 23.0f,
            KnuckleMaxSpin = 5.0f,
            KnuckleSideForceCoeff = 0.07f,
            KnuckleFrequency = 2.2f
        };
    }
}
