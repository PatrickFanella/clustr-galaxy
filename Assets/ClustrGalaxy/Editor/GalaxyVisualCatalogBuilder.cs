using ClustrGalaxy.World;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;

namespace ClustrGalaxy.Editor
{
    public static class GalaxyVisualCatalogBuilder
    {
        private const string ResourcesFolder = "Assets/ClustrGalaxy/Presentation";
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
                AssetDatabase.CreateFolder("Assets/ClustrGalaxy", "Presentation");
            }

            var catalog = AssetDatabase.LoadAssetAtPath<GalaxyVisualCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<GalaxyVisualCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            catalog.anomalyPrefabs = new[] { "EchoReceiver", "ShySatellite", "PocketOrrery", "WindowStation", "LanternCluster", "InvertedBeacon" }
                .Select(name => LoadPrefab(ExplorationAssetBuilder.Folder + "/" + name + ".prefab")).ToArray();
            catalog.keepsakePrefabs = new[] { "PocketResonator", "WanderingLantern", "WindowPrism" }
                .Select(name => LoadPrefab(ExplorationAssetBuilder.Folder + "/" + name + ".prefab")).ToArray();
            catalog.observatoryPlatformPrefab = LoadPrefab(ExplorationAssetBuilder.Folder + "/ObservatoryPlatform.prefab");
            catalog.explorationInstruments = new[]
            {
                AssetDatabase.LoadAssetAtPath<AudioClip>(ResourcesFolder + "/Audio/SoftBell.wav"),
                AssetDatabase.LoadAssetAtPath<AudioClip>(ResourcesFolder + "/Audio/WarmPluck.wav"),
                AssetDatabase.LoadAssetAtPath<AudioClip>(ResourcesFolder + "/Audio/AiryGlass.wav")
            };
            catalog.userShipPrefabs = ships;
            catalog.discussionSatellitePrefab = satellite;
            FreePresentationAssetBuilder.Configure(catalog);
            var settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            var entry = settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(CatalogPath), settings.DefaultGroup);
            entry.address = "galaxy-visual-catalog";
            EditorUtility.SetDirty(settings);
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            Debug.Log("[ClustrGalaxy.Assets] Visual catalog rebuilt with local presentation assets and fallbacks.");
        }

        private static GameObject LoadPrefab(string path) =>
            AssetDatabase.LoadAssetAtPath<GameObject>(path);
    }
}
