using System;
using UnityEngine;
using Sim.Core;

namespace Sim.Runtime
{
    /// <summary>
    /// The on-disk shape of a save. A plain serializable DTO with public fields, because that is all
    /// <see cref="JsonUtility"/> understands — the Core types (<see cref="CampaignProgress"/>,
    /// <see cref="Wallet"/>, <see cref="UpgradeState"/>) stay free of Unity serialization attributes
    /// and this class maps between the two.
    ///
    /// <para>
    /// Booleans are stored as 0/1 ints on purpose: it keeps the JSON trivially readable and avoids
    /// relying on <see cref="JsonUtility"/>'s handling of <c>bool[]</c>.
    /// </para>
    /// </summary>
    [Serializable]
    public class CampaignSaveData
    {
        /// <summary>Save format version; a mismatch is treated as "no save" (a fresh start).</summary>
        public int version;

        public int balance;
        public int lifetimeEarned;
        public int highestUnlocked;

        /// <summary>One entry per campaign level: 1 = cleared.</summary>
        public int[] completed;

        /// <summary>One entry per campaign level: best 0..3 star grade.</summary>
        public int[] stars;

        /// <summary>One entry per <see cref="UpgradeCatalog.All"/> track: its purchased level.</summary>
        public int[] upgradeLevels;

        /// <summary>Last selected aircraft id (see <see cref="AircraftCatalog"/>).</summary>
        public string aircraftId;
    }

    /// <summary>
    /// Reads and writes the campaign save through <see cref="PlayerPrefs"/>.
    ///
    /// <para>
    /// TOTAL BY DESIGN: a missing, truncated, wrong-version or outright corrupt entry NEVER throws —
    /// <see cref="Load"/> simply reports false and leaves the passed-in Core objects at whatever
    /// fresh state they already hold. Every restored number is re-clamped by the Core types
    /// themselves (<c>CampaignProgress.Restore</c>, <c>Wallet.Restore</c>,
    /// <c>UpgradeState.Restore</c>), so a hand-edited file cannot produce an impossible state either.
    /// </para>
    ///
    /// <para>
    /// Writing is EXPLICIT and rare: callers save when a level is completed or a purchase is made —
    /// never per frame. <see cref="Clear"/> is the explicit reset path.
    /// </para>
    ///
    /// Thin I/O glue only; all the rules live in <c>Sim.Core</c>.
    /// </summary>
    public static class CampaignSave
    {
        /// <summary>PlayerPrefs key holding the JSON blob.</summary>
        public const string PrefsKey = "sim.campaign.save";

        /// <summary>Current save format version.</summary>
        public const int CurrentVersion = 1;

        /// <summary>True when a save entry exists at all (it may still be unreadable).</summary>
        public static bool HasSave()
        {
            return PlayerPrefs.HasKey(PrefsKey);
        }

        /// <summary>
        /// Fills <paramref name="progress"/>, <paramref name="wallet"/> and
        /// <paramref name="upgrades"/> from the stored save. Returns false (leaving them untouched)
        /// when there is nothing to load or the stored text cannot be parsed.
        /// <paramref name="aircraftId"/> is always set: to the stored id, or to the catalogue default.
        /// </summary>
        public static bool Load(CampaignProgress progress, Wallet wallet, UpgradeState upgrades,
                                out string aircraftId)
        {
            aircraftId = AircraftCatalog.Default.Id;

            CampaignSaveData data = ReadData();
            if (data == null) return false;

            if (progress != null)
                progress.Restore(ToBools(data.completed), data.stars, data.highestUnlocked);
            if (wallet != null)
                wallet.Restore(data.balance, data.lifetimeEarned);
            if (upgrades != null)
                upgrades.Restore(data.upgradeLevels);

            // Resolved defensively: an unknown stored id falls back to the default profile.
            aircraftId = AircraftCatalog.GetOrDefault(data.aircraftId).Id;
            return true;
        }

        /// <summary>Writes the current campaign state. Call at save points, never every frame.</summary>
        public static void Save(CampaignProgress progress, Wallet wallet, UpgradeState upgrades,
                                string aircraftId)
        {
            var data = new CampaignSaveData
            {
                version = CurrentVersion,
                balance = wallet != null ? wallet.Balance : 0,
                lifetimeEarned = wallet != null ? wallet.LifetimeEarned : 0,
                highestUnlocked = progress != null ? progress.HighestUnlocked : 1,
                completed = progress != null ? ToInts(progress.CompletionSnapshot()) : new int[0],
                stars = progress != null ? progress.StarsSnapshot() : new int[0],
                upgradeLevels = upgrades != null ? upgrades.Snapshot() : new int[0],
                aircraftId = AircraftCatalog.GetOrDefault(aircraftId).Id
            };

            try
            {
                PlayerPrefs.SetString(PrefsKey, JsonUtility.ToJson(data));
                PlayerPrefs.Save();
            }
            catch (Exception e)
            {
                // A failed write must never take the game down mid-mission.
                Debug.LogWarning($"[CampaignSave] kayıt yazılamadı: {e.Message}");
            }
        }

        /// <summary>The explicit reset path: deletes the save entry (the next load starts fresh).</summary>
        public static void Clear()
        {
            PlayerPrefs.DeleteKey(PrefsKey);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Parses the stored blob, or returns null for "nothing usable here" — missing key, empty
        /// string, unparsable JSON or a version this build does not understand.
        /// </summary>
        private static CampaignSaveData ReadData()
        {
            if (!PlayerPrefs.HasKey(PrefsKey)) return null;

            string json = PlayerPrefs.GetString(PrefsKey, string.Empty);
            if (string.IsNullOrEmpty(json)) return null;

            CampaignSaveData data;
            try
            {
                data = JsonUtility.FromJson<CampaignSaveData>(json);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[CampaignSave] kayıt bozuk, sıfırdan başlanıyor: {e.Message}");
                return null;
            }

            if (data == null) return null;
            if (data.version != CurrentVersion) return null;
            return data;
        }

        private static int[] ToInts(bool[] flags)
        {
            if (flags == null) return new int[0];
            var ints = new int[flags.Length];
            for (int i = 0; i < flags.Length; i++) ints[i] = flags[i] ? 1 : 0;
            return ints;
        }

        private static bool[] ToBools(int[] ints)
        {
            if (ints == null) return null;
            var flags = new bool[ints.Length];
            for (int i = 0; i < ints.Length; i++) flags[i] = ints[i] != 0;
            return flags;
        }
    }
}
