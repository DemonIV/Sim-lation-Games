using UnityEngine;

namespace Sim.Core
{
    /// <summary>Squad-level assessment of whether the friendly force can still fight. Pure logic.</summary>
    public static class SquadStatus
    {
        /// <summary>True when no drones survive, or every survivor has a dry tank.</summary>
        public static bool IsCombatIneffective(int aliveDrones, int dronesWithFuel)
        {
            if (aliveDrones <= 0) return true;
            return dronesWithFuel <= 0;
        }
    }
}
