using NUnit.Framework;
using Sim.Core;

namespace Sim.Tests
{
    public class ScenarioLibraryTests
    {
        [Test]
        public void AllScenarios_HaveTitleDescriptionAndWaves()
        {
            Assert.AreEqual(4, ScenarioLibrary.All.Length);
            foreach (var k in ScenarioLibrary.All)
            {
                Assert.IsFalse(string.IsNullOrEmpty(ScenarioLibrary.Title(k)));
                Assert.IsFalse(string.IsNullOrEmpty(ScenarioLibrary.Description(k)));
                Assert.GreaterOrEqual(ScenarioLibrary.TotalWaves(k), 1);
            }
        }

        [Test]
        public void Recon_IsGroundTargetsOnly()
        {
            var c = ScenarioLibrary.Composition(ScenarioKind.Recon, 0);
            Assert.AreEqual(2, c.PlainHostiles);
            Assert.AreEqual(0, c.Sams);
            Assert.AreEqual(0, c.Fighters);
            Assert.AreEqual(2, c.Total);
        }

        [Test]
        public void Sead_IsAirDefencesOnly()
        {
            var c = ScenarioLibrary.Composition(ScenarioKind.Sead, 0);
            Assert.AreEqual(0, c.PlainHostiles);
            Assert.AreEqual(1, c.Sams);
            Assert.AreEqual(1, c.Aaa);
            Assert.AreEqual(0, c.Fighters);
        }

        [Test]
        public void AirCombat_IsFightersOnly()
        {
            var c = ScenarioLibrary.Composition(ScenarioKind.AirCombat, 1);
            Assert.AreEqual(3, c.Fighters);
            Assert.AreEqual(0, c.PlainHostiles);
            Assert.AreEqual(0, c.Sams);
            Assert.AreEqual(0, c.Aaa);
        }

        [Test]
        public void MixedDefense_MatchesWavePlan()
        {
            var c = ScenarioLibrary.Composition(ScenarioKind.MixedDefense, 2);
            Assert.AreEqual(WavePlan.TotalEnemiesForWave(2), c.Total);
        }

        [Test]
        public void Waves_EscalateAndClampNegativeIndex()
        {
            Assert.Greater(ScenarioLibrary.Composition(ScenarioKind.Sead, 2).Total,
                           ScenarioLibrary.Composition(ScenarioKind.Sead, 0).Total);
            Assert.AreEqual(ScenarioLibrary.Composition(ScenarioKind.Recon, 0).Total,
                            ScenarioLibrary.Composition(ScenarioKind.Recon, -3).Total);
        }
    }
}
