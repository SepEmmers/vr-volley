using NUnit.Framework;
using VRVolley.Rules;

namespace VRVolley.Tests.EditMode
{
    [TestFixture]
    public class RallyRefereeTests
    {
        [Test]
        public void FourHits_TriggersFaultAndAwardsPointToOpponent()
        {
            var referee = new RallyReferee(RuleSet.DefaultBeach2v2);
            referee.StartServe(TeamId.TeamA);
            referee.NotifyServeHit(1, TeamId.TeamA, 0.0);
            referee.NotifyBallCrossedNet();

            // Team B receives: Touch 1
            bool t1 = referee.NotifyBallTouch(3, TeamId.TeamB, false, 0.02f, 1.0, out var fault1);
            Assert.IsTrue(t1);

            // Team B sets: Touch 2
            bool t2 = referee.NotifyBallTouch(4, TeamId.TeamB, false, 0.02f, 2.0, out var fault2);
            Assert.IsTrue(t2);

            // Team B attacks: Touch 3
            bool t3 = referee.NotifyBallTouch(3, TeamId.TeamB, false, 0.02f, 3.0, out var fault3);
            Assert.IsTrue(t3);

            // Team B 4th touch: FAULT
            bool t4 = referee.NotifyBallTouch(4, TeamId.TeamB, false, 0.02f, 4.0, out var fault4);
            Assert.IsFalse(t4);
            Assert.AreEqual(FaultType.FourHits, fault4.Fault);
            Assert.AreEqual(TeamId.TeamA, fault4.WinningTeam);
            Assert.AreEqual(1, referee.ScoreTeamA);
        }

        [Test]
        public void DoubleContact_SamePlayerConsecutiveHitTriggersFault()
        {
            var referee = new RallyReferee(RuleSet.DefaultBeach2v2);
            referee.StartServe(TeamId.TeamA);
            referee.NotifyServeHit(1, TeamId.TeamA, 0.0);
            referee.NotifyBallCrossedNet();

            // Team B player 3 receives (Touch 1)
            referee.NotifyBallTouch(3, TeamId.TeamB, false, 0.02f, 1.0, out _);

            // Player 3 hits it again (Touch 2) after double contact window
            bool t2 = referee.NotifyBallTouch(3, TeamId.TeamB, false, 0.02f, 1.5, out var fault);
            Assert.IsFalse(t2);
            Assert.AreEqual(FaultType.DoubleContact, fault.Fault);
            Assert.AreEqual(TeamId.TeamA, fault.WinningTeam);
        }

        [Test]
        public void BeachBlock_CountsAsTeamTouch_AndBlockerMayTakeSecondTouch()
        {
            var referee = new RallyReferee(RuleSet.DefaultBeach2v2);
            referee.StartServe(TeamId.TeamA);
            referee.NotifyServeHit(1, TeamId.TeamA, 0.0);
            referee.NotifyBallCrossedNet();

            // Team B player 3 blocks at the net (Touch 1)
            bool blockTouch = referee.NotifyBallTouch(3, TeamId.TeamB, true, 0.02f, 1.0, out _);
            Assert.IsTrue(blockTouch);
            Assert.AreEqual(1, referee.TouchesThisPossession);

            // FIVB Beach Rule: Blocker (player 3) may play the next contact! (Touch 2)
            bool blockerDig = referee.NotifyBallTouch(3, TeamId.TeamB, false, 0.02f, 1.5, out var fault);
            Assert.IsTrue(blockerDig, "Blocker must be allowed to make the immediate next contact in beach volleyball");
            Assert.AreEqual(2, referee.TouchesThisPossession);

            // Teammate (player 4) attacks the ball over (Touch 3)
            bool teamAttack = referee.NotifyBallTouch(4, TeamId.TeamB, false, 0.02f, 2.0, out _);
            Assert.IsTrue(teamAttack);
            Assert.AreEqual(3, referee.TouchesThisPossession);

            // 4th touch must fault
            bool fourthTouch = referee.NotifyBallTouch(3, TeamId.TeamB, false, 0.02f, 2.5, out var fourFault);
            Assert.IsFalse(fourthTouch);
            Assert.AreEqual(FaultType.FourHits, fourFault.Fault);
        }

