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
    /// It is a SCORE tracker only — win/lose belongs to <see cref="ScenarioController"/>, so the
    /// mission state here is deliberately built so it can never terminate itself (see
    /// <see cref="Start"/>).
    ///
    /// Faction convention (from <see cref="Targetable"/>): 0 = friendly, 1 = hostile.
    /// </summary>
    public class SimulationDirector : MonoBehaviour
    {
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

        // Reusable per-frame buffers for UpdateAllocation: filled and cleared every frame instead of
        // allocating four fresh collections per frame.
        private readonly List<DetectableTarget> _hostileBuffer = new List<DetectableTarget>();
        private readonly List<SihaController> _shooters = new List<SihaController>();
        private readonly List<Vector3> _shooterPositions = new List<Vector3>();
        private readonly List<Vector3> _targetPositions = new List<Vector3>();
        private readonly List<int> _assignment = new List<int>();

        private void Start()
        {
            // This instance is a pure STAT/SCORE tracker: win/lose lives in ScenarioController, which
            // owns the waves (clearing the last one wins; a combat-ineffective squad loses).
            //
            // So the MissionState is built so it can NEVER auto-terminate (finding B-03): a hostile
            // total of 0 means the "all hostiles destroyed" branch never fires, and an effectively
            // infinite friendly-loss allowance means a second friendly loss can no longer flip the
            // status to Lost. Either would have frozen Status away from InProgress, after which
            // RecordHostileDestroyed/RecordFriendlyLost return early and the score silently stops
            // moving for the rest of the mission.
            //
            // A real starting hostile count is not available here anyway: waves spawn later, from
            // ScenarioController.Update, so the field is still empty at this point.
            Mission = new MissionState(0, int.MaxValue);

            // Count starting populations. CountAlive applies the same filtering as GetSnapshot
            // (destroyed targets excluded) without building a list.
            HostilesAlive = TargetRegistry.CountAlive(1);
            FriendliesAlive = TargetRegistry.CountAlive(0);
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

            // Recompute current alive counts (allocation-free: only the counts are needed here).
            HostilesAlive = TargetRegistry.CountAlive(1);
            FriendliesAlive = TargetRegistry.CountAlive(0);

            // Any DROP in hostile count is one or more kills. When a new wave spawns the count INCREASES,
            // so hostilesLost goes negative and the loop simply does not run — no negative kills are
            // recorded. The baseline (_prevHostilesAlive) is refreshed unconditionally below, so counting
            // stays correct across waves regardless of whether the count went up or down.
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
            TargetRegistry.GetSnapshot(1, _hostileBuffer);

            // Only armed drones (SİHA) receive attack assignments. Recon İHAs are left with
            // AssignedTargetId == -1 so they continue their patrol.
            _shooters.Clear();
            for (int i = 0; i < _friendlies.Count; i++)
            {
                // Unity's == first: the cached list can hold drones destroyed since the last refresh,
                // and 'is' alone would happily match such a dead wrapper.
                IhaController c = _friendlies[i];
                if (c == null) continue;
                if (c is SihaController siha) _shooters.Add(siha);
            }

            _shooterPositions.Clear();
            for (int i = 0; i < _shooters.Count; i++)
                _shooterPositions.Add(_shooters[i].transform.position);

            _targetPositions.Clear();
            for (int i = 0; i < _hostileBuffer.Count; i++)
                _targetPositions.Add(_hostileBuffer[i].Position);

            TargetAllocation.Assign(_shooterPositions, _targetPositions, _assignment);

            for (int i = 0; i < _shooters.Count; i++)
            {
                SihaController c = _shooters[i];
                if (c == null) continue;
                int a = _assignment[i];
                c.AssignedTargetId = (a >= 0 && a < _hostileBuffer.Count) ? _hostileBuffer[a].Id : -1;
            }
        }
    }
}
