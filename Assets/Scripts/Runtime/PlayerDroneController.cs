using System.Collections.Generic;
using UnityEngine;
using Sim.Core;

namespace Sim.Runtime
{
    /// <summary>
    /// Lets the player take over one friendly drone and fly it by hand ("pilot mode").
    ///
    /// Attach to a MANAGER GameObject (the SimulationDirector object), never to a drone itself: it
    /// picks a drone from the scene, raises <see cref="IhaController.ManualControl"/> on it (which
    /// suspends that drone's AI steering and AI gunnery) and then drives its transform directly,
    /// mirroring every step back into the drone's pure-logic <see cref="FlightModel"/> through
    /// <see cref="IhaController.SyncFlightTo(Vector3, Vector3, float)"/>. When control is released the
    /// AI therefore resumes cleanly from the position, heading and speed the pilot left it at.
    ///
    /// Controls: <c>C</c> take/release control · <c>Tab</c> pick drone · <c>W</c>/<c>S</c> throttle ·
    /// <c>A</c>/<c>D</c> yaw · <c>↑</c>/<c>↓</c> (or Left-Alt + mouse Y) pitch · <c>Space</c> guns ·
    /// <c>F</c> guided munition (SİHA only).
    ///
    /// This is a GAME / EDUCATIONAL flight feel with abstract, gamified parameters.
    /// </summary>
    public class PlayerDroneController : MonoBehaviour
    {
        [Header("Handling")]
        [SerializeField] private float pitchRateDeg = 45f;
        [SerializeField] private float yawRateDeg = 60f;
        // Throttle change per second, as a fraction of the pilot's maximum speed.
        [SerializeField] private float throttleStep = 0.6f;

        [Header("Weapons")]
        // Advisory gun range shown to the pilot; the GunTurret itself owns the authoritative range.
        [SerializeField] private float gunRange = 60f;
        [SerializeField] private int enemyFaction = 1;
        // How far ahead of the nose the free-aim gun point is placed.
        [SerializeField] private float aimDistance = 80f;

        /// <summary>Top speed the pilot can command, matching the drones' own flight envelope.</summary>
        private const float MaxSpeed = 40f;

        /// <summary>Hard altitude floor, mirroring <see cref="IhaController"/>'s own minimum.</summary>
        private const float MinAltitude = 5f;

        /// <summary>Pitch authority limit, so the player cannot flip the drone over.</summary>
        private const float MaxPitchDeg = 60f;

        /// <summary>Unpowered sink rate once the tank runs dry, mirroring <see cref="IhaController"/>.</summary>
        private const float DeadStickSinkRate = 6f;

        /// <summary>The drone currently being flown by the player, or null.</summary>
        public IhaController Controlled { get; private set; }

        /// <summary>True while the player is flying a live drone.</summary>
        public bool IsActive => Controlled != null;

        /// <summary>Commanded airspeed (m/s), for the HUD pilot panel.</summary>
        public float Speed => _speed;

        /// <summary>Advisory gun range for the HUD.</summary>
        public float GunRange => gunRange;

        // The drone Tab has highlighted; C takes control of this one.
        private IhaController _selected;
        private int _selectionIndex = -1;

        // Pilot flight state.
        private float _speed;
        private float _yaw;
        private float _pitch;

        private void Update()
        {
            // Drop a stale reference if the piloted drone was destroyed under us.
            if (Controlled == null) Controlled = null;
            if (_selected == null) _selected = null;

            if (Input.GetKeyDown(KeyCode.Tab)) CycleSelection();
            if (Input.GetKeyDown(KeyCode.C)) ToggleControl();

            if (Controlled == null) return;

            FlyControlled(Time.deltaTime);

            // FlyControlled can crash (and release) the drone on a dead-stick ground impact.
            if (Controlled == null) return;
            HandleWeapons();
        }

        private void OnDisable()
        {
            ReleaseControl();
        }

        /// <summary>Takes control of the selected drone, or hands the current one back to its AI.</summary>
        private void ToggleControl()
        {
            if (IsActive)
            {
                ReleaseControl();
                return;
            }

            if (_selected == null) CycleSelection();
            TakeControl(_selected);
        }

        /// <summary>
        /// Advances the highlighted drone through every friendly <see cref="IhaController"/> in the
        /// scene. While flying, control is transferred straight to the newly picked drone.
        /// </summary>
        private void CycleSelection()
        {
            List<IhaController> drones = FriendlyDrones();
            if (drones.Count == 0)
            {
                ReleaseControl();
                _selected = null;
                _selectionIndex = -1;
                return;
            }

            _selectionIndex = (_selectionIndex + 1) % drones.Count;
            IhaController next = drones[_selectionIndex];

            bool wasFlying = IsActive;
            if (wasFlying) ReleaseControl();
            _selected = next;
            if (wasFlying) TakeControl(next);
        }

