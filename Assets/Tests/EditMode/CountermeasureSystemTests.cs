using NUnit.Framework;
using Sim.Core;

namespace Sim.Tests
{
    public class CountermeasureSystemTests
    {
        [Test]
        public void New_IsFull()
        {
            var cm = new CountermeasureSystem(8, 2f, 0.6f);
            Assert.AreEqual(8, cm.Charges);
            Assert.AreEqual(1f, cm.ChargeFraction, 1e-4f);
            Assert.IsTrue(cm.CanDeploy);
        }

        [Test]
        public void Deploy_ConsumesChargeAndStartsCooldown()
        {
            var cm = new CountermeasureSystem(8, 2f, 0.6f);
            Assert.IsTrue(cm.TryDeploy());
            Assert.AreEqual(7, cm.Charges);
            Assert.AreEqual(2f, cm.Cooldown, 1e-4f);
            Assert.IsFalse(cm.TryDeploy());
        }

        [Test]
        public void Tick_ClearsCooldown()
        {
            var cm = new CountermeasureSystem(8, 2f, 0.6f);
            cm.TryDeploy();
            cm.Tick(2f);
            Assert.IsTrue(cm.CanDeploy);
        }

        [Test]
        public void Empty_CannotDeploy_UntilReload()
        {
            var cm = new CountermeasureSystem(1, 0f, 0.6f);
            Assert.IsTrue(cm.TryDeploy());
            Assert.IsFalse(cm.TryDeploy());
            cm.Reload();
            Assert.IsTrue(cm.TryDeploy());
        }
    }
}
