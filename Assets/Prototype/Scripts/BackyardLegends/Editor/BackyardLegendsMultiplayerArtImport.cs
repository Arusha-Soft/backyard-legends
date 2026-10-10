using System.IO;
using UnityEditor;
using UnityEngine;

namespace BackyardLegends.Editor
{
    /// <summary>
    /// Ensures Multiplayer Resources sprites import as UI sprites (Single + optional border).
    /// </summary>
    public static class BackyardLegendsMultiplayerArtImport
    {
        private const string ResourcesFolder = "Assets/Resources/BackyardLegends/Multiplayer";
        private const string SheetPath = "Assets/Prototype/Art/Multiplayer/Backyard legends.png";
        private const string BackgroundPath = "Assets/Prototype/Art/Multiplayer/background.jpg";

        private static readonly string[] SlicedNames =
        {
            "btnGold", "btnGreen", "btnRed", "btnDark", "btnGreenSpade", "btnDarkSpade",
            "btnGoldThin", "invitePanel"
        };

        [MenuItem("Backyard Legends/Import Multiplayer UI Art Settings")]
        public static void ApplyImportSettings()
        {
            EnsureFolderSprites(ResourcesFolder);
            ConfigureTexture(SheetPath, SpriteImportMode.Single, 0);
            ConfigureTexture(BackgroundPath, SpriteImportMode.Single, 0);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Multiplayer UI art import settings applied.");
        }

        private static void EnsureFolderSprites(string folder)
        {
            if (!AssetDatabase.IsValidFolder(folder))
            {
                Debug.LogWarning($"Multiplayer Resources folder missing: {folder}");
                return;
            }

            var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { folder });
            for (var i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var fileName = Path.GetFileNameWithoutExtension(path);
                var border = System.Array.IndexOf(SlicedNames, fileName) >= 0 ? 28 : 0;
                ConfigureTexture(path, SpriteImportMode.Single, border);
            }
        }

        private static void ConfigureTexture(string path, SpriteImportMode mode, int border)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                return;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = mode;
            importer.spritePixelsPerUnit = 100f;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.spriteBorder = border > 0
                ? new Vector4(border, border, border, border)
                : Vector4.zero;
            importer.SaveAndReimport();
        }
    }
}
