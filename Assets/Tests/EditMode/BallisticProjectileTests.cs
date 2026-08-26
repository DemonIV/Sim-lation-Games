using NUnit.Framework;
using UnityEngine;
using Sim.Core;

namespace Sim.Tests
{
    public class BallisticProjectileTests
    {
        [Test]
        public void NoDrag_GravityOnly_MatchesSemiImplicitEuler()
        {
            var p = new BallisticProjectile { DragCoefficient = 0f };
            var s = new BallisticState(new Vector3(0, 1000, 0), new Vector3(100, 0, 0));
            s = p.Step(s, 0.1f);
            Assert.AreEqual(100f, s.Velocity.x, 1e-3f);          // no horizontal force
            Assert.AreEqual(-0.981f, s.Velocity.y, 1e-3f);       // v - g*dt
            Assert.AreEqual(10f, s.Position.x, 1e-2f);
            Assert.AreEqual(999.9019f, s.Position.y, 1e-2f);
        }

        [Test]
        public void Drag_DeceleratesHorizontalMotion()
        {
            var p = new BallisticProjectile { Gravity = Vector3.zero }; // isolate drag
            var s = new BallisticState(Vector3.zero, new Vector3(200, 0, 0));
            s = p.Step(s, 0.1f);
            Assert.Less(s.Velocity.x, 200f);
            Assert.AreEqual(199.412f, s.Velocity.x, 0.05f);
        }

        [Test]
        public void Wind_PushesStationaryBodyDownwind()
        {
            var p = new BallisticProjectile { Gravity = Vector3.zero, Wind = new Vector3(10, 0, 0) };
            var s = new BallisticState(Vector3.zero, Vector3.zero);
            s = p.Step(s, 0.1f);
            Assert.Greater(s.Velocity.x, 0f);   // dragged toward the wind direction
        }

        [Test]
        public void HigherAltitude_MeansLessDrag()
        {
            var p = new BallisticProjectile { Gravity = Vector3.zero };
            var sea = new BallisticState(new Vector3(0, 0, 0), new Vector3(200, 0, 0));
            var high = new BallisticState(new Vector3(0, 15000, 0), new Vector3(200, 0, 0));
            float aSea = Mathf.Abs(p.Acceleration(sea).x);
            float aHigh = Mathf.Abs(p.Acceleration(high).x);
            Assert.Less(aHigh, aSea);
        }
    }
}
