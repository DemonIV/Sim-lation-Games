using UnityEngine;

namespace Sim.Core
{
    /// <summary>
    /// The free-flight sandbox's WORLD: how big it is, what its ground does, where its landmarks
    /// stand and how thickly it is dressed. Deterministic pure logic, like <see cref="TerrainField"/>
    /// — which it deliberately does NOT replace.
    ///
    /// <para>
    /// WHY A SECOND HEIGHT FIELD. <see cref="TerrainField"/> is the COMBAT field's ground and it is
    /// load-bearing: <see cref="FlightEnvelope"/> derives the 18 m cruise floor from its 3 m
    /// amplitude, and every hostile spawn, prop and drone in the campaign sits on it. Raising it to
    /// make the sandbox interesting would move the whole campaign's cruise band. So the sandbox gets
    /// its own field, built from the same Perlin law but at a longer wavelength, a taller amplitude
    /// and with three deliberate features cut into it — and the campaign is untouched.
    /// </para>
    ///
    /// <para>
    /// SCALE. The combat arena is 80 × 80 m (<see cref="ThreatEnvelope.FieldHalfExtent"/>) inside a
    /// 300 × 300 m terrain. Nothing constrains the sandbox that way — there is no engagement envelope
    /// — so the terrain here is <see cref="TerrainHalfExtent"/> × 2 = 1200 m on a side. The prop count
    /// is NOT multiplied to match: <see cref="PropDensityFactor"/> deliberately thins the scatter as
    /// the area grows, so a 15× bigger field carries only about 2.3× the props. The point of the mode
    /// is distance, not clutter.
    /// </para>
    ///
    /// <para>
    /// The ground has no collider anywhere in this project (<c>EnvironmentBuilder</c> creates none),
    /// so the relief below is read by the eye and by the prop placer, not by physics: an aircraft
    /// flown into a hill passes through it and is stopped only by the pilot's own 5 m altitude floor.
    /// That is a pre-existing property of the flight model, not something this mode introduces.
    /// </para>
    /// </summary>
    public static class FreeFlightWorld
    {
        // ---------------------------------------------------------------- extent

        /// <summary>Half the side length of the sandbox terrain, in metres: a 1200 × 1200 m world.</summary>
        public const float TerrainHalfExtent = 600f;

        /// <summary>
        /// Terrain grid cell size, in metres. Coarser than the combat field's 5 m because the world is
        /// four times wider: 1200 / 12 = 100 cells per side, i.e. 10 201 vertices and 20 000 triangles
        /// against the combat mesh's 3 721 / 7 200. The relief's shortest wavelength is
        /// 1 / <see cref="BaseFrequency"/> ≈ 222 m, so a 12 m cell still samples it ~18 times.
        /// </summary>
        public const float TerrainCellSize = 12f;

        /// <summary>
        /// Scatter half-extent for props, held inside <see cref="TerrainHalfExtent"/> so no tree or
        /// building ever hangs over the edge of the mesh.
        /// </summary>
        public const float PropHalfExtent = 560f;

        /// <summary>Corner-to-corner distance across the sandbox, in metres (~1697 m).</summary>
        public static float WorldDiagonal => TerrainHalfExtent * 2f * Mathf.Sqrt(2f);

        /// <summary>
        /// Camera far clip for the sandbox. It must exceed <see cref="WorldDiagonal"/>, otherwise a
        /// pilot standing in one corner cannot see the opposite one and the world would end in a hard
        /// edge mid-air.
        /// </summary>
        public const float FarClipPlane = 2600f;

        /// <summary>
        /// Fog density for the sandbox — thinner than the combat field's 0.0025. With Unity's
        /// exponential-squared fog, 0.0025 leaves only ~28 % of a 450 m landmark visible, which is
        /// fine for an 80 m arena and useless for a mode whose entire purpose is flying TOWARD
        /// something. At 0.0009 that same landmark reads at ~85 % and the far hills still fade.
        /// </summary>
        public const float FogDensity = 0.0009f;

        // ---------------------------------------------------------------- relief

        /// <summary>Peak-to-mean amplitude of the rolling base relief, in metres.</summary>
        public const float BaseAmplitude = 7f;

        /// <summary>
        /// Spatial frequency of the base relief (1 / metres). 0.0045 is a ~222 m wavelength: broad
        /// hills a jet crosses in a few seconds, rather than the combat field's 83 m ripples.
        /// </summary>
        public const float BaseFrequency = 0.0045f;

