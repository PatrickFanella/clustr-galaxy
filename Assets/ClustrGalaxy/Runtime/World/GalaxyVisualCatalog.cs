using UnityEngine;

namespace ClustrGalaxy.World
{
    public sealed class GalaxyVisualCatalog : ScriptableObject
    {
        public GameObject[] userShipPrefabs = System.Array.Empty<GameObject>();
        public GameObject discussionSatellitePrefab = null!;

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
