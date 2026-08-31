using UnityEngine;
using Sim.Core;

namespace Sim.Runtime
{
    /// <summary>
    /// Reads the owning <see cref="Targetable"/>'s health fraction and makes battle damage visible:
    /// a trailing smoke puff below 55% health, plus a flickering fire glow and darker, faster smoke
    /// below 25%. Emission is throttled by simple accumulators and goes through the budgeted
    /// <see cref="VfxLibrary"/>, so a wing of damaged units cannot flood the scene.
    ///
    /// Purely cosmetic — it only READS <see cref="Health"/> and never changes it.
    /// </summary>
    public class DamageVisuals : MonoBehaviour
    {
        [SerializeField] private float smokeThreshold = 0.55f;
        [SerializeField] private float fireThreshold = 0.25f;

        private Targetable _targetable;
        private float _smokeTimer;
        private float _fireTimer;

        private void Awake()
        {
            _targetable = GetComponent<Targetable>();
        }

        private void Update()
        {
            // The Targetable is created by the spawner; re-resolve defensively if it appeared later.
            if (_targetable == null)
            {
                _targetable = GetComponent<Targetable>();
                if (_targetable == null) return;
            }

            Health health = _targetable.Health;
            if (health == null || health.IsDestroyed) return;

            float fraction = health.Max > 0f ? health.Current / health.Max : 1f;
            if (fraction >= smokeThreshold) return;

            float dt = Time.deltaTime;
            bool critical = fraction < fireThreshold;

            // Emit from just behind the unit so the trail reads as coming off its tail.
            Vector3 emitPoint = transform.position - transform.forward * 1.2f;

            _smokeTimer -= dt;
            if (_smokeTimer <= 0f)
            {
                _smokeTimer = critical ? 0.3f : 0.45f;
                Color smoke = critical
                    ? new Color(0.10f, 0.09f, 0.09f, 0.65f)
                    : new Color(0.30f, 0.29f, 0.28f, 0.45f);
                VfxLibrary.Smoke(emitPoint,
                                 critical ? 1.1f : 0.8f,
                                 critical ? 3.5f : 2.2f,
                                 critical ? 1.3f : 1.8f,
                                 smoke);
            }

            if (!critical) return;

            _fireTimer -= dt;
            if (_fireTimer <= 0f)
            {
                _fireTimer = 0.25f;
                float flicker = Random.Range(0.8f, 1.3f);
                VfxLibrary.Glow(emitPoint,
                                0.4f * flicker,
                                new Color(1f, 0.45f, 0.1f),
                                new Color(2f, 0.8f, 0.15f) * flicker,
                                0.18f);
            }
        }
    }
}
