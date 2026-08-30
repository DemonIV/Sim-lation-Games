using UnityEngine;

namespace Sim.Core
{
    /// <summary>Fuel/endurance model. Burns fuel proportional to throttle. Pure logic.</summary>
    public class FuelTank
    {
        public float Capacity { get; private set; }
        public float Current { get; private set; }
        public float BurnRatePerSecond;   // consumption at full throttle

        public bool IsEmpty => Current <= 0f;
        public float Fraction => Capacity > 0f ? Current / Capacity : 0f;

        public FuelTank(float capacity, float burnRatePerSecond)
        {
            Capacity = Mathf.Max(0f, capacity);
            Current = Capacity;
            BurnRatePerSecond = burnRatePerSecond;
        }

        /// <summary>Consume fuel for one step at the given throttle (0..1).</summary>
        public void Consume(float throttle, float dt)
        {
            if (dt <= 0f) return;
            float t = Mathf.Clamp01(throttle);
            Current = Mathf.Max(0f, Current - BurnRatePerSecond * t * dt);
        }

        public void Refuel() => Current = Capacity;
    }
}
