using System.Collections.Generic;
using UnityEngine;

namespace Sim.Core
{
    /// <summary>
    /// Assigns shooters to targets, avoiding wasted overlap: greedy nearest-pair assignment where each
    /// target is taken by at most one shooter until all targets are used, then leftover shooters double
    /// up on their nearest target. Pure logic.
    /// </summary>
    public static class TargetAllocation
    {
        /// <summary>Returns an array mapping shooter index -> assigned target index (-1 if no targets).</summary>
        public static int[] Assign(IReadOnlyList<Vector3> shooters, IReadOnlyList<Vector3> targets)
        {
            int ns = shooters.Count;
            int nt = targets.Count;
            int[] result = new int[ns];
            for (int i = 0; i < ns; i++) result[i] = -1;
            if (nt == 0) return result;

            bool[] targetTaken = new bool[nt];
            var pairs = new List<(float d, int s, int t)>();
            for (int s = 0; s < ns; s++)
                for (int t = 0; t < nt; t++)
                    pairs.Add(((shooters[s] - targets[t]).sqrMagnitude, s, t));
            pairs.Sort((a, b) => a.d.CompareTo(b.d));

            int assigned = 0;
            foreach (var p in pairs)
            {
                if (assigned >= ns) break;
                if (result[p.s] != -1) continue;   // shooter already has a target
                if (targetTaken[p.t]) continue;    // target already taken
                result[p.s] = p.t;
                targetTaken[p.t] = true;
                assigned++;
            }

            // Leftover shooters (more shooters than targets): assign nearest target, allow doubling.
            if (assigned < ns)
            {
                for (int s = 0; s < ns; s++)
                {
                    if (result[s] != -1) continue;
                    int best = -1;
                    float bestD = float.MaxValue;
                    for (int t = 0; t < nt; t++)
                    {
                        float d = (shooters[s] - targets[t]).sqrMagnitude;
                        if (d < bestD) { bestD = d; best = t; }
                    }
                    result[s] = best;
                }
            }
            return result;
        }
    }
}
