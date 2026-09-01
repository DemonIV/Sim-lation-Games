using System.Collections.Generic;
using UnityEngine;

namespace Sim.Core
{
    /// <summary>
    /// Detects targets within range and field of view, and builds a time-based lock. Pure logic.
    ///
    /// <para>
    /// <see cref="DetectionRange"/> is the range against a <see cref="ReferenceRcs"/> target: each
    /// candidate's own <see cref="DetectableTarget.Signature"/> and jamming are folded in through
    /// <see cref="SignatureDetection"/>, so a bigger airframe is picked up further out and a smaller
    /// (or jamming) one closer in. A snapshot that carries no signature reads as the reference, which
    /// is exactly the configured range — the behaviour this class always had.
    /// </para>
    /// </summary>
    public class TargetingSystem
    {
        public float DetectionRange = 100f;
        public float FieldOfViewDeg = 60f;   // full cone angle
        public float LockTimeSeconds = 1.5f;

        /// <summary>
        /// The signature (m²) <see cref="DetectionRange"/> is quoted against. Leave it at the
        /// baseline unless the sensor is deliberately specified against something else.
        /// </summary>
        public float ReferenceRcs = SignatureDetection.BaselineRcs;

        public bool HasDetection { get; private set; }
        public int DetectedId { get; private set; } = -1;
        public float LockProgress { get; private set; }
        public bool IsLocked => HasDetection && LockProgress >= LockTimeSeconds;

        /// <summary>
        /// Range at which this sensor detects the given target, with its signature and jamming
        /// applied. Exposed so callers (and the HUD) can show WHY something is or is not seen.
        /// </summary>
        public float EffectiveRangeFor(DetectableTarget target)
        {
            return SignatureDetection.EffectiveRange(DetectionRange, ReferenceRcs, target.Signature,
                                                     target.JammerStrength);
        }

        /// <summary>Returns the nearest target within range and FOV, if any.</summary>
        public bool TryDetect(Vector3 selfPos, Vector3 selfForward,
                              IReadOnlyList<DetectableTarget> targets, out DetectableTarget best)
        {
            best = default;
            float bestSqr = float.MaxValue;
            bool found = false;
            Vector3 fwd = selfForward.sqrMagnitude > 1e-6f ? selfForward.normalized : Vector3.forward;

            for (int i = 0; i < targets.Count; i++)
            {
                Vector3 to = targets[i].Position - selfPos;
                float sqr = to.sqrMagnitude;

                // Signature-aware reach: this candidate's own detection range, not a shared one.
                float range = EffectiveRangeFor(targets[i]);
                if (range <= 0f) continue;
                if (sqr > range * range) continue;

                if (!SignatureDetection.IsInFieldOfView(fwd, to, FieldOfViewDeg)) continue;

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
