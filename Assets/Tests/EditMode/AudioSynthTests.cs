using NUnit.Framework;
using UnityEngine;
using Sim.Core;

namespace Sim.Tests
{
    /// <summary>
    /// Covers the project's procedural sound synthesis (<see cref="AudioSynth"/>). The project has no
    /// audio assets, so every clip is generated arithmetic — which means the arithmetic is exactly
    /// the kind of thing that can be verified here, without Unity: a sine really crosses zero twice
    /// per cycle, an envelope really opens and closes at silence, a filter really attenuates the side
    /// it is supposed to, and a degenerate request comes back empty instead of throwing.
    /// </summary>
    public class AudioSynthTests
    {
        private const int Rate = AudioSynth.DefaultSampleRate;

        /// <summary>Counts sign changes — twice the number of full cycles in the buffer.</summary>
        private static int ZeroCrossings(float[] buffer)
        {
            int crossings = 0;
            for (int i = 1; i < buffer.Length; i++)
            {
                bool wasPositive = buffer[i - 1] >= 0f;
                bool isPositive = buffer[i] >= 0f;
                if (wasPositive != isPositive) crossings++;
            }
            return crossings;
        }

        /// <summary>Root mean square level — the energy in the buffer, unlike <c>Peak</c>.</summary>
        private static float Rms(float[] buffer)
        {
            if (buffer.Length == 0) return 0f;
            double sum = 0.0;
            for (int i = 0; i < buffer.Length; i++) sum += (double)buffer[i] * buffer[i];
            return Mathf.Sqrt((float)(sum / buffer.Length));
        }

        private static float Mean(float[] buffer)
        {
            if (buffer.Length == 0) return 0f;
            double sum = 0.0;
            for (int i = 0; i < buffer.Length; i++) sum += buffer[i];
            return (float)(sum / buffer.Length);
        }

        // ------------------------------------------------------------------ buffers

        [Test]
        public void DegenerateRequests_ReturnEmpty_NeverThrow()
        {
            Assert.AreEqual(0, AudioSynth.SampleCount(0f, Rate));
            Assert.AreEqual(0, AudioSynth.SampleCount(-1f, Rate));
            Assert.AreEqual(0, AudioSynth.SampleCount(1f, 0));
            Assert.AreEqual(0, AudioSynth.SampleCount(1f, -Rate));

            Assert.AreEqual(0, AudioSynth.CreateBuffer(0f, Rate).Length);
            Assert.AreEqual(0, AudioSynth.CreateBuffer(1f, 0).Length);
            Assert.AreEqual(0, AudioSynth.CreateBuffer(-3f, Rate).Length);

            // A clip is only as long as memory allows: a silly request is truncated, not honoured.
            Assert.AreEqual(AudioSynth.SampleCount(AudioSynth.MaxSeconds, Rate),
                            AudioSynth.SampleCount(AudioSynth.MaxSeconds * 10f, Rate));
        }

        [Test]
        public void EveryEntryPoint_SurvivesANullBuffer()
        {
            Assert.DoesNotThrow(() =>
            {
                AudioSynth.AddSine(null, Rate, 440f, 1f);
                AudioSynth.AddHarmonicTone(null, Rate, 440f, 1f);
                AudioSynth.AddSweep(null, Rate, 100f, 200f, 1f);
                AudioSynth.AddNoise(null, 1f, 12345u);
                AudioSynth.LowPass(null, Rate, 500f);
                AudioSynth.HighPass(null, Rate, 500f);
                AudioSynth.ApplyEnvelope(null, 0.1f, 0.1f, 0.5f, 0.2f);
                AudioSynth.ApplyPercussiveEnvelope(null, 0.01f, 5f);
                AudioSynth.NormalizeTo(null, 0.5f);
                AudioSynth.Scale(null, 2f);
                AudioSynth.Clamp(null);
                Assert.AreEqual(0f, AudioSynth.Peak(null));
                Assert.AreEqual(0f, AudioSynth.DurationOf(null, Rate));
            });

            // ...and an empty one.
            float[] empty = AudioSynth.CreateBuffer(0f, Rate);
            Assert.DoesNotThrow(() =>
            {
                AudioSynth.AddSine(empty, Rate, 440f, 1f);
                AudioSynth.ApplyEnvelope(empty, 0.1f, 0.1f, 0.5f, 0.2f);
                AudioSynth.Clamp(empty);
            });
        }

