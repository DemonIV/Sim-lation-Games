using UnityEngine;

namespace Sim.Runtime
{
    /// <summary>
    /// Builds recognisable vehicle silhouettes out of Unity primitives and parents them under a single
    /// child named "Model" of the unit root. Purely cosmetic:
    /// <list type="bullet">
    ///   <item>every generated part has its Collider destroyed, so models never add physics;</item>
    ///   <item>the unit root keeps its own collider and transform — gameplay is untouched;</item>
    ///   <item>local +Z is forward, matching how the controllers orient the root.</item>
    /// </list>
    /// </summary>
    public static class VehicleModelBuilder
    {
        // ---------------------------------------------------------------- public API

        /// <summary>Slender high-aspect recon UAV (İHA). Returns the "Model" root transform.</summary>
        public static Transform BuildReconUav(Transform root, Color primary)
        {
            Transform model = CreateModelRoot(root);
            if (model == null) return null;

            Material body = Body(primary);
            Material trim = Trim(primary);
            Material dark = DarkMetal();

            Part(model, PrimitiveType.Cylinder, "Fuselage", new Vector3(0f, 0f, 0f), new Vector3(0.35f, 1.6f, 0.35f), new Vector3(90f, 0f, 0f), body);
            Part(model, PrimitiveType.Sphere, "Nose", new Vector3(0f, 0f, 1.55f), new Vector3(0.6f, 0.5f, 0.8f), Vector3.zero, body);
            Part(model, PrimitiveType.Sphere, "SensorTurret", new Vector3(0f, -0.35f, 1.25f), new Vector3(0.45f, 0.45f, 0.45f), Vector3.zero, dark);

            Part(model, PrimitiveType.Cube, "Wing", new Vector3(0f, 0.15f, -0.1f), new Vector3(7.0f, 0.10f, 0.75f), Vector3.zero, trim);
            Part(model, PrimitiveType.Cube, "WingtipFinL", new Vector3(-3.5f, 0.35f, -0.1f), new Vector3(0.08f, 0.45f, 0.5f), Vector3.zero, trim);
            Part(model, PrimitiveType.Cube, "WingtipFinR", new Vector3(3.5f, 0.35f, -0.1f), new Vector3(0.08f, 0.45f, 0.5f), Vector3.zero, trim);

            Part(model, PrimitiveType.Cylinder, "TailBoom", new Vector3(0f, 0f, -2.0f), new Vector3(0.15f, 0.5f, 0.15f), new Vector3(90f, 0f, 0f), body);
            Part(model, PrimitiveType.Cube, "VTailL", new Vector3(-0.35f, 0.45f, -2.3f), new Vector3(0.09f, 1.1f, 0.6f), new Vector3(0f, 0f, 35f), trim);
            Part(model, PrimitiveType.Cube, "VTailR", new Vector3(0.35f, 0.45f, -2.3f), new Vector3(0.09f, 1.1f, 0.6f), new Vector3(0f, 0f, -35f), trim);

            Part(model, PrimitiveType.Cylinder, "PropHub", new Vector3(0f, 0f, -2.5f), new Vector3(0.18f, 0.08f, 0.18f), new Vector3(90f, 0f, 0f), dark);
            // "Propeller" is spun around its local Z by the animation pass (part 2).
            Part(model, PrimitiveType.Cube, "Propeller", new Vector3(0f, 0f, -2.55f), new Vector3(0.12f, 1.8f, 0.04f), Vector3.zero, dark);

            return model;
        }

