using NUnit.Framework;
using UnityEngine;
using Sim.Core;

namespace Sim.Tests
{
    /// <summary>
    /// The jammer's DUTY CYCLE: a burst that runs its full length, then its full cooldown, and is
    /// worth nothing at all in between. The physics it feeds are covered by
    /// <see cref="ElectronicWarfareTests"/> and <see cref="SignatureDetectionTests"/>; nothing here
    /// re-tests them.
    /// </summary>
    public class JammerSystemTests
    {
        private static JammerSystem Fitted() => new JammerSystem(3f, 6f, 14f);

        [Test]
        public void FreshBurstJammer_IsReadyAndSilent()
        {
            JammerSystem j = Fitted();
            Assert.AreEqual(JammerState.Ready, j.State);
            Assert.IsFalse(j.IsActive);
            Assert.AreEqual(0f, j.CurrentStrength, 1e-5f);
            Assert.IsTrue(j.CanActivate);
            Assert.AreEqual(1f, j.ReadyFraction, 1e-5f);

            // Silent means literally indistinguishable from carrying nothing.
            Assert.AreEqual(1f, j.DetectionRangeFactor, 1e-5f);
        }

        [Test]
        public void UnfittedJammer_CanNeverDoAnything()
        {
            // Upgrade track level 0: strength 0, i.e. no emitter at all.
            var none = new JammerSystem(0f, 6f, 14f);
            Assert.IsFalse(none.IsFitted);
            Assert.IsFalse(none.CanActivate);
            Assert.IsFalse(none.TryActivate());
            none.Tick(100f);
            Assert.AreEqual(0f, none.CurrentStrength, 1e-5f);
            Assert.AreEqual(1f, none.DetectionRangeFactor, 1e-5f);
        }

        [Test]
        public void Activation_RadiatesForExactlyTheBurstLength()
        {
            JammerSystem j = Fitted();
            Assert.IsTrue(j.TryActivate());
            Assert.AreEqual(JammerState.Active, j.State);
            Assert.AreEqual(3f, j.CurrentStrength, 1e-5f);

            // Still radiating one tick short of the end...
            j.Tick(5.9f);
            Assert.AreEqual(JammerState.Active, j.State);
            Assert.Greater(j.CurrentStrength, 0f);

            // ...and silent the moment it runs out.
            j.Tick(0.2f);
            Assert.AreEqual(JammerState.Cooling, j.State);
            Assert.AreEqual(0f, j.CurrentStrength, 1e-5f);
        }

        [Test]
        public void ASecondActivation_IsRefusedUntilTheCooldownFinishes()
        {
            JammerSystem j = Fitted();
            j.TryActivate();

            j.Tick(6f);                                   // burst over, cooldown started
            Assert.AreEqual(JammerState.Cooling, j.State);
            Assert.IsFalse(j.CanActivate);
            Assert.IsFalse(j.TryActivate());

            j.Tick(13.9f);
            Assert.AreEqual(JammerState.Cooling, j.State);
            Assert.IsFalse(j.TryActivate());

            j.Tick(0.2f);
            Assert.AreEqual(JammerState.Ready, j.State);
            Assert.IsTrue(j.TryActivate());
        }

        [Test]
        public void HammeringTheKeyDuringABurst_NeverRestartsIt()
        {
            JammerSystem j = Fitted();
            j.TryActivate();
            j.Tick(3f);

            for (int i = 0; i < 10; i++) Assert.IsFalse(j.TryActivate());

            // The burst still ends on its original schedule (3 s left, not 6).
            j.Tick(3.01f);
            Assert.AreEqual(JammerState.Cooling, j.State);
        }

        [Test]
        public void OneHugeStep_DoesNotSkipTheCooldown()
        {
            // The whole point of the trade-off: a single long frame must not hand the burst back.
            JammerSystem j = Fitted();
            j.TryActivate();
            j.Tick(1000f);

            Assert.AreEqual(JammerState.Cooling, j.State);
            Assert.AreEqual(14f, j.SecondsRemaining, 1e-4f);
        }

