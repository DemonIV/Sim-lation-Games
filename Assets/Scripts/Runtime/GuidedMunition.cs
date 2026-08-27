using UnityEngine;
using Sim.Core;

namespace Sim.Runtime
{
    /// <summary>
    /// A SİHA guided munition. All steering and throttle logic lives in the pure-logic
    /// <see cref="Sim.Core.MunitionAutopilot"/> (proportional navigation under a g-limit, plus an
    /// axial thrust term that trims speed toward cruise); gravity and drag come from a lightweight
    /// <see cref="Sim.Core.BallisticProjectile"/> and genuinely shape the trajectory. A gimballed
    /// <see cref="Sim.Core.SeekerGimbal"/> gates the guidance: once the target leaves the seeker's
    /// off-boresight cone the munition loses its track and coasts. On proximity it applies gamified
    /// damage to the target and self-destructs.
    ///
    /// NOTE: this is a GAME / EDUCATIONAL guidance model with abstract, gamified parameters — not a
    /// fidelity-accurate weapon simulation.
    /// </summary>
    public class GuidedMunition : MonoBehaviour
    {
        [Header("Guidance")]
        [SerializeField] private float cruiseSpeed = 180f;
        [SerializeField] private float navGain = 4f;
        [SerializeField] private float thrustGain = 2f;               // 1/s
        [SerializeField] private float maxLateralAccel = 200f;        // m/s^2 airframe g-limit
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
        private MunitionAutopilot _autopilot;
        private float _elapsed;
        private bool _launched;

        /// <summary>The seeker head state, exposed for HUD/telemetry.</summary>
        public SeekerGimbal Seeker => _seeker;

        /// <summary>True while the seeker still holds the target inside its gimbal cone.</summary>
        public bool IsGuiding { get; private set; }

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
            _autopilot = new MunitionAutopilot
            {
                CruiseSpeed = cruiseSpeed,
                NavGain = navGain,
                ThrustGain = thrustGain,
                MaxLateralAcceleration = maxLateralAccel
            };
            IsGuiding = true;
            _elapsed = 0f;
            _launched = true;
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
            Vector3 losDir = los.sqrMagnitude > 1e-6f ? los.normalized : boresight;
            _seeker.Track(boresight, losDir, dt);

            // The seeker can only steer while the target sits inside its gimbal cone. Once it
            // slides outside, the track is lost for good and the munition coasts to a miss.
            if (IsGuiding) IsGuiding = _seeker.IsWithinGimbalLimits(boresight, losDir);

            // Steering + throttle from the autopilot (target velocity abstracted to zero for now),
            // then gravity and drag from the ballistic model on top.
            Vector3 relVel = Vector3.zero - _velocity;
            Vector3 accel = _autopilot.Acceleration(los, relVel, _velocity, IsGuiding);
            accel += _ballistic.Acceleration(new BallisticState(self, _velocity));

            // Integrate. Speed is left to the thrust/drag balance rather than forced to cruise.
            _velocity += accel * dt;

            // Move and orient along the velocity vector.
            Vector3 newPos = self + _velocity * dt;
            transform.position = newPos;
            if (_velocity.sqrMagnitude > 1e-6f)
                transform.forward = _velocity.normalized;

            Debug.DrawLine(self, newPos, IsGuiding ? Color.magenta : Color.grey);

            // Proximity fuze: detonate when close enough.
            if (Vector3.Distance(newPos, targetPos) <= proximityFuzeRadius)
            {
                _target.TakeDamage(damage);
                Destroy(gameObject);
            }
        }
    }
}
