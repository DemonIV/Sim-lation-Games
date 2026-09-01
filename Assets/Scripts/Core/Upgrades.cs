using System;
using UnityEngine;

namespace Sim.Core
{
    /// <summary>
    /// The hangar's upgrade tracks. Each one is bought level by level and moves ONE aspect of the
    /// player's aircraft (see <see cref="AircraftUpgrades.Apply"/>).
    /// </summary>
    public enum UpgradeTrack
    {
        /// <summary>Motor: top speed (AI envelope + pilot cap).</summary>
        Engine,

        /// <summary>Namlu gücü: gun damage per round.</summary>
        Gun,

        /// <summary>Yeni silahlar: extra missile racks (and their reach).</summary>
        Missiles,

        /// <summary>Kanat/çeviklik: turn rate.</summary>
        Agility,

        /// <summary>Gövde: hit points.</summary>
        Hull,

        /// <summary>Yakıt: tank capacity.</summary>
        Fuel,

        /// <summary>Radar: radar picture and targeting range.</summary>
        Radar
    }

    /// <summary>
    /// Static data for the upgrade tracks: Turkish name/description, how many levels each has, what
    /// one level is worth and what it costs.
    ///
    /// <para>
    /// THE COST CURVE IS A FORMULA, NOT A TABLE:
    /// <c>Cost(level) = round(BaseCost × Growth^(level−1) / 25) × 25</c>, with
    /// <see cref="Growth"/> = 1.6. Every level therefore costs ~60% more than the one before it, and
    /// prices land on tidy 25-money steps. Buying track level 5 costs roughly 6.5× its level 1.
    /// </para>
    ///
    /// <para>
    /// THE EFFECT CURVE IS ALSO A FORMULA: a track's level-<c>L</c> bonus is
    /// <c>L × <see cref="PerLevelGain"/>(track)</c> — linear, so the UI can state "+%X" and the
    /// player can add levels up mentally. <see cref="AircraftUpgrades.Apply"/> then applies that as a
    /// MULTIPLIER on the base profile.
    /// </para>
    ///
    /// Pure logic; no Unity scene dependency.
    /// </summary>
    public static class UpgradeCatalog
    {
        /// <summary>Cost growth per level (see the class remarks).</summary>
        public const float Growth = 1.6f;

        /// <summary>Prices are rounded to this step so the shop shows tidy numbers.</summary>
        public const int PriceStep = 25;

        private static readonly UpgradeTrack[] _all =
        {
            UpgradeTrack.Engine, UpgradeTrack.Gun, UpgradeTrack.Missiles,
            UpgradeTrack.Agility, UpgradeTrack.Hull, UpgradeTrack.Fuel, UpgradeTrack.Radar
        };

        /// <summary>Every track, in hangar display order.</summary>
        public static UpgradeTrack[] All => _all;

        /// <summary>Number of tracks — the length of an <see cref="UpgradeState"/>'s level array.</summary>
        public static int TrackCount => _all.Length;

        /// <summary>Turkish name shown on the hangar row.</summary>
        public static string Name(UpgradeTrack track)
        {
            switch (track)
            {
                case UpgradeTrack.Engine: return "Motor";
                case UpgradeTrack.Gun: return "Namlu Gücü";
                case UpgradeTrack.Missiles: return "Füze Yuvası";
                case UpgradeTrack.Agility: return "Kanat / Çeviklik";
                case UpgradeTrack.Hull: return "Gövde Zırhı";
                case UpgradeTrack.Fuel: return "Yakıt Tankı";
                default: return "Radar";
            }
        }

        /// <summary>One-line Turkish description of what the track does.</summary>
        public static string Description(UpgradeTrack track)
        {
            switch (track)
            {
                case UpgradeTrack.Engine: return "Azami ve pilot hızını artırır.";
                case UpgradeTrack.Gun: return "Her top mermisinin hasarını artırır.";
                case UpgradeTrack.Missiles: return "Ek füze yuvası açar, füze menzilini uzatır.";
                case UpgradeTrack.Agility: return "Dönüş hızını artırır: daha sert manevra.";
                case UpgradeTrack.Hull: return "Dayanıklılığı (can) artırır.";
                case UpgradeTrack.Fuel: return "Depo hacmini büyütür: daha uzun sorti.";
                default: return "Radar ve tespit menzilini genişletir.";
            }
        }

