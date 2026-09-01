using NUnit.Framework;
using UnityEngine;
using Sim.Core;

namespace Sim.Tests
{
    public class MissileAgilityTests
    {
        [Test]
        public void TurnRate_IsLoadFactorOverSpeed()
        {
            // 6 g at 85 m/s -> 6 * 9.81 / 85 rad/s.
            Assert.AreEqual(6f * 9.81f / 85f, MissileAgility.MaxTurnRateRad(6f, 85f), 1e-4f);
        }

        [Test]
        public void FasterMissile_TurnsSlower()
        {
            float slow = MissileAgility.MaxTurnRateRad(8f, 80f);
            float fast = MissileAgility.MaxTurnRateRad(8f, 200f);
            Assert.Less(fast, slow);
        }

        [Test]
        public void MoreG_TurnsFaster()
        {
            float soft = MissileAgility.MaxTurnRateRad(4f, 90f);
            float hard = MissileAgility.MaxTurnRateRad(12f, 90f);
            Assert.Greater(hard, soft);
        }

        [Test]
        public void DegenerateInputs_GiveNoTurnAuthority_AndNeverDivideByZero()
        {
            Assert.AreEqual(0f, MissileAgility.MaxTurnRateRad(6f, 0f), 1e-6f);
            Assert.AreEqual(0f, MissileAgility.MaxTurnRateRad(6f, -50f), 1e-6f);
            Assert.AreEqual(0f, MissileAgility.MaxTurnRateRad(0f, 85f), 1e-6f);
            Assert.AreEqual(0f, MissileAgility.MaxTurnRateRad(-3f, 85f), 1e-6f);
        }

        [Test]
        public void DegreeForm_MatchesRadianForm()
        {
            Assert.AreEqual(MissileAgility.MaxTurnRateRad(9f, 95f) * Mathf.Rad2Deg,
                            MissileAgility.MaxTurnRateDeg(9f, 95f), 1e-4f);
        }

        [Test]
        public void TurnRadius_IsSpeedOverTurnRate()
        {
            float r = MissileAgility.TurnRadius(6f, 85f);
            Assert.AreEqual(85f / MissileAgility.MaxTurnRateRad(6f, 85f), r, 1e-2f);
            Assert.IsTrue(float.IsPositiveInfinity(MissileAgility.TurnRadius(0f, 85f)));
        }

        [Test]
        public void SmallCommandedTurn_PassesThroughUnchanged()
        {
            // Wants 10deg, allowed 60deg this step.
            Vector3 desired = Quaternion.Euler(0f, 10f, 0f) * Vector3.forward;
            Vector3 result = MissileAgility.ClampTurn(Vector3.forward, desired, 60f * Mathf.Deg2Rad, 1f);
            Assert.AreEqual(10f, Vector3.Angle(Vector3.forward, result), 1e-2f);
            Assert.AreEqual(0f, Vector3.Angle(desired, result), 1e-2f);
        }

        [Test]
        public void LargeCommandedTurn_IsClampedToExactlyTheStepLimit()
        {
            // Wants 90deg, allowed 30deg/s over 0.5s = 15deg.
            Vector3 result = MissileAgility.ClampTurn(Vector3.forward, Vector3.right,
                                                      30f * Mathf.Deg2Rad, 0.5f);
            Assert.AreEqual(15f, Vector3.Angle(Vector3.forward, result), 1e-2f);
        }

        [Test]
        public void ClampedResult_IsAlwaysNormalised()
        {
            Vector3 a = MissileAgility.ClampTurn(Vector3.forward * 9f, Vector3.right * 4f,
                                                 30f * Mathf.Deg2Rad, 0.5f);
            Vector3 b = MissileAgility.ClampTurn(Vector3.forward * 9f, Vector3.right * 4f,
                                                 300f * Mathf.Deg2Rad, 1f);
            Assert.AreEqual(1f, a.magnitude, 1e-3f);
            Assert.AreEqual(1f, b.magnitude, 1e-3f);
        }

        [Test]
        public void NoAuthorityOrNoTime_HoldsTheCurrentHeading()
        {
            Assert.AreEqual(0f, Vector3.Angle(Vector3.forward,
                MissileAgility.ClampTurn(Vector3.forward, Vector3.right, 0f, 1f)), 1e-3f);
            Assert.AreEqual(0f, Vector3.Angle(Vector3.forward,
                MissileAgility.ClampTurn(Vector3.forward, Vector3.right, 5f, 0f)), 1e-3f);
        }

        [Test]
        public void DegenerateDirections_FallBackToTheCurrentHeading()
        {
            Vector3 kept = MissileAgility.ClampTurn(Vector3.right, Vector3.zero, 5f, 0.1f);
            Assert.AreEqual(0f, Vector3.Angle(Vector3.right, kept), 1e-3f);

            // No current heading at all: a defined unit vector, never NaN.
            Vector3 fallback = MissileAgility.ClampTurn(Vector3.zero, Vector3.zero, 5f, 0.1f);
            Assert.AreEqual(1f, fallback.magnitude, 1e-3f);
        }
    }
}
