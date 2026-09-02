using UnityEngine;

namespace Sim.Core
{
    /// <summary>
    /// The player's master audio setting: a volume level, a mute flag, and the rules for stepping
    /// between them. Pure logic — the Runtime layer pushes <see cref="EffectiveVolume"/> into
    /// <c>AudioListener.volume</c> and persists <see cref="MasterVolume"/>/<see cref="Muted"/>, but
    /// every rule about what those values may be lives here and is EditMode-tested.
    ///
    /// <para>
    /// MUTE IS NOT VOLUME 0. Muting remembers the level underneath it, so un-muting returns to
    /// exactly what the player had — the classic mistake of implementing mute as "set volume to zero"
    /// loses that. Stepping the volume while muted un-mutes, because a player pressing "louder" on a
    /// silent game means "I want to hear it".
    /// </para>
    ///
    /// This is a GAME model with abstract, gamified parameters; no Unity scene dependency.
    /// </summary>
    public class SoundSettings
    {
        /// <summary>Volume a fresh install starts at: loud enough to be heard, quiet enough not to startle.</summary>
        public const float DefaultVolume = 0.7f;

        /// <summary>Size of one <see cref="StepVolume"/> press.</summary>
        public const float VolumeStep = 0.1f;

        /// <summary>Lowest level the step key can reach; below it the player should mute instead.</summary>
        public const float MinVolume = 0f;

        /// <summary>Highest level; the synthesised clips are normalized so this never clips the mix.</summary>
        public const float MaxVolume = 1f;

        private float _volume = DefaultVolume;

        /// <summary>
        /// The stored level, 0..1, INDEPENDENT of mute. This is the number that is persisted and the
        /// number the HUD shows as a percentage.
        /// </summary>
        public float MasterVolume
        {
            get { return _volume; }
        }

        /// <summary>True while the player has silenced the game.</summary>
        public bool Muted { get; private set; }

        /// <summary>
        /// What the mixer should actually be set to: the stored level, or exactly 0 while muted. THE
        /// single number the Runtime layer reads.
        /// </summary>
        public float EffectiveVolume
        {
            get { return Muted ? 0f : _volume; }
        }

        /// <summary>The stored level as a 0..100 integer, for display.</summary>
        public int VolumePercent
        {
            get { return Mathf.RoundToInt(_volume * 100f); }
        }

        /// <summary>True when nothing will be heard — muted, or turned all the way down.</summary>
        public bool IsSilent
        {
            get { return EffectiveVolume <= 0f; }
        }

        /// <summary>
        /// Sets the stored level, clamped into [<see cref="MinVolume"/>, <see cref="MaxVolume"/>].
        /// NaN and infinities are rejected (the level is left as it was) rather than poisoning the
        /// mixer. Does not change the mute flag.
        /// </summary>
        public void SetVolume(float volume)
        {
            if (float.IsNaN(volume) || float.IsInfinity(volume)) return;
            _volume = Mathf.Clamp(volume, MinVolume, MaxVolume);
        }

        /// <summary>
        /// Nudges the volume by <paramref name="steps"/> × <see cref="VolumeStep"/> and returns the new
        /// stored level. A step in either direction also UN-MUTES: the step keys are how a player who
        /// muted by accident gets the sound back without knowing which key did it.
        /// </summary>
        public float StepVolume(int steps)
        {
            if (steps != 0) Muted = false;
            SetVolume(_volume + steps * VolumeStep);
            return _volume;
        }

        /// <summary>
        /// Flips mute and returns the new state. Un-muting onto a stored level of zero restores
        /// <see cref="DefaultVolume"/>, so the key can never appear to do nothing.
        /// </summary>
        public bool ToggleMute()
        {
            Muted = !Muted;
            if (!Muted && _volume <= 0f) _volume = DefaultVolume;
            return Muted;
        }

        /// <summary>Explicitly sets the mute flag (used when restoring a saved setting).</summary>
        public void SetMuted(bool muted)
        {
            Muted = muted;
        }

        /// <summary>
        /// Restores a persisted setting. Like every other <c>Restore</c> in the project this is TOTAL:
        /// a corrupt, out-of-range or NaN stored level falls back to <see cref="DefaultVolume"/>
        /// instead of throwing or producing an impossible state.
        /// </summary>
        public void Restore(float volume, bool muted)
        {
            if (float.IsNaN(volume) || float.IsInfinity(volume)) _volume = DefaultVolume;
            else _volume = Mathf.Clamp(volume, MinVolume, MaxVolume);
            Muted = muted;
        }

        /// <summary>Turkish HUD label: "SES %70" or "SES KAPALI".</summary>
        public string Label()
        {
            return Muted ? "SES KAPALI" : "SES %" + VolumePercent;
        }
    }
}
