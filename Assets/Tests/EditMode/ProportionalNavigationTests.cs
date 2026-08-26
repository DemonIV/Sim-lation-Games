using NUnit.Framework;
using UnityEngine;
using Sim.Core;

namespace Sim.Tests
{
    public class ProportionalNavigationTests
    {
        [Test]
        public void OffsetTarget_CommandsLateralAccelTowardTarget()
        {
            // Pursuer at origin moving +x; target above and ahead, stationary.
            Vector3 R = new Vector3(1000, 50, 0);   // target - pursuer
            Vector3 Vr = new Vector3(-100, 0, 0);   // targetVel - pursuerVel
            Vector3 a = ProportionalNavigation.Acceleration(R, Vr, 3f);
            Assert.AreEqual(0f, a.x, 1e-3f);
            Assert.AreEqual(1.4963f, a.y, 0.01f);   // toward target's +y offset
            Assert.AreEqual(0f, a.z, 1e-3f);
        }

        [Test]
        public void CollisionCourse_CommandsNoAcceleration()
        {
            Vector3 R = new Vector3(1000, 0, 0);
            Vector3 Vr = new Vector3(-100, 0, 0);   // no LOS rotation
            Vector3 a = ProportionalNavigation.Acceleration(R, Vr, 3f);
            Assert.Less(a.magnitude, 1e-4f);
        }

        [Test]
        public void ClosingVelocity_PositiveWhenClosing_NegativeWhenOpening()
        {
            Vector3 R = new Vector3(1000, 0, 0);
            Assert.AreEqual(100f, ProportionalNavigation.ClosingVelocity(R, new Vector3(-100, 0, 0)), 1e-3f);
            Assert.AreEqual(-100f, ProportionalNavigation.ClosingVelocity(R, new Vector3(100, 0, 0)), 1e-3f);
        }

        [Test]
        public void GuidanceLoop_InterceptsStationaryTarget()
        {
            float speed = 150f;
            Vector3 m = Vector3.zero;
            Vector3 vm = new Vector3(speed, 0, 0);
            Vector3 target = new Vector3(1500, 150, 0);
            Vector3 tv = Vector3.zero;
            float dt = 0.02f;
            float minDist = float.MaxValue;
            for (int i = 0; i < 1500; i++)
            {
                Vector3 R = target - m;
                Vector3 Vr = tv - vm;
                Vector3 a = ProportionalNavigation.Acceleration(R, Vr, 4f);
                vm += a * dt;
                vm = vm.normalized * speed;   // constant-speed missile
                m += vm * dt;
                target += tv * dt;
                float d = Vector3.Distance(m, target);
                if (d < minDist) minDist = d;
                if (d < 5f) break;
            }
            Assert.Less(minDist, 20f);
        }
    }
}
