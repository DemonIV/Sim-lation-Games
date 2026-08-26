using NUnit.Framework;
using UnityEngine;
using Sim.Core;

namespace Sim.Tests
{
    public class SeekerGimbalTests
    {
        [Test]
        public void SlewRate_LimitsRotationPerStep()
        {
            var g = new SeekerGimbal(Vector3.forward) { MaxSlewRateDeg = 30f, MaxOffBoresightDeg = 90f };
            bool track = g.Track(Vector3.forward, Vector3.right, 1f); // wants 90deg, allowed 30
            Assert.AreEqual(30f, Vector3.Angle(Vector3.forward, g.LookDirection), 0.5f);
            Assert.IsFalse(track);
        }

        [Test]
        public void OffBoresight_IsClamped()
        {
            var g = new SeekerGimbal(Vector3.forward) { MaxSlewRateDeg = 180f, MaxOffBoresightDeg = 40f };
            bool track = g.Track(Vector3.forward, Vector3.right, 1f); // fast slew to 90deg, cone 40
            Assert.AreEqual(40f, Vector3.Angle(Vector3.forward, g.LookDirection), 0.5f);
            Assert.IsFalse(track);
        }

        [Test]
        public void AcquiresTrack_WhenLosWithinLimits()
        {
            var g = new SeekerGimbal(Vector3.forward) { MaxSlewRateDeg = 30f, MaxOffBoresightDeg = 40f };
            Vector3 los = Quaternion.Euler(0, 20, 0) * Vector3.forward; // 20deg off
            bool track = g.Track(Vector3.forward, los, 1f);
            Assert.IsTrue(track);
            Assert.AreEqual(20f, Vector3.Angle(Vector3.forward, g.LookDirection), 0.5f);
        }
    }
}
