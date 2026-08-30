using UnityEngine;

namespace Sim.Core
{
    public enum ScenarioStatus { InProgress, Won, Lost }

    /// <summary>Drives multi-wave scenario progression and win/lose. Pure logic.</summary>
    public class ScenarioState
    {
        public int TotalWaves { get; private set; }
        public int CurrentWaveIndex { get; private set; }   // 0-based
        public bool AwaitingSpawn { get; private set; } = true;
        public ScenarioStatus Status { get; private set; } = ScenarioStatus.InProgress;

        public int CurrentWaveNumber => CurrentWaveIndex + 1;   // 1-based for display

        public ScenarioState(int totalWaves)
        {
            TotalWaves = Mathf.Max(1, totalWaves);
        }

        /// <summary>Call once the current wave's enemies have been spawned.</summary>
        public void MarkWaveSpawned()
        {
            if (Status == ScenarioStatus.InProgress) AwaitingSpawn = false;
        }

        /// <summary>Feed the number of live enemies each tick; advances the wave when the current one is cleared.</summary>
        public void UpdateEnemies(int liveEnemies)
        {
            if (Status != ScenarioStatus.InProgress) return;
            if (AwaitingSpawn) return;
            if (liveEnemies > 0) return;
            if (CurrentWaveIndex >= TotalWaves - 1)
            {
                Status = ScenarioStatus.Won;
                return;
            }
            CurrentWaveIndex++;
            AwaitingSpawn = true;
        }

        /// <summary>Mark the scenario failed (e.g. all friendly drones lost).</summary>
        public void Fail()
        {
            if (Status == ScenarioStatus.InProgress) Status = ScenarioStatus.Lost;
        }
    }
}
