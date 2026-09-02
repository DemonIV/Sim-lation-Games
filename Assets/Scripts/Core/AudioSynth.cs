using UnityEngine;

namespace Sim.Core
{
    /// <summary>
    /// THE sound synthesis maths of the project: oscillators, noise, one-pole filters and envelopes
    /// that fill a plain <c>float[]</c> of PCM samples. Pure logic — no Unity scene dependency, no
    /// <c>AudioClip</c>, no <c>AudioSource</c>; the Runtime layer (<c>AudioLibrary</c>) hands the
    /// finished buffer to <c>AudioClip.Create</c> and nothing else.
    ///
    /// <para>
    /// WHY THIS EXISTS. The project ships with NO audio assets and none can be imported, so every
    /// sound in the game is built from numbers at load time. Keeping that arithmetic here (instead of
    /// inside the MonoBehaviours) is the same split the rest of the codebase uses — see
    /// <see cref="Ballistics"/> or <see cref="SignatureDetection"/> — and it is what makes the
    /// synthesis testable in EditMode: a sine really does cross zero twice per cycle, an envelope
    /// really does start and end at silence, and a degenerate request really does come back empty
    /// instead of throwing.
    /// </para>
    ///
    /// <para>
    /// DESIGN. Every generator ADDS into the buffer rather than overwriting it, so a sound is written
    /// as a short recipe: create a buffer, add two oscillators and some noise, filter it, apply an
    /// envelope, clamp. Every entry point answers degenerate input (null buffer, zero length,
    /// non-positive sample rate or frequency) by doing nothing at all.
    /// </para>
    /// </summary>
    public static class AudioSynth
    {
        /// <summary>Sample rate every clip in the project is built at, in Hz.</summary>
        public const int DefaultSampleRate = 44100;

        /// <summary>
        /// Hard cap on a single generated buffer, in seconds. A synthesised clip is held in memory for
        /// the whole session, so 30 s at 44.1 kHz (~5 MB) is the ceiling any one sound may cost; a
        /// longer request is truncated rather than refused.
        /// </summary>
        public const float MaxSeconds = 30f;

        /// <summary>Shared zero-length buffer returned for degenerate requests (never allocated again).</summary>
        public static readonly float[] Empty = new float[0];

        // ------------------------------------------------------------------ buffers

        /// <summary>
        /// Number of samples <paramref name="seconds"/> of audio occupies at
        /// <paramref name="sampleRate"/> Hz. Returns 0 (never negative, never throws) for a
        /// non-positive duration or sample rate, and clamps the duration to <see cref="MaxSeconds"/>.
        /// </summary>
        public static int SampleCount(float seconds, int sampleRate)
        {
            if (seconds <= 0f || sampleRate <= 0) return 0;
            float clamped = Mathf.Min(seconds, MaxSeconds);
            return Mathf.Max(0, Mathf.RoundToInt(clamped * sampleRate));
        }

        /// <summary>
        /// Allocates a silent buffer of the given duration. A degenerate request yields
        /// <see cref="Empty"/> — callers can always index the result's <c>Length</c>, and the Runtime
        /// layer skips <c>AudioClip.Create</c> when it is zero.
        /// </summary>
        public static float[] CreateBuffer(float seconds, int sampleRate)
        {
            int n = SampleCount(seconds, sampleRate);
            return n <= 0 ? Empty : new float[n];
        }

        /// <summary>Duration of a buffer in seconds (0 for an empty buffer or a bad sample rate).</summary>
        public static float DurationOf(float[] buffer, int sampleRate)
        {
            if (buffer == null || buffer.Length == 0 || sampleRate <= 0) return 0f;
            return buffer.Length / (float)sampleRate;
        }

        // ------------------------------------------------------------------ oscillators

        /// <summary>
        /// Adds a sine of <paramref name="frequency"/> Hz at <paramref name="amplitude"/>, starting at
        /// <paramref name="phase"/> radians. The tonal half of every sound in the game.
        /// </summary>
        public static void AddSine(float[] buffer, int sampleRate, float frequency, float amplitude,
                                   float phase = 0f)
        {
            if (buffer == null || buffer.Length == 0) return;
            if (sampleRate <= 0 || frequency <= 0f || amplitude == 0f) return;

            float step = 2f * Mathf.PI * frequency / sampleRate;
            for (int i = 0; i < buffer.Length; i++)
            {
                buffer[i] += amplitude * Mathf.Sin(phase + step * i);
            }
        }

