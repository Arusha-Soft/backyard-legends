using System.Collections.Generic;
using UnityEngine;

namespace BackyardLegends.Runtime
{
    /// <summary>
    /// Loads sliced Multiplayer lobby sprites from Resources/BackyardLegends/Multiplayer.
    /// </summary>
    public static class MultiplayerUiArt
    {
        public const string ResourceRoot = "BackyardLegends/Multiplayer";

        public const string Logo = "logo";
        public const string Background = "background";
        public const string BtnGold = "btnGold";
        public const string BtnGoldThin = "btnGoldThin";
        public const string BtnGreen = "btnGreen";
        public const string BtnRed = "btnRed";
        public const string BtnDark = "btnDark";
        public const string BtnGreenSpade = "btnGreenSpade";
        public const string BtnDarkSpade = "btnDarkSpade";
        public const string InvitePanel = "invitePanel";
        public const string FrameRed = "frameRed";
        public const string FrameGold = "frameGold";
        public const string Vs = "vs";
        public const string SpadeIcon = "spadeIcon";
        public const string Key = "key";
        public const string Share = "share";
        public const string Copy = "copy";
        public const string Clipboard = "clipboard";
        public const string Check = "check";
        public const string Crown = "crown";
        public const string Gear = "gear";
        public const string BackChevron = "backChevron";
        public const string LeaveDoor = "leaveDoor";
        public const string BannerGold = "bannerGold";
        public const string BannerRed = "bannerRed";
        public const string Divider = "divider";

        private static Dictionary<string, Sprite> cache;

        public static Sprite Get(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return null;
            }

            EnsureCache();
            return cache.TryGetValue(name, out var sprite) ? sprite : null;
        }

        private static void EnsureCache()
        {
            if (cache != null)
            {
                return;
            }

            cache = new Dictionary<string, Sprite>();
            var sprites = Resources.LoadAll<Sprite>(ResourceRoot);
            for (var i = 0; i < sprites.Length; i++)
            {
                var sprite = sprites[i];
                if (sprite == null || string.IsNullOrEmpty(sprite.name))
                {
                    continue;
                }

                cache[sprite.name] = sprite;
            }

            // JPG backgrounds may load as Texture2D-backed sprites with file name.
            if (!cache.ContainsKey(Background))
            {
                var bg = Resources.Load<Sprite>($"{ResourceRoot}/{Background}");
                if (bg != null)
                {
                    cache[Background] = bg;
                }
            }
        }

        public static void Apply(UnityEngine.UI.Image image, string spriteName, bool sliced = false)
        {
            if (image == null)
            {
                return;
            }

            var sprite = Get(spriteName);
            if (sprite == null)
            {
                return;
            }

            image.sprite = sprite;
            image.type = sliced ? UnityEngine.UI.Image.Type.Sliced : UnityEngine.UI.Image.Type.Simple;
            image.preserveAspect = !sliced;
            image.color = Color.white;
        }
    }
}
