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
        /// Scatters trees, rocks and small buildings across the terrain, skipping the airbase area.
        /// Uses a fixed random seed so the world looks the same on every run.
        /// </summary>
        /// <param name="halfExtent">Scatter area half-extent on X and Z.</param>
        /// <param name="treeCount">Number of trees.</param>
        /// <param name="rockCount">Number of rocks.</param>
        /// <param name="buildingCount">Number of small buildings.</param>
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
            Material rockMat = MaterialLibrary.Create(new Color(0.45f, 0.45f, 0.47f), 0.1f, 0.15f);

            float keepOut = TerrainField.FlatRadius + 10f;

            for (int i = 0; i < treeCount; i++)
            {
                Vector3 p;
                if (!TryPickSpot(halfExtent, keepOut, out p)) continue;

                var tree = new GameObject("Tree_" + i);
                tree.transform.SetParent(root.transform, false);
                tree.transform.position = p;
                tree.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                float s = Random.Range(0.8f, 1.4f);
                tree.transform.localScale = new Vector3(s, s * Random.Range(0.9f, 1.3f), s);

                Material foliage = MaterialLibrary.Create(
                    new Color(Random.Range(0.10f, 0.20f), Random.Range(0.30f, 0.45f), Random.Range(0.10f, 0.18f)), 0f, 0.08f);

                Prop(tree.transform, PrimitiveType.Cylinder, "Trunk", new Vector3(0f, 1.2f, 0f), new Vector3(0.25f, 1.2f, 0.25f), Vector3.zero, trunkMat);
                Prop(tree.transform, PrimitiveType.Sphere, "Foliage_0", new Vector3(0f, 2.6f, 0f), new Vector3(2.2f, 2.2f, 2.2f), Vector3.zero, foliage);
                Prop(tree.transform, PrimitiveType.Sphere, "Foliage_1", new Vector3(0f, 3.9f, 0f), new Vector3(1.6f, 1.6f, 1.6f), Vector3.zero, foliage);
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

                float w = Random.Range(4f, 9f);
                float h = Random.Range(3f, 8f);
                float d = Random.Range(4f, 9f);
                Material wall = MaterialLibrary.Create(
                    new Color(Random.Range(0.45f, 0.66f), Random.Range(0.42f, 0.60f), Random.Range(0.36f, 0.52f)), 0f, 0.12f);

                var building = Prop(root.transform, PrimitiveType.Cube, "Building_" + i, Vector3.zero,
                    new Vector3(w, h, d), new Vector3(0f, Random.Range(0f, 360f), 0f), wall);
                building.transform.position = p + new Vector3(0f, h * 0.5f, 0f);
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
