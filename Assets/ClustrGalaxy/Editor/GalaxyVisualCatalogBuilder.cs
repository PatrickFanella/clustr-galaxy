using ClustrGalaxy.World;
using UnityEditor;
using UnityEngine;

namespace ClustrGalaxy.Editor
{
    public static class GalaxyVisualCatalogBuilder
    {
        private const string ResourcesFolder = "Assets/ClustrGalaxy/Resources";
        private const string CatalogPath = ResourcesFolder + "/GalaxyVisualCatalog.asset";

        [InitializeOnLoadMethod]
        private static void QueueBuild() => EditorApplication.delayCall += Build;

        [MenuItem("Clustr Galaxy/Rebuild Visual Catalog")]
        public static void Build()
        {
            var ships = new[]
            {
                LoadPrefab("Assets/Treys/Spaceships/Prefabs/1.1.prefab"),
                LoadPrefab("Assets/Treys/Spaceships/Prefabs/3.1.prefab"),
                LoadPrefab("Assets/Treys/Spaceships/Prefabs/5.1.prefab"),
                LoadPrefab("Assets/Treys/Spaceships/Prefabs/7.1.prefab"),
                LoadPrefab("Assets/Treys/Spaceships/Prefabs/9.1.prefab"),
                LoadPrefab("Assets/Treys/Spaceships/Prefabs/11.1.prefab")
            };
            var satellite = LoadPrefab("Assets/project/Sputnik_1/Prefabs/Sputnik_1.prefab");
            if (ships[0] == null || satellite == null)
            {
                return;
            }

            if (!AssetDatabase.IsValidFolder(ResourcesFolder))
            {
                AssetDatabase.CreateFolder("Assets/ClustrGalaxy", "Resources");
            }

            var catalog = AssetDatabase.LoadAssetAtPath<GalaxyVisualCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<GalaxyVisualCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            catalog.userShipPrefabs = ships;
            catalog.discussionSatellitePrefab = satellite;
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            Debug.Log("[ClustrGalaxy.Assets] Visual catalog linked to six ship prefabs and Sputnik.");
        }

        private static GameObject LoadPrefab(string path) =>
            AssetDatabase.LoadAssetAtPath<GameObject>(path);
    }
}
