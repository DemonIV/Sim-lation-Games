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

        /// <summary>
        /// True when a direction measured from the radar lies inside the scan cone. A target sitting
        /// on top of the radar has no meaningful bearing and always counts as inside the beam.
        /// </summary>
        public bool IsWithinBeam(Vector3 radarForward, Vector3 radarToTarget)
        {
            if (radarToTarget.sqrMagnitude <= 1e-12f) return true;
            return Vector3.Angle(radarForward, radarToTarget) <= BeamWidthDeg * 0.5f;
        }

        /// <summary>True if a target of the given RCS is within detection range, beam and LOS.</summary>
        public bool CanDetect(Vector3 radarPos, Vector3 radarForward, Vector3 targetPos, float rcs)
        {
            Vector3 to = targetPos - radarPos;
            if (to.magnitude > DetectionRange(rcs)) return false;
            return IsWithinBeam(radarForward, to);
        }
    }
}
