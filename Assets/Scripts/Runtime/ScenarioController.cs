using UnityEngine;
using Sim.Core;

namespace Sim.Runtime
{
    /// <summary>
    /// Owns the wave-based scenario. Each wave it spawns an escalating mix of three hostile enemy
    /// archetypes (a plain objective target, a long-range SAM, and a short-range fast-firing AAA)
    /// according to <see cref="WavePlan"/>, then waits until the field is cleared before spawning the
    /// next wave. Win/lose is driven by a pure-logic <see cref="ScenarioState"/>: clearing the last
    /// wave wins; the mission fails once the friendly squad is combat-ineffective (see
    /// <see cref="SquadStatus"/> — no drone survives, or every survivor has a dry tank). The HUD reads
    /// this component's accessors.
    ///
    /// Enemy spawning here mirrors <see cref="SimulationBootstrap"/>'s primitive/Targetable/RcsComponent
    /// pattern, so the scenario is fully self-contained.
    ///
    /// This is a GAME / EDUCATIONAL simulation with abstract, gamified parameters.
    /// </summary>
    public class ScenarioController : MonoBehaviour
    {
        [Header("Scenario")]
        [SerializeField] private int totalWaves = 3;

        [Header("Spawn field")]
        [SerializeField] private float fieldHalfExtent = 40f;
        [SerializeField] private float groundY = 1f;
        [SerializeField] private float spawnMinRadius = 15f;   // keep enemies away from the very centre
        [SerializeField] private float fighterAltitude = 14f;  // hostile fighters spawn airborne, at cruise

        /// <summary>
        /// The mission the player picked in <see cref="ScenarioMenu"/>. STATIC on purpose: the restart
        /// path (R, and the menu's own "choose again") reloads the scene, which destroys every
        /// component, so the choice has to outlive the reload.
        /// </summary>
        public static ScenarioKind SelectedKind = ScenarioKind.MixedDefense;

        /// <summary>The scenario this controller is running (i.e. <see cref="SelectedKind"/>).</summary>
        public ScenarioKind Kind => SelectedKind;

        /// <summary>
        /// False until <see cref="BeginMission"/> is called. While false <see cref="Update"/> does
        /// nothing at all — no spawning, no wave advance, no fail check — so the mission-select menu
        /// can hold the sim on a clean field.
        /// </summary>
        public bool Started { get; private set; }

        private ScenarioState _state;

        // Friendly controllers, cached and refreshed occasionally so the squad-effectiveness check
        // below does not allocate a fresh array every frame.
        private IhaController[] _friendlies;
        private float _friendlyRefreshTimer;
        private const float FriendlyRefreshInterval = 1f;

        /// <summary>Current scenario outcome (InProgress / Won / Lost).</summary>
        public ScenarioStatus Status => _state != null ? _state.Status : ScenarioStatus.InProgress;

        /// <summary>1-based number of the current wave, for display.</summary>
        public int CurrentWaveNumber => _state != null ? _state.CurrentWaveNumber : 1;

        /// <summary>Total number of waves in this scenario.</summary>
        public int TotalWaves => _state != null ? _state.TotalWaves : totalWaves;

        /// <summary>Number of hostiles currently alive on the field (cached from the last tick).</summary>
        public int LiveEnemies { get; private set; }

        private void Start()
        {
            // Build the state from the selected scenario so the HUD can already show the right wave
            // count behind the menu, but do NOT start: Update stays idle until BeginMission().
            _state = new ScenarioState(ScenarioLibrary.TotalWaves(SelectedKind));
        }

        /// <summary>
        /// Starts (or restarts) the mission for the currently <see cref="SelectedKind"/> scenario.
        /// Called by <see cref="ScenarioMenu"/> when the player picks a mission.
        /// </summary>
        public void BeginMission()
        {
            _state = new ScenarioState(ScenarioLibrary.TotalWaves(SelectedKind));
            Started = true;
        }

        private void Update()
        {
            // Held by the mission-select menu until the player picks a scenario.
            if (!Started) return;
            if (_state == null) return;

            // 1. Spawn the current wave once, when awaiting a spawn.
            if (_state.AwaitingSpawn)
            {
                SpawnWave(_state.CurrentWaveIndex);
                _state.MarkWaveSpawned();
            }

            // 2. Count live hostiles (Faction 1) and advance the scenario.
            int live = TargetRegistry.GetSnapshot(1).Count;
            LiveEnemies = live;
            _state.UpdateEnemies(live);

            // 3. Squad effectiveness: the scenario fails when no friendly drone survives OR when every
            //    survivor has a dry tank (a squad that cannot fly cannot finish the mission).
            _friendlyRefreshTimer += Time.deltaTime;
            if (_friendlies == null || _friendlyRefreshTimer >= FriendlyRefreshInterval)
            {
                _friendlyRefreshTimer = 0f;
                _friendlies = FindObjectsByType<IhaController>(FindObjectsSortMode.None);
            }

            int alive = 0;
            int fuelled = 0;
            for (int i = 0; i < _friendlies.Length; i++)
            {
                IhaController c = _friendlies[i];
                if (c == null) continue;   // destroyed since the last refresh
                alive++;
                if (!c.IsOutOfFuel) fuelled++;
            }

            if (SquadStatus.IsCombatIneffective(alive, fuelled))
                _state.Fail();
        }