        /// <summary>How many levels this track can be bought to.</summary>
        public static int MaxLevel(UpgradeTrack track)
        {
            switch (track)
            {
                case UpgradeTrack.Missiles: return 3;   // each level is a whole extra rack
                case UpgradeTrack.Fuel: return 4;
                case UpgradeTrack.Radar: return 4;
                default: return 5;
            }
        }

        /// <summary>Price of this track's FIRST level; later levels grow by <see cref="Growth"/>.</summary>
        public static int BaseCost(UpgradeTrack track)
        {
            switch (track)
            {
                case UpgradeTrack.Engine: return 300;
                case UpgradeTrack.Gun: return 250;
                case UpgradeTrack.Missiles: return 400;
                case UpgradeTrack.Agility: return 250;
                case UpgradeTrack.Hull: return 300;
                case UpgradeTrack.Fuel: return 200;
                default: return 200;
            }
        }

        /// <summary>
        /// Fractional improvement one level of this track is worth (linear: level L gives
        /// <c>L × PerLevelGain</c>). Meaningless for <see cref="UpgradeTrack.Missiles"/>'s capacity,
        /// which is a whole extra rack per level; it is the missile RANGE gain.
        /// </summary>
        public static float PerLevelGain(UpgradeTrack track)
        {
            switch (track)
            {
                case UpgradeTrack.Engine: return 0.08f;
                case UpgradeTrack.Gun: return 0.12f;
                case UpgradeTrack.Missiles: return 0.10f;
                case UpgradeTrack.Agility: return 0.07f;
                case UpgradeTrack.Hull: return 0.10f;
                case UpgradeTrack.Fuel: return 0.15f;
                default: return 0.12f;
            }
        }

        /// <summary>The multiplier a track applies at the given level: <c>1 + level × PerLevelGain</c>.</summary>
        public static float Multiplier(UpgradeTrack track, int level)
        {
            int l = Mathf.Clamp(level, 0, MaxLevel(track));
            return 1f + l * PerLevelGain(track);
        }

        /// <summary>
        /// Cost of buying the given 1-based <paramref name="level"/> of this track. Returns 0 for a
        /// level of 0 or below, or above <see cref="MaxLevel"/> (nothing left to buy) — never throws.
        /// </summary>
        public static int CostOfLevel(UpgradeTrack track, int level)
        {
            if (level < 1 || level > MaxLevel(track)) return 0;

            float raw = BaseCost(track) * Mathf.Pow(Growth, level - 1);
            int steps = Mathf.Max(1, Mathf.RoundToInt(raw / PriceStep));
            return steps * PriceStep;
        }

        /// <summary>
        /// Turkish one-liner describing what the track is worth AT the given level ("+%16 hız",
        /// "+2 füze"). Level 0 reads as "temel" (stock).
        /// </summary>
        public static string EffectSummary(UpgradeTrack track, int level)
        {
            int l = Mathf.Clamp(level, 0, MaxLevel(track));
            if (l <= 0) return "temel";

            if (track == UpgradeTrack.Missiles)
                return $"+{l} füze  ·  +%{Mathf.RoundToInt(PerLevelGain(track) * l * 100f)} menzil";

            int pct = Mathf.RoundToInt(PerLevelGain(track) * l * 100f);
            switch (track)
            {
                case UpgradeTrack.Engine: return $"+%{pct} hız";
                case UpgradeTrack.Gun: return $"+%{pct} hasar";
                case UpgradeTrack.Agility: return $"+%{pct} dönüş";
                case UpgradeTrack.Hull: return $"+%{pct} can";
                case UpgradeTrack.Fuel: return $"+%{pct} yakıt";
                default: return $"+%{pct} menzil";
            }
        }
    }

    /// <summary>
    /// Which upgrade track sits at which level. A fresh state is all zeros — a brand-new player flies
    /// exactly what <see cref="AircraftProfile"/> defines.
    ///
    /// <para>
    /// <see cref="TryPurchase"/> is ATOMIC: it checks the max level and the funds FIRST and only then
    /// spends and advances, so a rejected purchase leaves both the wallet and this state untouched.
    /// </para>
    ///
    /// Pure logic; no Unity serialization attributes (the Runtime save layer maps to its own DTO).
    /// </summary>
    public class UpgradeState
    {
        private readonly int[] _levels = new int[UpgradeCatalog.TrackCount];

