using System.Collections.Generic;
using UnityEngine;
using Sim.Core;

namespace Sim.Runtime
{
    /// <summary>
    /// On-screen HUD drawn with IMGUI (<see cref="OnGUI"/>), so it needs zero Canvas/scene setup.
    /// Reads mission/score state from <see cref="SimulationDirector"/>, wave state from
    /// <see cref="ScenarioController"/> and radar contacts from every <see cref="RadarSensor"/> in
    /// the scene, and shows a mission-report screen when the mission ends.
    ///
    /// <para>
    /// The look follows the design mockup in <c>docs/design/hud/</c> (Main / PilotHud / MissionEnd):
    /// dark bordered panels, an amber accent, colour-coded bars and uppercase section labels. All
    /// palette and drawing primitives live in <see cref="HudTheme"/>.
    /// </para>
    ///
    /// <para>
    /// KNOWN CONSTRAINT: IMGUI here has no custom fonts (no font assets may be added to the project)
    /// and no letter-spacing, so the mockup's Barlow Condensed / JetBrains Mono typography can only
    /// be APPROXIMATED (uppercase labels, size hierarchy, built-in font). Colours, panels, borders,
    /// bars, layout and information hierarchy do match the design.
    /// </para>
    ///
    /// Presentation only — the HUD never writes gameplay state and no value it reads was changed.
    /// </summary>
    public class Hud : MonoBehaviour
    {
        private SimulationDirector _director;
        private ScenarioController _scenario;
        private RadarSensor[] _sensors;
        private GameControls _controls;
        private PlayerDroneController _pilot;
        private ScenarioMenu _menu;

        // Occasional refresh of the sensor list so newly spawned drones show up.
        private float _refreshTimer;
        private const float RefreshInterval = 2f;

        // ---- layout constants (design proportions, in screen pixels) ----
        private const float Margin = 16f;
        private const float ColWidth = 320f;
        private const float HeaderH = 22f;
        private const float RowH = 20f;
        private const float BarH = 11f;
        private const float StripH = 54f;

        // ---- pilot radar scope (plan-position indicator, bottom-right while flying) ----

        /// <summary>Radius of the scope in METRES: contacts further out than this are not shown.</summary>
        [SerializeField] private float scopeRange = 250f;

        /// <summary>Scope diameter in screen pixels.</summary>
        private const float ScopeSize = 200f;

        /// <summary>Height of the threat readout row drawn under the scope.</summary>
        private const float ScopeInfoH = 18f;

        // ---- gun pipper (the reticle is projected from the bore, not welded to screen centre) ----

        /// <summary>Full arm-to-arm span of the pipper in screen pixels (~1.7x the old reticle).</summary>
        private const float PipperSize = 110f;

        /// <summary>
        /// Muzzle speed used ONLY to draw the pipper's ballistic drop, in m/s. The gun in this sim is a
        /// HITSCAN abstraction — <see cref="GunTurret.TryFireAtPoint"/> spends a round and rolls the hit
        /// straight off the aim ray, no projectile is ever simulated — so there is no gameplay muzzle
        /// speed to read. 0 keeps the pipper honest: it marks the boresight, exactly where rounds go.
        /// </summary>
        [SerializeField] private float pipperMuzzleSpeed = 0f;

        /// <summary>
        /// Gravity magnitude (m/s², positive) used for the pipper's drop. 0 while the gun is hitscan;
        /// raising it only moves the RETICLE, never a gameplay value.
        /// </summary>
        [SerializeField] private float pipperGravity = 0f;

        // Camera rig, read for the cockpit-view hint in the control strip and for the camera the
        // pipper is projected through.
        private CameraRig _cameraRig;

        // The camera the rig drives (falls back to the tagged main camera).
        private Camera _camera;

        // Cached "Model" child of the piloted aircraft — BankingVisual rolls that child, never the
        // root, so the pipper's lean has to be read off it. Re-resolved when the aircraft changes.
        private Transform _bankOwner;
        private Transform _bankSource;

        // Radar contact rows, rebuilt each frame into reused lists so OnGUI allocates nothing extra.
        private readonly List<string> _contactId = new List<string>();
        private readonly List<string> _contactType = new List<string>();
        private readonly List<Color> _contactAccent = new List<Color>();
        private readonly List<string> _contactRange = new List<string>();

        private void Start()
        {
            _director = FindAnyObjectByType<SimulationDirector>();
            _scenario = FindAnyObjectByType<ScenarioController>();
            _controls = FindAnyObjectByType<GameControls>();
            _pilot = FindAnyObjectByType<PlayerDroneController>();
            _menu = FindAnyObjectByType<ScenarioMenu>();
            _cameraRig = FindAnyObjectByType<CameraRig>();
            RefreshSensors();
        }

        private void Update()
        {
            _refreshTimer += Time.unscaledDeltaTime;
            if (_refreshTimer >= RefreshInterval)
            {
                _refreshTimer = 0f;
                RefreshSensors();
                if (_director == null) _director = FindAnyObjectByType<SimulationDirector>();
                if (_scenario == null) _scenario = FindAnyObjectByType<ScenarioController>();
                if (_controls == null) _controls = FindAnyObjectByType<GameControls>();
                if (_pilot == null) _pilot = FindAnyObjectByType<PlayerDroneController>();
                if (_menu == null) _menu = FindAnyObjectByType<ScenarioMenu>();
                if (_cameraRig == null) _cameraRig = FindAnyObjectByType<CameraRig>();
            }
        }

        private void RefreshSensors()
        {
            _sensors = FindObjectsByType<RadarSensor>(FindObjectsSortMode.None);
        }

        private void OnGUI()
        {
            // The mission-select briefing owns the screen while it is up; drawing the HUD over it
            // would only make it hard to read.
            if (_menu != null && _menu.IsOpen) return;

            HudTheme.Ensure();

            bool piloting = _pilot != null && _pilot.IsActive && _pilot.Controlled != null;

            // The fleet panel is optional (G). Visible whenever there is no GameControls to ask, so
            // the default behaviour is unchanged.
            bool fleetVisible = _controls == null || _controls.FleetPanelVisible;

            float leftBottom = DrawMissionPanel(Margin, Margin);
            DrawRadarPanel(Margin, leftBottom + 12f);

            float rightX = Screen.width - Margin - ColWidth;
            float rightY = Margin;
            if (piloting) rightY = DrawPilotIdentity(rightX, rightY) + 12f;
            if (fleetVisible) DrawFleetPanel(rightX, rightY);

            if (piloting)
            {
                DrawPilotOverlay();
                DrawRadarScope();
            }

            DrawMissileWarning();
            DrawControlStrip(piloting, fleetVisible);
            DrawEndScreen();
        }

        // ------------------------------------------------------------------ mission panel

