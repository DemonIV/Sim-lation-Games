using UnityEngine;

namespace Sim.Core
{
    /// <summary>
    /// Probability that a gun round hits, from the dispersion cone radius at that range
    /// versus the target's size. Pure logic.
    /// </summary>
    public static class HitProbability
    {
        public static float Compute(float distance, float effectiveRange, float dispersionDeg, float targetRadius)
        {
            if (distance > effectiveRange) return 0f;
            if (distance <= 0.01f) return 1f;
            if (dispersionDeg <= 0f) return 1f;
            float coneRadius = distance * Mathf.Tan(dispersionDeg * Mathf.Deg2Rad);
            if (coneRadius <= 1e-4f) return 1f;
            float ratio = targetRadius / coneRadius;
            return Mathf.Clamp01(ratio * ratio);
        }
    }
}
