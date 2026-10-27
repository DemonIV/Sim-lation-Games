using Sim.Core;

namespace Sim.Runtime
{
    /// <summary>
    /// The player's persistent campaign state for this play session: progress, money and the garage.
    ///
    /// <para>
    /// STATIC on purpose, exactly like <see cref="ScenarioController.SelectedKind"/>:
    /// <see cref="SimulationBootstrap.Rebuild"/> destroys every component in the generated world, so
    /// the campaign has to outlive it. It is loaded lazily on first use and written back only at real
    /// save points — a level completed (<see cref="CompleteMission"/>) or a purchase made
    /// (<see cref="TryPurchase"/>) — never per frame.
    /// </para>
    ///
    /// <para>
    /// All rules live in <c>Sim.Core</c> (<see cref="CampaignProgress"/>, <see cref="Wallet"/>,
    /// <see cref="UpgradeState"/>, <see cref="CampaignReward"/>, <see cref="AircraftUpgrades"/>);
    /// this class only holds the instances, sequences the calls and persists the result through
    /// <see cref="CampaignSave"/>.
    /// </para>
    /// </summary>
    public static class CampaignSession
    {
        private static CampaignProgress _progress;
        private static Wallet _wallet;
        private static UpgradeState _upgrades;
        private static bool _loaded;

        /// <summary>Unlocks, completions and best grades.</summary>
        public static CampaignProgress Progress { get { EnsureLoaded(); return _progress; } }

        /// <summary>The player's money.</summary>
        public static Wallet Wallet { get { EnsureLoaded(); return _wallet; } }

        /// <summary>Purchased upgrade levels.</summary>
        public static UpgradeState Upgrades { get { EnsureLoaded(); return _upgrades; } }

        /// <summary>
        /// The 1-based level the player is flying (or about to fly). STATIC for the same reason as
        /// everything else here: it must survive a rebuild. Defaults to level 1.
        /// </summary>
        public static int SelectedLevelIndex = 1;

        /// <summary>The selected level, never null (an out-of-range index resolves to level 1).</summary>
        public static CampaignLevel SelectedLevel => CampaignLibrary.GetOrFirst(SelectedLevelIndex);

        /// <summary>
        /// What the player is doing right now: flying a campaign level, or free-flying the sandbox
        /// (see <see cref="SimMode"/> and <see cref="FreeFlightRules"/>).
        ///
        /// <para>
        /// STATIC for the same reason as <see cref="SelectedLevelIndex"/>: entering or leaving the
        /// sandbox goes through <see cref="SimulationBootstrap.Rebuild"/> (the two modes need
        /// different worlds), which destroys every component, so the choice has to outlive it.
        /// </para>
        ///
        /// <para>
        /// DELIBERATELY NOT PERSISTED. It is absent from <see cref="CampaignSaveData"/> and from
        /// <see cref="Save"/>, so a sandbox flight leaves no trace in <c>PlayerPrefs</c> and a
        /// relaunch always comes up on the campaign. That is the first of the two things that keep
        /// the mode out of the save; the second is that <see cref="CompleteMission"/> is never
        /// reached in it (see the guard there).
        /// </para>
        /// </summary>
        public static SimMode SelectedMode = SimMode.Campaign;

        /// <summary>True while the free-flight sandbox is the active mode.</summary>
        public static bool IsFreeFlight => FreeFlightRules.IsSandbox(SelectedMode);

        /// <summary>
        /// The profile the sandbox flies: <see cref="FreeFlightRules.AircraftId"/> (the fighter jet)
        /// with the garage's purchased upgrades folded in, exactly as <see cref="PlayerProfile"/>
        /// does for the campaign.
        ///
        /// <para>
        /// It READS the garage and never writes it, and it deliberately ignores — and leaves intact —
        /// the archetype the player picked on the menu for their campaign sorties. So the sandbox
        /// always puts the player in their own jet, upgrades included, without the mode becoming a
        /// second place where the aircraft choice is stored.
        /// </para>
        /// </summary>
        public static AircraftProfile FreeFlightProfile
        {
            get
            {
                return AircraftUpgrades.Apply(
                    AircraftCatalog.GetOrDefault(FreeFlightRules.AircraftId), Upgrades);
            }
        }

        /// <summary>
        /// The profile to actually spawn: the sandbox's jet in free flight, the player's campaign
        /// aircraft otherwise. One call site for the spawner, one place where the rule lives.
        /// </summary>
        public static AircraftProfile ActiveProfile => IsFreeFlight ? FreeFlightProfile : PlayerProfile;

