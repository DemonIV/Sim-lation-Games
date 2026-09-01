using System.Collections.Generic;
using UnityEngine;
using Sim.Core;

namespace Sim.Runtime
{
    /// <summary>
    /// Campaign / hangar screen drawn with IMGUI (<see cref="OnGUI"/>), so it needs zero Canvas or
    /// scene setup — <see cref="SimulationBootstrap"/> just adds the component.
    ///
    /// <para>
    /// It opens at launch, freezes the sim with <c>Time.timeScale = 0</c> and shows the CAMPAIGN:
    /// one card per <see cref="CampaignLibrary"/> level (SEVİYE 1, SEVİYE 2 …) with its name,
    /// briefing, best grade and locked/unlocked/completed state. Locked levels are drawn locked and
    /// cannot be clicked. Picking an unlocked level writes
    /// <see cref="CampaignSession.SelectedLevelIndex"/> (which also decides
    /// <see cref="ScenarioController.SelectedKind"/>) and calls
    /// <see cref="ScenarioController.BeginMission"/>, which is what actually releases the
    /// <see cref="ScenarioController"/> to start spawning waves.
    /// </para>
    ///
    /// <para>
    /// The second page is the HANGAR (<c>H</c>, or the button in the header): one row per
    /// <see cref="UpgradeCatalog"/> track showing its current level, what that level is worth, the
    /// next level's price and a buy button. The button is drawn disabled when the track is maxed or
    /// the player cannot afford it, and a click on a disabled button still reports WHY — a failed
    /// purchase is never a silent no-op. Purchases go through
    /// <see cref="CampaignSession.TryPurchase"/>, which persists them.
    /// </para>
    ///
    /// <para>
    /// Pressing <c>M</c> during a mission reopens this screen. Choosing from there cannot simply
    /// restart the scenario in place — the field is already full of hostiles and the drones are worn
    /// down — so it rebuilds the generated world through the same path <see cref="GameControls"/>
    /// uses for <c>R</c> (<see cref="SimulationBootstrap.Rebuild"/>). The choice survives that
    /// rebuild because it lives in the static <see cref="CampaignSession"/>.
    /// </para>
    ///
    /// <para>
    /// PRESENTATION: styled after <c>docs/design/hud/MissionSelect.dc.html</c> and drawn entirely
    /// with <see cref="HudTheme"/> (same palette, panels, hairline borders and tags as the combat
    /// HUD). Same typography caveat as the rest of the HUD: no font assets and no letter-spacing in
    /// IMGUI, so the type only approximates the mockup.
    /// </para>
    ///
    /// <para>
    /// Beneath the levels sits the AIRCRAFT row: one card per <see cref="AircraftCatalog"/> profile
    /// (name, one-line description and the profile's 0..1 ratings as <see cref="HudTheme.Bar"/>
    /// gauges). Clicking a card — or the ←/→ keys, which are bound nowhere else — writes
    /// <see cref="ScenarioController.SelectedAircraftId"/>. The choice only reaches the field when
    /// the world is (re)built, which is exactly what picking a level does.
    /// </para>
    ///
    /// Because the menu runs at <c>timeScale == 0</c> it never reads <see cref="Time.deltaTime"/>;
    /// the transient hangar message expires on <see cref="Time.unscaledTime"/> instead.
    /// </summary>
    public class ScenarioMenu : MonoBehaviour
    {
        /// <summary>Which page of the menu is showing.</summary>
        private enum MenuPage { Levels, Hangar }

        // Layout constants (design: 1600x900 artboard, scaled to the actual screen).
        private const float CardGap = 14f;
        private const float CardHeaderH = 20f;
        private const float CardActionH = 20f;
        private const float LegendH = 92f;
        private const float FooterH = 24f;

        // Aircraft row: header (20) + description (26+4) + FIVE 9px gauges with 5px gaps = ~124px of
        // content, so 134 is the smallest height that never clips a card. (Grown by one bar row when
        // the stealth gauge was added — the archetypes' radar signature is a real trade-off now, so
        // it has to be visible at the moment of choosing.)
        private const float AircraftRowH = 134f;
        private const float AircraftLabelH = 16f;
        private const float RatingBarH = 9f;
        private const float RatingBarGap = 5f;

