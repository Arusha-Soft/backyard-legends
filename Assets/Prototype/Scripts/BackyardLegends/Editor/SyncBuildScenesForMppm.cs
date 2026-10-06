using System.IO;
using UnityEditor;
using UnityEngine;

namespace BackyardLegends.Editor
{
    /// <summary>
    /// Unity 6 Multiplayer Play Mode virtual players can miss GameplayScene if the shared
    /// Build Profile scene list is stale. Force Lobby + Gameplay into EditorBuildSettings
    /// and sync into active MPPM virtual-player ProjectSettings.
    /// </summary>
    public static class SyncBuildScenesForMppm
    {
        private const string MenuPath = "Backyard Legends/Sync Build Scenes (MPPM Fix)";
        private const string LobbyPath = "Assets/Prototype/Scenes/LobbyScene.unity";
        private const string GameplayPath = "Assets/Prototype/Scenes/GameplayScene.unity";

        [MenuItem(MenuPath)]
        public static void Sync()
        {
            if (!File.Exists(LobbyPath) || !File.Exists(GameplayPath))
            {
                EditorUtility.DisplayDialog(
                    "Scenes Missing",
                    $"Expected:\n{LobbyPath}\n{GameplayPath}",
                    "OK");
                return;
            }

            var scenes = new[]
            {
                new EditorBuildSettingsScene(LobbyPath, true),
                new EditorBuildSettingsScene(GameplayPath, true)
            };
            EditorBuildSettings.scenes = scenes;
            AssetDatabase.SaveAssets();

            var synced = SyncToVirtualPlayers();
            Debug.Log(
                $"Build scenes synced: LobbyScene + GameplayScene. " +
                $"MPPM virtual player folders updated: {synced}. " +
                "Disable then re-enable virtual players (or restart Play Mode) before joining again.");

            EditorUtility.DisplayDialog(
                "Build Scenes Synced",
                "LobbyScene + GameplayScene are in Build Settings.\n\n" +
                $"Updated {synced} Multiplayer Play Mode player folder(s).\n\n" +
                "Next: stop Play Mode, disable/re-enable virtual players in the Multiplayer Play Mode window, then Host → Join again.",
                "OK");
        }

        private static int SyncToVirtualPlayers()
        {
            var source = Path.Combine(Directory.GetCurrentDirectory(), "ProjectSettings", "EditorBuildSettings.asset");
            if (!File.Exists(source))
            {
                return 0;
            }

            var vpRoot = Path.Combine(Directory.GetCurrentDirectory(), "Library", "VP");
            if (!Directory.Exists(vpRoot))
            {
                return 0;
            }

            var count = 0;
            foreach (var playerDir in Directory.GetDirectories(vpRoot))
            {
                var destDir = Path.Combine(playerDir, "ProjectSettings");
                if (!Directory.Exists(destDir))
                {
                    continue;
                }

                var dest = Path.Combine(destDir, "EditorBuildSettings.asset");
                File.Copy(source, dest, overwrite: true);
                count++;
            }

            return count;
        }
    }
}
