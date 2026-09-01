using UnityEngine;

namespace Sim.Core
{
    /// <summary>
    /// Structural agility of a guided munition: how fast an airframe pulling a given load factor can
    /// actually rotate its velocity vector, and how a commanded steering direction is clamped to that
    /// limit over one timestep. Pure logic.
    ///
    /// <para>
    /// This is the piece that makes a missile a BOUNDED pursuer: a guidance law on its own (see
    /// <see cref="ProportionalNavigation"/>) will happily command any lateral acceleration, and a
    /// missile that can turn arbitrarily hard is unbeatable by definition. Limiting the turn rate is
    /// the classic reason a late, hard break turn works — and the reason a FASTER missile is a LESS
    /// agile one: the same lateral acceleration bends a faster velocity vector through a smaller angle.
    /// </para>
    ///
    /// This is a GAME / EDUCATIONAL model with abstract, gamified parameters.
    /// </summary>
    public static class MissileAgility
    {
        /// <summary>Standard gravity (m/s²), the unit a load factor ("g") is expressed in.</summary>
        public const float StandardGravity = 9.81f;

        /// <summary>
        /// Maximum turn rate (radians per second) of an airframe flying at <paramref name="speed"/>
        /// while pulling <paramref name="maxG"/> of lateral load: <c>maxG · g / speed</c>.
        /// Returns 0 for degenerate inputs (zero/negative speed or load) — no turn authority, never a
        /// divide by zero.
        /// </summary>
        public static float MaxTurnRateRad(float maxG, float speed)
        {
            if (maxG <= 0f) return 0f;
            if (speed <= 1e-4f) return 0f;
            return maxG * StandardGravity / speed;
        }

        /// <summary>Degrees-per-second form of <see cref="MaxTurnRateRad"/>, for inspectors and HUDs.</summary>
        public static float MaxTurnRateDeg(float maxG, float speed)
        {
            return MaxTurnRateRad(maxG, speed) * Mathf.Rad2Deg;
        }

        /// <summary>
        /// Radius (m) of the tightest turn the airframe can fly at this speed and load factor.
        /// PositiveInfinity when it has no turn authority at all.
        /// </summary>
        public static float TurnRadius(float maxG, float speed)
        {
            float rate = MaxTurnRateRad(maxG, speed);
            if (rate <= 0f) return float.PositiveInfinity;
            return speed / rate;
        }

        /// <summary>
        /// Clamps a commanded steering direction to what the airframe can actually reach in
        /// <paramref name="dt"/> seconds at <paramref name="maxTurnRateRad"/>. A small command passes
        /// through unchanged; a large one is rotated toward the command by exactly
        /// <c>maxTurnRateRad · dt</c>. The result is always normalised.
        /// </summary>
        public static Vector3 ClampTurn(Vector3 currentDirection, Vector3 desiredDirection,
                                        float maxTurnRateRad, float dt)
        {
            Vector3 current = currentDirection.sqrMagnitude > 1e-6f
                ? currentDirection.normalized
                : Vector3.forward;

            if (desiredDirection.sqrMagnitude <= 1e-6f) return current;
            Vector3 desired = desiredDirection.normalized;

            // No authority, or no time passing: the heading cannot change at all.
            if (maxTurnRateRad <= 0f || dt <= 0f) return current;

            float maxStepRad = maxTurnRateRad * dt;
            Vector3 result = Vector3.RotateTowards(current, desired, maxStepRad, 0f);
            return result.sqrMagnitude > 1e-6f ? result.normalized : current;
        }
    }
}
