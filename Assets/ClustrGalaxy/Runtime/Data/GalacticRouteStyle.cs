using UnityEngine;

namespace ClustrGalaxy.Data
{
    public static class GalacticRouteStyle
    {
        public static float NormalizeStrength(long weight, long maximumWeight)
        {
            if (weight <= 0 || maximumWeight <= 0)
            {
                return 0.12f;
            }

            var normalized = Mathf.Log(1f + weight) / Mathf.Log(1f + maximumWeight);
            return Mathf.Clamp(normalized, 0.12f, 1f);
        }
    }
}