        /// <summary>Current level of a track (0 = stock).</summary>
        public int LevelOf(UpgradeTrack track)
        {
            int i = IndexOf(track);
            return i < 0 ? 0 : _levels[i];
        }

        /// <summary>True when the track cannot be bought any further.</summary>
        public bool IsMaxed(UpgradeTrack track)
        {
            return LevelOf(track) >= UpgradeCatalog.MaxLevel(track);
        }

        /// <summary>Price of the next level of this track, or 0 when it is already maxed.</summary>
        public int NextCost(UpgradeTrack track)
        {
            if (IsMaxed(track)) return 0;
            return UpgradeCatalog.CostOfLevel(track, LevelOf(track) + 1);
        }

        /// <summary>Sum of every track's level — a compact "how upgraded am I" figure for the HUD.</summary>
        public int TotalLevels
        {
            get
            {
                int sum = 0;
                for (int i = 0; i < _levels.Length; i++) sum += _levels[i];
                return sum;
            }
        }

        /// <summary>
        /// Buys the next level of <paramref name="track"/> out of <paramref name="wallet"/>.
        /// Returns false and changes NOTHING when the wallet is null, the track is already maxed, or
        /// the balance does not cover the price.
        /// </summary>
        public bool TryPurchase(UpgradeTrack track, Wallet wallet)
        {
            if (wallet == null) return false;

            int i = IndexOf(track);
            if (i < 0) return false;
            if (IsMaxed(track)) return false;

            int cost = NextCost(track);
            if (!wallet.CanAfford(cost)) return false;

            // Funds and level headroom are both confirmed above, so this pair cannot half-apply.
            if (!wallet.TrySpend(cost)) return false;
            _levels[i]++;
            return true;
        }

        /// <summary>
        /// Restores saved levels defensively: a short, long or nonsense array still yields a valid
        /// state (every entry clamped to 0..MaxLevel of its own track). A null array resets to stock.
        /// </summary>
        public void Restore(int[] levels)
        {
            for (int i = 0; i < _levels.Length; i++)
            {
                int stored = levels != null && i < levels.Length ? levels[i] : 0;
                _levels[i] = Mathf.Clamp(stored, 0, UpgradeCatalog.MaxLevel(UpgradeCatalog.All[i]));
            }
        }

        /// <summary>Copy of the level array (indexed like <see cref="UpgradeCatalog.All"/>), for saving.</summary>
        public int[] Snapshot()
        {
            var copy = new int[_levels.Length];
            for (int i = 0; i < _levels.Length; i++) copy[i] = _levels[i];
            return copy;
        }

