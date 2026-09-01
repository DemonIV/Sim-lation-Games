using System.Collections.Generic;
using UnityEngine;

namespace Sim.Core
{
    /// <summary>
    /// One level ("SEVİYE N") of the campaign.
    ///
    /// <para>
    /// A level is NOT a new scenario system — it is a THIN SELECTION on top of the existing one: it
    /// names a <see cref="ScenarioKind"/>, says how many waves of it to fly, and says how far into
    /// that scenario's own wave ramp to start (<see cref="StartWaveOffset"/>). Enemy composition
    /// therefore still comes from <see cref="ScenarioLibrary.Composition"/> (which for Mixed Defense
    /// delegates to <see cref="WavePlan"/>) — see <see cref="Composition"/>.
    /// </para>
    ///
    /// <para>
    /// <see cref="DifficultyMultiplier"/> and the reward parameters are pure economy values consumed
    /// by <see cref="CampaignReward"/>; they never touch enemy stats.
    /// </para>
    ///
    /// Immutable pure logic; no Unity scene dependency.
    /// </summary>
    public class CampaignLevel
    {
        /// <summary>1-based level number, as shown on the menu ("SEVİYE 1").</summary>
        public int Index { get; }

        /// <summary>Turkish level name.</summary>
        public string Name { get; }

        /// <summary>One-line Turkish briefing.</summary>
        public string Brief { get; }

        /// <summary>Which existing mission type this level flies.</summary>
        public ScenarioKind Scenario { get; }

        /// <summary>How many waves this level runs (at least 1).</summary>
        public int TotalWaves { get; }

        /// <summary>
        /// How far into the scenario's own wave ramp this level starts. Level 1 starts at 0 (the
        /// weakest wave the scenario can produce); later levels start deeper, so the very first wave
        /// of a late level is already as heavy as a late wave of an early one.
        /// </summary>
        public int StartWaveOffset { get; }

        /// <summary>Economy scale of this level (1.0 at level 1, rising). Reward only.</summary>
        public float DifficultyMultiplier { get; }

        /// <summary>Money paid for clearing the level at a 2-star grade, before the difficulty scale.</summary>
        public int BaseReward { get; }

        /// <summary>Money paid per destroyed hostile, before the difficulty scale.</summary>
        public int RewardPerKill { get; }

        /// <summary>Money deducted per friendly lost, before the difficulty scale.</summary>
        public int PenaltyPerLoss { get; }

        public CampaignLevel(int index, string name, string brief, ScenarioKind scenario,
                             int totalWaves, int startWaveOffset, float difficultyMultiplier,
                             int baseReward, int rewardPerKill, int penaltyPerLoss)
        {
            Index = Mathf.Max(1, index);
            Name = name;
            Brief = brief;
            Scenario = scenario;
            TotalWaves = Mathf.Max(1, totalWaves);
            StartWaveOffset = Mathf.Max(0, startWaveOffset);
            DifficultyMultiplier = Mathf.Max(0.1f, difficultyMultiplier);
            BaseReward = Mathf.Max(0, baseReward);
            RewardPerKill = Mathf.Max(0, rewardPerKill);
            PenaltyPerLoss = Mathf.Max(0, penaltyPerLoss);
        }

        /// <summary>
        /// Enemy mix of this level's <paramref name="waveIndex"/>'th wave (0-based). Delegates to
        /// <see cref="ScenarioLibrary.Composition"/> with the level's offset added, so the campaign
        /// reuses the shipped scenario/wave data instead of duplicating it.
        /// </summary>
        public WaveComposition Composition(int waveIndex)
        {
            return ScenarioLibrary.Composition(Scenario, StartWaveOffset + Mathf.Max(0, waveIndex));
        }

        /// <summary>Total hostiles this level spawns across all of its waves.</summary>
        public int TotalHostiles
        {
            get
            {
                int sum = 0;
                for (int w = 0; w < TotalWaves; w++) sum += Composition(w).Total;
                return sum;
            }
        }
    }

