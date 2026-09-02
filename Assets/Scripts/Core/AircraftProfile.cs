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
    /// (see <see cref="RadarSystem"/>), <c>RadarSignature</c> is the radar cross section the airframe
    /// presents to HOSTILE sensors and <c>Health</c> is the hit-point pool.
    /// </para>
    ///
    /// <para>
    /// <c>RadarSignature</c> is the newest of these and the one that makes the electronic-warfare
    /// layer matter to the player: the spawner hangs an <c>RcsComponent</c> carrying it on the
    /// aircraft, hostile sensors read it out of the detection snapshot, and
    /// <see cref="SignatureDetection"/> turns it into the distance at which that hostile actually
    /// picks the aircraft up. It is expressed in the same m² units
    /// <see cref="RadarCrossSection"/> already models, around its 1 m² nominal value.
    /// </para>
    ///
    /// <para>
    /// The five 0..1 ratings exist purely so the selection UI can draw comparative bars without ever
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

        /// <summary>
        /// How many BALİSTİK FÜZE rounds this airframe carries on its centreline station.
        ///
        /// <para>
        /// This is the fighter jet's exclusive heavy weapon: a lofted, unguided round that hits for
        /// <see cref="BallisticMissile.DamageMultiplier"/>× a normal missile (see
        /// <see cref="BallisticMissile"/>). ONLY the jet has the launcher — the SİHA and the recon İHA
        /// carry 0 and can never fire one, which is also why
        /// <see cref="AircraftUpgrades.Apply"/> only adds bought rounds to an airframe that already
        /// has the station.
        /// </para>
        /// </summary>
        public int BallisticRounds { get; }

        // ---------------------------------------------------------------- sensors & survivability
        /// <summary>Targeting/lock detection range (m).</summary>
        public float DetectionRange { get; }

        /// <summary>Radar reference range (m) against a 1 m² target — the size of the radar picture.</summary>
        public float RadarRange { get; }

        /// <summary>
        /// Nominal radar cross section (m²) this airframe presents to hostile sensors. The SİHA
        /// baseline is <see cref="SignatureDetection.BaselineRcs"/> (1 m²), which is exactly what
        /// every hostile detection range in the project is quoted against; a bigger value is picked
        /// up further out and a smaller one closer in, as the fourth root of the ratio.
        /// </summary>
        public float RadarSignature { get; }

        /// <summary>Hit points.</summary>
        public float Health { get; }

        /// <summary>
        /// Strength of the onboard noise jammer this aircraft flies with, or 0 when it carries none.
        ///
        /// <para>
        /// This is the ONE field no archetype sets: every catalogue profile leaves it at 0 and it is
        /// written solely by <see cref="AircraftUpgrades.Apply"/> from the hangar's
        /// <see cref="UpgradeTrack.ElectronicWarfare"/> track, so an aircraft only ever carries a
        /// jammer that was BOUGHT. The spawner reads it to decide whether to mount a <c>Jammer</c>
        /// component at all; <see cref="ElectronicWarfare.EffectiveRange"/> then divides every hostile
        /// detection range by <c>(1 + strength)^0.25</c> while a burst is radiating.
        /// </para>
        /// </summary>
        public float JammerStrength { get; }

        // ---------------------------------------------------------------- display ratings (0..1)
        /// <summary>Comparative speed rating for the selection screen (0..1).</summary>
        public float SpeedRating { get; }

        /// <summary>Comparative agility rating for the selection screen (0..1).</summary>
        public float AgilityRating { get; }

        /// <summary>Comparative firepower rating for the selection screen (0..1).</summary>
        public float FirepowerRating { get; }

        /// <summary>Comparative endurance rating for the selection screen (0..1).</summary>
        public float EnduranceRating { get; }

        /// <summary>
        /// Comparative STEALTH rating for the selection screen (0..1) — the inverse reading of
        /// <see cref="RadarSignature"/>, so that (like every other rating) MORE is better for the
        /// player: 1 is the hardest airframe to see, 0 the easiest.
        /// </summary>
        public float StealthRating { get; }

        public AircraftProfile(string id, string displayName, string description, AircraftKind kind,
                               float maxSpeed, float pilotMaxSpeed, float turnRateDeg, float cruiseAltitude,
                               float fuelCapacity, float fuelBurnRate,
                               int gunMagazine, float gunRoundsPerSecond, float gunRange,
                               float gunDispersionDeg, float gunDamage,
                               int missileCapacity, float missileRange,
                               float detectionRange, float radarRange, float radarSignature, float health,
                               float speedRating, float agilityRating,
                               float firepowerRating, float enduranceRating, float stealthRating,
                               float jammerStrength = 0f, int ballisticRounds = 0)
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
            // Optional (defaults to 0 = no launcher) for exactly the same reason jammerStrength is:
            // only ONE archetype carries the station, and every other call site is left untouched.
            BallisticRounds = Mathf.Max(0, ballisticRounds);

            DetectionRange = detectionRange;
            RadarRange = radarRange;
            RadarSignature = radarSignature;
            Health = health;
            // Optional (defaults to 0) so the three catalogue archetypes below — and every existing
            // call site — construct a jammer-less airframe without naming the argument at all.
            JammerStrength = Mathf.Max(0f, jammerStrength);

            SpeedRating = Mathf.Clamp01(speedRating);
            AgilityRating = Mathf.Clamp01(agilityRating);
            FirepowerRating = Mathf.Clamp01(firepowerRating);
            EnduranceRating = Mathf.Clamp01(enduranceRating);
            StealthRating = Mathf.Clamp01(stealthRating);
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

        // ENDURANCE: every tank was multiplied by 3 (SİHA 100 -> 300, jet 70 -> 210, recon İHA
        // 180 -> 540) with the BURN RATES untouched, because a sortie was running dry far too fast.
        // Full-throttle endurance was jet 70/3.2 = 21.9 s, SİHA 100/2 = 50 s, recon İHA 180/1.4 =
        // 128.6 s — and the afterburner burns 3x (jet: 7.3 s) and a radiating jammer 1.5x on top of
        // that. A campaign level, by contrast, is 2 hostiles on level 1 but 24 on level 5, 50 on
        // level 6 and 85 on level 8, spawned back to back with no inter-wave pause: at a realistic
        // ~10 s per kill that is roughly 20 s, 240 s, 500 s and 850 s of flying. The jet could not
        // finish level 1. It is now 65.6 / 150 / 385.7 s, so a leg covers several engagements plus
        // the trip home, and the base resupply cycle covers the rest of a long level. A UNIFORM
        // factor keeps the archetypes' ordering and every ratio exactly as authored (the recon İHA
        // still flies 5.9x as long as the jet), so the endurance RATINGS below are unchanged too.

        // SIGNATURES: expressed in the m² units Sim.Core.RadarCrossSection already models, whose
        // nominal BaseRcs is 1 m². The SİHA sits exactly on that 1 m² baseline, so every hostile
        // detection range in the project keeps meaning literally what it says. The other two are one
        // factor of FOUR either side (jet 4 m², recon İHA 0.25 m²), which the range equation's fourth
        // root turns into a clean ±√2 in detection distance: the jet is picked up ~41 % further out
        // than the SİHA, the recon İHA ~29 % closer in.

        // Baseline = today's armed SİHA: flight 30 m/s / 80 deg/s, pilot cap 40 m/s, cruise 20 m,
        // tank 300 @ 2/s (150 s of full throttle), gun 300 rounds / 10 rps / 60 m / 2.5° / 4.5 dmg,
        // 6 missiles at 120 m, targeting 120 m, radar reference 250 m, 1 m² signature, 100 HP.
        private static readonly AircraftProfile _siha = new AircraftProfile(
            id: SihaId,
            displayName: "SİHA",
            description: "Dengeli silahlı İHA: bol füze, uzun menzil, her işte iyi.",
            kind: AircraftKind.Siha,
            maxSpeed: 30f, pilotMaxSpeed: 40f, turnRateDeg: 80f, cruiseAltitude: 20f,
            fuelCapacity: 300f, fuelBurnRate: 2f,
            gunMagazine: 300, gunRoundsPerSecond: 10f, gunRange: 60f,
            gunDispersionDeg: 2.5f, gunDamage: 4.5f,
            missileCapacity: 6, missileRange: 120f,
            detectionRange: 120f, radarRange: 250f, radarSignature: 1f, health: 100f,
            speedRating: 0.6f, agilityRating: 0.6f, firepowerRating: 0.9f, enduranceRating: 0.6f,
            stealthRating: 0.55f);

        // Jet = baseline × (speed 1.5, pilot cap 1.5, turn 1.25, tank 0.7, burn 1.6, gun rate 1.6,
        // gun damage ~1.33, gun range ~1.17, radar 0.8, detection ~0.83, SIGNATURE 4); 2 missiles
        // instead of 6 and slightly fewer hit points. Fast and hard-hitting, but short-legged,
        // short-sighted and by far the easiest of the three to see coming.
        private static readonly AircraftProfile _fighterJet = new AircraftProfile(
            id: FighterJetId,
            displayName: "Savaş Uçağı",
            description: "Hızlı ve çevik avcı: güçlü top, az füze, çok yakıt yakar.",
            kind: AircraftKind.FighterJet,
            maxSpeed: 45f, pilotMaxSpeed: 60f, turnRateDeg: 100f, cruiseAltitude: 24f,
            fuelCapacity: 210f, fuelBurnRate: 3.2f,
            gunMagazine: 400, gunRoundsPerSecond: 16f, gunRange: 70f,
            gunDispersionDeg: 2f, gunDamage: 6f,
            missileCapacity: 2, missileRange: 100f,
            detectionRange: 100f, radarRange: 200f, radarSignature: 4f, health: 90f,
            speedRating: 1f, agilityRating: 0.9f, firepowerRating: 0.6f, enduranceRating: 0.35f,
            stealthRating: 0.2f,
            // The jet's EXCLUSIVE weapon: two centreline balistik füze rounds. The other two
            // archetypes leave this at its 0 default, so the launcher is the one thing only the jet
            // can bring to a sortie (and the one hangar track only the jet can spend money on).
            ballisticRounds: 2);

        // Recon İHA = baseline × (speed 0.7, turn ~0.81, tank 1.8, burn 0.7, radar 1.4, detection
        // 1.25, SIGNATURE 0.25) with the recon drone's own existing light gun fit (200 rounds / 8 rps
        // / 45 m / 3° / 3 dmg), no missiles at all and 70 hit points. Loiters forever, sees furthest
        // and is the hardest to see — but is weak and fragile.
        private static readonly AircraftProfile _iha = new AircraftProfile(
            id: IhaId,
            displayName: "Keşif İHA",
            description: "Uzun havada kalış ve en geniş radar: zayıf top, ince zırh.",
            kind: AircraftKind.Iha,
            maxSpeed: 21f, pilotMaxSpeed: 28f, turnRateDeg: 65f, cruiseAltitude: 18f,
            fuelCapacity: 540f, fuelBurnRate: 1.4f,
            gunMagazine: 200, gunRoundsPerSecond: 8f, gunRange: 45f,
            gunDispersionDeg: 3f, gunDamage: 3f,
            missileCapacity: 0, missileRange: 0f,
            detectionRange: 150f, radarRange: 350f, radarSignature: 0.25f, health: 70f,
            speedRating: 0.35f, agilityRating: 0.45f, firepowerRating: 0.25f, enduranceRating: 1f,
            stealthRating: 0.9f);

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
