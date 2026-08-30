using NUnit.Framework;
using Sim.Core;

namespace Sim.Tests
{
    public class GunSystemTests
    {
        [Test]
        public void New_IsFullyLoaded()
        {
            var g = new GunSystem(300, 10f, 60f, 2f);
            Assert.AreEqual(300, g.Ammo);
            Assert.AreEqual(1f, g.AmmoFraction, 1e-4f);
            Assert.IsTrue(g.CanFire);
        }

        [Test]
        public void TryFire_ConsumesAmmoAndSetsCooldown()
        {
            var g = new GunSystem(10, 10f, 60f, 2f);
            Assert.IsTrue(g.TryFire());
            Assert.AreEqual(9, g.Ammo);
            Assert.AreEqual(0.1f, g.Cooldown, 1e-4f);
            Assert.IsFalse(g.TryFire());
        }

        [Test]
        public void Tick_ClearsCooldown()
        {
            var g = new GunSystem(10, 10f, 60f, 2f);
            g.TryFire();
            g.Tick(0.1f);
            Assert.AreEqual(0f, g.Cooldown, 1e-4f);
            Assert.IsTrue(g.TryFire());
        }

        [Test]
        public void Empty_CannotFire_UntilReload()
        {
            var g = new GunSystem(2, 100f, 60f, 2f);
            g.TryFire(); g.Tick(1f);
            g.TryFire(); g.Tick(1f);
            Assert.AreEqual(0, g.Ammo);
            Assert.IsFalse(g.TryFire());
            g.Reload();
            Assert.AreEqual(2, g.Ammo);
            Assert.IsTrue(g.TryFire());
        }

        [Test]
        public void InRange_RespectsEffectiveRange()
        {
            var g = new GunSystem(10, 10f, 60f, 2f);
            Assert.IsTrue(g.InRange(59f));
            Assert.IsFalse(g.InRange(61f));
        }
    }
}
