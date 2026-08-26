using UnityEngine;

namespace Sim.Core
{
    /// <summary>Simple hit-point pool. Pure logic.</summary>
    public class Health
    {
        public float Max { get; private set; }
        public float Current { get; private set; }
        public bool IsDestroyed => Current <= 0f;

        public Health(float max)
        {
            Max = Mathf.Max(1f, max);
            Current = Max;
        }

        public void ApplyDamage(float amount)
        {
            if (amount <= 0f) return;
            Current = Mathf.Max(0f, Current - amount);
        }

        public void Heal(float amount)
        {
            if (amount <= 0f || IsDestroyed) return;
            Current = Mathf.Min(Max, Current + amount);
        }
    }
}
