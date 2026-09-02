using NUnit.Framework;
using UnityEngine;
using Sim.Core;

namespace Sim.Tests
{
    /// <summary>
    /// Guards the horizontal separation between the ARENA and the hostile sensor/weapon ranges — the
    /// mirror of <see cref="FlightEnvelopeTests"/>.
    ///
    /// <para>
    /// The defect these cover is a range that has quietly stopped being a range: a sensor quoted at
    /// 160 m on a field only 113 m across the diagonal detects everything, always, and the whole
    /// signature model underneath it (<see cref="SignatureDetection"/>) becomes invisible to the
    /// player. Every assertion below is a RELATIONSHIP rather than a literal wherever it can be, so
    /// re-tuning the numbers keeps the suite meaningful instead of merely making it fail.
    /// </para>
    /// </summary>
    public class ThreatEnvelopeTests
    {
        private const float JetRcs = 4f;
        private const float SihaRcs = 1f;
        private const float IhaRcs = 0.25f;

        private static float Det(float range, float rcs) => ThreatEnvelope.DetectionRangeAgainst(range, rcs);

        // ------------------------------------------------------------------ the arena itself

        [Test]
        public void Arena_MatchesTheScenarioScatterBox()
        {
            // 80 x 80 m box -> 113.1 m corner to corner.
            Assert.AreEqual(113.137f, ThreatEnvelope.FieldDiagonal, 0.01f);

            // A site on the spawn ring to the far corner of the box.
            Assert.AreEqual(96.569f, ThreatEnvelope.MaxEngagementDistance, 0.01f);

            // Hostiles live in an annulus, so the ring they sit on is inside the box.
            Assert.Greater(ThreatEnvelope.SpawnRingRadius, ThreatEnvelope.SpawnKeepOutRadius);
            Assert.LessOrEqual(ThreatEnvelope.SpawnRingRadius, ThreatEnvelope.FieldHalfExtent);
        }

        [Test]
        public void OldRanges_CoveredTheWholeField_NewOnesDoNot()
        {
            // What the project shipped before this pass: every one of them blanketed the arena.
            Assert.IsTrue(ThreatEnvelope.CoversWholeField(160f), "old SAM detection");
            Assert.IsTrue(ThreatEnvelope.CoversWholeField(130f), "old fighter detection");

            // ...and what it ships now: against the baseline SİHA, none of them do.
            Assert.IsFalse(ThreatEnvelope.CoversWholeField(ThreatEnvelope.SamDetectionRange));
            Assert.IsFalse(ThreatEnvelope.CoversWholeField(ThreatEnvelope.AaaDetectionRange));
            Assert.IsFalse(ThreatEnvelope.CoversWholeField(ThreatEnvelope.FighterDetectionRange));
        }

        // ------------------------------------------------------------------ per-hostile invariants

        [Test]
        public void EveryHostile_SeesFurtherThanItShoots()
        {
            // Otherwise the weapon can never be used at its stated range: AirDefenseSite returns early
            // on a lost contact, so fire range is clipped by detection range.
            Assert.Greater(ThreatEnvelope.SamDetectionRange, ThreatEnvelope.SamFireRange);
            Assert.Greater(ThreatEnvelope.AaaDetectionRange, ThreatEnvelope.AaaFireRange);
            Assert.Greater(ThreatEnvelope.FighterDetectionRange, ThreatEnvelope.FighterGunRange);
        }

        [Test]
        public void SamOutRangesAaa_InBothSensorAndWeapon()
        {
            Assert.Greater(ThreatEnvelope.SamDetectionRange, ThreatEnvelope.AaaDetectionRange);
            Assert.Greater(ThreatEnvelope.SamFireRange, ThreatEnvelope.AaaFireRange);
        }

        [Test]
        public void SamStillOutRangesEveryPlayerGun()
        {
            foreach (AircraftProfile p in AircraftCatalog.All)
            {
                Assert.GreaterOrEqual(ThreatEnvelope.SamFireRange, p.GunRange,
                                      $"{p.DisplayName} out-guns the SAM");
            }
        }

        [Test]
        public void AaaStaysInsideTheSihaGunRange()
        {
            // A gun run on an AAA has to remain a trade, not a free kill.
            AircraftProfile siha = AircraftCatalog.GetOrDefault(AircraftCatalog.SihaId);
            Assert.LessOrEqual(ThreatEnvelope.AaaFireRange, siha.GunRange);
        }

        // ------------------------------------------------------------------ the signature spread

        [Test]
        public void SignatureSpread_OrdersEveryHostileTheSameWay()
        {
            float[] ranges =
            {
                ThreatEnvelope.SamDetectionRange,
                ThreatEnvelope.AaaDetectionRange,
                ThreatEnvelope.FighterDetectionRange
            };

            foreach (float r in ranges)
            {
                Assert.Greater(Det(r, JetRcs), Det(r, SihaRcs));
                Assert.Greater(Det(r, SihaRcs), Det(r, IhaRcs));

                // A factor of four in signature is exactly √2 in distance (fourth-root law).
                Assert.AreEqual(r * Mathf.Sqrt(2f), Det(r, JetRcs), 1e-3f);
                Assert.AreEqual(r / Mathf.Sqrt(2f), Det(r, IhaRcs), 1e-3f);
            }
        }

