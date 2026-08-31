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
        /// <summary>
        /// The bootstrap component currently living in the scene, so other systems can reach it
        /// without a scene scan. Set in <see cref="Awake"/>, cleared in <see cref="OnDestroy"/>.
        /// </summary>
        public static SimulationBootstrap Instance { get; private set; }

        /// <summary>
        /// Parent of EVERYTHING <see cref="Build"/> generates (terrain, airbase, props, drones,
        /// waypoints, managers). Kept as a single root so the whole generated world can be torn down
        /// and rebuilt in one step. The main camera and the sun deliberately stay OUTSIDE it.
        /// </summary>
        public static Transform Root { get; private set; }

        private void Awake()
        {
            Instance = this;
            Build();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>
        /// Restarts the mission IN PLACE: tears the generated world down and builds it again.
        ///
        /// <para>
        /// This replaces the old <c>SceneManager.LoadScene</c> restart, which could not work: the
        /// project ships no .unity scene asset, so the runtime scene has no build index and reloading
        /// it threw (finding B-01). Rebuilding under the <see cref="Root"/> object achieves the same
        /// clean slate without touching Build Settings.
        /// </para>
        ///
        /// <para>
        /// Statics that must NOT survive are reset here (registry, live munitions, effect budget,
        /// time scale). Statics that MUST survive are deliberately left alone:
        /// <see cref="ScenarioController.SelectedKind"/> (the player's mission choice) and
        /// <see cref="ScenarioMenu"/>'s auto-begin flag (set by a mid-mission mission switch just
        /// before calling this, and consumed by the freshly built menu so the briefing does not
        /// wrongly reappear).
        /// </para>
        /// </summary>
        public void Rebuild()
        {
            // 1. Hand any piloted drone back to its AI before the drone itself goes away.
            var pilot = FindAnyObjectByType<PlayerDroneController>();
            if (pilot != null) pilot.ReleasePlayerControl();

            // 2. Tear the old world down. Deactivating first stops every Update/OnGUI on it
            //    immediately — Destroy alone is deferred to the end of the frame, which would let the
            //    dying objects run one more tick alongside the freshly built ones.
            if (Root != null)
            {
                GameObject old = Root.gameObject;
                Root = null;
                old.SetActive(false);
                Destroy(old);
            }

            // 3. Munitions are spawned parentless (they must not follow their launcher), so they
            //    outlive the root and have to be cleaned up explicitly.
            for (int i = GuidedMunition.Active.Count - 1; i >= 0; i--)
            {
                GuidedMunition m = GuidedMunition.Active[i];
                if (m == null) continue;
                m.gameObject.SetActive(false);
                Destroy(m.gameObject);
            }
            GuidedMunition.Active.Clear();

            // 4. Reset the remaining global state. Build() clears the registry again on its way in.
            TargetRegistry.Clear();
            VfxLibrary.ResetBudget();
            Time.timeScale = 1f;

            Build();
        }

        /// <summary>
        /// Creates the whole generated scene under a fresh "Simulation" root. Split out of
        /// <see cref="Awake"/> so it can be re-invoked to restart the mission in place.
        /// </summary>
        private void Build()
        {
            // Defensive: drop any stale registry entries left over from a previous scene load/restart.
            TargetRegistry.Clear();

            // One root for the generated world. Camera and sun are created outside it on purpose:
            // they must survive a rebuild.
            var rootGo = new GameObject("Simulation");
            rootGo.transform.position = Vector3.zero;
            Root = rootGo.transform;

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
            var scenarioGo = new GameObject("ScenarioController");
            scenarioGo.transform.SetParent(Root, false);
            scenarioGo.AddComponent<ScenarioController>();

            // Tactical layer (M1). The SimulationDirector tracks mission stats/score (win/lose belongs
            // to the ScenarioController above); the Hud draws an IMGUI overlay reading that state.
            // Creation order no longer matters for counting: hostiles are spawned wave by wave from
            // ScenarioController.Update, so the field is empty whatever runs first.
            var directorGo = new GameObject("SimulationDirector");
            directorGo.transform.SetParent(Root, false);
            var director = directorGo.AddComponent<SimulationDirector>();
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

        /// <summary>
        /// Creates a main camera looking down over the field plus a directional light, if none exist.
        /// Both live OUTSIDE the Simulation root, so a rebuild keeps them — which is also why both
        /// creations are guarded: on the second pass the existing camera/sun are simply reused.
        /// </summary>
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

            Light sun = FindSun();
            if (sun == null)
            {
                var lightGo = new GameObject("Directional Light");
                sun = lightGo.AddComponent<Light>();
                sun.type = LightType.Directional;
                sun.intensity = 1f;
                lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            }

            // Sky, fog, ambient light and sun tuning for the procedural landscape (cosmetic only).
            EnvironmentBuilder.ApplyAtmosphere(Camera.main, sun);
        }

        /// <summary>
        /// The scene's directional light, or null. Explicitly typed: effect flashes spawn POINT lights,
        /// so a plain "is there any Light?" test could mistake a live explosion for the sun.
        /// </summary>
        private static Light FindSun()
        {
            Light[] lights = FindObjectsByType<Light>(FindObjectsSortMode.None);
            for (int i = 0; i < lights.Length; i++)
            {
                Light l = lights[i];
                if (l == null) continue;
                if (l.type == LightType.Directional) return l;
            }
            return null;
        }

        /// <summary>Creates the procedural landscape: terrain mesh, airbase pad and scenery props.</summary>
        private void CreateGround()
        {
            Parent(EnvironmentBuilder.BuildTerrain());
            Parent(EnvironmentBuilder.BuildAirbase(Vector3.zero));
            Parent(EnvironmentBuilder.ScatterProps(150f));
        }

        /// <summary>Moves a generated object under the Simulation root, keeping its world pose.</summary>
        private static void Parent(GameObject go)
        {
            if (go == null) return;
            if (Root == null) return;
            go.transform.SetParent(Root, true);
        }

        /// <summary>Spawns a recon İHA drone (blue capsule) with a patrol route and friendly Targetable.</summary>
        private void SpawnIha(string name, Vector3 position, List<Transform> route, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = name;
            go.transform.position = position;
            Parent(go);
            ApplyColor(go, color);
            // Cosmetic: hide the placeholder capsule and build a recon-UAV silhouette in its place.
            VehicleModelBuilder.HideRootMesh(go);
            VehicleModelBuilder.BuildReconUav(go.transform, color);
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

            // Cosmetic: spinning propeller and roll-into-the-turn on the "Model" child only.
            go.AddComponent<PropellerSpinner>();
            go.AddComponent<BankingVisual>();
        }

        /// <summary>Spawns an armed SİHA drone (red capsule) with a patrol route and friendly Targetable.</summary>
        private void SpawnSiha(string name, Vector3 position, List<Transform> route, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = name;
            go.transform.position = position;
            Parent(go);
            ApplyColor(go, color);
            // Cosmetic: hide the placeholder capsule and build an armed-UAV silhouette in its place.
            VehicleModelBuilder.HideRootMesh(go);
            VehicleModelBuilder.BuildArmedUav(go.transform, color);
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

            // Cosmetic: spinning propeller and roll-into-the-turn on the "Model" child only.
            go.AddComponent<PropellerSpinner>();
            go.AddComponent<BankingVisual>();
        }

        /// <summary>Marks a drone GameObject as a friendly Targetable (Faction = 0).</summary>
        private void MarkFriendly(GameObject go)
        {
            var targetable = go.AddComponent<Targetable>();
            targetable.Faction = 0;
            targetable.MaxHealth = 100f;

            // Cosmetic: smoke/fire once the drone is badly damaged.
            go.AddComponent<DamageVisuals>();
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
                Parent(wp);
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