        /// <summary>All live friendly (Faction 0) drones in the scene, in a stable-enough order.</summary>
        private List<IhaController> FriendlyDrones()
        {
            var result = new List<IhaController>();
            IhaController[] all = FindObjectsByType<IhaController>(FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
            {
                IhaController c = all[i];
                if (c == null) continue;
                // Friendly-only when the drone carries a Targetable; unmarked drones are allowed too.
                var targetable = c.GetComponent<Targetable>();
                if (targetable != null && targetable.Faction != 0) continue;
                result.Add(c);
            }
            return result;
        }

        /// <summary>Suspends the drone's AI and seeds the pilot state from its current flight.</summary>
        private void TakeControl(IhaController drone)
        {
            if (drone == null) return;

            Controlled = drone;
            drone.ManualControl = true;

            Vector3 euler = drone.transform.rotation.eulerAngles;
            _yaw = euler.y;
            _pitch = Mathf.Clamp(NormalizeAngle(euler.x), -MaxPitchDeg, MaxPitchDeg);
            _speed = Mathf.Clamp(drone.Speed, 0f, MaxSpeed);
        }

        /// <summary>
        /// Hands the drone back to its AI, syncing the flight model to where the pilot left it so the
        /// AI does not snap back to a stale position/heading. Safe to call when nothing is controlled.
        /// </summary>
        private void ReleaseControl()
        {
            IhaController drone = Controlled;
            Controlled = null;
            if (drone == null) return;

            drone.ManualControl = false;
            Transform t = drone.transform;
            drone.SyncFlightTo(t.position, t.forward, _speed);
        }

        /// <summary>Reads the flight inputs and drives the controlled drone for one frame.</summary>
        private void FlyControlled(float dt)
        {
            if (dt <= 0f) return;
            if (Controlled == null) return;

            // Throttle.
            if (Input.GetKey(KeyCode.W)) _speed += throttleStep * MaxSpeed * dt;
            if (Input.GetKey(KeyCode.S)) _speed -= throttleStep * MaxSpeed * dt;
            _speed = Mathf.Clamp(_speed, 0f, MaxSpeed);

            // Endurance: the pilot burns the SAME tank the AI would (IhaController skips its own burn
            // while ManualControl is on, so this is the single source of consumption).
            float commanded = MaxSpeed > 0f ? _speed / MaxSpeed : 0f;
            Controlled.ConsumeFuel(commanded, dt);

            // A dry tank means no power: the governor caps the achievable speed, tapering it through
            // the last of the reserve and pinning it at zero when the tank is empty.
            float maxNow = MaxSpeed * ThrottleGovernor.Effective(1f, Controlled.FuelFraction);
            if (_speed > maxNow) _speed = maxNow;

            // Yaw.
            if (Input.GetKey(KeyCode.D)) _yaw += yawRateDeg * dt;
            if (Input.GetKey(KeyCode.A)) _yaw -= yawRateDeg * dt;

            // Pitch: arrows, plus mouse Y as a fine trim while Left-Alt is held. Negative pitch is
            // nose-up in Unity's Euler convention.
            if (Input.GetKey(KeyCode.UpArrow)) _pitch -= pitchRateDeg * dt;
            if (Input.GetKey(KeyCode.DownArrow)) _pitch += pitchRateDeg * dt;
            if (Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt))
                _pitch -= Input.GetAxis("Mouse Y") * pitchRateDeg * dt;
            _pitch = Mathf.Clamp(_pitch, -MaxPitchDeg, MaxPitchDeg);

            Quaternion rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            Vector3 forward = rotation * Vector3.forward;
            if (forward.sqrMagnitude <= 1e-6f) forward = Controlled.transform.forward;

            Vector3 pos = Controlled.transform.position + forward * (_speed * dt);

            if (Controlled.IsOutOfFuel)
            {
                // Dead stick: the drone's own sink/crash logic is suspended under manual control, so
                // the pilot glides down and hits the deck here instead.
                pos.y -= DeadStickSinkRate * dt;
                if (pos.y <= MinAltitude)
                {
                    Targetable wreck = Controlled.GetComponent<Targetable>();
                    Controlled.transform.position = pos;
                    // Hand the drone back BEFORE destroying it, so no stale reference survives.
                    ReleaseControl();
                    if (wreck != null) wreck.TakeDamage(99999f);
                    return;
                }
            }
            else if (pos.y < MinAltitude)
            {
                pos.y = MinAltitude;
            }

            // Drive the transform AND the drone's pure-logic flight model together.
            Controlled.SyncFlightTo(pos, forward, _speed);
        }

        /// <summary>Space fires the gun down the nose; F launches a guided munition from a SİHA.</summary>
        private void HandleWeapons()
        {
            if (Controlled == null) return;

            if (Input.GetKey(KeyCode.Space))
            {
                GunTurret gun = Controlled.Gun;
                if (gun != null)
                {
                    Transform t = Controlled.transform;
                    Vector3 aimPoint = t.position + t.forward * Mathf.Max(1f, aimDistance);
                    gun.TryFireAtPoint(aimPoint, enemyFaction);
                }
            }

            if (Input.GetKeyDown(KeyCode.F))
            {
                var siha = Controlled as SihaController;
                if (siha != null) siha.TryManualLaunch();
            }
        }

        /// <summary>Maps a 0..360 Euler angle into a signed -180..180 range for clamping.</summary>
        private static float NormalizeAngle(float angle)
        {
            angle %= 360f;
            if (angle > 180f) angle -= 360f;
            return angle;
        }
    }
}