        /// <summary>Top-left mission panel. Returns the panel's bottom edge.</summary>
        private float DrawMissionPanel(float x, float y)
        {
            MissionState m = _director != null ? _director.Mission : null;
            float h = m == null ? 94f : 206f;

            HudTheme.Panel(new Rect(x, y, ColWidth, h));
            HudTheme.Header(new Rect(x, y, ColWidth, HeaderH), "GÖREV",
                            $"M-{MissionIndex(ScenarioController.SelectedKind):00}");

            float cx = x + 10f;
            float cw = ColWidth - 20f;
            float cy = y + HeaderH + 8f;

            HudTheme.Draw(new Rect(cx, cy, cw, 36f),
                          Upper(ScenarioLibrary.Title(ScenarioController.SelectedKind)),
                          HudTheme.Title, HudTheme.Text);
            cy += 40f;

            if (m == null)
            {
                HudTheme.Draw(new Rect(cx, cy, cw, 16f), "BAŞLATILIYOR...",
                              HudTheme.Label, HudTheme.TextDim);
                return y + h;
            }

            // DURUM + SÜRE
            MissionStatus shown = _scenario != null ? MapScenario(_scenario.Status) : m.Status;
            StatBox(new Rect(cx, cy, cw - 114f, 36f), "DURUM", StatusText(shown), StatusColor(shown));
            StatBox(new Rect(cx + cw - 110f, cy, 110f, 36f), "SÜRE", Clock(m.ElapsedTime), HudTheme.Text);
            cy += 44f;

            // DALGA + DÜŞMAN (live count on the field)
            int wave = _scenario != null ? _scenario.CurrentWaveNumber : 1;
            int waves = _scenario != null ? _scenario.TotalWaves : 1;
            int live = _scenario != null ? _scenario.LiveEnemies : _director.HostilesAlive;
            KeyValueRow(new Rect(cx, cy, cw * 0.5f - 6f, 18f), "DALGA", $"{wave}/{waves}", HudTheme.Amber);
            KeyValueRow(new Rect(cx + cw * 0.5f + 6f, cy, cw * 0.5f - 6f, 18f), "DÜŞMAN",
                        live.ToString(), HudTheme.Critical);
            cy += 26f;

            // İMHA / KAYIP / SKOR
            float cell = (cw - 8f) / 3f;
            // Running totals only: the director's MissionState is a pure counter (no hostile total and
            // no friendly-loss limit), because win/lose is the ScenarioController's job. The mission
            // length is already shown by the DALGA row above.
            StatCell(new Rect(cx, cy, cell, 38f), "İMHA",
                     m.HostilesDestroyed.ToString(), HudTheme.Text);
            StatCell(new Rect(cx + cell + 4f, cy, cell, 38f), "KAYIP",
                     m.FriendliesLost.ToString(),
                     m.FriendliesLost > 0 ? HudTheme.Critical : HudTheme.Ok);
            StatCell(new Rect(cx + (cell + 4f) * 2f, cy, cell, 38f), "SKOR",
                     m.Score.ToString(), HudTheme.Amber);
            cy += 42f;

            // Live head-count, kept from the previous HUD ("sahada" figures).
            HudTheme.Draw(new Rect(cx, cy, cw, 14f),
                          $"SAHADA · DÜŞMAN {_director.HostilesAlive} · DOST {_director.FriendliesAlive}",
                          HudTheme.Small, HudTheme.TextFaint);

            return y + h;
        }

        // ------------------------------------------------------------------ radar contacts

        /// <summary>
        /// Left column, below the mission panel: one row per <see cref="RadarSensor"/> that currently
        /// holds a contact, showing the track number, a type token and the slant range in metres.
        /// </summary>
        private void DrawRadarPanel(float x, float y)
        {
            CollectContacts();

            int rows = Mathf.Max(1, _contactId.Count);
            float h = HeaderH + rows * RowH + 6f;

            HudTheme.Panel(new Rect(x, y, ColWidth, h));
            HudTheme.Header(new Rect(x, y, ColWidth, HeaderH), "RADAR TEMASLARI",
                            $"{_contactId.Count} AKTİF");

            float ry = y + HeaderH + 3f;

            if (_contactId.Count == 0)
            {
                HudTheme.Draw(new Rect(x + 12f, ry, ColWidth - 24f, RowH), "(TEMAS YOK)",
                              HudTheme.Small, HudTheme.TextFaint);
                return;
            }

            for (int i = 0; i < _contactId.Count; i++)
            {
                Color accent = _contactAccent[i];

                HudTheme.Fill(new Rect(x + 10f, ry + 4f, 3f, RowH - 8f), accent);
                HudTheme.Draw(new Rect(x + 20f, ry, 46f, RowH), _contactId[i],
                              HudTheme.Label, HudTheme.Text);
                HudTheme.Tag(new Rect(x + 70f, ry + 3f, 48f, RowH - 6f), _contactType[i], accent);
                DrawRight(new Rect(x + ColWidth - 80f, ry, 68f, RowH), _contactRange[i],
                          HudTheme.Label, HudTheme.Text);

                if (i < _contactId.Count - 1)
                    HudTheme.Fill(new Rect(x + 1f, ry + RowH, ColWidth - 2f, 1f), HudTheme.Line);

                ry += RowH;
            }
        }

        /// <summary>
        /// Rebuilds the contact rows. Rows whose target id cannot be resolved in the
        /// <see cref="TargetRegistry"/> are skipped, so a stale id never draws a half-empty row.
        /// </summary>
        private void CollectContacts()
        {
            _contactId.Clear();
            _contactType.Clear();
            _contactAccent.Clear();
            _contactRange.Clear();

            if (_sensors == null) return;

            for (int i = 0; i < _sensors.Length; i++)
            {
                RadarSensor s = _sensors[i];
                if (s == null || !s.HasContact) continue;

                Targetable t = TargetRegistry.FindById(s.ContactId);
                if (t == null) continue;

                Color accent;
                string type = ContactType(t, out accent);

                float range = Vector3.Distance(s.transform.position, s.EstimatedPosition);

                _contactId.Add($"T-{Mathf.Max(0, s.ContactId):00}");
                _contactType.Add(type);
                _contactAccent.Add(accent);
                _contactRange.Add($"{range:0} m");
            }
        }

        /// <summary>
        /// Classifies a radar contact from the components on the resolved <see cref="Targetable"/>:
        /// an <see cref="EnemyDroneController"/> is an interceptor (AVCI), an
        /// <see cref="AirDefenseSite"/> is a SAM or AAA battery — told apart by the name prefix
        /// <see cref="ScenarioController"/> spawns them with ("SAM_W1_0" / "AAA_W1_0"), since the
        /// site's ranges are private — and anything else is a plain objective (HEDEF).
        /// </summary>
        private static string ContactType(Targetable t, out Color accent)
        {
            accent = HudTheme.TextDim;
            if (t == null) return "HEDEF";

            if (t.GetComponent<EnemyDroneController>() != null)
            {
                accent = HudTheme.TextDim;
                return "AVCI";
            }

            if (t.GetComponent<AirDefenseSite>() != null)
            {
                string n = t.name;
                if (n != null && n.StartsWith("AAA"))
                {
                    accent = HudTheme.Amber;
                    return "AAA";
                }
                accent = HudTheme.Critical;
                return "SAM";
            }

            accent = HudTheme.TextFaint;
            return "HEDEF";
        }

