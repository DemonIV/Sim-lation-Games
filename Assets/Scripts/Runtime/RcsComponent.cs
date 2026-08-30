using UnityEngine;
using Sim.Core;

namespace Sim.Runtime
{
    /// <summary>
    /// Attaches an aspect-dependent radar cross section to a Targetable so that hostile
    /// <see cref="RadarSensor"/>s see a signature that varies with viewing angle (nose/tail-on
    /// is stealthy, broadside is bright). Thin wrapper around the pure-logic
    /// <see cref="Sim.Core.RadarCrossSection"/>.
    /// </summary>
    public class RcsComponent : MonoBehaviour
    {
        [Header("Radar Cross Section (m^2)")]
        [SerializeField] private float baseRcs = 1f;
        [SerializeField] private float frontalRcs = 0.1f;
        [SerializeField] private float broadsideRcs = 5f;

        private RadarCrossSection _rcs;

        private void Awake()
        {
            EnsureRcs();
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
