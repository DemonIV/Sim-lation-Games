using UnityEngine;

namespace Sim.Core
{
    /// <summary>
    /// True proportional navigation guidance law. Produces a lateral acceleration command
    /// proportional to the line-of-sight rotation rate. Pure logic.
    /// </summary>
    public static class ProportionalNavigation
    {
        public const float DefaultGain = 3f;

        /// <summary>
        /// Acceleration command for a pursuer.
        /// relativePosition = target - pursuer; relativeVelocity = targetVel - pursuerVel.
        /// </summary>
        public static Vector3 Acceleration(Vector3 relativePosition, Vector3 relativeVelocity, float gain = DefaultGain)
        {
            float r2 = relativePosition.sqrMagnitude;
            if (r2 < 1e-6f) return Vector3.zero;
            Vector3 omega = Vector3.Cross(relativePosition, relativeVelocity) / r2;
            return gain * Vector3.Cross(relativeVelocity, omega);
        }

        /// <summary>Closing velocity (positive = closing, negative = opening).</summary>
        public static float ClosingVelocity(Vector3 relativePosition, Vector3 relativeVelocity)
        {
            float r = relativePosition.magnitude;
            if (r < 1e-6f) return 0f;
            return -Vector3.Dot(relativePosition, relativeVelocity) / r;
        }
    }
}
