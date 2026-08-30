using NUnit.Framework;
using Sim.Core;

namespace Sim.Tests
{
    public class HitProbabilityTests
    {
        [Test]
        public void BeyondEffectiveRange_IsZero()
        {
            Assert.AreEqual(0f, HitProbability.Compute(100f, 60f, 2f, 1f), 1e-4f);
        }

        [Test]
        public void PointBlank_IsCertain()
        {
            Assert.AreEqual(1f, HitProbability.Compute(0f, 60f, 2f, 1f), 1e-4f);
        }

        [Test]
        public void LargeTargetInsideCone_IsCertain()
        {
            // cone radius at 10m with 2deg ~ 0.35m; a 5m target fills it.
            Assert.AreEqual(1f, HitProbability.Compute(10f, 60f, 2f, 5f), 1e-4f);
        }

        [Test]
        public void FallsOffWithRange()
        {
            float near = HitProbability.Compute(10f, 60f, 2f, 0.2f);
            float far = HitProbability.Compute(50f, 60f, 2f, 0.2f);
            Assert.Greater(near, far);
            Assert.AreEqual(0.0131f, far, 0.003f);
        }

        [Test]
        public void ZeroDispersion_IsCertain()
        {
            Assert.AreEqual(1f, HitProbability.Compute(30f, 60f, 0f, 0.2f), 1e-4f);
        }
    }
}
