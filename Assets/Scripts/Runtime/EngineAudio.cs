using UnityEngine;

namespace Sim.Runtime
{
    /// <summary>
    /// The aircraft's engine: one looping <see cref="AudioSource"/> whose pitch and volume follow the
    /// airframe's speed, so the throttle is audible.
    ///
    /// <para>
    /// ARCHETYPE CHARACTER comes free from the model: the recon İHA and the SİHA carry a
    /// <c>"Propeller"</c> part and the fighter jet does not (see <c>VehicleModelBuilder</c>, the same
    /// contract <see cref="PropellerSpinner"/> uses), so the presence of that transform picks between
    /// <see cref="AudioLibrary.EngineProp"/>'s chopping drone and
    /// <see cref="AudioLibrary.EngineJet"/>'s turbine whine. No new data, no new switch.
    /// </para>
    ///
    /// <para>
    /// 2D WHEN FLOWN, 3D WHEN WATCHED: while the player is piloting this aircraft the loop is
    /// un-spatialised — you are inside it, and it must not fade as the camera swings around. Every
    /// other aircraft (and this one under AI control) is heard positionally with distance rolloff.
    /// </para>
    ///
    /// <para>
    /// AFTERBURNER is a SECOND, quieter loop mixed UNDER the engine — not the engine turned up.
    /// <see cref="AudioLibrary.EngineAfterburner"/> is a low, thunder-like rumble that fades in over
    /// a couple of tenths of a second, so lighting the burner reads as an event. It keys off exactly
    /// the state the camera's FOV kick uses (<see cref="PlayerDroneController.AfterburnerActive"/> on
    /// the aircraft that pilot is actually flying, see <c>CameraRig.UpdateFov</c>), so the cue and
    /// the sound can never drift apart.
    /// </para>
    ///
    /// <para>
    /// BUDGET: exactly one source, created once in <see cref="Start"/> and reused for the object's
    /// whole life. If the clip cannot be built the component disables itself and the aircraft is
    /// simply silent. The burner source is created LAZILY, the first time this particular aircraft
    /// lights its burner — only the player's aircraft ever can, so the AI fleet never pays for a
    /// second voice.
    /// </para>
    ///
    /// Purely cosmetic — nothing here touches gameplay state.
    /// </summary>
    // Deliberately NOT [RequireComponent(typeof(IhaController))]: the player's aircraft carries the
    // DERIVED SihaController, and the component is optional here anyway — with no controller the loop
    // simply idles at its lowest setting instead of a second controller being bolted on.
    public class EngineAudio : MonoBehaviour
    {
        // Volume at a standstill and at the airframe's top speed.
        [SerializeField] private float idleVolume = 0.10f;
        [SerializeField] private float maxVolume = 0.50f;

        // Playback rate at a standstill and at top speed. Kept modest: the loops are already pitched
        // for the airframe, and a wide sweep reads as a siren rather than an engine.
        [SerializeField] private float minPitch = 0.70f;
        [SerializeField] private float maxPitch = 1.40f;

        // How quickly the mix chases the target, in units per second (unscaled).
        [SerializeField] private float responseRate = 2.5f;

        [SerializeField] private float minDistance = 6f;
        [SerializeField] private float maxDistance = 130f;

        // Afterburner layer: peak volume of the rumble, and how fast it fades in and out. The fade is
        // quicker than the engine's own so the burner "lights" rather than swells.
        [SerializeField] private float burnerVolume = 0.55f;
        [SerializeField] private float burnerResponseRate = 4f;

        private AudioSource _source;
        private IhaController _owner;
        private float _referenceSpeed = 30f;

        // The afterburner layer and its pilot lookup. Both stay null on every aircraft that never
        // lights a burner, which is every aircraft except the one the player is flying.
        private AudioSource _burner;
        private bool _burnerUnavailable;
        private PlayerDroneController _pilot;
        private float _pilotSearchTimer;

        /// <summary>Seconds between attempts to find the pilot component (it appears at runtime).</summary>
        private const float PilotSearchInterval = 0.5f;

        private void Start()
        {
            _owner = GetComponent<IhaController>();

            bool propeller = FindChild("Propeller") != null;
            AudioClip clip = propeller ? AudioLibrary.EngineProp : AudioLibrary.EngineJet;
            if (clip == null)
            {
                // No audio available: stay silent rather than ticking pointlessly every frame.
                enabled = false;
                return;
            }

            if (_owner != null && _owner.PilotMaxSpeed > 1e-3f) _referenceSpeed = _owner.PilotMaxSpeed;

            _source = gameObject.AddComponent<AudioSource>();
            _source.clip = clip;
            _source.loop = true;
            _source.playOnAwake = false;
            _source.spatialBlend = 1f;
            _source.rolloffMode = AudioRolloffMode.Logarithmic;
            _source.minDistance = minDistance;
            _source.maxDistance = maxDistance;
            _source.dopplerLevel = 0f;
            _source.volume = 0f;
            _source.pitch = minPitch;
            _source.Play();
        }