        [Test]
        public void IndoorBlock_DoesNotCountAsTeamTouch()
        {
            var referee = new RallyReferee(RuleSet.DefaultIndoor6v6);
            referee.StartServe(TeamId.TeamA);
            referee.NotifyServeHit(1, TeamId.TeamA, 0.0);
            referee.NotifyBallCrossedNet();

            // Team B player 3 blocks
            referee.NotifyBallTouch(3, TeamId.TeamB, true, 0.02f, 1.0, out _);
            Assert.AreEqual(0, referee.TouchesThisPossession, "Indoor block must not count towards the 3 team touches");

            // Team B still has 3 full touches remaining
            Assert.IsTrue(referee.NotifyBallTouch(5, TeamId.TeamB, false, 0.02f, 1.5, out _));
            Assert.IsTrue(referee.NotifyBallTouch(2, TeamId.TeamB, false, 0.02f, 2.0, out _));
            Assert.IsTrue(referee.NotifyBallTouch(4, TeamId.TeamB, false, 0.02f, 2.5, out _));
            Assert.AreEqual(3, referee.TouchesThisPossession);
        }

        [Test]
        public void CatchOrLift_ExceedingMaxHoldTimeTriggersFault()
        {
            var referee = new RallyReferee(RuleSet.DefaultBeach2v2);
            referee.StartServe(TeamId.TeamA);
            referee.NotifyServeHit(1, TeamId.TeamA, 0.0);
            referee.NotifyBallCrossedNet();

            // Player holds ball for 0.15s (exceeding beach 0.06s threshold)
            bool touch = referee.NotifyBallTouch(3, TeamId.TeamB, false, 0.15f, 1.0, out var fault);
            Assert.IsFalse(touch);
            Assert.AreEqual(FaultType.CatchOrLift, fault.Fault);
            Assert.AreEqual(TeamId.TeamA, fault.WinningTeam);
        }

        [Test]
        public void BallLandedInBounds_AwardsPointToOpposingAttackingTeam()
        {
            var referee = new RallyReferee(RuleSet.DefaultBeach2v2);
            referee.StartServe(TeamId.TeamA);
            referee.NotifyServeHit(1, TeamId.TeamA, 0.0);
            referee.NotifyBallCrossedNet();

            // Ball hits sand in bounds on Team B's court side
            var result = referee.NotifySurfaceHit(landedInBounds: true, courtSide: TeamId.TeamB);
            Assert.AreEqual(TeamId.TeamA, result.WinningTeam);
            Assert.AreEqual(1, referee.ScoreTeamA);
        }

        [Test]
        public void BeachSideSwitch_TriggersEvery7Points()
        {
            var referee = new RallyReferee(RuleSet.DefaultBeach2v2);
            referee.StartServe(TeamId.TeamA);

            // Simulate 4 points for A, 3 points for B (combined 7)
            for (int i = 0; i < 4; i++)
            {
                referee.StartServe(TeamId.TeamA);
                referee.NotifyServeHit(1, TeamId.TeamA, 0.0);
                referee.NotifySurfaceHit(landedInBounds: true, courtSide: TeamId.TeamB);
            }
            for (int i = 0; i < 3; i++)
            {
                referee.StartServe(TeamId.TeamB);
                referee.NotifyServeHit(3, TeamId.TeamB, 0.0);
                referee.NotifySurfaceHit(landedInBounds: true, courtSide: TeamId.TeamA);
            }

            Assert.AreEqual(4, referee.ScoreTeamA);
            Assert.AreEqual(3, referee.ScoreTeamB);
            Assert.IsTrue(referee.ShouldSwitchSides(), "Beach side switch must trigger when combined score reaches 7");
        }
    }
}
