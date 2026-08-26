using UnityEngine;

namespace Sim.Core
{
    /// <summary>
    /// Simplified monostatic radar. Detection range scales with the fourth root of RCS
    /// (radar range equation), limited by beam width and line of sight. Pure logic.
    /// </summary>
    public class RadarSystem
    {
        public float ReferenceRange = 100f;  // detection range against ReferenceRcs
        public float ReferenceRcs = 1f;      // m^2
        public float BeamWidthDeg = 120f;    // full scan cone

        /// <summary>Maximum detection range against a target of the given RCS.</summary>
        public float DetectionRange(float rcs)
        {
            if (rcs <= 0f) return 0f;
            return ReferenceRange * Mathf.Pow(rcs / ReferenceRcs, 0.25f);
        }

        /// <summary>True if a target of the given RCS is within detection range, beam and LOS.</summary>
        public bool CanDetect(Vector3 radarPos, Vector3 radarForward, Vector3 targetPos, float rcs)
        {
            float range = DetectionRange(rcs);
            Vector3 to = targetPos - radarPos;
            float dist = to.magnitude;
            if (dist > range) return false;
            if (dist > 1e-6f)
            {
                float ang = Vector3.Angle(radarForward, to);
                if (ang > BeamWidthDeg * 0.5f) return false;
            }
            return true;
        }
    }
}
