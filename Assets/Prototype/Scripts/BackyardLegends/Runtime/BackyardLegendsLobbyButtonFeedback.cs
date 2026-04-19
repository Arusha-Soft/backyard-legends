using System;
using System.Collections;
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
        private ThemeConfig theme;
        private Action hoverCallback;
        private Action selectCallback;
        private Color baseImageColor;
        private Color baseLabelColor;
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
            label = label != null ? label : GetComponentInChildren<Text>(true);
            theme = themeConfig;

            if (!initialized)
            {
                baseImageColor = image != null ? image.color : Color.white;
                baseLabelColor = label != null ? label.color : Color.black;
                baseLabelStyle = label != null ? label.fontStyle : FontStyle.Bold;
                initialized = true;
            }

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
                image.color = ResolveImageColor();
            }

            if (label != null)
            {
                label.color = selected ? selectedLabelColor : baseLabelColor;
                label.fontStyle = selected || hovered ? FontStyle.Bold : baseLabelStyle;
            }
        }

        private Color ResolveImageColor()
        {
            if (!initialized)
            {
                return Color.white;
            }

            var resolved = baseImageColor;
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

        private Color ResolveReadableSelectedLabelColor()
        {
            var imageColor = image != null ? image.color : Color.white;
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
    }
}
