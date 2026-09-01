using NUnit.Framework;
using Sim.Core;

namespace Sim.Tests
{
    /// <summary>
    /// Guards the vertical separation between the scenery and the cruise band. These tests are the
    /// reason the defect they cover cannot come back silently: growing a building archetype without
    /// raising <see cref="FlightEnvelope.MaxStructureHeight"/>, or lowering a cruise altitude into the
    /// margin, fails here rather than in the scene.
    /// </summary>
    public class FlightEnvelopeTests
    {
        private static AircraftProfile Jet => AircraftCatalog.GetOrDefault(AircraftCatalog.FighterJetId);
        private static AircraftProfile Siha => AircraftCatalog.GetOrDefault(AircraftCatalog.SihaId);
        private static AircraftProfile Iha => AircraftCatalog.GetOrDefault(AircraftCatalog.IhaId);

        [Test]
        public void Skyline_IsTerrainReliefPlusTallestStructure()
        {
            Assert.AreEqual(TerrainField.Amplitude + FlightEnvelope.MaxStructureHeight,
                            FlightEnvelope.MaxSkylineAltitude, 1e-4f);
            Assert.AreEqual(14f, FlightEnvelope.MaxSkylineAltitude, 1e-4f);
        }

        [Test]
        public void CruiseFloor_ClearsSkylineByTheStatedMargin()
        {
            Assert.AreEqual(FlightEnvelope.MaxSkylineAltitude + FlightEnvelope.StructureClearance,
                            FlightEnvelope.MinCruiseAltitude, 1e-4f);
            Assert.AreEqual(18f, FlightEnvelope.MinCruiseAltitude, 1e-4f);
            Assert.Greater(FlightEnvelope.MinCruiseAltitude - FlightEnvelope.MaxSkylineAltitude, 0f);
        }

        [Test]
        public void EveryAircraftProfile_CruisesAboveTheSkyline()
        {
            foreach (AircraftProfile p in AircraftCatalog.All)
            {
                Assert.IsTrue(FlightEnvelope.ClearsStructures(p.CruiseAltitude),
                              $"{p.Id} cruises at {p.CruiseAltitude} m, below the " +
                              $"{FlightEnvelope.MinCruiseAltitude} m structure-clearance floor.");
            }
        }

        [Test]
        public void CruiseOrdering_IsUnchangedByTheLift()
        {
            // The band was translated, not reshaped: recon İHA lowest, jet highest.
            Assert.Less(Iha.CruiseAltitude, Siha.CruiseAltitude);
            Assert.Less(Siha.CruiseAltitude, Jet.CruiseAltitude);
        }

        [Test]
        public void ClampToCruiseFloor_LiftsLowAltitudes_AndLeavesHighOnesAlone()
        {
            Assert.AreEqual(FlightEnvelope.MinCruiseAltitude,
                            FlightEnvelope.ClampToCruiseFloor(0f), 1e-4f);
            Assert.AreEqual(FlightEnvelope.MinCruiseAltitude,
                            FlightEnvelope.ClampToCruiseFloor(FlightEnvelope.MinCruiseAltitude - 2.4f), 1e-4f);
            Assert.AreEqual(30f, FlightEnvelope.ClampToCruiseFloor(30f), 1e-4f);
        }

        [Test]
        public void ClearsStructures_IsFalseInsideTheMargin()
        {
            Assert.IsFalse(FlightEnvelope.ClearsStructures(FlightEnvelope.MaxSkylineAltitude));
            Assert.IsFalse(FlightEnvelope.ClearsStructures(FlightEnvelope.MinCruiseAltitude - 0.1f));
            Assert.IsTrue(FlightEnvelope.ClearsStructures(FlightEnvelope.MinCruiseAltitude));
        }
    }
}
