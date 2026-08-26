using UnityEngine;

namespace Sim.Core
{
    /// <summary>Aspect-dependent radar cross section for a target. Pure logic.</summary>
    public class RadarCrossSection
    {
        public float BaseRcs = 1f;        // m^2, nominal
        public float FrontalRcs = 0.1f;   // m^2, nose/tail-on (minimum)
        public float BroadsideRcs = 5f;   // m^2, side-on (maximum)

        /// <summary>
        /// RCS seen by a radar, given the target's forward vector and the line-of-sight
        /// direction from radar to target. Nose/tail-on returns FrontalRcs; broadside returns BroadsideRcs.
        /// </summary>
        public float ValueForAspect(Vector3 targetForward, Vector3 radarToTargetDir)
        {
            if (targetForward.sqrMagnitude < 1e-6f || radarToTargetDir.sqrMagnitude < 1e-6f)
                return BaseRcs;
            float cos = Mathf.Abs(Vector3.Dot(targetForward.normalized, radarToTargetDir.normalized));
            return Mathf.Lerp(BroadsideRcs, FrontalRcs, cos);
        }
    }
}
