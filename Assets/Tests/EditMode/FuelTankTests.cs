using NUnit.Framework;
using Sim.Core;

namespace Sim.Tests
{
    public class FuelTankTests
    {
        [Test]
        public void New_IsFull()
        {
            var f = new FuelTank(100f, 10f);
            Assert.AreEqual(100f, f.Current, 1e-4f);
            Assert.AreEqual(1f, f.Fraction, 1e-4f);
            Assert.IsFalse(f.IsEmpty);
        }

        [Test]
        public void Consume_BurnsProportionalToThrottleAndTime()
        {
            var f = new FuelTank(100f, 10f);
            f.Consume(1f, 1f);
            Assert.AreEqual(90f, f.Current, 1e-4f);
            f.Consume(0.5f, 2f);   // 10 * 0.5 * 2 = 10
            Assert.AreEqual(80f, f.Current, 1e-4f);
        }

        [Test]
        public void Consume_ClampsAtEmpty()
        {
            var f = new FuelTank(100f, 10f);
            f.Consume(1f, 100f);
            Assert.AreEqual(0f, f.Current, 1e-4f);
            Assert.IsTrue(f.IsEmpty);
        }

        [Test]
        public void Refuel_RestoresToCapacity()
        {
            var f = new FuelTank(100f, 10f);
            f.Consume(1f, 5f);
            f.Refuel();
            Assert.AreEqual(100f, f.Current, 1e-4f);
        }
    }
}
