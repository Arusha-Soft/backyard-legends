using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BackyardLegends.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BackyardLegends.Runtime
{
    public sealed class BackyardLegendsBootstrap : MonoBehaviour
    {
        [SerializeField] private BackyardLegendsSceneRefs sceneRefs;
        [SerializeField] private ThemeConfig themeOverride;

        private readonly Dictionary<SeatId, SeatPanelView> seatViews = new();
        private readonly Dictionary<SeatId, TrickSlotView> trickSlots = new();
        private readonly List<CardButtonView> handPool = new();
        private readonly Dictionary<CardButtonView, Coroutine> handAnimations = new();
        private readonly Dictionary<int, Button> bidButtons = new();
        private readonly Dictionary<SeatId, Coroutine> bidBubbleLoops = new();
        private readonly Queue<string> recentFeed = new();
        private readonly Queue<IEnumerator> queuedAnimations = new();
        private readonly HashSet<Card> lastRenderedHand = new();
        private readonly HashSet<SeatId> hiddenTrickSlots = new();
        private readonly Dictionary<SeatId, Card> resolvedTrickCards = new();
        private readonly List<CardButtonView> floatingCards = new();
        private readonly List<Graphic> transientFx = new();

        private ThemeConfig theme;
        private RuleSetDefinition selectedRule;
        private Card? selectedCard;
        private Coroutine aiLoop;
        private Coroutine animationQueueLoop;
        private Coroutine bannerLoop;
        private Coroutine flashLoop;
        private Coroutine homeDeltaLoop;
        private Coroutine awayDeltaLoop;
        private SpadesMatchController controller;
        private IRuleEngine ruleEngine;
        private BackyardLegendsSession session;
        private AudioSource feedbackAudioSource;
        private AudioClip bidClip;
        private AudioClip selectClip;
        private AudioClip playClip;
        private AudioClip collectClip;
        private AudioClip bannerClip;
        private bool pendingRoundSheetOpen;
        private bool pendingEndSheetOpen;
        private bool openingDealPending;
        private bool openingDealRunning;
        private bool suppressNextHandEntryAnimation;
        private bool exitPromptOpen;

        private RectTransform AnimationRoot => (RectTransform)transform;
        private bool HasVisualMotionPending => openingDealRunning || animationQueueLoop != null || queuedAnimations.Count > 0 || floatingCards.Count > 0;

        private enum FeedbackCue
        {
            Select,
            Bid,
            Play,
            Collect,
            Banner
        }

        private readonly struct CardMotionSnapshot
        {
            public CardMotionSnapshot(
                Card card,
                SeatId seat,
                Vector2 startPosition,
                Vector2 endPosition,
                Vector2 startSize,
                Vector2 endSize,
                Quaternion startRotation,
                Quaternion endRotation,
                float arcHeight,
                float delay = 0f)
            {
                Card = card;
                Seat = seat;
                StartPosition = startPosition;
                EndPosition = endPosition;
                StartSize = startSize;
                EndSize = endSize;
                StartRotation = startRotation;
                EndRotation = endRotation;
                ArcHeight = arcHeight;
                Delay = delay;
            }

            public Card Card { get; }
            public SeatId Seat { get; }
            public Vector2 StartPosition { get; }
            public Vector2 EndPosition { get; }
            public Vector2 StartSize { get; }
            public Vector2 EndSize { get; }
            public Quaternion StartRotation { get; }
            public Quaternion EndRotation { get; }
            public float ArcHeight { get; }
            public float Delay { get; }
        }

        private void Awake()
        {
            Screen.orientation = ScreenOrientation.Portrait;
            Application.targetFrameRate = 60;

            session = BackyardLegendsSession.GetOrCreateRuntimeInstance();
            theme = themeOverride != null ? themeOverride : session.Theme ?? ThemeConfig.CreateFallback();
            sceneRefs = sceneRefs != null ? sceneRefs : GetComponent<BackyardLegendsSceneRefs>();
            if (sceneRefs == null)
            {
                sceneRefs = FindFirstObjectByType<BackyardLegendsSceneRefs>();
            }

            if (sceneRefs == null)
            {
                Debug.LogError("Backyard Legends gameplay refs are missing. Rebuild the authored gameplay scene.");
                enabled = false;
                return;
            }

            EnsureRuntimeOpeningWidgets();
            EnsureRuntimeBackNavigationWidgets();
            EnsureRuntimeSeatCallouts();

            var handLayoutGroup = sceneRefs.HandContent != null ? sceneRefs.HandContent.GetComponent<LayoutGroup>() : null;
            if (handLayoutGroup != null)
            {
                handLayoutGroup.enabled = false;
            }

            CacheViewRefs();
            ConfigureFeedbackAudio();
            ConfigureUiCallbacks();
            ApplyTheme();
            StartConfiguredMatch();
        }

        private void EnsureRuntimeOpeningWidgets()
        {
            if (sceneRefs.TablePanel == null)
            {
                return;
            }

            if (sceneRefs.OpeningStackImage == null)
            {
                sceneRefs.OpeningStackImage = CreateRuntimePanel("Opening Stack Runtime", sceneRefs.TablePanel.transform, new Vector2(0.42f, 0.39f), new Vector2(0.58f, 0.60f));
            }

            if (sceneRefs.OpeningStackText == null && sceneRefs.OpeningStackImage != null)
            {
                sceneRefs.OpeningStackText = CreateRuntimeText("Opening Stack Label Runtime", sceneRefs.OpeningStackImage.transform, "52\nCARDS", 24, FontStyle.Bold, theme.gold, TextAnchor.MiddleCenter, new Vector2(0.18f, 0.18f), new Vector2(0.82f, 0.82f));
            }

            if (sceneRefs.DealButton == null)
            {
                sceneRefs.DealButton = CreateRuntimeButton("Deal Button Runtime", sceneRefs.TablePanel.transform, "DEAL", theme.green, new Vector2(0.34f, 0.25f), new Vector2(0.66f, 0.34f));
            }
        }

        private void EnsureRuntimeBackNavigationWidgets()
        {
            if (sceneRefs.HudPanel == null)
            {
                return;
            }

            if (sceneRefs.BackButton == null)
            {
                sceneRefs.BackButton = CreateRuntimeButton("Back Button Runtime", sceneRefs.HudPanel.transform, "BACK", theme.panelStroke, new Vector2(0.04f, 0.60f), new Vector2(0.18f, 0.92f));
            }

            var titleTransform = sceneRefs.HudPanel.transform.Find("Title") as RectTransform;
            if (titleTransform != null)
            {
                SetAnchors(titleTransform, new Vector2(0.21f, 0.58f), new Vector2(0.60f, 0.96f));
            }

            if (sceneRefs.ExitPromptOverlay == null)
            {
                var overlayGo = new GameObject("Exit Prompt Overlay Runtime", typeof(RectTransform), typeof(Image));
                overlayGo.transform.SetParent(transform, false);
                var overlayRect = overlayGo.GetComponent<RectTransform>();
                StretchToParent(overlayRect);
                var overlayImage = overlayGo.GetComponent<Image>();
                overlayImage.sprite = theme.softPanelSprite != null ? theme.softPanelSprite : ResolveSoftPanelSprite();
                overlayImage.type = Image.Type.Sliced;
                overlayImage.color = new Color(0f, 0f, 0f, 0.64f);
                overlayGo.SetActive(false);
                sceneRefs.ExitPromptOverlay = overlayRect;
                sceneRefs.ExitPromptOverlayImage = overlayImage;

                var panel = CreateRuntimePanel("Exit Prompt Panel Runtime", overlayGo.transform, new Vector2(0.12f, 0.36f), new Vector2(0.88f, 0.65f));
                panel.sprite = ResolveSheetSprite();
                panel.type = Image.Type.Sliced;
                panel.color = new Color(0.15f, 0.16f, 0.18f, 0.98f);
                sceneRefs.ExitPromptPanelImage = panel;
                sceneRefs.ExitPromptTitleText = CreateRuntimeText("Exit Prompt Title Runtime", panel.transform, "LEAVE THE TABLE?", 32, FontStyle.Bold, theme.gold, TextAnchor.UpperCenter, new Vector2(0.08f, 0.74f), new Vector2(0.92f, 0.94f));
                sceneRefs.ExitPromptBodyText = CreateRuntimeText("Exit Prompt Body Runtime", panel.transform, "Current match progress will be lost if you go back to the lobby.", 23, FontStyle.Normal, theme.primaryText, TextAnchor.UpperLeft, new Vector2(0.08f, 0.34f), new Vector2(0.92f, 0.68f));
                sceneRefs.ExitPromptCancelButton = CreateRuntimeButton("Exit Prompt Cancel Runtime", panel.transform, "STAY HERE", theme.green, new Vector2(0.08f, 0.08f), new Vector2(0.44f, 0.22f));
                sceneRefs.ExitPromptConfirmButton = CreateRuntimeButton("Exit Prompt Confirm Runtime", panel.transform, "GO TO LOBBY", theme.red, new Vector2(0.56f, 0.08f), new Vector2(0.92f, 0.22f));
            }

            if (sceneRefs.ExitPromptOverlay != null)
            {
                sceneRefs.ExitPromptOverlay.SetAsLastSibling();
            }
        }

        private void EnsureRuntimeSeatCallouts()
        {
            EnsureSeatCallout(sceneRefs.BottomSeat);
            EnsureSeatCallout(sceneRefs.LeftSeat);
            EnsureSeatCallout(sceneRefs.TopSeat);
            EnsureSeatCallout(sceneRefs.RightSeat);
        }

        private void EnsureSeatCallout(SeatPanelView view)
        {
            if (view == null)
            {
                return;
            }

            if (view.BidCalloutPanel != null && view.BidCalloutText != null && view.BidCalloutGroup != null)
            {
                return;
            }

            var bubble = new GameObject("Bid Callout Runtime", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            bubble.transform.SetParent(view.transform, false);
            var bubbleRect = bubble.GetComponent<RectTransform>();
            SetAnchors(bubbleRect, new Vector2(0.16f, 1.02f), new Vector2(0.84f, 1.32f));
            var bubbleImage = bubble.GetComponent<Image>();
            bubbleImage.sprite = theme.buttonSprite != null ? theme.buttonSprite : ResolveSoftPanelSprite();
            bubbleImage.type = Image.Type.Sliced;
            bubbleImage.color = new Color(0.15f, 0.16f, 0.18f, 0.96f);
            var bubbleGroup = bubble.GetComponent<CanvasGroup>();
            bubbleGroup.alpha = 0f;
            bubbleGroup.blocksRaycasts = false;
            bubbleGroup.interactable = false;
            view.BidCalloutPanel = bubbleImage;
            view.BidCalloutGroup = bubbleGroup;
            view.BidCalloutText = CreateRuntimeText("Label", bubble.transform, "I BID 3", 18, FontStyle.Bold, theme.primaryText, TextAnchor.MiddleCenter, new Vector2(0.08f, 0.12f), new Vector2(0.92f, 0.88f));
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (exitPromptOpen)
                {
                    CloseBackWarning();
                }
                else
                {
                    OpenBackWarning();
                }
            }

            if (controller == null || controller.State.RoundState == null)
            {
                return;
            }

            if (exitPromptOpen)
            {
                foreach (var pair in seatViews)
                {
                    pair.Value.Root.localScale = Vector3.Lerp(pair.Value.Root.localScale, Vector3.one, Time.unscaledDeltaTime * 8f);
                }

                return;
            }

            if (openingDealPending || openingDealRunning)
            {
                foreach (var pair in seatViews)
                {
                    pair.Value.Root.localScale = Vector3.Lerp(pair.Value.Root.localScale, Vector3.one, Time.unscaledDeltaTime * 8f);
                }

                return;
            }

            foreach (var pair in seatViews)
            {
                var isActive = controller.State.Phase is MatchPhase.Bidding or MatchPhase.TrickPlay &&
                               GetCurrentTurnSeat() == pair.Key;
                var scale = isActive
                    ? 1f + Mathf.Sin(Time.unscaledTime * 5f) * 0.02f * theme.activePulseScale
                    : 1f;
                pair.Value.Root.localScale = Vector3.Lerp(pair.Value.Root.localScale, Vector3.one * scale, Time.unscaledDeltaTime * 8f);
            }
        }

        private void CacheViewRefs()
        {
            seatViews.Clear();
            trickSlots.Clear();
            bidButtons.Clear();

            seatViews[SeatId.Bottom] = sceneRefs.BottomSeat;
            seatViews[SeatId.Left] = sceneRefs.LeftSeat;
            seatViews[SeatId.Top] = sceneRefs.TopSeat;
            seatViews[SeatId.Right] = sceneRefs.RightSeat;

            trickSlots[SeatId.Bottom] = sceneRefs.BottomTrick;
            trickSlots[SeatId.Left] = sceneRefs.LeftTrick;
            trickSlots[SeatId.Top] = sceneRefs.TopTrick;
            trickSlots[SeatId.Right] = sceneRefs.RightTrick;

            if (sceneRefs.BidButtons != null)
            {
                for (var bid = 0; bid < sceneRefs.BidButtons.Length; bid++)
                {
                    if (sceneRefs.BidButtons[bid] != null)
                    {
                        bidButtons[bid] = sceneRefs.BidButtons[bid];
                    }
                }
            }
        }

        private void ConfigureUiCallbacks()
        {
            sceneRefs.NextRoundButton.onClick.RemoveAllListeners();
            sceneRefs.NextRoundButton.onClick.AddListener(() =>
            {
                SetSheetVisible(sceneRefs.RoundSheet, false);
                lastRenderedHand.Clear();
                selectedCard = null;
                controller.StartNextRound();
                RenderAll();
                ScheduleAiLoop();
            });

            sceneRefs.RematchButton.onClick.RemoveAllListeners();
            sceneRefs.RematchButton.onClick.AddListener(StartConfiguredMatch);

            if (sceneRefs.ReturnToLobbyButton != null)
            {
                sceneRefs.ReturnToLobbyButton.onClick.RemoveAllListeners();
                sceneRefs.ReturnToLobbyButton.onClick.AddListener(OpenBackWarning);
            }

            if (sceneRefs.BackButton != null)
            {
                sceneRefs.BackButton.onClick.RemoveAllListeners();
                sceneRefs.BackButton.onClick.AddListener(OpenBackWarning);
            }

            if (sceneRefs.ExitPromptCancelButton != null)
            {
                sceneRefs.ExitPromptCancelButton.onClick.RemoveAllListeners();
                sceneRefs.ExitPromptCancelButton.onClick.AddListener(CloseBackWarning);
            }

            if (sceneRefs.ExitPromptConfirmButton != null)
            {
                sceneRefs.ExitPromptConfirmButton.onClick.RemoveAllListeners();
                sceneRefs.ExitPromptConfirmButton.onClick.AddListener(ConfirmReturnToLobby);
            }

            sceneRefs.PlaySelectedButton.onClick.RemoveAllListeners();
            sceneRefs.PlaySelectedButton.onClick.AddListener(OnPlaySelected);
            if (sceneRefs.DealButton != null)
            {
                sceneRefs.DealButton.onClick.RemoveAllListeners();
                sceneRefs.DealButton.onClick.AddListener(OnDealPressed);
            }

            foreach (var pair in bidButtons)
            {
                var localBid = pair.Key;
                pair.Value.onClick.RemoveAllListeners();
                pair.Value.onClick.AddListener(() => SubmitBid(localBid));
            }
        }

        private void ConfigureFeedbackAudio()
        {
            feedbackAudioSource = GetComponent<AudioSource>();
            if (feedbackAudioSource == null)
            {
                feedbackAudioSource = gameObject.AddComponent<AudioSource>();
            }

            feedbackAudioSource.playOnAwake = false;
            feedbackAudioSource.loop = false;
            feedbackAudioSource.spatialBlend = 0f;
            feedbackAudioSource.volume = 0.18f;

            bidClip = CreateToneClip("Bid Cue", 680f, 920f, 0.09f, 0.16f);
            selectClip = CreateToneClip("Select Cue", 520f, 760f, 0.05f, 0.13f);
            playClip = CreateToneClip("Play Cue", 430f, 700f, 0.07f, 0.17f);
            collectClip = CreateToneClip("Collect Cue", 360f, 580f, 0.13f, 0.2f);
            bannerClip = CreateToneClip("Banner Cue", 720f, 1080f, 0.16f, 0.14f);
        }

        private static AudioClip CreateToneClip(string clipName, float frequencyA, float frequencyB, float duration, float volume)
        {
            const int sampleRate = 22050;
            var sampleCount = Mathf.CeilToInt(duration * sampleRate);
            var samples = new float[sampleCount];
            for (var i = 0; i < sampleCount; i++)
            {
                var t = i / (float)sampleRate;
                var envelope = Mathf.Sin(Mathf.Clamp01(i / (float)sampleCount) * Mathf.PI);
                var main = Mathf.Sin(2f * Mathf.PI * frequencyA * t);
                var harmonic = Mathf.Sin(2f * Mathf.PI * frequencyB * t) * 0.38f;
                samples[i] = (main + harmonic) * envelope * volume;
            }

            var clip = AudioClip.Create(clipName, sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private void PlayFeedback(FeedbackCue cue, float volumeScale = 1f)
        {
            if (feedbackAudioSource == null)
            {
                return;
            }

            var clip = cue switch
            {
                FeedbackCue.Select => selectClip,
                FeedbackCue.Bid => bidClip,
                FeedbackCue.Play => playClip,
                FeedbackCue.Collect => collectClip,
                FeedbackCue.Banner => bannerClip,
                _ => null
            };

            if (clip != null)
            {
                feedbackAudioSource.PlayOneShot(clip, Mathf.Clamp(volumeScale, 0f, 1f));
            }
        }

        private void ApplyTheme()
        {
            ApplyThemeText(sceneRefs.StatusText, theme.mutedText, 18, FontStyle.Normal);
            ApplyThemeText(sceneRefs.HudModeText, theme.primaryText, 22, FontStyle.Bold);
            ApplyThemeText(sceneRefs.TimerHookText, theme.mutedText, 16, FontStyle.Bold);
            ApplyThemeText(sceneRefs.HomeScoreText, theme.backgroundColor, 17, FontStyle.Bold);
            ApplyThemeText(sceneRefs.AwayScoreText, theme.backgroundColor, 17, FontStyle.Bold);
            ApplyThemeText(sceneRefs.HomeDeltaText, theme.green, 18, FontStyle.Bold);
            ApplyThemeText(sceneRefs.AwayDeltaText, theme.red, 18, FontStyle.Bold);
            ApplyThemeText(sceneRefs.LastTrickText, theme.mutedText, 18, FontStyle.Normal);
            ApplyThemeText(sceneRefs.FeedText, theme.mutedText, 15, FontStyle.Normal);
            ApplyThemeText(sceneRefs.CenterHintText, theme.primaryText, 22, FontStyle.Bold);
            ApplyThemeText(sceneRefs.DeckAnchorText, theme.primaryText, 15, FontStyle.Bold);
            ApplyThemeText(sceneRefs.DiscardAnchorText, theme.primaryText, 15, FontStyle.Bold);
            ApplyThemeText(sceneRefs.OpeningStackText, theme.gold, 24, FontStyle.Bold);
            ApplyThemeText(sceneRefs.RoundSummaryText, theme.primaryText, 24, FontStyle.Normal);
            ApplyThemeText(sceneRefs.EndSummaryText, theme.primaryText, 24, FontStyle.Normal);
            ApplyThemeText(sceneRefs.BannerText, theme.gold, 32, FontStyle.Bold);
            ApplyThemeText(sceneRefs.ExitPromptTitleText, theme.gold, 32, FontStyle.Bold);
            ApplyThemeText(sceneRefs.ExitPromptBodyText, theme.primaryText, 23, FontStyle.Normal);

            sceneRefs.BackgroundImage.sprite = theme.tableBackgroundSprite != null
                ? theme.tableBackgroundSprite
                : ThemeSpriteFactory.CreateBackgroundSprite(theme.backgroundSecondary, theme.backgroundColor);
            sceneRefs.BackgroundImage.color = Color.white;
            ApplyThemedImage(sceneRefs.HudPanel, theme.panelColor, ResolvePanelSprite());
            ApplyThemedImage(sceneRefs.TablePanel, new Color(0.18f, 0.19f, 0.21f, 0.9f), ResolvePanelSprite());
            ApplyThemedImage(sceneRefs.HandPanel, theme.panelColor, ResolvePanelSprite());
            ApplyThemedImage(sceneRefs.FeedPanel, new Color(1f, 1f, 1f, 0.18f), ResolveSoftPanelSprite());
            ApplyThemedImage(sceneRefs.DeckAnchorImage, new Color(1f, 1f, 1f, 0.22f), ResolveSoftPanelSprite());
            ApplyThemedImage(sceneRefs.DiscardAnchorImage, new Color(1f, 1f, 1f, 0.22f), ResolveSoftPanelSprite());
            ApplyThemedImage(sceneRefs.OpeningStackImage, Color.white, ResolveCardBackSprite());
            var sheetTint = new Color(0.15f, 0.16f, 0.18f, 0.98f);
            ApplyThemedImage(sceneRefs.BidSheetImage, sheetTint, ResolveSheetSprite());
            ApplyThemedImage(sceneRefs.RoundSheetImage, sheetTint, ResolveSheetSprite());
            ApplyThemedImage(sceneRefs.EndSheetImage, sheetTint, ResolveSheetSprite());
            ApplyThemedImage(sceneRefs.ExitPromptOverlayImage, new Color(0f, 0f, 0f, 0.64f), ResolveSoftPanelSprite());
            ApplyThemedImage(sceneRefs.ExitPromptPanelImage, sheetTint, ResolveSheetSprite());

            ConfigureChipImage(sceneRefs.HomeScoreText, theme.green);
            ConfigureChipImage(sceneRefs.AwayScoreText, theme.red);

            foreach (var view in seatViews.Values)
            {
                if (view != null && view.Panel != null)
                {
                    view.Panel.sprite = ResolvePanelSprite();
                    view.Panel.type = Image.Type.Sliced;
                }

                if (view?.BidCalloutPanel != null)
                {
                    view.BidCalloutPanel.sprite = theme.buttonSprite != null ? theme.buttonSprite : ResolveSoftPanelSprite();
                    view.BidCalloutPanel.type = Image.Type.Sliced;
                    view.BidCalloutPanel.color = new Color(0.15f, 0.16f, 0.18f, 0.96f);
                }

                if (view?.BidCalloutText != null)
                {
                    ApplyThemeText(view.BidCalloutText, theme.primaryText, 18, FontStyle.Bold);
                }

                if (view?.BidCalloutGroup != null)
                {
                    view.BidCalloutGroup.alpha = 0f;
                }
            }

            foreach (var slot in trickSlots.Values)
            {
                if (slot != null && slot.Panel != null)
                {
                    slot.Panel.sprite = ResolveSoftPanelSprite();
                    slot.Panel.type = Image.Type.Sliced;
                    slot.Panel.color = Color.white;
                }
            }

            TintButton(sceneRefs.NextRoundButton, theme.green);
            TintButton(sceneRefs.RematchButton, theme.green);
            TintButton(sceneRefs.BackButton, theme.panelStroke);
            TintButton(sceneRefs.ReturnToLobbyButton, theme.panelStroke);
            TintButton(sceneRefs.DealButton, theme.green);
            TintButton(sceneRefs.PlaySelectedButton, theme.gold);
            TintButton(sceneRefs.ExitPromptCancelButton, theme.green);
            TintButton(sceneRefs.ExitPromptConfirmButton, theme.red);

            foreach (var pair in bidButtons)
            {
                TintButton(pair.Value, theme.panelStroke);
            }

            sceneRefs.HomeDeltaText.canvasRenderer.SetAlpha(0f);
            sceneRefs.AwayDeltaText.canvasRenderer.SetAlpha(0f);
            sceneRefs.BannerText.canvasRenderer.SetAlpha(0f);
        }

        private void StartConfiguredMatch()
        {
            if (controller != null)
            {
                controller.EventRaised -= OnMatchEvent;
            }

            ClearTransientMotionState(true);
            recentFeed.Clear();
            selectedCard = null;
            lastRenderedHand.Clear();
            HideAllBidBubbles(true);
            openingDealPending = true;
            openingDealRunning = false;
            suppressNextHandEntryAnimation = false;
            exitPromptOpen = false;
            selectedRule = session != null ? session.SelectedRule : RuleSetConfig.CreateClassic(100);
            ruleEngine = new SpadesRuleEngine();
            controller = new SpadesMatchController(
                selectedRule,
                ruleEngine,
                new Dictionary<SeatId, IAiAgent>
                {
                    { SeatId.Left, new SimpleAiAgent() },
                    { SeatId.Top, new SimpleAiAgent() },
                    { SeatId.Right, new SimpleAiAgent() }
                });

            controller.EventRaised += OnMatchEvent;
            controller.StartMatch();
            AddFeedMessage($"Match start: {selectedRule.DisplayName} to {selectedRule.TargetScore}.");
            SetSheetVisible(sceneRefs.BidSheet, false);
            SetSheetVisible(sceneRefs.RoundSheet, false);
            SetSheetVisible(sceneRefs.EndSheet, false);
            SetSheetVisible(sceneRefs.ExitPromptOverlay, false);
            ShowSeatCallout(SeatId.Top, "HEY PARTNER !", 999f, new Color(theme.green.r, theme.green.g, theme.green.b, 0.95f), theme.backgroundColor);
            RenderAll();
        }

        private void OnMatchEvent(SpadesMatchEvent matchEvent)
        {
            switch (matchEvent)
            {
                case MatchStartedEvent:
                    ClearTransientMotionState(false);
                    AddFeedMessage("Cards in the air. Gameplay scene took the table live.");
                    break;
                case RoundStartedEvent:
                    ClearTransientMotionState(false);
                    AddFeedMessage($"Round {controller.State.RoundState.RoundNumber} started. Dealer: {controller.State.SeatNames[controller.State.RoundState.Dealer]}.");
                    break;
                case BidSubmittedEvent bidEvent:
                    AddFeedMessage($"{controller.State.SeatNames[bidEvent.Seat]} called {(bidEvent.Bid == 0 ? "Nil" : bidEvent.Bid.ToString())}.");
                    ShowBidCallout(bidEvent.Seat, bidEvent.Bid);
                    PlayFeedback(FeedbackCue.Bid, 0.22f);
                    break;
                case CardPlayedEvent playedEvent:
                    AddFeedMessage($"{controller.State.SeatNames[playedEvent.Seat]} dropped {playedEvent.Card.ShortLabel}.");
                    QueueCardPlayAnimation(playedEvent);
                    break;
                case TrickResolvedEvent trickEvent:
                    AddFeedMessage($"{controller.State.SeatNames[trickEvent.Winner]} took the hand.");
                    QueueTrickCollectionAnimation(trickEvent);
                    break;
                case SetBookReachedEvent setBook:
                    ShowBanner(setBook.Team == TeamId.Home ? "HOME SET BOOK" : "RIVALS SET BOOK", setBook.Team == TeamId.Home ? theme.green : theme.red);
                    AddFeedMessage(setBook.Team == TeamId.Home ? "Home team hit its contract." : "Rivals hit their contract.");
                    break;
                case RoundScoredEvent:
                    sceneRefs.RoundSummaryText.text = BuildRoundSummaryText();
                    AddFeedMessage("Round scored and wrapped.");
                    if (HasVisualMotionPending)
                    {
                        pendingRoundSheetOpen = true;
                    }
                    else
                    {
                        AnimateScoreDelta(TeamId.Home);
                        AnimateScoreDelta(TeamId.Away);
                        SetSheetVisible(sceneRefs.RoundSheet, true);
                    }
                    break;
                case MatchEndedEvent ended:
                    sceneRefs.EndSummaryText.text = BuildMatchSummaryText(ended.WinningTeam);
                    AddFeedMessage(ended.WinningTeam == TeamId.Home ? "Home team closed the match." : "Rivals closed the match.");
                    pendingRoundSheetOpen = false;
                    if (HasVisualMotionPending)
                    {
                        pendingEndSheetOpen = true;
                    }
                    else
                    {
                        SetSheetVisible(sceneRefs.RoundSheet, false);
                        SetSheetVisible(sceneRefs.EndSheet, true);
                    }
                    break;
            }

            RenderAll();
            ScheduleAiLoop();
        }

        private void RenderAll()
        {
            if (controller == null || controller.State.RoundState == null)
            {
                return;
            }

            sceneRefs.HudModeText.text = $"{selectedRule.DisplayName.ToUpperInvariant()} | {selectedRule.TargetScore}";
            sceneRefs.TimerHookText.text = selectedRule.EnableFutureTurnTimer
                ? $"TURN CLOCK | RESERVED {selectedRule.ReservedTurnTimerSeconds}s"
                : $"TURN CLOCK | OFF IN PHASE 1 ({selectedRule.ReservedTurnTimerSeconds}s HOOK)";
            sceneRefs.StatusText.text = controller.State.RoundState.LastStatusMessage;
            sceneRefs.HomeScoreText.text = $"HOME {controller.State.Scores[TeamId.Home].Score} | BAGS {controller.State.Scores[TeamId.Home].Bags} | T {controller.GetTeamTricks(TeamId.Home)}";
            sceneRefs.AwayScoreText.text = $"AWAY {controller.State.Scores[TeamId.Away].Score} | BAGS {controller.State.Scores[TeamId.Away].Bags} | T {controller.GetTeamTricks(TeamId.Away)}";
            var liveCards = controller.State.RoundState.HandsBySeat.Values.Sum(cards => cards.Count) + controller.State.RoundState.TrickState.Plays.Count;
            var takenCards = controller.State.RoundState.CompletedTricks.Count * 4;
            sceneRefs.DeckAnchorText.text = $"DECK\n{liveCards} LIVE";
            sceneRefs.DiscardAnchorText.text = $"DISCARD\n{takenCards} TAKEN";
            sceneRefs.StatusText.text = openingDealPending
                ? openingDealRunning ? "Throwing the cards out." : "Cut the deck when you're ready."
                : controller.State.RoundState.LastStatusMessage;
            sceneRefs.CenterHintText.text = openingDealPending
                ? openingDealRunning ? "Cards are spreading across the table." : "Press DEAL to start the game."
                : controller.State.Phase switch
            {
                MatchPhase.Bidding => controller.State.RoundState.BidState.CurrentBidder == SeatId.Bottom ? "Tap a bid to lock your contract." : $"{controller.State.SeatNames[controller.State.RoundState.BidState.CurrentBidder]} is bidding.",
                MatchPhase.TrickPlay => controller.State.RoundState.TrickState.CurrentTurn == SeatId.Bottom ? "Tap a card once to select, tap again to play." : $"{controller.State.SeatNames[controller.State.RoundState.TrickState.CurrentTurn]} is on the clock.",
                MatchPhase.RoundSummary => "Round scored. Review the wrap and continue.",
                MatchPhase.MatchEnded => "Match complete. Run it back or head to the lobby.",
                _ => "Ready."
            };
            if (sceneRefs.DealButton != null)
            {
                sceneRefs.DealButton.gameObject.SetActive(openingDealPending && !openingDealRunning);
                sceneRefs.DealButton.interactable = openingDealPending && !openingDealRunning;
            }

            if (sceneRefs.OpeningStackImage != null)
            {
                sceneRefs.OpeningStackImage.gameObject.SetActive(openingDealPending || openingDealRunning);
            }

            sceneRefs.LastTrickText.text = $"Last hand: {controller.DescribeLastTrick()}";
            sceneRefs.FeedText.text = recentFeed.Count == 0 ? "TABLE FEED\nNo hands yet." : "TABLE FEED\n" + string.Join("\n", recentFeed.Reverse());

            UpdateCenterHintLayout();
            RenderSeatPanels();
            RenderTrickArea();
            RenderHand();
            RenderBidSheet();
        }

        private void RenderSeatPanels()
        {
            foreach (var seat in SpadesSeatUtility.TurnOrder)
            {
                var view = seatViews[seat];
                var bid = controller.State.RoundState.BidState.BidsBySeat[seat];
                var tricks = controller.State.RoundState.TricksWonBySeat[seat];
                view.NameText.text = controller.State.SeatNames[seat];
                view.BidText.text = bid.HasValue ? $"Bid: {(bid.Value == 0 ? "Nil" : bid.Value.ToString())}" : "Bid: --";
                view.TricksText.text = $"Books: {tricks}";
                view.StatusText.text = seat == controller.HumanSeat ? "Player" : "Rule-based AI";
                view.Panel.color = seat.ToTeam() == TeamId.Home
                    ? new Color(theme.panelColor.r, theme.panelColor.g + 0.02f, theme.panelColor.b, 0.97f)
                    : new Color(theme.panelColor.r + 0.03f, theme.panelColor.g, theme.panelColor.b, 0.97f);
            }
        }

        private void RenderTrickArea()
        {
            var activeCards = controller.State.RoundState.TrickState.Plays.Count > 0
                ? controller.State.RoundState.TrickState.Plays.ToDictionary(play => play.Seat, play => play.Card)
                : new Dictionary<SeatId, Card>(resolvedTrickCards);

            foreach (var seat in trickSlots.Keys)
            {
                var slot = trickSlots[seat];
                slot.RankText.text = "--";
                slot.SuitText.text = seat == SeatId.Bottom ? "YOU" :
                    seat == SeatId.Top ? "PARTNER" :
                    seat == SeatId.Left ? "LEFT" : "RIGHT";
                slot.SuitText.color = theme.mutedText;
                slot.Panel.color = new Color(1f, 1f, 1f, 0.28f);
            }

            foreach (var pair in activeCards)
            {
                if (hiddenTrickSlots.Contains(pair.Key))
                {
                    continue;
                }

                var slot = trickSlots[pair.Key];
                slot.RankText.text = pair.Value.RankLabel;
                slot.SuitText.text = pair.Value.SuitIcon;
                slot.SuitText.color = pair.Value.IsRed ? theme.red : theme.primaryText;
                slot.Panel.color = Color.white;
            }
        }

        private void RenderHand()
        {
            if (openingDealPending || openingDealRunning)
            {
                foreach (var view in handPool)
                {
                    StopHandAnimation(view);
                    view.gameObject.SetActive(false);
                }

                sceneRefs.PlaySelectedButton.interactable = false;
                return;
            }

            var hand = controller.GetHand(SeatId.Bottom).ToList();
            selectedCard = selectedCard.HasValue && hand.Contains(selectedCard.Value) ? selectedCard : null;
            var legalCards = controller.State.Phase == MatchPhase.TrickPlay && controller.State.RoundState.TrickState.CurrentTurn == SeatId.Bottom
                ? controller.GetLegalCardsForSeat(SeatId.Bottom).ToHashSet()
                : new HashSet<Card>();

            EnsureCardPoolSize(hand.Count);
            var previousHand = lastRenderedHand.ToHashSet();

            for (var index = 0; index < hand.Count; index++)
            {
                var card = hand[index];
                var view = handPool[index];
                var isLegal = legalCards.Contains(card);
                var isSelected = selectedCard.HasValue && selectedCard.Value.Equals(card);
                var targetPosition = GetFanTargetPosition(index, hand.Count, isSelected);
                var targetRotation = GetFanTargetRotation(index, hand.Count);
                var targetScale = isSelected ? theme.selectedCardScale : 1f;
                ConfigureCardView(view, card, isLegal, isSelected);
                view.gameObject.SetActive(true);
                view.Root.SetSiblingIndex(index);

                if (!previousHand.Contains(card))
                {
                    if (suppressNextHandEntryAnimation)
                    {
                        ApplyFanLayout(view, targetPosition, targetRotation, targetScale);
                    }
                    else
                    {
                        StartCardEntryAnimation(view, targetPosition, targetRotation, targetScale);
                    }
                }
                else
                {
                    StartHandLayoutAnimation(view, targetPosition, targetRotation, targetScale);
                }
            }

            for (var index = hand.Count; index < handPool.Count; index++)
            {
                StopHandAnimation(handPool[index]);
                handPool[index].gameObject.SetActive(false);
            }

            lastRenderedHand.Clear();
            foreach (var card in hand)
            {
                lastRenderedHand.Add(card);
            }

            suppressNextHandEntryAnimation = false;
            sceneRefs.PlaySelectedButton.interactable = selectedCard.HasValue && legalCards.Contains(selectedCard.Value);
        }

        private void EnsureCardPoolSize(int count)
        {
            while (handPool.Count < count)
            {
                var view = Instantiate(sceneRefs.CardButtonPrefab, sceneRefs.HandContent);
                view.gameObject.name = $"Card {handPool.Count + 1}";
                view.CanvasGroup = view.CanvasGroup != null ? view.CanvasGroup : view.GetComponent<CanvasGroup>();
                if (view.CanvasGroup == null)
                {
                    view.CanvasGroup = view.gameObject.AddComponent<CanvasGroup>();
                }

                view.gameObject.SetActive(false);
                handPool.Add(view);
            }
        }

        private void ConfigureCardView(CardButtonView view, Card card, bool isLegal, bool isSelected)
        {
            StopHandAnimation(view);
            view.gameObject.name = card.ShortLabel;
            view.RankText.font = theme.ResolveFont();
            view.SuitText.font = theme.ResolveFont();
            view.RankText.text = card.RankLabel;
            view.SuitText.text = card.SuitIcon;
            view.RankText.color = new Color(0.08f, 0.08f, 0.08f, 1f);
            view.SuitText.color = card.IsRed ? theme.red : new Color(0.07f, 0.07f, 0.08f, 1f);
            view.Panel.sprite = ResolveCardSprite(isSelected, isLegal);
            view.Panel.type = Image.Type.Sliced;
            view.Panel.color = Color.white;
            view.CanvasGroup.alpha = 1f;
            view.Button.onClick.RemoveAllListeners();
            view.Button.interactable = controller.State.Phase == MatchPhase.TrickPlay;
            view.Button.onClick.AddListener(() => OnCardTapped(card));
        }

        private void ApplyCardBackVisual(CardButtonView view)
        {
            view.Panel.sprite = ResolveCardBackSprite();
            view.Panel.type = Image.Type.Sliced;
            view.Panel.color = Color.white;
            var rankColor = view.RankText.color;
            rankColor.a = 0f;
            view.RankText.color = rankColor;
            var suitColor = view.SuitText.color;
            suitColor.a = 0f;
            view.SuitText.color = suitColor;
        }

        private void ApplyCardFaceVisual(CardButtonView view, Sprite faceSprite, Image.Type faceType, Color rankColor, Color suitColor)
        {
            view.Panel.sprite = faceSprite;
            view.Panel.type = faceType;
            view.Panel.color = Color.white;
            view.RankText.color = rankColor;
            view.SuitText.color = suitColor;
        }

        private void StartHandLayoutAnimation(CardButtonView view, Vector2 targetPosition, Quaternion targetRotation, float targetScale)
        {
            var angleDelta = Mathf.Abs(Mathf.DeltaAngle(view.Root.localRotation.eulerAngles.z, targetRotation.eulerAngles.z));
            if (Vector2.Distance(view.Root.anchoredPosition, targetPosition) < 1f &&
                angleDelta < 0.5f &&
                Mathf.Abs(view.Root.localScale.x - targetScale) < 0.01f)
            {
                ApplyFanLayout(view, targetPosition, targetRotation, targetScale);
                return;
            }

            StopHandAnimation(view);
            view.CanvasGroup.alpha = 1f;
            handAnimations[view] = StartCoroutine(CardTransformRoutine(
                view,
                targetPosition,
                targetRotation,
                targetScale,
                Mathf.Max(0.09f, theme.pulseDuration * 0.9f),
                1f,
                1f,
                false,
                null,
                Image.Type.Sliced,
                default,
                default));
        }

        private void StartCardEntryAnimation(CardButtonView view, Vector2 targetPosition, Quaternion targetRotation, float targetScale)
        {
            StopHandAnimation(view);
            var faceSprite = view.Panel.sprite;
            var faceType = view.Panel.type;
            var rankColor = view.RankText.color;
            var suitColor = view.SuitText.color;
            ApplyCardBackVisual(view);
            view.CanvasGroup.alpha = 0f;
            view.Root.anchoredPosition = GetDealEntryPosition() + new Vector2(Random.Range(-18f, 18f), Random.Range(-8f, 14f));
            view.Root.localRotation = Quaternion.Euler(0f, 0f, Random.Range(-14f, 14f));
            view.Root.localScale = Vector3.one * 0.84f;
            handAnimations[view] = StartCoroutine(CardTransformRoutine(
                view,
                targetPosition,
                targetRotation,
                targetScale,
                Mathf.Max(0.14f, theme.modalDuration * 1.05f),
                0f,
                1f,
                true,
                faceSprite,
                faceType,
                rankColor,
                suitColor));
        }

        private IEnumerator CardTransformRoutine(
            CardButtonView view,
            Vector2 targetPosition,
            Quaternion targetRotation,
            float targetScale,
            float duration,
            float startAlpha,
            float endAlpha,
            bool flipReveal,
            Sprite faceSprite,
            Image.Type faceType,
            Color rankColor,
            Color suitColor)
        {
            var elapsed = 0f;
            var startPosition = view.Root.anchoredPosition;
            var startRotation = view.Root.localRotation;
            var startScale = view.Root.localScale;
            var revealedFace = !flipReveal;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                var eased = 1f - Mathf.Pow(1f - t, 3f);
                var arc = Mathf.Sin(t * Mathf.PI) * Mathf.Lerp(4f, 18f, Mathf.Clamp01(Vector2.Distance(startPosition, targetPosition) / 180f));
                view.CanvasGroup.alpha = Mathf.Lerp(startAlpha, endAlpha, eased);
                view.Root.anchoredPosition = Vector2.Lerp(startPosition, targetPosition, eased) + Vector2.up * arc;
                view.Root.localRotation = Quaternion.Slerp(startRotation, targetRotation, eased);
                if (flipReveal && !revealedFace && t >= 0.55f)
                {
                    ApplyCardFaceVisual(view, faceSprite, faceType, rankColor, suitColor);
                    revealedFace = true;
                }

                var scale = Vector3.Lerp(startScale, Vector3.one * targetScale, eased);
                if (flipReveal)
                {
                    var flip = t < 0.5f
                        ? Mathf.Lerp(1f, 0.08f, t / 0.5f)
                        : Mathf.Lerp(0.08f, 1f, (t - 0.5f) / 0.5f);
                    scale.x *= flip;
                }

                view.Root.localScale = scale;
                yield return null;
            }

            view.CanvasGroup.alpha = endAlpha;
            if (flipReveal)
            {
                ApplyCardFaceVisual(view, faceSprite, faceType, rankColor, suitColor);
            }
            ApplyFanLayout(view, targetPosition, targetRotation, targetScale);
            handAnimations.Remove(view);
        }

        private void StopHandAnimation(CardButtonView view)
        {
            if (!handAnimations.TryGetValue(view, out var routine) || routine == null)
            {
                return;
            }

            StopCoroutine(routine);
            handAnimations.Remove(view);
        }

        private void RenderBidSheet()
        {
            if (openingDealPending || openingDealRunning)
            {
                SetSheetVisible(sceneRefs.BidSheet, false);
                return;
            }

            var shouldShow = controller.State.Phase == MatchPhase.Bidding &&
                             controller.State.RoundState.BidState.CurrentBidder == SeatId.Bottom;
            SetSheetVisible(sceneRefs.BidSheet, shouldShow);
            if (!shouldShow)
            {
                return;
            }

            var legal = controller.GetLegalBidsForSeat(SeatId.Bottom).ToHashSet();
            foreach (var pair in bidButtons)
            {
                pair.Value.interactable = legal.Contains(pair.Key);
                pair.Value.image.color = legal.Contains(pair.Key) ? theme.gold : theme.panelStroke;
            }
        }

        private void OnDealPressed()
        {
            if (exitPromptOpen || !openingDealPending || openingDealRunning || controller == null || controller.State.RoundState == null)
            {
                return;
            }

            if (aiLoop != null)
            {
                StopCoroutine(aiLoop);
                aiLoop = null;
            }

            openingDealRunning = true;
            HideAllBidBubbles(true);
            PlayFeedback(FeedbackCue.Banner, 0.16f);
            SpawnImpactBurst(GetOpeningStackPosition(), theme.gold, 38f, 5);
            RenderAll();
            StartCoroutine(OpeningDealSequence());
        }

        private IEnumerator OpeningDealSequence()
        {
            if (sceneRefs.OpeningStackImage != null)
            {
                yield return PulseRect(sceneRefs.OpeningStackImage.rectTransform, 1.08f, Mathf.Max(0.14f, theme.pulseDuration));
            }

            ShowSeatCallout(SeatId.Top, "HEY PARTNER !", 1.6f, new Color(theme.green.r, theme.green.g, theme.green.b, 0.95f), theme.backgroundColor);
            SpawnImpactBurst(GetAnchoredPoint(seatViews[SeatId.Top].Root, new Vector2(0.5f, 1.05f)), theme.green, 28f, 4);

            var handsBySeat = SpadesSeatUtility.TurnOrder.ToDictionary(seat => seat, seat => controller.GetHand(seat).ToList());
            var perSeatIndex = SpadesSeatUtility.TurnOrder.ToDictionary(seat => seat, _ => 0);
            var ghosts = new List<CardButtonView>(52);
            var delayStep = 0.018f;
            var travelDuration = Mathf.Max(0.22f, theme.modalDuration * 1.15f);
            var launchIndex = 0;

            foreach (var recipient in BuildOpeningDealOrder(controller.State.RoundState.Dealer))
            {
                var cardIndex = perSeatIndex[recipient];
                perSeatIndex[recipient] = cardIndex + 1;
                var card = handsBySeat[recipient][cardIndex];
                var motion = BuildOpeningDealMotion(recipient, card, cardIndex, handsBySeat[recipient].Count);
                var ghost = CreateFloatingCard(motion);
                ghosts.Add(ghost);
                StartCoroutine(AnimateOpeningDealGhost(ghost, motion, delayStep * launchIndex, recipient == SeatId.Bottom, travelDuration, launchIndex == 0));
                launchIndex++;
            }

            yield return new WaitForSecondsRealtime(delayStep * launchIndex + travelDuration + 0.2f);

            suppressNextHandEntryAnimation = true;
            openingDealPending = false;
            openingDealRunning = false;
            RenderAll();

            foreach (var ghost in ghosts)
            {
                CleanupFloatingCard(ghost);
            }

            PlayFeedback(FeedbackCue.Collect, 0.18f);
            SpawnImpactBurst(GetAnchoredPoint(sceneRefs.HandContent, new Vector2(0.5f, 0.55f)), theme.gold, 34f, 4);
            ScheduleAiLoop();
        }

        private IEnumerator AnimateOpeningDealGhost(CardButtonView ghost, CardMotionSnapshot motion, float delay, bool revealToHand, float duration, bool playLaunchSound)
        {
            if (ghost == null)
            {
                yield break;
            }

            if (delay > 0f)
            {
                yield return new WaitForSecondsRealtime(delay);
            }

            if (!revealToHand)
            {
                ApplyCardBackVisual(ghost);
            }

            if (playLaunchSound)
            {
                PlayFeedback(FeedbackCue.Play, 0.16f);
            }

            yield return AnimateFloatingCard(ghost, motion, duration, fadeOutNearEnd: !revealToHand, revealFromBack: revealToHand);
        }

        private void SubmitBid(int bid)
        {
            if (exitPromptOpen || controller == null || openingDealPending || openingDealRunning)
            {
                return;
            }

            if (!controller.TrySubmitBid(SeatId.Bottom, bid, out var error))
            {
                FlashStatus(error, theme.red);
                return;
            }

            SetSheetVisible(sceneRefs.BidSheet, false);
            RenderAll();
            ScheduleAiLoop();
        }

        private void OnCardTapped(Card card)
        {
            if (exitPromptOpen)
            {
                return;
            }

            if (openingDealPending || openingDealRunning)
            {
                FlashStatus("Press DEAL to start the table.", theme.gold);
                return;
            }

            if (controller == null || controller.State.Phase != MatchPhase.TrickPlay)
            {
                FlashStatus("Cards are not live yet.", theme.red);
                return;
            }

            if (controller.State.RoundState.TrickState.CurrentTurn != SeatId.Bottom)
            {
                FlashStatus("Wait for your turn.", theme.red);
                return;
            }

            var legalCards = controller.GetLegalCardsForSeat(SeatId.Bottom).ToHashSet();
            if (!legalCards.Contains(card))
            {
                FlashStatus("Classic enforces follow suit. Street relaxes it but still scores reneges.", theme.red);
                var invalidView = handPool.FirstOrDefault(view => view.gameObject.activeSelf && view.gameObject.name == card.ShortLabel);
                if (invalidView != null)
                {
                    StartCoroutine(Shake(invalidView.Root));
                }

                return;
            }

            if (!selectedCard.HasValue || !selectedCard.Value.Equals(card))
            {
                selectedCard = card;
                PlayFeedback(FeedbackCue.Select, 0.18f);
                var selectedView = handPool.FirstOrDefault(view => view.gameObject.activeSelf && view.gameObject.name == card.ShortLabel);
                if (selectedView != null)
                {
                    SpawnImpactBurst(GetAnchoredPoint(selectedView.Root, new Vector2(0.5f, 0.68f)), theme.gold, 24f, 3);
                }
                RenderHand();
                return;
            }

            TryPlaySelected();
        }

        private void OnPlaySelected()
        {
            if (exitPromptOpen)
            {
                return;
            }

            TryPlaySelected();
        }

        private void TryPlaySelected()
        {
            if (exitPromptOpen)
            {
                return;
            }

            if (openingDealPending || openingDealRunning)
            {
                FlashStatus("Press DEAL to start the table.", theme.gold);
                return;
            }

            if (!selectedCard.HasValue)
            {
                FlashStatus("Pick a card first.", theme.red);
                return;
            }

            if (!controller.TryPlayCard(SeatId.Bottom, selectedCard.Value, out var error))
            {
                FlashStatus(error, theme.red);
                return;
            }

            selectedCard = null;
            RenderAll();
            ScheduleAiLoop();
        }

        private void ScheduleAiLoop()
        {
            if (aiLoop != null)
            {
                StopCoroutine(aiLoop);
                aiLoop = null;
            }

            if (exitPromptOpen || openingDealPending || HasVisualMotionPending)
            {
                return;
            }

            if (controller != null && controller.NeedsAiTurn)
            {
                aiLoop = StartCoroutine(RunAiLoop());
            }
        }

        private IEnumerator RunAiLoop()
        {
            while (controller != null && controller.NeedsAiTurn)
            {
                if (exitPromptOpen)
                {
                    aiLoop = null;
                    yield break;
                }

                yield return new WaitForSecondsRealtime(0.55f);
                if (exitPromptOpen || controller == null || HasVisualMotionPending)
                {
                    aiLoop = null;
                    yield break;
                }

                controller.AdvanceAiTurn();
                RenderAll();
            }

            aiLoop = null;
        }

        private void QueueCardPlayAnimation(CardPlayedEvent playedEvent)
        {
            hiddenTrickSlots.Add(playedEvent.Seat);
            EnqueueAnimation(AnimateCardPlayRoutine(BuildCardPlayMotion(playedEvent.Seat, playedEvent.Card)));
        }

        private void QueueTrickCollectionAnimation(TrickResolvedEvent trickEvent)
        {
            resolvedTrickCards.Clear();
            foreach (var play in trickEvent.CompletedTrick)
            {
                resolvedTrickCards[play.Seat] = play.Card;
            }

            var motions = trickEvent.CompletedTrick
                .Select((play, index) => BuildTrickCollectMotion(play, trickEvent.Winner, index))
                .ToList();
            EnqueueAnimation(AnimateTrickCollectRoutine(trickEvent.Winner, motions));
        }

        private void EnqueueAnimation(IEnumerator animation)
        {
            if (animation == null)
            {
                return;
            }

            if (aiLoop != null)
            {
                StopCoroutine(aiLoop);
                aiLoop = null;
            }

            queuedAnimations.Enqueue(animation);
            if (animationQueueLoop == null)
            {
                animationQueueLoop = StartCoroutine(ProcessAnimationQueue());
            }
        }

        private IEnumerator ProcessAnimationQueue()
        {
            while (queuedAnimations.Count > 0)
            {
                yield return StartCoroutine(queuedAnimations.Dequeue());
            }

            animationQueueLoop = null;
            ApplyDeferredSheetState();
            RenderAll();
            ScheduleAiLoop();
        }

        private IEnumerator AnimateCardPlayRoutine(CardMotionSnapshot motion)
        {
            var ghost = CreateFloatingCard(motion);
            yield return AnimateFloatingCard(
                ghost,
                motion,
                Mathf.Max(0.18f, theme.modalDuration * 1.1f),
                fadeOutNearEnd: false,
                revealFromBack: motion.Seat != SeatId.Bottom);
            CleanupFloatingCard(ghost);
            hiddenTrickSlots.Remove(motion.Seat);
            RenderTrickArea();
            PlayFeedback(FeedbackCue.Play, 0.18f);
            SpawnImpactBurst(GetAnchoredPoint(trickSlots[motion.Seat].Root, new Vector2(0.5f, 0.5f)), motion.Card.IsRed ? theme.red : theme.gold, 32f, 4);
            yield return PulseRect(trickSlots[motion.Seat].Root, 1.06f, Mathf.Max(0.12f, theme.pulseDuration * 0.75f));
        }

        private IEnumerator AnimateTrickCollectRoutine(SeatId winner, IReadOnlyList<CardMotionSnapshot> motions)
        {
            if (motions == null || motions.Count == 0)
            {
                yield break;
            }

            yield return new WaitForSecondsRealtime(0.18f);

            var ghosts = new List<CardButtonView>(motions.Count);
            foreach (var motion in motions)
            {
                hiddenTrickSlots.Add(motion.Seat);
                ghosts.Add(CreateFloatingCard(motion));
            }

            RenderTrickArea();

            var duration = Mathf.Max(0.2f, theme.modalDuration * 1.15f);
            var maxDelay = motions.Max(motion => motion.Delay);
            var elapsed = 0f;
            while (elapsed < duration + maxDelay)
            {
                elapsed += Time.unscaledDeltaTime;
                for (var index = 0; index < motions.Count; index++)
                {
                    var ghost = ghosts[index];
                    if (ghost == null)
                    {
                        continue;
                    }

                    var motion = motions[index];
                    var localTime = Mathf.Clamp01((elapsed - motion.Delay) / duration);
                    if (localTime <= 0f)
                    {
                        continue;
                    }

                    ApplyFloatingCardPose(ghost, motion, localTime, fadeOutNearEnd: true, revealFromBack: false);
                }

                yield return null;
            }

            foreach (var ghost in ghosts)
            {
                CleanupFloatingCard(ghost);
            }

            hiddenTrickSlots.Clear();
            resolvedTrickCards.Clear();
            RenderTrickArea();
            PlayFeedback(FeedbackCue.Collect, 0.22f);
            SpawnImpactBurst(GetAnchoredPoint(seatViews[winner].Root, GetSeatInnerAnchor(winner)), winner.ToTeam() == TeamId.Home ? theme.green : theme.red, 42f, 5);
            SpawnImpactBurst(GetAnchoredPoint(sceneRefs.DiscardAnchorImage.rectTransform, new Vector2(0.5f, 0.5f)), theme.gold, 28f, 3);
            yield return PulseRect(sceneRefs.DiscardAnchorImage.rectTransform, 1.07f, Mathf.Max(0.12f, theme.pulseDuration * 0.8f));
            yield return PulseRect(seatViews[winner].Root, 1.08f, Mathf.Max(0.14f, theme.pulseDuration));
        }

        private IEnumerator AnimateFloatingCard(CardButtonView ghost, CardMotionSnapshot motion, float duration, bool fadeOutNearEnd, bool revealFromBack)
        {
            var faceSprite = ghost.Panel.sprite;
            var faceType = ghost.Panel.type;
            var rankColor = ghost.RankText.color;
            var suitColor = ghost.SuitText.color;
            var revealedFace = !revealFromBack;
            if (revealFromBack)
            {
                ApplyCardBackVisual(ghost);
            }

            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                if (revealFromBack && !revealedFace && t >= 0.48f)
                {
                    ApplyCardFaceVisual(ghost, faceSprite, faceType, rankColor, suitColor);
                    revealedFace = true;
                }

                ApplyFloatingCardPose(ghost, motion, t, fadeOutNearEnd, revealFromBack);
                yield return null;
            }

            if (revealFromBack)
            {
                ApplyCardFaceVisual(ghost, faceSprite, faceType, rankColor, suitColor);
            }

            ApplyFloatingCardPose(ghost, motion, 1f, fadeOutNearEnd, false);
        }

        private void ApplyFloatingCardPose(CardButtonView ghost, CardMotionSnapshot motion, float progress, bool fadeOutNearEnd, bool revealFromBack)
        {
            if (ghost == null)
            {
                return;
            }

            var eased = 1f - Mathf.Pow(1f - progress, 3f);
            var arc = Mathf.Sin(progress * Mathf.PI) * motion.ArcHeight;
            ghost.Root.anchoredPosition = Vector2.Lerp(motion.StartPosition, motion.EndPosition, eased) + Vector2.up * arc;
            ghost.Root.sizeDelta = Vector2.Lerp(motion.StartSize, motion.EndSize, eased);
            ghost.Root.localRotation = Quaternion.Slerp(motion.StartRotation, motion.EndRotation, eased);
            ghost.Root.localScale = revealFromBack
                ? new Vector3(progress < 0.5f
                    ? Mathf.Lerp(1f, 0.08f, progress / 0.5f)
                    : Mathf.Lerp(0.08f, 1f, (progress - 0.5f) / 0.5f), 1f, 1f)
                : Vector3.one;
            ghost.CanvasGroup.alpha = fadeOutNearEnd
                ? Mathf.Lerp(1f, 0f, Mathf.Clamp01((progress - 0.68f) / 0.32f))
                : Mathf.Lerp(0.92f, 1f, eased);
        }

        private CardMotionSnapshot BuildCardPlayMotion(SeatId seat, Card card)
        {
            var sourceRect = ResolvePlaySourceRect(seat, card);
            var sourceAnchor = sourceRect == seatViews[seat].Root ? GetSeatInnerAnchor(seat) : new Vector2(0.5f, 0.5f);
            var targetRect = trickSlots[seat].Root;
            var sourceSize = sourceRect == seatViews[seat].Root
                ? GetAnchoredSize(targetRect) * 0.92f
                : GetAnchoredSize(sourceRect);
            return new CardMotionSnapshot(
                card,
                seat,
                GetAnchoredPoint(sourceRect, sourceAnchor),
                GetAnchoredPoint(targetRect, new Vector2(0.5f, 0.5f)),
                sourceSize,
                GetAnchoredSize(targetRect),
                Quaternion.Euler(0f, 0f, sourceRect.eulerAngles.z),
                Quaternion.identity,
                seat == SeatId.Bottom ? 96f : 72f);
        }

        private CardMotionSnapshot BuildTrickCollectMotion(TrickPlay play, SeatId winner, int index)
        {
            var trickRect = trickSlots[play.Seat].Root;
            var winnerRect = seatViews[winner].Root;
            return new CardMotionSnapshot(
                play.Card,
                play.Seat,
                GetAnchoredPoint(trickRect, new Vector2(0.5f, 0.5f)),
                GetAnchoredPoint(winnerRect, GetSeatInnerAnchor(winner)),
                GetAnchoredSize(trickRect),
                GetAnchoredSize(trickRect) * 0.54f,
                Quaternion.identity,
                Quaternion.Euler(0f, 0f, winner == SeatId.Left ? -10f : winner == SeatId.Right ? 10f : 0f),
                54f,
                index * 0.04f);
        }

        private IEnumerable<SeatId> BuildOpeningDealOrder(SeatId dealer)
        {
            var recipient = dealer.NextClockwise();
            for (var round = 0; round < 13; round++)
            {
                for (var count = 0; count < 4; count++)
                {
                    yield return recipient;
                    recipient = recipient.NextClockwise();
                }
            }
        }

        private CardMotionSnapshot BuildOpeningDealMotion(SeatId seat, Card card, int index, int count)
        {
            var startPosition = GetOpeningStackPosition() + new Vector2(Random.Range(-12f, 12f), Random.Range(-10f, 10f));
            var startSize = sceneRefs.CardButtonPrefab != null ? sceneRefs.CardButtonPrefab.Root.sizeDelta * 0.82f : new Vector2(82f, 116f);
            var endSize = seat == SeatId.Bottom
                ? (sceneRefs.CardButtonPrefab != null ? sceneRefs.CardButtonPrefab.Root.sizeDelta : new Vector2(82f, 116f))
                : (sceneRefs.CardButtonPrefab != null ? sceneRefs.CardButtonPrefab.Root.sizeDelta * 0.52f : new Vector2(46f, 64f));
            var endRotation = seat == SeatId.Bottom
                ? GetFanTargetRotation(index, count)
                : Quaternion.Euler(0f, 0f, seat == SeatId.Left ? -84f : seat == SeatId.Right ? 84f : 0f);
            return new CardMotionSnapshot(
                card,
                seat,
                startPosition,
                seat == SeatId.Bottom ? GetHandAnimationPoint(index, count, false) : GetSeatDealPoint(seat, index, count),
                startSize,
                endSize,
                Quaternion.Euler(0f, 0f, Random.Range(-18f, 18f)),
                endRotation,
                seat == SeatId.Bottom ? 78f : 58f);
        }

        private RectTransform ResolvePlaySourceRect(SeatId seat, Card card)
        {
            if (seat == SeatId.Bottom)
            {
                var handView = handPool.FirstOrDefault(view => view.gameObject.activeSelf && view.gameObject.name == card.ShortLabel);
                if (handView != null)
                {
                    return handView.Root;
                }
            }

            return seatViews[seat].Root;
        }

        private CardButtonView CreateFloatingCard(CardMotionSnapshot motion)
        {
            var ghost = Instantiate(sceneRefs.CardButtonPrefab, AnimationRoot);
            ghost.gameObject.name = $"Motion {motion.Card.ShortLabel}";
            ghost.transform.SetAsLastSibling();
            ghost.Button.onClick.RemoveAllListeners();
            ghost.Button.enabled = false;
            ghost.CanvasGroup.blocksRaycasts = false;
            ghost.Root.anchorMin = new Vector2(0.5f, 0.5f);
            ghost.Root.anchorMax = new Vector2(0.5f, 0.5f);
            ghost.Root.pivot = new Vector2(0.5f, 0.5f);
            ghost.Root.anchoredPosition = motion.StartPosition;
            ghost.Root.sizeDelta = motion.StartSize;
            ghost.Root.localRotation = motion.StartRotation;
            ghost.Root.localScale = Vector3.one;
            ghost.CanvasGroup.alpha = 1f;
            ConfigureFloatingCardView(ghost, motion.Card);
            floatingCards.Add(ghost);
            return ghost;
        }

        private void ConfigureFloatingCardView(CardButtonView view, Card card)
        {
            view.RankText.font = theme.ResolveFont();
            view.SuitText.font = theme.ResolveFont();
            view.RankText.text = card.RankLabel;
            view.SuitText.text = card.SuitIcon;
            view.RankText.color = new Color(0.08f, 0.08f, 0.08f, 1f);
            view.SuitText.color = card.IsRed ? theme.red : new Color(0.07f, 0.07f, 0.08f, 1f);
            view.Panel.sprite = theme.cardFaceDefaultSprite != null ? theme.cardFaceDefaultSprite : ResolveCardSprite(false, true);
            view.Panel.type = Image.Type.Sliced;
            view.Panel.color = Color.white;
        }

        private void CleanupFloatingCard(CardButtonView ghost)
        {
            if (ghost == null)
            {
                return;
            }

            floatingCards.Remove(ghost);
            Destroy(ghost.gameObject);
        }

        private void ClearFloatingCards()
        {
            foreach (var ghost in floatingCards.ToArray())
            {
                if (ghost != null)
                {
                    Destroy(ghost.gameObject);
                }
            }

            floatingCards.Clear();
        }

        private void ClearTransientFx()
        {
            foreach (var fx in transientFx.ToArray())
            {
                if (fx != null)
                {
                    Destroy(fx.gameObject);
                }
            }

            transientFx.Clear();
        }

        private void ClearTransientMotionState(bool stopQueue)
        {
            hiddenTrickSlots.Clear();
            resolvedTrickCards.Clear();
            pendingRoundSheetOpen = false;
            pendingEndSheetOpen = false;
            ClearFloatingCards();
            ClearTransientFx();
            HideAllBidBubbles(true);

            if (!stopQueue)
            {
                return;
            }

            queuedAnimations.Clear();
            if (animationQueueLoop != null)
            {
                StopCoroutine(animationQueueLoop);
                animationQueueLoop = null;
            }
        }

        private void ApplyDeferredSheetState()
        {
            if (pendingEndSheetOpen)
            {
                pendingEndSheetOpen = false;
                pendingRoundSheetOpen = false;
                SetSheetVisible(sceneRefs.RoundSheet, false);
                SetSheetVisible(sceneRefs.EndSheet, true);
                return;
            }

            if (!pendingRoundSheetOpen)
            {
                return;
            }

            pendingRoundSheetOpen = false;
            AnimateScoreDelta(TeamId.Home);
            AnimateScoreDelta(TeamId.Away);
            SetSheetVisible(sceneRefs.RoundSheet, true);
        }

        private void ShowBidCallout(SeatId seat, int bid)
        {
            ShowSeatCallout(seat, bid == 0 ? "I BID NIL" : $"I BID {bid}", 1.3f, new Color(0.15f, 0.16f, 0.18f, 0.96f), theme.primaryText);
        }

        private void ShowSeatCallout(SeatId seat, string text, float holdSeconds, Color panelColor, Color textColor)
        {
            if (!seatViews.TryGetValue(seat, out var view) || view?.BidCalloutGroup == null || view.BidCalloutText == null || view.BidCalloutPanel == null)
            {
                return;
            }

            if (bidBubbleLoops.TryGetValue(seat, out var runningLoop) && runningLoop != null)
            {
                StopCoroutine(runningLoop);
            }

            view.BidCalloutPanel.color = panelColor;
            view.BidCalloutText.color = textColor;
            view.BidCalloutText.text = text;
            SpawnImpactBurst(GetAnchoredPoint(view.BidCalloutGroup.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f)), textColor, 24f, 3);
            bidBubbleLoops[seat] = StartCoroutine(BidCalloutRoutine(view, seat, holdSeconds));
        }

        private IEnumerator BidCalloutRoutine(SeatPanelView view, SeatId seat, float holdSeconds)
        {
            view.BidCalloutGroup.alpha = 0f;
            yield return PulseRect(view.BidCalloutGroup.GetComponent<RectTransform>(), 1.05f, Mathf.Max(0.1f, theme.pulseDuration * 0.85f));
            var elapsed = 0f;
            var fadeIn = Mathf.Max(0.08f, theme.pulseDuration * 0.45f);
            while (elapsed < fadeIn)
            {
                elapsed += Time.unscaledDeltaTime;
                view.BidCalloutGroup.alpha = Mathf.Clamp01(elapsed / fadeIn);
                yield return null;
            }

            view.BidCalloutGroup.alpha = 1f;
            yield return new WaitForSecondsRealtime(Mathf.Max(0.05f, holdSeconds));
            elapsed = 0f;
            var fadeOut = Mathf.Max(0.18f, theme.modalDuration * 0.9f);
            while (elapsed < fadeOut)
            {
                elapsed += Time.unscaledDeltaTime;
                view.BidCalloutGroup.alpha = 1f - Mathf.Clamp01(elapsed / fadeOut);
                yield return null;
            }

            view.BidCalloutGroup.alpha = 0f;
            ResetCalloutVisual(view);
            bidBubbleLoops.Remove(seat);
        }

        private void HideAllBidBubbles(bool immediate)
        {
            foreach (var pair in bidBubbleLoops.ToArray())
            {
                if (pair.Value != null)
                {
                    StopCoroutine(pair.Value);
                }
            }

            bidBubbleLoops.Clear();
            foreach (var view in seatViews.Values)
            {
                if (view?.BidCalloutGroup == null)
                {
                    continue;
                }

                if (immediate)
                {
                    view.BidCalloutGroup.alpha = 0f;
                }

                ResetCalloutVisual(view);
            }
        }

        private void ResetCalloutVisual(SeatPanelView view)
        {
            if (view?.BidCalloutPanel != null)
            {
                view.BidCalloutPanel.color = new Color(0.15f, 0.16f, 0.18f, 0.96f);
            }

            if (view?.BidCalloutText != null)
            {
                view.BidCalloutText.color = theme.primaryText;
            }
        }

        private void SpawnImpactBurst(Vector2 anchoredPosition, Color color, float size, int pieces)
        {
            StartCoroutine(ImpactBurstRoutine(anchoredPosition, color, size, pieces));
        }

        private IEnumerator ImpactBurstRoutine(Vector2 anchoredPosition, Color color, float size, int pieces)
        {
            var sprites = ResolveFxSprite();
            var localPieces = new List<Graphic>(pieces);
            for (var index = 0; index < pieces; index++)
            {
                var fxGo = new GameObject($"Impact Fx {index}", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
                fxGo.transform.SetParent(AnimationRoot, false);
                fxGo.transform.SetAsLastSibling();
                var rect = fxGo.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = anchoredPosition;
                rect.sizeDelta = Vector2.one * Mathf.Lerp(size * 0.38f, size * 0.58f, index / Mathf.Max(1f, pieces - 1f));
                rect.localRotation = Quaternion.Euler(0f, 0f, 45f + index * (180f / Mathf.Max(1, pieces)));
                var image = fxGo.GetComponent<Image>();
                image.sprite = sprites;
                image.type = Image.Type.Sliced;
                image.color = new Color(color.r, color.g, color.b, 0.88f);
                transientFx.Add(image);
                localPieces.Add(image);
            }

            var elapsed = 0f;
            var duration = Mathf.Max(0.16f, theme.pulseDuration * 0.8f);
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                var eased = 1f - Mathf.Pow(1f - t, 2f);
                for (var index = 0; index < localPieces.Count; index++)
                {
                    if (localPieces[index] == null)
                    {
                        continue;
                    }

                    var rect = (RectTransform)localPieces[index].transform;
                    var angle = (360f / Mathf.Max(1, localPieces.Count)) * index;
                    var offset = (Vector2)(Quaternion.Euler(0f, 0f, angle) * Vector3.right) * Mathf.Lerp(8f, size * 0.45f, eased);
                    rect.anchoredPosition = anchoredPosition + offset;
                    rect.localScale = Vector3.one * Mathf.Lerp(0.42f, 1.08f, eased);
                    localPieces[index].color = new Color(color.r, color.g, color.b, Mathf.Lerp(0.9f, 0f, t));
                }

                yield return null;
            }

            foreach (var fx in localPieces)
            {
                if (fx != null)
                {
                    transientFx.Remove(fx);
                    Destroy(fx.gameObject);
                }
            }
        }

        private IEnumerator PulseRect(RectTransform rectTransform, float peakScale, float duration)
        {
            if (rectTransform == null)
            {
                yield break;
            }

            var elapsed = 0f;
            var startScale = rectTransform.localScale;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                var wave = Mathf.Sin(t * Mathf.PI);
                rectTransform.localScale = Vector3.Lerp(startScale, startScale * peakScale, wave);
                yield return null;
            }

            rectTransform.localScale = startScale;
        }

        private Vector2 GetDealEntryPosition()
        {
            return sceneRefs.DeckAnchorImage != null
                ? GetAnchoredPoint(sceneRefs.DeckAnchorImage.rectTransform, new Vector2(0.5f, 0.5f))
                : GetAnchoredPoint(sceneRefs.TablePanel.rectTransform, new Vector2(0.5f, 0.46f));
        }

        private Vector2 GetOpeningStackPosition()
        {
            return sceneRefs.OpeningStackImage != null
                ? GetAnchoredPoint(sceneRefs.OpeningStackImage.rectTransform, new Vector2(0.5f, 0.5f))
                : GetAnchoredPoint(sceneRefs.TablePanel.rectTransform, new Vector2(0.5f, 0.5f));
        }

        private Vector2 GetHandAnimationPoint(int index, int count, bool isSelected)
        {
            var localTarget = GetFanTargetPosition(index, count, isSelected);
            var worldPoint = sceneRefs.HandContent.TransformPoint(localTarget);
            return WorldToAnimationPoint(worldPoint);
        }

        private Vector2 GetSeatDealPoint(SeatId seat, int index, int count)
        {
            var spreadIndex = index - (count - 1) * 0.5f;
            var offset = seat switch
            {
                SeatId.Top => new Vector2(spreadIndex * 10f, 0f),
                SeatId.Left => new Vector2(0f, -spreadIndex * 8f),
                SeatId.Right => new Vector2(0f, spreadIndex * 8f),
                _ => Vector2.zero
            };
            return GetAnchoredPointWithOffset(seatViews[seat].Root, GetSeatInnerAnchor(seat), offset);
        }

        private Vector2 GetAnchoredPoint(RectTransform rectTransform, Vector2 normalizedPoint)
        {
            var worldPoint = rectTransform.TransformPoint(new Vector3(
                Mathf.Lerp(rectTransform.rect.xMin, rectTransform.rect.xMax, normalizedPoint.x),
                Mathf.Lerp(rectTransform.rect.yMin, rectTransform.rect.yMax, normalizedPoint.y),
                0f));
            return WorldToAnimationPoint(worldPoint);
        }

        private Vector2 GetAnchoredPointWithOffset(RectTransform rectTransform, Vector2 normalizedPoint, Vector2 localOffset)
        {
            var localPoint = new Vector3(
                Mathf.Lerp(rectTransform.rect.xMin, rectTransform.rect.xMax, normalizedPoint.x) + localOffset.x,
                Mathf.Lerp(rectTransform.rect.yMin, rectTransform.rect.yMax, normalizedPoint.y) + localOffset.y,
                0f);
            return WorldToAnimationPoint(rectTransform.TransformPoint(localPoint));
        }

        private Vector2 GetAnchoredSize(RectTransform rectTransform)
        {
            var corners = new Vector3[4];
            rectTransform.GetWorldCorners(corners);
            var bottomLeft = WorldToAnimationPoint(corners[0]);
            var topRight = WorldToAnimationPoint(corners[2]);
            return new Vector2(Mathf.Abs(topRight.x - bottomLeft.x), Mathf.Abs(topRight.y - bottomLeft.y));
        }

        private Vector2 WorldToAnimationPoint(Vector3 worldPoint)
        {
            var screenPoint = RectTransformUtility.WorldToScreenPoint(null, worldPoint);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(AnimationRoot, screenPoint, null, out var localPoint);
            return localPoint;
        }

        private static Vector2 GetSeatInnerAnchor(SeatId seat)
        {
            return seat switch
            {
                SeatId.Bottom => new Vector2(0.5f, 0.82f),
                SeatId.Top => new Vector2(0.5f, 0.18f),
                SeatId.Left => new Vector2(0.8f, 0.5f),
                SeatId.Right => new Vector2(0.2f, 0.5f),
                _ => new Vector2(0.5f, 0.5f)
            };
        }

        private void FlashStatus(string message, Color color)
        {
            sceneRefs.StatusText.text = message;
            AddFeedMessage(message);
            if (flashLoop != null)
            {
                StopCoroutine(flashLoop);
            }

            flashLoop = StartCoroutine(FlashStatusColor(color));
        }

        private void ShowBanner(string message, Color color)
        {
            if (bannerLoop != null)
            {
                StopCoroutine(bannerLoop);
            }

            PlayFeedback(FeedbackCue.Banner, 0.24f);
            SpawnImpactBurst(GetAnchoredPoint(sceneRefs.BannerText.rectTransform, new Vector2(0.5f, 0.5f)), color, 42f, 5);
            bannerLoop = StartCoroutine(BannerRoutine(message, color));
        }

        private IEnumerator FlashStatusColor(Color color)
        {
            sceneRefs.StatusText.color = color;
            yield return new WaitForSecondsRealtime(0.9f);
            sceneRefs.StatusText.color = theme.mutedText;
        }

        private IEnumerator BannerRoutine(string message, Color color)
        {
            sceneRefs.BannerText.text = message;
            sceneRefs.BannerText.color = color;
            sceneRefs.BannerText.CrossFadeAlpha(1f, 0.08f, true);
            yield return new WaitForSecondsRealtime(theme.bannerDuration);
            sceneRefs.BannerText.CrossFadeAlpha(0f, 0.28f, true);
        }

        private IEnumerator Shake(RectTransform rectTransform)
        {
            var elapsed = 0f;
            var start = rectTransform.anchoredPosition;
            while (elapsed < theme.shakeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                var strength = Mathf.Sin(elapsed * 60f) * 10f;
                rectTransform.anchoredPosition = start + new Vector2(strength, 0f);
                yield return null;
            }

            rectTransform.anchoredPosition = start;
        }

        private void OpenBackWarning()
        {
            if (sceneRefs.ExitPromptOverlay == null)
            {
                ReturnToLobby();
                return;
            }

            if (exitPromptOpen)
            {
                return;
            }

            exitPromptOpen = true;
            if (aiLoop != null)
            {
                StopCoroutine(aiLoop);
                aiLoop = null;
            }

            if (sceneRefs.ExitPromptTitleText != null)
            {
                sceneRefs.ExitPromptTitleText.text = controller != null && controller.State.Phase == MatchPhase.MatchEnded
                    ? "BACK TO LOBBY?"
                    : "LEAVE THE TABLE?";
            }

            if (sceneRefs.ExitPromptBodyText != null)
            {
                sceneRefs.ExitPromptBodyText.text = controller != null && controller.State.Phase == MatchPhase.MatchEnded
                    ? "Head back to the lobby, or stay here and review the match wrap a little longer."
                    : "Current match progress will be lost if you go back to the lobby now.";
            }

            sceneRefs.ExitPromptOverlay.SetAsLastSibling();
            SetSheetVisible(sceneRefs.ExitPromptOverlay, true);
            PlayFeedback(FeedbackCue.Select, 0.16f);
            SpawnImpactBurst(GetAnchoredPoint(sceneRefs.ExitPromptOverlay, new Vector2(0.5f, 0.5f)), theme.red, 32f, 4);
            if (sceneRefs.ExitPromptPanelImage != null)
            {
                StartCoroutine(PulseRect(sceneRefs.ExitPromptPanelImage.rectTransform, 1.03f, Mathf.Max(0.12f, theme.pulseDuration)));
            }
        }

        private void CloseBackWarning()
        {
            if (!exitPromptOpen)
            {
                return;
            }

            exitPromptOpen = false;
            SetSheetVisible(sceneRefs.ExitPromptOverlay, false);
            ScheduleAiLoop();
        }

        private void ConfirmReturnToLobby()
        {
            exitPromptOpen = false;
            SetSheetVisible(sceneRefs.ExitPromptOverlay, false);
            ReturnToLobby();
        }

        private void ReturnToLobby()
        {
            if (session != null)
            {
                session.LoadLobbyScene();
                return;
            }

            SceneManager.LoadScene("LobbyScene");
        }

        private SeatId GetCurrentTurnSeat()
        {
            return controller.State.Phase == MatchPhase.Bidding
                ? controller.State.RoundState.BidState.CurrentBidder
                : controller.State.RoundState.TrickState.CurrentTurn;
        }

        private void AddFeedMessage(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            recentFeed.Enqueue(message);
            while (recentFeed.Count > 5)
            {
                recentFeed.Dequeue();
            }

            sceneRefs.FeedText.text = "TABLE FEED\n" + string.Join("\n", recentFeed.Reverse());
        }

        private void AnimateScoreDelta(TeamId team)
        {
            var score = controller.State.Scores[team];
            var delta = score.RoundDelta + score.NilDelta;
            if (delta == 0)
            {
                return;
            }

            var target = team == TeamId.Home ? sceneRefs.HomeDeltaText : sceneRefs.AwayDeltaText;
            target.text = delta > 0 ? $"+{delta}" : delta.ToString();
            target.color = delta > 0 ? theme.green : theme.red;

            if (team == TeamId.Home && homeDeltaLoop != null)
            {
                StopCoroutine(homeDeltaLoop);
            }

            if (team == TeamId.Away && awayDeltaLoop != null)
            {
                StopCoroutine(awayDeltaLoop);
            }

            var routine = StartCoroutine(ScoreDeltaRoutine(target));
            if (team == TeamId.Home)
            {
                homeDeltaLoop = routine;
            }
            else
            {
                awayDeltaLoop = routine;
            }
        }

        private IEnumerator ScoreDeltaRoutine(Text target)
        {
            target.CrossFadeAlpha(1f, 0.08f, true);
            yield return new WaitForSecondsRealtime(1.15f);
            target.CrossFadeAlpha(0f, 0.3f, true);
        }

        private string BuildRoundSummaryText()
        {
            var home = controller.State.Scores[TeamId.Home];
            var away = controller.State.Scores[TeamId.Away];
            var trickTail = controller.State.RoundState.CompletedTricks.TakeLast(3).ToList();
            var startNumber = controller.State.RoundState.CompletedTricks.Count - trickTail.Count + 1;
            var recentTricks = trickTail
                .Select((trick, index) => $"{startNumber + index}. {string.Join(" | ", trick.Select(play => $"{controller.State.SeatNames[play.Seat]} {play.Card.ShortLabel}"))}")
                .ToList();
            var reneges = controller.State.RoundState.RenegeSeats.Count == 0
                ? "None"
                : string.Join(", ", controller.State.RoundState.RenegeSeats.Select(seat => controller.State.SeatNames[seat]));

            return
                $"Home: {home.Score} total | round {home.RoundDelta:+#;-#;0} | nil {home.NilDelta:+#;-#;0}\n" +
                $"Away: {away.Score} total | round {away.RoundDelta:+#;-#;0} | nil {away.NilDelta:+#;-#;0}\n\n" +
                $"Reneges this round: {reneges}\n" +
                $"Last hands:\n{(recentTricks.Count > 0 ? string.Join("\n", recentTricks) : "No completed tricks recorded.")}";
        }

        private string BuildMatchSummaryText(TeamId winningTeam)
        {
            var winnerLabel = winningTeam == TeamId.Home ? "You and your partner" : "The rivals";
            return
                $"{winnerLabel} reached the finish line.\n\n" +
                $"Home: {controller.State.Scores[TeamId.Home].Score}\n" +
                $"Away: {controller.State.Scores[TeamId.Away].Score}\n\n" +
                $"{BuildRoundSummaryText()}";
        }

        private void ApplyThemeText(Text label, Color color, int fontSize, FontStyle style)
        {
            if (label == null)
            {
                return;
            }

            label.font = theme.ResolveFont();
            label.color = color;
            label.fontSize = fontSize;
            label.fontStyle = style;
        }

        private void UpdateCenterHintLayout()
        {
            if (sceneRefs.CenterHintText == null)
            {
                return;
            }

            var rect = sceneRefs.CenterHintText.rectTransform;
            switch (controller.State.Phase)
            {
                case MatchPhase.Bidding:
                    SetAnchors(rect, new Vector2(0.30f, 0.37f), new Vector2(0.70f, 0.45f));
                    sceneRefs.CenterHintText.alignment = TextAnchor.MiddleCenter;
                    sceneRefs.CenterHintText.fontSize = 22;
                    break;
                case MatchPhase.TrickPlay:
                    SetAnchors(rect, new Vector2(0.28f, 0.37f), new Vector2(0.72f, 0.45f));
                    sceneRefs.CenterHintText.alignment = TextAnchor.MiddleCenter;
                    sceneRefs.CenterHintText.fontSize = 22;
                    break;
                case MatchPhase.RoundSummary:
                    SetAnchors(rect, new Vector2(0.22f, 0.28f), new Vector2(0.78f, 0.36f));
                    sceneRefs.CenterHintText.alignment = TextAnchor.MiddleCenter;
                    sceneRefs.CenterHintText.fontSize = 20;
                    break;
                case MatchPhase.MatchEnded:
                    SetAnchors(rect, new Vector2(0.22f, 0.25f), new Vector2(0.78f, 0.33f));
                    sceneRefs.CenterHintText.alignment = TextAnchor.MiddleCenter;
                    sceneRefs.CenterHintText.fontSize = 20;
                    break;
            }
        }

        private void ApplyFanLayout(CardButtonView view, Vector2 targetPosition, Quaternion targetRotation, float targetScale)
        {
            view.Root.anchorMin = new Vector2(0.5f, 0.5f);
            view.Root.anchorMax = new Vector2(0.5f, 0.5f);
            view.Root.pivot = new Vector2(0.5f, 0.5f);
            view.Root.anchoredPosition = targetPosition;
            view.Root.localRotation = targetRotation;
            view.Root.localScale = targetScale * Vector3.one;
        }

        private Vector2 GetFanTargetPosition(int index, int count, bool isSelected)
        {
            if (count <= 0)
            {
                return Vector2.zero;
            }

            var span = Mathf.Max(1, count - 1);
            var t = count == 1 ? 0f : (index / (float)span) * 2f - 1f;
            var containerWidth = sceneRefs.HandContent != null ? sceneRefs.HandContent.rect.width : 720f;
            var maxSpread = Mathf.Clamp((containerWidth - 120f) / Mathf.Max(1, count), 28f, 52f);
            var x = t * span * 0.5f * maxSpread;
            var y = -Mathf.Abs(t) * 20f + 8f + (isSelected ? theme.cardLiftAmount : 0f);
            return new Vector2(x, y);
        }

        private Quaternion GetFanTargetRotation(int index, int count)
        {
            if (count <= 1)
            {
                return Quaternion.identity;
            }

            var t = (index / (float)(count - 1)) * 2f - 1f;
            return Quaternion.Euler(0f, 0f, -t * 10f);
        }

        private static void SetAnchors(RectTransform rectTransform, Vector2 anchorMin, Vector2 anchorMax)
        {
            if (rectTransform == null)
            {
                return;
            }

            rectTransform.anchorMin = anchorMin;
            rectTransform.anchorMax = anchorMax;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
        }

        private static void StretchToParent(RectTransform rectTransform)
        {
            if (rectTransform == null)
            {
                return;
            }

            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
        }

        private Sprite ResolvePanelSprite()
        {
            return theme.panelSprite != null
                ? theme.panelSprite
                : ThemeSpriteFactory.CreateRoundedRectSprite(theme.panelColor, theme.panelStroke, 512, 256, 28);
        }

        private Sprite ResolveSheetSprite()
        {
            return theme.sheetSprite != null
                ? theme.sheetSprite
                : ThemeSpriteFactory.CreateRoundedRectSprite(theme.panelColor, theme.panelStroke, 768, 1024, 34);
        }

        private Sprite ResolveSoftPanelSprite()
        {
            return theme.softPanelSprite != null
                ? theme.softPanelSprite
                : ThemeSpriteFactory.CreateRoundedRectSprite(new Color(0f, 0f, 0f, 0.08f), theme.panelStroke, 512, 256, 22);
        }

        private Sprite ResolveCardBackSprite()
        {
            return theme.cardBackHeroSprite != null
                ? theme.cardBackHeroSprite
                : ThemeSpriteFactory.CreateRoundedRectSprite(theme.cardBack, theme.gold, 128, 180, 20);
        }

        private Sprite ResolveFxSprite()
        {
            return theme.chipSprite != null
                ? theme.chipSprite
                : theme.buttonSprite != null
                    ? theme.buttonSprite
                    : ResolveSoftPanelSprite();
        }

        private Sprite ResolveCardSprite(bool isSelected, bool isLegal)
        {
            if (isSelected && theme.cardFaceSelectedSprite != null)
            {
                return theme.cardFaceSelectedSprite;
            }

            if (isLegal && theme.cardFacePlayableSprite != null)
            {
                return theme.cardFacePlayableSprite;
            }

            if (!isLegal && controller.State.Phase == MatchPhase.TrickPlay && theme.cardFaceMutedSprite != null)
            {
                return theme.cardFaceMutedSprite;
            }

            if (theme.cardFaceDefaultSprite != null)
            {
                return theme.cardFaceDefaultSprite;
            }

            return ThemeSpriteFactory.CreateRoundedRectSprite(
                theme.cardFace,
                isSelected ? theme.gold : isLegal ? theme.panelStroke : new Color(0f, 0f, 0f, 0.15f),
                128,
                180,
                20);
        }

        private void ConfigureChipImage(Text scoreLabel, Color tint)
        {
            if (scoreLabel == null || scoreLabel.transform.parent == null)
            {
                return;
            }

            var chipImage = scoreLabel.transform.parent.GetComponent<Image>();
            if (chipImage == null)
            {
                return;
            }

            chipImage.sprite = theme.chipSprite != null
                ? theme.chipSprite
                : ThemeSpriteFactory.CreateChipSprite(tint, theme.gold);
            chipImage.type = Image.Type.Sliced;
            chipImage.color = tint;
        }

        private static void ApplyThemedImage(Image image, Color color, Sprite sprite)
        {
            if (image == null)
            {
                return;
            }

            image.sprite = sprite;
            image.type = sprite != null ? Image.Type.Sliced : Image.Type.Simple;
            image.color = color;
        }

        private Image CreateRuntimePanel(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            var rect = image.rectTransform;
            SetAnchors(rect, anchorMin, anchorMax);
            image.sprite = ResolveCardBackSprite();
            image.type = Image.Type.Sliced;
            image.color = Color.white;
            return image;
        }

        private Button CreateRuntimeButton(string name, Transform parent, string label, Color tint, Vector2 anchorMin, Vector2 anchorMax)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            SetAnchors(rect, anchorMin, anchorMax);
            var image = go.GetComponent<Image>();
            image.sprite = theme.buttonSprite != null ? theme.buttonSprite : ResolvePanelSprite();
            image.type = Image.Type.Sliced;
            image.color = tint;
            var button = go.GetComponent<Button>();
            button.targetGraphic = image;
            var text = CreateRuntimeText("Label", go.transform, label, 22, FontStyle.Bold, theme.backgroundColor, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one);
            text.rectTransform.offsetMin = new Vector2(8f, 8f);
            text.rectTransform.offsetMax = new Vector2(-8f, -8f);
            return button;
        }

        private Text CreateRuntimeText(string name, Transform parent, string value, int fontSize, FontStyle style, Color color, TextAnchor anchor, Vector2 anchorMin, Vector2 anchorMax)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<Text>();
            text.font = theme.ResolveFont();
            text.text = value;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.color = color;
            text.alignment = anchor;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            SetAnchors(text.rectTransform, anchorMin, anchorMax);
            return text;
        }

        private void TintButton(Button button, Color tint)
        {
            if (button == null || button.image == null)
            {
                return;
            }

            button.image.sprite = theme.buttonSprite != null
                ? theme.buttonSprite
                : ThemeSpriteFactory.CreateRoundedRectSprite(tint, theme.backgroundSecondary, 256, 96, 22);
            button.image.type = Image.Type.Sliced;
            button.image.color = tint;
            var label = button.GetComponentInChildren<Text>();
            if (label != null)
            {
                label.font = theme.ResolveFont();
                label.color = theme.backgroundColor;
            }
        }

        private static void SetSheetVisible(RectTransform sheet, bool visible)
        {
            if (sheet == null)
            {
                return;
            }

            sheet.gameObject.SetActive(visible);
        }
    }
}
