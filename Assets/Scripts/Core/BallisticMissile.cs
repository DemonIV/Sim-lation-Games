using UnityEngine;

namespace Sim.Core
{
    /// <summary>
    /// The fighter jet's balistik füze — the shared, pure-logic definition of what the weapon IS.
    /// The runtime launcher and the HUD both read their numbers from here instead of each carrying a
    /// copy.
    ///
    /// <para>
    /// THE WARHEAD IS DERIVED, NEVER TYPED OUT. <see cref="Damage"/> takes the airframe's NORMAL
    /// missile damage and multiplies it by <see cref="DamageMultiplier"/> (= 2), so the promise "iki
    /// kat hasar" stays true by construction: retune the guided missile and the ballistic round moves
    /// with it, with no second number to forget.
    /// </para>
    ///
    /// <para>
    /// The round itself is UNGUIDED. It is thrown along the lofted solution from
    /// <see cref="Ballistics.TryLoftedLaunchVelocity"/> and then flown by
    /// <see cref="BallisticProjectile"/> (gravity, drag, wind, air density) — the same integrator the
    /// guided munition already borrows its gravity/drag term from. That is the whole character of the
    /// weapon: it is aimed at a DESIGNATED POINT, arcs high, arrives late and cannot correct.
    /// </para>
    ///
    /// This is a GAME / EDUCATIONAL model with abstract, gamified parameters.
    /// </summary>
    public static class BallisticMissile
    {
        /// <summary>
        /// How much harder the ballistic round hits than a normal guided missile from the same
        /// aircraft. Exactly 2, which is the entire point of the weapon.
        /// </summary>
        public const float DamageMultiplier = 2f;

        /// <summary>
        /// Damage of one ballistic round, derived from the shooter's own guided-missile warhead.
        /// A non-positive input yields 0 rather than a negative warhead.
        /// </summary>
        public static float Damage(float normalMissileDamage)
        {
            if (normalMissileDamage <= 0f) return 0f;
            return normalMissileDamage * DamageMultiplier;
        }

        /// <summary>
        /// Launch speed of the round in m/s. Deliberately far SLOWER than the guided munition's
        /// 180 m/s cruise: a heavy round that visibly arcs over and takes its time to arrive is what
        /// makes this read as a different weapon rather than a stronger missile.
        /// </summary>
        public const float LaunchSpeed = 70f;

        /// <summary>
        /// Gravity magnitude (m/s², positive, pulling down) the aiming solution is computed against —
        /// the same constant <see cref="GunPipper"/> and <see cref="BallisticProjectile"/> use.
        /// </summary>
        public const float Gravity = GunPipper.EarthGravity;

        /// <summary>
        /// Seconds between two shots. A heavy centreline store is not a repeater; with only a couple
        /// of rounds aboard this mostly stops a double key press from emptying the rack.
        /// </summary>
        public const float ReloadSeconds = 2f;

        /// <summary>Blast radius (m) within which the round detonates on its designated point.</summary>
        public const float FuzeRadius = 7f;

        /// <summary>
        /// Furthest designated point the launcher will accept, in metres. Beyond this the aiming
        /// solution would be out of reach at <see cref="LaunchSpeed"/> anyway (flat maximum range is
        /// <c>v² / g</c> ≈ 499 m), so this is the practical, honest limit the HUD can quote.
        /// </summary>
        public const float MaxRange = 400f;

        /// <summary>
        /// True when a point is close enough to be shot at with a round leaving the rail at
        /// <see cref="LaunchSpeed"/>. Range only — whether the geometry has a solution at all is
        /// <see cref="Ballistics.TryLoftedLaunchVelocity"/>'s answer to give.
        /// </summary>
        public static bool InRange(Vector3 launchPoint, Vector3 targetPoint)
        {
            return (targetPoint - launchPoint).sqrMagnitude <= MaxRange * MaxRange;
        }
    }
}