    /// <summary>
    /// The hand-authored campaign: an ordered list of <see cref="CampaignLevel"/>s.
    ///
    /// <para>
    /// THE RAMP IS EXPLICIT. Only the shape of each level is authored by hand (scenario, wave count,
    /// how deep into the wave ramp it starts, and its name/brief); every NUMBER is derived from the
    /// level index by the formulas below, so the curve can be read off in one place instead of being
    /// sprinkled across eight literals:
    /// </para>
    ///
    /// <list type="bullet">
    /// <item><c>DifficultyMultiplier = 1 + 0.25 × (index − 1)</c> — 1.00, 1.25, 1.50 … 2.75.</item>
    /// <item><c>BaseReward = 200 × DifficultyMultiplier</c> (rounded to 10).</item>
    /// <item><c>RewardPerKill = 25 × DifficultyMultiplier</c> (rounded, at least 25).</item>
    /// <item><c>PenaltyPerLoss = 60</c> — flat: losing a wingman hurts the same everywhere.</item>
    /// </list>
    ///
    /// <para>
    /// The authored shape ramps deliberately: levels 1–2 are ground-only recon (level 1 has no air
    /// defence at all, level 2 adds a single light AAA), level 3 introduces hostile fighters, level 4
    /// introduces SAM batteries, levels 5+ mix everything and start deeper in the wave ramp, and the
    /// last two are long, heavy missions.
    /// </para>
    ///
    /// Pure logic; no Unity scene dependency.
    /// </summary>
    public static class CampaignLibrary
    {
        /// <summary>Difficulty (economy) added per level above level 1.</summary>
        public const float DifficultyStep = 0.25f;

        /// <summary>Clear reward of a level-1 mission at a 2-star grade.</summary>
        public const int BaseRewardAtLevelOne = 200;

        /// <summary>Kill money on a level-1 mission.</summary>
        public const int RewardPerKillAtLevelOne = 25;

        /// <summary>Money lost per friendly destroyed — flat across the campaign.</summary>
        public const int PenaltyPerLoss = 60;

        /// <summary>The economy scale of the given 1-based level index.</summary>
        public static float DifficultyFor(int index)
        {
            return 1f + DifficultyStep * (Mathf.Max(1, index) - 1);
        }

        // Authored shape only — see the class remarks for where the numbers come from.
        private static readonly CampaignLevel[] _all =
        {
            Make(1, "İlk Sorti", "Hafif direniş, hava savunması yok. Uçuşu ve topu tanı.",
                 ScenarioKind.Recon, waves: 1, offset: 0),

            Make(2, "Saha Taraması", "Aynı saha, iki dalga: daha çok hedef ve ilk hafif uçaksavar.",
                 ScenarioKind.Recon, waves: 2, offset: 0),

            Make(3, "İlk Temas", "Düşman avcı drone'ları sahada. İlk hava muharebesi.",
                 ScenarioKind.AirCombat, waves: 2, offset: 0),

            Make(4, "Kalkanı Kır", "SAM ve AAA bataryaları devrede. Kaçış manevrasını öğren.",
                 ScenarioKind.Sead, waves: 3, offset: 0),

            Make(5, "Karma Cephe", "Yer hedefi, batarya ve avcı bir arada. Dalgalar daha derinden başlar.",
                 ScenarioKind.MixedDefense, waves: 3, offset: 1),

            Make(6, "Derin Nüfuz", "Yoğun hava savunması içinde uzun bir görev. Dört dalga.",
                 ScenarioKind.MixedDefense, waves: 4, offset: 2),

            Make(7, "Hava Üstünlüğü", "Sadece avcılar, ama çok sayıda. Saf hava muharebesi.",
                 ScenarioKind.AirCombat, waves: 4, offset: 2),

            Make(8, "Son Kale", "Kampanyanın en ağır sortisi: beş dalga, her şeyden en çok.",
                 ScenarioKind.MixedDefense, waves: 5, offset: 3)
        };

        /// <summary>Every level, in play order (level 1 first).</summary>
        public static IReadOnlyList<CampaignLevel> All => _all;

        /// <summary>Number of levels in the campaign.</summary>
        public static int Count => _all.Length;

