using UnityEngine;

namespace Sim.Core
{
    /// <summary>
    /// Pure geometry of the gun pipper: where a round leaving the muzzle actually arrives at a given
    /// range. Same convention as <see cref="BallisticProjectile"/> — gravity pulls along
    /// <see cref="Vector3.down"/> and is given as a POSITIVE magnitude in m/s².
    ///
    /// <para>
    /// Deliberately simpler than <see cref="BallisticProjectile"/>: no drag, no wind, no atmosphere.
    /// A HUD pipper is redrawn every frame for a flat "time of flight = range / muzzle speed" run, so
    /// the closed-form drop is both enough and stable. Pass <c>gravity = 0</c> for a hitscan gun.
    /// </para>
    ///
    /// Pure logic: no Unity scene dependency, fully unit-testable.
    /// </summary>
    public static class GunPipper
    {
        /// <summary>Sea-level gravity magnitude, matching <see cref="BallisticProjectile.Gravity"/>.</summary>
        public const float EarthGravity = 9.81f;

        /// <summary>
        /// World position the rounds arrive at <paramref name="range"/> metres down the bore.
        /// </summary>
        /// <param name="muzzle">Where the round leaves the barrel.</param>
        /// <param name="forward">Bore direction; normalised internally.</param>
        /// <param name="muzzleSpeed">Muzzle speed in m/s. Zero or less means an instantaneous
        /// (hitscan) round, which cannot drop.</param>
        /// <param name="range">Distance down the bore, in metres.</param>
        /// <param name="gravity">Gravity magnitude in m/s² (positive, pulling down). Zero = no drop.</param>
        public static Vector3 AimPoint(Vector3 muzzle, Vector3 forward, float muzzleSpeed,
                                       float range, float gravity)
        {
            // A degenerate bore has no direction to shoot along: the only sane answer is the muzzle.
            if (forward.sqrMagnitude <= 1e-12f) return muzzle;
            forward = forward.normalized;

            // No time of flight to integrate over (instant round, or nothing downrange): straight bore.
            if (muzzleSpeed <= 0f || range <= 0f)
                return muzzle + forward * Mathf.Max(range, 0f);

            float t = range / muzzleSpeed;
            return muzzle + forward * range + Vector3.down * (0.5f * gravity * t * t);
        }
    }
}
