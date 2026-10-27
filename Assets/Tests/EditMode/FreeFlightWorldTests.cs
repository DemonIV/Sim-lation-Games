using NUnit.Framework;
using UnityEngine;
using Sim.Core;

namespace Sim.Tests
{
    /// <summary>
    /// Guards the free-flight sandbox's world. Three things are being protected here.
    ///
    /// <para>
    /// (1) The CAMPAIGN'S ground is untouched: the sandbox has its own height field, so no assertion
    /// about <see cref="TerrainField"/> or <see cref="FlightEnvelope"/> may change because of
    /// anything in this file.
    /// </para>
    ///
    /// <para>
    /// (2) The world is BOUNDED and FLYABLE: the relief stays inside the range the mode advertises,
    /// the airbase stays dead level under the runway, and the spawn altitude clears the highest
    /// ground.
    /// </para>
    ///
    /// <para>
    /// (3) The prop budget stays honest: the sandbox is 15× the combat field's area but must carry
    /// only a same-order-of-magnitude COUNT, which can only be true if the per-square-metre density
    /// actually went down.
    /// </para>
    /// </summary>
    public class FreeFlightWorldTests
    {
        // Sampling grid used by the "everywhere" assertions. 61 x 61 points over the whole world is
        // a 20 m lattice, dense enough to land inside every feature the field defines.
        private const int Samples = 61;

        // ------------------------------------------------------------------ extent

        [Test]
        public void World_IsFarBiggerThanTheCombatField()
        {
            // The combat arena is 80 x 80 m inside a 300 x 300 m terrain; this is 1200 x 1200 m.
            Assert.AreEqual(1200f, FreeFlightWorld.TerrainHalfExtent * 2f, 1e-3f);
            Assert.Greater(FreeFlightWorld.TerrainHalfExtent,
                           ThreatEnvelope.FieldHalfExtent * 4f);

            // Props stay inside the mesh, so nothing hangs over the edge.
            Assert.Less(FreeFlightWorld.PropHalfExtent, FreeFlightWorld.TerrainHalfExtent);
        }

        [Test]
        public void TerrainGrid_Is100CellsPerSide()
        {
            float cells = FreeFlightWorld.TerrainHalfExtent * 2f / FreeFlightWorld.TerrainCellSize;
            Assert.AreEqual(100f, cells, 1e-3f);
        }

        /// <summary>
        /// A far clip shorter than the diagonal would end the world in a hard edge in mid-air when
        /// looking from one corner toward the other.
        /// </summary>
        [Test]
        public void FarClip_ExceedsTheWorldDiagonal()
        {
            Assert.Greater(FreeFlightWorld.FarClipPlane, FreeFlightWorld.WorldDiagonal);
        }

        [Test]
        public void SandboxFog_IsThinnerThanTheCombatField()
        {
            // The combat field uses 0.0025 (see EnvironmentBuilder.ApplyAtmosphere); flying toward a
            // 450 m landmark through that much haze is the problem this value exists to solve.
            Assert.Less(FreeFlightWorld.FogDensity, 0.0025f);
            Assert.Greater(FreeFlightWorld.FogDensity, 0f);
        }

        // ------------------------------------------------------------------ relief

        /// <summary>
        /// The airbase disc must stay perfectly level: the runway, the apron and the hangars are flat
        /// boxes placed at y = 0, so any relief under them would push them through the ground.
        /// </summary>
        [Test]
        public void AirbaseDisc_IsDeadLevel()
        {
            Assert.AreEqual(0f, FreeFlightWorld.Height(0f, 0f), 1e-4f);
            Assert.AreEqual(0f, FreeFlightWorld.Height(20f, -10f), 1e-4f);
            Assert.AreEqual(0f, FreeFlightWorld.Height(-30f, 25f), 1e-4f);
        }

        [Test]
        public void Relief_StaysInsideTheAdvertisedRange()
        {
            float step = FreeFlightWorld.TerrainHalfExtent * 2f / (Samples - 1);

            for (int zi = 0; zi < Samples; zi++)
            {
                for (int xi = 0; xi < Samples; xi++)
                {
                    float x = -FreeFlightWorld.TerrainHalfExtent + xi * step;
                    float z = -FreeFlightWorld.TerrainHalfExtent + zi * step;
                    float h = FreeFlightWorld.Height(x, z);

                    Assert.LessOrEqual(h, FreeFlightWorld.MaxTerrainAltitude + 1e-3f,
                                       $"ground too high at ({x}, {z})");
                    Assert.GreaterOrEqual(h, FreeFlightWorld.MinTerrainAltitude - 1e-3f,
                                          $"ground too low at ({x}, {z})");
                }
            }
        }

