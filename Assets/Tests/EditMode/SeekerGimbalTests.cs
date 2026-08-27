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

        [Test]
        public void IsWithinGimbalLimits_FollowsTheOffBoresightCone()
        {
            var g = new SeekerGimbal(Vector3.forward) { MaxOffBoresightDeg = 40f };
            Assert.IsTrue(g.IsWithinGimbalLimits(Vector3.forward, Quaternion.Euler(0, 39, 0) * Vector3.forward));
            Assert.IsFalse(g.IsWithinGimbalLimits(Vector3.forward, Quaternion.Euler(0, 41, 0) * Vector3.forward));
        }

        [Test]
        public void IsWithinGimbalLimits_IsIndependentOfSlewProgress()
        {
            // The LOS is inside the cone even though a slow seeker has not caught up to it yet,
            // so guidance stays alive on the very first step.
            var g = new SeekerGimbal(Vector3.forward) { MaxSlewRateDeg = 1f, MaxOffBoresightDeg = 40f };
            Vector3 los = Quaternion.Euler(0, 30, 0) * Vector3.forward;

            Assert.IsFalse(g.Track(Vector3.forward, los, 0.02f), "seeker cannot have slewed there yet");
            Assert.IsTrue(g.IsWithinGimbalLimits(Vector3.forward, los));
        }

        [Test]
        public void IsWithinGimbalLimits_RejectsDegenerateDirections()
        {
            var g = new SeekerGimbal(Vector3.forward) { MaxOffBoresightDeg = 40f };
            Assert.IsFalse(g.IsWithinGimbalLimits(Vector3.zero, Vector3.forward));
            Assert.IsFalse(g.IsWithinGimbalLimits(Vector3.forward, Vector3.zero));
        }

        [Test]
        public void TrackTolerance_IsConfigurable()
        {
            // A 3 deg residual counts as a track under a 5 deg tolerance but not under the 1 deg default.
            Vector3 los = Quaternion.Euler(0, 3f, 0) * Vector3.forward;

            var strict = new SeekerGimbal(Vector3.forward) { MaxSlewRateDeg = 0f, MaxOffBoresightDeg = 40f };
            Assert.IsFalse(strict.Track(Vector3.forward, los, 1f));

            var loose = new SeekerGimbal(Vector3.forward)
            {
                MaxSlewRateDeg = 0f,
                MaxOffBoresightDeg = 40f,
                TrackToleranceDeg = 5f
            };
            Assert.IsTrue(loose.Track(Vector3.forward, los, 1f));
        }
    }
}
