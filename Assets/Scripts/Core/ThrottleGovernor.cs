using UnityEngine;

namespace Sim.Core
{
    /// <summary>Limits commanded throttle by fuel state: a dry tank means no power. Pure logic.</summary>
    public static class ThrottleGovernor
    {
        /// <summary>Fuel fraction below which available power tapers off toward zero.</summary>
        public const float ReserveFraction = 0.05f;

        /// <summary>Effective throttle for the commanded value at the given fuel fraction.</summary>
        public static float Effective(float commandedThrottle, float fuelFraction)
        {
            float cmd = Mathf.Clamp01(commandedThrottle);
            if (fuelFraction <= 0f) return 0f;
            if (fuelFraction >= ReserveFraction) return cmd;
            return cmd * (fuelFraction / ReserveFraction);
        }
    }
}
