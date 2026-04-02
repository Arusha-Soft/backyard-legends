using System.Collections.Generic;
using System.Linq;
using BackyardLegends.Core;
using UnityEngine;
using UnityEngine.UI;

namespace BackyardLegends.Runtime
{
    public sealed class BackyardLegendsLobbyPresenter : MonoBehaviour
    {
        [SerializeField] private BackyardLegendsLobbySceneRefs sceneRefs;
        [SerializeField] private ThemeConfig themeOverride;

        private readonly List<Button> modeButtons = new();
        private readonly List<Button> targetButtons = new();

        private BackyardLegendsSession session;
        private ThemeConfig theme;

        private void Awake()
        {
            Screen.orientation = ScreenOrientation.Portrait;
            Application.targetFrameRate = 60;

            session = BackyardLegendsSession.GetOrCreateRuntimeInstance();
            theme = themeOverride != null ? themeOverride : session.Theme ?? ThemeConfig.CreateFallback();
            sceneRefs = sceneRefs != null ? sceneRefs : GetComponent<BackyardLegendsLobbySceneRefs>();
            if (sceneRefs == null)
            {
                sceneRefs = FindFirstObjectByType<BackyardLegendsLobbySceneRefs>();
            }

            if (sceneRefs == null)
            {
                Debug.LogError("Backyard Legends lobby refs are missing. Rebuild the authored lobby scene.");
                enabled = false;
                return;
            }

            CacheButtons();
            ConfigureUiCallbacks();
            ApplyTheme();
            RefreshContent();
        }

        private void CacheButtons()
        {
            modeButtons.Clear();
            targetButtons.Clear();

            if (sceneRefs.ModeButtons != null)
            {
                modeButtons.AddRange(sceneRefs.ModeButtons.Where(button => button != null));
            }

            if (sceneRefs.TargetButtons != null)
            {
                targetButtons.AddRange(sceneRefs.TargetButtons.Where(button => button != null));
            }
        }

        private void ConfigureUiCallbacks()
        {
            sceneRefs.StartMatchButton.onClick.RemoveAllListeners();
            sceneRefs.StartMatchButton.onClick.AddListener(() => session.LoadGameplayScene());

            for (var i = 0; i < modeButtons.Count; i++)
            {
                var localIndex = i;
                modeButtons[i].onClick.RemoveAllListeners();
                modeButtons[i].onClick.AddListener(() =>
                {
                    session.SelectMode(localIndex);
                    RefreshContent();
                });
            }

            for (var i = 0; i < targetButtons.Count; i++)
            {
                var localIndex = i;
                targetButtons[i].onClick.RemoveAllListeners();
                targetButtons[i].onClick.AddListener(() =>
                {
                    var options = session.GetTargetOptions();
                    if (localIndex < options.Length)
                    {
                        session.SelectTarget(options[localIndex]);
                        RefreshContent();
                    }
                });
            }
        }

