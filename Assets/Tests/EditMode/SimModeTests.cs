using NUnit.Framework;
using Sim.Core;

namespace Sim.Tests
{
    /// <summary>
    /// Guards the free-flight sandbox's rule set. The point of every assertion below is that the
    /// mode's promises cannot drift apart from each other: "no mission pressure" has to mean no
    /// hostiles AND no booking AND no mission rows AND no fuel clock, and the CAMPAIGN mode has to be
    /// left exactly as it was — a regression that quietly turned one of these on in the sandbox (or
    /// off in the campaign) fails here instead of in the save file.
    /// </summary>
    public class SimModeTests
    {
        [Test]
        public void OnlyFreeFlight_IsASandbox()
        {
            Assert.IsTrue(FreeFlightRules.IsSandbox(SimMode.FreeFlight));
            Assert.IsFalse(FreeFlightRules.IsSandbox(SimMode.Campaign));
        }

        [Test]
        public void Sandbox_HasNoHostilesNoBookingNoMissionRowsAndNoFuelClock()
        {
            Assert.IsFalse(FreeFlightRules.SpawnsHostiles(SimMode.FreeFlight));
            Assert.IsFalse(FreeFlightRules.TouchesCampaignSave(SimMode.FreeFlight));
            Assert.IsFalse(FreeFlightRules.ShowsMissionRows(SimMode.FreeFlight));
            Assert.IsFalse(FreeFlightRules.ConsumesFuel(SimMode.FreeFlight));
            Assert.IsFalse(FreeFlightRules.SpawnsWingmen(SimMode.FreeFlight));
        }

        [Test]
        public void Sandbox_StartsInTheCockpit_CampaignDoesNot()
        {
            Assert.IsTrue(FreeFlightRules.StartsUnderPlayerControl(SimMode.FreeFlight));
            Assert.IsFalse(FreeFlightRules.StartsUnderPlayerControl(SimMode.Campaign));
        }

        /// <summary>
        /// The campaign must keep every single one of the behaviours the sandbox drops. This is the
        /// test that makes the mode ADDITIVE: it cannot be implemented by turning something off
        /// globally.
        /// </summary>
        [Test]
        public void Campaign_KeepsEverythingTheSandboxDrops()
        {
            Assert.IsTrue(FreeFlightRules.SpawnsHostiles(SimMode.Campaign));
            Assert.IsTrue(FreeFlightRules.TouchesCampaignSave(SimMode.Campaign));
            Assert.IsTrue(FreeFlightRules.ShowsMissionRows(SimMode.Campaign));
            Assert.IsTrue(FreeFlightRules.ConsumesFuel(SimMode.Campaign));
            Assert.IsTrue(FreeFlightRules.SpawnsWingmen(SimMode.Campaign));
        }

        [Test]
        public void SandboxFliesTheJet_AndTheIdResolvesToARealProfile()
        {
            Assert.AreEqual(AircraftCatalog.FighterJetId, FreeFlightRules.AircraftId);

            AircraftProfile profile = AircraftCatalog.GetOrDefault(FreeFlightRules.AircraftId);
            Assert.IsNotNull(profile);
            Assert.AreEqual(AircraftKind.FighterJet, profile.Kind);
        }

        [Test]
        public void ModeHasATurkishTitleAndBrief()
        {
            Assert.IsNotEmpty(FreeFlightRules.Title);
            Assert.IsNotEmpty(FreeFlightRules.Brief);
        }
    }
}
