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
    }
}
