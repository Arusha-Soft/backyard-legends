using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BackyardLegends.Editor
{
    public static class BackyardLegendsConvertButtonLabelsToTmp
    {
        [MenuItem("Backyard Legends/Convert Button Labels To TextMeshPro")]
        public static void ConvertAll()
        {
            var font = TMP_Settings.defaultFontAsset;
            if (font == null)
            {
                font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                    "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
            }

            if (font == null)
            {
                Debug.LogError("TMP font asset missing. Import TMP Essential Resources first.");
                return;
            }

            var report = new StringBuilder();
            var converted = 0;

            converted += ConvertOpenScenes(font, report);
            converted += ConvertPrefab("Assets/Prototype/Prefabs/UI/LoginAuthPanel.prefab", font, report);
            converted += ConvertPrefab("Assets/Resources/BackyardLegends/LoginAuthPanel.prefab", font, report);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"Converted {converted} button labels to TextMeshPro.\n{report}");
        }

        private static int ConvertOpenScenes(TMP_FontAsset font, StringBuilder report)
        {
            var count = 0;
            for (var i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded)
                {
                    continue;
                }

                var roots = scene.GetRootGameObjects();
                for (var r = 0; r < roots.Length; r++)
                {
                    count += ConvertUnder(roots[r].transform, font, report, scene.name);
                }

                if (count > 0)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                }
            }

            EditorSceneManager.SaveOpenScenes();
            return count;
        }

        private static int ConvertPrefab(string path, TMP_FontAsset font, StringBuilder report)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            if (root == null)
            {
                report.AppendLine($"SKIP missing prefab {path}");
                return 0;
            }

            var count = ConvertUnder(root.transform, font, report, path);
            if (count > 0)
            {
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }

            PrefabUtility.UnloadPrefabContents(root);
            return count;
        }

        private static int ConvertUnder(Transform root, TMP_FontAsset font, StringBuilder report, string context)
        {
            var count = 0;
            var buttons = root.GetComponentsInChildren<Button>(true);
            for (var i = 0; i < buttons.Length; i++)
            {
                count += ConvertButton(buttons[i], font, report, context);
            }

            return count;
        }

        private static int ConvertButton(Button button, TMP_FontAsset font, StringBuilder report, string context)
        {
            if (button == null)
            {
                return 0;
            }

            var count = 0;
            var legacyTexts = button.GetComponentsInChildren<Text>(true);
            for (var i = 0; i < legacyTexts.Length; i++)
            {
                var legacy = legacyTexts[i];
                if (legacy == null)
                {
                    continue;
                }

                // Keep InputField text/placeholder as legacy UI.Text (InputField requires it).
                if (legacy.GetComponentInParent<InputField>() != null)
                {
                    continue;
                }

                if (legacy.GetComponent<TextMeshProUGUI>() != null)
                {
                    continue;
                }

                var go = legacy.gameObject;
                var textValue = legacy.text;
                var size = legacy.fontSize;
                var color = legacy.color;
                var style = legacy.fontStyle;
                var anchor = legacy.alignment;
                var raycast = legacy.raycastTarget;
                var bestFit = legacy.resizeTextForBestFit;
                var minSize = legacy.resizeTextMinSize;
                var maxSize = legacy.resizeTextMaxSize;

                Object.DestroyImmediate(legacy, true);
                var tmp = go.GetComponent<TextMeshProUGUI>();
                if (tmp == null)
                {
                    tmp = go.AddComponent<TextMeshProUGUI>();
                }

                tmp.font = font;
                tmp.text = textValue;
                tmp.fontSize = size;
                tmp.color = color;
                tmp.fontStyle = MapFontStyle(style);
                tmp.alignment = MapAlignment(anchor);
                tmp.raycastTarget = raycast;
                tmp.enableAutoSizing = bestFit;
                if (bestFit)
                {
                    tmp.fontSizeMin = minSize;
                    tmp.fontSizeMax = Mathf.Max(maxSize, size);
                }

                tmp.textWrappingMode = TextWrappingModes.NoWrap;
                tmp.overflowMode = TextOverflowModes.Overflow;

                count++;
                report.AppendLine($"{context} :: {GetPath(button.transform)} \"{textValue}\"");
            }

            return count;
        }

        private static FontStyles MapFontStyle(FontStyle style)
        {
            switch (style)
            {
                case FontStyle.Bold:
                    return FontStyles.Bold;
                case FontStyle.Italic:
                    return FontStyles.Italic;
                case FontStyle.BoldAndItalic:
                    return FontStyles.Bold | FontStyles.Italic;
                default:
                    return FontStyles.Normal;
            }
        }

        private static TextAlignmentOptions MapAlignment(TextAnchor anchor)
        {
            switch (anchor)
            {
                case TextAnchor.UpperLeft:
                    return TextAlignmentOptions.TopLeft;
                case TextAnchor.UpperCenter:
                    return TextAlignmentOptions.Top;
                case TextAnchor.UpperRight:
                    return TextAlignmentOptions.TopRight;
                case TextAnchor.MiddleLeft:
                    return TextAlignmentOptions.Left;
                case TextAnchor.MiddleCenter:
                    return TextAlignmentOptions.Center;
                case TextAnchor.MiddleRight:
                    return TextAlignmentOptions.Right;
                case TextAnchor.LowerLeft:
                    return TextAlignmentOptions.BottomLeft;
                case TextAnchor.LowerCenter:
                    return TextAlignmentOptions.Bottom;
                case TextAnchor.LowerRight:
                    return TextAlignmentOptions.BottomRight;
                default:
                    return TextAlignmentOptions.Center;
            }
        }

        private static string GetPath(Transform tr)
        {
            var path = tr.name;
            while (tr.parent != null)
            {
                tr = tr.parent;
                path = tr.name + "/" + path;
            }

            return path;
        }
    }
}