        /// <summary>
        /// Spawns the full enemy mix for the given (0-based) wave. The mix comes from
        /// <see cref="ScenarioLibrary.Composition"/> for the selected scenario — Mixed Defense
        /// delegates to the original <see cref="WavePlan"/>, the other missions have their own
        /// per-wave composition (ground-only recon, SAM/AAA-only SEAD, fighters-only air combat).
        /// Ground archetypes are scattered on the deck; fighters spawn airborne at cruise altitude.
        /// </summary>
        private void SpawnWave(int waveIndex)
        {
            WaveComposition c = ScenarioLibrary.Composition(SelectedKind, waveIndex);

            int wave = waveIndex + 1;
            for (int i = 0; i < c.PlainHostiles; i++) SpawnPlainHostile(RandomScatterPosition(), $"Hostile_W{wave}_{i}");
            for (int i = 0; i < c.Sams; i++) SpawnSam(RandomScatterPosition(), $"SAM_W{wave}_{i}");
            for (int i = 0; i < c.Aaa; i++) SpawnAaa(RandomScatterPosition(), $"AAA_W{wave}_{i}");
            for (int i = 0; i < c.Fighters; i++) SpawnFighter(RandomAirbornePosition(), $"Fighter_W{wave}_{i}");
        }

        /// <summary>Scatter position lifted to the fighters' cruise altitude, so they spawn airborne.</summary>
        private Vector3 RandomAirbornePosition()
        {
            Vector3 p = RandomScatterPosition();
            p.y = fighterAltitude;
            return p;
        }

        /// <summary>
        /// Picks a random ground position within ±fieldHalfExtent on X/Z whose planar distance from the
        /// origin is at least spawnMinRadius, at height groundY.
        /// </summary>
        private Vector3 RandomScatterPosition()
        {
            float extent = Mathf.Max(spawnMinRadius, fieldHalfExtent);
            float x = 0f, z = 0f;
            // A few tries to land outside the central keep-out radius; fall back defensively.
            for (int attempt = 0; attempt < 8; attempt++)
            {
                x = Random.Range(-extent, extent);
                z = Random.Range(-extent, extent);
                if (new Vector2(x, z).magnitude >= spawnMinRadius) break;
            }

            // Defensive: if still inside the keep-out disc, push out along the current bearing.
            var planar = new Vector2(x, z);
            if (planar.magnitude < spawnMinRadius)
            {
                if (planar.sqrMagnitude < 1e-6f) planar = Vector2.right;
                planar = planar.normalized * spawnMinRadius;
                x = planar.x;
                z = planar.y;
            }

            return new Vector3(x, groundY, z);
        }

        /// <summary>
        /// Moves a freshly spawned enemy under the generated-world root
        /// (<see cref="SimulationBootstrap.Root"/>) so a restart tears it down with everything else.
        /// World pose is kept, and the call is a no-op when there is no root (e.g. this controller
        /// dropped into a hand-authored scene).
        /// </summary>
        private static void ParentToSimulationRoot(GameObject go)
        {
            if (go == null) return;
            Transform root = SimulationBootstrap.Root;
            if (root == null) return;
            go.transform.SetParent(root, true);
        }

        /// <summary>Spawns a plain objective hostile: a grey cube with no weapon (pure target).</summary>
        private void SpawnPlainHostile(Vector3 pos, string name)
        {
            // Cosmetic: sit on the procedural terrain surface (x/z unchanged).
            pos.y = TerrainField.Height(pos.x, pos.z);

            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.position = pos;
            go.transform.localScale = new Vector3(2f, 2f, 2f);
            ParentToSimulationRoot(go);
            ApplyColor(go, new Color(0.5f, 0.5f, 0.5f));
            VehicleModelBuilder.HideRootMesh(go);
            VehicleModelBuilder.BuildGroundTarget(go.transform, new Color(0.5f, 0.5f, 0.5f));

            var targetable = go.AddComponent<Targetable>();
            targetable.Faction = 1;
            targetable.MaxHealth = 60f;

            // Cosmetic: smoke/fire once the unit is badly damaged.
            go.AddComponent<DamageVisuals>();

            // Aspect-dependent radar signature so friendly RadarSensors see angle-varying RCS.
            go.AddComponent<RcsComponent>();
        }

