using UnityEngine;

namespace Sim.Core
{
    /// <summary>The mission types the player can pick.</summary>
    public enum ScenarioKind { Recon, Sead, AirCombat, MixedDefense }

    /// <summary>Enemy composition for one wave of a scenario.</summary>
    public struct WaveComposition
    {
        public int PlainHostiles;
        public int Sams;
        public int Aaa;
        public int Fighters;

        public int Total => PlainHostiles + Sams + Aaa + Fighters;
    }

    /// <summary>Mission definitions: title, briefing, length and per-wave enemy mix. Pure logic.</summary>
    public static class ScenarioLibrary
    {
        public static readonly ScenarioKind[] All =
        {
            ScenarioKind.Recon, ScenarioKind.Sead, ScenarioKind.AirCombat, ScenarioKind.MixedDefense
        };

        public static string Title(ScenarioKind kind)
        {
            switch (kind)
            {
                case ScenarioKind.Recon: return "Keşif Görevi";
                case ScenarioKind.Sead: return "SEAD - Hava Savunması Bastırma";
                case ScenarioKind.AirCombat: return "Hava Muharebesi";
                default: return "Karma Savunma";
            }
        }

        public static string Description(ScenarioKind kind)
        {
            switch (kind)
            {
                case ScenarioKind.Recon:
                    return "Hafif direnişe karşı sahayı temizle. Yeni pilotlar için.";
                case ScenarioKind.Sead:
                    return "Radar ve füze bataryalarını imha et. Yoğun yer savunması.";
                case ScenarioKind.AirCombat:
                    return "Düşman avcı drone'larıyla hava muharebesi. Top ve manevra.";
                default:
                    return "Her tipten düşman. Uzun ve dengeli bir görev.";
            }
        }

        public static int TotalWaves(ScenarioKind kind)
        {
            switch (kind)
            {
                case ScenarioKind.Recon: return 2;
                case ScenarioKind.Sead: return 3;
                case ScenarioKind.AirCombat: return 3;
                default: return 4;
            }
        }

        public static WaveComposition Composition(ScenarioKind kind, int waveIndex)
        {
            int w = Mathf.Max(0, waveIndex);
            WaveComposition c = new WaveComposition();
            switch (kind)
            {
                case ScenarioKind.Recon:
                    c.PlainHostiles = 2 + w;
                    c.Aaa = w;
                    break;
                case ScenarioKind.Sead:
                    c.Sams = 1 + w;
                    c.Aaa = 1 + w;
                    break;
                case ScenarioKind.AirCombat:
                    c.Fighters = 2 + w;
                    break;
                default:
                    c.PlainHostiles = WavePlan.PlainHostilesForWave(w);
                    c.Sams = WavePlan.SamsForWave(w);
                    c.Aaa = WavePlan.AaaForWave(w);
                    c.Fighters = WavePlan.FightersForWave(w);
                    break;
            }
            return c;
        }
    }
}
