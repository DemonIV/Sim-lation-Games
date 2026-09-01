using System.Collections.Generic;
using UnityEngine;
using Sim.Core;

namespace Sim.Runtime
{
    /// <summary>
    /// Mission-select / briefing screen drawn with IMGUI (<see cref="OnGUI"/>), so it needs zero
    /// Canvas or scene setup — <see cref="SimulationBootstrap"/> just adds the component.
    ///
    /// <para>
    /// It opens at launch, freezes the sim with <c>Time.timeScale = 0</c> and lists every mission in
    /// <see cref="ScenarioLibrary.All"/>. Picking one writes
    /// <see cref="ScenarioController.SelectedKind"/> and calls
    /// <see cref="ScenarioController.BeginMission"/>, which is what actually releases the
    /// <see cref="ScenarioController"/> to start spawning waves.
    /// </para>
    ///
    /// <para>
    /// Pressing <c>M</c> during a mission reopens it. Choosing from there cannot simply restart the
    /// scenario in place — the field is already full of hostiles and the drones are worn down — so it
    /// rebuilds the generated world through the same path <see cref="GameControls"/> uses for
    /// <c>R</c> (<see cref="SimulationBootstrap.Rebuild"/>). The chosen mission survives that rebuild
    /// because <c>SelectedKind</c> is static.
    /// </para>
    ///
    /// <para>
    /// PRESENTATION: styled after <c>docs/design/hud/MissionSelect.dc.html</c> and drawn entirely
    /// with <see cref="HudTheme"/> (same palette, panels, hairline borders and tags as the combat
    /// HUD) — dark full-screen backdrop, centred heading, a row of mission cards with a briefing
    /// line, wave count and a three-block difficulty indicator, and the controls legend at the
    /// bottom. Same typography caveat as the rest of the HUD: no font assets and no letter-spacing
    /// in IMGUI, so the type only approximates the mockup.
    /// </para>
    ///
    /// <para>
    /// Beneath the missions sits the AIRCRAFT row: one card per <see cref="AircraftCatalog"/> profile
    /// (name, one-line description and the profile's 0..1 ratings as <see cref="HudTheme.Bar"/>
    /// gauges), drawn with the same card treatment as the missions so the screen reads as one design.
    /// Clicking a card — or the ←/→ keys, which are bound nowhere else — writes
    /// <see cref="ScenarioController.SelectedAircraftId"/>. The choice only reaches the field when the
    /// world is (re)built, which is exactly what picking a mission does.
    /// </para>
    ///
    /// Because the menu runs at <c>timeScale == 0</c> it never reads <see cref="Time.deltaTime"/>.
    /// </summary>
    public class ScenarioMenu : MonoBehaviour
    {
        // Layout constants (design: 1600x900 artboard, scaled to the actual screen).
        private const float CardGap = 14f;
        private const float CardHeaderH = 20f;
        private const float CardActionH = 20f;
        private const float CardFooterH = 26f;
        private const float LegendH = 92f;
        private const float FooterH = 24f;

        // Aircraft row: header (20) + description (26+4) + four 9px gauges with 5px gaps = ~106px of
        // content, so 120 is the smallest height that never clips a card.
        private const float AircraftRowH = 120f;
        private const float AircraftLabelH = 16f;
        private const float MinMissionCardH = 150f;
        private const float RatingBarH = 9f;
        private const float RatingBarGap = 5f;

        private ScenarioController _controller;

        /// <summary>
        /// Word-wrapped briefing text. The theme's <c>Small</c> face with wrapping turned on —
        /// a local variant of the shared style, not a second palette.
        /// </summary>
        private GUIStyle _brief;

        /// <summary>True while the menu is showing and the sim is held paused.</summary>
        public bool IsOpen { get; private set; }

        /// <summary>
        /// True once the player has picked a mission at least once with THIS component. Used to tell a
        /// fresh launch (just begin the mission) apart from a mid-mission reopen (rebuild the world).
        /// </summary>
        private bool _missionStarted;

        /// <summary>
        /// Set just before the mid-mission rebuild and consumed by <see cref="Start"/> on the freshly
        /// built menu, so the player does not have to pick the same mission twice. STATIC for the same
        /// reason as <see cref="ScenarioController.SelectedKind"/>: it must survive the rebuild that
        /// destroys this component. <see cref="SimulationBootstrap.Rebuild"/> leaves it alone on
        /// purpose.
        /// </summary>
        private static bool _autoBegin;

