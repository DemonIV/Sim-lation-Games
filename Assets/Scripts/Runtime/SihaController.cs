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

        [Header("Guided Munition")]
        // When true, firing launches a proportional-navigation GuidedMunition instead of applying
        // instant damage. See GuidedMunition (Sim.Runtime) + ProportionalNavigation (Sim.Core).
        [SerializeField] private bool useGuidedMunition = true;
        [SerializeField] private float munitionSpeed = 180f;

        private WeaponSystem _weapon;

        /// <summary>Read-only access to the fire-control state (ammo, cooldown) for HUD/telemetry.</summary>
        public WeaponSystem Weapon => _weapon;

        /// <summary>Remaining ammunition as a 0..1 fraction, from the weapon's magazine.</summary>
        public override float AmmoFraction =>
            Weapon != null && Weapon.MagazineSize > 0 ? (float)Weapon.Ammo / Weapon.MagazineSize : 0f;

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
                if (useGuidedMunition)
                {
                    // Launch a guided munition (proportional navigation + ballistic model) rather than
                    // applying instant damage. The munition homes on the resolved target Targetable.
                    LaunchMunition(self, target);
                }
                else
                {
                    target.TakeDamage(damagePerHit);
                    // Visualize the shot for a single frame.
                    Debug.DrawLine(self, intercept, Color.red);
                }
            }
        }

        /// <summary>
        /// Spawns a placeholder sphere munition at the launcher, attaches a <see cref="GuidedMunition"/>,
        /// and fires it at the given target along the current heading. The target is the one already
        /// resolved from the lock via <see cref="TargetRegistry.FindById"/> (DetectedId).
        /// </summary>
        private void LaunchMunition(Vector3 origin, Targetable target)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "GuidedMunition";
            go.transform.position = origin;
            go.transform.localScale = Vector3.one * 0.6f;
            go.transform.forward = transform.forward;

            // Bright, self-lit placeholder so the munition reads clearly against the scene.
            var renderer = go.GetComponent<Renderer>();
            if (renderer != null)
            {
                Shader shader = Shader.Find("Unlit/Color");
                if (shader == null) shader = Shader.Find("Standard");
                if (shader != null)
                {
                    var mat = new Material(shader) { color = Color.yellow };
                    if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.yellow);
                    if (mat.HasProperty("_EmissionColor"))
                    {
                        mat.EnableKeyword("_EMISSION");
                        mat.SetColor("_EmissionColor", Color.yellow);
                    }
                    renderer.material = mat;
                }
            }

            var munition = go.AddComponent<GuidedMunition>();
            munition.Launch(target, transform.forward * munitionSpeed);
        }
    }
}
