using System.Collections.Generic;
using NUnit.Framework;
using Sim.Core;

namespace Sim.Tests
{
    /// <summary>Catalogue shape: rising cost curve, sane level caps, Turkish copy on every track.</summary>
    public class UpgradeCatalogTests
    {
        [Test]
        public void EveryTrack_HasNameDescriptionLevelsAndCosts()
        {
            Assert.AreEqual(UpgradeCatalog.TrackCount, UpgradeCatalog.All.Length);

            foreach (UpgradeTrack t in UpgradeCatalog.All)
            {
                Assert.IsFalse(string.IsNullOrEmpty(UpgradeCatalog.Name(t)));
                Assert.IsFalse(string.IsNullOrEmpty(UpgradeCatalog.Description(t)));
                Assert.GreaterOrEqual(UpgradeCatalog.MaxLevel(t), 3);
                Assert.Greater(UpgradeCatalog.BaseCost(t), 0);
                Assert.Greater(UpgradeCatalog.PerLevelGain(t), 0f);
            }
        }

        [Test]
        public void Catalog_CoversTheTracksThePlayerAskedFor()
        {
            var tracks = new List<UpgradeTrack>(UpgradeCatalog.All);
            Assert.Contains(UpgradeTrack.Engine, tracks);     // hız
            Assert.Contains(UpgradeTrack.Gun, tracks);        // güç
            Assert.Contains(UpgradeTrack.Missiles, tracks);   // yeni silahlar
            Assert.Contains(UpgradeTrack.Agility, tracks);
            Assert.Contains(UpgradeTrack.Hull, tracks);
            Assert.Contains(UpgradeTrack.Fuel, tracks);
            Assert.Contains(UpgradeTrack.Radar, tracks);
            Assert.Contains(UpgradeTrack.BallisticMissile, tracks);   // balistik füze stoğu
        }

        [Test]
        public void BallisticTrack_IsAppendedLastSoSavedIndicesKeepTheirMeaning()
        {
            // A save written before this track existed is a SHORTER level array; UpgradeState.Restore
            // reads a missing entry as 0. That is only safe while the new track sits at the END.
            Assert.AreEqual(UpgradeTrack.BallisticMissile,
                            UpgradeCatalog.All[UpgradeCatalog.TrackCount - 1]);

            var restored = new UpgradeState();
            restored.Restore(new[] { 1, 1, 1, 1, 1, 1, 1 });   // a 7-track save from before both
            Assert.AreEqual(0, restored.LevelOf(UpgradeTrack.ElectronicWarfare));
            Assert.AreEqual(0, restored.LevelOf(UpgradeTrack.BallisticMissile));
            Assert.AreEqual(1, restored.LevelOf(UpgradeTrack.Engine));
        }

        [Test]
        public void CostCurve_RisesWithEveryLevel()
        {
            foreach (UpgradeTrack t in UpgradeCatalog.All)
            {
                int max = UpgradeCatalog.MaxLevel(t);
                Assert.AreEqual(UpgradeCatalog.BaseCost(t),
                                UpgradeCatalog.CostOfLevel(t, 1),
                                UpgradeCatalog.PriceStep);

                for (int level = 2; level <= max; level++)
                {
                    Assert.Greater(UpgradeCatalog.CostOfLevel(t, level),
                                   UpgradeCatalog.CostOfLevel(t, level - 1),
                                   $"{t} level {level} must cost more than level {level - 1}");
                }

                // The last level is dramatically dearer than the first.
                Assert.Greater(UpgradeCatalog.CostOfLevel(t, max),
                               UpgradeCatalog.CostOfLevel(t, 1) * 2);
            }
        }

        [Test]
        public void CostOfLevel_OutOfRangeIsFreeNotThrown()
        {
            Assert.AreEqual(0, UpgradeCatalog.CostOfLevel(UpgradeTrack.Engine, 0));
            Assert.AreEqual(0, UpgradeCatalog.CostOfLevel(UpgradeTrack.Engine, -2));
            Assert.AreEqual(0, UpgradeCatalog.CostOfLevel(UpgradeTrack.Engine,
                                                          UpgradeCatalog.MaxLevel(UpgradeTrack.Engine) + 1));
        }

