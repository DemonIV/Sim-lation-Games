namespace Sim.Core
{
    /// <summary>
    /// What the player is actually doing in the simulation right now.
    ///
    /// <para>
    /// This is NOT a difficulty, a scenario or a campaign level — it is one level ABOVE all of those:
    /// <see cref="Campaign"/> means "fly a <see cref="CampaignLevel"/>", with waves, win/lose,
    /// scoring and an economy; <see cref="FreeFlight"/> means "just fly", with none of them. Keeping
    /// the distinction in a single enum is what lets every consumer ask one question instead of
    /// inferring the answer from an empty wave list or a null level.
    /// </para>
    /// </summary>
    public enum SimMode
    {
        /// <summary>The campaign: levels, waves, win/lose, stars, credits and the hangar.</summary>
        Campaign,

        /// <summary>"Serbest Uçuş": an empty sandbox world with an aircraft in it and nothing else.</summary>
        FreeFlight
    }

    /// <summary>
    /// The RULES of the free-flight sandbox, stated once.
    ///
    /// <para>
    /// Every one of these is a question some runtime system would otherwise answer with its own local
    /// <c>if</c>, and the whole point of the mode is that those answers must not disagree: a sandbox
    /// that pays no credits but still unlocks a level, or hides the score row but still books a
    /// mission result, is worse than no sandbox at all. So the policy lives here as pure logic with
    /// tests on it, and <c>Sim.Runtime</c> only ever asks.
    /// </para>
    ///
    /// <para>
    /// THE SAVE IS THE IMPORTANT ONE. <see cref="TouchesCampaignSave"/> is false for
    /// <see cref="SimMode.FreeFlight"/>, and the runtime honours it by never reaching
    /// <c>CampaignSession.CompleteMission</c> in that mode — which is the single function that pays
    /// money, marks a level complete (unlocking the next) and writes <c>PlayerPrefs</c>. A sandbox
    /// flight therefore cannot advance, enrich or corrupt a campaign, however long it lasts.
    /// </para>
    ///
    /// Pure logic; no Unity scene dependency.
    /// </summary>
    public static class FreeFlightRules
    {
        /// <summary>Turkish name of the mode, as the menu card and the HUD title show it.</summary>
        public const string Title = "SERBEST UÇUŞ";

        /// <summary>One-line Turkish briefing for the menu card.</summary>
        public const string Brief =
            "Düşman yok, dalga yok, görev yok. Geniş bir arazide savaş uçağıyla serbest uçuş.";

        /// <summary>
        /// The archetype the sandbox flies: the fighter jet, because "bir jet ile uçmak" is what the
        /// mode was asked for. Deliberately NOT the menu's aircraft selection — the sandbox reads this
        /// id and leaves the campaign's own choice exactly as the player left it.
        /// </summary>
        public const string AircraftId = AircraftCatalog.FighterJetId;

        /// <summary>True for the one mode that is a sandbox.</summary>
        public static bool IsSandbox(SimMode mode)
        {
            return mode == SimMode.FreeFlight;
        }

        /// <summary>
        /// Whether hostiles, waves and win/lose run at all. False in the sandbox, which is what keeps
        /// the scenario controller idle instead of having it spawn an empty wave list.
        /// </summary>
        public static bool SpawnsHostiles(SimMode mode)
        {
            return !IsSandbox(mode);
        }

        /// <summary>
        /// Whether a mission result may be BOOKED — paid out, graded, level-completed and saved.
        /// False in the sandbox. See the class remarks: this is the flag that keeps the mode out of
        /// the campaign save.
        /// </summary>
        public static bool TouchesCampaignSave(SimMode mode)
        {
            return !IsSandbox(mode);
        }

        /// <summary>
        /// Whether the HUD draws the mission-specific rows (status, wave, hostile count, kills,
        /// losses, score) and the end-of-mission report. False in the sandbox: the flight instruments
        /// stay, the mission bookkeeping goes.
        /// </summary>
        public static bool ShowsMissionRows(SimMode mode)
        {
            return !IsSandbox(mode);
        }

        /// <summary>
        /// Whether the tank actually empties. False in the sandbox — fuel is DISABLED rather than
        /// merely enlarged, because a sandbox whose only hard ending is a dead-stick crash at minute
        /// eleven is still a session with a timer on it. Weapons stay live; only the clock goes.
        /// </summary>
        public static bool ConsumesFuel(SimMode mode)
        {
            return !IsSandbox(mode);
        }

        /// <summary>
        /// Whether the player starts already flying the aircraft. True in the sandbox: there is
        /// nothing to brief and nobody to hand over from, so the mode begins in the cockpit instead of
        /// waiting for the take-over key.
        /// </summary>
        public static bool StartsUnderPlayerControl(SimMode mode)
        {
            return IsSandbox(mode);
        }

        /// <summary>
        /// Whether AI wingmen are spawned alongside the player. False in the sandbox: a wingman flying
        /// a 30 m patrol box in a 1.2 km world is scenery, and the squad panel reads more honestly
        /// with one aircraft in it than with two that are not doing anything.
        /// </summary>
        public static bool SpawnsWingmen(SimMode mode)
        {
            return !IsSandbox(mode);
        }
    }
}
