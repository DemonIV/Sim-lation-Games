using NUnit.Framework;
using Sim.Core;

namespace Sim.Tests
{
    public class ElectronicWarfareTests
    {
        [Test]
        public void NoJamming_LeavesRangeUnchanged()
        {
            Assert.AreEqual(200f, ElectronicWarfare.EffectiveRange(200f, 0f), 1e-3f);
        }

        [Test]
        public void Jamming_ReducesRange()
        {
            // 200 / (1+15)^0.25 = 200 / 2 = 100
            Assert.AreEqual(100f, ElectronicWarfare.EffectiveRange(200f, 15f), 1e-3f);
        }

        [Test]
        public void MoreJamming_MeansShorterRange()
        {
            Assert.Greater(ElectronicWarfare.EffectiveRange(200f, 3f),
                           ElectronicWarfare.EffectiveRange(200f, 15f));
        }

        [Test]
        public void LockProbability_DropsWithEcm()
        {
            Assert.AreEqual(1f, ElectronicWarfare.LockProbability(0f), 1e-4f);
            Assert.AreEqual(0.5f, ElectronicWarfare.LockProbability(1f), 1e-4f);
            Assert.AreEqual(0.25f, ElectronicWarfare.LockProbability(3f), 1e-4f);
        }
    }
}