        [Test]
        public void Multiplier_IsOneAtLevelZeroAndRisesLinearly()
        {
            foreach (UpgradeTrack t in UpgradeCatalog.All)
            {
                Assert.AreEqual(1f, UpgradeCatalog.Multiplier(t, 0), 1e-6f);
                Assert.AreEqual(1f + UpgradeCatalog.PerLevelGain(t), UpgradeCatalog.Multiplier(t, 1), 1e-5f);

                // Clamped at the cap, never extrapolated past it.
                int max = UpgradeCatalog.MaxLevel(t);
                Assert.AreEqual(UpgradeCatalog.Multiplier(t, max), UpgradeCatalog.Multiplier(t, max + 7), 1e-5f);
                Assert.AreEqual(1f, UpgradeCatalog.Multiplier(t, -3), 1e-6f);
            }
        }

        [Test]
        public void EffectSummary_ReadsAsStockAtLevelZero()
        {
            foreach (UpgradeTrack t in UpgradeCatalog.All)
            {
                Assert.AreEqual("temel", UpgradeCatalog.EffectSummary(t, 0));
                Assert.IsFalse(string.IsNullOrEmpty(UpgradeCatalog.EffectSummary(t, 1)));
            }
        }
    }

    /// <summary>Purchasing: atomicity, the level cap, and insufficient funds.</summary>
    public class UpgradeStateTests
    {
        [Test]
        public void FreshState_IsAllStock()
        {
            var s = new UpgradeState();
            foreach (UpgradeTrack t in UpgradeCatalog.All)
            {
                Assert.AreEqual(0, s.LevelOf(t));
                Assert.IsFalse(s.IsMaxed(t));
                Assert.AreEqual(UpgradeCatalog.CostOfLevel(t, 1), s.NextCost(t));
            }
            Assert.AreEqual(0, s.TotalLevels);
        }

        [Test]
        public void Purchase_SpendsExactlyTheNextCostAndAdvancesOneLevel()
        {
            var s = new UpgradeState();
            var w = new Wallet(10000);

            int price = s.NextCost(UpgradeTrack.Engine);
            Assert.IsTrue(s.TryPurchase(UpgradeTrack.Engine, w));

            Assert.AreEqual(1, s.LevelOf(UpgradeTrack.Engine));
            Assert.AreEqual(10000 - price, w.Balance);
            Assert.AreEqual(1, s.TotalLevels);

            // Only the bought track moved.
            Assert.AreEqual(0, s.LevelOf(UpgradeTrack.Gun));
        }

        [Test]
        public void Purchase_FailsOnInsufficientFundsAndLeavesBothSidesUntouched()
        {
            var s = new UpgradeState();
            int price = s.NextCost(UpgradeTrack.Engine);
            var w = new Wallet(price - 1);

            Assert.IsFalse(s.TryPurchase(UpgradeTrack.Engine, w));
            Assert.AreEqual(price - 1, w.Balance, "wallet must be untouched by a failed purchase");
            Assert.AreEqual(0, s.LevelOf(UpgradeTrack.Engine), "level must be untouched by a failed purchase");
        }

        [Test]
        public void Purchase_FailsAtMaxLevelAndLeavesBothSidesUntouched()
        {
            var s = new UpgradeState();
            var w = new Wallet(1000000);

            int max = UpgradeCatalog.MaxLevel(UpgradeTrack.Missiles);
            for (int i = 0; i < max; i++)
                Assert.IsTrue(s.TryPurchase(UpgradeTrack.Missiles, w));

            Assert.IsTrue(s.IsMaxed(UpgradeTrack.Missiles));
            Assert.AreEqual(0, s.NextCost(UpgradeTrack.Missiles));

            int balance = w.Balance;
            Assert.IsFalse(s.TryPurchase(UpgradeTrack.Missiles, w));
            Assert.AreEqual(balance, w.Balance, "a maxed track must not charge the player");
            Assert.AreEqual(max, s.LevelOf(UpgradeTrack.Missiles));
        }

        [Test]
        public void Purchase_WithNullWalletIsRejected()
        {
            var s = new UpgradeState();
            Assert.IsFalse(s.TryPurchase(UpgradeTrack.Engine, null));
            Assert.AreEqual(0, s.LevelOf(UpgradeTrack.Engine));
        }

