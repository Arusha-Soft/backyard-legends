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
        private const float BottomHandCardSizeMultiplier = 2.9f;
        private const float LastTrickCardSizeMultiplier = 0.54f;
        private const int BidCalloutFontSize = 46;
        private const float BookImpactExaggeration = 3f;
        private const float AvatarIntroMinSeconds = 2f;
        private const float AvatarIntroMaxSeconds = 4f;
        private const float AvatarIntroEntrySeconds = 1.15f;
        private const float OpeningDeckIntroDelayStep = 0.001f;
        private const float OpeningDeckIntroDuration = 0.14f;
        private const float OpeningDeckSettleSeconds = 0.28f;
        private const float OpeningGatherTravelSeconds = 0.46f;
        private const float OpeningGatherDelayStep = 0.006f;
        private const float OpeningDealDelayStep = 0.038f;
        private const float OpeningDealTravelSeconds = 0.78f;
        private const float OpeningDealWavePauseSeconds = 0.08f;
        private const float OpeningDeckStackSizeMultiplier = 2.15f;
        private const float OpeningDeckStackXOffset = 0.62f;
        private const float OpeningDeckStackYOffset = -0.42f;
        private const float BookCollectSmokeChance = 0.38f;
        private const float BigBookCollectSmokeChance = 0.62f;
        private const float OptionsMenuOpenAnimationSeconds = 0.34f;
        private const float OptionsMenuCloseAnimationSeconds = 0.24f;
        private const float OptionsMenuEntryYOffset = -34f;
        private const string AvatarResourceRoot = "BackyardLegends/Avatars";
        private const string AvatarAssetFolder = "Assets/Prototype/Art/Update3/avatar";
        public const string SfxMutedPlayerPrefsKey = "BackyardLegends.SfxMuted";
        private static readonly string[] CardPlaceAudioResourceNames =
        {
            "Card_Place_01",
            "Card_Place_02",
            "Card_Place_03"
        };
        private static readonly string[] SetBookAudioResourceNames =
        {
            "Set_Book_Impact",
            "Set_Book_Impact_Heavy"
        };
        private static readonly string[] BookWinAudioResourceNames =
        {
            "Book_Win_01",
            "Book_Win_02",
            "Book_Win_03",
            "Book_Win_04",
            "Book_Win_05",
            "Book_Win_06",
            "Set_Book_Impact"
        };
        private static readonly string[] BookWhooshAudioResourceNames =
        {
            "Book_Whoosh_01",
            "Book_Whoosh_02",
            "Set_Book_Whoosh"
        };
        private static readonly string[] TableSlamAudioResourceNames =
        {
            "Table_Slam_01",
            "Table_Slam_02",
            "Table_Slam_03"
        };
        private static readonly string[] GraffitiSprayAudioResourceNames =
        {
            "Graffiti_Spray_01",
            "Graffiti_Spray_02",
            "Graffiti_Spray_03"
        };
        private static readonly string[] SpadesBrokenAudioResourceNames =
        {
            "Spades_Broken_Metal_01",
            "Spades_Broken_Metal_02",
            "Spades_Broken_Metal_03"
        };
        private static readonly string[] CrowdReactionAudioResourceNames =
        {
            "Crowd_Reaction_Clap_01",
            "Crowd_Reaction_Clap_02",
            "Crowd_Reaction_Clap_03",
            "Crowd_Reaction_Clap_04",
            "Crowd_Reaction_Hooray_01",
            "Crowd_Reaction_Hooray_02",
            "Crowd_Reaction_Hooray_03",
            "Crowd_Reaction_Hooray_04"
        };
        private static readonly string[] TrashTalkTags =
        {
            "CLEAN",
            "NO MERCY",
            "LOCKED",
            "STREET RULES"
        };
        private const float TrashTalkStrongCardChance = 0.07f;
        private const float TrashTalkBigBookChance = 0.16f;
        private const float TrashTalkSpadesBrokenChance = 0.32f;
        private static readonly Vector2 BidCalloutAnchorMin = new(0.08f, 1.00f);
        private static readonly Vector2 BidCalloutAnchorMax = new(0.92f, 1.42f);
        private static readonly string[] AvatarDisplayNames =
        {
            string.Empty,
            "FANG",
            "YOU",
            "QUEEN",
            "SHADES",
            "RAIDER",
            "DIVA",
            "ACE",
            "ROXY",
            "KNOX",
            "ZARA",
            "KINGJAY",
            "MONK",
            "BANDIT",
            "SOUL",
            "MASK",
            "DREAD",
            "OG",
            "GREEN"
        };
        private static readonly string[] StartupHiddenCardGroupNames =
        {
            "Top Opponent Cards",
            "Left Opponent Cards",
            "Right Opponent Cards",
            "Bottom Player Cards",
            "Update3 Runtime Hand Content"
        };

        [SerializeField] private BackyardLegendsSceneRefs sceneRefs;
        [SerializeField] private ThemeConfig themeOverride;
        [SerializeField] private float handReviewSeconds = 3f;
        [SerializeField] private float bidTurnDelaySeconds = 1.15f;
        [Header("Audio Overrides")]
        [SerializeField] private AudioClip selectClipAsset;
        [SerializeField] private AudioClip bidClipAsset;
        [SerializeField] private AudioClip playClipAsset;
        [SerializeField] private AudioClip collectClipAsset;
        [SerializeField] private AudioClip bannerClipAsset;
        [SerializeField] private AudioClip dealClipAsset;
        [SerializeField] private AudioClip invalidClipAsset;
        [SerializeField] private AudioClip roundScoreClipAsset;
        [SerializeField] private AudioClip matchEndClipAsset;
        [SerializeField] private AudioClip setBookClipAsset;
        [SerializeField] private AudioClip avatarRouletteClipAsset;
        [SerializeField] private AudioClip avatarAssignedClipAsset;
        [SerializeField] private AudioClip bidPanelOpenClipAsset;
        [SerializeField] private AudioClip optionsMenuOpenClipAsset;
        [SerializeField] private AudioClip optionsMenuCloseClipAsset;
        [SerializeField] private AudioClip[] cardPlaceClipAssets;
        [SerializeField] private AudioSource soundFxAudioSource;
        [Header("Strong Card FX")]
        [SerializeField] private GameObject aceCardFxPrefab;
        [SerializeField] private GameObject kingCardFxPrefab;
        [SerializeField] private GameObject queenCardFxPrefab;
        [SerializeField] private GameObject jackCardFxPrefab;
        [SerializeField] private GameObject highSpadeFxPrefab;
        [SerializeField] private GameObject importantCardFxPrefab;
        [SerializeField] private GameObject cardImpactFxPrefab;
        [SerializeField] private GameObject cardSmokeFxPrefab;
        [SerializeField] private GameObject bookWinFxPrefab;
        [SerializeField] private GameObject setBookFxPrefab;
        [SerializeField] private GameObject tableSlamFxPrefab;
        [SerializeField] private GameObject spadesBrokenSmokeFxPrefab;
        [SerializeField] private GameObject spadesBrokenLightningFxPrefab;

        private readonly Dictionary<SeatId, SeatPanelView> seatViews = new();
        private readonly Dictionary<SeatId, TrickSlotView> trickSlots = new();
        private readonly List<CardButtonView> handPool = new();
        private readonly List<CardButtonView> lastTrickCardViews = new();
        private readonly Dictionary<CardButtonView, Coroutine> handAnimations = new();
        private readonly List<LastTrickCardPose> lastTrickCardTargetPoses = new();
        private readonly List<CardButtonView> openingStackPreviewCards = new();
        private readonly Dictionary<CardButtonView, Coroutine> openingStackPreviewAnimations = new();
        private readonly Dictionary<int, Button> bidButtons = new();
        private readonly Dictionary<SeatId, Coroutine> bidBubbleLoops = new();
        private readonly Dictionary<SeatId, Coroutine> bookTextLoops = new();
        private readonly Dictionary<SeatId, BookTextVisualState> bookTextDefaults = new();
        private readonly Dictionary<SeatId, Coroutine> bookAvatarLoops = new();
        private readonly Dictionary<SeatId, int> consecutiveBookStreaks = new();
        private readonly Dictionary<SeatId, Image> seatAvatarImages = new();
        private readonly Dictionary<SeatId, CanvasGroup> seatIntroGroups = new();
        private readonly Dictionary<SeatId, Component> avatarBookLightningFx = new();
        private readonly Dictionary<SeatId, GameObject> seatAuraObjects = new();
        private readonly List<Sprite> avatarRouletteSprites = new();
        private readonly Queue<string> recentFeed = new();
        private readonly Queue<IEnumerator> queuedAnimations = new();
        private readonly Queue<TeamId> pendingSetBookMoments = new();
        private readonly HashSet<Card> lastRenderedHand = new();
        private readonly HashSet<SeatId> hiddenTrickSlots = new();
        private readonly Dictionary<SeatId, Card> resolvedTrickCards = new();
        private readonly List<CardButtonView> floatingCards = new();
        private readonly List<CardButtonView> openingDealRuntimeSeatCards = new();
        private readonly List<Graphic> transientFx = new();
        private readonly List<GameObject> activeRuntimeParticleFx = new();
        private readonly HashSet<Image> preservedSeatPanelVisuals = new();
        private readonly Dictionary<SeatId, Vector3> preservedSeatRootScales = new();

        private ThemeConfig theme;
        private RuleSetDefinition selectedRule;
        private Card? selectedCard;
        private int? pendingBidSelection;
        private Coroutine aiLoop;
        private Coroutine animationQueueLoop;
        private Coroutine openingStackIntroLoop;
        private Coroutine avatarIntroLoop;
        private Coroutine deferredSheetStateLoop;
        private Coroutine bannerLoop;
        private Coroutine flashLoop;
        private Coroutine homeDeltaLoop;
        private Coroutine awayDeltaLoop;
        private Coroutine dealButtonFadeLoop;
        private Coroutine handReviewLoop;
        private Coroutine bidTurnDelayLoop;
        private Coroutine exitPromptFadeLoop;
        private Coroutine optionsMenuAnimationLoop;
        private Coroutine bookCameraShakeLoop;
        private Coroutine bidCameraFocusLoop;
        private Coroutine lastTrickDisplayLoop;
        private Transform bookCameraShakeTarget;
        private Vector3 bookCameraShakeStartPosition;
        private Quaternion bookCameraShakeStartRotation;
        private Camera bookCameraShakeCamera;
        private float bookCameraShakeStartOrthographicSize;
        private float bookCameraShakeStartFieldOfView;
        private Camera bidFocusCamera;
        private Vector3 bidFocusDefaultPosition;
        private Quaternion bidFocusDefaultRotation;
        private float bidFocusDefaultOrthographicSize;
        private float bidFocusDefaultFieldOfView;
        private bool bidFocusDefaultsCaptured;
        private SpadesMatchController controller;
        private IRuleEngine ruleEngine;
        private BackyardLegendsSession session;
        private AudioSource feedbackAudioSource;
        private Image openingStackEffectImage;
        private AudioClip bidClip;
        private AudioClip selectClip;
        private AudioClip playClip;
        private AudioClip collectClip;
        private AudioClip bannerClip;
        private AudioClip dealClip;
        private AudioClip invalidClip;
        private AudioClip roundScoreClip;
        private AudioClip matchEndClip;
        private AudioClip setBookClip;
        private AudioClip avatarRouletteClip;
        private AudioClip avatarAssignedClip;
        private AudioClip bidPanelOpenClip;
        private AudioClip optionsMenuOpenClip;
        private AudioClip optionsMenuCloseClip;
        private AudioClip[] cardPlaceClips;
        private AudioClip[] setBookClips;
        private AudioClip[] bookWinClips;
        private AudioClip[] bookWhooshClips;
        private AudioClip[] tableSlamClips;
        private AudioClip[] graffitiSprayClips;
        private AudioClip[] spadesBrokenClips;
        private AudioClip[] crowdReactionClips;
        private int lastBookWinClipIndex = -1;
        private int lastBookWhooshClipIndex = -1;
        private int lastSetBookClipIndex = -1;
        private int lastTableSlamClipIndex = -1;
        private int lastGraffitiSprayClipIndex = -1;
        private int lastSpadesBrokenClipIndex = -1;
        private int lastCrowdReactionClipIndex = -1;
        private Sprite graffitiSplashSprite;
        private Image lastTrickPanel;
        private Text lastTrickTitleText;
        private RectTransform lastTrickCardsRoot;
        private CanvasGroup lastTrickGroup;
        private string lastTrickSignature;
        private RectTransform runtimeFxCanvasRoot;
        private RectTransform runtimeAnimationRoot;
        private RectTransform optionsMenuAnimationTarget;
        private CanvasGroup optionsMenuCanvasGroup;
        private Vector2 optionsMenuBaseAnchoredPosition;
        private Vector3 optionsMenuBaseScale = Vector3.one;
        private Quaternion optionsMenuBaseRotation = Quaternion.identity;
        private Sprite bidButtonDefaultSprite;
        private Sprite bidButtonSelectedSprite;
        private int bannerDefaultFontSize;
        private FontStyle bannerDefaultFontStyle;
        private TextAnchor bannerDefaultAlignment;
        private Color bannerDefaultColor;
        private Vector2 bannerDefaultPosition;
        private Vector3 bannerDefaultScale;
        private bool bannerDefaultsCaptured;
        private bool pendingRoundSheetOpen;
        private bool pendingEndSheetOpen;
        private bool setBookMomentRunning;
        private bool avatarIntroRunning;
        private bool openingDealPending;
        private bool openingDealRunning;
        private bool handReviewPending;
        private bool bidTurnDelayPending;
        private bool openingStackIntroRunning;
        private bool suppressNextHandEntryAnimation;
        private bool optionsMenuAnimationDefaultsCaptured;
        private bool optionsMenuOpen;
        private bool exitPromptOpen;
        private bool bidSheetWasVisible;
        private bool sfxMuted;
        private bool spadesBrokenMomentShown;
        private float nextAvatarRouletteCueTime;
        private float nextTrashTalkPopupTime;
        private float nextCrowdReactionTime;
#if UNITY_EDITOR
        private float editorDefaultFixedDeltaTime;