        [Test]
        public void BufferLength_MatchesTheRequestedDuration()
        {
            float[] buffer = AudioSynth.CreateBuffer(0.5f, Rate);
            Assert.AreEqual(Rate / 2, buffer.Length);
            Assert.AreEqual(0.5f, AudioSynth.DurationOf(buffer, Rate), 1e-4f);

            // A fresh buffer is silence.
            Assert.AreEqual(0f, AudioSynth.Peak(buffer), 1e-6f);
        }

        // ------------------------------------------------------------------ oscillators

        [Test]
        public void Sine_HasTwoZeroCrossingsPerCycle()
        {
            const float freq = 100f;
            float[] buffer = AudioSynth.CreateBuffer(1f, Rate);
            AudioSynth.AddSine(buffer, Rate, freq, 0.5f);

            // One second of 100 Hz = 100 cycles = 200 sign changes (±1 for the truncated last cycle).
            Assert.AreEqual(2f * freq, ZeroCrossings(buffer), 2f);

            // ...at the amplitude asked for, centred on zero.
            Assert.AreEqual(0.5f, AudioSynth.Peak(buffer), 0.01f);
            Assert.AreEqual(0f, Mean(buffer), 0.01f);
        }

        [Test]
        public void Generators_AddIntoTheBufferInsteadOfOverwritingIt()
        {
            float[] one = AudioSynth.CreateBuffer(0.1f, Rate);
            AudioSynth.AddSine(one, Rate, 200f, 0.3f);

            float[] two = AudioSynth.CreateBuffer(0.1f, Rate);
            AudioSynth.AddSine(two, Rate, 200f, 0.3f);
            AudioSynth.AddSine(two, Rate, 200f, 0.3f);

            Assert.AreEqual(2f * AudioSynth.Peak(one), AudioSynth.Peak(two), 0.01f);
        }

        [Test]
        public void Sine_IgnoresNonsenseFrequencyAndAmplitude()
        {
            float[] buffer = AudioSynth.CreateBuffer(0.05f, Rate);
            AudioSynth.AddSine(buffer, Rate, 0f, 1f);
            AudioSynth.AddSine(buffer, Rate, -440f, 1f);
            AudioSynth.AddSine(buffer, Rate, 440f, 0f);
            Assert.AreEqual(0f, AudioSynth.Peak(buffer), 1e-6f);
        }

        [Test]
        public void Sweep_CrossesZeroAtItsMeanFrequency()
        {
            // A LINEAR chirp 100 -> 300 Hz over one second contains as many cycles as a steady 200 Hz
            // tone. That only holds if the phase is integrated (rather than sin(2*pi*f(t)*t)), so this
            // is the test that the sweep is continuous and does not click.
            float[] buffer = AudioSynth.CreateBuffer(1f, Rate);
            AudioSynth.AddSweep(buffer, Rate, 100f, 300f, 0.8f);

            Assert.AreEqual(2f * 200f, ZeroCrossings(buffer), 4f);
            Assert.AreEqual(0.8f, AudioSynth.Peak(buffer), 0.01f);
        }

        [Test]
        public void HarmonicTone_IsBuzzierThanASineButStaysBounded()
        {
            float[] sine = AudioSynth.CreateBuffer(0.2f, Rate);
            AudioSynth.AddSine(sine, Rate, 300f, 0.5f);

            float[] tone = AudioSynth.CreateBuffer(0.2f, Rate);
            AudioSynth.AddHarmonicTone(tone, Rate, 300f, 0.5f);

            // The odd harmonics add energy, and exactly as much as Parseval says they should:
            // sqrt(1 + 1/9 + 1/25) = 1.0729 times the RMS of the bare fundamental.
            Assert.Greater(Rms(tone), Rms(sine));
            Assert.AreEqual(Rms(sine) * Mathf.Sqrt(1f + 1f / 9f + 1f / 25f), Rms(tone), 1e-3f);

            // ...and it can never exceed the sum of its parts, so it stays inside the legal range.
            Assert.LessOrEqual(AudioSynth.Peak(tone), 0.5f * (1f + 1f / 3f + 1f / 5f) + 1e-3f);
        }

        // ------------------------------------------------------------------ noise

