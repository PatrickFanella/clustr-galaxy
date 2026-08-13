using System;
using System.Collections.Generic;
using ClustrGalaxy.Data;
using ClustrGalaxy.Generation;
using ClustrGalaxy.Simulation;
using ClustrGalaxy.Flight;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ClustrGalaxy.World
{
    public sealed class LiveGalaxyMaterializer : MonoBehaviour
    {
        private SpatialSceneLoader loader = null!;
        private Transform regionRoot = null!;
        private Transform systemRoot = null!;
        private Transform discussionRoot = null!;
        private Transform threadRoot = null!;
        private Material starMaterial = null!;
        private Material planetMaterial = null!;
        private Material userMaterial = null!;
        private Material satelliteMaterial = null!;
        private Material commentMaterial = null!;
        private Material routeMaterial = null!;
        private Material trafficMaterial = null!;
        private GalaxyNavigationController navigation = null!;
        private GalacticOverviewRenderer overview = null!;
        private GalaxyEntityPool entityPool = null!;

        public void Initialize(SpatialSceneLoader sceneLoader)
        {
            loader = sceneLoader;
            entityPool = new GalaxyEntityPool(transform);
            loader.ScopeLoaded += Materialize;
        }

        public void BindNavigation(GalaxyNavigationController controller, GalacticOverviewRenderer overviewRenderer)
        {
            navigation = controller;
            overview = overviewRenderer;
            navigation.ViewChanged += NavigationChanged;
            ApplyVisibility(navigation.Level);
        }

        private void Materialize(SpatialScene scene)
        {
            switch (scene.scope)
            {
                case "community":
                    MaterializeRegion(scene);
                    break;
                case "system":
                    MaterializeSystem(scene);
                    break;
                case "discussion":
                    MaterializeDiscussion(scene);
                    break;
                case "thread":
                    MaterializeThread(scene);
                    break;
            }
            if (navigation != null) ApplyVisibility(navigation.Level);
        }

        private void NavigationChanged(GalaxyNavigationLevel level, string _) => ApplyVisibility(level);

        private void ApplyVisibility(GalaxyNavigationLevel level)
        {
            var visibility = GalaxyLayerVisibility.For(level,
                regionRoot != null, systemRoot != null, discussionRoot != null, threadRoot != null);
            overview?.SetLayerActive(visibility.Overview);
            SetActive(regionRoot, visibility.Region);
            SetActive(systemRoot, visibility.System);
            SetActive(discussionRoot, visibility.Discussion);
            SetActive(threadRoot, visibility.Thread);
        }

        private static void SetActive(Transform root, bool active)
        {
            if (root != null) root.gameObject.SetActive(active);
        }

        private void MaterializeRegion(SpatialScene scene)
        {
            DestroyRoot(ref regionRoot);
            DestroyRoot(ref systemRoot);
            DestroyRoot(ref discussionRoot);
            DestroyRoot(ref threadRoot);
            regionRoot = new GameObject($"Live Region {scene.root_id}").transform;
            regionRoot.SetParent(transform, false);
            EnsureMaterials();

            var positions = new Dictionary<string, Vector3>(StringComparer.Ordinal);

            foreach (var node in scene.nodes)
            {
                var position = RegionPosition(node.id);
                positions[node.id] = position;
                if (node.type == "subreddit")
                {
                    var radius = Radius(node.metrics.unique_users > 0 ? node.metrics.unique_users : node.val,
                        loader.Manifest.normalization_quantiles.subreddit, 12f, 30f);
                    CreateSphere(regionRoot, node, "subreddit star", position, radius, starMaterial);
                }
                else if (node.type == "user")
                {
                    var ship = CreateCapsule(regionRoot, node, "user ship", position, 2.2f, userMaterial);
                    ship.transform.rotation = Quaternion.LookRotation(position.normalized, Vector3.up);
                }
            }

            var maximumWeight = 1L;
            foreach (var link in scene.links) maximumWeight = Math.Max(maximumWeight, link.weight);
            foreach (var link in scene.links)
            {
                if (!positions.TryGetValue(link.source, out var source) || !positions.TryGetValue(link.target, out var target))
                {
                    continue;
                }
                var strength = GalacticRouteStyle.NormalizeStrength(link.weight, maximumWeight);
                var route = new GameObject($"Historical route {link.source} -> {link.target}");
                route.transform.SetParent(regionRoot, false);
                var line = route.AddComponent<LineRenderer>();
                line.useWorldSpace = false;
                line.positionCount = 2;
                line.SetPosition(0, source);
                line.SetPosition(1, target);
                line.widthMultiplier = Mathf.Lerp(0.035f, 0.42f, strength);
                line.sharedMaterial = routeMaterial;
                var color = new Color(0.24f, 0.78f, 1f, Mathf.Lerp(0.12f, 0.8f, strength));
                line.startColor = color;
                line.endColor = color;
                route.AddComponent<HistoricalTrafficRoute>().Initialize(
                    source, target, link.weight, link.directed, loader.Manifest.revision_id,
                    $"{link.source}:{link.target}:{link.type}", trafficMaterial);
            }
        }

        private void MaterializeSystem(SpatialScene scene)
        {
            var fixture = GameObject.Find("Region: Technology Community Cluster");
            if (fixture != null)
            {
                fixture.SetActive(false);
            }
            if (regionRoot != null)
            {
                regionRoot.gameObject.SetActive(false);
            }
            DestroyRoot(ref systemRoot);
            DestroyRoot(ref discussionRoot);
            DestroyRoot(ref threadRoot);
            systemRoot = new GameObject($"Live System {scene.root_id}").transform;
            systemRoot.SetParent(transform, false);
            EnsureMaterials();

            var rootNode = FindNode(scene, scene.root_id);
            if (rootNode == null)
            {
                return;
            }
            var starRadius = Radius(rootNode.metrics.unique_users > 0 ? rootNode.metrics.unique_users : rootNode.val,
                loader.Manifest.normalization_quantiles.subreddit, 16f, 34f);
            var star = CreateSphere(systemRoot, rootNode, "subreddit star", Vector3.zero, starRadius, starMaterial).transform;
            const double starMu = 720d;
            GetOrAdd<GravitySource>(star.gameObject).Initialize(starMu, Math.Max(4d, starRadius * 0.55d), 12d);

            var posts = new List<SpatialNode>();
            var users = new List<SpatialNode>();
            foreach (var node in scene.nodes)
            {
                if (node.type == "post") posts.Add(node);
                else if (node.type == "user") users.Add(node);
            }
            posts.Sort(CompareActivityThenId);
            for (var index = 0; index < posts.Count; index++)
            {
                var node = posts[index];
                var value = node.metrics.comment_count > 0 ? node.metrics.comment_count : node.val;
                var radius = Radius(value, loader.Manifest.normalization_quantiles.post, 4f, 10f);
                var axis = GalaxyScale.FirstPlanetAxis + index * GalaxyScale.PlanetLaneGap + radius * 2d;
                var elements = StableOrbitGenerator.GeneratePlanet(
                    loader.Manifest.revision_id, loader.Manifest.spatial_catalog_id, scene.root_id, node.id, axis);
                var planet = CreateSphere(systemRoot, node, "post planet", Vector3.zero, radius, planetMaterial);
                GetOrAdd<KeplerOrbit>(planet).Initialize(star, elements, starMu, GalaxyScale.PlanetOrbitTimeScale);
                var mu = Mathf.Lerp(180f, 420f, Mathf.InverseLerp(4f, 10f, radius));
                GetOrAdd<GravitySource>(planet).Initialize(mu, Math.Max(1.5d, radius * 0.6d), 6d);
            }

            users.Sort(CompareActivityThenId);
            var visibleUsers = Math.Min(users.Count, 128);
            for (var index = 0; index < visibleUsers; index++)
            {
                var node = users[index];
                var ship = CreateCapsule(systemRoot, node, "user ship", Vector3.zero, 1.8f, userMaterial);
                var elements = StableOrbitGenerator.GeneratePlanet(
                    loader.Manifest.revision_id, loader.Manifest.spatial_catalog_id, scene.root_id, node.id,
                    420d + (index % 24) * 85d);
                GetOrAdd<KeplerOrbit>(ship).Initialize(star, elements, starMu, GalaxyScale.ShipOrbitTimeScale);
            }
        }

        private void MaterializeDiscussion(SpatialScene scene)
        {
            if (!GalaxyEntity.TryGet(scene.root_id, out var planet))
            {
                return;
            }
            DestroyRoot(ref discussionRoot);
            DestroyRoot(ref threadRoot);
            discussionRoot = new GameObject($"Discussion {scene.root_id}").transform;
            discussionRoot.SetParent(systemRoot != null ? systemRoot : transform, false);
            EnsureMaterials();

            var comments = new List<SpatialNode>();
            foreach (var node in scene.nodes)
            {
                if (node.type == "comment" && node.id != scene.root_id)
                {
                    comments.Add(node);
                }
            }
            comments.Sort((left, right) =>
            {
                var replies = right.metrics.reply_count.CompareTo(left.metrics.reply_count);
                return replies != 0 ? replies : CompareActivityThenId(left, right);
            });

            var planetRadius = Mathf.Max(1f, planet.transform.lossyScale.x * 0.5f);
            var satelliteCount = 0;
            var ringIndex = 0;
            foreach (var node in comments)
            {
                if (node.metrics.reply_count > 0 && satelliteCount < 64)
                {
                    var radius = Mathf.Min(planetRadius * 0.25f,
                        Mathf.Lerp(0.8f, 2.4f, Mathf.Clamp01(Mathf.Log10(node.metrics.reply_count + 1) / 3f)));
                    var axis = planetRadius + 42d + satelliteCount * Math.Max(10d, radius * 4d);
                    var elements = StableOrbitGenerator.GenerateMoon(
                        loader.Manifest.revision_id, loader.Manifest.spatial_catalog_id, scene.root_id, node.id, axis);
                    var satellite = CreateSphere(discussionRoot, node, "discussion satellite", Vector3.zero, radius, satelliteMaterial);
                    GetOrAdd<KeplerOrbit>(satellite).Initialize(planet.transform, elements, 300d, GalaxyScale.MoonOrbitTimeScale);
                    satelliteCount++;
                    continue;
                }

                var anomaly = CommentRingLayout.AngleRadians(node.id);
                var ringAxis = planetRadius + 95d + (ringIndex % 9) * 4.5d;
                var ringElements = new OrbitalElements(ringAxis, 0.01d, (ringIndex % 4) * 1.5d * Mathf.Deg2Rad,
                    0.35d, 0d, anomaly);
                var particle = CreateSphere(discussionRoot, node,
                    node.metrics.reply_count > 0 ? "parent comment particle" : "comment ring particle",
                    Vector3.zero, 0.35f + (ringIndex % 4) * 0.08f, commentMaterial);
                GetOrAdd<KeplerOrbit>(particle).Initialize(planet.transform, ringElements, 300d, GalaxyScale.MoonOrbitTimeScale);
                ringIndex++;
            }
        }

        private void MaterializeThread(SpatialScene scene)
        {
            if (!GalaxyEntity.TryGet(scene.root_id, out var parent))
            {
                return;
            }
            DestroyRoot(ref threadRoot);
            threadRoot = new GameObject($"Thread {scene.root_id}").transform;
            threadRoot.SetParent(systemRoot != null ? systemRoot : transform, false);
            threadRoot.gameObject.AddComponent<OrbitAnchorFollower>().Initialize(parent.transform);
            EnsureMaterials();

            var index = 0;
            foreach (var node in scene.nodes)
            {
                if (node.id == scene.root_id || node.type != "comment")
                {
                    continue;
                }
                var depth = Mathf.Clamp(node.metrics.depth, 1, 3);
                var angle = CommentRingLayout.AngleRadians(node.id);
                var distance = 4f + depth * 3.5f + (index % 7) * 0.8f;
                var position = new Vector3(
                    Mathf.Cos((float)angle) * distance,
                    (depth - 2) * 0.5f,
                    Mathf.Sin((float)angle) * distance);
                CreateSphere(threadRoot, node, $"reply depth {depth}", position, 0.5f, commentMaterial);
                index++;
            }
        }

        private static SpatialNode FindNode(SpatialScene scene, string id)
        {
            foreach (var node in scene.nodes)
            {
                if (node.id == id) return node;
            }
            return null;
        }

        private static int CompareActivityThenId(SpatialNode left, SpatialNode right)
        {
            var value = right.val.CompareTo(left.val);
            return value != 0 ? value : string.CompareOrdinal(left.id, right.id);
        }

        private static float Radius(long value, SpatialQuantile quantile, float minimum, float maximum)
        {
            var low = quantile != null ? quantile.q05 : 0;
            var high = quantile != null ? quantile.q95 : 0;
            if (high <= low)
            {
                low = 1;
                high = Math.Max(100L, value);
            }
            var normalized = MetricNormalizer.NormalizeLog(Math.Max(0, value), low, high);
            return Mathf.Lerp(minimum, maximum, Mathf.Pow((float)normalized, 0.65f));
        }

        private static Vector3 RegionPosition(string stableId)
        {
            var hash = StableHash(stableId);
            var angle = (hash % 10000) / 10000f * Mathf.PI * 2f;
            var radius = 420f + ((hash >> 14) % 1200);
            var height = ((hash >> 27) % 5000) / 5000f * 420f - 210f;
            return new Vector3(Mathf.Cos(angle) * radius, height, 1800f + Mathf.Sin(angle) * radius);
        }

        private static uint StableHash(string value)
        {
            var hash = 2166136261u;
            foreach (var character in value ?? string.Empty)
            {
                hash = unchecked((hash ^ character) * 16777619u);
            }
            return hash;
        }

        private GameObject CreateSphere(Transform parent, SpatialNode node, string type, Vector3 position, float radius, Material material)
        {
            var body = entityPool.Acquire(PrimitiveType.Sphere, parent);
            body.transform.localPosition = position;
            body.transform.localScale = Vector3.one * radius * 2f;
            body.GetComponent<MeshRenderer>().sharedMaterial = material;
            InitializeEntity(body, node, type);
            return body;
        }

        private GameObject CreateCapsule(Transform parent, SpatialNode node, string type, Vector3 position, float radius, Material material)
        {
            var body = entityPool.Acquire(PrimitiveType.Capsule, parent);
            body.transform.localPosition = position;
            body.transform.localScale = new Vector3(radius, radius * 1.8f, radius);
            body.GetComponent<MeshRenderer>().sharedMaterial = material;
            InitializeEntity(body, node, type);
            return body;
        }

        private static void InitializeEntity(GameObject body, SpatialNode node, string type)
        {
            body.GetComponent<GalaxyEntity>().Initialize(node.id,
                string.IsNullOrWhiteSpace(node.name) ? node.id : node.name, type, node.val);
            body.GetComponent<LiveEntityMetadata>().Initialize(node);
        }

        private static T GetOrAdd<T>(GameObject body) where T : Behaviour
        {
            var component = body.GetComponent<T>();
            if (component == null) component = body.AddComponent<T>();
            component.enabled = true;
            return component;
        }

        private void EnsureMaterials()
        {
            starMaterial ??= CreateMaterial(new Color(0.72f, 1f, 0.4f));
            planetMaterial ??= CreateMaterial(new Color(0.25f, 0.72f, 1f));
            userMaterial ??= CreateMaterial(new Color(1f, 0.62f, 0.18f));
            satelliteMaterial ??= CreateMaterial(new Color(0.78f, 0.88f, 1f));
            commentMaterial ??= CreateMaterial(new Color(0.55f, 0.72f, 0.92f));
            routeMaterial ??= CreateMaterial(new Color(0.18f, 0.62f, 0.95f));
            trafficMaterial ??= CreateMaterial(new Color(0.8f, 0.96f, 1f));
        }

        private static Material CreateMaterial(Color color)
        {
            var shader = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline != null
                ? Shader.Find("Universal Render Pipeline/Lit")
                : Shader.Find("Standard");
            shader ??= Shader.Find("Sprites/Default");
            var material = new Material(shader);
            material.color = color;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            return material;
        }

        private void DestroyRoot(ref Transform root)
        {
            if (root != null)
            {
                entityPool.ReleaseDescendants(root);
                Object.Destroy(root.gameObject);
                root = null!;
            }
        }

        private void OnDestroy()
        {
            if (loader != null) loader.ScopeLoaded -= Materialize;
            if (navigation != null) navigation.ViewChanged -= NavigationChanged;
            foreach (var material in new[] { starMaterial, planetMaterial, userMaterial, satelliteMaterial, commentMaterial, routeMaterial, trafficMaterial })
            {
                if (material != null) Object.Destroy(material);
            }
        }
    }
}
