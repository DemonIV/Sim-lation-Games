using UnityEngine;
using Sim.Core;

namespace Sim.Runtime
{
    /// <summary>
    /// The fighter jet's balistik füze in flight: a HEAVY, UNGUIDED round thrown along the lofted
    /// solution from <see cref="Ballistics.TryLoftedLaunchVelocity"/> and then flown by the pure-logic
    /// <see cref="BallisticProjectile"/> integrator (gravity + drag + wind + air density).
    ///
    /// <para>
    /// HOW IT DIFFERS FROM THE GUIDED MISSILE, and why each difference is there:
    /// </para>
    /// <list type="bullet">
    ///   <item><b>No guidance at all.</b> <see cref="GuidedMunition"/> runs proportional navigation
    ///         through a seeker every step and can chase a manoeuvring target. This round is given one
    ///         velocity at launch and never corrects: it arrives where the aiming solution said the
    ///         DESIGNATED POINT was. A target that drives out from under it is missed, which is the
    ///         price of the double warhead.</item>
    ///   <item><b>Lofted, not flat.</b> The high-arc (+) branch of the solution is used, so the round
    ///         climbs away steeply and falls back down onto the target instead of flying the shallow
    ///         line a 180 m/s missile does.</item>
    ///   <item><b>Slow.</b> It leaves the rail at <see cref="BallisticMissile.LaunchSpeed"/> (70 m/s
    ///         against the missile's 180) and spends the whole arc decelerating and then accelerating
    ///         under gravity, so the pilot waits several seconds for the impact.</item>
    ///   <item><b>Visually heavier.</b> A bigger body, a wider and longer-lived trail, a denser smoke
    ///         column and a blast scaled by <see cref="BallisticMissile.DamageMultiplier"/>.</item>
    /// </list>
    ///
    /// <para>
    /// The MASS/DRAG figures below are the guided munition's own (8 kg, Cd 0.2) scaled for a heavier
    /// store, and the ballistic model is shared rather than re-derived — there is exactly one
    /// ballistics implementation in this project and this is a caller of it.
    /// </para>
    ///
    /// Cosmetic layers are all budgeted through <see cref="VfxLibrary"/>, so a busy firefight degrades
    /// gracefully. This is a GAME / EDUCATIONAL model with abstract, gamified parameters.
    /// </summary>
    public class BallisticRound : MonoBehaviour
    {
        /// <summary>
        /// Blast size the normal guided munition spawns on impact (see
        /// <c>GuidedMunition.FixedUpdate</c>). Kept here only so the figure below can be DERIVED from
        /// it rather than being a second magic number that drifts out of step.
        /// </summary>
        private const float NormalMissileBlastSize = 3f;

        /// <summary>
        /// Visual blast size: the normal missile's, scaled by the same multiplier the warhead is. A
        /// round that hits twice as hard should look it.
        /// </summary>
        private const float BlastSize = NormalMissileBlastSize * BallisticMissile.DamageMultiplier;

        /// <summary>Seconds between exhaust/smoke puffs. Denser than the missile's 0.06 s ribbon.</summary>
        private const float SmokeInterval = 0.045f;

        /// <summary>
        /// Hard lifetime cap in seconds. The launcher passes the computed time of flight plus a
        /// margin; this is only a backstop so a degenerate solution cannot litter the sky forever.
        /// </summary>
        private const float MaxLifetime = 30f;

        // Shared ballistic model: gravity + quadratic drag + wind + altitude density. A heavier,
        // draggier store than the guided munition's 8 kg / Cd 0.2 airframe.
        private readonly BallisticProjectile _ballistic = new BallisticProjectile
        {
            Mass = 24f,
            DragCoefficient = 0.22f,
            CrossSectionArea = 0.016f
        };

        private BallisticState _state;
        private Targetable _target;
        private Vector3 _aimPoint;
        private float _damage;
        private float _life;
        private float _elapsed;
        private float _smokeTimer;
        private bool _launched;
        private bool _detonated;

