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

        // Pure-logic cores.
        protected FlightModel _flight;
        protected WaypointNavigator _nav;
        protected TargetingSystem _targeting;

        /// <summary>True when a hostile target is currently detected within range and FOV.</summary>
        public bool HasTarget { get; private set; }

        /// <summary>Instance id of the currently detected target, or -1 when none.</summary>
        public int DetectedId { get; private set; } = -1;

        /// <summary>Read-only access to the targeting/lock state for companion systems (e.g. a SİHA weapon).</summary>
        public TargetingSystem Targeting => _targeting;

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
        }

        protected virtual void Update()
        {
            float dt = Time.deltaTime;
            Vector3 pos = transform.position;

            // Navigation: advance waypoints and pick a steering direction.
            _nav.Update(pos);
            Vector3 dir = _nav.DesiredDirection(pos);
            if (dir.sqrMagnitude <= 1e-6f)
            {
                // No waypoint (empty route or complete): keep flying straight ahead.
                dir = _flight.Forward;
            }

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