        /// <summary>Armed UAV (SİHA): the recon airframe plus underwing pylons and munitions.</summary>
        public static Transform BuildArmedUav(Transform root, Color primary)
        {
            Transform model = BuildReconUav(root, primary);
            if (model == null) return null;

            Material trim = Trim(primary);
            Material dark = DarkMetal();

            Part(model, PrimitiveType.Cube, "PylonL", new Vector3(-1.6f, -0.05f, -0.1f), new Vector3(0.1f, 0.3f, 0.15f), Vector3.zero, trim);
            Part(model, PrimitiveType.Cube, "PylonR", new Vector3(1.6f, -0.05f, -0.1f), new Vector3(0.1f, 0.3f, 0.15f), Vector3.zero, trim);
            Part(model, PrimitiveType.Capsule, "MunitionL", new Vector3(-1.6f, -0.35f, -0.1f), new Vector3(0.18f, 0.5f, 0.18f), new Vector3(90f, 0f, 0f), dark);
            Part(model, PrimitiveType.Capsule, "MunitionR", new Vector3(1.6f, -0.35f, -0.1f), new Vector3(0.18f, 0.5f, 0.18f), new Vector3(90f, 0f, 0f), dark);

            return model;
        }

        /// <summary>Aggressive delta-wing enemy jet with a glowing exhaust.</summary>
        public static Transform BuildEnemyFighter(Transform root, Color primary)
        {
            Transform model = CreateModelRoot(root);
            if (model == null) return null;

            Material body = Body(primary);
            Material trim = Trim(primary);

            Part(model, PrimitiveType.Capsule, "Fuselage", new Vector3(0f, 0f, 0f), new Vector3(0.45f, 1.3f, 0.45f), new Vector3(90f, 0f, 0f), body);
            Part(model, PrimitiveType.Sphere, "NoseCone", new Vector3(0f, 0f, 1.5f), new Vector3(0.5f, 0.45f, 0.9f), Vector3.zero, body);

            Part(model, PrimitiveType.Cube, "DeltaWing", new Vector3(0f, -0.05f, -0.35f), new Vector3(4.2f, 0.09f, 1.7f), new Vector3(0f, 0f, 0f), trim);
            Part(model, PrimitiveType.Cube, "SweepL", new Vector3(-1.5f, -0.05f, 0.35f), new Vector3(1.6f, 0.09f, 1.0f), new Vector3(0f, -25f, 0f), trim);
            Part(model, PrimitiveType.Cube, "SweepR", new Vector3(1.5f, -0.05f, 0.35f), new Vector3(1.6f, 0.09f, 1.0f), new Vector3(0f, 25f, 0f), trim);

            Part(model, PrimitiveType.Cube, "TailFinL", new Vector3(-0.6f, 0.5f, -1.5f), new Vector3(0.08f, 0.9f, 0.6f), Vector3.zero, trim);
            Part(model, PrimitiveType.Cube, "TailFinR", new Vector3(0.6f, 0.5f, -1.5f), new Vector3(0.08f, 0.9f, 0.6f), Vector3.zero, trim);

            Material glow = MaterialLibrary.Create(new Color(1f, 0.45f, 0.15f), 0f, 0.6f, new Color(2f, 0.7f, 0.15f));
            Part(model, PrimitiveType.Sphere, "EngineGlow", new Vector3(0f, 0f, -1.8f), new Vector3(0.35f, 0.35f, 0.35f), Vector3.zero, glow);

            return model;
        }

