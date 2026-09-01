using NUnit.Framework;
using Sim.Core;

namespace Sim.Tests
{
    public class HealthTests
    {
        [Test]
        public void New_IsFull_AndAlive()
        {
            var h = new Health(100f);
            Assert.AreEqual(100f, h.Current, 1e-4f);
            Assert.IsFalse(h.IsDestroyed);
        }

        [Test]
        public void ApplyDamage_ReducesCurrent()
        {
            var h = new Health(100f);
            h.ApplyDamage(30f);
            Assert.AreEqual(70f, h.Current, 1e-4f);
        }

        [Test]
        public void OverDamage_ClampsToZero_AndDestroys()
        {
            var h = new Health(100f);
            h.ApplyDamage(150f);
            Assert.AreEqual(0f, h.Current, 1e-4f);
            Assert.IsTrue(h.IsDestroyed);
        }

        [Test]
        public void Heal_ClampsToMax_AndNotWhenDestroyed()
        {
            var h = new Health(100f);
            h.ApplyDamage(40f);
            h.Heal(1000f);
            Assert.AreEqual(100f, h.Current, 1e-4f);

            h.ApplyDamage(1000f);
            h.Heal(50f);
            Assert.IsTrue(h.IsDestroyed);
        }

        [Test]
        public void SetMax_OnFullPool_RaisesBothMaxAndCurrent()
        {
            var h = new Health(100f);
            h.SetMax(120f);
            Assert.AreEqual(120f, h.Max, 1e-4f);
            Assert.AreEqual(120f, h.Current, 1e-4f);
            Assert.IsFalse(h.IsDestroyed);
        }

        [Test]
        public void SetMax_KeepsRatio_WhenRaising()
        {
            var h = new Health(100f);
            h.ApplyDamage(50f);          // 50 / 100 = 50 %
            h.SetMax(200f);
            Assert.AreEqual(200f, h.Max, 1e-4f);
            Assert.AreEqual(100f, h.Current, 1e-4f);
        }

        [Test]
        public void SetMax_KeepsRatio_WhenLowering()
        {
            var h = new Health(100f);
            h.ApplyDamage(25f);          // 75 / 100 = 75 %
            h.SetMax(40f);
            Assert.AreEqual(40f, h.Max, 1e-4f);
            Assert.AreEqual(30f, h.Current, 1e-4f);
            Assert.IsFalse(h.IsDestroyed);
        }

        [Test]
        public void SetMax_NeverExceedsMax_AndNeverGoesNegative()
        {
            var h = new Health(100f);
            h.SetMax(10f);
            Assert.LessOrEqual(h.Current, h.Max);
            Assert.GreaterOrEqual(h.Current, 0f);
        }

        [Test]
        public void SetMax_DoesNotResurrectADestroyedPool()
        {
            var h = new Health(100f);
            h.ApplyDamage(500f);
            Assert.IsTrue(h.IsDestroyed);

            h.SetMax(500f);
            Assert.AreEqual(500f, h.Max, 1e-4f);
            Assert.AreEqual(0f, h.Current, 1e-4f);
            Assert.IsTrue(h.IsDestroyed);
        }

        [Test]
        public void SetMax_ClampsNonPositiveToOne_LikeConstructor()
        {
            var h = new Health(100f);
            h.SetMax(0f);
            Assert.AreEqual(1f, h.Max, 1e-4f);
            Assert.AreEqual(1f, h.Current, 1e-4f);

            var g = new Health(100f);
            g.SetMax(-50f);
            Assert.AreEqual(1f, g.Max, 1e-4f);
        }

        [Test]
        public void SetMax_ThenDamage_UsesTheNewPool()
        {
            var h = new Health(100f);
            h.SetMax(120f);
            h.ApplyDamage(110f);
            Assert.AreEqual(10f, h.Current, 1e-4f);
            Assert.IsFalse(h.IsDestroyed);
        }
    }
}