        // Per-instance materials created for the body and the trail; freed in OnDestroy so the round
        // cannot leak them (finding B-04's rule: an effect owns and destroys its own material).
        private Material _bodyMaterial;
        private Material _trailMaterial;

        /// <summary>The hostile this round was designated against, or null once it is gone.</summary>
        public Targetable Target => _target;

        /// <summary>Current velocity (m/s), for telemetry.</summary>
        public Vector3 Velocity => _state.Velocity;

        /// <summary>Seconds this round has been in the air.</summary>
        public float Elapsed => _elapsed;

        /// <summary>
        /// Throws the round. <paramref name="launchVelocity"/> is the lofted solution the launcher
        /// already computed (see <see cref="Ballistics.TryLoftedLaunchVelocity"/>);
        /// <paramref name="aimPoint"/> is the designated point it was solved against, which is where the
        /// round detonates if the target has moved off it. <paramref name="damage"/> comes from
        /// <see cref="BallisticMissile.Damage"/> — this class never computes a warhead of its own.
        /// </summary>
        public void Launch(Targetable target, Vector3 aimPoint, Vector3 launchVelocity,
                           float damage, float expectedFlightSeconds)
        {
            _target = target;
            _aimPoint = aimPoint;
            _damage = Mathf.Max(0f, damage);
            _state = new BallisticState(transform.position, launchVelocity);

            // Generous margin over the drag-free prediction: drag makes the real arc take LONGER than
            // the closed-form solution says, and the round must still be alive when it gets there.
            float life = expectedFlightSeconds > 0f ? expectedFlightSeconds * 2f + 3f : MaxLifetime;
            _life = Mathf.Clamp(life, 1f, MaxLifetime);

            if (launchVelocity.sqrMagnitude > 1e-6f)
                transform.forward = launchVelocity.normalized;

            SetupVisuals();
            _launched = true;
        }

        private void FixedUpdate()
        {
            if (!_launched || _detonated) return;

            float dt = Time.fixedDeltaTime;
            if (dt <= 0f) return;

            _elapsed += dt;

            Vector3 previous = _state.Position;
            _state = _ballistic.Step(_state, dt);
            Vector3 pos = _state.Position;

            transform.position = pos;
            if (_state.Velocity.sqrMagnitude > 1e-6f)
                transform.forward = _state.Velocity.normalized;

            // Cosmetic: a thick smoke column marking the arc. Budgeted, so it thins out under load.
            _smokeTimer += dt;
            if (_smokeTimer >= SmokeInterval)
            {
                _smokeTimer = 0f;
                VfxLibrary.Smoke(pos, 0.55f, 1.0f, 1.1f, new Color(0.6f, 0.59f, 0.57f, 0.45f));
            }

            Debug.DrawLine(previous, pos, new Color(1f, 0.45f, 0.1f));

            // Proximity fuze against the designated hostile. Unity's overloaded == is what reports a
            // destroyed target here, so it is written out explicitly (never ?.).
            if (_target != null)
            {
                Vector3 targetPos = _target.transform.position;
                if (Vector3.Distance(pos, targetPos) <= BallisticMissile.FuzeRadius)
                {
                    Detonate(_target);
                    return;
                }
            }

            // Arrived at the designated point: detonate there whether or not anything is still on it.
            // An unguided round is aimed at a POINT, so this is the honest outcome of a target that
            // drove away from where the solution was computed.
            if (Vector3.Distance(pos, _aimPoint) <= BallisticMissile.FuzeRadius)
            {
                Detonate(null);
                return;
            }

            // Ground impact: the round has no collider (nothing in this sim uses physics), so the
            // terrain is tested against the same height field every other system reads.
            if (pos.y <= TerrainField.Height(pos.x, pos.z))
            {
                Detonate(null);
                return;
            }

            if (_elapsed >= _life) Detonate(null);
        }