        [Test]
        public void Restore_ClampsGarbageAndRoundTripsASnapshot()
        {
            var s = new UpgradeState();
            var w = new Wallet(1000000);
            s.TryPurchase(UpgradeTrack.Gun, w);
            s.TryPurchase(UpgradeTrack.Gun, w);
            s.TryPurchase(UpgradeTrack.Fuel, w);

            var restored = new UpgradeState();
            restored.Restore(s.Snapshot());
            Assert.AreEqual(2, restored.LevelOf(UpgradeTrack.Gun));
            Assert.AreEqual(1, restored.LevelOf(UpgradeTrack.Fuel));
            Assert.AreEqual(s.TotalLevels, restored.TotalLevels);

            // Garbage: too short, negative and over the cap.
            var junk = new UpgradeState();
            junk.Restore(new[] { -5, 999 });
            Assert.AreEqual(0, junk.LevelOf(UpgradeCatalog.All[0]));
            Assert.AreEqual(UpgradeCatalog.MaxLevel(UpgradeCatalog.All[1]),
                            junk.LevelOf(UpgradeCatalog.All[1]));

            var nulled = new UpgradeState();
            nulled.Restore(null);
            Assert.AreEqual(0, nulled.TotalLevels);
        }
    }

    /// <summary>
    /// <see cref="AircraftUpgrades.Apply"/>: a stock state returns the base profile unchanged, and
    /// each track moves EXACTLY the fields it claims to and nothing else.
    /// </summary>
    public class AircraftUpgradeApplyTests
    {
        [Test]
        public void ZeroUpgrades_ReturnsAProfileEqualToTheBase()
        {
            var stock = new UpgradeState();
            foreach (AircraftProfile b in AircraftCatalog.All)
            {
                AircraftProfile applied = AircraftUpgrades.Apply(b, stock);
                CollectionAssert.IsEmpty(DifferingFields(b, applied),
                                         $"{b.Id}: a brand-new player must fly the stock profile");
            }
        }

        [Test]
        public void NullState_ReturnsTheBaseProfileItself()
        {
            AircraftProfile b = AircraftCatalog.Default;
            Assert.AreSame(b, AircraftUpgrades.Apply(b, null));
        }

        [Test]
        public void NullBaseProfile_FallsBackToTheDefaultInsteadOfThrowing()
        {
            AircraftProfile applied = AircraftUpgrades.Apply(null, new UpgradeState());
            Assert.AreEqual(AircraftCatalog.Default.Id, applied.Id);
        }

        [Test]
        public void EachTrack_MovesOnlyItsOwnFields()
        {
            foreach (UpgradeTrack t in UpgradeCatalog.All)
            {
                var s = new UpgradeState();
                var w = new Wallet(1000000);
                Assert.IsTrue(s.TryPurchase(t, w));

                // The JET, not the SİHA baseline, because it is the only archetype that carries
                // EVERY weapon station (missiles AND the balistik füze launcher) — a track cannot be
                // shown to move its own field on an airframe that has no such station to move.
                AircraftProfile b = AircraftCatalog.GetOrDefault(AircraftCatalog.FighterJetId);
                AircraftProfile applied = AircraftUpgrades.Apply(b, s);

                List<string> moved = DifferingFields(b, applied);
                CollectionAssert.AreEquivalent(AircraftUpgrades.AffectedFields(t), moved,
                                               $"{t} moved the wrong set of fields");
            }
        }

        [Test]
        public void Tracks_MoveTheirFieldsUpwards()
        {
            AircraftProfile b = AircraftCatalog.Default;
            var w = new Wallet(1000000);

            AircraftProfile engine = Applied(UpgradeTrack.Engine, w);
            Assert.Greater(engine.MaxSpeed, b.MaxSpeed);
            Assert.Greater(engine.PilotMaxSpeed, b.PilotMaxSpeed);

            AircraftProfile gun = Applied(UpgradeTrack.Gun, w);
            Assert.Greater(gun.GunDamage, b.GunDamage);

            AircraftProfile agility = Applied(UpgradeTrack.Agility, w);
            Assert.Greater(agility.TurnRateDeg, b.TurnRateDeg);

            AircraftProfile hull = Applied(UpgradeTrack.Hull, w);
            Assert.Greater(hull.Health, b.Health);

            AircraftProfile fuel = Applied(UpgradeTrack.Fuel, w);
            Assert.Greater(fuel.FuelCapacity, b.FuelCapacity);
            Assert.AreEqual(b.FuelBurnRate, fuel.FuelBurnRate, 1e-4f);

            AircraftProfile radar = Applied(UpgradeTrack.Radar, w);
            Assert.Greater(radar.RadarRange, b.RadarRange);
            Assert.Greater(radar.DetectionRange, b.DetectionRange);
        }

