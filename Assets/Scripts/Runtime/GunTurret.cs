using System.Collections.Generic;
using UnityEngine;
using Sim.Core;

namespace Sim.Runtime
{
    /// <summary>
    /// Thin MonoBehaviour wrapper around the pure-logic <see cref="GunSystem"/>. Owns the ammo belt,
    /// rate of fire and dispersion of a rapid-fire gun, rolls hits with <see cref="HitProbability"/>
    /// and draws an asset-free <see cref="TracerEffect"/> for every round sent downrange.
    ///
    /// All the maths lives in Sim.Core; this component only translates the scene (transforms,
    /// <see cref="Targetable"/>s) into calls on that logic.
    ///
    /// This is a GAME / EDUCATIONAL model with abstract, gamified parameters.
    /// </summary>
    public class GunTurret : MonoBehaviour
    {
        [Header("Gun")]
        [SerializeField] private int magazineSize = 300;
        [SerializeField] private float roundsPerSecond = 10f;
        [SerializeField] private float effectiveRange = 60f;
        [SerializeField] private float dispersionDeg = 2.5f;
        [SerializeField] private float damagePerRound = 4f;

        [Header("Ballistic abstraction")]
        // Effective radius of a typical target, used against the dispersion cone to roll a hit.
        [SerializeField] private float targetRadius = 1.2f;

        [Header("Visuals")]
        [SerializeField] private Color tracerColor = Color.yellow;

        // How far off the aim ray a free-aim shot may still catch an enemy (world units).
        private const float FreeAimRadius = 4f;

        private GunSystem _gun;

        /// <summary>The pure-logic gun state (ammo, cooldown, range, dispersion). Never null after first use.</summary>
        public GunSystem Gun => EnsureGun();

        /// <summary>Remaining belt as a 0..1 fraction, for HUD/telemetry.</summary>
        public float AmmoFraction => EnsureGun().AmmoFraction;

        /// <summary>
        /// Overrides the serialized gun parameters. Spawners call this BEFORE <see cref="Start"/> so the
        /// <see cref="GunSystem"/> is built from the configured values.
        /// </summary>
        public void Configure(int mag, float rps, float range, float dispersion, float damage)
        {
            magazineSize = mag;
            roundsPerSecond = rps;
            effectiveRange = range;
            dispersionDeg = dispersion;
            damagePerRound = damage;
            // Drop any gun already built from the previous values so it is rebuilt on next use.
            _gun = null;
        }

        /// <summary>Sets the tracer colour used by this turret's shots.</summary>
        public void SetTracerColor(Color color) => tracerColor = color;

        private void Start()
        {
            EnsureGun();
        }

        /// <summary>Builds the backing <see cref="GunSystem"/> lazily so Configure may precede Start.</summary>
        private GunSystem EnsureGun()
        {
            if (_gun == null)
                _gun = new GunSystem(magazineSize, roundsPerSecond, effectiveRange, dispersionDeg);
            return _gun;
        }

        /// <summary>Advances the firing cooldown. Called by the owning controller each frame.</summary>
        public void Tick(float dt)
        {
            EnsureGun().Tick(dt);
        }

        /// <summary>
        /// Fires a single round at the given target if it is in range and the gun is ready. Returns true
        /// when a round was actually spent (whether or not it hit).
        /// </summary>
        public bool TryFireAt(Targetable target)
        {
            if (target == null) return false;

            GunSystem gun = EnsureGun();
            Vector3 self = transform.position;
            Vector3 targetPos = target.transform.position;

            float dist = Vector3.Distance(self, targetPos);
            if (!gun.InRange(dist)) return false;
            if (!gun.TryFire()) return false;

            TracerEffect.Spawn(self, targetPos, tracerColor);

            float p = HitProbability.Compute(dist, gun.EffectiveRange, gun.DispersionDeg, targetRadius);
            if (Random.value <= p) target.TakeDamage(damagePerRound);
            return true;
        }

        /// <summary>
        /// Free-aim fire for the player: sends a round toward <paramref name="aimPoint"/> and damages the
        /// nearest enemy <see cref="Targetable"/> lying close to that ray. When nothing is near the ray a
        /// round is still spent and a tracer is drawn (a genuine miss).
        /// </summary>
        public bool TryFireAtPoint(Vector3 aimPoint, int enemyFaction)
        {
            GunSystem gun = EnsureGun();
            if (!gun.TryFire()) return false;

            Vector3 self = transform.position;
            Vector3 aimDir = aimPoint - self;
            if (aimDir.sqrMagnitude <= 1e-6f) aimDir = transform.forward;
            aimDir = aimDir.normalized;

            Targetable hit = FindTargetNearRay(self, aimDir, gun.EffectiveRange, enemyFaction, out float hitDist);

            if (hit == null)
            {
                TracerEffect.Spawn(self, aimPoint, tracerColor);
                return true;
            }

            TracerEffect.Spawn(self, hit.transform.position, tracerColor);

            float p = HitProbability.Compute(hitDist, gun.EffectiveRange, gun.DispersionDeg, targetRadius);
            if (Random.value <= p) hit.TakeDamage(damagePerRound);
            return true;
        }

        /// <summary>
        /// Finds the enemy targetable closest to the aim ray (origin + dir * t, t clamped to [0, range])
        /// within <see cref="FreeAimRadius"/> of it, preferring the nearest one along the ray.
        /// </summary>
        private Targetable FindTargetNearRay(Vector3 origin, Vector3 dir, float range,
                                             int enemyFaction, out float distance)
        {
            distance = 0f;
            Targetable best = null;
            float bestAlong = float.MaxValue;

            List<DetectableTarget> snapshot = TargetRegistry.GetSnapshot(enemyFaction);
            for (int i = 0; i < snapshot.Count; i++)
            {
                Vector3 to = snapshot[i].Position - origin;
                float along = Vector3.Dot(to, dir);
                along = Mathf.Clamp(along, 0f, range);

                Vector3 closest = origin + dir * along;
                float offRay = Vector3.Distance(closest, snapshot[i].Position);
                if (offRay > FreeAimRadius) continue;

                float actual = to.magnitude;
                if (actual > range) continue;

                if (along < bestAlong)
                {
                    Targetable candidate = TargetRegistry.FindById(snapshot[i].Id);
                    if (candidate == null) continue;
                    bestAlong = along;
                    best = candidate;
                    distance = actual;
                }
            }

            return best;
        }
    }
}
