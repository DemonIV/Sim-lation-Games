using System.Collections.Generic;
using UnityEngine;
using Sim.Core;

namespace Sim.Runtime
{
    /// <summary>
    /// Asset-free sound factory: builds every clip in the game from numbers with
    /// <see cref="Sim.Core.AudioSynth"/> and caches it, exactly the way
    /// <see cref="MaterialLibrary"/> caches runtime materials and <see cref="VfxLibrary"/> owns the
    /// visual primitives.
    ///
    /// <para>
    /// WHY. The project contains no audio assets and none can be imported, so a sound is a recipe —
    /// noise through a filter, a couple of oscillators, an envelope — evaluated once into a
    /// <c>float[]</c> and handed to <c>AudioClip.Create</c>. All of the arithmetic lives in
    /// <c>Sim.Core.AudioSynth</c> and is EditMode-tested; this class only writes the recipes and owns
    /// the Unity objects.
    /// </para>
    ///
    /// <para>
    /// BUDGET. A clip is built on FIRST USE and kept for the session — never per shot, never per
    /// frame. The whole set is well under a megabyte, and a failed build is remembered as null so it
    /// is not retried every time. Nothing here throws: if audio is unavailable the properties simply
    /// return null and every caller (see <see cref="AudioDirector"/>) treats that as silence.
    /// </para>
    ///
    /// <para>
    /// LOOPS. <see cref="EngineProp"/>, <see cref="EngineJet"/> and <see cref="EngineAfterburner"/>
    /// must loop without a click, so every tonal partial completes a whole number of cycles inside
    /// the clip (a multiple of 2 Hz in a 0.5 s buffer — <see cref="Loop"/> snaps them there instead
    /// of leaving it to hand-picked round numbers) and the tremolo starts at its maximum. They carry
    /// no envelope, for the same reason.
    /// </para>
    ///
    /// <para>
    /// NOISE IN A LOOP. Tones alone read as a synthesiser; a turbofan is mostly BROADBAND noise. The
    /// loops used to avoid noise altogether because filtered noise cannot simply be looped — the
    /// filters start from rest, so the buffer opens on silence and the wrap ticks once per cycle.
    /// That is now solved at the source rather than dodged: <see cref="AudioSynth.AddLoopableNoise"/>
    /// generates the samples that come AFTER the loop point too and cross-fades them over the head,
    /// so the head begins on exactly the sample that follows the tail and the wrap is an ordinary
    /// step of the same stream. The engine loops are therefore noise-based now.
    /// </para>
    ///
    /// Purely cosmetic — nothing here touches gameplay state.
    /// </summary>
    public static class AudioLibrary
    {
        /// <summary>Sample rate every clip is generated at.</summary>
        public const int SampleRate = AudioSynth.DefaultSampleRate;

        /// <summary>Length of the seamless engine loops, in seconds.</summary>
        public const float EngineLoopSeconds = 0.5f;

        // name -> clip. A stored null means "we tried and it failed"; the entry stops further retries.
        private static readonly Dictionary<string, AudioClip> Cache = new Dictionary<string, AudioClip>();

        /// <summary>How many clips have been generated so far, for diagnostics.</summary>
        public static int ClipCount
        {
            get { return Cache.Count; }
        }

        // ------------------------------------------------------------------ the sounds

        /// <summary>Blast: low-passed noise plus a falling sub-bass sweep under a percussive tail.</summary>
        public static AudioClip Explosion
        {
            get { return Get("Explosion", 1.4f, BuildExplosion); }
        }

        /// <summary>Gun report: a high-passed crack over a short low thump. One per burst, not per round.</summary>
        public static AudioClip GunShot
        {
            get { return Get("GunShot", 0.13f, BuildGunShot); }
        }

        /// <summary>Missile launch: a rising, band-limited whoosh.</summary>
        public static AudioClip MissileLaunch
        {
            get { return Get("MissileLaunch", 1.1f, BuildMissileLaunch); }
        }

