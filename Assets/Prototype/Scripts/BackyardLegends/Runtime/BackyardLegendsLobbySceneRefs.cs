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

        public RectTransform LobbySheetRect => SheetImage != null ? SheetImage.rectTransform : null;
        public RectTransform PreviewPanelRect => PreviewPanelImage != null ? PreviewPanelImage.rectTransform : null;
        public RectTransform ModeRowRect => ModeButtons != null && ModeButtons.Length > 0 && ModeButtons[0] != null
            ? ModeButtons[0].transform.parent as RectTransform
            : null;
        public RectTransform ScoreRowRect => TargetButtons != null && TargetButtons.Length > 0 && TargetButtons[0] != null
            ? TargetButtons[0].transform.parent as RectTransform
            : null;

        public void ResolveMissingReferences()
        {
            BackgroundImage = BackgroundImage != null ? BackgroundImage : FindByPath<Image>("Background");
            SheetImage = SheetImage != null ? SheetImage : FindByPath<Image>("Lobby Sheet");
            PreviewPanelImage = PreviewPanelImage != null ? PreviewPanelImage : FindByPath<Image>("Lobby Sheet/Preview Panel");
            SubtitleText = SubtitleText != null ? SubtitleText : FindByPath<Text>("Lobby Sheet/Subtitle");
            FlavorText = FlavorText != null ? FlavorText : FindByPath<Text>("Lobby Sheet/Preview Panel/Flavor");
            RuleSummaryText = RuleSummaryText != null ? RuleSummaryText : FindByPath<Text>("Lobby Sheet/Preview Panel/Rule Summary");
            SelectionSummaryText = SelectionSummaryText != null ? SelectionSummaryText : FindByPath<Text>("Lobby Sheet/Selection Summary");
            StartMatchButton = StartMatchButton != null ? StartMatchButton : FindByPath<Button>("Lobby Sheet/Start Match");
            ModeButtons = ResolveButtons(
                ModeButtons,
                "Lobby Sheet/Mode Row/Classic Mode",
                "Lobby Sheet/Mode Row/Street Mode");
            TargetButtons = ResolveButtons(
                TargetButtons,
                "Lobby Sheet/Score Row/Score 100",
                "Lobby Sheet/Score Row/Score 200",
                "Lobby Sheet/Score Row/Score 500");
        }

        private T FindByPath<T>(string path) where T : Component
        {
            var target = transform.Find(path);
            return target != null ? target.GetComponent<T>() : null;
        }

        private Button[] ResolveButtons(Button[] existing, params string[] paths)
        {
            var resolved = new Button[paths.Length];
            for (var i = 0; i < paths.Length; i++)
            {
                if (existing != null && i < existing.Length && existing[i] != null)
                {
                    resolved[i] = existing[i];
                    continue;
                }

                resolved[i] = FindByPath<Button>(paths[i]);
            }

            return resolved;
        }
    }
}
