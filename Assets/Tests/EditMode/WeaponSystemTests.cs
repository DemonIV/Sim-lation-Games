using NUnit.Framework;
using Sim.Core;

namespace Sim.Tests
{
    public class WeaponSystemTests
    {
        [Test]
        public void NewWeapon_IsFullyLoaded_AndReady()
        {
            var w = new WeaponSystem(8, 2f);
            Assert.AreEqual(8, w.Ammo);
            Assert.AreEqual(0f, w.Cooldown, 1e-4f);
        }

        [Test]
        public void TryFire_WithoutLock_Fails()
        {
            var w = new WeaponSystem(8, 2f);
            Assert.IsFalse(w.TryFire(hasLock: false));
            Assert.AreEqual(8, w.Ammo);
        }

        [Test]
        public void TryFire_WithLock_ConsumesAmmoAndSetsCooldown()
        {
            var w = new WeaponSystem(8, 2f);
            Assert.IsTrue(w.TryFire(hasLock: true));
            Assert.AreEqual(7, w.Ammo);
            Assert.AreEqual(0.5f, w.Cooldown, 1e-4f);       // 1 / 2 rps
            Assert.IsFalse(w.TryFire(hasLock: true));       // still cooling down
        }

        [Test]
        public void Tick_ClearsCooldown_AllowingNextShot()
        {
            var w = new WeaponSystem(8, 2f);
            w.TryFire(true);
            w.Tick(0.5f);
            Assert.AreEqual(0f, w.Cooldown, 1e-4f);
            Assert.IsTrue(w.TryFire(true));
            Assert.AreEqual(6, w.Ammo);
        }

        [Test]
        public void CannotFire_WhenEmpty_UntilReload()
        {
            var w = new WeaponSystem(2, 100f);
            Assert.IsTrue(w.TryFire(true)); w.Tick(1f);
            Assert.IsTrue(w.TryFire(true)); w.Tick(1f);
            Assert.AreEqual(0, w.Ammo);
            Assert.IsFalse(w.TryFire(true));
            w.Reload();
            Assert.AreEqual(2, w.Ammo);
            Assert.IsTrue(w.TryFire(true));
        }
    }
}
