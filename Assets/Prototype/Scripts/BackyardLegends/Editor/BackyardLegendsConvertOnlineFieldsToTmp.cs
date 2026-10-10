using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BackyardLegends.Editor
{
    public static class BackyardLegendsConvertOnlineFieldsToTmp
    {
        [MenuItem("Backyard Legends/Convert Online Status And Join Code To TextMeshPro")]
        public static void Convert()
        {
            var font = TMP_Settings.defaultFontAsset
                       ?? AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                           "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
            if (font == null)
            {
                Debug.LogError("TMP font missing.");
                return;
            }

            var converted = 0;
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
                    converted += ConvertOnlineStatus(roots[r].transform, font);
                    converted += ConvertJoinCodeInput(roots[r].transform, font);
                    RebindLobbyRefs(roots[r].transform);
                }

                EditorSceneManager.MarkSceneDirty(scene);
            }

            EditorSceneManager.SaveOpenScenes();
            AssetDatabase.SaveAssets();
            Debug.Log($"Converted Online Status / Join Code Input to TMP. touches={converted}");
        }

        private static int ConvertOnlineStatus(Transform root, TMP_FontAsset font)
        {
            var legacy = FindNamed<Text>(root, "Online Status");
            if (legacy == null)
            {
                return 0;
            }

            var go = legacy.gameObject;
            var text = legacy.text;
            var size = legacy.fontSize;
            var color = legacy.color;
            var style = legacy.fontStyle;
            var anchor = legacy.alignment;
            var raycast = legacy.raycastTarget;
            var bestFit = legacy.resizeTextForBestFit;
            var minSize = legacy.resizeTextMinSize;
            var maxSize = legacy.resizeTextMaxSize;

            Object.DestroyImmediate(legacy, true);
            var tmp = go.GetComponent<TextMeshProUGUI>() ?? go.AddComponent<TextMeshProUGUI>();
            tmp.font = font;
            tmp.text = text;
            tmp.fontSize = Mathf.Max(size, 22);
            tmp.color = color;
            tmp.fontStyle = MapFontStyle(style);
            tmp.alignment = MapAlignment(anchor);
            tmp.raycastTarget = raycast;
            tmp.enableAutoSizing = bestFit || true;
            tmp.fontSizeMin = bestFit ? minSize : 16;
            tmp.fontSizeMax = bestFit ? Mathf.Max(maxSize, size) : 32;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            tmp.overflowMode = TextOverflowModes.Overflow;
            return 1;
        }

        private static int ConvertJoinCodeInput(Transform root, TMP_FontAsset font)
        {
            var legacyInput = FindNamed<InputField>(root, "Join Code Input");
            if (legacyInput == null)
            {
                return FindNamed<TMP_InputField>(root, "Join Code Input") != null ? 0 : 0;
            }

            var go = legacyInput.gameObject;
            var currentText = legacyInput.text ?? string.Empty;
            var placeholderValue = "Invite code";
            if (legacyInput.placeholder is Text legacyPlaceholder && !string.IsNullOrWhiteSpace(legacyPlaceholder.text))
            {
                placeholderValue = legacyPlaceholder.text;
            }

            var contentType = legacyInput.contentType;
            var characterLimit = legacyInput.characterLimit;
            var caretBlink = legacyInput.caretBlinkRate;
            var caretWidth = legacyInput.caretWidth;
            var selectionColor = legacyInput.selectionColor;
            var targetGraphic = legacyInput.targetGraphic;

            // Remove legacy text children + InputField.
            var legacyTexts = go.GetComponentsInChildren<Text>(true);
            for (var i = 0; i < legacyTexts.Length; i++)
            {
                Object.DestroyImmediate(legacyTexts[i].gameObject, true);
            }

            Object.DestroyImmediate(legacyInput, true);

            var textGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            textGo.transform.SetParent(go.transform, false);
            var text = textGo.GetComponent<TextMeshProUGUI>();
            text.font = font;
            text.fontSize = 22;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.Left;
            text.raycastTarget = true;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            Stretch(text.rectTransform, 12f, 8f);

            var placeholderGo = new GameObject("Placeholder", typeof(RectTransform), typeof(TextMeshProUGUI));
            placeholderGo.transform.SetParent(go.transform, false);
            var placeholder = placeholderGo.GetComponent<TextMeshProUGUI>();
            placeholder.font = font;
            placeholder.fontSize = 22;
            placeholder.fontStyle = FontStyles.Italic;
            placeholder.color = new Color(1f, 1f, 1f, 0.4f);
            placeholder.alignment = TextAlignmentOptions.Left;
            placeholder.text = placeholderValue;
            placeholder.raycastTarget = false;
            placeholder.textWrappingMode = TextWrappingModes.NoWrap;
            Stretch(placeholder.rectTransform, 12f, 8f);

            var tmpInput = go.GetComponent<TMP_InputField>() ?? go.AddComponent<TMP_InputField>();
            tmpInput.textViewport = go.GetComponent<RectTransform>();
            tmpInput.textComponent = text;
            tmpInput.placeholder = placeholder;
            tmpInput.fontAsset = font;
            tmpInput.text = currentText;
            tmpInput.characterLimit = characterLimit;
            tmpInput.caretBlinkRate = caretBlink;
            tmpInput.caretWidth = caretWidth;
            tmpInput.selectionColor = selectionColor;
            tmpInput.lineType = TMP_InputField.LineType.SingleLine;
            tmpInput.contentType = MapContentType(contentType);
            if (targetGraphic != null)
            {
                tmpInput.targetGraphic = targetGraphic;
            }
            else if (go.TryGetComponent<Image>(out var image))
            {
                tmpInput.targetGraphic = image;
            }

            return 1;
        }

        private static void RebindLobbyRefs(Transform root)
        {
            var refs = root.GetComponentsInChildren<BackyardLegends.Runtime.BackyardLegendsLobbySceneRefs>(true);
            for (var i = 0; i < refs.Length; i++)
            {
                refs[i].ResolveMissingReferences();
                EditorUtility.SetDirty(refs[i]);
            }
        }

        private static T FindNamed<T>(Transform root, string name) where T : Component
        {
            var all = root.GetComponentsInChildren<T>(true);
            for (var i = 0; i < all.Length; i++)
            {
                if (all[i].name == name)
                {
                    return all[i];
                }
            }

            return null;
        }

        private static void Stretch(RectTransform rect, float padX, float padY)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(padX, padY);
            rect.offsetMax = new Vector2(-padX, -padY);
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
                    return TextAlignmentOptions.Left;
            }
        }

        private static TMP_InputField.ContentType MapContentType(InputField.ContentType contentType)
        {
            switch (contentType)
            {
                case InputField.ContentType.IntegerNumber:
                    return TMP_InputField.ContentType.IntegerNumber;
                case InputField.ContentType.DecimalNumber:
                    return TMP_InputField.ContentType.DecimalNumber;
                case InputField.ContentType.Alphanumeric:
                    return TMP_InputField.ContentType.Alphanumeric;
                case InputField.ContentType.Name:
                    return TMP_InputField.ContentType.Name;
                case InputField.ContentType.EmailAddress:
                    return TMP_InputField.ContentType.EmailAddress;
                case InputField.ContentType.Password:
                    return TMP_InputField.ContentType.Password;
                case InputField.ContentType.Pin:
                    return TMP_InputField.ContentType.Pin;
                case InputField.ContentType.Autocorrected:
                    return TMP_InputField.ContentType.Autocorrected;
                default:
                    return TMP_InputField.ContentType.Standard;
            }
        }
    }
}
