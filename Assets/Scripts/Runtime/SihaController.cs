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
        // Weapon range >= the inherited sensor detection range (120) so any locked SAM is shootable.
        [SerializeField] private float weaponRange = 120f;

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
            EnsureWeapon();
        }

        /// <summary>
        /// Everything <see cref="IhaController.ApplyProfile"/> applies, plus the guided-munition
        /// magazine and firing range. Dropping <c>_weapon</c> makes <see cref="EnsureWeapon"/> rebuild
        /// the fire control from the new magazine size on next use (mirrors
        /// <see cref="GunTurret.Configure"/>).
        /// </summary>
        public override void ApplyProfile(AircraftProfile profile)
        {
            base.ApplyProfile(profile);
            if (profile == null) return;

            magazineSize = Mathf.Max(0, profile.MissileCapacity);
            weaponRange = profile.MissileRange;
            _weapon = null;
        }

        /// <summary>
        /// Builds the pure-logic fire control on first use. Like the cores in
        /// <see cref="IhaController.EnsureInitialized"/> it is a plain C# object Unity does not
        /// serialize, so it can be null while Update is already running.
        /// </summary>
        private WeaponSystem EnsureWeapon()
        {
            if (_weapon == null) _weapon = new WeaponSystem(magazineSize, roundsPerSecond);
            return _weapon;
        }

        /// <summary>
        /// Base servicing for an armed drone: everything the recon İHA gets (fuel, gun belt, flares)
        /// plus a full missile magazine. Refilling the magazine is what lets a SİHA that shot itself
        /// dry come home, rearm and go back on the attack instead of loitering in ReturnToBase — see
        /// <see cref="EngagementPolicy.Decide"/>, which keys off exactly this ammo fraction.
        /// </summary>
        public override void Resupply()
        {
            base.Resupply();
            EnsureWeapon().Reload();
        }

        protected override void Update()
        {
            // Run flight, navigation, and sensing/lock exactly like the recon drone.
            base.Update();

            // base.Update() can write this drone off (out-of-fuel ground impact), which destroys the
            // GameObject; stop before touching its transform or firing from a wreck.
            if (Crashed) return;

            float dt = Time.deltaTime;
            EnsureWeapon().Tick(dt);

            // Under manual control the pilot decides when to shoot (see TryManualLaunch); the weapon
            // cooldown above still advances so the AI resumes cleanly when control is released.
            if (ManualControl) return;

            // Engagement: only fire on a confirmed lock against an in-range hostile.
            if (Targeting == null || !Targeting.IsLocked) return;

            // FindById can only hand back a LIVE Targetable, but re-check with Unity's == anyway: the
            // object may be destroyed between the lookup and the shot.
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
        /// Player-commanded launch against the currently detected hostile. Reuses the exact AI firing
        /// path (range + ammo/cooldown checks, then <see cref="LaunchMunition"/>) but is driven by the
        /// pilot instead of a confirmed lock, so the player is never left waiting on the lock timer.
        /// Returns true when a munition was actually launched. The AI firing path is unchanged.
        /// </summary>
        public bool TryManualLaunch()
        {
            if (Crashed) return false;
            if (!HasTarget) return false;
            EnsureWeapon();

            Targetable target = TargetRegistry.FindById(DetectedId);
            if (target == null) return false;

            Vector3 self = transform.position;
            Vector3 targetPos = target.transform.position;
            if (Vector3.Distance(self, targetPos) > weaponRange) return false;

            // hasLock: the pilot IS the lock. Ammo and cooldown are still enforced by WeaponSystem.
            if (!_weapon.TryFire(true)) return false;

            if (useGuidedMunition)
            {
                LaunchMunition(self, target);
            }
            else
            {
                target.TakeDamage(damagePerHit);
                Vector3 intercept = Ballistics.ComputeInterceptPoint(self, targetPos, Vector3.zero, projectileSpeed);
                Debug.DrawLine(self, intercept, Color.red);
            }
            return true;
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
            // Pass the SİHA's own damage explicitly (clean param, no reflection).
            munition.Launch(target, transform.forward * munitionSpeed, damagePerHit);

            // Launch whoosh, spatialised at the rail. Cosmetic; a missing clip is simply silence.
            AudioDirector.PlayAt(origin, AudioLibrary.MissileLaunch, 0.55f, 1f, 6f, 140f);
        }
    }
}
