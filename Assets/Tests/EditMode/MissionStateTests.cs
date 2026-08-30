using NUnit.Framework;
using Sim.Core;

namespace Sim.Tests
{
    public class MissionStateTests
    {
        [Test]
        public void Starts_InProgress()
        {
            var m = new MissionState(3, 1);
            Assert.AreEqual(MissionStatus.InProgress, m.Status);
        }

        [Test]
        public void DestroyingAllHostiles_Wins()
        {
            var m = new MissionState(3, 1);
            m.RecordHostileDestroyed();
            m.RecordHostileDestroyed();
            Assert.AreEqual(MissionStatus.InProgress, m.Status);
            m.RecordHostileDestroyed();
            Assert.AreEqual(MissionStatus.Won, m.Status);
        }

        [Test]
        public void ExceedingFriendlyLosses_Loses()
        {
            var m = new MissionState(3, 1);
            m.RecordFriendlyLost();
            Assert.AreEqual(MissionStatus.InProgress, m.Status);
            m.RecordFriendlyLost();
            Assert.AreEqual(MissionStatus.Lost, m.Status);
        }

        [Test]
        public void AfterMissionEnds_FurtherEventsIgnored()
        {
            var m = new MissionState(1, 1);
            m.RecordHostileDestroyed();
            Assert.AreEqual(MissionStatus.Won, m.Status);
            m.RecordHostileDestroyed();
            Assert.AreEqual(1, m.HostilesDestroyed);
        }

        [Test]
        public void Score_RewardsKills_PenalisesLosses()
        {
            var m = new MissionState(5, 3);
            m.RecordHostileDestroyed();
            m.RecordHostileDestroyed();
            m.RecordHostileDestroyed();
            Assert.AreEqual(300, m.Score);          // 3 * 100
            m.RecordFriendlyLost();
            Assert.AreEqual(150, m.Score);          // 300 - 150
            m.Tick(5.5f);
            Assert.AreEqual(150, m.Score);          // elapsed time no longer affects score
        }

        [Test]
        public void Tick_OnlyAccumulatesWhileInProgress()
        {
            var m = new MissionState(1, 0);
            m.RecordFriendlyLost();                 // exceeds 0 -> Lost
            Assert.AreEqual(MissionStatus.Lost, m.Status);
            m.Tick(10f);
            Assert.AreEqual(0f, m.ElapsedTime, 1e-4f);
        }
    }
}
