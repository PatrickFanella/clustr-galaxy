using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ClustrGalaxy.Editor
{
    public static class GalaxyLinuxBetaBuilder
    {
        // A request file lets an open editor run the same deterministic build after a domain reload.
        public const string LaunchScenePath = "Assets/ClustrGalaxy/Scenes/Galaxy.unity";
        public const string BuildPath = "Builds/Linux/ClustrGalaxy.x86_64";
        private const string BuildRequestPath = "Temp/ClustrGalaxyLinuxBuild.request";

        [InitializeOnLoadMethod]
        private static void RunRequestedBuild()
        {
            if (!File.Exists(BuildRequestPath))
            {
                return;
            }

            File.Delete(BuildRequestPath);
            EditorApplication.delayCall += BuildLinuxBeta;
        }

        [MenuItem("Clustr Galaxy/Build Linux Beta %#&l")]
        public static void BuildLinuxBeta()
        {
            ConfigurePlayer();
            EnsureLaunchScene();

            Directory.CreateDirectory(Path.GetDirectoryName(BuildPath) ?? "Builds/Linux");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { LaunchScenePath },
                locationPathName = BuildPath,
                target = BuildTarget.StandaloneLinux64,
                options = BuildOptions.Development
            });

            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Linux beta build failed: {report.summary.result}, {report.summary.totalErrors} errors.");
            }

            var sizeMiB = report.summary.totalSize / (1024d * 1024d);
            Debug.Log(
                $"[ClustrGalaxy.Build] Linux beta succeeded: {BuildPath}, {sizeMiB:F1} MiB, " +
                $"{report.summary.totalTime.TotalSeconds:F1}s, {report.summary.totalWarnings} warnings.");
        }

        private static void ConfigurePlayer()
        {
            PlayerSettings.companyName = "Subcult";
            PlayerSettings.productName = "Clustr Galaxy";
            PlayerSettings.bundleVersion = "0.1.0-beta";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Standalone, "tv.subcult.clustr.galaxy");
            PlayerSettings.runInBackground = true;
            PlayerSettings.defaultScreenWidth = 2560;
            PlayerSettings.defaultScreenHeight = 1440;
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
        }

        private static void EnsureLaunchScene()
        {
            EnsureFolder("Assets/ClustrGalaxy", "Scenes");

            if (!File.Exists(LaunchScenePath))
            {
                var sourceScene = SceneManager.GetActiveScene();
                if (!sourceScene.IsValid() || !EditorSceneManager.SaveScene(sourceScene, LaunchScenePath, true))
                {
                    throw new InvalidOperationException($"Could not create launch scene copy at {LaunchScenePath}.");
                }

                var launchScene = EditorSceneManager.OpenScene(LaunchScenePath, OpenSceneMode.Additive);
                try
                {
                    foreach (var root in launchScene.GetRootGameObjects())
                    {
                        UnityEngine.Object.DestroyImmediate(root);
                    }

                    if (!EditorSceneManager.SaveScene(launchScene, LaunchScenePath))
                    {
                        throw new InvalidOperationException($"Could not save launch scene at {LaunchScenePath}.");
                    }
                }
                finally
                {
                    if (launchScene.IsValid())
                    {
                        EditorSceneManager.CloseScene(launchScene, true);
                    }
                }
            }

            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(LaunchScenePath, true) };
            AssetDatabase.SaveAssets();
        }

        private static void EnsureFolder(string parent, string child)
        {
            var path = $"{parent}/{child}";
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parent, child);
            }
        }
    }
}
