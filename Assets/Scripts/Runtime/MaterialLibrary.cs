using System.Collections.Generic;
using UnityEngine;

namespace Sim.Runtime
{
    /// <summary>
    /// Small runtime material factory. Resolves a usable lit shader once (Standard → URP/Lit →
    /// Unlit/Color) and caches every material it creates, so building hundreds of primitive parts
    /// does not leak one material instance per renderer.
    ///
    /// Purely cosmetic — nothing here touches gameplay state.
    /// </summary>
    public static class MaterialLibrary
    {
        private static Shader _shader;
        private static bool _shaderResolved;
        private static readonly Dictionary<string, Material> Cache = new Dictionary<string, Material>();

        /// <summary>Resolves (once) the best available shader for runtime-created materials.</summary>
        private static Shader ResolveShader()
        {
            if (_shaderResolved) return _shader;
            _shaderResolved = true;

            _shader = Shader.Find("Standard");
            if (_shader == null) _shader = Shader.Find("Universal Render Pipeline/Lit");
            if (_shader == null) _shader = Shader.Find("Unlit/Color");
            return _shader;
        }

        /// <summary>
        /// Creates (or returns a cached) material with the given colour and PBR-ish settings.
        /// </summary>
        /// <param name="color">Base/albedo colour.</param>
        /// <param name="metallic">Metallic value in 0..1.</param>
        /// <param name="smoothness">Smoothness/glossiness in 0..1.</param>
        /// <param name="emission">Emission colour; black/default means no emission.</param>
        public static Material Create(Color color, float metallic = 0.25f, float smoothness = 0.5f, Color emission = default)
        {
            string key = string.Format(
                "{0:F3}_{1:F3}_{2:F3}_{3:F3}|{4:F2}|{5:F2}|{6:F3}_{7:F3}_{8:F3}_{9:F3}",
                color.r, color.g, color.b, color.a, metallic, smoothness,
                emission.r, emission.g, emission.b, emission.a);

            Material cached;
            if (Cache.TryGetValue(key, out cached) && cached != null) return cached;

            Shader shader = ResolveShader();
            if (shader == null) return null;

            var mat = new Material(shader);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", Mathf.Clamp01(metallic));
            if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", Mathf.Clamp01(smoothness));
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", Mathf.Clamp01(smoothness));

            bool hasEmission = emission.r > 0f || emission.g > 0f || emission.b > 0f;
            if (hasEmission)
            {
                mat.EnableKeyword("_EMISSION");
                if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", emission);
                if (mat.HasProperty("_EmissiveColor")) mat.SetColor("_EmissiveColor", emission);
            }

            Cache[key] = mat;
            return mat;
        }

        /// <summary>Assigns a shared material to the GameObject's Renderer, if it has one.</summary>
        public static void Apply(GameObject go, Material mat)
        {
            if (go == null || mat == null) return;
            var renderer = go.GetComponent<Renderer>();
            if (renderer == null) return;
            renderer.sharedMaterial = mat;
        }
    }
}
