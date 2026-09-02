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
    /// BUDGET: exactly one source, created once in <see cref="Start"/> and reused for the object's
    /// whole life. If the clip cannot be built the component disables itself and the aircraft is
    /// simply silent.
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

        private AudioSource _source;
        private IhaController _owner;
        private float _referenceSpeed = 30f;

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
