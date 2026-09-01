using UnityEngine;

namespace Sim.Core
{
    /// <summary>Projects world positions onto a nose-up radar scope. Pure logic.</summary>
    public static class RadarScope
    {
        /// <summary>
        /// Projects a world position onto scope-local coordinates in the range -1..1, where +Y is the
        /// aircraft's nose and +X is its right. Returns false when the target is outside the scope range.
        /// Altitude is ignored (a plan-position indicator).
        /// </summary>
        public static bool TryProject(Vector3 selfPosition, Vector3 selfForward, Vector3 targetPosition,
                                      float range, out Vector2 scopePosition)
        {
            scopePosition = Vector2.zero;
            if (range <= 0f) return false;

            // Flatten the aircraft heading onto the XZ plane: (x, z) -> (x, y) in scope space.
            Vector2 f = new Vector2(selfForward.x, selfForward.z);
            f = f.sqrMagnitude > 1e-6f ? f.normalized : new Vector2(0f, 1f);

            // Right-hand side of the nose in the same flattened space (Unity is left-handed in XZ).
            Vector2 r = new Vector2(f.y, -f.x);

            Vector2 rel = new Vector2(targetPosition.x - selfPosition.x, targetPosition.z - selfPosition.z);

            scopePosition = new Vector2(Vector2.Dot(rel, r), Vector2.Dot(rel, f)) / range;

            if (scopePosition.magnitude > 1f)
            {
                scopePosition = Vector2.zero;
                return false;
            }

            return true;
        }
    }
}
