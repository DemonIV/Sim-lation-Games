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

        /// <summary>The seeker head state, exposed for HUD/telemetry.</summary>
        public SeekerGimbal Seeker => _seeker;

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

            SetupVisuals();
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

            // Miss: target gone/destroyed or the motor/seeker has timed out.
            if (_target == null || _elapsed > maxLifetime)
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
