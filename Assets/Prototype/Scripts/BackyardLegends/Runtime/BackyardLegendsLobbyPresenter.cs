using System.Collections;
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
        private readonly Dictionary<Button, BackyardLegendsLobbyButtonFeedback> buttonFeedback = new();
        private readonly List<RectTransform> modeSelectionMarkers = new();

        private BackyardLegendsSession session;
        private ThemeConfig theme;
        private AudioSource feedbackAudioSource;
        private AudioClip hoverClip;
        private AudioClip selectClip;
        private AudioClip confirmClip;

        private enum FeedbackCue
        {
            Hover,
            Select,
            Confirm
        }

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

            sceneRefs.ResolveMissingReferences();
            CacheButtons();
            if (!HasRequiredReferences())
            {
                Debug.LogError("Backyard Legends lobby refs are incomplete. Resolve the missing authored objects before entering play mode.");
                enabled = false;
                return;
            }

            CacheModeSelectionMarkers();
            ConfigureFeedbackAudio();
            ConfigureUiCallbacks();
            ApplyTheme();
            ConfigureButtonFeedback();
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

        private bool HasRequiredReferences()
        {
            return sceneRefs.BackgroundImage != null &&
                   sceneRefs.SheetImage != null &&
                   sceneRefs.PreviewPanelImage != null &&
                   sceneRefs.SubtitleText != null &&
                   sceneRefs.FlavorText != null &&
                   sceneRefs.RuleSummaryText != null &&
                   sceneRefs.SelectionSummaryText != null &&
                   sceneRefs.StartMatchButton != null &&
                   modeButtons.Count > 0 &&
                   targetButtons.Count > 0;
        }

        private void CacheModeSelectionMarkers()
        {
            modeSelectionMarkers.Clear();
            for (var i = 0; i < modeButtons.Count; i++)
            {
                var marker = ResolveModeSelectionMarker(modeButtons[i], i);
                modeSelectionMarkers.Add(marker != null ? marker.rectTransform : null);
            }
        }

        private void ConfigureUiCallbacks()
        {
            sceneRefs.StartMatchButton.onClick.RemoveAllListeners();
            sceneRefs.StartMatchButton.onClick.AddListener(() =>
            {
                PlayFeedback(FeedbackCue.Confirm, 0.95f);
                KickButton(sceneRefs.StartMatchButton, 0.7f);
                session.LoadGameplayScene();
            });

            for (var i = 0; i < modeButtons.Count; i++)
            {
                var localIndex = i;
                modeButtons[i].onClick.RemoveAllListeners();
                modeButtons[i].onClick.AddListener(() =>
                {
                    session.SelectMode(localIndex);
                    RefreshContent();
                    PlayModeSelectionFeedback(localIndex);
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
                        PlayTargetSelectionFeedback(targetButtons[localIndex]);
                    }
                });
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
            feedbackAudioSource.volume = 0.16f;

            hoverClip = CreateToneClip("Lobby Hover Cue", 680f, 920f, 0.03f, 0.05f);
            selectClip = CreateToneClip("Lobby Select Cue", 500f, 760f, 0.05f, 0.09f);
            confirmClip = CreateToneClip("Lobby Confirm Cue", 400f, 620f, 0.11f, 0.13f);
        }

        private void ApplyTheme()
        {
            EnsureFallbackImage(
                sceneRefs.BackgroundImage,
                theme.tableBackgroundSprite != null
                    ? theme.tableBackgroundSprite
                    : ThemeSpriteFactory.CreateBackgroundSprite(theme.backgroundSecondary, theme.backgroundColor));
            EnsureFallbackImage(sceneRefs.SheetImage, ResolveSheetSprite());
            EnsureFallbackImage(sceneRefs.PreviewPanelImage, ResolveSoftPanelSprite());
            EnsureFallbackImage(sceneRefs.HeroCardImage, ResolveCardBackSprite());

            EnsureFont(sceneRefs.TitleText);
            EnsureFont(sceneRefs.SubtitleText);
            EnsureFont(sceneRefs.FlavorText);
            EnsureFont(sceneRefs.RuleSummaryText);
            EnsureFont(sceneRefs.SelectionSummaryText);
            EnsureButtonFont(sceneRefs.StartMatchButton);
            foreach (var button in modeButtons)
            {
                EnsureButtonFont(button);
            }

            foreach (var button in targetButtons)
            {
                EnsureButtonFont(button);
            }
        }

        private void ConfigureButtonFeedback()
        {
            buttonFeedback.Clear();

            ConfigureButtonFeedback(sceneRefs.StartMatchButton, true);
            foreach (var button in modeButtons)
            {
                ConfigureButtonFeedback(button);
            }

            foreach (var button in targetButtons)
            {
                ConfigureButtonFeedback(button);
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
            if (sceneRefs.RuleSummaryText != null)
            {
                sceneRefs.RuleSummaryText.text =
                    $"{selectedRule.DisplayName} mode locks the core single-player loop first.\n" +
                    $"{(selectedRule.FollowSuitRequired ? "Follow suit stays hot." : "Street lets you throw off-suit.")}\n" +
                    $"{(selectedRule.RenegePenaltyEnabled ? "Reneges trigger a -200 penalty." : "Classic keeps the table strict without a renege penalty hook.")}";
            }

            if (sceneRefs.SelectionSummaryText != null)
            {
                sceneRefs.SelectionSummaryText.text =
                    $"Portrait-first | Human vs 3 AI | Race to {selectedRule.TargetScore}\n" +
                    "Dark table, gold calls, green wins, red pressure.";
            }

            if (sceneRefs.FlavorText != null)
            {
                sceneRefs.FlavorText.text =
                    "Backyard energy over casino polish. Short, sharp rounds with enough swagger to sell the street-table tone.";
            }

            UpdateSelectionButtons();
        }

        private void UpdateSelectionButtons()
        {
            for (var i = 0; i < modeButtons.Count; i++)
            {
                var isSelected = i == session.SelectedModeIndex;
                SyncButtonFeedback(modeButtons[i], isSelected);
                SetModeSelectionMarkerState(i, isSelected);
            }

            foreach (var button in targetButtons)
            {
                var label = button.GetComponentInChildren<Text>();
                var isSelected = label != null && int.TryParse(label.text, out var score) && score == session.SelectedTargetScore;
                SyncButtonFeedback(button, isSelected);
            }
        }

        private void EnsureFont(Text label)
        {
            if (label == null)
            {
                return;
            }

            if (label.font == null)
            {
                label.font = theme.ResolveFont();
            }
        }

        private void EnsureButtonFont(Button button)
        {
            if (button == null)
            {
                return;
            }

            button.transition = Selectable.Transition.None;
            EnsureFont(button.GetComponentInChildren<Text>());
        }

        private void ConfigureButtonFeedback(Button button, bool isConfirmButton = false)
        {
            if (button == null)
            {
                return;
            }

            var feedback = button.GetComponent<BackyardLegendsLobbyButtonFeedback>();
            if (feedback == null)
            {
                feedback = button.gameObject.AddComponent<BackyardLegendsLobbyButtonFeedback>();
            }

            feedback.Initialize(theme);
            feedback.SetCallbacks(
                () => PlayFeedback(FeedbackCue.Hover, 0.55f),
                isConfirmButton ? null : () => PlayFeedback(FeedbackCue.Select, 0.75f));
            buttonFeedback[button] = feedback;
        }

        private void SyncButtonFeedback(Button button, bool isSelected)
        {
            if (button == null || !buttonFeedback.TryGetValue(button, out var feedback))
            {
                return;
            }

            feedback.SetSelected(isSelected, true);
        }

        private void PlayModeSelectionFeedback(int selectedIndex)
        {
            if (selectedIndex < 0 || selectedIndex >= modeButtons.Count)
            {
                return;
            }

            KickButton(modeButtons[selectedIndex], 0.9f);
        }

        private void PlayTargetSelectionFeedback(Button selectedButton)
        {
            KickButton(selectedButton, 0.75f);
        }

        private void KickButton(Button button, float intensity)
        {
            if (button != null && buttonFeedback.TryGetValue(button, out var feedback))
            {
                feedback.Kick(intensity);
            }
        }

        private void SetModeSelectionMarkerState(int index, bool isSelected)
        {
            if (index < 0 || index >= modeSelectionMarkers.Count || modeSelectionMarkers[index] == null)
            {
                return;
            }

            modeSelectionMarkers[index].gameObject.SetActive(isSelected);
        }

        private void PlayFeedback(FeedbackCue cue, float volumeScale = 1f)
        {
            if (feedbackAudioSource == null)
            {
                return;
            }

            var clip = cue switch
            {
                FeedbackCue.Hover => hoverClip,
                FeedbackCue.Select => selectClip,
                FeedbackCue.Confirm => confirmClip,
                _ => null
            };

            if (clip != null)
            {
                feedbackAudioSource.PlayOneShot(clip, Mathf.Clamp(volumeScale, 0f, 1f));
            }
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
                var harmonic = Mathf.Sin(2f * Mathf.PI * frequencyB * t) * 0.35f;
                samples[i] = (main + harmonic) * envelope * volume;
            }

            var clip = AudioClip.Create(clipName, sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static Image ResolveModeSelectionMarker(Button button, int index)
        {
            if (button == null)
            {
                return null;
            }

            var preferredName = index == 0 ? "Selected1" : "Selected2";
            var marker = button.transform.Find(preferredName);
            if (marker == null)
            {
                marker = button.transform.Cast<Transform>().FirstOrDefault(child => child.name.StartsWith("Selected"));
            }

            return marker != null ? marker.GetComponent<Image>() : null;
        }

        private void EnsureFallbackImage(Image image, Sprite fallback)
        {
            if (image == null || image.sprite != null || fallback == null)
            {
                return;
            }

            image.sprite = fallback;
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
                : ThemeSpriteFactory.CreateRoundedRectSprite(theme.cardBack, theme.gold, 180, 260, 24);
        }

    }
}
