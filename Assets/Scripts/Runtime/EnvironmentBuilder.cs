using UnityEngine;
using Sim.Core;

namespace Sim.Runtime
{
    /// <summary>
    /// Builds the cosmetic world: a procedural rolling-terrain mesh driven by
    /// <see cref="TerrainField"/>, a small airbase pad at the origin, scattered scenery props and the
    /// sky/fog/ambient/sun setup.
    ///
    /// Everything here is decoration only — no colliders are created, so nothing added by this class
    /// can influence physics, raycasts or gameplay.
    /// </summary>
    public static class EnvironmentBuilder
    {
        /// <summary>
        /// Builds the terrain mesh: a grid centred on the origin whose vertex heights come from
        /// <see cref="TerrainField.Height"/>. No collider is attached.
        /// </summary>
        /// <param name="halfExtent">Half the side length of the square terrain, in metres.</param>
        /// <param name="cellSize">Grid cell size, in metres.</param>
        public static GameObject BuildTerrain(float halfExtent = 150f, float cellSize = 5f)
        {
            if (cellSize < 0.5f) cellSize = 0.5f;
            if (halfExtent < cellSize) halfExtent = cellSize;

            int cells = Mathf.Max(1, Mathf.RoundToInt(halfExtent * 2f / cellSize));
            int side = cells + 1;

            var vertices = new Vector3[side * side];
            var uvs = new Vector2[side * side];
            var triangles = new int[cells * cells * 6];

            for (int zi = 0; zi < side; zi++)
            {
                for (int xi = 0; xi < side; xi++)
                {
                    float x = -halfExtent + xi * cellSize;
                    float z = -halfExtent + zi * cellSize;
                    int index = zi * side + xi;
                    vertices[index] = new Vector3(x, TerrainField.Height(x, z), z);
                    uvs[index] = new Vector2(x * 0.05f, z * 0.05f);
                }
            }

            int t = 0;
            for (int zi = 0; zi < cells; zi++)
            {
                for (int xi = 0; xi < cells; xi++)
                {
                    int i0 = zi * side + xi;         // (x,   z)
                    int i1 = i0 + 1;                 // (x+1, z)
                    int i2 = i0 + side;              // (x,   z+1)
                    int i3 = i2 + 1;                 // (x+1, z+1)

                    // Winding chosen so the face normals point up (+Y).
                    triangles[t++] = i0;
                    triangles[t++] = i2;
                    triangles[t++] = i1;

                    triangles[t++] = i1;
                    triangles[t++] = i2;
                    triangles[t++] = i3;
                }
            }

            var mesh = new Mesh();
            mesh.name = "ProceduralTerrain";
            // Safety: large grids can exceed the 16-bit index limit.
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            var go = new GameObject("Terrain");
            go.transform.position = Vector3.zero;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            var mat = MaterialLibrary.Create(new Color(0.34f, 0.36f, 0.22f), 0f, 0.05f);
            if (mat != null) renderer.sharedMaterial = mat;

            return go;
        }

        /// <summary>Builds a modest airbase: an apron pad, a runway with markings and two hangars.</summary>
        /// <param name="centre">World position of the apron centre (usually the origin).</param>
        /// <returns>The "Airbase" root GameObject that parents everything created here.</returns>
        public static GameObject BuildAirbase(Vector3 centre)
        {
            var root = new GameObject("Airbase");
            root.transform.position = centre;

            Material tarmac = MaterialLibrary.Create(new Color(0.16f, 0.16f, 0.17f), 0f, 0.15f);
            Material concrete = MaterialLibrary.Create(new Color(0.62f, 0.62f, 0.60f), 0f, 0.1f);
            Material paint = MaterialLibrary.Create(new Color(0.92f, 0.92f, 0.88f), 0f, 0.2f);
            Material hangar = MaterialLibrary.Create(new Color(0.45f, 0.46f, 0.42f), 0.2f, 0.25f);

            Prop(root.transform, PrimitiveType.Cylinder, "Apron", new Vector3(0f, 0.02f, 0f), new Vector3(18f, 0.05f, 18f), Vector3.zero, tarmac);
            Prop(root.transform, PrimitiveType.Cube, "Runway", new Vector3(0f, 0.06f, 0f), new Vector3(6f, 0.06f, 44f), Vector3.zero, concrete);

            for (int i = -4; i <= 4; i++)
            {
                Prop(root.transform, PrimitiveType.Cube, "RunwayMark_" + (i + 4),
                    new Vector3(0f, 0.10f, i * 4.5f), new Vector3(0.35f, 0.04f, 2.2f), Vector3.zero, paint);
            }

            Prop(root.transform, PrimitiveType.Cube, "Hangar_1", new Vector3(-13f, 1.6f, -8f), new Vector3(7f, 3.2f, 9f), Vector3.zero, hangar);
            Prop(root.transform, PrimitiveType.Cube, "Hangar_2", new Vector3(-13f, 1.4f, 6f), new Vector3(6f, 2.8f, 8f), Vector3.zero, hangar);

            return root;
        }

