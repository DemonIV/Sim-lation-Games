using UnityEngine;

namespace Sim.Core
{
    /// <summary>Computes an evasive steering direction away from a threat. Pure logic.</summary>
    public static class EvasionSteering
    {
        /// <summary>
        /// Given the drone's forward, the direction from the drone to the threat, and an up vector,
        /// returns a unit evade direction: a lateral jink (perpendicular to the threat bearing, on the
        /// side nearer the current heading) blended with a component away from the threat.
        /// </summary>
        public static Vector3 Evade(Vector3 forward, Vector3 threatDirection, Vector3 up)
        {
            Vector3 f = forward.sqrMagnitude > 1e-6f ? forward.normalized : Vector3.forward;
            Vector3 threat = threatDirection.sqrMagnitude > 1e-6f ? threatDirection.normalized : f;
            Vector3 u = up.sqrMagnitude > 1e-6f ? up.normalized : Vector3.up;

            Vector3 perp = Vector3.Cross(u, threat);
            if (perp.sqrMagnitude < 1e-6f) perp = Vector3.Cross(Vector3.right, threat);
            perp = perp.normalized;
            if (Vector3.Dot(perp, f) < 0f) perp = -perp;   // jink toward current heading side

            Vector3 away = -threat;
            Vector3 evade = perp * 0.8f + away * 0.4f;
            return evade.sqrMagnitude > 1e-6f ? evade.normalized : perp;
        }

        /// <summary>
        /// A proper BREAK TURN: the heading exactly perpendicular to the threat bearing — i.e. it puts
        /// the threat on the beam — on whichever of the two perpendicular sides is closer to the
        /// current heading, so the aircraft never reverses back into the missile.
        ///
        /// <para>
        /// This is the manoeuvre that actually defeats a proportional-navigation missile:
        /// <see cref="ProportionalNavigation"/> commands acceleration proportional to the
        /// line-of-sight rotation rate, and a beam aspect MAXIMISES that rate. Compare
        /// <see cref="Evade"/>, which blends in a run-away component: that gains separation but lowers
        /// the line-of-sight rate, which is exactly what a guidance law wants.
        /// </para>
        /// </summary>
        public static Vector3 BreakTurn(Vector3 forward, Vector3 threatDirection, Vector3 up)
        {
            Vector3 f = forward.sqrMagnitude > 1e-6f ? forward.normalized : Vector3.forward;
            Vector3 threat = threatDirection.sqrMagnitude > 1e-6f ? threatDirection.normalized : f;
            Vector3 u = up.sqrMagnitude > 1e-6f ? up.normalized : Vector3.up;

            // Cross(up, threat) is perpendicular to the threat bearing by construction, and level.
            Vector3 perp = Vector3.Cross(u, threat);
            // Degenerate only when the threat is straight up/down; pick any other reference axis.
            if (perp.sqrMagnitude < 1e-6f) perp = Vector3.Cross(Vector3.right, threat);
            if (perp.sqrMagnitude < 1e-6f) perp = Vector3.Cross(Vector3.forward, threat);
            if (perp.sqrMagnitude < 1e-6f) return f;

            perp = perp.normalized;
            // Of the two beam directions, take the one we are already closer to.
            if (Vector3.Dot(perp, f) < 0f) perp = -perp;
            return perp;
        }
    }
}
