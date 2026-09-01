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
    /// <para>
    /// The munition is a BOUNDED pursuer, which is what makes it beatable: its steering is clamped to
    /// the turn rate an airframe pulling <c>maxLoadG</c> can actually fly at its current speed (see
    /// <see cref="Sim.Core.MissileAgility"/>), and its seeker really gates guidance — when the target
    /// stays outside the seeker cone for longer than the grace period the munition goes ballistic
    /// instead of tracking. A target that flies straight is on a collision course and is hit; a target
    /// that breaks hard late demands more load than the airframe has and is missed.
    /// </para>
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

        [Header("Airframe")]
        // Structural load limit in g. Turn rate = maxLoadG * 9.81 / speed, so this is the single knob
        // that decides whether a break turn can defeat this munition. The default is the SİHA's own
        // air-to-ground missile: 18 g at the 180 m/s cruise above is ~57 deg/s, ample against the
        // static ground targets it is fired at. Air-defence rounds override it at launch.
        [SerializeField] private float maxLoadG = 18f;

        [Header("Seeker")]
        [SerializeField] private float maxOffBoresightDeg = 45f;
        [SerializeField] private float maxSlewRateDeg = 60f;
        // How long the seeker may stay off the target line of sight before the munition gives up and
        // goes ballistic. Short enough that a real break defeats the shot, long enough that the normal
        // endgame slew lag (the last fraction of a second) does not.
        [SerializeField] private float lostLockGraceSeconds = 0.4f;

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

        // Smoothed estimate of the target's velocity, finite-differenced from its position each step.
        // TRUE proportional navigation needs the real relative velocity: with the target abstracted to
        // zero the law degenerates into a pursuit course, whose load demand grows the same way whether
        // the target is flying straight or breaking — and then no load limit can tell the two apart.
        private Vector3 _targetVelocity;
        private Vector3 _lastTargetPos;
        private bool _hasLastTargetPos;

        // Seeker gating: how long the seeker has been off the target, and whether the munition has
        // already given up and gone ballistic (unguided) as a result.
        private float _lostLockTimer;
        private bool _lostLock;
        private float _ballisticElapsed;

        // The target's drone controller (if any), so a salvo released DURING a break turn is scored
        // with MissileThreat's break bonus. May be null (static ground targets carry no controller).
        private IhaController _targetDrone;

        /// <summary>
        /// Exponential smoothing applied to the per-step target-velocity estimate. The target moves in
        /// Update while this integrates in FixedUpdate, so a raw finite difference alternates between
        /// zero and a double step; smoothing over ~0.1 s removes that without adding real lag.
        /// </summary>
        private const float TargetVelocitySmoothing = 0.25f;

        /// <summary>
        /// How long a munition that has lost its seeker keeps flying unguided before it self-destructs.
        /// Long enough for the player to SEE it fly harmlessly past, short enough not to litter the sky.
        /// </summary>
        private const float BallisticSelfDestructSeconds = 1.5f;

        // Cosmetic only: guards one-time visual construction and throttles the exhaust trail puffs.
        private bool _visualsBuilt;
        private float _exhaustTimer;

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
        /// True while this munition is still homing. False once it has been decoyed or its seeker has
        /// lost the target for longer than the grace period, i.e. once the shot has been DEFEATED and
        /// it is coasting ballistically. Missile-warning scans skip non-guiding munitions, so beating
        /// a shot clears the warning immediately instead of leaving a phantom threat on the HUD.
        /// </summary>
        public bool IsGuiding => _launched && !_lostLock && _target != null;

        /// <summary>Structural load limit of this airframe, in g. See <see cref="Sim.Core.MissileAgility"/>.</summary>
        public float MaxLoadG => maxLoadG;

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

        /// <summary>
        /// As <see cref="Launch(Targetable, Vector3, float)"/>, but also overrides the cruise speed the
        /// motor trims back toward. Without this the munition accelerates to its serialized cruise speed
        /// no matter how slowly it left the rail, so a shooter that wants a slower missile must set both.
        /// </summary>
        public void Launch(Targetable target, Vector3 initialVelocity, float damage, float cruiseSpeed)
        {
            if (cruiseSpeed > 0f) this.cruiseSpeed = cruiseSpeed;
            Launch(target, initialVelocity, damage);
        }

        /// <summary>
        /// As <see cref="Launch(Targetable, Vector3, float, float)"/>, but also sets the airframe's
        /// structural load limit in g, which decides how hard a target has to break to defeat it.
        /// A non-positive value keeps the serialized default.
        /// </summary>
        public void Launch(Targetable target, Vector3 initialVelocity, float damage, float cruiseSpeed,
                           float maxLoadG)
        {
            if (maxLoadG > 0f) this.maxLoadG = maxLoadG;
            Launch(target, initialVelocity, damage, cruiseSpeed);
        }

        /// <summary>Arms and fires the munition toward the given target with an initial velocity.</summary>
        public void Launch(Targetable target, Vector3 initialVelocity)
        {
            _target = target;
            _velocity = initialVelocity;
            Vector3 boresight = initialVelocity.sqrMagnitude > 1e-6f ? initialVelocity.normalized : transform.forward;

            // The seeker starts looking AT THE TARGET, not down the launch rail: a shooter that fires
            // on a lead course points the airframe ahead of the target, and a seeker seeded with the
            // rail direction would spend the whole (short) flight slewing onto a target it can already
            // see. The off-boresight cone still has to contain that lead angle.
            Vector3 toTarget = target != null ? target.transform.position - transform.position : Vector3.zero;
            Vector3 seekerLook = toTarget.sqrMagnitude > 1e-6f ? toTarget.normalized : boresight;

            _seeker = new SeekerGimbal(seekerLook)
            {
                MaxOffBoresightDeg = maxOffBoresightDeg,
                MaxSlewRateDeg = maxSlewRateDeg
            };
            _elapsed = 0f;
            _launched = true;

            // Guidance state: no lock loss yet, and no target-velocity history to difference against.
            _lostLockTimer = 0f;
            _lostLock = false;
            _ballisticElapsed = 0f;
            _targetVelocity = Vector3.zero;
            _hasLastTargetPos = target != null;
            _lastTargetPos = target != null ? target.transform.position : transform.position;

            // Countermeasures: remember the target's dispenser and its CURRENT salvo number, so only
            // salvos released AFTER launch can decoy this munition.
            _targetCm = target != null ? target.GetComponent<CountermeasureDispenser>() : null;
            _lastSalvoSeen = _targetCm != null ? _targetCm.SalvoCount : 0;

            // Break-turn coupling for that salvo roll (null for targets that are not drones).
            _targetDrone = target != null ? target.GetComponent<IhaController>() : null;

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
            if (_visualsBuilt) return;
            _visualsBuilt = true;

            // The launchers spawn us from a primitive, so we arrive with a collider. Nothing in the
            // simulation uses physics (hits are distance checks in FixedUpdate), and a moving collider
            // with no Rigidbody makes PhysX rebuild its static broadphase every frame — drop it.
            VehicleModelBuilder.StripCollider(gameObject);

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
                // Longer-lived, slightly wider ribbon so an incoming missile reads clearly against
                // the sky and its threat axis can be judged at a glance. Cosmetic only.
                trail.time = 1.1f;
                trail.startWidth = 0.38f;
                trail.endWidth = 0f;
                trail.startColor = glow;
                trail.endColor = new Color(glow.r, glow.g, glow.b, 0f);

                Shader trailShader = Shader.Find("Sprites/Default");
                if (trailShader == null) trailShader = Shader.Find("Unlit/Color");
                if (trailShader == null) trailShader = Shader.Find("Standard");
                if (trailShader != null) trail.material = new Material(trailShader) { color = glow };
            }

            // Cosmetic: a bright emissive sphere at the rear reads as the rocket motor burning.
            var engine = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            engine.name = "EngineGlow";
            engine.transform.SetParent(transform, false);
            engine.transform.localPosition = new Vector3(0f, 0f, -0.6f);
            engine.transform.localScale = Vector3.one * 0.35f;

            var engineCollider = engine.GetComponent<Collider>();
            if (engineCollider != null) Destroy(engineCollider);

            Material engineMat = MaterialLibrary.Create(new Color(1f, 0.7f, 0.2f), 0f, 0.6f,
                                                        new Color(3f, 1.6f, 0.4f));
            if (engineMat != null) MaterialLibrary.Apply(engine, engineMat);
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

            // Target velocity estimate: finite difference of its position, exponentially smoothed.
            // Feeds the TRUE relative velocity into the guidance law below.
            if (dt > 0f && _hasLastTargetPos)
            {
                Vector3 sample = (targetPos - _lastTargetPos) / dt;
                _targetVelocity = Vector3.Lerp(_targetVelocity, sample, TargetVelocitySmoothing);
            }
            _lastTargetPos = targetPos;
            _hasLastTargetPos = true;

            // Line of sight to the target and the seeker slew toward it.
            Vector3 los = targetPos - self;
            Vector3 boresight = _velocity.sqrMagnitude > 1e-6f ? _velocity.normalized : transform.forward;

            // Seeker gate: the seeker really decides whether this munition guides. Track() honours the
            // slew-rate and off-boresight limits and reports whether it is actually ON the line of
            // sight; sustained failure (a target that has broken out of the cone) drops the lock.
            if (!_lostLock)
            {
                bool tracking = _seeker.Track(boresight,
                                              los.sqrMagnitude > 1e-6f ? los.normalized : boresight, dt);
                if (tracking) _lostLockTimer = 0f;
                else _lostLockTimer += dt;

                if (_lostLockTimer > Mathf.Max(0f, lostLockGraceSeconds))
                {
                    _lostLock = true;
                    _ballisticElapsed = 0f;
                }
            }

            if (_lostLock)
            {
                // Unguided from here: coast on gravity/drag/thrust only, then scrub itself.
                _ballisticElapsed += dt;
                if (_ballisticElapsed > BallisticSelfDestructSeconds)
                {
                    ExplosionEffect.Spawn(self, 1.5f);
                    Destroy(gameObject);
                    return;
                }
            }
            else if (_targetCm != null && _targetCm.SalvoCount != _lastSalvoSeen)
            {
                // Countermeasures: when the target has released a NEW flare/chaff salvo, roll ONCE for
                // it. An early release against a missile that is off the target's nose works best, and
                // a salvo thrown DURING a break turn works better still (see Sim.Core.MissileThreat).
                _lastSalvoSeen = _targetCm.SalvoCount;

                float range = los.magnitude;
                float closing = ProportionalNavigation.ClosingVelocity(los, _targetVelocity - _velocity);
                float tti = MissileThreat.TimeToImpact(range, closing);
                Vector3 toMissile = self - targetPos;
                float aspectDot = toMissile.sqrMagnitude > 1e-6f
                    ? Vector3.Dot(_target.transform.forward, toMissile.normalized)
                    : 0f;
                bool breaking = _targetDrone != null && _targetDrone.Breaking;

                if (Random.value < MissileThreat.DecoyChance(_targetCm.DecoyProbability, tti,
                                                             aspectDot, breaking))
                {
                    // Decoyed: the seeker breaks lock. The existing miss path (null target) destroys
                    // the munition on the next step.
                    ExplosionEffect.Spawn(self, 1.5f);
                    _target = null;
                    _targetCm = null;
                    _targetDrone = null;
                    return;
                }
            }

            // Heading and speed BEFORE this step's steering, for the structural turn limit below.
            Vector3 headingBefore = boresight;
            float speedBefore = _velocity.magnitude;

            // Proportional navigation guidance against the TRUE relative velocity. Suspended once the
            // seeker has given up: an unguided round only flies gravity, drag and thrust.
            Vector3 accel = Vector3.zero;
            if (!_lostLock)
            {
                Vector3 relPos = targetPos - self;
                Vector3 relVel = _targetVelocity - _velocity;
                accel = ProportionalNavigation.Acceleration(relPos, relVel, navGain);
            }

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

            // STRUCTURAL TURN LIMIT. Without this the guidance law rotates the velocity vector by
            // whatever angle it likes each step, which makes the munition an unbeatable pursuer: any
            // line-of-sight rate the target generates is matched within a frame. Clamping the heading
            // change to maxLoadG at the current speed is what gives a late, hard break a chance.
            if (speedBefore > 1e-3f && _velocity.sqrMagnitude > 1e-6f)
            {
                float maxTurnRad = MissileAgility.MaxTurnRateRad(maxLoadG, speedBefore);
                Vector3 limited = MissileAgility.ClampTurn(headingBefore, _velocity.normalized,
                                                           maxTurnRad, dt);
                _velocity = limited * _velocity.magnitude;
            }

            // Move and orient along the velocity vector.
            Vector3 newPos = self + _velocity * dt;
            transform.position = newPos;
            if (_velocity.sqrMagnitude > 1e-6f)
                transform.forward = _velocity.normalized;

            Debug.DrawLine(self, newPos, Color.magenta);

            // Cosmetic: leave a puffed exhaust trail alongside the TrailRenderer. Throttled so the
            // effect budget is not eaten by a single missile.
            _exhaustTimer += dt;
            if (_exhaustTimer >= 0.06f)
            {
                _exhaustTimer = 0f;
                VfxLibrary.Smoke(newPos - transform.forward * 0.8f, 0.35f, 1.2f, 0.7f,
                                 new Color(0.55f, 0.54f, 0.52f, 0.35f));
            }

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
