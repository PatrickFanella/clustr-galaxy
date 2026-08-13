using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace ClustrGalaxy.World
{
    public sealed class InstancedStarfield : MonoBehaviour
    {
        private const int BatchSize = 1023;
        private Mesh mesh = null!;
        private Material material = null!;
        private Matrix4x4[] matrices = Array.Empty<Matrix4x4>();

        public void Initialize(Mesh starMesh, Material sharedMaterial, Matrix4x4[] instances)
        {
            mesh = starMesh;
            material = sharedMaterial;
            material.enableInstancing = true;
            matrices = instances ?? Array.Empty<Matrix4x4>();
        }

        private void LateUpdate()
        {
            if (mesh == null || material == null || matrices.Length == 0)
            {
                return;
            }
            for (var offset = 0; offset < matrices.Length; offset += BatchSize)
            {
                var count = Math.Min(BatchSize, matrices.Length - offset);
                var batch = new Matrix4x4[count];
                Array.Copy(matrices, offset, batch, 0, count);
                Graphics.DrawMeshInstanced(mesh, 0, material, batch, count, null,
                    ShadowCastingMode.Off, receiveShadows: false, gameObject.layer);
            }
        }
    }
}
