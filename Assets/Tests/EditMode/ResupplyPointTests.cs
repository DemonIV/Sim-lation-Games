using NUnit.Framework;
using Sim.Core;

namespace Sim.Tests
{
    public class ResupplyPointTests
    {
        [Test]
        public void AwayFromBase_MakesNoProgress()
        {
            var r = new ResupplyPoint(4f);
            Assert.IsFalse(r.Tick(false, 10f));
            Assert.AreEqual(0f, r.Progress, 1e-4f);
            Assert.IsFalse(r.IsServicing);
        }

        [Test]
        public void PartialDwell_ShowsProgress_ButDoesNotComplete()
        {
            var r = new ResupplyPoint(4f);
            Assert.IsFalse(r.Tick(true, 2f));
            Assert.AreEqual(0.5f, r.Progress, 1e-4f);
            Assert.IsTrue(r.IsServicing);
        }

        [Test]
        public void FullDwell_CompletesOnceThenResets()
        {
            var r = new ResupplyPoint(4f);
            r.Tick(true, 2f);
            Assert.IsTrue(r.Tick(true, 2f));      // completes
            Assert.AreEqual(0f, r.Progress, 1e-4f);
            Assert.IsFalse(r.Tick(true, 1f));     // new cycle started
        }

        [Test]
        public void LeavingEarly_ResetsProgress()
        {
            var r = new ResupplyPoint(4f);
            r.Tick(true, 3f);
            r.Tick(false, 0.1f);
            Assert.AreEqual(0f, r.Progress, 1e-4f);
        }
    }
}
