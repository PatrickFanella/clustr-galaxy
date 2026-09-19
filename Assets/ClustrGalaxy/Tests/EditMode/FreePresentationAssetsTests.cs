using ClustrGalaxy.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ClustrGalaxy.Tests.EditMode
{
    public sealed class FreePresentationAssetsTests
    {
        private static GalaxyVisualCatalog Catalog => AssetDatabase.LoadAssetAtPath<GalaxyVisualCatalog>(
            "Assets/ClustrGalaxy/Presentation/GalaxyVisualCatalog.asset");

        [Test]
        public void MissingCatalogSurfacesPreserveProceduralFallback()
        {
            var catalog = ScriptableObject.CreateInstance<GalaxyVisualCatalog>();
            try { Assert.That(catalog.SurfaceFor("post:test", false), Is.Null); Assert.That(catalog.SurfaceFor("star:test", true), Is.Null); }
            finally { Object.DestroyImmediate(catalog); }
        }
        [Test]
        public void StableIdentityChoosesTheSameSharedSurfaceAfterReload()
        {
            Assert.That(Catalog.planetSurfaces.Length, Is.EqualTo(5));
            var copy = Object.Instantiate(Catalog);
            try
            {
                foreach (var id in new[] { "post:one", "post:two", "post:three", "post:unicode-星", "" })
                    Assert.That(copy.SurfaceFor(id, false), Is.SameAs(Catalog.SurfaceFor(id, false)));
                Assert.That(copy.SurfaceFor("any-star", true), Is.SameAs(Catalog.starSurface));
            }
            finally { Object.DestroyImmediate(copy); }
        }
        [Test]
        public void CelestialMapsAreBoundedSrgbMipmappedAndUseTheProjectShader()
        {
            foreach (var material in Catalog.planetSurfaces) CheckSurface(material, false);
            CheckSurface(Catalog.starSurface, true);
        }
        private static void CheckSurface(Material material, bool star)
        {
            Assert.That(material, Is.Not.Null);
            Assert.That(material.shader.name, Is.EqualTo("Clustr/Galaxy Planet Surface"));
            Assert.That(material.GetFloat("_UseSurfaceMap"), Is.EqualTo(1));
            Assert.That(material.GetFloat("_Star"), Is.EqualTo(star ? 1 : 0));
            var texture = material.GetTexture("_SurfaceMap") as Texture2D;
            Assert.That(texture, Is.Not.Null); Assert.That(texture.width, Is.LessThanOrEqualTo(2048));
            var importer = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(texture));
            Assert.That(importer.textureShape, Is.EqualTo(TextureImporterShape.Texture2D));
            Assert.That(importer.mipmapEnabled, Is.True); Assert.That(importer.sRGBTexture, Is.True);
            Assert.That(importer.isReadable, Is.False);
            Assert.That(importer.wrapModeU, Is.EqualTo(TextureWrapMode.Repeat));
            Assert.That(importer.wrapModeV, Is.EqualTo(TextureWrapMode.Clamp));
        }
        [Test]
        public void CuratedModelsAreNormalizedAndCannotChangeFlightPhysics()
        {
            Assert.That(Catalog.userShipPrefabs.Length, Is.EqualTo(6));
            foreach (var model in Catalog.userShipPrefabs) CheckModel(model);
            CheckModel(Catalog.discussionSatellitePrefab); CheckModel(Catalog.cockpitConsole);
        }
        private static void CheckModel(GameObject prefab)
        {
            Assert.That(prefab, Is.Not.Null);
            Assert.That(AssetDatabase.GetAssetPath(prefab), Does.Contain("FreeAssets/Generated/"));
            Assert.That(prefab.GetComponentsInChildren<Collider>(true), Is.Empty);
            Assert.That(prefab.GetComponentsInChildren<Rigidbody>(true), Is.Empty);
            Assert.That(prefab.GetComponentsInChildren<MonoBehaviour>(true), Is.Empty);
            var renderers = prefab.GetComponentsInChildren<Renderer>(true);
            Assert.That(renderers, Is.Not.Empty); var bounds = renderers[0].bounds;
            foreach (var renderer in renderers)
            {
                bounds.Encapsulate(renderer.bounds);
                foreach (var material in renderer.sharedMaterials)
                    Assert.That(material.shader.name, Is.EqualTo("Universal Render Pipeline/Unlit"));
            }
            Assert.That(Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z)), Is.EqualTo(1).Within(.01));
            Assert.That(bounds.center.magnitude, Is.LessThan(.01));
        }
        [Test]
        public void DemoFallbackUsesTheSameShipsWithoutDuplicatingLargeSurfaceMaps()
        {
            var fallback = GalaxyVisualCatalog.Load(); Assert.That(fallback, Is.Not.Null);
            CollectionAssert.AreEqual(Catalog.userShipPrefabs, fallback.userShipPrefabs);
            Assert.That(fallback.discussionSatellitePrefab, Is.SameAs(Catalog.discussionSatellitePrefab));
            Assert.That(fallback.planetSurfaces, Is.Empty); Assert.That(fallback.starSurface, Is.Null);
        }
        [Test]
        public void ActivityShaderIsExplicitlyRetainedIndependentOfShipMaterials()
        {
            Assert.That(Catalog.activityMaterial, Is.Not.Null);
            Assert.That(Catalog.activityMaterial.shader.name, Is.EqualTo("Universal Render Pipeline/Lit"));
            Assert.That(Catalog.activityMaterial.IsKeywordEnabled("_EMISSION"), Is.True);
            Assert.That(GalaxyVisualCatalog.Load().activityMaterial, Is.SameAs(Catalog.activityMaterial));
        }
        [Test]
        public void SoundsAndControllerPromptsAreLocalAndReadyToUse()
        {
            foreach (var clip in new[] { Catalog.engineSound, Catalog.scannerSound, Catalog.interfaceSound })
            {
                Assert.That(clip, Is.Not.Null); Assert.That(clip.length, Is.GreaterThan(0));
                Assert.That(clip.loadType, Is.EqualTo(AudioClipLoadType.DecompressOnLoad));
            }
            Assert.That(Catalog.confirmPrompt, Is.Not.Null); Assert.That(Catalog.backPrompt, Is.Not.Null);
        }
    }
}
