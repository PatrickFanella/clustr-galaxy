using System;
using System.Collections.Generic;
using ClustrGalaxy.Data;
using ClustrGalaxy.Generation;
using ClustrGalaxy.Interaction;
using ClustrGalaxy.Simulation;
using ClustrGalaxy.Flight;
using UnityEngine;
using Object = UnityEngine.Object;
using ClustrGalaxy.Core.Domain;
using ClustrGalaxy.Core.Data;
using ClustrGalaxy.Core.World;
using ClustrGalaxy.Core.Session;

namespace ClustrGalaxy.World
{
    public sealed class LiveGalaxyMaterializer : MonoBehaviour
    {
        private static int StablePresentationIndex(string id)
        {
            unchecked
            {
                uint hash = 2166136261;
                foreach (var character in id) hash = (hash ^ character) * 16777619;
                return (int)(hash % 1024);
            }
        }

        private SpatialSceneLoader loader = null!;
        private GalaxySession session;
        private readonly List<Transform> retiredRoots = new();
        private Transform regionRoot = null!;
        private Transform systemRoot = null!;
        private Transform discussionRoot = null!;
        private Transform illuminatingStar;
        private void LateUpdate() { if (illuminatingStar != null) Shader.SetGlobalVector("_GalaxySunPosition", illuminatingStar.position); }
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
        private GalaxySurveyVisibilityController discoveryVisibility = null!;
        private CommentThreadController threadInspection = null!;
        private string activeSystemId = string.Empty;
        private GalaxyEntityRegistry registry = null!;

        public void Initialize(SpatialSceneLoader sceneLoader)
        {
            loader = sceneLoader;
            if (!TryGetComponent<CelestialFrame>(out _)) gameObject.AddComponent<CelestialFrame>();
            entityPool = new GalaxyEntityPool(transform);
            loader.SceneCommitted += Materialize;
        }

        public void BindNavigation(GalaxyNavigationController controller, GalacticOverviewRenderer overviewRenderer)
        {
            navigation = controller;
            overview = overviewRenderer;
            navigation.ViewChanged += NavigationChanged;
            navigation.ModeChanged += MapModeChanged;
            ApplyVisibility(navigation.Level);
        }

        public void BindDiscoveryVisibility(GalaxySurveyVisibilityController visibilityController)
        {
            discoveryVisibility = visibilityController;
            registry = visibilityController?.Registry;
        }

        public void BindThreadInspection(CommentThreadController controller)
        {
            threadInspection = controller;
        }

        private void Materialize(SpatialScene scene)
        {
            if (session == null && loader.Session != null)
            { session = loader.Session; session.ResidentChanged += ResidentChanged; }
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
            discoveryVisibility?.ApplyVisibility();
        }

        private void MapModeChanged(bool _) => ApplyVisibility(navigation.Level);

        private void NavigationChanged(GalaxyNavigationLevel level, string _) => ApplyVisibility(level);

        private void ApplyVisibility(GalaxyNavigationLevel level)
        {
            var visibility = GalaxyLayerVisibility.For(level,
                regionRoot != null, systemRoot != null, discussionRoot != null, false);
            overview?.SetLayerActive(visibility.Overview);
            SetActive(regionRoot, visibility.Region && (systemRoot == null || navigation.IsMapOpen));
            // The current physical system keeps its gravity and ephemerides during chart inspection.
            SetActive(systemRoot, systemRoot != null);
            if (systemRoot != null)
                foreach (var renderer in systemRoot.GetComponentsInChildren<Renderer>(true))
                    renderer.enabled = !navigation.IsMapOpen || visibility.System;
            SetActive(discussionRoot, visibility.Discussion);
        }

        private static void SetActive(Transform root, bool active)
        {
            if (root != null) root.gameObject.SetActive(active);
        }

