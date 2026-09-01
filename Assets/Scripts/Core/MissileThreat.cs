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

        /// <summary>
        /// Multiplier applied to <see cref="DecoyChance(float,float,float)"/> when the target is in a
        /// hard break turn as the salvo goes out. A seeker that is already fighting a high
        /// line-of-sight rate is far easier to walk off the target, so flares plus a break are
        /// meaningfully better than either on its own.
        /// </summary>
        public const float BreakTurnDecoyBonus = 1.5f;

        /// <summary>
        /// <see cref="DecoyChance(float,float,float)"/> with the break-turn coupling: releasing the
        /// salvo while breaking multiplies the chance by <see cref="BreakTurnDecoyBonus"/>.
        /// </summary>
        public static float DecoyChance(float baseProbability, float timeToImpact, float aspectDot,
                                        bool breaking)
        {
            float chance = DecoyChance(baseProbability, timeToImpact, aspectDot);
            return Mathf.Clamp01(chance * (breaking ? BreakTurnDecoyBonus : 1f));
        }
    }
}