        // ------------------------------------------------------------------ fleet status

        /// <summary>Right-hand column: one block per friendly drone with state tag and gauges.</summary>
        private void DrawFleetPanel(float x, float y)
        {
            IReadOnlyList<IhaController> friendlies = _director != null ? _director.Friendlies : null;

            // Measure first so the panel frame wraps exactly around its blocks.
            float bodyH = 0f;
            int shown = 0;
            if (friendlies != null)
            {
                for (int i = 0; i < friendlies.Count; i++)
                {
                    IhaController c = friendlies[i];
                    if (c == null) continue;
                    bodyH += BlockHeight(c);
                    shown++;
                }
            }
            if (shown == 0) bodyH = 26f;

            float h = HeaderH + bodyH + 4f;
            HudTheme.Panel(new Rect(x, y, ColWidth, h));

            int alive = _director != null ? _director.FriendliesAlive : shown;
            HudTheme.Header(new Rect(x, y, ColWidth, HeaderH), "FİLO DURUMU", $"{alive} HAVADA");

            float by = y + HeaderH + 2f;

            if (shown == 0)
            {
                HudTheme.Draw(new Rect(x + 12f, by, ColWidth - 24f, 22f), "(DRONE YOK)",
                              HudTheme.Small, HudTheme.TextFaint);
                return;
            }

            int drawn = 0;
            for (int i = 0; i < friendlies.Count; i++)
            {
                IhaController c = friendlies[i];
                if (c == null) continue;

                float blockH = BlockHeight(c);
                DrawDroneBlock(new Rect(x, by, ColWidth, blockH), c);
                drawn++;
                by += blockH;

                if (drawn < shown)
                    HudTheme.Fill(new Rect(x + 1f, by, ColWidth - 2f, 1f), HudTheme.Line);
            }
        }

        /// <summary>Height of one drone block: header row plus one row per gauge actually carried.</summary>
        private static float BlockHeight(IhaController c)
        {
            int bars = 1;                                   // fuel is always present
            if (c.Gun != null) bars++;                      // gun ammo
            if (c as SihaController != null) bars++;        // missiles (armed SİHA only)
            if (c.Countermeasures != null) bars++;          // flare/chaff
            return 22f + bars * (BarH + 4f) + 8f;
        }

        private void DrawDroneBlock(Rect r, IhaController c)
        {
            float x = r.x + 10f;
            float w = r.width - 20f;
            float y = r.y + 4f;

            HudTheme.Draw(new Rect(x, y, 96f, 18f), Upper(c.name), HudTheme.Value, HudTheme.Text);

            EngagementState st = c.State;
            HudTheme.Tag(new Rect(x + 98f, y + 2f, 58f, 14f), StateText(st), StateColor(st));

            // Base servicing in progress (dwelling at base to refuel/rearm).
            if (c.IsResupplying)
                HudTheme.Tag(new Rect(x + 160f, y + 2f, 74f, 14f),
                             $"İKMAL %{c.ResupplyProgress * 100f:0}", HudTheme.Ok);

            // A dry tank is a death sentence (dead-stick descent), so flag it loudly.
            if (c.IsOutOfFuel)
                DrawRight(new Rect(x + w - 76f, y, 76f, 18f), "[YAKIT BİTTİ]",
                          HudTheme.Small, HudTheme.Critical);

            float by = y + 22f;
            HudTheme.Bar(new Rect(x, by, w, BarH), c.FuelFraction, "YAKIT", Pct(c.FuelFraction));
            by += BarH + 4f;

            GunTurret gun = c.Gun;
            if (gun != null)
            {
                HudTheme.Bar(new Rect(x, by, w, BarH), gun.AmmoFraction, "TOP", Pct(gun.AmmoFraction));
                by += BarH + 4f;
            }

            var siha = c as SihaController;
            if (siha != null)
            {
                HudTheme.Bar(new Rect(x, by, w, BarH), siha.AmmoFraction, "FÜZE", Pct(siha.AmmoFraction));
                by += BarH + 4f;
            }

            CountermeasureDispenser cm = c.Countermeasures;
            if (cm != null)
                HudTheme.Bar(new Rect(x, by, w, BarH), cm.ChargeFraction, "FLARE", Pct(cm.ChargeFraction));
        }

        // ------------------------------------------------------------------ pilot mode

        /// <summary>Top-right identity block while the player flies a drone. Returns its bottom edge.</summary>
        private float DrawPilotIdentity(float x, float y)
        {
            IhaController drone = _pilot.Controlled;
            float h = drone.IsOutOfFuel ? 74f : 56f;

            HudTheme.Panel(new Rect(x, y, ColWidth, h));
            HudTheme.Draw(new Rect(x + 10f, y + 6f, ColWidth - 20f, 20f), Upper(drone.name),
                          HudTheme.Value, HudTheme.Text);
            HudTheme.Tag(new Rect(x + 10f, y + 30f, 88f, 16f), "PİLOT MODU", HudTheme.Amber);

            if (drone.IsResupplying)
                HudTheme.Tag(new Rect(x + 104f, y + 30f, 84f, 16f),
                             $"İKMAL %{drone.ResupplyProgress * 100f:0}", HudTheme.Ok);

            if (drone.IsOutOfFuel)
                HudTheme.Draw(new Rect(x + 10f, y + 52f, ColWidth - 20f, 18f),
                              "YAKIT BİTTİ - SÜZÜLÜYOR", HudTheme.Warning, HudTheme.Critical);

            return y + h;
        }

