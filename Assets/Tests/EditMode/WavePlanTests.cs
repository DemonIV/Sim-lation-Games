using NUnit.Framework;
using Sim.Core;

namespace Sim.Tests
{
    public class WavePlanTests
    {
        [Test]
        public void PlainHostiles_GrowEachWave()
        {
            Assert.AreEqual(1, WavePlan.PlainHostilesForWave(0));
            Assert.AreEqual(3, WavePlan.PlainHostilesForWave(2));
        }

        [Test]
        public void Sams_GrowEveryTwoWaves()
        {
            Assert.AreEqual(1, WavePlan.SamsForWave(0));
            Assert.AreEqual(1, WavePlan.SamsForWave(1));
            Assert.AreEqual(2, WavePlan.SamsForWave(2));
        }

        [Test]
        public void Aaa_MatchesWaveIndex()
        {
            Assert.AreEqual(0, WavePlan.AaaForWave(0));
            Assert.AreEqual(2, WavePlan.AaaForWave(2));
        }

        [Test]
        public void Total_SumsAllTypes()
        {
            Assert.AreEqual(2, WavePlan.TotalEnemiesForWave(0));   // 1+1+0
            Assert.AreEqual(7, WavePlan.TotalEnemiesForWave(2));   // 3+2+2
        }

        [Test]
        public void NegativeIndex_Clamps()
        {
            Assert.AreEqual(1, WavePlan.PlainHostilesForWave(-5));
        }
    }
}
