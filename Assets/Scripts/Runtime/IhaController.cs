using System.Collections.Generic;
using UnityEngine;
using Sim.Core;

namespace Sim.Runtime
{
    /// <summary>
    /// Recon-drone (İHA) MonoBehaviour. Drives a pure-logic <see cref="FlightModel"/> along a
    /// <see cref="WaypointNavigator"/> patrol route and runs a <see cref="TargetingSystem"/> to
    /// detect and lock hostile <see cref="Targetable"/> objects. No weapon.
    /// </summary>
    public class IhaController : MonoBehaviour
    {
        [Header("Flight")]
        [SerializeField] private float maxSpeed = 30f;
        [SerializeField] private float maxAccel = 8f;
        [SerializeField] private float maxTurnRateDeg = 80f;
        [Range(0f, 1f)]
        [SerializeField] private float throttle = 1f;

        [Header("Patrol")]
        [SerializeField] private List<Transform> patrolWaypoints = new List<Transform>();
        [SerializeField] private float arrivalRadius = 6f;
        [SerializeField] private bool loopPatrol = true;

        [Header("Sensors")]
        [SerializeField] private float detectionRange = 120f;
        [SerializeField] private float fovDeg = 70f;
        [SerializeField] private int hostileFaction = 1;

        [Header("Endurance")]
        [SerializeField] private float fuelCapacity = 100f;
        [SerializeField] private float fuelBurnRate = 2f;

        [Header("Resupply")]
        // How close to BasePosition the drone must be to count as "on station" for servicing.
        [SerializeField] private float baseRadius = 12f;
        // How long it must dwell inside that radius to be refuelled and rearmed.
        [SerializeField] private float serviceSeconds = 4f;

        [Header("Altitude")]
        // Hard floor: the flight model has no ground collision, so never let the drone sink below this.
        [SerializeField] private float minAltitude = 5f;
        // Unpowered sink rate applied once the tank runs dry (dead-stick glide toward the ground).
        [SerializeField] private float sinkRatePerSecond = 6f;

        // Cruise altitude the drone tries to hold (spawn altitude); keeps it approaching horizontally
        // instead of diving into the terrain. Set from BasePosition.y in Start.
        private float _cruiseAltitude;

        // Throttle flown while dwelling at base for servicing. At cruise throttle the turn radius
        // (speed / turn rate) is far larger than baseRadius, so the drone would sail straight back out
        // of the service area; crawling lets it orbit the base until Resupply() fires.
        private const float StationKeepThrottle = 0.15f;

        // Pure-logic cores.
        protected FlightModel _flight;
        protected WaypointNavigator _nav;
        protected TargetingSystem _targeting;

        // Tactical cores (endurance + engagement decision).
        protected FuelTank _fuel;
        protected EngagementPolicy _policy;

        // Base servicing cycle: dwell inside baseRadius for serviceSeconds to be refuelled/rearmed.
        protected ResupplyPoint _resupply;

        // Optional defensive gun, if a GunTurret component is attached to this drone. May be null.
        protected GunTurret _gun;

        // This drone's own Targetable, cached in Start. Used to destroy the drone through the normal
        // damage path (explosion + friendly-loss accounting) when it crashes out of fuel. May be null.
        protected Targetable _self;

        // Optional flare/chaff dispenser (added by the spawner). May be null.
        protected CountermeasureDispenser _cm;

        // World position of the nearest munition currently homing on this drone (valid only while
        // MissileIncoming is true), and whether a salvo was already fired against the current threat.
        private Vector3 _missilePos;
        private bool _flaredThisThreat;

        // Threat memory for evasion: a recorded threat position and the time it expires.
        private Vector3 _threatPos;
        private float _threatExpiry;

        // True once the pure-logic cores below have been built. Guards against Update running before
        // (or without) Start — see EnsureInitialized.
        private bool _initialized;

        /// <summary>
        /// True once this drone has been written off (out-of-fuel ground impact). The GameObject is
        /// destroyed through <see cref="Targetable.TakeDamage"/>, but Unity defers the actual teardown,
        /// so Update can still be entered afterwards; everything downstream of the crash is skipped.
        /// </summary>
        protected bool Crashed { get; private set; }

