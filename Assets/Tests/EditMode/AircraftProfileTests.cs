using System.Collections.Generic;
using NUnit.Framework;
using Sim.Core;

namespace Sim.Tests
{
    /// <summary>
    /// Catalogue invariants for the three flyable archetypes. Deliberately asserts RELATIONSHIPS
    /// (jet faster than SİHA faster than İHA, …) rather than literal numbers, so the profiles can be
    /// retuned without rewriting the suite.
    /// </summary>
    public class AircraftProfileTests
    {
        private static AircraftProfile Jet => AircraftCatalog.GetOrDefault(AircraftCatalog.FighterJetId);
        private static AircraftProfile Siha => AircraftCatalog.GetOrDefault(AircraftCatalog.SihaId);
        private static AircraftProfile Iha => AircraftCatalog.GetOrDefault(AircraftCatalog.IhaId);

        [Test]
        public void Catalog_HasThreeProfilesWithUniqueIdsAndNames()
        {
            IReadOnlyList<AircraftProfile> all = AircraftCatalog.All;
            Assert.AreEqual(3, all.Count);

            var ids = new HashSet<string>();
            var names = new HashSet<string>();
            for (int i = 0; i < all.Count; i++)
            {
                AircraftProfile p = all[i];
                Assert.IsFalse(string.IsNullOrEmpty(p.Id));
                Assert.IsFalse(string.IsNullOrEmpty(p.DisplayName));
                Assert.IsFalse(string.IsNullOrEmpty(p.Description));
                Assert.IsTrue(ids.Add(p.Id), "duplicate id: " + p.Id);
                Assert.IsTrue(names.Add(p.DisplayName), "duplicate name: " + p.DisplayName);
            }
        }

        [Test]
        public void EachKind_AppearsExactlyOnce()
        {
            var kinds = new HashSet<AircraftKind>();
            foreach (AircraftProfile p in AircraftCatalog.All)
            {
                Assert.IsTrue(kinds.Add(p.Kind), "duplicate kind: " + p.Kind);
            }
            Assert.AreEqual(3, kinds.Count);
        }

        [Test]
        public void Default_IsTheSihaBaselineAndIsInAll()
        {
            AircraftProfile def = AircraftCatalog.Default;
            Assert.AreEqual(AircraftKind.Siha, def.Kind);

            bool inCatalog = false;
            foreach (AircraftProfile p in AircraftCatalog.All)
            {
                if (ReferenceEquals(p, def)) inCatalog = true;
            }
            Assert.IsTrue(inCatalog, "Default must be one of the catalogue entries");
        }

        [Test]
        public void TryGet_ResolvesEveryId()
        {
            foreach (AircraftProfile p in AircraftCatalog.All)
            {
                Assert.IsTrue(AircraftCatalog.TryGet(p.Id, out AircraftProfile found));
                Assert.AreSame(p, found);
            }
        }

        [Test]
        public void TryGet_FailsQuietlyForUnknownOrNullId()
        {
            Assert.IsFalse(AircraftCatalog.TryGet("no_such_aircraft", out AircraftProfile unknown));
            Assert.IsNull(unknown);

            Assert.IsFalse(AircraftCatalog.TryGet(null, out AircraftProfile ofNull));
            Assert.IsNull(ofNull);

            Assert.IsFalse(AircraftCatalog.TryGet(string.Empty, out AircraftProfile ofEmpty));
            Assert.IsNull(ofEmpty);
        }

        [Test]
        public void GetOrDefault_FallsBackToDefault()
        {
            Assert.AreSame(AircraftCatalog.Default, AircraftCatalog.GetOrDefault(null));
            Assert.AreSame(AircraftCatalog.Default, AircraftCatalog.GetOrDefault("bogus"));
            Assert.AreSame(Jet, AircraftCatalog.GetOrDefault(AircraftCatalog.FighterJetId));
        }

        [Test]
        public void Cycle_WrapsInBothDirections()
        {
            IReadOnlyList<AircraftProfile> all = AircraftCatalog.All;
            int n = all.Count;

            // Stepping forward through the whole list returns to the starting profile.
            AircraftProfile p = AircraftCatalog.Default;
            for (int i = 0; i < n; i++) p = AircraftCatalog.Cycle(p.Id, 1);
            Assert.AreSame(AircraftCatalog.Default, p);

            // ...and so does stepping backward.
            for (int i = 0; i < n; i++) p = AircraftCatalog.Cycle(p.Id, -1);
            Assert.AreSame(AircraftCatalog.Default, p);

            // A single step really moves, and +1 then -1 comes back.
            AircraftProfile next = AircraftCatalog.Cycle(AircraftCatalog.Default.Id, 1);
            Assert.AreNotSame(AircraftCatalog.Default, next);
            Assert.AreSame(AircraftCatalog.Default, AircraftCatalog.Cycle(next.Id, -1));

            // Wrapping from the ends does not fall off the list.
            Assert.AreSame(all[n - 1], AircraftCatalog.Cycle(all[0].Id, -1));
            Assert.AreSame(all[0], AircraftCatalog.Cycle(all[n - 1].Id, 1));
        }

