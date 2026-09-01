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
        // Scratch buffers reused across calls so a per-frame allocation does not fall out of the
        // assignment. Only ever touched inside Assign, which runs on the single simulation thread.
        private static readonly List<(float d, int s, int t)> Pairs = new List<(float d, int s, int t)>();
        private static readonly List<bool> TargetTaken = new List<bool>();
        private static readonly System.Comparison<(float d, int s, int t)> ByDistance =
            (a, b) => a.d.CompareTo(b.d);

        /// <summary>
        /// Returns an array mapping shooter index -> assigned target index (-1 if no targets).
        /// Allocating convenience overload for cold paths; per-frame callers should use the
        /// buffer-filling overload below.
        /// </summary>
        public static int[] Assign(IReadOnlyList<Vector3> shooters, IReadOnlyList<Vector3> targets)
        {
            var buffer = new List<int>(shooters.Count);
            Assign(shooters, targets, buffer);
            return buffer.ToArray();
        }

        /// <summary>
        /// Allocation-free variant of <see cref="Assign(IReadOnlyList{Vector3}, IReadOnlyList{Vector3})"/>
        /// for per-frame callers: writes shooter index -> assigned target index into the caller-owned
        /// <paramref name="result"/> buffer, which is resized to one entry per shooter. Identical
        /// assignment logic and identical results — only the buffers differ.
        /// </summary>
        public static void Assign(IReadOnlyList<Vector3> shooters, IReadOnlyList<Vector3> targets,
                                  List<int> result)
        {
            if (result == null) return;

            int ns = shooters.Count;
            int nt = targets.Count;

            result.Clear();
            for (int i = 0; i < ns; i++) result.Add(-1);
            if (nt == 0) return;

            TargetTaken.Clear();
            for (int t = 0; t < nt; t++) TargetTaken.Add(false);

            Pairs.Clear();
            for (int s = 0; s < ns; s++)
                for (int t = 0; t < nt; t++)
                    Pairs.Add(((shooters[s] - targets[t]).sqrMagnitude, s, t));
            Pairs.Sort(ByDistance);

            int assigned = 0;
            for (int i = 0; i < Pairs.Count; i++)
            {
                if (assigned >= ns) break;
                var p = Pairs[i];
                if (result[p.s] != -1) continue;      // shooter already has a target
                if (TargetTaken[p.t]) continue;       // target already taken
                result[p.s] = p.t;
                TargetTaken[p.t] = true;
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
        }
    }
}
