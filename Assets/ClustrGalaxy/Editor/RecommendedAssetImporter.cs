using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ClustrGalaxy.Editor
{
    public static class RecommendedAssetImporter
    {
        private const string SessionGuard = "ClustrGalaxy.ReviewedModelsImportQueued";

        [InitializeOnLoadMethod]
        private static void ImportOnceWhenMissing()
        {
            if (AssetDatabase.IsValidFolder("Assets/Treys/Spaceships") &&
                AssetDatabase.IsValidFolder("Assets/project/Sputnik_1"))
            {
                return;
            }

            if (SessionState.GetBool(SessionGuard, false))
            {
                return;
            }

            SessionState.SetBool(SessionGuard, true);
            EditorApplication.delayCall += ImportReviewedSpaceModels;
        }

        [MenuItem("Clustr Galaxy/Import Reviewed Space Models")]
        public static void ImportReviewedSpaceModels()
        {
            var cacheRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "unity3d", "Asset Store-5.x");
            var packages = new[]
            {
                Path.Combine(cacheRoot, "Zckr", "3D ModelsVehiclesSpace", "Low-Poly Spaceships Set.unitypackage"),
                Path.Combine(cacheRoot, "union3dmodellers", "3D ModelsVehiclesSpace", "3d model Sputnik 1.unitypackage")
            };

            foreach (var package in packages)
            {
                if (!File.Exists(package))
                {
                    Debug.LogWarning($"[ClustrGalaxy.Assets] Cached package not found: {package}");
                    continue;
                }

                Debug.Log($"[ClustrGalaxy.Assets] Importing {Path.GetFileName(package)}");
                AssetDatabase.ImportPackage(package, false);
            }
        }
    }
}
