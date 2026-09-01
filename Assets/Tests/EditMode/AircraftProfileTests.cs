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
