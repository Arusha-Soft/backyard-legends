using UnityEngine;

namespace BackyardLegends.Core
{
    [CreateAssetMenu(fileName = "ThemeConfig", menuName = "Backyard Legends/Theme Config")]
    public sealed class ThemeConfig : ScriptableObject
    {
        [Header("Palette")]
        public Color backgroundColor = new(0.07f, 0.08f, 0.09f, 1f);
        public Color backgroundSecondary = new(0.11f, 0.12f, 0.13f, 1f);
        public Color panelColor = new(0.13f, 0.14f, 0.16f, 0.93f);
        public Color panelStroke = new(0.23f, 0.19f, 0.11f, 1f);
        public Color gold = new(0.89f, 0.73f, 0.25f, 1f);
        public Color red = new(0.82f, 0.24f, 0.2f, 1f);
        public Color green = new(0.23f, 0.72f, 0.48f, 1f);
        public Color mutedText = new(0.66f, 0.67f, 0.7f, 1f);
        public Color primaryText = new(0.95f, 0.95f, 0.95f, 1f);
        public Color cardFace = new(0.96f, 0.94f, 0.9f, 1f);
        public Color cardBack = new(0.16f, 0.12f, 0.1f, 1f);
        public Color highlight = new(1f, 0.84f, 0.38f, 1f);

        [Header("Authored Art")]
        public Sprite tableBackgroundSprite;
        public Sprite panelSprite;
        public Sprite sheetSprite;
        public Sprite softPanelSprite;
        public Sprite buttonSprite;
        public Sprite chipSprite;
        public Sprite cardFaceDefaultSprite;
        public Sprite cardFacePlayableSprite;
        public Sprite cardFaceSelectedSprite;
        public Sprite cardFaceMutedSprite;
        public Sprite cardBackHeroSprite;

        [Header("Layout")]
        [Range(4f, 40f)] public float baseSpacing = 14f;
        [Range(4f, 32f)] public float cardLiftAmount = 26f;
        [Range(1f, 1.25f)] public float selectedCardScale = 1.08f;
        [Range(0.8f, 1.3f)] public float activePulseScale = 1.05f;

        [Header("Motion")]
        [Range(0.1f, 0.6f)] public float modalDuration = 0.22f;
        [Range(0.1f, 0.4f)] public float pulseDuration = 0.3f;
        [Range(0.05f, 0.35f)] public float shakeDuration = 0.18f;
        [Range(0.4f, 2f)] public float bannerDuration = 1.2f;

        public Font ResolveFont()
        {
            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        public static ThemeConfig CreateFallback()
        {
            var instance = CreateInstance<ThemeConfig>();
            instance.name = "Theme_Fallback";
            return instance;
        }
    }
}
