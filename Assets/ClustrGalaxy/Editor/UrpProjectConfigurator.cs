using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ClustrGalaxy.Editor
{
    public static class UrpProjectConfigurator
    {
        private const string SettingsFolder = "Assets/ClustrGalaxy/Settings";
        private const string PipelinePath = SettingsFolder + "/ClustrGalaxyURP.asset";
        private const string RendererPath = SettingsFolder + "/ClustrGalaxyURP_Renderer.asset";

        [InitializeOnLoadMethod]
        private static void ConfigureOnce()
        {
            EditorApplication.delayCall += Configure;
        }

        [MenuItem("Clustr Galaxy/Configure URP")]
        public static void Configure()
        {
            Directory.CreateDirectory(SettingsFolder);
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            if (pipeline == null)
            {
                var renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(renderer, RendererPath);
                pipeline = UniversalRenderPipelineAsset.Create(renderer);
                pipeline.name = "Clustr Galaxy URP";
                pipeline.renderScale = 1f;
                pipeline.msaaSampleCount = 4;
                pipeline.supportsHDR = true;
                AssetDatabase.CreateAsset(pipeline, PipelinePath);
            }

            if (GraphicsSettings.defaultRenderPipeline != pipeline)
            {
                GraphicsSettings.defaultRenderPipeline = pipeline;
            }
            var originalQuality = QualitySettings.GetQualityLevel();
            for (var index = 0; index < QualitySettings.names.Length; index++)
            {
                QualitySettings.SetQualityLevel(index, applyExpensiveChanges: false);
                QualitySettings.renderPipeline = pipeline;
            }
            QualitySettings.SetQualityLevel(originalQuality, applyExpensiveChanges: false);

            ConvertImportedStandardMaterials();
            AssetDatabase.SaveAssets();
            Debug.Log("[ClustrGalaxy.Rendering] URP 17.4 configured; compatible imported Standard materials converted without deleting source assets.");
        }

        private static void ConvertImportedStandardMaterials()
        {
            var urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit == null)
            {
                return;
            }
            foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets/project", "Assets/Treys" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null || material.shader == null || material.shader.name != "Standard")
                {
                    continue;
                }
                var texture = material.HasProperty("_MainTex") ? material.GetTexture("_MainTex") : null;
                var color = material.HasProperty("_Color") ? material.GetColor("_Color") : Color.white;
                material.shader = urpLit;
                if (texture != null && material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
                if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
                EditorUtility.SetDirty(material);
            }
        }
    }
}
