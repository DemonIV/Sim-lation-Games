using UnityEngine;

namespace Sim.Core
{
    /// <summary>
    /// Simplified monostatic radar. Detection range scales with the fourth root of RCS
    /// (radar range equation), limited by beam width and line of sight. Pure logic.
    ///
    /// <para>
    /// The arithmetic itself lives in <see cref="SignatureDetection"/> — the one place the project
    /// states its detection law — so this class is the SETTINGS (reference range, reference
    /// signature, beam width) plus thin delegations to it.
    /// </para>
    /// </summary>
    public class RadarSystem
    {
        public float ReferenceRange = 100f;  // detection range against ReferenceRcs
        public float ReferenceRcs = 1f;      // m^2
        public float BeamWidthDeg = 120f;    // full scan cone

        /// <summary>Maximum detection range against a target of the given RCS.</summary>
        public float DetectionRange(float rcs)
        {
            return SignatureDetection.RangeForRcs(ReferenceRange, ReferenceRcs, rcs);
        }

        /// <summary>
        /// Maximum detection range against a target of the given RCS that is jamming this radar with
        /// the given noise strength (0 = not jamming). See
        /// <see cref="ElectronicWarfare.EffectiveRange"/>.
        /// </summary>
        public float DetectionRange(float rcs, float jammerStrength)
        {
            return SignatureDetection.EffectiveRange(ReferenceRange, ReferenceRcs, rcs, jammerStrength);
        }

        /// <summary>True if a target of the given RCS is within detection range, beam and LOS.</summary>
        public bool CanDetect(Vector3 radarPos, Vector3 radarForward, Vector3 targetPos, float rcs)
        {
            return CanDetect(radarPos, radarForward, targetPos, rcs, 0f);
        }

        /// <summary>
        /// True if a target of the given RCS, jamming with the given noise strength, is within the
        /// (jamming-degraded) detection range, the beam and LOS.
        /// </summary>
        public bool CanDetect(Vector3 radarPos, Vector3 radarForward, Vector3 targetPos, float rcs,
                              float jammerStrength)
        {
            return SignatureDetection.CanDetect(radarPos, radarForward, BeamWidthDeg, targetPos,
                                                ReferenceRange, ReferenceRcs, rcs, jammerStrength);
        }
    }
}