        /// <summary>
        /// The flying instruments: centre crosshair, speed and altitude boxes flanking it, the
        /// weapon-status block at the bottom centre and the afterburner/evade indicators.
        /// </summary>
        private void DrawPilotOverlay()
        {
            IhaController drone = _pilot.Controlled;
            float cx = Screen.width * 0.5f;
            float cy = Screen.height * 0.5f;

            DrawGunPipper(drone);

            // HIZ (left of the crosshair) + the fuel gauge under it.
            var speedBox = new Rect(cx - 250f, cy - 24f, 130f, 46f);
            ReadoutBox(speedBox, "HIZ", $"{_pilot.Speed:0}", "m/s");
            HudTheme.Bar(new Rect(speedBox.x, speedBox.yMax + 8f, speedBox.width, BarH),
                         drone.FuelFraction, "YAKIT", Pct(drone.FuelFraction));

            // İRTİFA (right of the crosshair).
            var altBox = new Rect(cx + 120f, cy - 24f, 130f, 46f);
            ReadoutBox(altBox, "İRTİFA", $"{drone.transform.position.y:0}", "m");

            // Weapon block, bottom centre above the control strip.
            GunTurret gun = drone.Gun;
            var siha = drone as SihaController;
            CountermeasureDispenser cm = drone.Countermeasures;

            const float wpnW = 380f;
            float wpnH = HeaderH + 3f * (BarH + 5f) + 8f;
            float wpnY = Screen.height - StripH - 12f - wpnH;
            var wpn = new Rect(cx - wpnW * 0.5f, wpnY, wpnW, wpnH);

            HudTheme.Panel(wpn);
            HudTheme.Header(new Rect(wpn.x, wpn.y, wpn.width, HeaderH), "SİLAH DURUMU", string.Empty);

            float bx = wpn.x + 12f;
            float bw = wpn.width - 24f;
            float by = wpn.y + HeaderH + 5f;

            if (gun != null)
                HudTheme.Bar(new Rect(bx, by, bw, BarH), gun.AmmoFraction, "TOP", Pct(gun.AmmoFraction));
            else
                HudTheme.Draw(new Rect(bx, by, bw, BarH), "TOP: YOK", HudTheme.Small, HudTheme.TextFaint);
            by += BarH + 5f;

            if (siha != null)
                HudTheme.Bar(new Rect(bx, by, bw, BarH), siha.AmmoFraction, "FÜZE", Pct(siha.AmmoFraction));
            else
                HudTheme.Draw(new Rect(bx, by, bw, BarH), "FÜZE: YOK", HudTheme.Small, HudTheme.TextFaint);
            by += BarH + 5f;

            if (cm != null)
                HudTheme.Bar(new Rect(bx, by, bw, BarH), cm.ChargeFraction, "FLARE", Pct(cm.ChargeFraction));
            else
                HudTheme.Draw(new Rect(bx, by, bw, BarH), "FLARE: YOK", HudTheme.Small, HudTheme.TextFaint);

            // Active special abilities (E afterburner / X evasive break turn). The break turn also
            // reports its cooldown, so the pilot can tell "not ready yet" from "did nothing".
            float ax = Screen.width - Margin - 150f;
            HudTheme.Tag(new Rect(ax, wpnY, 150f, 22f), "ART YAKICI",
                         _pilot.AfterburnerActive ? HudTheme.Amber : HudTheme.TextFaint);

            string evadeText;
            Color evadeAccent;
            if (_pilot.EvadeActive)
            {
                // Confirmation flash: the key press is visibly doing something.
                float flash = 0.6f + 0.4f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 12f));
                evadeText = "KAÇIŞ · ETKİN";
                evadeAccent = new Color(HudTheme.Ok.r, HudTheme.Ok.g, HudTheme.Ok.b, flash);
            }
            else if (!_pilot.EvadeReady)
            {
                evadeText = $"KAÇIŞ · {_pilot.EvadeCooldownRemaining:0.0} s";
                evadeAccent = HudTheme.TextFaint;
            }
            else
            {
                evadeText = "KAÇIŞ · HAZIR [X]";
                evadeAccent = HudTheme.Amber;
            }

            var evadeTag = new Rect(ax, wpnY + 28f, 150f, 22f);
            HudTheme.Tag(evadeTag, evadeText, evadeAccent);

            // Recharge bar along the bottom edge of the chip.
            float evadeFraction = Mathf.Clamp01(_pilot.EvadeReadyFraction);
            HudTheme.Fill(new Rect(evadeTag.x + 1f, evadeTag.yMax - 3f, evadeTag.width - 2f, 2f),
                          HudTheme.Track);
            if (evadeFraction > 0f)
                HudTheme.Fill(new Rect(evadeTag.x + 1f, evadeTag.yMax - 3f,
                                       (evadeTag.width - 2f) * evadeFraction, 2f),
                              _pilot.EvadeReady ? HudTheme.Ok : HudTheme.Amber);