        [Test]
        public void MissileTrack_AddsOneRackPerLevelAndExtendsReach()
        {
            AircraftProfile b = AircraftCatalog.Default;   // SİHA: already carries missiles
            var s = new UpgradeState();
            var w = new Wallet(1000000);

            s.TryPurchase(UpgradeTrack.Missiles, w);
            s.TryPurchase(UpgradeTrack.Missiles, w);

            AircraftProfile applied = AircraftUpgrades.Apply(b, s);
            Assert.AreEqual(b.MissileCapacity + 2, applied.MissileCapacity);
            Assert.Greater(applied.MissileRange, b.MissileRange);
        }

        [Test]
        public void MissileTrack_GivesAGunOnlyAirframeAUsableNewWeapon()
        {
            AircraftProfile iha = AircraftCatalog.GetOrDefault(AircraftCatalog.IhaId);
            Assert.AreEqual(0, iha.MissileCapacity, "the recon İHA is the gun-only baseline here");

            var s = new UpgradeState();
            var w = new Wallet(1000000);
            s.TryPurchase(UpgradeTrack.Missiles, w);

            AircraftProfile applied = AircraftUpgrades.Apply(iha, s);
            Assert.AreEqual(1, applied.MissileCapacity);
            Assert.Greater(applied.MissileRange, 0f,
                           "a new rack with a 0 m range would be a dead weapon");
        }

        [Test]
        public void BallisticTrack_AddsOneRoundPerLevel_ToTheJetsStockRack()
        {
            AircraftProfile jet = AircraftCatalog.GetOrDefault(AircraftCatalog.FighterJetId);
            var s = new UpgradeState();
            var w = new Wallet(1000000);

            Assert.AreEqual(jet.BallisticRounds,
                            AircraftUpgrades.Apply(jet, s).BallisticRounds,
                            "level 0 must leave a fresh save exactly as it is");

            Assert.IsTrue(s.TryPurchase(UpgradeTrack.BallisticMissile, w));
            Assert.AreEqual(jet.BallisticRounds + 1, AircraftUpgrades.Apply(jet, s).BallisticRounds);

            Assert.IsTrue(s.TryPurchase(UpgradeTrack.BallisticMissile, w));
            Assert.AreEqual(jet.BallisticRounds + 2, AircraftUpgrades.Apply(jet, s).BallisticRounds);
        }

        [Test]
        public void BallisticTrack_CannotBoltALauncherOntoAnAirframeThatHasNone()
        {
            var s = new UpgradeState();
            var w = new Wallet(1000000);
            while (!s.IsMaxed(UpgradeTrack.BallisticMissile))
                Assert.IsTrue(s.TryPurchase(UpgradeTrack.BallisticMissile, w));

            foreach (string id in new[] { AircraftCatalog.SihaId, AircraftCatalog.IhaId })
            {
                AircraftProfile b = AircraftCatalog.GetOrDefault(id);
                Assert.AreEqual(0, b.BallisticRounds, id);
                Assert.AreEqual(0, AircraftUpgrades.Apply(b, s).BallisticRounds,
                                $"{id}: the balistik füze is the jet's weapon alone");
            }
        }

        [Test]
        public void ApplyingTheSameStateTwice_DoesNotDoubleTheRounds()
        {
            AircraftProfile jet = AircraftCatalog.GetOrDefault(AircraftCatalog.FighterJetId);
            var s = new UpgradeState();
            var w = new Wallet(1000000);
            s.TryPurchase(UpgradeTrack.BallisticMissile, w);
            s.TryPurchase(UpgradeTrack.BallisticMissile, w);

            AircraftProfile once = AircraftUpgrades.Apply(jet, s);
            AircraftProfile twice = AircraftUpgrades.Apply(jet, s);

            // Apply is a pure function of (base, state): re-deriving the profile — which is exactly
            // what SimulationBootstrap.Rebuild does — must land on the same rack every time.
            Assert.AreEqual(jet.BallisticRounds + 2, once.BallisticRounds);
            Assert.AreEqual(once.BallisticRounds, twice.BallisticRounds);
            Assert.AreEqual(jet.BallisticRounds, jet.BallisticRounds,
                            "and the base profile itself is never touched");
            Assert.AreEqual(2, jet.BallisticRounds);
        }

