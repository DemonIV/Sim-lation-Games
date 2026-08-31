using UnityEngine;
using UnityEngine.SceneManagement;
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
    /// reloads the active scene through the same path <see cref="GameControls"/> uses for <c>R</c>.
    /// The chosen mission survives that reload because <c>SelectedKind</c> is static.
    /// </para>
    ///
    /// Because the menu runs at <c>timeScale == 0</c> it never reads <see cref="Time.deltaTime"/>.
    /// </summary>
    public class ScenarioMenu : MonoBehaviour
    {
        // Panel geometry.
        private const float PanelWidth = 560f;
        private const float PanelHeight = 420f;

        private ScenarioController _controller;

        // Cached GUI styles, built lazily on the GUI thread (first OnGUI).
        private GUIStyle _headingStyle;
        private GUIStyle _subtitleStyle;
        private GUIStyle _buttonStyle;
        private GUIStyle _descStyle;
        private GUIStyle _legendStyle;

        /// <summary>True while the menu is showing and the sim is held paused.</summary>
        public bool IsOpen { get; private set; }

        /// <summary>
        /// True once the player has picked a mission at least once in this scene load. Used to tell a
        /// fresh launch (just begin the mission) apart from a mid-mission reopen (reload the scene).
        /// </summary>
        private bool _missionStarted;

        /// <summary>
        /// Set just before the mid-mission scene reload and consumed by <see cref="Start"/> in the
        /// freshly loaded scene, so the player does not have to pick the same mission twice. STATIC
        /// for the same reason as <see cref="ScenarioController.SelectedKind"/>: it must survive the
        /// reload that destroys this component.
        /// </summary>
        private static bool _autoBegin;

        private void Start()
        {
            _controller = FindAnyObjectByType<ScenarioController>();

            if (_autoBegin)
            {
                // Coming back from a mission change: the choice was already made, so skip the briefing
                // and launch straight into the freshly rebuilt field.
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

        private void EnsureStyles()
        {
            if (_headingStyle == null)
                _headingStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 24,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };
            if (_subtitleStyle == null)
                _subtitleStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 16,
                    alignment = TextAnchor.MiddleCenter
                };
            if (_buttonStyle == null)
                _buttonStyle = new GUIStyle(GUI.skin.button)
                {
                    fontSize = 16,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };
            if (_descStyle == null)
                _descStyle = new GUIStyle(GUI.skin.label) { fontSize = 12, wordWrap = true };
            if (_legendStyle == null)
                _legendStyle = new GUIStyle(GUI.skin.label) { fontSize = 12 };
        }

        private void OnGUI()
        {
            if (!IsOpen) return;

            EnsureStyles();

            var panel = new Rect(
                (Screen.width - PanelWidth) * 0.5f,
                (Screen.height - PanelHeight) * 0.5f,
                PanelWidth, PanelHeight);

            GUILayout.BeginArea(panel, GUI.skin.box);

            GUILayout.Space(8);
            GUILayout.Label("İHA / SİHA TAKTİK SİMÜLASYONU", _headingStyle);
            GUILayout.Label("Görev seç", _subtitleStyle);
            GUILayout.Space(8);

            ScenarioKind[] kinds = ScenarioLibrary.All;
            if (kinds != null)
            {
                for (int i = 0; i < kinds.Length; i++)
                {
                    ScenarioKind kind = kinds[i];
                    string label = $"{ScenarioLibrary.Title(kind)}   —   Dalga: {ScenarioLibrary.TotalWaves(kind)}";
                    if (GUILayout.Button(label, _buttonStyle, GUILayout.Height(30f)))
                        Choose(kind);

                    GUILayout.Label($"    {ScenarioLibrary.Description(kind)}", _descStyle);
                    GUILayout.Space(4);
                }
            }

            GUILayout.FlexibleSpace();
            GUILayout.Label("Kamera: WASD + sağ tık bak · Tab drone takip · F serbest", _legendStyle);
            GUILayout.Label("Pilot: C aç/kapat · W/S gaz · A/D dönüş · ↑/↓ yunuslama", _legendStyle);
            GUILayout.Label("Silah: Space top · F füze · Q flare · E art yakıcı · X kaçış", _legendStyle);
            GUILayout.Label("Genel: P duraklat · +/- hız · R yeniden · M görev menüsü", _legendStyle);
            GUILayout.Space(6);

            GUILayout.EndArea();
        }

        /// <summary>
        /// Applies the player's choice. On the first pick of a scene load the mission simply begins;
        /// a mid-mission pick reloads the scene first so the new mission starts on a clean field.
        /// </summary>
        private void Choose(ScenarioKind kind)
        {
            ScenarioController.SelectedKind = kind;

            if (_missionStarted)
            {
                // Mid-mission change: mirror GameControls' R restart exactly. SelectedKind and
                // _autoBegin are static, so the freshly loaded scene picks up the mission chosen here
                // and starts it without showing this briefing a second time.
                IsOpen = false;
                _autoBegin = true;
                Time.timeScale = 1f;
                TargetRegistry.Clear();
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
                return;
            }

            if (_controller == null) _controller = FindAnyObjectByType<ScenarioController>();
            if (_controller != null) _controller.BeginMission();

            _missionStarted = true;
            Time.timeScale = 1f;
            IsOpen = false;
        }
    }
}
