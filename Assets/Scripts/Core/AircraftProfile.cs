using System.Collections.Generic;
using UnityEngine;

namespace Sim.Core
{
    /// <summary>The flyable aircraft archetypes the player can pick on the mission-select screen.</summary>
    public enum AircraftKind
    {
        /// <summary>Savaş uçağı: fast, agile, gun-heavy, short on missiles and fuel.</summary>
        FighterJet,

        /// <summary>SİHA: the armed-UAV baseline every other profile is expressed against.</summary>
        Siha,

        /// <summary>Keşif İHA: slow, long-endurance, wide radar picture, weakly armed and fragile.</summary>
        Iha
    }

    /// <summary>
    /// An immutable performance profile for one flyable aircraft archetype.
    ///
    /// <para>
    /// EVERY tunable below has a real consumer in <c>Sim.Runtime</c> — nothing here is decoration:
    /// <c>MaxSpeed</c>/<c>TurnRateDeg</c> feed <see cref="FlightModel"/> through the İHA controller's
    /// serialized flight fields, <c>PilotMaxSpeed</c> is the top speed the human pilot can command,
    /// <c>CruiseAltitude</c> is the spawn (and therefore altitude-hold) height,
    /// <c>FuelCapacity</c>/<c>FuelBurnRate</c> build the <see cref="FuelTank"/>, the five gun values are
    /// exactly the arguments of the runtime gun turret's configure call (backing a
    /// <see cref="GunSystem"/>), <c>MissileCapacity</c>/<c>MissileRange</c> size the SİHA's
    /// <see cref="WeaponSystem"/> magazine and firing range, <c>DetectionRange</c> is the
    /// <see cref="TargetingSystem"/> range, <c>RadarRange</c> is the radar sensor's reference range
    /// (see <see cref="RadarSystem"/>) and <c>Health</c> is the hit-point pool.
    /// </para>
    ///
    /// <para>
    /// NOT MODELLED YET: the archetypes also differ in radar signature, but nothing in the simulation
    /// reads a FRIENDLY aircraft's <see cref="RadarCrossSection"/> — hostile sensors detect by plain
    /// range/FOV — so an RCS field would be dead weight. Making hostile sensors signature-aware is a
    /// separate change to enemy behaviour.
    /// </para>
    ///
    /// <para>
    /// The four 0..1 ratings exist purely so the selection UI can draw comparative bars without ever
    /// knowing the raw units.
    /// </para>
    ///
    /// This is a GAME / EDUCATIONAL model with abstract, gamified parameters. Pure logic.
    /// </summary>
    public class AircraftProfile
    {
        /// <summary>Stable identifier persisted across menu/rebuild cycles.</summary>
        public string Id { get; }

        /// <summary>Turkish name shown on the selection card.</summary>
        public string DisplayName { get; }

        /// <summary>One-line Turkish description shown on the selection card.</summary>
        public string Description { get; }

        /// <summary>Which airframe family this profile flies as.</summary>
        public AircraftKind Kind { get; }

        // ---------------------------------------------------------------- flight
        /// <summary>Flight-model top speed (m/s) — the AI envelope of this airframe.</summary>
        public float MaxSpeed { get; }

        /// <summary>Top speed (m/s) the human pilot can command in pilot mode.</summary>
        public float PilotMaxSpeed { get; }

        /// <summary>Flight-model turn-rate limit (deg/s).</summary>
        public float TurnRateDeg { get; }

        /// <summary>Spawn / altitude-hold height (m).</summary>
        public float CruiseAltitude { get; }

        // ---------------------------------------------------------------- endurance
        /// <summary>Fuel-tank capacity (abstract units).</summary>
        public float FuelCapacity { get; }

        /// <summary>Fuel burned per second at full throttle.</summary>
        public float FuelBurnRate { get; }

        // ---------------------------------------------------------------- gun
        /// <summary>Rounds in the gun belt.</summary>
        public int GunMagazine { get; }

        /// <summary>Gun rate of fire (rounds/s).</summary>
        public float GunRoundsPerSecond { get; }

        /// <summary>Gun effective range (m).</summary>
        public float GunRange { get; }

        /// <summary>Gun dispersion cone half-angle (deg) — smaller is more accurate.</summary>
        public float GunDispersionDeg { get; }

        /// <summary>Damage of a single gun round.</summary>
        public float GunDamage { get; }

        // ---------------------------------------------------------------- missiles
        /// <summary>Guided-munition magazine size. Zero means this archetype carries no missiles.</summary>
        public int MissileCapacity { get; }

        /// <summary>Guided-munition firing range (m). Meaningless when <see cref="MissileCapacity"/> is 0.</summary>
        public float MissileRange { get; }

        // ---------------------------------------------------------------- sensors & survivability
        /// <summary>Targeting/lock detection range (m).</summary>
        public float DetectionRange { get; }

