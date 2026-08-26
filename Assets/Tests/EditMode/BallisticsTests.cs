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
    }
}
