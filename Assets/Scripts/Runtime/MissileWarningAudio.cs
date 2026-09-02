using UnityEngine;

namespace Sim.Runtime
{
    /// <summary>
    /// The cockpit missile-warning tone: a repeating beep while a missile is tracking the aircraft the
    /// player is flying, which speeds up and hardens the moment the break window opens.
    ///
    /// <para>
    /// WHY IT MATTERS. The evasive-manoeuvre ability (<c>X</c>) only works if it is used INSIDE the
    /// break window (<see cref="Sim.Core.EvasiveManeuver.InBreakWindow"/>), and until now the only cue
    /// was a HUD row the player is not necessarily looking at. This is the audible half of the same
    /// state — it is driven by exactly the two flags the HUD's <c>FÜZE!</c> band and
    /// <c>KAÇIŞ MANEVRASI</c> row already read (<see cref="PlayerDroneController.MissileIncoming"/> and
    /// <see cref="PlayerDroneController.BreakWindowOpen"/>), so the ear and the eye can never disagree.
    /// </para>
    ///
    /// <para>
    /// 2D on purpose: this is the aircraft's own warning receiver, not a thing in the world, so it must
    /// not fade with camera distance. Beeps run on SCALED time — a paused game does not beep, and a
    /// time-compressed one beeps proportionally faster.
    /// </para>
    ///
    /// Purely cosmetic — it only reads state that already exists.
    /// </summary>
    public class MissileWarningAudio : MonoBehaviour
    {
        /// <summary>Seconds between beeps while a missile is inbound but the break window is not open.</summary>
        public const float WarnInterval = 0.6f;

        /// <summary>Seconds between beeps inside the break window — three times as urgent.</summary>
        public const float BreakInterval = 0.2f;

        private PlayerDroneController _pilot;
        private float _timer;
        private bool _lastBreak;

        private void Start()
        {
            _pilot = FindAnyObjectByType<PlayerDroneController>();
        }

        private void Update()
        {
            if (_pilot == null)
            {
                // The pilot component lives on the manager object built by SimulationBootstrap; after a
                // rebuild this may be a different instance, so re-resolve rather than going dead.
                _pilot = FindAnyObjectByType<PlayerDroneController>();
                if (_pilot == null) return;
            }

            // Only the aircraft the player is actually flying gets a warning receiver.
            if (!_pilot.IsActive || !_pilot.MissileIncoming)
            {
                _timer = 0f;
                _lastBreak = false;
                return;
            }

            bool breaking = _pilot.BreakWindowOpen;
            if (breaking != _lastBreak)
            {
                // Entering (or leaving) the break window changes the tone IMMEDIATELY, so the switch
                // itself is the cue to press X.
                _lastBreak = breaking;
                _timer = 0f;
            }

            _timer -= Time.deltaTime;
            if (_timer > 0f) return;

            _timer = breaking ? BreakInterval : WarnInterval;
            AudioClip clip = breaking ? AudioLibrary.BreakWarning : AudioLibrary.MissileWarning;
            AudioDirector.Play2D(clip, breaking ? 0.8f : 0.55f);
        }
    }
}