        [Test]
        public void HillCluster_RisesWellAboveTheSurroundingPlain()
        {
            Vector2 c = FreeFlightWorld.HillClusterCentre;
            float peak = FreeFlightWorld.Height(c.x, c.y);

            // The dome contributes its full height at the centre; only the base relief can move it.
            Assert.GreaterOrEqual(peak,
                FreeFlightWorld.HillClusterHeight - FreeFlightWorld.BaseAmplitude - 1e-3f);
            Assert.LessOrEqual(peak, FreeFlightWorld.MaxTerrainAltitude + 1e-3f);

            // And it is a LOCAL feature: well outside its radius the ground is back to plain relief.
            float away = FreeFlightWorld.Height(c.x + FreeFlightWorld.HillClusterRadius + 260f, c.y);
            Assert.LessOrEqual(Mathf.Abs(away), FreeFlightWorld.BaseAmplitude + 1e-3f);
        }

        [Test]
        public void Gorge_IsDeeperThanItsOwnRims()
        {
            Vector2 g = FreeFlightWorld.GorgeCentre;
            float floor = FreeFlightWorld.Height(g.x, g.y);
            float rim = FreeFlightWorld.Height(g.x + FreeFlightWorld.GorgeHalfWidth + 4f, g.y);

            Assert.Less(floor, rim - FreeFlightWorld.GorgeDepth * 0.5f);
            Assert.LessOrEqual(floor,
                FreeFlightWorld.BaseAmplitude - FreeFlightWorld.GorgeDepth + 1e-3f);

            // The rim itself is untouched ground, so it stays inside the plain relief band.
            Assert.LessOrEqual(Mathf.Abs(rim), FreeFlightWorld.BaseAmplitude + 1e-3f);
        }

        [Test]
        public void Lake_IsADeadLevelSheet()
        {
            Vector2 l = FreeFlightWorld.LakeCentre;
            float half = FreeFlightWorld.LakeRadius * 0.5f;

            Assert.AreEqual(FreeFlightWorld.LakeBedAltitude,
                            FreeFlightWorld.Height(l.x, l.y), 1e-4f);
            Assert.AreEqual(FreeFlightWorld.LakeBedAltitude,
                            FreeFlightWorld.Height(l.x + half, l.y), 1e-4f);
            Assert.AreEqual(FreeFlightWorld.LakeBedAltitude,
                            FreeFlightWorld.Height(l.x, l.y - half), 1e-4f);

            // Beyond the shore ramp the natural ground is back.
            float outside = FreeFlightWorld.Height(
                l.x + FreeFlightWorld.LakeRadius + FreeFlightWorld.LakeShoreBlend + 10f, l.y);
            Assert.LessOrEqual(Mathf.Abs(outside), FreeFlightWorld.BaseAmplitude + 1e-3f);
        }

        /// <summary>
        /// Every landmark has to be far enough out that the airbase blend does not flatten it and the
        /// player has somewhere to fly TO — that is the whole point of the mode.
        /// </summary>
        [Test]
        public void Landmarks_StandWellClearOfTheAirbaseAndInsideTheWorld()
        {
            Vector2[] anchors =
            {
                FreeFlightWorld.HillClusterCentre,
                FreeFlightWorld.LakeCentre,
                FreeFlightWorld.GorgeCentre,
                FreeFlightWorld.MastCentre
            };

            float blendEnd = TerrainField.FlatRadius + TerrainField.FlatBlend;

            for (int i = 0; i < anchors.Length; i++)
            {
                Vector2 a = anchors[i];
                float d = a.magnitude;
                Assert.Greater(d, blendEnd + 200f, $"landmark {i} is too close to the airbase");
                Assert.Less(Mathf.Abs(a.x), FreeFlightWorld.PropHalfExtent);
                Assert.Less(Mathf.Abs(a.y), FreeFlightWorld.PropHalfExtent);
            }
        }