        [Test]
        public void Noise_IsDeterministicBoundedAndCentred()
        {
            float[] a = AudioSynth.CreateBuffer(0.2f, Rate);
            float[] b = AudioSynth.CreateBuffer(0.2f, Rate);
            AudioSynth.AddNoise(a, 0.5f, 7u);
            AudioSynth.AddNoise(b, 0.5f, 7u);

            // Same seed, same sound: the clips a build produces are reproducible.
            for (int i = 0; i < a.Length; i += 97) Assert.AreEqual(a[i], b[i], 1e-6f);

            Assert.LessOrEqual(AudioSynth.Peak(a), 0.5f);
            Assert.Greater(AudioSynth.Peak(a), 0.4f);      // it really is noise, not near-silence
            Assert.AreEqual(0f, Mean(a), 0.05f);

            // A different seed is a different stream, and seed 0 is handled (xorshift sticks at zero).
            float[] c = AudioSynth.CreateBuffer(0.2f, Rate);
            AudioSynth.AddNoise(c, 0.5f, 0u);
            Assert.Greater(AudioSynth.Peak(c), 0.4f);
            Assert.AreNotEqual(a[100], c[100]);
        }

        [Test]
        public void Noise_ContinuesFromTheReturnedSeed()
        {
            float[] first = AudioSynth.CreateBuffer(0.01f, Rate);
            uint next = AudioSynth.AddNoise(first, 0.5f, 99u);
            Assert.AreNotEqual(99u, next);

            float[] second = AudioSynth.CreateBuffer(0.01f, Rate);
            AudioSynth.AddNoise(second, 0.5f, next);
            Assert.AreNotEqual(first[0], second[0]);
        }

        // ------------------------------------------------------------------ filters

        [Test]
        public void LowPass_KeepsTheLowSideAndKillsTheHigh()
        {
            float[] low = AudioSynth.CreateBuffer(0.2f, Rate);
            AudioSynth.AddSine(low, Rate, 50f, 0.5f);
            AudioSynth.LowPass(low, Rate, 400f);

            float[] high = AudioSynth.CreateBuffer(0.2f, Rate);
            AudioSynth.AddSine(high, Rate, 6000f, 0.5f);
            AudioSynth.LowPass(high, Rate, 400f);

            Assert.Greater(AudioSynth.Peak(low), 0.4f);
            Assert.Less(AudioSynth.Peak(high), 0.1f);

            // A nonsense cutoff is a no-op, not a wipe.
            float[] untouched = AudioSynth.CreateBuffer(0.05f, Rate);
            AudioSynth.AddSine(untouched, Rate, 1000f, 0.5f);
            AudioSynth.LowPass(untouched, Rate, 0f);
            AudioSynth.LowPass(untouched, Rate, -100f);
            AudioSynth.LowPass(untouched, 0, 500f);
            Assert.AreEqual(0.5f, AudioSynth.Peak(untouched), 0.01f);
        }

        [Test]
        public void HighPass_DoesTheOpposite()
        {
            float[] low = AudioSynth.CreateBuffer(0.2f, Rate);
            AudioSynth.AddSine(low, Rate, 50f, 0.5f);
            AudioSynth.HighPass(low, Rate, 2000f);

            float[] high = AudioSynth.CreateBuffer(0.2f, Rate);
            AudioSynth.AddSine(high, Rate, 8000f, 0.5f);
            AudioSynth.HighPass(high, Rate, 2000f);

            Assert.Less(AudioSynth.Peak(low), 0.1f);
            Assert.Greater(AudioSynth.Peak(high), 0.4f);
        }

        // ------------------------------------------------------------------ envelopes

        [Test]
        public void Envelope_StartsAtSilence_PeaksAtOne_EndsAtSilence()
        {
            const float a = 0.1f, d = 0.2f, s = 0.6f, r = 0.3f;

            Assert.AreEqual(0f, AudioSynth.EnvelopeAt(0f, a, d, s, r), 1e-6f);
            Assert.AreEqual(1f, AudioSynth.EnvelopeAt(a, a, d, s, r), 1e-4f);
            Assert.AreEqual(0f, AudioSynth.EnvelopeAt(1f, a, d, s, r), 1e-6f);

            // Sustain plateau between the decay and the release.
            Assert.AreEqual(s, AudioSynth.EnvelopeAt(0.5f, a, d, s, r), 1e-4f);

            // Never leaves [0, 1] anywhere, including outside the legal time range.
            for (int i = -10; i <= 1010; i++)
            {
                float v = AudioSynth.EnvelopeAt(i / 1000f, a, d, s, r);
                Assert.GreaterOrEqual(v, 0f);
                Assert.LessOrEqual(v, 1f);
            }
        }