        /// <summary>
        /// Scatters trees, rocks and buildings across the terrain, skipping the airbase area.
        /// Uses a fixed random seed so the world looks the same on every run.
        ///
        /// <para>Trees come in three species (conifer / broadleaf / scrub) and buildings in three
        /// archetypes (warehouse / mid-rise block / tower). Every size, tilt, colour and archetype
        /// choice is drawn from THIS seeded stream, so the whole layout stays reproducible.</para>
        /// </summary>
        /// <param name="halfExtent">Scatter area half-extent on X and Z.</param>
        /// <param name="treeCount">Number of trees.</param>
        /// <param name="rockCount">Number of rocks.</param>
        /// <param name="buildingCount">Number of buildings.</param>
        /// <returns>The "Props" root GameObject that parents every scattered prop.</returns>
        public static GameObject ScatterProps(float halfExtent, int treeCount = 220, int rockCount = 70, int buildingCount = 18)
        {
            // The fixed seed must stay local to the scatter: every other Random consumer (enemy
            // spawn placement, hit dice, radar noise, decoy rolls) would otherwise be pinned to the
            // same deterministic stream and every run would play out identically.
            Random.State previousRandomState = Random.state;
            Random.InitState(12345);

            var root = new GameObject("Props");
            root.transform.position = Vector3.zero;

            Material trunkMat = MaterialLibrary.Create(new Color(0.30f, 0.21f, 0.13f), 0f, 0.1f);
            Material branchMat = MaterialLibrary.Create(new Color(0.35f, 0.25f, 0.16f), 0f, 0.1f);
            Material rockMat = MaterialLibrary.Create(new Color(0.45f, 0.45f, 0.47f), 0.1f, 0.15f);
            Material[] foliageMats = FoliagePalette();
            Material[] wallMats = WallPalette();
            Material glassMat = MaterialLibrary.Create(new Color(0.13f, 0.16f, 0.20f), 0.2f, 0.55f);
            Material concreteMat = MaterialLibrary.Create(new Color(0.58f, 0.57f, 0.54f), 0f, 0.12f);
            Material roofMat = MaterialLibrary.Create(new Color(0.31f, 0.31f, 0.32f), 0.1f, 0.2f);
            Material metalMat = MaterialLibrary.Create(new Color(0.42f, 0.43f, 0.44f), 0.5f, 0.4f);

            float keepOut = TerrainField.FlatRadius + 10f;

            for (int i = 0; i < treeCount; i++)
            {
                Vector3 p;
                if (!TryPickSpot(halfExtent, keepOut, out p)) continue;

                var tree = new GameObject("Tree_" + i);
                tree.transform.SetParent(root.transform, false);
                tree.transform.position = p;
                // Random yaw plus a couple of degrees of lean. A stand of perfectly upright,
                // identically oriented trees is the single biggest tell that a field is procedural.
                tree.transform.rotation = Quaternion.Euler(
                    Random.Range(-4f, 4f), Random.Range(0f, 360f), Random.Range(-4f, 4f));

                float species = Random.value;
                float s = Random.Range(0.8f, 1.4f);
                float heightFactor = Random.Range(0.9f, 1.3f);
                float crown = Random.Range(0.85f, 1.2f);

                if (species < 0.40f)
                {
                    tree.transform.localScale = new Vector3(s, s * heightFactor, s);
                    // Conifers take the three DARK tones, broadleaves the three light ones: species
                    // then reads by colour as well as by silhouette.
                    BuildConifer(tree.transform, trunkMat, foliageMats[Random.Range(0, 3)], crown);
                }
                else if (species < 0.75f)
                {
                    tree.transform.localScale = new Vector3(s, s * heightFactor, s);
                    BuildBroadleaf(tree.transform, trunkMat, branchMat, foliageMats[Random.Range(3, 6)], crown);
                }
                else
                {
                    // Scrub is bush-sized: same draw, scaled down, and no trunk at all.
                    float b = s * 0.72f;
                    tree.transform.localScale = new Vector3(b, b * heightFactor, b);
                    BuildScrub(tree.transform, foliageMats[Random.Range(2, 5)], crown);
                }
            }

            for (int i = 0; i < rockCount; i++)
            {
                Vector3 p;
                if (!TryPickSpot(halfExtent, keepOut, out p)) continue;

                float s = Random.Range(0.7f, 1.8f);
                var rock = Prop(root.transform, PrimitiveType.Sphere, "Rock_" + i, Vector3.zero,
                    new Vector3(1.4f * s, 0.8f * s, 1.2f * s),
                    new Vector3(Random.Range(-20f, 20f), Random.Range(0f, 360f), Random.Range(-20f, 20f)), rockMat);
                rock.transform.position = p;
            }

            for (int i = 0; i < buildingCount; i++)
            {
                Vector3 p;
                if (!TryPickSpot(halfExtent, keepOut, out p)) continue;

                var building = new GameObject("Building_" + i);
                building.transform.SetParent(root.transform, false);
                building.transform.position = p;
                building.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

                Material wall = wallMats[Random.Range(0, wallMats.Length)];
                float archetype = Random.value;

                if (archetype < 0.36f)
                    BuildWarehouse(building.transform, wall, glassMat, concreteMat, roofMat, metalMat);
                else if (archetype < 0.74f)
                    BuildBlock(building.transform, wall, glassMat, concreteMat, metalMat);
                else
                    BuildTower(building.transform, wall, glassMat, concreteMat, metalMat);
            }

            Random.state = previousRandomState;
            return root;
        }

