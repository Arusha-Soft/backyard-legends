using BackyardLegends.Runtime;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BackyardLegends.Editor
{
    public static class BackyardLegendsMultiplayerLobbyHudAuthoring
    {
        public const string PrefabPath = "Assets/Prototype/Prefabs/UI/MultiplayerLobbyHud.prefab";
        private const string GameplayScenePath = "Assets/Prototype/Scenes/GameplayScene.unity";

        [MenuItem("Backyard Legends/Create Multiplayer Lobby HUD Prefab")]
        public static void CreatePrefab()
        {
            EnsureFolder("Assets/Prototype/Prefabs");
            EnsureFolder("Assets/Prototype/Prefabs/UI");

            var temp = new GameObject("Multiplayer Lobby HUD");
            var hud = temp.AddComponent<MultiplayerLobbyHud>();
            if (temp.transform.childCount == 0)
            {
                hud.BuildDefaultLayout();
            }

            PrefabUtility.SaveAsPrefabAsset(temp, PrefabPath);
            Object.DestroyImmediate(temp);

            AssignPrefabToGameplayBootstraps();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab != null)
            {
                Selection.activeObject = prefab;
                EditorGUIUtility.PingObject(prefab);
            }

            Debug.Log($"Created Multiplayer Lobby HUD prefab at {PrefabPath} and assigned it on GameplayScene bootstraps.");
        }

        [MenuItem("Backyard Legends/Rebuild Multiplayer Lobby HUD Prefab Layout")]
        public static void RebuildPrefabLayout()
        {
            var prototype = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prototype == null)
            {
                CreatePrefab();
                return;
            }

            var root = (GameObject)PrefabUtility.InstantiatePrefab(prototype);
            var hud = root.GetComponent<MultiplayerLobbyHud>();
            if (hud == null)
            {
                hud = root.AddComponent<MultiplayerLobbyHud>();
            }

            hud.BuildDefaultLayout();
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            AssignPrefabToGameplayBootstraps();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Rebuilt Multiplayer Lobby HUD prefab layout from defaults.");
        }

        [MenuItem("Backyard Legends/Patch Multiplayer Lobby HUD Host Badges")]
        public static void PatchHostBadgeBackgrounds()
        {
            var prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefabAsset == null)
            {
                return;
            }

            var sprite = MultiplayerUiArt.Get(MultiplayerUiArt.BtnGoldThin);
            if (sprite == null)
            {
                Debug.LogWarning("btnGoldThin sprite not found in Resources/BackyardLegends/Multiplayer.");
                return;
            }

            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            var hud = root.GetComponent<MultiplayerLobbyHud>();
            var so = hud != null ? new SerializedObject(hud) : null;
            var seats = so != null ? so.FindProperty("seatSlots") : null;
            var changed = false;

            var transforms = root.GetComponentsInChildren<Transform>(true);
            for (var i = 0; i < transforms.Length; i++)
            {
                var badgeTransform = transforms[i];
                if (badgeTransform == null || badgeTransform.name != "HostBadge")
                {
                    continue;
                }

                var badgeGo = badgeTransform.gameObject;
                var image = badgeGo.GetComponent<Image>();
                if (image == null)
                {
                    image = badgeGo.AddComponent<Image>();
                    changed = true;
                }

                if (image.sprite != sprite || image.type != Image.Type.Sliced || image.color != Color.white)
                {
                    image.sprite = sprite;
                    image.type = Image.Type.Sliced;
                    image.preserveAspect = false;
                    image.color = Color.white;
                    image.raycastTarget = false;
                    changed = true;
                }

                var label = badgeGo.transform.Find("Label")?.GetComponent<TextMeshProUGUI>();
                var legacyLabel = badgeGo.GetComponent<TextMeshProUGUI>();
                if (label == null && legacyLabel != null)
                {
                    var labelGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
                    labelGo.transform.SetParent(badgeGo.transform, false);
                    var labelRect = labelGo.GetComponent<RectTransform>();
                    labelRect.anchorMin = new Vector2(0.08f, 0.10f);
                    labelRect.anchorMax = new Vector2(0.92f, 0.90f);
                    labelRect.offsetMin = Vector2.zero;
                    labelRect.offsetMax = Vector2.zero;

                    label = labelGo.GetComponent<TextMeshProUGUI>();
                    label.text = string.IsNullOrEmpty(legacyLabel.text) ? "HOST" : legacyLabel.text;
                    label.font = legacyLabel.font;
                    label.fontSize = legacyLabel.fontSize;
                    label.fontStyle = legacyLabel.fontStyle;
                    label.alignment = TextAlignmentOptions.Center;
                    label.color = new Color(0.18f, 0.12f, 0.05f, 1f);
                    label.raycastTarget = false;
                    label.enableAutoSizing = true;
                    label.fontSizeMin = 12f;
                    label.fontSizeMax = 22f;

                    Object.DestroyImmediate(legacyLabel);
                    changed = true;
                }
                else if (label != null)
                {
                    var dark = new Color(0.18f, 0.12f, 0.05f, 1f);
                    if (label.color != dark)
                    {
                        label.color = dark;
                        changed = true;
                    }
                }

                if (label != null && seats != null)
                {
                    for (var s = 0; s < seats.arraySize; s++)
                    {
                        var hostBadge = seats.GetArrayElementAtIndex(s).FindPropertyRelative("HostBadge");
                        if (hostBadge == null)
                        {
                            continue;
                        }

                        var current = hostBadge.objectReferenceValue as TextMeshProUGUI;
                        if (current == null)
                        {
                            continue;
                        }

                        var currentRoot = current.transform.name == "HostBadge"
                            ? current.transform
                            : current.transform.parent;
                        if (currentRoot == badgeTransform && current != label)
                        {
                            hostBadge.objectReferenceValue = label;
                            changed = true;
                        }
                    }
                }
            }

            if (so != null && changed)
            {
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            if (changed)
            {
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Debug.Log("Patched HostBadge backgrounds with btnGoldThin.");
            }

            PrefabUtility.UnloadPrefabContents(root);
        }

        [MenuItem("Backyard Legends/Assign Multiplayer Lobby HUD Prefab To Gameplay")]
        public static void AssignPrefabToGameplayBootstraps()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<MultiplayerLobbyHud>(PrefabPath);
            if (prefab == null)
            {
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
                if (go != null)
                {
                    prefab = go.GetComponent<MultiplayerLobbyHud>();
                }
            }

            if (prefab == null)
            {
                Debug.LogWarning($"Missing prefab at {PrefabPath}. Run Create Multiplayer Lobby HUD Prefab first.");
                return;
            }

            var scene = EditorSceneManager.OpenScene(GameplayScenePath, OpenSceneMode.Additive);
            var bootstraps = Object.FindObjectsByType<BackyardLegendsBootstrap>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var assigned = 0;
            for (var i = 0; i < bootstraps.Length; i++)
            {
                if (bootstraps[i] == null || bootstraps[i].gameObject.scene.path != GameplayScenePath)
                {
                    continue;
                }

                var serialized = new SerializedObject(bootstraps[i]);
                var property = serialized.FindProperty("multiplayerLobbyHudPrefab");
                if (property == null)
                {
                    continue;
                }

                property.objectReferenceValue = prefab;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(bootstraps[i]);
                assigned++;
            }

            if (assigned > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }

            if (scene.isLoaded && SceneManager.GetActiveScene().path != GameplayScenePath)
            {
                EditorSceneManager.CloseScene(scene, true);
            }

            Debug.Log($"Assigned MultiplayerLobbyHud prefab on {assigned} GameplayScene bootstrap(s).");
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            var parts = path.Split('/');
            var current = parts[0];
            for (var i = 1; i < parts.Length; i++)
            {
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }

                current = next;
            }
        }
    }
}
