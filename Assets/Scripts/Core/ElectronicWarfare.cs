using UnityEngine;

namespace Sim.Core
{
    /// <summary>
    /// Electronic-warfare effects: noise jamming reduces a radar's effective detection
    /// range (burn-through), and ECM lowers lock probability. Pure logic.
    /// </summary>
    public static class ElectronicWarfare
    {
        /// <summary>Detection range degraded by noise jamming of the given strength (0 = none).</summary>
        public static float EffectiveRange(float baseRange, float jammerStrength)
        {
            if (jammerStrength <= 0f) return baseRange;
            return baseRange / Mathf.Pow(1f + jammerStrength, 0.25f);
        }

        /// <summary>Range within which the target's skin return burns through the jamming.</summary>
        public static float BurnThroughRange(float baseRange, float jammerStrength)
        {
            return EffectiveRange(baseRange, jammerStrength);
        }

        /// <summary>Probability (0..1) of achieving/holding a lock under ECM of the given strength.</summary>
        public static float LockProbability(float ecmStrength)
        {
            if (ecmStrength <= 0f) return 1f;
            return 1f / (1f + ecmStrength);
        }
    }
}