        private void MaterializeRegion(SpatialScene scene)
        {
            // Chart refreshes cannot repurpose stable-ID physical bodies into small map proxies.
            // Keep the existing chart while a physical system is active; loaded data remains in the HUD model.
            if (systemRoot != null) return;
            DestroyRoot(ref regionRoot);
            DestroyRoot(ref systemRoot);
            DestroyRoot(ref discussionRoot);
            regionRoot = new GameObject($"Live Region {scene.root_id}").transform;
            regionRoot.SetParent(transform, false);
            regionRoot.gameObject.AddComponent<CelestialFrame>();
            EnsureMaterials();

            var positions = new Dictionary<string, Vector3>(StringComparer.Ordinal);

            foreach (var node in scene.nodes)
            {
                var position = RenderPosition(ProjectRegionPosition(node.id, GalaxyTuning.SystemDistanceScale));
                positions[node.id] = position;
                var kind = GalaxyKinds.ParseEntityKind(node.type);
                if (kind == GalaxyEntityKind.Subreddit)
                {
                    var radius = Radius(node.metrics.unique_users > 0 ? node.metrics.unique_users : node.val,
                        loader.Manifest.normalization_quantiles.subreddit, 12f, 30f);
                    CreateSphere(regionRoot, node, "subreddit star", position, radius, starMaterial);
                }
                else if (kind == GalaxyEntityKind.User)
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
                route.AddComponent<GalaxySurveyRoute>().Initialize($"{link.source}:{link.target}:{link.type}",
                    link.source, link.target, registry);
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
            if (!loader.Session.CurrentSystemId.IsEmpty && loader.Session.CurrentSystemId.Value != scene.root_id) return;
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
            systemRoot = new GameObject($"Live System {scene.root_id}").transform;
            systemRoot.SetParent(transform, false);
            systemRoot.gameObject.AddComponent<CelestialFrame>();
            EnsureMaterials();

            var rootNode = FindNode(scene, scene.root_id);
            if (rootNode == null)
            {
                return;
            }
            var starRadius = Radius(rootNode.metrics.unique_users > 0 ? rootNode.metrics.unique_users : rootNode.val,
                loader.Manifest.normalization_quantiles.subreddit, GalaxyTuning.SunRadius, GalaxyTuning.SunRadius * 1.25f);
            var star = CreateSphere(systemRoot, rootNode, "subreddit star", RenderPosition(Vector3.zero), starRadius, starMaterial).transform;
            illuminatingStar = star;
            var starMu = GalaxyTuning.SunSurfaceGravity * starRadius * starRadius;
            GetOrAdd<GravitySource>(star.gameObject).Initialize(starMu, Math.Max(1d, starRadius * .1d), 12d, DoubleVector3.Zero);

            var posts = new List<SpatialNode>();
            var users = new List<SpatialNode>();
            foreach (var node in scene.nodes)
            {
                var kind = GalaxyKinds.ParseEntityKind(node.type);
                if (kind == GalaxyEntityKind.Post) posts.Add(node);
                else if (kind == GalaxyEntityKind.User) users.Add(node);
            }
            posts.Sort(CompareActivityThenId);
            var previousPlanet = default(OrbitalElements);
            var previousEnvelope = 0d;
            for (var index = 0; index < posts.Count; index++)
            {
                var node = posts[index];
                var value = node.metrics.comment_count > 0 ? node.metrics.comment_count : node.val;
                var radius = Radius(value, loader.Manifest.normalization_quantiles.post, 4f, 10f);
                var seed = StableOrbitGenerator.GeneratePlanet(
                    loader.Manifest.revision_id, loader.Manifest.spatial_catalog_id, scene.root_id, node.id, 1d);
                var elements = CelestialLayout.Planet(seed, radius * GalaxyTuning.PlanetRadiusScale,
                    CelestialLayout.MaximumMoons, starRadius, starMu, previousPlanet, previousEnvelope);
                previousPlanet = elements;
                previousEnvelope = CelestialLayout.SatelliteEnvelope(radius * GalaxyTuning.PlanetRadiusScale, CelestialLayout.MaximumMoons);
                var planet = CreateSphere(systemRoot, node, "post planet", Vector3.zero, radius, planetMaterial);
                GetOrAdd<KeplerOrbit>(planet).Initialize(star, elements, starMu, GalaxyScale.PlanetOrbitTimeScale);
                GetOrAdd<GravitySource>(planet).Initialize(GalaxyTuning.PlanetSurfaceGravity * radius * radius * GalaxyTuning.PlanetRadiusScale * GalaxyTuning.PlanetRadiusScale, radius * GalaxyTuning.PlanetRadiusScale * .1d, 6d);
            }

            users.Sort(CompareActivityThenId);
            var visibleUsers = Math.Min(users.Count, 128);
            for (var index = 0; index < visibleUsers; index++)
            {
                var node = users[index];
                var ship = CreateCapsule(systemRoot, node, "user ship", Vector3.zero, 1.8f, userMaterial);
                var elements = StableOrbitGenerator.GeneratePlanet(
                    loader.Manifest.revision_id, loader.Manifest.spatial_catalog_id, scene.root_id, node.id,
                    previousPlanet.SemiMajorAxis * (1d + previousPlanet.Eccentricity) + previousEnvelope + GalaxyScale.TravelerOrbitOffset + index * GalaxyScale.TravelerLaneGap);
                GetOrAdd<KeplerOrbit>(ship).Initialize(star, elements, starMu, GalaxyScale.ShipOrbitTimeScale);
            }
            activeSystemId = scene.root_id;
            GalaxySurveyMembership.InitializeDescendants(systemRoot, activeSystemId);
        }

        private void MaterializeDiscussion(SpatialScene scene)
        {
            var planet = registry?.FindActiveEntity(scene.root_id);
            if (planet == null)
            {
                return;
            }
            DestroyRoot(ref discussionRoot);
            discussionRoot = new GameObject($"Discussion {scene.root_id}").transform;
            discussionRoot.SetParent(systemRoot != null ? systemRoot : transform, false);
            discussionRoot.gameObject.AddComponent<CelestialFrame>();
            EnsureMaterials();

            var comments = new List<SpatialNode>();
            foreach (var node in scene.nodes)
            {
                if (GalaxyKinds.ParseEntityKind(node.type) == GalaxyEntityKind.Comment && node.id != scene.root_id)
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
            var previousMoon = default(OrbitalElements);
            var previousMoonRadius = 0d;
            foreach (var node in comments)
            {
                if (node.metrics.reply_count > 0 && satelliteCount < 64)
                {
                    var radius = planetRadius * Mathf.Lerp(.02f, .05f, Mathf.Clamp01(Mathf.Log10(node.metrics.reply_count + 1) / 3f));
                    var seed = StableOrbitGenerator.GenerateMoon(
                        loader.Manifest.revision_id, loader.Manifest.spatial_catalog_id, scene.root_id, node.id, 1d);
                    var elements = CelestialLayout.Moon(seed, planetRadius, radius, previousMoon, previousMoonRadius);
                    previousMoon = elements;
                    previousMoonRadius = radius;
                    var satellite = CreateSphere(discussionRoot, node, "discussion satellite", Vector3.zero, radius, satelliteMaterial);
                    GetOrAdd<CommentThreadNode>(satellite).Initialize(node.metrics.reply_count);
                    GetOrAdd<KeplerOrbit>(satellite).Initialize(planet.transform, elements, planet.GetComponent<GravitySource>()?.GravitationalParameter ?? 300d, GalaxyScale.MoonOrbitTimeScale);
                    GetOrAdd<GravitySource>(satellite).Initialize(GalaxyTuning.PlanetSurfaceGravity * radius * radius, radius * .1d, 6d);
                    satelliteCount++;
                    continue;
                }

                var anomaly = CommentRingLayout.AngleRadians(node.id);
                var ringAxis = Math.Max(planetRadius, previousMoon.SemiMajorAxis * (1d + previousMoon.Eccentricity) + previousMoonRadius) + GalaxyScale.RingInnerGap +
                    (ringIndex % 9) * (4.5d * GalaxyTuning.MoonOrbitScale);
                var ringElements = new OrbitalElements(ringAxis, 0.01d, (ringIndex % 4) * 1.5d * Mathf.Deg2Rad,
                    0.35d, 0d, anomaly);
                var particle = CreateSphere(discussionRoot, node,
                    node.metrics.reply_count > 0 ? "parent comment particle" : "comment ring particle",
                    Vector3.zero, 0.35f + (ringIndex % 4) * 0.08f, commentMaterial);
                if (node.metrics.reply_count > 0)
                {
                    GetOrAdd<CommentThreadNode>(particle).Initialize(node.metrics.reply_count);
                }
                GetOrAdd<KeplerOrbit>(particle).Initialize(planet.transform, ringElements, planet.GetComponent<GravitySource>()?.GravitationalParameter ?? 300d, GalaxyScale.MoonOrbitTimeScale);
                ringIndex++;
            }
            GalaxySurveyMembership.InitializeDescendants(discussionRoot, activeSystemId);
        }

        private void MaterializeThread(SpatialScene scene)
        {
            threadInspection?.PresentThread(scene);
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

        public static Vector3 ProjectRegionPosition(string stableId, float distanceScale = 1f)
        {
            var hash = StableHash(stableId);
            var angle = (hash % 10000) / 10000f * Mathf.PI * 2f;
            var radius = 420f + ((hash >> 14) % 1200);
            var height = ((hash >> 27) % 5000) / 5000f * 420f - 210f;
            return new Vector3(Mathf.Cos(angle) * radius, height, 1800f + Mathf.Sin(angle) * radius) *
                Mathf.Max(0f, distanceScale);
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
            var body = AcquireEntity(node, PrimitiveType.Sphere, parent);
            body.transform.localPosition = position;
            body.transform.localScale = Vector3.one * radius * 2f * (type == "post planet" ? GalaxyTuning.PlanetRadiusScale : 1f);
            body.GetComponent<MeshRenderer>().sharedMaterial = material;
            InitializeEntity(body, node, type);
            if (type == "post planet" || type == "subreddit star") GetOrAdd<CelestialMeshDetail>(body).Configure();
            else body.GetComponent<CelestialMeshDetail>()?.ReleaseDetail();
            return body;
        }

        private GameObject CreateCapsule(Transform parent, SpatialNode node, string type, Vector3 position, float radius, Material material)
        {
            var body = AcquireEntity(node, PrimitiveType.Capsule, parent);
            body.transform.localPosition = position;
            body.transform.localScale = new Vector3(radius, radius * 1.8f, radius);
            body.GetComponent<MeshRenderer>().sharedMaterial = material;
            InitializeEntity(body, node, type);
            return body;
        }

        private GameObject AcquireEntity(SpatialNode node, PrimitiveType shape, Transform parent)
        {
            var existing = registry?.FindEntity(node.id);
            if (existing == null) return entityPool.Acquire(shape,parent);
            var body = existing.gameObject;
            body.SetActive(false);
            var orbit = body.GetComponent<KeplerOrbit>(); if (orbit != null) orbit.enabled = false;
            var gravity = body.GetComponent<GravitySource>(); if (gravity != null) gravity.enabled = false;
            body.transform.SetParent(parent,false);
            body.transform.SetLocalPositionAndRotation(Vector3.zero,Quaternion.identity);
            return body;
        }

        private void InitializeEntity(GameObject body, SpatialNode node, string type)
        {
            body.GetComponent<GalaxyEntity>().Initialize(node.id,
                string.IsNullOrWhiteSpace(node.name) ? node.id : node.name, type, node.val, registry);
            body.GetComponent<LiveEntityMetadata>().Initialize(node);
            var catalog = loader.GetComponent<GalaxyVisualCatalogProvider>()?.Catalog;
            var prefab = node.type == "user" ? catalog?.UserShipForIndex(StablePresentationIndex(node.id)) :
                node.type == "comment" ? catalog?.discussionSatellitePrefab : null;
            if (prefab != null && body.transform.Find("Catalog Visual") == null)
            {
                var visual = Instantiate(prefab, body.transform, false);
                visual.name = "Catalog Visual";
                foreach (var collider in visual.GetComponentsInChildren<Collider>()) Destroy(collider);
            }
            body.SetActive(session != null ? session.Resident.TryFindActive((GalaxyEntityId)node.id,out _) :
                discoveryVisibility == null || discoveryVisibility.CanRender(body.GetComponent<GalaxyEntity>()));
        }

        private static T GetOrAdd<T>(GameObject body) where T : Behaviour
        {
            var component = body.GetComponent<T>();
            if (component == null) component = body.AddComponent<T>();
            component.enabled = true;
            return component;
        }

        private static Vector3 RenderPosition(Vector3 logical)
        {
            var origin = FloatingOrigin.Active != null ? FloatingOrigin.Active.LogicalOrigin : DoubleVector3.Zero;
            return new DoubleVector3(logical.x, logical.y, logical.z).RelativeTo(origin);
        }

        private void EnsureMaterials()
        {
            starMaterial ??= CreateSurfaceMaterial(new Color(1f, .69f, .23f), true);
            planetMaterial ??= CreateSurfaceMaterial(new Color(0.25f, 0.72f, 1f), false);
            userMaterial ??= CreateMaterial(new Color(1f, 0.62f, 0.18f));
            satelliteMaterial ??= CreateMaterial(new Color(0.78f, 0.88f, 1f));
            commentMaterial ??= CreateMaterial(new Color(0.55f, 0.72f, 0.92f));
            routeMaterial ??= CreateMaterial(new Color(0.18f, 0.62f, 0.95f));
            trafficMaterial ??= CreateMaterial(new Color(0.8f, 0.96f, 1f));
        }

        private static Material CreateSurfaceMaterial(Color color, bool star)
        {
            var shader = Resources.Load<Shader>("GalaxyPlanetSurface");
            if (shader == null) return CreateMaterial(color);
            var material = new Material(shader);
            material.SetColor("_Tint", color); material.SetFloat("_Star", star ? 1f : 0f);
            return material;
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
            if (root == null) return;
            foreach (var marker in root.GetComponentsInChildren<PooledGalaxyObject>(true))
            {
                var entity = marker.GetComponent<GalaxyEntity>();
                if (session == null || !session.Resident.TryFindLoaded((GalaxyEntityId)entity.StableId,out _))
                    entityPool.Release(marker.gameObject);
            }
            foreach (var route in root.GetComponentsInChildren<GalaxySurveyRoute>(true)) Object.Destroy(route.gameObject);
            root.gameObject.SetActive(false);
            retiredRoots.Add(root);
            root = null;
            ReapRoots();
        }
        private void ReapRoots()
        {
            for (var index = retiredRoots.Count - 1; index >= 0; index--)
            {
                var root = retiredRoots[index];
                if (root != null && root.GetComponentsInChildren<PooledGalaxyObject>(true).Length != 0) continue;
                if (root != null) Object.Destroy(root.gameObject);
                retiredRoots.RemoveAt(index);
            }
        }
        private void ResidentChanged(ResidentGalaxyChangeSet change)
        {
            foreach (var id in change.Released)
            {
                var entity = registry?.FindEntity(id.Value);
                if (entity != null) entityPool.Release(entity.gameObject);
            }
            foreach (var id in change.Deactivated)
            {
                var entity = registry?.FindEntity(id.Value);
                if (entity != null) entity.gameObject.SetActive(false);
            }
            foreach (var id in change.Activated)
            {
                var entity = registry?.FindEntity(id.Value);
                if (entity != null) entity.gameObject.SetActive(true);
            }
            ReapRoots();
        }

        private void OnDestroy()
        {
            if (loader != null) loader.SceneCommitted -= Materialize;
            if (navigation != null) navigation.ModeChanged -= MapModeChanged;
            if (session != null) session.ResidentChanged -= ResidentChanged;
            if (navigation != null) navigation.ViewChanged -= NavigationChanged;
            foreach (var material in new[] { starMaterial, planetMaterial, userMaterial, satelliteMaterial, commentMaterial, routeMaterial, trafficMaterial })
            {
                if (material != null) Object.Destroy(material);
            }
        }
    }
}