        /// <summary>True when a hostile target is currently detected within range and FOV.</summary>
        public bool HasTarget { get; private set; }

        /// <summary>Instance id of the currently detected target, or -1 when none.</summary>
        public int DetectedId { get; private set; } = -1;

        /// <summary>Read-only access to the targeting/lock state for companion systems (e.g. a SİHA weapon).</summary>
        public TargetingSystem Targeting => _targeting;

        /// <summary>Remaining fuel as a 0..1 fraction (1 when no tank exists yet).</summary>
        public float FuelFraction => _fuel != null ? _fuel.Fraction : 1f;

        /// <summary>
        /// True once the tank is dry. The engine produces no power (see <see cref="ThrottleGovernor"/>),
        /// the drone sinks and it is destroyed when it reaches the ground.
        /// </summary>
        public bool IsOutOfFuel => FuelFraction <= 0f;

        /// <summary>
        /// Burns fuel from this drone's tank at the given normalized throttle. Lets an external driver
        /// (the human pilot in <see cref="PlayerDroneController"/>) spend the same tank the AI uses.
        /// Null-safe: a no-op before <see cref="Start"/> has built the tank.
        /// </summary>
        public void ConsumeFuel(float throttle01, float dt)
        {
            if (_fuel == null) return;
            _fuel.Consume(throttle01, dt);
        }

        /// <summary>Remaining ammunition as a 0..1 fraction. Recon İHA has no weapon, so always 1.</summary>
        public virtual float AmmoFraction => 1f;

        /// <summary>Progress of the current base servicing cycle as a 0..1 fraction (0 when not servicing).</summary>
        public float ResupplyProgress => _resupply != null ? _resupply.Progress : 0f;

        /// <summary>True while this drone is dwelling at base and being serviced.</summary>
        public bool IsResupplying => _resupply != null && _resupply.IsServicing;

        /// <summary>Current high-level engagement state, driven by <see cref="EngagementPolicy"/> each frame.</summary>
        public EngagementState State { get; protected set; } = EngagementState.Patrol;

        /// <summary>Externally assigned hostile <see cref="Targetable"/> id to head toward, or -1 for none.</summary>
        public int AssignedTargetId { get; set; } = -1;

        /// <summary>Home/base position (spawn point) used when the drone returns to base.</summary>
        public Vector3 BasePosition { get; set; }

        /// <summary>
        /// True while a human pilot (see <see cref="PlayerDroneController"/>) is flying this drone.
        /// The AI steering and AI gunnery are suspended; fuel, engagement state, gun cooldown and
        /// sensing keep running so the HUD and weapons stay live.
        /// </summary>
        public bool ManualControl { get; set; }

        /// <summary>Optional defensive gun attached to this drone, or null when unarmed.</summary>
        public GunTurret Gun => _gun;

        /// <summary>Optional flare/chaff dispenser attached to this drone, or null when it carries none.</summary>
        public CountermeasureDispenser Countermeasures => _cm;

        /// <summary>True while at least one live <see cref="GuidedMunition"/> is homing on this drone.</summary>
        public bool MissileIncoming { get; private set; }

        /// <summary>
        /// Seconds until the nearest incoming munition arrives, or PositiveInfinity when nothing is
        /// closing on this drone. See <see cref="Sim.Core.MissileThreat"/>.
        /// </summary>
        public float TimeToImpact { get; private set; } = float.PositiveInfinity;

        /// <summary>
        /// World position of the nearest incoming munition. Only meaningful while
        /// <see cref="MissileIncoming"/> is true.
        /// </summary>
        public Vector3 IncomingMissilePosition => _missilePos;

        /// <summary>Current airspeed from the flight model (m/s), for HUD/telemetry.</summary>
        public float Speed => _flight != null ? _flight.Speed : 0f;

        /// <summary>
        /// Rewrites the drone's flight state from an external driver (the player pilot). Keeps the
        /// pure-logic <see cref="FlightModel"/> and the scene transform from diverging, so when manual
        /// control is released the AI resumes from exactly where the pilot left the drone.
        /// </summary>
        public void SyncFlightTo(Vector3 position, Vector3 forward)
        {
            SyncFlightTo(position, forward, _flight != null ? _flight.Speed : 0f);
        }

