using UnityEngine;

namespace Sim.Core
{
    public enum ManeuverType { None, BreakTurn, Dive, Climb, Barrel }

    /// <summary>Named evasive maneuvers producing a steering direction. Pure logic.</summary>
    public static class EvasiveManeuver
    {
        /// <summary>
        /// Widest time-to-impact worth manoeuvring against. Beyond this the shot is still far enough
        /// out that breaking only bleeds energy, so <see cref="Choose"/> returns
        /// <see cref="ManeuverType.None"/> and the aircraft keeps its normal task.
        /// </summary>
        public const float MaxWarningSeconds = 6f;

        /// <summary>
        /// The window — seconds to impact — in which a break turn actually defeats the shot.
        ///
        /// <para>
        /// A guidance law corrects a heading error with an acceleration that scales as
        /// <c>1 / timeToImpact</c>: break early and the missile simply re-establishes its collision
        /// course at a comfortable load; break late and the demanded load exceeds the missile's own
        /// structural limit (see <see cref="MissileAgility"/>) and it cannot recover before it flies
        /// past. This constant lives HERE, next to the manoeuvre logic, so the HUD cue and the
        /// steering both read the same number.
        /// </para>
        /// </summary>
        public const float BreakWindowSeconds = 2.5f;

        /// <summary>
        /// True when a break turn released right now falls inside <see cref="BreakWindowSeconds"/>,
        /// i.e. late enough that the missile cannot re-correct. False when nothing is inbound
        /// (PositiveInfinity) or the shot has already passed.
        /// </summary>
        public static bool InBreakWindow(float timeToImpact)
        {
            if (float.IsNaN(timeToImpact) || float.IsInfinity(timeToImpact)) return false;
            return timeToImpact > 0f && timeToImpact <= BreakWindowSeconds;
        }

        public static Vector3 Direction(ManeuverType type, Vector3 forward, Vector3 threatDirection, Vector3 up)
        {
            Vector3 f = forward.sqrMagnitude > 1e-6f ? forward.normalized : Vector3.forward;
            Vector3 t = threatDirection.sqrMagnitude > 1e-6f ? threatDirection.normalized : f;
            Vector3 u = up.sqrMagnitude > 1e-6f ? up.normalized : Vector3.up;

            switch (type)
            {
                // A true break: put the threat on the beam to MAXIMISE the line-of-sight rate the
                // missile's guidance law has to null. The dive/climb variants add the vertical
                // component on top of that same beam heading.
                case ManeuverType.BreakTurn:
                    return EvasionSteering.BreakTurn(f, t, u);
                case ManeuverType.Dive:
                    return (EvasionSteering.BreakTurn(f, t, u) - u * 1.2f).normalized;
                case ManeuverType.Climb:
                    return (EvasionSteering.BreakTurn(f, t, u) + u * 1.2f).normalized;
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
            if (timeToImpact > MaxWarningSeconds) return ManeuverType.None;
            if (altitude > minAltitude * 4f) return ManeuverType.Dive;
            if (altitude < minAltitude * 2f) return ManeuverType.Climb;
            return ManeuverType.BreakTurn;
        }
    }
}