        /// <summary>Incoming-missile beep. Played REPEATEDLY at an interval, not looped.</summary>
        public static AudioClip MissileWarning
        {
            get { return Get("MissileWarning", 0.18f, BuildMissileWarning); }
        }

        /// <summary>
        /// The break-window beep: shorter, higher and harder than <see cref="MissileWarning"/>, and
        /// repeated roughly three times as fast — the audible half of "KAÇIŞ MANEVRASI".
        /// </summary>
        public static AudioClip BreakWarning
        {
            get { return Get("BreakWarning", 0.09f, BuildBreakWarning); }
        }

        /// <summary>Soft UI tick for a menu/hangar selection.</summary>
        public static AudioClip UiClick
        {
            get { return Get("UiClick", 0.05f, BuildUiClick); }
        }

        /// <summary>Rising two-tone chime: a purchase went through.</summary>
        public static AudioClip UiConfirm
        {
            get { return Get("UiConfirm", 0.28f, BuildUiConfirm); }
        }

        /// <summary>Low buzz: the purchase was refused (locked, or not enough credits).</summary>
        public static AudioClip UiDenied
        {
            get { return Get("UiDenied", 0.3f, BuildUiDenied); }
        }

        /// <summary>Seamless propeller loop (recon İHA / SİHA): low harmonic stack with a blade chop.</summary>
        public static AudioClip EngineProp
        {
            get { return Get("EngineProp", EngineLoopSeconds, BuildEngineProp); }
        }

        /// <summary>
        /// Seamless turbofan loop (fighter jet): a broadband roar with a high compressor whine and
        /// its blade-passing tones over it.
        /// </summary>
        public static AudioClip EngineJet
        {
            get { return Get("EngineJet", EngineLoopSeconds, BuildEngineJet); }
        }

        /// <summary>
        /// Seamless afterburner LAYER, mixed UNDER <see cref="EngineJet"/> (never instead of it) while
        /// the burner is lit: a low, thunder-like rumble with heavy sub energy and a slow swell.
        /// </summary>
        public static AudioClip EngineAfterburner
        {
            get { return Get("EngineAfterburner", EngineLoopSeconds, BuildEngineAfterburner); }
        }

        // ------------------------------------------------------------------ recipes

        private static void BuildExplosion(float[] d, int rate)
        {
            AudioSynth.AddNoise(d, 0.9f, 4242u);
            AudioSynth.LowPass(d, rate, 650f);                 // the roar
            AudioSynth.AddSweep(d, rate, 140f, 24f, 0.6f);     // the body of the blast
            AudioSynth.ApplyPercussiveEnvelope(d, 0.006f, 4.2f);
            AudioSynth.NormalizeTo(d, 0.95f);
        }

        private static void BuildGunShot(float[] d, int rate)
        {
            AudioSynth.AddNoise(d, 1f, 991u);
            AudioSynth.HighPass(d, rate, 850f);                // the crack
            AudioSynth.AddSweep(d, rate, 420f, 70f, 0.7f);     // the thump under it
            AudioSynth.ApplyPercussiveEnvelope(d, 0.004f, 26f);
            AudioSynth.NormalizeTo(d, 0.85f);
        }

        private static void BuildMissileLaunch(float[] d, int rate)
        {
            AudioSynth.AddNoise(d, 1f, 5150u);
            AudioSynth.LowPass(d, rate, 2400f);
            AudioSynth.AddSweep(d, rate, 60f, 280f, 0.35f);    // rising: it is leaving
            AudioSynth.ApplyEnvelope(d, 0.08f, 0.15f, 0.8f, 0.45f);
            AudioSynth.NormalizeTo(d, 0.8f);
        }

        private static void BuildMissileWarning(float[] d, int rate)
        {
            AudioSynth.AddHarmonicTone(d, rate, 760f, 0.55f);
            AudioSynth.AddSine(d, rate, 1520f, 0.15f);
            AudioSynth.ApplyEnvelope(d, 0.08f, 0.1f, 0.85f, 0.3f);
            AudioSynth.NormalizeTo(d, 0.75f);
        }