        [Test]
        public void ReadyFraction_ClimbsBackFromZeroAcrossTheCooldown()
        {
            JammerSystem j = Fitted();
            j.TryActivate();
            j.Tick(6f);
            Assert.AreEqual(0f, j.ReadyFraction, 1e-4f);

            j.Tick(7f);
            Assert.AreEqual(0.5f, j.ReadyFraction, 1e-3f);

            j.Tick(7f);
            Assert.AreEqual(1f, j.ReadyFraction, 1e-4f);
        }

        [Test]
        public void BurstFraction_RunsFromOneDownToZero()
        {
            JammerSystem j = Fitted();
            Assert.AreEqual(0f, j.BurstFraction, 1e-4f);

            j.TryActivate();
            Assert.AreEqual(1f, j.BurstFraction, 1e-4f);

            j.Tick(3f);
            Assert.AreEqual(0.5f, j.BurstFraction, 1e-3f);

            j.Tick(3f);
            Assert.AreEqual(0f, j.BurstFraction, 1e-4f);
        }

        [Test]
        public void ContinuousMode_IsTheOldAlwaysOnBehaviour()
        {
            // A non-positive burst length means no duty cycle: this is what a Jammer dropped into a
            // hand-authored scene used to do, and must keep doing.
            var always = new JammerSystem(4f, 0f, 14f);
            Assert.IsTrue(always.IsContinuous);
            Assert.AreEqual(JammerState.Active, always.State);
            Assert.AreEqual(4f, always.CurrentStrength, 1e-5f);
            Assert.IsFalse(always.CanActivate);

            always.Tick(1000f);
            Assert.AreEqual(4f, always.CurrentStrength, 1e-5f, "a continuous emitter never times out");
        }

        [Test]
        public void Reset_RearmsTheEmitter()
        {
            JammerSystem j = Fitted();
            j.TryActivate();
            j.Tick(6f);
            Assert.AreEqual(JammerState.Cooling, j.State);

            j.Reset();
            Assert.AreEqual(JammerState.Ready, j.State);
            Assert.IsTrue(j.CanActivate);
        }

        [Test]
        public void DegenerateInputs_AreAnsweredNotThrown()
        {
            var j = new JammerSystem(-5f, 6f, -3f);
            Assert.AreEqual(0f, j.Strength, 1e-5f, "negative strength reads as no emitter");
            Assert.AreEqual(0f, j.CooldownSeconds, 1e-5f);

            var free = new JammerSystem(2f, 6f, 0f);      // zero cooldown: straight back to ready
            Assert.IsTrue(free.TryActivate());
            free.Tick(6f);
            Assert.AreEqual(JammerState.Ready, free.State);

            JammerSystem fitted = Fitted();
            fitted.Tick(0f);
            fitted.Tick(-4f);
            Assert.AreEqual(JammerState.Ready, fitted.State);
        }

        [Test]
        public void ActiveStrength_ShortensRangesExactlyAsElectronicWarfareSays()
        {
            // No second copy of the formula: the system just gates the one in Sim.Core.
            JammerSystem j = Fitted();
            j.TryActivate();
            Assert.AreEqual(ElectronicWarfare.EffectiveRange(85f, 3f),
                            85f * j.DetectionRangeFactor, 1e-3f);
        }
    }

    /// <summary>
    /// The hangar's "Elektronik Harp" track: level 0 must leave a stock aircraft untouched, and each
    /// level above must buy a strictly stronger emitter at a strictly higher price.
    /// </summary>
    public class ElectronicWarfareTrackTests
    {
        private const UpgradeTrack Ew = UpgradeTrack.ElectronicWarfare;

