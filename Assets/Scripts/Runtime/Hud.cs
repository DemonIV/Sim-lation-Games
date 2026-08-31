using System.Collections.Generic;
using UnityEngine;
using Sim.Core;

namespace Sim.Runtime
{
    /// <summary>
    /// On-screen HUD drawn with IMGUI (<see cref="OnGUI"/>), so it needs zero Canvas/scene setup.
    /// Reads mission/score state from <see cref="SimulationDirector"/> and radar contacts from every
    /// <see cref="RadarSensor"/> in the scene, and shows a win/lose banner when the mission ends.
    /// </summary>
    public class Hud : MonoBehaviour
    {
        private SimulationDirector _director;
        private ScenarioController _scenario;
        private RadarSensor[] _sensors;
        private GameControls _controls;
        private PlayerDroneController _pilot;
        private ScenarioMenu _menu;

        // Cached GUI styles (built lazily on first OnGUI so we're on the GUI thread).
        private GUIStyle _labelStyle;
        private GUIStyle _titleStyle;
        private GUIStyle _smallStyle;
        private GUIStyle _warningStyle;
        private GUIStyle _bannerStyle;
        private GUIStyle _centerHintStyle;
        private GUIStyle _panelStyle;

        // 1x1 textures built once: a plain white one (tinted via GUI.color when drawing bars) and the
        // dark translucent panel background.
        private Texture2D _white;
        private Texture2D _panelTex;

        // Bar palette: green above 50%, amber between 20% and 50%, red below 20%.
        private static readonly Color BarGreen = new Color(0.30f, 0.80f, 0.35f, 0.95f);
        private static readonly Color BarAmber = new Color(0.95f, 0.70f, 0.20f, 0.95f);
        private static readonly Color BarRed = new Color(0.90f, 0.25f, 0.20f, 0.95f);
        private static readonly Color BarBack = new Color(0.08f, 0.09f, 0.10f, 0.90f);

        // Occasional refresh of the sensor list so newly spawned drones show up.
        private float _refreshTimer;
        private const float RefreshInterval = 2f;

        private void Start()
        {
            _director = FindAnyObjectByType<SimulationDirector>();
            _scenario = FindAnyObjectByType<ScenarioController>();
            _controls = FindAnyObjectByType<GameControls>();
            _pilot = FindAnyObjectByType<PlayerDroneController>();
            _menu = FindAnyObjectByType<ScenarioMenu>();
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
            }
        }

        private void RefreshSensors()
        {
            _sensors = FindObjectsByType<RadarSensor>(FindObjectsSortMode.None);
        }

