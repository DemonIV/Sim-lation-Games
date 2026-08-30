using UnityEngine;

namespace Sim.Core
{
    /// <summary>Incoming-missile geometry: time to impact and countermeasure effectiveness. Pure logic.</summary>
    public static class MissileThreat
    {
        /// <summary>Seconds until impact; PositiveInfinity when the missile is not closing.</summary>
        public static float TimeToImpact(float range, float closingVelocity)
        {
            if (closingVelocity <= 0.01f) return float.PositiveInfinity;
            return range / closingVelocity;
        }

        /// <summary>
        /// Chance a salvo defeats the missile. Early release helps most; presenting the nose to the
        /// missile (aspectDot near 1) helps least.
        /// </summary>
        public static float DecoyChance(float baseProbability, float timeToImpact, float aspectDot)
        {
            float p = Mathf.Clamp01(baseProbability);
            float timing = Mathf.Clamp01(timeToImpact / 3f);
            float aspect = Mathf.Lerp(1f, 0.5f, Mathf.Clamp01(aspectDot));
            return Mathf.Clamp01(p * timing * aspect);
        }
    }
}