        [Test]
        public void Envelope_ScalesOverlongSegmentsInsteadOfBreaking()
        {
            // 0.6 + 0.6 + 0.6 does not fit in 1: the segments are scaled, and the shape still opens
            // and closes at silence with nothing out of range.
            for (int i = 0; i <= 100; i++)
            {
                float v = AudioSynth.EnvelopeAt(i / 100f, 0.6f, 0.6f, 0.5f, 0.6f);
                Assert.GreaterOrEqual(v, 0f);
                Assert.LessOrEqual(v, 1f);
            }
            Assert.AreEqual(0f, AudioSynth.EnvelopeAt(0f, 0.6f, 0.6f, 0.5f, 0.6f), 1e-6f);
            Assert.AreEqual(0f, AudioSynth.EnvelopeAt(1f, 0.6f, 0.6f, 0.5f, 0.6f), 1e-6f);

            // Degenerate: no attack, no decay, no release is a plain rectangular gate at full level.
            Assert.AreEqual(1f, AudioSynth.EnvelopeAt(0.5f, 0f, 0f, 1f, 0f), 1e-6f);
        }

        [Test]
        public void ApplyEnvelope_SilencesBothEndsOfTheClip()
        {
            float[] buffer = AudioSynth.CreateBuffer(0.3f, Rate);
            AudioSynth.AddSine(buffer, Rate, 440f, 1f);
            AudioSynth.ApplyEnvelope(buffer, 0.1f, 0.2f, 0.6f, 0.3f);

            Assert.AreEqual(0f, buffer[0], 1e-6f);
            Assert.AreEqual(0f, buffer[buffer.Length - 1], 1e-6f);
            Assert.LessOrEqual(AudioSynth.Peak(buffer), 1f);
            Assert.Greater(AudioSynth.Peak(buffer), 0.5f);
        }

        [Test]
        public void PercussiveEnvelope_DecaysAndEndsSilent()
        {
            float[] buffer = AudioSynth.CreateBuffer(0.4f, Rate);
            for (int i = 0; i < buffer.Length; i++) buffer[i] = 1f;   // DC, so the result IS the gain
            AudioSynth.ApplyPercussiveEnvelope(buffer, 0.01f, 8f);

            Assert.AreEqual(0f, buffer[0], 1e-6f);
            Assert.AreEqual(0f, buffer[buffer.Length - 1], 1e-6f);

            // Loud at the front, quiet at the back, and monotone through the tail.
            int attackEnd = Mathf.RoundToInt(buffer.Length * 0.02f);
            Assert.Greater(buffer[attackEnd], 0.7f);
            Assert.Less(buffer[buffer.Length / 2], buffer[attackEnd]);
            Assert.Less(buffer[(int)(buffer.Length * 0.9f)], buffer[buffer.Length / 2]);
        }

        [Test]
        public void Tremolo_SwingsBetweenFullAndTheDepth_AndKeepsTheLoopSeamless()
        {
            // DC input, so the buffer IS the modulator.
            float[] buffer = AudioSynth.CreateBuffer(0.5f, Rate);
            for (int i = 0; i < buffer.Length; i++) buffer[i] = 1f;
            AudioSynth.ApplyTremolo(buffer, Rate, 20f, 0.6f);

            // Starts at full gain (and 20 Hz divides 0.5 s in whole cycles, so the loop point matches).
            Assert.AreEqual(1f, buffer[0], 1e-5f);
            Assert.AreEqual(1f, buffer[buffer.Length - 1], 5e-3f);

            // Trough is exactly 1 - depth: half a modulation period in.
            int trough = Mathf.RoundToInt(Rate / 20f / 2f);
            Assert.AreEqual(0.4f, buffer[trough], 1e-3f);

            for (int i = 0; i < buffer.Length; i++)
            {
                Assert.GreaterOrEqual(buffer[i], 0.4f - 1e-4f);
                Assert.LessOrEqual(buffer[i], 1f + 1e-4f);
            }

            // Degenerate settings are no-ops, not wipes.
            float[] untouched = AudioSynth.CreateBuffer(0.05f, Rate);
            AudioSynth.AddSine(untouched, Rate, 500f, 0.5f);
            AudioSynth.ApplyTremolo(untouched, Rate, 0f, 0.5f);
            AudioSynth.ApplyTremolo(untouched, Rate, 10f, 0f);
            AudioSynth.ApplyTremolo(untouched, 0, 10f, 0.5f);
            Assert.AreEqual(0.5f, AudioSynth.Peak(untouched), 0.01f);
        }

        // ------------------------------------------------------------------ loopable noise

