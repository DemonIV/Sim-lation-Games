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
    }
}
