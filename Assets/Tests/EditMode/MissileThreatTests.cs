using NUnit.Framework;
using Sim.Core;

namespace Sim.Tests
{
    public class MissileThreatTests
    {
        [Test]
        public void TimeToImpact_IsRangeOverClosingVelocity()
        {
            Assert.AreEqual(2f, MissileThreat.TimeToImpact(100f, 50f), 1e-4f);
        }

        [Test]
        public void NotClosing_MeansNoImpact()
        {
            Assert.IsTrue(float.IsPositiveInfinity(MissileThreat.TimeToImpact(100f, 0f)));
            Assert.IsTrue(float.IsPositiveInfinity(MissileThreat.TimeToImpact(100f, -20f)));
        }

        [Test]
        public void EarlyBeamingRelease_KeepsFullChance()
        {
            // 3s+ warning, missile off the nose (aspectDot <= 0) -> full base probability
            Assert.AreEqual(0.6f, MissileThreat.DecoyChance(0.6f, 4f, -1f), 1e-3f);
        }

        [Test]
        public void LateRelease_IsMuchWorse()
        {
            Assert.AreEqual(0.06f, MissileThreat.DecoyChance(0.6f, 0.3f, 0f), 1e-3f);
        }

        [Test]
        public void NoseOnAspect_HalvesChance()
        {
            Assert.AreEqual(0.3f, MissileThreat.DecoyChance(0.6f, 5f, 1f), 1e-3f);
        }
    }
}
