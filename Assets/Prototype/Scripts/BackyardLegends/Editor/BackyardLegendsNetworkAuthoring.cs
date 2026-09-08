using BackyardLegends.Runtime.Network;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;

namespace BackyardLegends.Editor
{
    public static class BackyardLegendsNetworkAuthoring
    {
        private const string PrefabFolder = "Assets/Prototype/Prefabs/Network";
        private const string PrefabPath = PrefabFolder + "/SpadesTableNetwork.prefab";
        private const string ResourcesFolder = "Assets/Resources/BackyardLegends";
        private const string ResourcesPrefabPath = ResourcesFolder + "/SpadesTableNetwork.prefab";

        [MenuItem("Backyard Legends/Create Network Prefabs")]
        public static void CreateNetworkPrefabs()
        {
            EnsureFolder("Assets/Prototype");
            EnsureFolder("Assets/Prototype/Prefabs");
            EnsureFolder(PrefabFolder);
            EnsureFolder("Assets/Resources");
            EnsureFolder(ResourcesFolder);

            var temp = new GameObject("SpadesTableNetwork");
            temp.AddComponent<NetworkObject>();
            temp.AddComponent<SpadesTableNetwork>();

            PrefabUtility.SaveAsPrefabAsset(temp, PrefabPath);
            PrefabUtility.SaveAsPrefabAsset(temp, ResourcesPrefabPath);
            Object.DestroyImmediate(temp);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"Created Spades table network prefab at {PrefabPath} and {ResourcesPrefabPath}");
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