        /// <summary>
        /// Adds a band-limited square-ish tone: the fundamental plus its third and fifth harmonics at
        /// 1/3 and 1/5 amplitude. Reads as "buzzy" (propeller, warning tone) where a bare sine reads
        /// as "flute", at a fraction of the cost of a real waveform table.
        /// </summary>
        public static void AddHarmonicTone(float[] buffer, int sampleRate, float frequency,
                                           float amplitude, float phase = 0f)
        {
            AddSine(buffer, sampleRate, frequency, amplitude, phase);
            AddSine(buffer, sampleRate, frequency * 3f, amplitude / 3f, phase);
            AddSine(buffer, sampleRate, frequency * 5f, amplitude / 5f, phase);
        }

        /// <summary>
        /// Adds a LINEAR frequency sweep (chirp) from <paramref name="startHz"/> to
        /// <paramref name="endHz"/> across the whole buffer. Downward sweeps are the backbone of the
        /// explosion and the missile launch; upward ones of the gun.
        ///
        /// <para>
        /// The phase is integrated analytically — φ(t) = 2π(f₀t + (f₁−f₀)t²/2T) — so the sweep is
        /// continuous and never clicks, which a naive per-sample <c>sin(2πf(t)·t)</c> would.
        /// </para>
        /// </summary>
        public static void AddSweep(float[] buffer, int sampleRate, float startHz, float endHz,
                                    float amplitude)
        {
            if (buffer == null || buffer.Length == 0) return;
            if (sampleRate <= 0 || amplitude == 0f) return;
            if (startHz <= 0f && endHz <= 0f) return;

            float duration = buffer.Length / (float)sampleRate;
            if (duration <= 0f) return;

            float f0 = Mathf.Max(0f, startHz);
            float f1 = Mathf.Max(0f, endHz);
            float slope = (f1 - f0) / (2f * duration);

            for (int i = 0; i < buffer.Length; i++)
            {
                float t = i / (float)sampleRate;
                float phase = 2f * Mathf.PI * (f0 * t + slope * t * t);
                buffer[i] += amplitude * Mathf.Sin(phase);
            }
        }

        /// <summary>
        /// Adds white noise at <paramref name="amplitude"/> using a deterministic xorshift stream, and
        /// returns the advanced seed so successive calls continue it instead of repeating.
        ///
        /// <para>
        /// Deliberately NOT <c>Random.value</c>: the noise must be reproducible in EditMode (the tests
        /// assert on its level), and the global <c>Random.state</c> must not be disturbed — the same
        /// rule the prop scatter follows (finding B-08). Seed 0 is replaced with 1, because xorshift
        /// is stuck at zero.
        /// </para>
        /// </summary>
        public static uint AddNoise(float[] buffer, float amplitude, uint seed)
        {
            uint s = seed != 0u ? seed : 1u;
            if (buffer == null || buffer.Length == 0 || amplitude == 0f) return s;

            for (int i = 0; i < buffer.Length; i++)
            {
                s ^= s << 13;
                s ^= s >> 17;
                s ^= s << 5;
                // uint -> [-1, 1)
                float r = (s / 2147483648f) - 1f;
                buffer[i] += amplitude * r;
            }
            return s;
        }

