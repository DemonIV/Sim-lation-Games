using NUnit.Framework;
using Sim.Core;

namespace Sim.Tests
{
    public class EngagementPolicyTests
    {
        [Test]
        public void LowFuel_ReturnsToBase_EvenWithTarget()
        {
            var p = new EngagementPolicy { BingoFuelFraction = 0.25f };
            Assert.AreEqual(EngagementState.ReturnToBase, p.Decide(hasTarget: true, hasAmmo: true, fuelFraction: 0.2f));
        }

        [Test]
        public void NoAmmo_ReturnsToBase()
        {
            var p = new EngagementPolicy();
            Assert.AreEqual(EngagementState.ReturnToBase, p.Decide(hasTarget: true, hasAmmo: false, fuelFraction: 1f));
        }

        [Test]
        public void TargetWithMeans_Engages()
        {
            var p = new EngagementPolicy();
            Assert.AreEqual(EngagementState.Engage, p.Decide(hasTarget: true, hasAmmo: true, fuelFraction: 1f));
        }

        [Test]
        public void NoTarget_Patrols()
        {
            var p = new EngagementPolicy();
            Assert.AreEqual(EngagementState.Patrol, p.Decide(hasTarget: false, hasAmmo: true, fuelFraction: 1f));
        }

        [Test]
        public void AtBingoThreshold_ReturnsToBase()
        {
            var p = new EngagementPolicy { BingoFuelFraction = 0.25f };
            Assert.AreEqual(EngagementState.ReturnToBase, p.Decide(true, true, 0.25f));
        }
    }
}
