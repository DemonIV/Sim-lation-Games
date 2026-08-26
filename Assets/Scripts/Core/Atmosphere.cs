using UnityEngine;

namespace Sim.Core
{
    /// <summary>Exponential atmosphere model: air density as a function of altitude.</summary>
    public static class Atmosphere
    {
        public const float SeaLevelDensity = 1.225f;   // kg/m^3
        public const float ScaleHeight = 8500f;        // m

        /// <summary>Air density (kg/m^3) at the given altitude in meters. Clamped to sea level at/below 0.</summary>
        public static float DensityAtAltitude(float altitudeMeters)
        {
            if (altitudeMeters <= 0f) return SeaLevelDensity;
            return SeaLevelDensity * Mathf.Exp(-altitudeMeters / ScaleHeight);
        }
    }
}