        private void Start()
        {
            _controller = FindAnyObjectByType<ScenarioController>();

            if (_autoBegin)
            {
                // Coming back from a mission change: the choice was already made, so skip the briefing
                // and launch straight into the freshly rebuilt field. Note this menu is a NEW component
                // built by SimulationBootstrap.Rebuild — the one the player clicked is already gone.
                _autoBegin = false;
                _missionStarted = true;
                IsOpen = false;
                Time.timeScale = 1f;
                if (_controller != null) _controller.BeginMission();
                return;
            }

            // Open on launch and hold the sim until a mission is chosen.
            IsOpen = true;
            Time.timeScale = 0f;
        }

        private void Update()
        {
            // Defensive: the controller may not have existed yet when Start ran.
            if (_controller == null) _controller = FindAnyObjectByType<ScenarioController>();

            if (IsOpen)
            {
                // Keep the sim frozen while the briefing is up, even if something else (e.g. the P
                // key in GameControls) changed the time scale underneath us.
                if (Time.timeScale != 0f) Time.timeScale = 0f;
                HandleAircraftKeys();
                return;
            }

            // M reopens the briefing mid-mission (and pauses). Only handled while closed, so the key
            // can never dismiss the menu without a mission being chosen.
            if (Input.GetKeyDown(KeyCode.M))
            {
                IsOpen = true;
                Time.timeScale = 0f;
            }
        }

        /// <summary>
        /// ←/→ step through the aircraft catalogue while the briefing is up. Those two keys are used
        /// nowhere else in the project (the pilot pitches with ↑/↓, the camera flies with WASD), so
        /// this cannot collide with an existing binding.
        /// </summary>
        private static void HandleAircraftKeys()
        {
            int delta = 0;
            if (Input.GetKeyDown(KeyCode.RightArrow)) delta = 1;
            else if (Input.GetKeyDown(KeyCode.LeftArrow)) delta = -1;
            if (delta == 0) return;

            ScenarioController.SelectedAircraftId =
                AircraftCatalog.Cycle(ScenarioController.SelectedAircraftId, delta).Id;
        }

        /// <summary>Builds the one local style. Call from OnGUI, after <c>HudTheme.Ensure()</c>.</summary>
        private void EnsureStyles()
        {
            if (_brief != null) return;

            _brief = new GUIStyle(HudTheme.Small)
            {
                fontSize = 12,
                wordWrap = true,
                alignment = TextAnchor.UpperLeft
            };
        }

        // ------------------------------------------------------------------ drawing

