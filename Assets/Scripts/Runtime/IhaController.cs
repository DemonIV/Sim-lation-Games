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

        // Pure-logic cores.
        protected FlightModel _flight;
        protected WaypointNavigator _nav;
        protected TargetingSystem _targeting;

        // Tactical cores (endurance + engagement decision).
        protected FuelTank _fuel;
        protected EngagementPolicy _policy;

        // Threat memory for evasion: a recorded threat position and the time it expires.
        private Vector3 _threatPos;
        private float _threatExpiry;

        /// <summary>True when a hostile target is currently detected within range and FOV.</summary>
        public bool HasTarget { get; private set; }

        /// <summary>Instance id of the currently detected target, or -1 when none.</summary>
        public int DetectedId { get; private set; } = -1;

        /// <summary>Read-only access to the targeting/lock state for companion systems (e.g. a SİHA weapon).</summary>
        public TargetingSystem Targeting => _targeting;

        /// <summary>Remaining fuel as a 0..1 fraction (1 when no tank exists yet).</summary>
        public float FuelFraction => _fuel != null ? _fuel.Fraction : 1f;

        /// <summary>Remaining ammunition as a 0..1 fraction. Recon İHA has no weapon, so always 1.</summary>
        public virtual float AmmoFraction => 1f;

        /// <summary>Current high-level engagement state, driven by <see cref="EngagementPolicy"/> each frame.</summary>
        public EngagementState State { get; protected set; } = EngagementState.Patrol;

        /// <summary>Externally assigned hostile <see cref="Targetable"/> id to head toward, or -1 for none.</summary>
        public int AssignedTargetId { get; set; } = -1;

        /// <summary>Home/base position (spawn point) used when the drone returns to base.</summary>
        public Vector3 BasePosition { get; set; }

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

            // Remember the spawn point so ReturnToBase can steer home.
            BasePosition = transform.position;
        }

        protected virtual void Update()
        {
            float dt = Time.deltaTime;
            Vector3 pos = transform.position;

            // Endurance: burn fuel proportional to the current throttle.
            if (_fuel != null) _fuel.Consume(throttle, dt);

            // Engagement decision from the current fuel/ammo/target situation.
            if (_policy != null)
                State = _policy.Decide(HasTarget, AmmoFraction > 0f, FuelFraction);

            // Navigation: advance waypoints and compute the DEFAULT patrol steering direction.
            _nav.Update(pos);
            Vector3 patrolDir = _nav.DesiredDirection(pos);
            if (patrolDir.sqrMagnitude <= 1e-6f)
            {
                // No waypoint (empty route or complete): keep flying straight ahead.
                patrolDir = _flight.Forward;
            }

            // Desired-direction selection, highest priority first. The flight-integration and
            // orientation code below is unchanged; only this chosen direction differs.
            Vector3 dir = patrolDir;
            if (State == EngagementState.ReturnToBase)
            {
                // Head home; within ~10m of base fall back to the patrol loop so it loiters, not stalls.
                Vector3 toBase = BasePosition - pos;
                dir = toBase.sqrMagnitude > 100f ? toBase : patrolDir;
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
                    Vector3 toTarget = assigned.transform.position - pos;
                    if (toTarget.sqrMagnitude > 1e-6f) dir = toTarget;
                }
            }

            if (dir.sqrMagnitude <= 1e-6f) dir = _flight.Forward;

            // Flight integration.
            _flight.Step(dir, throttle, dt);
            transform.position = _flight.Position;
            if (_flight.Forward.sqrMagnitude > 1e-6f)
            {
                Quaternion targetRot = Quaternion.LookRotation(_flight.Forward, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, 10f * dt);
            }

            // Sensing: detect nearest hostile and advance the lock.
            RunSensing(dt);
        }

        /// <summary>Runs detection against hostile targets and advances the lock timer.</summary>
        protected void RunSensing(float dt)
        {
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
