using System.Collections.Generic;
using UnityEngine;

namespace Sim.Core
{
    /// <summary>Detects targets within range and field of view, and builds a time-based lock. Pure logic.</summary>
    public class TargetingSystem
    {
        public float DetectionRange = 100f;
        public float FieldOfViewDeg = 60f;   // full cone angle
        public float LockTimeSeconds = 1.5f;

        public bool HasDetection { get; private set; }
        public int DetectedId { get; private set; } = -1;
        public float LockProgress { get; private set; }
        public bool IsLocked => HasDetection && LockProgress >= LockTimeSeconds;

        /// <summary>Returns the nearest target within range and FOV, if any.</summary>
        public bool TryDetect(Vector3 selfPos, Vector3 selfForward,
                              IReadOnlyList<DetectableTarget> targets, out DetectableTarget best)
        {
            best = default;
            float bestSqr = float.MaxValue;
            bool found = false;
            Vector3 fwd = selfForward.sqrMagnitude > 1e-6f ? selfForward.normalized : Vector3.forward;
            float halfFov = FieldOfViewDeg * 0.5f;
            float rangeSqr = DetectionRange * DetectionRange;

            for (int i = 0; i < targets.Count; i++)
            {
                Vector3 to = targets[i].Position - selfPos;
                float sqr = to.sqrMagnitude;
                if (sqr > rangeSqr) continue;
                if (sqr > 1e-6f)
                {
                    float ang = Vector3.Angle(fwd, to);
                    if (ang > halfFov) continue;
                }
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = targets[i];
                    found = true;
                }
            }
            return found;
        }

        /// <summary>Advance lock state. Pass whether a target is currently detected and its id.</summary>
        public void UpdateLock(bool hasTarget, int targetId, float dt)
        {
            if (!hasTarget)
            {
                HasDetection = false;
                DetectedId = -1;
                LockProgress = 0f;
                return;
            }

            if (HasDetection && DetectedId == targetId)
                LockProgress += dt;
            else
            {
                DetectedId = targetId;
                LockProgress = dt;
            }
            HasDetection = true;
        }
    }
}
