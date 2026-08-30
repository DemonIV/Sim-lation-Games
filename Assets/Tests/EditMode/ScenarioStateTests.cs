using NUnit.Framework;
using Sim.Core;

namespace Sim.Tests
{
    public class ScenarioStateTests
    {
        [Test]
        public void Starts_AwaitingFirstWave()
        {
            var s = new ScenarioState(3);
            Assert.AreEqual(ScenarioStatus.InProgress, s.Status);
            Assert.AreEqual(1, s.CurrentWaveNumber);
            Assert.IsTrue(s.AwaitingSpawn);
        }

        [Test]
        public void EnemiesBeforeSpawn_DoNotAdvance()
        {
            var s = new ScenarioState(3);
            s.UpdateEnemies(0);          // still awaiting spawn
            Assert.AreEqual(0, s.CurrentWaveIndex);
            Assert.IsTrue(s.AwaitingSpawn);
        }

        [Test]
        public void ClearingWave_AdvancesToNext()
        {
            var s = new ScenarioState(3);
            s.MarkWaveSpawned();
            s.UpdateEnemies(3);
            Assert.AreEqual(0, s.CurrentWaveIndex);
            s.UpdateEnemies(0);
            Assert.AreEqual(1, s.CurrentWaveIndex);
            Assert.IsTrue(s.AwaitingSpawn);
        }

        [Test]
        public void ClearingLastWave_Wins()
        {
            var s = new ScenarioState(2);
            s.MarkWaveSpawned();
            s.UpdateEnemies(0);          // clear wave 0 -> wave 1
            s.MarkWaveSpawned();
            s.UpdateEnemies(0);          // clear wave 1 (last) -> Won
            Assert.AreEqual(ScenarioStatus.Won, s.Status);
        }

        [Test]
        public void Fail_SetsLost_AndStops()
        {
            var s = new ScenarioState(3);
            s.MarkWaveSpawned();
            s.Fail();
            Assert.AreEqual(ScenarioStatus.Lost, s.Status);
            s.UpdateEnemies(0);
            Assert.AreEqual(ScenarioStatus.Lost, s.Status);
        }
    }
}