            HudTheme.Draw(new Rect(ax, wpnY + 54f, 150f, 14f), "YANIK = ETKİN · SÖNÜK = PASİF",
                          HudTheme.Small, HudTheme.TextFaint);
        }

        /// <summary>A bordered instrument readout: dim caption, big value and a unit suffix.</summary>
        private static void ReadoutBox(Rect r, string caption, string value, string unit)
        {
            HudTheme.Fill(r, HudTheme.PanelBg);
            HudTheme.Border(r, HudTheme.Amber);
            HudTheme.Draw(new Rect(r.x + 8f, r.y + 4f, r.width - 16f, 12f), caption,
                          HudTheme.Small, HudTheme.Amber);
            HudTheme.Draw(new Rect(r.x + 8f, r.y + 18f, r.width - 16f, 24f), value,
                          HudTheme.Verdict24(), HudTheme.Text);
            DrawRight(new Rect(r.x + 8f, r.y + 22f, r.width - 16f, 18f), unit,
                      HudTheme.Small, HudTheme.TextDim);
        }

        // ------------------------------------------------------------------ gun pipper

        /// <summary>
        /// Draws the gun pipper where the rounds actually go: <see cref="Sim.Core.GunPipper"/> gives the
        /// world point the bore reaches at the gun's own effective range, that point is projected through
        /// the camera the <see cref="CameraRig"/> drives, and the reticle is drawn there. Because it is a
        /// WORLD point, pitching the nose up or down slides the pipper across the screen instead of
        /// leaving it welded to the centre; the reticle is additionally rolled with the airframe's bank.
        ///
        /// Nothing is drawn when the aim point falls behind the camera or off the viewport.
        /// Presentation only: no gameplay value is read for anything but display.
        /// </summary>
        private void DrawGunPipper(IhaController drone)
        {
            if (drone == null) return;

            Camera cam = ResolveCamera();
            if (cam == null) return;

            Transform t = drone.transform;

            // Range: the gun's OWN effective range when the aircraft carries one, otherwise the pilot's
            // advisory figure. Both already exist — no gameplay number is introduced or changed here.
            GunTurret turret = drone.Gun;
            float range = turret != null ? turret.Gun.EffectiveRange
                                         : (_pilot != null ? _pilot.GunRange : 60f);
            if (range <= 1f) range = 60f;

            Vector3 aim = GunPipper.AimPoint(t.position, t.forward, pipperMuzzleSpeed, range, pipperGravity);

            Vector3 sp = cam.WorldToScreenPoint(aim);
            if (sp.z <= 0f) return;                                  // behind the camera
            if (sp.x < 0f || sp.x > Screen.width) return;            // outside the viewport
            if (sp.y < 0f || sp.y > Screen.height) return;

            // IMGUI's Y axis grows downward, the camera's upward.
            var p = new Vector2(sp.x, Screen.height - sp.y);

            Color c = TargetInGunRange(drone, range) ? HudTheme.Critical : HudTheme.Amber;
            float bank = BankAngle(t, cam);

            // Roll the reticle graphic with the airframe. The matrix is restored in a finally block so
            // an exception inside the drawing can never leave the whole GUI rotated.
            Matrix4x4 previous = GUI.matrix;
            try
            {
                GUIUtility.RotateAroundPivot(bank, p);
                HudTheme.Crosshair(p, PipperSize, c);
                Ring(p, PipperSize * 0.34f, c, 48);
            }
            finally
            {
                GUI.matrix = previous;
            }
        }

        /// <summary>
        /// The camera the <see cref="CameraRig"/> drives, falling back to the tagged main camera when
        /// there is no rig (hand-authored scenes). Cached; re-resolved whenever the cache goes null.
        /// </summary>
        private Camera ResolveCamera()
        {
            if (_camera != null) return _camera;
            if (_cameraRig != null) _camera = _cameraRig.GetComponent<Camera>();
            if (_camera == null) _camera = Camera.main;
            return _camera;
        }

        /// <summary>
        /// How far the airframe is banked as seen by the camera, in degrees, positive clockwise on
        /// screen (the direction IMGUI rotates for a positive angle). Read off the "Model" child,
        /// because <see cref="BankingVisual"/> rolls that child and never the root transform.
        /// </summary>
        private float BankAngle(Transform aircraft, Camera cam)
        {
            if (aircraft == null || cam == null) return 0f;

            // Unity's overloaded == is what makes this safe: a destroyed owner compares equal to null
            // and therefore never matches a live aircraft, so the stale cache is dropped.
            if (_bankOwner != aircraft)
            {
                _bankOwner = aircraft;
                Transform model = aircraft.Find("Model");
                _bankSource = model != null ? model : aircraft;
            }

            Transform banked = _bankSource != null ? _bankSource : aircraft;

            Transform camT = cam.transform;
            float right = Vector3.Dot(banked.up, camT.right);
            float up = Vector3.Dot(banked.up, camT.up);
            if (Mathf.Abs(right) < 1e-5f && Mathf.Abs(up) < 1e-5f) return 0f;

            return Mathf.Atan2(right, up) * Mathf.Rad2Deg;
        }

        /// <summary>
        /// True when the drone's currently detected target is a live hostile inside the given gun
        /// range — the pipper turns to the critical colour then. Read-only.
        /// </summary>
        private static bool TargetInGunRange(IhaController drone, float range)
        {
            if (drone == null || !drone.HasTarget) return false;

            Targetable t = TargetRegistry.FindById(drone.DetectedId);
            if (t == null) return false;
            if (t.Faction == 0) return false;
            if (t.Health != null && t.Health.IsDestroyed) return false;

            return Vector3.Distance(drone.transform.position, t.transform.position) <= range;
        }

        // ------------------------------------------------------------------ pilot radar scope

        /// <summary>
        /// Nose-up radar scope (plan-position indicator) in the bottom-right corner, drawn only while
        /// the player is flying. Every blip is projected with <see cref="Sim.Core.RadarScope"/> from the
        /// piloted aircraft's position and heading: hostiles are red squares, friendlies teal squares,
        /// the currently detected target is a larger amber square and munitions homing on the player are
        /// blinking triangles with a threat-axis line drawn toward the centre.
        /// </summary>
        private void DrawRadarScope()
        {
            IhaController drone = _pilot != null ? _pilot.Controlled : null;
            if (drone == null) return;

            float range = Mathf.Max(1f, scopeRange);
            Vector3 self = drone.transform.position;
            Vector3 forward = drone.transform.forward;
            Targetable selfTargetable = drone.GetComponent<Targetable>();

            // Sit above the pilot overlay's ability tags, hugging the bottom-right corner.
            float wpnH = HeaderH + 3f * (BarH + 5f) + 8f;
            float blockBottom = Screen.height - StripH - 12f - wpnH - 12f;
            var disc = new Rect(Screen.width - Margin - ScopeSize,
                                blockBottom - ScopeInfoH - ScopeSize, ScopeSize, ScopeSize);

            Vector2 c = disc.center;
            float radius = ScopeSize * 0.5f - 2f;

            // Face: dark disc, hairline rim, three range rings and a centre crosshair.
            FillDisc(c, radius, new Color(0.02f, 0.04f, 0.045f, 0.92f));
            Ring(c, radius, HudTheme.Line, 72);
            Ring(c, radius * 0.66f, HudTheme.Line, 56);
            Ring(c, radius * 0.33f, HudTheme.Line, 40);
            HudTheme.Fill(new Rect(c.x - radius, c.y - 0.5f, radius * 2f, 1f), HudTheme.Line);
            HudTheme.Fill(new Rect(c.x - 0.5f, c.y - radius, 1f, radius * 2f), HudTheme.Line);

            // Nose marker at the top of the scope.
            BlipTriangle(new Vector2(c.x, disc.y - 2f), 9f, HudTheme.Amber);

            // Sweep line. Time.time is scaled, so the sweep freezes with the simulation when paused.
            float sweep = Time.time * 90f * Mathf.Deg2Rad;
            var sweepDir = new Vector2(Mathf.Sin(sweep), -Mathf.Cos(sweep));
            DottedLine(c, c + sweepDir * radius,
                       new Color(HudTheme.Amber.r, HudTheme.Amber.g, HudTheme.Amber.b, 0.5f), 2f, 26);

            // Own ship at the centre.
            HudTheme.Fill(new Rect(c.x - 2f, c.y - 2f, 4f, 4f), HudTheme.Ok);

            // ---- unit blips -------------------------------------------------------------
            TargetRegistry.Prune();
            List<Targetable> all = TargetRegistry.All;
            for (int i = 0; i < all.Count; i++)
            {
                Targetable t = all[i];
                if (t == null) continue;
                if (selfTargetable != null && t == selfTargetable) continue;
                if (t.Health != null && t.Health.IsDestroyed) continue;

                if (!RadarScope.TryProject(self, forward, t.transform.position, range, out Vector2 p))
                    continue;

                var screen = new Vector2(c.x + p.x * radius, c.y - p.y * radius);

                bool locked = drone.HasTarget && t.Id == drone.DetectedId;
                if (locked)
                    BlipSquare(screen, 9f, HudTheme.Amber);
                else if (t.Faction == 0)
                    BlipSquare(screen, 5f, HudTheme.Ok);
                else
                    BlipSquare(screen, 7f, HudTheme.Critical);
            }

            // ---- incoming munitions ------------------------------------------------------
            int incoming = 0;
            if (selfTargetable != null)
            {
                // Blink on unscaled time so the threat still reads while the game is paused.
                bool blinkOn = Mathf.Sin(Time.unscaledTime * 9f) > -0.2f;

                GuidedMunition.Prune();
                List<GuidedMunition> munitions = GuidedMunition.Active;
                for (int i = 0; i < munitions.Count; i++)
                {
                    GuidedMunition m = munitions[i];
                    if (m == null) continue;
                    if (m.Target != selfTargetable) continue;
                    // A decoyed / lock-lost round is coasting ballistically: the shot is beaten, so it
                    // drops off the threat picture instead of lingering as a phantom contact.
                    if (!m.IsGuiding) continue;

                    incoming++;

                    if (!blinkOn) continue;
                    if (!RadarScope.TryProject(self, forward, m.transform.position, range, out Vector2 p))
                        continue;

                    var screen = new Vector2(c.x + p.x * radius, c.y - p.y * radius);

                    // Threat axis: a short dotted run from the blip toward own ship.
                    Vector2 toCentre = c - screen;
                    float len = toCentre.magnitude;
                    if (len > 1f)
                    {
                        Vector2 dir = toCentre / len;
                        DottedLine(screen, screen + dir * Mathf.Min(26f, len),
                                   HudTheme.Critical, 2f, 8);
                    }

                    BlipTriangle(screen, 11f, HudTheme.Critical);
                }
            }

            // ---- threat readout under the scope ------------------------------------------
            var info = new Rect(disc.x, disc.yMax + 2f, ScopeSize, ScopeInfoH);
            if (incoming > 0)
            {
                float tti = _pilot.TimeToImpact;
                string time = float.IsPositiveInfinity(tti) ? "--" : $"{tti:0.0} s";
                HudTheme.Draw(info, $"FÜZE x{incoming} · {time}", HudTheme.Small, HudTheme.Critical);
            }
            else
            {
                HudTheme.Draw(info, "TEHDİT YOK", HudTheme.Small, HudTheme.TextFaint);
            }

            DrawRight(info, $"MENZİL {range:0} m", HudTheme.Small, HudTheme.TextDim);
        }

        /// <summary>Fills a circle out of stacked 1px rows (IMGUI can only draw rectangles).</summary>
        private static void FillDisc(Vector2 centre, float radius, Color color)
        {
            if (radius <= 0f) return;

            int rows = Mathf.Max(4, Mathf.RoundToInt(radius));
            float step = radius * 2f / rows;
            for (int i = 0; i < rows; i++)
            {
                float dy = -radius + step * (i + 0.5f);
                float halfW = Mathf.Sqrt(Mathf.Max(0f, radius * radius - dy * dy));
                if (halfW <= 0.5f) continue;
                HudTheme.Fill(new Rect(centre.x - halfW, centre.y + dy - step * 0.5f,
                                       halfW * 2f, step + 0.5f), color);
            }
        }

        /// <summary>Strokes a circle as evenly spaced dots — a dotted range ring.</summary>
        private static void Ring(Vector2 centre, float radius, Color color, int segments)
        {
            if (radius <= 0f || segments < 3) return;

            for (int i = 0; i < segments; i++)
            {
                float a = (i / (float)segments) * Mathf.PI * 2f;
                float x = centre.x + Mathf.Cos(a) * radius;
                float y = centre.y + Mathf.Sin(a) * radius;
                HudTheme.Fill(new Rect(x - 0.5f, y - 0.5f, 1.5f, 1.5f), color);
            }
        }

        /// <summary>Draws a straight run of dots between two points (IMGUI has no line primitive).</summary>
        private static void DottedLine(Vector2 from, Vector2 to, Color color, float dotSize, int dots)
        {
            if (dots < 2) return;

            for (int i = 0; i < dots; i++)
            {
                float t = i / (float)(dots - 1);
                Vector2 p = Vector2.Lerp(from, to, t);
                HudTheme.Fill(new Rect(p.x - dotSize * 0.5f, p.y - dotSize * 0.5f, dotSize, dotSize),
                              color);
            }
        }

        /// <summary>A square contact blip centred on the given point, with a darker outline.</summary>
        private static void BlipSquare(Vector2 p, float size, Color color)
        {
            float half = size * 0.5f;
            var r = new Rect(p.x - half, p.y - half, size, size);
            HudTheme.Fill(r, color);
            HudTheme.Border(r, new Color(0f, 0f, 0f, 0.6f));
        }

        /// <summary>A small upward triangle blip, stacked out of rows like the warning triangle.</summary>
        private static void BlipTriangle(Vector2 p, float size, Color color)
        {
            const int rows = 5;
            float rowH = size / rows;
            float half = size * 0.5f;
            for (int i = 0; i < rows; i++)
            {
                float t = (i + 1) / (float)rows;
                float w = size * t;
                HudTheme.Fill(new Rect(p.x - w * 0.5f, p.y - half + i * rowH, w, rowH + 0.5f), color);
            }
        }

        // ------------------------------------------------------------------ missile warning

        /// <summary>
        /// Pulsing centred missile warning band. Follows the PILOTED drone when the player is flying,
        /// and otherwise warns about any friendly drone that has a munition homing on it.
        /// </summary>
        private void DrawMissileWarning()
        {
            bool incoming = false;
            float tti = float.PositiveInfinity;

            if (_pilot != null && _pilot.IsActive)
            {
                incoming = _pilot.MissileIncoming;
                tti = _pilot.TimeToImpact;
            }
            else if (_director != null && _director.Friendlies != null)
            {
                IReadOnlyList<IhaController> friendlies = _director.Friendlies;
                for (int i = 0; i < friendlies.Count; i++)
                {
                    IhaController c = friendlies[i];
                    if (c == null || !c.MissileIncoming) continue;
                    incoming = true;
                    if (c.TimeToImpact < tti) tti = c.TimeToImpact;
                }
            }

            if (!incoming) return;

            // Pulse so it cannot be missed. Unscaled time keeps it alive while paused.
            float pulse = 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 6f));
            Color red = HudTheme.Critical;
            var band = new Rect(Screen.width * 0.5f - 190f, Screen.height * 0.18f, 380f, 54f);

            HudTheme.Fill(band, new Color(0.10f, 0.05f, 0.047f, 0.94f));
            HudTheme.Border(band, new Color(red.r, red.g, red.b, pulse));

            HudTheme.WarningTriangle(new Rect(band.x + 14f, band.y + 13f, 30f, 28f),
                                     new Color(red.r, red.g, red.b, pulse));

            string time = float.IsPositiveInfinity(tti) ? string.Empty : $"{tti:0.0} s";
            HudTheme.Draw(new Rect(band.x + 56f, band.y, 120f, band.height), "FÜZE!",
                          HudTheme.Verdict28(), new Color(red.r, red.g, red.b, pulse));
            HudTheme.Fill(new Rect(band.x + 176f, band.y + 12f, 1f, band.height - 24f), HudTheme.Line);
            HudTheme.Draw(new Rect(band.x + 190f, band.y, 110f, band.height), time,
                          HudTheme.Verdict28(), HudTheme.Text);
            DrawRight(new Rect(band.x + 190f, band.y + 16f, band.width - 204f, 24f), "Q: FLARE",
                      HudTheme.Small, HudTheme.TextDim);

            DrawBreakCue(new Rect(band.x, band.yMax + 6f, band.width, 26f), tti, pulse);
        }

        /// <summary>
        /// The break-turn cue under the missile warning: the one thing the player has to read to know
        /// WHEN pressing X is worth anything.
        ///
        /// <para>
        /// The window comes from <see cref="Sim.Core.EvasiveManeuver.InBreakWindow"/> — it lives next
        /// to the manoeuvre logic, not here, so the cue and the steering can never drift apart. Amber
        /// while the shot is still too far out to break against, pulsing red the moment it enters the
        /// window, and faint with the remaining lock-out while the ability is recharging.
        /// </para>
        /// </summary>
        private void DrawBreakCue(Rect r, float tti, float pulse)
        {
            // Only the pilot has the ability; the AI breaks on its own.
            if (_pilot == null || !_pilot.IsActive) return;

            bool inWindow = EvasiveManeuver.InBreakWindow(tti);
            bool active = _pilot.EvadeActive;
            bool ready = _pilot.EvadeReady;

            string text;
            Color accent;

            if (active)
            {
                // Confirmation flash while the manoeuvre is being flown: pressing X visibly does
                // something. Faster pulse than the warning band so the two read apart.
                float flash = 0.6f + 0.4f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 12f));
                text = "KAÇIŞ MANEVRASI — UYGULANIYOR";
                accent = new Color(HudTheme.Ok.r, HudTheme.Ok.g, HudTheme.Ok.b, flash);
            }
            else if (!ready)
            {
                text = $"KAÇIŞ MANEVRASI — DOLUYOR {_pilot.EvadeCooldownRemaining:0.0} s";
                accent = HudTheme.TextFaint;
            }
            else if (inWindow)
            {
                text = "KAÇIŞ MANEVRASI — ŞİMDİ! [X]";
                accent = new Color(HudTheme.Critical.r, HudTheme.Critical.g, HudTheme.Critical.b, pulse);
            }
            else
            {
                text = $"KAÇIŞ MANEVRASI — BEKLE ({EvasiveManeuver.BreakWindowSeconds:0.0} s ALTINDA KIR)";
                accent = HudTheme.Amber;
            }

            HudTheme.Tag(r, text, accent);

            // Recharge bar hugging the bottom edge of the chip, so the cooldown is legible at a glance.
            float fraction = Mathf.Clamp01(_pilot.EvadeReadyFraction);
            HudTheme.Fill(new Rect(r.x + 1f, r.yMax - 3f, r.width - 2f, 2f), HudTheme.Track);
            if (fraction > 0f)
                HudTheme.Fill(new Rect(r.x + 1f, r.yMax - 3f, (r.width - 2f) * fraction, 2f),
                              ready ? HudTheme.Ok : HudTheme.Amber);
        }

        // ------------------------------------------------------------------ bottom strip

        /// <summary>
        /// Bottom control strip: every control hint the HUD used to list (camera, pilot, weapons,
        /// pause / time scale / restart / mission menu) plus the design's bar colour-code legend.
        /// </summary>
        private void DrawControlStrip(bool piloting, bool fleetVisible)
        {
            var strip = new Rect(0f, Screen.height - StripH, Screen.width, StripH);
            HudTheme.Fill(strip, new Color(HudTheme.Bg.r, HudTheme.Bg.g, HudTheme.Bg.b, 0.94f));
            HudTheme.Fill(new Rect(0f, strip.y, Screen.width, 1f), HudTheme.Line);

            float scale = _controls != null ? _controls.CurrentTimeScale : Time.timeScale;
            bool paused = _controls != null ? _controls.IsPaused : Time.timeScale == 0f;
            string pauseText = paused ? "DURAKLADI" : "DURAKLAT";

            float hintsW = Screen.width - 250f;
            var l1 = new Rect(Margin, strip.y + 4f, hintsW, 15f);
            var l2 = new Rect(Margin, strip.y + 19f, hintsW, 15f);
            var l3 = new Rect(Margin, strip.y + 34f, hintsW, 15f);

            bool cockpit = _cameraRig != null && _cameraRig.CockpitView;
            HudTheme.Draw(l1, "WASD + SAĞ TIK: KAMERA · TAB: DRONE TAKİP · F: SERBEST · C: PİLOT MODU"
                              + (piloting ? " (ÇIKIŞ)" : string.Empty)
                              + (piloting ? (cockpit ? " · V: TAKİP KAMERASI" : " · V: KOKPİT") : string.Empty),
                          HudTheme.Centered, HudTheme.TextFaint);
            HudTheme.Draw(l2, "W/S: GAZ · A/D: DÖNÜŞ · ↑/↓: YUNUSLAMA · SPACE: TOP · F: FÜZE · "
                              + "Q: FLARE · E: ART YAKICI · X: KAÇIŞ MANEVRASI (SERT KIRIŞ)",
                          HudTheme.Centered, HudTheme.TextFaint);
            string fleetHint = fleetVisible ? "GİZLE" : "GÖSTER";
            HudTheme.Draw(l3, $"P: {pauseText} · +/−: HIZ (x{scale:0.00}) · R: YENİDEN · "
                              + $"M: GÖREV MENÜSÜ · G: FİLO PANELİ ({fleetHint})",
                          HudTheme.Centered, HudTheme.TextFaint);

            // Bar colour-code legend (design: green >50, amber 20–50, red <20).
            float lx = Screen.width - 234f;
            float ly = strip.y + 20f;
            HudTheme.Draw(new Rect(lx, strip.y + 4f, 220f, 14f), "BAR RENK KODU",
                          HudTheme.Small, HudTheme.TextFaint);
            LegendChip(lx, ly, HudTheme.Ok, ">50");
            LegendChip(lx + 74f, ly, HudTheme.Amber, "20-50");
            LegendChip(lx + 152f, ly, HudTheme.Critical, "<20");
        }

        private static void LegendChip(float x, float y, Color c, string text)
        {
            HudTheme.Fill(new Rect(x, y + 4f, 12f, 8f), c);
            HudTheme.Draw(new Rect(x + 17f, y, 56f, 16f), text, HudTheme.Small, HudTheme.TextDim);
        }

        // ------------------------------------------------------------------ end screen

        /// <summary>
        /// Mission-report screen. Win/lose is owned by the <see cref="ScenarioController"/> (waves);
        /// if it is missing for any reason we fall back to the <see cref="MissionState"/> result so
        /// nothing breaks.
        /// </summary>
        private void DrawEndScreen()
        {
            MissionState m = _director != null ? _director.Mission : null;
            if (m == null) return;

            MissionStatus endStatus = _scenario != null ? MapScenario(_scenario.Status) : m.Status;
            if (endStatus != MissionStatus.Won && endStatus != MissionStatus.Lost) return;

            bool won = endStatus == MissionStatus.Won;
            Color accent = won ? HudTheme.Ok : HudTheme.Critical;

            // Dim the field behind the report.
            HudTheme.Fill(new Rect(0f, 0f, Screen.width, Screen.height),
                          new Color(HudTheme.Bg.r, HudTheme.Bg.g, HudTheme.Bg.b, 0.80f));

            const float w = 640f;
            const float h = 300f;
            var panel = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);

            HudTheme.Fill(panel, HudTheme.PanelBg);
            HudTheme.Border(panel, accent);
            HudTheme.Header(new Rect(panel.x, panel.y, w, HeaderH), "GÖREV RAPORU",
                            Upper(ScenarioLibrary.Title(ScenarioController.SelectedKind)));

            float x = panel.x + 20f;
            float cw = w - 40f;
            float y = panel.y + HeaderH + 16f;

            HudTheme.Draw(new Rect(x, y, cw, 52f), won ? "GÖREV BAŞARILI" : "GÖREV BAŞARISIZ",
                          HudTheme.Verdict, accent);
            y += 58f;

            // Star rating (filled/empty), from the same MissionGrade call as before.
            int stars = MissionGrade.Stars(endStatus, m.FriendliesLost, m.ElapsedTime);
            stars = Mathf.Clamp(stars, 0, 3);
            string starText = new string('★', stars) + new string('☆', 3 - stars);
            HudTheme.Draw(new Rect(x, y, cw, 34f), starText, HudTheme.Verdict28C(), HudTheme.Amber);
            DrawCenter(new Rect(x, y + 34f, cw, 14f), $"DERECE {stars} / 3",
                       HudTheme.Small, HudTheme.TextDim);
            y += 56f;

            // Stats row.
            float cell = (cw - 12f) / 4f;
            StatCell(new Rect(x, y, cell, 44f), "SÜRE", Clock(m.ElapsedTime), HudTheme.Text);
            StatCell(new Rect(x + cell + 4f, y, cell, 44f), "İMHA",
                     m.HostilesDestroyed.ToString(), won ? HudTheme.Ok : HudTheme.Text);
            StatCell(new Rect(x + (cell + 4f) * 2f, y, cell, 44f), "KAYIP",
                     m.FriendliesLost.ToString(),
                     m.FriendliesLost > 0 ? HudTheme.Critical : HudTheme.Ok);
            StatCell(new Rect(x + (cell + 4f) * 3f, y, cell, 44f), "SKOR",
                     m.Score.ToString(), HudTheme.Amber);
            y += 52f;

            // Actions.
            ActionBox(new Rect(x, y, cw * 0.5f - 6f, 34f), "R", "YENİDEN BAŞLAT", HudTheme.Amber);
            ActionBox(new Rect(x + cw * 0.5f + 6f, y, cw * 0.5f - 6f, 34f), "M", "GÖREV MENÜSÜ",
                      HudTheme.TextDim);
        }

        private static void ActionBox(Rect r, string key, string label, Color accent)
        {
            HudTheme.Fill(r, new Color(accent.r * 0.14f, accent.g * 0.14f, accent.b * 0.14f, 0.92f));
            HudTheme.Border(r, accent);
            HudTheme.Fill(new Rect(r.x + 10f, r.y + 8f, 22f, 18f), accent);
            DrawCenter(new Rect(r.x + 10f, r.y + 8f, 22f, 18f), key, HudTheme.Small, HudTheme.Bg);
            HudTheme.Draw(new Rect(r.x + 42f, r.y, r.width - 52f, r.height), label,
                          HudTheme.Label, accent);
        }

        // ------------------------------------------------------------------ small helpers

        /// <summary>A bordered inset with a dim caption above a coloured value.</summary>
        private static void StatBox(Rect r, string caption, string value, Color valueColor)
        {
            HudTheme.Inset(r);
            HudTheme.Draw(new Rect(r.x + 7f, r.y + 3f, r.width - 14f, 12f), caption,
                          HudTheme.Small, HudTheme.TextDim);
            HudTheme.Draw(new Rect(r.x + 7f, r.y + 16f, r.width - 14f, 18f), value,
                          HudTheme.Value, valueColor);
        }

        /// <summary>A borderless statistic cell: dim caption over a large coloured number.</summary>
        private static void StatCell(Rect r, string caption, string value, Color valueColor)
        {
            HudTheme.Draw(new Rect(r.x, r.y, r.width, 12f), caption, HudTheme.Small, HudTheme.TextDim);
            HudTheme.Draw(new Rect(r.x, r.y + 14f, r.width, 22f), value, HudTheme.Value, valueColor);
        }

        /// <summary>A hairline-underlined "LABEL ........ value" row.</summary>
        private static void KeyValueRow(Rect r, string key, string value, Color valueColor)
        {
            HudTheme.Draw(new Rect(r.x, r.y, r.width * 0.6f, r.height), key,
                          HudTheme.Small, HudTheme.TextDim);
            DrawRight(new Rect(r.x, r.y, r.width, r.height), value, HudTheme.Label, valueColor);
            HudTheme.Fill(new Rect(r.x, r.yMax, r.width, 1f), HudTheme.Line);
        }

        /// <summary>Draws right-aligned text with a style that is normally left/centre aligned.</summary>
        private static void DrawRight(Rect r, string text, GUIStyle style, Color c)
        {
            if (style == null) return;
            TextAnchor prev = style.alignment;
            style.alignment = TextAnchor.MiddleRight;
            HudTheme.Draw(r, text, style, c);
            style.alignment = prev;
        }

        /// <summary>Draws centred text with a style that is normally left aligned.</summary>
        private static void DrawCenter(Rect r, string text, GUIStyle style, Color c)
        {
            if (style == null) return;
            TextAnchor prev = style.alignment;
            style.alignment = TextAnchor.MiddleCenter;
            HudTheme.Draw(r, text, style, c);
            style.alignment = prev;
        }

        private static string Pct(float fraction01)
        {
            return $"%{Mathf.Clamp01(fraction01) * 100f:0}";
        }

        /// <summary>mm:ss, the design's mission clock format.</summary>
        private static string Clock(float seconds)
        {
            if (seconds < 0f) seconds = 0f;
            int total = Mathf.FloorToInt(seconds);
            return $"{total / 60:00}:{total % 60:00}";
        }

        /// <summary>Uppercase using the invariant culture (Turkish 'i' casing is not wanted here).</summary>
        private static string Upper(string s)
        {
            return string.IsNullOrEmpty(s) ? string.Empty : s.ToUpperInvariant();
        }

        /// <summary>1-based position of the mission in <see cref="ScenarioLibrary.All"/> (M-01 … M-04).</summary>
        private static int MissionIndex(ScenarioKind kind)
        {
            ScenarioKind[] all = ScenarioLibrary.All;
            if (all != null)
            {
                for (int i = 0; i < all.Length; i++)
                    if (all[i] == kind) return i + 1;
            }
            return 1;
        }

        private static string StateText(EngagementState s)
        {
            switch (s)
            {
                case EngagementState.Engage: return "ANGAJE";
                case EngagementState.ReturnToBase: return "DÖNÜŞ";
                default: return "DEVRİYE";
            }
        }

        private static Color StateColor(EngagementState s)
        {
            switch (s)
            {
                case EngagementState.Engage: return HudTheme.Amber;
                case EngagementState.ReturnToBase: return HudTheme.TextDim;
                default: return HudTheme.Ok;
            }
        }

        private static string StatusText(MissionStatus s)
        {
            switch (s)
            {
                case MissionStatus.Won: return "BAŞARILI";
                case MissionStatus.Lost: return "BAŞARISIZ";
                default: return "DEVAM EDİYOR";
            }
        }

        private static Color StatusColor(MissionStatus s)
        {
            switch (s)
            {
                case MissionStatus.Won: return HudTheme.Ok;
                case MissionStatus.Lost: return HudTheme.Critical;
                default: return HudTheme.Ok;
            }
        }

        /// <summary>Maps the scenario's win/lose outcome onto the mission-status enum used by the report.</summary>
        private static MissionStatus MapScenario(ScenarioStatus s)
        {
            switch (s)
            {
                case ScenarioStatus.Won: return MissionStatus.Won;
                case ScenarioStatus.Lost: return MissionStatus.Lost;
                default: return MissionStatus.InProgress;
            }
        }
    }
}
