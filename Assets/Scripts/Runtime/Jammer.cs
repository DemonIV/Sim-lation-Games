using UnityEngine;
using Sim.Core;

namespace Sim.Runtime
{
    /// <summary>
    /// Onboard noise jammer. Represents a self-protection ECM emitter that raises the noise floor
    /// of hostile radars, shortening the range at which their skin return burns through (see
    /// <see cref="Sim.Core.ElectronicWarfare.EffectiveRange"/>). A <see cref="RadarSensor"/> and the
    /// hostile detection snapshot (<c>TargetRegistry.GetSnapshot</c>) both read
    /// <see cref="Strength"/> from candidates that carry this component and degrade their detection
    /// range accordingly.
    ///
    /// <para>
    /// THIN GLUE over the pure-logic <see cref="JammerSystem"/>, which owns the duty cycle (burst
    /// length, cooldown, and therefore whether the emitter is radiating at all). Built lazily so a
    /// spawner's <see cref="Configure"/> call can precede <see cref="Start"/>, exactly like
    /// <see cref="CountermeasureDispenser"/>.
    /// </para>
    ///
    /// <para>
    /// BACKWARDS COMPATIBLE: the serialized defaults are a continuous emitter
    /// (<c>burstSeconds</c> ≤ 0), which is precisely what this component did before the duty cycle
    /// existed — a jammer dropped into a hand-authored scene keeps radiating forever and never needs
    /// anyone to tick it. Only the duty-cycled player jammer needs <see cref="Tick"/>, and that one
    /// is always mounted on an <see cref="IhaController"/>, which ticks it alongside the flare
    /// dispenser.
    /// </para>
    /// </summary>
    public class Jammer : MonoBehaviour
    {
        [Header("Electronic Warfare")]
        [SerializeField] private float jammerStrength = 4f;
        [SerializeField] private bool active = true;

        [Header("Duty cycle")]
        // Non-positive burst = continuous (the historical behaviour). See the class remarks.
        [SerializeField] private float burstSeconds;
        [SerializeField] private float cooldownSeconds;

        // Built lazily so a spawner's Configure(...) call can precede Start().
        private JammerSystem _system;

        /// <summary>
        /// Effective jamming strength RIGHT NOW — 0 unless the emitter is actually radiating. Fed to
        /// <see cref="ElectronicWarfare.EffectiveRange"/> by every sensor path, so an idle or cooling
        /// jammer is indistinguishable from carrying none at all.
        /// </summary>
        public float Strength => active ? EnsureSystem().CurrentStrength : 0f;

        /// <summary>The emitter's rated strength, radiating or not (0 = no jammer fitted).</summary>
        public float NominalStrength => EnsureSystem().Strength;

        /// <summary>Current phase of the duty cycle, for the HUD.</summary>
        public JammerState State => EnsureSystem().State;

        /// <summary>True when this is a real emitter rather than a level-0 placeholder.</summary>
        public bool IsFitted => EnsureSystem().IsFitted;

        /// <summary>True when a burst can be triggered right now.</summary>
        public bool CanActivate => active && EnsureSystem().CanActivate;

        /// <summary>True while the emitter is radiating.</summary>
        public bool IsActive => active && EnsureSystem().IsActive;

        /// <summary>Readiness as a 0..1 fraction: 1 = available, climbing back across the cooldown.</summary>
        public float ReadyFraction => EnsureSystem().ReadyFraction;

        /// <summary>Burst progress as a 1 → 0 fraction; 0 when not radiating a timed burst.</summary>
        public float BurstFraction => EnsureSystem().BurstFraction;

        /// <summary>Seconds left in the current phase (0 while ready).</summary>
        public float SecondsRemaining => EnsureSystem().SecondsRemaining;

        /// <summary>
        /// The factor every hostile detection range against this aircraft is multiplied by right now:
        /// 1 when silent, <c>1 / (1 + strength)^0.25</c> while radiating.
        /// </summary>
        public float DetectionRangeFactor =>
            active ? EnsureSystem().DetectionRangeFactor : 1f;

        /// <summary>
        /// Overrides the emitter parameters before <see cref="Start"/>, mirroring the
        /// <c>Configure(...)</c> pattern used by the other runtime components. Called by
        /// <see cref="SimulationBootstrap"/> with the hangar's "Elektronik Harp" level.
        /// </summary>
        public void Configure(float strength, float burst, float cooldown)
        {
            jammerStrength = Mathf.Max(0f, strength);
            burstSeconds = burst;
            cooldownSeconds = Mathf.Max(0f, cooldown);
            // Drop any system already built with the old values; it is rebuilt on next access.
            _system = null;
        }

        /// <summary>
        /// Fires one jamming burst. Returns true only when the emitter actually started radiating —
        /// hammering the key during a burst or its cooldown changes nothing.
        /// </summary>
        public bool TryActivate()
        {
            if (!active) return false;
            return EnsureSystem().TryActivate();
        }

        /// <summary>Advances the duty cycle. Call once per frame from the owning controller.</summary>
        public void Tick(float dt)
        {
            EnsureSystem().Tick(dt);
        }

        /// <summary>Returns the emitter to its start state (rearm at base / respawn).</summary>
        public void Rearm()
        {
            EnsureSystem().Reset();
        }

        private void Start()
        {
            EnsureSystem();
        }

        /// <summary>Builds the pure-logic system on first use, so Configure can precede Start.</summary>
        private JammerSystem EnsureSystem()
        {
            if (_system == null)
                _system = new JammerSystem(jammerStrength, burstSeconds, cooldownSeconds);
            return _system;
        }
    }
}