        /// <summary>Radar reference range (m) against a 1 m² target — the size of the radar picture.</summary>
        public float RadarRange { get; }

        /// <summary>Hit points.</summary>
        public float Health { get; }

        // ---------------------------------------------------------------- display ratings (0..1)
        /// <summary>Comparative speed rating for the selection screen (0..1).</summary>
        public float SpeedRating { get; }

        /// <summary>Comparative agility rating for the selection screen (0..1).</summary>
        public float AgilityRating { get; }

        /// <summary>Comparative firepower rating for the selection screen (0..1).</summary>
        public float FirepowerRating { get; }

        /// <summary>Comparative endurance rating for the selection screen (0..1).</summary>
        public float EnduranceRating { get; }

        public AircraftProfile(string id, string displayName, string description, AircraftKind kind,
                               float maxSpeed, float pilotMaxSpeed, float turnRateDeg, float cruiseAltitude,
                               float fuelCapacity, float fuelBurnRate,
                               int gunMagazine, float gunRoundsPerSecond, float gunRange,
                               float gunDispersionDeg, float gunDamage,
                               int missileCapacity, float missileRange,
                               float detectionRange, float radarRange, float health,
                               float speedRating, float agilityRating,
                               float firepowerRating, float enduranceRating)
        {
            Id = id;
            DisplayName = displayName;
            Description = description;
            Kind = kind;

            MaxSpeed = maxSpeed;
            PilotMaxSpeed = pilotMaxSpeed;
            TurnRateDeg = turnRateDeg;
            CruiseAltitude = cruiseAltitude;

            FuelCapacity = fuelCapacity;
            FuelBurnRate = fuelBurnRate;

            GunMagazine = gunMagazine;
            GunRoundsPerSecond = gunRoundsPerSecond;
            GunRange = gunRange;
            GunDispersionDeg = gunDispersionDeg;
            GunDamage = gunDamage;

            MissileCapacity = missileCapacity;
            MissileRange = missileRange;

            DetectionRange = detectionRange;
            RadarRange = radarRange;
            Health = health;

            SpeedRating = Mathf.Clamp01(speedRating);
            AgilityRating = Mathf.Clamp01(agilityRating);
            FirepowerRating = Mathf.Clamp01(firepowerRating);
            EnduranceRating = Mathf.Clamp01(enduranceRating);
        }
    }

    /// <summary>
    /// The catalogue of flyable archetypes.
    ///
    /// <para>
    /// The SİHA entry is the 1.0× BASELINE: its numbers are exactly the values the simulation already
    /// shipped (armed-UAV flight envelope, tank, gun fit, missile magazine, radar and hit points), so a
    /// player who never touches the selector flies precisely what they flew before. The jet and the
    /// recon İHA are deliberate multipliers of that baseline — see the comments on each profile.
    /// </para>
    ///
    /// Pure logic; no Unity scene dependency.
    /// </summary>
    public static class AircraftCatalog
    {
        /// <summary>Stable id of the fighter-jet profile.</summary>
        public const string FighterJetId = "fighter_jet";

        /// <summary>Stable id of the SİHA (baseline) profile.</summary>
        public const string SihaId = "siha";

        /// <summary>Stable id of the recon-İHA profile.</summary>
        public const string IhaId = "iha";

        // CRUISE ALTITUDES: the whole band was translated +6 m (12/14/18 -> 18/20/24) so it clears
        // the scenery. The detailed buildings reach ~11 m above their own footing and the terrain adds
        // up to another 3 m, so the skyline tops out at 14 m — the old band ran straight through it.
        // 18 m is FlightEnvelope.MinCruiseAltitude (14 m skyline + 4 m margin); the relative spacing
        // between the three archetypes is unchanged, and FlightEnvelopeTests asserts the floor holds.

        // Baseline = today's armed SİHA: flight 30 m/s / 80 deg/s, pilot cap 40 m/s, cruise 20 m,
        // tank 100 @ 2/s, gun 300 rounds / 10 rps / 60 m / 2.5° / 4.5 dmg, 6 missiles at 120 m,
        // targeting 120 m, radar reference 250 m, 100 HP, nominal radar signature.
        private static readonly AircraftProfile _siha = new AircraftProfile(
            id: SihaId,
            displayName: "SİHA",
            description: "Dengeli silahlı İHA: bol füze, uzun menzil, her işte iyi.",
            kind: AircraftKind.Siha,
            maxSpeed: 30f, pilotMaxSpeed: 40f, turnRateDeg: 80f, cruiseAltitude: 20f,
            fuelCapacity: 100f, fuelBurnRate: 2f,
            gunMagazine: 300, gunRoundsPerSecond: 10f, gunRange: 60f,
            gunDispersionDeg: 2.5f, gunDamage: 4.5f,
            missileCapacity: 6, missileRange: 120f,
            detectionRange: 120f, radarRange: 250f, health: 100f,
            speedRating: 0.6f, agilityRating: 0.6f, firepowerRating: 0.9f, enduranceRating: 0.6f);