        private void OnGUI()
        {
            if (!IsOpen) return;

            HudTheme.Ensure();
            EnsureStyles();

            float sw = Screen.width;
            float sh = Screen.height;

            // Dark full-screen backdrop over the frozen field.
            HudTheme.Fill(new Rect(0f, 0f, sw, sh),
                          new Color(HudTheme.Bg.r, HudTheme.Bg.g, HudTheme.Bg.b, 0.94f));

            // Bottom caption strip, then the corner brackets on top of it.
            var footer = new Rect(0f, sh - FooterH, sw, FooterH);
            HudTheme.Fill(footer, HudTheme.PanelAlt);
            HudTheme.Fill(new Rect(0f, footer.y, sw, 1f), HudTheme.Line);
            HudTheme.Draw(footer, "EĞİTİM AMAÇLI SOYUT SİMÜLASYON",
                          HudTheme.Centered, HudTheme.TextFaint);

            DrawCornerBrackets(sw, sh);

            float margin = Mathf.Clamp(sw * 0.07f, 24f, 130f);
            float contentW = Mathf.Max(320f, sw - margin * 2f);

            // ---- heading
            float top = Mathf.Clamp(sh * 0.08f, 20f, 90f);
            DrawEyebrow(new Rect(0f, top, sw, 14f), "TAKTİK EĞİTİM SİMÜLATÖRÜ");

            GUIStyle titleStyle = sw < 1180f ? HudTheme.Verdict28C() : HudTheme.Verdict;
            HudTheme.Draw(new Rect(0f, top + 22f, sw, 46f), "İHA / SİHA TAKTİK SİMÜLASYONU",
                          titleStyle, HudTheme.Text);

            DrawSubtitle(new Rect(0f, top + 74f, sw, 28f), "GÖREV SEÇ");
            float headingBottom = top + 106f;

            // ---- controls legend, sitting just above the caption strip
            float legendY = footer.y - 22f - LegendH;
            DrawLegend(new Rect(margin, legendY, contentW, LegendH));

            // ---- mission cards
            ScenarioKind[] kinds = ScenarioLibrary.All;
            if (kinds == null || kinds.Length == 0) return;

            // The space between the heading and the legend is shared by the mission cards (top) and
            // the aircraft row (bottom); the aircraft row gets a bounded share of it.
            float cardsTop = headingBottom + 24f;
            float available = Mathf.Max(MinMissionCardH + AircraftRowH + AircraftLabelH + 20f,
                                        legendY - 24f - cardsTop);
            float aircraftH = Mathf.Clamp(available * 0.42f, AircraftRowH, 160f);
            float cardsH = Mathf.Max(MinMissionCardH, available - aircraftH - AircraftLabelH - 20f);
            float cardW = (contentW - CardGap * (kinds.Length - 1)) / kinds.Length;

            for (int i = 0; i < kinds.Length; i++)
            {
                var r = new Rect(margin + i * (cardW + CardGap), cardsTop, cardW, cardsH);
                if (DrawCard(r, kinds[i], i))
                {
                    Choose(kinds[i]);
                    // Choose may rebuild the world; stop drawing this frame.
                    return;
                }
            }

            // ---- aircraft selection
            float aircraftLabelY = cardsTop + cardsH + 12f;
            DrawEyebrow(new Rect(0f, aircraftLabelY, sw, 14f), "UÇAK SEÇ  ·  ←/→ İLE DEĞİŞTİR");
            DrawAircraftRow(new Rect(margin, aircraftLabelY + AircraftLabelH + 4f, contentW, aircraftH));
        }

        /// <summary>
        /// The row of aircraft cards, one per <see cref="AircraftCatalog"/> profile. Clicking a card
        /// records the choice in <see cref="ScenarioController.SelectedAircraftId"/>; it is applied
        /// when the world is next built.
        /// </summary>
        private void DrawAircraftRow(Rect r)
        {
            IReadOnlyList<AircraftProfile> profiles = AircraftCatalog.All;
            if (profiles == null || profiles.Count == 0) return;

            // Resolved defensively, so an unknown stored id still highlights a real card.
            string selectedId = ScenarioController.SelectedAircraft.Id;

            float w = (r.width - CardGap * (profiles.Count - 1)) / profiles.Count;
            for (int i = 0; i < profiles.Count; i++)
            {
                AircraftProfile p = profiles[i];
                var card = new Rect(r.x + i * (w + CardGap), r.y, w, r.height);
                if (DrawAircraftCard(card, p, p.Id == selectedId))
                    ScenarioController.SelectedAircraftId = p.Id;
            }
        }

