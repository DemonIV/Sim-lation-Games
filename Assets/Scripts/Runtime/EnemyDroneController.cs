using System.Collections.Generic;
using UnityEngine;
using Sim.Core;

namespace Sim.Runtime
{
    /// <summary>
    /// A HOSTILE fighter drone: it flies, hunts friendly drones with a <see cref="TargetingSystem"/>
    /// and strafes them with a <see cref="GunTurret"/>. With no contact it orbits the field centre so
    /// it never wanders off or stalls.
    ///
    /// Deliberately self-contained (it does NOT derive from <see cref="IhaController"/>, which owns
    /// friendly patrol/fuel/engagement logic), but it mirrors that controller's proven flight pattern:
    /// a pure-logic <see cref="FlightModel"/>, an altitude-hold bias, a hard minimum-altitude floor
    /// written back into both the model and the transform, and a Slerped orientation.
    ///
    /// This is a GAME / EDUCATIONAL model with abstract, gamified parameters.
    /// </summary>
    public class EnemyDroneController : MonoBehaviour
    {
        [Header("Flight")]
        [SerializeField] private float maxSpeed = 34f;
        [SerializeField] private float maxAccel = 9f;
        [SerializeField] private float maxTurnRateDeg = 85f;
        [Range(0f, 1f)]
        [SerializeField] private float throttle = 1f;

        [Header("Sensors")]
        [SerializeField] private float detectionRange = 130f;
        [SerializeField] private float fovDeg = 100f;
        // Faction this fighter hunts. 0 = friendly drones.
        [SerializeField] private int targetFaction = 0;

        [Header("Altitude")]
        // Held level with the player's SİHA (Sim.Core.AircraftCatalog) so dogfights stay co-altitude,
        // and clear of the 14 m scenery ceiling by the Sim.Core.FlightEnvelope margin. Raised from
        // 14 m with the rest of the cruise band — buildings reach into the old height.
        [SerializeField] private float cruiseAltitude = 20f;
        // Ground floor, NOT a structure-clearance floor: this is the deck the flight model is clamped
        // to because there is no ground collision. A deliberate dive is still allowed below the
        // scenery ceiling; only the cruise altitude is guaranteed to clear it.
        [SerializeField] private float minAltitude = 5f;

        [Header("Engagement")]
        // Radius of the loiter orbit flown around the field centre while searching.
        [SerializeField] private float standoff = 25f;

        // Pure-logic cores.
        private FlightModel _flight;
        private TargetingSystem _targeting;

        // Optional gun (added by the spawner). May be null.
        private GunTurret _gun;

        // Optional flare/chaff dispenser and this fighter's own Targetable (used to recognise the
        // munitions that are homing on IT). Both may be null.
        private CountermeasureDispenser _cm;
        private Targetable _self;

        // One salvo per threat: latched while a munition is inbound, cleared when the sky is clear.
        private bool _flaredThisThreat;

        // Reusable detection buffer, refilled every frame instead of allocating a new snapshot list.
        private readonly List<DetectableTarget> _scanBuffer = new List<DetectableTarget>();

        // Slowly advancing bearing used for the search orbit, in radians.
        private float _wanderAngle;

        // Per-instance loiter offsets set by the spawner (see SetLoiterOffsets). Purely a placement
        // spread: the configured standoff radius and cruise altitude themselves are untouched.
        private float _loiterRadiusOffset;
        private float _loiterPhaseOffset;

        /// <summary>
        /// Fans this fighter's search orbit out from its wave mates: <paramref name="radiusOffset"/>
        /// metres are added to the loiter radius and <paramref name="phaseOffsetRad"/> radians to its
        /// bearing on that orbit. Spacing only — no flight, sensor or weapon value changes.
        /// </summary>
        public void SetLoiterOffsets(float radiusOffset, float phaseOffsetRad)
        {
            _loiterRadiusOffset = radiusOffset;
            _loiterPhaseOffset = phaseOffsetRad;
        }

        /// <summary>True when a friendly drone is currently detected in range and FOV.</summary>
        public bool HasTarget { get; private set; }

        /// <summary>Stable id of the detected friendly, or -1 when none.</summary>
        public int DetectedId { get; private set; } = -1;

        // True once the pure-logic cores below have been built (see EnsureInitialized).
        private bool _initialized;

        private void Start()
        {
            EnsureInitialized();
        }

        /// <summary>
        /// Builds the pure-logic cores and resolves the optional sibling components, exactly once.
        /// They are plain C# objects / non-serialized references, so they are null whenever Start has
        /// not run for this component while Update is already ticking; building them lazily keeps the
        /// fighter flying instead of dereferencing null (or freezing forever behind a guard).
        /// </summary>
        private void EnsureInitialized()
        {
            if (_initialized) return;
            _initialized = true;

            _flight = new FlightModel(transform.position, transform.forward)
            {
                MaxSpeed = maxSpeed,
                MaxAcceleration = maxAccel,
                MaxTurnRateDeg = maxTurnRateDeg
            };

            _targeting = new TargetingSystem
            {
                DetectionRange = detectionRange,
                FieldOfViewDeg = fovDeg
            };

            _gun = GetComponent<GunTurret>();
            _cm = GetComponent<CountermeasureDispenser>();
            _self = GetComponent<Targetable>();

            // Seed the search orbit from the spawn bearing so fighters spread out instead of stacking.
            Vector3 pos = transform.position;
            _wanderAngle = Mathf.Atan2(pos.z, pos.x);
        }

