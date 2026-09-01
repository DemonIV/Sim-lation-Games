using UnityEngine;

namespace Sim.Core
{
    /// <summary>
    /// Lightweight snapshot of a detectable object for the targeting system.
    ///
    /// <para>
    /// Besides where it is, a snapshot carries HOW VISIBLE it is: its radar cross section and the
    /// strength of any noise jamming it radiates. <see cref="TargetingSystem"/> feeds both into
    /// <see cref="SignatureDetection"/>, so a big airframe is picked up further out than a small one
    /// and a jamming target further in. Both are OPTIONAL: an unset signature reads as the
    /// <see cref="SignatureDetection.BaselineRcs"/> the sensor's range is quoted against, which makes
    /// a snapshot built without them behave exactly like a plain range/FOV contact.
    /// </para>
    /// </summary>
    public struct DetectableTarget
    {
        public int Id;
        public Vector3 Position;
        public Vector3 Velocity;

        /// <summary>
        /// Nominal radar cross section (m²). Non-positive means "not modelled" and is read as
        /// <see cref="SignatureDetection.BaselineRcs"/> — see <see cref="Signature"/>.
        /// </summary>
        public float Rcs;

        /// <summary>Noise-jamming strength this target radiates (0 = none).</summary>
        public float JammerStrength;

        /// <summary>
        /// The signature a sensor should actually use: <see cref="Rcs"/> when it was set, and the
        /// baseline otherwise. Reading it through this property is what keeps a
        /// <c>default(DetectableTarget)</c> — or any snapshot built by the older two/three-argument
        /// constructor — detectable at exactly the sensor's configured range.
        /// </summary>
        public float Signature => Rcs > 0f ? Rcs : SignatureDetection.BaselineRcs;

        public DetectableTarget(int id, Vector3 position, Vector3 velocity = default)
        {
            Id = id;
            Position = position;
            Velocity = velocity;
            Rcs = SignatureDetection.BaselineRcs;
            JammerStrength = 0f;
        }

        /// <summary>Snapshot of a target whose signature (and jamming) the sensor should honour.</summary>
        public DetectableTarget(int id, Vector3 position, Vector3 velocity, float rcs,
                                float jammerStrength)
        {
            Id = id;
            Position = position;
            Velocity = velocity;
            Rcs = rcs;
            JammerStrength = jammerStrength;
        }
    }
}
