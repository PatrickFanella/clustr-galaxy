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
        private static bool demoRequested;
        public static bool IsDemo => demoRequested || GalaxyLaunchOptions.IsDemo(Environment.GetCommandLineArgs());
        public static void StartDemo()
        {
            demoRequested = true;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += DemoSceneLoaded;
            UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
        }
        private static void DemoSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= DemoSceneLoaded;
            CreatePrototype();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreatePrototype()
        {
#if UNITY_EDITOR
            if (Array.Exists(Environment.GetCommandLineArgs(), value => value == "-runTests") ||
                UnityEditor.SessionState.GetBool("ClustrGalaxy.SuppressAutomaticBootstrap", false))
            {
                return;
            }
#endif
            RenderSettings.ambientLight = new Color(0.06f, 0.075f, 0.11f);
            RenderSettings.fog = false;
            RenderSettings.fogColor = new Color(0.004f, 0.008f, 0.018f);
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.0012f;

            var registry = new GalaxyEntityRegistry();
            var starfield = CreateStarfield();
            if (IsDemo)
            {
                CreateSystem(registry);
                CreateSurveySignalDestination(registry);
                CreateSurveyRoutes(registry);
            }
            var dataLoader = CreateDataLoader();
            CreateProbe(dataLoader, starfield, registry);
            var entityCount = registry.Entities.Count;
            Debug.Log($"[ClustrGalaxy] Prototype ready: observer probe, {entityCount} targetable entities, and deterministic Kepler orbits loaded.");
        }

        private static SpatialSceneLoader CreateDataLoader()
        {
            var dataClient = new GameObject("Clustr Public Data Client");
            var loader = dataClient.AddComponent<SpatialSceneLoader>();
            dataClient.AddComponent<GalacticOverviewRenderer>().Initialize(loader);
            dataClient.AddComponent<LiveGalaxyMaterializer>().Initialize(loader);
            var arguments = Environment.GetCommandLineArgs();
            if (GalaxyLaunchOptions.IsLiveSmoke(arguments))
            {
                dataClient.AddComponent<GalaxyLiveDataSmokeRunner>().Initialize(loader);
            }
            loader.Initialize(
                GalaxyLaunchOptions.FixtureApi(arguments) ?? "https://clustr.subcult.tv/api",
                GalaxyLaunchOptions.IsOfflineOnly(arguments));
            return loader;
        }

        private static void CreateProbe(SpatialSceneLoader dataLoader, InstancedStarfield starfield,
            GalaxyEntityRegistry registry)
        {
            var existingCamera = Camera.main;
            if (existingCamera != null)
            {
                Object.Destroy(existingCamera.gameObject);
            }

            var probe = new GameObject("Observer Probe");
            probe.transform.SetPositionAndRotation(
                new Vector3(0f, 45f, (float)-GalaxyScale.FirstPlanetAxis), Quaternion.identity);
            if (IsDemo)
            {
                var firstPlanet=registry.FindEntity("post:open-source-ai");
                if(firstPlanet!=null)
                {
                    var radial=firstPlanet.transform.position.normalized;
                    var position=firstPlanet.transform.position+radial*750+Vector3.Cross(Vector3.up,radial)*800+Vector3.up*280;
                    probe.transform.SetPositionAndRotation(position,Quaternion.LookRotation(Vector3.Lerp(firstPlanet.transform.position,Vector3.zero,.06f)-position,Vector3.up));
                }
            }
            var rigidbody = probe.AddComponent<Rigidbody>();
            rigidbody.mass = 1f;
            var flight = probe.AddComponent<ObserverProbeController>();
            probe.AddComponent<FloatingOrigin>();
            var discoveryVisibility = probe.AddComponent<GalaxySurveyVisibilityController>();
            var targeting = probe.AddComponent<ProbeTargeting>();
            var scanner = probe.AddComponent<SignalScanner>();
            var threadInspection = probe.AddComponent<CommentThreadController>();
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            var cameraObject = new GameObject("Probe Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetParent(probe.transform, false);
            var camera = cameraObject.AddComponent<Camera>();
            camera.cullingMask &= ~(1 << 31); // Observatory models render only in their dedicated local view.
            camera.fieldOfView = 72f;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 250000f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.002f, 0.004f, 0.012f);
            cameraObject.AddComponent<AudioListener>();
            var navigation = probe.AddComponent<GalaxyNavigationController>();
            var materializer = dataLoader.GetComponent<LiveGalaxyMaterializer>();
            var overview = dataLoader.GetComponent<GalacticOverviewRenderer>();
            probe.AddComponent<LiveGalaxySelectionController>();
            dataLoader.GetComponent<GalaxyLiveDataSmokeRunner>()?.BindPlayer(targeting, registry);

            var hud = probe.AddComponent<ProbeHud>();
            probe.AddComponent<CockpitFlightDeck>().Initialize(camera,hud,navigation);
            var performance = probe.AddComponent<GalaxyPerformanceMonitor>();
            if(Array.Exists(Environment.GetCommandLineArgs(),value=>value=="--clustr-visual-smoke"))probe.AddComponent<GalaxyVisualSmokeRunner>();
            var arguments = Environment.GetCommandLineArgs();
            if (GalaxyLaunchOptions.IsBetaProfile(arguments))
            {
                probe.AddComponent<GalaxyBetaProfileRunner>().Initialize(dataLoader, performance,
                    GalaxyLaunchOptions.ProfileDurationSeconds(arguments));
            }
            var gameplay = probe.AddComponent<GalaxySessionBehaviour>();
            gameplay.BindStarfield(starfield);
            gameplay.Initialize(flight, targeting, dataLoader, navigation, discoveryVisibility, scanner,
                threadInspection, hud, registry, new[]
                {
                    new ScanRoute("route:technology-signal-horizon", "subreddit:technology",
                        "subreddit:signal-horizon", 8f),
                    new ScanRoute("route:technology-weak-archive", "subreddit:technology",
                        "subreddit:weak-archive", 2f)
                });
            overview.BindNavigation(navigation);
            overview.BindDiscoveryVisibility(discoveryVisibility);
            materializer.BindNavigation(navigation, overview);
            materializer.BindDiscoveryVisibility(discoveryVisibility);
            materializer.BindThreadInspection(threadInspection);
        }

        private static void CreateSystem(GalaxyEntityRegistry registry)
        {
            const long revisionId = 9;
            const long catalogId = 3;
            const double starMu = GalaxyTuning.SunSurfaceGravity * GalaxyTuning.SunRadius * GalaxyTuning.SunRadius;
            const float timeScale = GalaxyScale.PlanetOrbitTimeScale;

            var root = new GameObject("Region: Technology Community Cluster");
            root.AddComponent<CelestialFrame>();
            var star = CreateBody(root.transform, "subreddit:technology", "r/technology", "subreddit star", 14820,
                Vector3.zero, GalaxyTuning.SunRadius, new Color(1f, .69f, .23f), registry);
            star.gameObject.AddComponent<SolarBodyVisual>().Initialize(new Color(1f,.69f,.23f),true,true);
            star.gameObject.AddComponent<GravitySource>().Initialize(GalaxyTuning.SunSurfaceGravity * GalaxyTuning.SunRadius * GalaxyTuning.SunRadius, GalaxyTuning.SunRadius * .1d, 12d);
            var light = star.gameObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, .78f, .5f);
            light.range = 120000f;
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
                var visualRadius = GalaxyTuning.PlanetRadiusScale * Mathf.Lerp(4f, 10f, (float)Math.Pow(activity, 0.65d));
                var postMu = GalaxyTuning.PlanetSurfaceGravity * visualRadius * visualRadius;
                var trial = StableOrbitGenerator.GeneratePlanet(
                    revisionId, catalogId, "subreddit:technology", seed.Id, 1d);
                var moonCount = Math.Min(3, 1 + (int)(activity * 3d));
                var elements = CelestialLayout.Planet(trial, visualRadius, moonCount, GalaxyTuning.SunRadius,
                    starMu, previousElements, previousRadius);

                var planet = CreateBody(root.transform, seed.Id, seed.Title, "post planet", seed.CommentCount,
                    Vector3.zero, visualRadius, seed.Color, registry);
                planet.gameObject.AddComponent<KeplerOrbit>().Initialize(star, elements, starMu, timeScale);
                planet.gameObject.AddComponent<GravitySource>().Initialize(GalaxyTuning.PlanetSurfaceGravity * visualRadius * visualRadius,
                    visualRadius * .1d, 6d);
                CreateOrbitLine(root.transform, star, elements, starMu,
                    new Color(seed.Color.r, seed.Color.g, seed.Color.b, 0.28f), 0.075f);
                CreateComments(planet, seed, elements, visualRadius, postMu,
                    starMu, revisionId, catalogId, GalaxyScale.MoonOrbitTimeScale, registry);

                previousElements = elements;
                previousRadius = CelestialLayout.SatelliteEnvelope(visualRadius, moonCount);
            }

            CreateTravelers(root.transform, star, starMu, revisionId, catalogId, timeScale,
                previousElements.SemiMajorAxis + GalaxyScale.TravelerOrbitOffset, registry);
            GalaxySurveyMembership.InitializeDescendants(root.transform, "subreddit:technology");
        }

        private static void CreateSurveySignalDestination(GalaxyEntityRegistry registry)
        {
            var root = new GameObject("Survey Signal Horizon");
            root.AddComponent<CelestialFrame>();
            var destination = CreateBody(root.transform, "subreddit:signal-horizon", "r/signal-horizon",
                "subreddit star", 0, new Vector3(60000f, 18000f, -50000f), GalaxyTuning.SunRadius, new Color(0.35f, 0.45f, 0.9f), registry);
            destination.gameObject.AddComponent<GalaxySurveyMembership>().Initialize("subreddit:signal-horizon");
            var archive = CreateBody(root.transform, "subreddit:weak-archive", "r/weak-archive",
                "subreddit star", 0, new Vector3(-60000f, -16000f, -50000f), GalaxyTuning.SunRadius, new Color(0.6f, 0.4f, 0.75f), registry);
            archive.gameObject.AddComponent<GalaxySurveyMembership>().Initialize("subreddit:weak-archive");
            foreach (var marker in new[] { destination, archive })
            {
                marker.gameObject.AddComponent<GravitySource>().Initialize(GalaxyTuning.SunSurfaceGravity * GalaxyTuning.SunRadius * GalaxyTuning.SunRadius, GalaxyTuning.SunRadius * .1d, 12d);
                marker.gameObject.AddComponent<SolarBodyVisual>().Initialize(new Color(.8f, .65f, 1f), true);
            }
        }

        private static void CreateSurveyRoutes(GalaxyEntityRegistry registry)
        {
            CreateSurveyRoute(registry, "signal-horizon", 8, new Color(0.35f, 0.65f, 0.95f));
            CreateSurveyRoute(registry, "weak-archive", 2, new Color(0.55f, 0.4f, 0.7f));
        }

        private static void CreateSurveyRoute(GalaxyEntityRegistry registry, string destination, long weight, Color color)
        {
            var root = new GameObject("Authored route to " + destination);
            var sourceId = "subreddit:technology";
            var targetId = "subreddit:" + destination;
            var line = root.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.positionCount = 2;
            line.SetPosition(0, registry.FindEntity(sourceId).transform.position);
            line.SetPosition(1, registry.FindEntity(targetId).transform.position);
            line.startWidth = line.endWidth = weight >= 8f ? 2f : 1f;
            line.sharedMaterial = CreateMaterial(color, true);
            root.AddComponent<GalaxySurveyRoute>().Initialize("route:technology-" + destination,
                sourceId, targetId, registry, weight);
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
            float timeScale,
            GalaxyEntityRegistry registry)
        {
            var systemRoot = planet.parent;
            var physicalPlanetRadius = planetVisualRadius * 0.35d;
            var maximumMoonAxis = OrbitStability.MaximumProgradeMoonAxis(
                planetElements.SemiMajorAxis * (1d - planetElements.Eccentricity), planetMu, starMu);
            var namedMoonCount = Math.Min(3, 1 + (int)(MetricNormalizer.NormalizeLog(post.CommentCount, 1, 2000) * 3d));
            var nextAxis = Math.Max(planetVisualRadius + GalaxyScale.MoonInitialGap,
                2d * (physicalPlanetRadius + 0.8d));
            var created = 0;
            var previousMoon = default(OrbitalElements);
            var previousMoonRadius = 0d;

            for (var index = 0; index < namedMoonCount; index++)
            {
                var moonRadius = planetVisualRadius * Mathf.Lerp(.02f, .05f, (index + 1f) / (namedMoonCount + 1f));
                var commentId = $"comment:{post.Id.Substring(5)}-{index + 1}";
                var trial = StableOrbitGenerator.GenerateMoon(
                    revisionId, catalogId, post.Id, commentId, 1d);
                nextAxis = Math.Max(nextAxis, OrbitStability.MinimumVisualMoonAxis(
                    planetVisualRadius, moonRadius, trial.Eccentricity, GalaxyScale.MoonVisualGap));
                var elements = CelestialLayout.Moon(trial, planetVisualRadius, moonRadius, previousMoon, previousMoonRadius);
                previousMoon = elements;
                previousMoonRadius = moonRadius;
                if (elements.SemiMajorAxis * (1d + elements.Eccentricity) > maximumMoonAxis)
                {
                    break;
                }

                var moon = CreateBody(systemRoot, commentId, $"Active parent comment {index + 1}",
                    "discussion satellite", Math.Max(1, post.CommentCount / (index + 3)),
                    Vector3.zero, moonRadius, Color.Lerp(post.Color, Color.white, 0.42f), registry);
                var visualCatalog = GalaxyVisualCatalog.Load();
                if (visualCatalog != null && visualCatalog.discussionSatellitePrefab != null)
                {
                    moon.GetComponent<MeshRenderer>().enabled = false;
                    AttachVisual(moon, visualCatalog.discussionSatellitePrefab, moonRadius * 2f);
                }
                moon.gameObject.AddComponent<KeplerOrbit>().Initialize(planet, elements, planetMu, timeScale);
                moon.gameObject.AddComponent<GravitySource>().Initialize(GalaxyTuning.PlanetSurfaceGravity * moonRadius * moonRadius, moonRadius * .1d, 6d);
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
                    CreateCommentParticleRing(systemRoot, planet, post, ringAxis, planetMu, timeScale, registry);
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
            double innerAxis,
            GalaxyEntityRegistry registry)
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
                ship.AddComponent<GalaxyEntity>().Initialize(id, $"traveler_{index:00}", "user ship", 140 + index * 37, registry);
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

        private static Transform CreateBody(Transform parent, string id, string label, string type, long value,
            Vector3 position, float scale, Color color, GalaxyEntityRegistry registry)
        {
            var body = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            body.transform.SetParent(parent, false);
            body.transform.localPosition = position;
            body.transform.localScale = Vector3.one * (scale * 2f);
            body.AddComponent<GalaxyEntity>().Initialize(id, label, type, value, registry);
            if(type == "post planet") body.AddComponent<SolarBodyVisual>().Initialize(color,false);
            else body.GetComponent<MeshRenderer>().material = CreateMaterial(color, true);
            if (type == "post planet" || type == "subreddit star") body.AddComponent<CelestialMeshDetail>();
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
            const int segments = 512;
            var orbit = new GameObject($"Orbit {elements.SemiMajorAxis:0.0}");
            orbit.transform.SetParent(layerParent, false);
            orbit.AddComponent<OrbitAnchorFollower>().Initialize(center);
            var line = orbit.AddComponent<LineRenderer>();
            line.loop = true;
            line.useWorldSpace = false;
            line.positionCount = segments;
            line.widthMultiplier = width * 5f;
            line.numCornerVertices = 3;
            line.material = CreateMaterial(color, false);

            for (var i = 0; i < segments; i++)
            {
                line.SetPosition(i, KeplerOrbitMath.PositionAtEccentricAnomaly(elements, Math.PI * 2d * i / segments));
            }
        }

        private static void CreateCommentParticleRing(
            Transform systemRoot,
            Transform planet,
            PostSeed post,
            double axis,
            double planetMu,
            float timeScale,
            GalaxyEntityRegistry registry)
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
                    id, $"Parent comment {index + 1}", "parent comment", Math.Max(1, post.CommentCount / parentCount), registry);
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

        private static InstancedStarfield CreateStarfield()
        {
            var stars = new GameObject("Distant Starfield");
            var random = new System.Random(64103);
            var material = CreateMaterial(new Color(0.58f, 0.78f, 1f), true);
            var meshSource = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            var mesh = meshSource.GetComponent<MeshFilter>().sharedMesh;
            Object.Destroy(meshSource);
            var matrices = new Matrix4x4[900];
            for (var i = 0; i < matrices.Length; i++)
            {
                var direction = RandomDirection(random);
                var distance = 4000f + (float)random.NextDouble() * 22000f;
                var scale = Vector3.one * distance * (.0007f + (float)random.NextDouble() * .0012f);
                matrices[i] = Matrix4x4.TRS(direction * distance, Quaternion.identity, scale);
            }
            var starfield = stars.AddComponent<InstancedStarfield>();
            starfield.Initialize(mesh, material, matrices);
            return starfield;
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