        [Test]
        public void ReconIha_IsShotAtCloserThanTheStatedFireRange()
        {
            // THE payoff of a small signature: the recon İHA's detection distance falls short of the
            // weapon's reach, so its exposure envelope is genuinely smaller — not merely delayed.
            Assert.Less(Det(ThreatEnvelope.SamDetectionRange, IhaRcs), ThreatEnvelope.SamFireRange);
            Assert.Less(Det(ThreatEnvelope.AaaDetectionRange, IhaRcs), ThreatEnvelope.AaaFireRange);
            Assert.Less(Det(ThreatEnvelope.FighterDetectionRange, IhaRcs), ThreatEnvelope.FighterGunRange);
        }

        [Test]
        public void JetAndSiha_AreShotAtTheFullFireRange()
        {
            // Both are seen further out than the weapon reaches, so nothing is clipped for them.
            Assert.AreEqual(ThreatEnvelope.SamFireRange,
                            ThreatEnvelope.FireDistanceAgainst(ThreatEnvelope.SamDetectionRange,
                                                               ThreatEnvelope.SamFireRange, SihaRcs), 1e-3f);
            Assert.AreEqual(ThreatEnvelope.SamFireRange,
                            ThreatEnvelope.FireDistanceAgainst(ThreatEnvelope.SamDetectionRange,
                                                               ThreatEnvelope.SamFireRange, JetRcs), 1e-3f);
            Assert.AreEqual(ThreatEnvelope.AaaFireRange,
                            ThreatEnvelope.FireDistanceAgainst(ThreatEnvelope.AaaDetectionRange,
                                                               ThreatEnvelope.AaaFireRange, SihaRcs), 1e-3f);
        }

        [Test]
        public void ReconIha_IsSneakyButNotInvisible()
        {
            // Sneaky: the SAM no longer sees it across the whole arena...
            Assert.Less(Det(ThreatEnvelope.SamDetectionRange, IhaRcs), ThreatEnvelope.MaxEngagementDistance);

            // ...but not invisible: it is still picked up well before it can use its own 45 m gun on a
            // battery, so a SEAD run is a decision rather than a walk-in.
            AircraftProfile iha = AircraftCatalog.GetOrDefault(AircraftCatalog.IhaId);
            Assert.Greater(Det(ThreatEnvelope.SamDetectionRange, IhaRcs), iha.GunRange);
        }

        [Test]
        public void Jet_IsConspicuousButSurvivable()
        {
            // Conspicuous: a SAM tracks the jet anywhere on the field.
            Assert.IsTrue(ThreatEnvelope.CoversWholeField(Det(ThreatEnvelope.SamDetectionRange, JetRcs)));

            // Survivable: being SEEN everywhere is not being SHOT everywhere — the fire range is
            // unchanged by signature, and it is far short of the arena.
            Assert.Less(ThreatEnvelope.FireDistanceAgainst(ThreatEnvelope.SamDetectionRange,
                                                           ThreatEnvelope.SamFireRange, JetRcs),
                        ThreatEnvelope.MaxEngagementDistance);
        }

        // ------------------------------------------------------------------ the published table

        [Test]
        public void PublishedBalanceTable_StillHolds()
        {
            // Detection, metres (DEVLOG table).
            Assert.AreEqual(120.21f, Det(ThreatEnvelope.SamDetectionRange, JetRcs), 0.01f);
            Assert.AreEqual(85.00f, Det(ThreatEnvelope.SamDetectionRange, SihaRcs), 0.01f);
            Assert.AreEqual(60.10f, Det(ThreatEnvelope.SamDetectionRange, IhaRcs), 0.01f);

            Assert.AreEqual(84.85f, Det(ThreatEnvelope.AaaDetectionRange, JetRcs), 0.01f);
            Assert.AreEqual(60.00f, Det(ThreatEnvelope.AaaDetectionRange, SihaRcs), 0.01f);
            Assert.AreEqual(42.43f, Det(ThreatEnvelope.AaaDetectionRange, IhaRcs), 0.01f);

            Assert.AreEqual(91.92f, Det(ThreatEnvelope.FighterDetectionRange, JetRcs), 0.01f);
            Assert.AreEqual(65.00f, Det(ThreatEnvelope.FighterDetectionRange, SihaRcs), 0.01f);
            Assert.AreEqual(45.96f, Det(ThreatEnvelope.FighterDetectionRange, IhaRcs), 0.01f);
        }

        // ------------------------------------------------------------------ degenerate inputs

        [Test]
        public void DegenerateInputs_AreAnsweredNotThrown()
        {
            Assert.AreEqual(0f, Det(0f, SihaRcs), 1e-4f);
            Assert.AreEqual(0f, Det(-10f, SihaRcs), 1e-4f);
            Assert.AreEqual(0f, Det(ThreatEnvelope.SamDetectionRange, 0f), 1e-4f);
            Assert.AreEqual(0f, ThreatEnvelope.FireDistanceAgainst(85f, -5f, SihaRcs), 1e-4f);
            Assert.IsFalse(ThreatEnvelope.CoversWholeField(0f));
        }
    }
}