        /// <summary>Builds a 1x1 texture of the given colour, kept out of the scene/asset database.</summary>
        private static Texture2D SolidTexture(Color color)
        {
            var tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, color);
            tex.Apply();
            tex.hideFlags = HideFlags.HideAndDontSave;
            return tex;
        }

        private void EnsureStyles()
        {
            if (_white == null) _white = SolidTexture(Color.white);
            if (_panelTex == null) _panelTex = SolidTexture(new Color(0.05f, 0.06f, 0.07f, 0.85f));

            if (_panelStyle == null)
            {
                _panelStyle = new GUIStyle(GUI.skin.box);
                _panelStyle.normal.background = _panelTex;
                _panelStyle.padding = new RectOffset(10, 10, 8, 8);
            }

            if (_labelStyle == null)
            {
                _labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 14 };
                _labelStyle.normal.textColor = new Color(0.88f, 0.90f, 0.92f);
            }
            if (_titleStyle == null)
            {
                _titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold };
                _titleStyle.normal.textColor = new Color(1f, 0.97f, 0.85f);
            }
            if (_smallStyle == null)
            {
                _smallStyle = new GUIStyle(GUI.skin.label) { fontSize = 11 };
                _smallStyle.normal.textColor = new Color(0.95f, 0.96f, 0.98f);
                _smallStyle.padding = new RectOffset(0, 0, 0, 0);
            }
            if (_warningStyle == null)
            {
                _warningStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold };
                _warningStyle.normal.textColor = new Color(1f, 0.45f, 0.35f);
            }
            if (_bannerStyle == null)
                _bannerStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 48,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };
            if (_centerHintStyle == null)
                _centerHintStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 22,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };
        }

        /// <summary>Colour code for a 0..1 gauge: green above 50%, amber down to 20%, then red.</summary>
        private static Color BarColor(float fraction01)
        {
            if (fraction01 > 0.5f) return BarGreen;
            if (fraction01 >= 0.2f) return BarAmber;
            return BarRed;
        }

        /// <summary>
        /// Draws a horizontal gauge: a dark background box, a colour-coded filled foreground box and
        /// the label drawn on top. Purely a presentation of a value the HUD already read.
        /// </summary>
        private void DrawBar(Rect r, float fraction01, Color fill, string label)
        {
            if (_white == null) return;

            float f = Mathf.Clamp01(fraction01);
            Color prev = GUI.color;

            GUI.color = BarBack;
            GUI.DrawTexture(r, _white);

            if (f > 0f)
            {
                GUI.color = fill;
                GUI.DrawTexture(new Rect(r.x, r.y, r.width * f, r.height), _white);
            }

            GUI.color = prev;
            GUI.Label(new Rect(r.x + 4f, r.y - 1f, r.width - 6f, r.height + 2f), label, _smallStyle);
        }

        /// <summary>Reserves a layout rect and draws a labelled, colour-coded gauge in it.</summary>
        private void LayoutBar(string label, float fraction01, float width)
        {
            Rect r = GUILayoutUtility.GetRect(width, 15f, GUILayout.Width(width));
            float f = Mathf.Clamp01(fraction01);
            DrawBar(r, f, BarColor(f), $"{label} {f * 100f:0}%");
        }

        private void OnGUI()
        {
            // The mission-select briefing owns the screen while it is up; drawing the HUD over it
            // would only make it hard to read. Checked before any BeginArea so nothing is left open.
            if (_menu != null && _menu.IsOpen) return;

            EnsureStyles();

            GUILayout.BeginArea(new Rect(10, 10, 350, 620), _panelStyle);
            GUILayout.Label("İHA/SİHA Taktik Simülasyonu", _titleStyle);
            // Active mission, as picked in the ScenarioMenu.
            GUILayout.Label($"Görev: {ScenarioLibrary.Title(ScenarioController.SelectedKind)}", _labelStyle);

            if (_director == null || _director.Mission == null)
            {
                GUILayout.Label("Başlatılıyor...", _labelStyle);
                GUILayout.EndArea();
                return;
            }

            MissionState m = _director.Mission;

            GUILayout.Label($"Durum: {StatusText(m.Status)}   Süre: {m.ElapsedTime:0.0}s", _labelStyle);
            GUILayout.Label($"Düşman: {m.HostilesDestroyed} / {m.HostilesTotal} imha   (sahada: {_director.HostilesAlive})", _labelStyle);
            GUILayout.Label($"Dost kayıp: {m.FriendliesLost} / {m.MaxFriendlyLosses}   (sahada: {_director.FriendliesAlive})", _labelStyle);
            GUILayout.Label($"Skor: {m.Score}", _labelStyle);
            if (_scenario != null)
                GUILayout.Label($"Dalga: {_scenario.CurrentWaveNumber}/{_scenario.TotalWaves}   Düşman: {_scenario.LiveEnemies}", _labelStyle);

            GUILayout.Space(6);
            GUILayout.Label("Radar temasları:", _labelStyle);
            int contacts = 0;
            if (_sensors != null)
            {
                for (int i = 0; i < _sensors.Length; i++)
                {
                    RadarSensor s = _sensors[i];
                    if (s != null && s.HasContact)
                    {
                        GUILayout.Label($"  Radar contact: id={s.ContactId}", _labelStyle);
                        contacts++;
                    }
                }
            }
            if (contacts == 0)
                GUILayout.Label("  (temas yok)", _labelStyle);

            GUILayout.Space(6);
            GUILayout.Label("Drone durumu:", _labelStyle);
            var friendlies = _director.Friendlies;
            int shown = 0;
            if (friendlies != null)
            {
                for (int i = 0; i < friendlies.Count; i++)
                {
                    IhaController c = friendlies[i];
                    if (c == null) continue;
                    // Gun ammo only when this drone actually carries a GunTurret.
                    GunTurret gun = c.Gun;
                    // A dry tank is a death sentence (dead-stick descent), so flag it loudly.
                    bool dry = c.IsOutOfFuel;
                    string fuelText = dry ? "  [YAKIT BİTTİ]" : string.Empty;
                    // Flare/chaff charges, only when this drone carries a dispenser.
                    CountermeasureDispenser cm = c.Countermeasures;
                    // Base servicing in progress (dwelling at base to refuel/rearm).
                    string supplyText = c.IsResupplying
                        ? $"  [İKMAL %{c.ResupplyProgress * 100f:0}]"
                        : string.Empty;

                    GUILayout.Label($"  {c.name}: {c.State}{fuelText}{supplyText}",
                                    dry ? _warningStyle : _labelStyle);

                    // Same numbers as before, now as colour-coded gauges.
                    GUILayout.BeginHorizontal();
                    LayoutBar("Yakıt", c.FuelFraction, 78f);
                    LayoutBar("Müh.", c.AmmoFraction, 78f);
                    if (gun != null) LayoutBar("Top", gun.AmmoFraction, 78f);
                    if (cm != null) LayoutBar("Flare", cm.ChargeFraction, 78f);
                    GUILayout.FlexibleSpace();
                    GUILayout.EndHorizontal();
                    shown++;
                }
            }
            if (shown == 0)
                GUILayout.Label("  (drone yok)", _labelStyle);

            GUILayout.Space(6);
            GUILayout.Label("WASD+Sağ tık: kamera, Tab: drone takip, F: serbest", _labelStyle);
            GUILayout.Label("C: pilot modu  Tab: drone seç  W/S: gaz  A/D: dönüş", _labelStyle);
            GUILayout.Label("↑/↓: yunuslama  Space: top  F: füze", _labelStyle);
            GUILayout.Label("Q: flare  E: art yakıcı  X: kaçış manevrası", _labelStyle);

            float scale = _controls != null ? _controls.CurrentTimeScale : Time.timeScale;
            bool paused = _controls != null ? _controls.IsPaused : Time.timeScale == 0f;
            string pauseText = paused ? "DURAKLADI" : "duraklat";
            GUILayout.Label($"P: {pauseText}  +/-: hız (x{scale:0.00})  R: yeniden", _labelStyle);
            GUILayout.Label("M: görev menüsü", _labelStyle);

            GUILayout.EndArea();

            DrawPilotPanel();
            DrawMissileWarning();

            // End-of-mission banner. Win/lose is owned by the ScenarioController (waves); if it is
            // missing for any reason we fall back to the MissionState-based result so nothing breaks.
            MissionStatus endStatus = _scenario != null ? MapScenario(_scenario.Status) : m.Status;

            if (endStatus == MissionStatus.Won || endStatus == MissionStatus.Lost)
            {
                bool won = endStatus == MissionStatus.Won;
                string text = won ? "GÖREV BAŞARILI" : "GÖREV BAŞARISIZ";
                Color prev = GUI.color;
                GUI.color = won ? Color.green : Color.red;
                var rect = new Rect(0, Screen.height * 0.5f - 60f, Screen.width, 80f);
                GUI.Label(rect, text, _bannerStyle);

                if (won)
                {
                    int stars = MissionGrade.Stars(endStatus, m.FriendliesLost, m.ElapsedTime);
                    string starText = new string('★', stars) + new string('☆', 3 - stars);
                    GUI.color = Color.yellow;
                    var starRect = new Rect(0, Screen.height * 0.5f + 24f, Screen.width, 60f);
                    GUI.Label(starRect, starText, _bannerStyle);
                }

                GUI.color = won ? Color.green : Color.red;
                var hintRect = new Rect(0, Screen.height * 0.5f + 84f, Screen.width, 40f);
                GUI.Label(hintRect, "R: yeniden başlat", _centerHintStyle);
                GUI.color = prev;
            }
        }

        /// <summary>
        /// Draws the "PİLOT MODU" block plus a centred crosshair while the player is flying a drone
        /// (see <see cref="PlayerDroneController"/>). A no-op when nobody is piloting.
        /// </summary>
        private void DrawPilotPanel()
        {
            if (_pilot == null || !_pilot.IsActive) return;

            IhaController drone = _pilot.Controlled;
            if (drone == null) return;

            GUILayout.BeginArea(new Rect(Screen.width - 270f, 10f, 260f, 240f), _panelStyle);
            GUILayout.Label("PİLOT MODU", _titleStyle);
            GUILayout.Label($"Drone: {drone.name}", _labelStyle);
            GUILayout.Label($"Hız: {_pilot.Speed:0} m/s", _labelStyle);
            GUILayout.Label($"İrtifa: {drone.transform.position.y:0} m", _labelStyle);
            LayoutBar("Yakıt", drone.FuelFraction, 236f);

            if (drone.IsOutOfFuel)
            {
                // Dead stick: no power left, the drone is gliding down toward a crash.
                Color prevFuel = GUI.color;
                GUI.color = Color.red;
                GUILayout.Label("YAKIT BİTTİ - SÜZÜLÜYOR", _titleStyle);
                GUI.color = prevFuel;
            }

            GunTurret gun = drone.Gun;
            if (gun != null) LayoutBar("Top", gun.AmmoFraction, 236f);
            else GUILayout.Label("Top: yok", _labelStyle);

            var siha = drone as SihaController;
            if (siha != null) LayoutBar("Füze", siha.AmmoFraction, 236f);

            CountermeasureDispenser cm = drone.Countermeasures;
            if (cm != null) LayoutBar("Flare", cm.ChargeFraction, 236f);
            else GUILayout.Label("flare: yok", _labelStyle);

            if (drone.IsResupplying)
            {
                // Sitting on station: hold position until the bar fills to be refuelled and rearmed.
                Color prevSupply = GUI.color;
                GUI.color = Color.cyan;
                GUILayout.Label($"[İKMAL %{drone.ResupplyProgress * 100f:0}]", _labelStyle);
                GUI.color = prevSupply;
            }

            // Active special abilities (E afterburner / X evasive maneuver).
            if (_pilot.AfterburnerActive || _pilot.EvadeActive)
            {
                Color prevAbility = GUI.color;
                GUI.color = Color.yellow;
                string abilities = _pilot.AfterburnerActive ? "ART YAKICI" : string.Empty;
                if (_pilot.EvadeActive)
                    abilities = abilities.Length > 0 ? abilities + "  KAÇIŞ" : "KAÇIŞ";
                GUILayout.Label(abilities, _labelStyle);
                GUI.color = prevAbility;
            }

            GUILayout.EndArea();

            // Simple centred crosshair.
            var crossRect = new Rect(Screen.width * 0.5f - 15f, Screen.height * 0.5f - 15f, 30f, 30f);
            Color prev = GUI.color;
            GUI.color = Color.green;
            GUI.Label(crossRect, "+", _centerHintStyle);
            GUI.color = prev;
        }

        /// <summary>
        /// Big red centred missile warning. Follows the PILOTED drone when the player is flying, and
        /// otherwise warns about any friendly drone that has a munition homing on it.
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

            string text = float.IsPositiveInfinity(tti) ? "⚠ FÜZE!" : $"⚠ FÜZE! {tti:0.0}s";
            Color prev = GUI.color;
            // Pulse the warning so it cannot be missed. Unscaled time keeps it alive while paused.
            float pulse = 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 6f));
            GUI.color = new Color(1f, 0.25f, 0.2f, pulse);
            var rect = new Rect(0f, Screen.height * 0.2f, Screen.width, 40f);
            GUI.Label(rect, text, _centerHintStyle);
            GUI.color = prev;
        }

        private static string StatusText(MissionStatus s)
        {
            switch (s)
            {
                case MissionStatus.Won: return "Won";
                case MissionStatus.Lost: return "Lost";
                default: return "InProgress";
            }
        }

        /// <summary>Maps the scenario's win/lose outcome onto the mission-status enum used by the banner/stars.</summary>
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
