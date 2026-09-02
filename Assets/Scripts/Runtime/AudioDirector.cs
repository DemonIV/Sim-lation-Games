using UnityEngine;
using Sim.Core;

namespace Sim.Runtime
{
    /// <summary>
    /// Persists the master audio setting through <see cref="PlayerPrefs"/>.
    ///
    /// <para>
    /// DELIBERATELY ITS OWN KEYS, not a field inside <see cref="CampaignSaveData"/>: the campaign save
    /// is a versioned blob whose version bump means "start fresh", and a volume slider must never be
    /// able to cost somebody their campaign. Two scalar prefs are also cheaper to write, which matters
    /// because this one IS written on every key press.
    /// </para>
    ///
    /// Total by design: a missing, corrupt or out-of-range entry falls back to the defaults through
    /// <see cref="SoundSettings.Restore"/> and never throws.
    /// </summary>
    public static class AudioSave
    {
        /// <summary>PlayerPrefs key holding the 0..1 master volume.</summary>
        public const string VolumePrefsKey = "sim.audio.volume";

        /// <summary>PlayerPrefs key holding the mute flag as 0/1.</summary>
        public const string MutedPrefsKey = "sim.audio.muted";

        /// <summary>Fills <paramref name="settings"/> from the stored preference (defaults when absent).</summary>
        public static void Load(SoundSettings settings)
        {
            if (settings == null) return;

            try
            {
                float volume = PlayerPrefs.GetFloat(VolumePrefsKey, SoundSettings.DefaultVolume);
                bool muted = PlayerPrefs.GetInt(MutedPrefsKey, 0) != 0;
                settings.Restore(volume, muted);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[AudioSave] ses ayarı okunamadı: {e.Message}");
            }
        }

        /// <summary>Writes the current setting. Called on a key press, never per frame.</summary>
        public static void Save(SoundSettings settings)
        {
            if (settings == null) return;

            try
            {
                PlayerPrefs.SetFloat(VolumePrefsKey, settings.MasterVolume);
                PlayerPrefs.SetInt(MutedPrefsKey, settings.Muted ? 1 : 0);
                PlayerPrefs.Save();
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[AudioSave] ses ayarı yazılamadı: {e.Message}");
            }
        }
    }

    /// <summary>
    /// THE audio front desk: owns the master volume key, guarantees there is an
    /// <see cref="AudioListener"/> to hear with, and hands out a POOL of reusable
    /// <see cref="AudioSource"/>s so a firefight never allocates one per bang.
    ///
    /// <para>
    /// BUDGET, mirroring <see cref="VfxLibrary"/>: <see cref="PoolSize"/> spatial sources are created
    /// once in <c>Awake</c> and recycled round-robin; a request that arrives with every source busy
    /// steals the oldest rather than growing the pool, so the cost of audio is bounded no matter what
    /// happens on screen. One extra 2D source serves UI and cockpit sounds through
    /// <c>PlayOneShot</c>, which mixes overlapping clicks on a single source.
    /// </para>
    ///
    /// <para>
    /// SILENCE IS FREE: while the player has the game muted (or turned all the way down) the play
    /// calls return immediately, so nothing is generated, positioned or mixed.
    /// </para>
    ///
    /// <para>
    /// The setting itself is STATIC and outlives <see cref="SimulationBootstrap.Rebuild"/> — the
    /// director object lives under the rebuilt <c>Simulation</c> root, but restarting a mission must
    /// not un-mute the game.
    /// </para>
    ///
    /// Thin glue: every rule about what the volume may be lives in <see cref="SoundSettings"/>.
    /// </summary>
    public class AudioDirector : MonoBehaviour
    {
        /// <summary>How many spatial (3D) sources are pooled.</summary>
        public const int PoolSize = 12;

        /// <summary>
        /// Master volume key. <c>N</c> is one of the few keys still free (every audio-mnemonic letter
        /// — S, K, M, V, C — is already bound; see the DEVLOG key audit), and one press walks the
        /// whole ladder: %100 → %70 → %40 → KAPALI → %100.
        /// </summary>
        public const KeyCode VolumeKey = KeyCode.N;

        /// <summary>Distance at which a spatial sound starts falling off, in metres.</summary>
        public const float DefaultMinDistance = 8f;

        /// <summary>Distance at which a spatial sound has faded out, in metres (the arena is ~113 m across).</summary>
        public const float DefaultMaxDistance = 160f;

        private static readonly SoundSettings _settings = new SoundSettings();
        private static bool _settingsLoaded;

        /// <summary>The live master volume / mute state, shared by every scene rebuild.</summary>
        public static SoundSettings Settings
        {
            get { return _settings; }
        }

        /// <summary>The active director, or null when none exists (every play call is then a no-op).</summary>
        public static AudioDirector Instance { get; private set; }

        private AudioSource _ui;
        private AudioSource[] _pool;
        private int _next;