        /// <summary>
        /// One aircraft card: header (name + SEÇİLİ marker), the one-line description and the four
        /// 0..1 ratings as labelled gauges. The selected card gets the SAME amber treatment the
        /// mission cards use for hover, so selection reads consistently across the screen. Returns
        /// true when the card was clicked.
        /// </summary>
        private bool DrawAircraftCard(Rect r, AircraftProfile p, bool selected)
        {
            bool hover = r.Contains(Event.current.mousePosition);
            bool lit = selected || hover;
            Color accent = lit ? HudTheme.Amber : HudTheme.Line;

            HudTheme.Fill(r, lit ? Tint(HudTheme.Amber, 0.12f, 0.96f) : HudTheme.PanelBg);
            HudTheme.Border(r, accent);

            // Header strip: aircraft name on the left, selection marker on the right.
            var head = new Rect(r.x + 1f, r.y + 1f, r.width - 2f, CardHeaderH);
            HudTheme.Fill(head, lit ? Tint(HudTheme.Amber, 0.20f, 1f) : HudTheme.PanelAlt);
            HudTheme.Fill(new Rect(head.x, head.yMax - 1f, head.width, 1f), accent);

            var headText = new Rect(head.x + 8f, head.y, Mathf.Max(20f, head.width - 16f), head.height);
            HudTheme.Draw(headText, Upper(p.DisplayName), HudTheme.SectionLabel,
                          lit ? HudTheme.Amber : HudTheme.Text);
            if (selected)
                HudTheme.Draw(headText, "SEÇİLİ", HudTheme.SmallRight, HudTheme.Amber);

            float x = r.x + 12f;
            float cw = Mathf.Max(20f, r.width - 24f);

            // One-line description.
            float y = head.yMax + 8f;
            HudTheme.Draw(new Rect(x, y, cw, 26f), p.Description, _brief,
                          lit ? HudTheme.Text : HudTheme.TextDim);

            // Rating gauges (0..1, no raw units on screen).
            y += 30f;
            y = DrawRating(x, y, cw, "HIZ", p.SpeedRating);
            y = DrawRating(x, y, cw, "ÇEVİK", p.AgilityRating);
            y = DrawRating(x, y, cw, "ATEŞ", p.FirepowerRating);
            DrawRating(x, y, cw, "SÜRE", p.EnduranceRating);

            // Invisible hit area over the whole card, exactly like the mission cards.
            return GUI.Button(r, GUIContent.none, GUIStyle.none);
        }

        /// <summary>
        /// Draws one labelled rating gauge with the HUD's shared bar helper and returns the Y for the
        /// next row.
        /// </summary>
        private static float DrawRating(float x, float y, float width, string label, float rating01)
        {
            HudTheme.Bar(new Rect(x, y, width, RatingBarH), rating01, label, string.Empty);
            return y + RatingBarH + RatingBarGap;
        }

        /// <summary>
        /// One mission card: header (mission code + difficulty blocks), title, briefing line, a
        /// footer with the wave count and difficulty word, and an action strip. Returns true when
        /// the card was clicked.
        /// </summary>
        private bool DrawCard(Rect r, ScenarioKind kind, int index)
        {
            bool hover = r.Contains(Event.current.mousePosition);
            Color accent = hover ? HudTheme.Amber : HudTheme.Line;

            int waves = ScenarioLibrary.TotalWaves(kind);
            int level = DifficultyLevel(waves);
            Color diff = DifficultyColor(level);

            // Body: flat panel fill, warmed to amber while hovered, with a 1px accent border.
            HudTheme.Fill(r, hover ? Tint(HudTheme.Amber, 0.12f, 0.96f) : HudTheme.PanelBg);
            HudTheme.Border(r, accent);

            // Header strip.
            var head = new Rect(r.x + 1f, r.y + 1f, r.width - 2f, CardHeaderH);
            HudTheme.Fill(head, hover ? Tint(HudTheme.Amber, 0.20f, 1f) : HudTheme.PanelAlt);
            HudTheme.Fill(new Rect(head.x, head.yMax - 1f, head.width, 1f), accent);
            HudTheme.Draw(new Rect(head.x + 8f, head.y, 60f, head.height), $"M-{index + 1:00}",
                          HudTheme.Small, hover ? HudTheme.Amber : HudTheme.TextFaint);
            DrawDifficultyBlocks(new Rect(head.xMax - 53f, head.y + 6f, 45f, 9f), level, diff);

            float x = r.x + 14f;
            float cw = Mathf.Max(20f, r.width - 28f);

            float actionY = r.yMax - 1f - CardActionH;
            float footY = actionY - CardFooterH;

            // Title (uppercase, wrapped) and the one-line briefing.
            float y = head.yMax + 10f;
            HudTheme.Draw(new Rect(x, y, cw, 44f), Upper(ScenarioLibrary.Title(kind)),
                          HudTheme.Title, HudTheme.Text);
            y += 48f;
            HudTheme.Draw(new Rect(x, y, cw, Mathf.Max(14f, footY - y - 8f)),
                          ScenarioLibrary.Description(kind), _brief,
                          hover ? HudTheme.Text : HudTheme.TextDim);

            // Footer: hairline, wave count on the left, difficulty word on the right.
            HudTheme.Fill(new Rect(x, footY, cw, 1f), HudTheme.Line);
            HudTheme.Draw(new Rect(x, footY + 7f, cw * 0.5f, 16f), $"{waves} DALGA",
                          HudTheme.Small, HudTheme.TextDim);
            HudTheme.Draw(new Rect(x, footY + 7f, cw, 16f), DifficultyText(level),
                          HudTheme.SmallRight, diff);

            // Action strip: amber call-to-action while hovered, dim hint otherwise.
            var action = new Rect(r.x + 1f, actionY, r.width - 2f, CardActionH);
            if (hover)
            {
                HudTheme.Fill(action, HudTheme.Amber);
                HudTheme.Draw(action, "BAŞLAT", HudTheme.TagText, HudTheme.Bg);
            }
            else
            {
                HudTheme.Fill(new Rect(action.x, action.y, action.width, 1f), HudTheme.Line);
                HudTheme.Draw(action, "SEÇMEK İÇİN TIKLA", HudTheme.TagText, HudTheme.TextFaint);
            }

            // Invisible hit area over the whole card — the card art above is the "button face".
            return GUI.Button(r, GUIContent.none, GUIStyle.none);
        }