        /// <summary>Planar centre of the hill cluster landmark.</summary>
        public static Vector2 HillClusterCentre => new Vector2(-330f, 300f);

        /// <summary>Height of the tallest dome in the hill cluster, in metres above the surrounding plain.</summary>
        public const float HillClusterHeight = 55f;

        /// <summary>Radius of the hill cluster's main dome, in metres.</summary>
        public const float HillClusterRadius = 150f;

        /// <summary>Planar centre of the lake flat landmark.</summary>
        public static Vector2 LakeCentre => new Vector2(300f, -340f);

        /// <summary>Radius of the perfectly level lake bed, in metres.</summary>
        public const float LakeRadius = 110f;

        /// <summary>Width of the shore ramp that blends the lake bed back into the natural relief.</summary>
        public const float LakeShoreBlend = 45f;

        /// <summary>Altitude of the lake bed, in metres. Slightly below zero so water reads as a basin.</summary>
        public const float LakeBedAltitude = -3f;

        /// <summary>Planar centre of the gorge — and of the bridge that spans it.</summary>
        public static Vector2 GorgeCentre => new Vector2(-250f, -300f);

        /// <summary>Half-width of the gorge across its short axis (X), in metres.</summary>
        public const float GorgeHalfWidth = 26f;

        /// <summary>Half-length of the gorge along its long axis (Z), in metres.</summary>
        public const float GorgeHalfLength = 110f;

        /// <summary>Depth of the gorge at its centre line, in metres below the surrounding rims.</summary>
        public const float GorgeDepth = 22f;

        /// <summary>
        /// Highest altitude the sandbox ground can reach: the base relief under the tallest hill dome.
        /// The sandbox's own spawn altitude is set clear of this (see <see cref="SpawnAltitude"/>).
        /// </summary>
        public static float MaxTerrainAltitude => BaseAmplitude + HillClusterHeight;

        /// <summary>Lowest altitude the sandbox ground can reach: the base relief under the gorge floor.</summary>
        public static float MinTerrainAltitude => -(BaseAmplitude + GorgeDepth);

        // ---------------------------------------------------------------- built landmarks

        /// <summary>Planar position of the tall lattice mast landmark.</summary>
        public static Vector2 MastCentre => new Vector2(120f, 420f);

        /// <summary>Overall height of the mast above its own footing, in metres — the tallest built thing in the world.</summary>
        public const float MastHeight = 88f;

        /// <summary>Clear radius kept around the mast's footing, so the scatter never swallows it.</summary>
        public const float MastClearRadius = 45f;

        /// <summary>
        /// Deck span of the bridge, in metres: wide enough to land both abutments on the gorge RIMS
        /// (half the span, 44 m, is outside <see cref="GorgeHalfWidth"/>) rather than in the ravine.
        /// </summary>
        public const float BridgeSpan = 88f;

        /// <summary>How far the deck sits above the higher of its two abutments, in metres.</summary>
        public const float BridgeDeckClearance = 5f;

        /// <summary>Clear radius kept around the bridge, so its approaches stay readable.</summary>
        public const float BridgeClearRadius = 70f;

        /// <summary>
        /// World altitude of the bridge deck: the higher of its two abutment grounds plus
        /// <see cref="BridgeDeckClearance"/>. Derived rather than authored, so moving the gorge or the
        /// relief moves the bridge with it instead of leaving it floating.
        /// </summary>
        public static float BridgeDeckAltitude
        {
            get
            {
                float half = BridgeSpan * 0.5f;
                float a = Height(GorgeCentre.x - half, GorgeCentre.y);
                float b = Height(GorgeCentre.x + half, GorgeCentre.y);
                return Mathf.Max(a, b) + BridgeDeckClearance;
            }
        }

        // ---------------------------------------------------------------- spawn

        /// <summary>
        /// Altitude the sandbox spawns the aircraft at, in metres. Set clear of
        /// <see cref="MaxTerrainAltitude"/> so the hill cluster reads as terrain BELOW the aircraft on
        /// the first frame — the player can then dive at it, which is the whole invitation of the mode.
        /// </summary>
        public const float SpawnAltitude = 70f;

        /// <summary>
        /// Where the aircraft starts: over the airbase, a little short of the runway threshold and
        /// facing +Z, which is the default orientation of a freshly created primitive. The mast
        /// landmark sits ahead and slightly right of that heading on purpose.
        /// </summary>
        public static Vector3 SpawnPosition => new Vector3(0f, SpawnAltitude, -60f);

