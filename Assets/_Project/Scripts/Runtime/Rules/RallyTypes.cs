namespace VRVolley.Rules
{
    public enum TeamId
    {
        TeamA,
        TeamB
    }

    public enum RallyState
    {
        PreServe,
        Serving,
        InPlay,
        RallyEnd,
        SetEnd,
        MatchEnd
    }

    public enum FaultType
    {
        None,
        FourHits,
        DoubleContact,
        CatchOrLift,
        NetTouch,
        AntennaHit,
        BallOutOfBounds,
        FootFault,
        ServeTimeout
    }

    public struct RallyResult
    {
        public TeamId WinningTeam;
        public FaultType Fault;
        public int FaultingPlayerId;
        public TeamId FaultingTeam;

        public RallyResult(TeamId winningTeam, FaultType fault, int faultingPlayerId, TeamId faultingTeam)
        {
            WinningTeam = winningTeam;
            Fault = fault;
            FaultingPlayerId = faultingPlayerId;
            FaultingTeam = faultingTeam;
        }

        public static RallyResult None => new RallyResult(TeamId.TeamA, FaultType.None, -1, TeamId.TeamA);
    }
}
