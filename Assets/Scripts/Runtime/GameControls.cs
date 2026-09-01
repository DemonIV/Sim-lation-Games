using UnityEngine;

namespace Sim.Runtime
{
    /// <summary>
    /// Global keyboard controls for the sim: restart, pause, and time-scale adjustment.
    /// Thin glue only — it manipulates <see cref="Time.timeScale"/> and asks
    /// <see cref="SimulationBootstrap"/> to rebuild the generated world.
    ///
    /// Keys: R = restart · P = pause/resume · +/- (top row or keypad) = faster/slower ·
    /// G = show/hide the fleet status panel.
    /// </summary>
    public class GameControls : MonoBehaviour
    {
        private const float MinScale = 0.25f;
        private const float MaxScale = 4f;

        // Remembers the last non-zero time scale so unpausing restores the chosen speed.
        private float _lastNonZeroScale = 1f;

        /// <summary>Current <see cref="Time.timeScale"/>, exposed for the HUD.</summary>
        public float CurrentTimeScale => Time.timeScale;

        /// <summary>True while the game is paused (time scale frozen at zero).</summary>
        public bool IsPaused => Time.timeScale == 0f;

        /// <summary>
        /// Whether the HUD's side fleet-status panel (the İHA/SİHA blocks) should be drawn. Toggled
        /// with <c>G</c> ("gizle/göster") and VISIBLE by default, so a player who never presses the key
        /// sees exactly what they saw before. Presentation only — no gameplay state is touched.
        /// </summary>
        public bool FleetPanelVisible { get; private set; } = true;

        private void Update()
        {
            // R: restart from a clean slate by rebuilding the generated world in place. Reloading the
            // scene is not an option — the project has no .unity scene asset in Build Settings, so the
            // active scene has no build index to reload (finding B-01).
            if (Input.GetKeyDown(KeyCode.R))
            {
                SimulationBootstrap boot = SimulationBootstrap.Instance;
                if (boot == null) boot = FindAnyObjectByType<SimulationBootstrap>();
                // No bootstrap (hand-authored scene): there is nothing to rebuild, so leave the sim as is.
                if (boot != null) boot.Rebuild();
                return;
            }

            // P: toggle pause, remembering the previous speed.
            if (Input.GetKeyDown(KeyCode.P))
            {
                if (Time.timeScale == 0f)
                {
                    Time.timeScale = _lastNonZeroScale > 0f ? _lastNonZeroScale : 1f;
                }
                else
                {
                    _lastNonZeroScale = Time.timeScale;
                    Time.timeScale = 0f;
                }
                return;
            }

            // G: show/hide the fleet status panel. F would read better ("filo") but it is already the
            // pilot's guided-munition key AND the camera's free-fly key, so G is used instead.
            if (Input.GetKeyDown(KeyCode.G))
            {
                FleetPanelVisible = !FleetPanelVisible;
                return;
            }

            // +/- adjust time scale (only when not paused).
            if (Time.timeScale > 0f)
            {
                if (Input.GetKeyDown(KeyCode.Equals) || Input.GetKeyDown(KeyCode.KeypadPlus))
                {
                    Time.timeScale = Mathf.Clamp(Time.timeScale + 0.25f, MinScale, MaxScale);
                    _lastNonZeroScale = Time.timeScale;
                }
                else if (Input.GetKeyDown(KeyCode.Minus) || Input.GetKeyDown(KeyCode.KeypadMinus))
                {
                    Time.timeScale = Mathf.Clamp(Time.timeScale - 0.25f, MinScale, MaxScale);
                    _lastNonZeroScale = Time.timeScale;
                }
            }
        }
    }
}
