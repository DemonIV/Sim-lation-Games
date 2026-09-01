using System.Collections.Generic;
using UnityEngine;
using Sim.Core;

namespace Sim.Runtime
{
    /// <summary>
    /// Realistic radar sensor that replaces the naive line-of-sight detector on a drone. It couples
    /// the pure-logic <see cref="Sim.Core.RadarSystem"/> (RCS^0.25 range equation, beam limits) with
    /// aspect-dependent <see cref="RcsComponent"/> signatures, <see cref="Jammer"/> range degradation
    /// via <see cref="Sim.Core.ElectronicWarfare"/>, and an alpha-beta <see cref="Sim.Core.TargetTracker"/>
    /// that smooths a noisy measured position into a position/velocity estimate.
    ///
    /// This is a GAME / EDUCATIONAL model with abstract, gamified parameters.
    /// </summary>
    public class RadarSensor : MonoBehaviour
    {
        [Header("Radar")]
        [SerializeField] private float referenceRange = 250f;   // detection range against referenceRcs
        [SerializeField] private float referenceRcs = 1f;       // m^2
        [SerializeField] private float beamWidthDeg = 140f;     // full scan cone
        [SerializeField] private int hostileFaction = 1;
        [SerializeField] private float measurementNoise = 2f;   // meters of gaussian jitter on measurement

        private RadarSystem _radar;
        private TargetTracker _tracker;

        /// <summary>True while the sensor holds a smoothed contact on a hostile.</summary>
        public bool HasContact { get; private set; }

        /// <summary>Instance id of the current contact, or -1 when none.</summary>
        public int ContactId { get; private set; } = -1;

        /// <summary>Filtered (smoothed) estimated position of the current contact.</summary>
        public Vector3 EstimatedPosition { get; private set; }

        /// <summary>Filtered estimated velocity of the current contact.</summary>
        public Vector3 EstimatedVelocity { get; private set; }

        private void Start()
        {
            EnsureInitialized();
        }

        /// <summary>
        /// Builds the pure-logic radar and tracker on first use. These are plain C# objects that Unity
        /// does NOT serialize, so they are null whenever <see cref="Start"/> has not run for this
        /// component (or its managed state was dropped, e.g. by a play-mode domain reload) while
        /// <see cref="Update"/> keeps ticking. Building them lazily — the same pattern
        /// <see cref="GunTurret"/> and <see cref="CountermeasureDispenser"/> already use — makes the
        /// sensor immune to that ordering instead of dereferencing null every frame.
        /// </summary>
        private void EnsureInitialized()
        {
            if (_radar == null)
            {
                _radar = new RadarSystem
                {
                    ReferenceRange = referenceRange,
                    ReferenceRcs = referenceRcs,
                    BeamWidthDeg = beamWidthDeg
                };
            }

            if (_tracker == null) _tracker = new TargetTracker();
        }

        private void Update()
        {
            EnsureInitialized();

            float dt = Time.deltaTime;
            Vector3 self = transform.position;
            Vector3 forward = transform.forward;

            // Scan the registry directly: the old path built a snapshot list and then called
            // FindById per candidate, and every FindById pruned + walked the whole list again
            // (O(n²) per sensor per frame, plus one list allocation). Pruning once here and reading
            // the live Targetables keeps exactly the same candidate set and order.
            TargetRegistry.Prune();
            List<Targetable> all = TargetRegistry.All;

            bool found = false;
            int bestId = -1;
            Vector3 bestPos = Vector3.zero;
            float bestDist = float.MaxValue;

            for (int i = 0; i < all.Count; i++)
            {
                // A target can be DESTROYED while the scan runs (Unity's == reports that as null), in
                // which case there is nothing left to paint — skip it rather than touching a member.
                Targetable t = all[i];
                if (t == null) continue;
                if (t.Faction != hostileFaction) continue;
                if (t.Health != null && t.Health.IsDestroyed) continue;

                Vector3 targetPos = t.transform.position;

                // Aspect-dependent RCS if the target carries an RcsComponent, else the nominal value.
                // The component reference is cached on the Targetable, so this is a field read.
                float rcs = referenceRcs;
                RcsComponent rcsComp = t.Rcs;
                if (rcsComp != null) rcs = rcsComp.RcsFrom(self);

                // Baseline detection range from the radar range equation.
                float range = _radar.DetectionRange(rcs);

                // Noise jamming shortens the burn-through range (cached lookup: free when absent).
                Jammer jammer = t.Jammer;
                if (jammer != null)
                    range = ElectronicWarfare.EffectiveRange(range, jammer.Strength);

                // Detectable if inside the (jamming-reduced) range and within the beam.
                Vector3 to = targetPos - self;
                float dist = to.magnitude;
                if (dist > range) continue;
                if (dist > 1e-6f && Vector3.Angle(forward, to) > beamWidthDeg * 0.5f) continue;

                // Keep the nearest detectable candidate.
                if (dist < bestDist)
                {
                    bestDist = dist;
                    bestId = t.Id;
                    bestPos = targetPos;
                    found = true;
                }
            }

            if (found)
            {
                // A brand-new contact restarts the filter.
                if (bestId != ContactId) _tracker.Reset();

                // Feed a noisy measurement into the alpha-beta filter.
                Vector3 measurement = bestPos + GaussianVector() * measurementNoise;
                _tracker.Update(measurement, dt);

                HasContact = true;
                ContactId = bestId;
                EstimatedPosition = _tracker.Position;
                EstimatedVelocity = _tracker.Velocity;

                Debug.DrawLine(self, EstimatedPosition, Color.cyan);
            }
            else
            {
                HasContact = false;
                ContactId = -1;
                _tracker.Reset();
            }
        }

        /// <summary>A unit-ish gaussian 3-vector via Box-Muller from UnityEngine.Random.</summary>
        private static Vector3 GaussianVector()
        {
            return new Vector3(Gaussian(), Gaussian(), Gaussian());
        }

        /// <summary>Standard normal sample (mean 0, stddev 1) via the Box-Muller transform.</summary>
        private static float Gaussian()
        {
            float u1 = Mathf.Max(1e-6f, Random.value);
            float u2 = Random.value;
            return Mathf.Sqrt(-2f * Mathf.Log(u1)) * Mathf.Cos(2f * Mathf.PI * u2);
        }
    }
}
