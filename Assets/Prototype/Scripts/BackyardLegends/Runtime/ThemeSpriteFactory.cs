using System.Collections.Generic;
using UnityEngine;

namespace BackyardLegends.Runtime
{
    public static class ThemeSpriteFactory
    {
        private static readonly Dictionary<string, Sprite> SpriteCache = new();

        public static Sprite CreateBackgroundSprite(Color top, Color bottom)
        {
            return GetOrCreate($"bg_{top}_{bottom}", () =>
            {
                const int size = 256;
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp
                };

                for (var y = 0; y < size; y++)
                {
                    var gradient = Mathf.Lerp(0f, 1f, y / (size - 1f));
                    var rowColor = Color.Lerp(bottom, top, gradient);
                    for (var x = 0; x < size; x++)
                    {
                        var noise = Mathf.PerlinNoise((x + 13f) * 0.07f, (y + 29f) * 0.09f);
                        var grit = Mathf.PerlinNoise((x + 77f) * 0.2f, (y + 101f) * 0.18f) * 0.045f;
                        texture.SetPixel(x, y, rowColor * (0.92f + noise * 0.12f + grit));
                    }
                }

                texture.Apply();
                return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            });
        }

        public static Sprite CreateRoundedRectSprite(Color fill, Color stroke, int width = 96, int height = 96, int radius = 18)
        {
            return GetOrCreate($"rr_{fill}_{stroke}_{width}_{height}_{radius}", () =>
            {
                var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp
                };

                var innerRadius = Mathf.Max(0, radius - 3);
                var centerX = width * 0.5f;
                var centerY = height * 0.5f;
                for (var y = 0; y < height; y++)
                {
                    for (var x = 0; x < width; x++)
                    {
                        var insideOuter = InRoundedRect(x, y, width, height, radius, centerX, centerY);
                        if (!insideOuter)
                        {
                            texture.SetPixel(x, y, Color.clear);
                            continue;
                        }

                        var insideInner = InRoundedRect(x, y, width - 6, height - 6, innerRadius, centerX, centerY);
                        if (!insideInner)
                        {
                            texture.SetPixel(x, y, stroke);
                            continue;
                        }

                        var noise = Mathf.PerlinNoise((x + 13f) * 0.11f, (y + 19f) * 0.11f);
                        texture.SetPixel(x, y, fill * (0.95f + noise * 0.06f));
                    }
                }

                texture.Apply();
                return Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 100f);
            });
        }

        public static Sprite CreateChipSprite(Color fill, Color stroke)
        {
            return CreateRoundedRectSprite(fill, stroke, 96, 48, 22);
        }

        private static Sprite GetOrCreate(string key, System.Func<Sprite> builder)
        {
            if (SpriteCache.TryGetValue(key, out var sprite) && sprite != null && sprite.texture != null)
            {
                return sprite;
            }

            sprite = builder();
            SpriteCache[key] = sprite;
            return sprite;
        }

        private static bool InRoundedRect(float x, float y, float width, float height, float radius, float centerX, float centerY)
        {
            var localX = x - centerX;
            var localY = y - centerY;
            var halfWidth = width * 0.5f;
            var halfHeight = height * 0.5f;

            var clampedX = Mathf.Clamp(localX, -halfWidth + radius, halfWidth - radius);
            var clampedY = Mathf.Clamp(localY, -halfHeight + radius, halfHeight - radius);
            var deltaX = localX - clampedX;
            var deltaY = localY - clampedY;
            return deltaX * deltaX + deltaY * deltaY <= radius * radius;
        }
    }
}
