using UnityEngine;

namespace BackyardLegends.Core
{
    [CreateAssetMenu(fileName = "RuleSetConfig", menuName = "Backyard Legends/Rule Set Config")]
    public sealed class RuleSetConfig : ScriptableObject
    {
        [SerializeField] private string displayName = "Classic";
        [SerializeField] private bool spadesMustBeBroken = true;
        [SerializeField] private bool allowSpadesAnytime;
        [SerializeField] private bool followSuitRequired = true;
        [SerializeField] private bool renegePenaltyEnabled;
        [SerializeField] private int renegePenaltyPoints = -200;
        [SerializeField] private int nilScore = 100;
        [SerializeField] private int nilUnlockScoreGap = 150;
        [SerializeField] private int minimumTeamBid = 4;
        [SerializeField] private int maxBid = 13;
        [SerializeField] private int bagPenaltyThreshold = 10;
        [SerializeField] private int bagPenaltyPoints = -100;
        [SerializeField] private int[] targetScoreOptions = { 100, 200, 300 };
        [SerializeField] private bool enableFutureTurnTimer;
        [SerializeField] private int reservedTurnTimerSeconds = 30;

        public string DisplayName => displayName;
        public int[] TargetScoreOptions => targetScoreOptions;

        public RuleSetDefinition ToDefinition(int targetScore)
        {
            return new RuleSetDefinition
            {
                DisplayName = displayName,
                SpadesMustBeBroken = spadesMustBeBroken,
                AllowSpadesAnytime = allowSpadesAnytime,
                FollowSuitRequired = followSuitRequired,
                RenegePenaltyEnabled = renegePenaltyEnabled,
                RenegePenaltyPoints = renegePenaltyPoints,
                NilScore = nilScore,
                NilUnlockScoreGap = nilUnlockScoreGap,
                MinimumTeamBid = minimumTeamBid,
                MaxBid = maxBid,
                BagPenaltyThreshold = bagPenaltyThreshold,
                BagPenaltyPoints = bagPenaltyPoints,
                TargetScore = targetScore,
                EnableFutureTurnTimer = enableFutureTurnTimer,
                ReservedTurnTimerSeconds = reservedTurnTimerSeconds
            };
        }

        public static RuleSetDefinition CreateClassic(int targetScore)
        {
            return new RuleSetDefinition
            {
                DisplayName = "Classic",
                SpadesMustBeBroken = true,
                AllowSpadesAnytime = false,
                FollowSuitRequired = true,
                RenegePenaltyEnabled = false,
                RenegePenaltyPoints = -200,
                NilScore = 100,
                NilUnlockScoreGap = 150,
                MinimumTeamBid = 4,
                MaxBid = 13,
                BagPenaltyThreshold = 10,
                BagPenaltyPoints = -100,
                TargetScore = targetScore,
                EnableFutureTurnTimer = false,
                ReservedTurnTimerSeconds = 30
            };
        }

        public static RuleSetDefinition CreateStreet(int targetScore)
        {
            return new RuleSetDefinition
            {
                DisplayName = "Street",
                SpadesMustBeBroken = false,
                AllowSpadesAnytime = true,
                FollowSuitRequired = true,
                RenegePenaltyEnabled = true,
                RenegePenaltyPoints = -200,
                NilScore = 100,
                NilUnlockScoreGap = 150,
                MinimumTeamBid = 4,
                MaxBid = 13,
                BagPenaltyThreshold = 10,
                BagPenaltyPoints = -100,
                TargetScore = targetScore,
                EnableFutureTurnTimer = false,
                ReservedTurnTimerSeconds = 30
            };
        }
    }
}
