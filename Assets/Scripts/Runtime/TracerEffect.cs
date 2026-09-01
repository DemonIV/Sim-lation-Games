using UnityEngine;

namespace Sim.Runtime
{
    /// <summary>
    /// A self-contained, asset-free gun tracer. Draws a short bright line between two world points
    /// with a <see cref="LineRenderer"/> and destroys itself after a fraction of a second. Uses
    /// scaled time so a paused game freezes the tracer like every other effect.
    ///
    /// This is a GAME / EDUCATIONAL visual only — no external assets or particle systems required.
    /// </summary>
    public class TracerEffect : MonoBehaviour
    {
        private const float Lifetime = 0.06f;
        private const float Width = 0.08f;

        private float _elapsed;
        private bool _budgeted;

        /// <summary>
        /// Spawns a one-shot tracer line from <paramref name="from"/> to <paramref name="to"/> in the
        /// given <paramref name="color"/>. Fully defensive: if no usable shader exists the tracer is
        /// still created (and cleaned up), it simply may not render.
        ///
        /// Tracers count against the global <see cref="VfxLibrary"/> effect budget like every other
        /// effect, so a long firefight cannot spawn unbounded objects. Nothing is spawned when the
        /// budget is exhausted.
        /// </summary>
        public static void Spawn(Vector3 from, Vector3 to, Color color)
        {
            if (!VfxLibrary.TrySpawnBudget()) return;

            var go = new GameObject("Tracer");
            go.transform.position = from;

            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.SetPosition(0, from);
            line.SetPosition(1, to);
            line.startWidth = Width;
            line.endWidth = Width;
            line.startColor = color;
            line.endColor = new Color(color.r, color.g, color.b, 0.25f);

            // Shared, cached material: the tracer never writes to it (its colour gradient lives on the
            // LineRenderer), so one material per tracer colour is enough — a per-round instance would
            // leak dozens of materials per second of sustained fire.
            Material mat = MaterialLibrary.CreateUnlit(color);
            if (mat != null) line.sharedMaterial = mat;

            var effect = go.AddComponent<TracerEffect>();
            effect._budgeted = true;
        }

        private void Update()
        {
            _elapsed += Time.deltaTime;
            if (_elapsed >= Lifetime) Destroy(gameObject);
        }

        private void OnDestroy()
        {
            if (!_budgeted) return;
            _budgeted = false;
            VfxLibrary.Release();
        }
    }
}