        /// <summary>Clearance between the spawn altitude and the highest ground, in metres.</summary>
        public static float SpawnGroundClearance => SpawnAltitude - MaxTerrainAltitude;

        // ---------------------------------------------------------------- prop budget

        /// <summary>
        /// Radius around the origin that carries no props: the same keep-out the combat field's
        /// scatter uses (the flat airbase disc plus 10 m), restated here so the area maths below and
        /// the placer agree.
        /// </summary>
        public static float KeepOutRadius => TerrainField.FlatRadius + 10f;

        /// <summary>The combat field's scatter half-extent, i.e. the baseline this world is measured against.</summary>
        public const float CombatPropHalfExtent = 150f;

        /// <summary>
        /// How much of the combat field's PER-SQUARE-METRE prop density the sandbox keeps.
        ///
        /// <para>
        /// This is the knob that stops a 15× bigger world from costing 15× the objects. At 0.15 the
        /// sandbox is 6.7× sparser than the combat field, which multiplies out to about 2.3× its prop
        /// COUNT — the same order of magnitude, over fifteen times the ground. Mean spacing goes from
        /// ~16 m to ~42 m: still a dressed landscape from the air, and nothing like a forest.
        /// </para>
        /// </summary>
        public const float PropDensityFactor = 0.15f;

        /// <summary>
        /// Area actually available to the scatter for the given half-extent: the square minus the
        /// airbase keep-out disc.
        /// </summary>
        public static float ScatterArea(float halfExtent)
        {
            float h = Mathf.Max(0f, halfExtent);
            float keepOut = Mathf.Max(0f, KeepOutRadius);
            return Mathf.Max(1f, 4f * h * h - Mathf.PI * keepOut * keepOut);
        }

        /// <summary>
        /// Factor applied to every combat-field prop count to get the sandbox's: the area ratio times
        /// <see cref="PropDensityFactor"/>. About 2.32.
        /// </summary>
        public static float CountScale =>
            ScatterArea(PropHalfExtent) / ScatterArea(CombatPropHalfExtent) * PropDensityFactor;

        /// <summary>
        /// Scales one of the combat field's prop counts to the sandbox. Never returns less than 1 for
        /// a positive input (a category that exists in the combat field exists here too), and 0 for a
        /// non-positive one.
        /// </summary>
        public static int ScaleCount(int combatCount)
        {
            if (combatCount <= 0) return 0;
            return Mathf.Max(1, Mathf.RoundToInt(combatCount * CountScale));
        }

        /// <summary>
        /// Props per square metre in the sandbox for a given combat-field count — exposed so a test
        /// can assert that widening the world LOWERED the density instead of raising the count.
        /// </summary>
        public static float PropDensity(int combatCount)
        {
            return ScaleCount(combatCount) / ScatterArea(PropHalfExtent);
        }

        /// <summary>Props per square metre in the combat field for the same count.</summary>
        public static float CombatPropDensity(int combatCount)
        {
            return Mathf.Max(0, combatCount) / ScatterArea(CombatPropHalfExtent);
        }

        // ---------------------------------------------------------------- height field

        /// <summary>
        /// Ground altitude at the given planar position: rolling base relief, levelled off over the
        /// airbase, with the hill cluster added, the gorge cut out and the lake bed flattened in.
        /// Deterministic — the same (x, z) always yields the same height.
        /// </summary>
        public static float Height(float x, float z)
        {
            // 1. Rolling base relief, faded to dead level over the airbase exactly the way
            //    TerrainField does it, so the runway and the apron stay flat.
            float n = Mathf.PerlinNoise((x + 4000f) * BaseFrequency, (z + 4000f) * BaseFrequency);
            float h = (n - 0.5f) * 2f * BaseAmplitude * AirbaseBlend(x, z);

            // 2. Landmarks cut into the ground. All three stand well clear of the airbase blend above
            //    (the nearest is 390 m out), so none of them is flattened by it.
            h += HillHeightAt(x, z);
            h -= GorgeDepthAt(x, z);
            return LakeHeightAt(x, z, h);
        }

        /// <summary>
        /// 0 over the level airbase, ramping to 1 at full relief — the same flat-disc-plus-blend law
        /// <see cref="TerrainField"/> uses, reading its radii so the two fields agree about where the
        /// base is.
        /// </summary>
        private static float AirbaseBlend(float x, float z)
        {
            float d = Mathf.Sqrt(x * x + z * z);
            return Mathf.Clamp01((d - TerrainField.FlatRadius) / TerrainField.FlatBlend);
        }