#endif
        private ConfirmationPromptType activePrompt;

        private RectTransform AnimationRoot => ResolveVisibleAnimationRoot();
        private bool HasCardMotionPending => openingDealRunning || animationQueueLoop != null || queuedAnimations.Count > 0 || floatingCards.Count > 0;

        private bool HasVisualMotionPending => HasCardMotionPending || setBookMomentRunning || pendingSetBookMoments.Count > 0 || bookTextLoops.Count > 0;

        private enum FeedbackCue
        {
            Select,
            Bid,
            Play,
            Collect,
            Banner,
            Deal,
            Invalid,
            RoundScore,
            MatchEnd,
            SetBook,
            AvatarRoulette,
            AvatarAssigned,
            BidPanelOpen,
            OptionsMenuOpen,
            OptionsMenuClose
        }

        private enum ConfirmationPromptType
        {
            None,
            ReturnToLobby,
            ClaimRest,
            ForfeitMatch
        }

        private sealed class AvatarIntroSeatState
        {
            public SeatId Seat;
            public SeatPanelView View;
            public Image AvatarImage;
            public CanvasGroup Group;
            public Sprite OriginalSprite;
            public Vector3 OriginalScale;
            public Quaternion OriginalRotation;
            public Vector2 OriginalAnchoredPosition;
            public Vector2 EntryOffset;
            public Vector2 EntryCurveOffset;
            public float EntryRotationDegrees;
            public string OriginalName;
            public Sprite FinalSprite;
            public string FinalName;
            public float EntryDelay;
            public float RouletteDuration;
            public int RouletteOffset;
            public int LastRouletteIndex = -1;
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
                Vector2 burstOffset,
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
                BurstOffset = burstOffset;
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
            public Vector2 BurstOffset { get; }
            public float Delay { get; }
        }

        private readonly struct LastTrickCardPose
        {
            public LastTrickCardPose(Vector2 position, Vector2 size, Quaternion rotation, Vector3 scale)
            {
                Position = position;
                Size = size;
                Rotation = rotation;
                Scale = scale;
            }

            public Vector2 Position { get; }
            public Vector2 Size { get; }
            public Quaternion Rotation { get; }
            public Vector3 Scale { get; }
        }

        private readonly struct CardVisualState
        {
            public CardVisualState(
                Sprite panelSprite,
                Image.Type panelType,
                Color panelColor,
                Sprite artSprite,
                Color artColor,
                bool artVisible,
                Color rankColor,
                Color suitColor)
            {
                PanelSprite = panelSprite;
                PanelType = panelType;
                PanelColor = panelColor;
                ArtSprite = artSprite;
                ArtColor = artColor;
                ArtVisible = artVisible;
                RankColor = rankColor;
                SuitColor = suitColor;
            }

            public Sprite PanelSprite { get; }
            public Image.Type PanelType { get; }
            public Color PanelColor { get; }
            public Sprite ArtSprite { get; }
            public Color ArtColor { get; }
            public bool ArtVisible { get; }
            public Color RankColor { get; }
            public Color SuitColor { get; }
        }

        private sealed class OpeningDealFlight
        {
            public CardMotionSnapshot Motion;
            public bool RevealToHand;
            public CanvasGroup RevealTarget;
            public CardButtonView Ghost;
            public float Delay;
            public int SequenceIndex;
        }

        private readonly struct BookTextVisualState
        {
            public BookTextVisualState(Color color, int fontSize, FontStyle fontStyle, TextAnchor alignment, Vector3 scale, Vector2 position, float alpha)
            {
                Color = color;
                FontSize = fontSize;
                FontStyle = fontStyle;
                Alignment = alignment;
                Scale = scale;
                Position = position;
                Alpha = alpha;
            }

            public Color Color { get; }
            public int FontSize { get; }
            public FontStyle FontStyle { get; }
            public TextAnchor Alignment { get; }
            public Vector3 Scale { get; }
            public Vector2 Position { get; }
            public float Alpha { get; }
        }

        private void Awake()
        {
            Screen.orientation = ScreenOrientation.Portrait;
            Application.targetFrameRate = 60;
#if UNITY_EDITOR
            editorDefaultFixedDeltaTime = Time.fixedDeltaTime / Mathf.Max(0.0001f, Time.timeScale);
#endif

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

            ResolveVisibleAnimationRoot();
            EnsureRuntimeOpeningWidgets();
            EnsureRuntimeBackNavigationWidgets();
            EnsureRuntimeOptionsMenuWidgets();
            BindAuthoredBidConfirmationWidget();
            EnsureRuntimeSeatCallouts();
            BindAuthoredLastTrickDisplay();

            var handLayoutGroup = sceneRefs.HandContent != null ? sceneRefs.HandContent.GetComponent<LayoutGroup>() : null;
            if (handLayoutGroup != null)
            {
                handLayoutGroup.enabled = false;
            }

            CacheViewRefs();
            CapturePreservedSeatPanelVisuals();
            EnsureRuntimeCardArtTargets();
            ConfigureFeedbackAudio();
            ConfigureUiCallbacks();
            ApplyTheme();
            StartConfiguredMatch();
        }

        private RectTransform ResolveVisibleAnimationRoot()
        {
            var activeCanvas = ResolveRuntimeFxCanvasRoot();
            if (runtimeAnimationRoot != null)
            {
                if (activeCanvas != null && runtimeAnimationRoot.parent != activeCanvas)
                {
                    runtimeAnimationRoot.SetParent(activeCanvas, false);
                }

                runtimeAnimationRoot.gameObject.SetActive(true);
                runtimeAnimationRoot.SetAsLastSibling();
                return runtimeAnimationRoot;
            }

            var parent = activeCanvas != null ? activeCanvas : transform;
            var existing = parent.Find("Opening Animation Runtime Root") as RectTransform;
            if (existing != null)
            {
                runtimeAnimationRoot = existing;
            }
            else
            {
                var rootObject = new GameObject("Opening Animation Runtime Root", typeof(RectTransform), typeof(CanvasGroup));
                rootObject.transform.SetParent(parent, false);
                runtimeAnimationRoot = rootObject.GetComponent<RectTransform>();
            }

            runtimeAnimationRoot.anchorMin = Vector2.zero;
            runtimeAnimationRoot.anchorMax = Vector2.one;
            runtimeAnimationRoot.pivot = new Vector2(0.5f, 0.5f);
            runtimeAnimationRoot.offsetMin = Vector2.zero;
            runtimeAnimationRoot.offsetMax = Vector2.zero;
            runtimeAnimationRoot.localScale = Vector3.one;
            runtimeAnimationRoot.localRotation = Quaternion.identity;
            runtimeAnimationRoot.gameObject.SetActive(true);
            runtimeAnimationRoot.SetAsLastSibling();

            var group = runtimeAnimationRoot.GetComponent<CanvasGroup>() ?? runtimeAnimationRoot.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 1f;
            group.interactable = false;
            group.blocksRaycasts = false;

            return runtimeAnimationRoot;
        }

        private RectTransform ResolveRuntimeFxCanvasRoot()
        {
            var activeCanvas = FindActiveGameplayCanvasRoot();
            if (activeCanvas == null)
            {
                return transform as RectTransform;
            }

            if (runtimeFxCanvasRoot != null)
            {
                if (!runtimeFxCanvasRoot.gameObject.activeInHierarchy || runtimeFxCanvasRoot.parent != activeCanvas)
                {
                    runtimeFxCanvasRoot.SetParent(activeCanvas, false);
                }

                ConfigureRuntimeFxCanvas(activeCanvas, runtimeFxCanvasRoot);
                return runtimeFxCanvasRoot;
            }

            var existing = activeCanvas.Find("Backyard Legends Runtime FX Canvas") as RectTransform;
            if (existing != null)
            {
                runtimeFxCanvasRoot = existing;
            }
            else
            {
                var canvasObject = new GameObject("Backyard Legends Runtime FX Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                canvasObject.transform.SetParent(activeCanvas, false);
                runtimeFxCanvasRoot = canvasObject.GetComponent<RectTransform>();
            }

            ConfigureRuntimeFxCanvas(activeCanvas, runtimeFxCanvasRoot);
            return runtimeFxCanvasRoot;
        }

        private static void ConfigureRuntimeFxCanvas(RectTransform sourceCanvasRoot, RectTransform fxCanvasRoot)
        {
            if (sourceCanvasRoot == null || fxCanvasRoot == null)
            {
                return;
            }

            fxCanvasRoot.anchorMin = Vector2.zero;
            fxCanvasRoot.anchorMax = Vector2.one;
            fxCanvasRoot.pivot = new Vector2(0.5f, 0.5f);
            fxCanvasRoot.offsetMin = Vector2.zero;
            fxCanvasRoot.offsetMax = Vector2.zero;
            fxCanvasRoot.localScale = Vector3.one;
            fxCanvasRoot.localRotation = Quaternion.identity;
            fxCanvasRoot.gameObject.SetActive(true);
            fxCanvasRoot.SetAsLastSibling();

            var sourceCanvas = sourceCanvasRoot.GetComponent<Canvas>();
            var fxCanvas = fxCanvasRoot.GetComponent<Canvas>();
            if (sourceCanvas != null && fxCanvas != null)
            {
                fxCanvas.renderMode = sourceCanvas.renderMode;
                fxCanvas.worldCamera = sourceCanvas.worldCamera;
                fxCanvas.planeDistance = sourceCanvas.planeDistance;
                fxCanvas.overrideSorting = true;
                fxCanvas.sortingLayerID = sourceCanvas.sortingLayerID;
                fxCanvas.sortingOrder = sourceCanvas.sortingOrder + 40;
            }

            var sourceScaler = sourceCanvasRoot.GetComponent<CanvasScaler>();
            var fxScaler = fxCanvasRoot.GetComponent<CanvasScaler>();
            if (sourceScaler != null && fxScaler != null)
            {
                fxScaler.uiScaleMode = sourceScaler.uiScaleMode;
                fxScaler.referenceResolution = sourceScaler.referenceResolution;
                fxScaler.screenMatchMode = sourceScaler.screenMatchMode;
                fxScaler.matchWidthOrHeight = sourceScaler.matchWidthOrHeight;
                fxScaler.referencePixelsPerUnit = sourceScaler.referencePixelsPerUnit;
                fxScaler.scaleFactor = sourceScaler.scaleFactor;
            }

            var raycaster = fxCanvasRoot.GetComponent<GraphicRaycaster>();
            if (raycaster != null)
            {
                raycaster.enabled = false;
            }
        }

        private RectTransform FindActiveGameplayCanvasRoot()
        {
            var canvases = Resources.FindObjectsOfTypeAll<Canvas>();
            var update3 = canvases.FirstOrDefault(canvas =>
                canvas != null &&
                canvas.gameObject.scene.IsValid() &&
                canvas.gameObject.activeInHierarchy &&
                canvas.name == "Update3 Gameplay World UI");
            if (update3 != null && update3.transform is RectTransform update3Rect)
            {
                return update3Rect;
            }

            if (sceneRefs?.TablePanel != null)
            {
                var canvas = sceneRefs.TablePanel.GetComponentInParent<Canvas>();
                if (canvas != null && canvas.gameObject.activeInHierarchy && canvas.transform is RectTransform canvasRect)
                {
                    return canvasRect;
                }
            }

            return transform as RectTransform;
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

        }

        private void EnsureRuntimeBackNavigationWidgets()
        {
            if (sceneRefs.HudPanel == null)
            {
                return;
            }

            var titleTransform = sceneRefs.HudPanel.transform.Find("Title") as RectTransform;
            if (titleTransform != null)
            {
                SetAnchors(titleTransform, new Vector2(0.21f, 0.58f), new Vector2(0.60f, 0.96f));
            }

            if (sceneRefs.ExitPromptOverlay != null)
            {
                sceneRefs.ExitPromptOverlay.SetAsLastSibling();
            }
        }

        private void EnsureRuntimeOptionsMenuWidgets()
        {
            if (sceneRefs.BackButton != null)
            {
                SetButtonLabel(sceneRefs.BackButton, "MENU");
            }

            if (sceneRefs.OptionsMenuImage == null)
            {
                sceneRefs.OptionsMenuImage = CreateRuntimePanel("Options Menu Runtime", AnimationRoot, new Vector2(0.04f, 0.52f), new Vector2(0.52f, 0.86f));
            }

            sceneRefs.OptionsMenu = sceneRefs.OptionsMenuImage.rectTransform;
            if (sceneRefs.OptionsMenuTitleText == null)
            {
                sceneRefs.OptionsMenuTitleText = CreateRuntimeText("Title", sceneRefs.OptionsMenu, "SETTINGS", 22, FontStyle.Bold, theme.gold, TextAnchor.MiddleCenter, new Vector2(0.08f, 0.82f), new Vector2(0.92f, 0.97f));
            }
            else
            {
                sceneRefs.OptionsMenuTitleText.text = "SETTINGS";
            }

            if (sceneRefs.ClaimTheRestButton == null)
            {
                sceneRefs.ClaimTheRestButton = CreateRuntimeButton("Claim The Rest", sceneRefs.OptionsMenu, "CLAIM REST", theme.gold, new Vector2(0.08f, 0.62f), new Vector2(0.92f, 0.77f));
            }

            sceneRefs.ClaimTheRestButton.gameObject.SetActive(false);

            if (sceneRefs.ForfeitMatchButton == null)
            {
                sceneRefs.ForfeitMatchButton = CreateRuntimeButton("Forfeit Match", sceneRefs.OptionsMenu, "FORFEIT MATCH", theme.red, new Vector2(0.08f, 0.60f), new Vector2(0.92f, 0.75f));
            }

            if (sceneRefs.LeaveTableButton == null)
            {
                sceneRefs.LeaveTableButton = CreateRuntimeButton("Leave Table", sceneRefs.OptionsMenu, "LEAVE TABLE", theme.panelStroke, new Vector2(0.08f, 0.42f), new Vector2(0.92f, 0.57f));
            }

            if (sceneRefs.SfxToggleButton == null)
            {
                sceneRefs.SfxToggleButton = CreateRuntimeButton("SFX Toggle", sceneRefs.OptionsMenu, "SFX: ON", theme.gold, new Vector2(0.08f, 0.24f), new Vector2(0.92f, 0.39f));
            }

            if (sceneRefs.CloseOptionsMenuButton == null)
            {
                sceneRefs.CloseOptionsMenuButton = CreateRuntimeButton("Resume", sceneRefs.OptionsMenu, "RESUME", theme.green, new Vector2(0.08f, 0.06f), new Vector2(0.92f, 0.21f));
            }

            SetButtonLabel(sceneRefs.ForfeitMatchButton, "FORFEIT MATCH");
            SetButtonLabel(sceneRefs.LeaveTableButton, "LEAVE TABLE");
            RenderSfxToggleLabel();
            SetButtonLabel(sceneRefs.CloseOptionsMenuButton, "RESUME");

            SetOptionsMenuVisibleImmediate(false);
        }

        private void BindAuthoredBidConfirmationWidget()
        {
            if (sceneRefs.BidSheet == null || sceneRefs.ConfirmBidButton != null)
            {
                return;
            }

            var confirmTransform = sceneRefs.BidSheet.Find("Confirm Bid");
            if (confirmTransform != null)
            {
                sceneRefs.ConfirmBidButton = confirmTransform.GetComponent<Button>();
            }
        }

        private void EnsureRuntimeSeatCallouts()
        {
            EnsureSeatCallout(sceneRefs.BottomSeat);
            EnsureSeatCallout(sceneRefs.LeftSeat);
            EnsureSeatCallout(sceneRefs.TopSeat);
            EnsureSeatCallout(sceneRefs.RightSeat);
        }

        private void BindAuthoredLastTrickDisplay()
        {
            lastTrickCardViews.Clear();
            lastTrickPanel = sceneRefs.LastTrickPanel != null
                ? sceneRefs.LastTrickPanel
                : sceneRefs.TablePanel != null
                    ? sceneRefs.TablePanel.transform.Find("Last Hand Played")?.GetComponent<Image>()
                    : null;
            lastTrickTitleText = sceneRefs.LastTrickTitleText != null
                ? sceneRefs.LastTrickTitleText
                : lastTrickPanel != null
                    ? lastTrickPanel.transform.Find("Title")?.GetComponent<Text>()
                    : null;
            lastTrickCardsRoot = sceneRefs.LastTrickCardsRoot != null
                ? sceneRefs.LastTrickCardsRoot
                : lastTrickPanel != null
                    ? lastTrickPanel.transform.Find("Cards") as RectTransform
                    : null;
            lastTrickGroup = sceneRefs.LastTrickGroup != null
                ? sceneRefs.LastTrickGroup
                : lastTrickPanel != null
                    ? lastTrickPanel.GetComponent<CanvasGroup>()
                    : null;

            if (sceneRefs.LastTrickCards != null)
            {
                lastTrickCardViews.AddRange(sceneRefs.LastTrickCards.Where(view => view != null));
            }

            if (lastTrickCardViews.Count == 0 && lastTrickCardsRoot != null)
            {
                lastTrickCardViews.AddRange(lastTrickCardsRoot
                    .GetComponentsInChildren<CardButtonView>(true)
                    .OrderBy(view => view.transform.GetSiblingIndex()));
            }

            if (lastTrickPanel == null || lastTrickCardViews.Count == 0)
            {
                return;
            }

            CaptureAuthoredLastTrickCardPoses();

            lastTrickPanel.raycastTarget = false;
            if (lastTrickGroup != null)
            {
                lastTrickGroup.blocksRaycasts = false;
                lastTrickGroup.interactable = false;
            }

            foreach (var view in lastTrickCardViews)
            {
                ConfigureAuthoredLastTrickCard(view);
            }

            if (sceneRefs.LastTrickText != null)
            {
                sceneRefs.LastTrickText.gameObject.SetActive(false);
            }
        }

        private void CaptureAuthoredLastTrickCardPoses()
        {
            lastTrickCardTargetPoses.Clear();
            foreach (var view in lastTrickCardViews)
            {
                if (view == null)
                {
                    continue;
                }

                lastTrickCardTargetPoses.Add(new LastTrickCardPose(
                    view.Root.anchoredPosition,
                    view.Root.sizeDelta,
                    view.Root.localRotation,
                    view.Root.localScale));
            }
        }

        private void ConfigureAuthoredLastTrickCard(CardButtonView view)
        {
            if (view == null)
            {
                return;
            }

            view.Button.onClick.RemoveAllListeners();
            view.Button.enabled = false;
            view.Panel.raycastTarget = false;
            view.RankText.raycastTarget = false;
            view.SuitText.raycastTarget = false;
            if (view.CanvasGroup != null)
            {
                view.CanvasGroup.blocksRaycasts = false;
                view.CanvasGroup.interactable = false;
            }

            if (view.FaceImage != null)
            {
                view.FaceImage.raycastTarget = false;
            }
        }

        private void EnsureSeatCallout(SeatPanelView view)
        {
            if (view == null)
            {
                return;
            }

            if (view.BidCalloutPanel != null && view.BidCalloutText != null && view.BidCalloutGroup != null)
            {
                EnsureBidCalloutSplash(view);
                ConfigureSeatCalloutLayout(view);
                return;
            }

            var bubble = new GameObject("Bid Callout Runtime", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            bubble.transform.SetParent(view.transform, false);
            var bubbleRect = bubble.GetComponent<RectTransform>();
            SetAnchors(bubbleRect, BidCalloutAnchorMin, BidCalloutAnchorMax);
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
            EnsureBidCalloutSplash(view);
            view.BidCalloutText = CreateRuntimeText("Label", bubble.transform, "I BID 3", BidCalloutFontSize, FontStyle.Bold, theme.primaryText, TextAnchor.MiddleCenter, new Vector2(0.02f, 0.06f), new Vector2(0.98f, 0.94f));
            ConfigureSeatCalloutLayout(view);
        }

        private void EnsureBidCalloutSplash(SeatPanelView view)
        {
            if (view?.BidCalloutGroup == null)
            {
                return;
            }

            var parent = view.BidCalloutGroup.transform;
            if (view.BidCalloutSplash == null)
            {
                var existing = parent.Find("Graffiti Splash");
                view.BidCalloutSplash = existing != null
                    ? existing.GetComponent<Image>()
                    : null;
            }

            if (view.BidCalloutSplash == null)
            {
                var splashObject = new GameObject("Graffiti Splash", typeof(RectTransform), typeof(Image));
                splashObject.transform.SetParent(parent, false);
                view.BidCalloutSplash = splashObject.GetComponent<Image>();
            }

            view.BidCalloutSplash.sprite = ResolveGraffitiSplashSprite();
            view.BidCalloutSplash.type = Image.Type.Simple;
            view.BidCalloutSplash.color = new Color(1f, 1f, 1f, 0f);
            view.BidCalloutSplash.raycastTarget = false;
            view.BidCalloutSplash.transform.SetAsFirstSibling();
        }

        private void Update()
        {
#if UNITY_EDITOR
            if (WasSpacePressedThisFrame())
            {
                ForceEndMatchForEditorTest();
            }
#endif

            if (WasBackPressedThisFrame())
            {
                if (exitPromptOpen)
                {
                    CloseBackWarning();
                }
                else if (optionsMenuOpen)
                {
                    CloseOptionsMenu();
                }
                else
                {
                    OpenOptionsMenu();
                }
            }

            if (controller == null || controller.State.RoundState == null)
            {
                return;
            }

            RestoreSeatRootScales();
        }

#if UNITY_EDITOR
        private void ForceEndMatchForEditorTest()
        {
            if (!CanRunEditorEndShortcut() || controller == null || controller.State.RoundState == null)
            {
                return;
            }

            Time.timeScale = 1f;
            Time.fixedDeltaTime = editorDefaultFixedDeltaTime;
            StopAllCoroutines();
            ResetEditorShortcutCoroutineRefs();
            avatarIntroRunning = false;
            openingDealPending = false;
            openingDealRunning = false;
            handReviewPending = false;
            bidTurnDelayPending = false;
            optionsMenuOpen = false;
            exitPromptOpen = false;
            activePrompt = ConfirmationPromptType.None;
            selectedCard = null;
            pendingBidSelection = null;
            lastRenderedHand.Clear();

            ClearTransientMotionState(true);
            SetBidSheetVisible(false);
            SetSheetVisible(sceneRefs.RoundSheet, false);
            SetOptionsMenuVisibleImmediate(false);
            SetSheetVisible(sceneRefs.ExitPromptOverlay, false);

            ApplyEditorTestEndState(TeamId.Home);
            OnMatchEvent(new MatchEndedEvent(controller.State, TeamId.Home));
            ApplyDeferredSheetState();
            if (sceneRefs.EndSheet != null)
            {
                sceneRefs.EndSheet.SetAsLastSibling();
            }

            FlashStatus("EDITOR TEST: MATCH ENDED", theme != null ? theme.gold : Color.white);
        }

        private bool CanRunEditorEndShortcut()
        {
            var canvas = sceneRefs?.EndSheet != null
                ? sceneRefs.EndSheet.GetComponentInParent<Canvas>()
                : GetComponentInParent<Canvas>();
            return canvas == null || canvas.enabled;
        }

        private void ResetEditorShortcutCoroutineRefs()
        {
            aiLoop = null;
            animationQueueLoop = null;
            openingStackIntroLoop = null;
            avatarIntroLoop = null;
            deferredSheetStateLoop = null;
            bannerLoop = null;
            flashLoop = null;
            homeDeltaLoop = null;
            awayDeltaLoop = null;
            dealButtonFadeLoop = null;
            handReviewLoop = null;
            bidTurnDelayLoop = null;
            exitPromptFadeLoop = null;
            optionsMenuAnimationLoop = null;
            bookCameraShakeLoop = null;
            bidCameraFocusLoop = null;
        }

        private void ApplyEditorTestEndState(TeamId winningTeam)
        {
            var targetScore = controller.State.TargetScore > 0
                ? controller.State.TargetScore
                : selectedRule != null && selectedRule.TargetScore > 0
                    ? selectedRule.TargetScore
                    : 100;
            controller.State.TargetScore = Mathf.Max(1, targetScore);
            controller.State.WinningTeam = winningTeam;
            controller.State.Phase = MatchPhase.MatchEnded;
            controller.State.RoundState.LastStatusMessage = "Editor test match ended.";
            controller.State.RoundState.TrickState.Plays.Clear();
            controller.State.RoundState.TrickState.LeadSuit = null;
            controller.State.RoundState.RenegeSeats.Clear();

            var losingTeam = winningTeam == TeamId.Home ? TeamId.Away : TeamId.Home;
            ApplyEditorTestScore(winningTeam, 6, 7, 70, Mathf.Max(controller.State.TargetScore, GetEditorScore(winningTeam).Score + 70));
            ApplyEditorTestScore(losingTeam, 5, 4, -50, Mathf.Min(controller.State.TargetScore - 10, GetEditorScore(losingTeam).Score - 50));
        }

        private ScoreSnapshot GetEditorScore(TeamId team)
        {
            if (!controller.State.Scores.TryGetValue(team, out var score) || score == null)
            {
                score = new ScoreSnapshot { Team = team };
                controller.State.Scores[team] = score;
            }

            return score;
        }

        private void ApplyEditorTestScore(TeamId team, int contractBid, int tricksWon, int roundDelta, int finalScore)
        {
            var score = GetEditorScore(team);
            score.ContractBid = contractBid;
            score.TricksWon = tricksWon;
            score.RoundDelta = roundDelta;
            score.NilDelta = 0;
            score.BagsEarned = Mathf.Max(0, tricksWon - contractBid);
            score.BagPenaltyDelta = 0;
            score.Score = finalScore;
        }

        private static bool WasSpacePressedThisFrame()
        {
            if (WasInputSystemKeyPressedThisFrame("spaceKey"))
            {
                return true;
            }

            try
            {
                return Input.GetKeyDown(KeyCode.Space);
            }
            catch (System.InvalidOperationException)
            {
                return false;
            }
        }

        private static bool WasInputSystemKeyPressedThisFrame(string keyPropertyName)
        {
            var keyboardType = System.Type.GetType("UnityEngine.InputSystem.Keyboard, Unity.InputSystem");
            if (keyboardType != null)
            {
                var currentKeyboard = keyboardType.GetProperty("current", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
                    ?.GetValue(null, null);
                if (currentKeyboard != null)
                {
                    var spaceKey = keyboardType.GetProperty(keyPropertyName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
                        ?.GetValue(currentKeyboard, null);
                    if (spaceKey != null)
                    {
                        var wasPressed = spaceKey.GetType().GetProperty("wasPressedThisFrame", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                        if (wasPressed != null)
                        {
                            return (bool)wasPressed.GetValue(spaceKey, null);
                        }
                    }
                }
            }

            return false;
        }
#endif

        private static bool WasBackPressedThisFrame()
        {
#if ENABLE_INPUT_SYSTEM
            var keyboardType = System.Type.GetType("UnityEngine.InputSystem.Keyboard, Unity.InputSystem");
            if (keyboardType != null)
            {
                var currentKeyboard = keyboardType.GetProperty("current", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
                    ?.GetValue(null, null);
                if (currentKeyboard != null)
                {
                    var escapeKey = keyboardType.GetProperty("escapeKey", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
                        ?.GetValue(currentKeyboard, null);
                    if (escapeKey != null)
                    {
                        var wasPressed = escapeKey.GetType().GetProperty("wasPressedThisFrame", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                        if (wasPressed != null)
                        {
                            return (bool)wasPressed.GetValue(escapeKey, null);
                        }
                    }
                }
            }

            return false;
#else
            return Input.GetKeyDown(KeyCode.Escape);
#endif
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

            CacheSeatAvatarRefs();

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

        private void CacheSeatAvatarRefs()
        {
            seatAvatarImages.Clear();
            seatIntroGroups.Clear();
            seatAuraObjects.Clear();
            avatarBookLightningFx.Clear();

            foreach (var pair in seatViews)
            {
                var view = pair.Value;
                if (view == null)
                {
                    continue;
                }

                var avatarTransform = view.Root.Find("Avatar");
                if (avatarTransform != null && avatarTransform.TryGetComponent<Image>(out var avatarImage))
                {
                    seatAvatarImages[pair.Key] = avatarImage;
                    var lightningOwner = ResolveAvatarBorderObject(view, avatarImage);
                    var lightning = FindComponentByTypeName(lightningOwner, "_2dxFX_LightningBolt") ??
                                    FindComponentByTypeName(avatarImage.gameObject, "_2dxFX_LightningBolt");
                    if (lightning != null)
                    {
                        avatarBookLightningFx[pair.Key] = lightning;
                    }

                    var aura = avatarTransform.Find("Aura");
                    if (aura != null)
                    {
                        seatAuraObjects[pair.Key] = aura.gameObject;
                        aura.gameObject.SetActive(false);
                    }
                }

                var group = ResolveCanvasGroup(view.gameObject);
                seatIntroGroups[pair.Key] = group;
            }

            DisableBookLightningFxExcept(null);
        }

        private void EnsureRuntimeCardArtTargets()
        {
            foreach (var slot in trickSlots.Values)
            {
                EnsureTrickFaceImage(slot);
            }
        }

        private void CapturePreservedSeatPanelVisuals()
        {
            preservedSeatPanelVisuals.Clear();
            preservedSeatRootScales.Clear();
            foreach (var pair in seatViews)
            {
                var view = pair.Value;
                if (view?.Panel != null)
                {
                    preservedSeatPanelVisuals.Add(view.Panel);
                }

                if (view != null)
                {
                    preservedSeatRootScales[pair.Key] = view.Root.localScale;
                }
            }
        }

        private Image EnsureCardFaceImage(CardButtonView view)
        {
            if (view == null)
            {
                return null;
            }

            if (view.FaceImage != null)
            {
                SetAnchors(view.FaceImage.rectTransform, Vector2.zero, Vector2.one);
                return view.FaceImage;
            }

            var art = new GameObject("Face Art Runtime", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            art.transform.SetParent(view.transform, false);
            art.transform.SetSiblingIndex(0);
            var rect = art.GetComponent<RectTransform>();
            SetAnchors(rect, Vector2.zero, Vector2.one);
            var image = art.GetComponent<Image>();
            image.raycastTarget = false;
            image.preserveAspect = true;
            image.type = Image.Type.Simple;
            image.color = Color.clear;
            image.enabled = false;
            view.FaceImage = image;
            return image;
        }

        private Image EnsureTrickFaceImage(TrickSlotView view)
        {
            if (view == null)
            {
                return null;
            }

            if (view.FaceImage != null)
            {
                SetAnchors(view.FaceImage.rectTransform, Vector2.zero, Vector2.one);
                return view.FaceImage;
            }

            var art = new GameObject("Face Art Runtime", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            art.transform.SetParent(view.transform, false);
            art.transform.SetSiblingIndex(0);
            var rect = art.GetComponent<RectTransform>();
            SetAnchors(rect, Vector2.zero, Vector2.one);
            var image = art.GetComponent<Image>();
            image.raycastTarget = false;
            image.preserveAspect = true;
            image.type = Image.Type.Simple;
            image.color = Color.clear;
            image.enabled = false;
            view.FaceImage = image;
            return image;
        }

        private static void HideFaceImage(Image image)
        {
            if (image == null)
            {
                return;
            }

            image.sprite = null;
            image.color = Color.clear;
            image.enabled = false;
        }

        private static void SetGraphicAlpha(Graphic graphic, float alpha)
        {
            if (graphic == null)
            {
                return;
            }

            var color = graphic.color;
            color.a = alpha;
            graphic.color = color;
        }

        private CardVisualState CaptureCardFaceState(CardButtonView view)
        {
            if (view == null || view.Panel == null || view.RankText == null || view.SuitText == null)
            {
                return new CardVisualState(null, Image.Type.Simple, Color.clear, null, Color.clear, false, Color.clear, Color.clear);
            }

            var artImage = EnsureCardFaceImage(view);
            var artVisible = artImage != null && artImage.enabled && artImage.sprite != null;
            return new CardVisualState(
                view.Panel.sprite,
                view.Panel.type,
                view.Panel.color,
                artVisible ? artImage.sprite : null,
                artImage != null ? artImage.color : Color.clear,
                artVisible,
                view.RankText.color,
                view.SuitText.color);
        }

        private void ApplyCardFaceVisual(CardButtonView view, CardVisualState state)
        {
            if (view == null || view.Panel == null)
            {
                return;
            }

            view.Panel.sprite = state.PanelSprite;
            view.Panel.type = state.PanelType;
            view.Panel.color = state.PanelColor;
            if (state.ArtVisible && state.ArtSprite != null)
            {
                var artImage = EnsureCardFaceImage(view);
                if (artImage != null)
                {
                    artImage.sprite = state.ArtSprite;
                    artImage.color = state.ArtColor;
                    artImage.enabled = true;
                }
            }
            else
            {
                HideFaceImage(view.FaceImage);
            }

            if (view.RankText != null)
            {
                view.RankText.color = state.RankColor;
            }

            if (view.SuitText != null)
            {
                view.SuitText.color = state.SuitColor;
            }
        }

        private Color ResolveCardArtTint(bool isLegal)
        {
            return controller != null &&
                   controller.State.Phase == MatchPhase.TrickPlay &&
                   !isLegal
                ? new Color(1f, 1f, 1f, 0.62f)
                : Color.white;
        }

        private bool TryApplyImportedCardFace(CardButtonView view, Card card, bool isLegal, bool createFaceImage = true)
        {
            var artImage = createFaceImage ? EnsureCardFaceImage(view) : view?.FaceImage;
            if (artImage == null || !BackyardLegendsCardArtCatalog.TryGetFaceSprite(card, out var sprite))
            {
                HideFaceImage(artImage);
                return false;
            }

            artImage.sprite = sprite;
            artImage.color = ResolveCardArtTint(isLegal);
            artImage.enabled = true;
            view.Panel.sprite = null;
            view.Panel.type = Image.Type.Simple;
            view.Panel.color = Color.clear;
            SetGraphicAlpha(view.RankText, 0f);
            SetGraphicAlpha(view.SuitText, 0f);
            return true;
        }

        private bool TryApplyImportedTrickFace(TrickSlotView view, Card card)
        {
            var artImage = EnsureTrickFaceImage(view);
            if (artImage == null || !BackyardLegendsCardArtCatalog.TryGetFaceSprite(card, out var sprite))
            {
                HideFaceImage(artImage);
                return false;
            }

            artImage.sprite = sprite;
            artImage.color = Color.white;
            artImage.enabled = true;
            view.Panel.sprite = null;
            view.Panel.type = Image.Type.Simple;
            view.Panel.color = Color.clear;
            SetGraphicAlpha(view.RankText, 0f);
            SetGraphicAlpha(view.SuitText, 0f);
            return true;
        }

        private void ConfigureUiCallbacks()
        {
            if (sceneRefs.NextRoundButton != null)
            {
                SetButtonLabel(sceneRefs.NextRoundButton, "Continue");
                sceneRefs.NextRoundButton.onClick.RemoveAllListeners();
                sceneRefs.NextRoundButton.onClick.AddListener(StartNextHandFromScoreboard);
            }

            if (sceneRefs.RematchButton != null)
            {
                sceneRefs.RematchButton.onClick.RemoveAllListeners();
                sceneRefs.RematchButton.onClick.AddListener(StartConfiguredMatch);
            }

            if (sceneRefs.ReturnToLobbyButton != null)
            {
                sceneRefs.ReturnToLobbyButton.onClick.RemoveAllListeners();
                sceneRefs.ReturnToLobbyButton.onClick.AddListener(OpenBackWarning);
            }

            if (sceneRefs.BackButton != null)
            {
                sceneRefs.BackButton.onClick.RemoveAllListeners();
                sceneRefs.BackButton.onClick.AddListener(ToggleOptionsMenu);
            }

            if (sceneRefs.ClaimTheRestButton != null)
            {
                sceneRefs.ClaimTheRestButton.onClick.RemoveAllListeners();
                sceneRefs.ClaimTheRestButton.gameObject.SetActive(false);
            }

            if (sceneRefs.ForfeitMatchButton != null)
            {
                sceneRefs.ForfeitMatchButton.onClick.RemoveAllListeners();
                sceneRefs.ForfeitMatchButton.onClick.AddListener(OpenForfeitWarning);
            }

            if (sceneRefs.LeaveTableButton != null)
            {
                sceneRefs.LeaveTableButton.onClick.RemoveAllListeners();
                sceneRefs.LeaveTableButton.onClick.AddListener(OpenBackWarning);
            }

            if (sceneRefs.SfxToggleButton != null)
            {
                sceneRefs.SfxToggleButton.onClick.RemoveAllListeners();
                sceneRefs.SfxToggleButton.onClick.AddListener(ToggleSfxMuted);
            }

            if (sceneRefs.CloseOptionsMenuButton != null)
            {
                sceneRefs.CloseOptionsMenuButton.onClick.RemoveAllListeners();
                sceneRefs.CloseOptionsMenuButton.onClick.AddListener(CloseOptionsMenu);
            }

            if (sceneRefs.ExitPromptCancelButton != null)
            {
                sceneRefs.ExitPromptCancelButton.onClick.RemoveAllListeners();
                sceneRefs.ExitPromptCancelButton.onClick.AddListener(CloseBackWarning);
            }

            if (sceneRefs.ExitPromptConfirmButton != null)
            {
                sceneRefs.ExitPromptConfirmButton.onClick.RemoveAllListeners();
                sceneRefs.ExitPromptConfirmButton.onClick.AddListener(ConfirmActivePrompt);
            }

            sceneRefs.PlaySelectedButton.onClick.RemoveAllListeners();
            sceneRefs.PlaySelectedButton.onClick.AddListener(OnPlaySelected);
            if (sceneRefs.ConfirmBidButton != null)
            {
                sceneRefs.ConfirmBidButton.onClick.RemoveAllListeners();
                sceneRefs.ConfirmBidButton.onClick.AddListener(ConfirmSelectedBid);
            }

            if (sceneRefs.DealButton != null)
            {
                sceneRefs.DealButton.onClick.RemoveAllListeners();
                sceneRefs.DealButton.gameObject.SetActive(false);
            }

            foreach (var pair in bidButtons)
            {
                var localBid = pair.Key;
                pair.Value.onClick.RemoveAllListeners();
                pair.Value.onClick.AddListener(() => SelectBid(localBid));
            }

            BindScoreboardActions(sceneRefs.RoundScoreboardView);
            BindScoreboardActions(sceneRefs.EndScoreboardView);
        }

        private void BindScoreboardActions(EndOfHandScoreboardView scoreboardView)
        {
            if (scoreboardView == null)
            {
                return;
            }

            scoreboardView.BindActions(null, StartNextHandFromScoreboard, StartConfiguredMatch, OpenBackWarning);
        }

        private void StartNextHandFromScoreboard()
        {
            if (controller == null)
            {
                return;
            }

            SetSheetVisible(sceneRefs.RoundSheet, false);
            SetSheetVisible(sceneRefs.EndSheet, false);
            lastRenderedHand.Clear();
            selectedCard = null;
            controller.StartNextRound();
            RenderAll();
            ScheduleAiLoop();
        }

        private void ConfigureFeedbackAudio()
        {
            feedbackAudioSource = soundFxAudioSource != null
                ? soundFxAudioSource
                : FindSoundFxAudioSource();
            var usingExternalSoundFxSource = feedbackAudioSource != null;
            if (!usingExternalSoundFxSource)
            {
                feedbackAudioSource = GetComponent<AudioSource>();
                if (feedbackAudioSource == null)
                {
                    feedbackAudioSource = gameObject.AddComponent<AudioSource>();
                }
            }

            feedbackAudioSource.playOnAwake = false;
            feedbackAudioSource.loop = false;
            feedbackAudioSource.spatialBlend = 0f;
            if (!usingExternalSoundFxSource)
            {
                feedbackAudioSource.volume = 0.18f;
            }

            bidClip = ResolveFeedbackClip(bidClipAsset, "Bid_Lock", () => CreateToneClip("Bid Cue", 680f, 920f, 0.09f, 0.16f));
            selectClip = ResolveFeedbackClip(selectClipAsset, "Ui_Select", () => CreateToneClip("Select Cue", 520f, 760f, 0.05f, 0.13f));
            playClip = ResolveFeedbackClip(playClipAsset, "Card_Play", () => CreateToneClip("Play Cue", 430f, 700f, 0.07f, 0.17f));
            collectClip = ResolveFeedbackClip(collectClipAsset, "Trick_Collect", () => CreateToneClip("Collect Cue", 360f, 580f, 0.13f, 0.2f));
            bannerClip = ResolveFeedbackClip(bannerClipAsset, "Banner_Pop", () => CreateToneClip("Banner Cue", 720f, 1080f, 0.16f, 0.14f));
            dealClip = ResolveFeedbackClip(dealClipAsset, "Card_Deal", () => CreateToneClip("Deal Cue", 250f, 410f, 0.18f, 0.2f));
            invalidClip = ResolveFeedbackClip(invalidClipAsset, "Ui_Invalid", () => CreateToneClip("Invalid Cue", 240f, 160f, 0.1f, 0.16f));
            roundScoreClip = ResolveFeedbackClip(roundScoreClipAsset, "Score_Punch", () => CreateToneClip("Round Score Cue", 560f, 820f, 0.2f, 0.18f));
            matchEndClip = ResolveFeedbackClip(matchEndClipAsset, "Match_End", () => CreateToneClip("Match End Cue", 460f, 920f, 0.28f, 0.22f));
            setBookClip = ResolveFeedbackClip(setBookClipAsset, "Set_Book_Impact", CreateSetBookImpactClip);
            avatarRouletteClip = ResolveFeedbackClip(avatarRouletteClipAsset, "Avatar_Roulette", () => CreateToneClip("Avatar Roulette Cue", 760f, 980f, 0.035f, 0.08f));
            avatarAssignedClip = ResolveFeedbackClip(avatarAssignedClipAsset, "Avatar_Assigned", () => CreateToneClip("Avatar Assigned Cue", 420f, 840f, 0.16f, 0.14f));
            bidPanelOpenClip = ResolveFeedbackClip(bidPanelOpenClipAsset, "Bid_Panel_Open", () => CreateToneClip("Bid Panel Open Cue", 300f, 560f, 0.14f, 0.12f));
            optionsMenuOpenClip = ResolveFeedbackClip(optionsMenuOpenClipAsset, "Ui_Menu_Open", () => CreateToneClip("Options Menu Open Cue", 420f, 900f, 0.16f, 0.13f));
            optionsMenuCloseClip = ResolveFeedbackClip(optionsMenuCloseClipAsset, "Ui_Menu_Close", () => CreateToneClip("Options Menu Close Cue", 720f, 280f, 0.13f, 0.11f));
            cardPlaceClips = ResolveCardPlaceClips();
            setBookClips = ResolveAudioClips(SetBookAudioResourceNames, setBookClip);
            bookWinClips = ResolveAudioClips(BookWinAudioResourceNames, collectClip);
            bookWhooshClips = ResolveAudioClips(BookWhooshAudioResourceNames, null);
            tableSlamClips = ResolveAudioClips(TableSlamAudioResourceNames, setBookClip);
            graffitiSprayClips = ResolveAudioClips(GraffitiSprayAudioResourceNames, null);
            spadesBrokenClips = ResolveAudioClips(SpadesBrokenAudioResourceNames, null);
            crowdReactionClips = ResolveAudioClips(CrowdReactionAudioResourceNames, null);
            SetSfxMuted(PlayerPrefs.GetInt(SfxMutedPlayerPrefsKey, 0) == 1, false);
        }

        private void ToggleSfxMuted()
        {
            SetSfxMuted(!sfxMuted, true);
            RenderOptionsMenu();
            if (!sfxMuted)
            {
                PlayFeedback(FeedbackCue.Select, 0.12f);
            }
        }

        private void SetSfxMuted(bool muted, bool persist)
        {
            sfxMuted = muted;
            if (feedbackAudioSource != null)
            {
                feedbackAudioSource.mute = sfxMuted;
            }

            if (persist)
            {
                PlayerPrefs.SetInt(SfxMutedPlayerPrefsKey, sfxMuted ? 1 : 0);
                PlayerPrefs.Save();
            }

            RenderSfxToggleLabel();
        }

        private void RenderSfxToggleLabel()
        {
            SetButtonLabel(sceneRefs != null ? sceneRefs.SfxToggleButton : null, sfxMuted ? "SFX: OFF" : "SFX: ON");
        }

        private static AudioClip ResolveFeedbackClip(AudioClip overrideClip, string resourceName, System.Func<AudioClip> fallbackFactory)
        {
            if (overrideClip != null)
            {
                return overrideClip;
            }

            return BackyardLegendsStreetAudio.LoadSfx(resourceName) ?? fallbackFactory();
        }

        private AudioClip[] ResolveCardPlaceClips()
        {
            var overrideClips = cardPlaceClipAssets?
                .Where(clip => clip != null)
                .ToArray();
            if (overrideClips != null && overrideClips.Length > 0)
            {
                return overrideClips;
            }

            return CardPlaceAudioResourceNames
                .Select(BackyardLegendsStreetAudio.LoadSfx)
                .Where(clip => clip != null)
                .ToArray();
        }

        private static AudioClip[] ResolveAudioClips(IEnumerable<string> resourceNames, AudioClip fallbackClip)
        {
            var clips = resourceNames
                .Select(BackyardLegendsStreetAudio.LoadSfx)
                .Where(clip => clip != null)
                .ToArray();
            return clips.Length > 0
                ? clips
                : new[] { fallbackClip }.Where(clip => clip != null).ToArray();
        }

        private AudioSource FindSoundFxAudioSource()
        {
            var scene = gameObject.scene;
            return Resources.FindObjectsOfTypeAll<AudioSource>()
                .Where(source => source != null &&
                                 source.gameObject.scene == scene &&
                                 source.gameObject.name == "Sound FX")
                .OrderByDescending(source => source.gameObject.activeInHierarchy)
                .FirstOrDefault();
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

        private static AudioClip CreateSetBookImpactClip()
        {
            const int sampleRate = 22050;
            const float duration = 0.36f;
            var sampleCount = Mathf.CeilToInt(duration * sampleRate);
            var samples = new float[sampleCount];
            for (var i = 0; i < sampleCount; i++)
            {
                var t = i / (float)sampleRate;
                var normalized = t / duration;
                var thumpFrequency = Mathf.Lerp(120f, 48f, normalized);
                var thump = Mathf.Sin(2f * Mathf.PI * thumpFrequency * t) * Mathf.Exp(-8f * normalized);
                var crack = Mathf.Sin(2f * Mathf.PI * 940f * t) * Mathf.Exp(-24f * normalized);
                var afterHitTime = Mathf.Max(0f, t - 0.12f);
                var afterHit = t >= 0.12f
                    ? Mathf.Sin(2f * Mathf.PI * 420f * afterHitTime) * Mathf.Exp(-18f * afterHitTime)
                    : 0f;
                samples[i] = Mathf.Clamp((thump * 0.95f + crack * 0.38f + afterHit * 0.42f) * 0.34f, -1f, 1f);
            }

            var clip = AudioClip.Create("Set Book Impact Cue", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private void PlayFeedback(FeedbackCue cue, float volumeScale = 1f)
        {
            if (feedbackAudioSource == null || sfxMuted)
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
                FeedbackCue.Deal => dealClip,
                FeedbackCue.Invalid => invalidClip,
                FeedbackCue.RoundScore => roundScoreClip,
                FeedbackCue.MatchEnd => matchEndClip,
                FeedbackCue.SetBook => setBookClip,
                FeedbackCue.AvatarRoulette => avatarRouletteClip,
                FeedbackCue.AvatarAssigned => avatarAssignedClip,
                FeedbackCue.BidPanelOpen => bidPanelOpenClip,
                FeedbackCue.OptionsMenuOpen => optionsMenuOpenClip,
                FeedbackCue.OptionsMenuClose => optionsMenuCloseClip,
                _ => null
            };

            if (clip != null)
            {
                feedbackAudioSource.PlayOneShot(clip, Mathf.Clamp(volumeScale, 0f, 1f));
            }
        }

        private void PlayRandomCardPlaceSound()
        {
            if (feedbackAudioSource == null || sfxMuted)
            {
                return;
            }

            var clips = cardPlaceClips?
                .Where(clip => clip != null)
                .ToArray();
            if (clips == null || clips.Length == 0)
            {
                PlayFeedback(FeedbackCue.Play, 0.18f);
                return;
            }

            var clip = clips[Random.Range(0, clips.Length)];
            feedbackAudioSource.PlayOneShot(clip, 0.28f);
        }

        private void PlayAvatarRouletteCue()
        {
            if (Time.unscaledTime < nextAvatarRouletteCueTime)
            {
                return;
            }

            nextAvatarRouletteCueTime = Time.unscaledTime + 0.095f;
            PlayFeedback(FeedbackCue.AvatarRoulette, 0.11f);
        }

        private void PlaySetBookSound()
        {
            if (feedbackAudioSource == null || sfxMuted)
            {
                return;
            }

            var impact = PickRandomClip(setBookClips, ref lastSetBookClipIndex) ??
                         PickRandomClip(bookWinClips, ref lastBookWinClipIndex);
            if (impact == null)
            {
                PlayFeedback(FeedbackCue.SetBook, 1f);
                return;
            }

            feedbackAudioSource.PlayOneShot(impact, 2.25f);
            var whoosh = PickRandomClip(bookWhooshClips, ref lastBookWhooshClipIndex);
            if (whoosh != null)
            {
                feedbackAudioSource.PlayOneShot(whoosh, 0.95f);
            }

            var extra = PickRandomClip(bookWinClips, ref lastBookWinClipIndex);
            if (extra != null && extra != impact)
            {
                feedbackAudioSource.PlayOneShot(extra, 1.05f);
            }
        }

        private void PlayBookWonSound()
        {
            if (feedbackAudioSource == null || sfxMuted)
            {
                return;
            }

            var impact = PickRandomClip(bookWinClips, ref lastBookWinClipIndex);
            if (impact == null)
            {
                PlayFeedback(FeedbackCue.Collect, 0.5f);
                return;
            }

            feedbackAudioSource.PlayOneShot(impact, 1.18f);
            if (Random.value < 0.48f)
            {
                var whoosh = PickRandomClip(bookWhooshClips, ref lastBookWhooshClipIndex);
                if (whoosh != null)
                {
                    feedbackAudioSource.PlayOneShot(whoosh, 0.38f);
                }
            }
        }

        private void PlayTableSlamSound()
        {
            if (feedbackAudioSource == null || sfxMuted)
            {
                return;
            }

            var impact = PickRandomClip(tableSlamClips, ref lastTableSlamClipIndex) ??
                         PickRandomClip(setBookClips, ref lastSetBookClipIndex) ??
                         PickRandomClip(bookWinClips, ref lastBookWinClipIndex);
            if (impact == null)
            {
                PlayFeedback(FeedbackCue.SetBook, 1f);
                return;
            }

            feedbackAudioSource.PlayOneShot(impact, impact.length > 0.8f ? 0.95f : 1.72f);
            var whoosh = PickRandomClip(bookWhooshClips, ref lastBookWhooshClipIndex);
            if (whoosh != null && Random.value < 0.42f)
            {
                feedbackAudioSource.PlayOneShot(whoosh, 0.34f);
            }
        }

        private void PlayGraffitiSpraySound()
        {
            if (feedbackAudioSource == null || sfxMuted)
            {
                return;
            }

            var spray = PickRandomClip(graffitiSprayClips, ref lastGraffitiSprayClipIndex);
            if (spray == null)
            {
                PlayFeedback(FeedbackCue.Bid, 0.16f);
                return;
            }

            feedbackAudioSource.PlayOneShot(spray, spray.length > 0.55f ? 0.32f : 0.46f);
        }

        private void PlaySpadesBrokenSound()
        {
            if (feedbackAudioSource == null || sfxMuted)
            {
                return;
            }

            var metalHit = PickRandomClip(spadesBrokenClips, ref lastSpadesBrokenClipIndex);
            if (metalHit == null)
            {
                PlayFeedback(FeedbackCue.Invalid, 0.28f);
                return;
            }

            feedbackAudioSource.PlayOneShot(metalHit, 1.05f);
        }

        private void TryPlayCrowdReaction(float chance, float volumeScale, float delaySeconds = 0f, bool ignoreCooldown = false)
        {
            if (feedbackAudioSource == null || sfxMuted || Random.value > chance)
            {
                return;
            }

            if (!ignoreCooldown && Time.unscaledTime < nextCrowdReactionTime)
            {
                return;
            }

            nextCrowdReactionTime = Time.unscaledTime + Mathf.Max(0.45f, delaySeconds) + Random.Range(1.05f, 1.85f);
            if (delaySeconds > 0f)
            {
                StartCoroutine(PlayCrowdReactionAfterDelay(delaySeconds, volumeScale));
                return;
            }

            PlayCrowdReactionNow(volumeScale);
        }

        private IEnumerator PlayCrowdReactionAfterDelay(float delaySeconds, float volumeScale)
        {
            yield return new WaitForSecondsRealtime(Mathf.Max(0.01f, delaySeconds));
            PlayCrowdReactionNow(volumeScale);
        }

        private void PlayCrowdReactionNow(float volumeScale)
        {
            if (feedbackAudioSource == null || sfxMuted)
            {
                return;
            }

            var reaction = PickRandomClip(crowdReactionClips, ref lastCrowdReactionClipIndex);
            if (reaction == null)
            {
                return;
            }

            feedbackAudioSource.PlayOneShot(reaction, Mathf.Clamp(volumeScale * Random.Range(0.94f, 1.18f), 0.24f, 1.15f));
        }

        private static AudioClip PickRandomClip(AudioClip[] clips, ref int lastIndex)
        {
            if (clips == null || clips.Length == 0)
            {
                return null;
            }

            if (clips.Length == 1)
            {
                lastIndex = 0;
                return clips[0];
            }

            var index = Random.Range(0, clips.Length);
            if (index == lastIndex)
            {
                index = (index + 1) % clips.Length;
            }

            lastIndex = index;
            return clips[index];
        }

        private void ApplyTheme()
        {
            ApplyThemeText(sceneRefs.StatusText, theme.mutedText, 18, FontStyle.Normal);
            ApplyThemeText(sceneRefs.HudModeText, theme.primaryText, 22, FontStyle.Bold);
            ApplyThemeText(sceneRefs.TimerHookText, theme.mutedText, 16, FontStyle.Bold);
            ApplyThemeText(sceneRefs.HomeScoreText, theme.backgroundColor, 14, FontStyle.Bold);
            ApplyThemeText(sceneRefs.AwayScoreText, theme.backgroundColor, 14, FontStyle.Bold);
            ApplyThemeText(sceneRefs.HomeDeltaText, theme.green, 18, FontStyle.Bold);
            ApplyThemeText(sceneRefs.AwayDeltaText, theme.red, 18, FontStyle.Bold);
            ApplyThemeText(sceneRefs.LastTrickText, theme.mutedText, 18, FontStyle.Normal);
            ApplyThemeText(lastTrickTitleText, theme.mutedText, 13, FontStyle.Bold);
            ApplyThemeText(sceneRefs.FeedText, theme.mutedText, 15, FontStyle.Normal);
            ApplyThemeText(sceneRefs.CenterHintText, theme.primaryText, 22, FontStyle.Bold);
            EnsureFallbackFont(sceneRefs.DeckAnchorText);
            EnsureFallbackFont(sceneRefs.DiscardAnchorText);
            ApplyThemeText(sceneRefs.OpeningStackText, theme.gold, 24, FontStyle.Bold);
            if (sceneRefs.RoundScoreboardView == null)
            {
                ApplyThemeText(sceneRefs.RoundSummaryText, theme.primaryText, 20, FontStyle.Normal);
                ConfigureWrapSummaryText(sceneRefs.RoundSummaryText, new Vector2(0.08f, 0.18f), new Vector2(0.92f, 0.76f), 20, 16);
            }

            if (sceneRefs.EndScoreboardView == null)
            {
                ApplyThemeText(sceneRefs.EndSummaryText, theme.primaryText, 20, FontStyle.Normal);
                ConfigureWrapSummaryText(sceneRefs.EndSummaryText, new Vector2(0.08f, 0.20f), new Vector2(0.92f, 0.78f), 20, 14);
            }

            ApplyThemeText(sceneRefs.BannerText, theme.gold, 32, FontStyle.Bold);
            ApplyThemeText(sceneRefs.OptionsMenuTitleText, theme.gold, 22, FontStyle.Bold);
            if (sceneRefs.BackgroundImage != null)
            {
                ApplyFallbackSprite(
                    sceneRefs.BackgroundImage,
                    theme.tableBackgroundSprite != null
                        ? theme.tableBackgroundSprite
                        : ThemeSpriteFactory.CreateBackgroundSprite(theme.backgroundSecondary, theme.backgroundColor));
                sceneRefs.BackgroundImage.color = Color.white;
            }

            ApplyThemedImage(sceneRefs.HudPanel, theme.panelColor, ResolvePanelSprite());
            ApplyThemedImage(sceneRefs.TablePanel, new Color(0.18f, 0.19f, 0.21f, 0.9f), ResolvePanelSprite());
            ApplyThemedImage(sceneRefs.HandPanel, theme.panelColor, ResolvePanelSprite());
            ApplyThemedImage(sceneRefs.FeedPanel, new Color(1f, 1f, 1f, 0.18f), ResolveSoftPanelSprite());
            ApplyThemedImage(lastTrickPanel, new Color(1f, 1f, 1f, 0.13f), ResolveSoftPanelSprite());
            ApplyFallbackSprite(sceneRefs.DeckAnchorImage, ResolveSoftPanelSprite());
            ApplyFallbackSprite(sceneRefs.DiscardAnchorImage, ResolveSoftPanelSprite());
            ApplyThemedImage(sceneRefs.OpeningStackImage, Color.white, ResolveCardBackSprite());
            var sheetTint = new Color(0.15f, 0.16f, 0.18f, 0.98f);
            ApplyThemedImage(sceneRefs.BidSheetImage, sheetTint, ResolveSheetSprite());
            ApplyThemedImage(sceneRefs.OptionsMenuImage, sheetTint, ResolveSheetSprite());
            if (sceneRefs.RoundScoreboardView == null)
            {
                ApplyThemedImage(sceneRefs.RoundSheetImage, sheetTint, ResolveSheetSprite());
            }

            if (sceneRefs.EndScoreboardView == null)
            {
                ApplyThemedImage(sceneRefs.EndSheetImage, sheetTint, ResolveSheetSprite());
            }
            ConfigureChipImage(sceneRefs.HomeScoreText, theme.green);
            ConfigureChipImage(sceneRefs.AwayScoreText, theme.red);
            ConfigureScoreboardLayout();

            foreach (var pair in seatViews)
            {
                var view = pair.Value;
                if (view != null && view.Panel != null)
                {
                    if (!ShouldPreserveSeatPanelVisual(view.Panel))
                    {
                        ApplyFallbackSprite(view.Panel, ResolvePanelSprite());
                        view.Panel.color = GetSeatPanelTint(pair.Key);
                    }
                }

                if (view?.BidCalloutPanel != null)
                {
                    ApplyFallbackSprite(view.BidCalloutPanel, theme.buttonSprite != null ? theme.buttonSprite : ResolveSoftPanelSprite());
                    view.BidCalloutPanel.color = new Color(0.15f, 0.16f, 0.18f, 0.96f);
                }

                if (view?.BidCalloutSplash != null)
                {
                    view.BidCalloutSplash.sprite = ResolveGraffitiSplashSprite();
                    view.BidCalloutSplash.color = new Color(1f, 1f, 1f, 0f);
                }

                if (view?.BidCalloutText != null)
                {
                    ApplyThemeText(view.BidCalloutText, theme.primaryText, BidCalloutFontSize, FontStyle.Bold);
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
                    ApplyFallbackSprite(slot.Panel, ResolveSoftPanelSprite());
                    slot.Panel.color = Color.white;
                }
            }

            TintButtonIfNotScoreboard(sceneRefs.NextRoundButton, theme.green);
            TintButtonIfNotScoreboard(sceneRefs.RematchButton, theme.green);
            TintButton(sceneRefs.BackButton, theme.panelStroke);
            TintButton(sceneRefs.ClaimTheRestButton, theme.gold);
            TintButton(sceneRefs.ForfeitMatchButton, theme.red);
            TintButton(sceneRefs.LeaveTableButton, theme.panelStroke);
            TintButton(sceneRefs.SfxToggleButton, theme.gold);
            TintButton(sceneRefs.CloseOptionsMenuButton, theme.green);
            TintButtonIfNotScoreboard(sceneRefs.ReturnToLobbyButton, theme.panelStroke);
            TintButton(sceneRefs.DealButton, theme.green);
            CacheBidButtonSprites();
            foreach (var pair in bidButtons)
            {
                ApplyBidButtonVisual(pair.Value, true, false);
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
            pendingBidSelection = null;
            lastRenderedHand.Clear();
            HideAllBidBubbles(true);
            ClearOpeningDealRuntimeSeatCards();
            if (avatarIntroLoop != null)
            {
                StopCoroutine(avatarIntroLoop);
                avatarIntroLoop = null;
            }

            avatarIntroRunning = true;
            openingDealPending = true;
            openingDealRunning = false;
            handReviewPending = false;
            bidTurnDelayPending = false;
            suppressNextHandEntryAnimation = false;
            optionsMenuOpen = false;
            exitPromptOpen = false;
            activePrompt = ConfirmationPromptType.None;
            if (handReviewLoop != null)
            {
                StopCoroutine(handReviewLoop);
                handReviewLoop = null;
            }

            if (bidTurnDelayLoop != null)
            {
                StopCoroutine(bidTurnDelayLoop);
                bidTurnDelayLoop = null;
            }

            if (dealButtonFadeLoop != null)
            {
                StopCoroutine(dealButtonFadeLoop);
                dealButtonFadeLoop = null;
            }

            ResetDealButtonVisualState();
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
            SetBidSheetVisible(false);
            SetSheetVisible(sceneRefs.RoundSheet, false);
            SetSheetVisible(sceneRefs.EndSheet, false);
            SetOptionsMenuVisibleImmediate(false);
            SetSheetVisible(sceneRefs.ExitPromptOverlay, false);
            ResetExitPromptVisualState();
            RenderAll();
            StartAvatarIntro();
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
                    spadesBrokenMomentShown = false;
                    AddFeedMessage($"Round {controller.State.RoundState.RoundNumber} started. Dealer: {controller.State.SeatNames[controller.State.RoundState.Dealer]}.");
                    if (!openingDealPending && !openingDealRunning)
                    {
                        BeginHandReview();
                    }

                    break;
                case BidSubmittedEvent bidEvent:
                    AddFeedMessage($"{controller.State.SeatNames[bidEvent.Seat]} called {(bidEvent.Bid == 0 ? "Nil" : bidEvent.Bid.ToString())}.");
                    ShowBidCallout(bidEvent.Seat, bidEvent.Bid);
                    PlayFeedback(FeedbackCue.Bid, 0.22f);
                    if (controller.State.Phase == MatchPhase.Bidding)
                    {
                        BeginBidTurnDelay();
                    }

                    break;
                case CardPlayedEvent playedEvent:
                    AddFeedMessage($"{controller.State.SeatNames[playedEvent.Seat]} dropped {playedEvent.Card.ShortLabel}.");
                    var triggerSpadesBrokenMoment = ShouldTriggerSpadesBrokenMoment(playedEvent);
                    if (triggerSpadesBrokenMoment)
                    {
                        spadesBrokenMomentShown = true;
                        AddFeedMessage("Street rules changed. Spades are broken.");
                    }

                    QueueCardPlayAnimation(playedEvent, triggerSpadesBrokenMoment);
                    break;
                case TrickResolvedEvent trickEvent:
                    AddFeedMessage($"{controller.State.SeatNames[trickEvent.Winner]} took the hand.");
                    QueueTrickCollectionAnimation(trickEvent);
                    break;
                case SetBookReachedEvent setBook:
                    QueueSetBookMoment(setBook.Team);
                    AddFeedMessage(setBook.Team == TeamId.Home ? "Home team missed its contract." : "Rivals missed their contract.");
                    break;
                case RemainingBooksClaimedEvent claimedEvent:
                    AddFeedMessage(claimedEvent.Team == TeamId.Home
                        ? $"Home claimed the final {claimedEvent.ClaimedBooks} {BookLabel(claimedEvent.ClaimedBooks)}."
                        : $"Rivals claimed the final {claimedEvent.ClaimedBooks} {BookLabel(claimedEvent.ClaimedBooks)}.");
                    ShowBanner("CLAIMED", claimedEvent.Team == TeamId.Home ? theme.green : theme.red);
                    PlayFeedback(FeedbackCue.RoundScore, 0.22f);
                    break;
                case RoundScoredEvent:
                    if (controller.State.Phase == MatchPhase.MatchEnded)
                    {
                        break;
                    }

                    if (sceneRefs.RoundSummaryText != null)
                    {
                        sceneRefs.RoundSummaryText.text = BuildRoundSummaryText();
                    }

                    RenderScoreboard(sceneRefs.RoundScoreboardView, false, null);
                    AddFeedMessage("Round scored. Review the wrap and keep playing.");
                    PlayFeedback(FeedbackCue.RoundScore, 0.2f);
                    pendingRoundSheetOpen = true;
                    pendingEndSheetOpen = false;
                    ScheduleDeferredSheetState();
                    break;
                case MatchForfeitedEvent forfeited:
                    AddFeedMessage(forfeited.ForfeitingTeam == TeamId.Home
                        ? "Home team forfeited. Rivals win the match."
                        : "Rivals forfeited. Home team wins the match.");
                    ShowBanner("FORFEIT", theme.red);
                    break;
                case MatchEndedEvent ended:
                    if (sceneRefs.EndSummaryText != null)
                    {
                        sceneRefs.EndSummaryText.text = BuildMatchSummaryText(ended.WinningTeam);
                    }

                    RenderScoreboard(sceneRefs.EndScoreboardView, true, ended.WinningTeam);
                    AddFeedMessage(ended.WinningTeam == TeamId.Home ? "Home team closed the match." : "Rivals closed the match.");
                    PlayFeedback(FeedbackCue.MatchEnd, 0.24f);
                    pendingRoundSheetOpen = false;
                    pendingEndSheetOpen = true;
                    ScheduleDeferredSheetState();
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
            sceneRefs.TimerHookText.text = BuildTurnIndicatorText();
            sceneRefs.StatusText.text = controller.State.RoundState.LastStatusMessage;
            sceneRefs.HomeScoreText.text = BuildScoreboardText(TeamId.Home);
            sceneRefs.AwayScoreText.text = BuildScoreboardText(TeamId.Away);
            sceneRefs.StatusText.text = openingDealPending
                ? openingDealRunning
                    ? "Dealing from center table."
                    : openingStackIntroRunning
                        ? "Deck is set at center table."
                        : "Cards deal automatically."
                : handReviewPending
                    ? "Count your books before bidding."
                : bidTurnDelayPending
                    ? "Next bid is coming up."
                : controller.State.RoundState.LastStatusMessage;
            sceneRefs.CenterHintText.text = openingDealPending
                ? openingDealRunning
                    ? "Cards are spreading across the table."
                    : openingStackIntroRunning
                        ? "All cards start from the center stack."
                        : "Getting ready to deal."
                : handReviewPending
                    ? "Review your hand. Bidding opens in a moment."
                : bidTurnDelayPending
                    ? $"{controller.State.SeatNames[controller.State.RoundState.BidState.CurrentBidder]} is counting books."
                : controller.State.Phase switch
            {
                MatchPhase.Bidding => controller.State.RoundState.BidState.CurrentBidder == SeatId.Bottom ? "Choose a bid, then confirm it." : $"{controller.State.SeatNames[controller.State.RoundState.BidState.CurrentBidder]} is bidding.",
                MatchPhase.TrickPlay => controller.State.RoundState.TrickState.CurrentTurn == SeatId.Bottom ? "Tap a card once to select, tap again to play." : $"{controller.State.SeatNames[controller.State.RoundState.TrickState.CurrentTurn]} is on the clock.",
                MatchPhase.RoundSummary => "Round scored. Review the wrap and continue.",
                MatchPhase.MatchEnded => "Match complete. Run it back or head to the lobby.",
                _ => "Ready."
            };
            if (sceneRefs.DealButton != null)
            {
                sceneRefs.DealButton.interactable = false;
                sceneRefs.DealButton.gameObject.SetActive(false);
            }

            if (sceneRefs.OpeningStackImage != null)
            {
                var openingStackVisible = openingDealPending && !openingDealRunning;
                sceneRefs.OpeningStackImage.gameObject.SetActive(openingStackVisible);
                sceneRefs.OpeningStackImage.color = new Color(1f, 1f, 1f,
                    openingDealPending && !openingDealRunning && (openingStackIntroRunning || openingStackPreviewCards.Count > 0)
                        ? 0f
                        : 1f);
            }

            if (sceneRefs.OpeningStackText != null)
            {
                sceneRefs.OpeningStackText.gameObject.SetActive(openingDealPending && !openingDealRunning && !openingStackIntroRunning && openingStackPreviewCards.Count == 0);
            }

            SetDeckCountersVisible(false);

            if (openingDealPending || openingDealRunning)
            {
                RefreshOpeningStackEffectVisual();
            }

            if (openingStackEffectImage != null)
            {
                openingStackEffectImage.gameObject.SetActive(false);
            }

            RenderLastTrickDisplay();
            sceneRefs.FeedText.text = recentFeed.Count == 0 ? "TABLE FEED\nNo hands yet." : "TABLE FEED\n" + string.Join("\n", recentFeed.Reverse());

            UpdateCenterHintLayout();
            RenderSeatPanels();
            RenderTrickArea();
            RenderOpponentHands();
            RenderHand();
            RenderBidSheet();
            RenderOptionsMenuButton();
            RenderOptionsMenu();
            ApplyAvatarIntroVisibility();
        }

        private void RenderLastTrickDisplay()
        {
            var lastTrick = controller.State.RoundState.CompletedTricks.LastOrDefault();
            var hasLastTrick = lastTrick != null && lastTrick.Count > 0;
            var hasCardDisplay = lastTrickPanel != null && lastTrickCardViews.Count > 0;

            if (!hasCardDisplay)
            {
                if (sceneRefs.LastTrickText != null)
                {
                    sceneRefs.LastTrickText.gameObject.SetActive(true);
                    sceneRefs.LastTrickText.text = $"Previous book: {controller.DescribeLastTrick()}";
                }

                return;
            }

            if (sceneRefs.LastTrickText != null)
            {
                sceneRefs.LastTrickText.gameObject.SetActive(false);
            }

            lastTrickPanel.gameObject.SetActive(hasLastTrick);
            if (lastTrickGroup != null)
            {
                lastTrickGroup.blocksRaycasts = false;
                lastTrickGroup.interactable = false;
            }

            if (!hasLastTrick)
            {
                StopLastTrickDisplayAnimation();
                lastTrickSignature = null;
                foreach (var view in lastTrickCardViews)
                {
                    view.gameObject.SetActive(false);
                }

                return;
            }

            if (lastTrickTitleText != null)
            {
                lastTrickTitleText.text = "LAST HAND PLAYED";
            }

            var signature = BuildLastTrickSignature(lastTrick);
            var shouldAnimate = signature != lastTrickSignature;
            if (lastTrickDisplayLoop == null || shouldAnimate)
            {
                ApplyLastTrickTargetLayout(lastTrick.Count);
            }
            for (var index = 0; index < lastTrickCardViews.Count; index++)
            {
                var view = lastTrickCardViews[index];
                var isVisible = index < lastTrick.Count;
                view.gameObject.SetActive(isVisible);
                if (isVisible)
                {
                    ConfigureLastTrickCardView(view, lastTrick[index]);
                }
            }

            if (shouldAnimate)
            {
                lastTrickSignature = signature;
                PlayLastTrickDisplayAnimation(lastTrick.Count);
            }
        }

        private void ApplyLastTrickTargetLayout(int visibleCount)
        {
            if (visibleCount <= 0)
            {
                return;
            }

            for (var index = 0; index < lastTrickCardViews.Count; index++)
            {
                var view = lastTrickCardViews[index];
                var pose = ResolveLastTrickTargetPose(index, visibleCount);
                view.Root.sizeDelta = pose.Size;
                view.Root.anchoredPosition = pose.Position;
                view.Root.localRotation = pose.Rotation;
                view.Root.localScale = pose.Scale;
            }
        }

        private void ConfigureLastTrickCardView(CardButtonView view, TrickPlay play)
        {
            var card = play.Card;
            view.RankText.font = theme.ResolveFont();
            view.SuitText.font = theme.ResolveFont();
            view.RankText.text = card.RankLabel;
            view.SuitText.text = card.SuitIcon;
            view.RankText.resizeTextForBestFit = true;
            view.SuitText.resizeTextForBestFit = true;
            view.RankText.resizeTextMinSize = 8;
            view.SuitText.resizeTextMinSize = 8;
            view.RankText.resizeTextMaxSize = 18;
            view.SuitText.resizeTextMaxSize = 20;
            view.Panel.sprite = theme.cardFaceDefaultSprite != null ? theme.cardFaceDefaultSprite : ResolveCardSprite(false, true);
            view.Panel.type = Image.Type.Sliced;
            view.Panel.color = Color.white;
            if (view.CanvasGroup != null)
            {
                view.CanvasGroup.alpha = 0.92f;
            }

            if (!TryApplyImportedCardFace(view, card, true, false))
            {
                HideFaceImage(view.FaceImage);
                view.RankText.color = new Color(0.08f, 0.08f, 0.08f, 1f);
                view.SuitText.color = card.IsRed ? theme.red : new Color(0.07f, 0.07f, 0.08f, 1f);
                SetGraphicAlpha(view.RankText, 1f);
                SetGraphicAlpha(view.SuitText, 1f);
            }
        }

        private string BuildLastTrickSignature(IReadOnlyList<TrickPlay> trick)
        {
            return trick == null || trick.Count == 0
                ? string.Empty
                : string.Join("|", trick.Select(play => $"{play.Seat}:{play.Card.Suit}:{play.Card.Rank}"));
        }

        private LastTrickCardPose ResolveLastTrickTargetPose(int index, int visibleCount)
        {
            if (index >= 0 && index < lastTrickCardTargetPoses.Count)
            {
                return lastTrickCardTargetPoses[index];
            }

            var cardSize = ResolveLastTrickCardSize();
            if (visibleCount == 4)
            {
                var positions = new[]
                {
                    new Vector2(0f, cardSize.y * 0.42f),
                    new Vector2(-cardSize.x * 0.72f, 0f),
                    new Vector2(cardSize.x * 0.72f, 0f),
                    new Vector2(0f, -cardSize.y * 0.42f)
                };
                var rotations = new[] { 0f, 23f, -23f, 0f };
                return new LastTrickCardPose(
                    index < positions.Length ? positions[index] : Vector2.zero,
                    cardSize,
                    Quaternion.Euler(0f, 0f, index < rotations.Length ? rotations[index] : 0f),
                    Vector3.one);
            }

            var spacing = Mathf.Clamp(cardSize.x * 0.18f, 6f, 12f);
            var totalWidth = visibleCount * cardSize.x + (visibleCount - 1) * spacing;
            var position = new Vector2(-totalWidth * 0.5f + cardSize.x * 0.5f + index * (cardSize.x + spacing), 0f);
            var rotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(-3f, 3f, index / Mathf.Max(1f, visibleCount - 1f)));
            return new LastTrickCardPose(position, cardSize, rotation, Vector3.one);
        }

        private void PlayLastTrickDisplayAnimation(int visibleCount)
        {
            StopLastTrickDisplayAnimation();
            if (!isActiveAndEnabled || visibleCount <= 0)
            {
                return;
            }

            lastTrickDisplayLoop = StartCoroutine(LastTrickDisplayAnimationRoutine(visibleCount));
        }

        private void StopLastTrickDisplayAnimation()
        {
            if (lastTrickDisplayLoop != null)
            {
                StopCoroutine(lastTrickDisplayLoop);
                lastTrickDisplayLoop = null;
            }
        }

        private IEnumerator LastTrickDisplayAnimationRoutine(int visibleCount)
        {
            var duration = Mathf.Max(0.28f, theme != null ? theme.modalDuration * 1.18f : 0.36f);
            const float staggerSeconds = 0.045f;

            if (lastTrickPanel != null)
            {
                lastTrickPanel.gameObject.SetActive(true);
            }

            var poses = new List<LastTrickCardPose>();
            for (var index = 0; index < visibleCount && index < lastTrickCardViews.Count; index++)
            {
                var view = lastTrickCardViews[index];
                var pose = ResolveLastTrickTargetPose(index, visibleCount);
                poses.Add(pose);

                view.Root.sizeDelta = pose.Size;
                view.Root.anchoredPosition = pose.Position + new Vector2(-18f + index * 10f, -20f);
                view.Root.localRotation = pose.Rotation * Quaternion.Euler(0f, 0f, index % 2 == 0 ? -10f : 10f);
                view.Root.localScale = pose.Scale * 0.74f;
                if (view.CanvasGroup != null)
                {
                    view.CanvasGroup.alpha = 0f;
                }
            }

            var elapsed = 0f;
            var totalDuration = duration + Mathf.Max(0, visibleCount - 1) * staggerSeconds;
            while (elapsed < totalDuration)
            {
                elapsed += Time.unscaledDeltaTime;

                for (var index = 0; index < visibleCount && index < lastTrickCardViews.Count; index++)
                {
                    var localT = Mathf.Clamp01((elapsed - index * staggerSeconds) / duration);
                    var eased = EaseOutBack(localT);
                    var settle = Mathf.Sin(localT * Mathf.PI);
                    var view = lastTrickCardViews[index];
                    var pose = poses[index];
                    var entryPosition = pose.Position + new Vector2(-18f + index * 10f, -20f);
                    var entryRotation = pose.Rotation * Quaternion.Euler(0f, 0f, index % 2 == 0 ? -10f : 10f);

                    view.Root.anchoredPosition = Vector2.LerpUnclamped(entryPosition, pose.Position, eased) + Vector2.up * (settle * 5f);
                    view.Root.localRotation = Quaternion.Slerp(entryRotation, pose.Rotation, Mathf.Clamp01(eased));
                    view.Root.localScale = pose.Scale * (Mathf.LerpUnclamped(0.74f, 1f, eased) + settle * 0.045f);
                    if (view.CanvasGroup != null)
                    {
                        view.CanvasGroup.alpha = Mathf.Lerp(0f, 0.92f, EaseOutCubic(localT));
                    }
                }

                yield return null;
            }

            for (var index = 0; index < visibleCount && index < lastTrickCardViews.Count; index++)
            {
                var view = lastTrickCardViews[index];
                var pose = poses[index];
                view.Root.sizeDelta = pose.Size;
                view.Root.anchoredPosition = pose.Position;
                view.Root.localRotation = pose.Rotation;
                view.Root.localScale = pose.Scale;
                if (view.CanvasGroup != null)
                {
                    view.CanvasGroup.alpha = 0.92f;
                }
            }

            lastTrickDisplayLoop = null;
        }

        private void RenderScoreboard(EndOfHandScoreboardView view, bool matchComplete, TeamId? winningTeam)
        {
            if (view == null || controller == null)
            {
                return;
            }

            BindScoreboardActions(view);
            view.Render(controller.State, selectedRule ?? controller.State.RuleSet, matchComplete, winningTeam);
        }

        private void RenderSeatPanels()
        {
            foreach (var seat in SpadesSeatUtility.TurnOrder)
            {
                var view = seatViews[seat];
                var bid = controller.State.RoundState.BidState.BidsBySeat[seat];
                var tricks = controller.State.RoundState.TricksWonBySeat[seat];
                var isCurrentTurn = IsCurrentTurnSeat(seat);
                view.NameText.text = controller.State.SeatNames[seat];
                view.BidText.text = bid.HasValue ? $"Bid: {(bid.Value == 0 ? "Nil" : bid.Value.ToString())}" : "Bid: --";
                view.TricksText.text = $"Books: {tricks}";
                view.StatusText.text = isCurrentTurn
                    ? controller.State.Phase == MatchPhase.Bidding ? "BIDDING NOW" : "TURN NOW"
                    : seat == controller.HumanSeat ? "Player" : "AI";
                if (view.Panel != null && !ShouldPreserveSeatPanelVisual(view.Panel))
                {
                    view.Panel.color = isCurrentTurn ? theme.gold : GetSeatPanelTint(seat);
                }
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
                EnsureTrickFaceImage(slot);
                HideFaceImage(slot.FaceImage);
                slot.RankText.text = string.Empty;
                slot.SuitText.text = string.Empty;
                SetGraphicAlpha(slot.RankText, 0f);
                SetGraphicAlpha(slot.SuitText, 0f);
                slot.Panel.color = new Color(1f, 1f, 1f, 0f);
            }

            foreach (var pair in activeCards)
            {
                if (hiddenTrickSlots.Contains(pair.Key))
                {
                    continue;
                }

                var slot = trickSlots[pair.Key];
                if (TryApplyImportedTrickFace(slot, pair.Value))
                {
                    continue;
                }

                slot.RankText.text = pair.Value.RankLabel;
                slot.SuitText.text = pair.Value.SuitIcon;
                slot.SuitText.color = pair.Value.IsRed ? theme.red : theme.primaryText;
                slot.Panel.color = Color.white;
            }
        }

        private void RenderOpponentHands()
        {
            if (openingDealPending || openingDealRunning || controller?.State?.RoundState?.HandsBySeat == null)
            {
                return;
            }

            foreach (var seat in SpadesSeatUtility.TurnOrder)
            {
                if (seat == SeatId.Bottom)
                {
                    continue;
                }

                var targets = FindAuthoredOpeningDealSeatCards(seat);
                var handCount = controller.State.RoundState.HandsBySeat.TryGetValue(seat, out var hand)
                    ? hand.Count
                    : 0;
                for (var index = 0; index < targets.Count; index++)
                {
                    SetCanvasGroupVisible(targets[index], index < handCount);
                }
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

                SetPlaySelectedButtonActive(false);
                return;
            }

            var hand = controller.GetHand(SeatId.Bottom).ToList();
            selectedCard = selectedCard.HasValue && hand.Contains(selectedCard.Value) ? selectedCard : null;
            var selectedIndex = selectedCard.HasValue ? hand.FindIndex(card => card.Equals(selectedCard.Value)) : -1;
            var legalCards = controller.State.Phase == MatchPhase.TrickPlay && controller.State.RoundState.TrickState.CurrentTurn == SeatId.Bottom
                ? controller.GetLegalCardsForSeat(SeatId.Bottom).ToHashSet()
                : new HashSet<Card>();

            EnsureCardPoolSize(hand.Count);
            var previousHand = lastRenderedHand.ToHashSet();
            CardButtonView selectedView = null;

            for (var index = 0; index < hand.Count; index++)
            {
                var card = hand[index];
                var view = handPool[index];
                var isLegal = legalCards.Contains(card);
                var isSelected = selectedCard.HasValue && selectedCard.Value.Equals(card);
                var targetPosition = GetFanTargetPosition(index, hand.Count, isSelected, selectedIndex);
                var targetRotation = GetFanTargetRotation(index, hand.Count);
                var targetScale = isSelected ? theme.selectedCardScale : 1f;
                ConfigureCardView(view, card, isLegal, isSelected);
                view.gameObject.SetActive(true);
                view.Root.SetSiblingIndex(index);
                if (isSelected)
                {
                    selectedView = view;
                }

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

            if (selectedView != null)
            {
                selectedView.Root.SetAsLastSibling();
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
            SetPlaySelectedButtonActive(selectedCard.HasValue && legalCards.Contains(selectedCard.Value));
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

                EnsureCardFaceImage(view);
                view.gameObject.SetActive(false);
                handPool.Add(view);
            }
        }

        private void ConfigureCardView(CardButtonView view, Card card, bool isLegal, bool isSelected)
        {
            StopHandAnimation(view);
            EnsureCardFaceImage(view);
            view.gameObject.name = card.ShortLabel;
            view.Root.sizeDelta = ResolveBottomHandCardSize();
            view.RankText.font = theme.ResolveFont();
            view.SuitText.font = theme.ResolveFont();
            view.RankText.text = card.RankLabel;
            view.SuitText.text = card.SuitIcon;
            view.Panel.sprite = ResolveCardSprite(isSelected, isLegal);
            view.Panel.type = Image.Type.Sliced;
            view.Panel.color = Color.white;
            if (!TryApplyImportedCardFace(view, card, isLegal))
            {
                HideFaceImage(view.FaceImage);
                view.RankText.color = new Color(0.08f, 0.08f, 0.08f, 1f);
                view.SuitText.color = card.IsRed ? theme.red : new Color(0.07f, 0.07f, 0.08f, 1f);
            }
            view.CanvasGroup.alpha = 1f;
            view.CanvasGroup.blocksRaycasts = true;
            view.CanvasGroup.interactable = true;
            view.Panel.raycastTarget = true;
            if (view.FaceImage != null)
            {
                view.FaceImage.raycastTarget = false;
            }

            view.RankText.raycastTarget = false;
            view.SuitText.raycastTarget = false;
            view.Button.enabled = true;
            view.Button.targetGraphic = view.Panel;
            view.Button.onClick.RemoveAllListeners();
            view.Button.interactable = controller.State.Phase == MatchPhase.TrickPlay;
            view.Button.onClick.AddListener(() => OnCardTapped(card));
        }

        private void ApplyCardBackVisual(CardButtonView view)
        {
            if (view == null || view.Panel == null)
            {
                return;
            }

            view.Panel.sprite = ResolveCardBackSprite();
            view.Panel.type = Image.Type.Sliced;
            view.Panel.color = Color.white;
            HideFaceImage(view.FaceImage);
            SetGraphicAlpha(view.RankText, 0f);
            SetGraphicAlpha(view.SuitText, 0f);
        }

        private void ApplyOpeningStackPreviewVisual(CardButtonView view)
        {
            ApplyCardBackVisual(view);
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
            var faceState = CaptureCardFaceState(view);
            handAnimations[view] = StartCoroutine(CardTransformRoutine(
                view,
                targetPosition,
                targetRotation,
                targetScale,
                Mathf.Max(0.09f, theme.pulseDuration * 0.9f),
                1f,
                1f,
                false,
                faceState));
        }

        private void StartCardEntryAnimation(CardButtonView view, Vector2 targetPosition, Quaternion targetRotation, float targetScale)
        {
            StopHandAnimation(view);
            var faceState = CaptureCardFaceState(view);
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
                faceState));
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
            CardVisualState faceState)
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
                    ApplyCardFaceVisual(view, faceState);
                    revealedFace = true;
                }

                var scale = Vector3.Lerp(startScale, Vector3.one * targetScale, eased);
                if (flipReveal)
                {
                    scale *= 1f + Mathf.Sin(t * Mathf.PI) * 0.035f;
                }

                view.Root.localScale = scale;
                yield return null;
            }

            view.CanvasGroup.alpha = endAlpha;
            if (flipReveal)
            {
                ApplyCardFaceVisual(view, faceState);
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
            if (openingDealPending || openingDealRunning || handReviewPending || bidTurnDelayPending)
            {
                SetBidSheetVisible(false);
                pendingBidSelection = null;
                return;
            }

            var shouldShow = controller.State.Phase == MatchPhase.Bidding &&
                             controller.State.RoundState.BidState.CurrentBidder == SeatId.Bottom;
            SetBidSheetVisible(shouldShow);
            if (shouldShow)
            {
                RestoreBidCameraFocus();
            }

            if (!shouldShow)
            {
                pendingBidSelection = null;
                return;
            }

            var legal = controller.GetLegalBidsForSeat(SeatId.Bottom).ToHashSet();
            if (pendingBidSelection.HasValue && !legal.Contains(pendingBidSelection.Value))
            {
                pendingBidSelection = null;
            }

            foreach (var pair in bidButtons)
            {
                var isLegal = legal.Contains(pair.Key);
                var isSelected = pendingBidSelection == pair.Key;
                pair.Value.interactable = isLegal;
                ApplyBidButtonVisual(pair.Value, isLegal, isSelected);
            }

        }

        private void RenderOptionsMenu()
        {
            if (sceneRefs.OptionsMenu != null)
            {
                var shouldShowMenu = optionsMenuOpen || optionsMenuAnimationLoop != null;
                SetSheetVisible(sceneRefs.OptionsMenu, shouldShowMenu);
                if (shouldShowMenu)
                {
                    sceneRefs.OptionsMenu.SetAsLastSibling();
                }

                if (optionsMenuOpen && optionsMenuAnimationLoop == null)
                {
                    ApplyOptionsMenuVisiblePose(true);
                }
            }

            if (sceneRefs.ClaimTheRestButton != null)
            {
                sceneRefs.ClaimTheRestButton.gameObject.SetActive(false);
            }

            if (sceneRefs.ForfeitMatchButton != null)
            {
                var canForfeit = CanForfeitMatch();
                sceneRefs.ForfeitMatchButton.gameObject.SetActive(canForfeit);
                sceneRefs.ForfeitMatchButton.interactable = canForfeit;
                if (sceneRefs.ForfeitMatchButton.image != null)
                {
                    sceneRefs.ForfeitMatchButton.image.color = canForfeit ? theme.red : new Color(0.35f, 0.36f, 0.38f, 0.92f);
                }
            }

            RenderSfxToggleLabel();
        }

        private void RenderOptionsMenuButton()
        {
            if (sceneRefs.BackButton == null)
            {
                return;
            }

            var canOpen = CanOpenOptionsMenu();
            var shouldShow = optionsMenuOpen || optionsMenuAnimationLoop != null || canOpen;
            sceneRefs.BackButton.gameObject.SetActive(shouldShow);
            sceneRefs.BackButton.interactable = optionsMenuOpen || canOpen;
            if (shouldShow)
            {
                SetButtonLabel(sceneRefs.BackButton, optionsMenuOpen ? "CLOSE" : "MENU");
            }
        }

        private void SelectBid(int bid)
        {
            if (IsGameplayInputBlocked() || controller == null || controller.State.Phase != MatchPhase.Bidding ||
                controller.State.RoundState.BidState.CurrentBidder != SeatId.Bottom)
            {
                return;
            }

            if (!controller.GetLegalBidsForSeat(SeatId.Bottom).Contains(bid))
            {
                PlayFeedback(FeedbackCue.Invalid, 0.16f);
                FlashStatus("That bid is not available right now.", theme.red);
                return;
            }

            pendingBidSelection = bid;
            PlayFeedback(FeedbackCue.Select, 0.2f);
            FlashStatus($"Bid {BidSelectionLabel(bid)} selected. Confirm to lock it.", theme.gold);
            RenderAll();
        }

        private void ConfirmSelectedBid()
        {
            if (!pendingBidSelection.HasValue)
            {
                PlayFeedback(FeedbackCue.Invalid, 0.16f);
                FlashStatus("Select a bid first.", theme.red);
                return;
            }

            SubmitBid(pendingBidSelection.Value);
        }

        private void ApplyBidButtonVisual(Button button, bool isLegal, bool isSelected)
        {
            if (button == null || button.image == null)
            {
                return;
            }

            CacheBidButtonSprites();
            button.image.sprite = isSelected && bidButtonSelectedSprite != null
                ? bidButtonSelectedSprite
                : bidButtonDefaultSprite != null
                    ? bidButtonDefaultSprite
                    : button.image.sprite;
            button.image.type = Image.Type.Simple;
            button.image.color = isLegal
                ? Color.white
                : new Color(0.42f, 0.42f, 0.42f, 0.72f);

            var label = button.GetComponentInChildren<Text>();
            if (label != null)
            {
                label.color = isLegal ? theme.primaryText : theme.mutedText;
                label.fontStyle = isSelected ? FontStyle.Bold : FontStyle.Normal;
            }
        }

        private void CacheBidButtonSprites()
        {
            if (bidButtonDefaultSprite != null && bidButtonSelectedSprite != null)
            {
                return;
            }

            foreach (var button in bidButtons.Values)
            {
                var sprite = button != null && button.image != null ? button.image.sprite : null;
                if (sprite == null)
                {
                    continue;
                }

                var buttonName = button.gameObject.name;
                if (buttonName.IndexOf("Selected", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    bidButtonSelectedSprite ??= sprite;
                    continue;
                }

                if (bidButtonDefaultSprite == null || sprite.name.EndsWith("_1", System.StringComparison.Ordinal))
                {
                    bidButtonDefaultSprite = sprite;
                }

                if (sprite.name.EndsWith("_2", System.StringComparison.Ordinal))
                {
                    bidButtonSelectedSprite = sprite;
                }
            }

            bidButtonDefaultSprite ??= theme.buttonSprite;
            bidButtonSelectedSprite ??= bidButtonDefaultSprite;
        }

        private static string BidSelectionLabel(int bid)
        {
            return bid == 0 ? "NIL" : bid.ToString();
        }

        private static string BookLabel(int count)
        {
            return count == 1 ? "book" : "books";
        }

        private bool IsGameplayInputBlocked()
        {
            return exitPromptOpen || optionsMenuOpen || avatarIntroRunning;
        }

        private bool CanOpenOptionsMenu()
        {
            return !exitPromptOpen &&
                   !optionsMenuOpen &&
                   optionsMenuAnimationLoop == null &&
                   !IsOptionsMenuLockedByOpeningFlow();
        }

        private bool IsOptionsMenuLockedByOpeningFlow()
        {
            return controller == null ||
                   controller.State?.RoundState == null ||
                   avatarIntroRunning ||
                   openingDealPending ||
                   openingDealRunning ||
                   openingStackIntroRunning ||
                   handReviewPending ||
                   bidTurnDelayPending ||
                   controller.State.Phase == MatchPhase.Bidding ||
                   bidBubbleLoops.Count > 0;
        }

        private bool CanClaimRemainingBooks()
        {
            return controller != null &&
                   controller.State.Phase == MatchPhase.TrickPlay &&
                   !openingDealPending &&
                   !openingDealRunning &&
                   !avatarIntroRunning &&
                   !handReviewPending &&
                   !bidTurnDelayPending &&
                   !HasVisualMotionPending &&
                   controller.GetRemainingBookCount() > 0;
        }

        private bool CanForfeitMatch()
        {
            return controller != null &&
                   (controller.State.Phase == MatchPhase.Bidding ||
                    controller.State.Phase == MatchPhase.TrickPlay);
        }

        private void OnDealPressed()
        {
            if (IsGameplayInputBlocked() || !openingDealPending || openingDealRunning || controller == null || controller.State.RoundState == null)
            {
                return;
            }

            if (openingStackIntroRunning)
            {
                FlashStatus("Give the deck a second to settle.", theme.gold);
                return;
            }

            if (aiLoop != null)
            {
                StopCoroutine(aiLoop);
                aiLoop = null;
            }

            openingDealRunning = true;
            StartDealButtonDismiss();
            HideAllBidBubbles(true);
            PlayFeedback(FeedbackCue.Deal, 0.2f);
            RenderAll();
            StartCoroutine(OpeningDealSequence());
        }

        private void StartAvatarIntro()
        {
            if (avatarIntroLoop != null)
            {
                StopCoroutine(avatarIntroLoop);
                avatarIntroLoop = null;
            }

            EnsureAvatarRouletteSprites();
            ApplyAvatarIntroVisibility();

            var states = BuildAvatarIntroSeatStates();
            if (states.Count == 0)
            {
                CompleteAvatarIntroAndStartDeal(states);
                return;
            }

            avatarIntroLoop = StartCoroutine(AvatarIntroSequence(states));
        }

        private List<AvatarIntroSeatState> BuildAvatarIntroSeatStates()
        {
            var states = new List<AvatarIntroSeatState>();
            var finalSpritePool = avatarRouletteSprites.Where(sprite => sprite != null).ToList();

            foreach (var seat in SpadesSeatUtility.TurnOrder)
            {
                if (!seatViews.TryGetValue(seat, out var view) || view == null)
                {
                    continue;
                }

                seatAvatarImages.TryGetValue(seat, out var avatarImage);
                seatIntroGroups.TryGetValue(seat, out var group);
                group = group != null ? group : ResolveCanvasGroup(view.gameObject);

                var state = new AvatarIntroSeatState
                {
                    Seat = seat,
                    View = view,
                    AvatarImage = avatarImage,
                    Group = group,
                    OriginalSprite = avatarImage != null ? avatarImage.sprite : null,
                    OriginalScale = view.Root.localScale,
                    OriginalRotation = view.Root.localRotation,
                    OriginalAnchoredPosition = view.Root.anchoredPosition,
                    EntryOffset = GetAvatarIntroEntryOffset(view, seat),
                    EntryCurveOffset = GetAvatarIntroCurveOffset(seat),
                    EntryRotationDegrees = GetAvatarIntroEntryRotation(seat),
                    OriginalName = view.NameText != null ? view.NameText.text : seat.DisplayName(),
                    EntryDelay = seat == SeatId.Bottom ? 0.05f : Random.Range(0.16f, 0.48f),
                    RouletteDuration = seat == SeatId.Bottom ? 0.4f : Random.Range(AvatarIntroMinSeconds, AvatarIntroMaxSeconds),
                    RouletteOffset = Random.Range(0, Mathf.Max(1, avatarRouletteSprites.Count))
                };

                if (seat == SeatId.Bottom)
                {
                    state.FinalSprite = state.OriginalSprite;
                    state.FinalName = "YOU";
                }
                else
                {
                    state.FinalSprite = TakeAvatarIntroFinalSprite(finalSpritePool, state.OriginalSprite);
                    state.FinalName = GetAvatarDisplayName(state.FinalSprite, seat);
                }

                states.Add(state);
            }

            return states;
        }

        private IEnumerator AvatarIntroSequence(List<AvatarIntroSeatState> states)
        {
            SetSeatGameplayLabelsVisible(false);
            SetGameplayChromeVisible(false);

            foreach (var state in states)
            {
                if (state.View == null)
                {
                    continue;
                }

                state.View.gameObject.SetActive(true);
                state.View.Root.localScale = state.OriginalScale * 0.68f;
                state.View.Root.localRotation = state.OriginalRotation * Quaternion.Euler(0f, 0f, state.EntryRotationDegrees);
                state.View.Root.anchoredPosition = state.OriginalAnchoredPosition + state.EntryOffset;
                if (state.Group != null)
                {
                    state.Group.alpha = 0f;
                    state.Group.interactable = false;
                    state.Group.blocksRaycasts = false;
                }

                ApplyAvatarIntroSeatVisual(state, true, 0f);
            }

            var totalDuration = states.Count == 0 ? AvatarIntroMinSeconds : states.Max(state => state.EntryDelay + state.RouletteDuration) + 0.38f;
            var elapsed = 0f;
            while (elapsed < totalDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                foreach (var state in states)
                {
                    UpdateAvatarIntroSeat(state, elapsed);
                }

                yield return null;
            }

            foreach (var state in states)
            {
                ApplyAvatarIntroSeatFinal(state);
            }

            if (states.Count > 0)
            {
                PlayFeedback(FeedbackCue.AvatarAssigned, 0.34f);
            }

            yield return new WaitForSecondsRealtime(0.22f);
            CompleteAvatarIntroAndStartDeal(states);
        }

        private void UpdateAvatarIntroSeat(AvatarIntroSeatState state, float elapsed)
        {
            if (state.View == null)
            {
                return;
            }

            var localTime = elapsed - state.EntryDelay;
            if (localTime < 0f)
            {
                return;
            }

            var entryT = Mathf.Clamp01(localTime / AvatarIntroEntrySeconds);
            var entryEase = EaseOutBack(entryT);
            var entryVisualEase = Mathf.SmoothStep(0f, 1f, entryT);
            if (state.Group != null)
            {
                state.Group.alpha = entryVisualEase;
            }

            var entryStart = state.OriginalAnchoredPosition + state.EntryOffset;
            var entryMid = state.OriginalAnchoredPosition + state.EntryOffset * 0.42f + state.EntryCurveOffset;
            state.View.Root.anchoredPosition = QuadraticBezier(entryStart, entryMid, state.OriginalAnchoredPosition, entryEase);
            var drift = Mathf.Sin(entryT * Mathf.PI * 2.5f) * (1f - entryVisualEase);
            state.View.Root.localRotation = state.OriginalRotation *
                                            Quaternion.Euler(0f, 0f, Mathf.Lerp(state.EntryRotationDegrees, 0f, entryVisualEase) + drift * 7f);

            var settlePulse = localTime > state.RouletteDuration
                ? Mathf.Sin(Mathf.Clamp01((localTime - state.RouletteDuration) / 0.26f) * Mathf.PI) * 0.08f
                : Mathf.Sin(localTime * 18f) * 0.018f;
            var entryPop = Mathf.Sin(entryT * Mathf.PI) * 0.09f;
            state.View.Root.localScale = state.OriginalScale * (Mathf.Lerp(0.68f, 1f, entryVisualEase) + entryPop + settlePulse);

            if (state.Seat == SeatId.Bottom)
            {
                ApplyAvatarIntroSeatSprite(state, state.FinalSprite);
                if (state.View.NameText != null)
                {
                    state.View.NameText.text = state.FinalName;
                    state.View.NameText.gameObject.SetActive(true);
                    SetGraphicAlpha(state.View.NameText, 1f);
                }

                return;
            }

            if (localTime >= state.RouletteDuration || avatarRouletteSprites.Count == 0)
            {
                ApplyAvatarIntroSeatFinal(state);
                return;
            }

            var rouletteIndex = (state.RouletteOffset + Mathf.FloorToInt(localTime / 0.055f)) % avatarRouletteSprites.Count;
            if (rouletteIndex != state.LastRouletteIndex)
            {
                state.LastRouletteIndex = rouletteIndex;
                PlayAvatarRouletteCue();
            }

            var sprite = avatarRouletteSprites[rouletteIndex];
            ApplyAvatarIntroSeatSprite(state, sprite);
            if (state.View.NameText != null)
            {
                state.View.NameText.text = GetAvatarDisplayName(sprite, state.Seat);
                state.View.NameText.gameObject.SetActive(true);
                SetGraphicAlpha(state.View.NameText, 1f);
            }
        }

        private void CompleteAvatarIntroAndStartDeal(List<AvatarIntroSeatState> states)
        {
            foreach (var state in states)
            {
                ApplyAvatarIntroSeatFinal(state);
                if (state.Group != null)
                {
                    state.Group.alpha = 1f;
                    state.Group.interactable = true;
                    state.Group.blocksRaycasts = true;
                }
            }

            avatarIntroRunning = false;
            avatarIntroLoop = null;
            SetGameplayChromeVisible(true);
            SetSeatGameplayLabelsVisible(true);
            RenderAll();
            StartOpeningStackIntro();
        }

        private void ApplyAvatarIntroSeatFinal(AvatarIntroSeatState state)
        {
            if (state.View == null)
            {
                return;
            }

            state.View.Root.anchoredPosition = state.OriginalAnchoredPosition;
            state.View.Root.localScale = state.OriginalScale;
            state.View.Root.localRotation = state.OriginalRotation;
            ApplyAvatarIntroSeatSprite(state, state.FinalSprite);
            if (state.View.NameText != null)
            {
                state.View.NameText.text = state.FinalName;
                state.View.NameText.gameObject.SetActive(true);
                SetGraphicAlpha(state.View.NameText, 1f);
            }

            if (controller?.State?.SeatNames != null)
            {
                controller.State.SeatNames[state.Seat] = state.FinalName;
            }
        }

        private void ApplyAvatarIntroSeatVisual(AvatarIntroSeatState state, bool useFinal, float alpha)
        {
            ApplyAvatarIntroSeatSprite(state, useFinal ? state.FinalSprite : state.OriginalSprite);
            if (state.View?.NameText != null)
            {
                state.View.NameText.text = useFinal ? state.FinalName : state.OriginalName;
                state.View.NameText.gameObject.SetActive(true);
                SetGraphicAlpha(state.View.NameText, alpha);
            }
        }

        private void ApplyAvatarIntroSeatSprite(AvatarIntroSeatState state, Sprite sprite)
        {
            if (state.AvatarImage == null || sprite == null)
            {
                return;
            }

            state.AvatarImage.sprite = sprite;
            state.AvatarImage.preserveAspect = true;
            state.AvatarImage.color = Color.white;
            state.AvatarImage.enabled = true;
        }

        private Sprite TakeAvatarIntroFinalSprite(List<Sprite> pool, Sprite fallback)
        {
            if (pool == null || pool.Count == 0)
            {
                return fallback;
            }

            var index = Random.Range(0, pool.Count);
            var sprite = pool[index];
            pool.RemoveAt(index);
            return sprite != null ? sprite : fallback;
        }

        private static Vector2 GetAvatarIntroEntryOffset(SeatPanelView view, SeatId seat)
        {
            var parentRect = view?.Root != null ? view.Root.parent as RectTransform : null;
            var parentSize = parentRect != null
                ? parentRect.rect.size
                : new Vector2(Mathf.Max(Screen.width, 1280f), Mathf.Max(Screen.height, 720f));
            var seatSize = view?.Root != null
                ? view.Root.rect.size
                : new Vector2(220f, 180f);
            var horizontalTravel = Mathf.Max(parentSize.x, Screen.width, 1280f) + seatSize.x + 160f;
            var verticalTravel = Mathf.Max(parentSize.y, Screen.height, 720f) + seatSize.y + 160f;
            return seat switch
            {
                SeatId.Left => new Vector2(-horizontalTravel, 0f),
                SeatId.Right => new Vector2(horizontalTravel, 0f),
                SeatId.Top => new Vector2(0f, verticalTravel),
                SeatId.Bottom => new Vector2(0f, -verticalTravel),
                _ => Vector2.zero
            };
        }

        private static Vector2 GetAvatarIntroCurveOffset(SeatId seat)
        {
            return seat switch
            {
                SeatId.Left => new Vector2(130f, 210f),
                SeatId.Right => new Vector2(-130f, -210f),
                SeatId.Top => new Vector2(-250f, -120f),
                SeatId.Bottom => new Vector2(250f, 120f),
                _ => Vector2.zero
            };
        }

        private static float GetAvatarIntroEntryRotation(SeatId seat)
        {
            return seat switch
            {
                SeatId.Left => -18f,
                SeatId.Right => 18f,
                SeatId.Top => -14f,
                SeatId.Bottom => 14f,
                _ => 0f
            };
        }

        private static float EaseOutBack(float t)
        {
            const float overshoot = 1.18f;
            t = Mathf.Clamp01(t) - 1f;
            return 1f + t * t * ((overshoot + 1f) * t + overshoot);
        }

        private static Vector2 QuadraticBezier(Vector2 a, Vector2 b, Vector2 c, float t)
        {
            t = Mathf.Clamp01(t);
            var oneMinusT = 1f - t;
            return oneMinusT * oneMinusT * a + 2f * oneMinusT * t * b + t * t * c;
        }

        private void EnsureAvatarRouletteSprites()
        {
            if (avatarRouletteSprites.Count > 0)
            {
                return;
            }

            AddAvatarSprites(Resources.LoadAll<Sprite>(AvatarResourceRoot));

#if UNITY_EDITOR
            if (avatarRouletteSprites.Count == 0)
            {
                var guids = UnityEditor.AssetDatabase.FindAssets("t:Sprite", new[] { AvatarAssetFolder });
                foreach (var guid in guids)
                {
                    var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                    if (path.EndsWith("border.png", System.StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    AddAvatarSprite(UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(path));
                }
            }
#endif

            if (avatarRouletteSprites.Count == 0)
            {
                foreach (var image in seatAvatarImages.Values)
                {
                    AddAvatarSprite(image != null ? image.sprite : null);
                }
            }

            avatarRouletteSprites.Sort((left, right) => GetAvatarSortIndex(left).CompareTo(GetAvatarSortIndex(right)));
        }

        private void AddAvatarSprites(IEnumerable<Sprite> sprites)
        {
            if (sprites == null)
            {
                return;
            }

            foreach (var sprite in sprites)
            {
                AddAvatarSprite(sprite);
            }
        }

        private void AddAvatarSprite(Sprite sprite)
        {
            if (sprite == null || sprite.name.Equals("border", System.StringComparison.OrdinalIgnoreCase) || avatarRouletteSprites.Contains(sprite))
            {
                return;
            }

            avatarRouletteSprites.Add(sprite);
        }

        private static int GetAvatarSortIndex(Sprite sprite)
        {
            return sprite != null && int.TryParse(sprite.name, out var index) ? index : int.MaxValue;
        }

        private static string GetAvatarDisplayName(Sprite sprite, SeatId fallbackSeat)
        {
            var index = GetAvatarSortIndex(sprite);
            if (index > 0 && index < AvatarDisplayNames.Length)
            {
                return AvatarDisplayNames[index];
            }

            return fallbackSeat.DisplayName().ToUpperInvariant();
        }

        private void ApplyAvatarIntroVisibility()
        {
            if (!avatarIntroRunning)
            {
                return;
            }

            SetGameplayChromeVisible(false);
            SetSeatGameplayLabelsVisible(false);
            foreach (var view in seatViews.Values)
            {
                if (view != null)
                {
                    view.gameObject.SetActive(true);
                    if (view.NameText != null)
                    {
                        view.NameText.gameObject.SetActive(true);
                    }
                }
            }
        }

        private void SetGameplayChromeVisible(bool visible)
        {
            SetStartupCardGroupsVisible(visible && !avatarIntroRunning && !openingDealPending && !openingDealRunning);
            SetGraphicObjectActive(sceneRefs.HudPanel, visible);
            SetGraphicObjectActive(sceneRefs.TablePanel, visible);
            SetGraphicObjectActive(sceneRefs.HandPanel, visible);
            SetGraphicObjectActive(sceneRefs.FeedPanel, visible);
            SetGraphicObjectActive(sceneRefs.DeckAnchorImage, visible);
            SetGraphicObjectActive(sceneRefs.DiscardAnchorImage, visible);
            SetGraphicObjectActive(sceneRefs.OpeningStackImage, visible);
            SetGraphicObjectActive(sceneRefs.LastTrickPanel, visible);
            SetGraphicObjectActive(lastTrickPanel, visible);
            SetTextObjectActive(sceneRefs.StatusText, visible);
            SetTextObjectActive(sceneRefs.HudModeText, visible);
            SetTextObjectActive(sceneRefs.TimerHookText, visible);
            SetTextObjectActive(sceneRefs.HomeScoreText, visible);
            SetTextObjectActive(sceneRefs.AwayScoreText, visible);
            SetTextObjectActive(sceneRefs.LastTrickText, visible);
            SetTextObjectActive(sceneRefs.FeedText, visible);
            SetTextObjectActive(sceneRefs.CenterHintText, visible);
            SetTextObjectActive(sceneRefs.DeckAnchorText, visible);
            SetTextObjectActive(sceneRefs.DiscardAnchorText, visible);
            SetTextObjectActive(sceneRefs.OpeningStackText, visible);
            SetTextObjectActive(sceneRefs.BannerText, visible);
            if (openingStackEffectImage != null)
            {
                openingStackEffectImage.gameObject.SetActive(false);
            }

            SetBidSheetVisible(visible &&
                               !openingDealPending &&
                               !openingDealRunning &&
                               controller?.State?.Phase == MatchPhase.Bidding &&
                               controller.State.RoundState.BidState.CurrentBidder == SeatId.Bottom);
            SetSheetVisible(sceneRefs.RoundSheet, false);
            SetSheetVisible(sceneRefs.EndSheet, false);
            SetOptionsMenuVisibleImmediate(false);
            SetSheetVisible(sceneRefs.ExitPromptOverlay, false);
            if (sceneRefs.DealButton != null)
            {
                sceneRefs.DealButton.gameObject.SetActive(false);
            }
        }

        private static void SetStartupCardGroupsVisible(bool visible)
        {
            var allTransforms = Resources.FindObjectsOfTypeAll<Transform>();
            foreach (var target in allTransforms)
            {
                if (target == null ||
                    !target.gameObject.scene.IsValid() ||
                    !StartupHiddenCardGroupNames.Contains(target.name))
                {
                    continue;
                }

                var group = ResolveCanvasGroup(target.gameObject);
                group.alpha = visible ? 1f : 0f;
                group.interactable = visible;
                group.blocksRaycasts = visible;
            }
        }

        private void SetSeatGameplayLabelsVisible(bool visible)
        {
            foreach (var view in seatViews.Values)
            {
                if (view == null)
                {
                    continue;
                }

                SetTextObjectActive(view.StatusText, visible);
                SetTextObjectActive(view.BidText, visible);
                SetTextObjectActive(view.TricksText, visible);
                if (view.NameText != null)
                {
                    view.NameText.gameObject.SetActive(true);
                    SetGraphicAlpha(view.NameText, 1f);
                }
            }
        }

        private static void SetGraphicObjectActive(Graphic graphic, bool active)
        {
            if (graphic != null)
            {
                graphic.gameObject.SetActive(true);
                graphic.enabled = active;
            }
        }

        private static void SetTextObjectActive(Text text, bool active)
        {
            if (text != null)
            {
                text.gameObject.SetActive(true);
                text.enabled = active;
            }
        }

        private List<CanvasGroup> PrepareBottomOpeningDealRealCards(List<Card> hand)
        {
            var targets = new List<CanvasGroup>();
            if (hand == null || sceneRefs.HandContent == null)
            {
                return targets;
            }

            EnsureCardPoolSize(hand.Count);
            for (var index = 0; index < hand.Count; index++)
            {
                var view = handPool[index];
                view.CanvasGroup = view.CanvasGroup != null ? view.CanvasGroup : ResolveCanvasGroup(view.gameObject);
                ConfigureCardView(view, hand[index], false, false);
                ApplyFanLayout(view, GetFanTargetPosition(index, hand.Count, false), GetFanTargetRotation(index, hand.Count), 1f);
                view.Root.SetSiblingIndex(index);
                view.gameObject.SetActive(true);
                view.Button.interactable = false;
                SetCanvasGroupVisible(view.CanvasGroup, false);
                targets.Add(view.CanvasGroup);
            }

            for (var index = hand.Count; index < handPool.Count; index++)
            {
                StopHandAnimation(handPool[index]);
                handPool[index].gameObject.SetActive(false);
            }

            SetPlaySelectedButtonActive(false);
            return targets;
        }

        private Dictionary<SeatId, List<CanvasGroup>> PrepareOpponentOpeningDealRealCards()
        {
            var targetsBySeat = new Dictionary<SeatId, List<CanvasGroup>>();
            foreach (var seat in SpadesSeatUtility.TurnOrder)
            {
                if (seat == SeatId.Bottom)
                {
                    continue;
                }

                var targets = FindAuthoredOpeningDealSeatCards(seat);
                targetsBySeat[seat] = targets;
                foreach (var group in targets)
                {
                    SetCanvasGroupVisible(group, false);
                }
            }

            return targetsBySeat;
        }

        private CanvasGroup GetOpponentOpeningDealRevealTarget(
            IReadOnlyDictionary<SeatId, List<CanvasGroup>> targetsBySeat,
            SeatId seat,
            int cardIndex)
        {
            if (targetsBySeat != null &&
                targetsBySeat.TryGetValue(seat, out var targets) &&
                targets != null &&
                cardIndex >= 0 &&
                cardIndex < targets.Count)
            {
                return targets[cardIndex];
            }

            return null;
        }

        private static List<CanvasGroup> FindAuthoredOpeningDealSeatCards(SeatId seat)
        {
            var prefix = GetAuthoredOpeningDealCardPrefix(seat);
            if (string.IsNullOrEmpty(prefix))
            {
                return new List<CanvasGroup>();
            }

            return Resources.FindObjectsOfTypeAll<Transform>()
                .Where(target => target != null &&
                                 target.gameObject.scene.IsValid() &&
                                 TryGetAuthoredOpeningDealCardIndex(target.name, prefix, out _) &&
                                 !target.name.Contains("White Edge", System.StringComparison.OrdinalIgnoreCase))
                .OrderBy(target => TryGetAuthoredOpeningDealCardIndex(target.name, prefix, out var index) ? index : int.MaxValue)
                .Select(target => ResolveCanvasGroup(target.gameObject))
                .ToList();
        }

        private static string GetAuthoredOpeningDealCardPrefix(SeatId seat)
        {
            return seat switch
            {
                SeatId.Top => "Top Opponent Card ",
                SeatId.Left => "Left Opponent Card ",
                SeatId.Right => "Right Opponent Card ",
                _ => string.Empty
            };
        }

        private static bool TryGetAuthoredOpeningDealCardIndex(string objectName, string prefix, out int index)
        {
            index = 0;
            return !string.IsNullOrEmpty(objectName) &&
                   objectName.StartsWith(prefix, System.StringComparison.Ordinal) &&
                   int.TryParse(objectName.Substring(prefix.Length), out index);
        }

        private CardButtonView CreateOpeningDealRuntimeSeatCard(SeatId seat, int index, CardMotionSnapshot motion)
        {
            if (sceneRefs.CardButtonPrefab == null)
            {
                return null;
            }

            var view = Instantiate(sceneRefs.CardButtonPrefab, AnimationRoot);
            view.gameObject.name = $"{seat} Runtime Dealt Card {index + 1}";
            view.transform.SetAsLastSibling();
            view.CanvasGroup = view.CanvasGroup != null ? view.CanvasGroup : ResolveCanvasGroup(view.gameObject);
            view.CanvasGroup.blocksRaycasts = false;
            view.Button.onClick.RemoveAllListeners();
            view.Button.enabled = false;
            view.Root.anchorMin = new Vector2(0.5f, 0.5f);
            view.Root.anchorMax = new Vector2(0.5f, 0.5f);
            view.Root.pivot = new Vector2(0.5f, 0.5f);
            view.Root.anchoredPosition = motion.EndPosition;
            view.Root.sizeDelta = motion.EndSize;
            view.Root.localRotation = motion.EndRotation;
            view.Root.localScale = Vector3.one;
            ApplyCardBackVisual(view);
            SetCanvasGroupVisible(view.CanvasGroup, false);
            openingDealRuntimeSeatCards.Add(view);
            return view;
        }

        private void DetachOpeningStackPreviewCard(CardButtonView preview)
        {
            if (preview == null)
            {
                return;
            }

            if (openingStackPreviewAnimations.TryGetValue(preview, out var routine) && routine != null)
            {
                StopCoroutine(routine);
            }

            openingStackPreviewAnimations.Remove(preview);
            openingStackPreviewCards.Remove(preview);
        }

        private static void RevealOpeningDealRealCard(CanvasGroup target)
        {
            SetCanvasGroupVisible(target, true);
        }

        private static void SetCanvasGroupVisible(CanvasGroup group, bool visible)
        {
            if (group == null)
            {
                return;
            }

            group.gameObject.SetActive(true);
            group.alpha = visible ? 1f : 0f;
            group.interactable = false;
            group.blocksRaycasts = false;
        }

        private IEnumerator OpeningDealSequence()
        {
            if (sceneRefs.OpeningStackImage != null)
            {
                StartCoroutine(PulseRect(sceneRefs.OpeningStackImage.rectTransform, 1.14f, Mathf.Max(0.24f, theme.pulseDuration * 1.35f)));
            }

            ShowSeatCallout(SeatId.Top, "HEY PARTNER !", 1.6f, new Color(theme.green.r, theme.green.g, theme.green.b, 0.95f), theme.backgroundColor);

            var handsBySeat = SpadesSeatUtility.TurnOrder.ToDictionary(seat => seat, seat => controller.GetHand(seat).ToList());
            var perSeatIndex = SpadesSeatUtility.TurnOrder.ToDictionary(seat => seat, _ => 0);
            SetStartupCardGroupsVisible(true);
            var opponentRevealTargets = PrepareOpponentOpeningDealRealCards();
            var bottomRevealTargets = PrepareBottomOpeningDealRealCards(handsBySeat[SeatId.Bottom]);
            var previewSources = openingStackPreviewCards.ToList();
            var flights = new List<OpeningDealFlight>(52);
            var ghosts = new List<CardButtonView>(52);
            var delayStep = OpeningDealDelayStep;
            var travelDuration = Mathf.Max(OpeningDealTravelSeconds, theme.modalDuration * 1.75f);

            foreach (var recipient in BuildOpeningDealOrder(controller.State.RoundState.Dealer))
            {
                var cardIndex = perSeatIndex[recipient];
                perSeatIndex[recipient] = cardIndex + 1;
                var card = handsBySeat[recipient][cardIndex];
                var revealTarget = recipient == SeatId.Bottom
                    ? cardIndex < bottomRevealTargets.Count ? bottomRevealTargets[cardIndex] : null
                    : GetOpponentOpeningDealRevealTarget(opponentRevealTargets, recipient, cardIndex);
                var motion = BuildOpeningDealMotion(
                    recipient,
                    card,
                    cardIndex,
                    handsBySeat[recipient].Count,
                    revealTarget,
                    flights.Count,
                    previewSources.Count);
                if (recipient != SeatId.Bottom && revealTarget == null)
                {
                    revealTarget = CreateOpeningDealRuntimeSeatCard(recipient, cardIndex, motion)?.CanvasGroup;
                }

                flights.Add(new OpeningDealFlight
                {
                    Motion = motion,
                    RevealToHand = recipient == SeatId.Bottom,
                    RevealTarget = revealTarget,
                    Delay = GetOpeningDealFlightDelay(flights.Count, delayStep),
                    SequenceIndex = flights.Count
                });
            }

            for (var i = 0; i < flights.Count; i++)
            {
                var flight = flights[i];
                var usesPreviewCard = i < previewSources.Count && previewSources[i] != null;
                var ghost = usesPreviewCard
                    ? previewSources[i]
                    : CreateFloatingCard(flight.Motion);
                DetachOpeningStackPreviewCard(ghost);
                if (!floatingCards.Contains(ghost))
                {
                    floatingCards.Add(ghost);
                }

                PrepareOpeningDealGhostAtCenter(ghost, flight);
                flight.Ghost = ghost;
                ghosts.Add(ghost);
            }

            for (var i = 0; i < flights.Count; i++)
            {
                StartCoroutine(AnimateOpeningDealGhost(flights[i], travelDuration, i == 0));
            }

            var finalDelay = flights.Count > 0 ? flights.Max(flight => flight.Delay) : 0f;
            yield return new WaitForSecondsRealtime(finalDelay + travelDuration + 0.45f);

            suppressNextHandEntryAnimation = true;
            openingDealPending = false;
            openingDealRunning = false;
            ClearOpeningStackPreviewCards();
            SetStartupCardGroupsVisible(true);
            RenderAll();

            foreach (var ghost in ghosts)
            {
                CleanupFloatingCard(ghost);
            }

            PlayFeedback(FeedbackCue.Collect, 0.18f);
            yield return StartCoroutine(OpeningDealFinale());
            yield return StartCoroutine(AnimateAnimationRootCardsOut());
            BeginHandReview();
        }

        private void BeginHandReview()
        {
            if (handReviewLoop != null)
            {
                StopCoroutine(handReviewLoop);
            }

            handReviewPending = true;
            SetBidSheetVisible(false);
            RenderAll();
            handReviewLoop = StartCoroutine(HandReviewRoutine());
        }

        private IEnumerator HandReviewRoutine()
        {
            yield return new WaitForSecondsRealtime(Mathf.Max(0f, handReviewSeconds));
            handReviewPending = false;
            handReviewLoop = null;
            RenderAll();
            ScheduleAiLoop();
        }

        private void BeginBidTurnDelay()
        {
            if (bidTurnDelayLoop != null)
            {
                StopCoroutine(bidTurnDelayLoop);
            }

            bidTurnDelayPending = true;
            SetBidSheetVisible(false);
            RenderAll();
            bidTurnDelayLoop = StartCoroutine(BidTurnDelayRoutine());
        }

        private IEnumerator BidTurnDelayRoutine()
        {
            yield return new WaitForSecondsRealtime(Mathf.Max(0f, bidTurnDelaySeconds));
            bidTurnDelayPending = false;
            bidTurnDelayLoop = null;
            RenderAll();
            ScheduleAiLoop();
        }

        private void StartDealButtonDismiss()
        {
            if (sceneRefs.DealButton == null)
            {
                return;
            }

            sceneRefs.DealButton.interactable = false;
            sceneRefs.DealButton.gameObject.SetActive(false);
        }

        private IEnumerator FadeDealButtonOut()
        {
            if (sceneRefs.DealButton == null)
            {
                dealButtonFadeLoop = null;
                yield break;
            }

            var canvasGroup = ResolveCanvasGroup(sceneRefs.DealButton.gameObject);
            var startAlpha = canvasGroup.alpha;
            var duration = Mathf.Max(0.08f, theme.modalDuration * 0.6f);
            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                canvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, Mathf.SmoothStep(0f, 1f, t));
                yield return null;
            }

            canvasGroup.alpha = 0f;
            sceneRefs.DealButton.gameObject.SetActive(false);
            canvasGroup.alpha = 1f;
            dealButtonFadeLoop = null;
        }

        private void ResetDealButtonVisualState()
        {
            if (sceneRefs.DealButton == null)
            {
                return;
            }

            var canvasGroup = ResolveCanvasGroup(sceneRefs.DealButton.gameObject);
            canvasGroup.alpha = 1f;
            sceneRefs.DealButton.gameObject.SetActive(false);
            sceneRefs.DealButton.interactable = false;
        }

        private static CanvasGroup ResolveCanvasGroup(GameObject target)
        {
            var canvasGroup = target.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = target.AddComponent<CanvasGroup>();
            }

            return canvasGroup;
        }

        private void StartExitPromptVisibility(bool visible, System.Action onComplete = null)
        {
            if (sceneRefs.ExitPromptOverlay == null)
            {
                onComplete?.Invoke();
                return;
            }

            if (exitPromptFadeLoop != null)
            {
                StopCoroutine(exitPromptFadeLoop);
                exitPromptFadeLoop = null;
            }

            exitPromptFadeLoop = StartCoroutine(AnimateExitPromptVisibility(visible, onComplete));
        }

        private IEnumerator AnimateExitPromptVisibility(bool visible, System.Action onComplete)
        {
            if (sceneRefs.ExitPromptOverlay == null)
            {
                exitPromptFadeLoop = null;
                onComplete?.Invoke();
                yield break;
            }

            var overlayObject = sceneRefs.ExitPromptOverlay.gameObject;
            var canvasGroup = ResolveCanvasGroup(overlayObject);
            var startAlpha = canvasGroup.alpha;
            var targetAlpha = visible ? 1f : 0f;
            var duration = Mathf.Max(0.12f, theme.modalDuration * 0.85f);

            if (visible)
            {
                overlayObject.SetActive(true);
                canvasGroup.blocksRaycasts = true;
                canvasGroup.interactable = true;
            }
            else
            {
                canvasGroup.blocksRaycasts = false;
                canvasGroup.interactable = false;
            }

            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, Mathf.SmoothStep(0f, 1f, t));
                yield return null;
            }

            canvasGroup.alpha = targetAlpha;
            if (!visible)
            {
                overlayObject.SetActive(false);
            }

            exitPromptFadeLoop = null;
            onComplete?.Invoke();
        }

        private void ResetExitPromptVisualState()
        {
            if (sceneRefs.ExitPromptOverlay == null)
            {
                return;
            }

            if (exitPromptFadeLoop != null)
            {
                StopCoroutine(exitPromptFadeLoop);
                exitPromptFadeLoop = null;
            }

            var overlayObject = sceneRefs.ExitPromptOverlay.gameObject;
            var canvasGroup = ResolveCanvasGroup(overlayObject);
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
            overlayObject.SetActive(false);
            activePrompt = ConfirmationPromptType.None;
        }

        private void PrepareOpeningDealGhostAtCenter(CardButtonView ghost, OpeningDealFlight flight)
        {
            if (ghost == null || flight == null)
            {
                return;
            }

            var motion = flight.Motion;
            ghost.gameObject.SetActive(true);
            ghost.transform.SetAsLastSibling();
            ghost.Button.onClick.RemoveAllListeners();
            ghost.Button.enabled = false;
            ghost.CanvasGroup = ghost.CanvasGroup != null ? ghost.CanvasGroup : ResolveCanvasGroup(ghost.gameObject);
            ghost.CanvasGroup.alpha = 1f;
            ghost.CanvasGroup.blocksRaycasts = false;
            ghost.Root.anchorMin = new Vector2(0.5f, 0.5f);
            ghost.Root.anchorMax = new Vector2(0.5f, 0.5f);
            ghost.Root.pivot = new Vector2(0.5f, 0.5f);
            ghost.Root.anchoredPosition = motion.StartPosition;
            ghost.Root.localRotation = motion.StartRotation;
            ghost.Root.sizeDelta = motion.StartSize;
            ghost.Root.localScale = Vector3.one;
            ApplyCardBackVisual(ghost);
        }

        private IEnumerator AnimateOpeningDealGhost(OpeningDealFlight flight, float duration, bool playLaunchSound)
        {
            if (flight == null || flight.Ghost == null)
            {
                yield break;
            }

            var ghost = flight.Ghost;
            var motion = flight.Motion;
            var revealToHand = flight.RevealToHand;
            if (flight.Delay > 0f)
            {
                yield return AnimateOpeningDealCenterHold(ghost, motion, flight.Delay, flight.SequenceIndex);
            }

            if (ghost == null)
            {
                yield break;
            }

            ghost.gameObject.SetActive(true);
            ghost.transform.SetAsLastSibling();
            ghost.Root.anchoredPosition = motion.StartPosition;
            ghost.Root.localRotation = motion.StartRotation;
            ghost.Root.sizeDelta = motion.StartSize;
            ghost.Root.localScale = Vector3.one;
            ghost.CanvasGroup = ghost.CanvasGroup != null ? ghost.CanvasGroup : ResolveCanvasGroup(ghost.gameObject);
            ghost.CanvasGroup.alpha = 1f;

            if (revealToHand)
            {
                ConfigureFloatingCardView(ghost, motion.Card);
            }
            else
            {
                ApplyCardBackVisual(ghost);
            }

            if (ghost == null)
            {
                yield break;
            }

            if (playLaunchSound)
            {
                PlayFeedback(FeedbackCue.Deal, 0.16f);
            }

            if (!playLaunchSound && flight.SequenceIndex % 4 == 0)
            {
                PlayFeedback(FeedbackCue.Deal, 0.08f);
            }

            var faceState = CaptureCardFaceState(ghost);
            var revealedFace = !revealToHand;
            if (revealToHand)
            {
                ApplyCardBackVisual(ghost);
            }

            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                if (revealToHand && !revealedFace && t >= 0.54f)
                {
                    ApplyCardFaceVisual(ghost, faceState);
                    revealedFace = true;
                }

                ApplyOpeningDealFlightPose(ghost, motion, t, revealToHand);
                yield return null;
            }

            if (revealToHand)
            {
                ApplyCardFaceVisual(ghost, faceState);
            }

            ApplyOpeningDealFlightPose(ghost, motion, 1f, false);
            RevealOpeningDealRealCard(flight.RevealTarget);
            CleanupFloatingCard(ghost);
        }

        private IEnumerator AnimateOpeningDealCenterHold(CardButtonView ghost, CardMotionSnapshot motion, float duration, int sequenceIndex)
        {
            var elapsed = 0f;
            var phase = sequenceIndex * 0.37f;
            while (elapsed < duration)
            {
                if (ghost == null)
                {
                    yield break;
                }

                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                var driftFalloff = Mathf.Lerp(1f, 0.2f, t);
                var drift = new Vector2(
                    Mathf.Sin(Time.unscaledTime * 1.4f + phase) * 0.8f,
                    Mathf.Cos(Time.unscaledTime * 1.1f + phase) * 0.6f) * driftFalloff;
                ghost.Root.anchoredPosition = motion.StartPosition + drift;
                ghost.Root.sizeDelta = motion.StartSize;
                ghost.Root.localRotation = motion.StartRotation;
                ghost.Root.localScale = Vector3.one;
                ghost.CanvasGroup.alpha = 1f;
                yield return null;
            }
        }

        private void ApplyOpeningDealFlightPose(CardButtonView ghost, CardMotionSnapshot motion, float progress, bool flipReveal)
        {
            if (ghost == null || ghost.CanvasGroup == null)
            {
                return;
            }

            var root = ghost.Root;
            var eased = EaseInOutCubic(progress);
            var start = motion.StartPosition;
            var end = motion.EndPosition;
            var distance = Vector2.Distance(start, end);
            var direction = distance > 0.1f ? (end - start).normalized : Vector2.up;
            var side = GetOpeningDealLaneSide(motion.Seat);
            var tangent = new Vector2(-direction.y, direction.x) * side;
            var launchPullback = progress < 0.13f
                ? -direction * Mathf.Sin(Mathf.Clamp01(progress / 0.13f) * Mathf.PI) * Mathf.Clamp(distance * 0.035f, 8f, 28f)
                : Vector2.zero;
            var controlOne = start
                             + direction * Mathf.Clamp(distance * 0.16f, 26f, 88f)
                             + Vector2.up * Mathf.Clamp(distance * 0.1f, 28f, 84f)
                             - tangent * Mathf.Clamp(distance * 0.035f, 10f, 32f);
            var controlTwo = Vector2.Lerp(start, end, 0.68f)
                             + Vector2.up * Mathf.Clamp(distance * 0.21f, 58f, 178f)
                             + tangent * Mathf.Clamp(distance * 0.115f, 24f, 92f);
            var settleDip = Vector2.down * Mathf.Sin(progress * Mathf.PI) * Mathf.Lerp(0f, 10f, Mathf.Clamp01((progress - 0.62f) / 0.38f));
            var snap = progress > 0.8f
                ? direction * Mathf.Sin(Mathf.Clamp01((progress - 0.8f) / 0.2f) * Mathf.PI) * Mathf.Clamp(distance * 0.018f, 5f, 18f)
                : Vector2.zero;
            root.anchoredPosition = CubicBezier(start, controlOne, controlTwo, end, eased) + settleDip + launchPullback + snap;

            var sizeEase = EaseOutCubic(progress);
            var peakSize = Vector2.Lerp(motion.StartSize, motion.EndSize, 0.55f) * 1.13f;
            root.sizeDelta = progress < 0.5f
                ? Vector2.Lerp(motion.StartSize, peakSize, EaseOutCubic(progress / 0.5f))
                : Vector2.Lerp(peakSize, motion.EndSize, EaseOutCubic((progress - 0.5f) / 0.5f));

            var travelRotation = Quaternion.Slerp(
                motion.StartRotation,
                motion.EndRotation,
                sizeEase);
            var twist = Mathf.Sin(progress * Mathf.PI) * GetOpeningDealTwist(motion.Seat);
            var snapTilt = Mathf.Sin(Mathf.Clamp01((progress - 0.78f) / 0.22f) * Mathf.PI) * -GetOpeningDealLaneSide(motion.Seat) * 4f;
            root.localRotation = travelRotation * Quaternion.Euler(0f, 0f, twist + snapTilt);

            var revealSquash = flipReveal && progress > 0.48f && progress < 0.62f
                ? Mathf.Sin(Mathf.Clamp01((progress - 0.48f) / 0.14f) * Mathf.PI) * 0.09f
                : 0f;
            var pulse = 1f + Mathf.Sin(progress * Mathf.PI) * (flipReveal ? 0.085f : 0.06f) + revealSquash;
            root.localScale = Vector3.one * pulse;
            ghost.CanvasGroup.alpha = Mathf.Lerp(0.96f, 1f, sizeEase);
        }

        private void SubmitBid(int bid)
        {
            if (IsGameplayInputBlocked() || controller == null || openingDealPending || openingDealRunning)
            {
                return;
            }

            if (!controller.TrySubmitBid(SeatId.Bottom, bid, out var error))
            {
                PlayFeedback(FeedbackCue.Invalid, 0.18f);
                FlashStatus(error, theme.red);
                return;
            }

            pendingBidSelection = null;
            SetBidSheetVisible(false);
            HideAllBidBubbles(true);
            RenderAll();
            ScheduleAiLoop();
        }

        private void OnCardTapped(Card card)
        {
            if (IsGameplayInputBlocked())
            {
                return;
            }

            if (openingDealPending || openingDealRunning)
            {
                FlashStatus("Cards are dealing automatically.", theme.gold);
                return;
            }

            if (controller == null || controller.State.Phase != MatchPhase.TrickPlay)
            {
                PlayFeedback(FeedbackCue.Invalid, 0.16f);
                FlashStatus("Cards are not live yet.", theme.red);
                return;
            }

            if (controller.State.RoundState.TrickState.CurrentTurn != SeatId.Bottom)
            {
                PlayFeedback(FeedbackCue.Invalid, 0.16f);
                FlashStatus("Wait for your turn.", theme.red);
                return;
            }

            if (selectedCard.HasValue && selectedCard.Value.Equals(card))
            {
                TryPlaySelected();
                return;
            }

            var clearedPreviousSelection = selectedCard.HasValue && !selectedCard.Value.Equals(card);
            if (clearedPreviousSelection)
            {
                selectedCard = null;
            }

            var legalCards = controller.GetLegalCardsForSeat(SeatId.Bottom).ToHashSet();
            if (!legalCards.Contains(card))
            {
                PlayFeedback(FeedbackCue.Invalid, 0.18f);
                FlashStatus("Follow suit is required. Spades can only cut when you are void in the led suit.", theme.red);
                var invalidView = handPool.FirstOrDefault(view => view.gameObject.activeSelf && view.gameObject.name == card.ShortLabel);
                if (invalidView != null)
                {
                    StartCoroutine(Shake(invalidView.Root));
                }

                if (clearedPreviousSelection)
                {
                    RenderHand();
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

            RenderHand();
        }

        private void OnPlaySelected()
        {
            if (IsGameplayInputBlocked())
            {
                return;
            }

            TryPlaySelected();
        }

        private void TryPlaySelected()
        {
            if (IsGameplayInputBlocked())
            {
                return;
            }

            if (openingDealPending || openingDealRunning)
            {
                FlashStatus("Cards are dealing automatically.", theme.gold);
                return;
            }

            if (!selectedCard.HasValue)
            {
                PlayFeedback(FeedbackCue.Invalid, 0.16f);
                FlashStatus("Pick a card first.", theme.red);
                return;
            }

            if (!controller.TryPlayCard(SeatId.Bottom, selectedCard.Value, out var error))
            {
                PlayFeedback(FeedbackCue.Invalid, 0.18f);
                FlashStatus(error, theme.red);
                return;
            }

            selectedCard = null;
            RenderAll();
            ScheduleAiLoop();
        }

        private void SetPlaySelectedButtonActive(bool active)
        {
            if (sceneRefs.PlaySelectedButton == null)
            {
                return;
            }

            sceneRefs.PlaySelectedButton.gameObject.SetActive(active);
        }

        private void ScheduleAiLoop()
        {
            if (aiLoop != null)
            {
                StopCoroutine(aiLoop);
                aiLoop = null;
            }

            if (exitPromptOpen || optionsMenuOpen || avatarIntroRunning || openingDealPending || handReviewPending || bidTurnDelayPending || HasVisualMotionPending)
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
                if (exitPromptOpen || optionsMenuOpen || avatarIntroRunning)
                {
                    aiLoop = null;
                    yield break;
                }

                yield return new WaitForSecondsRealtime(0.55f);
                if (exitPromptOpen || optionsMenuOpen || avatarIntroRunning || handReviewPending || bidTurnDelayPending || controller == null || HasVisualMotionPending)
                {
                    aiLoop = null;
                    yield break;
                }

                controller.AdvanceAiTurn();
                RenderAll();
            }

            aiLoop = null;
        }

        private bool ShouldTriggerSpadesBrokenMoment(CardPlayedEvent playedEvent)
        {
            if (spadesBrokenMomentShown || playedEvent == null || playedEvent.Card.Suit != Suit.Spades)
            {
                return false;
            }

            if (selectedRule == null)
            {
                return true;
            }

            return selectedRule.SpadesMustBeBroken && !selectedRule.AllowSpadesAnytime;
        }

        private void QueueCardPlayAnimation(CardPlayedEvent playedEvent, bool triggerSpadesBrokenMoment)
        {
            hiddenTrickSlots.Add(playedEvent.Seat);
            EnqueueAnimation(AnimateCardPlayRoutine(BuildCardPlayMotion(playedEvent.Seat, playedEvent.Card), triggerSpadesBrokenMoment));
        }

        private void QueueTrickCollectionAnimation(TrickResolvedEvent trickEvent)
        {
            resolvedTrickCards.Clear();
            foreach (var play in trickEvent.CompletedTrick)
            {
                resolvedTrickCards[play.Seat] = play.Card;
            }

            var bigBook = IsBigBook(trickEvent.CompletedTrick, trickEvent.Winner);
            var motions = trickEvent.CompletedTrick
                .Select((play, index) => BuildTrickCollectMotion(play, trickEvent.Winner, index, bigBook))
                .ToList();
            EnqueueAnimation(AnimateTrickCollectRoutine(trickEvent.Winner, motions, bigBook));
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

        private IEnumerator AnimateCardPlayRoutine(CardMotionSnapshot motion, bool triggerSpadesBrokenMoment)
        {
            var ghost = CreateFloatingCard(motion);
            yield return AnimateStreetCardPlay(
                ghost,
                motion,
                Mathf.Max(0.34f, theme.modalDuration * 1.45f),
                revealFromBack: motion.Seat != SeatId.Bottom);
            CleanupFloatingCard(ghost);
            hiddenTrickSlots.Remove(motion.Seat);
            RenderTrickArea();
            PlayRandomCardPlaceSound();
            SpawnCardPlayFx(motion);
            if (triggerSpadesBrokenMoment)
            {
                PlaySpadesBrokenSound();
                SpawnSpadesBrokenMomentFx(motion);
                TrySpawnTrashTalkPopup(motion, "STREET RULES", TrashTalkSpadesBrokenChance);
            }
            else if (IsTrashTalkStrongMoveCard(motion.Card))
            {
                TrySpawnTrashTalkPopup(motion, null, TrashTalkStrongCardChance);
            }

            yield return PulseRect(trickSlots[motion.Seat].Root, 1.06f, Mathf.Max(0.12f, theme.pulseDuration * 0.75f));
        }

        private IEnumerator AnimateTrickCollectRoutine(SeatId winner, IReadOnlyList<CardMotionSnapshot> motions, bool bigBook)
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

            var duration = bigBook
                ? Mathf.Max(0.16f, theme.modalDuration * 0.82f)
                : Mathf.Max(0.2f, theme.modalDuration * 1.15f);
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

                    ApplyTrickCollectCardPose(ghost, motion, localTime, bigBook);
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
            var bookStreak = RegisterBookStreak(winner);
            var streakBoost = ResolveBookStreakBoost(bookStreak);
            PlayFeedback(FeedbackCue.Collect, 0.16f);
            PlayBookWonSound();
            if (bigBook)
            {
                PlayTableSlamSound();
            }

            TryPlayCrowdReaction(0.25f, ResolveBookStreakCrowdVolume(bookStreak, bigBook), 0.24f, true);

            RefreshBookLeaderLightningFx();
            SetLatestBookAura(winner);
            StartBookCameraShake(winner, (bigBook ? 0.95f : 0.62f) * streakBoost);
            StartBookAvatarHit(winner, bookStreak, bigBook);
            var winnerPoint = GetAnchoredPoint(seatViews[winner].Root, GetSeatInnerAnchor(winner));
            var discardPoint = GetAnchoredPoint(sceneRefs.DiscardAnchorImage.rectTransform, new Vector2(0.5f, 0.5f));
            SpawnBookStreakFx(winnerPoint, bookStreak, bigBook);
            if (bigBook)
            {
                SpawnEpicToonFx(tableSlamFxPrefab != null ? tableSlamFxPrefab : cardImpactFxPrefab, discardPoint + new Vector2(0f, -8f), 0.98f + Mathf.Min(bookStreak - 1, 5) * 0.08f);
            }

            TrySpawnBookCollectSmoke(discardPoint, bigBook);
            StartBookTextImpact(winner, null, winner.ToTeam() == TeamId.Home ? theme.green : theme.red, false);
            if (bigBook)
            {
                TrySpawnTrashTalkPopup(winnerPoint + new Vector2(0f, 40f), null, TrashTalkBigBookChance, theme.gold);
            }

            if (bigBook && seatViews.TryGetValue(winner, out var winnerView) && winnerView?.Root != null)
            {
                StartCoroutine(PulseRect(winnerView.Root, 1.055f, Mathf.Max(0.12f, theme.pulseDuration * 0.7f)));
            }

            yield return PulseRect(sceneRefs.DiscardAnchorImage.rectTransform, 1.07f, Mathf.Max(0.12f, theme.pulseDuration * 0.8f));
        }

        private void StartBookAvatarHit(SeatId seat, int streak, bool bigBook)
        {
            if (bookAvatarLoops.TryGetValue(seat, out var activeLoop) && activeLoop != null)
            {
                return;
            }

            if (!seatAvatarImages.TryGetValue(seat, out var avatarImage) || avatarImage == null)
            {
                return;
            }

            var avatarRect = avatarImage.rectTransform;
            if (avatarRect == null)
            {
                return;
            }

            var borderObject = ResolveAvatarBorderObject(seat, avatarImage);
            var borderRect = borderObject != null ? borderObject.transform as RectTransform : null;
            bookAvatarLoops[seat] = StartCoroutine(BookAvatarHitRoutine(seat, avatarImage, avatarRect, borderRect, streak, bigBook));
        }

        private IEnumerator BookAvatarHitRoutine(
            SeatId seat,
            Image avatarImage,
            RectTransform avatarRect,
            RectTransform borderRect,
            int streak,
            bool bigBook)
        {
            var avatarStartScale = avatarRect.localScale;
            var avatarStartRotation = avatarRect.localRotation;
            var borderStartScale = borderRect != null ? borderRect.localScale : Vector3.one;
            var borderStartRotation = borderRect != null ? borderRect.localRotation : Quaternion.identity;
            var avatarStartColor = avatarImage.color;
            var borderGraphics = borderRect != null ? borderRect.GetComponentsInChildren<Graphic>(true) : System.Array.Empty<Graphic>();
            var borderStartColors = borderGraphics.Select(graphic => graphic != null ? graphic.color : Color.white).ToArray();
            var teamColor = seat.ToTeam() == TeamId.Home ? theme.green : theme.red;
            var streak01 = Mathf.Clamp01((Mathf.Max(1, streak) - 1) / 5f);
            var peakScale = bigBook
                ? Mathf.Lerp(1.18f, 1.28f, streak01)
                : Mathf.Lerp(1.12f, 1.2f, streak01);
            var borderPeakScale = peakScale + 0.06f;
            var tilt = (seat == SeatId.Left || seat == SeatId.Top ? -1f : 1f) * Mathf.Lerp(7f, 12f, streak01) * (bigBook ? 1.12f : 1f);
            var duration = bigBook ? 0.54f : 0.44f;
            var elapsed = 0f;

            while (elapsed < duration && avatarRect != null)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                var intro = Mathf.Clamp01(t / 0.34f);
                var settle = Mathf.Clamp01((t - 0.28f) / 0.72f);
                var pop = EaseOutBack(intro);
                var settleWave = Mathf.Sin(settle * Mathf.PI);
                var avatarScale = Mathf.LerpUnclamped(1f, peakScale, pop) - settleWave * 0.055f;
                var borderScale = Mathf.LerpUnclamped(1f, borderPeakScale, pop) - settleWave * 0.075f;
                var rotation = Mathf.Sin(t * Mathf.PI) * tilt + Mathf.Sin(t * Mathf.PI * 3.2f) * 2.1f * (1f - t);
                avatarRect.localScale = avatarStartScale * avatarScale;
                avatarRect.localRotation = avatarStartRotation * Quaternion.Euler(0f, 0f, rotation);
                avatarImage.color = Color.Lerp(avatarStartColor, Color.Lerp(Color.white, teamColor, 0.42f), Mathf.Sin(t * Mathf.PI));

                if (borderRect != null)
                {
                    borderRect.localScale = borderStartScale * borderScale;
                    borderRect.localRotation = borderStartRotation * Quaternion.Euler(0f, 0f, -rotation * 0.62f);
                }

                for (var index = 0; index < borderGraphics.Length; index++)
                {
                    var graphic = borderGraphics[index];
                    if (graphic == null)
                    {
                        continue;
                    }

                    graphic.color = Color.Lerp(borderStartColors[index], Color.Lerp(theme.gold, teamColor, 0.35f), Mathf.Sin(t * Mathf.PI));
                }

                yield return null;
            }

            if (avatarRect != null)
            {
                avatarRect.localScale = avatarStartScale;
                avatarRect.localRotation = avatarStartRotation;
            }

            if (avatarImage != null)
            {
                avatarImage.color = avatarStartColor;
            }

            if (borderRect != null)
            {
                borderRect.localScale = borderStartScale;
                borderRect.localRotation = borderStartRotation;
            }

            for (var index = 0; index < borderGraphics.Length; index++)
            {
                if (borderGraphics[index] != null)
                {
                    borderGraphics[index].color = borderStartColors[index];
                }
            }

            bookAvatarLoops[seat] = null;
        }

        private int RegisterBookStreak(SeatId winner)
        {
            var previous = consecutiveBookStreaks.TryGetValue(winner, out var count) ? count : 0;
            var next = Mathf.Clamp(previous + 1, 1, 8);
            consecutiveBookStreaks[SeatId.Bottom] = winner == SeatId.Bottom ? next : 0;
            consecutiveBookStreaks[SeatId.Left] = winner == SeatId.Left ? next : 0;
            consecutiveBookStreaks[SeatId.Top] = winner == SeatId.Top ? next : 0;
            consecutiveBookStreaks[SeatId.Right] = winner == SeatId.Right ? next : 0;
            return next;
        }

        private void ResetBookStreaks()
        {
            consecutiveBookStreaks[SeatId.Bottom] = 0;
            consecutiveBookStreaks[SeatId.Left] = 0;
            consecutiveBookStreaks[SeatId.Top] = 0;
            consecutiveBookStreaks[SeatId.Right] = 0;
        }

        private static float ResolveBookStreakBoost(int streak)
        {
            var step = Mathf.Clamp(streak - 1, 0, 6);
            return 1f + step * 0.34f + Mathf.Max(0, step - 2) * 0.08f;
        }

        private static float ResolveBookStreakCrowdVolume(int streak, bool bigBook)
        {
            return Mathf.Clamp((bigBook ? 1.02f : 0.82f) + Mathf.Clamp(streak - 1, 0, 5) * 0.13f, 0.7f, 1.24f);
        }

        private void SpawnBookStreakFx(Vector2 winnerPoint, int streak, bool bigBook)
        {
            var streakStep = Mathf.Clamp(streak - 1, 0, 5);
            StartCoroutine(BookStreakStickerRoutine(winnerPoint, streak, bigBook, streakStep));
        }

        private IEnumerator BookStreakStickerRoutine(Vector2 winnerPoint, int streak, bool bigBook, int streakStep)
        {
            var root = AnimationRoot;
            if (root == null)
            {
                yield break;
            }

            var fxObject = new GameObject("Book Streak Sticker Runtime", typeof(RectTransform), typeof(CanvasGroup));
            fxObject.transform.SetParent(root, false);
            fxObject.transform.SetAsLastSibling();
            var fxRect = (RectTransform)fxObject.transform;
            fxRect.anchorMin = new Vector2(0.5f, 0.5f);
            fxRect.anchorMax = new Vector2(0.5f, 0.5f);
            fxRect.pivot = new Vector2(0.5f, 0.5f);
            fxRect.sizeDelta = new Vector2(180f + streakStep * 18f, 88f + streakStep * 9f);
            fxRect.anchoredPosition = winnerPoint + new Vector2(0f, 12f + streakStep * 5f);
            fxRect.localScale = Vector3.one * 0.5f;
            fxRect.localRotation = Quaternion.Euler(0f, 0f, Random.Range(-7f, 7f));

            var group = fxObject.GetComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;
            group.alpha = 0f;

            var plate = CreateStickerImage("Book Streak Plate", fxRect, new Vector2(0.12f, 0.18f), new Vector2(0.88f, 0.82f), new Color(0.02f, 0.024f, 0.025f, 0.8f));
            var flash = CreateStickerImage("Book Streak Flash", fxRect, new Vector2(0f, 0f), new Vector2(1f, 1f), new Color(theme.gold.r, theme.gold.g, theme.gold.b, 0.28f));
            var strikeA = CreateStickerImage("Book Streak Strike A", fxRect, new Vector2(0.04f, 0.44f), new Vector2(0.96f, 0.56f), new Color(theme.gold.r, theme.gold.g, theme.gold.b, 0.9f));
            var strikeB = CreateStickerImage("Book Streak Strike B", fxRect, new Vector2(0.18f, 0.31f), new Vector2(0.82f, 0.42f), new Color(theme.green.r, theme.green.g, theme.green.b, 0.75f));
            strikeA.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -8f);
            strikeB.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 7f);

            var labelObject = new GameObject("Book Streak Label", typeof(RectTransform), typeof(Text), typeof(Outline), typeof(Shadow));
            labelObject.transform.SetParent(fxRect, false);
            labelObject.transform.SetAsLastSibling();
            var labelRect = (RectTransform)labelObject.transform;
            SetAnchors(labelRect, new Vector2(0.08f, 0.04f), new Vector2(0.92f, 0.96f));
            var label = labelObject.GetComponent<Text>();
            label.font = theme.ResolveFont();
            label.text = streak >= 2 ? $"BOOK x{streak}" : "BOOK";
            label.fontSize = streak >= 2 ? 31 + streakStep * 2 : 34;
            label.fontStyle = FontStyle.BoldAndItalic;
            label.alignment = TextAnchor.MiddleCenter;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.color = bigBook ? theme.gold : theme.primaryText;
            label.raycastTarget = false;
            var outline = labelObject.GetComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
            outline.effectDistance = new Vector2(2.8f, -2.8f);
            var shadow = labelObject.GetComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.42f);
            shadow.effectDistance = new Vector2(7f, -7f);

            transientFx.Add(plate);
            transientFx.Add(flash);
            transientFx.Add(strikeA);
            transientFx.Add(strikeB);
            transientFx.Add(label);

            var duration = 0.82f;
            var elapsed = 0f;
            var startRotation = fxRect.localRotation;
            var endRotation = startRotation * Quaternion.Euler(0f, 0f, Random.Range(-4f, 4f));
            while (elapsed < duration && fxRect != null)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                var intro = Mathf.Clamp01(t / 0.28f);
                var outT = Mathf.Clamp01((t - 0.66f) / 0.34f);
                var punch = EaseOutBack(intro);
                var swagger = Mathf.Sin(t * Mathf.PI * 4f) * (1f - t) * 2.8f;
                fxRect.localScale = Vector3.one * Mathf.LerpUnclamped(0.5f, 1.05f + streakStep * 0.06f, punch) * Mathf.Lerp(1f, 0.74f, outT);
                fxRect.anchoredPosition = winnerPoint + new Vector2(0f, 12f + streakStep * 5f + Mathf.Sin(t * Mathf.PI) * 18f + outT * 22f);
                fxRect.localRotation = Quaternion.Lerp(startRotation, endRotation, t) * Quaternion.Euler(0f, 0f, swagger);
                group.alpha = Mathf.Min(Mathf.Clamp01(intro * 2.4f), 1f - outT);
                flash.color = new Color(flash.color.r, flash.color.g, flash.color.b, Mathf.Lerp(0.38f, 0f, t));
                yield return null;
            }

            transientFx.Remove(plate);
            transientFx.Remove(flash);
            transientFx.Remove(strikeA);
            transientFx.Remove(strikeB);
            transientFx.Remove(label);
            if (fxObject != null)
            {
                Destroy(fxObject);
            }
        }

        private Image CreateStickerImage(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Color color)
        {
            var imageObject = new GameObject(name, typeof(RectTransform), typeof(Image));
            imageObject.transform.SetParent(parent, false);
            var rect = (RectTransform)imageObject.transform;
            SetAnchors(rect, anchorMin, anchorMax);
            var image = imageObject.GetComponent<Image>();
            image.sprite = ResolveSoftPanelSprite();
            image.type = Image.Type.Sliced;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private IEnumerator AnimateFloatingCard(CardButtonView ghost, CardMotionSnapshot motion, float duration, bool fadeOutNearEnd, bool revealFromBack)
        {
            if (ghost == null)
            {
                yield break;
            }

            var faceState = CaptureCardFaceState(ghost);
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
                    ApplyCardFaceVisual(ghost, faceState);
                    revealedFace = true;
                }

                ApplyFloatingCardPose(ghost, motion, t, fadeOutNearEnd, revealFromBack);
                yield return null;
            }

            if (revealFromBack)
            {
                ApplyCardFaceVisual(ghost, faceState);
            }

            ApplyFloatingCardPose(ghost, motion, 1f, fadeOutNearEnd, false);
        }

        private IEnumerator AnimateStreetCardPlay(CardButtonView ghost, CardMotionSnapshot motion, float duration, bool revealFromBack)
        {
            if (ghost == null)
            {
                yield break;
            }

            var faceState = CaptureCardFaceState(ghost);
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
                if (revealFromBack && !revealedFace && t >= 0.36f)
                {
                    ApplyCardFaceVisual(ghost, faceState);
                    revealedFace = true;
                }

                ApplyStreetCardPlayPose(ghost, motion, t, revealFromBack && !revealedFace);
                yield return null;
            }

            if (revealFromBack)
            {
                ApplyCardFaceVisual(ghost, faceState);
            }

            ApplyStreetCardPlayPose(ghost, motion, 1f, false);
        }

        private void ApplyStreetCardPlayPose(CardButtonView ghost, CardMotionSnapshot motion, float progress, bool backShowing)
        {
            if (ghost == null || ghost.CanvasGroup == null)
            {
                return;
            }

            RectTransform root;
            try
            {
                root = ghost.transform as RectTransform;
            }
            catch (MissingReferenceException)
            {
                return;
            }

            if (root == null)
            {
                return;
            }

            var windup = Mathf.Clamp01(progress / 0.18f);
            var travel = Mathf.Clamp01((progress - 0.10f) / 0.74f);
            var settle = Mathf.Clamp01((progress - 0.78f) / 0.22f);
            var travelEase = 1f - Mathf.Pow(1f - travel, 3f);
            var settleEase = Mathf.Sin(settle * Mathf.PI);
            var seatSide = motion.Seat switch
            {
                SeatId.Left => -1f,
                SeatId.Right => 1f,
                SeatId.Top => 0.45f,
                _ => -0.35f
            };
            var sourcePull = new Vector2(seatSide * -18f, motion.Seat == SeatId.Top ? 16f : -16f) * Mathf.Sin(windup * Mathf.PI);
            var arc = Mathf.Sin(travel * Mathf.PI) * motion.ArcHeight;
            var laneDrift = new Vector2(seatSide * Mathf.Sin(travel * Mathf.PI) * 34f, 0f);
            var snap = new Vector2(seatSide * Mathf.Sin(settle * Mathf.PI * 2f) * 5f, -Mathf.Sin(settle * Mathf.PI) * 4f);

            root.anchoredPosition = Vector2.Lerp(motion.StartPosition, motion.EndPosition, travelEase) + sourcePull + laneDrift + snap + Vector2.up * arc;
            root.sizeDelta = Vector2.Lerp(motion.StartSize, motion.EndSize, travelEase);

            var throwTilt = Quaternion.Euler(0f, 0f, seatSide * Mathf.Sin(travel * Mathf.PI) * 18f);
            var slamTilt = Quaternion.Euler(0f, 0f, -seatSide * settleEase * 7f);
            root.localRotation = Quaternion.Slerp(motion.StartRotation, motion.EndRotation, travelEase) * throwTilt * slamTilt;

            var lift = Mathf.Sin(travel * Mathf.PI) * 0.10f;
            var slamPunch = settleEase * 0.075f;
            var backFlipPulse = backShowing ? Mathf.Sin(progress * Mathf.PI) * 0.035f : 0f;
            root.localScale = Vector3.one * (1f + lift + slamPunch + backFlipPulse);
            ghost.CanvasGroup.alpha = Mathf.Lerp(0.88f, 1f, Mathf.Clamp01(progress / 0.2f));
        }

        private void ApplyTrickCollectCardPose(CardButtonView ghost, CardMotionSnapshot motion, float progress, bool bigBook)
        {
            if (!bigBook)
            {
                ApplyFloatingCardPose(ghost, motion, progress, fadeOutNearEnd: true, revealFromBack: false);
                return;
            }

            if (ghost == null || ghost.CanvasGroup == null)
            {
                return;
            }

            RectTransform root;
            try
            {
                root = ghost.transform as RectTransform;
            }
            catch (MissingReferenceException)
            {
                return;
            }

            if (root == null)
            {
                return;
            }

            var windup = Mathf.Clamp01(progress / 0.12f);
            var travel = Mathf.Clamp01((progress - 0.04f) / 0.72f);
            var slam = Mathf.Clamp01((progress - 0.72f) / 0.28f);
            var travelEase = 1f - Mathf.Pow(1f - travel, 4f);
            var slamEase = Mathf.Sin(slam * Mathf.PI);
            var arc = Mathf.Sin(travel * Mathf.PI) * motion.ArcHeight;
            var pullback = new Vector2(-motion.BurstOffset.x * 0.55f, 10f) * Mathf.Sin(windup * Mathf.PI);
            var pileSpread = motion.BurstOffset * slamEase;
            var tablePress = new Vector2(0f, -12f * slamEase);

            root.anchoredPosition = Vector2.Lerp(motion.StartPosition, motion.EndPosition, travelEase) + pullback + pileSpread + tablePress + Vector2.up * arc;
            root.sizeDelta = Vector2.Lerp(motion.StartSize, motion.EndSize, travelEase);

            var snapTilt = Quaternion.Euler(0f, 0f, Mathf.Sin(slam * Mathf.PI * 2f) * 5f);
            root.localRotation = Quaternion.Slerp(motion.StartRotation, motion.EndRotation, travelEase) * snapTilt;
            root.localScale = Vector3.one * (1f + Mathf.Sin(travel * Mathf.PI) * 0.04f + slamEase * 0.12f);
            ghost.CanvasGroup.alpha = Mathf.Lerp(1f, 0f, Mathf.Clamp01((progress - 0.76f) / 0.24f));
        }

        private void ApplyFloatingCardPose(CardButtonView ghost, CardMotionSnapshot motion, float progress, bool fadeOutNearEnd, bool revealFromBack)
        {
            if (ghost == null || ghost.CanvasGroup == null)
            {
                return;
            }

            RectTransform root;
            try
            {
                root = ghost.transform as RectTransform;
            }
            catch (MissingReferenceException)
            {
                return;
            }

            if (root == null)
            {
                return;
            }

            var eased = 1f - Mathf.Pow(1f - progress, 3f);
            var arc = Mathf.Sin(progress * Mathf.PI) * motion.ArcHeight;
            root.anchoredPosition = Vector2.Lerp(motion.StartPosition, motion.EndPosition, eased) + Vector2.up * arc;
            root.sizeDelta = Vector2.Lerp(motion.StartSize, motion.EndSize, eased);
            root.localRotation = Quaternion.Slerp(motion.StartRotation, motion.EndRotation, eased);
            root.localScale = revealFromBack
                ? Vector3.one * (1f + Mathf.Sin(progress * Mathf.PI) * 0.045f)
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
                seat == SeatId.Bottom ? 96f : 72f,
                Vector2.zero);
        }

        private CardMotionSnapshot BuildTrickCollectMotion(TrickPlay play, SeatId winner, int index, bool bigBook)
        {
            var trickRect = trickSlots[play.Seat].Root;
            var winnerRect = seatViews[winner].Root;
            var collectTilt = winner == SeatId.Left ? -10f : winner == SeatId.Right ? 10f : 0f;
            if (bigBook)
            {
                collectTilt += (index - 1.5f) * 3.2f;
            }

            return new CardMotionSnapshot(
                play.Card,
                play.Seat,
                GetAnchoredPoint(trickRect, new Vector2(0.5f, 0.5f)),
                GetAnchoredPoint(winnerRect, GetSeatInnerAnchor(winner)),
                GetAnchoredSize(trickRect),
                GetAnchoredSize(trickRect) * (bigBook ? 0.48f : 0.54f),
                Quaternion.identity,
                Quaternion.Euler(0f, 0f, collectTilt),
                bigBook ? 34f : 54f,
                bigBook ? new Vector2((index - 1.5f) * 13f, 5f - index * 3f) : Vector2.zero,
                index * (bigBook ? 0.018f : 0.04f));
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

        private static float GetOpeningDealFlightDelay(int sequenceIndex, float delayStep)
        {
            var wavePause = (sequenceIndex / 13) * OpeningDealWavePauseSeconds;
            var syncopation = Mathf.Sin(sequenceIndex * 1.618f) * 0.007f;
            return delayStep * sequenceIndex + wavePause + Mathf.Max(0f, syncopation);
        }

        private CardMotionSnapshot BuildOpeningDealMotion(
            SeatId seat,
            Card card,
            int index,
            int count,
            CanvasGroup revealTarget,
            int sequenceIndex,
            int sequenceCount)
        {
            var stackCenter = GetOpeningStackPosition();
            var startPosition = stackCenter + GetOpeningDealLaunchOffset(sequenceIndex, sequenceCount);
            var startSize = ResolveOpeningDealLaunchCardSize();
            var targetPose = ResolveOpeningDealTargetPose(seat, index, count, revealTarget);
            return new CardMotionSnapshot(
                card,
                seat,
                startPosition,
                targetPose.Position,
                startSize,
                targetPose.Size,
                GetOpeningDealLaunchRotation(sequenceIndex, sequenceCount),
                targetPose.Rotation,
                seat == SeatId.Bottom ? 78f : 58f,
                Vector2.zero);
        }

        private (Vector2 Position, Vector2 Size, Quaternion Rotation) ResolveOpeningDealTargetPose(SeatId seat, int index, int count, CanvasGroup revealTarget)
        {
            if (revealTarget != null && revealTarget.transform is RectTransform targetRect)
            {
                return (
                    GetAnchoredPoint(targetRect, new Vector2(0.5f, 0.5f)),
                    GetAnchoredSize(targetRect),
                    Quaternion.Euler(0f, 0f, targetRect.eulerAngles.z));
            }

            return seat == SeatId.Bottom
                ? (
                    GetHandAnimationPoint(index, count, false),
                    ResolveBottomHandCardSize(),
                    GetFanTargetRotation(index, count))
                : (
                    GetSeatDealPoint(seat, index, count),
                    ResolveOpponentHandCardSize(),
                    GetSeatFanTargetRotation(seat, index, count));
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

            var opponentCard = ResolveOpponentPlaySourceRect(seat);
            if (opponentCard != null)
            {
                return opponentCard;
            }

            return seatViews[seat].Root;
        }

        private RectTransform ResolveOpponentPlaySourceRect(SeatId seat)
        {
            if (seat == SeatId.Bottom || controller?.State?.RoundState?.HandsBySeat == null)
            {
                return null;
            }

            var targets = FindAuthoredOpeningDealSeatCards(seat);
            if (targets.Count == 0)
            {
                return null;
            }

            var remainingCount = controller.State.RoundState.HandsBySeat.TryGetValue(seat, out var hand)
                ? hand.Count
                : 0;
            var sourceIndex = Mathf.Clamp(remainingCount, 0, targets.Count - 1);
            return targets[sourceIndex] != null
                ? targets[sourceIndex].transform as RectTransform
                : null;
        }

        private CardButtonView CreateFloatingCard(CardMotionSnapshot motion)
        {
            var root = AnimationRoot;
            root.gameObject.SetActive(true);
            root.SetAsLastSibling();
            var ghost = Instantiate(sceneRefs.CardButtonPrefab, root);
            ghost.gameObject.name = $"Motion {motion.Card.ShortLabel}";
            ghost.gameObject.SetActive(true);
            ghost.transform.SetAsLastSibling();
            ghost.Button.onClick.RemoveAllListeners();
            ghost.Button.enabled = false;
            ghost.CanvasGroup = ghost.CanvasGroup != null ? ghost.CanvasGroup : ResolveCanvasGroup(ghost.gameObject);
            ghost.CanvasGroup.blocksRaycasts = false;
            ghost.CanvasGroup.interactable = false;
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
            EnsureCardFaceImage(view);
            view.RankText.font = theme.ResolveFont();
            view.SuitText.font = theme.ResolveFont();
            view.RankText.text = card.RankLabel;
            view.SuitText.text = card.SuitIcon;
            view.Panel.sprite = theme.cardFaceDefaultSprite != null ? theme.cardFaceDefaultSprite : ResolveCardSprite(false, true);
            view.Panel.type = Image.Type.Sliced;
            view.Panel.color = Color.white;
            if (!TryApplyImportedCardFace(view, card, true))
            {
                HideFaceImage(view.FaceImage);
                view.RankText.color = new Color(0.08f, 0.08f, 0.08f, 1f);
                view.SuitText.color = card.IsRed ? theme.red : new Color(0.07f, 0.07f, 0.08f, 1f);
            }
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

        private void SpawnCardPlayFx(CardMotionSnapshot motion)
        {
            if (!trickSlots.TryGetValue(motion.Seat, out var slot) || slot?.Root == null)
            {
                return;
            }

            var anchoredPosition = GetAnchoredPoint(slot.Root, new Vector2(0.5f, 0.5f));
            var card = motion.Card;
            var important = IsImportantCard(card);
            var spawnedPrefab = SpawnStrongCardFx(motion, anchoredPosition);
            if (important)
            {
                if (!spawnedPrefab)
                {
                    SpawnEpicToonFx(cardImpactFxPrefab, anchoredPosition + new Vector2(0f, 14f), 0.82f);
                }

                return;
            }

            SpawnEpicToonFx(cardImpactFxPrefab, anchoredPosition, 0.58f);
        }

        private void TrySpawnTrashTalkPopup(CardMotionSnapshot motion, string forcedTag, float chance)
        {
            if (!trickSlots.TryGetValue(motion.Seat, out var slot) || slot?.Root == null)
            {
                return;
            }

            var point = GetAnchoredPoint(slot.Root, new Vector2(0.5f, 0.68f));
            TrySpawnTrashTalkPopup(point, forcedTag, chance, ResolveTrashTalkColor(motion.Card));
        }

        private void TrySpawnTrashTalkPopup(Vector2 anchoredPosition, string forcedTag, float chance, Color color)
        {
            if (Time.unscaledTime < nextTrashTalkPopupTime || Random.value > chance)
            {
                return;
            }

            var tag = string.IsNullOrEmpty(forcedTag)
                ? TrashTalkTags[Random.Range(0, TrashTalkTags.Length)]
                : forcedTag;
            nextTrashTalkPopupTime = Time.unscaledTime + Random.Range(2.8f, 4.8f);
            PlayFeedback(FeedbackCue.Select, 0.055f);
            StartCoroutine(TrashTalkPopupRoutine(anchoredPosition, tag, color));
        }

        private IEnumerator TrashTalkPopupRoutine(Vector2 anchoredPosition, string tag, Color color)
        {
            var root = AnimationRoot;
            if (root == null)
            {
                yield break;
            }

            var go = new GameObject("Trash Talk Tag Runtime", typeof(RectTransform), typeof(Text), typeof(CanvasGroup), typeof(Outline), typeof(Shadow));
            go.transform.SetParent(root, false);
            go.transform.SetAsLastSibling();

            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(430f, 118f);
            var startPosition = anchoredPosition + new Vector2(Random.Range(-26f, 26f), Random.Range(18f, 34f));
            rect.anchoredPosition = startPosition;
            rect.localScale = Vector3.one * 0.18f;
            var startAngle = Random.Range(-12f, 12f);
            rect.localRotation = Quaternion.Euler(0f, 0f, startAngle);

            var text = go.GetComponent<Text>();
            text.font = theme.ResolveFont();
            text.text = tag;
            text.fontSize = tag.Length > 8 ? 48 : 58;
            text.fontStyle = FontStyle.BoldAndItalic;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.color = color;
            text.raycastTarget = false;

            var outline = go.GetComponent<Outline>();
            outline.effectColor = new Color(0.015f, 0.015f, 0.016f, 0.96f);
            outline.effectDistance = new Vector2(4.4f, -4.4f);
            var shadow = go.GetComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.46f);
            shadow.effectDistance = new Vector2(9f, -9f);

            var group = go.GetComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;
            group.alpha = 0f;

            transientFx.Add(text);
            var introDuration = 0.34f;
            var holdDuration = 0.82f;
            var outroDuration = 0.42f;
            var duration = introDuration + holdDuration + outroDuration;
            var elapsed = 0f;
            var holdPosition = startPosition + new Vector2(Random.Range(-8f, 8f), 18f);
            var exitPosition = holdPosition + new Vector2(Random.Range(-22f, 22f), 48f);
            var holdAngle = startAngle * 0.4f;
            var exitAngle = holdAngle + Random.Range(-10f, 10f);
            while (elapsed < duration && rect != null)
            {
                elapsed += Time.unscaledDeltaTime;
                if (elapsed < introDuration)
                {
                    var t = Mathf.Clamp01(elapsed / introDuration);
                    var snap = EaseOutBack(t);
                    var wobble = Mathf.Sin(t * Mathf.PI * 4f) * (1f - t) * 6f;
                    rect.localScale = Vector3.one * Mathf.LerpUnclamped(0.18f, 1.28f, snap);
                    rect.anchoredPosition = Vector2.Lerp(startPosition - new Vector2(0f, 18f), holdPosition, 1f - Mathf.Pow(1f - t, 3f));
                    rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(startAngle * 2.4f, holdAngle, snap) + wobble);
                    group.alpha = Mathf.Clamp01(t * 3.2f);
                }
                else if (elapsed < introDuration + holdDuration)
                {
                    var t = Mathf.Clamp01((elapsed - introDuration) / holdDuration);
                    var breathe = Mathf.Sin(t * Mathf.PI * 2f) * 0.035f;
                    var swagger = Mathf.Sin(t * Mathf.PI * 3f) * 2.2f;
                    rect.localScale = Vector3.one * (1.12f + breathe);
                    rect.anchoredPosition = holdPosition + new Vector2(Mathf.Sin(t * Mathf.PI * 2f) * 4f, Mathf.Sin(t * Mathf.PI) * 8f);
                    rect.localRotation = Quaternion.Euler(0f, 0f, holdAngle + swagger);
                    group.alpha = 1f;
                }
                else
                {
                    var t = Mathf.Clamp01((elapsed - introDuration - holdDuration) / outroDuration);
                    var ease = 1f - Mathf.Pow(1f - t, 2f);
                    rect.localScale = Vector3.one * Mathf.Lerp(1.12f, 0.58f, ease);
                    rect.anchoredPosition = Vector2.Lerp(holdPosition, exitPosition, ease);
                    rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(holdAngle, exitAngle, ease));
                    group.alpha = 1f - ease;
                }

                yield return null;
            }

            transientFx.Remove(text);
            if (go != null)
            {
                Destroy(go);
            }
        }

        private Color ResolveTrashTalkColor(Card card)
        {
            if (card.Suit == Suit.Spades)
            {
                return theme.gold;
            }

            if (card.Rank >= 13)
            {
                return theme.green;
            }

            return card.IsRed ? theme.red : theme.primaryText;
        }

        private bool SpawnStrongCardFx(CardMotionSnapshot motion, Vector2 anchoredPosition)
        {
            var prefab = ResolveStrongCardFxPrefab(motion.Card);
            return SpawnEpicToonFx(prefab, anchoredPosition, 0.78f);
        }

        private void SpawnSpadesBrokenMomentFx(CardMotionSnapshot motion)
        {
            if (!trickSlots.TryGetValue(motion.Seat, out var slot) || slot?.Root == null)
            {
                return;
            }

            var anchoredPosition = GetAnchoredPoint(slot.Root, new Vector2(0.5f, 0.5f));
            SpawnEpicToonFx(spadesBrokenSmokeFxPrefab != null ? spadesBrokenSmokeFxPrefab : cardSmokeFxPrefab, anchoredPosition + new Vector2(0f, -4f), 1.16f);
            SpawnEpicToonFx(spadesBrokenLightningFxPrefab != null ? spadesBrokenLightningFxPrefab : highSpadeFxPrefab, anchoredPosition + new Vector2(0f, 16f), 0.86f);
            StartCoroutine(SpadesBrokenFlashRoutine());
        }

        private IEnumerator SpadesBrokenFlashRoutine()
        {
            var root = AnimationRoot;
            if (root == null)
            {
                yield break;
            }

            var flashObject = new GameObject("Spades Broken Lightning Flicker", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            flashObject.transform.SetParent(root, false);
            flashObject.transform.SetAsLastSibling();
            var rect = (RectTransform)flashObject.transform;
            SetAnchors(rect, Vector2.zero, Vector2.one);
            var image = flashObject.GetComponent<Image>();
            image.sprite = ResolveSoftPanelSprite();
            image.type = Image.Type.Simple;
            image.color = new Color(0.52f, 0.78f, 1f, 1f);
            image.raycastTarget = false;
            var group = flashObject.GetComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;

            var duration = 0.22f;
            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                var strobe = Mathf.Abs(Mathf.Sin(t * Mathf.PI * 5.5f));
                group.alpha = strobe * (1f - t) * 0.34f;
                yield return null;
            }

            Destroy(flashObject);
        }

        private bool SpawnEpicToonFx(GameObject prefab, Vector2 anchoredPosition, float scaleMultiplier)
        {
            if (prefab == null)
            {
                return false;
            }

            var root = AnimationRoot;
            var effect = Instantiate(prefab, root);
            effect.name = $"{prefab.name} Runtime";
            effect.transform.SetAsLastSibling();
            effect.transform.localPosition = new Vector3(anchoredPosition.x, anchoredPosition.y, -12f);
            effect.transform.localRotation = prefab.transform.localRotation;
            var rootScale = Mathf.Max(0.0001f, root.lossyScale.x);
            effect.transform.localScale = Vector3.one * (Mathf.Max(0.05f, scaleMultiplier) / rootScale);

            var particles = effect.GetComponentsInChildren<ParticleSystem>(true);
            foreach (var particle in particles)
            {
                particle.Clear(true);
                particle.Play(true);
            }

            activeRuntimeParticleFx.RemoveAll(fx => fx == null);
            activeRuntimeParticleFx.Add(effect);
            Destroy(effect, ResolveStrongCardFxLifetime(particles));
            return true;
        }

        private void TrySpawnBookCollectSmoke(Vector2 discardPoint, bool bigBook)
        {
            var chance = bigBook ? BigBookCollectSmokeChance : BookCollectSmokeChance;
            if (Random.value > chance)
            {
                return;
            }

            SpawnEpicToonFx(cardSmokeFxPrefab, discardPoint, bigBook ? 0.88f : 0.72f);
        }

        private GameObject ResolveStrongCardFxPrefab(Card card)
        {
            if (card.Rank == 14)
            {
                return aceCardFxPrefab;
            }

            if (card.Rank == 13)
            {
                return kingCardFxPrefab;
            }

            if (card.Rank == 12)
            {
                return queenCardFxPrefab;
            }

            if (card.Rank == 11)
            {
                return jackCardFxPrefab;
            }

            return card.Suit == Suit.Spades && card.Rank >= 10
                ? highSpadeFxPrefab
                : null;
        }

        private static bool IsImportantCard(Card card)
        {
            return card.Rank >= 12 || (card.Suit == Suit.Spades && card.Rank >= 10);
        }

        private static bool IsTrashTalkStrongMoveCard(Card card)
        {
            return IsImportantCard(card) || card.Rank == 11;
        }

        private static bool IsBigBook(IReadOnlyList<TrickPlay> trick, SeatId winner)
        {
            if (trick == null || trick.Count == 0)
            {
                return false;
            }

            var winningPlay = trick.FirstOrDefault(play => play != null && play.Seat == winner);
            if (winningPlay != null &&
                (winningPlay.Card.Rank >= 13 || (winningPlay.Card.Suit == Suit.Spades && winningPlay.Card.Rank >= 10)))
            {
                return true;
            }

            return trick.Count(play => play != null && IsImportantCard(play.Card)) >= 2;
        }

        private Color ResolveCardFxColor(Card card)
        {
            if (card.Suit == Suit.Spades)
            {
                return theme.gold;
            }

            if (card.Rank >= 13)
            {
                return theme.red;
            }

            return card.IsRed ? theme.red : theme.primaryText;
        }

        private static float ResolveStrongCardFxLifetime(IReadOnlyCollection<ParticleSystem> particles)
        {
            if (particles == null || particles.Count == 0)
            {
                return 2.5f;
            }

            var lifetime = 0f;
            foreach (var particle in particles)
            {
                var main = particle.main;
                lifetime = Mathf.Max(lifetime, main.duration + main.startLifetime.constantMax);
            }

            return Mathf.Clamp(lifetime + 0.35f, 1.25f, 5f);
        }

        private void ClearOpeningDealRuntimeSeatCards()
        {
            foreach (var view in openingDealRuntimeSeatCards.ToArray())
            {
                if (view != null)
                {
                    Destroy(view.gameObject);
                }
            }

            openingDealRuntimeSeatCards.Clear();
        }

        private void ClearAnimationRootCards()
        {
            var root = runtimeAnimationRoot;
            if (root == null)
            {
                return;
            }

            var cards = root.GetComponentsInChildren<CardButtonView>(true);
            foreach (var card in cards)
            {
                if (card == null)
                {
                    continue;
                }

                floatingCards.Remove(card);
                openingStackPreviewCards.Remove(card);
                openingDealRuntimeSeatCards.Remove(card);
                if (openingStackPreviewAnimations.TryGetValue(card, out var routine) && routine != null)
                {
                    StopCoroutine(routine);
                }

                openingStackPreviewAnimations.Remove(card);
                Destroy(card.gameObject);
            }

        }

        private IEnumerator AnimateAnimationRootCardsOut()
        {
            var root = runtimeAnimationRoot;
            if (root == null)
            {
                yield break;
            }

            var cards = root.GetComponentsInChildren<CardButtonView>(true)
                .Where(card => card != null)
                .ToList();
            if (cards.Count == 0)
            {
                yield break;
            }

            foreach (var card in cards)
            {
                if (openingStackPreviewAnimations.TryGetValue(card, out var routine) && routine != null)
                {
                    StopCoroutine(routine);
                }

                openingStackPreviewAnimations.Remove(card);
                card.gameObject.SetActive(true);
                card.transform.SetAsLastSibling();
                card.CanvasGroup = card.CanvasGroup != null ? card.CanvasGroup : ResolveCanvasGroup(card.gameObject);
                card.CanvasGroup.alpha = 1f;
                card.CanvasGroup.blocksRaycasts = false;
            }

            var starts = cards.ToDictionary(card => card, card => card.Root.anchoredPosition);
            var sizes = cards.ToDictionary(card => card, card => card.Root.sizeDelta);
            var rotations = cards.ToDictionary(card => card, card => card.Root.localRotation);
            var rootHeight = Mathf.Max(640f, root.rect.height);
            var rootWidth = Mathf.Max(360f, root.rect.width);
            var duration = 0.42f;
            var elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                var eased = EaseInOutCubic(t);
                for (var index = 0; index < cards.Count; index++)
                {
                    var card = cards[index];
                    if (card == null)
                    {
                        continue;
                    }

                    var side = index % 2 == 0 ? -1f : 1f;
                    var target = new Vector2(
                        side * (rootWidth * 0.75f + index * 3f),
                        rootHeight * 0.68f + index * 5f);
                    var arc = Vector2.up * Mathf.Sin(t * Mathf.PI) * 58f;
                    card.Root.anchoredPosition = Vector2.Lerp(starts[card], target, eased) + arc;
                    card.Root.sizeDelta = Vector2.Lerp(sizes[card], sizes[card] * 0.72f, EaseOutCubic(t));
                    card.Root.localRotation = Quaternion.Slerp(rotations[card], Quaternion.Euler(0f, 0f, side * 24f), eased);
                    card.Root.localScale = Vector3.one * Mathf.Lerp(1f, 0.82f, eased);
                    card.CanvasGroup.alpha = Mathf.Lerp(1f, 0f, Mathf.Clamp01((t - 0.55f) / 0.45f));
                }

                yield return null;
            }

            ClearAnimationRootCards();
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
            ClearRuntimeParticleFx();
        }

        private void ClearRuntimeParticleFx()
        {
            foreach (var fx in activeRuntimeParticleFx.ToArray())
            {
                if (fx == null)
                {
                    continue;
                }

                foreach (var particle in fx.GetComponentsInChildren<ParticleSystem>(true))
                {
                    particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    particle.Clear(true);
                }

                fx.SetActive(false);
                Destroy(fx);
            }

            activeRuntimeParticleFx.Clear();
        }

        private void RefreshBookLeaderLightningFx()
        {
            var leader = ResolveBookLightningLeader();
            if (!leader.HasValue ||
                !seatAvatarImages.TryGetValue(leader.Value, out var avatarImage) ||
                avatarImage == null)
            {
                DisableBookLightningFxExcept(null);
                return;
            }

            DisableBookLightningFxExcept(leader.Value);
            var lightning = EnsureAvatarBookLightningFx(leader.Value, avatarImage);
            if (lightning == null)
            {
                return;
            }

            EnableBookLightningFx(lightning);
        }

        private Component EnsureAvatarBookLightningFx(SeatId seat, Image avatarImage)
        {
            if (avatarBookLightningFx.TryGetValue(seat, out var existing) && existing != null)
            {
                return existing;
            }

            var lightningOwner = ResolveAvatarBorderObject(seat, avatarImage) ?? avatarImage.gameObject;
            var lightning = FindComponentByTypeName(lightningOwner, "_2dxFX_LightningBolt") ??
                            FindComponentByTypeName(avatarImage.gameObject, "_2dxFX_LightningBolt");
            if (lightning == null)
            {
                var lightningType = Resolve2DxFxType("_2dxFX_LightningBolt");
                if (lightningType == null)
                {
                    return null;
                }

                lightning = lightningOwner.AddComponent(lightningType);
            }

            avatarBookLightningFx[seat] = lightning;
            return lightning;
        }

        private GameObject ResolveAvatarBorderObject(SeatId seat, Image avatarImage)
        {
            return seatViews.TryGetValue(seat, out var view)
                ? ResolveAvatarBorderObject(view, avatarImage)
                : ResolveAvatarBorderObject(null, avatarImage);
        }

        private static GameObject ResolveAvatarBorderObject(SeatPanelView view, Image avatarImage)
        {
            var root = view != null ? view.Root : avatarImage != null ? avatarImage.transform.parent : null;
            var border = root != null ? root.Find("Avatar Border") : null;
            if (border != null)
            {
                return border.gameObject;
            }

            border = avatarImage != null && avatarImage.transform.parent != null
                ? avatarImage.transform.parent.Find("Avatar Border")
                : null;
            return border != null ? border.gameObject : null;
        }

        private SeatId? ResolveBookLightningLeader()
        {
            var tricksBySeat = controller?.State?.RoundState?.TricksWonBySeat;
            if (tricksBySeat == null || tricksBySeat.Count == 0)
            {
                return null;
            }

            SeatId? leader = null;
            var leaderBooks = int.MinValue;
            var tiedForLead = false;
            foreach (var seat in SpadesSeatUtility.TurnOrder)
            {
                var books = tricksBySeat.TryGetValue(seat, out var count) ? count : 0;
                if (books > leaderBooks)
                {
                    leaderBooks = books;
                    leader = seat;
                    tiedForLead = false;
                }
                else if (books == leaderBooks)
                {
                    tiedForLead = true;
                }
            }

            if (!leader.HasValue || tiedForLead || leaderBooks <= 0)
            {
                return null;
            }

            return leader;
        }

        private static void EnableBookLightningFx(Component lightning)
        {
            if (lightning == null)
            {
                return;
            }

            if (lightning is Behaviour behaviour)
            {
                behaviour.enabled = true;
            }

            CallFxUpdate(lightning);
        }

        private void DisableBookLightningFxExcept(SeatId? seatToKeep)
        {
            foreach (var pair in avatarBookLightningFx.ToArray())
            {
                var fx = pair.Value;
                if (fx == null)
                {
                    avatarBookLightningFx.Remove(pair.Key);
                    continue;
                }

                if (seatToKeep.HasValue && pair.Key == seatToKeep.Value)
                {
                    continue;
                }

                if (fx is Behaviour behaviour)
                {
                    behaviour.enabled = false;
                }
            }
        }

        private void SetLatestBookAura(SeatId winner)
        {
            foreach (var pair in seatAuraObjects)
            {
                if (pair.Value != null)
                {
                    pair.Value.SetActive(pair.Key == winner);
                }
            }
        }

        private void ClearLatestBookAura()
        {
            foreach (var aura in seatAuraObjects.Values)
            {
                if (aura != null)
                {
                    aura.SetActive(false);
                }
            }
        }

        private static System.Type Resolve2DxFxType(string typeName)
        {
            return System.AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(typeName))
                .FirstOrDefault(type => type != null);
        }

        private static Component FindComponentByTypeName(GameObject target, string typeName)
        {
            return target == null
                ? null
                : target.GetComponents<Component>()
                    .FirstOrDefault(component => component != null && component.GetType().Name == typeName);
        }

        private static void SetFxBool(Component component, string fieldName, bool value)
        {
            component?.GetType()
                .GetField(fieldName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)
                ?.SetValue(component, value);
        }

        private static void SetFxFloat(Component component, string fieldName, float value)
        {
            component?.GetType()
                .GetField(fieldName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)
                ?.SetValue(component, value);
        }

        private static void CallFxUpdate(Component component)
        {
            component?.GetType()
                .GetMethod("CallUpdate", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)
                ?.Invoke(component, null);
        }

        private void ClearBookLightningFx()
        {
            DisableBookLightningFxExcept(null);
        }

        private void ClearPlayerScoreOverlayFx()
        {
            ClearLatestBookAura();
            ClearBookLightningFx();
            ClearTransientFx();
        }

        private void StartBidCameraFocus(SeatId seat)
        {
            var targetCamera = Camera.main ?? FindFirstObjectByType<Camera>();
            if (targetCamera == null)
            {
                return;
            }

            CaptureBidCameraDefaults(targetCamera);
            if (bidCameraFocusLoop != null)
            {
                StopCoroutine(bidCameraFocusLoop);
            }

            var side = GetBidCameraFocusDirection(seat);
            var travel = targetCamera.orthographic
                ? Mathf.Max(0.62f, bidFocusDefaultOrthographicSize * 0.28f)
                : 0.72f;
            var targetPosition = bidFocusDefaultPosition + new Vector3(side.x * travel, side.y * travel * 0.86f, 0f);
            var targetOrthographicSize = targetCamera.orthographic
                ? bidFocusDefaultOrthographicSize * 0.85f
                : bidFocusDefaultOrthographicSize;
            var targetFieldOfView = targetCamera.orthographic
                ? bidFocusDefaultFieldOfView
                : bidFocusDefaultFieldOfView * 0.88f;

            bidCameraFocusLoop = StartCoroutine(BidCameraFocusRoutine(
                targetCamera,
                targetPosition,
                bidFocusDefaultRotation,
                targetOrthographicSize,
                targetFieldOfView,
                1.08f));
        }

        private void RestoreBidCameraFocus(bool immediate = false)
        {
            if (!bidFocusDefaultsCaptured || bidFocusCamera == null)
            {
                return;
            }

            if (bidCameraFocusLoop != null)
            {
                StopCoroutine(bidCameraFocusLoop);
                bidCameraFocusLoop = null;
            }

            if (immediate)
            {
                ApplyBidCameraState(bidFocusCamera, bidFocusDefaultPosition, bidFocusDefaultRotation, bidFocusDefaultOrthographicSize, bidFocusDefaultFieldOfView);
                bidFocusDefaultsCaptured = false;
                bidFocusCamera = null;
                return;
            }

            bidCameraFocusLoop = StartCoroutine(BidCameraFocusRoutine(
                bidFocusCamera,
                bidFocusDefaultPosition,
                bidFocusDefaultRotation,
                bidFocusDefaultOrthographicSize,
                bidFocusDefaultFieldOfView,
                0.85f,
                clearDefaultsOnComplete: true));
        }

        private void CaptureBidCameraDefaults(Camera targetCamera)
        {
            if (bidFocusDefaultsCaptured && bidFocusCamera == targetCamera)
            {
                return;
            }

            bidFocusCamera = targetCamera;
            bidFocusDefaultPosition = targetCamera.transform.localPosition;
            bidFocusDefaultRotation = targetCamera.transform.localRotation;
            bidFocusDefaultOrthographicSize = targetCamera.orthographicSize;
            bidFocusDefaultFieldOfView = targetCamera.fieldOfView;
            bidFocusDefaultsCaptured = true;
        }

        private IEnumerator BidCameraFocusRoutine(
            Camera targetCamera,
            Vector3 targetPosition,
            Quaternion targetRotation,
            float targetOrthographicSize,
            float targetFieldOfView,
            float duration,
            bool clearDefaultsOnComplete = false)
        {
            if (targetCamera == null)
            {
                yield break;
            }

            var transform = targetCamera.transform;
            var startPosition = transform.localPosition;
            var startRotation = transform.localRotation;
            var startOrthographicSize = targetCamera.orthographicSize;
            var startFieldOfView = targetCamera.fieldOfView;
            var elapsed = 0f;
            duration = Mathf.Max(0.05f, duration);
            while (elapsed < duration && targetCamera != null)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                var eased = EaseInOutCubic(t);
                var floatDrift = Mathf.Sin(t * Mathf.PI) * 0.035f;
                transform.localPosition = Vector3.Lerp(startPosition, targetPosition, eased) + transform.up * floatDrift;
                transform.localRotation = Quaternion.Slerp(startRotation, targetRotation, eased);
                if (targetCamera.orthographic)
                {
                    targetCamera.orthographicSize = Mathf.Lerp(startOrthographicSize, targetOrthographicSize, eased);
                }
                else
                {
                    targetCamera.fieldOfView = Mathf.Lerp(startFieldOfView, targetFieldOfView, eased);
                }

                yield return null;
            }

            if (targetCamera != null)
            {
                ApplyBidCameraState(targetCamera, targetPosition, targetRotation, targetOrthographicSize, targetFieldOfView);
            }

            bidCameraFocusLoop = null;
            if (clearDefaultsOnComplete)
            {
                bidFocusDefaultsCaptured = false;
                bidFocusCamera = null;
            }
        }

        private static void ApplyBidCameraState(Camera targetCamera, Vector3 position, Quaternion rotation, float orthographicSize, float fieldOfView)
        {
            if (targetCamera == null)
            {
                return;
            }

            targetCamera.transform.localPosition = position;
            targetCamera.transform.localRotation = rotation;
            if (targetCamera.orthographic)
            {
                targetCamera.orthographicSize = orthographicSize;
            }
            else
            {
                targetCamera.fieldOfView = fieldOfView;
            }
        }

        private static Vector2 GetBidCameraFocusDirection(SeatId seat)
        {
            return seat switch
            {
                SeatId.Left => new Vector2(-1f, 0.08f),
                SeatId.Right => new Vector2(1f, 0.08f),
                SeatId.Top => new Vector2(0f, 0.88f),
                SeatId.Bottom => new Vector2(0f, -0.76f),
                _ => Vector2.zero
            };
        }

        private void StartBookCameraShake(SeatId focusSeat, float intensity = 1f)
        {
            if (bookCameraShakeLoop != null)
            {
                RestoreBookCameraShakeTarget();
                StopCoroutine(bookCameraShakeLoop);
                bookCameraShakeLoop = null;
            }

            var targetCamera = Camera.main ?? FindFirstObjectByType<Camera>();
            var target = targetCamera != null
                ? targetCamera.transform
                : AnimationRoot;
            if (target == null)
            {
                return;
            }

            bookCameraShakeTarget = target;
            bookCameraShakeStartPosition = target.localPosition;
            bookCameraShakeStartRotation = target.localRotation;
            bookCameraShakeCamera = targetCamera;
            if (bookCameraShakeCamera != null)
            {
                bookCameraShakeStartOrthographicSize = bookCameraShakeCamera.orthographicSize;
                bookCameraShakeStartFieldOfView = bookCameraShakeCamera.fieldOfView;
            }

            bookCameraShakeLoop = StartCoroutine(BookCameraShakeRoutine(target, GetBidCameraFocusDirection(focusSeat), Mathf.Max(0.2f, intensity)));
        }

        private IEnumerator BookCameraShakeRoutine(Transform target, Vector2 focusDirection, float intensity)
        {
            var shakePower = Mathf.Clamp(intensity, 0.2f, 1.8f);
            var shake01 = Mathf.InverseLerp(0.2f, 1.8f, shakePower);
            var duration = Mathf.Max(0.28f, theme.shakeDuration * Mathf.Lerp(1.25f, 2.05f, shake01));
            var punchTravel = bookCameraShakeCamera != null && bookCameraShakeCamera.orthographic
                ? Mathf.Max(0.13f, bookCameraShakeStartOrthographicSize * 0.065f) * shakePower
                : 0.26f * shakePower;
            var punchOffset = new Vector3(focusDirection.x * punchTravel, focusDirection.y * punchTravel * 0.72f, 0f);
            var punchOrthographicSize = bookCameraShakeCamera != null && bookCameraShakeCamera.orthographic
                ? bookCameraShakeStartOrthographicSize * Mathf.Lerp(0.975f, 0.925f, shake01)
                : bookCameraShakeStartOrthographicSize;
            var punchFieldOfView = bookCameraShakeCamera != null && !bookCameraShakeCamera.orthographic
                ? bookCameraShakeStartFieldOfView * Mathf.Lerp(0.975f, 0.935f, shake01)
                : bookCameraShakeStartFieldOfView;
            var elapsed = 0f;
            while (elapsed < duration && target != null)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                var primaryPunch = Mathf.Sin(Mathf.Clamp01(t / 0.58f) * Mathf.PI);
                var afterPunch = Mathf.Sin(Mathf.Clamp01((t - 0.32f) / 0.68f) * Mathf.PI) * 0.14f;
                var punch = Mathf.Clamp01(primaryPunch + afterPunch);
                var falloff = 1f - EaseOutCubic(t);
                var hit = Mathf.Exp(-12f * t);
                var rumble = Mathf.Sin(t * Mathf.PI) * 0.22f;
                var x = (Mathf.Sin(elapsed * 128f) * 0.12f + Mathf.Sin(elapsed * 43f) * 0.05f) * falloff * shakePower;
                var y = (Mathf.Cos(elapsed * 106f) * 0.09f + Mathf.Sin(elapsed * 57f) * 0.04f) * falloff * shakePower;
                var roll = (Mathf.Sin(elapsed * 152f) * 0.95f + Mathf.Sin(elapsed * 31f) * 0.32f) * falloff * shakePower;
                x += Mathf.Sin(elapsed * 235f) * 0.08f * hit * shakePower;
                y += Mathf.Cos(elapsed * 225f) * 0.065f * hit * shakePower;
                roll += Mathf.Sin(elapsed * 275f) * 0.72f * hit * shakePower;
                x += focusDirection.x * rumble * 0.08f * shakePower;
                y += focusDirection.y * rumble * 0.055f * shakePower;
                target.localPosition = bookCameraShakeStartPosition + punchOffset * punch + new Vector3(x, y, 0f);
                target.localRotation = bookCameraShakeStartRotation * Quaternion.Euler(0f, 0f, roll);
                if (bookCameraShakeCamera != null)
                {
                    if (bookCameraShakeCamera.orthographic)
                    {
                        bookCameraShakeCamera.orthographicSize = Mathf.Lerp(bookCameraShakeStartOrthographicSize, punchOrthographicSize, punch);
                    }
                    else
                    {
                        bookCameraShakeCamera.fieldOfView = Mathf.Lerp(bookCameraShakeStartFieldOfView, punchFieldOfView, punch);
                    }
                }

                yield return null;
            }

            RestoreBookCameraShakeTarget();
            bookCameraShakeLoop = null;
        }

        private void RestoreBookCameraShakeTarget()
        {
            if (bookCameraShakeTarget == null)
            {
                return;
            }

            bookCameraShakeTarget.localPosition = bookCameraShakeStartPosition;
            bookCameraShakeTarget.localRotation = bookCameraShakeStartRotation;
            if (bookCameraShakeCamera != null)
            {
                if (bookCameraShakeCamera.orthographic)
                {
                    bookCameraShakeCamera.orthographicSize = bookCameraShakeStartOrthographicSize;
                }
                else
                {
                    bookCameraShakeCamera.fieldOfView = bookCameraShakeStartFieldOfView;
                }
            }

            bookCameraShakeTarget = null;
            bookCameraShakeCamera = null;
        }

        private void ClearTransientMotionState(bool stopQueue)
        {
            hiddenTrickSlots.Clear();
            resolvedTrickCards.Clear();
            pendingSetBookMoments.Clear();
            pendingRoundSheetOpen = false;
            pendingEndSheetOpen = false;
            setBookMomentRunning = false;
            spadesBrokenMomentShown = false;
            nextTrashTalkPopupTime = 0f;
            nextCrowdReactionTime = 0f;
            ResetBookStreaks();
            ClearBookTextAnimations();
            StopOpeningStackIntro();
            ClearOpeningStackPreviewCards();
            ClearFloatingCards();
            ClearOpeningDealRuntimeSeatCards();
            ClearTransientFx();
            ClearBookLightningFx();
            ClearLatestBookAura();
            if (bookCameraShakeLoop != null)
            {
                StopCoroutine(bookCameraShakeLoop);
                bookCameraShakeLoop = null;
            }

            RestoreBookCameraShakeTarget();
            RestoreBidCameraFocus(immediate: true);
            HideAllBidBubbles(true);
            if (deferredSheetStateLoop != null)
            {
                StopCoroutine(deferredSheetStateLoop);
                deferredSheetStateLoop = null;
            }

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

        private void StartOpeningStackIntro()
        {
            StopOpeningStackIntro();
            ClearOpeningStackPreviewCards();
            if (!openingDealPending || controller?.State?.RoundState == null || sceneRefs.CardButtonPrefab == null)
            {
                openingStackIntroRunning = false;
                RenderAll();
                return;
            }

            openingStackIntroLoop = StartCoroutine(OpeningStackIntroSequence());
        }

        private void StopOpeningStackIntro()
        {
            if (openingStackIntroLoop != null)
            {
                StopCoroutine(openingStackIntroLoop);
                openingStackIntroLoop = null;
            }

            openingStackIntroRunning = false;
        }

        private IEnumerator OpeningStackIntroSequence()
        {
            openingStackIntroRunning = true;
            SetStartupCardGroupsVisible(false);
            RefreshOpeningStackEffectVisual();
            RenderAll();

            var totalCards = ResolveOpeningStackCardCount();
            var delayStep = OpeningDeckIntroDelayStep;
            var baseTravelDuration = Mathf.Max(OpeningDeckIntroDuration, theme.modalDuration * 0.72f);
            var previewSize = ResolveOpeningStackPreviewCardSize();
            var maxTravelDuration = 0f;
            if (openingStackEffectImage != null)
            {
                openingStackEffectImage.gameObject.SetActive(false);
            }

            yield return StartCoroutine(OpeningGatherToCenterSequence(totalCards, previewSize));

            for (var index = 0; index < totalCards; index++)
            {
                var travelDuration = baseTravelDuration + Mathf.Lerp(0f, 0.08f, GetOpeningStackPreviewRandom01(index + 13));
                maxTravelDuration = Mathf.Max(maxTravelDuration, travelDuration);
                var preview = index < openingStackPreviewCards.Count && openingStackPreviewCards[index] != null
                    ? openingStackPreviewCards[index]
                    : CreateOpeningStackPreviewCard(index, totalCards, previewSize);
                openingStackPreviewAnimations[preview] = StartCoroutine(AnimateOpeningStackPreviewCard(preview, index, totalCards, travelDuration, delayStep * index));
            }

            PlayFeedback(FeedbackCue.Collect, 0.14f);
            yield return new WaitForSecondsRealtime(delayStep * Mathf.Max(0, totalCards - 1) + maxTravelDuration);
            yield return StartCoroutine(OpeningStackShuffleFlourish());
            yield return new WaitForSecondsRealtime(OpeningDeckSettleSeconds);

            openingStackIntroRunning = false;
            openingStackIntroLoop = null;
            RenderAll();
            OnDealPressed();
        }

        private IEnumerator OpeningGatherToCenterSequence(int totalCards, Vector2 previewSize)
        {
            if (sceneRefs.CardButtonPrefab == null || totalCards <= 0)
            {
                yield break;
            }

            PlayFeedback(FeedbackCue.Collect, 0.14f);

            var maxDelay = 0f;
            var globalIndex = 0;
            foreach (var seat in SpadesSeatUtility.TurnOrder)
            {
                var handCount = controller?.GetHand(seat)?.Count ?? 13;
                for (var index = 0; index < handCount; index++)
                {
                    var preview = CreateOpeningGatherCard(seat, index, handCount, globalIndex, totalCards, previewSize);
                    var delay = GetOpeningGatherDelay(seat, index, globalIndex);
                    maxDelay = Mathf.Max(maxDelay, delay);
                    openingStackPreviewAnimations[preview] = StartCoroutine(AnimateOpeningGatherCard(preview, seat, index, handCount, globalIndex, totalCards, delay));
                    globalIndex++;
                }
            }

            yield return new WaitForSecondsRealtime(OpeningGatherTravelSeconds + maxDelay + 0.28f);
            PlayFeedback(FeedbackCue.Deal, 0.12f);
        }

        private CardButtonView CreateOpeningGatherCard(SeatId seat, int index, int seatCount, int globalIndex, int totalCards, Vector2 previewSize)
        {
            var preview = Instantiate(sceneRefs.CardButtonPrefab, AnimationRoot);
            preview.gameObject.name = $"Opening Gather {seat} {index + 1}";
            preview.transform.SetAsLastSibling();
            preview.Button.onClick.RemoveAllListeners();
            preview.Button.enabled = false;
            preview.CanvasGroup = preview.CanvasGroup != null ? preview.CanvasGroup : ResolveCanvasGroup(preview.gameObject);
            preview.CanvasGroup.blocksRaycasts = false;
            preview.Root.anchorMin = new Vector2(0.5f, 0.5f);
            preview.Root.anchorMax = new Vector2(0.5f, 0.5f);
            preview.Root.pivot = new Vector2(0.5f, 0.5f);
            preview.Root.anchoredPosition = GetOpeningGatherSourcePoint(seat, index, seatCount);
            preview.Root.sizeDelta = GetOpeningGatherSourceSize(seat, previewSize);
            preview.Root.localRotation = GetOpeningGatherSourceRotation(seat, index, seatCount);
            preview.Root.localScale = Vector3.one * 1.24f;
            preview.CanvasGroup.alpha = 1f;
            ApplyCardBackVisual(preview);
            openingStackPreviewCards.Add(preview);
            return preview;
        }

        private IEnumerator AnimateOpeningGatherCard(
            CardButtonView preview,
            SeatId seat,
            int index,
            int seatCount,
            int globalIndex,
            int totalCards,
            float delay)
        {
            if (preview == null)
            {
                yield break;
            }

            if (delay > 0f)
            {
                yield return new WaitForSecondsRealtime(delay);
            }

            var startPosition = preview.Root.anchoredPosition;
            var startRotation = preview.Root.localRotation;
            var startSize = preview.Root.sizeDelta;
            var targetPosition = GetOpeningStackDeckIntroPosition(globalIndex, totalCards);
            var targetSize = ResolveOpeningStackPreviewCardSize();
            var targetRotation = Quaternion.Euler(0f, 0f, GetOpeningStackDeckRotation(globalIndex, totalCards));
            var direction = (targetPosition - startPosition).sqrMagnitude > 1f
                ? (targetPosition - startPosition).normalized
                : Vector2.up;
            var side = GetOpeningDealLaneSide(seat);
            var tangent = new Vector2(-direction.y, direction.x) * side;
            var distance = Vector2.Distance(startPosition, targetPosition);
            var controlOne = Vector2.Lerp(startPosition, targetPosition, 0.28f)
                             + Vector2.up * Mathf.Clamp(distance * 0.12f, 36f, 130f)
                             + tangent * Mathf.Clamp(distance * 0.09f, 18f, 78f);
            var controlTwo = Vector2.Lerp(startPosition, targetPosition, 0.72f)
                             + Vector2.up * Mathf.Clamp(distance * 0.18f, 42f, 160f)
                             - tangent * Mathf.Clamp(distance * 0.045f, 12f, 48f);
            var duration = OpeningGatherTravelSeconds + Mathf.Lerp(0f, 0.12f, GetOpeningStackPreviewRandom01(globalIndex + 41));
            var elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                var eased = EaseInOutCubic(t);
                var pulse = 1.16f + Mathf.Sin(t * Mathf.PI) * 0.06f;
                preview.CanvasGroup.alpha = 1f;
                preview.Root.anchoredPosition = CubicBezier(startPosition, controlOne, controlTwo, targetPosition, eased);
                preview.Root.sizeDelta = Vector2.Lerp(startSize, targetSize, EaseOutCubic(t));
                preview.Root.localRotation = Quaternion.Slerp(startRotation, targetRotation, eased) *
                                             Quaternion.Euler(0f, 0f, Mathf.Sin(t * Mathf.PI) * GetOpeningDealTwist(seat));
                preview.Root.localScale = Vector3.one * pulse;
                yield return null;
            }

            preview.CanvasGroup.alpha = 1f;
            preview.Root.anchoredPosition = targetPosition;
            preview.Root.sizeDelta = targetSize;
            preview.Root.localRotation = targetRotation;
            preview.Root.localScale = Vector3.one * 1.16f;
            openingStackPreviewAnimations.Remove(preview);
        }

        private CardButtonView CreateOpeningStackPreviewCard(int index, int totalCards, Vector2 previewSize)
        {
            var preview = Instantiate(sceneRefs.CardButtonPrefab, AnimationRoot);
            preview.gameObject.name = $"Opening Stack Preview {index + 1}";
            preview.transform.SetAsLastSibling();
            preview.Button.onClick.RemoveAllListeners();
            preview.Button.enabled = false;
            preview.CanvasGroup = preview.CanvasGroup != null ? preview.CanvasGroup : preview.GetComponent<CanvasGroup>();
            if (preview.CanvasGroup == null)
            {
                preview.CanvasGroup = preview.gameObject.AddComponent<CanvasGroup>();
            }

            preview.CanvasGroup.blocksRaycasts = false;
            preview.Root.anchorMin = new Vector2(0.5f, 0.5f);
            preview.Root.anchorMax = new Vector2(0.5f, 0.5f);
            preview.Root.pivot = new Vector2(0.5f, 0.5f);
            preview.Root.anchoredPosition = GetOpeningStackDeckIntroPosition(index, totalCards);
            preview.Root.sizeDelta = previewSize;
            preview.Root.localRotation = Quaternion.identity;
            preview.Root.localScale = Vector3.one * 1.16f;
            preview.CanvasGroup.alpha = 0f;
            ApplyOpeningStackPreviewVisual(preview);
            openingStackPreviewCards.Add(preview);
            return preview;
        }

        private IEnumerator AnimateOpeningStackPreviewCard(CardButtonView preview, int index, int totalCards, float duration, float delay)
        {
            if (preview == null)
            {
                yield break;
            }

            if (delay > 0f)
            {
                yield return new WaitForSecondsRealtime(delay);
            }

            var startPosition = preview.Root.anchoredPosition;
            var startRotation = preview.Root.localRotation;
            var startScale = preview.Root.localScale;
            var startSize = preview.Root.sizeDelta;
            var previewSize = ResolveOpeningStackPreviewCardSize();
            var targetPosition = GetOpeningStackDeckPosition(index, totalCards);
            var targetRotation = Quaternion.Euler(0f, 0f, GetOpeningStackDeckRotation(index, totalCards));
            var arcHeight = Mathf.Lerp(4f, 12f, GetOpeningStackPreviewRandom01(index + 29));
            var elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                var eased = EaseOutCubic(t);
                var settlePulse = 1f + Mathf.Sin(t * Mathf.PI) * 0.045f;
                preview.CanvasGroup.alpha = Mathf.Lerp(0f, 1f, eased);
                preview.Root.anchoredPosition = Vector2.Lerp(startPosition, targetPosition, eased) + Vector2.up * Mathf.Sin(t * Mathf.PI) * arcHeight;
                preview.Root.sizeDelta = Vector2.Lerp(startSize, previewSize, eased);
                preview.Root.localRotation = Quaternion.Slerp(startRotation, targetRotation, eased);
            preview.Root.localScale = Vector3.Lerp(startScale, Vector3.one * 1.16f * settlePulse, eased);
                yield return null;
            }

            preview.CanvasGroup.alpha = 1f;
            preview.Root.anchoredPosition = targetPosition;
            preview.Root.sizeDelta = previewSize;
            preview.Root.localRotation = targetRotation;
            preview.Root.localScale = Vector3.one * 1.16f;
            while (preview != null && openingDealPending && !openingDealRunning)
            {
                var phase = Time.unscaledTime * GetOpeningStackPreviewFloatSpeed(index) + index * 0.19f;
                var amplitude = GetOpeningStackPreviewFloatAmplitude(index);
                var hover = new Vector2(Mathf.Sin(phase) * amplitude.x, Mathf.Cos(phase * 0.82f) * amplitude.y);
                preview.Root.anchoredPosition = targetPosition + hover;
                preview.Root.localRotation = targetRotation * Quaternion.Euler(0f, 0f, Mathf.Sin(phase * 0.6f) * 1.1f);
                preview.Root.localScale = Vector3.one * (1.16f + Mathf.Sin(phase * 0.75f) * 0.006f);
                yield return null;
            }

            openingStackPreviewAnimations.Remove(preview);
        }

        private IEnumerator OpeningStackShuffleFlourish()
        {
            if (openingStackPreviewCards.Count == 0)
            {
                yield break;
            }

            PlayFeedback(FeedbackCue.Deal, 0.1f);
            var duration = 0.36f;
            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                var wave = Mathf.Sin(t * Mathf.PI);
                for (var index = 0; index < openingStackPreviewCards.Count; index++)
                {
                    var preview = openingStackPreviewCards[index];
                    if (preview == null)
                    {
                        continue;
                    }

                    var total = openingStackPreviewCards.Count;
                    var target = GetOpeningStackDeckPosition(index, total);
                    var phase = (index / Mathf.Max(1f, total - 1f)) * Mathf.PI * 2f;
                    var riffle = new Vector2(
                        Mathf.Sin(phase + t * Mathf.PI * 4f) * 10f,
                        Mathf.Cos(phase * 0.7f + t * Mathf.PI * 3f) * 5f) * wave;
                    preview.Root.anchoredPosition = target + riffle;
                    preview.Root.localRotation = Quaternion.Euler(0f, 0f, GetOpeningStackDeckRotation(index, total) + Mathf.Sin(phase + t * Mathf.PI * 5f) * 6f * wave);
                    preview.Root.localScale = Vector3.one * (1.16f + wave * 0.025f);
                    preview.CanvasGroup.alpha = 1f;
                }

                yield return null;
            }

            for (var index = 0; index < openingStackPreviewCards.Count; index++)
            {
                var preview = openingStackPreviewCards[index];
                if (preview == null)
                {
                    continue;
                }

                preview.Root.anchoredPosition = GetOpeningStackDeckPosition(index, openingStackPreviewCards.Count);
                preview.Root.localRotation = Quaternion.Euler(0f, 0f, GetOpeningStackDeckRotation(index, openingStackPreviewCards.Count));
                preview.Root.localScale = Vector3.one * 1.16f;
            }
        }

        private void ClearOpeningStackPreviewCards()
        {
            foreach (var pair in openingStackPreviewAnimations.ToArray())
            {
                if (pair.Value != null)
                {
                    StopCoroutine(pair.Value);
                }
            }

            openingStackPreviewAnimations.Clear();
            foreach (var preview in openingStackPreviewCards.ToArray())
            {
                if (preview != null)
                {
                    Destroy(preview.gameObject);
                }
            }

            openingStackPreviewCards.Clear();
        }

        private void RefreshOpeningStackEffectVisual()
        {
            if (openingStackEffectImage == null)
            {
                return;
            }

            openingStackEffectImage.gameObject.SetActive(false);
            openingStackEffectImage.enabled = false;
        }

        private void EnsureOpeningStackEffectVisual()
        {
            if (sceneRefs.OpeningStackImage == null || openingStackEffectImage != null)
            {
                return;
            }

            var go = new GameObject("Opening Stack Effect Runtime", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(sceneRefs.OpeningStackImage.transform, false);
            openingStackEffectImage = go.GetComponent<Image>();
            openingStackEffectImage.raycastTarget = false;
        }

        private void ApplyDeferredSheetState()
        {
            if (HasCardMotionPending || setBookMomentRunning)
            {
                return;
            }

            while (pendingSetBookMoments.Count > 0)
            {
                if (ShowSetBookMoment(pendingSetBookMoments.Dequeue()))
                {
                    return;
                }
            }

            if (pendingEndSheetOpen)
            {
                pendingEndSheetOpen = false;
                pendingRoundSheetOpen = false;
                ClearPlayerScoreOverlayFx();
                SetSheetVisible(sceneRefs.RoundSheet, false);
                SetSheetVisible(sceneRefs.EndSheet, true);
                return;
            }

            if (!pendingRoundSheetOpen)
            {
                return;
            }

            pendingRoundSheetOpen = false;
            ClearPlayerScoreOverlayFx();
            AnimateScoreDelta(TeamId.Home);
            AnimateScoreDelta(TeamId.Away);
            SetSheetVisible(sceneRefs.RoundSheet, true);
        }

        private void ScheduleDeferredSheetState()
        {
            if (deferredSheetStateLoop != null)
            {
                return;
            }

            deferredSheetStateLoop = StartCoroutine(DeferredSheetStateRoutine());
        }

        private IEnumerator DeferredSheetStateRoutine()
        {
            yield return null;
            deferredSheetStateLoop = null;
            ApplyDeferredSheetState();
        }

        private void QueueSetBookMoment(TeamId team)
        {
            pendingSetBookMoments.Enqueue(team);
            ApplyDeferredSheetState();
        }

        private void ShowBidCallout(SeatId seat, int bid)
        {
            var holdForPlayerDecision = ShouldHoldBidCalloutForPlayerDecision(seat);
            if (seat != SeatId.Bottom)
            {
                StartBidCameraFocus(seat);
            }

            ShowSeatCallout(
                seat,
                bid == 0 ? "I BID NIL" : $"I BID {bid}",
                1.3f,
                ResolveBidTagColor(seat, bid),
                new Color(0.045f, 0.05f, 0.052f, 1f),
                holdForPlayerDecision,
                graffitiTag: true);
        }

        private Color ResolveBidTagColor(SeatId seat, int bid)
        {
            if (bid == 0)
            {
                return new Color(1f, 0.42f, 0.2f, 0.96f);
            }

            return seat switch
            {
                SeatId.Bottom => new Color(1f, 0.82f, 0.18f, 0.96f),
                SeatId.Left => new Color(1f, 0.28f, 0.48f, 0.96f),
                SeatId.Top => new Color(0.24f, 0.92f, 0.58f, 0.96f),
                SeatId.Right => new Color(0.2f, 0.76f, 1f, 0.96f),
                _ => theme.gold
            };
        }

        private static float ResolveBidTagRotation(SeatId seat)
        {
            return seat switch
            {
                SeatId.Bottom => 2.2f,
                SeatId.Left => -3.4f,
                SeatId.Top => -1.7f,
                SeatId.Right => 3.1f,
                _ => 0f
            };
        }

        private bool ShouldHoldBidCalloutForPlayerDecision(SeatId seat)
        {
            if (seat == SeatId.Bottom || controller == null || controller.State.Phase != MatchPhase.Bidding || controller.State.RoundState == null)
            {
                return false;
            }

            return controller.State.RoundState.BidState.BidsBySeat.TryGetValue(SeatId.Bottom, out var playerBid) && !playerBid.HasValue;
        }

        private void ShowSeatCallout(SeatId seat, string text, float holdSeconds, Color panelColor, Color textColor, bool holdVisible = false, bool graffitiTag = false)
        {
            if (!seatViews.TryGetValue(seat, out var view) || view?.BidCalloutGroup == null || view.BidCalloutText == null || view.BidCalloutPanel == null)
            {
                return;
            }

            if (bidBubbleLoops.TryGetValue(seat, out var runningLoop) && runningLoop != null)
            {
                StopCoroutine(runningLoop);
                ResetCalloutVisual(view);
            }

            if (graffitiTag)
            {
                ApplyGraffitiCalloutVisual(view, panelColor, textColor);
                PlayGraffitiSpraySound();
            }
            else
            {
                ApplyFallbackSprite(view.BidCalloutPanel, theme.buttonSprite != null ? theme.buttonSprite : ResolveSoftPanelSprite());
                view.BidCalloutPanel.type = Image.Type.Sliced;
                view.BidCalloutPanel.color = panelColor;
                if (view.BidCalloutSplash != null)
                {
                    view.BidCalloutSplash.color = new Color(1f, 1f, 1f, 0f);
                }
            }

            view.BidCalloutText.color = textColor;
            view.BidCalloutText.text = text;
            if (!graffitiTag)
            {
                SpawnImpactBurst(GetAnchoredPoint(view.BidCalloutGroup.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f)), textColor, 24f, 3);
            }

            bidBubbleLoops[seat] = StartCoroutine(BidCalloutRoutine(view, seat, holdSeconds, holdVisible, graffitiTag));
        }

        private void ApplyGraffitiCalloutVisual(SeatPanelView view, Color tagColor, Color textColor)
        {
            ApplyFallbackSprite(view.BidCalloutPanel, ResolveSoftPanelSprite());
            view.BidCalloutPanel.type = Image.Type.Sliced;
            view.BidCalloutPanel.color = new Color(0.015f, 0.018f, 0.02f, 0.72f);
            EnsureBidCalloutSplash(view);
            if (view.BidCalloutSplash != null)
            {
                view.BidCalloutSplash.sprite = ResolveGraffitiSplashSprite();
                view.BidCalloutSplash.color = tagColor;
                view.BidCalloutSplash.transform.SetAsFirstSibling();
            }

            if (view.BidCalloutText != null)
            {
                view.BidCalloutText.fontStyle = FontStyle.BoldAndItalic;
                view.BidCalloutText.color = textColor;
                view.BidCalloutText.transform.SetAsLastSibling();
            }
        }

        private IEnumerator GraffitiBidTagIntroRoutine(SeatPanelView view, SeatId seat)
        {
            var rect = view.BidCalloutGroup.GetComponent<RectTransform>();
            var tagRotation = ResolveBidTagRotation(seat);
            var duration = Mathf.Max(0.18f, theme.pulseDuration * 1.05f);
            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                var ease = 1f - Mathf.Pow(1f - t, 3f);
                var overshoot = Mathf.Sin(t * Mathf.PI) * 0.18f;
                var stencilJitter = Mathf.Sin(t * Mathf.PI * 9f) * (1f - t) * 2.2f;
                view.BidCalloutGroup.alpha = Mathf.Clamp01(t * 1.7f);
                rect.localScale = Vector3.one * (Mathf.Lerp(0.68f, 1f, ease) + overshoot);
                rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(tagRotation * 2.1f, tagRotation, ease) + stencilJitter);
                yield return null;
            }

            view.BidCalloutGroup.alpha = 1f;
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.Euler(0f, 0f, tagRotation);
        }

        private IEnumerator BidCalloutRoutine(SeatPanelView view, SeatId seat, float holdSeconds, bool holdVisible, bool graffitiTag)
        {
            var groupRect = view.BidCalloutGroup.GetComponent<RectTransform>();
            view.BidCalloutGroup.alpha = 0f;
            var originalRotation = groupRect.localRotation;
            if (graffitiTag)
            {
                yield return GraffitiBidTagIntroRoutine(view, seat);
            }
            else
            {
                yield return PulseRect(groupRect, 1.05f, Mathf.Max(0.1f, theme.pulseDuration * 0.85f));
                var fadeInElapsed = 0f;
                var fadeIn = Mathf.Max(0.08f, theme.pulseDuration * 0.45f);
                while (fadeInElapsed < fadeIn)
                {
                    fadeInElapsed += Time.unscaledDeltaTime;
                    view.BidCalloutGroup.alpha = Mathf.Clamp01(fadeInElapsed / fadeIn);
                    yield return null;
                }
            }

            view.BidCalloutGroup.alpha = 1f;
            if (holdVisible)
            {
                bidBubbleLoops.Remove(seat);
                yield break;
            }

            yield return new WaitForSecondsRealtime(Mathf.Max(0.05f, holdSeconds));
            var elapsed = 0f;
            var fadeOut = Mathf.Max(0.18f, theme.modalDuration * 0.9f);
            while (elapsed < fadeOut)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / fadeOut);
                view.BidCalloutGroup.alpha = 1f - t;
                if (graffitiTag)
                {
                    groupRect.localScale = Vector3.one * Mathf.Lerp(1f, 0.94f, t);
                }

                yield return null;
            }

            view.BidCalloutGroup.alpha = 0f;
            groupRect.localScale = Vector3.one;
            groupRect.localRotation = originalRotation;
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
                ApplyFallbackSprite(view.BidCalloutPanel, theme.buttonSprite != null ? theme.buttonSprite : ResolveSoftPanelSprite());
                view.BidCalloutPanel.type = Image.Type.Sliced;
                view.BidCalloutPanel.color = new Color(0.15f, 0.16f, 0.18f, 0.96f);
            }

            if (view?.BidCalloutSplash != null)
            {
                view.BidCalloutSplash.color = new Color(1f, 1f, 1f, 0f);
            }

            if (view?.BidCalloutText != null)
            {
                view.BidCalloutText.color = theme.primaryText;
                view.BidCalloutText.fontStyle = FontStyle.Bold;
            }

            if (view?.BidCalloutGroup != null)
            {
                var rect = (RectTransform)view.BidCalloutGroup.transform;
                rect.localScale = Vector3.one;
                rect.localRotation = Quaternion.identity;
            }
        }

        private void SpawnImpactBurst(Vector2 anchoredPosition, Color color, float size, int pieces)
        {
            SpawnEpicToonFx(cardImpactFxPrefab, anchoredPosition, Mathf.Clamp(size / 58f, 0.45f, 1.55f));
        }

        private void SpawnCardSmokeFx(Vector2 anchoredPosition, Color color, float scale, int puffs)
        {
            SpawnEpicToonFx(cardSmokeFxPrefab, anchoredPosition, Mathf.Clamp(scale, 0.35f, 1.35f));
        }

        private IEnumerator OpeningDealFinale()
        {
            foreach (var seat in SpadesSeatUtility.TurnOrder)
            {
                if (seatViews.TryGetValue(seat, out var view) && view != null)
                {
                    StartCoroutine(PulseRect(view.Root, 1.035f, 0.26f));
                }
            }

            if (sceneRefs.HandContent != null)
            {
                yield return PulseRect(sceneRefs.HandContent, 1.045f, 0.34f);
            }
            else
            {
                yield return new WaitForSecondsRealtime(0.34f);
            }
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

        private IEnumerator CardSmokeFxRoutine(Vector2 anchoredPosition, Color color, float scale, int puffs)
        {
            var sprite = ResolveSoftPanelSprite();
            var localPuffs = new List<Graphic>(puffs);
            for (var index = 0; index < puffs; index++)
            {
                var fxGo = new GameObject($"Card Smoke Fx {index}", typeof(RectTransform), typeof(Image));
                fxGo.transform.SetParent(AnimationRoot, false);
                fxGo.transform.SetAsLastSibling();
                var rect = fxGo.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                var spread = 18f * scale;
                rect.anchoredPosition = anchoredPosition + new Vector2(Random.Range(-spread, spread), Random.Range(-spread * 0.25f, spread * 0.35f));
                rect.sizeDelta = Vector2.one * Random.Range(30f, 54f) * scale;
                rect.localRotation = Quaternion.Euler(0f, 0f, Random.Range(-24f, 24f));
                var image = fxGo.GetComponent<Image>();
                image.sprite = sprite;
                image.type = Image.Type.Sliced;
                image.raycastTarget = false;
                image.color = color;
                transientFx.Add(image);
                localPuffs.Add(image);
            }

            var elapsed = 0f;
            var duration = Mathf.Max(0.22f, theme.pulseDuration * 1.45f);
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                var eased = EaseOutCubic(t);
                for (var index = 0; index < localPuffs.Count; index++)
                {
                    var puff = localPuffs[index];
                    if (puff == null)
                    {
                        continue;
                    }

                    var rect = (RectTransform)puff.transform;
                    var side = index % 2 == 0 ? -1f : 1f;
                    rect.anchoredPosition += new Vector2(side * 10f, 24f) * Time.unscaledDeltaTime * scale;
                    rect.localScale = Vector3.one * Mathf.Lerp(0.72f, 1.55f, eased);
                    puff.color = new Color(color.r, color.g, color.b, Mathf.Lerp(color.a, 0f, t));
                }

                yield return null;
            }

            foreach (var puff in localPuffs)
            {
                if (puff != null)
                {
                    transientFx.Remove(puff);
                    Destroy(puff.gameObject);
                }
            }
        }

        private void StartBookTextImpact(SeatId seat, string overrideText, Color color, bool setBookSlam)
        {
            if (!seatViews.TryGetValue(seat, out var view) || view?.TricksText == null)
            {
                return;
            }

            if (bookTextLoops.TryGetValue(seat, out var existing) && existing != null)
            {
                StopCoroutine(existing);
            }

            bookTextLoops[seat] = StartCoroutine(BookTextImpactRoutine(seat, view.TricksText, overrideText, color, setBookSlam));
        }

        private IEnumerator BookTextImpactRoutine(SeatId seat, Text text, string overrideText, Color color, bool setBookSlam)
        {
            if (text == null)
            {
                bookTextLoops.Remove(seat);
                yield break;
            }

            var rectTransform = text.rectTransform;
            var defaultText = BuildBooksText(seat);
            var defaults = CaptureBookTextDefaults(seat, text);
            var center = GetAnchoredPoint(rectTransform, new Vector2(0.5f, 0.5f));

            text.text = string.IsNullOrEmpty(overrideText) ? defaultText : overrideText;
            text.color = color;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            var baseFontScale = setBookSlam ? 1.75f : 1.2f;
            var exaggeratedFontScale = 1f + (baseFontScale - 1f) * BookImpactExaggeration;
            text.fontSize = setBookSlam
                ? Mathf.Max(defaults.FontSize + Mathf.RoundToInt(14f * BookImpactExaggeration), Mathf.RoundToInt(defaults.FontSize * exaggeratedFontScale))
                : Mathf.Max(defaults.FontSize + Mathf.RoundToInt(5f * BookImpactExaggeration), Mathf.RoundToInt(defaults.FontSize * exaggeratedFontScale));
            SetGraphicAlpha(text, 0f);
            var entryScale = setBookSlam ? 0.18f : 0.46f;
            var lift = (setBookSlam ? 9f : 3f) * BookImpactExaggeration;
            rectTransform.localScale = defaults.Scale * entryScale;
            rectTransform.anchoredPosition = defaults.Position + Vector2.up * lift;

            SpawnImpactBurst(
                center,
                color,
                (setBookSlam ? 52f : 28f) * BookImpactExaggeration,
                Mathf.RoundToInt((setBookSlam ? 6f : 3f) * BookImpactExaggeration));

            var slamDuration = (setBookSlam ? 0.28f : 0.18f) * 1.2f;
            var elapsed = 0f;
            while (elapsed < slamDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / slamDuration);
                var eased = Mathf.SmoothStep(0f, 1f, t);
                var basePeak = setBookSlam ? 1.7f : 1.26f;
                var peak = 1f + (basePeak - 1f) * BookImpactExaggeration;
                var overshoot = t < 0.62f
                    ? Mathf.Lerp(entryScale, peak, Mathf.Clamp01(t / 0.62f))
                    : Mathf.Lerp(peak, 1f, Mathf.Clamp01((t - 0.62f) / 0.38f));
                var shake = Mathf.Sin(elapsed * 98f) * Mathf.Lerp((setBookSlam ? 7f : 3f) * BookImpactExaggeration, 0f, eased);
                SetGraphicAlpha(text, Mathf.Clamp01(t * 4.5f));
                rectTransform.localScale = defaults.Scale * overshoot;
                rectTransform.anchoredPosition = defaults.Position + new Vector2(shake, Mathf.Lerp(lift, 0f, eased));
                yield return null;
            }

            rectTransform.localScale = defaults.Scale;
            rectTransform.anchoredPosition = defaults.Position;
            SetGraphicAlpha(text, 1f);

            if (setBookSlam)
            {
                yield return new WaitForSecondsRealtime(0.65f);
                elapsed = 0f;
                const float fadeDuration = 0.2f;
                while (elapsed < fadeDuration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    var t = Mathf.Clamp01(elapsed / fadeDuration);
                    SetGraphicAlpha(text, 1f - t);
                    rectTransform.localScale = defaults.Scale * Mathf.Lerp(1f, 1f + 0.08f * BookImpactExaggeration, t);
                    yield return null;
                }
            }
            else
            {
                yield return new WaitForSecondsRealtime(0.16f);
            }

            text.text = BuildBooksText(seat);
            RestoreBookTextVisual(seat, text);
            bookTextLoops.Remove(seat);
            ApplyDeferredSheetState();
            ScheduleAiLoop();
        }

        private BookTextVisualState CaptureBookTextDefaults(SeatId seat, Text text)
        {
            if (bookTextDefaults.TryGetValue(seat, out var defaults))
            {
                return defaults;
            }

            var rectTransform = text.rectTransform;
            defaults = new BookTextVisualState(
                text.color,
                text.fontSize,
                text.fontStyle,
                text.alignment,
                rectTransform.localScale,
                rectTransform.anchoredPosition,
                text.canvasRenderer.GetAlpha());
            bookTextDefaults[seat] = defaults;
            return defaults;
        }

        private void RestoreBookTextVisual(SeatId seat, Text text)
        {
            var defaults = CaptureBookTextDefaults(seat, text);
            text.color = defaults.Color;
            text.fontSize = defaults.FontSize;
            text.fontStyle = defaults.FontStyle;
            text.alignment = defaults.Alignment;
            text.rectTransform.localScale = defaults.Scale;
            text.rectTransform.anchoredPosition = defaults.Position;
            SetGraphicAlpha(text, defaults.Alpha);
        }

        private void ClearBookTextAnimations()
        {
            foreach (var loop in bookTextLoops.Values.ToArray())
            {
                if (loop != null)
                {
                    StopCoroutine(loop);
                }
            }

            bookTextLoops.Clear();
            foreach (var pair in seatViews)
            {
                if (pair.Value?.TricksText == null)
                {
                    continue;
                }

                var text = pair.Value.TricksText;
                text.text = BuildBooksText(pair.Key);
                RestoreBookTextVisual(pair.Key, text);
            }
        }

        private string BuildBooksText(SeatId seat)
        {
            var tricks = controller?.State?.RoundState?.TricksWonBySeat != null &&
                         controller.State.RoundState.TricksWonBySeat.TryGetValue(seat, out var count)
                ? count
                : 0;
            return $"Books: {tricks}";
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

        private int ResolveOpeningStackCardCount()
        {
            return controller?.State?.RoundState != null
                ? controller.State.RoundState.HandsBySeat.Values.Sum(cards => cards.Count)
                : 52;
        }

        private Vector2 ResolveOpeningStackPreviewCardSize()
        {
            return ResolveOpeningDealLaunchCardSize();
        }

        private Vector2 ResolveOpeningDealLaunchCardSize()
        {
            return ResolveCardButtonBaseSize() * OpeningDeckStackSizeMultiplier;
        }

        private static Vector2 GetOpeningDealLaunchOffset(int index, int count)
        {
            return GetOpeningStackDeckOffset(index, count);
        }

        private static Quaternion GetOpeningDealLaunchRotation(int index, int count)
        {
            return Quaternion.identity;
        }

        private Vector2 GetOpeningStackDeckIntroPosition(int index, int totalCards)
        {
            return GetOpeningStackPosition() + GetOpeningStackDeckOffset(index, totalCards) + Vector2.up * 18f;
        }

        private Vector2 GetOpeningGatherSourcePoint(SeatId seat, int index, int count)
        {
            var center = GetOpeningStackPosition();
            var span = Mathf.Max(1, count - 1);
            var t = count <= 1 ? 0f : index / (float)span * 2f - 1f;
            var spread = index - span * 0.5f;
            return seat switch
            {
                SeatId.Top => center + new Vector2(spread * 28f, 205f + Mathf.Abs(t) * 16f),
                SeatId.Left => center + new Vector2(-132f - Mathf.Abs(t) * 18f, spread * 18f + 10f),
                SeatId.Right => center + new Vector2(132f + Mathf.Abs(t) * 18f, spread * 18f + 10f),
                SeatId.Bottom => center + new Vector2(spread * 24f, -235f - Mathf.Abs(t) * 18f),
                _ => center
            };
        }

        private Vector2 GetOpeningGatherSourceSize(SeatId seat, Vector2 fallbackSize)
        {
            var readableSize = fallbackSize * 1.18f;
            if (seat == SeatId.Bottom)
            {
                return Vector2.Max(ResolveBottomHandCardSize() * 0.72f, readableSize);
            }

            return readableSize;
        }

        private Quaternion GetOpeningGatherSourceRotation(SeatId seat, int index, int count)
        {
            if (seat == SeatId.Bottom)
            {
                return GetFanTargetRotation(index, count);
            }

            return GetSeatFanTargetRotation(seat, index, count);
        }

        private static float GetOpeningGatherDelay(SeatId seat, int index, int globalIndex)
        {
            var seatOffset = seat switch
            {
                SeatId.Top => 0f,
                SeatId.Left => 0.07f,
                SeatId.Right => 0.14f,
                SeatId.Bottom => 0.21f,
                _ => 0f
            };

            return seatOffset + index * OpeningGatherDelayStep + Mathf.Max(0f, Mathf.Sin(globalIndex * 1.73f) * 0.01f);
        }

        private Vector2 GetOpeningStackDeckPosition(int index, int totalCards)
        {
            return GetOpeningStackPosition() + GetOpeningStackDeckOffset(index, totalCards);
        }

        private static Vector2 GetOpeningStackDeckOffset(int index, int totalCards)
        {
            if (totalCards <= 1)
            {
                return Vector2.zero;
            }

            var depth = index - (totalCards - 1) * 0.5f;
            return new Vector2(depth * OpeningDeckStackXOffset, depth * OpeningDeckStackYOffset);
        }

        private static float GetOpeningStackDeckRotation(int index, int totalCards)
        {
            return 0f;
        }

        private static float GetOpeningStackPreviewRandom01(int index)
        {
            return Mathf.Abs(Mathf.Sin((index + 1) * 12.9898f));
        }

        private static float GetOpeningStackPreviewFloatSpeed(int index)
        {
            return Mathf.Lerp(0.82f, 1.28f, GetOpeningStackPreviewRandom01(index + 17));
        }

        private static Vector2 GetOpeningStackPreviewFloatAmplitude(int index)
        {
            return new Vector2(
                Mathf.Lerp(0.6f, 1.6f, GetOpeningStackPreviewRandom01(index + 31)),
                Mathf.Lerp(0.8f, 2.0f, GetOpeningStackPreviewRandom01(index + 53)));
        }

        private static Vector2 CubicBezier(Vector2 a, Vector2 b, Vector2 c, Vector2 d, float t)
        {
            var oneMinusT = 1f - t;
            return oneMinusT * oneMinusT * oneMinusT * a
                   + 3f * oneMinusT * oneMinusT * t * b
                   + 3f * oneMinusT * t * t * c
                   + t * t * t * d;
        }

        private static float EaseOutCubic(float t)
        {
            t = Mathf.Clamp01(t);
            return 1f - Mathf.Pow(1f - t, 3f);
        }

        private static float EaseInOutCubic(float t)
        {
            t = Mathf.Clamp01(t);
            return t < 0.5f
                ? 4f * t * t * t
                : 1f - Mathf.Pow(-2f * t + 2f, 3f) * 0.5f;
        }

        private static float GetOpeningDealLaneSide(SeatId seat)
        {
            return seat switch
            {
                SeatId.Left => -1.1f,
                SeatId.Right => 1.1f,
                SeatId.Top => 0.35f,
                SeatId.Bottom => -0.25f,
                _ => 0f
            };
        }

        private static float GetOpeningDealTwist(SeatId seat)
        {
            return seat switch
            {
                SeatId.Left => -10f,
                SeatId.Right => 10f,
                SeatId.Top => 5f,
                SeatId.Bottom => -6f,
                _ => 0f
            };
        }

        private Vector2 GetHandAnimationPoint(int index, int count, bool isSelected)
        {
            var localTarget = GetFanTargetPosition(index, count, isSelected);
            var worldPoint = sceneRefs.HandContent.TransformPoint(localTarget);
            return WorldToAnimationPoint(worldPoint);
        }

        private Vector2 GetSeatDealPoint(SeatId seat, int index, int count)
        {
            var span = Mathf.Max(1, count - 1);
            var t = count <= 1 ? 0f : index / (float)span * 2f - 1f;
            var spreadIndex = index - span * 0.5f;
            var arcLift = (1f - t * t) * 22f;
            var offset = seat switch
            {
                SeatId.Top => new Vector2(-30f + spreadIndex * 32f, -212f + arcLift),
                SeatId.Left => new Vector2(-24f + spreadIndex * 18f, -76f + arcLift),
                SeatId.Right => new Vector2(24f + spreadIndex * 18f, -76f + arcLift),
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
            return (Vector2)AnimationRoot.InverseTransformPoint(worldPoint);
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
                bannerLoop = null;
            }

            setBookMomentRunning = false;
            RestoreBannerVisualDefaults(false);
            PlayFeedback(FeedbackCue.Banner, 0.24f);
            SpawnImpactBurst(GetAnchoredPoint(sceneRefs.BannerText.rectTransform, new Vector2(0.5f, 0.5f)), color, 42f, 5);
            bannerLoop = StartCoroutine(BannerRoutine(message, color));
        }

        private bool ShowSetBookMoment(TeamId team)
        {
            if (sceneRefs.BannerText == null)
            {
                return false;
            }

            if (bannerLoop != null)
            {
                StopCoroutine(bannerLoop);
                bannerLoop = null;
            }

            RestoreBannerVisualDefaults(false);
            var color = team == TeamId.Home ? theme.green : theme.red;
            PlaySetBookSound();
            TriggerSetBookHaptic();
            var center = GetAnchoredPoint(sceneRefs.BannerText.rectTransform, new Vector2(0.5f, 0.5f));
            SpawnEpicToonFx(setBookFxPrefab != null ? setBookFxPrefab : bookWinFxPrefab, center, 1.28f);
            SpawnImpactBurst(center, color, 88f, 10);
            SpawnImpactBurst(center + new Vector2(0f, -18f), theme.gold, 56f, 7);
            foreach (var seat in SpadesSeatUtility.TurnOrder.Where(seat => seat.ToTeam() == team))
            {
                StartBookTextImpact(seat, "SET BOOK", color, true);
            }

            setBookMomentRunning = true;
            bannerLoop = StartCoroutine(SetBookMomentRoutine(color));
            return true;
        }

        private IEnumerator FlashStatusColor(Color color)
        {
            sceneRefs.StatusText.color = color;
            yield return new WaitForSecondsRealtime(0.9f);
            sceneRefs.StatusText.color = theme.mutedText;
        }

        private IEnumerator BannerRoutine(string message, Color color)
        {
            RestoreBannerVisualDefaults(false);
            sceneRefs.BannerText.text = message;
            sceneRefs.BannerText.color = color;
            sceneRefs.BannerText.CrossFadeAlpha(1f, 0.08f, true);
            yield return new WaitForSecondsRealtime(theme.bannerDuration);
            sceneRefs.BannerText.CrossFadeAlpha(0f, 0.28f, true);
        }

        private IEnumerator SetBookMomentRoutine(Color color)
        {
            CaptureBannerVisualDefaults();
            var target = sceneRefs.BannerText;
            var rectTransform = target.rectTransform;
            var startPosition = bannerDefaultPosition;
            target.text = "SET BOOK";
            target.color = color;
            target.fontSize = 74;
            target.fontStyle = FontStyle.Bold;
            target.alignment = TextAnchor.MiddleCenter;
            target.canvasRenderer.SetAlpha(1f);
            SetGraphicAlpha(target, 0f);
            rectTransform.localScale = bannerDefaultScale * 0.58f;
            rectTransform.anchoredPosition = startPosition + new Vector2(0f, 20f);

            const float slamDuration = 0.26f;
            var elapsed = 0f;
            while (elapsed < slamDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / slamDuration);
                var eased = Mathf.SmoothStep(0f, 1f, t);
                var overshoot = t < 0.68f
                    ? Mathf.Lerp(0.58f, 1.28f, Mathf.Clamp01(t / 0.68f))
                    : Mathf.Lerp(1.28f, 1f, Mathf.Clamp01((t - 0.68f) / 0.32f));
                var shake = Mathf.Sin(elapsed * 92f) * Mathf.Lerp(18f, 0f, eased);
                SetGraphicAlpha(target, Mathf.Clamp01(t * 3.8f));
                rectTransform.localScale = bannerDefaultScale * overshoot;
                rectTransform.anchoredPosition = startPosition + new Vector2(shake, Mathf.Lerp(20f, 0f, eased));
                yield return null;
            }

            rectTransform.localScale = bannerDefaultScale;
            rectTransform.anchoredPosition = startPosition;
            SetGraphicAlpha(target, 1f);
            yield return new WaitForSecondsRealtime(0.7f);

            const float fadeDuration = 0.22f;
            elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / fadeDuration);
                SetGraphicAlpha(target, 1f - t);
                rectTransform.localScale = bannerDefaultScale * Mathf.Lerp(1f, 1.08f, t);
                yield return null;
            }

            RestoreBannerVisualDefaults(true);
            setBookMomentRunning = false;
            bannerLoop = null;
            ApplyDeferredSheetState();
            RenderAll();
            ScheduleAiLoop();
        }

        private void CaptureBannerVisualDefaults()
        {
            if (bannerDefaultsCaptured || sceneRefs.BannerText == null)
            {
                return;
            }

            var target = sceneRefs.BannerText;
            bannerDefaultFontSize = target.fontSize;
            bannerDefaultFontStyle = target.fontStyle;
            bannerDefaultAlignment = target.alignment;
            bannerDefaultColor = target.color;
            bannerDefaultPosition = target.rectTransform.anchoredPosition;
            bannerDefaultScale = target.rectTransform.localScale;
            bannerDefaultsCaptured = true;
        }

        private void RestoreBannerVisualDefaults(bool clearText)
        {
            CaptureBannerVisualDefaults();
            if (sceneRefs.BannerText == null)
            {
                return;
            }

            var target = sceneRefs.BannerText;
            target.fontSize = bannerDefaultFontSize;
            target.fontStyle = bannerDefaultFontStyle;
            target.alignment = bannerDefaultAlignment;
            target.color = bannerDefaultColor;
            target.rectTransform.anchoredPosition = bannerDefaultPosition;
            target.rectTransform.localScale = bannerDefaultScale;
            if (clearText)
            {
                target.text = string.Empty;
                target.canvasRenderer.SetAlpha(0f);
                SetGraphicAlpha(target, 0f);
            }
        }

        private static void TriggerSetBookHaptic()
        {
#if UNITY_IOS || UNITY_ANDROID
            Handheld.Vibrate();
#endif
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

        private void ToggleOptionsMenu()
        {
            if (optionsMenuOpen)
            {
                CloseOptionsMenu();
                return;
            }

            OpenOptionsMenu();
        }

        private void OpenOptionsMenu()
        {
            if (!CanOpenOptionsMenu())
            {
                return;
            }

            optionsMenuOpen = true;
            if (aiLoop != null)
            {
                StopCoroutine(aiLoop);
                aiLoop = null;
            }

            if (sceneRefs.OptionsMenu != null)
            {
                sceneRefs.OptionsMenu.SetAsLastSibling();
            }

            RenderAll();
            PlayOptionsMenuOpenAnimation();
            PlayFeedback(FeedbackCue.OptionsMenuOpen, 0.24f);
        }

        private void CloseOptionsMenu()
        {
            CloseOptionsMenu(true);
        }

        private void CloseOptionsMenu(bool resumeAi)
        {
            if (!optionsMenuOpen)
            {
                return;
            }

            optionsMenuOpen = false;
            PlayOptionsMenuCloseAnimation();
            PlayFeedback(FeedbackCue.OptionsMenuClose, 0.2f);
            RenderOptionsMenu();
            if (resumeAi)
            {
                ScheduleAiLoop();
            }
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

            CloseOptionsMenu(false);
            activePrompt = ConfirmationPromptType.ReturnToLobby;
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

            SetButtonLabel(sceneRefs.ExitPromptCancelButton, "STAY HERE");
            SetButtonLabel(sceneRefs.ExitPromptConfirmButton, "GO TO LOBBY");
            TintButton(sceneRefs.ExitPromptCancelButton, theme.green);
            TintButton(sceneRefs.ExitPromptConfirmButton, theme.red);
            sceneRefs.ExitPromptOverlay.SetAsLastSibling();
            StartExitPromptVisibility(true);
            PlayFeedback(FeedbackCue.Select, 0.16f);
            SpawnImpactBurst(GetAnchoredPoint(sceneRefs.ExitPromptOverlay, new Vector2(0.5f, 0.5f)), theme.red, 32f, 4);
        }

        private void OpenForfeitWarning()
        {
            if (!CanForfeitMatch())
            {
                PlayFeedback(FeedbackCue.Invalid, 0.16f);
                FlashStatus("Forfeit is only available during an active hand.", theme.red);
                RenderOptionsMenu();
                return;
            }

            if (sceneRefs.ExitPromptOverlay == null)
            {
                ConfirmForfeitMatch();
                return;
            }

            if (exitPromptOpen)
            {
                return;
            }

            CloseOptionsMenu(false);
            activePrompt = ConfirmationPromptType.ForfeitMatch;
            exitPromptOpen = true;
            if (aiLoop != null)
            {
                StopCoroutine(aiLoop);
                aiLoop = null;
            }

            if (sceneRefs.ExitPromptTitleText != null)
            {
                sceneRefs.ExitPromptTitleText.text = "FORFEIT MATCH?";
            }

            if (sceneRefs.ExitPromptBodyText != null)
            {
                sceneRefs.ExitPromptBodyText.text = "Your team will concede this match and the opponent will be marked as the winner.";
            }

            SetButtonLabel(sceneRefs.ExitPromptCancelButton, "KEEP PLAYING");
            SetButtonLabel(sceneRefs.ExitPromptConfirmButton, "FORFEIT");
            TintButton(sceneRefs.ExitPromptCancelButton, theme.green);
            TintButton(sceneRefs.ExitPromptConfirmButton, theme.red);
            sceneRefs.ExitPromptOverlay.SetAsLastSibling();
            StartExitPromptVisibility(true);
            PlayFeedback(FeedbackCue.Select, 0.16f);
            SpawnImpactBurst(GetAnchoredPoint(sceneRefs.ExitPromptOverlay, new Vector2(0.5f, 0.5f)), theme.red, 32f, 4);
        }

        private void OpenClaimRestWarning()
        {
            if (!CanClaimRemainingBooks())
            {
                PlayFeedback(FeedbackCue.Invalid, 0.16f);
                FlashStatus("Claim is available once live cards are settled.", theme.red);
                RenderOptionsMenu();
                return;
            }

            if (sceneRefs.ExitPromptOverlay == null)
            {
                ConfirmClaimRest();
                return;
            }

            if (exitPromptOpen)
            {
                return;
            }

            CloseOptionsMenu(false);
            activePrompt = ConfirmationPromptType.ClaimRest;
            exitPromptOpen = true;
            if (aiLoop != null)
            {
                StopCoroutine(aiLoop);
                aiLoop = null;
            }

            var remainingBooks = controller.GetRemainingBookCount();
            if (sceneRefs.ExitPromptTitleText != null)
            {
                sceneRefs.ExitPromptTitleText.text = "CLAIM THE REST?";
            }

            if (sceneRefs.ExitPromptBodyText != null)
            {
                sceneRefs.ExitPromptBodyText.text = $"Your team will take the remaining {remainingBooks} {BookLabel(remainingBooks)} and score this hand now.";
            }

            SetButtonLabel(sceneRefs.ExitPromptCancelButton, "KEEP PLAYING");
            SetButtonLabel(sceneRefs.ExitPromptConfirmButton, "CLAIM BOOKS");
            TintButton(sceneRefs.ExitPromptCancelButton, theme.panelStroke);
            TintButton(sceneRefs.ExitPromptConfirmButton, theme.green);
            sceneRefs.ExitPromptOverlay.SetAsLastSibling();
            StartExitPromptVisibility(true);
            PlayFeedback(FeedbackCue.Select, 0.16f);
            SpawnImpactBurst(GetAnchoredPoint(sceneRefs.ExitPromptOverlay, new Vector2(0.5f, 0.5f)), theme.gold, 32f, 4);
        }

        private void CloseBackWarning()
        {
            if (!exitPromptOpen)
            {
                return;
            }

            exitPromptOpen = false;
            activePrompt = ConfirmationPromptType.None;
            StartExitPromptVisibility(false, ScheduleAiLoop);
        }

        private void ConfirmActivePrompt()
        {
            switch (activePrompt)
            {
                case ConfirmationPromptType.ClaimRest:
                    ConfirmClaimRest();
                    break;
                case ConfirmationPromptType.ForfeitMatch:
                    ConfirmForfeitMatch();
                    break;
                case ConfirmationPromptType.ReturnToLobby:
                    exitPromptOpen = false;
                    activePrompt = ConfirmationPromptType.None;
                    StartExitPromptVisibility(false, ReturnToLobby);
                    break;
                default:
                    CloseBackWarning();
                    break;
            }
        }

        private void ConfirmForfeitMatch()
        {
            exitPromptOpen = false;
            activePrompt = ConfirmationPromptType.None;
            StartExitPromptVisibility(false, ForfeitMatch);
        }

        private void ConfirmClaimRest()
        {
            exitPromptOpen = false;
            activePrompt = ConfirmationPromptType.None;
            StartExitPromptVisibility(false, ClaimRemainingBooks);
        }

        private void ForfeitMatch()
        {
            if (controller == null)
            {
                ScheduleAiLoop();
                return;
            }

            Time.timeScale = 1f;
            selectedCard = null;
            pendingBidSelection = null;
            lastRenderedHand.Clear();
            ClearTransientMotionState(true);
            SetBidSheetVisible(false);
            SetOptionsMenuVisibleImmediate(false);
            if (!controller.TryForfeitMatch(TeamId.Home, out var error))
            {
                PlayFeedback(FeedbackCue.Invalid, 0.18f);
                FlashStatus(error, theme.red);
                ScheduleAiLoop();
                return;
            }

            RenderAll();
        }

        private void ClaimRemainingBooks()
        {
            if (controller == null)
            {
                ScheduleAiLoop();
                return;
            }

            if (!controller.TryClaimRemainingBooks(TeamId.Home, out var error))
            {
                PlayFeedback(FeedbackCue.Invalid, 0.18f);
                FlashStatus(error, theme.red);
                ScheduleAiLoop();
                return;
            }

            selectedCard = null;
            pendingBidSelection = null;
            lastRenderedHand.Clear();
            RenderAll();
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

        private string BuildTurnIndicatorText()
        {
            if (openingDealPending || openingDealRunning)
            {
                return "AUTO DEAL";
            }

            if (handReviewPending)
            {
                return "COUNT BOOKS";
            }

            if (bidTurnDelayPending)
            {
                return "NEXT BID";
            }

            return controller.State.Phase switch
            {
                MatchPhase.Bidding => $"BID: {controller.State.SeatNames[controller.State.RoundState.BidState.CurrentBidder]}",
                MatchPhase.TrickPlay => $"TURN: {controller.State.SeatNames[controller.State.RoundState.TrickState.CurrentTurn]}",
                MatchPhase.RoundSummary => "ROUND COMPLETE",
                MatchPhase.MatchEnded => "MATCH COMPLETE",
                _ => "READY"
            };
        }

        private bool IsCurrentTurnSeat(SeatId seat)
        {
            if (openingDealPending || openingDealRunning || handReviewPending || bidTurnDelayPending)
            {
                return false;
            }

            return controller.State.Phase switch
            {
                MatchPhase.Bidding => controller.State.RoundState.BidState.CurrentBidder == seat,
                MatchPhase.TrickPlay => controller.State.RoundState.TrickState.CurrentTurn == seat,
                _ => false
            };
        }

        private string BuildScoreboardText(TeamId team)
        {
            var score = controller.State.Scores[team];
            return $"{score.Score}/{selectedRule.TargetScore}";
        }

        private void SetDeckCountersVisible(bool visible)
        {
            if (sceneRefs.DeckAnchorImage != null)
            {
                sceneRefs.DeckAnchorImage.gameObject.SetActive(visible);
            }

            if (sceneRefs.DiscardAnchorImage != null)
            {
                sceneRefs.DiscardAnchorImage.gameObject.SetActive(visible);
            }
        }

        private void ConfigureScoreboardLayout()
        {
            SetScoreChipAnchors(sceneRefs.HomeScoreText, new Vector2(0.04f, 0.08f), new Vector2(0.40f, 0.34f));
            SetScoreChipAnchors(sceneRefs.AwayScoreText, new Vector2(0.60f, 0.08f), new Vector2(0.96f, 0.34f));
            if (sceneRefs.TimerHookText != null)
            {
                SetAnchors(sceneRefs.TimerHookText.rectTransform, new Vector2(0.40f, 0.08f), new Vector2(0.60f, 0.34f));
                sceneRefs.TimerHookText.alignment = TextAnchor.MiddleCenter;
                sceneRefs.TimerHookText.fontSize = 15;
            }
        }

        private static void SetScoreChipAnchors(Text label, Vector2 anchorMin, Vector2 anchorMax)
        {
            if (label == null || label.transform.parent == null)
            {
                return;
            }

            if (label.transform.root != null && label.transform.root.name == "Update3 Gameplay World UI")
            {
                label.alignment = TextAnchor.MiddleCenter;
                label.horizontalOverflow = HorizontalWrapMode.Overflow;
                label.verticalOverflow = VerticalWrapMode.Truncate;
                return;
            }

            if (label.transform.parent is RectTransform chip)
            {
                SetAnchors(chip, anchorMin, anchorMax);
            }

            label.alignment = TextAnchor.MiddleCenter;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
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
            var rules = selectedRule ?? controller.State.RuleSet;
            var bagTarget = rules.BagPenaltyThreshold;
            var reneges = controller.State.RoundState.RenegeSeats.Count == 0
                ? "None"
                : string.Join(", ", controller.State.RoundState.RenegeSeats.Select(seat => controller.State.SeatNames[seat]));

            return
                $"{BuildTeamRoundSummary("Home", home, bagTarget)}\n" +
                $"{BuildTeamRoundSummary("Away", away, bagTarget)}\n\n" +
                $"Reneges: {reneges}\n" +
                $"Target: {rules.TargetScore}";
        }

        private static string BuildTeamRoundSummary(string label, ScoreSnapshot score, int bagTarget)
        {
            return
                $"{label}\n" +
                $"Bid: {score.ContractBid} | Books: {score.TricksWon} | Bags gained: {score.BagsEarned} | Bags now: {score.Bags}/{bagTarget}\n" +
                $"Bag penalty: {FormatSigned(score.BagPenaltyDelta)} | Round score: {FormatSigned(score.RoundDelta)} | Nil: {FormatSigned(score.NilDelta)} | Total: {score.Score}";
        }

        private static string FormatSigned(int value)
        {
            return value.ToString("+#;-#;0");
        }

        private static void ConfigureWrapSummaryText(Text label, Vector2 anchorMin, Vector2 anchorMax, int maxFontSize, int minFontSize)
        {
            if (label == null)
            {
                return;
            }

            SetAnchors(label.rectTransform, anchorMin, anchorMax);
            label.alignment = TextAnchor.UpperLeft;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = minFontSize;
            label.resizeTextMaxSize = maxFontSize;
        }

        private string BuildMatchSummaryText(TeamId winningTeam)
        {
            var winnerLabel = winningTeam == TeamId.Home ? "You and your partner" : "The rivals";
            return
                $"{winnerLabel} closed the table.\n\n" +
                $"Home: {controller.State.Scores[TeamId.Home].Score}\n" +
                $"Away: {controller.State.Scores[TeamId.Away].Score}\n\n" +
                $"{BuildRoundSummaryText()}";
        }

        private TeamId ResolveCurrentLeadingTeam()
        {
            if (controller?.State?.Scores == null)
            {
                return TeamId.Home;
            }

            var homeScore = controller.State.Scores.TryGetValue(TeamId.Home, out var home) && home != null
                ? home.Score
                : 0;
            var awayScore = controller.State.Scores.TryGetValue(TeamId.Away, out var away) && away != null
                ? away.Score
                : 0;
            return awayScore > homeScore ? TeamId.Away : TeamId.Home;
        }

        private void ApplyThemeText(Text label, Color color, int fontSize, FontStyle style)
        {
            if (label == null)
            {
                return;
            }

            if (label.transform.root != null && label.transform.root.name == "Update3 Gameplay World UI")
            {
                return;
            }

            label.color = color;
            label.fontSize = fontSize;
            label.fontStyle = style;
            if (label.font == null)
            {
                label.font = theme.ResolveFont();
            }
        }

        private void ConfigureSeatCalloutLayout(SeatPanelView view)
        {
            if (view?.BidCalloutGroup != null)
            {
                SetAnchors((RectTransform)view.BidCalloutGroup.transform, BidCalloutAnchorMin, BidCalloutAnchorMax);
            }

            if (view?.BidCalloutSplash != null)
            {
                SetAnchors(view.BidCalloutSplash.rectTransform, new Vector2(-0.08f, -0.14f), new Vector2(1.08f, 1.12f));
                view.BidCalloutSplash.transform.SetAsFirstSibling();
            }

            if (view?.BidCalloutText == null)
            {
                return;
            }

            SetAnchors(view.BidCalloutText.rectTransform, new Vector2(0.02f, 0.06f), new Vector2(0.98f, 0.94f));
            view.BidCalloutText.fontSize = BidCalloutFontSize;
            view.BidCalloutText.fontStyle = FontStyle.Bold;
            view.BidCalloutText.alignment = TextAnchor.MiddleCenter;
            view.BidCalloutText.horizontalOverflow = HorizontalWrapMode.Wrap;
            view.BidCalloutText.verticalOverflow = VerticalWrapMode.Truncate;
            view.BidCalloutText.resizeTextForBestFit = true;
            view.BidCalloutText.resizeTextMinSize = 34;
            view.BidCalloutText.resizeTextMaxSize = BidCalloutFontSize;
            view.BidCalloutText.transform.SetAsLastSibling();
        }

        private void EnsureFallbackFont(Text label)
        {
            if (label == null || label.font != null)
            {
                return;
            }

            label.font = theme.ResolveFont();
        }

        private void UpdateCenterHintLayout()
        {
            // Preserve the editor-authored Center Hint layout and position.
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

        private Vector2 GetFanTargetPosition(int index, int count, bool isSelected, int selectedIndex = -1)
        {
            if (count <= 0)
            {
                return Vector2.zero;
            }

            var span = Mathf.Max(1, count - 1);
            var containerWidth = sceneRefs.HandContent != null ? sceneRefs.HandContent.rect.width : 720f;
            var cardWidth = ResolveBottomHandCardSize().x;
            var fitSpacing = count <= 1 ? 0f : (containerWidth - cardWidth) / span;
            var preferredSpread = cardWidth * 0.32f;
            var maxSpread = count <= 1 ? 0f : Mathf.Max(16f, Mathf.Min(preferredSpread, fitSpacing));
            var x = (index - span * 0.5f) * maxSpread;
            if (selectedIndex >= 0 && selectedIndex < count && index != selectedIndex)
            {
                var delta = index - selectedIndex;
                var pushBase = Mathf.Clamp(cardWidth * 0.12f, 18f, 32f);
                var falloff = Mathf.Pow(0.72f, Mathf.Abs(delta) - 1);
                x += Mathf.Sign(delta) * pushBase * falloff;
            }

            var t = count <= 1 ? 0f : index / (float)span * 2f - 1f;
            var arcLift = (1f - t * t) * Mathf.Clamp(cardWidth * 0.08f, 8f, 14f);
            var y = arcLift + (isSelected ? theme.cardLiftAmount : 0f);
            return new Vector2(x, y);
        }

        private Quaternion GetFanTargetRotation(int index, int count)
        {
            if (count <= 1)
            {
                return Quaternion.identity;
            }

            return Quaternion.Euler(0f, 0f, Mathf.Lerp(5f, -5f, index / (float)(count - 1)));
        }

        private Quaternion GetSeatFanTargetRotation(SeatId seat, int index, int count)
        {
            if (count <= 1)
            {
                return seat switch
                {
                    SeatId.Left => Quaternion.Euler(0f, 0f, 7f),
                    SeatId.Right => Quaternion.Euler(0f, 0f, -7f),
                    _ => Quaternion.identity
                };
            }

            var t = index / (float)(count - 1) * 2f - 1f;
            var baseAngle = seat switch
            {
                SeatId.Left => 7f,
                SeatId.Right => -7f,
                _ => 0f
            };
            return Quaternion.Euler(0f, 0f, baseAngle - t * 10f);
        }

        private Vector2 ResolveCardButtonBaseSize()
        {
            return sceneRefs.CardButtonPrefab != null ? sceneRefs.CardButtonPrefab.Root.sizeDelta : new Vector2(82f, 116f);
        }

        private Vector2 ResolveBottomHandCardSize()
        {
            return ResolveCardButtonBaseSize() * BottomHandCardSizeMultiplier;
        }

        private Vector2 ResolveOpponentHandCardSize()
        {
            return ResolveCardButtonBaseSize() * 1.13f;
        }

        private Vector2 ResolveLastTrickCardSize()
        {
            return ResolveCardButtonBaseSize() * LastTrickCardSizeMultiplier;
        }

        private Color GetSeatPanelTint(SeatId seat)
        {
            return seat.ToTeam() == TeamId.Home
                ? new Color(theme.panelColor.r, theme.panelColor.g + 0.02f, theme.panelColor.b, 0.97f)
                : new Color(theme.panelColor.r + 0.03f, theme.panelColor.g, theme.panelColor.b, 0.97f);
        }

        private void RestoreSeatRootScales()
        {
            foreach (var pair in seatViews)
            {
                if (pair.Value == null)
                {
                    continue;
                }

                pair.Value.Root.localScale = preservedSeatRootScales.TryGetValue(pair.Key, out var scale)
                    ? scale
                    : Vector3.one;
            }
        }

        private bool ShouldPreserveSeatPanelVisual(Image image)
        {
            return image != null && preservedSeatPanelVisuals.Contains(image);
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

        private Sprite ResolveGraffitiSplashSprite()
        {
            if (graffitiSplashSprite != null)
            {
                return graffitiSplashSprite;
            }

            const int width = 256;
            const int height = 128;
            var pixels = new Color32[width * height];
            for (var i = 0; i < pixels.Length; i++)
            {
                pixels[i] = new Color32(255, 255, 255, 0);
            }

            StampGraffitiEllipse(pixels, width, height, 128f, 64f, 116f, 42f, 230);
            StampGraffitiEllipse(pixels, width, height, 86f, 48f, 44f, 30f, 205);
            StampGraffitiEllipse(pixels, width, height, 174f, 78f, 52f, 28f, 210);
            StampGraffitiEllipse(pixels, width, height, 52f, 72f, 20f, 16f, 170);
            StampGraffitiEllipse(pixels, width, height, 211f, 48f, 22f, 17f, 175);
            StampGraffitiEllipse(pixels, width, height, 98f, 104f, 10f, 20f, 145);
            StampGraffitiEllipse(pixels, width, height, 155f, 106f, 8f, 18f, 130);
            for (var i = 0; i < 54; i++)
            {
                var angle = i * 2.399963f;
                var orbit = 26f + (i * 37 % 84);
                var cx = 128f + Mathf.Cos(angle) * orbit;
                var cy = 64f + Mathf.Sin(angle) * orbit * 0.46f;
                var radius = 2f + (i * 19 % 9);
                var alpha = (byte)(92 + (i * 23 % 118));
                StampGraffitiEllipse(pixels, width, height, cx, cy, radius, radius * (0.72f + (i % 4) * 0.18f), alpha);
            }

            var texture = new Texture2D(width, height, TextureFormat.ARGB32, false)
            {
                name = "Graffiti Bid Splash Runtime",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            texture.SetPixels32(pixels);
            texture.Apply();
            graffitiSplashSprite = Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(24f, 18f, 24f, 18f));
            return graffitiSplashSprite;
        }

        private static void StampGraffitiEllipse(Color32[] pixels, int width, int height, float centerX, float centerY, float radiusX, float radiusY, byte alpha)
        {
            var minX = Mathf.Clamp(Mathf.FloorToInt(centerX - radiusX), 0, width - 1);
            var maxX = Mathf.Clamp(Mathf.CeilToInt(centerX + radiusX), 0, width - 1);
            var minY = Mathf.Clamp(Mathf.FloorToInt(centerY - radiusY), 0, height - 1);
            var maxY = Mathf.Clamp(Mathf.CeilToInt(centerY + radiusY), 0, height - 1);
            for (var y = minY; y <= maxY; y++)
            {
                var dy = (y - centerY) / Mathf.Max(1f, radiusY);
                for (var x = minX; x <= maxX; x++)
                {
                    var dx = (x - centerX) / Mathf.Max(1f, radiusX);
                    var distance = dx * dx + dy * dy;
                    if (distance > 1f)
                    {
                        continue;
                    }

                    var edge = Mathf.Clamp01((1f - distance) * 2.8f);
                    var grain = 0.72f + Mathf.Abs(Mathf.Sin((x * 12.9898f + y * 78.233f) * 0.15f)) * 0.28f;
                    var value = (byte)Mathf.Clamp(Mathf.RoundToInt(alpha * edge * grain), 0, 255);
                    var index = y * width + x;
                    if (value > pixels[index].a)
                    {
                        pixels[index] = new Color32(255, 255, 255, value);
                    }
                }
            }
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

            ApplyFallbackSprite(
                chipImage,
                theme.chipSprite != null
                    ? theme.chipSprite
                    : ThemeSpriteFactory.CreateChipSprite(tint, theme.gold));
            chipImage.color = tint;
        }

        private static void ApplyFallbackSprite(Image image, Sprite sprite)
        {
            if (image == null || image.sprite != null || sprite == null)
            {
                return;
            }

            image.sprite = sprite;
            image.type = Image.Type.Sliced;
        }

        private static void ApplyThemedImage(Image image, Color color, Sprite sprite)
        {
            if (image == null)
            {
                return;
            }

            if (image.gameObject.name.StartsWith("Update3 Runtime"))
            {
                return;
            }

            ApplyFallbackSprite(image, sprite);
            image.color = color;
        }

        private Image CreateRuntimePanel(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            var rect = image.rectTransform;
            SetAnchors(rect, anchorMin, anchorMax);
            image.sprite = null;
            image.type = Image.Type.Simple;
            image.color = Color.white;
            return image;
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

        private Button CreateRuntimeButton(string name, Transform parent, string label, Color tint, Vector2 anchorMin, Vector2 anchorMax)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var button = go.GetComponent<Button>();
            SetAnchors((RectTransform)go.transform, anchorMin, anchorMax);
            button.transition = Selectable.Transition.ColorTint;
            TintButton(button, tint);
            var buttonText = CreateRuntimeText("Label", go.transform, label, 18, FontStyle.Bold, theme.backgroundColor, TextAnchor.MiddleCenter, new Vector2(0.08f, 0.12f), new Vector2(0.92f, 0.88f));
            buttonText.resizeTextForBestFit = true;
            buttonText.resizeTextMinSize = 12;
            buttonText.resizeTextMaxSize = 18;
            return button;
        }

        private static void SetButtonLabel(Button button, string label)
        {
            if (button == null)
            {
                return;
            }

            var texts = button.GetComponentsInChildren<Text>(true);
            for (var i = 0; i < texts.Length; i++)
            {
                texts[i].text = label;
            }
        }

        private void TintButton(Button button, Color tint)
        {
            if (button == null || button.image == null)
            {
                return;
            }

            ApplyFallbackSprite(
                button.image,
                theme.buttonSprite != null
                    ? theme.buttonSprite
                    : ThemeSpriteFactory.CreateRoundedRectSprite(tint, theme.backgroundSecondary, 256, 96, 22));
            button.image.color = tint;
            var label = button.GetComponentInChildren<Text>();
            if (label != null)
            {
                if (label.font == null)
                {
                    label.font = theme.ResolveFont();
                }
            }
        }

        private void TintButtonIfNotScoreboard(Button button, Color tint)
        {
            if (button == null || IsScoreboardChild(button.transform))
            {
                return;
            }

            TintButton(button, tint);
        }

        private bool IsScoreboardChild(Transform target)
        {
            return target != null &&
                   ((sceneRefs.RoundScoreboardView != null && target.IsChildOf(sceneRefs.RoundScoreboardView.transform)) ||
                    (sceneRefs.EndScoreboardView != null && target.IsChildOf(sceneRefs.EndScoreboardView.transform)));
        }

        private void PlayOptionsMenuOpenAnimation()
        {
            if (!PrepareOptionsMenuAnimationTarget())
            {
                return;
            }

            StopOptionsMenuAnimation();
            SetSheetVisible(optionsMenuAnimationTarget, true);
            optionsMenuAnimationTarget.SetAsLastSibling();
            optionsMenuAnimationLoop = StartCoroutine(AnimateOptionsMenuRoutine(true));
        }

        private void PlayOptionsMenuCloseAnimation()
        {
            if (!PrepareOptionsMenuAnimationTarget())
            {
                return;
            }

            StopOptionsMenuAnimation();
            SetSheetVisible(optionsMenuAnimationTarget, true);
            optionsMenuAnimationTarget.SetAsLastSibling();
            optionsMenuAnimationLoop = StartCoroutine(AnimateOptionsMenuRoutine(false));
        }

        private void SetOptionsMenuVisibleImmediate(bool visible)
        {
            if (!PrepareOptionsMenuAnimationTarget())
            {
                SetSheetVisible(sceneRefs != null ? sceneRefs.OptionsMenu : null, visible);
                return;
            }

            StopOptionsMenuAnimation();
            ApplyOptionsMenuVisiblePose(visible);
            SetSheetVisible(optionsMenuAnimationTarget, visible);
        }

        private void StopOptionsMenuAnimation()
        {
            if (optionsMenuAnimationLoop == null)
            {
                return;
            }

            StopCoroutine(optionsMenuAnimationLoop);
            optionsMenuAnimationLoop = null;
        }

        private bool PrepareOptionsMenuAnimationTarget()
        {
            if (sceneRefs == null || sceneRefs.OptionsMenu == null)
            {
                return false;
            }

            var menu = sceneRefs.OptionsMenu;
            if (optionsMenuAnimationTarget != menu || !optionsMenuAnimationDefaultsCaptured)
            {
                optionsMenuAnimationTarget = menu;
                optionsMenuBaseAnchoredPosition = menu.anchoredPosition;
                optionsMenuBaseScale = menu.localScale;
                optionsMenuBaseRotation = menu.localRotation;
                optionsMenuAnimationDefaultsCaptured = true;
            }

            optionsMenuCanvasGroup = menu.GetComponent<CanvasGroup>();
            if (optionsMenuCanvasGroup == null)
            {
                optionsMenuCanvasGroup = menu.gameObject.AddComponent<CanvasGroup>();
            }

            return true;
        }

        private IEnumerator AnimateOptionsMenuRoutine(bool opening)
        {
            if (!PrepareOptionsMenuAnimationTarget())
            {
                optionsMenuAnimationLoop = null;
                yield break;
            }

            var duration = opening ? OptionsMenuOpenAnimationSeconds : OptionsMenuCloseAnimationSeconds;
            var elapsed = 0f;
            if (opening)
            {
                ApplyOptionsMenuFrame(0f, OptionsMenuEntryYOffset, 0.72f, -2.5f, false, true);
            }
            else
            {
                ApplyOptionsMenuVisiblePose(false);
                ApplyOptionsMenuFrame(1f, 0f, 1f, 0f, false, true);
            }

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                if (opening)
                {
                    AnimateOptionsMenuOpenFrame(t);
                }
                else
                {
                    AnimateOptionsMenuCloseFrame(t);
                }

                yield return null;
            }

            if (opening)
            {
                ApplyOptionsMenuVisiblePose(true);
            }
            else
            {
                ApplyOptionsMenuVisiblePose(false);
                SetSheetVisible(optionsMenuAnimationTarget, false);
            }

            optionsMenuAnimationLoop = null;
        }

        private void AnimateOptionsMenuOpenFrame(float t)
        {
            var eased = EaseOutBackStrong(t);
            var alpha = SmoothStep01(Mathf.Clamp01(t / 0.62f));
            var offsetY = Mathf.LerpUnclamped(OptionsMenuEntryYOffset, 0f, eased);
            var scale = Mathf.LerpUnclamped(0.72f, 1f, eased);
            var rotation = Mathf.Sin(t * Mathf.PI * 2.25f) * (1f - t) * 3f;
            ApplyOptionsMenuFrame(alpha, offsetY, scale, rotation, false, true);
        }

        private void AnimateOptionsMenuCloseFrame(float t)
        {
            const float bumpPortion = 0.28f;
            if (t < bumpPortion)
            {
                var bumpT = EaseOutQuad(t / bumpPortion);
                ApplyOptionsMenuFrame(1f, Mathf.Lerp(0f, 6f, bumpT), Mathf.Lerp(1f, 1.06f, bumpT), Mathf.Lerp(0f, -1.2f, bumpT), false, true);
                return;
            }

            var collapseT = Mathf.Clamp01((t - bumpPortion) / (1f - bumpPortion));
            var eased = EaseInBack(collapseT);
            var alpha = 1f - SmoothStep01(collapseT);
            var offsetY = Mathf.LerpUnclamped(6f, OptionsMenuEntryYOffset * 0.65f, eased);
            var scale = Mathf.LerpUnclamped(1.06f, 0.78f, eased);
            var rotation = Mathf.LerpUnclamped(-1.2f, 3.2f, eased);
            ApplyOptionsMenuFrame(alpha, offsetY, scale, rotation, false, true);
        }

        private void ApplyOptionsMenuVisiblePose(bool visible)
        {
            if (!PrepareOptionsMenuAnimationTarget())
            {
                return;
            }

            optionsMenuAnimationTarget.anchoredPosition = optionsMenuBaseAnchoredPosition;
            optionsMenuAnimationTarget.localScale = optionsMenuBaseScale;
            optionsMenuAnimationTarget.localRotation = optionsMenuBaseRotation;
            SetOptionsMenuCanvasGroup(visible ? 1f : 0f, visible, visible);
        }

        private void ApplyOptionsMenuFrame(float alpha, float offsetY, float scaleMultiplier, float rotationZ, bool interactable, bool blocksRaycasts)
        {
            if (!PrepareOptionsMenuAnimationTarget())
            {
                return;
            }

            optionsMenuAnimationTarget.anchoredPosition = optionsMenuBaseAnchoredPosition + new Vector2(0f, offsetY);
            optionsMenuAnimationTarget.localScale = optionsMenuBaseScale * scaleMultiplier;
            optionsMenuAnimationTarget.localRotation = optionsMenuBaseRotation * Quaternion.Euler(0f, 0f, rotationZ);
            SetOptionsMenuCanvasGroup(alpha, interactable, blocksRaycasts);
        }

        private void SetOptionsMenuCanvasGroup(float alpha, bool interactable, bool blocksRaycasts)
        {
            if (optionsMenuCanvasGroup == null && !PrepareOptionsMenuAnimationTarget())
            {
                return;
            }

            optionsMenuCanvasGroup.alpha = Mathf.Clamp01(alpha);
            optionsMenuCanvasGroup.interactable = interactable;
            optionsMenuCanvasGroup.blocksRaycasts = blocksRaycasts;
        }

        private static float EaseOutQuad(float t)
        {
            t = Mathf.Clamp01(t);
            return 1f - (1f - t) * (1f - t);
        }

        private static float EaseOutBackStrong(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            t = Mathf.Clamp01(t) - 1f;
            return 1f + c3 * t * t * t + c1 * t * t;
        }

        private static float EaseInBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            t = Mathf.Clamp01(t);
            return c3 * t * t * t - c1 * t * t;
        }

        private static float SmoothStep01(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        private void SetBidSheetVisible(bool visible)
        {
            var wasVisible = bidSheetWasVisible &&
                             sceneRefs.BidSheet != null &&
                             sceneRefs.BidSheet.gameObject.activeSelf;
            SetSheetVisible(sceneRefs.BidSheet, visible);
            var isVisible = sceneRefs.BidSheet != null && sceneRefs.BidSheet.gameObject.activeSelf;
            if (isVisible && !wasVisible)
            {
                PlayFeedback(FeedbackCue.BidPanelOpen, 0.24f);
            }

            bidSheetWasVisible = isVisible;
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
