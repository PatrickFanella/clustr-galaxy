using UnityEngine;

namespace ClustrGalaxy.World
{
    /// <summary>Three shared meshes; native LOD selection needs no per-body Update allocation.</summary>
    public sealed class CelestialMeshDetail : MonoBehaviour
    {
        private static Mesh nearMesh, middleMesh, farMesh;
        private MeshRenderer distant, middle;
        private void Start() { if (enabled) Configure(); }
        public void ReleaseDetail()
        {
            enabled = false;
            var group = GetComponent<LODGroup>(); if (group != null) group.enabled = false;
            if (distant != null) distant.gameObject.SetActive(false);
            if (middle != null) middle.gameObject.SetActive(false);
            GetComponent<MeshRenderer>().enabled = true;
            if (farMesh != null) GetComponent<MeshFilter>().sharedMesh = farMesh;
        }
        public void Configure()
        {
            enabled = true;
            var renderer = GetComponent<MeshRenderer>();
            if (renderer == null) return;
            if (nearMesh == null) nearMesh = Sphere(128, 64);
            if (middleMesh == null) middleMesh = Sphere(32, 16);
            if (farMesh == null) farMesh = Sphere(16, 8);
            GetComponent<MeshFilter>().sharedMesh = nearMesh;
            if (distant == null)
            {
                var child = new GameObject("Distant surface"); child.transform.SetParent(transform, false);
                child.AddComponent<MeshFilter>().sharedMesh = farMesh;
                distant = child.AddComponent<MeshRenderer>();
            }
            if (middle == null)
            {
                var child = new GameObject("Middle surface"); child.transform.SetParent(transform, false);
                child.AddComponent<MeshFilter>().sharedMesh = middleMesh;
                middle = child.AddComponent<MeshRenderer>();
            }
            middle.gameObject.SetActive(true);
            middle.sharedMaterial = renderer.sharedMaterial;
            distant.gameObject.SetActive(true);
            distant.sharedMaterial = renderer.sharedMaterial;
            var group = GetComponent<LODGroup>() ?? gameObject.AddComponent<LODGroup>();
            group.enabled = true;
            // Unity multiplies projected height by the global quality bias. Keep these
            // silhouette thresholds tied to actual screen coverage at every launch quality.
            var bias = Mathf.Max(.01f, QualitySettings.lodBias);
            group.SetLODs(new[] { new LOD(.12f * bias, new Renderer[] { renderer }),
                new LOD(.015f * bias, new Renderer[] { middle }), new LOD(0f, new Renderer[] { distant }) });
            group.RecalculateBounds();
        }
        private static Mesh Sphere(int longitude, int latitude)
        {
            var vertices = new Vector3[(longitude + 1) * (latitude + 1)];
            var normals = new Vector3[vertices.Length]; var uv = new Vector2[vertices.Length];
            var indices = new int[longitude * latitude * 6]; var cursor = 0;
            for (var y = 0; y <= latitude; y++) for (var x = 0; x <= longitude; x++)
            {
                var a = x * Mathf.PI * 2 / longitude; var b = y * Mathf.PI / latitude;
                var index = y * (longitude + 1) + x;
                normals[index] = new Vector3(Mathf.Sin(b) * Mathf.Cos(a), Mathf.Cos(b), Mathf.Sin(b) * Mathf.Sin(a));
                vertices[index] = normals[index] * .5f; uv[index] = new Vector2((float)x / longitude, 1f - (float)y / latitude);
                if (x == longitude || y == latitude) continue;
                var next = index + longitude + 1;
                indices[cursor++] = index; indices[cursor++] = index + 1; indices[cursor++] = next;
                indices[cursor++] = index + 1; indices[cursor++] = next + 1; indices[cursor++] = next;
            }
            var mesh = new Mesh { name = "Shared celestial sphere " + longitude, vertices = vertices, normals = normals, uv = uv, triangles = indices };
            mesh.RecalculateBounds(); return mesh;
        }
    }
}
