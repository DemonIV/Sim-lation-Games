using System.Collections.Generic;
using UnityEngine;
using Sim.Core;

namespace Sim.Runtime
{
    /// <summary>
    /// Realistic radar sensor that replaces the naive line-of-sight detector on a drone. It gathers
    /// aspect-dependent <see cref="RcsComponent"/> signatures and <see cref="Jammer"/> strengths from
    /// the scene, hands them to the pure-logic <see cref="Sim.Core.RadarScan"/> sweep (RCS^0.25 range
    /// equation + burn-through + beam limits), and smooths the resulting contact through an
    /// alpha-beta <see cref="Sim.Core.TargetTracker"/>.
    ///
    /// The sweep is driven by the owning controller via <see cref="Scan"/> rather than by its own
    /// Update, so it always runs against the drone's post-move pose instead of a frame-order lottery.
    /// See <see cref="IhaController.RunSensing"/>.
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

        // Reused between sweeps so a per-frame scan does not allocate.
        private readonly List<RadarScanTarget> _candidates = new List<RadarScanTarget>();

        /// <summary>True while the sensor holds a smoothed contact on a hostile.</summary>
        public bool HasContact { get; private set; }

        /// <summary>Instance id of the current contact, or -1 when none.</summary>
        public int ContactId { get; private set; } = -1;

        /// <summary>Filtered (smoothed) estimated position of the current contact.</summary>
        public Vector3 EstimatedPosition { get; private set; }

        /// <summary>Filtered estimated velocity of the current contact.</summary>
        public Vector3 EstimatedVelocity { get; private set; }

        private void Awake()
        {
            _radar = new RadarSystem
            {
                ReferenceRange = referenceRange,
                ReferenceRcs = referenceRcs,
                BeamWidthDeg = beamWidthDeg
            };
            _tracker = new TargetTracker();
        }

        /// <summary>
        /// Runs one radar sweep and advances the track filter. Called by the owning controller after
        /// it has moved the drone, so the sweep uses the current pose.
        /// </summary>
        public void Scan(float dt)
        {
            Vector3 self = transform.position;

            // Gather what the scene knows about each hostile: aspect RCS and jamming strength.
            _candidates.Clear();
            List<DetectableTarget> snapshot = TargetRegistry.GetSnapshot(hostileFaction);
            for (int i = 0; i < snapshot.Count; i++)
            {
                DetectableTarget candidate = snapshot[i];
                Targetable t = TargetRegistry.FindById(candidate.Id);

                float rcs = referenceRcs;
                float jamming = 0f;
                if (t != null)
                {
                    RcsComponent rcsComp = t.GetComponent<RcsComponent>();
                    if (rcsComp != null) rcs = rcsComp.RcsFrom(self);

                    Jammer jammer = t.GetComponent<Jammer>();
                    if (jammer != null) jamming = jammer.Strength;
                }

                _candidates.Add(new RadarScanTarget(candidate.Id, candidate.Position, rcs, jamming));
            }

            // The range equation, burn-through and beam limits all live in Sim.Core.
            if (RadarScan.FindNearest(_radar, self, transform.forward, _candidates, out RadarContact contact))
            {
                // A brand-new contact restarts the filter.
                if (contact.Id != ContactId) _tracker.Reset();

                // Feed a noisy measurement into the alpha-beta filter.
                _tracker.Update(contact.Position + GaussianVector() * measurementNoise, dt);

                HasContact = true;
                ContactId = contact.Id;
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
