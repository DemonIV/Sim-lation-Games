using NUnit.Framework;
using UnityEngine;
using Sim.Core;

namespace Sim.Tests
{
    /// <summary>
    /// The project's detection law. Deliberately asserts RELATIONSHIPS and RATIOS (bigger is seen
    /// further, fourth-root scaling, FOV vetoes size, jamming shortens everything) rather than
    /// literal distances, so the archetype signatures and sensor ranges can be retuned without
    /// rewriting the suite.
    /// </summary>
    public class SignatureDetectionTests
    {
        private const float Ref = 100f;      // reference range, m
        private const float Baseline = 1f;   // reference RCS, m²

        // ------------------------------------------------------------------ range equation

        [Test]
        public void BaselineTarget_IsSeenAtExactlyTheConfiguredRange()
        {
            Assert.AreEqual(Ref, SignatureDetection.RangeForRcs(Ref, Baseline, Baseline), 1e-3f);
            Assert.AreEqual(Ref, SignatureDetection.EffectiveRange(Ref, Baseline, Baseline, 0f), 1e-3f);
        }

        [Test]
        public void BiggerSignature_IsDetectedFurtherAway()
        {
            float small = SignatureDetection.RangeForRcs(Ref, Baseline, 0.25f);
            float mid = SignatureDetection.RangeForRcs(Ref, Baseline, 1f);
            float big = SignatureDetection.RangeForRcs(Ref, Baseline, 4f);

            Assert.Less(small, mid);
            Assert.Less(mid, big);
        }

        [Test]
        public void FourthRootLaw_SixteenTimesTheSignatureIsTwiceTheRange()
        {
            float one = SignatureDetection.RangeForRcs(Ref, Baseline, 1f);
            float sixteen = SignatureDetection.RangeForRcs(Ref, Baseline, 16f);
            Assert.AreEqual(2f, sixteen / one, 1e-3f);

            // ...and the law holds in the other direction and away from the baseline too.
            float sixteenth = SignatureDetection.RangeForRcs(Ref, Baseline, 1f / 16f);
            Assert.AreEqual(0.5f, sixteenth / one, 1e-3f);

            float four = SignatureDetection.RangeForRcs(Ref, Baseline, 4f);
            float sixtyFour = SignatureDetection.RangeForRcs(Ref, Baseline, 64f);
            Assert.AreEqual(2f, sixtyFour / four, 1e-3f);
        }

        [Test]
        public void FactorOfFourInSignature_IsRootTwoInRange()
        {
            // The exact spacing the aircraft catalogue is built on.
            float quarter = SignatureDetection.RangeForRcs(Ref, Baseline, 0.25f);
            float four = SignatureDetection.RangeForRcs(Ref, Baseline, 4f);

            Assert.AreEqual(Mathf.Sqrt(2f), four / Ref, 1e-3f);
            Assert.AreEqual(1f / Mathf.Sqrt(2f), quarter / Ref, 1e-3f);
        }

        [Test]
        public void RangeScalesLinearlyWithTheReferenceRange()
        {
            float a = SignatureDetection.RangeForRcs(100f, Baseline, 4f);
            float b = SignatureDetection.RangeForRcs(200f, Baseline, 4f);
            Assert.AreEqual(2f, b / a, 1e-3f);
        }

        [Test]
        public void ReferenceRcs_IsWhatTheRangeIsQuotedAgainst()
        {
            // A sensor specified against a 16 m² target sees a 16 m² target at its stated range,
            // and a 1 m² target at half of it.
            Assert.AreEqual(Ref, SignatureDetection.RangeForRcs(Ref, 16f, 16f), 1e-3f);
            Assert.AreEqual(Ref * 0.5f, SignatureDetection.RangeForRcs(Ref, 16f, 1f), 1e-3f);
        }

        // ------------------------------------------------------------------ jamming

        [Test]
        public void Jamming_ShortensTheEffectiveRange()
        {
            float clean = SignatureDetection.EffectiveRange(Ref, Baseline, Baseline, 0f);
            float jammed = SignatureDetection.EffectiveRange(Ref, Baseline, Baseline, 4f);
            float harder = SignatureDetection.EffectiveRange(Ref, Baseline, Baseline, 15f);

            Assert.Less(jammed, clean);
            Assert.Less(harder, jammed);

            // Jamming is exactly the Core EW model: range / (1 + strength)^0.25.
            Assert.AreEqual(ElectronicWarfare.EffectiveRange(clean, 4f), jammed, 1e-3f);
            Assert.AreEqual(clean * 0.5f, harder, 1e-3f);   // (1+15)^0.25 = 2
        }