        /// <summary>
        /// Adds a band-limited noise bed that LOOPS WITHOUT A SEAM, at <paramref name="amplitude"/>
        /// peak. This is what lets a sustained loop (the jet's roar, the afterburner's rumble) be
        /// broadband instead of a bare stack of harmonics — a turbofan is mostly noise, and a pure
        /// tone stack reads as a synthesiser, not an engine.
        ///
        /// <para>
        /// THE SEAM, AND WHY THIS IS NOT JUST <see cref="AddNoise"/> + <see cref="LowPass"/>. A
        /// filtered noise buffer cannot simply be looped: the filters start from rest, so the buffer
        /// begins at (near) silence and ends at whatever value the stream happened to hold, and the
        /// wrap from the last sample back to the first is a step — an audible tick once a loop.
        /// Three things are done about it, all inside this method:
        /// </para>
        /// <list type="number">
        ///   <item>a WARM-UP region is generated and filtered ahead of the buffer and then thrown
        ///   away, so what is kept is the filters' steady state, not their start transient;</item>
        ///   <item>the noise stream is generated <paramref name="buffer"/>'s length PLUS a fade
        ///   region, i.e. the samples that would come immediately AFTER the loop point are computed
        ///   too;</item>
        ///   <item>those continuation samples are cross-faded over the head of the buffer. The head
        ///   therefore starts on exactly x[N] — the sample that genuinely follows x[N−1], which is
        ///   where the buffer ends — so the wrap is one ordinary step of the same noise stream and
        ///   not a discontinuity. The loop is seamless BY CONSTRUCTION rather than by luck.</item>
        /// </list>
        ///
        /// <para>
        /// <paramref name="lowPassHz"/> and <paramref name="highPassHz"/> shape the band (either may
        /// be 0 to skip that filter); the result is normalised so its loudest sample is exactly
        /// <paramref name="amplitude"/>, which keeps a recipe's mix predictable however the band is
        /// set. Returns the advanced seed, exactly like <see cref="AddNoise"/>.
        /// </para>
        /// </summary>
        public static uint AddLoopableNoise(float[] buffer, int sampleRate, float amplitude,
                                            float lowPassHz, float highPassHz, uint seed)
        {
            uint s = seed != 0u ? seed : 1u;
            if (buffer == null || buffer.Length == 0) return s;
            if (sampleRate <= 0 || amplitude == 0f) return s;

            int n = buffer.Length;

            // Cross-fade region: ~20 ms, but never more than an eighth of the loop (a short loop must
            // still keep a majority of un-faded material) and never less than one sample.
            int fade = Mathf.Clamp(n / 8, 1, Mathf.Max(1, sampleRate / 50));

            // Filter warm-up: ~50 ms of material that is generated, filtered and then discarded.
            int warm = Mathf.Clamp(sampleRate / 20, 1, n);

            var work = new float[warm + n + fade];
            s = AddNoise(work, 1f, s);
            if (lowPassHz > 0f) LowPass(work, sampleRate, lowPassHz);
            if (highPassHz > 0f) HighPass(work, sampleRate, highPassHz);

            float peak = Peak(work);
            if (peak <= 1e-6f) return s;
            float gain = amplitude / peak;

            for (int i = 0; i < n; i++)
            {
                float v = work[warm + i];
                if (i < fade)
                {
                    // w = 0 at i = 0, so the first sample IS the stream's continuation past the end.
                    float w = i / (float)fade;
                    v = work[warm + n + i] * (1f - w) + v * w;
                }
                buffer[i] += gain * v;
            }
            return s;
        }

        /// <summary>
        /// Snaps a frequency to the nearest one that completes a WHOLE number of cycles in a loop of
        /// <paramref name="loopSeconds"/>, i.e. to a multiple of 1/<paramref name="loopSeconds"/> Hz.
        ///
        /// <para>
        /// Every partial of a looping engine clip must satisfy this or the loop clicks. It used to be
        /// satisfied by hand-picking round numbers; going through here makes it mechanical, so a
        /// recipe can ask for the frequency it actually wants (a blade-passing sideband at
        /// 3300 − 110 Hz, say) and still get a seamless loop. Never returns less than one full cycle
        /// per loop, and returns 0 for degenerate input.
        /// </para>
        /// </summary>
        public static float SnapToLoop(float frequencyHz, float loopSeconds)
        {
            if (frequencyHz <= 0f || loopSeconds <= 0f) return 0f;

            float bin = 1f / loopSeconds;
            float cycles = Mathf.Round(frequencyHz / bin);
            if (cycles < 1f) cycles = 1f;
            return cycles * bin;
        }

        // ------------------------------------------------------------------ filters

