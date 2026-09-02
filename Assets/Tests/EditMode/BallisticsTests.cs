using NUnit.Framework;
using UnityEngine;
using Sim.Core;

namespace Sim.Tests
{
    public class BallisticsTests
    {
        [Test]
        public void Intercept_StationaryTarget_ReturnsTargetPosition()
        {
            var aim = Ballistics.ComputeInterceptPoint(
                Vector3.zero, new Vector3(0, 0, 100f), Vector3.zero, 100f);
            Assert.AreEqual(new Vector3(0, 0, 100f), aim);
        }

        [Test]
        public void Intercept_MovingTarget_LeadsAlongVelocity()
        {
            // Target 100m ahead moving +x at 10 m/s, projectile 100 m/s -> t ~ 1s -> lead ~10m in x.
            var aim = Ballistics.ComputeInterceptPoint(
                Vector3.zero, new Vector3(0, 0, 100f), new Vector3(10f, 0, 0), 100f);
            Assert.AreEqual(10.05f, aim.x, 0.2f);
            Assert.AreEqual(100f, aim.z, 1e-3f);
        }

        [Test]
        public void Intercept_ZeroProjectileSpeed_ReturnsTargetPosition()
        {
            var aim = Ballistics.ComputeInterceptPoint(
                Vector3.zero, new Vector3(5, 0, 5), new Vector3(1, 0, 0), 0f);
            Assert.AreEqual(new Vector3(5, 0, 5), aim);
        }

        // ------------------------------------------------------------------ lofted launch solution

        private const float G = GunPipper.EarthGravity;

        /// <summary>Where a drag-free round launched with this velocity is after <c>t</c> seconds.</summary>
        private static Vector3 Fly(Vector3 launch, Vector3 velocity, float t)
        {
            return launch + velocity * t + Vector3.down * (0.5f * G * t * t);
        }

        [Test]
        public void LoftedLaunch_HighArc_ActuallyArrivesOnTheTarget()
        {
            Vector3 launch = new Vector3(0f, 24f, 0f);
            Vector3 target = new Vector3(40f, 6f, 90f);

            Assert.IsTrue(Ballistics.TryLoftedLaunchVelocity(launch, target, 35f, G, true,
                                                             out Vector3 v));

            float t = Ballistics.LoftedTimeOfFlight(launch, target, v);
            Assert.Greater(t, 0f);

            Vector3 impact = Fly(launch, v, t);
            Assert.AreEqual(target.x, impact.x, 0.05f);
            Assert.AreEqual(target.y, impact.y, 0.05f);
            Assert.AreEqual(target.z, impact.z, 0.05f);
        }

        [Test]
        public void LoftedLaunch_LowArc_AlsoArrives_ButFlatterAndSooner()
        {
            Vector3 launch = new Vector3(0f, 24f, 0f);
            Vector3 target = new Vector3(0f, 20f, 120f);

            Assert.IsTrue(Ballistics.TryLoftedLaunchVelocity(launch, target, 80f, G, true,
                                                             out Vector3 high));
            Assert.IsTrue(Ballistics.TryLoftedLaunchVelocity(launch, target, 80f, G, false,
                                                             out Vector3 low));

            float tHigh = Ballistics.LoftedTimeOfFlight(launch, target, high);
            float tLow = Ballistics.LoftedTimeOfFlight(launch, target, low);

            Vector3 impact = Fly(launch, low, tLow);
            Assert.AreEqual(target.z, impact.z, 0.05f);
            Assert.AreEqual(target.y, impact.y, 0.05f);

            Assert.Greater(high.y, low.y, "the lofted solution must leave the rail steeper");
            Assert.Greater(tHigh, tLow, "the lofted round must take longer to arrive");
        }

        [Test]
        public void LoftedLaunch_SpeedIsHonouredExactly()
        {
            Assert.IsTrue(Ballistics.TryLoftedLaunchVelocity(Vector3.zero, new Vector3(0f, 0f, 60f),
                                                             55f, G, true, out Vector3 v));
            Assert.AreEqual(55f, v.magnitude, 1e-3f);
        }

