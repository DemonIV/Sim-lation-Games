using NUnit.Framework;
using Sim.Core;

namespace Sim.Tests
{
    public class SquadStatusTests
    {
        [Test]
        public void NoDronesLeft_IsIneffective()
        {
            Assert.IsTrue(SquadStatus.IsCombatIneffective(0, 0));
        }

        [Test]
        public void AllSurvivorsDry_IsIneffective()
        {
            Assert.IsTrue(SquadStatus.IsCombatIneffective(2, 0));
        }

        [Test]
        public void OneFuelledDrone_IsStillEffective()
        {
            Assert.IsFalse(SquadStatus.IsCombatIneffective(3, 1));
        }
    }
}
