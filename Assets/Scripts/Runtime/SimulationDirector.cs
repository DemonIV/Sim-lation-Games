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

        private void Start()
        {
            // Count starting populations. GetSnapshot already filters out destroyed targets.
            int hostilesTotal = TargetRegistry.GetSnapshot(1).Count;
            Mission = new MissionState(hostilesTotal, maxFriendlyLosses);

            HostilesAlive = hostilesTotal;
            FriendliesAlive = TargetRegistry.GetSnapshot(0).Count;
            _prevHostilesAlive = HostilesAlive;
            _prevFriendliesAlive = FriendliesAlive;
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

            // NOTE: Optional per-drone tactical wiring (pushing TargetAllocation results and
            // EngagementPolicy states into the controllers) is intentionally NOT done here. The
            // existing IhaController/SihaController drive firing internally from their own
            // lock state and expose no public hook to inject an assignment or engagement state, so
            // injecting one would require risky edits to their private engagement flow. Mission
            // scoring and the HUD work fully without it. See the M1 notes in README.
        }
    }
}