        private void Awake()
        {
            Instance = this;

            if (!_settingsLoaded)
            {
                _settingsLoaded = true;
                AudioSave.Load(_settings);
            }

            EnsureListener();
            BuildSources();
            ApplySettings();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (!Input.GetKeyDown(VolumeKey)) return;

            _settings.CycleVolume();
            ApplySettings();
            AudioSave.Save(_settings);

            // Audible confirmation of the new level — silent by definition when the new state is mute.
            Play2D(AudioLibrary.UiClick, 1f);
        }

        /// <summary>Pushes the effective volume into the global mixer.</summary>
        private void ApplySettings()
        {
            AudioListener.volume = Mathf.Clamp01(_settings.EffectiveVolume);
        }

        /// <summary>
        /// Guarantees something is listening. The generated scene builds its camera by hand
        /// (<c>SimulationBootstrap.EnsureCameraAndLight</c>), and a camera created that way has no
        /// <see cref="AudioListener"/> — without this the whole game would be silent for reasons no
        /// amount of volume would fix.
        /// </summary>
        private void EnsureListener()
        {
            AudioListener existing = FindAnyObjectByType<AudioListener>();
            if (existing != null) return;

            Camera cam = Camera.main;
            if (cam == null) return;
            cam.gameObject.AddComponent<AudioListener>();
        }

        /// <summary>Creates the 2D source and the spatial pool once, as children of this object.</summary>
        private void BuildSources()
        {
            _ui = CreateSource("UiAudioSource", 0f);

            _pool = new AudioSource[PoolSize];
            for (int i = 0; i < PoolSize; i++) _pool[i] = CreateSource("SfxAudioSource_" + i, 1f);
        }

        private AudioSource CreateSource(string name, float spatialBlend)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);

            var src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.loop = false;
            src.spatialBlend = spatialBlend;
            src.rolloffMode = AudioRolloffMode.Logarithmic;
            src.minDistance = DefaultMinDistance;
            src.maxDistance = DefaultMaxDistance;
            src.dopplerLevel = 0f;      // the sim's speeds are abstract; Doppler only smears the mix
            return src;
        }

        // ------------------------------------------------------------------ playback API

        /// <summary>
        /// Plays a one-shot AT A POSITION in the world, so distance and direction are audible. A null
        /// clip, a missing director or a muted game are all quiet no-ops.
        /// </summary>
        public static void PlayAt(Vector3 position, AudioClip clip, float volume = 1f, float pitch = 1f)
        {
            PlayAt(position, clip, volume, pitch, DefaultMinDistance, DefaultMaxDistance);
        }

        /// <summary><see cref="PlayAt(Vector3,AudioClip,float,float)"/> with an explicit rolloff.</summary>
        public static void PlayAt(Vector3 position, AudioClip clip, float volume, float pitch,
                                  float minDistance, float maxDistance)
        {
            AudioDirector director = Instance;
            if (director == null || clip == null) return;
            if (_settings.IsSilent) return;

            AudioSource src = director.TakeSource();
            if (src == null) return;

            src.transform.position = position;
            src.spatialBlend = 1f;
            src.minDistance = Mathf.Max(0.1f, minDistance);
            src.maxDistance = Mathf.Max(src.minDistance + 0.1f, maxDistance);
            src.pitch = Mathf.Clamp(pitch, 0.1f, 3f);
            src.volume = Mathf.Clamp01(volume);
            src.clip = clip;
            src.Play();
        }

        /// <summary>
        /// Plays a one-shot with NO spatialisation — UI feedback and the player's own cockpit sounds,
        /// which should not fade when the camera moves.
        /// </summary>
        public static void Play2D(AudioClip clip, float volume = 1f, float pitch = 1f)
        {
            AudioDirector director = Instance;
            if (director == null || clip == null) return;
            if (_settings.IsSilent) return;
            if (director._ui == null) return;

            director._ui.pitch = Mathf.Clamp(pitch, 0.1f, 3f);
            director._ui.PlayOneShot(clip, Mathf.Clamp01(volume));
        }

        /// <summary>
        /// The next free pooled source, or the oldest one if every source is busy. Never allocates:
        /// stealing keeps the worst case bounded, and the stolen sound is the one that started
        /// longest ago.
        /// </summary>
        private AudioSource TakeSource()
        {
            if (_pool == null || _pool.Length == 0) return null;

            for (int i = 0; i < _pool.Length; i++)
            {
                int index = (_next + i) % _pool.Length;
                AudioSource candidate = _pool[index];
                if (candidate == null) continue;
                if (candidate.isPlaying) continue;

                _next = (index + 1) % _pool.Length;
                return candidate;
            }

            AudioSource stolen = _pool[_next];
            _next = (_next + 1) % _pool.Length;
            if (stolen == null) return null;

            stolen.Stop();
            return stolen;
        }
    }
}
