using UnityEngine;

namespace Sim.Core
{
    /// <summary>High-level tactical state for an autonomous drone.</summary>
    public enum EngagementState
    {
        Patrol,
        Engage,
        ReturnToBase
    }

    /// <summary>
    /// Decides a drone's engagement state from its situation (fuel, ammo, whether it has a target).
    /// Pure logic, tunable thresholds.
    /// </summary>
    public class EngagementPolicy
    {
        /// <summary>At or below this fuel fraction the drone returns to base (bingo fuel).</summary>
        public float BingoFuelFraction = 0.25f;

        public EngagementState Decide(bool hasTarget, bool hasAmmo, float fuelFraction)
        {
            // Low fuel or empty weapon -> disengage and head home.
            if (fuelFraction <= BingoFuelFraction || !hasAmmo)
                return EngagementState.ReturnToBase;
            // Able to fight and a target is available -> engage.
            if (hasTarget)
                return EngagementState.Engage;
            // Otherwise keep patrolling.
            return EngagementState.Patrol;
        }
    }
}
