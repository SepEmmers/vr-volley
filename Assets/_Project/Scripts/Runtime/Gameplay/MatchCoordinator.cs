using System;
using UnityEngine;
using VRVolley.Physics;
using VRVolley.Rules;

namespace VRVolley.Gameplay
{
    /// <summary>
    /// Coordinates the active volleyball rally: monitors net plane crossing,
    /// bridges contact solver events and court ground impacts to the FIVB RallyReferee,
    /// and announces points, side switches, and set completions.
    /// Strictly ZERO managed allocations in per-frame loops.
    /// </summary>
    public sealed class MatchCoordinator : MonoBehaviour
    {
        [Header("Scene Dependencies")]
        [SerializeField] private BallBody _ballBody;
        [SerializeField] private HandBallContactSolver _playerContactSolver;
        [SerializeField] private CourtDefinition _courtDefinition;

        [Header("Game Rules")]
        [SerializeField] private bool _isBeachMode = true;

        private RallyReferee _referee;
        private bool _wasBallBehindNet;
        private bool _hasNetStateInitialized;

        public event Action<RallyResult, int, int> OnPointAwarded; // result, scoreA, scoreB
        public event Action OnSideSwitchRequired;
        public event Action<TeamId> OnSetCompleted;

        public RallyReferee Referee => _referee;
        public CourtDefinition Court => _courtDefinition;
        public int ScoreTeamA => _referee != null ? _referee.ScoreTeamA : 0;
        public int ScoreTeamB => _referee != null ? _referee.ScoreTeamB : 0;

        private void Awake()
        {
            if (_ballBody == null)
            {
                _ballBody = FindFirstObjectByType<BallBody>();
            }
            if (_playerContactSolver == null)
            {
                _playerContactSolver = FindFirstObjectByType<HandBallContactSolver>();
            }
            if (_courtDefinition == null)
            {
                _courtDefinition = ScriptableObject.CreateInstance<CourtDefinition>();
            }

            RuleSet rules = _isBeachMode ? RuleSet.DefaultBeach2v2 : RuleSet.DefaultIndoor6v6;
            _referee = new RallyReferee(rules);
        }

        private void OnEnable()
        {
            if (_playerContactSolver != null)
            {
                _playerContactSolver.OnBallContact += HandlePlayerBallContact;
            }
            if (_ballBody != null)
            {
                _ballBody.OnEnvironmentCollision += HandleBallEnvironmentCollision;
            }
        }

        private void OnDisable()
        {
            if (_playerContactSolver != null)
            {
                _playerContactSolver.OnBallContact -= HandlePlayerBallContact;
            }
            if (_ballBody != null)
            {
                _ballBody.OnEnvironmentCollision -= HandleBallEnvironmentCollision;
            }
        }

        private void Start()
        {
            StartNewRally(TeamId.TeamA);
        }

        public void StartNewRally(TeamId servingTeam)
        {
            _referee.StartServe(servingTeam);
            _hasNetStateInitialized = false;

            // Reset ball to server position
            float serverZ = servingTeam == TeamId.TeamA ? -(_courtDefinition.HalfLength + 0.5f) : (_courtDefinition.HalfLength + 0.5f);
            Vector3 servePos = new Vector3(0f, 1.8f, serverZ);
            _ballBody.ResetToPosition(servePos, Vector3.zero, Vector3.zero);
        }

        private void FixedUpdate()
        {
            if (_referee == null || _ballBody == null || _courtDefinition == null)
            {
                return;
            }

            if (_referee.State != RallyState.InPlay)
            {
                return;
            }

            // Monitor net plane crossing (Z = 0)
            Vector3 ballPos = _ballBody.CurrentState.Position;
            bool isBehindNet = ballPos.z < 0f;

            if (!_hasNetStateInitialized)
            {
                _wasBallBehindNet = isBehindNet;
                _hasNetStateInitialized = true;
                return;
            }

            if (isBehindNet != _wasBallBehindNet)
            {
                // Ball crossed the net plane!
                _wasBallBehindNet = isBehindNet;

                // Validate legal crossing space: must be within the two antennas and above net tape
                bool withinAntennas = Mathf.Abs(ballPos.x) <= _courtDefinition.HalfWidth;
                bool aboveNet = ballPos.y >= _courtDefinition.NetHeight;

                if (withinAntennas && aboveNet)
                {
                    _referee.NotifyBallCrossedNet();
                }
                else
                {
                    // Crossing space violation or antenna hit!
                    RallyResult faultResult = _referee.NotifyAntennaHit(_referee.LastTouchingPlayerId, _referee.LastTouchingTeam);
                    ProcessRallyResult(faultResult);
                }
            }
        }

        private void HandlePlayerBallContact(int playerId, TeamId team, XR.ContactVolumeType volumeType, float impulseMag)
        {
            if (_referee.State == RallyState.Serving)
            {
                _referee.NotifyServeHit(playerId, team, Time.fixedTimeAsDouble);
                return;
            }

            bool isBlock = (volumeType == XR.ContactVolumeType.DualForearmPlatform && _ballBody.CurrentState.Position.y >= _courtDefinition.NetHeight);

            bool validTouch = _referee.NotifyBallTouch(playerId, team, isBlock, 0.02f, Time.fixedTimeAsDouble, out RallyResult fault);
            if (!validTouch)
            {
                ProcessRallyResult(fault);
            }
        }

        private void HandleBallEnvironmentCollision(Vector3 contactPoint, Vector3 normal, bool isGround)
        {
            if (_referee.State != RallyState.InPlay)
            {
                return;
            }

            if (isGround)
            {
                bool inBounds = _courtDefinition.IsInBounds(contactPoint, _ballBody.Radius, out TeamId defendingSide);
                RallyResult result = _referee.NotifySurfaceHit(inBounds, defendingSide);
                ProcessRallyResult(result);
            }
        }

        private void ProcessRallyResult(RallyResult result)
        {
            OnPointAwarded?.Invoke(result, _referee.ScoreTeamA, _referee.ScoreTeamB);

            if (_referee.IsSetOver(out TeamId winner))
            {
                OnSetCompleted?.Invoke(winner);
                return;
            }

            if (_referee.ShouldSwitchSides())
            {
                OnSideSwitchRequired?.Invoke();
            }

            // Prepare next rally after short delay
            StartNewRally(_referee.ServingTeam);
        }
    }
}