        /// <summary>
        /// Mean absolute difference between neighbouring samples — the "normal" step size of the
        /// signal. A seamless loop's wrap has to be one of these; a seam is many times larger.
        /// </summary>
        private static float MeanStep(float[] buffer)
        {
            if (buffer.Length < 2) return 0f;
            double sum = 0.0;
            for (int i = 1; i < buffer.Length; i++) sum += Mathf.Abs(buffer[i] - buffer[i - 1]);
            return (float)(sum / (buffer.Length - 1));
        }

        [Test]
        public void LoopableNoise_IsBandLimitedNoise_AtTheRequestedPeak()
        {
            float[] buffer = AudioSynth.CreateBuffer(0.5f, Rate);
            AudioSynth.AddLoopableNoise(buffer, Rate, 0.6f, 900f, 60f, 7717u);

            // Normalised to the asked-for peak, and actually carrying energy (not near-silence):
            // a noise bed's RMS sits at roughly a quarter of its peak.
            Assert.AreEqual(0.6f, AudioSynth.Peak(buffer), 1e-3f);
            Assert.Greater(Rms(buffer), 0.02f);

            // Band-limited, not white: a 900 Hz low-pass leaves far fewer zero crossings than white
            // noise, which changes sign on about half of all sample pairs.
            Assert.Less(ZeroCrossings(buffer), buffer.Length / 3);

            // ...but it IS noise: neighbouring samples genuinely differ.
            Assert.Greater(MeanStep(buffer), 0f);
        }

        [Test]
        public void LoopableNoise_WrapsWithoutASeam()
        {
            float[] looped = AudioSynth.CreateBuffer(0.5f, Rate);
            AudioSynth.AddLoopableNoise(looped, Rate, 0.6f, 400f, 0f, 2024u);

            // THE invariant of the whole generator. The head is cross-faded starting from exactly the
            // sample that FOLLOWS the tail in the noise stream, so playing the buffer on repeat steps
            // from buffer[N-1] to buffer[0] the way the stream itself steps: the wrap is an ordinary
            // step, not a discontinuity. Six times the mean step is a ~4.8-sigma bound on that.
            float wrap = Mathf.Abs(looped[0] - looped[looped.Length - 1]);
            Assert.Greater(MeanStep(looped), 0f);
            Assert.LessOrEqual(wrap, 6f * MeanStep(looped));

            // And the head is NOT the filter's start transient. Feeding the same noise straight
            // through the low-pass — the naive recipe this generator replaces — always opens on
            // (near) silence, because a one-pole filter starts from rest: its very first output is
            // only a*x[0], with a = dt/(RC+dt) = 0.6 % at 40 Hz. That silent opening is the tick you
            // hear once per loop.
            float[] naive = AudioSynth.CreateBuffer(0.5f, Rate);
            AudioSynth.AddNoise(naive, 1f, 2024u);
            AudioSynth.LowPass(naive, Rate, 40f);
            AudioSynth.NormalizeTo(naive, 0.6f);
            Assert.Less(Mathf.Abs(naive[0]), 0.1f * AudioSynth.Peak(naive));
        }

        [Test]
        public void LoopableNoise_IsDeterministic_AndAnswersDegenerateInputQuietly()
        {
            float[] a = AudioSynth.CreateBuffer(0.05f, Rate);
            float[] b = AudioSynth.CreateBuffer(0.05f, Rate);
            AudioSynth.AddLoopableNoise(a, Rate, 0.5f, 1000f, 0f, 99u);
            AudioSynth.AddLoopableNoise(b, Rate, 0.5f, 1000f, 0f, 99u);
            for (int i = 0; i < a.Length; i += 17) Assert.AreEqual(a[i], b[i], 1e-6f);

            // The seed advances, so two beds in one recipe are two different beds.
            uint next = AudioSynth.AddLoopableNoise(b, Rate, 0.5f, 1000f, 0f, 99u);
            Assert.AreNotEqual(99u, next);

            // Degenerate input: nothing written, nothing thrown.
            float[] untouched = AudioSynth.CreateBuffer(0.05f, Rate);
            AudioSynth.AddLoopableNoise(untouched, 0, 0.5f, 1000f, 0f, 5u);
            AudioSynth.AddLoopableNoise(untouched, Rate, 0f, 1000f, 0f, 5u);
            AudioSynth.AddLoopableNoise(null, Rate, 0.5f, 1000f, 0f, 5u);
            AudioSynth.AddLoopableNoise(AudioSynth.Empty, Rate, 0.5f, 1000f, 0f, 5u);
            Assert.AreEqual(0f, AudioSynth.Peak(untouched), 1e-6f);
        }

