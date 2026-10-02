using System;
using System.Numerics;
namespace ClustrGalaxy.Core.Activities
{
    /// <summary>Authored observation geometry outside physical and stellar safety surfaces.</summary>
    public static class CampaignPlacement
    {
        public const float AnomalyClearance = 160;
        public static double Clearance(double centreDistance, double exclusionRadius)
        {
            if (!double.IsFinite(centreDistance) || !double.IsFinite(exclusionRadius) || centreDistance < 0 || exclusionRadius < 0)
                return double.NaN;
            // Negative means inside the safety surface: it must never qualify as an observation.
            return centreDistance - exclusionRadius;
        }
        public static Vector3 AnomalyOffset(double exclusionRadius, int index, double seconds, bool reducedMotion)
        {
            if (!double.IsFinite(exclusionRadius) || exclusionRadius < 0 || index < 0 || index > 5 || !double.IsFinite(seconds))
                throw new ArgumentException("Invalid authored anomaly placement.");
            var angularRate = Math.Min(.35, 60 / (exclusionRadius + AnomalyClearance));
            var phase = index + (reducedMotion ? 0 : seconds * angularRate);
            var direction = Vector3.Normalize(new Vector3((float)Math.Sin(phase) * .25f, 1, (float)Math.Cos(phase) * .25f));
            return direction * (float)(exclusionRadius + AnomalyClearance);
        }
    }
}
