using System.Collections.Generic;
using UnityEngine;

namespace Sim.Core
{
    /// <summary>
    /// One candidate presented to a radar sweep: where it is, how bright it is (aspect RCS already
    /// resolved by the caller) and how hard it is jamming.
    /// </summary>
    public struct RadarScanTarget
    {
        public int Id;
        public Vector3 Position;
        public float Rcs;              // m^2
        public float JammerStrength;   // 0 = not jamming

        public RadarScanTarget(int id, Vector3 position, float rcs, float jammerStrength = 0f)
        {
            Id = id;
            Position = position;
            Rcs = rcs;
            JammerStrength = jammerStrength;
        }
    }

    /// <summary>The contact a radar sweep settled on.</summary>
    public struct RadarContact
    {
        public int Id;
        public Vector3 Position;
        public float Range;

        public RadarContact(int id, Vector3 position, float range)
        {
            Id = id;
            Position = position;
            Range = range;
        }
    }

    /// <summary>
    /// A single radar sweep over a candidate list: applies the range equation, noise-jamming range
    /// degradation and the beam limit, then returns the nearest contact. Pure logic.
    /// </summary>
    public static class RadarScan
    {
        /// <summary>
        /// Detection range against one candidate: the radar range equation (RCS^0.25) reduced by
        /// that candidate's noise jamming (burn-through).
        /// </summary>
        public static float EffectiveDetectionRange(RadarSystem radar, RadarScanTarget target)
        {
            return ElectronicWarfare.EffectiveRange(radar.DetectionRange(target.Rcs), target.JammerStrength);
        }

        /// <summary>
        /// Returns the nearest candidate that is inside its own (jamming-reduced) detection range
        /// and inside the radar beam.
        /// </summary>
        public static bool FindNearest(RadarSystem radar, Vector3 radarPos, Vector3 radarForward,
                                       IReadOnlyList<RadarScanTarget> targets, out RadarContact contact)
        {
            contact = default;
            if (radar == null || targets == null) return false;

            bool found = false;
            float bestDist = float.MaxValue;

            for (int i = 0; i < targets.Count; i++)
            {
                RadarScanTarget t = targets[i];
                Vector3 to = t.Position - radarPos;
                float dist = to.magnitude;

                if (dist > EffectiveDetectionRange(radar, t)) continue;
                if (!radar.IsWithinBeam(radarForward, to)) continue;

                if (dist < bestDist)
                {
                    bestDist = dist;
                    contact = new RadarContact(t.Id, t.Position, dist);
                    found = true;
                }
            }

            return found;
        }
    }
}
