using UnityEngine;

namespace Sim.Core
{
    public enum ManeuverType { None, BreakTurn, Dive, Climb, Barrel }

    /// <summary>Named evasive maneuvers producing a steering direction. Pure logic.</summary>
    public static class EvasiveManeuver
    {
        public static Vector3 Direction(ManeuverType type, Vector3 forward, Vector3 threatDirection, Vector3 up)
        {
            Vector3 f = forward.sqrMagnitude > 1e-6f ? forward.normalized : Vector3.forward;
            Vector3 t = threatDirection.sqrMagnitude > 1e-6f ? threatDirection.normalized : f;
            Vector3 u = up.sqrMagnitude > 1e-6f ? up.normalized : Vector3.up;

            switch (type)
            {
                case ManeuverType.BreakTurn:
                    return EvasionSteering.Evade(f, t, u);
                case ManeuverType.Dive:
                    return (EvasionSteering.Evade(f, t, u) - u * 1.2f).normalized;
                case ManeuverType.Climb:
                    return (EvasionSteering.Evade(f, t, u) + u * 1.2f).normalized;
                case ManeuverType.Barrel:
                {
                    Vector3 perp = Vector3.Cross(u, t);
                    if (perp.sqrMagnitude < 1e-6f) perp = Vector3.Cross(Vector3.right, t);
                    return (perp.normalized * 0.7f + u * 0.7f).normalized;
                }
                default:
                    return f;
            }
        }

        /// <summary>Picks a maneuver: no evasion when the shot is far off, dive when high, climb when low.</summary>
        public static ManeuverType Choose(float altitude, float minAltitude, float timeToImpact)
        {
            if (timeToImpact > 6f) return ManeuverType.None;
            if (altitude > minAltitude * 4f) return ManeuverType.Dive;
            if (altitude < minAltitude * 2f) return ManeuverType.Climb;
            return ManeuverType.BreakTurn;
        }
    }
}
