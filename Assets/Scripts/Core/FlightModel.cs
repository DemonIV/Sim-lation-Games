using UnityEngine;

namespace Sim.Core
{
    /// <summary>Deterministic kinematic flight model for a UAV. Pure logic, no MonoBehaviour.</summary>
    public class FlightModel
    {
        public Vector3 Position;
        public Vector3 Forward;   // normalized heading
        public float Speed;       // current speed (m/s)

        public float MaxSpeed = 50f;
        public float MaxAcceleration = 10f;   // m/s^2
        public float MaxTurnRateDeg = 90f;    // deg/s

        public FlightModel(Vector3 position, Vector3 forward)
        {
            Position = position;
            Forward = forward.sqrMagnitude > 1e-6f ? forward.normalized : Vector3.forward;
            Speed = 0f;
        }

        /// <summary>Advance the simulation one step. desiredDirection need not be normalized. throttle in [0,1].</summary>
        public void Step(Vector3 desiredDirection, float throttle, float dt)
        {
            if (dt <= 0f) return;
            throttle = Mathf.Clamp01(throttle);

            if (desiredDirection.sqrMagnitude > 1e-6f)
            {
                Vector3 target = desiredDirection.normalized;
                float maxRad = MaxTurnRateDeg * Mathf.Deg2Rad * dt;
                Forward = Vector3.RotateTowards(Forward, target, maxRad, 0f).normalized;
            }

            float targetSpeed = throttle * MaxSpeed;
            float ds = MaxAcceleration * dt;
            Speed = Mathf.MoveTowards(Speed, targetSpeed, ds);

            Position += Forward * Speed * dt;
        }
    }
}