        [Test]
        public void Jamming_CanHideABigAircraftBehindASmallOne()
        {
            // A 4 m² jet under strong jamming ends up harder to see than a clean 0.25 m² recon drone.
            float jetJammed = SignatureDetection.EffectiveRange(Ref, Baseline, 4f, 20f);
            float ihaClean = SignatureDetection.EffectiveRange(Ref, Baseline, 0.25f, 0f);
            Assert.Less(jetJammed, ihaClean);
        }

        [Test]
        public void NegativeJamming_IsTreatedAsNone()
        {
            Assert.AreEqual(SignatureDetection.EffectiveRange(Ref, Baseline, Baseline, 0f),
                            SignatureDetection.EffectiveRange(Ref, Baseline, Baseline, -5f), 1e-3f);
        }

        // ------------------------------------------------------------------ readout multiplier

        [Test]
        public void RangeMultiplier_IsOneAtTheBaselineAndOrdersTheArchetypes()
        {
            Assert.AreEqual(1f, SignatureDetection.DetectionRangeMultiplier(1f, 0f), 1e-3f);

            float jet = SignatureDetection.DetectionRangeMultiplier(4f, 0f);
            float siha = SignatureDetection.DetectionRangeMultiplier(1f, 0f);
            float iha = SignatureDetection.DetectionRangeMultiplier(0.25f, 0f);

            Assert.Greater(jet, siha);
            Assert.Greater(siha, iha);
            Assert.AreEqual(Mathf.Sqrt(2f), jet, 1e-3f);
            Assert.AreEqual(1f / Mathf.Sqrt(2f), iha, 1e-3f);
        }

        [Test]
        public void RangeMultiplier_IsIndependentOfTheSensorItDescribes()
        {
            // It is a pure property of the TARGET: any sensor scales by the same factor.
            float mult = SignatureDetection.DetectionRangeMultiplier(4f, 3f);
            Assert.AreEqual(SignatureDetection.EffectiveRange(160f, Baseline, 4f, 3f) / 160f,
                            mult, 1e-3f);
            Assert.AreEqual(SignatureDetection.EffectiveRange(80f, Baseline, 4f, 3f) / 80f,
                            mult, 1e-3f);
        }

        [Test]
        public void RangeMultiplier_FallsWhenJammingComesOn()
        {
            Assert.Less(SignatureDetection.DetectionRangeMultiplier(1f, 4f),
                        SignatureDetection.DetectionRangeMultiplier(1f, 0f));
        }

        // ------------------------------------------------------------------ the predicate

        [Test]
        public void CanDetect_InsideRangeAndCone_True()
        {
            Assert.IsTrue(SignatureDetection.CanDetect(Vector3.zero, Vector3.forward, 120f,
                                                       new Vector3(0f, 0f, 80f),
                                                       Ref, Baseline, Baseline, 0f));
        }

        [Test]
        public void CanDetect_BeyondRange_False()
        {
            Assert.IsFalse(SignatureDetection.CanDetect(Vector3.zero, Vector3.forward, 120f,
                                                        new Vector3(0f, 0f, 150f),
                                                        Ref, Baseline, Baseline, 0f));
        }

        [Test]
        public void ABiggerTarget_IsSeenWhereASmallerOneIsNot()
        {
            // 130 m: inside the jet's 141 m reach, outside the SİHA's 100 m and the İHA's 71 m.
            var far = new Vector3(0f, 0f, 130f);

            Assert.IsTrue(SignatureDetection.CanDetect(Vector3.zero, Vector3.forward, 120f, far,
                                                       Ref, Baseline, 4f, 0f));
            Assert.IsFalse(SignatureDetection.CanDetect(Vector3.zero, Vector3.forward, 120f, far,
                                                        Ref, Baseline, 1f, 0f));
            Assert.IsFalse(SignatureDetection.CanDetect(Vector3.zero, Vector3.forward, 120f, far,
                                                        Ref, Baseline, 0.25f, 0f));
        }

        [Test]
        public void OutsideTheFieldOfView_SizeDoesNotHelp()
        {
            // 90° off boresight with a 120° cone (half = 60°): even a huge signature is invisible.
            var abeam = new Vector3(10f, 0f, 0f);
            Assert.IsFalse(SignatureDetection.CanDetect(Vector3.zero, Vector3.forward, 120f, abeam,
                                                        Ref, Baseline, 10000f, 0f));

            // ...while an omnidirectional sensor at the same spot sees it.
            Assert.IsTrue(SignatureDetection.CanDetect(Vector3.zero, Vector3.forward, 360f, abeam,
                                                       Ref, Baseline, 10000f, 0f));
        }

        [Test]
        public void Jamming_CanPushATargetOutOfDetection()
        {
            var at90 = new Vector3(0f, 0f, 90f);   // inside the clean 100 m reach
            Assert.IsTrue(SignatureDetection.CanDetect(Vector3.zero, Vector3.forward, 120f, at90,
                                                       Ref, Baseline, Baseline, 0f));
            Assert.IsFalse(SignatureDetection.CanDetect(Vector3.zero, Vector3.forward, 120f, at90,
                                                        Ref, Baseline, Baseline, 15f));
        }

