using System.Collections.Generic;
using UnityEngine;
using Sim.Core;

namespace Sim.Runtime
{
    /// <summary>
    /// Tactical brain + mission tracker. Watches the <see cref="TargetRegistry"/> each frame, detects
    /// when hostiles or friendlies disappear (are destroyed), and feeds those events into a pure-logic
    /// <see cref="MissionState"/> that scores the scenario. The HUD reads this component's accessors.
    ///
    /// Faction convention (from <see cref="Targetable"/>): 0 = friendly, 1 = hostile.
    /// </summary>
    public class SimulationDirector : MonoBehaviour
    {
        [SerializeField] private int maxFriendlyLosses = 1;

        /// <summary>Pure-logic mission/score tracker. Populated in <see cref="Start"/>.</summary>
        public MissionState Mission { get; private set; }

        /// <summary>Number of hostiles (Faction 1) currently alive in the registry.</summary>
        public int HostilesAlive { get; private set; }

        /// <summary>Number of friendlies (Faction 0) currently alive in the registry.</summary>
        public int FriendliesAlive { get; private set; }

        // Previous-frame alive counts, used to detect losses edge-wise.
        private int _prevHostilesAlive;
        private int _prevFriendliesAlive;

        // Cached friendly controllers (recon + armed via inheritance), refreshed periodically so newly
        // spawned drones are picked up without a per-frame scene scan.
        private readonly List<IhaController> _friendlies = new List<IhaController>();
        private float _friendlyRefreshTimer;
        private const float FriendlyRefreshInterval = 2f;

        /// <summary>All friendly drone controllers (İHA + SİHA), for HUD per-drone state.</summary>
        public IReadOnlyList<IhaController> Friendlies => _friendlies;

        private void Start()
        {
            // Count starting populations. GetSnapshot already filters out destroyed targets.
            int hostilesTotal = TargetRegistry.GetSnapshot(1).Count;
            Mission = new MissionState(hostilesTotal, maxFriendlyLosses);

            HostilesAlive = hostilesTotal;
            FriendliesAlive = TargetRegistry.GetSnapshot(0).Count;
            _prevHostilesAlive = HostilesAlive;
            _prevFriendliesAlive = FriendliesAlive;

            RefreshFriendlies();
        }

        /// <summary>Rebuilds the cached list of friendly drone controllers from the scene.</summary>
        private void RefreshFriendlies()
        {
            _friendlies.Clear();
            IhaController[] found = FindObjectsByType<IhaController>(FindObjectsSortMode.None);
            for (int i = 0; i < found.Length; i++)
                if (found[i] != null) _friendlies.Add(found[i]);
        }

        private void Update()
        {
            if (Mission == null) return;

            float dt = Time.deltaTime;
            Mission.Tick(dt);

            // Recompute current alive counts.
            HostilesAlive = TargetRegistry.GetSnapshot(1).Count;
            FriendliesAlive = TargetRegistry.GetSnapshot(0).Count;

            // Any drop in hostile count is one or more kills.
            int hostilesLost = _prevHostilesAlive - HostilesAlive;
            for (int i = 0; i < hostilesLost; i++)
                Mission.RecordHostileDestroyed();

            // Any drop in friendly count is one or more friendly losses.
            int friendliesLost = _prevFriendliesAlive - FriendliesAlive;
            for (int i = 0; i < friendliesLost; i++)
                Mission.RecordFriendlyLost();

            _prevHostilesAlive = HostilesAlive;
            _prevFriendliesAlive = FriendliesAlive;

            // Tactical coordination: allocate hostiles to friendly drones so they spread out over the
            // threats instead of all chasing the same one. Runs each frame off the cached friendly list.
            _friendlyRefreshTimer += dt;
            if (_friendlyRefreshTimer >= FriendlyRefreshInterval)
            {
                _friendlyRefreshTimer = 0f;
                RefreshFriendlies();
            }

            UpdateAllocation();
        }

        /// <summary>
        /// Assigns each armed drone (SİHA) a hostile to head toward via <see cref="TargetAllocation"/>,
        /// pushing the result into that controller's <see cref="IhaController.AssignedTargetId"/>.
        /// Recon İHAs are intentionally excluded from allocation so they keep patrolling (their
        /// <see cref="IhaController.AssignedTargetId"/> stays -1) rather than diving at ground targets.
        /// </summary>
        private void UpdateAllocation()
        {
            List<DetectableTarget> hostiles = TargetRegistry.GetSnapshot(1);

            // Only armed drones (SİHA) receive attack assignments. Recon İHAs are left with
            // AssignedTargetId == -1 so they continue their patrol.
            var shooters = new List<SihaController>(_friendlies.Count);
            for (int i = 0; i < _friendlies.Count; i++)
            {
                if (_friendlies[i] is SihaController siha && siha != null)
                    shooters.Add(siha);
            }

            var shooterPositions = new List<Vector3>(shooters.Count);
            for (int i = 0; i < shooters.Count; i++)
                shooterPositions.Add(shooters[i].transform.position);

            var targetPositions = new List<Vector3>(hostiles.Count);
            for (int i = 0; i < hostiles.Count; i++)
                targetPositions.Add(hostiles[i].Position);

            int[] assignment = TargetAllocation.Assign(shooterPositions, targetPositions);

            for (int i = 0; i < shooters.Count; i++)
            {
                SihaController c = shooters[i];
                if (c == null) continue;
                int a = assignment[i];
                c.AssignedTargetId = (a >= 0 && a < hostiles.Count) ? hostiles[a].Id : -1;
            }
        }
    }
}
