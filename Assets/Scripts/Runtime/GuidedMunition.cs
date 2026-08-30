using System.Collections.Generic;
using UnityEngine;
using Sim.Core;

namespace Sim.Runtime
{
    /// <summary>
    /// A SİHA guided munition. Steers with the pure-logic <see cref="Sim.Core.ProportionalNavigation"/>
    /// guidance law, has a gimballed <see cref="Sim.Core.SeekerGimbal"/> that must keep the target
    /// within its off-boresight cone, and is perturbed by gravity/drag from a lightweight
    /// <see cref="Sim.Core.BallisticProjectile"/> model while a thrust term trims speed back toward
    /// cruise. On proximity it applies gamified damage to the target and self-destructs.
    ///
    /// NOTE: this is a GAME / EDUCATIONAL guidance model with abstract, gamified parameters — not a
    /// fidelity-accurate weapon simulation.
    /// </summary>
    public class GuidedMunition : MonoBehaviour
    {
        [Header("Guidance")]
        [SerializeField] private float cruiseSpeed = 180f;
        [SerializeField] private float navGain = 4f;
        [SerializeField] private float proximityFuzeRadius = 6f;
        [SerializeField] private float damage = 60f;
        [SerializeField] private float maxLifetime = 12f;

        [Header("Seeker")]
        [SerializeField] private float maxOffBoresightDeg = 45f;
        [SerializeField] private float maxSlewRateDeg = 60f;

        // Lightweight ballistic model providing gravity + drag influence on the flight path.
        private readonly BallisticProjectile _ballistic = new BallisticProjectile
        {
            Mass = 8f,
            DragCoefficient = 0.2f,
            CrossSectionArea = 0.01f
        };

        private Targetable _target;
        private Vector3 _velocity;
        private SeekerGimbal _seeker;
        private float _elapsed;
        private bool _launched;

        // The target's flare/chaff dispenser (if any) plus the last salvo number this munition has
        // already rolled against, so every salvo is rolled exactly ONCE per missile.
        private CountermeasureDispenser _targetCm;
        private int _lastSalvoSeen;

        /// <summary>
        /// Every munition currently in the air. Lets drones see what is shot at them (missile warning
        /// and evasion) without scanning the scene graph. Entries are added on
        /// <see cref="Launch(Targetable, Vector3)"/> and removed in <see cref="OnDestroy"/>.
        /// </summary>
        public static readonly List<GuidedMunition> Active = new List<GuidedMunition>();

        /// <summary>The seeker head state, exposed for HUD/telemetry.</summary>
        public SeekerGimbal Seeker => _seeker;

        /// <summary>The object this munition is homing on, or null once it has lost lock.</summary>
        public Targetable Target => _target;

        /// <summary>Current velocity vector (m/s), for threat geometry on the receiving end.</summary>
        public Vector3 Velocity => _velocity;

        /// <summary>
        /// Arms and fires the munition toward the given target with an initial velocity, overriding the
        /// serialized default warhead <paramref name="damage"/>. Preferred over reflection for wiring a
        /// shooter's damage into the munition.
        /// </summary>
        public void Launch(Targetable target, Vector3 initialVelocity, float damage)
        {
            this.damage = damage;
            Launch(target, initialVelocity);
        }

        /// <summary>Arms and fires the munition toward the given target with an initial velocity.</summary>
        public void Launch(Targetable target, Vector3 initialVelocity)
        {
            _target = target;
            _velocity = initialVelocity;
            Vector3 boresight = initialVelocity.sqrMagnitude > 1e-6f ? initialVelocity.normalized : transform.forward;
            _seeker = new SeekerGimbal(boresight)
            {
                MaxOffBoresightDeg = maxOffBoresightDeg,
                MaxSlewRateDeg = maxSlewRateDeg
            };
            _elapsed = 0f;
            _launched = true;

            // Countermeasures: remember the target's dispenser and its CURRENT salvo number, so only
            // salvos released AFTER launch can decoy this munition.
            _targetCm = target != null ? target.GetComponent<CountermeasureDispenser>() : null;
            _lastSalvoSeen = _targetCm != null ? _targetCm.SalvoCount : 0;

            if (!Active.Contains(this)) Active.Add(this);

            SetupVisuals();
        }

        /// <summary>Drops this munition from the active-threat registry.</summary>
        private void OnDestroy()
        {
            Active.Remove(this);
        }

        /// <summary>
        /// Drops every DESTROYED munition from <see cref="Active"/>. A destroyed Unity object compares
        /// equal to null but still holds its slot (OnDestroy may not have run yet, e.g. when the whole
        /// GameObject is torn down), and reading <c>.Target</c>/<c>.transform</c> on it throws. Every
        /// missile-warning scan calls this first. Iterates BACKWARDS so removal does not skip entries.
        /// </summary>
        public static void Prune()
        {
            for (int i = Active.Count - 1; i >= 0; i--)
            {
                // Unity's overloaded == detects the destroyed state; never use ReferenceEquals here.
                if (Active[i] == null) Active.RemoveAt(i);
            }
        }