        /// <summary>
        /// Applies the warhead and spawns the blast, then removes the round. <paramref name="hit"/> is
        /// the target the fuze actually triggered on, or null for a point/ground/timeout detonation —
        /// in which case nothing takes damage, exactly as a missed unguided round should behave.
        /// </summary>
        private void Detonate(Targetable hit)
        {
            if (_detonated) return;
            _detonated = true;

            if (hit != null) hit.TakeDamage(_damage);

            ExplosionEffect.Spawn(transform.position, BlastSize);
            Destroy(gameObject);
        }

        /// <summary>
        /// Makes the round read as a heavy store: a larger emissive body than the missile's, a wider
        /// and longer-lived trail, and a bright motor glow at the tail. Fully defensive — a missing
        /// shader simply means a plainer round, never an exception.
        /// </summary>
        private void SetupVisuals()
        {
            // The launcher spawns us from a primitive, so we arrive with a collider. A moving collider
            // with no Rigidbody makes PhysX rebuild its static broadphase every frame (finding B-05).
            VehicleModelBuilder.StripCollider(gameObject);

            // Hotter and more orange than the missile's yellow (1, 0.85, 0.2), so the two weapons are
            // never confused in flight.
            Color glow = new Color(1f, 0.52f, 0.12f);

            var renderer = GetComponent<Renderer>();
            if (renderer != null)
            {
                Shader shader = Shader.Find("Standard");
                if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) shader = Shader.Find("Unlit/Color");
                if (shader != null)
                {
                    _bodyMaterial = new Material(shader) { color = glow };
                    if (_bodyMaterial.HasProperty("_BaseColor"))
                        _bodyMaterial.SetColor("_BaseColor", glow);
                    if (_bodyMaterial.HasProperty("_EmissionColor"))
                    {
                        _bodyMaterial.EnableKeyword("_EMISSION");
                        _bodyMaterial.SetColor("_EmissionColor", glow * 2f);
                    }
                    renderer.material = _bodyMaterial;
                }
            }

            // Trail: roughly 2.5x the guided munition's ribbon (time 1.1 s, startWidth 0.38) so the
            // arc is readable from across the map and obviously heavier than a missile's.
            var trail = GetComponent<TrailRenderer>();
            if (trail == null)
            {
                trail = gameObject.AddComponent<TrailRenderer>();
                trail.time = 2.8f;
                trail.startWidth = 0.95f;
                trail.endWidth = 0f;
                trail.startColor = glow;
                trail.endColor = new Color(glow.r, glow.g, glow.b, 0f);

                Shader trailShader = Shader.Find("Sprites/Default");
                if (trailShader == null) trailShader = Shader.Find("Unlit/Color");
                if (trailShader == null) trailShader = Shader.Find("Standard");
                if (trailShader != null)
                {
                    _trailMaterial = new Material(trailShader) { color = glow };
                    trail.material = _trailMaterial;
                }
            }

            // Cosmetic: a bright booster glow at the tail, bigger than the missile's 0.35 sphere.
            var motor = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            motor.name = "BoosterGlow";
            motor.transform.SetParent(transform, false);
            motor.transform.localPosition = new Vector3(0f, 0f, -0.75f);
            motor.transform.localScale = Vector3.one * 0.6f;
            VehicleModelBuilder.StripCollider(motor);

            Material motorMat = MaterialLibrary.Create(new Color(1f, 0.6f, 0.15f), 0f, 0.6f,
                                                       new Color(4f, 1.8f, 0.4f));
            if (motorMat != null) MaterialLibrary.Apply(motor, motorMat);
        }

        /// <summary>Frees the per-instance materials this round created for its body and trail.</summary>
        private void OnDestroy()
        {
            if (_bodyMaterial != null)
            {
                Destroy(_bodyMaterial);
                _bodyMaterial = null;
            }
            if (_trailMaterial != null)
            {
                Destroy(_trailMaterial);
                _trailMaterial = null;
            }
        }
    }
}
