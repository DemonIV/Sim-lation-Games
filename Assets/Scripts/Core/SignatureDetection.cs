using UnityEngine;

namespace Sim.Core
{
    /// <summary>
    /// THE detection law of the simulation, stated exactly once: how far a sensor quoted against a
    /// baseline signature actually reaches against a target of a given radar cross section, and
    /// whether that target is visible right now.
    ///
    /// <para>
    /// It is the composition of the two Core systems that already model the two halves —
    /// <see cref="RadarSystem"/>'s radar range equation (detection range ∝ RCS^0.25) and
    /// <see cref="ElectronicWarfare.EffectiveRange"/>'s noise-jamming burn-through — plus the field
    /// of view. <see cref="RadarSystem"/> and <see cref="TargetingSystem"/> both DELEGATE here, so
    /// there is only one copy of the arithmetic in the project.
    /// </para>
    ///
    /// <para>
    /// Every sensor's configured range is read as "the range at which it sees a
    /// <see cref="BaselineRcs"/> target": a bigger signature is picked up further out, a smaller one
    /// closer in, and jamming pulls every one of those distances back in. Degenerate inputs are
    /// answered, never thrown on.
    /// </para>
    ///
    /// This is a GAME / EDUCATIONAL model with abstract, gamified parameters. Pure logic.
    /// </summary>
    public static class SignatureDetection
    {
        /// <summary>
        /// The nominal 1 m² signature every sensor range in the project is quoted against, and the
        /// value an unset signature falls back to (see <see cref="DetectableTarget.Signature"/>).
        /// </summary>
        public const float BaselineRcs = 1f;

        /// <summary>
        /// The radar range equation's exponent: detection range scales with the FOURTH ROOT of RCS,
        /// because received power falls off as 1/range⁴. Sixteen times the signature is exactly twice
        /// the detection range.
        /// </summary>
        public const float RangeExponent = 0.25f;

        /// <summary>
        /// Detection range against a target of <paramref name="rcs"/> m², for a sensor that reaches
        /// <paramref name="referenceRange"/> metres against a <paramref name="referenceRcs"/> m²
        /// target. No jamming.
        ///
        /// <para>
        /// Degenerate inputs: a non-positive reference range or a non-positive
        /// <paramref name="rcs"/> yields 0 (nothing is detected — an object with no radar return is
        /// not a target), and a non-positive <paramref name="referenceRcs"/> is read as
        /// <see cref="BaselineRcs"/> rather than dividing by zero.
        /// </para>
        /// </summary>
        public static float RangeForRcs(float referenceRange, float referenceRcs, float rcs)
        {
            if (referenceRange <= 0f) return 0f;
            if (rcs <= 0f) return 0f;

            float reference = referenceRcs > 0f ? referenceRcs : BaselineRcs;

            // Fast (and exactly identity) path for the overwhelmingly common baseline target.
            if (Mathf.Approximately(rcs, reference)) return referenceRange;

            return referenceRange * Mathf.Pow(rcs / reference, RangeExponent);
        }

        /// <summary>
        /// <see cref="RangeForRcs"/> with noise jamming folded in through
        /// <see cref="ElectronicWarfare.EffectiveRange"/>: the range at which this sensor really
        /// detects this target right now. A non-positive <paramref name="jammerStrength"/> means no
        /// jamming and leaves the range untouched.
        /// </summary>
        public static float EffectiveRange(float referenceRange, float referenceRcs, float rcs,
                                           float jammerStrength)
        {
            float range = RangeForRcs(referenceRange, referenceRcs, rcs);
            if (range <= 0f) return 0f;
            return ElectronicWarfare.EffectiveRange(range, jammerStrength);
        }

        /// <summary>
        /// How far ANY sensor reaches against this target relative to its stated range: 1 means
        /// exactly the configured (baseline) range, 1.41 means half again as far, 0.71 means it has
        /// to come that much closer. Because the reference range cancels out, this single number is
        /// "how detectable am I right now" — signature and jamming folded together — which is
        /// precisely what the HUD's signature readout shows the player.
        /// </summary>
        public static float DetectionRangeMultiplier(float rcs, float jammerStrength)
        {
            return EffectiveRange(1f, BaselineRcs, rcs, jammerStrength);
        }

        /// <summary>
        /// THE predicate: can a sensor at <paramref name="sensorPos"/> looking along
        /// <paramref name="sensorForward"/> see this target right now? Combines all four terms —
        /// range, signature, field of view and jamming.
        ///
        /// <para>
        /// <paramref name="fovDeg"/> is the FULL cone angle (360 = omnidirectional). A target closer
        /// than a millimetre is inside the cone by definition, so a degenerate bearing never decides
        /// the answer. A zero/negative FOV, range or signature simply returns false.
        /// </para>
        /// </summary>
        public static bool CanDetect(Vector3 sensorPos, Vector3 sensorForward, float fovDeg,
                                     Vector3 targetPos, float referenceRange, float referenceRcs,
                                     float rcs, float jammerStrength)
        {
            float range = EffectiveRange(referenceRange, referenceRcs, rcs, jammerStrength);
            if (range <= 0f) return false;

            Vector3 to = targetPos - sensorPos;
            float dist = to.magnitude;
            if (dist > range) return false;

            return IsInFieldOfView(sensorForward, to, fovDeg);
        }

        /// <summary>
        /// True when <paramref name="toTarget"/> lies inside the <paramref name="fovDeg"/> cone
        /// around <paramref name="sensorForward"/>. A degenerate (near-zero) boresight or offset
        /// vector counts as inside, so it can never veto a contact on numerical noise alone.
        /// </summary>
        public static bool IsInFieldOfView(Vector3 sensorForward, Vector3 toTarget, float fovDeg)
        {
            if (toTarget.sqrMagnitude <= 1e-12f) return true;
            if (sensorForward.sqrMagnitude <= 1e-12f) return true;
            return Vector3.Angle(sensorForward, toTarget) <= fovDeg * 0.5f;
        }
    }
}
