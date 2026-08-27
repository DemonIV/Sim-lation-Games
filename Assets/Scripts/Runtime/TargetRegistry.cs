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

        private static int _nextId = 1;

        /// <summary>
        /// Stable simulation id, handed out on Awake. Deliberately ours rather than Unity's
        /// instance id: those are an engine detail (and <c>GetInstanceID</c> is obsolete in
        /// Unity 6), while <see cref="Sim.Core.DetectableTarget.Id"/> only needs a unique int.
        /// </summary>
        public int Id { get; private set; }

        private void Awake()
        {
            Id = _nextId++;
            Health = new Health(MaxHealth);
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

        /// <summary>
        /// Builds a lightweight snapshot of every live targetable in the given faction,
        /// suitable for feeding into <see cref="Sim.Core.TargetingSystem"/>.
        /// </summary>
        public static List<DetectableTarget> GetSnapshot(int factionFilter)
        {
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

        /// <summary>Finds a registered targetable by its <see cref="Targetable.Id"/>, or null.</summary>
        public static Targetable FindById(int id)
        {
            for (int i = 0; i < All.Count; i++)
            {
                Targetable t = All[i];
                if (t != null && t.Id == id) return t;
            }
            return null;
        }
    }
}