        [Test]
        public void Cycle_ReturnsDefaultForUnknownId()
        {
            Assert.AreSame(AircraftCatalog.Default, AircraftCatalog.Cycle("bogus", 1));
            Assert.AreSame(AircraftCatalog.Default, AircraftCatalog.Cycle(null, -1));
        }

        [Test]
        public void Jet_IsFastest_IhaIsSlowest()
        {
            Assert.Greater(Jet.MaxSpeed, Siha.MaxSpeed);
            Assert.Greater(Siha.MaxSpeed, Iha.MaxSpeed);

            // The pilot-commandable cap follows the same ordering.
            Assert.Greater(Jet.PilotMaxSpeed, Siha.PilotMaxSpeed);
            Assert.Greater(Siha.PilotMaxSpeed, Iha.PilotMaxSpeed);

            // ...and so does agility.
            Assert.Greater(Jet.TurnRateDeg, Siha.TurnRateDeg);
            Assert.Greater(Siha.TurnRateDeg, Iha.TurnRateDeg);
        }

        [Test]
        public void Iha_HasMostFuel_JetHasLeast()
        {
            Assert.Greater(Iha.FuelCapacity, Siha.FuelCapacity);
            Assert.Greater(Siha.FuelCapacity, Jet.FuelCapacity);

            // The jet is also the thirstiest, the recon drone the most frugal.
            Assert.Greater(Jet.FuelBurnRate, Siha.FuelBurnRate);
            Assert.Greater(Siha.FuelBurnRate, Iha.FuelBurnRate);
        }

        [Test]
        public void EveryArchetype_FliesLongEnoughToBeWorthTakingOff()
        {
            // Endurance is what the player actually feels — capacity ALONE says nothing, because the
            // jet's smaller tank is paired with a heavier burn. A sortie used to end in 21.9 s on the
            // jet (7.3 s of it if the afterburner was held, which costs 3x), which is less than one
            // lap of the 80 x 80 m arena plus a firing pass. The floors below are the "this is a
            // sortie, not a hop" bound; they are deliberately well under the authored values so the
            // profiles stay retunable.
            Assert.Greater(Endurance(Jet), 45f);
            Assert.Greater(Endurance(Siha), 100f);
            Assert.Greater(Endurance(Iha), 250f);

            // Even with the afterburner's 3x burn AND a radiating jammer's 1.5x on top (4.5x), the
            // thirstiest archetype still gets a usable burst rather than a puff.
            Assert.Greater(Endurance(Jet) / 4.5f, 10f);

            // And the ORDERING is the archetypes' identity: the recon İHA is the long-legged one and
            // the jet the short-legged one, whatever the tanks are retuned to.
            Assert.Greater(Endurance(Iha), Endurance(Siha));
            Assert.Greater(Endurance(Siha), Endurance(Jet));
        }

        /// <summary>Seconds of flight at full throttle: the tank divided by the burn rate.</summary>
        private static float Endurance(AircraftProfile p)
        {
            return p.FuelCapacity / p.FuelBurnRate;
        }

        [Test]
        public void Iha_SeesFurthest_JetSeesLeast()
        {
            Assert.Greater(Iha.RadarRange, Siha.RadarRange);
            Assert.Greater(Siha.RadarRange, Jet.RadarRange);

            Assert.Greater(Iha.DetectionRange, Siha.DetectionRange);
            Assert.Greater(Siha.DetectionRange, Jet.DetectionRange);
        }

        [Test]
        public void Jet_HasStrongestGun_IhaTheWeakest()
        {
            Assert.Greater(Jet.GunRoundsPerSecond, Siha.GunRoundsPerSecond);
            Assert.Greater(Siha.GunRoundsPerSecond, Iha.GunRoundsPerSecond);

            Assert.Greater(Jet.GunDamage, Siha.GunDamage);
            Assert.Greater(Siha.GunDamage, Iha.GunDamage);

            Assert.Greater(Jet.GunRange, Iha.GunRange);
        }

        [Test]
        public void Siha_CarriesTheMostMissiles_IhaCarriesNone()
        {
            Assert.Greater(Siha.MissileCapacity, Jet.MissileCapacity);
            Assert.Greater(Jet.MissileCapacity, Iha.MissileCapacity);
            Assert.AreEqual(0, Iha.MissileCapacity);

            // The armed drone also shoots furthest of the missile carriers.
            Assert.Greater(Siha.MissileRange, Jet.MissileRange);
        }

        [Test]
        public void Jet_IsTheEasiestToSee_IhaTheHardest()
        {
            Assert.Greater(Jet.RadarSignature, Siha.RadarSignature);
            Assert.Greater(Siha.RadarSignature, Iha.RadarSignature);

            // The SİHA is the baseline every hostile detection range is quoted against, so its
            // signature must stay exactly on it — that is what keeps those ranges meaning what
            // they say.
            Assert.AreEqual(SignatureDetection.BaselineRcs, Siha.RadarSignature, 1e-4f);
        }

