using System.IO;
using ClustrGalaxy.World;
using UnityEditor;
using UnityEngine;

namespace ClustrGalaxy.Editor
{
    /// <summary>Curated CC0 models and attributed maps. Generated prefabs contain presentation only.</summary>
    public static class FreePresentationAssetBuilder
    {
        public const string Root = "Assets/ClustrGalaxy/Presentation/FreeAssets";
        private const string Generated = Root + "/Generated";
        public static void Configure(GalaxyVisualCatalog catalog)
        {
            if (!Directory.Exists(Root)) return;
            if (!AssetDatabase.IsValidFolder(Generated)) AssetDatabase.CreateFolder(Root, "Generated");
            catalog.activityMaterial = ActivityMaterial();
            EnsureTextureShapes();
            var names = new[] { "mars", "jupiter", "moon", "mercury", "neptune" };
            catalog.planetSurfaces = new Material[names.Length];
            for (var i = 0; i < names.Length; i++) catalog.planetSurfaces[i] = Surface(names[i], false);
            catalog.starSurface = Surface("sun", true);
            var ships = new[] { "craft_cargoA", "craft_cargoB", "craft_miner", "craft_racer", "craft_speederA", "craft_speederB" };
            catalog.userShipPrefabs = new GameObject[ships.Length];
            for (var i = 0; i < ships.Length; i++) catalog.userShipPrefabs[i] = Model("space-kit", ships[i]);
            catalog.discussionSatellitePrefab = Model("space-kit", "satelliteDish");
            catalog.cockpitConsole = Model("space-station-kit", "computer-wide");
            catalog.engineSound = AssetDatabase.LoadAssetAtPath<AudioClip>(Root + "/sci-fi-sounds/Audio/spaceEngineLow_000.ogg");
            catalog.scannerSound = AssetDatabase.LoadAssetAtPath<AudioClip>(Root + "/sci-fi-sounds/Audio/computerNoise_000.ogg");
            catalog.interfaceSound = AssetDatabase.LoadAssetAtPath<AudioClip>(Root + "/interface-sounds/Audio/click_001.ogg");
            catalog.confirmPrompt = Prompt("a"); catalog.backPrompt = Prompt("b");
            // The synchronous demo fallback only references small models. Maps/audio remain Addressable.
            var fallback = AssetDatabase.LoadAssetAtPath<GalaxyVisualCatalog>("Assets/ClustrGalaxy/Resources/GalaxyVisualCatalog.asset");
            if (fallback != null)
            {
                fallback.activityMaterial = catalog.activityMaterial;
                fallback.userShipPrefabs = catalog.userShipPrefabs;
                fallback.discussionSatellitePrefab = catalog.discussionSatellitePrefab;
                EditorUtility.SetDirty(fallback);
            }
        }
        private static Material ActivityMaterial()
        {
            // Keep the runtime activity shader and its emission variant as an explicit
            // dependency, independent of whichever third-party ships are selected.
            var path = Generated + "/activity-display.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) { material = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(material, path); }
            material.color = new Color(.4f, .8f, .9f);
            material.EnableKeyword("_EMISSION"); material.SetColor("_EmissionColor", new Color(.25f, .6f, .75f));
            EditorUtility.SetDirty(material); return material;
        }
        private static void EnsureTextureShapes()
        {
            // Repair already-imported drafts as well as configuring first imports.
            foreach (var path in Directory.GetFiles(Root, "*", SearchOption.AllDirectories))
            {
                var extension = Path.GetExtension(path);
                if (extension != ".jpg" && extension != ".png") continue;
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null || importer.textureShape == TextureImporterShape.Texture2D) continue;
                importer.textureShape = TextureImporterShape.Texture2D;
                importer.SaveAndReimport();
            }
        }
        private static Sprite Prompt(string key) => AssetDatabase.LoadAssetAtPath<Sprite>(Root + "/input-prompts/Xbox Series/Default/xbox_button_" + key + ".png");
        private static Material Surface(string name, bool star)
        {
            var path = Generated + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) { material = new Material(Resources.Load<Shader>("GalaxyPlanetSurface")); AssetDatabase.CreateAsset(material, path); }
            material.SetTexture("_SurfaceMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/SolarSystemScope/2k_" + name + ".jpg"));
            material.SetFloat("_UseSurfaceMap", 1); material.SetFloat("_Star", star ? 1 : 0);
            EditorUtility.SetDirty(material); return material;
        }
        private static GameObject Model(string pack, string name)
        {
            var path = Generated + "/" + name + ".prefab";
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/" + pack + "/Models/FBX format/" + name + ".fbx");
            if (source == null) throw new System.InvalidOperationException("Missing curated model: " + name);
            var root = new GameObject(name);
            try
            {
                var child = Object.Instantiate(source, root.transform, false); child.name = "Model";
                foreach (var collider in child.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(collider);
                var renderers = child.GetComponentsInChildren<Renderer>(true);
                var bounds = new Bounds(); var first = true;
                foreach (var renderer in renderers)
                {
                    if (first) { bounds = renderer.bounds; first = false; } else bounds.Encapsulate(renderer.bounds);
                    var materials = renderer.sharedMaterials;
                    for (var i = 0; i < materials.Length; i++) materials[i] = PaletteMaterial(pack, materials[i]);
                    renderer.sharedMaterials = materials;
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                }
                var extent = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
                if (extent <= 0) throw new System.InvalidOperationException("Empty curated model: " + name);
                child.transform.localScale /= extent; child.transform.localPosition = (child.transform.localPosition - bounds.center) / extent;
                return PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { Object.DestroyImmediate(root); }
        }
        private static Material PaletteMaterial(string pack, Material source)
        {
            var safeName = (source == null ? "default" : source.name).Replace('/', '_').Replace('\\', '_');
            var path = Generated + "/" + pack + "-" + safeName + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) { material = new Material(Shader.Find("Universal Render Pipeline/Unlit")); AssetDatabase.CreateAsset(material, path); }
            var texture = pack == "space-station-kit" ? AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/space-station-kit/Models/FBX format/Textures/colormap.png") : source?.mainTexture;
            material.SetTexture("_BaseMap", texture);
            material.SetColor("_BaseColor", texture != null ? Color.white : source != null && source.HasProperty("_Color") ? source.color : new Color(.4f,.52f,.6f));
            EditorUtility.SetDirty(material); return material;
        }
    }
    public sealed class FreePresentationAssetImporter : AssetPostprocessor
    {
        public override uint GetVersion() => 2;
        private bool Curated => assetPath.StartsWith(FreePresentationAssetBuilder.Root + "/", System.StringComparison.Ordinal);
        private void OnPreprocessTexture()
        {
            if (!Curated) return;
            var importer = (TextureImporter)assetImporter;
            importer.textureShape = TextureImporterShape.Texture2D;
            importer.maxTextureSize = 2048; importer.isReadable = false;
            importer.mipmapEnabled = !assetPath.Contains("input-prompts");
            importer.wrapModeU = TextureWrapMode.Repeat; importer.wrapModeV = TextureWrapMode.Clamp;
            if (assetPath.Contains("input-prompts")) { importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single; importer.alphaIsTransparency = true; }
        }
        private void OnPreprocessModel()
        {
            if (!Curated) return;
            var importer = (ModelImporter)assetImporter; importer.importAnimation = false; importer.addCollider = false;
        }
        private void OnPreprocessAudio()
        {
            if (!Curated) return;
            var importer = (AudioImporter)assetImporter; importer.forceToMono = true;
            var settings = importer.defaultSampleSettings; settings.loadType = AudioClipLoadType.DecompressOnLoad;
            importer.defaultSampleSettings = settings;
        }
    }
}
