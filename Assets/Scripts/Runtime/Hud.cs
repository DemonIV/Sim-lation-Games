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

        // Cached GUI styles (built lazily on first OnGUI so we're on the GUI thread).
        private GUIStyle _labelStyle;
        private GUIStyle _titleStyle;
        private GUIStyle _bannerStyle;
        private GUIStyle _centerHintStyle;

        // Occasional refresh of the sensor list so newly spawned drones show up.
        private float _refreshTimer;
        private const float RefreshInterval = 2f;

        private void Start()
        {
            _director = FindAnyObjectByType<SimulationDirector>();
            _scenario = FindAnyObjectByType<ScenarioController>();
            _controls = FindAnyObjectByType<GameControls>();
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
            }
        }

        private void RefreshSensors()
        {
            _sensors = FindObjectsByType<RadarSensor>(FindObjectsSortMode.None);
        }

        private void EnsureStyles()
        {
            if (_labelStyle == null)
                _labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 14 };
            if (_titleStyle == null)
                _titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold };
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

        private void OnGUI()
        {
            EnsureStyles();

            GUILayout.BeginArea(new Rect(10, 10, 340, 540), GUI.skin.box);
            GUILayout.Label("İHA/SİHA Taktik Simülasyonu", _titleStyle);

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
                    GUILayout.Label(
                        $"  {c.name}: {c.State}  yakıt={c.FuelFraction * 100f:0}%  mühimmat={c.AmmoFraction * 100f:0}%",
                        _labelStyle);
                    shown++;
                }
            }
            if (shown == 0)
                GUILayout.Label("  (drone yok)", _labelStyle);

            GUILayout.Space(6);
            GUILayout.Label("WASD+Sağ tık: kamera, Tab: drone takip, F: serbest", _labelStyle);

            float scale = _controls != null ? _controls.CurrentTimeScale : Time.timeScale;
            bool paused = _controls != null ? _controls.IsPaused : Time.timeScale == 0f;
            string pauseText = paused ? "DURAKLADI" : "duraklat";
            GUILayout.Label($"P: {pauseText}  +/-: hız (x{scale:0.00})  R: yeniden", _labelStyle);

            GUILayout.EndArea();

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
