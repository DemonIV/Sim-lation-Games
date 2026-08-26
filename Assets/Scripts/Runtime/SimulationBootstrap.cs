using System.Collections.Generic;
using UnityEngine;

namespace Sim.Runtime
{
    /// <summary>
    /// Builds a fully runnable demo scene from primitives at runtime, so the project runs without a
    /// hand-authored .unity scene.
    ///
    /// USAGE: Create a new empty scene, add an empty GameObject, attach this component to it, and
    /// press Play. The camera, lighting, ground, İHA/SİHA drones, patrol routes, and hostile targets
    /// are all created automatically. The generated primitives are placeholders — swap in real 3D
    /// models and materials later without touching the Core logic.
    ///
    /// This is a GAME / EDUCATIONAL simulation with abstract, gamified mechanics.
    /// </summary>
    public class SimulationBootstrap : MonoBehaviour
    {
        private void Awake()
        {
            EnsureCameraAndLight();
            CreateGround();

            // Two recon İHA drones (blue) with rectangular patrol routes.
            SpawnIha("IHA_1", new Vector3(-20f, 10f, -20f), RectangleRoute(new Vector3(-20f, 10f, -20f), 30f, 25f), Color.blue);
            SpawnIha("IHA_2", new Vector3(20f, 12f, 20f), RectangleRoute(new Vector3(20f, 12f, 20f), 25f, 30f), new Color(0.2f, 0.5f, 1f));

            // One armed SİHA drone (red) with its own patrol route.
            SpawnSiha("SIHA_1", new Vector3(0f, 14f, -30f), RectangleRoute(new Vector3(0f, 14f, -30f), 40f, 20f), new Color(1f, 0.35f, 0.2f));

            // Three hostile targets (grey cubes) scattered on the field.
            // Hostile_2 carries a noise jammer to demonstrate EW: it is harder to detect at range.
            SpawnHostile("Hostile_1", new Vector3(15f, 1f, 10f), false);
            SpawnHostile("Hostile_2", new Vector3(-25f, 1f, 5f), true);
            SpawnHostile("Hostile_3", new Vector3(5f, 1f, -15f), false);
        }

        /// <summary>Creates a main camera looking down over the field plus a directional light, if none exist.</summary>
        private void EnsureCameraAndLight()
        {
            if (Camera.main == null)
            {
                var camGo = new GameObject("Main Camera");
                camGo.tag = "MainCamera";
                var cam = camGo.AddComponent<Camera>();
                cam.transform.position = new Vector3(0f, 60f, -80f);
                cam.transform.LookAt(new Vector3(0f, 0f, 0f));
                cam.farClipPlane = 1000f;
            }

            if (FindObjectOfType<Light>() == null)
            {
                var lightGo = new GameObject("Directional Light");
                var light = lightGo.AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1f;
                lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            }
        }

        /// <summary>Creates a large ground plane at the origin.</summary>
        private void CreateGround()
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.position = Vector3.zero;
            ground.transform.localScale = new Vector3(20f, 1f, 20f);
            ApplyColor(ground, new Color(0.25f, 0.35f, 0.25f));
        }

        /// <summary>Spawns a recon İHA drone (blue capsule) with a patrol route and friendly Targetable.</summary>
        private void SpawnIha(string name, Vector3 position, List<Transform> route, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = name;
            go.transform.position = position;
            ApplyColor(go, color);
            MarkFriendly(go);

            var ctrl = go.AddComponent<IhaController>();
            AssignRoute(ctrl, route);

            // Realistic radar sensor (RCS^0.25 range, jamming, alpha-beta tracking).
            // hostileFaction defaults to 1, matching the grey-cube targets.
            go.AddComponent<RadarSensor>();
        }

        /// <summary>Spawns an armed SİHA drone (red capsule) with a patrol route and friendly Targetable.</summary>
        private void SpawnSiha(string name, Vector3 position, List<Transform> route, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = name;
            go.transform.position = position;
            ApplyColor(go, color);
            MarkFriendly(go);

            var ctrl = go.AddComponent<SihaController>();
            AssignRoute(ctrl, route);

            // Realistic radar sensor (hostileFaction defaults to 1).
            go.AddComponent<RadarSensor>();
        }

        /// <summary>Spawns a hostile target (grey cube, Faction = 1). Optionally fits a noise jammer.</summary>
        private void SpawnHostile(string name, Vector3 position, bool withJammer)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.position = position;
            go.transform.localScale = new Vector3(2f, 2f, 2f);
            ApplyColor(go, new Color(0.5f, 0.5f, 0.5f));

            var targetable = go.AddComponent<Targetable>();
            targetable.Faction = 1;
            targetable.MaxHealth = 100f;

            // Aspect-dependent radar signature so hostile RadarSensors see angle-varying RCS.
            go.AddComponent<RcsComponent>();

            // One hostile carries onboard noise jamming to demonstrate EW range degradation.
            if (withJammer) go.AddComponent<Jammer>();
        }

        /// <summary>Marks a drone GameObject as a friendly Targetable (Faction = 0).</summary>
        private void MarkFriendly(GameObject go)
        {
            var targetable = go.AddComponent<Targetable>();
            targetable.Faction = 0;
            targetable.MaxHealth = 100f;
        }

        /// <summary>Injects the patrol waypoint transforms into a controller via its serialized private field.</summary>
        private void AssignRoute(IhaController ctrl, List<Transform> route)
        {
            var field = typeof(IhaController).GetField("patrolWaypoints",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (field != null) field.SetValue(ctrl, route);
        }

        /// <summary>Builds four empty waypoint GameObjects arranged in a rectangle centred on 'center'.</summary>
        private List<Transform> RectangleRoute(Vector3 center, float width, float depth)
        {
            var offsets = new[]
            {
                new Vector3(-width * 0.5f, 0f, -depth * 0.5f),
                new Vector3( width * 0.5f, 0f, -depth * 0.5f),
                new Vector3( width * 0.5f, 0f,  depth * 0.5f),
                new Vector3(-width * 0.5f, 0f,  depth * 0.5f),
            };

            var list = new List<Transform>();
            for (int i = 0; i < offsets.Length; i++)
            {
                var wp = new GameObject($"WP_{center.x}_{center.z}_{i}");
                wp.transform.position = center + offsets[i];
                list.Add(wp.transform);
            }
            return list;
        }

        /// <summary>Applies a solid color to a primitive, preferring the Standard shader with fallbacks.</summary>
        private void ApplyColor(GameObject go, Color color)
        {
            var renderer = go.GetComponent<Renderer>();
            if (renderer == null) return;

            Shader shader = Shader.Find("Standard");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = renderer.sharedMaterial != null ? renderer.sharedMaterial.shader : null;

            var mat = shader != null ? new Material(shader) : new Material(renderer.material);
            mat.color = color;
            // URP/Lit uses _BaseColor; setting it is harmless for the Standard shader.
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            renderer.material = mat;
        }
    }
}
