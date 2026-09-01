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

        /// <summary>
        /// Resizes the pool in place, PRESERVING the current health ratio.
        ///
        /// <para>
        /// A pool that is at 100% stays at 100% (so resizing right after construction simply gives the
        /// unit its intended hit points), a pool at 50% stays at 50%, and a destroyed pool (ratio 0)
        /// stays destroyed — raising the maximum can never resurrect something. That makes the call
        /// safe mid-mission: it is a change of scale, not a heal and not a hidden kill.
        /// </para>
        ///
        /// <para>Values below 1 are clamped to 1, exactly like the constructor.</para>
        /// </summary>
        public void SetMax(float max)
        {
            float newMax = Mathf.Max(1f, max);
            float ratio = Max > 0f ? Current / Max : 1f;
            Max = newMax;
            Current = Mathf.Clamp(newMax * ratio, 0f, newMax);
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