        // ------------------------------------------------------------------ degenerate inputs

        [Test]
        public void ZeroOrNegativeSignature_IsNotDetectedAndDoesNotThrow()
        {
            Assert.AreEqual(0f, SignatureDetection.RangeForRcs(Ref, Baseline, 0f), 1e-4f);
            Assert.AreEqual(0f, SignatureDetection.RangeForRcs(Ref, Baseline, -3f), 1e-4f);
            Assert.AreEqual(0f, SignatureDetection.EffectiveRange(Ref, Baseline, 0f, 4f), 1e-4f);

            Assert.IsFalse(SignatureDetection.CanDetect(Vector3.zero, Vector3.forward, 360f,
                                                        Vector3.zero, Ref, Baseline, 0f, 0f));
        }

        [Test]
        public void ZeroOrNegativeReferenceRange_DetectsNothing()
        {
            Assert.AreEqual(0f, SignatureDetection.RangeForRcs(0f, Baseline, 4f), 1e-4f);
            Assert.AreEqual(0f, SignatureDetection.RangeForRcs(-50f, Baseline, 4f), 1e-4f);
            Assert.IsFalse(SignatureDetection.CanDetect(Vector3.zero, Vector3.forward, 360f,
                                                        new Vector3(0f, 0f, 1f),
                                                        0f, Baseline, 4f, 0f));
        }

        [Test]
        public void ZeroOrNegativeReferenceRcs_FallsBackToTheBaselineInsteadOfDividingByZero()
        {
            float expected = SignatureDetection.RangeForRcs(Ref, SignatureDetection.BaselineRcs, 4f);
            Assert.AreEqual(expected, SignatureDetection.RangeForRcs(Ref, 0f, 4f), 1e-3f);
            Assert.AreEqual(expected, SignatureDetection.RangeForRcs(Ref, -2f, 4f), 1e-3f);
        }

        [Test]
        public void ZeroFieldOfView_SeesOnlyDeadAhead_AndNeverThrows()
        {
            Assert.IsFalse(SignatureDetection.CanDetect(Vector3.zero, Vector3.forward, 0f,
                                                        new Vector3(1f, 0f, 50f),
                                                        Ref, Baseline, Baseline, 0f));

            // Exactly on the boresight is still a zero-degree bearing error, so it stays visible.
            Assert.IsTrue(SignatureDetection.CanDetect(Vector3.zero, Vector3.forward, 0f,
                                                       new Vector3(0f, 0f, 50f),
                                                       Ref, Baseline, Baseline, 0f));

            // A negative cone is simply never satisfied off-boresight.
            Assert.IsFalse(SignatureDetection.CanDetect(Vector3.zero, Vector3.forward, -30f,
                                                        new Vector3(1f, 0f, 50f),
                                                        Ref, Baseline, Baseline, 0f));
        }

        [Test]
        public void CoincidentOrDegenerateVectors_CountAsInsideTheCone()
        {
            Assert.IsTrue(SignatureDetection.IsInFieldOfView(Vector3.forward, Vector3.zero, 10f));
            Assert.IsTrue(SignatureDetection.IsInFieldOfView(Vector3.zero, Vector3.forward, 10f));

            // A target sitting on top of the sensor is at zero range, so it is detected.
            Assert.IsTrue(SignatureDetection.CanDetect(Vector3.zero, Vector3.forward, 10f,
                                                       Vector3.zero, Ref, Baseline, Baseline, 0f));
        }

        // ------------------------------------------------------------------ delegating callers

        [Test]
        public void RadarSystem_UsesTheSameLaw()
        {
            var radar = new RadarSystem { ReferenceRange = Ref, ReferenceRcs = Baseline, BeamWidthDeg = 120f };

            Assert.AreEqual(SignatureDetection.RangeForRcs(Ref, Baseline, 4f),
                            radar.DetectionRange(4f), 1e-3f);
            Assert.AreEqual(SignatureDetection.EffectiveRange(Ref, Baseline, 4f, 15f),
                            radar.DetectionRange(4f, 15f), 1e-3f);

            // The jamming overload really shortens the radar's reach.
            Assert.Less(radar.DetectionRange(4f, 15f), radar.DetectionRange(4f));
            Assert.IsTrue(radar.CanDetect(Vector3.zero, Vector3.forward, new Vector3(0f, 0f, 130f), 4f));
            Assert.IsFalse(radar.CanDetect(Vector3.zero, Vector3.forward, new Vector3(0f, 0f, 130f), 4f, 15f));
        }
    }
}
