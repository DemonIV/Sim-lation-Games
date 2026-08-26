using NUnit.Framework;
using UnityEngine;
using Sim.Core;

namespace Sim.Tests
{
    public class FlightModelTests
    {
        [Test]
        public void NewModel_StartsAtRest()
        {
            var m = new FlightModel(Vector3.zero, Vector3.forward);
            Assert.AreEqual(0f, m.Speed, 1e-4f);
            Assert.AreEqual(Vector3.forward, m.Forward);
        }

        [Test]
        public void Step_AcceleratesTowardThrottleSpeed_LimitedByMaxAccel()
        {
            var m = new FlightModel(Vector3.zero, Vector3.forward)
            { MaxSpeed = 50f, MaxAcceleration = 10f };
            m.Step(Vector3.forward, 1f, 1f);
            Assert.AreEqual(10f, m.Speed, 1e-3f);          // one second at 10 m/s^2
            Assert.AreEqual(new Vector3(0, 0, 10f), m.Position, "moved forward by new speed * dt");
        }

        [Test]
        public void Step_NeverExceedsMaxSpeed()
        {
            var m = new FlightModel(Vector3.zero, Vector3.forward)
            { MaxSpeed = 50f, MaxAcceleration = 10f };
            for (int i = 0; i < 100; i++) m.Step(Vector3.forward, 1f, 0.5f);
            Assert.LessOrEqual(m.Speed, 50f + 1e-3f);
            Assert.AreEqual(50f, m.Speed, 1e-3f);
        }

        [Test]
        public void Step_TurnRateIsLimited()
        {
            var m = new FlightModel(Vector3.zero, Vector3.forward) { MaxTurnRateDeg = 90f };
            m.Step(Vector3.right, 0f, 0.5f);               // want 90deg turn, allowed 45deg
            float angle = Vector3.Angle(Vector3.forward, m.Forward);
            Assert.AreEqual(45f, angle, 0.5f);
        }

        [Test]
        public void Step_ZeroDeltaTime_DoesNothing()
        {
            var m = new FlightModel(Vector3.zero, Vector3.forward);
            m.Step(Vector3.right, 1f, 0f);
            Assert.AreEqual(0f, m.Speed, 1e-4f);
            Assert.AreEqual(Vector3.zero, m.Position);
        }
    }
}