        /// <summary>
        /// Makes the munition read clearly against the scene: a bright emissive body plus a fading
        /// trail. Fully defensive so it is a no-op if renderers/shaders are unavailable.
        /// </summary>
        private void SetupVisuals()
        {
            Color glow = new Color(1f, 0.85f, 0.2f);

            // Bright, self-lit body so the munition pops against the ground/sky.
            var renderer = GetComponent<Renderer>();
            if (renderer != null)
            {
                Shader shader = Shader.Find("Standard");
                if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader != null)
                {
                    var mat = new Material(shader) { color = glow };
                    if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", glow);
                    if (mat.HasProperty("_EmissionColor"))
                    {
                        mat.EnableKeyword("_EMISSION");
                        mat.SetColor("_EmissionColor", glow);
                    }
                    renderer.material = mat;
                }
            }

            // Add a trail if none exists so the flight path is visible.
            var trail = GetComponent<TrailRenderer>();
            if (trail == null)
            {
                trail = gameObject.AddComponent<TrailRenderer>();
                trail.time = 0.5f;
                trail.startWidth = 0.3f;
                trail.endWidth = 0f;
                trail.startColor = glow;
                trail.endColor = new Color(glow.r, glow.g, glow.b, 0f);

                Shader trailShader = Shader.Find("Sprites/Default");
                if (trailShader == null) trailShader = Shader.Find("Unlit/Color");
                if (trailShader == null) trailShader = Shader.Find("Standard");
                if (trailShader != null) trail.material = new Material(trailShader) { color = glow };
            }
        }

        private void FixedUpdate()
        {
            if (!_launched) return;

            float dt = Time.fixedDeltaTime;
            _elapsed += dt;

            // Miss: target gone/destroyed or the motor/seeker has timed out. _target == null uses
            // Unity's overloaded comparison, so a DESTROYED Targetable counts as gone here.
            if (_target == null || _elapsed > maxLifetime)
            {
                Destroy(gameObject);
                return;
            }

            // Defensive: the seeker is built in Launch. If it is somehow missing (state lost before the
            // first step) the munition cannot guide, so scrub it instead of dereferencing null below.
            if (_seeker == null)
            {
                Destroy(gameObject);
                return;
            }

            Vector3 self = transform.position;
            Vector3 targetPos = _target.transform.position;

            // Line of sight to the target and the seeker slew toward it.
            Vector3 los = targetPos - self;
            Vector3 boresight = _velocity.sqrMagnitude > 1e-6f ? _velocity.normalized : transform.forward;
            _seeker.Track(boresight, los.sqrMagnitude > 1e-6f ? los.normalized : boresight, dt);

            // Countermeasures: when the target has released a NEW flare/chaff salvo, roll ONCE for it.
            // An early release against a missile that is off the target's nose works best
            // (see Sim.Core.MissileThreat).
            if (_targetCm != null && _targetCm.SalvoCount != _lastSalvoSeen)
            {
                _lastSalvoSeen = _targetCm.SalvoCount;

                float range = los.magnitude;
                // Relative velocity with the target abstracted to zero, as in the guidance below.
                float closing = ProportionalNavigation.ClosingVelocity(los, -_velocity);
                float tti = MissileThreat.TimeToImpact(range, closing);
                Vector3 toMissile = self - targetPos;
                float aspectDot = toMissile.sqrMagnitude > 1e-6f
                    ? Vector3.Dot(_target.transform.forward, toMissile.normalized)
                    : 0f;

                if (Random.value < MissileThreat.DecoyChance(_targetCm.DecoyProbability, tti, aspectDot))
                {
                    // Decoyed: the seeker breaks lock. The existing miss path (null target) destroys
                    // the munition on the next step.
                    ExplosionEffect.Spawn(self, 1.5f);
                    _target = null;
                    _targetCm = null;
                    return;
                }
            }

            // Proportional navigation guidance (target velocity abstracted to zero for now).
            Vector3 relPos = targetPos - self;
            Vector3 relVel = Vector3.zero - _velocity;
            Vector3 accel = ProportionalNavigation.Acceleration(relPos, relVel, navGain);

            // Gravity + aerodynamic drag influence from the ballistic model.
            var state = new BallisticState(self, _velocity);
            accel += _ballistic.Acceleration(state);

            // Thrust term: trim speed back toward cruise along the current heading.
            float speed = _velocity.magnitude;
            if (speed > 1e-6f)
                accel += _velocity.normalized * (cruiseSpeed - speed);

            // Integrate velocity and gently renormalize toward cruise so it keeps flying.
            _velocity += accel * dt;
            float newSpeed = _velocity.magnitude;
            if (newSpeed > 1e-6f)
                _velocity = _velocity.normalized * Mathf.Lerp(newSpeed, cruiseSpeed, 0.1f);

            // Move and orient along the velocity vector.
            Vector3 newPos = self + _velocity * dt;
            transform.position = newPos;
            if (_velocity.sqrMagnitude > 1e-6f)
                transform.forward = _velocity.normalized;

            Debug.DrawLine(self, newPos, Color.magenta);

            // Proximity fuze: detonate when close enough.
            if (Vector3.Distance(newPos, targetPos) <= proximityFuzeRadius)
            {
                _target.TakeDamage(damage);
                ExplosionEffect.Spawn(transform.position, 3f);
                Destroy(gameObject);
            }
        }
    }
}
