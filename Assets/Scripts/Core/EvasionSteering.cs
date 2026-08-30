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
    }
}
