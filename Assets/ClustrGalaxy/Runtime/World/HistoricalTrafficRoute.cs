using System;
using UnityEngine;

namespace ClustrGalaxy.World
{
    public sealed class HistoricalTrafficRoute : MonoBehaviour
    {
        private Transform[] markers = Array.Empty<Transform>();
        private Vector3 start;
        private Vector3 end;
        private float speed;
        private float phase;
        private bool directed;

        public void Initialize(
            Vector3 source,
            Vector3 target,
            long weight,
            bool isDirected,
            long revisionId,
            string routeId,
            Material markerMaterial)
        {
            start = source;
            end = target;
            directed = isDirected;
            var strength = Mathf.Clamp01(Mathf.Log10(Mathf.Max(1f, weight)) / 5f);
            speed = Mathf.Lerp(0.035f, 0.24f, strength);
            phase = StableUnit($"{revisionId}:{routeId}:traffic");
            var count = Mathf.Clamp(1 + Mathf.RoundToInt(strength * 7f), 1, 8);
            markers = new Transform[count];
            for (var index = 0; index < count; index++)
            {
                var marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                marker.name = "historical traffic marker";
                Destroy(marker.GetComponent<Collider>());
                marker.transform.SetParent(transform, false);
                marker.transform.localScale = Vector3.one * Mathf.Lerp(0.12f, 0.34f, strength);
                marker.GetComponent<MeshRenderer>().sharedMaterial = markerMaterial;
                markers[index] = marker.transform;
            }
        }

        private void Update()
        {
            for (var index = 0; index < markers.Length; index++)
            {
                var offset = (float)index / markers.Length;
                var t = Mathf.Repeat(phase + offset + Time.time * speed, 1f);
                if (!directed && (index & 1) == 1)
                {
                    t = 1f - t;
                }
                var arc = Vector3.up * Mathf.Sin(t * Mathf.PI) * Vector3.Distance(start, end) * 0.055f;
                markers[index].localPosition = Vector3.Lerp(start, end, t) + arc;
            }
        }

        private static float StableUnit(string value)
        {
            var hash = 2166136261u;
            foreach (var character in value)
            {
                hash = unchecked((hash ^ character) * 16777619u);
            }
            return (hash & 0x00ffffff) / 16777215f;
        }
    }
}
