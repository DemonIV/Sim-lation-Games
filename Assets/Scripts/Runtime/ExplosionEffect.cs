using UnityEngine;

namespace Sim.Runtime
{
    /// <summary>
    /// A self-contained, asset-free explosion marker. Spawns a bright emissive orange sphere that
    /// expands from a small radius up to its target size while fading its alpha/emission to zero over
    /// a short lifetime, then destroys itself. Uses scaled time so a paused game freezes the effect.
    ///
    /// This is a GAME / EDUCATIONAL visual only — no external assets or particle systems required.
    /// </summary>
    public class ExplosionEffect : MonoBehaviour
    {
        private const float Lifetime = 0.45f;

        private float _elapsed;
        private float _size = 1f;
        private Renderer _renderer;
        private Material _material;
        private static readonly Color BaseColor = new Color(1f, 0.5f, 0.1f);

        /// <summary>
        /// Creates a GameObject carrying an <see cref="ExplosionEffect"/> at the given position and
        /// initializes it to grow up to <paramref name="size"/> world units before self-destructing.
        /// </summary>
        public static void Spawn(Vector3 position, float size)
        {
            var go = new GameObject("ExplosionEffect");
            go.transform.position = position;
            var fx = go.AddComponent<ExplosionEffect>();
            fx.Initialize(size);
        }

        /// <summary>Builds the emissive sphere visual and stores the target size.</summary>
        private void Initialize(float size)
        {
            _size = Mathf.Max(0.01f, size);

            // Build a Sphere primitive child that renders the blast; strip its collider.
            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = "ExplosionSphere";
            sphere.transform.SetParent(transform, false);
            sphere.transform.localPosition = Vector3.zero;

            var collider = sphere.GetComponent<Collider>();
            if (collider != null) Destroy(collider);

            _renderer = sphere.GetComponent<Renderer>();
            if (_renderer != null)
            {
                Shader shader = Shader.Find("Standard");
                if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) shader = Shader.Find("Unlit/Color");
                if (shader != null)
                {
                    _material = new Material(shader) { color = BaseColor };
                    if (_material.HasProperty("_BaseColor")) _material.SetColor("_BaseColor", BaseColor);
                    if (_material.HasProperty("_EmissionColor"))
                    {
                        _material.EnableKeyword("_EMISSION");
                        _material.SetColor("_EmissionColor", BaseColor);
                    }
                    _renderer.material = _material;
                }
            }

            // Start small.
            transform.localScale = Vector3.one * (_size * 0.2f);
        }

        private void Update()
        {
            _elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(_elapsed / Lifetime);

            // Grow from 0.2*size up to full size.
            float scale = Mathf.Lerp(_size * 0.2f, _size, t);
            transform.localScale = Vector3.one * scale;

            // Fade alpha + emission out toward the end of the lifetime.
            float fade = 1f - t;
            if (_material != null)
            {
                Color c = BaseColor;
                c.a = fade;
                _material.color = c;
                if (_material.HasProperty("_BaseColor")) _material.SetColor("_BaseColor", c);
                if (_material.HasProperty("_EmissionColor"))
                    _material.SetColor("_EmissionColor", BaseColor * fade);
            }

            if (_elapsed >= Lifetime)
                Destroy(gameObject);
        }
    }
}