        /// <summary>True when <paramref name="index"/> is a real 1-based level number.</summary>
        public static bool IsValidIndex(int index)
        {
            return index >= 1 && index <= _all.Length;
        }

        /// <summary>
        /// The level with the given 1-based index, or null when the index is out of range (never
        /// throws — callers fall back to <see cref="First"/>).
        /// </summary>
        public static CampaignLevel Get(int index)
        {
            return IsValidIndex(index) ? _all[index - 1] : null;
        }

        /// <summary>The first level, which is always unlocked.</summary>
        public static CampaignLevel First => _all[0];

        /// <summary>Same as <see cref="Get"/> but never null: an unknown index resolves to <see cref="First"/>.</summary>
        public static CampaignLevel GetOrFirst(int index)
        {
            CampaignLevel level = Get(index);
            return level != null ? level : First;
        }

        /// <summary>Builds a level, deriving every economy number from the index (see class remarks).</summary>
        private static CampaignLevel Make(int index, string name, string brief, ScenarioKind scenario,
                                          int waves, int offset)
        {
            float difficulty = DifficultyFor(index);
            int baseReward = Mathf.RoundToInt(BaseRewardAtLevelOne * difficulty / 10f) * 10;
            int perKill = Mathf.Max(RewardPerKillAtLevelOne,
                                    Mathf.RoundToInt(RewardPerKillAtLevelOne * difficulty));

            return new CampaignLevel(index, name, brief, scenario, waves, offset, difficulty,
                                     baseReward, perKill, PenaltyPerLoss);
        }
    }

    /// <summary>
    /// Turns a finished level into money. Built ON TOP of the existing scoring types — the caller
    /// passes the <see cref="MissionState"/> counters and the star rating that
    /// <see cref="MissionGrade.Stars"/> already produced; nothing is re-scored here.
    ///
    /// <para>
    /// REPLAYS ARE NOT A FAUCET: a level pays in full only the FIRST time it is cleared. Every later
    /// clear of the same level pays <see cref="ReplayFactor"/> (25%) of that, which keeps grinding an
    /// easy level far worse than pushing forward while still rewarding practice.
    /// </para>
    ///
    /// Pure logic; no Unity scene dependency.
    /// </summary>
    public static class CampaignReward
    {
        /// <summary>Share of the full reward paid when a level that was already completed is re-flown.</summary>
        public const float ReplayFactor = 0.25f;

        /// <summary>
        /// Multiplier applied to the level's <see cref="CampaignLevel.BaseReward"/> for a 0..3 star
        /// grade: 0★ pays no clear bonus at all (the mission was not won), 1★ 0.8, 2★ 1.0, 3★ 1.2.
        /// </summary>
        public static float StarFactor(int stars)
        {
            int s = Mathf.Clamp(stars, 0, 3);
            return s <= 0 ? 0f : 0.6f + 0.2f * s;
        }

        /// <summary>
        /// Money earned for one attempt at <paramref name="level"/>.
        ///
        /// <para>
        /// <c>(BaseReward × StarFactor + kills × RewardPerKill − losses × PenaltyPerLoss)
        /// × DifficultyMultiplier</c>, floored at 0, then scaled by <see cref="ReplayFactor"/> when
        /// <paramref name="firstClear"/> is false. A null level or a negative result pays 0 rather
        /// than throwing or handing out negative money.
        /// </para>
        /// </summary>
        public static int Money(CampaignLevel level, int hostilesDestroyed, int friendliesLost,
                                int stars, bool firstClear)
        {
            if (level == null) return 0;

            int kills = Mathf.Max(0, hostilesDestroyed);
            int losses = Mathf.Max(0, friendliesLost);

            float raw = level.BaseReward * StarFactor(stars)
                        + kills * level.RewardPerKill
                        - losses * level.PenaltyPerLoss;
            if (raw <= 0f) return 0;

            float scaled = raw * level.DifficultyMultiplier;
            if (!firstClear) scaled *= ReplayFactor;

            return Mathf.Max(0, Mathf.RoundToInt(scaled));
        }
    }
}
