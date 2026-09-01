using NUnit.Framework;
using UnityEngine;
using Sim.Core;

namespace Sim.Tests
{
    public class TargetingSystemTests
    {
        [Test]
        public void TryDetect_TargetBeyondRange_NotDetected()
        {
            var ts = new TargetingSystem { DetectionRange = 100f, FieldOfViewDeg = 360f };
            var targets = new[] { new DetectableTarget(1, new Vector3(0, 0, 150f)) };
            Assert.IsFalse(ts.TryDetect(Vector3.zero, Vector3.forward, targets, out _));
        }

        [Test]
        public void TryDetect_TargetOutsideFov_NotDetected()
        {
            var ts = new TargetingSystem { DetectionRange = 100f, FieldOfViewDeg = 60f };
            var targets = new[] { new DetectableTarget(1, new Vector3(0, 0, -10f)) }; // directly behind
            Assert.IsFalse(ts.TryDetect(Vector3.zero, Vector3.forward, targets, out _));
        }

        [Test]
        public void TryDetect_ChoosesNearestInView()
        {
            var ts = new TargetingSystem { DetectionRange = 100f, FieldOfViewDeg = 90f };
            var targets = new[]
            {
                new DetectableTarget(1, new Vector3(0, 0, 50f)),
                new DetectableTarget(2, new Vector3(0, 0, 20f)),
            };
            Assert.IsTrue(ts.TryDetect(Vector3.zero, Vector3.forward, targets, out var best));
            Assert.AreEqual(2, best.Id);
        }

        // ------------------------------------------------------------------ signature awareness

        [Test]
        public void SnapshotWithoutASignature_KeepsTheConfiguredRangeExactly()
        {
            var ts = new TargetingSystem { DetectionRange = 100f, FieldOfViewDeg = 360f };

            // Both the convenience constructor and a raw default() must behave like a plain contact.
            var built = new[] { new DetectableTarget(1, new Vector3(0f, 0f, 99f)) };
            var raw = new[] { new DetectableTarget { Id = 2, Position = new Vector3(0f, 0f, 99f) } };

            Assert.IsTrue(ts.TryDetect(Vector3.zero, Vector3.forward, built, out _));
            Assert.IsTrue(ts.TryDetect(Vector3.zero, Vector3.forward, raw, out _));
            Assert.AreEqual(100f, ts.EffectiveRangeFor(built[0]), 1e-3f);
            Assert.AreEqual(100f, ts.EffectiveRangeFor(raw[0]), 1e-3f);
        }

        [Test]
        public void ABigSignature_IsDetectedFurtherOutThanASmallOne()
        {
            var ts = new TargetingSystem { DetectionRange = 100f, FieldOfViewDeg = 360f };
            var at130 = new Vector3(0f, 0f, 130f);

            var big = new[] { new DetectableTarget(1, at130, Vector3.zero, 4f, 0f) };
            var baseline = new[] { new DetectableTarget(2, at130, Vector3.zero, 1f, 0f) };
            var small = new[] { new DetectableTarget(3, at130, Vector3.zero, 0.25f, 0f) };

            Assert.IsTrue(ts.TryDetect(Vector3.zero, Vector3.forward, big, out _));
            Assert.IsFalse(ts.TryDetect(Vector3.zero, Vector3.forward, baseline, out _));
            Assert.IsFalse(ts.TryDetect(Vector3.zero, Vector3.forward, small, out _));

            // ...and the ordering of the reaches themselves is the same.
            Assert.Greater(ts.EffectiveRangeFor(big[0]), ts.EffectiveRangeFor(baseline[0]));
            Assert.Greater(ts.EffectiveRangeFor(baseline[0]), ts.EffectiveRangeFor(small[0]));
        }

        [Test]
        public void ASmallSignatureOutsideItsReach_LosesToABigOneFurtherAway()
        {
            // The nearest contact is not automatically the detected one any more: a stealthy target
            // at 80 m is invisible while a big one at 120 m is not.
            var ts = new TargetingSystem { DetectionRange = 100f, FieldOfViewDeg = 360f };
            var targets = new[]
            {
                new DetectableTarget(1, new Vector3(0f, 0f, 80f), Vector3.zero, 0.02f, 0f),
                new DetectableTarget(2, new Vector3(0f, 0f, 120f), Vector3.zero, 4f, 0f),
            };

            Assert.IsTrue(ts.TryDetect(Vector3.zero, Vector3.forward, targets, out var best));
            Assert.AreEqual(2, best.Id);
        }

        [Test]
        public void Jamming_PullsTheDetectionRangeIn()
        {
            var ts = new TargetingSystem { DetectionRange = 100f, FieldOfViewDeg = 360f };
            var at90 = new Vector3(0f, 0f, 90f);

            var clean = new[] { new DetectableTarget(1, at90, Vector3.zero, 1f, 0f) };
            var jamming = new[] { new DetectableTarget(1, at90, Vector3.zero, 1f, 15f) };

            Assert.IsTrue(ts.TryDetect(Vector3.zero, Vector3.forward, clean, out _));
            Assert.IsFalse(ts.TryDetect(Vector3.zero, Vector3.forward, jamming, out _));
            Assert.Less(ts.EffectiveRangeFor(jamming[0]), ts.EffectiveRangeFor(clean[0]));
        }

        [Test]
        public void OutsideTheFov_ABigSignatureIsStillInvisible()
        {
            var ts = new TargetingSystem { DetectionRange = 100f, FieldOfViewDeg = 60f };
            var behind = new[] { new DetectableTarget(1, new Vector3(0f, 0f, -10f), Vector3.zero, 500f, 0f) };
            Assert.IsFalse(ts.TryDetect(Vector3.zero, Vector3.forward, behind, out _));
        }

        [Test]
        public void UpdateLock_BuildsLockOverTime_OnSameTarget()
        {
            var ts = new TargetingSystem { LockTimeSeconds = 1.5f };
            ts.UpdateLock(true, 1, 0.5f);
            Assert.IsFalse(ts.IsLocked);
            ts.UpdateLock(true, 1, 0.5f);
            ts.UpdateLock(true, 1, 0.5f);
            Assert.IsTrue(ts.IsLocked);
        }

        [Test]
        public void UpdateLock_LosingTarget_ResetsLock()
        {
            var ts = new TargetingSystem { LockTimeSeconds = 1.0f };
            ts.UpdateLock(true, 1, 1.0f);
            Assert.IsTrue(ts.IsLocked);
            ts.UpdateLock(false, -1, 0.5f);
            Assert.IsFalse(ts.IsLocked);
            Assert.AreEqual(0f, ts.LockProgress, 1e-4f);
        }

        [Test]
        public void UpdateLock_SwitchingTarget_RestartsProgress()
        {
            var ts = new TargetingSystem { LockTimeSeconds = 1.5f };
            ts.UpdateLock(true, 1, 1.0f);
            ts.UpdateLock(true, 2, 0.5f);   // switched
            Assert.AreEqual(0.5f, ts.LockProgress, 1e-4f);
            Assert.AreEqual(2, ts.DetectedId);
        }
    }
}
