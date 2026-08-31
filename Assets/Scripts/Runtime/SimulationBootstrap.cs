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
            // Defensive: drop any stale registry entries left over from a previous scene load/restart.
            TargetRegistry.Clear();

            EnsureCameraAndLight();
            CreateGround();

            // Two recon İHA drones (blue) with rectangular patrol routes.
            SpawnIha("IHA_1", new Vector3(-20f, 10f, -20f), RectangleRoute(new Vector3(-20f, 10f, -20f), 30f, 25f), Color.blue);
            SpawnIha("IHA_2", new Vector3(20f, 12f, 20f), RectangleRoute(new Vector3(20f, 12f, 20f), 25f, 30f), new Color(0.2f, 0.5f, 1f));

            // One armed SİHA drone (red) with its own patrol route.
            SpawnSiha("SIHA_1", new Vector3(0f, 14f, -30f), RectangleRoute(new Vector3(0f, 14f, -30f), 40f, 20f), new Color(1f, 0.35f, 0.2f));

            // Wave-based scenario. The ScenarioController (created after the drones) now owns ALL enemy
            // spawning — escalating waves of three hostile archetypes (plain target / SAM / AAA) — and
            // decides win/lose. The old fixed enemy spawns were removed in favour of this system.
            new GameObject("ScenarioController").AddComponent<ScenarioController>();

            // Tactical layer (M1). Created LAST so the director's Start() counts every hostile that
            // was just spawned. The SimulationDirector tracks mission progress/score; the Hud draws
            // an IMGUI overlay reading that state.
            var director = new GameObject("SimulationDirector").AddComponent<SimulationDirector>();
            director.gameObject.AddComponent<Hud>();

            // Global keyboard controls: R restart, P pause, +/- time scale.
            director.gameObject.AddComponent<GameControls>();

            // Mission-select briefing screen. Added AFTER the ScenarioController above so its Start()
            // can find the controller it has to release with BeginMission(). It holds the sim paused
            // until the player picks a mission, and reopens on M.
            director.gameObject.AddComponent<ScenarioMenu>();

            // Pilot mode: lets the player take over one friendly drone (C) and fly it by hand.
            // Lives on the manager object, never on a drone.
            director.gameObject.AddComponent<PlayerDroneController>();

            // Attach a free-fly / drone-follow spectator camera to the main camera (created above in
            // EnsureCameraAndLight), if it doesn't already have one.
            if (Camera.main != null && Camera.main.GetComponent<CameraRig>() == null)
                Camera.main.gameObject.AddComponent<CameraRig>();
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

            if (FindAnyObjectByType<Light>() == null)
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

            // Light defensive gun, added before the controller so its Start() picks it up.
            // magazineSize, roundsPerSecond, effectiveRange, dispersionDeg, damagePerRound
            go.AddComponent<GunTurret>().Configure(200, 8f, 45f, 3f, 3f);

            // Flare/chaff dispenser, added before the controller so its Start() picks it up.
            go.AddComponent<CountermeasureDispenser>();

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

            // Stronger gun than the recon İHA, added before the controller so its Start() picks it up.
            // magazineSize, roundsPerSecond, effectiveRange, dispersionDeg, damagePerRound
            go.AddComponent<GunTurret>().Configure(300, 10f, 60f, 2.5f, 4.5f);

            // Flare/chaff dispenser, added before the controller so its Start() picks it up.
            go.AddComponent<CountermeasureDispenser>();

            var ctrl = go.AddComponent<SihaController>();
            AssignRoute(ctrl, route);

            // Realistic radar sensor (hostileFaction defaults to 1).
            go.AddComponent<RadarSensor>();
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
