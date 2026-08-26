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
