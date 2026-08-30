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
        private RadarSensor[] _sensors;

        // Cached GUI styles (built lazily on first OnGUI so we're on the GUI thread).
        private GUIStyle _labelStyle;
        private GUIStyle _titleStyle;
        private GUIStyle _bannerStyle;

        // Occasional refresh of the sensor list so newly spawned drones show up.
        private float _refreshTimer;
        private const float RefreshInterval = 2f;

        private void Start()
        {
            _director = FindAnyObjectByType<SimulationDirector>();
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
        }

        private void OnGUI()
        {
            EnsureStyles();

            GUILayout.BeginArea(new Rect(10, 10, 340, 420), GUI.skin.box);
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
            GUILayout.Label("WASD+Sağ tık: kamera, Tab: drone takip, F: serbest", _labelStyle);

            GUILayout.EndArea();

            // End-of-mission banner.
            if (m.Status == MissionStatus.Won || m.Status == MissionStatus.Lost)
            {
                string text = m.Status == MissionStatus.Won ? "GÖREV BAŞARILI" : "GÖREV BAŞARISIZ";
                Color prev = GUI.color;
                GUI.color = m.Status == MissionStatus.Won ? Color.green : Color.red;
                var rect = new Rect(0, Screen.height * 0.5f - 40f, Screen.width, 80f);
                GUI.Label(rect, text, _bannerStyle);
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
    }
}
