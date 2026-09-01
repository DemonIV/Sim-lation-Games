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

        [Test]
        public void BreakTurn_PutsTheThreatOnTheBeam()
        {
            // A break turn must be (near) perpendicular to the threat bearing — that is the geometry
            // that maximises the line-of-sight rate a PN missile has to null.
            Vector3 threat = Quaternion.Euler(0f, 25f, 0f) * Vector3.forward;
            Vector3 d = EvasiveManeuver.Direction(ManeuverType.BreakTurn, Vector3.forward, threat, Vector3.up);

            Assert.AreEqual(90f, Vector3.Angle(d, threat), 0.5f);
            Assert.GreaterOrEqual(Vector3.Dot(d, Vector3.forward), -1e-3f);   // never reverse into it
        }

        [Test]
        public void DiveAndClimb_KeepTheBeamHeading_Horizontally()
        {
            Vector3 threat = Vector3.forward;
            Vector3 dive = EvasiveManeuver.Direction(ManeuverType.Dive, Vector3.forward, threat, Vector3.up);
            Vector3 climb = EvasiveManeuver.Direction(ManeuverType.Climb, Vector3.forward, threat, Vector3.up);

            // The horizontal part of a dive/climb is still the beam heading (zero along the threat).
            Assert.AreEqual(0f, Vector3.Dot(new Vector3(dive.x, 0f, dive.z), threat), 1e-3f);
            Assert.AreEqual(0f, Vector3.Dot(new Vector3(climb.x, 0f, climb.z), threat), 1e-3f);
        }

        [Test]
        public void BreakWindow_CoversTheLateShotOnly()
        {
            Assert.IsTrue(EvasiveManeuver.InBreakWindow(1.5f));
            Assert.IsTrue(EvasiveManeuver.InBreakWindow(EvasiveManeuver.BreakWindowSeconds));
            Assert.IsFalse(EvasiveManeuver.InBreakWindow(EvasiveManeuver.BreakWindowSeconds + 0.1f));
            Assert.IsFalse(EvasiveManeuver.InBreakWindow(0f));
            Assert.IsFalse(EvasiveManeuver.InBreakWindow(float.PositiveInfinity));
        }

        [Test]
        public void BreakWindow_IsInsideTheWarningWindow()
        {
            // Choose() must already be commanding a manoeuvre by the time breaking starts to work.
            Assert.Less(EvasiveManeuver.BreakWindowSeconds, EvasiveManeuver.MaxWarningSeconds);
            Assert.AreEqual(ManeuverType.None,
                            EvasiveManeuver.Choose(15f, 5f, EvasiveManeuver.MaxWarningSeconds + 0.1f));
            Assert.AreNotEqual(ManeuverType.None,
                               EvasiveManeuver.Choose(15f, 5f, EvasiveManeuver.BreakWindowSeconds));
        }
    }
}