        /// <summary>Three small blocks; the first <paramref name="level"/> of them are filled.</summary>
        private static void DrawDifficultyBlocks(Rect r, int level, Color c)
        {
            const int count = 3;
            float gap = 3f;
            float w = (r.width - gap * (count - 1)) / count;

            for (int i = 0; i < count; i++)
            {
                var block = new Rect(r.x + i * (w + gap), r.y, w, r.height);
                if (i < level) HudTheme.Fill(block, c);
                else HudTheme.Border(block, HudTheme.Line);
            }
        }

        /// <summary>Centred small caption flanked by two hairlines.</summary>
        private static void DrawEyebrow(Rect r, string text)
        {
            HudTheme.Draw(r, text, HudTheme.Centered, HudTheme.TextDim);

            float half = HudTheme.Centered.CalcSize(new GUIContent(text)).x * 0.5f;
            float cy = r.y + r.height * 0.5f;
            HudTheme.Fill(new Rect(r.center.x - half - 96f, cy, 80f, 1f), HudTheme.Line);
            HudTheme.Fill(new Rect(r.center.x + half + 16f, cy, 80f, 1f), HudTheme.Line);
        }

        /// <summary>Centred amber subtitle flanked by two short amber rules.</summary>
        private static void DrawSubtitle(Rect r, string text)
        {
            GUIStyle style = HudTheme.Verdict28C();
            HudTheme.Draw(r, text, style, HudTheme.Amber);

            float half = style.CalcSize(new GUIContent(text)).x * 0.5f;
            float cy = r.y + r.height * 0.5f - 1f;
            HudTheme.Fill(new Rect(r.center.x - half - 38f, cy, 24f, 2f), HudTheme.Amber);
            HudTheme.Fill(new Rect(r.center.x + half + 14f, cy, 24f, 2f), HudTheme.Amber);
        }

        /// <summary>
        /// The compact controls legend: a labelled left cell and the four key rows on the right.
        /// Same information as before, set in the HUD's type and colours.
        /// </summary>
        private static void DrawLegend(Rect r)
        {
            HudTheme.Panel(r);

            const float labelW = 190f;
            HudTheme.Fill(new Rect(r.x + labelW, r.y + 1f, 1f, r.height - 2f), HudTheme.Line);
            HudTheme.Draw(new Rect(r.x + 14f, r.y + 14f, labelW - 28f, 16f), "KONTROLLER",
                          HudTheme.SectionLabel, HudTheme.Amber);
            HudTheme.Draw(new Rect(r.x + 14f, r.y + 32f, labelW - 28f, 14f), "KAMERA VE PİLOT",
                          HudTheme.Small, HudTheme.TextFaint);

            float x = r.x + labelW + 16f;
            float w = Mathf.Max(40f, r.xMax - 16f - x);
            float y = r.y + 12f;

            HudTheme.Draw(new Rect(x, y, w, 18f),
                          "WASD + SAĞ TIK: KAMERA · TAB: DRONE TAKİP · F: SERBEST",
                          HudTheme.Small, HudTheme.TextDim);
            HudTheme.Draw(new Rect(x, y + 18f, w, 18f),
                          "C: PİLOT MODU · W/S: GAZ · A/D: DÖNÜŞ · ↑/↓: YUNUSLAMA",
                          HudTheme.Small, HudTheme.TextDim);
            HudTheme.Draw(new Rect(x, y + 36f, w, 18f),
                          "SPACE: TOP · F: FÜZE · Q: FLARE · E: ART YAKICI · X: KAÇIŞ",
                          HudTheme.Small, HudTheme.TextDim);
            HudTheme.Draw(new Rect(x, y + 54f, w, 18f),
                          "P: DURAKLAT · +/−: HIZ · R: YENİDEN · M: GÖREV MENÜSÜ",
                          HudTheme.Small, HudTheme.TextDim);
        }

