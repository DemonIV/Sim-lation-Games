using NUnit.Framework;
using UnityEngine;
using Sim.Core;

namespace Sim.Tests
{
    public class GunPipperTests
    {
        private const float Tol = 1e-3f;

        [Test]
        public void ZeroGravity_LandsExactlyOnTheBore()
        {
            Vector3 muzzle = new Vector3(10f, 50f, -4f);
            Vector3 p = GunPipper.AimPoint(muzzle, Vector3.forward, 400f, 200f, 0f);

            Assert.AreEqual(10f, p.x, Tol);
            Assert.AreEqual(50f, p.y, Tol);
            Assert.AreEqual(196f, p.z, Tol);
        }

        [Test]
        public void PositiveGravity_DropsByHalfGTSquared()
        {
            // t = range / speed = 200 / 400 = 0.5 s -> drop = 0.5 * 9.81 * 0.25 = 1.22625 m
            Vector3 muzzle = new Vector3(0f, 100f, 0f);
            Vector3 p = GunPipper.AimPoint(muzzle, Vector3.forward, 400f, 200f, GunPipper.EarthGravity);

            Assert.AreEqual(0f, p.x, Tol);
            Assert.AreEqual(200f, p.z, Tol);
            Assert.AreEqual(100f - 1.22625f, p.y, Tol);
        }

        [Test]
        public void DropIsOnlyVertical_NeverAlongTheBore()
        {
            Vector3 flat = GunPipper.AimPoint(Vector3.zero, Vector3.forward, 100f, 100f, 0f);
            Vector3 dropped = GunPipper.AimPoint(Vector3.zero, Vector3.forward, 100f, 100f,
                                                 GunPipper.EarthGravity);

            Assert.AreEqual(flat.x, dropped.x, Tol);
            Assert.AreEqual(flat.z, dropped.z, Tol);
            // t = 1 s -> drop = 4.905 m
            Assert.AreEqual(flat.y - 4.905f, dropped.y, Tol);
        }

        [Test]
        public void DropGrowsWithTheSquareOfTimeOfFlight()
        {
            float near = GunPipper.AimPoint(Vector3.zero, Vector3.forward, 100f, 50f,
                                            GunPipper.EarthGravity).y;
            float far = GunPipper.AimPoint(Vector3.zero, Vector3.forward, 100f, 100f,
                                           GunPipper.EarthGravity).y;

            // Twice the range at the same speed is twice the flight time, so four times the drop.
            Assert.AreEqual(4f * -near, -far, Tol);
        }

        [Test]
        public void ForwardIsNormalised_SoRangeIsAlwaysInMetres()
        {
            Vector3 p = GunPipper.AimPoint(Vector3.zero, new Vector3(0f, 0f, 7f), 0f, 25f, 0f);

            Assert.AreEqual(0f, p.x, Tol);
            Assert.AreEqual(0f, p.y, Tol);
            Assert.AreEqual(25f, p.z, Tol);
        }

        [Test]
        public void DiagonalBore_KeepsTheRangeAlongTheBore()
        {
            Vector3 p = GunPipper.AimPoint(Vector3.zero, new Vector3(1f, 0f, 1f), 0f, 10f, 0f);

            Assert.AreEqual(10f, p.magnitude, Tol);
            Assert.AreEqual(p.x, p.z, Tol);
        }

        [Test]
        public void ZeroMuzzleSpeed_IsAHitscanRoundOnTheBore()
        {
            // No division by zero and no drop: an instantaneous round lands on the bore line.
            Vector3 p = GunPipper.AimPoint(Vector3.zero, Vector3.forward, 0f, 60f,
                                           GunPipper.EarthGravity);

            Assert.AreEqual(0f, p.y, Tol);
            Assert.AreEqual(60f, p.z, Tol);
            Assert.IsFalse(float.IsNaN(p.y) || float.IsInfinity(p.y));
        }

        [Test]
        public void NegativeMuzzleSpeed_IsTreatedAsHitscan()
        {
            Vector3 p = GunPipper.AimPoint(Vector3.zero, Vector3.forward, -250f, 60f,
                                           GunPipper.EarthGravity);

            Assert.AreEqual(0f, p.y, Tol);
            Assert.AreEqual(60f, p.z, Tol);
        }

        [Test]
        public void ZeroRange_ReturnsTheMuzzle()
        {
            Vector3 muzzle = new Vector3(3f, 12f, -7f);
            Vector3 p = GunPipper.AimPoint(muzzle, Vector3.forward, 400f, 0f, GunPipper.EarthGravity);

            Assert.AreEqual(muzzle.x, p.x, Tol);
            Assert.AreEqual(muzzle.y, p.y, Tol);
            Assert.AreEqual(muzzle.z, p.z, Tol);
        }

        [Test]
        public void NegativeRange_NeverAimsBehindTheMuzzle()
        {
            Vector3 muzzle = new Vector3(3f, 12f, -7f);
            Vector3 p = GunPipper.AimPoint(muzzle, Vector3.forward, 400f, -80f, GunPipper.EarthGravity);

            Assert.AreEqual(muzzle.x, p.x, Tol);
            Assert.AreEqual(muzzle.y, p.y, Tol);
            Assert.AreEqual(muzzle.z, p.z, Tol);
        }

        [Test]
        public void ZeroForward_ReturnsTheMuzzle()
        {
            Vector3 muzzle = new Vector3(-2f, 40f, 6f);
            Vector3 p = GunPipper.AimPoint(muzzle, Vector3.zero, 400f, 200f, GunPipper.EarthGravity);

            Assert.AreEqual(muzzle.x, p.x, Tol);
            Assert.AreEqual(muzzle.y, p.y, Tol);
            Assert.AreEqual(muzzle.z, p.z, Tol);
        }

        [Test]
        public void PitchedNoseUp_PutsTheAimPointAboveTheMuzzle()
        {
            // 30° nose-up bore, hitscan: the pipper must sit above the shooter, which is what makes
            // the HUD reticle slide down the screen as the aircraft pitches up.
            Vector3 up30 = Quaternion.Euler(-30f, 0f, 0f) * Vector3.forward;
            Vector3 p = GunPipper.AimPoint(Vector3.zero, up30, 0f, 100f, 0f);

            Assert.AreEqual(50f, p.y, 1e-2f);      // sin(30°) * 100
            Assert.AreEqual(86.602f, p.z, 1e-2f);  // cos(30°) * 100
        }

        [Test]
        public void MuzzleOffset_IsCarriedIntoTheResult()
        {
            Vector3 muzzle = new Vector3(120f, 33f, -45f);
            Vector3 p = GunPipper.AimPoint(muzzle, Vector3.right, 0f, 10f, 0f);

            Assert.AreEqual(130f, p.x, Tol);
            Assert.AreEqual(33f, p.y, Tol);
            Assert.AreEqual(-45f, p.z, Tol);
        }
    }
}
