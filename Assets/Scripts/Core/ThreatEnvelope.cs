using UnityEngine;

namespace Sim.Core
{
    /// <summary>
    /// The HORIZONTAL counterpart of <see cref="FlightEnvelope"/>: how big the playfield actually is,
    /// and how far each hostile archetype is allowed to see and shoot inside it. Pure logic.
    ///
    /// <para>
    /// This exists for the same reason the vertical envelope does — the two sets of numbers were
    /// chosen independently and stopped relating to each other. Hostile detection ranges were quoted
    /// in the hundreds of metres (SAM 160, enemy fighter 130) on a field whose CORNER-TO-CORNER
    /// distance is only <see cref="FieldDiagonal"/> ≈ 113 m, so every sensor covered every square
    /// metre of the arena at all times. "Detected at 113 m" then means "detected always", and the
    /// ±√2 spread <see cref="AircraftProfile.RadarSignature"/> produces
    /// (see <see cref="SignatureDetection"/>) had nowhere to show up: all three archetypes were
    /// simply seen everywhere.
    /// </para>
    ///
    /// <para>
    /// The ranges below are therefore re-anchored to the ARENA rather than to nothing, and the
    /// invariants that make them playable are asserted by <c>ThreatEnvelopeTests</c> instead of being
    /// re-derived by hand every time a value moves:
    /// </para>
    ///
    /// <list type="bullet">
    /// <item>Detection must EXCEED fire range for the baseline SİHA — a site that cannot see as far as
    /// it shoots can never use its weapon at its stated range.</item>
    /// <item>Detection must NOT cover the whole arena (<see cref="MaxEngagementDistance"/>) for the
    /// baseline, or the range is decorative.</item>
    /// <item>The recon İHA's detection distance must fall SHORT of the fire range, so a small
    /// signature really does shrink the envelope it can be shot inside.</item>
    /// </list>
    ///
    /// This is a GAME / EDUCATIONAL model with abstract, gamified parameters.
    /// </summary>
    public static class ThreatEnvelope
    {
        // ------------------------------------------------------------------ the arena

        /// <summary>
        /// Half the side of the square the scenario scatters hostiles in, in metres — i.e. the
        /// playfield is 80 × 80 m. Mirrored by <c>ScenarioController.fieldHalfExtent</c>, which reads
        /// this constant, so the two can never drift apart.
        ///
        /// <para>
        /// Note this is the COMBAT field, not the terrain mesh: the environment builder lays a 300 m
        /// square of ground and scatters scenery over it, but nothing is ever spawned or fought
        /// outside this box.
        /// </para>
        /// </summary>
        public const float FieldHalfExtent = 40f;

        /// <summary>
        /// Radius of the airbase keep-out disc hostiles are never placed inside, in metres. Mirrored
        /// by <c>ScenarioController.spawnMinRadius</c>. Together with <see cref="FieldHalfExtent"/>
        /// it says where hostiles actually live: the annulus 32 m ≤ r ≤ 56.6 m (square-clipped), so a
        /// typical site sits about 40 m out from the centre.
        /// </summary>
        public const float SpawnKeepOutRadius = 32f;

        /// <summary>
        /// Corner-to-corner distance across the playfield: 2 × 40 × √2 ≈ 113.1 m. THE number the old
        /// sensor ranges have to be read against — a 160 m detection range is 1.4 times the entire
        /// diagonal of the world it is used in.
        /// </summary>
        public static float FieldDiagonal => FieldHalfExtent * 2f * Mathf.Sqrt(2f);

        /// <summary>
        /// The distance from a typical hostile site to the furthest point a player can fly to,
        /// in metres: a site on the 40 m ring plus the 56.6 m corner radius of the box ≈ 96.6 m. A
        /// sensor that reaches this far detects the target EVERYWHERE, which is exactly the state
        /// this class exists to get the game out of.
        /// </summary>
        public static float MaxEngagementDistance => SpawnRingRadius + FieldHalfExtent * Mathf.Sqrt(2f);

        /// <summary>
        /// Radius of the ring a typical ground hostile is scattered on, in metres — the midpoint of
        /// the spawn annulus, used as the reference point for the arena-coverage arithmetic above.
        /// </summary>
        public const float SpawnRingRadius = 40f;

