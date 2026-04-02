using UnityEngine;
using UnityEngine.UI;

namespace BackyardLegends.Runtime
{
    public sealed class BackyardLegendsLobbySceneRefs : MonoBehaviour
    {
        [Header("Theme Surfaces")]
        public Image BackgroundImage;
        public Image SheetImage;
        public Image PreviewPanelImage;
        public Image HeroCardImage;

        [Header("Texts")]
        public Text TitleText;
        public Text SubtitleText;
        public Text FlavorText;
        public Text RuleSummaryText;
        public Text SelectionSummaryText;

        [Header("Buttons")]
        public Button StartMatchButton;
        public Button[] ModeButtons;
        public Button[] TargetButtons;
    }
}
