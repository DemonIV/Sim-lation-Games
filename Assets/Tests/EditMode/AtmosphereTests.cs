using NUnit.Framework;
using Sim.Core;

namespace Sim.Tests
{
    public class AtmosphereTests
    {
        [Test]
        public void SeaLevel_IsReferenceDensity()
        {
            Assert.AreEqual(1.225f, Atmosphere.DensityAtAltitude(0f), 1e-4f);
        }

        [Test]
        public void OneScaleHeight_IsAboutOneOverE()
        {
            // 1.225 * e^-1 ~= 0.4507
            Assert.AreEqual(0.4507f, Atmosphere.DensityAtAltitude(Atmosphere.ScaleHeight), 0.01f);
        }

        [Test]
        public void Density_DecreasesWithAltitude()
        {
            Assert.Greater(Atmosphere.DensityAtAltitude(1000f), Atmosphere.DensityAtAltitude(5000f));
        }

        [Test]
        public void NegativeAltitude_ClampsToSeaLevel()
        {
            Assert.AreEqual(Atmosphere.SeaLevelDensity, Atmosphere.DensityAtAltitude(-500f), 1e-4f);
        }
    }
}
