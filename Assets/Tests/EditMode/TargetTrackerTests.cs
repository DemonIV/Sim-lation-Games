using NUnit.Framework;
using UnityEngine;
using Sim.Core;

namespace Sim.Tests
{
    public class TargetTrackerTests
    {
        [Test]
        public void FirstMeasurement_InitializesToMeasurement()
        {
            var t = new TargetTracker();
            t.Update(new Vector3(5, 0, 0), 0.1f);
            Assert.IsTrue(t.Initialized);
            Assert.AreEqual(new Vector3(5, 0, 0), t.Position);
            Assert.AreEqual(Vector3.zero, t.Velocity);
        }

        [Test]
        public void StepMeasurement_IsSmoothedNotJumped()
        {
            var t = new TargetTracker { Alpha = 0.5f, Beta = 0.1f };
            t.Update(Vector3.zero, 1f);          // init at 0
            t.Update(new Vector3(10, 0, 0), 1f); // predicted 0, residual 10
            Assert.AreEqual(5f, t.Position.x, 1e-4f);   // 0 + alpha*10
            Assert.AreEqual(1f, t.Velocity.x, 1e-4f);   // 0 + (beta/dt)*10
        }

        [Test]
        public void ConstantVelocityTarget_ConvergesToTrueVelocity()
        {
            var t = new TargetTracker { Alpha = 0.5f, Beta = 0.1f };
            float dt = 0.1f;
            Vector3 truePos = Vector3.zero;
            Vector3 trueVel = new Vector3(10, 0, 0);
            for (int i = 0; i < 800; i++)
            {
                truePos += trueVel * dt;
                t.Update(truePos, dt);   // noise-free measurements
            }
            Assert.AreEqual(10f, t.Velocity.x, 0.5f);
            Assert.AreEqual(truePos.x, t.Position.x, 2f);
        }
    }
}