        /// <summary>The mockup's four amber corner brackets.</summary>
        private static void DrawCornerBrackets(float sw, float sh)
        {
            const float inset = 18f;
            const float arm = 26f;
            const float t = 2f;
            Color c = new Color(HudTheme.Amber.r, HudTheme.Amber.g, HudTheme.Amber.b, 0.55f);

            HudTheme.Fill(new Rect(inset, inset, arm, t), c);
            HudTheme.Fill(new Rect(inset, inset, t, arm), c);

            HudTheme.Fill(new Rect(sw - inset - arm, inset, arm, t), c);
            HudTheme.Fill(new Rect(sw - inset - t, inset, t, arm), c);

            HudTheme.Fill(new Rect(inset, sh - inset - t, arm, t), c);
            HudTheme.Fill(new Rect(inset, sh - inset - arm, t, arm), c);

            HudTheme.Fill(new Rect(sw - inset - arm, sh - inset - t, arm, t), c);
            HudTheme.Fill(new Rect(sw - inset - t, sh - inset - arm, t, arm), c);
        }

        // ------------------------------------------------------------------ small helpers

        /// <summary>A very dark tint of an accent colour, used for hovered fills.</summary>
        private static Color Tint(Color accent, float amount, float alpha)
        {
            return new Color(accent.r * amount, accent.g * amount, accent.b * amount, alpha);
        }

        /// <summary>
        /// Presentation-only difficulty rating derived from the mission length, matching the mockup
        /// (2 waves = easy, 3 = medium, 4 = hard). Reads mission data; changes nothing.
        /// </summary>
        private static int DifficultyLevel(int waves)
        {
            return Mathf.Clamp(waves - 1, 1, 3);
        }

        private static Color DifficultyColor(int level)
        {
            if (level <= 1) return HudTheme.Ok;
            return level == 2 ? HudTheme.Amber : HudTheme.Critical;
        }

        private static string DifficultyText(int level)
        {
            if (level <= 1) return "KOLAY";
            return level == 2 ? "ORTA" : "ZOR";
        }

        /// <summary>Uppercase using the invariant culture, exactly as <see cref="Hud"/> does.</summary>
        private static string Upper(string s)
        {
            return string.IsNullOrEmpty(s) ? string.Empty : s.ToUpperInvariant();
        }

        /// <summary>
        /// Applies the player's choice. On the first pick the mission simply begins; a mid-mission
        /// pick rebuilds the generated world first, so the new mission starts on a clean field.
        /// </summary>
        private void Choose(ScenarioKind kind)
        {
            ScenarioController.SelectedKind = kind;

            SimulationBootstrap boot = SimulationBootstrap.Instance;
            if (boot == null) boot = FindAnyObjectByType<SimulationBootstrap>();

            if (_missionStarted && boot != null)
            {
                // Mid-mission change: mirror GameControls' R restart exactly. SelectedKind and
                // _autoBegin are static, so the menu rebuilt below picks up the mission chosen here
                // and starts it without showing this briefing a second time. Rebuild() restores the
                // time scale and clears the registry itself.
                IsOpen = false;
                _autoBegin = true;
                boot.Rebuild();
                return;
            }

            // No bootstrap to rebuild with (hand-authored scene): fall through and just (re)start the
            // scenario in place — the best that can be done without regenerating the field.

            if (_controller == null) _controller = FindAnyObjectByType<ScenarioController>();
            if (_controller != null) _controller.BeginMission();

            _missionStarted = true;
            Time.timeScale = 1f;
            IsOpen = false;
        }
    }
}