        /// <summary>
        /// Spawns a long-range SAM site: a dark-red cylinder that detects friendlies far out and
        /// launches heavy guided munitions. Tougher and slower-firing than the AAA.
        /// </summary>
        private void SpawnSam(Vector3 pos, string name)
        {
            // Cosmetic: sit on the procedural terrain surface (x/z unchanged).
            pos.y = TerrainField.Height(pos.x, pos.z);

            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            go.transform.position = pos;
            go.transform.localScale = new Vector3(3f, 2f, 3f);
            ParentToSimulationRoot(go);
            ApplyColor(go, new Color(0.5f, 0.05f, 0.05f));
            VehicleModelBuilder.HideRootMesh(go);
            VehicleModelBuilder.BuildSamSite(go.transform, new Color(0.5f, 0.05f, 0.05f));

            var targetable = go.AddComponent<Targetable>();
            targetable.Faction = 1;
            targetable.MaxHealth = 120f;

            // Cosmetic: smoke/fire once the unit is badly damaged.
            go.AddComponent<DamageVisuals>();

            go.AddComponent<RcsComponent>();

            var site = go.AddComponent<AirDefenseSite>();
            // detectionRange, fireRange, lockTimeSeconds, magazineSize, roundsPerSecond, munitionSpeed, damage
            site.Configure(160f, 120f, 1.2f, 6, 0.4f, 150f, 55f);

            // Cosmetic: sweeping radar dish and a turret that tracks the held contact.
            go.AddComponent<TurretVisual>();
        }

        /// <summary>
        /// Spawns a short-range AAA piece: an orange, short cylinder that engages only close-in but
        /// fires fast, light rounds. Weaker and shorter-ranged than the SAM.
        /// </summary>
        private void SpawnAaa(Vector3 pos, string name)
        {
            // Cosmetic: sit on the procedural terrain surface (x/z unchanged).
            pos.y = TerrainField.Height(pos.x, pos.z);

            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            go.transform.position = pos;
            go.transform.localScale = new Vector3(2f, 1.5f, 2f);
            ParentToSimulationRoot(go);
            ApplyColor(go, new Color(1f, 0.55f, 0.1f));
            VehicleModelBuilder.HideRootMesh(go);
            VehicleModelBuilder.BuildAaaSite(go.transform, new Color(1f, 0.55f, 0.1f));

            var targetable = go.AddComponent<Targetable>();
            targetable.Faction = 1;
            targetable.MaxHealth = 70f;

            // Cosmetic: smoke/fire once the unit is badly damaged.
            go.AddComponent<DamageVisuals>();

            go.AddComponent<RcsComponent>();

            var site = go.AddComponent<AirDefenseSite>();
            // detectionRange, fireRange, lockTimeSeconds, magazineSize, roundsPerSecond, munitionSpeed, damage
            site.Configure(80f, 60f, 0.8f, 20, 1.5f, 130f, 20f);

            // Cosmetic: turret tracking (this archetype has no radar dish part).
            go.AddComponent<TurretVisual>();
        }

        /// <summary>
        /// Spawns a hostile fighter drone: a dark-magenta capsule that flies, hunts friendly drones and
        /// strafes them with a gun (<see cref="EnemyDroneController"/> + <see cref="GunTurret"/>). Unlike
        /// the ground archetypes it is spawned AIRBORNE at its cruise altitude.
        /// </summary>
        private void SpawnFighter(Vector3 pos, string name)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = name;
            go.transform.position = pos;
            go.transform.localScale = new Vector3(1f, 1f, 1f);
            ParentToSimulationRoot(go);
            ApplyColor(go, new Color(0.55f, 0.1f, 0.6f));
            // Cosmetic only — the airborne spawn altitude is untouched.
            VehicleModelBuilder.HideRootMesh(go);
            VehicleModelBuilder.BuildEnemyFighter(go.transform, new Color(0.55f, 0.1f, 0.6f));

            var targetable = go.AddComponent<Targetable>();
            targetable.Faction = 1;
            targetable.MaxHealth = 70f;

            // Cosmetic: smoke/fire once the unit is badly damaged.
            go.AddComponent<DamageVisuals>();

            go.AddComponent<RcsComponent>();

            // Gun BEFORE the controller so EnemyDroneController.Start finds it via GetComponent.
            var gun = go.AddComponent<GunTurret>();
            // magazineSize, roundsPerSecond, effectiveRange, dispersionDeg, damagePerRound
            gun.Configure(200, 8f, 55f, 3f, 3.5f);
            gun.SetTracerColor(new Color(1f, 0.35f, 0.35f));

            // Flare/chaff dispenser, also BEFORE the controller so it is picked up in Start. Slightly
            // leaner than the friendly fit so the player keeps an edge.
            go.AddComponent<CountermeasureDispenser>().Configure(6, 2.5f, 0.5f);

            go.AddComponent<EnemyDroneController>();

            // Cosmetic: roll into turns; only the "Model" child is rotated.
            go.AddComponent<BankingVisual>();
        }

        /// <summary>
        /// Applies a solid color to a primitive, preferring the Standard shader with URP fallbacks.
        /// Mirrors <c>SimulationBootstrap.ApplyColor</c> so the scenario stays self-contained.
        /// </summary>
        private void ApplyColor(GameObject go, Color color)
        {
            if (go == null) return;
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
