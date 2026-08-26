using NUnit.Framework;
using UnityEngine;
using Sim.Core;

namespace Sim.Tests
{
    public class RadarCrossSectionTests
    {
        [Test]
        public void NoseOn_ReturnsFrontalRcs()
        {
            var rcs = new RadarCrossSection { FrontalRcs = 0.1f, BroadsideRcs = 5f };
            // target forward parallel to LOS -> nose/tail on
            Assert.AreEqual(0.1f, rcs.ValueForAspect(Vector3.forward, Vector3.forward), 1e-3f);
        }

        [Test]
        public void Broadside_ReturnsBroadsideRcs()
        {
            var rcs = new RadarCrossSection { FrontalRcs = 0.1f, BroadsideRcs = 5f };
            Assert.AreEqual(5f, rcs.ValueForAspect(Vector3.forward, Vector3.right), 1e-3f);
        }
    }
}