        /// <summary>
        /// <see cref="SyncFlightTo(Vector3, Vector3)"/> with an explicit airspeed, so the AI does not
        /// have to re-accelerate from the model's stale speed after the pilot hands the drone back.
        /// </summary>
        public void SyncFlightTo(Vector3 position, Vector3 forward, float speed)
        {
            bool hasForward = forward.sqrMagnitude > 1e-6f;
            Vector3 fwd = hasForward ? forward.normalized : transform.forward;

            transform.position = position;
            if (hasForward) transform.rotation = Quaternion.LookRotation(fwd, Vector3.up);

            if (_flight == null) return;
            _flight.Position = position;
            _flight.Forward = fwd;
            _flight.Speed = Mathf.Clamp(speed, 0f, _flight.MaxSpeed);
        }

        /// <summary>True while a recorded threat is still active (not yet expired).</summary>
        private bool IsThreatened => Time.time <= _threatExpiry;

        /// <summary>
        /// Records an incoming threat at the given world position for a limited duration. While active,
        /// the drone steers evasively away from it.
        /// </summary>
        public void SetThreat(Vector3 threatWorldPosition, float duration)
        {
            _threatPos = threatWorldPosition;
            _threatExpiry = Time.time + Mathf.Max(0f, duration);
        }

        protected virtual void Start()
        {
            EnsureInitialized();
        }

        /// <summary>
        /// Builds every pure-logic core and resolves the optional sibling components, exactly once.
        /// <para>
        /// All of these are plain C# objects / non-serialized references, so they are null whenever
        /// <see cref="Start"/> has not run for this component while <see cref="Update"/> is already
        /// ticking (Unity keeps calling Update on a behaviour whose managed state was dropped, e.g. by
        /// a play-mode domain reload). Every other controller in the project already guards this;
        /// building lazily here — like <see cref="GunTurret"/> and <see cref="CountermeasureDispenser"/>
        /// do — is what stops <c>_nav</c>/<c>_flight</c> from being dereferenced while null.
        /// </para>
        /// </summary>
        protected void EnsureInitialized()
        {
            if (_initialized) return;
            _initialized = true;

            _flight = new FlightModel(transform.position, transform.forward)
            {
                MaxSpeed = maxSpeed,
                MaxAcceleration = maxAccel,
                MaxTurnRateDeg = maxTurnRateDeg
            };

            var points = new List<Vector3>();
            if (patrolWaypoints != null)
            {
                foreach (Transform wp in patrolWaypoints)
                {
                    if (wp != null) points.Add(wp.position);
                }
            }
            _nav = new WaypointNavigator(points, arrivalRadius, loopPatrol);

            _targeting = new TargetingSystem
            {
                DetectionRange = detectionRange,
                FieldOfViewDeg = fovDeg
            };

            // Tactical cores: fuel/endurance and engagement-state decision.
            _fuel = new FuelTank(fuelCapacity, fuelBurnRate);
            _policy = new EngagementPolicy();

            // Base servicing cycle (dwell timer). See Resupply().
            _resupply = new ResupplyPoint(serviceSeconds);

            // Optional defensive gun (added by the spawner). Null when this drone carries none.
            _gun = GetComponent<GunTurret>();

            // Own damage handle, used for the out-of-fuel ground impact.
            _self = GetComponent<Targetable>();

            // Optional flare/chaff dispenser (added by the spawner). Null when this drone carries none.
            _cm = GetComponent<CountermeasureDispenser>();

            // Remember the spawn point so ReturnToBase can steer home.
            BasePosition = transform.position;
            // Hold the spawn altitude as the cruise altitude.
            _cruiseAltitude = BasePosition.y;
        }