        /// <summary>
        /// One-pole low-pass, in place. Turns white noise into the dull roar of an explosion or the
        /// rumble of an engine. A non-positive cutoff leaves the buffer untouched; a cutoff at or above
        /// Nyquist is a no-op by construction (the coefficient saturates at 1).
        /// </summary>
        public static void LowPass(float[] buffer, int sampleRate, float cutoffHz)
        {
            if (buffer == null || buffer.Length == 0) return;
            if (sampleRate <= 0 || cutoffHz <= 0f) return;

            float dt = 1f / sampleRate;
            float rc = 1f / (2f * Mathf.PI * cutoffHz);
            float a = Mathf.Clamp01(dt / (rc + dt));

            float y = 0f;
            for (int i = 0; i < buffer.Length; i++)
            {
                y += a * (buffer[i] - y);
                buffer[i] = y;
            }
        }

        /// <summary>
        /// One-pole high-pass, in place. Keeps the crack of a gun report from turning to mud once the
        /// low-pass has been applied to the same buffer.
        /// </summary>
        public static void HighPass(float[] buffer, int sampleRate, float cutoffHz)
        {
            if (buffer == null || buffer.Length == 0) return;
            if (sampleRate <= 0 || cutoffHz <= 0f) return;

            float dt = 1f / sampleRate;
            float rc = 1f / (2f * Mathf.PI * cutoffHz);
            float a = Mathf.Clamp01(rc / (rc + dt));

            float prevIn = buffer[0];
            float y = 0f;
            for (int i = 0; i < buffer.Length; i++)
            {
                float x = buffer[i];
                y = a * (y + x - prevIn);
                prevIn = x;
                buffer[i] = y;
            }
        }

        // ------------------------------------------------------------------ envelopes

        /// <summary>
        /// The ADSR envelope as a pure function of NORMALIZED time: <paramref name="t01"/> 0 is the
        /// start of the sound and 1 its end, and <paramref name="attack"/>/<paramref name="decay"/>/
        /// <paramref name="release"/> are FRACTIONS of that whole (not seconds), so the shape is
        /// independent of the clip's length.
        ///
        /// <para>
        /// Guarantees the tests pin down: it is 0 at t=0 and at t=1 (so no clip ever begins or ends
        /// with a click), it reaches exactly 1 at the end of the attack, and it never leaves [0, 1].
        /// Segments that together exceed the whole are scaled down proportionally rather than
        /// overlapping, and a zero-length attack simply starts at full level.
        /// </para>
        /// </summary>
        public static float EnvelopeAt(float t01, float attack, float decay, float sustain, float release)
        {
            float t = Mathf.Clamp01(t01);
            float a = Mathf.Max(0f, attack);
            float d = Mathf.Max(0f, decay);
            float r = Mathf.Max(0f, release);
            float s = Mathf.Clamp01(sustain);

            float span = a + d + r;
            if (span > 1f)
            {
                float k = 1f / span;
                a *= k;
                d *= k;
                r *= k;
            }

            if (t <= 0f || t >= 1f) return 0f;

            if (a > 0f && t < a) return Mathf.Clamp01(t / a);
            if (d > 0f && t < a + d) return Mathf.Clamp01(Mathf.Lerp(1f, s, (t - a) / d));

            float releaseStart = 1f - r;
            if (r > 0f && t >= releaseStart) return Mathf.Clamp01(s * (1f - (t - releaseStart) / r));

            // No attack yet and no decay: the level before the sustain plateau is still full.
            return (a <= 0f && d <= 0f) ? 1f : Mathf.Clamp01(s);
        }

        /// <summary>Multiplies the buffer by <see cref="EnvelopeAt"/> across its whole length.</summary>
        public static void ApplyEnvelope(float[] buffer, float attack, float decay, float sustain,
                                         float release)
        {
            if (buffer == null || buffer.Length <= 0) return;

            int last = buffer.Length - 1;
            for (int i = 0; i < buffer.Length; i++)
            {
                float t = last > 0 ? i / (float)last : 0f;
                buffer[i] *= EnvelopeAt(t, attack, decay, sustain, release);
            }
        }

