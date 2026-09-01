using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Sim.Core;

namespace Sim.Tests
{
    public class TargetAllocationTests
    {
        [Test]
        public void EachShooterGetsItsNearestDistinctTarget()
        {
            var shooters = new List<Vector3> { new Vector3(0, 0, 0), new Vector3(100, 0, 0) };
            var targets = new List<Vector3> { new Vector3(5, 0, 0), new Vector3(105, 0, 0) };
            var a = TargetAllocation.Assign(shooters, targets);
            Assert.AreEqual(0, a[0]);
            Assert.AreEqual(1, a[1]);
        }

        [Test]
        public void ContestedTargets_ResolveToDistinctAssignments()
        {
            var shooters = new List<Vector3> { new Vector3(0, 0, 0), new Vector3(1, 0, 0) };
            var targets = new List<Vector3> { new Vector3(2, 0, 0), new Vector3(50, 0, 0) };
            var a = TargetAllocation.Assign(shooters, targets);
            Assert.AreNotEqual(a[0], a[1]);
            CollectionAssert.AreEquivalent(new[] { 0, 1 }, a);
        }

        [Test]
        public void MoreShootersThanTargets_DoubleUp()
        {
            var shooters = new List<Vector3> { new Vector3(0, 0, 0), new Vector3(1, 0, 0) };
            var targets = new List<Vector3> { new Vector3(10, 0, 0) };
            var a = TargetAllocation.Assign(shooters, targets);
            Assert.AreEqual(0, a[0]);
            Assert.AreEqual(0, a[1]);
        }

        [Test]
        public void NoTargets_AllUnassigned()
        {
            var shooters = new List<Vector3> { Vector3.zero, Vector3.one };
            var targets = new List<Vector3>();
            var a = TargetAllocation.Assign(shooters, targets);
            Assert.AreEqual(-1, a[0]);
            Assert.AreEqual(-1, a[1]);
        }

        [Test]
        public void BufferOverload_MatchesAllocatingOverload()
        {
            var shooters = new List<Vector3> { new Vector3(0, 0, 0), new Vector3(1, 0, 0), new Vector3(60, 0, 0) };
            var targets = new List<Vector3> { new Vector3(2, 0, 0), new Vector3(50, 0, 0) };

            var expected = TargetAllocation.Assign(shooters, targets);

            var buffer = new List<int>();
            TargetAllocation.Assign(shooters, targets, buffer);

            Assert.AreEqual(shooters.Count, buffer.Count);
            CollectionAssert.AreEqual(expected, buffer);
        }

        [Test]
        public void BufferOverload_IsReusableAcrossCalls()
        {
            var buffer = new List<int> { 7, 7, 7, 7, 7 };   // stale content from an earlier call

            var shooters = new List<Vector3> { new Vector3(0, 0, 0), new Vector3(100, 0, 0) };
            var targets = new List<Vector3> { new Vector3(5, 0, 0), new Vector3(105, 0, 0) };
            TargetAllocation.Assign(shooters, targets, buffer);

            Assert.AreEqual(2, buffer.Count);
            Assert.AreEqual(0, buffer[0]);
            Assert.AreEqual(1, buffer[1]);

            // A second call with fewer shooters and no targets must fully reset the buffer.
            var fewer = new List<Vector3> { Vector3.zero };
            TargetAllocation.Assign(fewer, new List<Vector3>(), buffer);

            Assert.AreEqual(1, buffer.Count);
            Assert.AreEqual(-1, buffer[0]);
        }

        [Test]
        public void BufferOverload_NullBuffer_IsIgnored()
        {
            var shooters = new List<Vector3> { Vector3.zero };
            var targets = new List<Vector3> { Vector3.one };
            Assert.DoesNotThrow(() => TargetAllocation.Assign(shooters, targets, null));
        }
    }
}
