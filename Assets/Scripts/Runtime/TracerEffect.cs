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

        /// <summary>
        /// Spawns a one-shot tracer line from <paramref name="from"/> to <paramref name="to"/> in the
        /// given <paramref name="color"/>. Fully defensive: if no usable shader exists the tracer is
        /// still created (and cleaned up), it simply may not render.
        /// </summary>
        public static void Spawn(Vector3 from, Vector3 to, Color color)
        {
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

            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            if (shader == null) shader = Shader.Find("Standard");
            if (shader != null)
            {
                var mat = new Material(shader) { color = color };
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
                line.material = mat;
            }

            go.AddComponent<TracerEffect>();
        }

        private void Update()
        {
            _elapsed += Time.deltaTime;
            if (_elapsed >= Lifetime) Destroy(gameObject);
        }
    }
}