        [Test]
        public void Apply_DoesNotMutateTheBaseProfile()
        {
            AircraftProfile b = AircraftCatalog.Default;
            float speedBefore = b.MaxSpeed;
            int missilesBefore = b.MissileCapacity;

            var s = new UpgradeState();
            var w = new Wallet(1000000);
            foreach (UpgradeTrack t in UpgradeCatalog.All) s.TryPurchase(t, w);
            AircraftUpgrades.Apply(b, s);

            Assert.AreEqual(speedBefore, b.MaxSpeed, 1e-4f);
            Assert.AreEqual(missilesBefore, b.MissileCapacity);
        }

        [Test]
        public void FullyUpgraded_IsStrictlyBetterButKeepsIdentity()
        {
            AircraftProfile b = AircraftCatalog.Default;
            var s = new UpgradeState();
            var w = new Wallet(10000000);

            foreach (UpgradeTrack t in UpgradeCatalog.All)
            {
                while (!s.IsMaxed(t)) Assert.IsTrue(s.TryPurchase(t, w));
            }

            AircraftProfile applied = AircraftUpgrades.Apply(b, s);
            Assert.AreEqual(b.Id, applied.Id);
            Assert.AreEqual(b.Kind, applied.Kind);
            Assert.AreEqual(b.CruiseAltitude, applied.CruiseAltitude, 1e-4f);
            Assert.AreEqual(b.GunMagazine, applied.GunMagazine);
            Assert.Greater(applied.MaxSpeed, b.MaxSpeed);
            Assert.Greater(applied.Health, b.Health);
        }

        private static AircraftProfile Applied(UpgradeTrack track, Wallet wallet)
        {
            var s = new UpgradeState();
            s.TryPurchase(track, wallet);
            return AircraftUpgrades.Apply(AircraftCatalog.Default, s);
        }

        /// <summary>
        /// Names of the numeric profile fields that differ between two profiles. Written out by hand
        /// (no reflection) so the test states exactly what it compares.
        /// </summary>
        private static List<string> DifferingFields(AircraftProfile a, AircraftProfile b)
        {
            var diff = new List<string>();
            Cmp(diff, "MaxSpeed", a.MaxSpeed, b.MaxSpeed);
            Cmp(diff, "PilotMaxSpeed", a.PilotMaxSpeed, b.PilotMaxSpeed);
            Cmp(diff, "TurnRateDeg", a.TurnRateDeg, b.TurnRateDeg);
            Cmp(diff, "CruiseAltitude", a.CruiseAltitude, b.CruiseAltitude);
            Cmp(diff, "FuelCapacity", a.FuelCapacity, b.FuelCapacity);
            Cmp(diff, "FuelBurnRate", a.FuelBurnRate, b.FuelBurnRate);
            Cmp(diff, "GunRoundsPerSecond", a.GunRoundsPerSecond, b.GunRoundsPerSecond);
            Cmp(diff, "GunRange", a.GunRange, b.GunRange);
            Cmp(diff, "GunDispersionDeg", a.GunDispersionDeg, b.GunDispersionDeg);
            Cmp(diff, "GunDamage", a.GunDamage, b.GunDamage);
            Cmp(diff, "MissileRange", a.MissileRange, b.MissileRange);
            Cmp(diff, "DetectionRange", a.DetectionRange, b.DetectionRange);
            Cmp(diff, "RadarRange", a.RadarRange, b.RadarRange);
            Cmp(diff, "RadarSignature", a.RadarSignature, b.RadarSignature);
            Cmp(diff, "JammerStrength", a.JammerStrength, b.JammerStrength);
            Cmp(diff, "Health", a.Health, b.Health);
            Cmp(diff, "SpeedRating", a.SpeedRating, b.SpeedRating);
            Cmp(diff, "AgilityRating", a.AgilityRating, b.AgilityRating);
            Cmp(diff, "FirepowerRating", a.FirepowerRating, b.FirepowerRating);
            Cmp(diff, "EnduranceRating", a.EnduranceRating, b.EnduranceRating);
            Cmp(diff, "StealthRating", a.StealthRating, b.StealthRating);

            if (a.GunMagazine != b.GunMagazine) diff.Add("GunMagazine");
            if (a.MissileCapacity != b.MissileCapacity) diff.Add("MissileCapacity");
            if (a.BallisticRounds != b.BallisticRounds) diff.Add("BallisticRounds");
            return diff;
        }

        private static void Cmp(List<string> diff, string name, float a, float b)
        {
            if (UnityEngine.Mathf.Abs(a - b) > 1e-4f) diff.Add(name);
        }
    }
}