        private static void BuildBreakWarning(float[] d, int rate)
        {
            AudioSynth.AddHarmonicTone(d, rate, 1180f, 0.55f);
            AudioSynth.AddSine(d, rate, 2360f, 0.2f);
            AudioSynth.ApplyPercussiveEnvelope(d, 0.06f, 6f);
            AudioSynth.NormalizeTo(d, 0.85f);
        }

        private static void BuildUiClick(float[] d, int rate)
        {
            AudioSynth.AddSine(d, rate, 1400f, 0.5f);
            AudioSynth.AddSine(d, rate, 2100f, 0.2f);
            AudioSynth.ApplyPercussiveEnvelope(d, 0.02f, 18f);
            AudioSynth.NormalizeTo(d, 0.45f);                  // deliberately soft
        }

        private static void BuildUiConfirm(float[] d, int rate)
        {
            AudioSynth.AddSweep(d, rate, 660f, 990f, 0.6f);    // rising = it worked
            AudioSynth.AddSine(d, rate, 1320f, 0.15f);
            AudioSynth.ApplyPercussiveEnvelope(d, 0.02f, 5f);
            AudioSynth.NormalizeTo(d, 0.6f);
        }

        private static void BuildUiDenied(float[] d, int rate)
        {
            AudioSynth.AddHarmonicTone(d, rate, 150f, 0.6f);   // low = it did not
            AudioSynth.ApplyTremolo(d, rate, 18f, 0.8f);       // ...and it buzzes
            AudioSynth.ApplyEnvelope(d, 0.02f, 0.06f, 0.85f, 0.3f);
            AudioSynth.NormalizeTo(d, 0.6f);
        }

        private static void BuildEngineProp(float[] d, int rate)
        {
            // 60 Hz blade fundamental + harmonics; every partial completes whole cycles in 0.5 s.
            AudioSynth.AddSine(d, rate, 60f, 0.50f);
            AudioSynth.AddSine(d, rate, 120f, 0.28f);
            AudioSynth.AddSine(d, rate, 180f, 0.16f);
            AudioSynth.AddSine(d, rate, 240f, 0.09f);
            AudioSynth.AddSine(d, rate, 1200f, 0.03f);         // faint gearbox whine
            AudioSynth.ApplyTremolo(d, rate, 20f, 0.45f);      // blade-pass chop
            AudioSynth.NormalizeTo(d, 0.75f);
        }

        /// <summary>
        /// The fighter's turbofan. A real one is mostly BROADBAND: a low roar from the core and a
        /// wideband hiss from the exhaust, with a narrow, piercing compressor whine on top. The whine
        /// is not one tone either — it is the fan's blade-passing frequency (blade count x shaft
        /// rate) flanked by shaft-rate sidebands, which is what gives a jet its metallic, slightly
        /// beating edge. All of it is loop-safe: the noise beds through
        /// <see cref="AudioSynth.AddLoopableNoise"/>, every tone through <see cref="Loop"/>.
        /// </summary>
        private static void BuildEngineJet(float[] d, int rate)
        {
            // The roar: the core's low broadband energy, and the exhaust hiss well above it.
            uint seed = AudioSynth.AddLoopableNoise(d, rate, 0.55f, 1100f, 70f, 7717u);
            AudioSynth.AddLoopableNoise(d, rate, 0.16f, 9000f, 2600f, seed);

            // Shaft and core tones under the noise: body, not melody.
            AudioSynth.AddSine(d, rate, Loop(110f), 0.20f);
            AudioSynth.AddSine(d, rate, Loop(220f), 0.15f);
            AudioSynth.AddSine(d, rate, Loop(440f), 0.09f);

            // Compressor whine: blade-passing fundamental (30 blades x 110 Hz), its two shaft-rate
            // sidebands, and the second harmonic. The sidebands are what stop it reading as a test
            // tone — they beat against the fundamental at the shaft rate.
            AudioSynth.AddSine(d, rate, Loop(3300f), 0.15f);
            AudioSynth.AddSine(d, rate, Loop(3300f - 110f), 0.07f);
            AudioSynth.AddSine(d, rate, Loop(3300f + 110f), 0.07f);
            AudioSynth.AddSine(d, rate, Loop(6600f), 0.05f);

            AudioSynth.ApplyTremolo(d, rate, Loop(4f), 0.08f); // slight unsteadiness
            AudioSynth.NormalizeTo(d, 0.8f);
        }

