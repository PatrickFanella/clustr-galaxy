using System;
using ClustrGalaxy.Data;
using ClustrGalaxy.Flight;
using ClustrGalaxy.Generation;
using ClustrGalaxy.Gameplay;
using ClustrGalaxy.Interaction;
using ClustrGalaxy.Simulation;
using ClustrGalaxy.Scanning;
using ClustrGalaxy.UI;
using ClustrGalaxy.World;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ClustrGalaxy.Bootstrap
{
    public static class GalaxyPrototypeBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreatePrototype()
        {
#if UNITY_EDITOR
            if (UnityEditor.SessionState.GetBool("ClustrGalaxy.SuppressAutomaticBootstrap", false))
            {
                return;
            }
#endif
            if (Object.FindAnyObjectByType<ObserverProbeController>() != null)
            {
                return;
            }

            RenderSettings.ambientLight = new Color(0.06f, 0.075f, 0.11f);
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(0.004f, 0.008f, 0.018f);
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.0012f;

            CreateStarfield();
            CreateSystem();
            CreateSurveySignalDestination();
            var dataLoader = CreateDataLoader();
            CreateProbe(dataLoader);
            var entityCount = Object.FindObjectsByType<GalaxyEntity>(FindObjectsInactive.Exclude).Length;
            Debug.Log($"[ClustrGalaxy] Prototype ready: observer probe, {entityCount} targetable entities, and deterministic Kepler orbits loaded.");
        }

        private static SpatialSceneLoader CreateDataLoader()
        {
            var dataClient = new GameObject("Clustr Public Data Client");
            var loader = dataClient.AddComponent<SpatialSceneLoader>();
            dataClient.AddComponent<GalacticOverviewRenderer>().Initialize(loader);
            dataClient.AddComponent<LiveGalaxyMaterializer>().Initialize(loader);
            loader.Initialize(
                "https://clustr.subcult.tv/api",
                GalaxyLaunchOptions.IsOfflineOnly(Environment.GetCommandLineArgs()));
            return loader;
        }

        private static void CreateProbe(SpatialSceneLoader dataLoader)
        {
            var existingCamera = Camera.main;
            if (existingCamera != null)
            {
                Object.Destroy(existingCamera.gameObject);
            }

            var probe = new GameObject("Observer Probe");
            probe.transform.SetPositionAndRotation(
                new Vector3(0f, 45f, (float)-GalaxyScale.FirstPlanetAxis), Quaternion.identity);
            var rigidbody = probe.AddComponent<Rigidbody>();
            rigidbody.mass = 1f;
            var flight = probe.AddComponent<ObserverProbeController>();
            var modeState = new GalaxyModeState();
            flight.Initialize(modeState);
            probe.AddComponent<FloatingOrigin>();
            var discoveryVisibility = probe.AddComponent<GalaxySurveyVisibilityController>();
            discoveryVisibility.Initialize(modeState, SpatialSceneLoader.DemoSurveySnapshotKey);
            discoveryVisibility.BindDataLoader(dataLoader);
            var targeting = probe.AddComponent<ProbeTargeting>();
            discoveryVisibility.BindTargeting(targeting);
            var surveyEvents = new SurveyEventBus();
            var scanner = probe.AddComponent<SignalScanner>();
            scanner.Initialize(modeState, discoveryVisibility, targeting, surveyEvents, new[]
            {
                new ScanRoute("route:technology-signal-horizon", "subreddit:technology",
                    "subreddit:signal-horizon", 8f),
                new ScanRoute("route:technology-weak-archive", "subreddit:technology",
                    "subreddit:weak-archive", 2f)
            });
            scanner.BindDataLoader(dataLoader);
            var threadInspection = probe.AddComponent<CommentThreadController>();
            threadInspection.Initialize(targeting);
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            var cameraObject = new GameObject("Probe Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetParent(probe.transform, false);
            var camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 72f;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 40000f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.002f, 0.004f, 0.012f);
            cameraObject.AddComponent<AudioListener>();
            var navigation = probe.AddComponent<GalaxyNavigationController>();
            navigation.Initialize(targeting, camera);
            navigation.BindDiscoveryVisibility(discoveryVisibility);
            var materializer = dataLoader.GetComponent<LiveGalaxyMaterializer>();
            var overview = dataLoader.GetComponent<GalacticOverviewRenderer>();
            overview.BindNavigation(navigation);
            overview.BindDiscoveryVisibility(discoveryVisibility);
            materializer.BindNavigation(navigation, overview);
            materializer.BindDiscoveryVisibility(discoveryVisibility);
            materializer.BindThreadInspection(threadInspection);
            threadInspection.BindNavigation(navigation);
            probe.AddComponent<LiveGalaxySelectionController>().Initialize(targeting, dataLoader, navigation);

            var hud = probe.AddComponent<ProbeHud>();
            probe.AddComponent<GalaxyPerformanceMonitor>();
            hud.Initialize(flight, targeting, dataLoader, navigation, modeState, discoveryVisibility, scanner,
                threadInspection);
        }

        private static void CreateSystem()
        {
            const long revisionId = 9;
            const long catalogId = 3;
            const double starMu = 720d;
            const float timeScale = GalaxyScale.PlanetOrbitTimeScale;

            var root = new GameObject("Region: Technology Community Cluster");
            var star = CreateBody(root.transform, "subreddit:technology", "r/technology", "subreddit star", 14820,
                Vector3.zero, 22f, new Color(0.7f, 1f, 0.35f));
            star.gameObject.AddComponent<GravitySource>().Initialize(starMu, 14d, 12d);
            var light = star.gameObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(0.68f, 1f, 0.45f);
            light.range = 2200f;
            light.intensity = 5f;

            var posts = new[]
            {
                new PostSeed("post:open-source-ai", "Open source AI field guide", 1840, 7200, new Color(0.25f, 0.82f, 1f)),
                new PostSeed("post:linux-handhelds", "Linux handhelds in 2026", 720, 5100, new Color(1f, 0.72f, 0.22f)),
                new PostSeed("post:right-to-repair", "Right to repair megathread", 310, 3900, new Color(1f, 0.3f, 0.56f)),
                new PostSeed("post:privacy-tools", "Community privacy toolkit", 96, 2300, new Color(0.62f, 0.42f, 1f)),
                new PostSeed("post:retro-computing", "Retro computing showcase", 28, 980, new Color(0.25f, 1f, 0.68f))
            };

            var previousElements = default(OrbitalElements);
            var previousRadius = 0d;
            for (var index = 0; index < posts.Length; index++)
            {
                var seed = posts[index];
                var activity = MetricNormalizer.NormalizeLog(seed.CommentCount, 1, 2000);
                var visualRadius = Mathf.Lerp(4f, 10f, (float)Math.Pow(activity, 0.65d));
                var postMu = Mathf.Lerp(180f, 420f, (float)Math.Sqrt(activity));
                var trial = StableOrbitGenerator.GeneratePlanet(
                    revisionId, catalogId, "subreddit:technology", seed.Id, 1d);
                var semiMajorAxis = index == 0
                    ? GalaxyScale.FirstPlanetAxis
                    : OrbitLaneAllocator.AllocateAfter(
                        previousElements, previousRadius, trial.Eccentricity, visualRadius, GalaxyScale.PlanetLaneGap);
                var elements = StableOrbitGenerator.GeneratePlanet(
                    revisionId, catalogId, "subreddit:technology", seed.Id, semiMajorAxis);

                var planet = CreateBody(root.transform, seed.Id, seed.Title, "post planet", seed.CommentCount,
                    Vector3.zero, visualRadius, seed.Color);
                planet.gameObject.AddComponent<KeplerOrbit>().Initialize(star, elements, starMu, timeScale);
                planet.gameObject.AddComponent<GravitySource>().Initialize(postMu,
                    Math.Max(1.5d, visualRadius * 0.75d), 6d);
                CreateOrbitLine(root.transform, star, elements, starMu,
                    new Color(seed.Color.r, seed.Color.g, seed.Color.b, 0.28f), 0.075f);
                CreateComments(planet, seed, elements, visualRadius, postMu,
                    starMu, revisionId, catalogId, GalaxyScale.MoonOrbitTimeScale);

                previousElements = elements;
                previousRadius = visualRadius;
            }

            CreateTravelers(root.transform, star, starMu, revisionId, catalogId, timeScale,
                previousElements.SemiMajorAxis + GalaxyScale.TravelerOrbitOffset);
            GalaxySurveyMembership.InitializeDescendants(root.transform, "subreddit:technology");
        }

        private static void CreateSurveySignalDestination()
        {
            var root = new GameObject("Survey Signal Horizon");
            var destination = CreateBody(root.transform, "subreddit:signal-horizon", "r/signal-horizon",
                "subreddit star", 0, new Vector3(14500f, 2800f, -12000f), 16f, new Color(0.35f, 0.45f, 0.9f));
            destination.gameObject.AddComponent<GalaxySurveyMembership>().Initialize("subreddit:signal-horizon");
        }

        private static void CreateComments(
            Transform planet,
            PostSeed post,
            OrbitalElements planetElements,
            float planetVisualRadius,
            double planetMu,
            double starMu,
            long revisionId,
            long catalogId,
            float timeScale)
        {
            var systemRoot = planet.parent;
            var physicalPlanetRadius = planetVisualRadius * 0.35d;
            var maximumMoonAxis = OrbitStability.MaximumProgradeMoonAxis(
                planetElements.SemiMajorAxis, planetMu, starMu);
            var namedMoonCount = Math.Min(3, 1 + (int)(MetricNormalizer.NormalizeLog(post.CommentCount, 1, 2000) * 3d));
            var nextAxis = Math.Max(planetVisualRadius + GalaxyScale.MoonInitialGap,
                2d * (physicalPlanetRadius + 0.8d));
            var created = 0;

            for (var index = 0; index < namedMoonCount; index++)
            {
                var moonRadius = Mathf.Lerp(0.8f, 2.2f, (index + 1f) / (namedMoonCount + 1f));
                var commentId = $"comment:{post.Id.Substring(5)}-{index + 1}";
                var trial = StableOrbitGenerator.GenerateMoon(
                    revisionId, catalogId, post.Id, commentId, 1d);
                nextAxis = Math.Max(nextAxis, OrbitStability.MinimumVisualMoonAxis(
                    planetVisualRadius, moonRadius, trial.Eccentricity, GalaxyScale.MoonVisualGap));
                var elements = StableOrbitGenerator.GenerateMoon(
                    revisionId, catalogId, post.Id, commentId, nextAxis);
                if (elements.SemiMajorAxis * (1d + elements.Eccentricity) > maximumMoonAxis)
                {
                    break;
                }

                var moon = CreateBody(systemRoot, commentId, $"Active parent comment {index + 1}",
                    "discussion satellite", Math.Max(1, post.CommentCount / (index + 3)),
                    Vector3.zero, moonRadius, Color.Lerp(post.Color, Color.white, 0.42f));
                var visualCatalog = GalaxyVisualCatalog.Load();
                if (visualCatalog != null && visualCatalog.discussionSatellitePrefab != null)
                {
                    moon.GetComponent<MeshRenderer>().enabled = false;
                    AttachVisual(moon, visualCatalog.discussionSatellitePrefab, moonRadius * 2f);
                }
                moon.gameObject.AddComponent<KeplerOrbit>().Initialize(planet, elements, planetMu, timeScale);
                AttachPrototypeThread(moon, post, index);
                CreateOrbitLine(systemRoot, planet, elements, planetMu,
                    new Color(post.Color.r, post.Color.g, post.Color.b, 0.22f), 0.035f);
                created++;
                nextAxis = elements.SemiMajorAxis * (1d + elements.Eccentricity) +
                    GalaxyScale.MoonLaneGap + 3d * moonRadius;
            }

            if (post.CommentCount > created)
            {
                var minimumRingAxis = planetVisualRadius + GalaxyScale.RingInnerGap;
                var ringAxis = Math.Min(maximumMoonAxis * 0.9d,
                    Math.Max(minimumRingAxis, nextAxis));
                if (ringAxis >= minimumRingAxis)
                {
                    CreateCommentParticleRing(systemRoot, planet, post, ringAxis, planetMu, timeScale);
                }
            }
        }

        private static void CreateTravelers(
            Transform root,
            Transform star,
            double starMu,
            long revisionId,
            long catalogId,
            float timeScale,
            double innerAxis)
        {
            var visualCatalog = GalaxyVisualCatalog.Load();
            for (var index = 0; index < 6; index++)
            {
                var id = $"user:traveler-{index:00}";
                var ship = new GameObject();
                ship.transform.SetParent(root, false);
                var collider = ship.AddComponent<CapsuleCollider>();
                collider.radius = 0.45f;
                collider.height = 1.8f;
                ship.AddComponent<GalaxyEntity>().Initialize(id, $"traveler_{index:00}", "user ship", 140 + index * 37);
                var prefab = visualCatalog != null ? visualCatalog.UserShipForIndex(index) : null;
                if (prefab != null)
                {
                    AttachVisual(ship.transform, prefab, 1.8f);
                }
                else
                {
                    var fallback = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                    Object.Destroy(fallback.GetComponent<Collider>());
                    fallback.transform.SetParent(ship.transform, false);
                    fallback.transform.localScale = new Vector3(0.36f, 0.85f, 0.36f);
                    fallback.GetComponent<MeshRenderer>().material = CreateMaterial(new Color(0.38f, 0.72f, 1f), true);
                }
                var elements = StableOrbitGenerator.GeneratePlanet(
                    revisionId, catalogId, "subreddit:technology", id,
                    innerAxis + index * GalaxyScale.TravelerLaneGap);
                ship.AddComponent<KeplerOrbit>().Initialize(star, elements, starMu, GalaxyScale.ShipOrbitTimeScale);
            }
        }

        private static Transform CreateBody(Transform parent, string id, string label, string type, long value, Vector3 position, float scale, Color color)
        {
            var body = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            body.transform.SetParent(parent, false);
            body.transform.localPosition = position;
            body.transform.localScale = Vector3.one * (scale * 2f);
            body.AddComponent<GalaxyEntity>().Initialize(id, label, type, value);
            body.GetComponent<MeshRenderer>().material = CreateMaterial(color, true);
            return body.transform;
        }

        private static void CreateOrbitLine(
            Transform layerParent,
            Transform center,
            OrbitalElements elements,
            double parentMu,
            Color color,
            float width)
        {
            const int segments = 96;
            var orbit = new GameObject($"Orbit {elements.SemiMajorAxis:0.0}");
            orbit.transform.SetParent(layerParent, false);
            orbit.AddComponent<OrbitAnchorFollower>().Initialize(center);
            var line = orbit.AddComponent<LineRenderer>();
            line.loop = true;
            line.useWorldSpace = false;
            line.positionCount = segments;
            line.widthMultiplier = width;
            line.material = CreateMaterial(color, false);
            var period = KeplerOrbitMath.Period(elements, parentMu);
            for (var i = 0; i < segments; i++)
            {
                line.SetPosition(i, KeplerOrbitMath.PositionAtTime(elements, parentMu, period * i / segments));
            }
        }

        private static void CreateCommentParticleRing(
            Transform systemRoot,
            Transform planet,
            PostSeed post,
            double axis,
            double planetMu,
            float timeScale)
        {
            var activity = MetricNormalizer.NormalizeLog(post.CommentCount, 1, 2000);
            var parentCount = 10 + Mathf.RoundToInt((float)activity * 18f);
            var material = CreateMaterial(Color.Lerp(post.Color, Color.white, 0.35f), true);
            for (var index = 0; index < parentCount; index++)
            {
                var id = $"comment:{post.Id.Substring(5)}-parent-{index + 1}";
                var anomaly = CommentRingLayout.AngleRadians(id);
                var radialVariation = 1d + 0.055d * Math.Sin(anomaly * 7d);
                var elements = new OrbitalElements(
                    axis * radialVariation,
                    0.012d,
                    (2d + index % 4) * Mathf.Deg2Rad,
                    0.4d,
                    0d,
                    anomaly);
                var particle = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                particle.transform.SetParent(systemRoot, false);
                var radius = Mathf.Lerp(0.35f, 0.75f, (index % 5) / 4f);
                particle.transform.localScale = Vector3.one * radius * 2f;
                particle.GetComponent<MeshRenderer>().sharedMaterial = material;
                particle.GetComponent<SphereCollider>().radius = 1.25f;
                particle.AddComponent<GalaxyEntity>().Initialize(
                    id, $"Parent comment {index + 1}", "parent comment", Math.Max(1, post.CommentCount / parentCount));
                particle.AddComponent<KeplerOrbit>().Initialize(planet, elements, planetMu, timeScale);
                AttachPrototypeThread(particle.transform, post, index + 10);
            }
        }

        private static void AttachPrototypeThread(Transform parentComment, PostSeed post, int parentIndex)
        {
            var replyCount = 2 + parentIndex % 4;
            var parent = parentComment.GetComponent<GalaxyEntity>();
            var nodes = new SpatialNode[replyCount + 1];
            nodes[0] = new SpatialNode
            {
                id = parent.StableId,
                type = "comment",
                author_name = "prototype parent",
                metrics = new SpatialNodeMetrics { reply_count = replyCount }
            };
            for (var replyIndex = 0; replyIndex < replyCount; replyIndex++)
            {
                var parentId = replyIndex < 2 ? parent.StableId :
                    $"{parent.StableId}-reply-{replyIndex}";
                nodes[replyIndex + 1] = new SpatialNode
                {
                    id = $"{parent.StableId}-reply-{replyIndex + 1}",
                    type = "comment",
                    parent_id = parentId,
                    author_name = $"prototype_user_{parentIndex + replyIndex:00}",
                    excerpt = $"Prototype reply {replyIndex + 1} in {post.Title}.",
                    metrics = new SpatialNodeMetrics
                    {
                        score = replyCount - replyIndex,
                        reply_count = replyIndex + 1 < replyCount ? 1 : 0
                    }
                };
            }
            var model = ThreadInspectionModel.Build(new SpatialScene
            {
                scope = "thread",
                root_id = parent.StableId,
                nodes = nodes
            });
            parentComment.gameObject.AddComponent<CommentThreadNode>().Initialize(replyCount, model);
        }

        private static void CreateStarfield()
        {
            var stars = new GameObject("Distant Starfield");
            var random = new System.Random(64103);
            var material = CreateMaterial(new Color(0.58f, 0.78f, 1f), true);
            var meshSource = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            var mesh = meshSource.GetComponent<MeshFilter>().sharedMesh;
            Object.Destroy(meshSource);
            var matrices = new Matrix4x4[260];
            for (var i = 0; i < 260; i++)
            {
                var direction = RandomDirection(random);
                var distance = 4000f + (float)random.NextDouble() * 22000f;
                var scale = Vector3.one * (1.5f + (float)random.NextDouble() * 5f);
                matrices[i] = Matrix4x4.TRS(direction * distance, Quaternion.identity, scale);
            }
            stars.AddComponent<InstancedStarfield>().Initialize(mesh, material, matrices);
        }

        private static Vector3 RandomDirection(System.Random random)
        {
            var z = (float)(random.NextDouble() * 2.0 - 1.0);
            var angle = (float)(random.NextDouble() * Mathf.PI * 2f);
            var radius = Mathf.Sqrt(1f - z * z);
            return new Vector3(radius * Mathf.Cos(angle), z, radius * Mathf.Sin(angle));
        }

        private static Material CreateMaterial(Color color, bool emissive)
        {
            var shader = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline != null
                ? Shader.Find("Universal Render Pipeline/Lit")
                : Shader.Find("Standard");
            shader ??= Shader.Find("Sprites/Default");
            var material = new Material(shader) { color = color };
            if (emissive && material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color * 1.8f);
            }
            return material;
        }

        private static void AttachVisual(Transform entityRoot, GameObject prefab, float targetDiameter)
        {
            var visual = Object.Instantiate(prefab, entityRoot, false);
            visual.name = $"Visual: {prefab.name}";
            visual.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            var renderers = visual.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                var bounds = renderers[0].bounds;
                for (var index = 1; index < renderers.Length; index++)
                {
                    bounds.Encapsulate(renderers[index].bounds);
                }

                var largestDimension = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
                if (largestDimension > 0.0001f)
                {
                    visual.transform.localScale *= targetDiameter / largestDimension;
                }
            }

            foreach (var importedCollider in visual.GetComponentsInChildren<Collider>())
            {
                importedCollider.enabled = false;
            }
        }

        private readonly struct PostSeed
        {
            public PostSeed(string id, string title, long commentCount, long score, Color color)
            {
                Id = id;
                Title = title;
                CommentCount = commentCount;
                Score = score;
                Color = color;
            }

            public string Id { get; }
            public string Title { get; }
            public long CommentCount { get; }
            public long Score { get; }
            public Color Color { get; }
        }
    }
}
