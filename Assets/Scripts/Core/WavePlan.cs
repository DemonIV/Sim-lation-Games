using UnityEngine;

namespace Sim.Core
{
    /// <summary>Difficulty scaling: how many of each enemy type appear in a given (0-based) wave. Pure logic.</summary>
    public static class WavePlan
    {
        public static int PlainHostilesForWave(int waveIndex) => 1 + Mathf.Max(0, waveIndex);
        public static int SamsForWave(int waveIndex) => 1 + Mathf.Max(0, waveIndex) / 2;
        public static int AaaForWave(int waveIndex) => Mathf.Max(0, waveIndex);

        public static int TotalEnemiesForWave(int waveIndex)
            => PlainHostilesForWave(waveIndex) + SamsForWave(waveIndex) + AaaForWave(waveIndex);
    }
}
