using System.Collections.Generic;
using BackyardLegends.Core;
using UnityEngine;

namespace BackyardLegends.Runtime
{
    internal static class BackyardLegendsCardArtCatalog
    {
        private const string ResourceRoot = "BackyardLegends/CardFaces";
        private static readonly Dictionary<Card, Sprite> FaceCache = new();

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