        /// <summary>Long-range SAM battery: box hull, turret, four canted tubes and a radar dish.</summary>
        public static Transform BuildSamSite(Transform root, Color primary)
        {
            Transform model = CreateModelRoot(root);
            if (model == null) return null;

            Material body = Body(primary);
            Material trim = Trim(primary);
            Material dark = DarkMetal();

            Part(model, PrimitiveType.Cube, "Base", new Vector3(0f, 0.45f, 0f), new Vector3(3.2f, 0.9f, 3.2f), Vector3.zero, body);

            // "Turret" is an EMPTY, unscaled pivot animated by TurretVisual; the visible cylinder
            // ("TurretBody") and the missile tubes hang off it so they slew together. The pivot has to
            // stay scale-free — the body's non-uniform (0.9, 0.5, 0.9) would otherwise squash the tubes.
            Transform turret = Pivot(model, "Turret", new Vector3(0f, 1.2f, 0f));
            Part(turret, PrimitiveType.Cylinder, "TurretBody", Vector3.zero, new Vector3(0.9f, 0.5f, 0.9f), Vector3.zero, trim);

            Part(turret, PrimitiveType.Cube, "TubeLF", new Vector3(-0.35f, 0.7f, 0.25f), new Vector3(0.22f, 1.6f, 0.22f), new Vector3(-25f, 0f, 0f), dark);
            Part(turret, PrimitiveType.Cube, "TubeRF", new Vector3(0.35f, 0.7f, 0.25f), new Vector3(0.22f, 1.6f, 0.22f), new Vector3(-25f, 0f, 0f), dark);
            Part(turret, PrimitiveType.Cube, "TubeLB", new Vector3(-0.35f, 0.7f, -0.25f), new Vector3(0.22f, 1.6f, 0.22f), new Vector3(-25f, 0f, 0f), dark);
            Part(turret, PrimitiveType.Cube, "TubeRB", new Vector3(0.35f, 0.7f, -0.25f), new Vector3(0.22f, 1.6f, 0.22f), new Vector3(-25f, 0f, 0f), dark);

            // "Radar" stays on the model root — it sweeps independently of the turret.
            Part(model, PrimitiveType.Cylinder, "Radar", new Vector3(0f, 2.1f, -1.0f), new Vector3(1.1f, 0.06f, 1.1f), new Vector3(60f, 0f, 0f), trim);

            return model;
        }

        /// <summary>Short-range AAA piece: low hull, turret and twin elevated barrels.</summary>
        public static Transform BuildAaaSite(Transform root, Color primary)
        {
            Transform model = CreateModelRoot(root);
            if (model == null) return null;

            Material body = Body(primary);
            Material trim = Trim(primary);
            Material dark = DarkMetal();

            Part(model, PrimitiveType.Cube, "Base", new Vector3(0f, 0.35f, 0f), new Vector3(2.4f, 0.7f, 2.4f), Vector3.zero, body);

            // Same unscaled-pivot pattern as the SAM site: the barrels must slew with the turret.
            Transform turret = Pivot(model, "Turret", new Vector3(0f, 0.9f, 0f));
            Part(turret, PrimitiveType.Cylinder, "TurretBody", Vector3.zero, new Vector3(0.8f, 0.4f, 0.8f), Vector3.zero, trim);
            Part(turret, PrimitiveType.Cylinder, "BarrelL", new Vector3(-0.18f, 0.35f, 0.6f), new Vector3(0.1f, 0.9f, 0.1f), new Vector3(75f, 0f, 0f), dark);
            Part(turret, PrimitiveType.Cylinder, "BarrelR", new Vector3(0.18f, 0.35f, 0.6f), new Vector3(0.1f, 0.9f, 0.1f), new Vector3(75f, 0f, 0f), dark);

            return model;
        }

        /// <summary>Plain ground objective: a four-wheeled utility vehicle.</summary>
        public static Transform BuildGroundTarget(Transform root, Color primary)
        {
            Transform model = CreateModelRoot(root);
            if (model == null) return null;

            Material body = Body(primary);
            Material trim = Trim(primary);
            Material dark = DarkMetal();

            Part(model, PrimitiveType.Cube, "Hull", new Vector3(0f, 0.6f, 0f), new Vector3(2.6f, 0.8f, 1.5f), Vector3.zero, body);
            Part(model, PrimitiveType.Cube, "Cabin", new Vector3(0.4f, 1.2f, 0f), new Vector3(1.1f, 0.6f, 1.3f), Vector3.zero, trim);

            Part(model, PrimitiveType.Cylinder, "WheelFL", new Vector3(-0.9f, 0.3f, 0.75f), new Vector3(0.35f, 0.12f, 0.35f), new Vector3(0f, 0f, 90f), dark);
            Part(model, PrimitiveType.Cylinder, "WheelFR", new Vector3(0.9f, 0.3f, 0.75f), new Vector3(0.35f, 0.12f, 0.35f), new Vector3(0f, 0f, 90f), dark);
            Part(model, PrimitiveType.Cylinder, "WheelBL", new Vector3(-0.9f, 0.3f, -0.75f), new Vector3(0.35f, 0.12f, 0.35f), new Vector3(0f, 0f, 90f), dark);
            Part(model, PrimitiveType.Cylinder, "WheelBR", new Vector3(0.9f, 0.3f, -0.75f), new Vector3(0.35f, 0.12f, 0.35f), new Vector3(0f, 0f, 90f), dark);

            return model;
        }

