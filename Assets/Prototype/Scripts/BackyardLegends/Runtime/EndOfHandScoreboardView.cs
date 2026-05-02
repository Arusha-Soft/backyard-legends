using BackyardLegends.Core;
using UnityEngine;
using UnityEngine.UI;

namespace BackyardLegends.Runtime
{
    public sealed class EndOfHandScoreboardView : MonoBehaviour
    {
        [Header("Surface")]
        public Image BackgroundImage;

        [Header("Header")]
        public Text ModeText;
        public Text TitleText;
        public Button NotesButton;
        public Button SettingsButton;

        [Header("Hero")]
        public Text HomeOutcomeText;
        public Text AwayOutcomeText;
        public Text HomeRoundDeltaText;
        public Text AwayRoundDeltaText;
        public Image HomeHeroIcon;
        public Image AwayHeroIcon;
        public Image VersusIcon;

        [Header("Rows")]
        public Text HomeTeamText;
        public Text HomeTeamSubText;
        public Text HomeBidText;
        public Text HomeBooksText;
        public Text HomeResultText;
        public Text HomeScoreText;
        public Image HomeResultIcon;
        public Text AwayTeamText;
        public Text AwayTeamSubText;
        public Text AwayBidText;
        public Text AwayBooksText;
        public Text AwayResultText;
        public Text AwayScoreText;
        public Image AwayResultIcon;

        [Header("Totals")]
        public Text HomeTotalLabelText;
        public Text HomeTotalScoreText;
        public Text AwayTotalLabelText;
        public Text AwayTotalScoreText;
        public Text FooterText;

        [Header("Actions")]
        public Button ViewHandButton;
        public Button NextHandButton;
        public Button PlayAgainButton;
        public Button LeaveTableButton;

        public void Render(MatchState state, RuleSetDefinition rules, bool matchComplete, TeamId? winningTeam)
        {
            if (state?.Scores == null || !state.Scores.TryGetValue(TeamId.Home, out var home) || !state.Scores.TryGetValue(TeamId.Away, out var away))
            {
                return;
            }

            rules ??= state.RuleSet;
            var targetScore = rules != null ? rules.TargetScore : 500;
            SetText(ModeText, $"{state.RuleSet.DisplayName.ToUpperInvariant()} MODE");
            SetText(TitleText, matchComplete ? "MATCH COMPLETE" : "END OF HAND");
            RenderHero(home, away);
            RenderTeamRows(state, home, away);
            SetText(HomeTotalLabelText, "GOLD TEAM");
            SetText(AwayTotalLabelText, "RED TEAM");
            SetText(HomeTotalScoreText, home.Score.ToString());
            SetText(AwayTotalScoreText, away.Score.ToString());
            SetText(FooterText, $"SPADE FIRST TEAM TO {targetScore} WINS");

            if (NextHandButton != null)
            {
                NextHandButton.gameObject.SetActive(!matchComplete);
            }

            if (PlayAgainButton != null)
            {
                PlayAgainButton.gameObject.SetActive(matchComplete);
            }
        }

        private void RenderHero(ScoreSnapshot home, ScoreSnapshot away)
        {
            var homeMade = MadeContract(home);
            var awayMade = MadeContract(away);
            SetText(HomeOutcomeText, homeMade ? "WE MADE IT" : "WE GOT SET");
            SetText(AwayOutcomeText, awayMade ? "THEY MADE IT" : "THEY GOT SET");
            SetText(HomeRoundDeltaText, FormatSigned(home.RoundDelta));
            SetText(AwayRoundDeltaText, FormatSigned(away.RoundDelta));
            SetImageVisible(HomeHeroIcon, homeMade);
            SetImageVisible(AwayHeroIcon, !awayMade);
        }

        private void RenderTeamRows(MatchState state, ScoreSnapshot home, ScoreSnapshot away)
        {
            SetText(HomeTeamText, FormatTeamNames(state, SeatId.Bottom, SeatId.Top));
            SetText(HomeTeamSubText, "GOLD TEAM");
            SetText(HomeBidText, home.ContractBid.ToString());
            SetText(HomeBooksText, home.TricksWon.ToString());
            SetText(HomeResultText, BuildResultText(home));
            SetText(HomeScoreText, FormatSigned(home.RoundDelta));
            SetImageVisible(HomeResultIcon, MadeContract(home));

            SetText(AwayTeamText, FormatTeamNames(state, SeatId.Left, SeatId.Right));
            SetText(AwayTeamSubText, "RED TEAM");
            SetText(AwayBidText, away.ContractBid.ToString());
            SetText(AwayBooksText, away.TricksWon.ToString());
            SetText(AwayResultText, BuildResultText(away));
            SetText(AwayScoreText, FormatSigned(away.RoundDelta));
            SetImageVisible(AwayResultIcon, MadeContract(away));
        }

        private static string BuildResultText(ScoreSnapshot score)
        {
            var madeLabel = MadeContract(score) ? "MADE IT" : "SET";
            return $"{madeLabel}\nBags +{score.BagsEarned} | Penalty {FormatSigned(score.BagPenaltyDelta)}";
        }

        private static string FormatTeamNames(MatchState state, SeatId first, SeatId second)
        {
            var firstName = state.SeatNames != null && state.SeatNames.TryGetValue(first, out var firstValue) ? firstValue : first.DisplayName();
            var secondName = state.SeatNames != null && state.SeatNames.TryGetValue(second, out var secondValue) ? secondValue : second.DisplayName();
            return $"{firstName} & {secondName}".ToUpperInvariant();
        }

        private static bool MadeContract(ScoreSnapshot score)
        {
            return score.TricksWon >= score.ContractBid;
        }

        private static string FormatSigned(int value)
        {
            return value.ToString("+#;-#;0");
        }

        private static void SetText(Text target, string value)
        {
            if (target != null)
            {
                target.text = value;
            }
        }

        private static void SetImageVisible(Image target, bool visible)
        {
            if (target != null)
            {
                target.gameObject.SetActive(visible);
            }
        }
    }
}
