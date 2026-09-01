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

        [SerializeField] private float maxHealth = 100f;

        /// <summary>
        /// Maximum hit points of this object's <see cref="Sim.Core.Health"/> pool.
        ///
        /// <para>
        /// This is a PROPERTY, not a plain field, on purpose. <see cref="Awake"/> runs synchronously
        /// inside <c>AddComponent</c>, so a spawner can only ever assign this AFTER the pool has been
        /// built. As a bare field that assignment was silently dead — every hostile ended up with the
        /// default 100 HP. The setter now resizes the live pool as well, so writing it is correct at
        /// any point in the object's life and the ordering trap simply cannot be fallen into.
        /// </para>
        /// </summary>
        public float MaxHealth
        {
            get => maxHealth;
            set => SetMaxHealth(value);
        }

        /// <summary>The pure-logic health pool backing this object. Created in Awake.</summary>
        public Health Health { get; private set; }

        /// <summary>Stable simulation id assigned at Awake, used across detection snapshots.</summary>
        public int Id { get; private set; }

        private RcsComponent _rcs;
        private Jammer _jammer;
        private bool _sensorComponentsResolved;

        /// <summary>
        /// Cached <see cref="RcsComponent"/> on this object, or null when it has none. Resolved once
        /// (lazily, so every spawn-time <c>AddComponent</c> has already run) instead of once per radar
        /// per frame. Call <see cref="RefreshSensorComponents"/> if one is added later at runtime.
        /// </summary>
        public RcsComponent Rcs
        {
            get
            {
                ResolveSensorComponents();
                return _rcs;
            }
        }

        /// <summary>
        /// Cached <see cref="Jammer"/> on this object, or null when it has none. Nothing in the scene
        /// currently mounts a jammer, so this stays null and the radar's jamming path costs nothing.
        /// </summary>
        public Jammer Jammer
        {
            get
            {
                ResolveSensorComponents();
                return _jammer;
            }
        }

        /// <summary>
        /// Re-resolves the cached sensor components. Only needed when an <see cref="RcsComponent"/> or
        /// <see cref="Jammer"/> is attached (or removed) after this object has already been scanned.
        /// </summary>
        public void RefreshSensorComponents()
        {
            _sensorComponentsResolved = false;
            ResolveSensorComponents();
        }

        private void ResolveSensorComponents()
        {
            if (_sensorComponentsResolved) return;
            _sensorComponentsResolved = true;
            _rcs = GetComponent<RcsComponent>();
            _jammer = GetComponent<Jammer>();
        }

        private void Awake()
        {
            Health = new Health(maxHealth);
            Id = TargetRegistry.NextId();
            TargetRegistry.Register(this);
        }

        private void OnDestroy()
        {
            TargetRegistry.Unregister(this);
        }

        /// <summary>
        /// Sets the hit-point pool, resizing the live <see cref="Health"/> when it already exists.
        ///
        /// <para>
        /// Identical to assigning <see cref="MaxHealth"/> — the property setter forwards here — so both
        /// spellings behave the same whether they run before or after <see cref="Awake"/>. The pool is
        /// RESIZED rather than replaced, keeping the current health ratio (a full unit stays full, a
        /// half-dead one stays half-dead) and keeping any reference other components already hold.
        /// </para>
        ///
        /// <para>Non-positive values are ignored, so a mis-configured spawner cannot zero a unit out.</para>
        /// </summary>
        public void SetMaxHealth(float value)
        {
            if (value <= 0f) return;
            maxHealth = value;
            if (Health == null) Health = new Health(value);
            else Health.SetMax(value);
        }

        /// <summary>Applies damage to this object's health pool and destroys the GameObject when depleted.</summary>
        public void TakeDamage(float amount)
        {
            if (Health == null) return;
            Health.ApplyDamage(amount);
            if (Health.IsDestroyed)
            {
                Vector3 pos = transform.position;
                ExplosionEffect.Spawn(pos, 6f);

                // Cosmetic: a unit destroyed on (or just above) the ground leaves a burn mark behind.
                if (pos.y <= Sim.Core.TerrainField.Height(pos.x, pos.z) + 3f)
                    ScorchMark.Spawn(pos, 3.5f);

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
            var result = new List<DetectableTarget>();
            GetSnapshot(factionFilter, result);
            return result;
        }

        /// <summary>
        /// Allocation-free variant of <see cref="GetSnapshot(int)"/> for per-frame callers: fills the
        /// caller-owned <paramref name="buffer"/> (cleared first) instead of allocating a new list on
        /// every call. Same filtering, same order.
        /// </summary>
        public static void GetSnapshot(int factionFilter, List<DetectableTarget> buffer)
        {
            if (buffer == null) return;

            Prune();
            buffer.Clear();

            for (int i = 0; i < All.Count; i++)
            {
                Targetable t = All[i];
                if (t == null) continue;
                if (t.Faction != factionFilter) continue;
                if (t.Health != null && t.Health.IsDestroyed) continue;
                buffer.Add(new DetectableTarget(t.Id, t.transform.position, Vector3.zero));
            }
        }

        /// <summary>
        /// Number of live targetables in the given faction, counted without building a snapshot.
        /// Matches <c>GetSnapshot(faction).Count</c> exactly.
        /// </summary>
        public static int CountAlive(int factionFilter)
        {
            Prune();

            int count = 0;
            for (int i = 0; i < All.Count; i++)
            {
                Targetable t = All[i];
                if (t == null) continue;
                if (t.Faction != factionFilter) continue;
                if (t.Health != null && t.Health.IsDestroyed) continue;
                count++;
            }
            return count;
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
