using System;

namespace VRVolley.Physics
{
    /// <summary>
    /// Kinematic contact physical parameters. Defines the restitution, effective body segment mass,
    /// and surface friction for a given contact volume type.
    /// </summary>
    [Serializable]
    public struct ContactParams
    {
        /// <summary>Normal coefficient of restitution (0 = dead/absorbing, 1 = perfectly elastic).</summary>
        public float Restitution;

        /// <summary>Effective inertial mass of the striking body segment (kg). Folds arm compliance into the impulse.</summary>
        public float EffectiveMass;

        /// <summary>Tangential Coulomb friction coefficient (governs spin transfer between ball and hand/platform).</summary>
        public float Friction;

        public ContactParams(float restitution, float effectiveMass, float friction)
        {
            Restitution = restitution;
            EffectiveMass = effectiveMass;
            Friction = friction;
        }

        // Standard volleyball presets derived from FIVB reference specifications
        public static ContactParams ForearmPlatform => new ContactParams(0.50f, 8.0f, 0.30f);
        public static ContactParams SingleForearmDig => new ContactParams(0.45f, 4.0f, 0.30f);
        public static ContactParams SpikePalm => new ContactParams(0.65f, 3.5f, 0.70f);
        public static ContactParams BlockHands => new ContactParams(0.40f, 6.0f, 0.40f);
        public static ContactParams SandGround => new ContactParams(0.18f, 1000f, 0.90f);
        public static ContactParams IndoorGround => new ContactParams(0.78f, 1000f, 0.50f);
    }
}
