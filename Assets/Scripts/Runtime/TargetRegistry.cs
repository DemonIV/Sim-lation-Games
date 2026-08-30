using System.Collections.Generic;
using UnityEngine;
using Sim.Core;

namespace Sim.Runtime
{
    /// <summary>
    /// A component that marks a GameObject as a live, damageable participant in the simulation.
    /// Registers itself into <see cref="TargetRegistry"/> so controllers can query it each frame.
    /// </summary>
    public class Targetable : MonoBehaviour
    {
        /// <summary>Faction id. 0 = friendly, 1 = hostile.</summary>
        public int Faction = 0;

        /// <summary>Maximum hit points assigned to this object's <see cref="Sim.Core.Health"/> pool.</summary>
        public float MaxHealth = 100f;

        /// <summary>The pure-logic health pool backing this object. Created in Awake.</summary>
        public Health Health { get; private set; }

        /// <summary>Stable simulation id assigned at Awake, used across detection snapshots.</summary>
        public int Id { get; private set; }

        private void Awake()
        {
            Health = new Health(MaxHealth);
            Id = TargetRegistry.NextId();
            TargetRegistry.Register(this);
        }

        private void OnDestroy()
        {
            TargetRegistry.Unregister(this);
        }

        /// <summary>Applies damage to this object's health pool and destroys the GameObject when depleted.</summary>
        public void TakeDamage(float amount)
        {
            if (Health == null) return;
            Health.ApplyDamage(amount);
            if (Health.IsDestroyed)
            {
                ExplosionEffect.Spawn(transform.position, 6f);
                Destroy(gameObject);
            }
        }
    }

    /// <summary>
    /// Static registry of all live <see cref="Targetable"/> instances. Lets controllers build
    /// per-frame detection snapshots without scanning the whole scene graph.
    /// </summary>
    public static class TargetRegistry
    {
        /// <summary>All currently registered targetables.</summary>
        public static readonly List<Targetable> All = new List<Targetable>();

        /// <summary>Monotonic source of stable per-<see cref="Targetable"/> ids.</summary>
        private static int _nextId = 1;

        /// <summary>Returns the next stable id for a <see cref="Targetable"/>.</summary>
        public static int NextId() => _nextId++;

        /// <summary>Adds a targetable to the registry.</summary>
        public static void Register(Targetable t)
        {
            if (t != null && !All.Contains(t)) All.Add(t);
        }

        /// <summary>Removes a targetable from the registry.</summary>
        public static void Unregister(Targetable t)
        {
            All.Remove(t);
        }

        /// <summary>Clears every registered targetable. Used on scene restart to drop stale entries.</summary>
        public static void Clear()
        {
            All.Clear();
        }

        /// <summary>
        /// Drops every DESTROYED entry from <see cref="All"/>. A Unity object that has been destroyed
        /// compares equal to null but still occupies its slot in the list (OnDestroy is not guaranteed
        /// to have run yet, and a scene reload can leave stale wrappers behind); touching any member of
        /// such an entry throws. Called at the top of every scan so no consumer ever sees a dead entry.
        /// Iterates BACKWARDS so removal does not skip elements.
        /// </summary>
        public static void Prune()
        {
            for (int i = All.Count - 1; i >= 0; i--)
            {
                // Unity's overloaded == is what detects a destroyed object here; do NOT use
                // ReferenceEquals/is null, they would keep dead wrappers in the list.
                if (All[i] == null) All.RemoveAt(i);
            }
        }

        /// <summary>
        /// Builds a lightweight snapshot of every live targetable in the given faction,
        /// suitable for feeding into <see cref="Sim.Core.TargetingSystem"/>.
        /// </summary>
        public static List<DetectableTarget> GetSnapshot(int factionFilter)
        {
            Prune();

            var result = new List<DetectableTarget>();
            for (int i = 0; i < All.Count; i++)
            {
                Targetable t = All[i];
                if (t == null) continue;
                if (t.Faction != factionFilter) continue;
                if (t.Health != null && t.Health.IsDestroyed) continue;
                result.Add(new DetectableTarget(t.Id, t.transform.position, Vector3.zero));
            }
            return result;
        }

        /// <summary>
        /// Finds a LIVE registered targetable by its stable simulation id, or null. Destroyed entries
        /// are pruned rather than returned, so callers never get a dead reference back.
        /// </summary>
        public static Targetable FindById(int id)
        {
            Prune();

            for (int i = 0; i < All.Count; i++)
            {
                Targetable t = All[i];
                if (t != null && t.Id == id) return t;
            }
            return null;
        }
    }
}
