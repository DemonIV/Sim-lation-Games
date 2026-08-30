using NUnit.Framework;
using Sim.Core;

namespace Sim.Tests
{
    public class MissionGradeTests
    {
        [Test]
        public void NotWon_ZeroStars()
        {
            Assert.AreEqual(0, MissionGrade.Stars(MissionStatus.Lost, 0, 10f));
            Assert.AreEqual(0, MissionGrade.Stars(MissionStatus.InProgress, 0, 10f));
        }

        [Test]
        public void CleanFastWin_ThreeStars()
        {
            Assert.AreEqual(3, MissionGrade.Stars(MissionStatus.Won, 0, 30f));
        }

        [Test]
        public void WinWithLoss_LosesAStar()
        {
            Assert.AreEqual(2, MissionGrade.Stars(MissionStatus.Won, 1, 30f));
        }

        [Test]
        public void SlowWin_LosesAStar()
        {
            Assert.AreEqual(2, MissionGrade.Stars(MissionStatus.Won, 0, 200f));
        }

        [Test]
        public void MessyWin_FlooredAtOneStar()
        {
            Assert.AreEqual(1, MissionGrade.Stars(MissionStatus.Won, 3, 300f));
        }
    }
}
