using UnityEngine;
using Sim.Core;

namespace Sim.Runtime
{
    /// <summary>
    /// Onboard noise jammer. Represents a self-protection ECM emitter that raises the noise floor
    /// of hostile radars, shortening the range at which their skin return burns through (see
    /// <see cref="Sim.Core.ElectronicWarfare.EffectiveRange"/>). A <see cref="RadarSensor"/> reads
    /// <see cref="Strength"/> from candidates that carry this component and degrades its detection
    /// range accordingly.
    /// </summary>
    public class Jammer : MonoBehaviour
    {
        [Header("Electronic Warfare")]
        [SerializeField] private float jammerStrength = 4f;
        [SerializeField] private bool active = true;

        /// <summary>Effective jamming strength (0 when inactive). Fed to ElectronicWarfare.EffectiveRange.</summary>
        public float Strength => active ? Mathf.Max(0f, jammerStrength) : 0f;
    }
}
