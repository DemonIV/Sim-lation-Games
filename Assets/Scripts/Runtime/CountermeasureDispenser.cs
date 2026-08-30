using UnityEngine;
using Sim.Core;

namespace Sim.Runtime
{
    /// <summary>
    /// Flare/chaff dispenser attached to a drone. A thin wrapper over the pure-logic
    /// <see cref="CountermeasureSystem"/> (charges + cooldown) that adds the scene side of a salvo:
    /// a few short-lived bright puffs, and a <see cref="SalvoCount"/> that incoming
    /// <see cref="GuidedMunition"/>s watch so each of them rolls against a given salvo exactly once.
    ///
    /// This is a GAME / EDUCATIONAL model with abstract, gamified parameters.
    /// </summary>
    public class CountermeasureDispenser : MonoBehaviour
    {
        [Header("Dispenser")]
        [SerializeField] private int maxCharges = 8;
        [SerializeField] private float cooldownSeconds = 2f;
        [Range(0f, 1f)]
        [SerializeField] private float decoyProbability = 0.6f;

        [Header("Visuals")]
        // Number of puffs spawned per salvo, and how far they scatter around the drone.
        [SerializeField] private int puffsPerSalvo = 3;
        [SerializeField] private float puffScatter = 1.5f;

        // Built lazily so a spawner's Configure(...) call can precede Start().
        private CountermeasureSystem _system;

        /// <summary>
        /// Monotonic salvo counter, incremented on every successful <see cref="Deploy"/>. Missiles
        /// compare it against the value they last saw to detect a NEW salvo worth rolling against.
        /// </summary>
        public int SalvoCount { get; private set; }

        /// <summary>Remaining charges as a 0..1 fraction, for the HUD.</summary>
        public float ChargeFraction => EnsureSystem().ChargeFraction;

        /// <summary>Remaining charges.</summary>
        public int Charges => EnsureSystem().Charges;

        /// <summary>Base probability that a well-timed salvo defeats a tracking missile.</summary>
        public float DecoyProbability => EnsureSystem().DecoyProbability;

        /// <summary>True when a charge is available and the dispenser is off cooldown.</summary>
        public bool CanDeploy => EnsureSystem().CanDeploy;

        /// <summary>Advances the salvo cooldown. Call once per frame from the owning controller.</summary>
        public void Tick(float dt)
        {
            EnsureSystem().Tick(dt);
        }

        /// <summary>
        /// Overrides the dispenser parameters before <see cref="Start"/>, mirroring the
        /// <c>Configure(...)</c> pattern used by the other runtime components.
        /// </summary>
        public void Configure(int charges, float cooldown, float probability)
        {
            maxCharges = Mathf.Max(0, charges);
            cooldownSeconds = Mathf.Max(0f, cooldown);
            decoyProbability = Mathf.Clamp01(probability);
            // Drop any system already built with the old values; it is rebuilt on next access.
            _system = null;
        }

        /// <summary>
        /// Fires one salvo. Returns true when a charge was actually spent, in which case
        /// <see cref="SalvoCount"/> advances and a brief visual is spawned.
        /// </summary>
        public bool Deploy()
        {
            if (!EnsureSystem().TryDeploy()) return false;

            SalvoCount++;
            SpawnPuffs();
            return true;
        }

        /// <summary>Refills the dispenser (rearm).</summary>
        public void Reload()
        {
            EnsureSystem().Reload();
        }

        private void Start()
        {
            EnsureSystem();
        }

        /// <summary>Builds the pure-logic system on first use, so Configure can precede Start.</summary>
        private CountermeasureSystem EnsureSystem()
        {
            if (_system == null)
                _system = new CountermeasureSystem(maxCharges, cooldownSeconds, decoyProbability);
            return _system;
        }

        /// <summary>Scatters a few small bright puffs around the drone to mark the salvo.</summary>
        private void SpawnPuffs()
        {
            int count = Mathf.Max(0, puffsPerSalvo);
            float scatter = Mathf.Max(0f, puffScatter);
            Vector3 origin = transform.position;
            for (int i = 0; i < count; i++)
                ExplosionEffect.Spawn(origin + Random.insideUnitSphere * scatter, 1f);
        }
    }
}