        [Test]
        public void SnapToLoop_ReturnsWholeCyclesPerLoop()
        {
            // 0.5 s loop -> the bin is 2 Hz. A blade-passing sideband lands on the grid.
            Assert.AreEqual(3190f, AudioSynth.SnapToLoop(3190f, 0.5f), 1e-3f);
            Assert.AreEqual(3190f, AudioSynth.SnapToLoop(3189.4f, 0.5f), 1e-3f);
            Assert.AreEqual(3192f, AudioSynth.SnapToLoop(3191.4f, 0.5f), 1e-3f);

            // Whatever is asked for, the result completes whole cycles in the loop — which is the
            // condition every partial of a seamless engine loop has to satisfy.
            float[] wanted = { 37f, 110f, 441f, 2999f, 8000.4f };
            for (int i = 0; i < wanted.Length; i++)
            {
                float cycles = AudioSynth.SnapToLoop(wanted[i], 0.5f) * 0.5f;
                Assert.AreEqual(Mathf.Round(cycles), cycles, 1e-3f);
                Assert.GreaterOrEqual(cycles, 1f);
            }

            // Never below one cycle per loop, and degenerate input is 0 rather than an exception.
            Assert.AreEqual(2f, AudioSynth.SnapToLoop(0.4f, 0.5f), 1e-3f);
            Assert.AreEqual(0f, AudioSynth.SnapToLoop(-5f, 0.5f), 1e-6f);
            Assert.AreEqual(0f, AudioSynth.SnapToLoop(440f, 0f), 1e-6f);
        }

        // ------------------------------------------------------------------ levels

        [Test]
        public void NormalizeAndClamp_KeepEverySampleLegal()
        {
            float[] buffer = AudioSynth.CreateBuffer(0.1f, Rate);
            AudioSynth.AddSine(buffer, Rate, 200f, 3f);       // deliberately way over full scale
            Assert.Greater(AudioSynth.Peak(buffer), 1f);

            AudioSynth.NormalizeTo(buffer, 0.8f);
            Assert.AreEqual(0.8f, AudioSynth.Peak(buffer), 1e-3f);

            AudioSynth.Scale(buffer, 4f);
            AudioSynth.Clamp(buffer);
            Assert.LessOrEqual(AudioSynth.Peak(buffer), 1f);
            for (int i = 0; i < buffer.Length; i += 13)
            {
                Assert.GreaterOrEqual(buffer[i], -1f);
                Assert.LessOrEqual(buffer[i], 1f);
            }

            // Silence stays silence (no division by zero), and a nonsense target is ignored.
            float[] silent = AudioSynth.CreateBuffer(0.05f, Rate);
            AudioSynth.NormalizeTo(silent, 0.9f);
            AudioSynth.NormalizeTo(silent, -1f);
            Assert.AreEqual(0f, AudioSynth.Peak(silent), 1e-6f);
        }

        // ------------------------------------------------------------------ a whole recipe

        [Test]
        public void ExplosionRecipe_ProducesAudibleInRangeSamples()
        {
            // The shape the Runtime AudioLibrary actually uses: filtered noise + a downward sweep,
            // percussive envelope, normalized. This is the integration check that a recipe assembled
            // from these parts is playable — every sample legal, and not silence.
            float[] buffer = AudioSynth.CreateBuffer(1.2f, Rate);
            AudioSynth.AddNoise(buffer, 0.9f, 4242u);
            AudioSynth.LowPass(buffer, Rate, 700f);
            AudioSynth.AddSweep(buffer, Rate, 120f, 25f, 0.6f);
            AudioSynth.ApplyPercussiveEnvelope(buffer, 0.01f, 4.5f);
            AudioSynth.NormalizeTo(buffer, 0.95f);
            AudioSynth.Clamp(buffer);

            Assert.AreEqual(0.95f, AudioSynth.Peak(buffer), 1e-3f);
            Assert.AreEqual(0f, buffer[0], 1e-6f);
            Assert.AreEqual(0f, buffer[buffer.Length - 1], 1e-6f);
            for (int i = 0; i < buffer.Length; i += 31)
            {
                Assert.IsFalse(float.IsNaN(buffer[i]));
                Assert.LessOrEqual(Mathf.Abs(buffer[i]), 1f);
            }
        }
    }
}
