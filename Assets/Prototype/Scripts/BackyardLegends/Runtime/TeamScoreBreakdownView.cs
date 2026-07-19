using System;
using BackyardLegends.Core;
using UnityEngine;
using UnityEngine.UI;

namespace BackyardLegends.Runtime
{
    [Serializable]
    public sealed class ScoreBreakdownLineView
    {
        public GameObject Root;
        public Text LabelText;
        public Text ValueText;

        public void Set(string label, int value)
        {
            if (LabelText != null)
            {
                LabelText.text = label;
            }

            if (ValueText != null)
            {
                ValueText.text = TeamScoreBreakdownView.FormatSigned(value);
            }
        }

        public void SetVisible(bool visible)
        {
            if (Root != null)
            {
                Root.SetActive(visible);
                return;
            }

            if (LabelText != null)
            {
                LabelText.gameObject.SetActive(visible);
            }

            if (ValueText != null)
            {
                ValueText.gameObject.SetActive(visible);
            }
        }
    }

    public sealed class TeamScoreBreakdownView : MonoBehaviour
    {
        [Header("Team")]
        public Text TeamNamesText;
        public Text TeamLabelText;
        public Text OutcomeText;
        public Text BidBooksText;

        [Header("Scoring")]
        public ScoreBreakdownLineView BidLine = new();
        public ScoreBreakdownLineView BagsLine = new();
        public ScoreBreakdownLineView NilLine = new();
        public ScoreBreakdownLineView BagPenaltyLine = new();
        public ScoreBreakdownLineView RenegePenaltyLine = new();
        public ScoreBreakdownLineView RoundTotalLine = new();
        public ScoreBreakdownLineView MatchScoreLine = new();

        public void Render(string teamNames, string teamLabel, ScoreSnapshot score, bool renegePenaltyEnabled)
        {
            if (score == null)
            {
                return;
            }

            var madeContract = score.TricksWon >= score.ContractBid;
            var bidDelta = score.ContractBid * (madeContract ? 10 : -10);

            SetText(TeamNamesText, teamNames);
            SetText(TeamLabelText, teamLabel);
            SetText(OutcomeText, madeContract ? "MADE" : "SET");
            SetText(BidBooksText, $"BID {score.ContractBid}  •  BOOKS {score.TricksWon}");

            BidLine.Set(madeContract ? "Bid Made" : "Bid Set", bidDelta);
            BagsLine.Set("Bags", score.BagsEarned);
            NilLine.Set(ResolveNilLabel(score.NilDelta), score.NilDelta);
            BagPenaltyLine.Set("Bag Penalty", score.BagPenaltyDelta);
            RenegePenaltyLine.Set("Renege Penalty", score.RenegeDelta);
            RenegePenaltyLine.SetVisible(renegePenaltyEnabled);
            RoundTotalLine.Set("Round Total", score.RoundDelta + score.NilDelta);
            MatchScoreLine.Set("Match Score", score.Score);
        }

        internal static string FormatSigned(int value)
        {
            return value.ToString("+#;-#;0");
        }

        private static string ResolveNilLabel(int nilDelta)
        {
            if (nilDelta > 0)
            {
                return "Nil Bonus";
            }

            return nilDelta < 0 ? "Nil Penalty" : "Nil";
        }

        private static void SetText(Text target, string value)
        {
            if (target != null)
            {
                target.text = value;
            }
        }
    }
}
