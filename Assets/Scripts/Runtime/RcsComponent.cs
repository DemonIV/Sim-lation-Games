using UnityEngine;
using Sim.Core;

namespace Sim.Runtime
{
    /// <summary>
    /// Attaches a radar signature to a Targetable, in the m² units
    /// <see cref="Sim.Core.RadarCrossSection"/> models.
    ///
    /// <para>
    /// It answers two different questions. Hostile ground/air sensors — which scan through
    /// <see cref="TargetRegistry.GetSnapshot(int, System.Collections.Generic.List{DetectableTarget})"/>
    /// and <see cref="Sim.Core.TargetingSystem"/> — read the aspect-INDEPENDENT
    /// <see cref="NominalRcs"/>, so a detection range quoted against the 1 m² baseline keeps meaning
    /// exactly what it says. A friendly <see cref="RadarSensor"/> instead asks
    /// <see cref="RcsFrom"/>, which varies the signature with viewing angle (nose/tail-on is
    /// stealthy, broadside is bright).
    /// </para>
    ///
    /// Thin wrapper around the pure-logic <see cref="Sim.Core.RadarCrossSection"/>.
    /// </summary>
    public class RcsComponent : MonoBehaviour
    {
        /// <summary>
        /// Nose/tail-on signature as a fraction of the nominal one, and broadside as a multiple of
        /// it. These are exactly the shape of <see cref="Sim.Core.RadarCrossSection"/>'s defaults
        /// (0.1 / 1 / 5 m²), so <see cref="Configure"/> only ever SCALES that model — it never
        /// invents a different aspect curve.
        /// </summary>
        private const float FrontalFactor = 0.1f;
        private const float BroadsideFactor = 5f;

        [Header("Radar Cross Section (m^2)")]
        [SerializeField] private float baseRcs = 1f;
        [SerializeField] private float frontalRcs = 0.1f;
        [SerializeField] private float broadsideRcs = 5f;

        private RadarCrossSection _rcs;

        /// <summary>
        /// The aspect-independent signature (m²) this object presents — what hostile detection
        /// ranges are scaled by. Never returns a non-positive value: a zero signature would make the
        /// object literally undetectable, which is a configuration error, not a game mechanic.
        /// </summary>
        public float NominalRcs => baseRcs > 0f ? baseRcs : SignatureDetection.BaselineRcs;

        private void Awake()
        {
            EnsureRcs();
        }

        /// <summary>
        /// Sets this object's nominal signature (m²) and rescales the aspect curve around it, keeping
        /// <see cref="Sim.Core.RadarCrossSection"/>'s frontal/broadside proportions. Spawners call
        /// this straight after <c>AddComponent</c> — i.e. AFTER <see cref="Awake"/> has already run —
        /// so the cached pure-logic object is dropped and rebuilt from the new values. Non-positive
        /// values are ignored.
        /// </summary>
        public void Configure(float nominalRcs)
        {
            if (nominalRcs <= 0f) return;

            baseRcs = nominalRcs;
            frontalRcs = nominalRcs * FrontalFactor;
            broadsideRcs = nominalRcs * BroadsideFactor;

            // Drop the cached signature so the next query rebuilds it from the values above.
            _rcs = null;
        }

        /// <summary>
        /// Builds the pure-logic signature on first use. It is a plain C# object that Unity does not
        /// serialize, so it is null whenever <see cref="Awake"/> has not run for this component while
        /// callers (<see cref="RadarSensor"/>) are already querying it.
        /// </summary>
        private RadarCrossSection EnsureRcs()
        {
            if (_rcs == null)
            {
                _rcs = new RadarCrossSection
                {
                    BaseRcs = baseRcs,
                    FrontalRcs = frontalRcs,
                    BroadsideRcs = broadsideRcs
                };
            }
            return _rcs;
        }

        /// <summary>
        /// The radar cross section this object presents to a radar located at <paramref name="radarPos"/>,
        /// using the object's current facing as the aspect reference.
        /// </summary>
        public float RcsFrom(Vector3 radarPos)
        {
            return EnsureRcs().ValueForAspect(transform.forward, transform.position - radarPos);
        }
    }
}
