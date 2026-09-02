using UnityEngine;

namespace Sim.Core
{
    /// <summary>What an onboard jammer is doing right now.</summary>
    public enum JammerState
    {
        /// <summary>Armed and available: a burst can be triggered.</summary>
        Ready,

        /// <summary>Emitting — hostile detection ranges are being shortened.</summary>
        Active,

        /// <summary>Spent; recharging and unavailable.</summary>
        Cooling
    }

    /// <summary>
    /// The DUTY CYCLE of an onboard noise jammer: how long one burst lasts, how long it then takes to
    /// recharge, and how much jamming strength it is putting out at this instant. Pure logic.
    ///
    /// <para>
    /// The physics of jamming already live in <see cref="ElectronicWarfare.EffectiveRange"/> (every
    /// detection range divided by <c>(1 + strength)^0.25</c>) and are composed with the signature law
    /// in <see cref="SignatureDetection"/>. Nothing of that is repeated here — this class only owns
    /// the question those two cannot answer: <em>is it on?</em>
    /// </para>
    ///
    /// <para>
    /// It is a TIMED BURST on a cooldown, deliberately shaped like the pilot's existing evasive
    /// break turn rather than like a light switch: an always-on jammer is a permanent stat, whereas a
    /// burst is a decision about WHEN. A burst cannot be cancelled early — once spent it runs its
    /// full duration and then its full cooldown — so the ability has exactly one input and no way to
    /// game the timer.
    /// </para>
    ///
    /// <para>
    /// CONTINUOUS MODE: a non-positive <see cref="BurstSeconds"/> means "no duty cycle at all" — the
    /// jammer simply emits forever. That is the behaviour the <c>Jammer</c> component had before the
    /// ability existed, kept so a jammer dropped into a hand-authored scene still works the way it
    /// always did.
    /// </para>
    ///
    /// This is a GAME / EDUCATIONAL model with abstract, gamified parameters.
    /// </summary>
    public class JammerSystem
    {
        /// <summary>
        /// Jamming strength this emitter puts out while active (0 = no jammer at all). Fed to
        /// <see cref="ElectronicWarfare.EffectiveRange"/>, which divides every detection range by
        /// <c>(1 + strength)^0.25</c>.
        /// </summary>
        public float Strength { get; }

        /// <summary>How many seconds one burst emits for. Non-positive = continuous (see class remarks).</summary>
        public float BurstSeconds { get; }

        /// <summary>How many seconds the emitter is unavailable after a burst ends.</summary>
        public float CooldownSeconds { get; }

        /// <summary>True when this emitter has no duty cycle and simply runs forever.</summary>
        public bool IsContinuous => BurstSeconds <= 0f;

        /// <summary>True when there is no emitter at all (strength 0) — a level-0 upgrade track.</summary>
        public bool IsFitted => Strength > 0f;

        /// <summary>Current phase of the duty cycle.</summary>
        public JammerState State { get; private set; }

        /// <summary>Seconds left in the CURRENT phase (0 while <see cref="JammerState.Ready"/>).</summary>
        public float SecondsRemaining { get; private set; }

        public JammerSystem(float strength, float burstSeconds, float cooldownSeconds)
        {
            Strength = Mathf.Max(0f, strength);
            BurstSeconds = burstSeconds;
            CooldownSeconds = Mathf.Max(0f, cooldownSeconds);

            // A continuous emitter is "on" from the start; a duty-cycled one waits to be triggered.
            State = IsContinuous && IsFitted ? JammerState.Active : JammerState.Ready;
        }

        /// <summary>
        /// Jamming strength being radiated RIGHT NOW: <see cref="Strength"/> while active, otherwise
        /// zero. This is the number the detection snapshot carries to hostile sensors, so an idle or
        /// cooling jammer is indistinguishable from no jammer at all.
        /// </summary>
        public float CurrentStrength => State == JammerState.Active ? Strength : 0f;

        /// <summary>True while the emitter is radiating.</summary>
        public bool IsActive => State == JammerState.Active;

        /// <summary>
        /// True when a burst can be triggered: an emitter is fitted, it has a duty cycle, and it is
        /// sitting in <see cref="JammerState.Ready"/>. A continuous emitter is never "activatable" —
        /// it is already on.
        /// </summary>
        public bool CanActivate => IsFitted && !IsContinuous && State == JammerState.Ready;

        /// <summary>
        /// Progress of the current burst as a 1 → 0 fraction (1 the instant it starts, 0 when it
        /// ends). Zero whenever the emitter is not radiating a timed burst.
        /// </summary>
        public float BurstFraction
        {
            get
            {
                if (State != JammerState.Active || IsContinuous || BurstSeconds <= 0f) return 0f;
                return Mathf.Clamp01(SecondsRemaining / BurstSeconds);
            }
        }

        /// <summary>
        /// Readiness as a 0 → 1 fraction: 1 whenever a burst is available (or running), and climbing
        /// from 0 back to 1 across the cooldown. Shaped for a HUD bar where more is better.
        /// </summary>
        public float ReadyFraction
        {
            get
            {
                if (State != JammerState.Cooling) return 1f;
                if (CooldownSeconds <= 0f) return 1f;
                return Mathf.Clamp01(1f - SecondsRemaining / CooldownSeconds);
            }
        }

        /// <summary>
        /// Starts a burst. Returns false and changes nothing when no emitter is fitted, when the
        /// emitter is continuous, or when it is still running/recharging — so a caller may hammer the
        /// key without ever producing an invalid state.
        /// </summary>
        public bool TryActivate()
        {
            if (!CanActivate) return false;

            State = JammerState.Active;
            SecondsRemaining = BurstSeconds;
            return true;
        }

        /// <summary>
        /// Advances the duty cycle by <paramref name="dt"/> seconds. A non-positive step is ignored,
        /// and a step longer than the remaining phase does NOT overshoot into the phase after next:
        /// the burst ends and the full cooldown starts, exactly as a per-frame caller would see it.
        /// </summary>
        public void Tick(float dt)
        {
            if (dt <= 0f) return;
            if (IsContinuous) return;   // no timers at all

            switch (State)
            {
                case JammerState.Active:
                    SecondsRemaining -= dt;
                    if (SecondsRemaining > 0f) return;
                    State = JammerState.Cooling;
                    SecondsRemaining = CooldownSeconds;
                    if (SecondsRemaining <= 0f)
                    {
                        State = JammerState.Ready;
                        SecondsRemaining = 0f;
                    }
                    return;

                case JammerState.Cooling:
                    SecondsRemaining -= dt;
                    if (SecondsRemaining > 0f) return;
                    State = JammerState.Ready;
                    SecondsRemaining = 0f;
                    return;

                default:
                    return;
            }
        }

        /// <summary>Returns the emitter to its start state (rearm / respawn).</summary>
        public void Reset()
        {
            State = IsContinuous && IsFitted ? JammerState.Active : JammerState.Ready;
            SecondsRemaining = 0f;
        }

        /// <summary>
        /// The factor every hostile detection range against the carrier is multiplied by right now:
        /// 1 when idle, <c>1 / (1 + Strength)^0.25</c> while radiating. Convenience reading for the
        /// HUD — it is <see cref="ElectronicWarfare.EffectiveRange"/> of a unit range.
        /// </summary>
        public float DetectionRangeFactor => ElectronicWarfare.EffectiveRange(1f, CurrentStrength);
    }
}
