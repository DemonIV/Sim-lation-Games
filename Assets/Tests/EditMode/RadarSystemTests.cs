using NUnit.Framework;
using UnityEngine;
using Sim.Core;

namespace Sim.Tests
{
    public class RadarSystemTests
    {
        [Test]
        public void DetectionRange_ScalesWithFourthRootOfRcs()
        {
            var r = new RadarSystem { ReferenceRange = 100f, ReferenceRcs = 1f };
            Assert.AreEqual(100f, r.DetectionRange(1f), 1e-3f);
            Assert.AreEqual(200f, r.DetectionRange(16f), 1e-3f);   // 16^0.25 = 2
            Assert.AreEqual(50f, r.DetectionRange(0.0625f), 1e-3f); // 0.0625^0.25 = 0.5
        }

        [Test]
        public void SmallerRcs_ReducesDetectionRange()
        {
            var r = new RadarSystem { ReferenceRange = 100f, ReferenceRcs = 1f };
            Assert.Less(r.DetectionRange(0.1f), r.DetectionRange(1f));
        }

        [Test]
        public void CanDetect_InRangeAndBeam_True()
        {
            var r = new RadarSystem { ReferenceRange = 100f, BeamWidthDeg = 120f };
            Assert.IsTrue(r.CanDetect(Vector3.zero, Vector3.forward, new Vector3(0, 0, 80f), 1f));
        }

        [Test]
        public void CanDetect_BeyondRange_False()
        {
            var r = new RadarSystem { ReferenceRange = 100f, BeamWidthDeg = 120f };
            Assert.IsFalse(r.CanDetect(Vector3.zero, Vector3.forward, new Vector3(0, 0, 150f), 1f));
        }

        [Test]
        public void CanDetect_OutsideBeam_False()
        {
            var r = new RadarSystem { ReferenceRange = 100f, BeamWidthDeg = 120f }; // half = 60
            Assert.IsFalse(r.CanDetect(Vector3.zero, Vector3.forward, new Vector3(80f, 0, 0), 1f)); // 90deg off
        }

        [Test]
        public void IsWithinBeam_AcceptsInsideAndRejectsOutside()
        {
            var r = new RadarSystem { BeamWidthDeg = 120f }; // half = 60
            Assert.IsTrue(r.IsWithinBeam(Vector3.forward, Quaternion.Euler(0f, 59f, 0f) * Vector3.forward));
            Assert.IsFalse(r.IsWithinBeam(Vector3.forward, Quaternion.Euler(0f, 61f, 0f) * Vector3.forward));
        }

        [Test]
        public void IsWithinBeam_TargetOnTopOfRadar_HasNoBearing()
        {
            var r = new RadarSystem { BeamWidthDeg = 120f };
            Assert.IsTrue(r.IsWithinBeam(Vector3.forward, Vector3.zero));
        }
    }
}
