using BackyardLegends.Core;
using UnityEditor;
using UnityEngine;

namespace BackyardLegends.Editor
{
    public static class BackyardLegendsAssetCreator
    {
        private const string ResourceFolder = "Assets/Resources/BackyardLegends";

        [MenuItem("Backyard Legends/Create Default Assets")]
        public static void CreateDefaultAssets()
        {
            EnsureFolder("Assets/Resources");
            EnsureFolder(ResourceFolder);

            CreateThemeAsset();
            CreateRulesAsset("Rules_Classic.asset", typeof(RuleSetConfig));
            CreateRulesAsset("Rules_Street.asset", typeof(RuleSetConfig));

            var theme = AssetDatabase.LoadAssetAtPath<ThemeConfig>($"{ResourceFolder}/Theme_Default.asset");
            if (theme != null)
            {
                BackyardLegendsArtCreator.CreateOrUpdateArtKit(theme);
                EditorUtility.SetDirty(theme);
            }

            var classic = AssetDatabase.LoadAssetAtPath<RuleSetConfig>($"{ResourceFolder}/Rules_Classic.asset");
            var street = AssetDatabase.LoadAssetAtPath<RuleSetConfig>($"{ResourceFolder}/Rules_Street.asset");
            if (classic != null)
            {
                var classicSerialized = new SerializedObject(classic);
                classicSerialized.FindProperty("displayName").stringValue = "Classic";
                classicSerialized.FindProperty("spadesMustBeBroken").boolValue = true;
                classicSerialized.FindProperty("allowSpadesAnytime").boolValue = false;
                classicSerialized.FindProperty("followSuitRequired").boolValue = true;
                classicSerialized.FindProperty("renegePenaltyEnabled").boolValue = false;
                classicSerialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(classic);
            }

            if (street != null)
            {
                var streetSerialized = new SerializedObject(street);
                streetSerialized.FindProperty("displayName").stringValue = "Street";
                streetSerialized.FindProperty("spadesMustBeBroken").boolValue = false;
                streetSerialized.FindProperty("allowSpadesAnytime").boolValue = true;
                streetSerialized.FindProperty("followSuitRequired").boolValue = true;
                streetSerialized.FindProperty("renegePenaltyEnabled").boolValue = true;
                streetSerialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(street);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void CreateThemeAsset()
        {
            var path = $"{ResourceFolder}/Theme_Default.asset";
            if (AssetDatabase.LoadAssetAtPath<ThemeConfig>(path) != null)
            {
                return;
            }

            var asset = ScriptableObject.CreateInstance<ThemeConfig>();
            AssetDatabase.CreateAsset(asset, path);
        }

        private static void CreateRulesAsset(string fileName, System.Type type)
        {
            var path = $"{ResourceFolder}/{fileName}";
            if (AssetDatabase.LoadAssetAtPath(path, type) != null)
            {
                return;
            }

            var asset = ScriptableObject.CreateInstance(type);
            AssetDatabase.CreateAsset(asset, path);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            var lastSlash = path.LastIndexOf('/');
            var parent = lastSlash > 0 ? path[..lastSlash] : "Assets";
            var folder = lastSlash > 0 ? path[(lastSlash + 1)..] : path;
            if (!AssetDatabase.IsValidFolder(parent))
            {
                EnsureFolder(parent);
            }

            AssetDatabase.CreateFolder(parent, folder);
        }
    }
}
