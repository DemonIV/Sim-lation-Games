using UnityEngine;

namespace Sim.Core
{
    /// <summary>
    /// The vertical envelope the simulation flies in: how high the world's scenery can reach, and the
    /// altitude an aircraft must cruise at to stay clear of it. Pure logic.
    ///
    /// <para>
    /// This exists because the cruise band used to be chosen independently of the skyline and the two
    /// overlapped: the detailed buildings reach ~11 m above their own footing, the terrain relief adds
    /// up to another 3 m, and the drones cruised at 10–14 m — so a tower top sat INSIDE the band the
    /// drones held. The numbers are stated once here and consumed by
    /// <see cref="AircraftCatalog"/> and the runtime spawners, so raising a building or the terrain
    /// amplitude immediately shows up as a failing envelope test instead of as drones flying through
    /// roofs.
    /// </para>
    /// </summary>
    public static class FlightEnvelope
    {
        /// <summary>
        /// Tallest scenery prop measured from its OWN footing, in metres.
        ///
        /// <para>
        /// Worst case is the tower archetype: plinth 0.50 + shaft 7.2 (max) + mast offset 2.30 + mast
        /// half-height 0.90 = 10.90 m. The mid-rise block's aerial reaches 10.54 m and the tallest
        /// conifer about 8.7 m, so 11 m bounds every prop the environment builder can produce.
        /// </para>
        /// </summary>
        public const float MaxStructureHeight = 11f;

        /// <summary>
        /// Vertical margin kept between the highest possible scenery top and the lowest cruise
        /// altitude, in metres. Four metres is roughly a drone wingspan's worth of daylight: enough
        /// that a mast never brushes an airframe, small enough that the aircraft still read as flying
        /// low over the landscape rather than above it.
        /// </summary>
        public const float StructureClearance = 4f;

        /// <summary>
        /// Highest point any scenery can occupy in world space: the terrain relief
        /// (<see cref="TerrainField.Amplitude"/>) under the tallest prop. 3 + 11 = 14 m.
        /// </summary>
        public static float MaxSkylineAltitude => TerrainField.Amplitude + MaxStructureHeight;

        /// <summary>
        /// Lowest altitude an aircraft may hold as its CRUISE height: the skyline plus the clearance
        /// margin. 14 + 4 = 18 m.
        ///
        /// <para>
        /// This is NOT the hard floor. A deliberate descent — an evasive dive, a strafing pass, or the
        /// dead-stick glide of an aircraft that ran its tank dry — is still allowed to go below it,
        /// down to each controller's own minimum-altitude floor. What this guarantees is that nothing
        /// flying its normal profile is ever driven into the skyline.
        /// </para>
        /// </summary>
        public static float MinCruiseAltitude => MaxSkylineAltitude + StructureClearance;

        /// <summary>True when the given cruise altitude clears the skyline by the full margin.</summary>
        public static bool ClearsStructures(float altitude)
        {
            return altitude >= MinCruiseAltitude;
        }

        /// <summary>
        /// The given altitude lifted up to <see cref="MinCruiseAltitude"/> when it sits below it, and
        /// returned unchanged otherwise. Used where a spawner staggers aircraft around a nominal
        /// cruise height and the lowest slot could otherwise dip into the margin.
        /// </summary>
        public static float ClampToCruiseFloor(float altitude)
        {
            return Mathf.Max(MinCruiseAltitude, altitude);
        }
    }
}
