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
        private Color originalColor;
        private Color originalBaseColor;
        private Color originalEmissionColor;
        private bool hasBaseColor;
        private bool hasEmissionColor;
        private Color pulseBaseColor;
        private Color pulseColor;
        private float pulseRemaining;
        private float pulseDuration;
        private bool unsupportedReported;

        public void Initialize(Mesh starMesh, Material sharedMaterial, Matrix4x4[] instances)
        {
            mesh = starMesh;
            material = sharedMaterial;
            material.enableInstancing = true;
            matrices = instances ?? Array.Empty<Matrix4x4>();
            originalColor = material.color;
            hasBaseColor = material.HasProperty("_BaseColor");
            hasEmissionColor = material.HasProperty("_EmissionColor");
            originalBaseColor = hasBaseColor ? material.GetColor("_BaseColor") : originalColor;
            originalEmissionColor = hasEmissionColor ? material.GetColor("_EmissionColor") : Color.black;
            pulseBaseColor = originalBaseColor;
            pulseColor = pulseBaseColor;
        }

        /// <summary>Local, reversible presentation only; it changes no catalog data.</summary>
        public void TriggerAuthoredColorPulse(Color color, float durationSeconds = 1.2f)
        {
            pulseColor = color;
            pulseDuration = Mathf.Max(0.05f, durationSeconds);
            pulseRemaining = pulseDuration;
        }

        private void LateUpdate()
        {
            if (!CanRender(SystemInfo.supportsInstancing, mesh, material, matrices.Length))
            {
                if (mesh != null && material != null && matrices.Length > 0 &&
                    !SystemInfo.supportsInstancing)
                {
                    DisableUnsupported("GPU instancing is unavailable on the active graphics device.");
                }
                return;
            }
            UpdatePulse();
            try
            {
                for (var offset = 0; offset < matrices.Length; offset += BatchSize)
                {
                    var count = Math.Min(BatchSize, matrices.Length - offset);
                    var batch = new Matrix4x4[count];
                    Array.Copy(matrices, offset, batch, 0, count);
                    Graphics.DrawMeshInstanced(mesh, 0, material, batch, count, null,
                        ShadowCastingMode.Off, receiveShadows: false, gameObject.layer);
                }
            }
            catch (InvalidOperationException exception)
            {
                DisableUnsupported(exception.Message);
            }
        }

        public static bool CanRender(bool supportsInstancing, Mesh starMesh, Material starMaterial,
            int instanceCount) =>
            supportsInstancing && starMesh != null && starMaterial != null && instanceCount > 0;

        private void DisableUnsupported(string reason)
        {
            if (!unsupportedReported)
            {
                Debug.LogWarning($"[ClustrGalaxy.Starfield] Decorative instancing disabled: {reason}");
                unsupportedReported = true;
            }
            enabled = false;
        }

        private void UpdatePulse()
        {
            if (pulseRemaining <= 0f) return;
            pulseRemaining = Mathf.Max(0f, pulseRemaining - Time.unscaledDeltaTime);
            var phase = 1f - pulseRemaining / pulseDuration;
            SetPulseColor(Color.Lerp(pulseBaseColor, pulseColor, Mathf.Sin(phase * Mathf.PI)));
            if (pulseRemaining <= 0f) RestoreOriginalMaterialState();
        }

        private void SetPulseColor(Color color)
        {
            material.color = color;
            if (hasBaseColor) material.SetColor("_BaseColor", color);
            if (hasEmissionColor) material.SetColor("_EmissionColor", color * 1.8f);
        }

        private void OnDisable()
        {
            pulseRemaining = 0f;
            RestoreOriginalMaterialState();
        }

        private void OnDestroy()
        {
            pulseRemaining = 0f;
            RestoreOriginalMaterialState();
        }

        private void RestoreOriginalMaterialState()
        {
            if (material == null) return;
            material.color = originalColor;
            if (hasBaseColor) material.SetColor("_BaseColor", originalBaseColor);
            if (hasEmissionColor) material.SetColor("_EmissionColor", originalEmissionColor);
        }
    }
}
