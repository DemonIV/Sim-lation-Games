using System.Collections.Generic;
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
        // Keep-out disc around the airbase. The base footprint reaches ~22 m (runway z ±22, hangars at
        // x −13), so anything smaller lets a SAM/AAA spawn on the runway or inside a hangar.
        [SerializeField] private float spawnMinRadius = 32f;
        [SerializeField] private float fighterAltitude = 14f;  // hostile fighters spawn airborne, at cruise

        // Minimum planar spacing between two enemies placed in the same wave, and how many rejection
        // samples we are willing to draw before accepting the last candidate anyway.
        private const float SpawnSeparation = 12f;
        private const int SpawnPlacementAttempts = 24;

        // Planar (x,z) positions already handed out for the wave currently being spawned.
        private readonly List<Vector2> _wavePlacements = new List<Vector2>();

        /// <summary>
        /// The mission the player picked in <see cref="ScenarioMenu"/>. STATIC on purpose: the restart
        /// path (R, and the menu's own "choose again") reloads the scene, which destroys every
        /// component, so the choice has to outlive the reload.
        /// </summary>
        public static ScenarioKind SelectedKind = ScenarioKind.MixedDefense;

        /// <summary>
        /// Id of the aircraft archetype the player picked in <see cref="ScenarioMenu"/>
        /// (see <see cref="AircraftCatalog"/>). STATIC for exactly the same reason as
        /// <see cref="SelectedKind"/>: <see cref="SimulationBootstrap.Rebuild"/> destroys every
        /// component, so the choice has to outlive the rebuild.
        /// <para>
        /// Defaults to the SİHA baseline, so a player who never touches the selector flies precisely
        /// what the simulation shipped before.
        /// </para>
        /// </summary>
        public static string SelectedAircraftId = AircraftCatalog.Default.Id;

        /// <summary>
        /// The selected aircraft profile, resolved defensively: an unknown or empty
        /// <see cref="SelectedAircraftId"/> falls back to <see cref="AircraftCatalog.Default"/> instead
        /// of throwing. Never null.
        /// </summary>
        public static AircraftProfile SelectedAircraft => AircraftCatalog.GetOrDefault(SelectedAircraftId);

        /// <summary>The scenario this controller is running (i.e. <see cref="SelectedKind"/>).</summary>
        public ScenarioKind Kind => SelectedKind;

        /// <summary>
        /// The campaign level being flown (see <see cref="CampaignSession.SelectedLevel"/>). It is
        /// what actually decides the mission now: <see cref="SelectedKind"/> is derived from it, the
        /// wave count comes from <see cref="CampaignLevel.TotalWaves"/> and each wave's enemy mix from
        /// <see cref="CampaignLevel.Composition"/> — which itself delegates to
        /// <see cref="ScenarioLibrary.Composition"/>, so no scenario data is duplicated.
        /// Never null.
        /// </summary>
        public static CampaignLevel Level => CampaignSession.SelectedLevel;

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
            // Build the state from the selected level so the HUD can already show the right wave
            // count behind the menu, but do NOT start: Update stays idle until BeginMission().
            SyncStateToLevel();
        }

        /// <summary>
        /// Starts (or restarts) the mission for the currently selected campaign level.
        /// Called by <see cref="ScenarioMenu"/> when the player picks a level.
        /// </summary>
        public void BeginMission()
        {
            SyncStateToLevel();
            Started = true;
        }

        /// <summary>
        /// Rebuilds the wave state from <see cref="Level"/> and mirrors that level's scenario into
        /// <see cref="SelectedKind"/>, so the HUD and the mission report keep naming the right
        /// mission type.
        /// </summary>
        private void SyncStateToLevel()
        {
            CampaignLevel level = Level;
            SelectedKind = level.Scenario;
            _state = new ScenarioState(level.TotalWaves);
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
            int live = TargetRegistry.CountAlive(1);
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
        /// Spawns the full enemy mix for the given (0-based) wave. The mix comes from the campaign
        /// <see cref="Level"/>, which adds its own start offset and then delegates to
        /// <see cref="ScenarioLibrary.Composition"/> for the selected scenario — Mixed Defense in
        /// turn delegates to the original <see cref="WavePlan"/>, the other missions have their own
        /// per-wave composition (ground-only recon, SAM/AAA-only SEAD, fighters-only air combat).
        /// Ground archetypes are scattered on the deck; fighters spawn airborne at cruise altitude.
        /// </summary>
        private void SpawnWave(int waveIndex)
        {
            WaveComposition c = Level.Composition(waveIndex);

            // Spacing is enforced per wave: everything placed below is kept clear of the units this
            // wave has already put on the field.
            _wavePlacements.Clear();

            int wave = waveIndex + 1;
            for (int i = 0; i < c.PlainHostiles; i++) SpawnPlainHostile(RandomScatterPosition(), $"Hostile_W{wave}_{i}");
            for (int i = 0; i < c.Sams; i++) SpawnSam(RandomScatterPosition(), $"SAM_W{wave}_{i}");
            for (int i = 0; i < c.Aaa; i++) SpawnAaa(RandomScatterPosition(), $"AAA_W{wave}_{i}");
            for (int i = 0; i < c.Fighters; i++) SpawnFighter(RandomAirbornePosition(i), $"Fighter_W{wave}_{i}", i);
        }

        /// <summary>
        /// Scatter position lifted to the fighters' cruise altitude, so they spawn airborne. The
        /// altitude is staggered a couple of metres per fighter (around the unchanged cruise value) so
        /// a wave does not appear as one stack of overlapping silhouettes.
        /// </summary>
        private Vector3 RandomAirbornePosition(int index)
        {
            Vector3 p = RandomScatterPosition();
            p.y = fighterAltitude + ((index % 4) - 1.5f) * 1.6f;   // −2.4 … +2.4 m around cruise
            return p;
        }

        /// <summary>
        /// Picks a random ground position within ±fieldHalfExtent on X/Z, at height groundY, that is
        /// outside the airbase keep-out disc (spawnMinRadius) AND at least <see cref="SpawnSeparation"/>
        /// metres from every unit already placed in this wave. Rejection sampling is capped at
        /// <see cref="SpawnPlacementAttempts"/> draws, after which the last candidate is used (pushed
        /// out of the keep-out disc if needed), so a crowded wave can never loop forever.
        /// </summary>
        private Vector3 RandomScatterPosition()
        {
            float extent = Mathf.Max(spawnMinRadius, fieldHalfExtent);
            var planar = Vector2.zero;
            bool accepted = false;

            for (int attempt = 0; attempt < SpawnPlacementAttempts && !accepted; attempt++)
            {
                planar = new Vector2(Random.Range(-extent, extent), Random.Range(-extent, extent));
                accepted = planar.magnitude >= spawnMinRadius && IsClearOfWavePlacements(planar);
            }

            // Defensive fallback: keep the last candidate but push it out along its bearing so it can
            // never land on the runway or in a hangar.
            if (planar.magnitude < spawnMinRadius)
            {
                if (planar.sqrMagnitude < 1e-6f) planar = Vector2.right;
                planar = planar.normalized * spawnMinRadius;
            }

            _wavePlacements.Add(planar);
            return new Vector3(planar.x, groundY, planar.y);
        }

        /// <summary>
        /// True when the candidate keeps at least <see cref="SpawnSeparation"/> metres from every
        /// position already handed out for the current wave.
        /// </summary>
        private bool IsClearOfWavePlacements(Vector2 candidate)
        {
            float minSq = SpawnSeparation * SpawnSeparation;
            for (int i = 0; i < _wavePlacements.Count; i++)
            {
                if ((_wavePlacements[i] - candidate).sqrMagnitude < minSq) return false;
            }
            return true;
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
            // detectionRange, fireRange, lockTimeSeconds, magazineSize, roundsPerSecond, munitionSpeed,
            // damage, munitionLoadG
            // munitionSpeed 150 -> 85: a long-range SAM shot must be evadable, not an instant hit.
            // munitionLoadG 6: a heavy long-range round. 6 g at 85 m/s is ~40 deg/s (121 m turn
            // radius). It is launched on a lead course, so a drone flying straight is hit; a hard
            // break inside ~2 s demands roughly 4 x 85 x 0.47 / t_go m/s^2, which crosses 6 g at about
            // 2.3 s to impact — break late and it cannot recover, break early and it re-corrects.
            site.Configure(160f, 120f, 1.2f, 6, 0.4f, 85f, 55f, 6f);

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
            // detectionRange, fireRange, lockTimeSeconds, magazineSize, roundsPerSecond, munitionSpeed,
            // damage, munitionLoadG
            // munitionSpeed 130 -> 95: still the faster of the two, but short-ranged, so it stays close.
            // munitionLoadG 9: a lighter, nimbler round than the SAM's (~55 deg/s at 95 m/s), but it
            // only fires inside 60 m, so its whole flight lasts under a second. That means you cannot
            // react to an AAA launch — you have to be ALREADY breaking. Its answer is to not fly
            // straight over the gun; each round is only worth 20 damage.
            site.Configure(80f, 60f, 0.8f, 20, 1.5f, 95f, 20f, 9f);

            // Cosmetic: turret tracking (this archetype has no radar dish part).
            go.AddComponent<TurretVisual>();
        }

        /// <summary>
        /// Spawns a hostile fighter drone: a dark-magenta capsule that flies, hunts friendly drones and
        /// strafes them with a gun (<see cref="EnemyDroneController"/> + <see cref="GunTurret"/>). Unlike
        /// the ground archetypes it is spawned AIRBORNE at its cruise altitude.
        /// </summary>
        private void SpawnFighter(Vector3 pos, string name, int index)
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

            // Fan the search orbits: without this every fighter of a wave flies the same 25 m circle.
            // Spacing only — the controller's own standoff/cruise values are untouched.
            var pilot = go.AddComponent<EnemyDroneController>();
            pilot.SetLoiterOffsets(index * 4f, index * 0.9f);

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