        /// <summary>
        /// Height added by the hill cluster: three overlapping smooth domes combined with
        /// <see cref="Mathf.Max"/>, which is what keeps the cluster's peak bounded by
        /// <see cref="HillClusterHeight"/> instead of stacking to three times it.
        /// </summary>
        private static float HillHeightAt(float x, float z)
        {
            Vector2 c = HillClusterCentre;
            float h = Dome(x, z, c.x, c.y, HillClusterRadius, HillClusterHeight);
            h = Mathf.Max(h, Dome(x, z, c.x + 120f, c.y - 90f, 110f, HillClusterHeight * 0.72f));
            h = Mathf.Max(h, Dome(x, z, c.x - 110f, c.y + 100f, 120f, HillClusterHeight * 0.60f));
            return h;
        }

        /// <summary>
        /// One smooth dome: <paramref name="height"/> at the centre, 0 at <paramref name="radius"/>,
        /// with a flat top and a soft skirt (smoothstep, so neither join shows as a crease).
        /// </summary>
        private static float Dome(float x, float z, float cx, float cz, float radius, float height)
        {
            if (radius <= 0f) return 0f;
            float d = Planar(x, z, cx, cz);
            if (d >= radius) return 0f;
            float t = 1f - d / radius;
            return height * t * t * (3f - 2f * t);
        }

        /// <summary>
        /// Depth removed by the gorge: a trough running along Z, smoothstepped across its width so the
        /// walls are cut rather than stepped, and tapered over the last 30 % of each end so the ravine
        /// closes into the ground instead of ending in a cliff face.
        /// </summary>
        private static float GorgeDepthAt(float x, float z)
        {
            Vector2 c = GorgeCentre;
            float dx = Mathf.Abs(x - c.x);
            float dz = Mathf.Abs(z - c.y);
            if (dx >= GorgeHalfWidth || dz >= GorgeHalfLength) return 0f;

            float across = 1f - dx / GorgeHalfWidth;
            across = across * across * (3f - 2f * across);

            float along = Mathf.Clamp01((GorgeHalfLength - dz) / (GorgeHalfLength * 0.30f));
            return GorgeDepth * across * along;
        }

        /// <summary>
        /// The lake flat: dead level at <see cref="LakeBedAltitude"/> inside
        /// <see cref="LakeRadius"/>, lerped back to the natural ground over
        /// <see cref="LakeShoreBlend"/>. A flat sheet at distance is the cheapest landmark there is —
        /// it reads as water from the air with no shader and no asset.
        /// </summary>
        private static float LakeHeightAt(float x, float z, float naturalHeight)
        {
            float d = Planar(x, z, LakeCentre.x, LakeCentre.y);
            if (d >= LakeRadius + LakeShoreBlend) return naturalHeight;

            float t = Mathf.Clamp01((d - LakeRadius) / LakeShoreBlend);
            return Mathf.Lerp(LakeBedAltitude, naturalHeight, t);
        }

        // ---------------------------------------------------------------- placement rules

        /// <summary>
        /// Whether a scattered prop may stand at the given planar position. False over the airbase,
        /// in the lake and its shore, inside the gorge and on its rims, and in the clear ground kept
        /// around the two built landmarks.
        ///
        /// <para>
        /// The lake is the one that matters: everything else here is tidiness, but a tree standing in
        /// open water is the single placement that reads as a bug rather than as landscape.
        /// </para>
        /// </summary>
        public static bool IsScatterable(float x, float z)
        {
            if (Planar(x, z, 0f, 0f) < KeepOutRadius) return false;

            if (Planar(x, z, LakeCentre.x, LakeCentre.y) < LakeRadius + LakeShoreBlend * 0.5f)
                return false;

            Vector2 g = GorgeCentre;
            if (Mathf.Abs(x - g.x) < GorgeHalfWidth + 10f &&
                Mathf.Abs(z - g.y) < GorgeHalfLength) return false;

            if (Planar(x, z, MastCentre.x, MastCentre.y) < MastClearRadius) return false;
            if (Planar(x, z, g.x, g.y) < BridgeClearRadius) return false;

            return true;
        }

        /// <summary>Planar (XZ) distance between two points.</summary>
        private static float Planar(float x, float z, float cx, float cz)
        {
            float dx = x - cx;
            float dz = z - cz;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }
    }
}