        // Jet = baseline × (speed 1.5, pilot cap 1.5, turn 1.25, tank 0.7, burn 1.6, gun rate 1.6,
        // gun damage ~1.33, gun range ~1.17, radar 0.8, detection ~0.83); 2 missiles instead of 6 and
        // slightly fewer hit points. Fast and hard-hitting, but short-legged and short-sighted.
        private static readonly AircraftProfile _fighterJet = new AircraftProfile(
            id: FighterJetId,
            displayName: "Savaş Uçağı",
            description: "Hızlı ve çevik avcı: güçlü top, az füze, çok yakıt yakar.",
            kind: AircraftKind.FighterJet,
            maxSpeed: 45f, pilotMaxSpeed: 60f, turnRateDeg: 100f, cruiseAltitude: 24f,
            fuelCapacity: 70f, fuelBurnRate: 3.2f,
            gunMagazine: 400, gunRoundsPerSecond: 16f, gunRange: 70f,
            gunDispersionDeg: 2f, gunDamage: 6f,
            missileCapacity: 2, missileRange: 100f,
            detectionRange: 100f, radarRange: 200f, health: 90f,
            speedRating: 1f, agilityRating: 0.9f, firepowerRating: 0.6f, enduranceRating: 0.35f);

        // Recon İHA = baseline × (speed 0.7, turn ~0.81, tank 1.8, burn 0.7, radar 1.4, detection 1.25)
        // with the recon drone's own existing light gun fit (200 rounds / 8 rps / 45 m / 3° / 3 dmg),
        // no missiles at all and 70 hit points. Loiters forever and sees furthest, but is weak and
        // fragile.
        private static readonly AircraftProfile _iha = new AircraftProfile(
            id: IhaId,
            displayName: "Keşif İHA",
            description: "Uzun havada kalış ve en geniş radar: zayıf top, ince zırh.",
            kind: AircraftKind.Iha,
            maxSpeed: 21f, pilotMaxSpeed: 28f, turnRateDeg: 65f, cruiseAltitude: 18f,
            fuelCapacity: 180f, fuelBurnRate: 1.4f,
            gunMagazine: 200, gunRoundsPerSecond: 8f, gunRange: 45f,
            gunDispersionDeg: 3f, gunDamage: 3f,
            missileCapacity: 0, missileRange: 0f,
            detectionRange: 150f, radarRange: 350f, health: 70f,
            speedRating: 0.35f, agilityRating: 0.45f, firepowerRating: 0.25f, enduranceRating: 1f);

        private static readonly AircraftProfile[] _all = { _fighterJet, _siha, _iha };

        /// <summary>Every selectable profile, in menu order (jet, SİHA, İHA).</summary>
        public static IReadOnlyList<AircraftProfile> All => _all;

        /// <summary>The profile a player who never touches the selector flies: the SİHA baseline.</summary>
        public static AircraftProfile Default => _siha;

        /// <summary>
        /// Looks a profile up by id. Returns false (never throws) for a null, empty or unknown id, and
        /// leaves <paramref name="profile"/> null so callers can fall back to <see cref="Default"/>.
        /// </summary>
        public static bool TryGet(string id, out AircraftProfile profile)
        {
            profile = null;
            if (string.IsNullOrEmpty(id)) return false;

            for (int i = 0; i < _all.Length; i++)
            {
                if (_all[i].Id != id) continue;
                profile = _all[i];
                return true;
            }
            return false;
        }

        /// <summary>
        /// Same as <see cref="TryGet"/> but always yields a usable profile: an unknown or empty id
        /// resolves to <see cref="Default"/>.
        /// </summary>
        public static AircraftProfile GetOrDefault(string id)
        {
            return TryGet(id, out AircraftProfile profile) ? profile : Default;
        }

        /// <summary>
        /// Steps <paramref name="delta"/> entries left/right from <paramref name="currentId"/>, wrapping
        /// around both ends (keyboard selection). An unknown id yields <see cref="Default"/>.
        /// </summary>
        public static AircraftProfile Cycle(string currentId, int delta)
        {
            int index = IndexOf(currentId);
            if (index < 0) return Default;

            int n = _all.Length;
            int next = ((index + delta) % n + n) % n;
            return _all[next];
        }

        /// <summary>Position of the given id in <see cref="All"/>, or -1 when it is unknown.</summary>
        private static int IndexOf(string id)
        {
            if (string.IsNullOrEmpty(id)) return -1;
            for (int i = 0; i < _all.Length; i++)
            {
                if (_all[i].Id == id) return i;
            }
            return -1;
        }
    }
}
