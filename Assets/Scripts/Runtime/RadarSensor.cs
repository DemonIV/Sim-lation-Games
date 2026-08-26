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
            _radar = new RadarSystem
            {
                ReferenceRange = referenceRange,
                ReferenceRcs = referenceRcs,
                BeamWidthDeg = beamWidthDeg
            };
            _tracker = new TargetTracker();
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            Vector3 self = transform.position;
            Vector3 forward = transform.forward;

            List<DetectableTarget> snapshot = TargetRegistry.GetSnapshot(hostileFaction);

            bool found = false;
            int bestId = -1;
            Vector3 bestPos = Vector3.zero;
            float bestDist = float.MaxValue;

            for (int i = 0; i < snapshot.Count; i++)
            {
                DetectableTarget candidate = snapshot[i];

                // Resolve the live Targetable so we can read aspect RCS and jamming state.
                Targetable t = TargetRegistry.FindById(candidate.Id);

                // Aspect-dependent RCS if the target carries an RcsComponent, else the nominal value.
                float rcs = referenceRcs;
                if (t != null)
                {
                    RcsComponent rcsComp = t.GetComponent<RcsComponent>();
                    if (rcsComp != null) rcs = rcsComp.RcsFrom(self);
                }

                // Baseline detection range from the radar range equation.
                float range = _radar.DetectionRange(rcs);

                // Noise jamming shortens the burn-through range.
                if (t != null)
                {
                    Jammer jammer = t.GetComponent<Jammer>();
                    if (jammer != null)
                        range = ElectronicWarfare.EffectiveRange(range, jammer.Strength);
                }

                // Detectable if inside the (jamming-reduced) range and within the beam.
                Vector3 to = candidate.Position - self;
                float dist = to.magnitude;
                if (dist > range) continue;
                if (dist > 1e-6f && Vector3.Angle(forward, to) > beamWidthDeg * 0.5f) continue;

                // Keep the nearest detectable candidate.
                if (dist < bestDist)
                {
                    bestDist = dist;
                    bestId = candidate.Id;
                    bestPos = candidate.Position;
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
