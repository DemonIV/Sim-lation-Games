using UnityEngine;

namespace Sim.Core
{
    /// <summary>
    /// Timed servicing at a friendly base: a drone must dwell for the full cycle to be
    /// refuelled and rearmed. Leaving early resets progress. Pure logic.
    /// </summary>
    public class ResupplyPoint
    {
        public float ServiceSeconds;
        public float Elapsed { get; private set; }
        public bool IsServicing { get; private set; }

        public float Progress => ServiceSeconds > 0f ? Mathf.Clamp01(Elapsed / ServiceSeconds) : 1f;

        public ResupplyPoint(float serviceSeconds = 4f)
        {
            ServiceSeconds = Mathf.Max(0f, serviceSeconds);
        }

        /// <summary>
        /// Advance servicing for one step. Pass whether the drone is inside the base radius.
        /// Returns true exactly on the step the service completes.
        /// </summary>
        public bool Tick(bool atBase, float dt)
        {
            if (!atBase)
            {
                Elapsed = 0f;
                IsServicing = false;
                return false;
            }

            IsServicing = true;
            Elapsed += Mathf.Max(0f, dt);
            if (Elapsed >= ServiceSeconds)
            {
                Elapsed = 0f;
                IsServicing = false;
                return true;
            }
            return false;
        }
    }
}
