using System.Collections.Generic;
using BackyardLegends.Core;
using UnityEngine;
using UnityEngine.Events;
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

        private readonly List<RectTransformSnapshot> nextHandButtonLayout = new();
        private Button cachedNextHandButton;
        private bool hasCachedNextHandButtonLayout;

        private struct RectTransformSnapshot
        {
            public RectTransform Target;
            public Vector2 AnchorMin;
            public Vector2 AnchorMax;
            public Vector2 AnchoredPosition;
            public Vector2 SizeDelta;
            public Vector2 Pivot;
            public Vector3 LocalScale;
            public Quaternion LocalRotation;
        }

        private void Awake()
        {
            CacheNextHandButtonLayoutIfNeeded();
        }

        private void LateUpdate()
        {
            RestoreNextHandButtonLayout();
        }

        public void Render(MatchState state, RuleSetDefinition rules, bool matchComplete, TeamId? winningTeam)
        {
            if (state?.Scores == null || !state.Scores.TryGetValue(TeamId.Home, out var home) || !state.Scores.TryGetValue(TeamId.Away, out var away))
            {
                return;
            }

            CacheNextHandButtonLayoutIfNeeded();
            rules ??= state.RuleSet;
            var modeName = rules != null ? rules.DisplayName : state.RuleSet != null ? state.RuleSet.DisplayName : "Spades";
            var targetScore = ResolveTargetScore(state, rules);
            SetText(ModeText, $"{modeName.ToUpperInvariant()} MODE");
            SetText(TitleText, matchComplete ? "MATCH COMPLETE" : "ROUND COMPLETE");
            RenderHero(home, away);
            RenderTeamRows(state, home, away);
            SetText(HomeTotalLabelText, "GOLD TEAM");
            SetText(AwayTotalLabelText, "RED TEAM");
            SetText(HomeTotalScoreText, home.Score.ToString());
            SetText(AwayTotalScoreText, away.Score.ToString());
            SetText(FooterText, $"First team to {targetScore} wins.");

            if (NextHandButton != null)
            {
                RestoreNextHandButtonLayout();
                NextHandButton.gameObject.SetActive(!matchComplete);
                SetButtonLabel(NextHandButton, "Continue");
            }

            if (PlayAgainButton != null)
            {
                PlayAgainButton.gameObject.SetActive(matchComplete);
            }

            if (LeaveTableButton != null)
            {
                LeaveTableButton.gameObject.SetActive(matchComplete);
            }
        }

        public void BindActions(UnityAction onViewHand, UnityAction onNextHand, UnityAction onPlayAgain, UnityAction onLeaveTable)
        {
            BindButton(ViewHandButton, onViewHand);
            BindButton(NextHandButton, onNextHand);
            BindButton(PlayAgainButton, onPlayAgain);
            BindButton(LeaveTableButton, onLeaveTable);
        }

        private void CacheNextHandButtonLayoutIfNeeded()
        {
            if (NextHandButton == null)
            {
                cachedNextHandButton = null;
                nextHandButtonLayout.Clear();
                hasCachedNextHandButtonLayout = false;
                return;
            }

            if (hasCachedNextHandButtonLayout && cachedNextHandButton == NextHandButton)
            {
                return;
            }

            cachedNextHandButton = NextHandButton;
            nextHandButtonLayout.Clear();
            var rects = NextHandButton.GetComponentsInChildren<RectTransform>(true);
            for (var i = 0; i < rects.Length; i++)
            {
                nextHandButtonLayout.Add(CaptureRectTransform(rects[i]));
            }

            hasCachedNextHandButtonLayout = true;
        }

        private void RestoreNextHandButtonLayout()
        {
            CacheNextHandButtonLayoutIfNeeded();
            if (!hasCachedNextHandButtonLayout)
            {
                return;
            }

            for (var i = 0; i < nextHandButtonLayout.Count; i++)
            {
                RestoreRectTransform(nextHandButtonLayout[i]);
            }
        }

        private static RectTransformSnapshot CaptureRectTransform(RectTransform rect)
        {
            return new RectTransformSnapshot
            {
                Target = rect,
                AnchorMin = rect.anchorMin,
                AnchorMax = rect.anchorMax,
                AnchoredPosition = rect.anchoredPosition,
                SizeDelta = rect.sizeDelta,
                Pivot = rect.pivot,
                LocalScale = rect.localScale,
                LocalRotation = rect.localRotation
            };
        }

        private static void RestoreRectTransform(RectTransformSnapshot snapshot)
        {
            if (snapshot.Target == null)
            {
                return;
            }

            snapshot.Target.anchorMin = snapshot.AnchorMin;
            snapshot.Target.anchorMax = snapshot.AnchorMax;
            snapshot.Target.anchoredPosition = snapshot.AnchoredPosition;
            snapshot.Target.sizeDelta = snapshot.SizeDelta;
            snapshot.Target.pivot = snapshot.Pivot;
            snapshot.Target.localScale = snapshot.LocalScale;
            snapshot.Target.localRotation = snapshot.LocalRotation;
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
            SetImageVisible(HomeResultIcon, false);

            SetText(AwayTeamText, FormatTeamNames(state, SeatId.Left, SeatId.Right));
            SetText(AwayTeamSubText, "RED TEAM");
            SetText(AwayBidText, away.ContractBid.ToString());
            SetText(AwayBooksText, away.TricksWon.ToString());
            SetResult(AwayResultText, AwayMadeResultImage, AwaySetResultImage, away);
            SetText(AwayScoreText, FormatSigned(away.RoundDelta));
            SetImageVisible(AwayResultIcon, false);
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
            var bagPenalty = score.BagPenaltyDelta != 0
                ? $"\n{FormatSigned(score.BagPenaltyDelta)} Bag Penalty"
                : string.Empty;
            SetText(text, $"Bags +{score.BagsEarned}{bagPenalty}");
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

            return 500;
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

        private static void SetButtonLabel(Button button, string value)
        {
            if (button == null)
            {
                return;
            }

            var labels = button.GetComponentsInChildren<Text>(true);
            for (var i = 0; i < labels.Length; i++)
            {
                labels[i].text = value;
            }
        }

        private static void BindButton(Button button, UnityAction action)
        {
            if (button == null)
            {
                return;
            }

            button.onClick.RemoveAllListeners();
            if (action != null)
            {
                button.onClick.AddListener(action);
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
