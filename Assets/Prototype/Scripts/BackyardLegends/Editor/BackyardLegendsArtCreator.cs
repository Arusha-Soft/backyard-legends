using System.IO;
using BackyardLegends.Core;
using UnityEditor;
using UnityEngine;

namespace BackyardLegends.Editor
{
    public static class BackyardLegendsArtCreator
    {
        private const string ThemePath = "Assets/Resources/BackyardLegends/Theme_Default.asset";
        private const string ArtFolder = "Assets/Art/BackyardLegends/UI";

        [MenuItem("Backyard Legends/Create Authored UI Art")]
        public static void CreateAuthoredUiArt()
        {
            var theme = AssetDatabase.LoadAssetAtPath<ThemeConfig>(ThemePath);
            if (theme == null)
            {
                Debug.LogWarning("Theme asset missing. Run Create Default Assets first.");
                return;
            }

            CreateOrUpdateArtKit(theme);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        public static void CreateOrUpdateArtKit(ThemeConfig theme)
        {
            EnsureFolder("Assets/Art");
            EnsureFolder("Assets/Art/BackyardLegends");
            EnsureFolder(ArtFolder);

            theme.tableBackgroundSprite = SaveSprite("table-background.png", CreateTableBackground(theme, 1024, 1024), Vector4.zero, ImageType.Simple);
            theme.panelSprite = SaveSprite("panel-shell.png", CreatePanelShell(512, 256, 28, 0.82f), new Vector4(42f, 42f, 42f, 42f), ImageType.Sliced);
            theme.sheetSprite = SaveSprite("sheet-shell.png", CreatePanelShell(768, 1024, 34, 0.92f), new Vector4(56f, 56f, 56f, 56f), ImageType.Sliced);
            theme.softPanelSprite = SaveSprite("soft-panel-shell.png", CreateSoftPanel(512, 256, 24), new Vector4(36f, 36f, 36f, 36f), ImageType.Sliced);
            theme.buttonSprite = SaveSprite("button-shell.png", CreateButtonShell(384, 128, 30), new Vector4(48f, 48f, 48f, 48f), ImageType.Sliced);
            theme.chipSprite = SaveSprite("chip-shell.png", CreateChipShell(256, 96, 36), new Vector4(38f, 38f, 38f, 38f), ImageType.Sliced);
            theme.cardFaceDefaultSprite = SaveSprite("card-face-default.png", CreateCardFace(theme, new Color(0.52f, 0.45f, 0.3f, 1f), 256, 384), new Vector4(34f, 34f, 34f, 34f), ImageType.Sliced);
            theme.cardFacePlayableSprite = SaveSprite("card-face-playable.png", CreateCardFace(theme, theme.panelStroke, 256, 384), new Vector4(34f, 34f, 34f, 34f), ImageType.Sliced);
            theme.cardFaceSelectedSprite = SaveSprite("card-face-selected.png", CreateCardFace(theme, theme.gold, 256, 384), new Vector4(34f, 34f, 34f, 34f), ImageType.Sliced);
            theme.cardFaceMutedSprite = SaveSprite("card-face-muted.png", CreateCardFace(theme, new Color(0.26f, 0.27f, 0.3f, 0.8f), 256, 384), new Vector4(34f, 34f, 34f, 34f), ImageType.Sliced);
            theme.cardBackHeroSprite = SaveSprite("card-back-hero.png", CreateCardBack(theme, 256, 384), new Vector4(34f, 34f, 34f, 34f), ImageType.Sliced);
            EditorUtility.SetDirty(theme);
        }

        private static Sprite SaveSprite(string fileName, Texture2D texture, Vector4 border, ImageType imageType)
        {
            var path = $"{ArtFolder}/{fileName}";
            var absolutePath = Path.Combine(Directory.GetParent(Application.dataPath)!.FullName, path.Replace('/', Path.DirectorySeparatorChar));
            File.WriteAllBytes(absolutePath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 100f;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Bilinear;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.spriteBorder = imageType == ImageType.Sliced ? border : Vector4.zero;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static Texture2D CreateTableBackground(ThemeConfig theme, int width, int height)
        {
            var texture = CreateTexture(width, height);
            var top = theme.backgroundSecondary;
            var bottom = theme.backgroundColor;
            var glow = theme.green * 0.25f;

            for (var y = 0; y < height; y++)
            {
                var y01 = y / (height - 1f);
                for (var x = 0; x < width; x++)
                {
                    var x01 = x / (width - 1f);
                    var baseColor = Color.Lerp(bottom, top, Mathf.SmoothStep(0f, 1f, y01));
                    var centerGlow = Mathf.Clamp01(1f - Vector2.Distance(new Vector2(x01, y01), new Vector2(0.5f, 0.54f)) * 1.65f);
                    baseColor = Color.Lerp(baseColor, baseColor + glow, centerGlow * 0.32f);

                    var diagonalNoise = Mathf.PerlinNoise((x + 42f) * 0.014f, (y + 88f) * 0.017f);
                    var grit = Mathf.PerlinNoise((x + 201f) * 0.11f, (y + 157f) * 0.13f);
                    var vignette = Mathf.Clamp01(1f - Vector2.Distance(new Vector2(x01, y01), new Vector2(0.5f, 0.5f)) * 1.15f);
                    var scuff = Mathf.Abs(Mathf.PerlinNoise((x * 0.007f) + 0.4f, (y * 0.007f) + 0.15f) - 0.5f);
                    var scuffStrength = Mathf.Clamp01((0.08f - scuff) * 12f) * 0.08f;
                    var grain = 0.88f + diagonalNoise * 0.08f + grit * 0.045f + scuffStrength;

                    texture.SetPixel(x, y, baseColor * (grain * Mathf.Lerp(0.82f, 1.02f, vignette)));
                }
            }

            texture.Apply();
            return texture;
        }

        private static Texture2D CreatePanelShell(int width, int height, int radius, float fillAlpha)
        {
            var texture = CreateTexture(width, height);
            var fill = new Color(1f, 1f, 1f, fillAlpha);
            var stroke = new Color(0.74f, 0.74f, 0.74f, 0.92f);
            var highlight = new Color(1f, 1f, 1f, 0.15f);
            var shadow = new Color(0f, 0f, 0f, 0.18f);

            PaintRoundedRect(texture, fill, stroke, highlight, shadow, radius, 6, 10);
            texture.Apply();
            return texture;
        }

        private static Texture2D CreateSoftPanel(int width, int height, int radius)
        {
            var texture = CreateTexture(width, height);
            var fill = new Color(1f, 1f, 1f, 0.18f);
            var stroke = new Color(1f, 1f, 1f, 0.38f);
            var highlight = new Color(1f, 1f, 1f, 0.12f);
            var shadow = new Color(0f, 0f, 0f, 0.1f);
            PaintRoundedRect(texture, fill, stroke, highlight, shadow, radius, 4, 8);
            texture.Apply();
            return texture;
        }

        private static Texture2D CreateButtonShell(int width, int height, int radius)
        {
            var texture = CreateTexture(width, height);
            var fill = new Color(1f, 1f, 1f, 0.96f);
            var stroke = new Color(0.65f, 0.65f, 0.65f, 1f);
            var highlight = new Color(1f, 1f, 1f, 0.28f);
            var shadow = new Color(0f, 0f, 0f, 0.22f);
            PaintRoundedRect(texture, fill, stroke, highlight, shadow, radius, 6, 12);

            for (var y = 0; y < height; y++)
            {
                var gloss = Mathf.Clamp01(Mathf.InverseLerp(height * 0.65f, height * 0.98f, y)) * 0.09f;
                for (var x = 0; x < width; x++)
                {
                    var color = texture.GetPixel(x, y);
                    texture.SetPixel(x, y, Color.Lerp(color, Color.white, gloss));
                }
            }

            texture.Apply();
            return texture;
        }

        private static Texture2D CreateChipShell(int width, int height, int radius)
        {
            var texture = CreateTexture(width, height);
            var fill = new Color(1f, 1f, 1f, 0.95f);
            var stroke = new Color(0.58f, 0.58f, 0.58f, 1f);
            var highlight = new Color(1f, 1f, 1f, 0.22f);
            var shadow = new Color(0f, 0f, 0f, 0.16f);
            PaintRoundedRect(texture, fill, stroke, highlight, shadow, radius, 5, 10);
            texture.Apply();
            return texture;
        }

        private static Texture2D CreateCardFace(ThemeConfig theme, Color accent, int width, int height)
        {
            var texture = CreateTexture(width, height);
            var paper = theme.cardFace;
            var paperShadow = paper * 0.92f;
            var border = accent;
            const int radius = 24;

            for (var y = 0; y < height; y++)
            {
                var y01 = y / (height - 1f);
                for (var x = 0; x < width; x++)
                {
                    if (!InsideRoundedRect(x, y, width, height, radius))
                    {
                        texture.SetPixel(x, y, Color.clear);
                        continue;
                    }

                    var edge = DistanceToRectEdge(x, y, width, height);
                    if (edge < 7f)
                    {
                        texture.SetPixel(x, y, border);
                        continue;
                    }

                    var tone = Color.Lerp(paperShadow, paper, Mathf.SmoothStep(0f, 1f, y01));
                    var noise = Mathf.PerlinNoise((x + 35f) * 0.065f, (y + 79f) * 0.065f);
                    var lineAccent = edge < 16f ? 0.08f : 0f;
                    texture.SetPixel(x, y, Color.Lerp(tone * (0.95f + noise * 0.08f), border, lineAccent));
                }
            }

            AddCornerPips(texture, border);
            texture.Apply();
            return texture;
        }

        private static Texture2D CreateCardBack(ThemeConfig theme, int width, int height)
        {
            var texture = CreateTexture(width, height);
            var fill = theme.cardBack;
            var border = theme.gold;
            const int radius = 24;

            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    if (!InsideRoundedRect(x, y, width, height, radius))
                    {
                        texture.SetPixel(x, y, Color.clear);
                        continue;
                    }

                    var edge = DistanceToRectEdge(x, y, width, height);
                    if (edge < 7f)
                    {
                        texture.SetPixel(x, y, border);
                        continue;
                    }

                    var baseNoise = Mathf.PerlinNoise((x + 110f) * 0.045f, (y + 19f) * 0.045f);
                    var color = fill * (0.9f + baseNoise * 0.12f);
                    var diamond = Mathf.Abs((((x - width * 0.5f) + (y - height * 0.5f)) % 42f) / 42f - 0.5f);
                    var cross = Mathf.Abs((((x - width * 0.5f) - (y - height * 0.5f)) % 42f) / 42f - 0.5f);
                    var pattern = Mathf.Clamp01((0.11f - diamond) * 10f) + Mathf.Clamp01((0.11f - cross) * 10f);
                    color = Color.Lerp(color, border * 0.82f, Mathf.Clamp01(pattern * 0.22f));
                    texture.SetPixel(x, y, color);
                }
            }

            AddCenterDiamond(texture, theme.gold);
            texture.Apply();
            return texture;
        }

        private static void PaintRoundedRect(Texture2D texture, Color fill, Color stroke, Color highlight, Color shadow, int radius, int strokeWidth, int inset)
        {
            var width = texture.width;
            var height = texture.height;

            for (var y = 0; y < height; y++)
            {
                var y01 = y / (height - 1f);
                for (var x = 0; x < width; x++)
                {
                    if (!InsideRoundedRect(x, y, width, height, radius))
                    {
                        texture.SetPixel(x, y, Color.clear);
                        continue;
                    }

                    var edge = DistanceToRectEdge(x, y, width, height);
                    if (edge < strokeWidth)
                    {
                        texture.SetPixel(x, y, stroke);
                        continue;
                    }

                    var tone = Color.Lerp(fill * 0.88f, fill, Mathf.SmoothStep(0f, 1f, y01));
                    var noise = Mathf.PerlinNoise((x + 13f) * 0.08f, (y + 47f) * 0.08f) * 0.06f;
                    var color = tone * (0.96f + noise);

                    if (y > height - inset - 1)
                    {
                        color = Color.Lerp(color, Color.white, highlight.a * Mathf.InverseLerp(height - inset, height - 1f, y));
                    }

                    if (y < inset)
                    {
                        color = Color.Lerp(color, shadow, shadow.a * Mathf.InverseLerp(inset, 0f, y));
                    }

                    texture.SetPixel(x, y, color);
                }
            }
        }

        private static void AddCornerPips(Texture2D texture, Color accent)
        {
            DrawCornerPip(texture, 22, texture.height - 32, accent);
            DrawCornerPip(texture, texture.width - 22, 32, accent);
        }

        private static void DrawCornerPip(Texture2D texture, int centerX, int centerY, Color color)
        {
            for (var y = -8; y <= 8; y++)
            {
                for (var x = -8; x <= 8; x++)
                {
                    if (x * x + y * y > 20)
                    {
                        continue;
                    }

                    var px = Mathf.Clamp(centerX + x, 0, texture.width - 1);
                    var py = Mathf.Clamp(centerY + y, 0, texture.height - 1);
                    texture.SetPixel(px, py, color);
                }
            }
        }

        private static void AddCenterDiamond(Texture2D texture, Color accent)
        {
            var center = new Vector2(texture.width * 0.5f, texture.height * 0.5f);
            for (var y = 0; y < texture.height; y++)
            {
                for (var x = 0; x < texture.width; x++)
                {
                    var distance = Mathf.Abs(x - center.x) + Mathf.Abs(y - center.y);
                    if (distance > 36f || distance < 18f)
                    {
                        continue;
                    }

                    var current = texture.GetPixel(x, y);
                    texture.SetPixel(x, y, Color.Lerp(current, accent, 0.75f));
                }
            }
        }

        private static Texture2D CreateTexture(int width, int height)
        {
            return new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
        }

        private static bool InsideRoundedRect(int x, int y, int width, int height, int radius)
        {
            var localX = x - width * 0.5f;
            var localY = y - height * 0.5f;
            var halfWidth = width * 0.5f;
            var halfHeight = height * 0.5f;
            var clampedX = Mathf.Clamp(localX, -halfWidth + radius, halfWidth - radius);
            var clampedY = Mathf.Clamp(localY, -halfHeight + radius, halfHeight - radius);
            var deltaX = localX - clampedX;
            var deltaY = localY - clampedY;
            return deltaX * deltaX + deltaY * deltaY <= radius * radius;
        }

        private static float DistanceToRectEdge(int x, int y, int width, int height)
        {
            return Mathf.Min(Mathf.Min(x, width - 1 - x), Mathf.Min(y, height - 1 - y));
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            var split = path.Split('/');
            var current = split[0];
            for (var i = 1; i < split.Length; i++)
            {
                var next = $"{current}/{split[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, split[i]);
                }

                current = next;
            }
        }

        private enum ImageType
        {
            Simple,
            Sliced
        }
    }
}
