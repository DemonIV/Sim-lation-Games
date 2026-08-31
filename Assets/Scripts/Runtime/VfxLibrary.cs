using UnityEngine;

namespace Sim.Runtime
{
    /// <summary>
    /// Generic driver for a short-lived visual effect. Owns a lifetime, a per-frame callback and an
    /// optional cleanup callback, then destroys its own GameObject. Everything runs on SCALED
    /// <see cref="Time.deltaTime"/> so pausing the game freezes the effect too.
    ///
    /// Every ticker spawned through <see cref="VfxLibrary"/> counts against the global effect budget
    /// and releases its slot in <see cref="OnDestroy"/>.
    ///
    /// Purely cosmetic — nothing here touches gameplay state.
    /// </summary>
    public class VfxTicker : MonoBehaviour
    {
        /// <summary>Total lifetime in seconds before the GameObject destroys itself.</summary>
        public float Life = 1f;

        /// <summary>Per-frame callback: (normalized age 0..1, scaled delta time).</summary>
        public System.Action<float, float> OnTick;

        /// <summary>Called exactly once when the effect goes away (used to free material instances).</summary>
        public System.Action OnCleanup;

        private float _elapsed;
        private bool _budgeted;

        /// <summary>Marks this effect as holding a slot in the global budget (released on destroy).</summary>
        public void MarkBudgeted()
        {
            _budgeted = true;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            _elapsed += dt;

            float life = Life > 1e-4f ? Life : 1e-4f;
            float t = Mathf.Clamp01(_elapsed / life);

            if (OnTick != null) OnTick(t, dt);

            if (_elapsed >= life) Destroy(gameObject);
        }

        private void OnDestroy()
        {
            if (OnCleanup != null)
            {
                System.Action cleanup = OnCleanup;
                OnCleanup = null;
                cleanup();
            }

            if (_budgeted)
            {
                _budgeted = false;
                VfxLibrary.Release();
            }
        }
    }

    /// <summary>
    /// Asset-free effect primitives (emissive glow, point-light flash, debris, smoke, shockwave ring,
    /// sparks). Every effect builds itself from Unity primitives, strips its collider, animates on
    /// scaled time and self-destroys.
    ///
    /// A global live-effect budget keeps a busy firefight from drowning the scene: once
    /// <see cref="MaxLiveEffects"/> effects are alive, further spawn requests are silently skipped.
    ///
    /// Purely cosmetic — nothing here touches gameplay state.
    /// </summary>
    public static class VfxLibrary
    {
        /// <summary>Hard cap on simultaneously live effect objects.</summary>
        public const int MaxLiveEffects = 220;

        private static int _live;

        /// <summary>Number of effect objects currently alive, for diagnostics.</summary>
        public static int LiveCount => _live;

        /// <summary>
        /// Reserves one slot in the global effect budget. Returns false when the cap is reached, in
        /// which case the caller must NOT spawn anything.
        /// </summary>
        public static bool TrySpawnBudget()
        {
            if (_live >= MaxLiveEffects) return false;
            _live++;
            return true;
        }

        /// <summary>Frees one budget slot. Called by <see cref="VfxTicker.OnDestroy"/>.</summary>
        public static void Release()
        {
            _live = Mathf.Max(0, _live - 1);
        }

        /// <summary>Drops the live counter back to zero (used when a scene is torn down/restarted).</summary>
        public static void ResetBudget()
        {
            _live = 0;
        }

        // ------------------------------------------------------------------ effects

        /// <summary>
        /// Spawns an emissive sphere that fades its emission to black and shrinks slightly, then
        /// destroys itself. Returns the spawned object, or null when the budget is exhausted.
        /// </summary>
        public static GameObject Glow(Vector3 pos, float size, Color color, Color emission, float life)
        {
            if (!TrySpawnBudget()) return null;

            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "VfxGlow";
            go.transform.position = pos;
            float s = Mathf.Max(0.01f, size);
            go.transform.localScale = Vector3.one * s;
            StripCollider(go);

            Material mat = CreateEmissiveInstance(color, emission);
            var renderer = go.GetComponent<Renderer>();
            if (mat != null && renderer != null) renderer.material = mat;

            Transform tr = go.transform;
            Attach(go, life,
                (t, dt) =>
                {
                    if (tr == null) return;
                    tr.localScale = Vector3.one * (s * Mathf.Lerp(1f, 0.7f, t));
                    if (mat == null) return;
                    SetEmission(mat, emission * (1f - t));
                },
                () => { if (mat != null) Object.Destroy(mat); });

            return go;
        }

