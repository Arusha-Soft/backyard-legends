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
        public Image HomeMadeOutcomeImage;
        public Image HomeSetOutcomeImage;
        public Image AwayMadeOutcomeImage;
        public Image AwaySetOutcomeImage;
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
        public Image HomeMadeResultImage;
        public Image HomeSetResultImage;
        public Image HomeResultIcon;
        public Text AwayTeamText;
        public Text AwayTeamSubText;
        public Text AwayBidText;
        public Text AwayBooksText;
        public Text AwayResultText;
        public Text AwayScoreText;
        public Image AwayMadeResultImage;
        public Image AwaySetResultImage;
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
            var modeName = rules != null ? rules.DisplayName : state.RuleSet != null ? state.RuleSet.DisplayName : "Spades";
            var targetScore = ResolveTargetScore(state, rules);
            SetText(ModeText, $"{modeName.ToUpperInvariant()} MODE");
            SetText(TitleText, matchComplete ? "MATCH COMPLETE" : "END OF HAND");
            RenderHero(home, away);
            RenderTeamRows(state, home, away);
            SetText(HomeTotalLabelText, "GOLD TEAM");
            SetText(AwayTotalLabelText, "RED TEAM");
            SetText(HomeTotalScoreText, home.Score.ToString());
            SetText(AwayTotalScoreText, away.Score.ToString());
            SetText(FooterText, $"First team to {targetScore} wins.");

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
            SetOutcome(HomeOutcomeText, HomeMadeOutcomeImage, HomeSetOutcomeImage, homeMade ? "WE MADE IT" : "WE GOT SET", homeMade);
            SetOutcome(AwayOutcomeText, AwayMadeOutcomeImage, AwaySetOutcomeImage, awayMade ? "THEY MADE IT" : "THEY GOT SET", awayMade);
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
            SetResult(HomeResultText, HomeMadeResultImage, HomeSetResultImage, home);
            SetText(HomeScoreText, FormatSigned(home.RoundDelta));
            SetImageVisible(HomeResultIcon, MadeContract(home));

            SetText(AwayTeamText, FormatTeamNames(state, SeatId.Left, SeatId.Right));
            SetText(AwayTeamSubText, "RED TEAM");
            SetText(AwayBidText, away.ContractBid.ToString());
            SetText(AwayBooksText, away.TricksWon.ToString());
            SetResult(AwayResultText, AwayMadeResultImage, AwaySetResultImage, away);
            SetText(AwayScoreText, FormatSigned(away.RoundDelta));
            SetImageVisible(AwayResultIcon, MadeContract(away));
        }

        private static void SetOutcome(Text text, Image madeImage, Image setImage, string fallbackText, bool made)
        {
            var selectedImage = made ? madeImage : setImage;
            SetImageVisible(madeImage, made && madeImage != null);
            SetImageVisible(setImage, !made && setImage != null);
            SetTextVisible(text, selectedImage == null);
            SetText(text, fallbackText);
        }

        private static void SetResult(Text text, Image madeImage, Image setImage, ScoreSnapshot score)
        {
            var made = MadeContract(score);
            SetImageVisible(madeImage, made && madeImage != null);
            SetImageVisible(setImage, !made && setImage != null);
            SetText(text, $"Bags +{score.BagsEarned}\nPenalty {FormatSigned(score.BagPenaltyDelta)}");
            SetTextVisible(text, true);
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

        private static int ResolveTargetScore(MatchState state, RuleSetDefinition rules)
        {
            if (rules != null && rules.TargetScore > 0)
            {
                return rules.TargetScore;
            }

            if (state.TargetScore > 0)
            {
                return state.TargetScore;
            }

            if (state.RuleSet != null && state.RuleSet.TargetScore > 0)
            {
                return state.RuleSet.TargetScore;
            }

            return 300;
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

        private static void SetTextVisible(Text target, bool visible)
        {
            if (target != null)
            {
                target.gameObject.SetActive(visible);
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