        [Test]
        public void Mast_IsTheTallestBuiltThingInTheWorld()
        {
            // Taller than any scenery prop the shared environment builder can produce (11 m), which is
            // what makes it readable as a landmark from across the field.
            Assert.Greater(FreeFlightWorld.MastHeight, FlightEnvelope.MaxStructureHeight * 4f);
        }

        [Test]
        public void BridgeDeck_SitsAboveBothAbutmentsAndOverTheGorgeFloor()
        {
            Vector2 g = FreeFlightWorld.GorgeCentre;
            float half = FreeFlightWorld.BridgeSpan * 0.5f;

            float left = FreeFlightWorld.Height(g.x - half, g.y);
            float right = FreeFlightWorld.Height(g.x + half, g.y);
            float deck = FreeFlightWorld.BridgeDeckAltitude;

            Assert.GreaterOrEqual(deck, left + FreeFlightWorld.BridgeDeckClearance - 1e-3f);
            Assert.GreaterOrEqual(deck, right + FreeFlightWorld.BridgeDeckClearance - 1e-3f);
            Assert.Greater(deck, FreeFlightWorld.Height(g.x, g.y) + FreeFlightWorld.GorgeDepth * 0.5f);
        }

        /// <summary>Both abutments must land on the RIMS, not inside the ravine.</summary>
        [Test]
        public void BridgeSpan_ReachesAcrossTheGorge()
        {
            Assert.Greater(FreeFlightWorld.BridgeSpan * 0.5f, FreeFlightWorld.GorgeHalfWidth);
        }

        // ------------------------------------------------------------------ spawn

        [Test]
        public void Spawn_ClearsTheHighestGroundAndSitsInsideTheWorld()
        {
            Vector3 p = FreeFlightWorld.SpawnPosition;

            Assert.AreEqual(FreeFlightWorld.SpawnAltitude, p.y, 1e-4f);
            Assert.Greater(FreeFlightWorld.SpawnGroundClearance, 0f);
            Assert.Less(Mathf.Abs(p.x), FreeFlightWorld.TerrainHalfExtent);
            Assert.Less(Mathf.Abs(p.z), FreeFlightWorld.TerrainHalfExtent);

            // It spawns over the level airbase, so there is a runway underneath on the first frame.
            Assert.AreEqual(0f, FreeFlightWorld.Height(p.x, p.z), 1e-4f);
        }

        // ------------------------------------------------------------------ prop budget

        [Test]
        public void ScatterArea_IsTheSquareMinusTheAirbaseKeepOut()
        {
            float h = 150f;
            float expected = 4f * h * h - Mathf.PI * FreeFlightWorld.KeepOutRadius
                                                   * FreeFlightWorld.KeepOutRadius;
            Assert.AreEqual(expected, FreeFlightWorld.ScatterArea(h), 1f);
        }

        /// <summary>
        /// THE budget assertion. The sandbox is over fifteen times the combat field's ground, and it
        /// is allowed to carry only a little over twice its props — which is only possible because the
        /// density was lowered rather than the count multiplied.
        /// </summary>
        [Test]
        public void WideningTheWorld_LoweredTheDensityInsteadOfMultiplyingTheCount()
        {
            float areaRatio = FreeFlightWorld.ScatterArea(FreeFlightWorld.PropHalfExtent)
                            / FreeFlightWorld.ScatterArea(FreeFlightWorld.CombatPropHalfExtent);
            Assert.Greater(areaRatio, 15f);

            // Count grows by ~2.3x, i.e. the SAME order of magnitude as the combat field.
            Assert.AreEqual(2.32f, FreeFlightWorld.CountScale, 0.02f);
            Assert.Less(FreeFlightWorld.CountScale, 5f);

            // Density strictly falls, by exactly the factor the constant advertises.
            Assert.Less(FreeFlightWorld.PropDensity(220), FreeFlightWorld.CombatPropDensity(220));
            Assert.AreEqual(FreeFlightWorld.PropDensityFactor,
                            FreeFlightWorld.PropDensity(220) / FreeFlightWorld.CombatPropDensity(220),
                            0.01f);
        }

