using System;
using System.Linq;
using BackyardLegends.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BackyardLegends.Runtime
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    public sealed class BackyardLegendsLobbyButtonFeedback : MonoBehaviour,
        IPointerEnterHandler,
        IPointerExitHandler,
        IPointerDownHandler,
        IPointerUpHandler,
        IPointerClickHandler
    {
        private Button button;
        private Image image;
        private Text label;
        private CanvasGroup selectedIndicatorGroup;
        private Image selectedFrameImage;
        private Text selectedCheckmarkText;
        private ThemeConfig theme;
        private Action hoverCallback;
        private Action selectCallback;
        private Color baseImageColor;
        private Color baseLabelColor;
        private Color selectedImageColor;
        private Color selectedLabelColor;
        private FontStyle baseLabelStyle;
        private bool initialized;
        private bool selected;
        private bool hovered;
        private bool pressed;

        public void Initialize(ThemeConfig themeConfig)
        {
            button = button != null ? button : GetComponent<Button>();
            image = image != null ? image : GetComponent<Image>();
            label = label != null ? label : ResolveLabel();
            theme = themeConfig;

            if (!initialized)
            {
                baseImageColor = image != null ? image.color : Color.white;
                baseLabelColor = label != null ? label.color : Color.black;
                baseLabelStyle = label != null ? label.fontStyle : FontStyle.Bold;
                initialized = true;
            }

            EnsureButtonSprite();
            EnsureSelectedIndicator();
            selectedImageColor = ResolveSelectedImageColor();
            selectedLabelColor = ResolveReadableSelectedLabelColor();
            ApplyVisualState(true);
        }

        public void SetCallbacks(Action onHover, Action onSelect)
        {
            hoverCallback = onHover;
            selectCallback = onSelect;
        }

        public void SetSelected(bool isSelected, bool immediate = false)
        {
            selected = isSelected;
            ApplyVisualState(immediate);
        }

        public void Kick(float intensity)
        {
            ApplyVisualState(true);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!IsInteractive())
            {
                return;
            }

            hovered = true;
            hoverCallback?.Invoke();
            ApplyVisualState(false);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            hovered = false;
            pressed = false;
            ApplyVisualState(false);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!IsInteractive())
            {
                return;
            }

            pressed = true;
            ApplyVisualState(false);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            pressed = false;
            ApplyVisualState(false);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!IsInteractive())
            {
                return;
            }

            selectCallback?.Invoke();
        }

        private void OnDisable()
        {
            hovered = false;
            pressed = false;
        }

        private bool IsInteractive()
        {
            return button != null && button.IsInteractable() && isActiveAndEnabled;
        }

        private void ApplyVisualState(bool immediate)
        {
            if (!initialized)
            {
                return;
            }

            if (image != null)
            {
                EnsureButtonSprite();
                image.color = ResolveImageColor();
            }

            if (label != null)
            {
                label.color = selected ? selectedLabelColor : baseLabelColor;
                label.fontStyle = selected || hovered ? FontStyle.Bold : baseLabelStyle;
            }

            if (selectedIndicatorGroup != null)
            {
                selectedIndicatorGroup.alpha = selected ? 1f : 0f;
                selectedIndicatorGroup.gameObject.SetActive(selected);
            }

            if (selectedFrameImage != null)
            {
                if (selected && selectedFrameImage.sprite == null)
                {
                    selectedFrameImage.sprite = ResolveSelectedFrameSprite();
                }

                var canShowFrame = selected && selectedFrameImage.sprite != null;
                selectedFrameImage.enabled = canShowFrame;
                selectedFrameImage.color = canShowFrame ? Color.white : Color.clear;
            }

            if (selectedCheckmarkText != null)
            {
                selectedCheckmarkText.gameObject.SetActive(selected);
            }
        }

        private Color ResolveImageColor()
        {
            if (!initialized)
            {
                return Color.white;
            }

            var resolved = baseImageColor;
            if (selected)
            {
                resolved = selectedImageColor;
            }

            if (hovered)
            {
                resolved = Color.Lerp(resolved, Color.white, 0.05f);
            }

            if (pressed)
            {
                resolved = Color.Lerp(resolved, Color.black, 0.08f);
            }

            return resolved;
        }

        private void EnsureSelectedIndicator()
        {
            if (selectedIndicatorGroup != null)
            {
                if (selectedFrameImage == null)
                {
                    selectedFrameImage = selectedIndicatorGroup.GetComponent<Image>();
                }

                if (selectedFrameImage != null && selectedFrameImage.sprite == null)
                {
                    selectedFrameImage.sprite = ResolveSelectedFrameSprite();
                }

                return;
            }

            var indicator = transform.Find("Selected Indicator");
            GameObject indicatorObject;
            if (indicator != null)
            {
                indicatorObject = indicator.gameObject;
            }
            else
            {
                indicatorObject = new GameObject("Selected Indicator", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
                indicatorObject.transform.SetParent(transform, false);
            }

            var indicatorRect = indicatorObject.GetComponent<RectTransform>();
            Stretch(indicatorRect, -4f);
            indicatorRect.SetAsFirstSibling();

            selectedIndicatorGroup = indicatorObject.GetComponent<CanvasGroup>();
            selectedIndicatorGroup.blocksRaycasts = false;
            selectedIndicatorGroup.interactable = false;

            selectedFrameImage = indicatorObject.GetComponent<Image>();
            selectedFrameImage.raycastTarget = false;
            selectedFrameImage.type = Image.Type.Simple;
            selectedFrameImage.sprite = ResolveSelectedFrameSprite();

            selectedCheckmarkText = selectedCheckmarkText != null
                ? selectedCheckmarkText
                : transform.Find("Selected Checkmark")?.GetComponent<Text>();
            if (selectedCheckmarkText == null)
            {
                var checkmarkObject = new GameObject("Selected Checkmark", typeof(RectTransform), typeof(Text));
                checkmarkObject.transform.SetParent(transform, false);
                selectedCheckmarkText = checkmarkObject.GetComponent<Text>();
            }

            selectedCheckmarkText.raycastTarget = false;
            selectedCheckmarkText.text = "\u2713";
            selectedCheckmarkText.alignment = TextAnchor.MiddleCenter;
            selectedCheckmarkText.fontStyle = FontStyle.Bold;
            selectedCheckmarkText.fontSize = 24;
            selectedCheckmarkText.font = label != null && label.font != null
                ? label.font
                : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            selectedCheckmarkText.color = theme != null ? theme.backgroundColor : new Color(0.05f, 0.05f, 0.05f, 1f);
            selectedCheckmarkText.transform.SetAsLastSibling();
            SetAnchors(selectedCheckmarkText.rectTransform, new Vector2(0.76f, 0.54f), new Vector2(0.98f, 0.96f));
        }

        private void EnsureButtonSprite()
        {
            if (image == null || image.sprite != null)
            {
                return;
            }

            image.sprite = theme != null && theme.buttonSprite != null
                ? theme.buttonSprite
                : ThemeSpriteFactory.CreateRoundedRectSprite(
                    baseImageColor,
                    theme != null ? theme.panelStroke : new Color(0.68f, 0.61f, 0.52f, 1f),
                    256,
                    96,
                    22);
            image.type = Image.Type.Sliced;
        }

        private Sprite ResolveSelectedFrameSprite()
        {
            return ThemeSpriteFactory.CreateRoundedRectSprite(
                new Color(1f, 0.84f, 0.38f, 0.18f),
                theme != null ? theme.highlight : new Color(1f, 0.84f, 0.38f, 1f),
                192,
                96,
                20);
        }

        private Text ResolveLabel()
        {
            var labelTransform = transform.Find("Label");
            return labelTransform != null
                ? labelTransform.GetComponent<Text>()
                : GetComponentsInChildren<Text>(true).FirstOrDefault(text => text.name != "Selected Checkmark");
        }

        private Color ResolveSelectedImageColor()
        {
            var accent = theme != null ? theme.highlight : new Color(1f, 0.84f, 0.38f, 1f);
            accent.a = baseImageColor.a;
            return Color.Lerp(baseImageColor, accent, 0.82f);
        }

        private Color ResolveReadableSelectedLabelColor()
        {
            var imageColor = selectedImageColor;
            var darkCandidate = theme != null
                ? Color.Lerp(theme.backgroundColor, Color.black, 0.45f)
                : new Color(0.03f, 0.03f, 0.03f, 1f);
            darkCandidate.a = 1f;

            var lightCandidate = theme != null ? theme.primaryText : Color.white;
            lightCandidate.a = 1f;

            var bestColor = baseLabelColor;
            var bestContrast = Contrast(baseLabelColor, imageColor);

            var darkContrast = Contrast(darkCandidate, imageColor);
            if (darkContrast > bestContrast)
            {
                bestColor = darkCandidate;
                bestContrast = darkContrast;
            }

            var lightContrast = Contrast(lightCandidate, imageColor);
            if (lightContrast > bestContrast)
            {
                bestColor = lightCandidate;
            }

            return bestColor;
        }

        private static float Contrast(Color a, Color b)
        {
            var luminanceA = RelativeLuminance(a) + 0.05f;
            var luminanceB = RelativeLuminance(b) + 0.05f;
            return Mathf.Max(luminanceA, luminanceB) / Mathf.Min(luminanceA, luminanceB);
        }

        private static float RelativeLuminance(Color color)
        {
            return 0.2126f * color.r + 0.7152f * color.g + 0.0722f * color.b;
        }

        private static void Stretch(RectTransform rectTransform, float inset)
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = new Vector2(inset, inset);
            rectTransform.offsetMax = new Vector2(-inset, -inset);
        }

        private static void SetAnchors(RectTransform rectTransform, Vector2 anchorMin, Vector2 anchorMax)
        {
            rectTransform.anchorMin = anchorMin;
            rectTransform.anchorMax = anchorMax;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
        }
    }
}