        private void ApplyTheme()
        {
            sceneRefs.BackgroundImage.sprite = theme.tableBackgroundSprite != null
                ? theme.tableBackgroundSprite
                : ThemeSpriteFactory.CreateBackgroundSprite(theme.backgroundSecondary, theme.backgroundColor);
            sceneRefs.BackgroundImage.color = Color.white;
            sceneRefs.SheetImage.sprite = theme.sheetSprite != null
                ? theme.sheetSprite
                : ThemeSpriteFactory.CreateRoundedRectSprite(theme.panelColor, theme.panelStroke, 512, 768, 28);
            sceneRefs.SheetImage.color = new Color(0.15f, 0.16f, 0.18f, 0.98f);
            sceneRefs.PreviewPanelImage.sprite = theme.softPanelSprite != null
                ? theme.softPanelSprite
                : ThemeSpriteFactory.CreateRoundedRectSprite(new Color(0f, 0f, 0f, 0.08f), theme.panelStroke, 512, 256, 22);
            sceneRefs.PreviewPanelImage.color = new Color(1f, 1f, 1f, 0.22f);
            sceneRefs.HeroCardImage.sprite = theme.cardBackHeroSprite != null
                ? theme.cardBackHeroSprite
                : ThemeSpriteFactory.CreateRoundedRectSprite(theme.cardBack, theme.gold, 180, 260, 24);
            sceneRefs.HeroCardImage.color = Color.white;

            ApplyThemeText(sceneRefs.TitleText, theme.gold, 46, FontStyle.Bold);
            ApplyThemeText(sceneRefs.SubtitleText, theme.primaryText, 22, FontStyle.Normal);
            ApplyThemeText(sceneRefs.FlavorText, theme.mutedText, 22, FontStyle.Normal);
            ApplyThemeText(sceneRefs.RuleSummaryText, theme.primaryText, 22, FontStyle.Normal);
            ApplyThemeText(sceneRefs.SelectionSummaryText, theme.mutedText, 20, FontStyle.Bold);

            TintButton(sceneRefs.StartMatchButton, theme.green);
            foreach (var button in modeButtons)
            {
                TintButton(button, theme.panelStroke);
            }

            foreach (var button in targetButtons)
            {
                TintButton(button, theme.panelStroke);
            }
        }

        private void RefreshContent()
        {
            var labels = session.GetModeLabels();
            for (var i = 0; i < modeButtons.Count; i++)
            {
                var label = modeButtons[i].GetComponentInChildren<Text>();
                if (label != null)
                {
                    label.text = i < labels.Length ? labels[i].ToUpperInvariant() : $"MODE {i + 1}";
                }
            }

            var targetOptions = session.GetTargetOptions();
            for (var i = 0; i < targetButtons.Count; i++)
            {
                var button = targetButtons[i];
                button.gameObject.SetActive(i < targetOptions.Length);
                if (i >= targetOptions.Length)
                {
                    continue;
                }

                var label = button.GetComponentInChildren<Text>();
                if (label != null)
                {
                    label.text = targetOptions[i].ToString();
                }
            }

            var selectedRule = session.SelectedRule;
            sceneRefs.RuleSummaryText.text =
                $"{selectedRule.DisplayName} mode locks the core single-player loop first.\n" +
                $"{(selectedRule.FollowSuitRequired ? "Follow suit stays hot." : "Street lets you throw off-suit.")}\n" +
                $"{(selectedRule.RenegePenaltyEnabled ? "Reneges trigger a -200 penalty." : "Classic keeps the table strict without a renege penalty hook.")}";
            sceneRefs.SelectionSummaryText.text =
                $"Portrait-first | Human vs 3 AI | Race to {selectedRule.TargetScore}\n" +
                "Dark table, gold calls, green wins, red pressure.";
            sceneRefs.FlavorText.text =
                "Backyard energy over casino polish. Short, sharp rounds with enough swagger to sell the street-table tone.";

            UpdateSelectionButtons();
        }

        private void UpdateSelectionButtons()
        {
            for (var i = 0; i < modeButtons.Count; i++)
            {
                TintButton(modeButtons[i], i == session.SelectedModeIndex ? theme.gold : theme.panelStroke);
            }

            foreach (var button in targetButtons)
            {
                var label = button.GetComponentInChildren<Text>();
                var isSelected = label != null && int.TryParse(label.text, out var score) && score == session.SelectedTargetScore;
                TintButton(button, isSelected ? theme.gold : theme.panelStroke);
            }
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

        private void TintButton(Button button, Color tint)
        {
            if (button == null || button.image == null)
            {
                return;
            }

            button.image.sprite = theme.buttonSprite != null
                ? theme.buttonSprite
                : ThemeSpriteFactory.CreateRoundedRectSprite(tint, theme.backgroundSecondary, 256, 96, 22);
            button.image.color = tint;
            var label = button.GetComponentInChildren<Text>();
            if (label != null)
            {
                label.font = theme.ResolveFont();
                label.color = theme.backgroundColor;
                label.fontStyle = FontStyle.Bold;
            }
        }
    }
}
