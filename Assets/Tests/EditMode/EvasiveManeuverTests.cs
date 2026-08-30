using NUnit.Framework;
using UnityEngine;
using Sim.Core;

namespace Sim.Tests
{
    public class EvasiveManeuverTests
    {
        [Test]
        public void Directions_AreUnitVectors()
        {
            Assert.AreEqual(1f, EvasiveManeuver.Direction(ManeuverType.BreakTurn, Vector3.forward, Vector3.forward, Vector3.up).magnitude, 1e-3f);
            Assert.AreEqual(1f, EvasiveManeuver.Direction(ManeuverType.Dive, Vector3.forward, Vector3.forward, Vector3.up).magnitude, 1e-3f);
            Assert.AreEqual(1f, EvasiveManeuver.Direction(ManeuverType.Climb, Vector3.forward, Vector3.forward, Vector3.up).magnitude, 1e-3f);
            Assert.AreEqual(1f, EvasiveManeuver.Direction(ManeuverType.Barrel, Vector3.forward, Vector3.forward, Vector3.up).magnitude, 1e-3f);
        }

        [Test]
        public void Dive_GoesDown_Climb_GoesUp()
        {
            var dive = EvasiveManeuver.Direction(ManeuverType.Dive, Vector3.forward, Vector3.forward, Vector3.up);
            var climb = EvasiveManeuver.Direction(ManeuverType.Climb, Vector3.forward, Vector3.forward, Vector3.up);
            Assert.Less(dive.y, 0f);
            Assert.Greater(climb.y, 0f);
        }

        [Test]
        public void None_KeepsCurrentHeading()
        {
            var d = EvasiveManeuver.Direction(ManeuverType.None, Vector3.forward, Vector3.right, Vector3.up);
            Assert.AreEqual(Vector3.forward, d);
        }

        [Test]
        public void Choose_RespectsWarningTimeAndAltitude()
        {
            Assert.AreEqual(ManeuverType.None, EvasiveManeuver.Choose(50f, 5f, 10f));   // far off
            Assert.AreEqual(ManeuverType.Dive, EvasiveManeuver.Choose(100f, 5f, 3f));   // high
            Assert.AreEqual(ManeuverType.Climb, EvasiveManeuver.Choose(8f, 5f, 3f));    // low
            Assert.AreEqual(ManeuverType.BreakTurn, EvasiveManeuver.Choose(15f, 5f, 3f));
        }
    }
}
