using NUnit.Framework;
using Sim.Core;

namespace Sim.Tests
{
    public class ThrottleGovernorTests
    {
        [Test]
        public void FullTank_PassesThrottleThrough()
        {
            Assert.AreEqual(1f, ThrottleGovernor.Effective(1f, 1f), 1e-4f);
            Assert.AreEqual(0.5f, ThrottleGovernor.Effective(0.5f, 0.8f), 1e-4f);
        }

        [Test]
        public void DryTank_MeansNoPower()
        {
            Assert.AreEqual(0f, ThrottleGovernor.Effective(1f, 0f), 1e-4f);
        }

        [Test]
        public void InReserve_PowerTapers()
        {
            // half of the 5% reserve remaining -> half power
            Assert.AreEqual(0.5f, ThrottleGovernor.Effective(1f, 0.025f), 1e-3f);
        }

        [Test]
        public void CommandedThrottle_IsClamped()
        {
            Assert.AreEqual(1f, ThrottleGovernor.Effective(5f, 1f), 1e-4f);
            Assert.AreEqual(0f, ThrottleGovernor.Effective(-2f, 1f), 1e-4f);
        }
    }
}
