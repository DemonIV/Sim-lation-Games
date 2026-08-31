using System.Collections.Generic;
using UnityEngine;

namespace Sim.Runtime
{
    /// <summary>
    /// A flat, dark burn mark laid on the terrain where a ground unit was destroyed. Fades out over
    /// its lifetime and then removes itself. A hard cap keeps a long battle from carpeting the map:
    /// spawning past the cap destroys the OLDEST mark first.
    ///
    /// Purely cosmetic — nothing here touches gameplay state.
    /// </summary>
    public class ScorchMark : MonoBehaviour
    {
        private const float Lifetime = 25f;
        private const int MaxMarks = 40;

        // Oldest-first list of live marks, used to enforce the cap.
        private static readonly List<ScorchMark> Live = new List<ScorchMark>();

        private float _elapsed;
        private Material _material;
        private Color _baseColor = new Color(0.05f, 0.045f, 0.04f, 0.75f);

        /// <summary>
        /// Lays a scorch mark of the given radius on the terrain under <paramref name="groundPosition"/>
        /// (the y coordinate is taken from <see cref="Sim.Core.TerrainField"/>, not from the caller).
        /// </summary>
        public static void Spawn(Vector3 groundPosition, float radius)
        {
            // Drop any destroyed entries, then make room for the newcomer.
            for (int i = Live.Count - 1; i >= 0; i--)
                if (Live[i] == null) Live.RemoveAt(i);

            while (Live.Count >= MaxMarks)
            {
                ScorchMark oldest = Live[0];
                Live.RemoveAt(0);
                if (oldest != null) Destroy(oldest.gameObject);
            }

            float r = Mathf.Max(0.2f, radius);
            float y = Sim.Core.TerrainField.Height(groundPosition.x, groundPosition.z) + 0.05f;

            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "ScorchMark";
            go.transform.position = new Vector3(groundPosition.x, y, groundPosition.z);
            // A Unity cylinder is 2 units tall with radius 0.5, so x/z scale == diameter.
            go.transform.localScale = new Vector3(r * 2f, 0.02f, r * 2f);

            var collider = go.GetComponent<Collider>();
            if (collider != null) Destroy(collider);

            var mark = go.AddComponent<ScorchMark>();
            mark.Initialize();
            Live.Add(mark);
        }

        /// <summary>Builds the per-instance transparent material used to fade the mark out.</summary>
        private void Initialize()
        {
            _material = MaterialLibrary.CreateTransparent(_baseColor);
            var renderer = GetComponent<Renderer>();
            if (_material != null && renderer != null) renderer.material = _material;
        }

        private void Update()
        {
            // Scaled time, so a paused game freezes the fade like every other effect.
            _elapsed += Time.deltaTime;

            if (_material != null)
            {
                // Hold full opacity for most of the life, then fade over the last third.
                float fade = 1f - Mathf.Clamp01((_elapsed - Lifetime * 0.66f) / (Lifetime * 0.34f));
                Color c = _baseColor;
                c.a = _baseColor.a * fade;
                if (_material.HasProperty("_Color")) _material.SetColor("_Color", c);
                if (_material.HasProperty("_BaseColor")) _material.SetColor("_BaseColor", c);
            }

            if (_elapsed >= Lifetime) Destroy(gameObject);
        }

        private void OnDestroy()
        {
            Live.Remove(this);
            if (_material != null) Destroy(_material);
        }
    }
}
