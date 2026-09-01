using NUnit.Framework;
using UnityEngine;
using Sim.Core;

namespace Sim.Tests
{
    public class EvasionSteeringTests
    {
        [Test]
        public void Evade_ReturnsUnitVector()
        {
            var e = EvasionSteering.Evade(Vector3.forward, Vector3.forward, Vector3.up);
            Assert.AreEqual(1f, e.magnitude, 1e-3f);
        }

        [Test]
        public void Evade_MovesAwayFromThreatAhead_WithLateralJink()
        {
            // Threat straight ahead; evade should gain distance (negative dot with threat) and jink sideways.
            var e = EvasionSteering.Evade(Vector3.forward, Vector3.forward, Vector3.up);
            Assert.Less(Vector3.Dot(e, Vector3.forward), 0f);
            Assert.Greater(Mathf.Abs(e.x), 0.5f);
        }

        [Test]
        public void BreakTurn_IsPerpendicularToTheThreatBearing()
        {
            // Threat 30 degrees off the nose: the break must put it exactly on the beam.
            Vector3 threat = Quaternion.Euler(0f, 30f, 0f) * Vector3.forward;
            Vector3 b = EvasionSteering.BreakTurn(Vector3.forward, threat, Vector3.up);

            Assert.AreEqual(1f, b.magnitude, 1e-3f);
            Assert.AreEqual(0f, Vector3.Dot(b, threat.normalized), 1e-3f);
        }

        [Test]
        public void BreakTurn_PicksTheBeamSideNearerTheCurrentHeading()
        {
            // Threat straight ahead: both beams are 90 degrees away, so either is admissible, but the
            // manoeuvre must never reverse INTO the missile.
            Vector3 b = EvasionSteering.BreakTurn(Vector3.forward, Vector3.forward, Vector3.up);
            Assert.GreaterOrEqual(Vector3.Dot(b, Vector3.forward), -1e-3f);

            // Threat on the left while we are already turning right: keep turning right.
            Vector3 threat = Vector3.left;
            Vector3 heading = (Vector3.forward + Vector3.right * 0.4f).normalized;
            Vector3 r = EvasionSteering.BreakTurn(heading, threat, Vector3.up);
            Assert.Greater(Vector3.Dot(r, heading), 0f);
            Assert.AreEqual(0f, Vector3.Dot(r, threat.normalized), 1e-3f);
        }

        [Test]
        public void BreakTurn_IsUnitLength_EvenForAVerticalThreat()
        {
            Vector3 b = EvasionSteering.BreakTurn(Vector3.forward, Vector3.up, Vector3.up);
            Assert.AreEqual(1f, b.magnitude, 1e-3f);
        }

        [Test]
        public void BreakTurn_HoldsMoreLineOfSightRateThanEvade()
        {
            // Evade blends in a run-away component, which LOWERS the crossing (beam) component the
            // guidance law has to chase. The break turn is pure beam, so it must cross harder.
            Vector3 threat = Vector3.forward;
            Vector3 beam = EvasionSteering.BreakTurn(Vector3.forward, threat, Vector3.up);
            Vector3 evade = EvasionSteering.Evade(Vector3.forward, threat, Vector3.up);

            float beamCrossing = Vector3.ProjectOnPlane(beam, threat.normalized).magnitude;
            float evadeCrossing = Vector3.ProjectOnPlane(evade, threat.normalized).magnitude;
            Assert.Greater(beamCrossing, evadeCrossing);
        }
    }
}
