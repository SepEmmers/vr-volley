namespace VRVolley.Rules
{
    /// <summary>
    /// Pure C# referee and rally state machine adhering strictly to FIVB rules.
    /// Tracks touches, evaluates faults (four hits, double contacts, catches, out of bounds),
    /// and manages scoring without any Unity engine dependencies.
    /// </summary>
    public sealed class RallyReferee
    {
        public RuleSet Rules { get; set; }

        public RallyState State { get; private set; } = RallyState.PreServe;
        public TeamId ServingTeam { get; private set; } = TeamId.TeamA;
        public TeamId CurrentPossessionTeam { get; private set; } = TeamId.TeamA;

        public int ScoreTeamA { get; private set; }
        public int ScoreTeamB { get; private set; }
        public int CurrentSet { get; private set; } = 1;

        public int TouchesThisPossession { get; private set; }
        public int LastTouchingPlayerId { get; private set; } = -1;
        public TeamId LastTouchingTeam { get; private set; } = TeamId.TeamA;
        public double LastTouchTimestamp { get; private set; }
        public bool LastTouchWasBlock { get; private set; }

        public RallyResult LastRallyResult { get; private set; } = RallyResult.None;

        public RallyReferee(RuleSet rules)
        {
            Rules = rules;
        }

        public void StartServe(TeamId servingTeam)
        {
            ServingTeam = servingTeam;
            CurrentPossessionTeam = servingTeam;
            TouchesThisPossession = 0;
            LastTouchingPlayerId = -1;
            LastTouchWasBlock = false;
            State = RallyState.Serving;
        }

        public void NotifyServeHit(int playerId, TeamId team, double timestamp)
        {
            if (State != RallyState.Serving)
            {
                return;
            }

            LastTouchingPlayerId = playerId;
            LastTouchingTeam = team;
            CurrentPossessionTeam = team;
            TouchesThisPossession = 1;
            LastTouchTimestamp = timestamp;
            LastTouchWasBlock = false;
            State = RallyState.InPlay;
        }

        public bool NotifyBallTouch(int playerId, TeamId team, bool isBlock, float holdDuration, double timestamp, out RallyResult fault)
        {
            fault = RallyResult.None;
            if (State != RallyState.InPlay)
            {
                return false;
            }

            // 1. Lift / Catch fault: ball held longer than allowed duration
            if (holdDuration > Rules.MaxCatchHoldDuration)
            {
                fault = EndRallyWithFault(FaultType.CatchOrLift, playerId, team);
                return false;
            }

            // 2. Team possession switch if touching after opponent
            if (team != CurrentPossessionTeam)
            {
                CurrentPossessionTeam = team;
                TouchesThisPossession = 0;
                LastTouchingPlayerId = -1;
                LastTouchWasBlock = false;
            }

            // 3. Consecutive hit / Double contact check
            if (playerId == LastTouchingPlayerId && !LastTouchWasBlock)
            {
                // First team reception contact exception (deflection over short interval)
                bool isFirstTeamContact = (TouchesThisPossession == 0);
                float interval = (float)(timestamp - LastTouchTimestamp);

                if (!isFirstTeamContact || interval > Rules.DoubleContactWindow)
                {
                    fault = EndRallyWithFault(FaultType.DoubleContact, playerId, team);
                    return false;
                }
            }

            // 4. Touch counting
            if (isBlock)
            {
                LastTouchWasBlock = true;
                if (Rules.BlockCountsAsTeamTouch)
                {
                    TouchesThisPossession++;
                }
            }
            else
            {
                TouchesThisPossession++;
                LastTouchWasBlock = false;
            }

            LastTouchingPlayerId = playerId;
            LastTouchingTeam = team;
            LastTouchTimestamp = timestamp;

            // 5. Four Hits Fault: exceeded team touch limit
            if (TouchesThisPossession > Rules.MaxTeamTouches)
            {
                fault = EndRallyWithFault(FaultType.FourHits, playerId, team);
                return false;
            }

            return true;
        }

        public RallyResult NotifyBallCrossedNet()
        {
            if (State != RallyState.InPlay)
            {
                return RallyResult.None;
            }

            // Team possession transfers across the net plane
            CurrentPossessionTeam = (LastTouchingTeam == TeamId.TeamA) ? TeamId.TeamB : TeamId.TeamA;
            TouchesThisPossession = 0;
            LastTouchingPlayerId = -1;
            LastTouchWasBlock = false;
            return RallyResult.None;
        }

        public RallyResult NotifySurfaceHit(bool landedInBounds, TeamId courtSide)
        {
            if (State != RallyState.InPlay)
            {
                return RallyResult.None;
            }

            if (landedInBounds)
            {
                // Ball landed in bounds: the team defending this court side failed to return it
                TeamId winningTeam = (courtSide == TeamId.TeamA) ? TeamId.TeamB : TeamId.TeamA;
                return AwardPoint(winningTeam, FaultType.None, -1, courtSide);
            }
            else
            {
                // Ball landed out of bounds: the team that touched it last committed the out fault
                TeamId winningTeam = (LastTouchingTeam == TeamId.TeamA) ? TeamId.TeamB : TeamId.TeamA;
                return AwardPoint(winningTeam, FaultType.BallOutOfBounds, LastTouchingPlayerId, LastTouchingTeam);
            }
        }

        public RallyResult NotifyNetTouch(int playerId, TeamId team)
        {
            if (State != RallyState.InPlay)
            {
                return RallyResult.None;
            }
            return EndRallyWithFault(FaultType.NetTouch, playerId, team);
        }

        public RallyResult NotifyAntennaHit(int playerId, TeamId team)
        {
            if (State != RallyState.InPlay)
            {
                return RallyResult.None;
            }
            return EndRallyWithFault(FaultType.AntennaHit, playerId, team);
        }

        private RallyResult EndRallyWithFault(FaultType fault, int playerId, TeamId faultingTeam)
        {
            TeamId winningTeam = (faultingTeam == TeamId.TeamA) ? TeamId.TeamB : TeamId.TeamA;
            return AwardPoint(winningTeam, fault, playerId, faultingTeam);
        }

        private RallyResult AwardPoint(TeamId winningTeam, FaultType fault, int playerId, TeamId faultingTeam)
        {
            if (winningTeam == TeamId.TeamA)
            {
                ScoreTeamA++;
            }
            else
            {
                ScoreTeamB++;
            }

            ServingTeam = winningTeam;
            State = RallyState.RallyEnd;

            LastRallyResult = new RallyResult(winningTeam, fault, playerId, faultingTeam);
            return LastRallyResult;
        }

        public bool ShouldSwitchSides()
        {
            if (Rules.SideSwitchIntervalRegular <= 0)
            {
                return false;
            }
            int combined = ScoreTeamA + ScoreTeamB;
            int interval = (CurrentSet == 3) ? Rules.SideSwitchIntervalDeciding : Rules.SideSwitchIntervalRegular;
            return combined > 0 && (combined % interval == 0);
        }

        public bool IsSetOver(out TeamId setWinner)
        {
            setWinner = TeamId.TeamA;
            int target = (CurrentSet == 3) ? Rules.PointsToWinDecidingSet : Rules.PointsToWinRegularSet;
            int lead = System.Math.Abs(ScoreTeamA - ScoreTeamB);

            if ((ScoreTeamA >= target || ScoreTeamB >= target) && lead >= Rules.MinPointLead)
            {
                setWinner = (ScoreTeamA > ScoreTeamB) ? TeamId.TeamA : TeamId.TeamB;
                return true;
            }
            return false;
        }
    }
}
