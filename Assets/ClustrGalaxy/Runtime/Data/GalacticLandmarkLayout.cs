using System;
using System.Text;
using ClustrGalaxy.Generation;
using UnityEngine;

namespace ClustrGalaxy.Data
{
    public static class GalacticLandmarkLayout
    {
        private const float InnerRadius = 400f;
        private const float RadialSpan = 760f;
        private const float ElevationSpan = 150f;

        public static float OverviewCameraDistance(float verticalFieldOfView, float aspect)
        {
            // Fit the entire projection envelope, including clusters at its near edge.
            var radius = Mathf.Sqrt(Mathf.Pow((InnerRadius + RadialSpan) * GalaxyTuning.RegionDistanceScale, 2f) +
                                    Mathf.Pow(ElevationSpan * GalaxyTuning.RegionDistanceScale, 2f)) + 6f;
            var verticalHalfAngle = verticalFieldOfView * Mathf.Deg2Rad * .5f;
            var horizontalHalfAngle = Mathf.Atan(Mathf.Tan(verticalHalfAngle) * aspect);
            return radius / Mathf.Sin(Mathf.Min(verticalHalfAngle, horizontalHalfAngle));
        }

        public static Vector3 Project(SpatialNode node, SpatialBounds bounds)
        {
            var normalizedX = Normalize(node.x, bounds.min_x, bounds.max_x) - 0.5f;
            var normalizedY = Normalize(node.y, bounds.min_y, bounds.max_y) - 0.5f;
            var sourceRadius = Mathf.Clamp01(new Vector2(normalizedX, normalizedY).magnitude / 0.70710678f);
            var bearing = Mathf.Atan2(normalizedY, normalizedX);
            if (Mathf.Abs(normalizedX) + Mathf.Abs(normalizedY) < 0.000001f)
            {
                bearing = StableUnit(node.id, "bearing") * Mathf.PI * 2f;
            }

            var radius = (InnerRadius + sourceRadius * RadialSpan) * GalaxyTuning.RegionDistanceScale;
            var elevation = bounds.max_z > bounds.min_z
                ? (Normalize(node.z, bounds.min_z, bounds.max_z) - 0.5f) * ElevationSpan * 2f
                : (StableUnit(node.id, "elevation") - 0.5f) * ElevationSpan * 2f;
            elevation *= GalaxyTuning.RegionDistanceScale;
            return new Vector3(Mathf.Cos(bearing) * radius, elevation, Mathf.Sin(bearing) * radius);
        }

        public static SpatialBounds BoundsOf(SpatialNode[] nodes)
        {
            if (nodes == null || nodes.Length == 0)
            {
                return new SpatialBounds();
            }

            var bounds = new SpatialBounds
            {
                min_x = nodes[0].x, max_x = nodes[0].x,
                min_y = nodes[0].y, max_y = nodes[0].y,
                min_z = nodes[0].z, max_z = nodes[0].z
            };
            for (var index = 1; index < nodes.Length; index++)
            {
                var node = nodes[index];
                bounds.min_x = Mathf.Min(bounds.min_x, node.x);
                bounds.max_x = Mathf.Max(bounds.max_x, node.x);
                bounds.min_y = Mathf.Min(bounds.min_y, node.y);
                bounds.max_y = Mathf.Max(bounds.max_y, node.y);
                bounds.min_z = Mathf.Min(bounds.min_z, node.z);
                bounds.max_z = Mathf.Max(bounds.max_z, node.z);
            }

            return bounds;
        }

        private static float Normalize(float value, float minimum, float maximum) =>
            maximum > minimum ? Mathf.Clamp01((value - minimum) / (maximum - minimum)) : 0.5f;

        private static float StableUnit(string id, string channel)
        {
            const ulong offset = 14695981039346656037UL;
            const ulong prime = 1099511628211UL;
            var hash = offset;
            var bytes = Encoding.UTF8.GetBytes((id ?? string.Empty) + "|" + channel);
            for (var index = 0; index < bytes.Length; index++)
            {
                hash ^= bytes[index];
                hash *= prime;
            }

            return (float)((hash >> 11) * (1d / 9007199254740992d));
        }
    }
}
