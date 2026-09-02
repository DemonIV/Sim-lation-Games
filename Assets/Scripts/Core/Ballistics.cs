using UnityEngine;

namespace Sim.Core
{
    /// <summary>Ballistics helpers for aiming at moving targets. Pure logic.</summary>
    public static class Ballistics
    {
        /// <summary>Iteratively computes an intercept aim point for a finite-speed projectile against a moving target.</summary>
        public static Vector3 ComputeInterceptPoint(Vector3 shooter, Vector3 targetPos,
                                                     Vector3 targetVel, float projectileSpeed,
                                                     int iterations = 8)
        {
            if (projectileSpeed <= 0f) return targetPos;
            Vector3 aim = targetPos;
            for (int i = 0; i < iterations; i++)
            {
                float t = Vector3.Distance(shooter, aim) / projectileSpeed;
                aim = targetPos + targetVel * t;
            }
            return aim;
        }

        /// <summary>
        /// Launch velocity that throws a purely ballistic round from <paramref name="launchPoint"/>
        /// onto <paramref name="targetPoint"/> at a fixed <paramref name="speed"/> — the aiming
        /// solution the fighter jet's balistik füze is fired with.
        ///
        /// <para>
        /// This is the CLOSED-FORM gravity-only solution, deliberately: the round itself is flown by
        /// <see cref="BallisticProjectile"/> (gravity + drag + wind + air density), and no closed form
        /// exists for that. Solving the drag-free problem and letting the integrator fly the result is
        /// exactly how the gun pipper already treats its own drop.
        /// </para>
        ///
        /// <para>
        /// With <c>d</c> the horizontal distance, <c>y</c> the height difference and <c>v</c> the
        /// speed, the launch angle satisfies
        /// <c>tan θ = (v² ± √(v⁴ − g(g d² + 2 y v²))) / (g d)</c>. The discriminant going negative is
        /// the round being physically OUT OF REACH at this speed, which is reported as false rather
        /// than fudged. <paramref name="highArc"/> picks the lofted (+) solution — the heavy, arcing
        /// trajectory that makes this weapon read differently from the flat-shooting guided missile;
        /// the low (−) solution is the flat one.
        /// </para>
        ///
        /// <para><paramref name="gravity"/> is a POSITIVE magnitude in m/s² pulling along
        /// <see cref="Vector3.down"/>, the same convention as <see cref="GunPipper"/> and
        /// <see cref="BallisticProjectile.Gravity"/>. Pass <see cref="GunPipper.EarthGravity"/> unless
        /// there is a reason not to.</para>
        /// </summary>
        public static bool TryLoftedLaunchVelocity(Vector3 launchPoint, Vector3 targetPoint,
                                                   float speed, float gravity, bool highArc,
                                                   out Vector3 velocity)
        {
            velocity = Vector3.zero;
            if (speed <= 0f) return false;

            Vector3 delta = targetPoint - launchPoint;

            // No gravity to fight: firing straight at the target IS the solution.
            if (gravity <= 0f)
            {
                if (delta.sqrMagnitude <= 1e-8f) return false;
                velocity = delta.normalized * speed;
                return true;
            }

            Vector3 flat = new Vector3(delta.x, 0f, delta.z);
            float d = flat.magnitude;
            float y = delta.y;

            // Straight overhead (or straight below): there is no horizontal component to solve for.
            if (d <= 1e-4f)
            {
                if (y >= 0f)
                {
                    // Only reachable when the round can actually climb that high: v² / 2g ≥ y.
                    if (speed * speed < 2f * gravity * y) return false;
                    velocity = Vector3.up * speed;
                    return true;
                }
                velocity = Vector3.down * speed;
                return true;
            }

            float v2 = speed * speed;
            float discriminant = v2 * v2 - gravity * (gravity * d * d + 2f * y * v2);
            if (discriminant < 0f) return false;   // out of reach at this speed

            float root = Mathf.Sqrt(discriminant);
            float tangent = (highArc ? v2 + root : v2 - root) / (gravity * d);
            float angle = Mathf.Atan(tangent);

            Vector3 direction = flat / d;
            velocity = (direction * Mathf.Cos(angle) + Vector3.up * Mathf.Sin(angle)) * speed;
            return true;
        }

        /// <summary>
        /// Flight time of a ballistic round launched with <paramref name="launchVelocity"/> toward
        /// <paramref name="targetPoint"/>: horizontal distance divided by horizontal speed, which
        /// gravity does not change. Returns 0 for a degenerate (vertical or motionless) shot, so a
        /// caller can use it directly as a lifetime without guarding against infinities.
        /// </summary>
        public static float LoftedTimeOfFlight(Vector3 launchPoint, Vector3 targetPoint,
                                               Vector3 launchVelocity)
        {
            Vector3 delta = targetPoint - launchPoint;
            float d = new Vector3(delta.x, 0f, delta.z).magnitude;
            float horizontalSpeed = new Vector3(launchVelocity.x, 0f, launchVelocity.z).magnitude;
            if (d <= 0f || horizontalSpeed <= 1e-4f) return 0f;
            return d / horizontalSpeed;
        }
    }
}