        [Test]
        public void ScaledCounts_MatchTheStatedSandboxBudget()
        {
            // The combat field's own scatter budget is 220 trees / 70 rocks / 18 buildings.
            Assert.AreEqual(510, FreeFlightWorld.ScaleCount(220));
            Assert.AreEqual(162, FreeFlightWorld.ScaleCount(70));
            Assert.AreEqual(42, FreeFlightWorld.ScaleCount(18));
        }

        [Test]
        public void ScaleCount_HandlesDegenerateInputs()
        {
            Assert.AreEqual(0, FreeFlightWorld.ScaleCount(0));
            Assert.AreEqual(0, FreeFlightWorld.ScaleCount(-5));
            Assert.GreaterOrEqual(FreeFlightWorld.ScaleCount(1), 1);
        }

        [Test]
        public void ScatterArea_NeverReturnsZeroOrNegative()
        {
            Assert.Greater(FreeFlightWorld.ScatterArea(0f), 0f);
            Assert.Greater(FreeFlightWorld.ScatterArea(-100f), 0f);
        }

        // ------------------------------------------------------------------ placement rules

        [Test]
        public void NoPropsOverTheAirbaseTheLakeTheGorgeOrTheLandmarks()
        {
            Assert.IsFalse(FreeFlightWorld.IsScatterable(0f, 0f), "airbase");

            Vector2 l = FreeFlightWorld.LakeCentre;
            Assert.IsFalse(FreeFlightWorld.IsScatterable(l.x, l.y), "lake centre");
            Assert.IsFalse(FreeFlightWorld.IsScatterable(l.x + FreeFlightWorld.LakeRadius * 0.9f, l.y),
                           "lake edge");

            Vector2 g = FreeFlightWorld.GorgeCentre;
            Assert.IsFalse(FreeFlightWorld.IsScatterable(g.x, g.y), "gorge / bridge");
            Assert.IsFalse(FreeFlightWorld.IsScatterable(g.x, g.y + FreeFlightWorld.GorgeHalfLength * 0.8f),
                           "gorge run");

            Vector2 m = FreeFlightWorld.MastCentre;
            Assert.IsFalse(FreeFlightWorld.IsScatterable(m.x, m.y), "mast footing");
        }

        [Test]
        public void OpenCountry_IsScatterable()
        {
            Assert.IsTrue(FreeFlightWorld.IsScatterable(450f, 100f));
            Assert.IsTrue(FreeFlightWorld.IsScatterable(-120f, 160f));
        }

        /// <summary>
        /// The keep-outs are features, not a mask over the map: if they rejected most of the world the
        /// placer's twelve rejection samples would start dropping props and the scatter would thin
        /// unpredictably.
        /// </summary>
        [Test]
        public void KeepOuts_CoverOnlyASmallShareOfTheWorld()
        {
            float step = FreeFlightWorld.PropHalfExtent * 2f / (Samples - 1);
            int ok = 0;
            int total = 0;

            for (int zi = 0; zi < Samples; zi++)
            {
                for (int xi = 0; xi < Samples; xi++)
                {
                    float x = -FreeFlightWorld.PropHalfExtent + xi * step;
                    float z = -FreeFlightWorld.PropHalfExtent + zi * step;
                    total++;
                    if (FreeFlightWorld.IsScatterable(x, z)) ok++;
                }
            }

            Assert.Greater(ok / (float)total, 0.9f);
        }

        // ------------------------------------------------------------------ campaign isolation

        /// <summary>
        /// The sandbox field must not have moved the COMBAT field's ground or the cruise floor derived
        /// from it. Stated here as well as in <c>TerrainFieldTests</c>/<c>FlightEnvelopeTests</c>
        /// because this is the file that would have caused it.
        /// </summary>
        [Test]
        public void CombatTerrainAndCruiseFloor_AreUntouched()
        {
            Assert.AreEqual(3f, TerrainField.Amplitude, 1e-4f);
            Assert.AreEqual(0f, TerrainField.Height(0f, 0f), 1e-4f);
            Assert.AreEqual(18f, FlightEnvelope.MinCruiseAltitude, 1e-4f);

            // And the two fields really are different: the sandbox is the taller one.
            Assert.Greater(FreeFlightWorld.MaxTerrainAltitude, TerrainField.Amplitude * 10f);
        }
    }
}
