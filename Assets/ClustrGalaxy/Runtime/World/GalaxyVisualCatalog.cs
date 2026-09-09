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