        private void Update()
        {
            if (_source == null) return;

            float speed = _owner != null ? _owner.Speed : 0f;
            float t = _referenceSpeed > 1e-3f ? Mathf.Clamp01(speed / _referenceSpeed) : 0f;

            // A paused game and a dry tank are both silent: the engine is not running in either case.
            bool running = Time.timeScale > 0f && !(_owner != null && _owner.IsOutOfFuel);

            float targetVolume = running ? Mathf.Lerp(idleVolume, maxVolume, t) : 0f;
            float targetPitch = Mathf.Lerp(minPitch, maxPitch, t);

            // Unscaled: the fade-out has to keep running while the game is paused.
            float step = responseRate * Time.unscaledDeltaTime;
            _source.volume = Mathf.MoveTowards(_source.volume, targetVolume, step);
            _source.pitch = Mathf.MoveTowards(_source.pitch, targetPitch, step);

            // Inside the aircraft the player is flying, the engine is not "somewhere over there".
            bool piloted = _owner != null && _owner.ManualControl;
            _source.spatialBlend = piloted ? 0f : 1f;

            UpdateAfterburner(running, t);
        }

        /// <summary>
        /// Fades the afterburner rumble in and out under the engine loop. The layer is created on the
        /// first light and then reused; while the burner is out the source is stopped, so an idle
        /// aircraft costs no voice.
        /// </summary>
        private void UpdateAfterburner(bool running, float speed01)
        {
            bool lit = running && BurnerLit();

            if (lit && _burner == null && !_burnerUnavailable)
            {
                AudioClip clip = AudioLibrary.EngineAfterburner;
                if (clip == null)
                {
                    // No burner clip: the engine loop alone still plays, just without the layer.
                    _burnerUnavailable = true;
                }
                else
                {
                    _burner = gameObject.AddComponent<AudioSource>();
                    _burner.clip = clip;
                    _burner.loop = true;
                    _burner.playOnAwake = false;
                    _burner.rolloffMode = AudioRolloffMode.Logarithmic;
                    _burner.minDistance = minDistance;
                    _burner.maxDistance = maxDistance;
                    _burner.dopplerLevel = 0f;
                    _burner.volume = 0f;
                }
            }

            if (_burner == null) return;

            float target = lit ? burnerVolume : 0f;
            float step = burnerResponseRate * Time.unscaledDeltaTime;
            _burner.volume = Mathf.MoveTowards(_burner.volume, target, step);

            // The rumble sits with the engine: same spatialisation, and a touch of pitch with speed so
            // the two layers stay welded together instead of sounding like a separate machine.
            _burner.spatialBlend = _source.spatialBlend;
            _burner.pitch = Mathf.Lerp(0.92f, 1.08f, Mathf.Clamp01(speed01));

            if (_burner.volume > 0.001f)
            {
                if (!_burner.isPlaying) _burner.Play();
            }
            else if (_burner.isPlaying)
            {
                _burner.Stop();
            }
        }

        /// <summary>
        /// True while THIS aircraft's afterburner is lit. Deliberately the same source of truth the
        /// camera's FOV kick reads — the pilot component's <c>AfterburnerActive</c>, on the aircraft
        /// it is currently flying — so the visual and the audible cue cannot disagree.
        /// </summary>
        private bool BurnerLit()
        {
            if (_owner == null || !_owner.ManualControl) return false;

            if (_pilot == null)
            {
                _pilotSearchTimer -= Time.unscaledDeltaTime;
                if (_pilotSearchTimer > 0f) return false;
                _pilotSearchTimer = PilotSearchInterval;
                _pilot = FindAnyObjectByType<PlayerDroneController>();
            }

            if (_pilot == null) return false;
            return _pilot.IsActive && _pilot.Controlled == _owner && _pilot.AfterburnerActive;
        }

        /// <summary>Finds the first descendant transform with the given name, or null.</summary>
        private Transform FindChild(string childName)
        {
            Transform[] all = GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                Transform t = all[i];
                if (t == null) continue;
                if (t.name == childName) return t;
            }
            return null;
        }
    }
}
