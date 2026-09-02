using System.Collections.Generic;
using UnityEngine;
using Sim.Core;

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
        /// <see cref="ScenarioController.SelectedKind"/> (the player's mission choice),
        /// <see cref="ScenarioController.SelectedAircraftId"/> (the player's aircraft choice),
        /// <see cref="CampaignSession"/> (level progress, money and the garage — it is the campaign
        /// itself, and is deliberately NOT reloaded here) and <see cref="ScenarioMenu"/>'s
        /// auto-begin flag (set by a mid-mission mission switch just
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

            // Two recon İHA drones (blue) with rectangular patrol routes. Their spawn altitude IS
            // their cruise altitude (IhaController holds BasePosition.y), so it has to clear the
            // skyline: 18 / 20 m instead of the old 10 / 12 m, which sat inside the 14 m scenery
            // ceiling. See Sim.Core.FlightEnvelope for where those numbers come from.
            SpawnIha("IHA_1", new Vector3(-20f, 18f, -20f), RectangleRoute(new Vector3(-20f, 18f, -20f), 30f, 25f), Color.blue);
            SpawnIha("IHA_2", new Vector3(20f, 20f, 20f), RectangleRoute(new Vector3(20f, 20f, 20f), 25f, 30f), new Color(0.2f, 0.5f, 1f));

            // The PLAYER's aircraft slot, in the place the armed SİHA always held. WHICH airframe
            // stands here — and every performance number on it — comes from the archetype picked on
            // the mission-select screen WITH the hangar upgrades folded in
            // (see CampaignSession.PlayerProfile). AircraftUpgrades.Apply is a pure function of the
            // CATALOGUE profile, so re-reading it on every Build() can never compound: a rebuild
            // yields the same aircraft, not a doubly upgraded one. With the default SİHA profile and
            // an empty garage this spawns exactly the drone it always did. The two recon İHA above
            // are untouched AI wingmen: no profile is ever applied to them.
            IhaController playerAircraft = SpawnPlayerAircraft(CampaignSession.PlayerProfile);

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

            // Audio front desk: master volume key (N), the AudioListener the hand-built camera lacks,
            // and the pooled AudioSources every sound in the game is played through. Its SETTING is
            // static, so a rebuild cannot un-mute the game.
            director.gameObject.AddComponent<AudioDirector>();

            // Mission-select briefing screen. Added AFTER the ScenarioController above so its Start()
            // can find the controller it has to release with BeginMission(). It holds the sim paused
            // until the player picks a mission, and reopens on M.
            director.gameObject.AddComponent<ScenarioMenu>();

            // Pilot mode: lets the player take over one friendly drone (C) and fly it by hand.
            // Lives on the manager object, never on a drone. The aircraft built from the selected
            // profile is offered first, so C puts the player in the aircraft they picked.
            var playerPilot = director.gameObject.AddComponent<PlayerDroneController>();
            playerPilot.SetPreferredAircraft(playerAircraft);

            // Cockpit missile-warning tone, driven by the same two flags as the HUD's FÜZE! band and
            // KAÇIŞ MANEVRASI row. Added after the pilot component so its Start() finds it.
            director.gameObject.AddComponent<MissileWarningAudio>();

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

            // Radar signature, so hostile sensors see these wingmen for what they are: recon İHA
            // airframes, i.e. the catalogue's smallest signature. Added BEFORE the Targetable so the
            // registry's lazy component lookup finds it on the very first scan. Hostiles have carried
            // an RcsComponent since the scenario system was written; this is the friendly half that
            // was missing, and it makes the two sides symmetric.
            go.AddComponent<RcsComponent>()
              .Configure(AircraftCatalog.GetOrDefault(AircraftCatalog.IhaId).RadarSignature);

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

            // Cosmetic: looping engine note whose pitch/volume follow airspeed. Heard positionally,
            // since these two are AI wingmen the player watches rather than flies.
            go.AddComponent<EngineAudio>();
        }

        /// <summary>
        /// Spawns the PLAYER's aircraft from the archetype picked on the mission-select screen: flight
        /// envelope, fuel, gun, missiles, sensors, RADAR SIGNATURE and hit points all come from the
        /// <see cref="AircraftProfile"/>, applied through the same serialized fields / Configure calls
        /// the fixed spawns always used.
        ///
        /// <para>
        /// It stands in the slot (position, patrol route) the armed SİHA always occupied, so with the
        /// default SİHA profile — 30 m/s, 100 fuel, 300-round gun, 6 missiles, 250 m radar, 100 HP —
        /// this produces exactly the drone the simulation shipped. Nothing here touches the AI
        /// wingmen, and nothing survives a <see cref="Rebuild"/>: the profile is re-read from the
        /// (static) selection every time the world is built.
        /// </para>
        ///
        /// <para>
        /// Each archetype also gets its OWN airframe from <see cref="VehicleModelBuilder"/> — the
        /// jet no longer borrows the armed-UAV silhouette. The models are purely cosmetic: they hang
        /// under the "Model" child, carry no colliders and never touch the numbers above.
        /// </para>
        /// </summary>
        private IhaController SpawnPlayerAircraft(AircraftProfile profile)
        {
            // Defensive: an unknown/empty stored id must never reach this method, but never fly a
            // null profile either.
            if (profile == null) profile = AircraftCatalog.Default;

            Vector3 position = new Vector3(0f, profile.CruiseAltitude, -30f);
            Color color = PlayerAircraftColor(profile.Kind);

            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = PlayerAircraftName(profile.Kind);
            go.transform.position = position;
            Parent(go);
            ApplyColor(go, color);

            // Cosmetic: hide the placeholder capsule and build this archetype's own silhouette in
            // its place. The root keeps the primitive's unit scale, so the "Model" child's
            // reciprocal scale is (1,1,1) and every airframe below is drawn at its designed size.
            VehicleModelBuilder.HideRootMesh(go);
            switch (profile.Kind)
            {
                case AircraftKind.FighterJet:
                    VehicleModelBuilder.BuildFighterJet(go.transform, color);
                    break;
                case AircraftKind.Iha:
                    VehicleModelBuilder.BuildReconUav(go.transform, color);
                    break;
                default:
                    VehicleModelBuilder.BuildArmedUav(go.transform, color);
                    break;
            }

            // Radar signature of the CHOSEN archetype (jet 4 m², SİHA 1 m², recon İHA 0.25 m²).
            // This is what makes the electronic-warfare layer matter to the player: hostile sensors
            // read it out of the detection snapshot and scale their own detection range by its fourth
            // root, so the jet is picked up further out than the SİHA and the recon İHA closer in.
            // Added BEFORE the Targetable so the registry's lazy lookup finds it on the first scan.
            go.AddComponent<RcsComponent>().Configure(profile.RadarSignature);

            // Electronic warfare: an onboard noise jammer, fitted ONLY when the hangar's "Elektronik
            // Harp" track has actually been bought. Level 0 yields strength 0 (see
            // UpgradeCatalog.JammerStrengthAtLevel), so no component is added at all and a fresh save
            // flies exactly the aircraft it always did. Added BEFORE the Targetable, like the
            // RcsComponent above, so the registry's lazy component lookup finds it on the very first
            // detection snapshot — which is the path that carries the jamming strength to every
            // hostile sensor. AircraftUpgrades.Apply is a pure function of the upgrade state, so a
            // Rebuild() (which destroys this whole aircraft and re-derives the profile) can neither
            // double-apply the strength nor leave a stale emitter behind.
            if (profile.JammerStrength > 0f)
            {
                go.AddComponent<Jammer>().Configure(profile.JammerStrength,
                                                    UpgradeCatalog.JammerBurstSeconds,
                                                    UpgradeCatalog.JammerCooldownSeconds);
            }

            MarkFriendly(go, profile.Health);

            // Gun, added before the controller so its Start() picks it up.
            // magazineSize, roundsPerSecond, effectiveRange, dispersionDeg, damagePerRound
            go.AddComponent<GunTurret>().Configure(profile.GunMagazine, profile.GunRoundsPerSecond,
                                                   profile.GunRange, profile.GunDispersionDeg,
                                                   profile.GunDamage);

            // Flare/chaff dispenser, added before the controller so its Start() picks it up.
            go.AddComponent<CountermeasureDispenser>();

            // A missile carrier needs the armed controller; the recon archetype flies gun-only.
            IhaController ctrl;
            if (profile.MissileCapacity > 0) ctrl = go.AddComponent<SihaController>();
            else ctrl = go.AddComponent<IhaController>();

            // ApplyProfile runs BEFORE Start, so every pure-logic core is built from these values.
            ctrl.ApplyProfile(profile);
            AssignRoute(ctrl, RectangleRoute(position, 40f, 20f));

            // Realistic radar sensor (hostileFaction defaults to 1); the profile sizes its picture.
            go.AddComponent<RadarSensor>().ConfigureRange(profile.RadarRange);

            // Cosmetic: spinning propeller and roll-into-the-turn on the "Model" child only. The
            // jet airframe has no "Propeller" part; the spinner then simply finds nothing and idles.
            go.AddComponent<PropellerSpinner>();
            go.AddComponent<BankingVisual>();

            // Cosmetic: the engine note. The SAME "Propeller" test picks the loop, so the jet gets a
            // turbine whine and the two drone airframes a propeller drone; it goes 2D while piloted.
            go.AddComponent<EngineAudio>();

            return ctrl;
        }

        /// <summary>Scene name of the player's aircraft, so the HUD reads sensibly per archetype.</summary>
        private static string PlayerAircraftName(AircraftKind kind)
        {
            switch (kind)
            {
                case AircraftKind.FighterJet: return "JET_1";
                case AircraftKind.Iha: return "IHA_3";
                default: return "SIHA_1";
            }
        }

        /// <summary>
        /// Livery of the player's aircraft (cosmetic). The SİHA keeps the exact colour the fixed
        /// armed drone always had.
        /// </summary>
        private static Color PlayerAircraftColor(AircraftKind kind)
        {
            switch (kind)
            {
                case AircraftKind.FighterJet: return new Color(0.75f, 0.8f, 0.9f);
                case AircraftKind.Iha: return new Color(0.2f, 0.8f, 1f);
                default: return new Color(1f, 0.35f, 0.2f);
            }
        }

        /// <summary>Marks a drone GameObject as a friendly Targetable (Faction = 0) with 100 hit points.</summary>
        private void MarkFriendly(GameObject go)
        {
            MarkFriendly(go, 100f);
        }

        /// <summary>
        /// Marks a drone GameObject as a friendly Targetable (Faction = 0) with an explicit hit-point
        /// pool. <see cref="Targetable.SetMaxHealth"/> is used rather than the plain field because the
        /// component's Awake — which builds the pool — already ran inside <c>AddComponent</c>.
        /// </summary>
        private void MarkFriendly(GameObject go, float maxHealth)
        {
            var targetable = go.AddComponent<Targetable>();
            targetable.Faction = 0;
            targetable.SetMaxHealth(maxHealth);

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
