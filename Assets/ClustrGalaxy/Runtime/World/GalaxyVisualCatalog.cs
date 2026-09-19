using UnityEngine;

namespace ClustrGalaxy.World
{
    public sealed class GalaxyVisualCatalog : ScriptableObject
    {
        public GameObject[] anomalyPrefabs = System.Array.Empty<GameObject>();
        public GameObject[] keepsakePrefabs = System.Array.Empty<GameObject>();
        public GameObject observatoryPlatformPrefab;
        public GameObject[] userShipPrefabs = System.Array.Empty<GameObject>();
        public GameObject discussionSatellitePrefab = null!;
        public AudioClip[] explorationInstruments = System.Array.Empty<AudioClip>();

        public Material starSurface;
        public Material[] planetSurfaces = System.Array.Empty<Material>();
        public GameObject cockpitConsole;
        public AudioClip engineSound, scannerSound, interfaceSound;
        public Sprite confirmPrompt, backPrompt;

        // Stable across processes, platforms, source ordering and floating-origin shifts.
        public Material SurfaceFor(string stableId, bool star)
        {
            if (star) return starSurface;
            if (planetSurfaces == null || planetSurfaces.Length == 0) return null;
            uint hash = 2166136261;
            unchecked { foreach (var c in stableId ?? "") { hash ^= c; hash *= 16777619; } }
            return planetSurfaces[hash % (uint)planetSurfaces.Length];
        }

        public GameObject UserShipForIndex(int index)
        {
            if (userShipPrefabs == null || userShipPrefabs.Length == 0)
            {
                return null;
            }

            var wrapped = ((index % userShipPrefabs.Length) + userShipPrefabs.Length) % userShipPrefabs.Length;
            return userShipPrefabs[wrapped];
        }

        public static GalaxyVisualCatalog Load() =>
            Resources.Load<GalaxyVisualCatalog>("GalaxyVisualCatalog");
    }
}
