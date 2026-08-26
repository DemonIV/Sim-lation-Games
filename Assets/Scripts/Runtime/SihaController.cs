using UnityEngine;
using Sim.Core;

namespace Sim.Runtime
{
    /// <summary>
    /// Armed-drone (SİHA) MonoBehaviour. Builds on the recon behaviour of <see cref="IhaController"/>
    /// (flight + patrol + targeting/lock) and adds a pure-logic <see cref="WeaponSystem"/>. When a
    /// lock is achieved on an in-range hostile, it computes a lead intercept point and fires,
    /// applying abstract, gamified damage to the target <see cref="Targetable"/>.
    /// </summary>
    public class SihaController : IhaController
    {
        [Header("Weapon")]
        [SerializeField] private int magazineSize = 6;
        [SerializeField] private float roundsPerSecond = 1.5f;
        [SerializeField] private float projectileSpeed = 120f;
        [SerializeField] private float damagePerHit = 40f;
        [SerializeField] private float weaponRange = 90f;

        private WeaponSystem _weapon;

        /// <summary>Read-only access to the fire-control state (ammo, cooldown) for HUD/telemetry.</summary>
        public WeaponSystem Weapon => _weapon;

        protected override void Start()
        {
            base.Start();
            _weapon = new WeaponSystem(magazineSize, roundsPerSecond);
        }

        protected override void Update()
        {
            // Run flight, navigation, and sensing/lock exactly like the recon drone.
            base.Update();

            float dt = Time.deltaTime;
            _weapon.Tick(dt);

            // Engagement: only fire on a confirmed lock against an in-range hostile.
            if (!Targeting.IsLocked) return;

            Targetable target = TargetRegistry.FindById(DetectedId);
            if (target == null) return;

            Vector3 self = transform.position;
            Vector3 targetPos = target.transform.position;
            if (Vector3.Distance(self, targetPos) > weaponRange) return;

            if (!_weapon.CanFire(true)) return;

            // Lead the shot for a finite-speed munition (target velocity abstracted to zero for now).
            Vector3 intercept = Ballistics.ComputeInterceptPoint(self, targetPos, Vector3.zero, projectileSpeed);

            if (_weapon.TryFire(true))
            {
                target.TakeDamage(damagePerHit);
                // Visualize the shot for a single frame.
                Debug.DrawLine(self, intercept, Color.red);
            }
        }
    }
}