        // ------------------------------------------------------------------ SAM battery

        /// <summary>
        /// SAM detection range against a 1 m² (SİHA-baseline) target, in metres. Was 160 — 1.4 × the
        /// whole field diagonal, so the battery saw everything from everywhere. 85 m covers roughly
        /// the site's own half of the arena: the SİHA is tracked over most of where it usually flies
        /// but can break contact by working the far side, and the archetype spread now lands on
        /// genuinely different distances (jet 120 m — still everywhere; recon İHA 60 m — a real ring).
        /// </summary>
        public const float SamDetectionRange = 85f;

        /// <summary>
        /// SAM fire range, in metres. Was 120 — longer than the field diagonal, i.e. the battery could
        /// engage any point of the arena from any other. 70 m keeps its identity as the LONG-range
        /// threat (it still out-reaches every player gun: recon İHA 45, SİHA 60, jet 70) while making
        /// standoff a real position rather than an impossible one.
        /// </summary>
        public const float SamFireRange = 70f;

        // ------------------------------------------------------------------ AAA piece

        /// <summary>
        /// AAA detection range against a 1 m² target, in metres (was 80, which already covered most
        /// of the field). 60 m keeps it strictly a local sensor: it notices what comes into its own
        /// neighbourhood.
        /// </summary>
        public const float AaaDetectionRange = 60f;

        /// <summary>
        /// AAA fire range, in metres (was 60). 50 m keeps it inside the SİHA's own 60 m gun range —
        /// a gun run on an AAA is still a trade, not a free kill — while sitting clearly under
        /// <see cref="SamFireRange"/> so the two archetypes stay distinct.
        /// </summary>
        public const float AaaFireRange = 50f;

        // ------------------------------------------------------------------ hostile fighter

        /// <summary>
        /// Hostile fighter detection range against a 1 m² target, in metres. Was 130 — the entire
        /// sky, which meant every fighter of a wave turned toward the player the instant it spawned
        /// and the loiter orbit was never actually flown. 65 m makes the search phase real: fighters
        /// hold their orbit until something comes near enough, and how near depends on what you fly
        /// (jet 92 m, SİHA 65 m, recon İHA 46 m).
        /// </summary>
        public const float FighterDetectionRange = 65f;

        /// <summary>
        /// Hostile fighter gun range, in metres. UNCHANGED (55) — it was already field-scale and is
        /// the yardstick the detection range above is set against.
        /// </summary>
        public const float FighterGunRange = 55f;

        // ------------------------------------------------------------------ derived readings

        /// <summary>
        /// The distance at which a sensor quoted as <paramref name="detectionRange"/> against the
        /// 1 m² baseline actually picks up a target of <paramref name="rcs"/> m². Thin alias of
        /// <see cref="SignatureDetection.RangeForRcs"/> so the balance table can be expressed (and
        /// tested) in one call.
        /// </summary>
        public static float DetectionRangeAgainst(float detectionRange, float rcs)
        {
            return SignatureDetection.RangeForRcs(detectionRange, SignatureDetection.BaselineRcs, rcs);
        }

        /// <summary>
        /// The distance a hostile can actually OPEN FIRE at against a target of
        /// <paramref name="rcs"/> m²: its fire range, clipped by how far it can see. This is the real
        /// number a player feels, and the reason a small signature is worth something even when the
        /// weapon itself is unchanged — you cannot be shot at a range you cannot be seen at (see the
        /// early-out in <c>AirDefenseSite.Update</c>).
        /// </summary>
        public static float FireDistanceAgainst(float detectionRange, float fireRange, float rcs)
        {
            return Mathf.Min(Mathf.Max(0f, fireRange), DetectionRangeAgainst(detectionRange, rcs));
        }

        /// <summary>
        /// True when a sensor of this range reaches every point of the arena from a typical hostile
        /// position — i.e. when its "range" has stopped being a range at all.
        /// </summary>
        public static bool CoversWholeField(float detectionRange)
        {
            return detectionRange >= MaxEngagementDistance;
        }
    }
}