        // Level grid: four columns, so eight levels fit in two rows on any sane window.
        private const int LevelColumns = 4;
        private const float MinLevelCardH = 104f;

        // Hangar: one row per upgrade track.
        private const float HangarRowH = 34f;
        private const float HangarRowGap = 4f;
        private const float HangarBuyW = 118f;

        /// <summary>How long (unscaled seconds) a hangar message stays on screen.</summary>
        private const float MessageSeconds = 3f;

        private ScenarioController _controller;

        /// <summary>
        /// Word-wrapped briefing text. The theme's <c>Small</c> face with wrapping turned on —
        /// a local variant of the shared style, not a second palette.
        /// </summary>
        private GUIStyle _brief;

        /// <summary>True while the menu is showing and the sim is held paused.</summary>
        public bool IsOpen { get; private set; }

        private MenuPage _page = MenuPage.Levels;

        /// <summary>Last hangar feedback line (purchase made / refused) and when it expires.</summary>
        private string _message = string.Empty;
        private float _messageUntil;
        private Color _messageColor = HudTheme.Amber;

        /// <summary>Set by the first click on "kampanyayı sıfırla"; a second click confirms it.</summary>
        private bool _resetArmed;

        /// <summary>
        /// True once the player has picked a level at least once with THIS component. Used to tell a
        /// fresh launch (just begin the mission) apart from a mid-mission reopen (rebuild the world).
        /// </summary>
        private bool _missionStarted;

        /// <summary>
        /// Set just before the mid-mission rebuild and consumed by <see cref="Start"/> on the freshly
        /// built menu, so the player does not have to pick the same level twice. STATIC for the same
        /// reason as <see cref="CampaignSession.SelectedLevelIndex"/>: it must survive the rebuild
        /// that destroys this component. <see cref="SimulationBootstrap.Rebuild"/> leaves it alone on
        /// purpose.
        /// </summary>
        private static bool _autoBegin;

        private void Start()
        {
            _controller = FindAnyObjectByType<ScenarioController>();

            // Pull the campaign in (progress, money, garage) before anything reads it.
            CampaignSession.EnsureLoaded();

            if (_autoBegin)
            {
                // Coming back from a level change: the choice was already made, so skip the briefing
                // and launch straight into the freshly rebuilt field. Note this menu is a NEW component
                // built by SimulationBootstrap.Rebuild — the one the player clicked is already gone.
                _autoBegin = false;
                _missionStarted = true;
                IsOpen = false;
                Time.timeScale = 1f;
                if (_controller != null) _controller.BeginMission();
                return;
            }

            // Open on launch and hold the sim until a level is chosen.
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

                if (Input.GetKeyDown(KeyCode.H)) TogglePage();
                if (_page == MenuPage.Levels) HandleAircraftKeys();
                return;
            }

            // M reopens the briefing mid-mission (and pauses). Only handled while closed, so the key
            // can never dismiss the menu without a level being chosen.
            if (Input.GetKeyDown(KeyCode.M))
            {
                IsOpen = true;
                _page = MenuPage.Levels;
                Time.timeScale = 0f;
            }
        }

        /// <summary>Flips between the level list and the hangar, clearing any stale feedback line.</summary>
        private void TogglePage()
        {
            _page = _page == MenuPage.Levels ? MenuPage.Hangar : MenuPage.Levels;
            _message = string.Empty;
            _resetArmed = false;
        }

        /// <summary>
        /// ←/→ step through the aircraft catalogue while the level list is up. Those two keys are
        /// used nowhere else in the project (the pilot pitches with ↑/↓, the camera flies with WASD),
        /// so this cannot collide with an existing binding.
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

            DrawSubtitle(new Rect(0f, top + 74f, sw, 28f),
                         _page == MenuPage.Levels ? "SEVİYE SEÇ" : "HANGAR");
            float headingBottom = top + 106f;

            // ---- money + page switch, pinned to the top-left / top-right of the content column
            DrawWallet(new Rect(margin, top + 12f, 200f, 46f));
            bool switchClicked = DrawPageButton(new Rect(margin + contentW - 200f, top + 12f, 200f, 46f));

            // ---- controls legend, sitting just above the caption strip
            float legendY = footer.y - 22f - LegendH;
            DrawLegend(new Rect(margin, legendY, contentW, LegendH));