        /// <summary>Disables the root primitive's MeshRenderer (its collider is deliberately kept).</summary>
        public static void HideRootMesh(GameObject root)
        {
            if (root == null) return;
            var renderer = root.GetComponent<MeshRenderer>();
            if (renderer != null) renderer.enabled = false;
        }

        // ---------------------------------------------------------------- helpers

        /// <summary>
        /// Creates (or reuses) the "Model" child. Its local scale cancels the root's own scale, so the
        /// silhouettes keep their designed proportions even when the root primitive was scaled by the
        /// spawner — the root's collider size is left exactly as it was.
        /// </summary>
        private static Transform CreateModelRoot(Transform root)
        {
            if (root == null) return null;

            Transform existing = root.Find("Model");
            if (existing != null) return existing;

            var go = new GameObject("Model");
            go.transform.SetParent(root, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Reciprocal(root.localScale);
            return go.transform;
        }

        /// <summary>Component-wise 1/v, guarding against degenerate zero scales.</summary>
        private static Vector3 Reciprocal(Vector3 v)
        {
            return new Vector3(
                Mathf.Abs(v.x) < 1e-4f ? 1f : 1f / v.x,
                Mathf.Abs(v.y) < 1e-4f ? 1f : 1f / v.y,
                Mathf.Abs(v.z) < 1e-4f ? 1f : 1f / v.z);
        }

        /// <summary>
        /// Creates an empty, unscaled pivot (no renderer, no collider) under <paramref name="parent"/>.
        /// Used for rotating assemblies whose visible parts carry a non-uniform scale, which would
        /// otherwise shear their children.
        /// </summary>
        private static Transform Pivot(Transform parent, string name, Vector3 localPosition)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            return go.transform;
        }

        /// <summary>Spawns one collider-less primitive part under the model root.</summary>
        private static GameObject Part(Transform parent, PrimitiveType type, string name,
            Vector3 localPosition, Vector3 localScale, Vector3 localEuler, Material material)
        {
            var part = GameObject.CreatePrimitive(type);
            part.name = name;

            // Models must never add physics.
            var c = part.GetComponent<Collider>();
            if (c != null) UnityEngine.Object.Destroy(c);

            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localRotation = Quaternion.Euler(localEuler);
            part.transform.localScale = localScale;

            MaterialLibrary.Apply(part, material);
            return part;
        }

        /// <summary>Main body material for a unit's primary colour.</summary>
        private static Material Body(Color primary)
        {
            return MaterialLibrary.Create(primary, 0.3f, 0.45f);
        }

        /// <summary>Darker, desaturated variant used for wings, tails and secondary structure.</summary>
        private static Material Trim(Color primary)
        {
            float grey = primary.r * 0.299f + primary.g * 0.587f + primary.b * 0.114f;
            var c = new Color(
                Mathf.Lerp(primary.r, grey, 0.4f) * 0.72f,
                Mathf.Lerp(primary.g, grey, 0.4f) * 0.72f,
                Mathf.Lerp(primary.b, grey, 0.4f) * 0.72f,
                primary.a);
            return MaterialLibrary.Create(c, 0.35f, 0.35f);
        }

        /// <summary>Dark metallic material for sensors, barrels, munitions and wheels.</summary>
        private static Material DarkMetal()
        {
            return MaterialLibrary.Create(new Color(0.16f, 0.17f, 0.19f), 0.6f, 0.8f);
        }
    }
}