        /// <summary>Position of a track in <see cref="UpgradeCatalog.All"/>, or -1 when unknown.</summary>
        private static int IndexOf(UpgradeTrack track)
        {
            UpgradeTrack[] all = UpgradeCatalog.All;
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] == track) return i;
            }
            return -1;
        }
    }

    /// <summary>
    /// Folds an <see cref="UpgradeState"/> into an <see cref="AircraftProfile"/>.
    ///
    /// <para>
    /// EVERY effect is MULTIPLICATIVE ON THE BASE PROFILE, so the catalogue profiles stay the single
    /// source of truth: a state with no purchases returns a profile whose every field equals the base
    /// one, and applying the same state twice to the SAME base gives the same answer (the function is
    /// pure — it never mutates its input).
    /// </para>
    ///
    /// <para>
    /// The one non-multiplicative effect is the missile rack, which is a COUNT: each level adds one
    /// missile. A gun-only airframe (base capacity 0, base range 0) therefore gains a genuinely new
    /// weapon; because a 0 m range cannot be scaled up, its reach falls back to the airframe's own
    /// detection range — but only once at least one rack has been bought, so a stock profile is still
    /// returned byte-for-byte.
    /// </para>
    ///
    /// <para>
    /// The five 0..1 display ratings are passed through untouched: they compare ARCHETYPES on the
    /// selection screen and are not a readout of the player's garage. So is the airframe's radar
    /// signature — no upgrade track makes an aircraft physically smaller.
    /// </para>
    ///
    /// Pure logic; no Unity scene dependency.
    /// </summary>
    public static class AircraftUpgrades
    {
        /// <summary>
        /// Returns <paramref name="baseProfile"/> with every purchased upgrade applied. A null state
        /// (or one with no purchases) returns a profile equal to the base; a null base profile falls
        /// back to <see cref="AircraftCatalog.Default"/> rather than throwing.
        /// </summary>
        public static AircraftProfile Apply(AircraftProfile baseProfile, UpgradeState upgrades)
        {
            AircraftProfile b = baseProfile != null ? baseProfile : AircraftCatalog.Default;
            if (upgrades == null) return b;

            float speed = UpgradeCatalog.Multiplier(UpgradeTrack.Engine, upgrades.LevelOf(UpgradeTrack.Engine));
            float gun = UpgradeCatalog.Multiplier(UpgradeTrack.Gun, upgrades.LevelOf(UpgradeTrack.Gun));
            float turn = UpgradeCatalog.Multiplier(UpgradeTrack.Agility, upgrades.LevelOf(UpgradeTrack.Agility));
            float hull = UpgradeCatalog.Multiplier(UpgradeTrack.Hull, upgrades.LevelOf(UpgradeTrack.Hull));
            float fuel = UpgradeCatalog.Multiplier(UpgradeTrack.Fuel, upgrades.LevelOf(UpgradeTrack.Fuel));
            float radar = UpgradeCatalog.Multiplier(UpgradeTrack.Radar, upgrades.LevelOf(UpgradeTrack.Radar));

            int missileLevel = upgrades.LevelOf(UpgradeTrack.Missiles);
            int missileCapacity = b.MissileCapacity + missileLevel;
            float missileRange = b.MissileRange;
            if (missileLevel > 0)
            {
                // A stock rack scales; a brand-new one reaches as far as the airframe can see.
                float reference = b.MissileRange > 0f ? b.MissileRange : b.DetectionRange;
                missileRange = reference * UpgradeCatalog.Multiplier(UpgradeTrack.Missiles, missileLevel);
            }

            return new AircraftProfile(
                id: b.Id,
                displayName: b.DisplayName,
                description: b.Description,
                kind: b.Kind,
                maxSpeed: b.MaxSpeed * speed,
                pilotMaxSpeed: b.PilotMaxSpeed * speed,
                turnRateDeg: b.TurnRateDeg * turn,
                cruiseAltitude: b.CruiseAltitude,
                fuelCapacity: b.FuelCapacity * fuel,
                fuelBurnRate: b.FuelBurnRate,
                gunMagazine: b.GunMagazine,
                gunRoundsPerSecond: b.GunRoundsPerSecond,
                gunRange: b.GunRange,
                gunDispersionDeg: b.GunDispersionDeg,
                gunDamage: b.GunDamage * gun,
                missileCapacity: missileCapacity,
                missileRange: missileRange,
                detectionRange: b.DetectionRange * radar,
                radarRange: b.RadarRange * radar,
                // The garage sells SENSORS, not stealth: no track shrinks the airframe's own radar
                // signature, so it is passed through untouched (like the display ratings below).
                radarSignature: b.RadarSignature,
                health: b.Health * hull,
                speedRating: b.SpeedRating,
                agilityRating: b.AgilityRating,
                firepowerRating: b.FirepowerRating,
                enduranceRating: b.EnduranceRating,
                stealthRating: b.StealthRating);
        }

        /// <summary>
        /// Which profile FIELDS a track touches, for the hangar UI and for tests that assert a track
        /// moves nothing else. Returns an empty array for an unknown track.
        /// </summary>
        public static string[] AffectedFields(UpgradeTrack track)
        {
            switch (track)
            {
                case UpgradeTrack.Engine: return new[] { "MaxSpeed", "PilotMaxSpeed" };
                case UpgradeTrack.Gun: return new[] { "GunDamage" };
                case UpgradeTrack.Missiles: return new[] { "MissileCapacity", "MissileRange" };
                case UpgradeTrack.Agility: return new[] { "TurnRateDeg" };
                case UpgradeTrack.Hull: return new[] { "Health" };
                case UpgradeTrack.Fuel: return new[] { "FuelCapacity" };
                case UpgradeTrack.Radar: return new[] { "DetectionRange", "RadarRange" };
                default: return Array.Empty<string>();
            }
        }
    }
}
