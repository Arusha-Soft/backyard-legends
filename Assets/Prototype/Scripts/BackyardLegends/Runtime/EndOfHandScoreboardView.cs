using System.Collections;
using System.Collections.Generic;
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

        private const float PanelIntroSeconds = 0.42f;
        private const float CounterSeconds = 0.34f;
        private const float ValueStepDelay = 0.08f;

        private readonly List<CanvasGroup> animatedGroups = new();
        private Coroutine revealRoutine;
        private CanvasGroup rootGroup;
        private RectTransform rootRect;
        private Vector3 rootBaseScale = Vector3.one;
        private bool hasCachedRootScale;
        private ScoreAnimationValues animationValues;
        private bool hasAnimationValues;

        private struct ScoreAnimationValues
        {
            public int HomeBid;
            public int HomeBooks;
            public int HomeRoundDelta;
            public int HomeTotalStart;
            public int HomeTotalEnd;
            public int AwayBid;
            public int AwayBooks;
            public int AwayRoundDelta;
            public int AwayTotalStart;
            public int AwayTotalEnd;
        }

        private void Awake()
        {
            CacheRootMotion();
        }

        private void OnEnable()
        {
            if (!hasAnimationValues)
            {
                return;
            }

            if (revealRoutine != null)
            {
                StopCoroutine(revealRoutine);
            }

            revealRoutine = StartCoroutine(RevealRoutine());
        }

        private void OnDisable()
        {
            if (revealRoutine != null)
            {
                StopCoroutine(revealRoutine);
                revealRoutine = null;
            }

            RestoreRevealState();
        }

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
            CaptureAnimationValues(home, away);

            if (NextHandButton != null)
            {
                NextHandButton.gameObject.SetActive(!matchComplete);
            }

            if (PlayAgainButton != null)
            {
                PlayAgainButton.gameObject.SetActive(matchComplete);
            }
        }

        private void CaptureAnimationValues(ScoreSnapshot home, ScoreSnapshot away)
        {
            animationValues = new ScoreAnimationValues
            {
                HomeBid = home.ContractBid,
                HomeBooks = home.TricksWon,
                HomeRoundDelta = home.RoundDelta,
                HomeTotalStart = home.Score - home.RoundDelta - home.NilDelta,
                HomeTotalEnd = home.Score,
                AwayBid = away.ContractBid,
                AwayBooks = away.TricksWon,
                AwayRoundDelta = away.RoundDelta,
                AwayTotalStart = away.Score - away.RoundDelta - away.NilDelta,
                AwayTotalEnd = away.Score
            };
            hasAnimationValues = true;
        }

        private IEnumerator RevealRoutine()
        {
            CacheRootMotion();
            PrepareRevealState();
            yield return AnimatePanelIntro();

            yield return RevealGroup(ModeText, 0.04f, 1.03f);
            yield return RevealGroup(TitleText, 0.03f, 1.1f);
            yield return RevealGroup(HomeOutcomeText, 0.03f, 1.04f);
            yield return RevealGroup(AwayOutcomeText, 0.02f, 1.04f);
            RevealGraphic(HomeMadeOutcomeImage);
            RevealGraphic(HomeSetOutcomeImage);
            RevealGraphic(AwayMadeOutcomeImage);
            RevealGraphic(AwaySetOutcomeImage);

            yield return CountText(HomeBidText, 0, animationValues.HomeBid, false);
            yield return CountText(HomeBooksText, 0, animationValues.HomeBooks, false);
            yield return CountText(HomeRoundDeltaText, 0, animationValues.HomeRoundDelta, true);
            yield return CountText(HomeScoreText, 0, animationValues.HomeRoundDelta, true);
            yield return CountText(HomeTotalScoreText, animationValues.HomeTotalStart, animationValues.HomeTotalEnd, false);
            RevealGraphic(HomeHeroIcon);
            RevealGraphic(HomeResultIcon);
            yield return RevealGroup(HomeResultText, 0.04f, 1.04f);
            RevealGraphic(HomeMadeResultImage);
            RevealGraphic(HomeSetResultImage);

            yield return CountText(AwayBidText, 0, animationValues.AwayBid, false);
            yield return CountText(AwayBooksText, 0, animationValues.AwayBooks, false);
            yield return CountText(AwayRoundDeltaText, 0, animationValues.AwayRoundDelta, true);
            yield return CountText(AwayScoreText, 0, animationValues.AwayRoundDelta, true);
            yield return CountText(AwayTotalScoreText, animationValues.AwayTotalStart, animationValues.AwayTotalEnd, false);
            RevealGraphic(AwayHeroIcon);
            RevealGraphic(AwayResultIcon);
            yield return RevealGroup(AwayResultText, 0.04f, 1.04f);
            RevealGraphic(AwayMadeResultImage);
            RevealGraphic(AwaySetResultImage);

            yield return RevealGroup(FooterText, 0.03f, 1.02f);
            RevealButton(ViewHandButton);
            RevealButton(NextHandButton);
            RevealButton(PlayAgainButton);
            RevealButton(LeaveTableButton);
            RestoreRevealState();
            revealRoutine = null;
        }

        private void CacheRootMotion()
        {
            rootRect = transform as RectTransform;
            if (rootRect != null && !hasCachedRootScale)
            {
                rootBaseScale = rootRect.localScale;
                hasCachedRootScale = true;
            }

            rootGroup = GetComponent<CanvasGroup>();
            if (rootGroup == null)
            {
                rootGroup = gameObject.AddComponent<CanvasGroup>();
            }
        }

        private void PrepareRevealState()
        {
            animatedGroups.Clear();
            if (rootGroup != null)
            {
                rootGroup.alpha = 0f;
                rootGroup.interactable = false;
                rootGroup.blocksRaycasts = false;
            }

            if (rootRect != null)
            {
                rootRect.localScale = rootBaseScale * 0.86f;
                rootRect.localRotation = Quaternion.Euler(0f, 0f, -2.4f);
            }

            SetCounterTextsToStart();
            HideGroup(HomeBidText);
            HideGroup(HomeBooksText);
            HideGroup(HomeRoundDeltaText);
            HideGroup(HomeScoreText);
            HideGroup(HomeTotalScoreText);
            HideGroup(AwayBidText);
            HideGroup(AwayBooksText);
            HideGroup(AwayRoundDeltaText);
            HideGroup(AwayScoreText);
            HideGroup(AwayTotalScoreText);
            HideGroup(ModeText);
            HideGroup(TitleText);
            HideGroup(HomeOutcomeText);
            HideGroup(AwayOutcomeText);
            HideGroup(HomeResultText);
            HideGroup(AwayResultText);
            HideGroup(FooterText);
            HideGraphic(HomeMadeOutcomeImage);
            HideGraphic(HomeSetOutcomeImage);
            HideGraphic(AwayMadeOutcomeImage);
            HideGraphic(AwaySetOutcomeImage);
            HideGraphic(HomeHeroIcon);
            HideGraphic(AwayHeroIcon);
            HideGraphic(HomeResultIcon);
            HideGraphic(AwayResultIcon);
            HideGraphic(HomeMadeResultImage);
            HideGraphic(HomeSetResultImage);
            HideGraphic(AwayMadeResultImage);
            HideGraphic(AwaySetResultImage);
            HideButton(ViewHandButton);
            HideButton(NextHandButton);
            HideButton(PlayAgainButton);
            HideButton(LeaveTableButton);
        }

        private void SetCounterTextsToStart()
        {
            SetText(HomeBidText, "0");
            SetText(HomeBooksText, "0");
            SetText(HomeRoundDeltaText, FormatSigned(0));
            SetText(HomeScoreText, FormatSigned(0));
            SetText(HomeTotalScoreText, animationValues.HomeTotalStart.ToString());
            SetText(AwayBidText, "0");
            SetText(AwayBooksText, "0");
            SetText(AwayRoundDeltaText, FormatSigned(0));
            SetText(AwayScoreText, FormatSigned(0));
            SetText(AwayTotalScoreText, animationValues.AwayTotalStart.ToString());
        }

        private IEnumerator AnimatePanelIntro()
        {
            var elapsed = 0f;
            while (elapsed < PanelIntroSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / PanelIntroSeconds);
                var eased = EaseOutBack(t);
                if (rootGroup != null)
                {
                    rootGroup.alpha = Mathf.Lerp(0f, 1f, EaseOutCubic(t));
                }

                if (rootRect != null)
                {
                    rootRect.localScale = rootBaseScale * Mathf.LerpUnclamped(0.86f, 1f, eased);
                    rootRect.localRotation = Quaternion.Euler(0f, 0f, Mathf.LerpUnclamped(-2.4f, 0f, EaseOutCubic(t)));
                }

                yield return null;
            }

            if (rootGroup != null)
            {
                rootGroup.alpha = 1f;
            }

            if (rootRect != null)
            {
                rootRect.localScale = rootBaseScale;
                rootRect.localRotation = Quaternion.identity;
            }
        }

        private IEnumerator CountText(Text target, int from, int to, bool signed)
        {
            if (target == null)
            {
                yield break;
            }

            yield return RevealGroup(target, ValueStepDelay, 1.08f);
            var elapsed = 0f;
            while (elapsed < CounterSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = EaseOutCubic(Mathf.Clamp01(elapsed / CounterSeconds));
                var value = Mathf.RoundToInt(Mathf.Lerp(from, to, t));
                target.text = signed ? FormatSigned(value) : value.ToString();
                target.transform.localScale = Vector3.one * (1f + Mathf.Sin(t * Mathf.PI) * 0.08f);
                yield return null;
            }

            target.text = signed ? FormatSigned(to) : to.ToString();
            target.transform.localScale = Vector3.one;
            SpawnValueHit(target.rectTransform);
        }

        private IEnumerator RevealGroup(Graphic target, float delay, float peakScale)
        {
            if (target == null || !target.gameObject.activeInHierarchy)
            {
                yield break;
            }

            if (delay > 0f)
            {
                yield return new WaitForSecondsRealtime(delay);
            }

            var group = ResolveGroup(target.transform);
            group.alpha = 0f;
            var baseScale = target.transform.localScale;
            var elapsed = 0f;
            const float seconds = 0.18f;
            while (elapsed < seconds)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / seconds);
                group.alpha = EaseOutCubic(t);
                target.transform.localScale = baseScale * Mathf.LerpUnclamped(0.8f, peakScale, EaseOutBack(t));
                yield return null;
            }

            group.alpha = 1f;
            target.transform.localScale = baseScale;
        }

        private void RestoreRevealState()
        {
            if (rootGroup != null)
            {
                rootGroup.alpha = 1f;
                rootGroup.interactable = true;
                rootGroup.blocksRaycasts = true;
            }

            if (rootRect != null)
            {
                rootRect.localScale = rootBaseScale;
                rootRect.localRotation = Quaternion.identity;
            }

            foreach (var group in animatedGroups)
            {
                if (group != null)
                {
                    group.alpha = 1f;
                }
            }
        }

        private void HideGroup(Graphic target)
        {
            if (target == null || !target.gameObject.activeSelf)
            {
                return;
            }

            ResolveGroup(target.transform).alpha = 0f;
        }

        private void HideGraphic(Graphic target)
        {
            if (target == null || !target.gameObject.activeSelf)
            {
                return;
            }

            ResolveGroup(target.transform).alpha = 0f;
        }

        private void RevealGraphic(Graphic target)
        {
            if (target == null || !target.gameObject.activeInHierarchy)
            {
                return;
            }

            var group = ResolveGroup(target.transform);
            group.alpha = 1f;
            target.transform.localScale = Vector3.one;
            SpawnValueHit(target.rectTransform);
        }

        private void HideButton(Button button)
        {
            if (button == null || !button.gameObject.activeSelf)
            {
                return;
            }

            ResolveGroup(button.transform).alpha = 0f;
        }

        private void RevealButton(Button button)
        {
            if (button == null || !button.gameObject.activeInHierarchy)
            {
                return;
            }

            var group = ResolveGroup(button.transform);
            group.alpha = 1f;
            button.transform.localScale = Vector3.one;
        }

        private CanvasGroup ResolveGroup(Transform target)
        {
            var group = target.GetComponent<CanvasGroup>();
            if (group == null)
            {
                group = target.gameObject.AddComponent<CanvasGroup>();
            }

            if (!animatedGroups.Contains(group))
            {
                animatedGroups.Add(group);
            }

            return group;
        }

        private void SpawnValueHit(RectTransform target)
        {
            if (target == null || rootRect == null || !gameObject.activeInHierarchy)
            {
                return;
            }

            StartCoroutine(ValueHitRoutine(target));
        }

        private IEnumerator ValueHitRoutine(RectTransform target)
        {
            var burst = new GameObject("End Score Sticker Hit Runtime", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            burst.transform.SetParent(rootRect, false);
            var rect = burst.GetComponent<RectTransform>();
            var image = burst.GetComponent<Image>();
            var group = burst.GetComponent<CanvasGroup>();
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rootRect,
                RectTransformUtility.WorldToScreenPoint(null, target.TransformPoint(target.rect.center)),
                null,
                out var localPoint);
            rect.anchoredPosition = localPoint + new Vector2(Random.Range(-10f, 10f), Random.Range(-6f, 10f));
            rect.sizeDelta = new Vector2(Random.Range(18f, 28f), Random.Range(5f, 9f));
            rect.localRotation = Quaternion.Euler(0f, 0f, Random.Range(-18f, 18f));
            image.color = Random.value > 0.5f ? new Color(1f, 0.78f, 0.18f, 0.86f) : new Color(0.2f, 0.9f, 0.82f, 0.72f);
            image.raycastTarget = false;
            group.alpha = 0.95f;

            var elapsed = 0f;
            const float seconds = 0.28f;
            while (elapsed < seconds)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / seconds);
                rect.localScale = Vector3.one * Mathf.Lerp(0.55f, 1.55f, EaseOutCubic(t));
                group.alpha = Mathf.Lerp(0.95f, 0f, t);
                yield return null;
            }

            Destroy(burst);
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

        private static float EaseOutCubic(float t)
        {
            t = Mathf.Clamp01(t);
            return 1f - Mathf.Pow(1f - t, 3f);
        }

        private static float EaseOutBack(float t)
        {
            t = Mathf.Clamp01(t);
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
        }
    }
}
