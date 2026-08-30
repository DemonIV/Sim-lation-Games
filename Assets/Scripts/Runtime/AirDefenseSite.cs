using System.Collections.Generic;
using UnityEngine;
using Sim.Core;

namespace Sim.Runtime
{
    /// <summary>
    /// A hostile surface-to-air (SAM) site. Scans all around for friendly drones with a pure-logic
    /// <see cref="TargetingSystem"/>, builds a time-based lock, and launches homing
    /// <see cref="GuidedMunition"/>s (proportional-navigation guidance) at the locked drone through a
    /// <see cref="WeaponSystem"/> fire-control. It carries its own <see cref="Targetable"/> (Faction 1)
    /// so drones can suppress it (SEAD), making the engagement two-sided.
    ///
    /// This is a GAME / EDUCATIONAL model with abstract, gamified parameters.
    /// </summary>
    [RequireComponent(typeof(Targetable))]
    public class AirDefenseSite : MonoBehaviour
    {
        [Header("Sensor / Engagement")]
        [SerializeField] private float detectionRange = 140f;
        [SerializeField] private float fireRange = 110f;
        [SerializeField] private float lockTimeSeconds = 1.2f;

        [Header("Weapon")]
        [SerializeField] private int magazineSize = 6;
        [SerializeField] private float roundsPerSecond = 0.4f;
        [SerializeField] private float munitionSpeed = 150f;
        [SerializeField] private float damage = 55f;
        [SerializeField] private int friendlyFaction = 0;

        // Pure-logic cores.
        private TargetingSystem _targeting;
        private WeaponSystem _weapon;

        /// <summary>Id of the friendly drone currently being engaged, or -1 when none.</summary>
        public int CurrentTargetId { get; private set; } = -1;

        private void Start()
        {
            _targeting = new TargetingSystem
            {
                DetectionRange = detectionRange,
                FieldOfViewDeg = 360f,          // omnidirectional scan: any bearing passes the FOV test
                LockTimeSeconds = lockTimeSeconds
            };
            _weapon = new WeaponSystem(magazineSize, roundsPerSecond);
        }

        private void Update()
        {
            if (_targeting == null || _weapon == null) return;

            float dt = Time.deltaTime;
            Vector3 self = transform.position;

            _weapon.Tick(dt);

            // Scan for the nearest friendly drone. With a 360° FOV any boresight works.
            List<DetectableTarget> snapshot = TargetRegistry.GetSnapshot(friendlyFaction);
            bool found = _targeting.TryDetect(self, Vector3.up, snapshot, out DetectableTarget best);

            int detectedId = found ? best.Id : -1;
            _targeting.UpdateLock(found, detectedId, dt);
            CurrentTargetId = found ? detectedId : -1;

            if (!found) return;

            // Warn the engaged drone so it can begin evading (helps show two-sided behaviour).
            Targetable target = TargetRegistry.FindById(detectedId);
            if (target == null) return;

            IhaController drone = target.GetComponent<IhaController>();
            if (drone != null) drone.SetThreat(self, 2.5f);

            // Fire only on a confirmed lock against an in-range, alive drone.
            if (!_targeting.IsLocked) return;

            Vector3 targetPos = target.transform.position;
            if (Vector3.Distance(self, targetPos) > fireRange) return;
            if (target.Health != null && target.Health.IsDestroyed) return;
            if (!_weapon.CanFire(true)) return;

            if (_weapon.TryFire(true))
            {
                LaunchMunition(self, target, targetPos);
                Debug.DrawLine(self, targetPos, Color.red);
            }
        }

        /// <summary>
        /// Spawns a placeholder sphere munition at the site and launches a <see cref="GuidedMunition"/>
        /// toward the given drone. Mirrors <c>SihaController.LaunchMunition</c>.
        /// </summary>
        private void LaunchMunition(Vector3 origin, Targetable target, Vector3 targetPos)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "SAM_Munition";
            go.transform.position = origin;
            go.transform.localScale = Vector3.one * 0.6f;

            Vector3 toTarget = targetPos - origin;
            Vector3 dir = toTarget.sqrMagnitude > 1e-6f ? toTarget.normalized : Vector3.up;
            go.transform.forward = dir;

            // Bright red self-lit placeholder so the incoming SAM reads clearly against the scene.
            var renderer = go.GetComponent<Renderer>();
            if (renderer != null)
            {
                Shader shader = Shader.Find("Unlit/Color");
                if (shader == null) shader = Shader.Find("Standard");
                if (shader != null)
                {
                    Color c = new Color(1f, 0.2f, 0.1f);
                    var mat = new Material(shader) { color = c };
                    if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
                    if (mat.HasProperty("_EmissionColor"))
                    {
                        mat.EnableKeyword("_EMISSION");
                        mat.SetColor("_EmissionColor", c);
                    }
                    renderer.material = mat;
                }
            }

            var munition = go.AddComponent<GuidedMunition>();

            // Apply this site's damage to the munition's private serialized field, consistent with the
            // reflection-based wiring already used in SimulationBootstrap.AssignRoute.
            var damageField = typeof(GuidedMunition).GetField("damage",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (damageField != null) damageField.SetValue(munition, damage);

            munition.Launch(target, dir * munitionSpeed);
        }
    }
}
