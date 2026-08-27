using UnityEngine;

namespace Sim.Core
{
    /// <summary>
    /// Autopilot for a guided munition. Produces the commanded acceleration from two independent
    /// terms: a lateral steering command from <see cref="ProportionalNavigation"/>, limited by an
    /// airframe g-limit, and an axial thrust command that trims speed back toward cruise.
    /// External forces (gravity, drag) are NOT included — the caller adds those from
    /// <see cref="BallisticProjectile"/> so they genuinely shape the trajectory. Pure logic.
    /// </summary>
    public class MunitionAutopilot
    {
        public float CruiseSpeed = 180f;                              // m/s
        public float NavGain = ProportionalNavigation.DefaultGain;

        /// <summary>Axial thrust gain (1/s): commanded axial acceleration per m/s of speed error.</summary>
        public float ThrustGain = 2f;

        /// <summary>Airframe lateral acceleration limit (m/s^2). Keeps close-range PN commands finite.</summary>
        public float MaxLateralAcceleration = 200f;

        /// <summary>
        /// Steering command from proportional navigation, clamped to the airframe g-limit.
        /// relativePosition = target - munition; relativeVelocity = targetVel - munitionVel.
        /// </summary>
        public Vector3 LateralCommand(Vector3 relativePosition, Vector3 relativeVelocity)
        {
            Vector3 a = ProportionalNavigation.Acceleration(relativePosition, relativeVelocity, NavGain);
            float max = Mathf.Max(0f, MaxLateralAcceleration);
            if (a.sqrMagnitude > max * max) a = a.normalized * max;
            return a;
        }

        /// <summary>
        /// Axial thrust command (m/s^2) along the current heading, proportional to the speed error.
        /// Zero at cruise speed and zero when there is no heading to push along.
        /// </summary>
        public Vector3 ThrustCommand(Vector3 velocity)
        {
            float speed = velocity.magnitude;
            if (speed <= 1e-6f) return Vector3.zero;
            return (velocity / speed) * (ThrustGain * (CruiseSpeed - speed));
        }

        /// <summary>
        /// Total commanded acceleration. Pass <paramref name="guiding"/> = false when the seeker has
        /// lost the target: the motor keeps burning but no steering is commanded, so the munition
        /// coasts on its current heading.
        /// </summary>
        public Vector3 Acceleration(Vector3 relativePosition, Vector3 relativeVelocity,
                                    Vector3 velocity, bool guiding = true)
        {
            Vector3 a = ThrustCommand(velocity);
            if (guiding) a += LateralCommand(relativePosition, relativeVelocity);
            return a;
        }
    }
}