        protected virtual void Update()
        {
            // The drone already hit the ground out of fuel: it is on its way out (Destroy is deferred
            // to the end of the frame), so touching its transform or steering it again is meaningless
            // and unsafe.
            if (Crashed) return;

            // Build the pure-logic cores if Start has not done it. Without this, _nav/_flight can be
            // null here and every dereference below throws once per frame.
            EnsureInitialized();
            if (_flight == null || _nav == null || _targeting == null) return;

            float dt = Time.deltaTime;
            Vector3 pos = transform.position;

            // Endurance: a dry tank means no power, so the GOVERNED throttle (see ThrottleGovernor)
            // drives both the burn and the flight model below — an empty tank stops burning too.
            float effThrottle = ThrottleGovernor.Effective(throttle, FuelFraction);

            // While a human pilot flies this drone the PlayerDroneController owns the burn (it knows
            // the pilot's own throttle and afterburner), so the AI-side burn is skipped to avoid
            // consuming the same tank twice in one frame.
            if (_fuel != null && !ManualControl) _fuel.Consume(effThrottle, dt);

            // Base servicing: dwell inside baseRadius of the spawn point for the full cycle to be
            // refuelled and rearmed. Deliberately NOT gated on fuel — a dead-stick drone that manages
            // to glide home before hitting the ground still gets serviced. It runs for the human pilot
            // too, so landing on station rearms the piloted drone as well.
            if (_resupply != null)
            {
                bool atBase = Vector3.Distance(transform.position, BasePosition) <= baseRadius;
                if (_resupply.Tick(atBase, dt)) Resupply();
            }

            // Engagement decision from the current fuel/ammo/target situation. Because Decide() reads
            // ONLY fuel/ammo/target, a completed Resupply() above (full tank + full magazine) makes
            // this return Patrol/Engage instead of ReturnToBase on the very same frame — that is how a
            // serviced drone rejoins the fight without any extra state machine.
            if (_policy != null)
                State = _policy.Decide(HasTarget, AmmoFraction > 0f, FuelFraction);

            // Gun and dispenser cooldowns always advance, no matter who is flying.
            if (_gun != null) _gun.Tick(dt);
            if (_cm != null) _cm.Tick(dt);

            // Missile warning: refresh MissileIncoming/TimeToImpact from the live munitions. Runs for
            // the human pilot too, so the HUD can warn them.
            ScanForMissiles();

            // Manual control: the player flies and shoots this drone, so skip ALL AI steering, AI
            // gunnery and AI evasion. Timers above already ticked; sensing still runs so the lock/HUD
            // stay live.
            if (ManualControl)
            {
                RunSensing(dt);
                return;
            }

            // Self-defence: one flare/chaff salvo per threat, released late enough to matter.
            if (MissileIncoming && _cm != null && !_flaredThisThreat && TimeToImpact < 3f && _cm.CanDeploy)
            {
                if (_cm.Deploy()) _flaredThisThreat = true;
            }

            // Navigation: advance waypoints and compute the DEFAULT patrol steering direction.
            _nav.Update(pos);
            Vector3 patrolDir = _nav.DesiredDirection(pos);
            if (patrolDir.sqrMagnitude <= 1e-6f)
            {
                // No waypoint (empty route or complete): keep flying straight ahead.
                patrolDir = _flight.Forward;
            }

            // Missile evasion is only FLOWN when the shot is close enough to matter: Choose returns
            // None for a warning further out than ~6s, and the drone then keeps its normal task.
            ManeuverType evasive = MissileIncoming
                ? EvasiveManeuver.Choose(pos.y, minAltitude, TimeToImpact)
                : ManeuverType.None;

            // Desired-direction selection, HIGHEST PRIORITY FIRST:
            //   1. ReturnToBase (bingo fuel/ammo) — getting home outranks everything else.
            //   2. Incoming missile — a named evasive maneuver (break/dive/climb) beats the assigned
            //      target: a dead drone cannot prosecute it.
            //   3. Recorded threat (SetThreat) — lateral jink away from a shooter.
            //   4. Assigned target — fly toward the allocated hostile.
            //   5. Patrol route (default).
            // The out-of-fuel behaviour sits ABOVE all of this: it overrides the altitude bias below
            // and can destroy the drone before the flight integration finishes.
            Vector3 dir = patrolDir;
            // Throttle actually flown this frame. Normally the governed throttle; reduced to a crawl
            // while dwelling at base so the drone can hold station inside the service radius.
            float flightThrottle = effThrottle;
            if (State == EngagementState.ReturnToBase)
            {
                Vector3 toBase = BasePosition - pos;
                if (_resupply != null && _resupply.IsServicing)
                {
                    // On station: keep circling the base slowly until the service completes. Falling
                    // back to the patrol route here would be fatal to the whole feature — the route's
                    // nearest leg lies OUTSIDE baseRadius, so the drone would leave the service area
                    // on every pass and ResupplyPoint would reset before the cycle ever finished.
                    dir = toBase;
                    flightThrottle = Mathf.Min(effThrottle, StationKeepThrottle);
                }
                else
                {
                    // Head home; within ~10m of base fall back to the patrol loop so it loiters, not
                    // stalls (defensive: only reachable when there is no ResupplyPoint at all).
                    dir = toBase.sqrMagnitude > 100f ? toBase : patrolDir;
                }
            }
            else if (evasive != ManeuverType.None)
            {
                // Defensive maneuver against the nearest incoming munition.
                Vector3 dirToMissile = _missilePos - pos;
                dir = EvasiveManeuver.Direction(evasive, _flight.Forward, dirToMissile, Vector3.up);
            }
            else if (IsThreatened)
            {
                // Evade the recorded threat with a lateral jink away from it.
                Vector3 dirToThreat = _threatPos - pos;
                dir = EvasionSteering.Evade(_flight.Forward, dirToThreat, Vector3.up);
            }
            else if (AssignedTargetId >= 0)
            {
                // Fly toward the allocated hostile (dynamic waypoint) to get into engagement range.
                Targetable assigned = TargetRegistry.FindById(AssignedTargetId);
                if (assigned != null)
                {
                    // Aim at a point directly above the target at our cruise altitude rather than at
                    // the target's ground position, so we approach horizontally instead of diving.
                    // The weapon range still reaches the ground target from above.
                    Vector3 targetPos = assigned.transform.position;
                    Vector3 aim = new Vector3(targetPos.x, _cruiseAltitude, targetPos.z);
                    Vector3 toTarget = aim - pos;
                    if (toTarget.sqrMagnitude > 1e-6f) dir = toTarget;
                }
            }

            if (IsOutOfFuel)
            {
                // Dead stick: with no power there is nothing to hold altitude with, so bias the nose
                // down into a glide instead of seeking the cruise altitude.
                dir.y -= 0.6f;
            }
            else
            {
                // Gentle altitude-hold bias: seek the cruise altitude so drones neither climb away nor
                // sink toward the ground while steering.
                dir.y += (_cruiseAltitude - pos.y) * 0.5f;
            }

            if (dir.sqrMagnitude <= 1e-6f) dir = _flight.Forward;

            // Flight integration on the GOVERNED throttle: an empty tank produces no thrust at all.
            // (flightThrottle == effThrottle except while holding station at base for servicing.)
            _flight.Step(dir, flightThrottle, dt);

            Vector3 newPos = _flight.Position;
            if (IsOutOfFuel)
            {
                // Unpowered sink, then a hard ground impact at the minimum altitude. The drone is
                // destroyed through its own Targetable so the explosion and the friendly-loss
                // accounting behave exactly like a shoot-down.
                newPos.y -= sinkRatePerSecond * dt;
                _flight.Position = newPos;
                transform.position = newPos;
                if (newPos.y <= minAltitude)
                {
                    if (_self != null)
                    {
                        // Latch BEFORE the damage call: TakeDamage destroys the GameObject, and Unity
                        // keeps this Update alive until the end of the frame. The latch makes every
                        // later entry a no-op instead of a null dereference.
                        Crashed = true;
                        _self.TakeDamage(99999f);
                        return;
                    }

                    // Defensive: no Targetable to destroy this drone through, so just rest on the deck.
                    newPos.y = minAltitude;
                    _flight.Position = newPos;
                    transform.position = newPos;
                }
            }
            else
            {
                // Hard floor: the flight model has no ground collision, so clamp the integrated
                // position up to the minimum altitude and write it back so _flight.Position and
                // transform.position stay in sync (no divergence between the model and the scene).
                if (newPos.y < minAltitude)
                {
                    newPos.y = minAltitude;
                    _flight.Position = newPos;
                }
                transform.position = newPos;
            }

            if (_flight.Forward.sqrMagnitude > 1e-6f)
            {
                Quaternion targetRot = Quaternion.LookRotation(_flight.Forward, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, 10f * dt);
            }

            // Sensing: detect nearest hostile and advance the lock.
            RunSensing(dt);

            // AI gunnery: strafe the detected hostile with the defensive gun. GunTurret itself checks
            // range/ammo/cooldown, so this is a cheap best-effort call. Suppressed while heading home.
            if (_gun != null && HasTarget && State != EngagementState.ReturnToBase)
            {
                Targetable gunTarget = TargetRegistry.FindById(DetectedId);
                if (gunTarget != null) _gun.TryFireAt(gunTarget);
            }
        }

