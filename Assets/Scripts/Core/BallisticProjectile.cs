using UnityEngine;

namespace Sim.Core
{
    /// <summary>Position/velocity snapshot for a ballistic body.</summary>
    public struct BallisticState
    {
        public Vector3 Position;
        public Vector3 Velocity;

        public BallisticState(Vector3 position, Vector3 velocity)
        {
            Position = position;
            Velocity = velocity;
        }
    }

    /// <summary>
    /// Point-mass ballistic integrator with gravity, quadratic aerodynamic drag,
    /// wind and altitude-dependent air density. Pure logic, deterministic.
    /// </summary>
    public class BallisticProjectile
    {
        public float Mass = 10f;                 // kg
        public float DragCoefficient = 0.3f;     // Cd
        public float CrossSectionArea = 0.008f;  // m^2
        public Vector3 Gravity = new Vector3(0f, -9.81f, 0f);
        public Vector3 Wind = Vector3.zero;      // m/s
        public bool UseAtmosphere = true;

        /// <summary>Total acceleration acting on the body in the given state.</summary>
        public Vector3 Acceleration(BallisticState s)
        {
            Vector3 a = Gravity;
            Vector3 vRel = s.Velocity - Wind;
            float speed = vRel.magnitude;
            if (speed > 1e-6f && DragCoefficient > 0f && CrossSectionArea > 0f)
            {
                float rho = UseAtmosphere
                    ? Atmosphere.DensityAtAltitude(s.Position.y)
                    : Atmosphere.SeaLevelDensity;
                float dragMag = 0.5f * rho * speed * speed * DragCoefficient * CrossSectionArea;
                a += -(dragMag / Mass) * vRel.normalized;
            }
            return a;
        }

        /// <summary>Advance one step with semi-implicit (symplectic) Euler integration.</summary>
        public BallisticState Step(BallisticState s, float dt)
        {
            if (dt <= 0f) return s;
            Vector3 a = Acceleration(s);
            Vector3 v = s.Velocity + a * dt;
            Vector3 p = s.Position + v * dt;
            return new BallisticState(p, v);
        }
    }
}