        [Test]
        public void SignatureOrdering_SurvivesTheRangeEquation()
        {
            // What the player actually feels: the distance at which one and the same hostile sensor
            // picks each archetype up. Ratios, not absolutes, so the sensor can be retuned freely.
            const float sensor = 100f;
            float jet = Reach(sensor, Jet);
            float siha = Reach(sensor, Siha);
            float iha = Reach(sensor, Iha);

            Assert.Greater(jet, siha);
            Assert.Greater(siha, iha);

            // The baseline is detected at exactly the sensor's stated range.
            Assert.AreEqual(sensor, siha, 1e-3f);

            // The İHA is SNEAKY, NOT INVISIBLE: still well over half the baseline reach.
            Assert.Greater(iha / siha, 0.5f);

            // ...and the jet is not detected absurdly far out either.
            Assert.Less(jet / siha, 2f);
        }

        [Test]
        public void StealthRating_MirrorsTheSignatureOrdering()
        {
            // More is better on every rating bar, so stealth runs OPPOSITE to raw signature.
            Assert.Greater(Iha.StealthRating, Siha.StealthRating);
            Assert.Greater(Siha.StealthRating, Jet.StealthRating);
        }

        private static float Reach(float sensorRange, AircraftProfile p)
        {
            return SignatureDetection.RangeForRcs(sensorRange, SignatureDetection.BaselineRcs,
                                                  p.RadarSignature);
        }

        [Test]
        public void Iha_IsTheMostFragile()
        {
            Assert.Greater(Siha.Health, Jet.Health);
            Assert.Greater(Jet.Health, Iha.Health);
        }

        [Test]
        public void EveryTunable_IsPositiveAndSane()
        {
            foreach (AircraftProfile p in AircraftCatalog.All)
            {
                Assert.Greater(p.MaxSpeed, 0f, p.Id);
                Assert.GreaterOrEqual(p.PilotMaxSpeed, p.MaxSpeed, p.Id);
                Assert.Greater(p.TurnRateDeg, 0f, p.Id);
                Assert.Greater(p.CruiseAltitude, 0f, p.Id);
                Assert.Greater(p.FuelCapacity, 0f, p.Id);
                Assert.Greater(p.FuelBurnRate, 0f, p.Id);
                Assert.Greater(p.GunMagazine, 0, p.Id);
                Assert.Greater(p.GunRoundsPerSecond, 0f, p.Id);
                Assert.Greater(p.GunRange, 0f, p.Id);
                Assert.Greater(p.GunDispersionDeg, 0f, p.Id);
                Assert.Greater(p.GunDamage, 0f, p.Id);
                Assert.GreaterOrEqual(p.MissileCapacity, 0, p.Id);
                Assert.GreaterOrEqual(p.MissileRange, 0f, p.Id);
                Assert.Greater(p.DetectionRange, 0f, p.Id);
                Assert.Greater(p.RadarRange, 0f, p.Id);
                // A zero/negative signature would make the airframe literally undetectable.
                Assert.Greater(p.RadarSignature, 0f, p.Id);
                Assert.Greater(p.Health, 0f, p.Id);

                // A missile carrier must be able to shoot as far as it can see a target lock up.
                if (p.MissileCapacity > 0) Assert.GreaterOrEqual(p.MissileRange, p.DetectionRange * 0.5f, p.Id);
            }
        }

        [Test]
        public void EveryRating_IsWithinZeroToOne()
        {
            foreach (AircraftProfile p in AircraftCatalog.All)
            {
                AssertRating(p.SpeedRating, p.Id + ".Speed");
                AssertRating(p.AgilityRating, p.Id + ".Agility");
                AssertRating(p.FirepowerRating, p.Id + ".Firepower");
                AssertRating(p.EnduranceRating, p.Id + ".Endurance");
                AssertRating(p.StealthRating, p.Id + ".Stealth");
            }
        }

        [Test]
        public void Ratings_MatchTheArchetypeOrdering()
        {
            Assert.Greater(Jet.SpeedRating, Siha.SpeedRating);
            Assert.Greater(Siha.SpeedRating, Iha.SpeedRating);

            Assert.Greater(Iha.EnduranceRating, Siha.EnduranceRating);
            Assert.Greater(Siha.EnduranceRating, Jet.EnduranceRating);

            Assert.Greater(Siha.FirepowerRating, Jet.FirepowerRating);
            Assert.Greater(Jet.FirepowerRating, Iha.FirepowerRating);

            Assert.Greater(Jet.AgilityRating, Siha.AgilityRating);
            Assert.Greater(Siha.AgilityRating, Iha.AgilityRating);
        }

        private static void AssertRating(float value, string what)
        {
            Assert.GreaterOrEqual(value, 0f, what);
            Assert.LessOrEqual(value, 1f, what);
        }
    }
}