        /// <summary>
        /// Applies the atmosphere: procedural sky, distance fog, trilight ambient, a warm sun and a
        /// far clip plane wide enough for the new landscape. Null-safe on both arguments.
        /// </summary>
        public static void ApplyAtmosphere(Camera cam, Light sun)
        {
            var horizon = new Color(0.55f, 0.68f, 0.85f);

            Shader skyShader = Shader.Find("Skybox/Procedural");
            if (skyShader != null)
            {
                var sky = new Material(skyShader);
                if (sky.HasProperty("_SkyTint")) sky.SetColor("_SkyTint", new Color(0.45f, 0.58f, 0.80f));
                if (sky.HasProperty("_GroundColor")) sky.SetColor("_GroundColor", new Color(0.32f, 0.31f, 0.26f));
                if (sky.HasProperty("_AtmosphereThickness")) sky.SetFloat("_AtmosphereThickness", 1.1f);
                if (sky.HasProperty("_Exposure")) sky.SetFloat("_Exposure", 1.15f);
                RenderSettings.skybox = sky;
            }
            else if (cam != null)
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = horizon;
            }

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.70f, 0.79f, 0.90f);
            RenderSettings.fogDensity = 0.0025f;

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.52f, 0.60f, 0.72f);
            RenderSettings.ambientEquatorColor = new Color(0.42f, 0.44f, 0.44f);
            RenderSettings.ambientGroundColor = new Color(0.24f, 0.23f, 0.19f);

            if (sun != null)
            {
                sun.type = LightType.Directional;
                sun.color = new Color(1f, 0.96f, 0.88f);
                sun.intensity = 1.15f;
                sun.shadows = LightShadows.Soft;
                sun.transform.rotation = Quaternion.Euler(45f, -35f, 0f);
            }

            if (cam != null) cam.farClipPlane = 1200f;
        }

        // ---------------------------------------------------------------- palettes

        /// <summary>
        /// Six cached foliage tones: indices 0..2 are the dark conifer greens, 3..5 the lighter
        /// broadleaf ones.
        ///
        /// <para>QUANTISING the per-tree colour to a small palette is the point.
        /// <see cref="MaterialLibrary.Create"/> caches by colour, so the old continuous random tint
        /// defeated the cache and minted one material per tree — ~220 unique foliage materials, none
        /// of which could ever batch (finding B-16). Six shared materials cost six.</para>
        /// </summary>
        private static Material[] FoliagePalette()
        {
            return new[]
            {
                MaterialLibrary.Create(new Color(0.10f, 0.25f, 0.14f), 0f, 0.08f),
                MaterialLibrary.Create(new Color(0.12f, 0.30f, 0.16f), 0f, 0.08f),
                MaterialLibrary.Create(new Color(0.16f, 0.34f, 0.14f), 0f, 0.08f),
                MaterialLibrary.Create(new Color(0.19f, 0.40f, 0.16f), 0f, 0.08f),
                MaterialLibrary.Create(new Color(0.24f, 0.44f, 0.19f), 0f, 0.08f),
                MaterialLibrary.Create(new Color(0.29f, 0.48f, 0.23f), 0f, 0.08f),
            };
        }

        /// <summary>Five cached wall tones, quantised for the same reason as the foliage palette.</summary>
        private static Material[] WallPalette()
        {
            return new[]
            {
                MaterialLibrary.Create(new Color(0.62f, 0.59f, 0.52f), 0f, 0.12f),
                MaterialLibrary.Create(new Color(0.54f, 0.52f, 0.48f), 0f, 0.12f),
                MaterialLibrary.Create(new Color(0.66f, 0.60f, 0.50f), 0f, 0.12f),
                MaterialLibrary.Create(new Color(0.48f, 0.47f, 0.45f), 0f, 0.12f),
                MaterialLibrary.Create(new Color(0.58f, 0.54f, 0.50f), 0f, 0.12f),
            };
        }

        // ---------------------------------------------------------------- tree species

        /// <summary>
        /// Conifer: a slim trunk under three stacked, narrowing tiers and a spike. Unity has no cone
        /// primitive, so the classic stepped-cylinder fir is used — the tiers overlap in Y, which is
        /// what stops the steps reading as separate discs. 5 parts.
        /// </summary>
        private static void BuildConifer(Transform tree, Material trunk, Material foliage, float crown)
        {
            Prop(tree, PrimitiveType.Cylinder, "Trunk", new Vector3(0f, 1.00f, 0f), new Vector3(0.22f, 1.00f, 0.22f), Vector3.zero, trunk);
            Prop(tree, PrimitiveType.Cylinder, "Tier_0", new Vector3(0f, 1.60f, 0f), new Vector3(1.90f * crown, 0.55f, 1.90f * crown), Vector3.zero, foliage);
            Prop(tree, PrimitiveType.Cylinder, "Tier_1", new Vector3(0f, 2.60f, 0f), new Vector3(1.35f * crown, 0.50f, 1.35f * crown), Vector3.zero, foliage);
            Prop(tree, PrimitiveType.Cylinder, "Tier_2", new Vector3(0f, 3.50f, 0f), new Vector3(0.85f * crown, 0.45f, 0.85f * crown), Vector3.zero, foliage);
            Prop(tree, PrimitiveType.Cylinder, "Apex", new Vector3(0f, 4.30f, 0f), new Vector3(0.34f * crown, 0.45f, 0.34f * crown), Vector3.zero, foliage);
        }

        /// <summary>
        /// Broadleaf: trunk, two angled branch stubs and three overlapping, non-uniformly scaled
        /// crown spheres offset off the axis so the canopy is lopsided rather than a ball. 6 parts.
        ///
        /// <para>A +Z euler rotates a cylinder's local up toward -X, so the LEFT stub takes the
        /// positive angle — the same mirror convention the airframes' canted surfaces use.</para>
        /// </summary>
        private static void BuildBroadleaf(Transform tree, Material trunk, Material branch, Material foliage, float crown)
        {
            Prop(tree, PrimitiveType.Cylinder, "Trunk", new Vector3(0f, 1.10f, 0f), new Vector3(0.26f, 1.10f, 0.26f), Vector3.zero, trunk);
            Prop(tree, PrimitiveType.Cylinder, "BranchL", new Vector3(-0.42f, 1.95f, 0.10f), new Vector3(0.13f, 0.55f, 0.13f), new Vector3(0f, 0f, 32f), branch);
            Prop(tree, PrimitiveType.Cylinder, "BranchR", new Vector3(0.42f, 1.90f, -0.12f), new Vector3(0.13f, 0.50f, 0.13f), new Vector3(0f, 0f, -32f), branch);
            Prop(tree, PrimitiveType.Sphere, "Crown_0", new Vector3(0f, 2.85f, 0f), new Vector3(2.50f * crown, 2.05f * crown, 2.40f * crown), Vector3.zero, foliage);
            Prop(tree, PrimitiveType.Sphere, "Crown_1", new Vector3(-0.72f, 3.35f, 0.32f), new Vector3(1.70f * crown, 1.50f * crown, 1.65f * crown), Vector3.zero, foliage);
            Prop(tree, PrimitiveType.Sphere, "Crown_2", new Vector3(0.64f, 3.20f, -0.44f), new Vector3(1.60f * crown, 1.40f * crown, 1.72f * crown), Vector3.zero, foliage);
        }

        /// <summary>Scrub: three low, clustered spheres, no visible trunk. 3 parts.</summary>
        private static void BuildScrub(Transform tree, Material foliage, float crown)
        {
            Prop(tree, PrimitiveType.Sphere, "Bush_0", new Vector3(0f, 0.62f, 0f), new Vector3(1.75f * crown, 1.25f * crown, 1.65f * crown), Vector3.zero, foliage);
            Prop(tree, PrimitiveType.Sphere, "Bush_1", new Vector3(-0.62f, 0.48f, 0.34f), new Vector3(1.25f * crown, 0.95f * crown, 1.20f * crown), Vector3.zero, foliage);
            Prop(tree, PrimitiveType.Sphere, "Bush_2", new Vector3(0.55f, 0.52f, -0.42f), new Vector3(1.15f * crown, 1.00f * crown, 1.30f * crown), Vector3.zero, foliage);
        }

        // ---------------------------------------------------------------- building archetypes

        // Every archetype below draws its own footprint and height from the SEEDED scatter stream, so
        // the skyline varies but stays reproducible. Heights are deliberately held near the old
        // 3..8 m envelope — only thin clutter (aerials, masts) reaches higher, so the buildings do
        // not start swallowing the drones' 10..14 m cruise band.
        //
        // Window rows are "banding" boxes a few centimetres wider than the wall block: one part then
        // paints a dark glazing strip onto ALL FOUR faces at once. Four separate per-face panels per
        // storey would quadruple the cost for no visible gain at this scale — no textures exist, so
        // the strip is the window row.

        /// <summary>Low warehouse: plinth, long hall, clerestory band, roller door, ridged saw-tooth roof and roof vents. 9 parts.</summary>
        private static void BuildWarehouse(Transform b, Material wall, Material glass, Material concrete, Material roof, Material metal)
        {
            float w = Random.Range(7f, 11f);
            float d = Random.Range(9f, 15f);
            float h = Random.Range(3.2f, 4.4f);

            Prop(b, PrimitiveType.Cube, "Plinth", new Vector3(0f, 0.15f, 0f), new Vector3(w + 0.70f, 0.30f, d + 0.70f), Vector3.zero, concrete);
            Prop(b, PrimitiveType.Cube, "Walls", new Vector3(0f, 0.30f + h * 0.5f, 0f), new Vector3(w, h, d), Vector3.zero, wall);
            Prop(b, PrimitiveType.Cube, "WindowBand", new Vector3(0f, 0.30f + h * 0.76f, 0f), new Vector3(w + 0.10f, h * 0.20f, d + 0.10f), Vector3.zero, glass);
            Prop(b, PrimitiveType.Cube, "DoorBay", new Vector3(0f, 0.30f + h * 0.34f, d * 0.5f + 0.06f), new Vector3(w * 0.34f, h * 0.62f, 0.12f), Vector3.zero, metal);

            // Three strips across the width, each tilted 20°, spaced so the gaps between them read
            // as valleys: a saw-tooth ridge line rather than a flat lid. Raised enough that a
            // strip's low end only just kisses the wall head instead of cutting the window band.
            for (int r = 0; r < 3; r++)
            {
                Prop(b, PrimitiveType.Cube, "RoofRidge_" + r,
                    new Vector3(0f, 0.30f + h + 0.45f, (r - 1) * d * 0.30f),
                    new Vector3(w + 0.25f, 0.20f, d * 0.26f), new Vector3(20f, 0f, 0f), roof);
            }

            // Vents sit OUTSIDE the outermost ridge (which reaches |z| = 0.43d) so they stay visible.
            Prop(b, PrimitiveType.Cube, "RoofVent_0", new Vector3(w * 0.22f, 0.30f + h + 0.28f, -d * 0.45f), new Vector3(0.70f, 0.55f, 0.70f), Vector3.zero, metal);
            Prop(b, PrimitiveType.Cylinder, "RoofVent_1", new Vector3(-w * 0.20f, 0.30f + h + 0.35f, d * 0.45f), new Vector3(0.45f, 0.35f, 0.45f), Vector3.zero, metal);
        }

        /// <summary>Mid-rise block: plinth, 2-3 storeys of glazing banding over concrete spandrels, parapet, stair head and an aerial. 9-11 parts.</summary>
        private static void BuildBlock(Transform b, Material wall, Material glass, Material concrete, Material metal)
        {
            int storeys = Random.Range(2, 4);           // 2 or 3
            float storeyH = Random.Range(2.3f, 2.7f);
            float h = storeys * storeyH;
            float w = Random.Range(6f, 9f);
            float d = Random.Range(6f, 10f);

            Prop(b, PrimitiveType.Cube, "Plinth", new Vector3(0f, 0.22f, 0f), new Vector3(w + 0.80f, 0.44f, d + 0.80f), Vector3.zero, concrete);
            Prop(b, PrimitiveType.Cube, "Shaft", new Vector3(0f, 0.44f + h * 0.5f, 0f), new Vector3(w, h, d), Vector3.zero, wall);

            for (int s = 0; s < storeys; s++)
            {
                float floorY = 0.44f + s * storeyH;
                Prop(b, PrimitiveType.Cube, "StoreyBand_" + s, new Vector3(0f, floorY + storeyH * 0.06f, 0f),
                    new Vector3(w + 0.16f, storeyH * 0.10f, d + 0.16f), Vector3.zero, concrete);
                Prop(b, PrimitiveType.Cube, "WindowBand_" + s, new Vector3(0f, floorY + storeyH * 0.60f, 0f),
                    new Vector3(w + 0.10f, storeyH * 0.38f, d + 0.10f), Vector3.zero, glass);
            }

            Prop(b, PrimitiveType.Cube, "Parapet", new Vector3(0f, 0.44f + h + 0.26f, 0f), new Vector3(w + 0.36f, 0.52f, d + 0.36f), Vector3.zero, concrete);
            // Stair head and aerial both start ON the roof deck (0.44 + h), not floating above it.
            Prop(b, PrimitiveType.Cube, "StairHead", new Vector3(w * 0.18f, 0.44f + h + 0.60f, -d * 0.16f), new Vector3(w * 0.32f, 1.20f, d * 0.30f), Vector3.zero, wall);
            Prop(b, PrimitiveType.Cylinder, "Aerial", new Vector3(-w * 0.30f, 0.44f + h + 1.00f, d * 0.24f), new Vector3(0.07f, 1.00f, 0.07f), Vector3.zero, metal);
        }

        /// <summary>Tower: plinth, slim shaft with corner piers and three glazing bands, parapet, a setback top box, a roof vent and a mast. 11 parts.</summary>
        private static void BuildTower(Transform b, Material wall, Material glass, Material concrete, Material metal)
        {
            float w = Random.Range(4.5f, 6.0f);
            float d = w * Random.Range(0.85f, 1.15f);
            float h = Random.Range(6.0f, 7.2f);

            Prop(b, PrimitiveType.Cube, "Plinth", new Vector3(0f, 0.25f, 0f), new Vector3(w + 0.90f, 0.50f, d + 0.90f), Vector3.zero, concrete);
            Prop(b, PrimitiveType.Cube, "Shaft", new Vector3(0f, 0.50f + h * 0.5f, 0f), new Vector3(w, h, d), Vector3.zero, wall);

            for (int s = 0; s < 3; s++)
            {
                Prop(b, PrimitiveType.Cube, "WindowBand_" + s, new Vector3(0f, 0.50f + h * (0.22f + s * 0.26f), 0f),
                    new Vector3(w + 0.10f, h * 0.13f, d + 0.10f), Vector3.zero, glass);
            }

            // Two diagonally opposite piers: enough to break the glazing bands vertically without
            // paying for all four.
            Prop(b, PrimitiveType.Cube, "CornerPierL", new Vector3(-w * 0.5f, 0.50f + h * 0.5f, -d * 0.5f), new Vector3(0.35f, h, 0.35f), Vector3.zero, concrete);
            Prop(b, PrimitiveType.Cube, "CornerPierR", new Vector3(w * 0.5f, 0.50f + h * 0.5f, d * 0.5f), new Vector3(0.35f, h, 0.35f), Vector3.zero, concrete);

            Prop(b, PrimitiveType.Cube, "Parapet", new Vector3(0f, 0.50f + h + 0.18f, 0f), new Vector3(w + 0.32f, 0.36f, d + 0.32f), Vector3.zero, concrete);
            Prop(b, PrimitiveType.Cube, "Setback", new Vector3(0f, 0.50f + h + 0.85f, 0f), new Vector3(w * 0.60f, 1.40f, d * 0.60f), Vector3.zero, wall);
            // Clear of the setback's 0.60w x 0.60d footprint, otherwise the vent would be buried in it.
            Prop(b, PrimitiveType.Cube, "RoofVent", new Vector3(w * 0.36f, 0.50f + h + 0.25f, -d * 0.34f), new Vector3(0.55f, 0.45f, 0.55f), Vector3.zero, metal);
            Prop(b, PrimitiveType.Cylinder, "Mast", new Vector3(0f, 0.50f + h + 2.30f, 0f), new Vector3(0.09f, 0.90f, 0.09f), Vector3.zero, metal);
        }

        // ---------------------------------------------------------------- helpers

        /// <summary>
        /// Picks a random (x,z) inside the field but outside the airbase keep-out disc, returning the
        /// point lifted onto the terrain surface. False if no valid spot was found.
        /// </summary>
        private static bool TryPickSpot(float halfExtent, float keepOut, out Vector3 point)
        {
            for (int attempt = 0; attempt < 12; attempt++)
            {
                float x = Random.Range(-halfExtent, halfExtent);
                float z = Random.Range(-halfExtent, halfExtent);
                if (new Vector2(x, z).magnitude < keepOut) continue;

                point = new Vector3(x, TerrainField.Height(x, z), z);
                return true;
            }

            point = Vector3.zero;
            return false;
        }

        /// <summary>Creates one collider-less decorative primitive under <paramref name="parent"/>.</summary>
        private static GameObject Prop(Transform parent, PrimitiveType type, string name,
            Vector3 localPosition, Vector3 localScale, Vector3 localEuler, Material material)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;

            var c = go.GetComponent<Collider>();
            if (c != null) UnityEngine.Object.Destroy(c);

            if (parent != null) go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = Quaternion.Euler(localEuler);
            go.transform.localScale = localScale;

            MaterialLibrary.Apply(go, material);
            return go;
        }
    }
}