        /// <summary>
        /// The profile the player's aircraft actually flies: the archetype picked on the menu with
        /// every purchased upgrade folded in. With an empty garage this is byte-for-byte the base
        /// profile, so a brand-new player flies exactly what <see cref="AircraftCatalog"/> defines.
        /// </summary>
        public static AircraftProfile PlayerProfile
        {
            get { return AircraftUpgrades.Apply(ScenarioController.SelectedAircraft, Upgrades); }
        }

        /// <summary>
        /// Loads the save on first use. Safe to call repeatedly; a missing or corrupt save simply
        /// leaves the freshly constructed (empty) state in place.
        /// </summary>
        public static void EnsureLoaded()
        {
            if (_loaded) return;
            _loaded = true;

            _progress = new CampaignProgress();
            _wallet = new Wallet();
            _upgrades = new UpgradeState();

            if (CampaignSave.Load(_progress, _wallet, _upgrades, out string aircraftId))
            {
                ScenarioController.SelectedAircraftId = aircraftId;
                SelectedLevelIndex = _progress.NextUnclearedLevel();
            }
        }

        /// <summary>Writes the current state to the save. Called from the save points below.</summary>
        public static void Save()
        {
            EnsureLoaded();
            CampaignSave.Save(_progress, _wallet, _upgrades, ScenarioController.SelectedAircraftId);
        }

        /// <summary>
        /// Picks the level to fly. Returns false (changing nothing) for an unknown or still-locked
        /// level, so the menu can never launch a mission the player has not unlocked.
        /// </summary>
        public static bool SelectLevel(int levelIndex)
        {
            EnsureLoaded();
            if (!_progress.IsUnlocked(levelIndex)) return false;
            SelectedLevelIndex = levelIndex;
            return true;
        }

        /// <summary>
        /// Buys the next level of <paramref name="track"/> and persists on success. Returns false —
        /// having changed nothing — when the track is maxed or the money is short.
        /// </summary>
        public static bool TryPurchase(UpgradeTrack track)
        {
            EnsureLoaded();
            if (!_upgrades.TryPurchase(track, _wallet)) return false;
            Save();
            return true;
        }

        /// <summary>
        /// Books a finished mission: pays the reward, marks a won level complete (which unlocks the
        /// next one) and saves. Returns the money earned, which the mission-report screen shows.
        ///
        /// <para>
        /// Idempotent per attempt: the caller must call this ONCE per mission end (see
        /// <see cref="Hud"/>), because a second call would be scored as another attempt — at the
        /// reduced replay rate, since the level is completed by then.
        /// </para>
        /// </summary>
        public static int CompleteMission(int levelIndex, bool won, int hostilesDestroyed,
                                          int friendliesLost, int stars)
        {
            // THE SANDBOX NEVER BOOKS. Free flight has no hostiles, no waves and no win/lose, so the
            // caller should never get here in that mode — the scenario simply stays InProgress
            // forever. This is the belt to that braces: even if some future path did reach this
            // method during a sandbox flight, it pays no credits, completes no level (and so unlocks
            // nothing) and writes no save. See FreeFlightRules.TouchesCampaignSave.
            if (!FreeFlightRules.TouchesCampaignSave(SelectedMode)) return 0;

            EnsureLoaded();

            CampaignLevel level = CampaignLibrary.Get(levelIndex);
            if (level == null) return 0;

            bool fullRate = CampaignReward.IsFullRate(won, _progress.IsCompleted(levelIndex));
            int money = CampaignReward.Money(level, hostilesDestroyed, friendliesLost, stars, fullRate);

            _wallet.Earn(money);
            if (won) _progress.Complete(levelIndex, stars);

            Save();
            return money;
        }

        /// <summary>
        /// The explicit reset path: wipes the save and starts a fresh campaign (level 1 only, empty
        /// purse, stock aircraft). Nothing else in the project calls this automatically.
        /// </summary>
        public static void ResetAll()
        {
            CampaignSave.Clear();

            _progress = new CampaignProgress();
            _wallet = new Wallet();
            _upgrades = new UpgradeState();
            _loaded = true;

            SelectedLevelIndex = 1;
            ScenarioController.SelectedAircraftId = AircraftCatalog.Default.Id;
        }
    }
}