            float bodyTop = headingBottom + 24f;
            var body = new Rect(margin, bodyTop, contentW, Mathf.Max(220f, legendY - 24f - bodyTop));

            if (_page == MenuPage.Hangar) DrawHangar(body);
            else DrawLevelsPage(body);

            // Handled last so a page flip cannot invalidate the rects drawn above this frame.
            if (switchClicked) TogglePage();
        }

        // ------------------------------------------------------------------ levels page

        /// <summary>The level grid plus the aircraft-selection row beneath it.</summary>
        private void DrawLevelsPage(Rect body)
        {
            IReadOnlyList<CampaignLevel> levels = CampaignLibrary.All;
            if (levels == null || levels.Count == 0) return;

            int rows = Mathf.Max(1, Mathf.CeilToInt(levels.Count / (float)LevelColumns));

            float aircraftH = Mathf.Clamp(body.height * 0.3f, AircraftRowH, 164f);
            // The grid never shrinks below MinLevelCardH per ROW (gaps included), even if that means
            // spilling into the aircraft row on a very short window.
            float gridH = Mathf.Max(rows * MinLevelCardH + CardGap * (rows - 1),
                                    body.height - aircraftH - AircraftLabelH - 20f);

            float cellW = (body.width - CardGap * (LevelColumns - 1)) / LevelColumns;
            float cellH = (gridH - CardGap * (rows - 1)) / rows;

            CampaignProgress progress = CampaignSession.Progress;

            for (int i = 0; i < levels.Count; i++)
            {
                int col = i % LevelColumns;
                int row = i / LevelColumns;
                var r = new Rect(body.x + col * (cellW + CardGap),
                                 body.y + row * (cellH + CardGap), cellW, cellH);

                CampaignLevel level = levels[i];
                bool unlocked = progress.IsUnlocked(level.Index);
                if (DrawLevelCard(r, level, unlocked, progress.IsCompleted(level.Index),
                                  progress.BestStars(level.Index)) && unlocked)
                {
                    ChooseLevel(level);
                    // ChooseLevel may rebuild the world; stop drawing this frame.
                    return;
                }
            }

            // ---- aircraft selection
            float aircraftLabelY = body.y + gridH + 12f;
            DrawEyebrow(new Rect(0f, aircraftLabelY, Screen.width, 14f), "UÇAK SEÇ  ·  ←/→ İLE DEĞİŞTİR");
            DrawAircraftRow(new Rect(body.x, aircraftLabelY + AircraftLabelH + 4f,
                                     body.width, aircraftH));
        }

        /// <summary>
        /// One level card: header ("SEVİYE N" + difficulty blocks or a lock tag), the level name, its
        /// briefing, and a footer with the best grade and the wave count. A locked card is drawn dim
        /// and is not clickable. Returns true when an UNLOCKED card was clicked.
        /// </summary>
        private bool DrawLevelCard(Rect r, CampaignLevel level, bool unlocked, bool completed, int stars)
        {
            bool hover = unlocked && r.Contains(Event.current.mousePosition);
            Color accent = !unlocked ? HudTheme.Line
                                     : hover ? HudTheme.Amber
                                             : completed ? HudTheme.Ok : HudTheme.Line;

            int difficulty = DifficultyLevel(level.Index);
            Color diff = DifficultyColor(difficulty);

            HudTheme.Fill(r, hover ? Tint(HudTheme.Amber, 0.12f, 0.96f) : HudTheme.PanelBg);
            HudTheme.Border(r, accent);

            // Header strip: level number on the left, difficulty blocks (or LOCKED) on the right.
            var head = new Rect(r.x + 1f, r.y + 1f, r.width - 2f, CardHeaderH);
            HudTheme.Fill(head, hover ? Tint(HudTheme.Amber, 0.20f, 1f) : HudTheme.PanelAlt);
            HudTheme.Fill(new Rect(head.x, head.yMax - 1f, head.width, 1f), accent);
            HudTheme.Draw(new Rect(head.x + 8f, head.y, 90f, head.height),
                          $"SEVİYE {level.Index}", HudTheme.Small,
                          unlocked ? (hover ? HudTheme.Amber : HudTheme.Text) : HudTheme.TextFaint);

            if (unlocked)
                DrawDifficultyBlocks(new Rect(head.xMax - 53f, head.y + 6f, 45f, 9f), difficulty, diff);
            else
                HudTheme.Draw(new Rect(head.x, head.y, head.width - 8f, head.height), "KİLİTLİ",
                              HudTheme.SmallRight, HudTheme.TextFaint);

            float x = r.x + 12f;
            float cw = Mathf.Max(20f, r.width - 24f);

            float actionY = r.yMax - 1f - CardActionH;
            float footY = actionY - 18f;

            // Name and briefing.
            float y = head.yMax + 6f;
            HudTheme.Draw(new Rect(x, y, cw, 18f), Upper(level.Name), HudTheme.SectionLabel,
                          unlocked ? HudTheme.Text : HudTheme.TextFaint);
            y += 20f;
            HudTheme.Draw(new Rect(x, y, cw, Mathf.Max(12f, footY - y - 4f)), level.Brief, _brief,
                          unlocked ? (hover ? HudTheme.Text : HudTheme.TextDim) : HudTheme.TextFaint);

            // Footer: best grade on the left, wave count on the right.
            HudTheme.Fill(new Rect(x, footY, cw, 1f), HudTheme.Line);
            HudTheme.Draw(new Rect(x, footY + 3f, cw * 0.5f, 15f), StarText(stars),
                          HudTheme.Small, completed ? HudTheme.Amber : HudTheme.TextFaint);
            HudTheme.Draw(new Rect(x, footY + 3f, cw, 15f), $"{level.TotalWaves} DALGA",
                          HudTheme.SmallRight, unlocked ? HudTheme.TextDim : HudTheme.TextFaint);

            // Action strip.
            var action = new Rect(r.x + 1f, actionY, r.width - 2f, CardActionH);
            if (!unlocked)
            {
                HudTheme.Fill(new Rect(action.x, action.y, action.width, 1f), HudTheme.Line);
                HudTheme.Draw(action, "ÖNCEKİ SEVİYEYİ TAMAMLA", HudTheme.TagText, HudTheme.TextFaint);
                return false;   // locked cards are not clickable at all
            }

            if (hover)
            {
                HudTheme.Fill(action, HudTheme.Amber);
                HudTheme.Draw(action, "BAŞLAT", HudTheme.TagText, HudTheme.Bg);
            }
            else
            {
                HudTheme.Fill(new Rect(action.x, action.y, action.width, 1f), HudTheme.Line);
                HudTheme.Draw(action, completed ? "TEKRAR OYNA" : "SEÇMEK İÇİN TIKLA",
                              HudTheme.TagText, HudTheme.TextFaint);
            }

            // Invisible hit area over the whole card — the card art above is the "button face".
            return GUI.Button(r, GUIContent.none, GUIStyle.none);
        }

        // ------------------------------------------------------------------ hangar page

        /// <summary>
        /// The upgrade screen: a header, one row per <see cref="UpgradeCatalog"/> track and a footer
        /// carrying the feedback line and the campaign reset.
        /// </summary>
        private void DrawHangar(Rect body)
        {
            HudTheme.Panel(body);
            HudTheme.Header(new Rect(body.x, body.y, body.width, CardHeaderH),
                            $"HANGAR  ·  {Upper(ScenarioController.SelectedAircraft.DisplayName)}",
                            $"KREDİ {CampaignSession.Wallet.Balance}");

            UpgradeTrack[] tracks = UpgradeCatalog.All;
            float y = body.y + CardHeaderH + 10f;
            float x = body.x + 14f;
            float w = Mathf.Max(240f, body.width - 28f);

            for (int i = 0; i < tracks.Length; i++)
            {
                DrawHangarRow(new Rect(x, y, w, HangarRowH), tracks[i]);
                y += HangarRowH + HangarRowGap;
            }

            // Feedback line: why the last purchase was refused, or what was bought.
            y += 4f;
            if (!string.IsNullOrEmpty(_message) && Time.unscaledTime < _messageUntil)
                HudTheme.Draw(new Rect(x, y, w, 18f), _message, HudTheme.Label, _messageColor);

            // Footer: how upgraded the aircraft is, plus the explicit campaign reset.
            float footerY = body.yMax - 30f;
            HudTheme.Fill(new Rect(x, footerY - 8f, w, 1f), HudTheme.Line);
            HudTheme.Draw(new Rect(x, footerY, w * 0.6f, 18f),
                          $"TOPLAM YÜKSELTME {CampaignSession.Upgrades.TotalLevels}"
                          + $"  ·  TOPLAM YILDIZ {CampaignSession.Progress.TotalStars}",
                          HudTheme.Small, HudTheme.TextDim);

            var reset = new Rect(body.xMax - 14f - 190f, footerY, 190f, 20f);
            DrawResetButton(reset);
        }

        /// <summary>
        /// One upgrade row: name + description on the left, the level pips and effect in the middle,
        /// and the buy button on the right. The button is drawn disabled when the track is maxed or
        /// unaffordable, but a click on it still produces a reason.
        /// </summary>
        private void DrawHangarRow(Rect r, UpgradeTrack track)
        {
            int level = CampaignSession.Upgrades.LevelOf(track);
            int max = UpgradeCatalog.MaxLevel(track);
            bool maxed = CampaignSession.Upgrades.IsMaxed(track);
            int cost = CampaignSession.Upgrades.NextCost(track);
            bool affordable = !maxed && CampaignSession.Wallet.CanAfford(cost);

            HudTheme.Fill(r, HudTheme.PanelAlt);
            HudTheme.Border(r, HudTheme.Line);

            float x = r.x + 10f;
            HudTheme.Draw(new Rect(x, r.y + 3f, 150f, 15f), Upper(UpgradeCatalog.Name(track)),
                          HudTheme.SectionLabel, maxed ? HudTheme.Ok : HudTheme.Amber);
            HudTheme.Draw(new Rect(x, r.y + 17f, 210f, 14f), UpgradeCatalog.Description(track),
                          HudTheme.Small, HudTheme.TextFaint);

            // Level pips: one small block per level, the bought ones filled.
            float pipsX = x + 220f;
            DrawLevelPips(new Rect(pipsX, r.y + 6f, 9f * max + 3f * (max - 1), 9f), level, max,
                          maxed ? HudTheme.Ok : HudTheme.Amber);
            HudTheme.Draw(new Rect(pipsX, r.y + 18f, 180f, 14f),
                          $"SV {level}/{max}  ·  {UpgradeCatalog.EffectSummary(track, level)}",
                          HudTheme.Small, HudTheme.TextDim);

            // Next level's effect, so the player sees what the money buys.
            float nextX = pipsX + 200f;
            float nextW = Mathf.Max(60f, r.xMax - 10f - HangarBuyW - 10f - nextX);
            if (nextW > 60f)
            {
                HudTheme.Draw(new Rect(nextX, r.y + 3f, nextW, 15f),
                              maxed ? "AZAMİ SEVİYE" : $"SONRAKİ: {UpgradeCatalog.EffectSummary(track, level + 1)}",
                              HudTheme.Small, maxed ? HudTheme.Ok : HudTheme.Text);
                HudTheme.Draw(new Rect(nextX, r.y + 17f, nextW, 14f),
                              maxed ? string.Empty : $"BEDEL {cost} KREDİ",
                              HudTheme.Small, affordable ? HudTheme.TextDim : HudTheme.Critical);
            }

            var buy = new Rect(r.xMax - 10f - HangarBuyW, r.y + 6f, HangarBuyW, r.height - 12f);
            if (DrawBuyButton(buy, maxed, affordable, cost)) Purchase(track, maxed, affordable, cost);
        }

        /// <summary>
        /// The buy button. Enabled = amber call to action; disabled = dim, but STILL clickable so the
        /// press can be answered with a reason instead of nothing happening.
        /// </summary>
        private static bool DrawBuyButton(Rect r, bool maxed, bool affordable, int cost)
        {
            bool enabled = !maxed && affordable;
            bool hover = enabled && r.Contains(Event.current.mousePosition);

            if (enabled)
            {
                HudTheme.Fill(r, hover ? HudTheme.Amber : Tint(HudTheme.Amber, 0.22f, 1f));
                HudTheme.Border(r, HudTheme.Amber);
                HudTheme.Draw(r, $"SATIN AL  {cost}", HudTheme.TagText,
                              hover ? HudTheme.Bg : HudTheme.Amber);
            }
            else
            {
                HudTheme.Fill(r, HudTheme.Track);
                HudTheme.Border(r, HudTheme.Line);
                HudTheme.Draw(r, maxed ? "TAMAMLANDI" : "KREDİ YETMİYOR",
                              HudTheme.TagText, HudTheme.TextFaint);
            }

            return GUI.Button(r, GUIContent.none, GUIStyle.none);
        }

        /// <summary>
        /// Applies a buy click. A refused purchase always produces a message; a successful one is
        /// persisted by <see cref="CampaignSession.TryPurchase"/> and reported the same way.
        /// </summary>
        private void Purchase(UpgradeTrack track, bool maxed, bool affordable, int cost)
        {
            string name = UpgradeCatalog.Name(track);

            if (maxed) { SetMessage($"{name}: zaten azami seviyede.", HudTheme.TextDim); return; }
            if (!affordable)
            {
                int missing = cost - CampaignSession.Wallet.Balance;
                SetMessage($"{name}: kredi yetmiyor — {missing} kredi eksik.", HudTheme.Critical);
                return;
            }

            if (!CampaignSession.TryPurchase(track))
            {
                SetMessage($"{name}: satın alınamadı.", HudTheme.Critical);
                return;
            }

            int level = CampaignSession.Upgrades.LevelOf(track);
            SetMessage($"{name} seviye {level} alındı ({UpgradeCatalog.EffectSummary(track, level)}). "
                       + $"Kalan kredi: {CampaignSession.Wallet.Balance}.", HudTheme.Ok);
        }

        private void SetMessage(string text, Color color)
        {
            _message = text;
            _messageColor = color;
            // timeScale is 0 on this screen, so the wall clock is the only usable one.
            _messageUntil = Time.unscaledTime + MessageSeconds;
        }

        /// <summary>Two-step campaign reset: the first click arms it, the second wipes the save.</summary>
        private void DrawResetButton(Rect r)
        {
            Color accent = _resetArmed ? HudTheme.Critical : HudTheme.Line;
            HudTheme.Fill(r, HudTheme.PanelAlt);
            HudTheme.Border(r, accent);
            HudTheme.Draw(r, _resetArmed ? "EMİN MİSİN? TEKRAR TIKLA" : "KAMPANYAYI SIFIRLA",
                          HudTheme.TagText, _resetArmed ? HudTheme.Critical : HudTheme.TextFaint);

            if (!GUI.Button(r, GUIContent.none, GUIStyle.none)) return;

            if (!_resetArmed)
            {
                _resetArmed = true;
                SetMessage("Kampanyayı sıfırlamak için butona tekrar tıkla.", HudTheme.Amber);
                return;
            }

            _resetArmed = false;
            CampaignSession.ResetAll();
            SetMessage("Kampanya sıfırlandı: seviye 1, boş kasa, temel uçak.", HudTheme.Amber);
        }

        /// <summary>Small filled/empty blocks showing how many levels of a track are bought.</summary>
        private static void DrawLevelPips(Rect r, int level, int max, Color c)
        {
            if (max <= 0) return;
            const float gap = 3f;
            float w = (r.width - gap * (max - 1)) / max;

            for (int i = 0; i < max; i++)
            {
                var block = new Rect(r.x + i * (w + gap), r.y, w, r.height);
                if (i < level) HudTheme.Fill(block, c);
                else HudTheme.Border(block, HudTheme.Line);
            }
        }

        // ------------------------------------------------------------------ header widgets

        /// <summary>The persistent money readout.</summary>
        private static void DrawWallet(Rect r)
        {
            HudTheme.Panel(r);
            HudTheme.Draw(new Rect(r.x + 10f, r.y + 5f, r.width - 20f, 14f), "KREDİ",
                          HudTheme.SectionLabel, HudTheme.Amber);
            HudTheme.Draw(new Rect(r.x + 10f, r.y + 20f, r.width - 20f, 20f),
                          CampaignSession.Wallet.Balance.ToString(), HudTheme.Verdict24(),
                          HudTheme.Text);
        }

        /// <summary>The page switch (level list ↔ hangar). Returns true when clicked.</summary>
        private bool DrawPageButton(Rect r)
        {
            bool hover = r.Contains(Event.current.mousePosition);
            Color accent = hover ? HudTheme.Amber : HudTheme.Line;

            HudTheme.Fill(r, hover ? Tint(HudTheme.Amber, 0.18f, 1f) : HudTheme.PanelBg);
            HudTheme.Border(r, accent);
            HudTheme.Draw(new Rect(r.x + 10f, r.y + 5f, r.width - 20f, 14f), "H TUŞU",
                          HudTheme.Small, HudTheme.TextFaint);
            HudTheme.Draw(new Rect(r.x + 10f, r.y + 20f, r.width - 20f, 18f),
                          _page == MenuPage.Levels ? "HANGAR / YÜKSELTME" : "SEVİYE LİSTESİ",
                          HudTheme.Label, hover ? HudTheme.Amber : HudTheme.Text);

            return GUI.Button(r, GUIContent.none, GUIStyle.none);
        }

        // ------------------------------------------------------------------ aircraft row

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
                {
                    ScenarioController.SelectedAircraftId = p.Id;
                    CampaignSession.Save();
                }
            }
        }

        /// <summary>
        /// One aircraft card: header (name + SEÇİLİ marker), the one-line description and the five
        /// 0..1 ratings as labelled gauges. The selected card gets the SAME amber treatment the
        /// level cards use for hover, so selection reads consistently across the screen. Returns
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

            // Rating gauges (0..1, no raw units on screen). GİZLİ is the archetype's radar signature
            // read the way every other bar reads — MORE is better — so the recon İHA's small
            // cross section and the jet's big one are a visible trade-off at the moment of choosing.
            y += 30f;
            y = DrawRating(x, y, cw, "HIZ", p.SpeedRating);
            y = DrawRating(x, y, cw, "ÇEVİK", p.AgilityRating);
            y = DrawRating(x, y, cw, "ATEŞ", p.FirepowerRating);
            y = DrawRating(x, y, cw, "SÜRE", p.EnduranceRating);
            DrawRating(x, y, cw, "GİZLİ", p.StealthRating);

            // Invisible hit area over the whole card, exactly like the level cards.
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

        // ------------------------------------------------------------------ chrome

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
                          "P: DURAKLAT · +/−: HIZ · R: YENİDEN · M: MENÜ · H: HANGAR",
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

        /// <summary>Filled/empty stars for a 0..3 grade, matching the mission-report screen.</summary>
        private static string StarText(int stars)
        {
            int s = Mathf.Clamp(stars, 0, 3);
            return new string('★', s) + new string('☆', 3 - s);
        }

        /// <summary>
        /// Presentation-only difficulty rating (1..3) spread evenly over the campaign, so the first
        /// third of the levels reads KOLAY, the middle ORTA and the last ZOR. Reads data; changes
        /// nothing.
        /// </summary>
        private static int DifficultyLevel(int levelIndex)
        {
            int count = Mathf.Max(1, CampaignLibrary.Count);
            return Mathf.Clamp(Mathf.CeilToInt(levelIndex * 3f / count), 1, 3);
        }

        private static Color DifficultyColor(int level)
        {
            if (level <= 1) return HudTheme.Ok;
            return level == 2 ? HudTheme.Amber : HudTheme.Critical;
        }

        /// <summary>Uppercase using the invariant culture, exactly as <see cref="Hud"/> does.</summary>
        private static string Upper(string s)
        {
            return string.IsNullOrEmpty(s) ? string.Empty : s.ToUpperInvariant();
        }

        /// <summary>
        /// Applies the player's choice. On the first pick the mission simply begins; a mid-mission
        /// pick rebuilds the generated world first, so the new level starts on a clean field.
        /// </summary>
        private void ChooseLevel(CampaignLevel level)
        {
            if (level == null) return;
            // Refuses locked levels — the card is already unclickable, this is the second lock.
            if (!CampaignSession.SelectLevel(level.Index)) return;

            SimulationBootstrap boot = SimulationBootstrap.Instance;
            if (boot == null) boot = FindAnyObjectByType<SimulationBootstrap>();

            if (_missionStarted && boot != null)
            {
                // Mid-mission change: mirror GameControls' R restart exactly. The level choice lives
                // in the static CampaignSession and _autoBegin is static too, so the menu rebuilt
                // below picks up the level chosen here and starts it without showing this briefing a
                // second time. Rebuild() restores the time scale and clears the registry itself.
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