        /// <summary>
        /// The afterburner bed. Deliberately NOT "the jet loop but louder": it is a separate layer
        /// with its own character, so lighting the burner is an EVENT. Nearly all of its energy is
        /// low — a sub-bass pair under a heavily low-passed rumble — with a mid-band of grit for the
        /// ragged, torn edge of a burning exhaust, and two slow tremolos beating against each other
        /// so the level rolls rather than sits still. That combination is what reads as thunder.
        /// </summary>
        private static void BuildEngineAfterburner(float[] d, int rate)
        {
            // The thunder itself: a wide, very low noise bed.
            uint seed = AudioSynth.AddLoopableNoise(d, rate, 0.85f, 240f, 22f, 3131u);
            // Grit: the mid band, well below the jet loop's whine so the two layers do not fight.
            AudioSynth.AddLoopableNoise(d, rate, 0.28f, 1500f, 180f, seed);

            // Sub tones for the body a filtered noise bed alone never has.
            AudioSynth.AddSine(d, rate, Loop(46f), 0.30f);
            AudioSynth.AddSine(d, rate, Loop(92f), 0.16f);

            // Two rates, so the swell never settles into an obvious pulse. Both complete whole cycles
            // in the loop, so the product still starts and ends at full gain.
            AudioSynth.ApplyTremolo(d, rate, Loop(6f), 0.32f);
            AudioSynth.ApplyTremolo(d, rate, Loop(14f), 0.16f);
            AudioSynth.NormalizeTo(d, 0.9f);
        }

        // ------------------------------------------------------------------ plumbing

        /// <summary>
        /// Snaps a partial to the nearest frequency that completes whole cycles in
        /// <see cref="EngineLoopSeconds"/> — the condition for a click-free loop. Lets the recipes
        /// above ask for the frequency they actually mean (3300 - 110 Hz, say) instead of only ever
        /// using hand-picked multiples of the loop's 2 Hz bin.
        /// </summary>
        private static float Loop(float frequencyHz)
        {
            return AudioSynth.SnapToLoop(frequencyHz, EngineLoopSeconds);
        }

        /// <summary>
        /// Returns the cached clip, building it on first use. A name that failed to build once is
        /// remembered (as a null entry) and never retried.
        /// </summary>
        private static AudioClip Get(string name, float seconds, System.Action<float[], int> fill)
        {
            AudioClip cached;
            if (Cache.TryGetValue(name, out cached)) return cached;

            AudioClip clip = Build(name, seconds, fill);
            Cache[name] = clip;
            return clip;
        }

        /// <summary>
        /// Fills a sample buffer with the recipe and wraps it in an <see cref="AudioClip"/>. Returns
        /// null — never throws — for a degenerate length or if the audio system refuses the clip.
        /// </summary>
        private static AudioClip Build(string name, float seconds, System.Action<float[], int> fill)
        {
            float[] data = AudioSynth.CreateBuffer(seconds, SampleRate);
            if (data.Length == 0) return null;
            if (fill == null) return null;

            fill(data, SampleRate);
            AudioSynth.Clamp(data);

            try
            {
                AudioClip clip = AudioClip.Create(name, data.Length, 1, SampleRate, false);
                if (clip == null) return null;
                clip.SetData(data, 0);
                return clip;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[AudioLibrary] '{name}' üretilemedi: {e.Message}");
                return null;
            }
        }
    }
}
