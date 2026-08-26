using UnityEngine;

namespace Sim.Core
{
    /// <summary>
    /// Alpha-beta filter that estimates a target's position and velocity from noisy
    /// position measurements at a (roughly) fixed update rate. Pure logic.
    /// </summary>
    public class TargetTracker
    {
        public float Alpha = 0.5f;
        public float Beta = 0.1f;
        public Vector3 Position { get; private set; }
        public Vector3 Velocity { get; private set; }
        public bool Initialized { get; private set; }

        public void Reset()
        {
            Initialized = false;
            Position = Vector3.zero;
            Velocity = Vector3.zero;
        }

        /// <summary>Ingest a new position measurement; updates the smoothed estimate.</summary>
        public void Update(Vector3 measurement, float dt)
        {
            if (!Initialized)
            {
                Position = measurement;
                Velocity = Vector3.zero;
                Initialized = true;
                return;
            }

            Vector3 predicted = Position + Velocity * dt;
            Vector3 residual = measurement - predicted;
            Position = predicted + Alpha * residual;
            if (dt > 1e-6f) Velocity += (Beta / dt) * residual;
        }
    }
}