        [Test]
        public void LevelZero_MeansNoJammerAtAll()
        {
            Assert.AreEqual(0f, UpgradeCatalog.JammerStrengthAtLevel(0), 1e-5f);
            Assert.AreEqual(1f, UpgradeCatalog.JammerDetectionFactor(0), 1e-5f);

            // ...and therefore a fresh save flies a jammer-less aircraft.
            foreach (AircraftProfile b in AircraftCatalog.All)
            {
                Assert.AreEqual(0f, b.JammerStrength, 1e-5f, $"{b.Id} must ship without a jammer");
                Assert.AreEqual(0f, AircraftUpgrades.Apply(b, new UpgradeState()).JammerStrength, 1e-5f);
            }
        }

        [Test]
        public void EveryLevel_IsStrongerAndShortensDetectionFurther()
        {
            int max = UpgradeCatalog.MaxLevel(Ew);
            for (int l = 1; l <= max; l++)
            {
                Assert.Greater(UpgradeCatalog.JammerStrengthAtLevel(l),
                               UpgradeCatalog.JammerStrengthAtLevel(l - 1));
                Assert.Less(UpgradeCatalog.JammerDetectionFactor(l),
                            UpgradeCatalog.JammerDetectionFactor(l - 1));
            }

            // Clamped at the cap rather than extrapolated.
            Assert.AreEqual(UpgradeCatalog.JammerStrengthAtLevel(max),
                            UpgradeCatalog.JammerStrengthAtLevel(max + 5), 1e-5f);
            Assert.AreEqual(0f, UpgradeCatalog.JammerStrengthAtLevel(-2), 1e-5f);
        }

        [Test]
        public void PublishedStrengthsAndReductions_StillHold()
        {
            // 1.5 / 3.0 / 4.5 -> detection x0.795, x0.707, x0.653 (the DEVLOG table).
            Assert.AreEqual(1.5f, UpgradeCatalog.JammerStrengthAtLevel(1), 1e-4f);
            Assert.AreEqual(3.0f, UpgradeCatalog.JammerStrengthAtLevel(2), 1e-4f);
            Assert.AreEqual(4.5f, UpgradeCatalog.JammerStrengthAtLevel(3), 1e-4f);

            Assert.AreEqual(0.795f, UpgradeCatalog.JammerDetectionFactor(1), 1e-3f);
            Assert.AreEqual(0.707f, UpgradeCatalog.JammerDetectionFactor(2), 1e-3f);
            Assert.AreEqual(0.653f, UpgradeCatalog.JammerDetectionFactor(3), 1e-3f);
        }

        [Test]
        public void LevelTwo_ExactlyCancelsTheFighterJetsSignaturePenalty()
        {
            AircraftProfile jet = AircraftCatalog.GetOrDefault(AircraftCatalog.FighterJetId);
            AircraftProfile siha = AircraftCatalog.GetOrDefault(AircraftCatalog.SihaId);

            // A jamming jet is seen no further out than a clean SİHA — the neatest statement of what
            // the track is worth.
            float jamming = UpgradeCatalog.JammerStrengthAtLevel(2);
            float jetJammed = SignatureDetection.EffectiveRange(ThreatEnvelope.SamDetectionRange,
                                                                SignatureDetection.BaselineRcs,
                                                                jet.RadarSignature, jamming);
            float sihaClean = SignatureDetection.EffectiveRange(ThreatEnvelope.SamDetectionRange,
                                                                SignatureDetection.BaselineRcs,
                                                                siha.RadarSignature, 0f);
            Assert.AreEqual(sihaClean, jetJammed, 1e-2f);
        }

        [Test]
        public void BuyingTheTrack_FitsAJammerAndMovesNothingElse()
        {
            var s = new UpgradeState();
            var w = new Wallet(1000000);
            Assert.IsTrue(s.TryPurchase(Ew, w));

            AircraftProfile b = AircraftCatalog.Default;
            AircraftProfile applied = AircraftUpgrades.Apply(b, s);

            Assert.AreEqual(UpgradeCatalog.JammerStrengthAtLevel(1), applied.JammerStrength, 1e-4f);
            Assert.AreEqual(b.RadarSignature, applied.RadarSignature, 1e-4f,
                            "the garage sells noise, not a smaller airframe");
            Assert.AreEqual(b.MaxSpeed, applied.MaxSpeed, 1e-4f);
            Assert.AreEqual(b.Health, applied.Health, 1e-4f);
            Assert.AreEqual(b.DetectionRange, applied.DetectionRange, 1e-4f);
        }

