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
    /// <c>F</c> guided munition (SİHA only) · <c>Q</c> flares/chaff · <c>E</c> (held) afterburner ·
    /// <c>X</c> evasive break turn (an over-g burst onto the beam, on a cooldown) ·
    /// <c>K</c> karıştırıcı (one jamming burst, only on an aircraft that bought the emitter).
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

        /// <summary>
        /// Fallback top speed the pilot can command when no drone is being flown (or the airframe
        /// reports none). This is the constant the pilot used before aircraft profiles existed.
        /// </summary>
        private const float DefaultMaxSpeed = 40f;

        /// <summary>
        /// Top speed the pilot can command on the CURRENT airframe. Each drone carries its own cap
        /// (<see cref="IhaController.PilotMaxSpeed"/>), which the selected
        /// <see cref="Sim.Core.AircraftProfile"/> sets on the player's aircraft; AI-spawned drones keep
        /// the historical <see cref="DefaultMaxSpeed"/>.
        /// </summary>
        private float MaxSpeed
        {
            get
            {
                if (Controlled == null) return DefaultMaxSpeed;
                float cap = Controlled.PilotMaxSpeed;
                return cap > 0f ? cap : DefaultMaxSpeed;
            }
        }

        /// <summary>Hard altitude floor, mirroring <see cref="IhaController"/>'s own minimum.</summary>
        private const float MinAltitude = 5f;

        /// <summary>Pitch authority limit, so the player cannot flip the drone over.</summary>
        private const float MaxPitchDeg = 60f;

        /// <summary>Unpowered sink rate once the tank runs dry, mirroring <see cref="IhaController"/>.</summary>
        private const float DeadStickSinkRate = 6f;

        /// <summary>Top-speed multiplier while the afterburner (E) is held.</summary>
        private const float AfterburnerSpeedMultiplier = 1.6f;

        /// <summary>Fuel-burn multiplier while the afterburner is held.</summary>
        private const float AfterburnerFuelMultiplier = 3f;

        /// <summary>
        /// Fuel-burn multiplier while the noise jammer is radiating — THE downside of the ability.
        /// A high-power emitter is not free: it is fed off the same tank the engine is, exactly the
        /// way the afterburner is (just far more gently, 1.5× against the afterburner's 3×). It costs
        /// nothing at all when the jammer is idle or not fitted, so a player who never buys the track
        /// flies an unchanged sortie.
        /// </summary>
        private const float JammerFuelMultiplier = 1.5f;

        /// <summary>
        /// Duration of the one-shot evasive break turn (X), in seconds. Covers
        /// <see cref="Sim.Core.EvasiveManeuver.BreakWindowSeconds"/>, so a break released as the cue
        /// lights up is still being flown when the missile arrives — letting go halfway through would
        /// hand the guidance law a straight, non-manoeuvring target again.
        /// </summary>
        private const float EvadeDuration = 2f;

        /// <summary>
        /// Lock-out after a break turn, in seconds. Makes the manoeuvre an ABILITY with a cost rather
        /// than a key to hold down: a SAM site can put a second round in the air inside this window,
        /// so a break has to be spent on the shot that matters.
        /// </summary>
        private const float EvadeCooldownSeconds = 6f;

        /// <summary>
        /// Turn-rate multiplier while the break turn is flown — the "over-g" burst. The aircraft swings
        /// onto the beam heading roughly three times faster than the pilot could yaw it by hand, which
        /// is what generates the line-of-sight rate a guided round has to chase. It is deliberately a
        /// fast but FINITE turn: the old implementation snapped the heading instantly, which read on
        /// screen as nothing happening at all.
        /// </summary>
        private const float EvadeTurnRateMultiplier = 3f;

        /// <summary>The drone currently being flown by the player, or null.</summary>
        public IhaController Controlled { get; private set; }

        /// <summary>True while the player is flying a live drone.</summary>
        public bool IsActive => Controlled != null;

        /// <summary>Commanded airspeed (m/s), for the HUD pilot panel.</summary>
        public float Speed => _speed;

        /// <summary>Advisory gun range for the HUD.</summary>
        public float GunRange => gunRange;

        /// <summary>True while the afterburner (E) is held on a drone that still has fuel.</summary>
        public bool AfterburnerActive { get; private set; }

        /// <summary>True while the one-shot evasive maneuver (X) is flying the drone instead of the pilot.</summary>
        public bool EvadeActive { get; private set; }

        /// <summary>
        /// True while the noise jammer (K) is radiating on the piloted drone. Drives the extra fuel
        /// burn below; the HUD reads the emitter's own state off the airframe instead.
        /// </summary>
        public bool JammerActive { get; private set; }

        /// <summary>True when the break turn can be triggered right now (off cooldown).</summary>
        public bool EvadeReady => _evadeCooldown <= 0f;

        /// <summary>Seconds left on the break-turn cooldown, 0 when it is available.</summary>
        public float EvadeCooldownRemaining => _evadeCooldown;

        /// <summary>Cooldown progress as a 0..1 fraction: 1 = fully recharged, 0 = just spent.</summary>
        public float EvadeReadyFraction =>
            EvadeCooldownSeconds > 0f ? 1f - Mathf.Clamp01(_evadeCooldown / EvadeCooldownSeconds) : 1f;

        /// <summary>
        /// True while the inbound shot is inside <see cref="Sim.Core.EvasiveManeuver.BreakWindowSeconds"/>
        /// — the window in which breaking actually defeats it. Drives the HUD cue.
        /// </summary>
        public bool BreakWindowOpen => EvasiveManeuver.InBreakWindow(TimeToImpact);

        /// <summary>True when a munition is homing on the piloted drone. False when nobody is piloting.</summary>
        public bool MissileIncoming => Controlled != null && Controlled.MissileIncoming;

        /// <summary>Seconds until the incoming munition arrives, or PositiveInfinity when there is none.</summary>
        public float TimeToImpact => Controlled != null ? Controlled.TimeToImpact : float.PositiveInfinity;

        // The drone Tab has highlighted; C takes control of this one.
        private IhaController _selected;
        private int _selectionIndex = -1;

        /// <summary>
        /// Puts the player's chosen aircraft (the one <see cref="SimulationBootstrap"/> built from the
        /// selected <see cref="Sim.Core.AircraftProfile"/>) at the front of the queue, so pressing
        /// <c>C</c> without touching Tab first takes over exactly the aircraft picked in the menu.
        /// Tab still cycles through the whole squad from there. Null / destroyed drones are ignored.
        /// </summary>
        public void SetPreferredAircraft(IhaController drone)
        {
            if (drone == null) return;
            if (_selected == null) _selected = drone;
        }

        // Pilot flight state.
        private float _speed;
        private float _yaw;
        private float _pitch;

        // Remaining seconds of the one-shot evasive maneuver (X), and of its lock-out afterwards.
        private float _evadeTimer;
        private float _evadeCooldown;

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

        /// <summary>
        /// Hands any piloted drone back to its AI. Public entry point for the restart path
        /// (<see cref="SimulationBootstrap.Rebuild"/>), which tears the drone down right afterwards.
        /// Safe to call when nothing is being piloted.
        /// </summary>
        public void ReleasePlayerControl()
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

            // Resume cycling from whatever is highlighted right now (it may have been set from outside
            // by SetPreferredAircraft), so the first Tab steps AWAY from it instead of jumping to the
            // top of an unordered list.
            if (_selected != null)
            {
                for (int i = 0; i < drones.Count; i++)
                {
                    if (!ReferenceEquals(drones[i], _selected)) continue;
                    _selectionIndex = i;
                    break;
                }
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

            // Abilities are per-sortie: drop them with the drone.
            AfterburnerActive = false;
            EvadeActive = false;
            JammerActive = false;
            _evadeTimer = 0f;
            _evadeCooldown = 0f;

            drone.ManualControl = false;
            // The pilot is the only writer of Breaking while flying manually; clear it on the way out
            // so the AI does not inherit a stale break state.
            drone.Breaking = false;
            Transform t = drone.transform;
            drone.SyncFlightTo(t.position, t.forward, _speed);
        }

        /// <summary>Reads the flight inputs and drives the controlled drone for one frame.</summary>
        private void FlyControlled(float dt)
        {
            if (dt <= 0f) return;
            if (Controlled == null) return;

            // Special abilities: Q flares, E afterburner, X evasive maneuver, K jamming burst.
            HandleAbilities(dt);

            // Throttle. The afterburner raises the achievable top speed while it is held, and pushes
            // the drone toward that higher speed on its own.
            float boost = AfterburnerActive ? AfterburnerSpeedMultiplier : 1f;
            if (Input.GetKey(KeyCode.W)) _speed += throttleStep * MaxSpeed * dt;
            if (Input.GetKey(KeyCode.S)) _speed -= throttleStep * MaxSpeed * dt;
            if (AfterburnerActive) _speed += throttleStep * MaxSpeed * dt;
            _speed = Mathf.Clamp(_speed, 0f, MaxSpeed * boost);

            // Endurance: the pilot burns the SAME tank the AI would (IhaController skips its own burn
            // while ManualControl is on, so this is the single source of consumption). FuelTank clamps
            // throttle to 1, so the afterburner's heavier burn is applied to the time step instead.
            float commanded = MaxSpeed > 0f ? _speed / MaxSpeed : 0f;
            float burnDt = AfterburnerActive ? dt * AfterburnerFuelMultiplier : dt;
            // A radiating jammer is a power drain on the same tank (see JammerFuelMultiplier). Set by
            // HandleAbilities at the top of this method, so it is this frame's state, not last one's.
            if (JammerActive) burnDt *= JammerFuelMultiplier;
            Controlled.ConsumeFuel(commanded, burnDt);

            // A dry tank means no power: the governor caps the achievable speed, tapering it through
            // the last of the reserve and pinning it at zero when the tank is empty.
            float maxNow = MaxSpeed * boost * ThrottleGovernor.Effective(1f, Controlled.FuelFraction);
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

            // The evasive maneuver (X) flies the drone for its short duration, overriding raw input.
            if (EvadeActive) forward = EvasiveForward(forward, dt);

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

        /// <summary>
        /// Reads the special-ability inputs: <c>Q</c> releases a flare/chaff salvo, <c>E</c> holds the
        /// afterburner (more speed, far heavier burn), <c>X</c> arms a one-shot evasive maneuver and
        /// <c>K</c> fires a jamming burst. Defensive: every ability is a no-op when the drone lacks
        /// the matching hardware.
        /// </summary>
        private void HandleAbilities(float dt)
        {
            if (Controlled == null) return;

            // Q: one flare/chaff salvo. The dispenser owns charges + cooldown.
            if (Input.GetKeyDown(KeyCode.Q))
            {
                CountermeasureDispenser cm = Controlled.Countermeasures;
                if (cm != null) cm.Deploy();
            }

            // E (held): afterburner. A dry tank cannot light it.
            AfterburnerActive = Input.GetKey(KeyCode.E) && !Controlled.IsOutOfFuel;

            // X: arm the one-shot evasive break turn, then run its timer (and lock-out) down.
            if (_evadeCooldown > 0f) _evadeCooldown = Mathf.Max(0f, _evadeCooldown - dt);
            if (Input.GetKeyDown(KeyCode.X) && EvadeReady)
            {
                _evadeTimer = EvadeDuration;
                _evadeCooldown = EvadeCooldownSeconds;
            }
            if (_evadeTimer > 0f) _evadeTimer = Mathf.Max(0f, _evadeTimer - dt);
            EvadeActive = _evadeTimer > 0f;

            // K: one jamming burst. The duty cycle (burst length, cooldown, whether it is radiating)
            // lives in Sim.Core.JammerSystem and is ticked by the airframe itself, so this is only the
            // key; the emitter is null on any aircraft that never bought the "Elektronik Harp" track.
            // K is free: no other KeyCode in Sim.Runtime uses it (audited against every binding).
            Jammer jammer = Controlled.Jammer;
            if (jammer != null)
            {
                if (Input.GetKeyDown(KeyCode.K)) jammer.TryActivate();
                JammerActive = jammer.IsActive;
            }
            else
            {
                JammerActive = false;
            }

            // Publish the break state on the airframe: a flare/chaff salvo thrown DURING a break is
            // worth more than one thrown flying straight (see Sim.Core.MissileThreat). The AI branch
            // in IhaController is skipped under ManualControl, so the pilot is the only writer here.
            Controlled.Breaking = EvadeActive;
        }

        /// <summary>
        /// Heading for the evasive break turn: an altitude-aware manoeuvre against the nearest incoming
        /// munition, or a plain break off the current heading when nothing is inbound.
        ///
        /// <para>
        /// The commanded heading puts the missile on the BEAM (see
        /// <see cref="Sim.Core.EvasionSteering.BreakTurn"/>), which maximises the line-of-sight rate the
        /// round's guidance law has to null; the round is now load-limited
        /// (<see cref="Sim.Core.MissileAgility"/>) and cannot always follow. The aircraft SWINGS onto
        /// that heading at <see cref="EvadeTurnRateMultiplier"/> times the pilot's normal rate — an
        /// over-g burst — instead of snapping to it: a snap was both physically free and invisible on
        /// screen. Yaw/pitch stay the pilot's own state throughout, so releasing the ability does not
        /// jerk the aircraft.
        /// </para>
        /// </summary>
        private Vector3 EvasiveForward(Vector3 forward, float dt)
        {
            if (Controlled == null) return forward;

            Vector3 pos = Controlled.transform.position;

            // Threat bearing: toward the missile when there is one, otherwise straight ahead (which
            // makes the break turn jink off the current heading).
            Vector3 threatDir = forward;
            ManeuverType maneuver = ManeuverType.BreakTurn;

            if (Controlled.MissileIncoming)
            {
                threatDir = Controlled.IncomingMissilePosition - pos;
                ManeuverType chosen = EvasiveManeuver.Choose(pos.y, MinAltitude, Controlled.TimeToImpact);
                if (chosen != ManeuverType.None) maneuver = chosen;
            }

            Vector3 evade = EvasiveManeuver.Direction(maneuver, forward, threatDir, Vector3.up);
            if (evade.sqrMagnitude <= 1e-6f) return forward;

            evade = evade.normalized;

            // Over-g burst: drive the pilot's own yaw/pitch toward the break heading at a boosted but
            // finite rate, honouring the pitch limit exactly as manual input does.
            Vector3 euler = Quaternion.LookRotation(evade, Vector3.up).eulerAngles;
            float targetYaw = euler.y;
            float targetPitch = Mathf.Clamp(NormalizeAngle(euler.x), -MaxPitchDeg, MaxPitchDeg);

            _yaw = Mathf.MoveTowardsAngle(_yaw, targetYaw, yawRateDeg * EvadeTurnRateMultiplier * dt);
            _pitch = Mathf.MoveTowards(_pitch, targetPitch, pitchRateDeg * EvadeTurnRateMultiplier * dt);
            _pitch = Mathf.Clamp(_pitch, -MaxPitchDeg, MaxPitchDeg);

            Vector3 flown = Quaternion.Euler(_pitch, _yaw, 0f) * Vector3.forward;
            return flown.sqrMagnitude > 1e-6f ? flown.normalized : forward;
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