        [Test]
        public void LoftedLaunch_OutOfReach_ReturnsFalseInsteadOfFudgingAnAngle()
        {
            // Maximum flat range at speed v is v² / g; 30 m/s reaches ~91.7 m, so 400 m is impossible.
            Assert.IsFalse(Ballistics.TryLoftedLaunchVelocity(Vector3.zero, new Vector3(0f, 0f, 400f),
                                                             30f, G, true, out Vector3 v));
            Assert.AreEqual(Vector3.zero, v);
        }

        [Test]
        public void LoftedLaunch_NearMaximumRange_BothSolutionsConvergeOn45Degrees()
        {
            // Flat maximum range is v² / g, reached at exactly 45°. Just inside it the two solutions
            // are still distinct but have closed right up on 45° from either side. (Exactly AT the
            // limit the discriminant is zero, which float arithmetic cannot be trusted to reproduce.)
            const float speed = 60f;
            float range = 0.99f * speed * speed / G;
            var target = new Vector3(0f, 0f, range);

            Assert.IsTrue(Ballistics.TryLoftedLaunchVelocity(Vector3.zero, target, speed, G, true,
                                                             out Vector3 high));
            Assert.IsTrue(Ballistics.TryLoftedLaunchVelocity(Vector3.zero, target, speed, G, false,
                                                             out Vector3 low));

            float highDeg = Mathf.Atan2(high.y, high.z) * Mathf.Rad2Deg;
            float lowDeg = Mathf.Atan2(low.y, low.z) * Mathf.Rad2Deg;

            Assert.Greater(highDeg, 45f);
            Assert.Less(lowDeg, 45f);
            Assert.AreEqual(45f, highDeg, 5f);
            Assert.AreEqual(45f, lowDeg, 5f);
        }

        [Test]
        public void LoftedLaunch_ZeroSpeed_IsRefused()
        {
            Assert.IsFalse(Ballistics.TryLoftedLaunchVelocity(Vector3.zero, new Vector3(0f, 0f, 10f),
                                                              0f, G, true, out _));
        }

        [Test]
        public void LoftedLaunch_WithoutGravity_FiresStraightAtTheTarget()
        {
            Vector3 target = new Vector3(30f, 10f, 40f);
            Assert.IsTrue(Ballistics.TryLoftedLaunchVelocity(Vector3.zero, target, 50f, 0f, true,
                                                             out Vector3 v));

            Vector3 expected = target.normalized * 50f;
            Assert.AreEqual(expected.x, v.x, 1e-3f);
            Assert.AreEqual(expected.y, v.y, 1e-3f);
            Assert.AreEqual(expected.z, v.z, 1e-3f);
        }

        [Test]
        public void LoftedLaunch_StraightOverhead_ClimbsOrIsRefusedByEnergy()
        {
            // Straight up, within reach: v² / 2g = 45 m at 30 m/s.
            Assert.IsTrue(Ballistics.TryLoftedLaunchVelocity(Vector3.zero, new Vector3(0f, 20f, 0f),
                                                             30f, G, true, out Vector3 up));
            Assert.AreEqual(30f, up.y, 1e-3f);

            // Straight up, beyond the energy the round has.
            Assert.IsFalse(Ballistics.TryLoftedLaunchVelocity(Vector3.zero, new Vector3(0f, 200f, 0f),
                                                              30f, G, true, out _));

            // Straight down is always reachable.
            Assert.IsTrue(Ballistics.TryLoftedLaunchVelocity(new Vector3(0f, 50f, 0f), Vector3.zero,
                                                             30f, G, true, out Vector3 down));
            Assert.AreEqual(-30f, down.y, 1e-3f);
        }

        [Test]
        public void LoftedTimeOfFlight_IsZeroForADegenerateShot()
        {
            Assert.AreEqual(0f, Ballistics.LoftedTimeOfFlight(Vector3.zero, Vector3.zero,
                                                              Vector3.up * 40f), 1e-4f);
            Assert.AreEqual(0f, Ballistics.LoftedTimeOfFlight(Vector3.zero, new Vector3(0f, 0f, 50f),
                                                              Vector3.up * 40f), 1e-4f);
        }
    }
}
