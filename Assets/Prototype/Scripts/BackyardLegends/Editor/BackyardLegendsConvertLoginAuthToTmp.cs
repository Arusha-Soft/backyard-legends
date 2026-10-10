using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace BackyardLegends.Editor
{
    public static class BackyardLegendsConvertLoginAuthToTmp
    {
        private static readonly string[] PrefabPaths =
        {
            "Assets/Prototype/Prefabs/UI/LoginAuthPanel.prefab",
            "Assets/Resources/BackyardLegends/LoginAuthPanel.prefab"
        };

        [MenuItem("Backyard Legends/Convert LoginAuthPanel To TextMeshPro")]
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

            var count = 0;
            for (var i = 0; i < PrefabPaths.Length; i++)
            {
                count += ConvertPrefab(PrefabPaths[i], font);
            }

            // Also convert any live scene instance.
            var sceneViews = Object.FindObjectsByType<BackyardLegends.Runtime.BackyardLegendsLoginAuthView>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (var i = 0; i < sceneViews.Length; i++)
            {
                count += ConvertRoot(sceneViews[i].gameObject, font, true);
                RebindView(sceneViews[i]);
                EditorUtility.SetDirty(sceneViews[i]);
            }

            var lobbyRefs = Object.FindObjectsByType<BackyardLegends.Runtime.BackyardLegendsLobbySceneRefs>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (var i = 0; i < lobbyRefs.Length; i++)
            {
                lobbyRefs[i].ResolveMissingReferences();
                if (lobbyRefs[i].LoginAuthPanelInstance != null)
                {
                    lobbyRefs[i].LoginAuthPanelInstance.ApplyToLobbyRefs(lobbyRefs[i]);
                }

                EditorUtility.SetDirty(lobbyRefs[i]);
            }

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
            AssetDatabase.SaveAssets();
            Debug.Log($"LoginAuthPanel TMP conversion complete. touches={count}");
        }

        private static int ConvertPrefab(string path, TMP_FontAsset font)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            if (root == null)
            {
                Debug.LogWarning($"Missing prefab {path}");
                return 0;
            }

            var count = ConvertRoot(root, font, false);
            var view = root.GetComponent<BackyardLegends.Runtime.BackyardLegendsLoginAuthView>();
            if (view != null)
            {
                RebindView(view);
            }

            PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);
            return count;
        }

        private static int ConvertRoot(GameObject root, TMP_FontAsset font, bool preserveSceneObjects)
        {
            var count = 0;
            count += ConvertLabel(root.transform, "Title", font, 28, TextAlignmentOptions.Center);
            count += ConvertLabel(root.transform, "Account Status", font, 20, TextAlignmentOptions.Center);
            count += ConvertInput(root.transform, "Email Input", "Email", TMP_InputField.ContentType.EmailAddress, font);
            count += ConvertInput(root.transform, "Password Input", "Password", TMP_InputField.ContentType.Password, font);
            return count;
        }

        private static int ConvertLabel(
            Transform root,
            string name,
            TMP_FontAsset font,
            float fontSize,
            TextAlignmentOptions alignment)
        {
            var legacy = FindNamed<Text>(root, name);
            if (legacy == null)
            {
                return FindNamed<TextMeshProUGUI>(root, name) != null ? 0 : 0;
            }

            var go = legacy.gameObject;
            var textValue = legacy.text;
            var size = legacy.fontSize;
            var color = legacy.color;
            var style = legacy.fontStyle;
            var raycast = legacy.raycastTarget;

            Object.DestroyImmediate(legacy, true);
            var tmp = go.GetComponent<TextMeshProUGUI>() ?? go.AddComponent<TextMeshProUGUI>();
            tmp.font = font;
            tmp.text = textValue;
            tmp.fontSize = Mathf.Max(size, fontSize);
            tmp.color = color;
            tmp.fontStyle = MapFontStyle(style);
            tmp.alignment = alignment;
            tmp.raycastTarget = raycast;
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin = 14f;
            tmp.fontSizeMax = Mathf.Max(fontSize, size);
            tmp.textWrappingMode = TextWrappingModes.Normal;
            tmp.overflowMode = TextOverflowModes.Overflow;
            return 1;
        }

        private static int ConvertInput(
            Transform root,
            string name,
            string defaultPlaceholder,
            TMP_InputField.ContentType contentType,
            TMP_FontAsset font)
        {
            var legacyInput = FindNamed<InputField>(root, name);
            if (legacyInput == null)
            {
                return 0;
            }

            var go = legacyInput.gameObject;
            var currentText = legacyInput.text ?? string.Empty;
            var placeholderValue = defaultPlaceholder;
            if (legacyInput.placeholder is Text legacyPlaceholder && !string.IsNullOrWhiteSpace(legacyPlaceholder.text))
            {
                placeholderValue = legacyPlaceholder.text;
            }

            var characterLimit = legacyInput.characterLimit;
            var caretBlink = legacyInput.caretBlinkRate;
            var caretWidth = legacyInput.caretWidth;
            var selectionColor = legacyInput.selectionColor;
            var targetGraphic = legacyInput.targetGraphic;

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
            text.fontSize = 20;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.Left;
            text.raycastTarget = true;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            Stretch(text.rectTransform, 12f, 6f);

            var placeholderGo = new GameObject("Placeholder", typeof(RectTransform), typeof(TextMeshProUGUI));
            placeholderGo.transform.SetParent(go.transform, false);
            var placeholder = placeholderGo.GetComponent<TextMeshProUGUI>();
            placeholder.font = font;
            placeholder.fontSize = 20;
            placeholder.fontStyle = FontStyles.Italic;
            placeholder.color = new Color(1f, 1f, 1f, 0.4f);
            placeholder.alignment = TextAlignmentOptions.Left;
            placeholder.text = placeholderValue;
            placeholder.raycastTarget = false;
            placeholder.textWrappingMode = TextWrappingModes.NoWrap;
            Stretch(placeholder.rectTransform, 12f, 6f);

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
            tmpInput.contentType = contentType;
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

        private static void RebindView(BackyardLegends.Runtime.BackyardLegendsLoginAuthView view)
        {
            if (view == null)
            {
                return;
            }

            view.TitleText = FindNamed<TextMeshProUGUI>(view.transform, "Title");
            view.AccountStatusText = FindNamed<TextMeshProUGUI>(view.transform, "Account Status");
            view.EmailInput = FindNamed<TMP_InputField>(view.transform, "Email Input");
            view.PasswordInput = FindNamed<TMP_InputField>(view.transform, "Password Input");
            view.EmailRegisterButton = FindNamed<Button>(view.transform, "Email Register");
            view.EmailSignInButton = FindNamed<Button>(view.transform, "Email Sign In");
            view.SignInGoogleButton = FindNamed<Button>(view.transform, "Sign In Google");
            view.SignInAppleButton = FindNamed<Button>(view.transform, "Sign In Apple");
            view.ContinueAsGuestButton = FindNamed<Button>(view.transform, "Continue As Guest");
            EditorUtility.SetDirty(view);
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
    }
}
