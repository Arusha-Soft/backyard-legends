using System.Collections.Generic;
using BackyardLegends.Core;
using UnityEngine;

namespace BackyardLegends.Runtime
{
    internal static class BackyardLegendsCardArtCatalog
    {
        private const string ResourceRoot = "BackyardLegends/CardFaces";
        private const string EmptyCardResource = ResourceRoot + "/Empty";
        private const string OpeningEffectResource = "BackyardLegends/Opening/efect";
        private static readonly Dictionary<Card, Sprite> FaceCache = new();
        private static Sprite emptyCardSprite;
        private static bool emptyCardLoaded;
        private static Sprite openingEffectSprite;
        private static bool openingEffectLoaded;

        public static bool TryGetFaceSprite(Card card, out Sprite sprite)
        {
            if (FaceCache.TryGetValue(card, out sprite))
            {
                return sprite != null;
            }

            sprite = Resources.Load<Sprite>($"{ResourceRoot}/{GetSuitFolder(card.Suit)}/{card.RankLabel}");
            FaceCache[card] = sprite;
            return sprite != null;
        }

        public static bool TryGetOpeningEffectSprite(out Sprite sprite)
        {
            if (!openingEffectLoaded)
            {
                openingEffectLoaded = true;
                var texture = Resources.Load<Texture2D>(OpeningEffectResource);
                if (texture != null)
                {
                    openingEffectSprite = Sprite.Create(
                        texture,
                        new Rect(0f, 0f, texture.width, texture.height),
                        new Vector2(0.5f, 0.5f),
                        100f);
                    openingEffectSprite.name = "OpeningStackEffect";
                }
            }

            sprite = openingEffectSprite;
            return sprite != null;
        }

        public static bool TryGetEmptyCardSprite(out Sprite sprite)
        {
            if (!emptyCardLoaded)
            {
                emptyCardLoaded = true;
                emptyCardSprite = Resources.Load<Sprite>(EmptyCardResource);
            }

            sprite = emptyCardSprite;
            return sprite != null;
        }

        private static string GetSuitFolder(Suit suit)
        {
            return suit switch
            {
                Suit.Clubs => "Clubs",
                Suit.Diamonds => "Diamond",
                Suit.Hearts => "Heart",
                Suit.Spades => "Spades",
                _ => "Spades"
            };
        }
    }
}
