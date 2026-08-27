using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Sim.Core;

namespace Sim.Tests
{
    public class RadarScanTests
    {
        // ReferenceRange 100 against RCS 1; beam half-angle 60 deg.
        private static RadarSystem Radar()
        {
            return new RadarSystem { ReferenceRange = 100f, ReferenceRcs = 1f, BeamWidthDeg = 120f };
        }

        [Test]
        public void EffectiveDetectionRange_AppliesRangeEquationThenJamming()
        {
            // RCS 16 -> 100 * 16^0.25 = 200 m; jamming 15 -> 200 / 16^0.25 = 100 m.
            var radar = Radar();
            Assert.AreEqual(200f, RadarScan.EffectiveDetectionRange(radar, new RadarScanTarget(1, Vector3.zero, 16f)), 1e-2f);
            Assert.AreEqual(100f, RadarScan.EffectiveDetectionRange(radar, new RadarScanTarget(1, Vector3.zero, 16f, 15f)), 1e-2f);
        }

        [Test]
        public void FindNearest_PicksTheClosestDetectableTarget()
        {
            var targets = new List<RadarScanTarget>
            {
                new RadarScanTarget(7, new Vector3(0f, 0f, 80f), 1f),
                new RadarScanTarget(3, new Vector3(0f, 0f, 50f), 1f),
            };

            Assert.IsTrue(RadarScan.FindNearest(Radar(), Vector3.zero, Vector3.forward, targets, out RadarContact c));
            Assert.AreEqual(3, c.Id);
            Assert.AreEqual(50f, c.Range, 1e-2f);
        }

        [Test]
        public void Jamming_CanPushATargetOutOfDetectionRange()
        {
            // 150 m away with RCS 16: inside the 200 m clean range, outside the 100 m jammed range.
            var clean = new List<RadarScanTarget> { new RadarScanTarget(1, new Vector3(0f, 0f, 150f), 16f) };
            var jammed = new List<RadarScanTarget> { new RadarScanTarget(1, new Vector3(0f, 0f, 150f), 16f, 15f) };

            Assert.IsTrue(RadarScan.FindNearest(Radar(), Vector3.zero, Vector3.forward, clean, out _));
            Assert.IsFalse(RadarScan.FindNearest(Radar(), Vector3.zero, Vector3.forward, jammed, out _));
        }

        [Test]
        public void LowRcs_ShortensDetectionRange()
        {
            // Same 150 m target: bright broadside return is seen, dim nose-on return is not.
            var bright = new List<RadarScanTarget> { new RadarScanTarget(1, new Vector3(0f, 0f, 150f), 16f) };
            var dim = new List<RadarScanTarget> { new RadarScanTarget(1, new Vector3(0f, 0f, 150f), 1f) };

            Assert.IsTrue(RadarScan.FindNearest(Radar(), Vector3.zero, Vector3.forward, bright, out _));
            Assert.IsFalse(RadarScan.FindNearest(Radar(), Vector3.zero, Vector3.forward, dim, out _));
        }

        [Test]
        public void TargetOutsideTheBeam_IsNotDetected()
        {
            // 90 deg off boresight, well inside range: rejected by the beam limit alone.
            var targets = new List<RadarScanTarget> { new RadarScanTarget(1, new Vector3(100f, 0f, 0f), 16f) };
            Assert.IsFalse(RadarScan.FindNearest(Radar(), Vector3.zero, Vector3.forward, targets, out _));
        }

        [Test]
        public void NoCandidates_ReportsNoContact()
        {
            var empty = new List<RadarScanTarget>();
            Assert.IsFalse(RadarScan.FindNearest(Radar(), Vector3.zero, Vector3.forward, empty, out RadarContact c));
            Assert.AreEqual(0, c.Id);
        }
    }
}