        /// <summary>
        /// Completes one servicing cycle: refuels the tank and rearms every weapon this drone carries.
        /// Called by <see cref="Update"/> when <see cref="ResupplyPoint.Tick"/> reports the dwell is
        /// done. All references are optional, so every step is null-checked.
        /// <para>
        /// Overridden by <see cref="SihaController"/> to also reload the missile launcher.
        /// </para>
        /// </summary>
        public virtual void Resupply()
        {
            // Fuel: a full tank lifts the drone back above EngagementPolicy's bingo threshold.
            if (_fuel != null) _fuel.Refuel();

            // Gun belt, when this drone carries a turret (GunTurret.Reload -> GunSystem.Reload).
            if (_gun != null) _gun.Reload();

            // Flare/chaff charges, when this drone carries a dispenser.
            if (_cm != null) _cm.Reload();
        }

        /// <summary>
        /// Refreshes the incoming-missile picture from <see cref="GuidedMunition.Active"/>: picks the
        /// nearest munition homing on this drone and derives <see cref="TimeToImpact"/> from the range
        /// and closing velocity (see <see cref="Sim.Core.MissileThreat"/>). Clears the per-threat flare
        /// latch when the sky is clear again.
        /// </summary>
        private void ScanForMissiles()
        {
            MissileIncoming = false;
            TimeToImpact = float.PositiveInfinity;

            if (_self == null)
            {
                _flaredThisThreat = false;
                return;
            }

            Vector3 pos = transform.position;
            GuidedMunition nearest = null;
            float bestRangeSq = float.MaxValue;

            // Drop munitions that have already detonated/expired before reading anything off them.
            GuidedMunition.Prune();

            List<GuidedMunition> active = GuidedMunition.Active;
            for (int i = 0; i < active.Count; i++)
            {
                GuidedMunition m = active[i];
                // A destroyed munition compares equal to null but member access still throws.
                if (m == null) continue;
                if (m.Target != _self) continue;

                float d2 = (m.transform.position - pos).sqrMagnitude;
                if (d2 < bestRangeSq)
                {
                    bestRangeSq = d2;
                    nearest = m;
                }
            }

            if (nearest == null)
            {
                // Threat gone: rearm the one-salvo-per-threat latch.
                _flaredThisThreat = false;
                return;
            }

            _missilePos = nearest.transform.position;

            // Range and closing velocity along the missile->drone line of sight (own velocity
            // abstracted to zero, matching the munition's own guidance convention).
            Vector3 los = pos - _missilePos;
            float closing = ProportionalNavigation.ClosingVelocity(los, -nearest.Velocity);
            TimeToImpact = MissileThreat.TimeToImpact(los.magnitude, closing);
            MissileIncoming = true;
        }

        /// <summary>Runs detection against hostile targets and advances the lock timer.</summary>
        protected void RunSensing(float dt)
        {
            // Sensing needs the pure-logic cores; bail out rather than dereference null if this is
            // reached before they exist.
            EnsureInitialized();
            if (_targeting == null || _flight == null) return;

            List<DetectableTarget> snapshot = TargetRegistry.GetSnapshot(hostileFaction);
            bool found = _targeting.TryDetect(transform.position, _flight.Forward, snapshot, out DetectableTarget best);

            HasTarget = found;
            DetectedId = found ? best.Id : -1;
            _targeting.UpdateLock(found, DetectedId, dt);

            if (found)
            {
                Debug.DrawLine(transform.position, best.Position,
                    _targeting.IsLocked ? Color.red : Color.yellow);
            }
        }
    }
}
