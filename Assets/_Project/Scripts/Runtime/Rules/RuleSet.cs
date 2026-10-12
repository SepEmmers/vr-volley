using System;

namespace VRVolley.Rules
{
    /// <summary>
    /// Pure C# rule parameters for FIVB Beach 2v2 and Indoor 6v6 competitions.
    /// Decoupled from Unity objects for headless deterministic simulation.
    /// </summary>
    [Serializable]
    public struct RuleSet
    {
        public int MaxTeamTouches;               // FIVB: 3
        public bool BlockCountsAsTeamTouch;      // Beach: true, Indoor: false
        public bool BlockerMayTakeSecondTouch;   // Beach: true (counts as touch 2)
        public float MaxCatchHoldDuration;       // Seconds (Beach: 0.06s, Indoor: 0.09s)
        public float DoubleContactWindow;        // Seconds (approx 0.06s)
        public int PointsToWinRegularSet;        // Beach: 21, Indoor: 25
        public int PointsToWinDecidingSet;       // 15
        public int MinPointLead;                 // 2
        public int SideSwitchIntervalRegular;    // Beach: every 7 combined points
        public int SideSwitchIntervalDeciding;   // Beach: every 5 combined points

        public static RuleSet DefaultBeach2v2 => new RuleSet
        {
            MaxTeamTouches = 3,
            BlockCountsAsTeamTouch = true,
            BlockerMayTakeSecondTouch = true,
            MaxCatchHoldDuration = 0.06f,
            DoubleContactWindow = 0.06f,
            PointsToWinRegularSet = 21,
            PointsToWinDecidingSet = 15,
            MinPointLead = 2,
            SideSwitchIntervalRegular = 7,
            SideSwitchIntervalDeciding = 5
        };

        public static RuleSet DefaultIndoor6v6 => new RuleSet
        {
            MaxTeamTouches = 3,
            BlockCountsAsTeamTouch = false,
            BlockerMayTakeSecondTouch = true,
            MaxCatchHoldDuration = 0.09f,
            DoubleContactWindow = 0.07f,
            PointsToWinRegularSet = 25,
            PointsToWinDecidingSet = 15,
            MinPointLead = 2,
            SideSwitchIntervalRegular = 0,
            SideSwitchIntervalDeciding = 8
        };
    }
}
