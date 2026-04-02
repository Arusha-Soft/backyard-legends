using UnityEngine;
using UnityEngine.UI;

namespace BackyardLegends.Runtime
{
    public sealed class BackyardLegendsSceneRefs : MonoBehaviour
    {
        [Header("Theme Surfaces")]
        public Image BackgroundImage;
        public Image HudPanel;
        public Image TablePanel;
        public Image HandPanel;
        public Image FeedPanel;
        public Image DeckAnchorImage;
        public Image DiscardAnchorImage;
        public Image OpeningStackImage;
        public Image BidSheetImage;
        public Image RoundSheetImage;
        public Image EndSheetImage;
        public Image ExitPromptOverlayImage;
        public Image ExitPromptPanelImage;

        [Header("Texts")]
        public Text StatusText;
        public Text HudModeText;
        public Text TimerHookText;
        public Text HomeScoreText;
        public Text AwayScoreText;
        public Text HomeDeltaText;
        public Text AwayDeltaText;
        public Text LastTrickText;
        public Text FeedText;
        public Text CenterHintText;
        public Text DeckAnchorText;
        public Text DiscardAnchorText;
        public Text OpeningStackText;
        public Text RoundSummaryText;
        public Text EndSummaryText;
        public Text BannerText;
        public Text ExitPromptTitleText;
        public Text ExitPromptBodyText;

        [Header("Containers")]
        public RectTransform BidSheet;
        public RectTransform RoundSheet;
        public RectTransform EndSheet;
        public RectTransform ExitPromptOverlay;
        public RectTransform HandContent;

        [Header("Buttons")]
        public Button BackButton;
        public Button NextRoundButton;
        public Button RematchButton;
        public Button ReturnToLobbyButton;
        public Button DealButton;
        public Button PlaySelectedButton;
        public Button ExitPromptCancelButton;
        public Button ExitPromptConfirmButton;
        public Button[] BidButtons;

        [Header("Views")]
        public SeatPanelView BottomSeat;
        public SeatPanelView LeftSeat;
        public SeatPanelView TopSeat;
        public SeatPanelView RightSeat;
        public TrickSlotView BottomTrick;
        public TrickSlotView LeftTrick;
        public TrickSlotView TopTrick;
        public TrickSlotView RightTrick;
        public CardButtonView CardButtonPrefab;
    }
}