        /// <summary>Spawns a point light that fades its intensity to zero and then destroys itself.</summary>
        public static void Flash(Vector3 pos, float range, Color color, float intensity, float life)
        {
            if (!TrySpawnBudget()) return;

            var go = new GameObject("VfxFlash");
            go.transform.position = pos;

            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = Mathf.Max(0.1f, range);
            light.color = color;
            light.intensity = Mathf.Max(0f, intensity);
            light.shadows = LightShadows.None;

            float peak = light.intensity;
            Attach(go, life,
                (t, dt) => { if (light != null) light.intensity = peak * (1f - t); },
                null);
        }

        /// <summary>
        /// Flings small cubes outward (biased upward), integrating a simple -9.81 m/s² fall and a
        /// random tumble. Each fragment is its own budgeted, self-destroying object.
        /// </summary>
        public static void Debris(Vector3 pos, int count, float size, float speed, Color color, float life)
        {
            int n = Mathf.Clamp(count, 0, 24);
            if (n <= 0) return;

            Material shared = MaterialLibrary.Create(color, 0.3f, 0.25f);

            for (int i = 0; i < n; i++)
            {
                if (!TrySpawnBudget()) return;

                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = "VfxDebris";
                go.transform.position = pos;
                float s = Mathf.Max(0.02f, size) * Random.Range(0.6f, 1.4f);
                go.transform.localScale = Vector3.one * s;
                go.transform.rotation = Random.rotation;
                StripCollider(go);
                if (shared != null) MaterialLibrary.Apply(go, shared);

                // Bias the burst upward so fragments arc rather than spraying into the ground.
                Vector3 dir = (Random.onUnitSphere + Vector3.up * 0.8f).normalized;
                Vector3 vel = dir * (Mathf.Max(0.1f, speed) * Random.Range(0.5f, 1.2f));
                Vector3 spin = new Vector3(Random.Range(-360f, 360f), Random.Range(-360f, 360f),
                                           Random.Range(-360f, 360f));

                Transform tr = go.transform;
                Attach(go, life,
                    (t, dt) =>
                    {
                        if (tr == null) return;
                        vel += Vector3.up * (-9.81f * dt);
                        tr.position += vel * dt;
                        tr.Rotate(spin * dt, Space.Self);
                    },
                    null);
            }
        }

        /// <summary>
        /// Spawns a transparent puff that expands, drifts upward at <paramref name="rise"/> m/s and
        /// fades its alpha out. Owns a per-instance material, destroyed with the effect.
        /// </summary>
        public static void Smoke(Vector3 pos, float size, float rise, float life, Color color)
        {
            if (!TrySpawnBudget()) return;

            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "VfxSmoke";
            go.transform.position = pos;
            float s = Mathf.Max(0.05f, size);
            go.transform.localScale = Vector3.one * s;
            StripCollider(go);

            Material mat = MaterialLibrary.CreateTransparent(color);
            var renderer = go.GetComponent<Renderer>();
            if (mat != null && renderer != null) renderer.material = mat;

            float startAlpha = color.a > 0f ? color.a : 0.5f;
            Color baseColor = color;

            Transform tr = go.transform;
            Attach(go, life,
                (t, dt) =>
                {
                    if (tr == null) return;
                    tr.localScale = Vector3.one * (s * Mathf.Lerp(1f, 2.2f, t));
                    tr.position += Vector3.up * (rise * dt);
                    if (mat == null) return;
                    Color c = baseColor;
                    c.a = startAlpha * (1f - t);
                    if (mat.HasProperty("_Color")) mat.SetColor("_Color", c);
                    if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
                },
                () => { if (mat != null) Object.Destroy(mat); });
        }

