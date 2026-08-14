using System;
using System.Collections.Generic;
using ClustrGalaxy.Data;
using ClustrGalaxy.Generation;
using ClustrGalaxy.Flight;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ClustrGalaxy.World
{
    public sealed class GalacticOverviewRenderer : MonoBehaviour
    {
        private SpatialSceneLoader loader = null!;
        private Transform layerRoot = null!;
        private Material landmarkMaterial = null!;
        private Material routeMaterial = null!;
        private GalaxyNavigationController navigation = null!;
        private GalaxySurveyVisibilityController discoveryVisibility = null!;
        private GalaxyEntityRegistry registry = null!;

        public void Initialize(SpatialSceneLoader spatialSceneLoader)
        {
            loader = spatialSceneLoader;
            loader.SceneCommitted += RenderLoadedScene;
        }

        public void BindNavigation(GalaxyNavigationController controller)
        {
            navigation = controller;
            navigation.ViewChanged += NavigationChanged;
        }

        public void BindDiscoveryVisibility(GalaxySurveyVisibilityController visibilityController)
        {
            discoveryVisibility = visibilityController;
            registry = visibilityController?.Registry;
        }

        private void NavigationChanged(GalaxyNavigationLevel level, string _) =>
            SetLayerActive(level == GalaxyNavigationLevel.Galaxy);

        public void SetLayerActive(bool active)
        {
            if (layerRoot != null) layerRoot.gameObject.SetActive(active);
        }

        private void RenderLoadedScene(SpatialScene scene)
        {
            if (scene == null || scene.nodes == null || scene.nodes.Length == 0)
            {
                return;
            }
            if (!string.IsNullOrWhiteSpace(scene.scope) && scene.scope != "overview")
            {
                return;
            }

            if (layerRoot != null)
            {
                Object.Destroy(layerRoot.gameObject);
            }

            layerRoot = new GameObject("Live Galactic Regions (Far LOD)").transform;
            layerRoot.SetParent(transform, false);
            EnsureMaterials();

            var bounds = loader.Manifest != null && loader.Manifest.bounds.IsFiniteAndOrdered
                ? loader.Manifest.bounds
                : GalacticLandmarkLayout.BoundsOf(scene.nodes);
            var positions = new Dictionary<string, Vector3>(scene.nodes.Length, StringComparer.Ordinal);
            for (var index = 0; index < scene.nodes.Length; index++)
            {
                var node = scene.nodes[index];
                if (string.IsNullOrWhiteSpace(node.id) ||
                    positions.ContainsKey(node.id) ||
                    node.IsSuppressedAutomatedActivity)
                {
                    continue;
                }

                var position = GalacticLandmarkLayout.Project(node, bounds);
                positions.Add(node.id, position);
                CreateLandmark(node, position);
            }

            var maximumWeight = 0L;
            for (var index = 0; index < scene.links.Length; index++)
            {
                maximumWeight = Math.Max(maximumWeight, scene.links[index].weight);
            }

            var renderedRoutes = 0;
            for (var index = 0; index < scene.links.Length; index++)
            {
                var link = scene.links[index];
                if (!positions.TryGetValue(link.source, out var source) ||
                    !positions.TryGetValue(link.target, out var target))
                {
                    continue;
                }

                CreateRoute(link.source, link.target, link.type, source, target,
                    GalacticRouteStyle.NormalizeStrength(link.weight, maximumWeight));
                renderedRoutes++;
            }

            Debug.Log($"[ClustrGalaxy.Renderer] Rendered {positions.Count} galactic regions and {renderedRoutes} resident routes from the public overview.");
            discoveryVisibility?.ApplyVisibility();
        }

        private void CreateLandmark(SpatialNode node, Vector3 position)
        {
            var landmark = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            landmark.transform.SetParent(layerRoot, false);
            landmark.transform.localPosition = position;
            var activity = MetricNormalizer.NormalizeLog(node.val, 1, 50000);
            var radius = Mathf.Lerp(1.3f, 6.5f, (float)Math.Pow(activity, 0.62d));
            landmark.transform.localScale = Vector3.one * radius * 2f;
            landmark.AddComponent<GalaxyEntity>().Initialize(
                node.id,
                string.IsNullOrWhiteSpace(node.name) ? node.id : node.name,
                "community region",
                node.val,
                registry);
            landmark.AddComponent<LiveEntityMetadata>().Initialize(node);

            var color = LandmarkColor(node.id);
            var meshRenderer = landmark.GetComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = landmarkMaterial;
            var properties = new MaterialPropertyBlock();
            properties.SetColor("_Color", color);
            properties.SetColor("_BaseColor", color);
            properties.SetColor("_EmissionColor", color * 1.6f);
            meshRenderer.SetPropertyBlock(properties);
        }

        private void CreateRoute(string sourceId, string targetId, string routeType, Vector3 source, Vector3 target,
            float strength)
        {
            var route = new GameObject("Community activity route");
            route.transform.SetParent(layerRoot, false);
            route.AddComponent<GalaxySurveyRoute>().Initialize($"{sourceId}:{targetId}:{routeType}", sourceId, targetId, registry);
            var line = route.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.positionCount = 2;
            line.SetPosition(0, source);
            line.SetPosition(1, target);
            line.widthMultiplier = Mathf.Lerp(0.12f, 0.95f, strength);
            line.sharedMaterial = routeMaterial;
            var color = new Color(0.25f, 0.76f, 1f, Mathf.Lerp(0.12f, 0.72f, strength));
            line.startColor = color;
            line.endColor = color;
        }

        private void EnsureMaterials()
        {
            if (landmarkMaterial == null)
            {
                var litShader = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline != null
                    ? Shader.Find("Universal Render Pipeline/Lit")
                    : Shader.Find("Standard");
                litShader ??= Shader.Find("Sprites/Default");
                landmarkMaterial = new Material(litShader);
                if (landmarkMaterial.HasProperty("_EmissionColor"))
                {
                    landmarkMaterial.EnableKeyword("_EMISSION");
                }
            }

            if (routeMaterial == null)
            {
                routeMaterial = new Material(Shader.Find("Sprites/Default"));
            }
        }

        private static Color LandmarkColor(string id)
        {
            var hash = 17;
            for (var index = 0; index < id.Length; index++)
            {
                hash = unchecked(hash * 31 + id[index]);
            }

            var hue = Mathf.Abs(hash % 1000) / 1000f;
            return Color.HSVToRGB(hue, 0.52f, 1f);
        }

        private void OnDestroy()
        {
            if (loader != null)
            {
                loader.SceneCommitted -= RenderLoadedScene;
            }
            if (navigation != null) navigation.ViewChanged -= NavigationChanged;
            if (landmarkMaterial != null)
            {
                Object.Destroy(landmarkMaterial);
            }
            if (routeMaterial != null)
            {
                Object.Destroy(routeMaterial);
            }
        }
    }
}
