using UnityEngine;

namespace Sim.Core
{
    /// <summary>
    /// Deterministic procedural terrain height field, shared by the mesh builder and the
    /// unit spawners so ground objects sit on the surface. Pure logic.
    /// </summary>
    public static class TerrainField
    {
        public const float Amplitude = 3f;
        public const float Frequency = 0.012f;
        /// <summary>Everything inside this radius is perfectly level (the airbase).</summary>
        public const float FlatRadius = 45f;
        /// <summary>Blend distance from the flat area out to full relief.</summary>
        public const float FlatBlend = 25f;

        public static float Height(float x, float z)
        {
            float n = Mathf.PerlinNoise((x + 1000f) * Frequency, (z + 1000f) * Frequency);
            float h = (n - 0.5f) * 2f * Amplitude;
            float d = Mathf.Sqrt(x * x + z * z);
            float t = Mathf.Clamp01((d - FlatRadius) / FlatBlend);
            return h * t;
        }
    }
}
