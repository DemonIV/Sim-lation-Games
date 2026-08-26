using UnityEngine;

namespace Sim.Core
{
    /// <summary>Ballistics helpers for aiming at moving targets. Pure logic.</summary>
    public static class Ballistics
    {
        /// <summary>Iteratively computes an intercept aim point for a finite-speed projectile against a moving target.</summary>
        public static Vector3 ComputeInterceptPoint(Vector3 shooter, Vector3 targetPos,
                                                     Vector3 targetVel, float projectileSpeed,
                                                     int iterations = 8)
        {
            if (projectileSpeed <= 0f) return targetPos;
            Vector3 aim = targetPos;
            for (int i = 0; i < iterations; i++)
            {
                float t = Vector3.Distance(shooter, aim) / projectileSpeed;
                aim = targetPos + targetVel * t;
            }
            return aim;
        }
    }
}