        /// <summary>
        /// Spawns a very flat cylinder (reads as a ring) that scales outward to
        /// <paramref name="maxRadius"/> while its emission fades to black.
        /// </summary>
        public static void Shockwave(Vector3 pos, float maxRadius, float life, Color emission)
        {
            if (!TrySpawnBudget()) return;

            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "VfxShockwave";
            go.transform.position = pos;
            go.transform.localScale = new Vector3(0.1f, 0.01f, 0.1f);
            StripCollider(go);

            Material mat = CreateEmissiveInstance(new Color(1f, 0.9f, 0.65f), emission);
            var renderer = go.GetComponent<Renderer>();
            if (mat != null && renderer != null) renderer.material = mat;

            float maxDiameter = Mathf.Max(0.2f, maxRadius) * 2f;
            Transform tr = go.transform;
            Attach(go, life,
                (t, dt) =>
                {
                    if (tr == null) return;
                    float d = Mathf.Lerp(0.1f, maxDiameter, t);
                    tr.localScale = new Vector3(d, 0.02f, d);
                    if (mat == null) return;
                    SetEmission(mat, emission * (1f - t));
                },
                () => { if (mat != null) Object.Destroy(mat); });
        }

        /// <summary>
        /// Spawns tiny bright cubes flying along <paramref name="normal"/> with spread, shrinking and
        /// slowing quickly. Used for gun impacts.
        /// </summary>
        public static void Spark(Vector3 pos, Vector3 normal, int count, float life)
        {
            int n = Mathf.Clamp(count, 0, 16);
            if (n <= 0) return;

            Vector3 axis = normal.sqrMagnitude > 1e-6f ? normal.normalized : Vector3.up;
            Material shared = MaterialLibrary.Create(new Color(1f, 0.95f, 0.6f), 0f, 0.8f,
                                                     new Color(2f, 1.6f, 0.6f));

            for (int i = 0; i < n; i++)
            {
                if (!TrySpawnBudget()) return;

                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = "VfxSpark";
                go.transform.position = pos;
                float s = 0.09f * Random.Range(0.6f, 1.4f);
                go.transform.localScale = Vector3.one * s;
                go.transform.rotation = Random.rotation;
                StripCollider(go);
                if (shared != null) MaterialLibrary.Apply(go, shared);

                Vector3 dir = (axis + Random.onUnitSphere * 0.55f).normalized;
                Vector3 vel = dir * Random.Range(6f, 15f);

                Transform tr = go.transform;
                Attach(go, life,
                    (t, dt) =>
                    {
                        if (tr == null) return;
                        vel = Vector3.Lerp(vel, Vector3.zero, Mathf.Clamp01(4f * dt));
                        tr.position += vel * dt;
                        tr.localScale = Vector3.one * (s * (1f - t));
                    },
                    null);
            }
        }

        // ------------------------------------------------------------------ helpers

        /// <summary>Attaches a budgeted <see cref="VfxTicker"/> driving the effect's animation.</summary>
        private static VfxTicker Attach(GameObject go, float life,
                                        System.Action<float, float> tick, System.Action cleanup)
        {
            var ticker = go.AddComponent<VfxTicker>();
            ticker.Life = Mathf.Max(0.01f, life);
            ticker.OnTick = tick;
            ticker.OnCleanup = cleanup;
            ticker.MarkBudgeted();
            return ticker;
        }

        /// <summary>Removes the primitive's collider so effects never interact with the simulation.</summary>
        private static void StripCollider(GameObject go)
        {
            if (go == null) return;
            var collider = go.GetComponent<Collider>();
            if (collider != null) Object.Destroy(collider);
        }

        /// <summary>Writes an emission colour onto whichever property the active pipeline exposes.</summary>
        private static void SetEmission(Material mat, Color emission)
        {
            if (mat == null) return;
            if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", emission);
            if (mat.HasProperty("_EmissiveColor")) mat.SetColor("_EmissiveColor", emission);
        }

        private static Shader _shader;
        private static bool _shaderResolved;

        /// <summary>Resolves (once) the best available lit shader for runtime-created materials.</summary>
        private static Shader ResolveShader()
        {
            if (_shaderResolved) return _shader;
            _shaderResolved = true;

            _shader = Shader.Find("Standard");
            if (_shader == null) _shader = Shader.Find("Universal Render Pipeline/Lit");
            if (_shader == null) _shader = Shader.Find("Unlit/Color");
            return _shader;
        }

        /// <summary>
        /// Creates a NEW (uncached) emissive material. Per-instance because effects animate their own
        /// emission — the caller destroys it in the effect's cleanup callback.
        /// </summary>
        private static Material CreateEmissiveInstance(Color color, Color emission)
        {
            Shader shader = ResolveShader();
            if (shader == null) return null;

            var mat = new Material(shader);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0f);
            mat.EnableKeyword("_EMISSION");
            SetEmission(mat, emission);
            return mat;
        }
    }
}
