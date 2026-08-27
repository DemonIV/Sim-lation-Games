using NUnit.Framework;
using UnityEngine;
using Sim.Core;

namespace Sim.Tests
{
    public class MunitionAutopilotTests
    {
        private static MunitionAutopilot Autopilot()
        {
            return new MunitionAutopilot
            {
                CruiseSpeed = 180f,
                NavGain = 4f,
                ThrustGain = 2f,
                MaxLateralAcceleration = 200f
            };
        }

        [Test]
        public void ThrustCommand_IsZeroAtCruiseSpeed()
        {
            var ap = Autopilot();
            Vector3 a = ap.ThrustCommand(Vector3.forward * 180f);
            Assert.AreEqual(0f, a.magnitude, 1e-3f);
        }

        [Test]
        public void ThrustCommand_ScalesWithSpeedError()
        {
            // Gain is 1/s, so the command is gain * (cruise - speed): 2 * (180 - 100) = 160 m/s^2.
            var ap = Autopilot();
            Vector3 a = ap.ThrustCommand(Vector3.forward * 100f);
            Assert.AreEqual(160f, a.magnitude, 1e-2f);
            Assert.Greater(Vector3.Dot(a, Vector3.forward), 0f, "should accelerate when below cruise");
        }

        [Test]
        public void ThrustCommand_DeceleratesAboveCruise()
        {
            var ap = Autopilot();
            Vector3 a = ap.ThrustCommand(Vector3.forward * 250f);
            Assert.Less(Vector3.Dot(a, Vector3.forward), 0f);
            Assert.AreEqual(140f, a.magnitude, 1e-2f); // 2 * (250 - 180)
        }

        [Test]
        public void ThrustCommand_IsZeroWithoutHeading()
        {
            var ap = Autopilot();
            Assert.AreEqual(0f, ap.ThrustCommand(Vector3.zero).magnitude, 1e-6f);
        }

        [Test]
        public void LateralCommand_IsZeroOnCollisionCourse()
        {
            // Closing straight down the line of sight: no LOS rotation, so no steering needed.
            var ap = Autopilot();
            Vector3 relPos = Vector3.forward * 500f;
            Vector3 relVel = Vector3.forward * -180f;
            Assert.AreEqual(0f, ap.LateralCommand(relPos, relVel).magnitude, 1e-3f);
        }

        [Test]
        public void LateralCommand_IsClampedToAirframeLimit()
        {
            // Close range with a large LOS rate produces an enormous raw PN command.
            var ap = Autopilot();
            Vector3 relPos = new Vector3(20f, 0f, 20f);
            Vector3 relVel = new Vector3(0f, 0f, -180f);
            Assert.Greater(ProportionalNavigation.Acceleration(relPos, relVel, ap.NavGain).magnitude,
                           ap.MaxLateralAcceleration,
                           "test geometry should saturate the limiter");
            Assert.AreEqual(ap.MaxLateralAcceleration, ap.LateralCommand(relPos, relVel).magnitude, 1e-2f);
        }

        [Test]
        public void Acceleration_WithoutGuidance_IsPurelyAxial()
        {
            // Seeker has lost the target: the motor still burns but nothing steers.
            var ap = Autopilot();
            Vector3 velocity = Vector3.forward * 150f;
            Vector3 relPos = new Vector3(100f, 0f, 200f);
            Vector3 relVel = -velocity;

            Vector3 guided = ap.Acceleration(relPos, relVel, velocity, true);
            Vector3 coasting = ap.Acceleration(relPos, relVel, velocity, false);

            Assert.Greater(Vector3.Cross(guided, velocity).magnitude, 1f, "guided command should steer");
            Assert.AreEqual(0f, Vector3.Cross(coasting, velocity).magnitude, 1e-2f);
            Assert.AreEqual(ap.ThrustCommand(velocity).magnitude, coasting.magnitude, 1e-3f);
        }
    }
}