        private void Update()
        {
            EnsureInitialized();
            if (_flight == null || _targeting == null) return;

            float dt = Time.deltaTime;
            Vector3 pos = transform.position;

            if (_gun != null) _gun.Tick(dt);
            if (_cm != null) _cm.Tick(dt);

            // Sensing: nearest friendly drone within range and FOV (reused buffer, no per-frame alloc).
            TargetRegistry.GetSnapshot(targetFaction, _scanBuffer);
            bool found = _targeting.TryDetect(pos, _flight.Forward, _scanBuffer, out DetectableTarget best);
            HasTarget = found;
            DetectedId = found ? best.Id : -1;
            _targeting.UpdateLock(found, DetectedId, dt);

            Vector3 dir;
            if (found)
            {
                // Chase a point above the contact at our own cruise altitude, so the fighter closes
                // horizontally rather than diving through the ground (same trick as IhaController).
                Vector3 aim = new Vector3(best.Position.x, cruiseAltitude, best.Position.z);
                Vector3 toTarget = aim - pos;
                dir = toTarget.sqrMagnitude > 1e-6f ? toTarget : _flight.Forward;
            }
            else
            {
                // Search: orbit the field centre on a slowly advancing bearing.
                _wanderAngle += 0.35f * dt;
                float radius = Mathf.Max(1f, standoff + _loiterRadiusOffset);
                float bearing = _wanderAngle + _loiterPhaseOffset;
                var loiter = new Vector3(Mathf.Cos(bearing) * radius, cruiseAltitude,
                                         Mathf.Sin(bearing) * radius);
                Vector3 toLoiter = loiter - pos;
                dir = toLoiter.sqrMagnitude > 1e-6f ? toLoiter : _flight.Forward;
            }

            // Self-defence outranks the chase: when a munition is about to arrive, flare once and fly
            // a named evasive maneuver instead of pressing the attack.
            GuidedMunition threat = NearestIncomingMissile(pos, out float timeToImpact);
            if (threat == null)
            {
                _flaredThisThreat = false;
            }
            else if (timeToImpact < 3f)
            {
                if (_cm != null && !_flaredThisThreat && _cm.CanDeploy && _cm.Deploy())
                    _flaredThisThreat = true;

                ManeuverType maneuver = EvasiveManeuver.Choose(pos.y, minAltitude, timeToImpact);
                dir = EvasiveManeuver.Direction(maneuver, _flight.Forward,
                                                threat.transform.position - pos, Vector3.up);
            }

            // Gentle altitude-hold bias toward the cruise altitude.
            dir.y += (cruiseAltitude - pos.y) * 0.5f;
            if (dir.sqrMagnitude <= 1e-6f) dir = _flight.Forward;

            // Flight integration.
            _flight.Step(dir, throttle, dt);

            // Hard floor: the flight model has no ground collision, so clamp up to the minimum altitude
            // and write it back so _flight.Position and transform.position stay in sync.
            Vector3 newPos = _flight.Position;
            if (newPos.y < minAltitude)
            {
                newPos.y = minAltitude;
                _flight.Position = newPos;
            }
            transform.position = newPos;

            if (_flight.Forward.sqrMagnitude > 1e-6f)
            {
                Quaternion targetRot = Quaternion.LookRotation(_flight.Forward, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, 10f * dt);
            }

            // Gunnery: GunTurret checks range/ammo/cooldown itself, so this is a best-effort call.
            if (found && _gun != null)
            {
                Targetable victim = TargetRegistry.FindById(DetectedId);
                if (victim != null) _gun.TryFireAt(victim);
            }
        }

        /// <summary>
        /// Nearest live <see cref="GuidedMunition"/> homing on THIS fighter, with its time to impact
        /// (see <see cref="MissileThreat"/>). Returns null (and PositiveInfinity) when nothing is
        /// inbound or when this fighter carries no <see cref="Targetable"/>.
        /// </summary>
        private GuidedMunition NearestIncomingMissile(Vector3 pos, out float timeToImpact)
        {
            timeToImpact = float.PositiveInfinity;
            if (_self == null) return null;

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

            if (nearest == null) return null;

            // Own velocity abstracted to zero, matching the munition's own guidance convention.
            Vector3 los = pos - nearest.transform.position;
            float closing = ProportionalNavigation.ClosingVelocity(los, -nearest.Velocity);
            timeToImpact = MissileThreat.TimeToImpact(los.magnitude, closing);
            return nearest;
        }
    }
}
