using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Sim.Core;

namespace Sim.Tests
{
    public class WaypointNavigatorTests
    {
        [Test]
        public void EmptyRoute_IsCompleteImmediately()
        {
            var nav = new WaypointNavigator(new List<Vector3>());
            Assert.IsTrue(nav.IsComplete);
            Assert.AreEqual(Vector3.zero, nav.DesiredDirection(Vector3.zero));
        }

        [Test]
        public void DesiredDirection_PointsToCurrentWaypoint()
        {
            var nav = new WaypointNavigator(new[] { new Vector3(0, 0, 10f) }, 5f);
            Assert.AreEqual(Vector3.forward, nav.DesiredDirection(Vector3.zero));
        }

        [Test]
        public void Update_AdvancesWhenWithinArrivalRadius()
        {
            var nav = new WaypointNavigator(new[] { new Vector3(0, 0, 10f), new Vector3(0, 0, 20f) }, 5f);
            Assert.IsFalse(nav.Update(Vector3.zero));       // 10m away, not reached
            Assert.AreEqual(0, nav.CurrentIndex);
            Assert.IsTrue(nav.Update(new Vector3(0, 0, 8f))); // within 5m of first
            Assert.AreEqual(1, nav.CurrentIndex);
        }

        [Test]
        public void Update_CompletesAtLastWaypoint_WhenNotLooping()
        {
            var nav = new WaypointNavigator(new[] { new Vector3(0, 0, 10f) }, 5f);
            nav.Update(new Vector3(0, 0, 8f));
            Assert.IsTrue(nav.IsComplete);
        }

        [Test]
        public void Update_LoopsBackToStart_WhenLooping()
        {
            var nav = new WaypointNavigator(new[] { new Vector3(0, 0, 10f), new Vector3(0, 0, 20f) }, 5f, loop: true);
            nav.Update(new Vector3(0, 0, 10f));  // reach wp0 -> index 1
            nav.Update(new Vector3(0, 0, 20f));  // reach wp1 (last) -> loop to 0
            Assert.AreEqual(0, nav.CurrentIndex);
            Assert.IsFalse(nav.IsComplete);
        }
    }
}
