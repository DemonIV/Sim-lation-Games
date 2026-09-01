using NUnit.Framework;
using UnityEngine;
using Sim.Core;

namespace Sim.Tests
{
    public class RadarScopeTests
    {
        private const float Range = 100f;

        [Test]
        public void TargetDeadAhead_ProjectsUpTheScope()
        {
            bool ok = RadarScope.TryProject(Vector3.zero, Vector3.forward, new Vector3(0f, 0f, 50f),
                                            Range, out Vector2 p);
            Assert.IsTrue(ok);
            Assert.AreEqual(0f, p.x, 1e-4f);
            Assert.AreEqual(0.5f, p.y, 1e-4f);
        }

        [Test]
        public void TargetToTheRight_ProjectsToPositiveX()
        {
            bool ok = RadarScope.TryProject(Vector3.zero, Vector3.forward, Vector3.right * 50f,
                                            Range, out Vector2 p);
            Assert.IsTrue(ok);
            Assert.AreEqual(0.5f, p.x, 1e-4f);
            Assert.AreEqual(0f, p.y, 1e-4f);
        }

        [Test]
        public void TargetBehind_ProjectsToNegativeY()
        {
            bool ok = RadarScope.TryProject(Vector3.zero, Vector3.forward, new Vector3(0f, 0f, -50f),
                                            Range, out Vector2 p);
            Assert.IsTrue(ok);
            Assert.Less(p.y, 0f);
            Assert.AreEqual(0f, p.x, 1e-4f);
        }

        [Test]
        public void HeadingRotates_TheScopeWithIt()
        {
            // Nose points east (+X); a contact due north (+Z) must appear on the pilot's left.
            bool ok = RadarScope.TryProject(Vector3.zero, Vector3.right, new Vector3(0f, 0f, 50f),
                                            Range, out Vector2 p);
            Assert.IsTrue(ok);
            Assert.AreEqual(-0.5f, p.x, 1e-4f);
            Assert.AreEqual(0f, p.y, 1e-4f);
        }

        [Test]
        public void TargetBeyondRange_IsRejected()
        {
            bool ok = RadarScope.TryProject(Vector3.zero, Vector3.forward, new Vector3(0f, 0f, 150f),
                                            Range, out Vector2 _);
            Assert.IsFalse(ok);
        }

        [Test]
        public void TargetOnTheRangeRing_IsAccepted()
        {
            bool ok = RadarScope.TryProject(Vector3.zero, Vector3.forward, new Vector3(0f, 0f, Range),
                                            Range, out Vector2 p);
            Assert.IsTrue(ok);
            Assert.AreEqual(1f, p.y, 1e-4f);
        }

        [Test]
        public void AltitudeDifference_DoesNotChangeProjection()
        {
            RadarScope.TryProject(Vector3.zero, Vector3.forward, new Vector3(0f, 0f, 50f),
                                 Range, out Vector2 flat);
            bool ok = RadarScope.TryProject(Vector3.zero, Vector3.forward, new Vector3(0f, 40f, 50f),
                                            Range, out Vector2 high);
            Assert.IsTrue(ok);
            Assert.AreEqual(flat.x, high.x, 1e-4f);
            Assert.AreEqual(flat.y, high.y, 1e-4f);
        }

        [Test]
        public void SelfOffsetIsRelative_NotAbsolute()
        {
            bool ok = RadarScope.TryProject(new Vector3(200f, 12f, -80f), Vector3.forward,
                                            new Vector3(200f, 30f, -30f), Range, out Vector2 p);
            Assert.IsTrue(ok);
            Assert.AreEqual(0f, p.x, 1e-4f);
            Assert.AreEqual(0.5f, p.y, 1e-4f);
        }

        [Test]
        public void ZeroRange_ReturnsFalse()
        {
            Assert.IsFalse(RadarScope.TryProject(Vector3.zero, Vector3.forward, Vector3.zero, 0f, out Vector2 _));
        }

        [Test]
        public void NegativeRange_ReturnsFalse()
        {
            Assert.IsFalse(RadarScope.TryProject(Vector3.zero, Vector3.forward, new Vector3(0f, 0f, 10f),
                                                 -25f, out Vector2 _));
        }

        [Test]
        public void DegenerateForward_FallsBackToWorldNorth()
        {
            bool ok = RadarScope.TryProject(Vector3.zero, Vector3.up, new Vector3(0f, 0f, 50f),
                                            Range, out Vector2 p);
            Assert.IsTrue(ok);
            Assert.AreEqual(0f, p.x, 1e-4f);
            Assert.AreEqual(0.5f, p.y, 1e-4f);
        }
    }
}
