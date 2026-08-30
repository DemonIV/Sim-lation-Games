using UnityEngine;

namespace Sim.Core
{
    public enum MissionStatus { InProgress, Won, Lost }

    /// <summary>
    /// Tracks mission objectives and score: destroy all hostiles without exceeding an allowed number
    /// of friendly losses. Pure logic.
    /// </summary>
    public class MissionState
    {
        public int HostilesTotal { get; private set; }
        public int HostilesDestroyed { get; private set; }
        public int FriendliesLost { get; private set; }
        public int MaxFriendlyLosses;
        public float ElapsedTime { get; private set; }
        public MissionStatus Status { get; private set; } = MissionStatus.InProgress;

        public MissionState(int hostilesTotal, int maxFriendlyLosses)
        {
            HostilesTotal = Mathf.Max(0, hostilesTotal);
            MaxFriendlyLosses = maxFriendlyLosses;
        }

        public void Tick(float dt)
        {
            if (Status == MissionStatus.InProgress && dt > 0f) ElapsedTime += dt;
        }

        public void RecordHostileDestroyed()
        {
            if (Status != MissionStatus.InProgress) return;
            HostilesDestroyed++;
            Evaluate();
        }

        public void RecordFriendlyLost()
        {
            if (Status != MissionStatus.InProgress) return;
            FriendliesLost++;
            Evaluate();
        }

        private void Evaluate()
        {
            if (FriendliesLost > MaxFriendlyLosses) { Status = MissionStatus.Lost; return; }
            if (HostilesTotal > 0 && HostilesDestroyed >= HostilesTotal) Status = MissionStatus.Won;
        }

        /// <summary>Kills reward, losses and elapsed time penalise.</summary>
        public int Score => HostilesDestroyed * 100 - FriendliesLost * 150 - Mathf.FloorToInt(ElapsedTime);
    }
}