        /// <summary>
        /// The percussive envelope — a very short attack then an exponential tail, gain =
        /// e^(−<paramref name="decayRate"/>·t). What a gun report, an impact or an explosion actually
        /// sounds like, and what an ADSR is clumsy at. The tail is forced to exactly 0 at the last
        /// sample so the clip still ends in silence.
        /// </summary>
        public static void ApplyPercussiveEnvelope(float[] buffer, float attackFraction, float decayRate)
        {
            if (buffer == null || buffer.Length <= 0) return;

            float a = Mathf.Clamp(attackFraction, 0f, 0.5f);
            float k = Mathf.Max(0f, decayRate);
            int last = buffer.Length - 1;

            for (int i = 0; i < buffer.Length; i++)
            {
                float t = last > 0 ? i / (float)last : 0f;
                float gain;
                if (a > 0f && t < a) gain = t / a;
                else gain = Mathf.Exp(-k * (t - a));

                // End in silence: the exponential tail is faded over the last 2 % of the clip.
                if (t > 0.98f) gain *= Mathf.Clamp01((1f - t) / 0.02f);

                buffer[i] *= Mathf.Clamp01(gain);
            }
        }

        /// <summary>
        /// Amplitude modulation ("tremolo"), in place: multiplies the buffer by
        /// 1 − depth·(1 − cos(2π·rate·t))/2, i.e. a gain that swings between 1 and 1 − depth at
        /// <paramref name="rateHz"/> Hz and starts at 1.
        ///
        /// <para>
        /// This is what turns a static harmonic stack into a PROPELLER (a chop at blade-pass rate) and
        /// a low tone into a "denied" buzzer. A depth of 0, a non-positive rate or a bad sample rate
        /// leaves the buffer untouched.
        /// </para>
        ///
        /// <para>
        /// SEAMLESS LOOPS: the modulator is a cosine starting at its maximum, so as long as the
        /// modulation rate divides the buffer's length in whole cycles, the modulated buffer still
        /// loops without a click — which is exactly how the engine loops are built.
        /// </para>
        /// </summary>
        public static void ApplyTremolo(float[] buffer, int sampleRate, float rateHz, float depth)
        {
            if (buffer == null || buffer.Length == 0) return;
            if (sampleRate <= 0 || rateHz <= 0f) return;

            float d = Mathf.Clamp01(depth);
            if (d <= 0f) return;

            float step = 2f * Mathf.PI * rateHz / sampleRate;
            for (int i = 0; i < buffer.Length; i++)
            {
                float gain = 1f - d * (1f - Mathf.Cos(step * i)) * 0.5f;
                buffer[i] *= gain;
            }
        }

        // ------------------------------------------------------------------ levels

        /// <summary>Largest absolute sample in the buffer (0 for a null/empty one).</summary>
        public static float Peak(float[] buffer)
        {
            if (buffer == null || buffer.Length == 0) return 0f;

            float peak = 0f;
            for (int i = 0; i < buffer.Length; i++)
            {
                float v = buffer[i] < 0f ? -buffer[i] : buffer[i];
                if (v > peak) peak = v;
            }
            return peak;
        }

        /// <summary>Multiplies every sample by <paramref name="gain"/>.</summary>
        public static void Scale(float[] buffer, float gain)
        {
            if (buffer == null || buffer.Length == 0) return;
            for (int i = 0; i < buffer.Length; i++) buffer[i] *= gain;
        }

        /// <summary>
        /// Rescales the buffer so its loudest sample is exactly <paramref name="peak"/>. A silent
        /// buffer is left silent (no division by zero), and a non-positive target is ignored.
        /// </summary>
        public static void NormalizeTo(float[] buffer, float peak)
        {
            if (buffer == null || buffer.Length == 0 || peak <= 0f) return;

            float current = Peak(buffer);
            if (current <= 1e-6f) return;
            Scale(buffer, peak / current);
        }

        /// <summary>
        /// Hard-clips every sample into [−1, 1]. The last line of every recipe: <c>AudioClip.SetData</c>
        /// expects that range, and additive synthesis can easily overshoot it.
        /// </summary>
        public static void Clamp(float[] buffer)
        {
            if (buffer == null || buffer.Length == 0) return;
            for (int i = 0; i < buffer.Length; i++) buffer[i] = Mathf.Clamp(buffer[i], -1f, 1f);
        }
    }
}