        [Test]
        public void ApplyingTwice_DoesNotDoubleTheJammer()
        {
            // Apply is a pure function of the state, which is what makes SimulationBootstrap.Rebuild()
            // safe: rebuilding the world re-derives the same strength instead of stacking one.
            var s = new UpgradeState();
            var w = new Wallet(1000000);
            s.TryPurchase(Ew, w);
            s.TryPurchase(Ew, w);

            AircraftProfile b = AircraftCatalog.Default;
            float once = AircraftUpgrades.Apply(b, s).JammerStrength;
            float twice = AircraftUpgrades.Apply(AircraftUpgrades.Apply(b, s), s).JammerStrength;

            Assert.AreEqual(UpgradeCatalog.JammerStrengthAtLevel(2), once, 1e-4f);
            Assert.AreEqual(once, twice, 1e-4f);
        }

        [Test]
        public void SavedLevelsFromBeforeTheTrackExisted_RestoreItAtZero()
        {
            // The persisted array is one entry shorter than the catalogue now; Restore must read that
            // as "no jammer bought", not as garbage.
            var s = new UpgradeState();
            var legacy = new int[UpgradeCatalog.TrackCount - 1];
            for (int i = 0; i < legacy.Length; i++) legacy[i] = 1;

            s.Restore(legacy);
            Assert.AreEqual(0, s.LevelOf(Ew));
            Assert.AreEqual(0f, AircraftUpgrades.Apply(AircraftCatalog.Default, s).JammerStrength, 1e-5f);
        }

        [Test]
        public void DutyCycle_IsARealCost()
        {
            // A burst is a window, not a state: it must be strictly shorter than its own cooldown, so
            // the aircraft spends most of every minute radiating nothing.
            Assert.Greater(UpgradeCatalog.JammerBurstSeconds, 0f);
            Assert.Greater(UpgradeCatalog.JammerCooldownSeconds, UpgradeCatalog.JammerBurstSeconds);

            float duty = UpgradeCatalog.JammerBurstSeconds
                         / (UpgradeCatalog.JammerBurstSeconds + UpgradeCatalog.JammerCooldownSeconds);
            Assert.Less(duty, 0.5f);
            Assert.AreEqual(0.3f, duty, 1e-3f);
        }

        [Test]
        public void HangarCopy_IsTurkishAndNonEmpty()
        {
            Assert.AreEqual("Elektronik Harp", UpgradeCatalog.Name(Ew));
            Assert.IsFalse(string.IsNullOrEmpty(UpgradeCatalog.Description(Ew)));
            Assert.AreEqual("temel", UpgradeCatalog.EffectSummary(Ew, 0));
            for (int l = 1; l <= UpgradeCatalog.MaxLevel(Ew); l++)
                Assert.IsFalse(string.IsNullOrEmpty(UpgradeCatalog.EffectSummary(Ew, l)));
        }

        [Test]
        public void CostCurve_FollowsTheSameFormulaAsEveryOtherTrack()
        {
            int max = UpgradeCatalog.MaxLevel(Ew);
            for (int l = 1; l <= max; l++)
            {
                float raw = UpgradeCatalog.BaseCost(Ew) * Mathf.Pow(UpgradeCatalog.Growth, l - 1);
                int expected = Mathf.Max(1, Mathf.RoundToInt(raw / UpgradeCatalog.PriceStep))
                               * UpgradeCatalog.PriceStep;
                Assert.AreEqual(expected, UpgradeCatalog.CostOfLevel(Ew, l));
            }
        }
    }
}
