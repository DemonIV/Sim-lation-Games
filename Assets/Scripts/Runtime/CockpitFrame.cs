using UnityEngine;

namespace Sim.Runtime
{
    /// <summary>
    /// A procedural cockpit interior built from primitives and parented to the CAMERA (not to the
    /// aircraft), so the pilot really sits behind a windscreen: instrument coaming, glare shield,
    /// windscreen bow, A-pillars, canopy side rails, a tinted canopy and a suggestion of the nose
    /// out ahead.
    ///
    /// <para>Why the camera and not the airframe: the unit roots carry non-uniform scales that the
    /// "Model" child cancels, so an airframe-local cockpit would be sheared and would have to be
    /// re-tuned per aircraft. Hanging it off the camera keeps the framing exact.</para>
    ///
    /// <para>Every piece is laid out as a FRACTION of the camera's real frustum at the frame
    /// distance, never as a hardcoded metre value, so the cockpit frames the view identically at
    /// any field of view or aspect ratio. The layout is re-run whenever either drifts.</para>
    ///
    /// Purely cosmetic: no colliders, no shadows, no gameplay state.
    /// </summary>
    public class CockpitFrame : MonoBehaviour
    {
        // ---------------------------------------------------------------- tuning

        /// <summary>Nominal distance from the lens to the frame plane, in metres.</summary>
        private const float BaseFrameDistance = 0.6f;

        /// <summary>Frame bar depth, as a fraction of the frame distance.</summary>
        private const float ThicknessFraction = 0.08f;

        /// <summary>Dashboard rake: negative tips the coaming's top edge toward the pilot.</summary>
        private const float CoamingTiltDeg = -12f;

        /// <summary>Re-layout thresholds. Small enough to track the afterburner FOV kick smoothly,
        /// large enough that a still camera never re-lays out at all.</summary>
        private const float FovEpsilon = 0.25f;
        private const float AspectEpsilon = 0.005f;

        /// <summary>Neutral airframe grey used until the piloted aircraft's colour is known.</summary>
        private static readonly Color DefaultBodyColor = new Color(0.55f, 0.57f, 0.60f);

        // ---------------------------------------------------------------- state

        private Camera _camera;
        private Color _bodyColor = DefaultBodyColor;
        private bool _built;

        // Cached frustum the current layout was computed for.
        private float _lastFov = -1f;
        private float _lastAspect = -1f;

        // Pieces. Any of them may be null: a piece whose material could not be created is skipped
        // rather than drawn with a missing shader.
        private Transform _coaming;
        private Transform _lip;
        private Transform _bow;
        private Transform _pillarL;
        private Transform _pillarR;
        private Transform _railL;
        private Transform _railR;
        private Transform _noseMain;
        private Transform _noseTip;
        private Transform _glass;
        private Transform _glowL;
        private Transform _glowC;
        private Transform _glowR;

        // CreateTransparent hands back an UNCACHED material, so this instance is ours to destroy.
        private Material _glassMaterial;

        // ---------------------------------------------------------------- public API

        /// <summary>Builds (or returns the existing) cockpit interior on the given camera.</summary>
        public static CockpitFrame Attach(Camera cam)
        {
            return Attach(cam, DefaultBodyColor);
        }

        /// <summary>
        /// Builds (or returns the existing) cockpit interior on the given camera, colouring the nose
        /// ahead of the windscreen with <paramref name="bodyColor"/> so it matches the airframe.
        /// Returns null when there is no camera to attach to.
        /// </summary>
        public static CockpitFrame Attach(Camera cam, Color bodyColor)
        {
            if (cam == null) return null;

            // Idempotent: a second call just re-tints the frame that is already there.
            CockpitFrame existing = cam.GetComponentInChildren<CockpitFrame>(true);
            if (existing != null)
            {
                existing.SetBodyColor(bodyColor);
                return existing;
            }

            var go = new GameObject("CockpitFrame");
            go.transform.SetParent(cam.transform, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;

            var frame = go.AddComponent<CockpitFrame>();
            frame._camera = cam;
            frame._bodyColor = bodyColor;
            frame.Build();
            frame.Layout();
            return frame;
        }

        /// <summary>Shows/hides the whole cockpit interior.</summary>
        public void SetVisible(bool visible)
        {
            if (gameObject.activeSelf == visible) return;
            gameObject.SetActive(visible);
        }

        /// <summary>
        /// Re-tints the nose out ahead so it matches the aircraft currently being flown. Cheap and
        /// idempotent: identical colours are ignored, and the materials come from the shared cache.
        /// </summary>
        public void SetBodyColor(Color color)
        {
            if (SameColor(_bodyColor, color)) return;
            _bodyColor = color;
            ApplyNoseColor();
        }

        // ---------------------------------------------------------------- Unity

        private void Awake()
        {
            // Attach() sets this explicitly; the fallback covers a frame authored into a scene.
            if (_camera == null) _camera = GetComponentInParent<Camera>();
        }

        private void OnEnable()
        {
            // The layout is not maintained while hidden, so refresh it on the way back in.
            if (_built) Layout();
        }

        private void LateUpdate()
        {
            if (_camera == null) return;

            // Cheap, allocation-free drift check: the afterburner FOV kick re-lays out for a few
            // frames while it ramps, which is only a handful of transform writes.
            if (Mathf.Abs(_camera.fieldOfView - _lastFov) > FovEpsilon ||
                Mathf.Abs(_camera.aspect - _lastAspect) > AspectEpsilon)
            {
                Layout();
            }
        }

        private void OnDestroy()
        {
            if (_glassMaterial != null) Destroy(_glassMaterial);
            _glassMaterial = null;
        }

        // ---------------------------------------------------------------- build

        /// <summary>Creates every piece once, at identity; <see cref="Layout"/> places them.</summary>
        private void Build()
        {
            if (_built) return;
            _built = true;

            Material coamingMat = MaterialLibrary.Create(new Color(0.055f, 0.058f, 0.062f), 0.05f, 0.10f);
            Material lipMat = MaterialLibrary.Create(new Color(0.125f, 0.130f, 0.135f), 0.15f, 0.25f);
            Material frameMat = MaterialLibrary.Create(new Color(0.090f, 0.095f, 0.100f), 0.20f, 0.25f);
            Material railMat = MaterialLibrary.Create(new Color(0.065f, 0.068f, 0.072f), 0.15f, 0.20f);
            Material noseMat = MaterialLibrary.Create(_bodyColor, 0.3f, 0.45f);
            Material noseTipMat = MaterialLibrary.Create(Shade(_bodyColor, 0.85f), 0.3f, 0.45f);

            // Dim decorative panel lights. The real instrumentation is the HUD, so these must read
            // as scenery and never compete with it.
            Material amberMat = MaterialLibrary.Create(new Color(0.55f, 0.35f, 0.10f), 0f, 0.3f,
                                                       new Color(0.45f, 0.24f, 0.04f));
            Material tealMat = MaterialLibrary.Create(new Color(0.12f, 0.45f, 0.45f), 0f, 0.3f,
                                                      new Color(0.05f, 0.30f, 0.30f));

            _coaming = Piece("Coaming", coamingMat);
            _lip = Piece("GlareShieldLip", lipMat);
            _bow = Piece("WindscreenBow", frameMat);
            _pillarL = Piece("APillarL", frameMat);
            _pillarR = Piece("APillarR", frameMat);
            _railL = Piece("CanopyRailL", railMat);
            _railR = Piece("CanopyRailR", railMat);
            _noseMain = Piece("NoseDeck", noseMat);
            _noseTip = Piece("NoseTip", noseTipMat);
            _glowL = Piece("PanelGlowL", amberMat);
            _glowC = Piece("PanelGlowC", tealMat);
            _glowR = Piece("PanelGlowR", amberMat);

            // Canopy tint. Barely there by design — it must not darken the view. If the transparent
            // material cannot be built we simply fly without glass rather than draw an opaque pane.
            _glassMaterial = MaterialLibrary.CreateTransparent(new Color(0.62f, 0.72f, 0.82f, 0.06f));
            if (_glassMaterial != null) _glass = Piece("CanopyGlass", _glassMaterial);
        }

        /// <summary>
        /// Spawns one collider-less cube under the frame root, mirroring
        /// <see cref="VehicleModelBuilder"/>'s convention. Returns null when the material is
        /// missing, so the caller simply ends up without that piece.
        /// </summary>
        private Transform Piece(string pieceName, Material material)
        {
            if (material == null) return null;

            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = pieceName;

            // Cosmetic geometry must never add physics.
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);

            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;

            // Centimetres from the lens: shadows here would only smear over the whole screen.
            var r = go.GetComponent<Renderer>();
            if (r != null)
            {
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
            }

            MaterialLibrary.Apply(go, material);
            return go.transform;
        }

        // ---------------------------------------------------------------- layout

        /// <summary>
        /// Distance from the lens to the frame plane. The canopy glass sits at 0.9 of it, so keeping
        /// the frame at twice the near plane leaves even the nearest piece safely in front of it —
        /// a camera with an unusually large near plane pushes the whole cockpit out instead of us
        /// touching the camera's clip planes.
        /// </summary>
        private float FrameDistance()
        {
            float near = Mathf.Max(_camera.nearClipPlane, 0.001f);
            return Mathf.Max(BaseFrameDistance, near * 2f);
        }

        /// <summary>
        /// Places every piece as a fraction of the camera frustum's half-extents at the frame
        /// distance, in camera-local space (+Z forward, +Y up, +X right).
        /// </summary>
        private void Layout()
        {
            if (_camera == null) return;

            float fov = Mathf.Clamp(_camera.fieldOfView, 1f, 179f);
            float aspect = _camera.aspect;
            if (aspect <= 0f || float.IsNaN(aspect) || float.IsInfinity(aspect)) return;

            float d = FrameDistance();
            float halfH = d * Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad);
            float halfW = halfH * aspect;
            if (halfH <= 0f) return;

            _lastFov = _camera.fieldOfView;
            _lastAspect = aspect;

            // Frame bar depth, so nothing is a paper-thin sliver at low FOV.
            float t = d * ThicknessFraction;

            // ---- instrument coaming: full width, bottom edge to ~28% of the view height, raked.
            // Visible top edge lands at -0.44 * halfH, i.e. 28% of the full 2*halfH view height.
            Quaternion tilt = Quaternion.Euler(CoamingTiltDeg, 0f, 0f);
            var coamingCenter = new Vector3(0f, -0.77f * halfH, d);
            Place(_coaming, coamingCenter,
                  new Vector3(halfW * 2.2f, halfH * 0.66f, t), tilt);

            // ---- glare shield lip: rides the coaming's top edge, pushed toward the pilot so it
            // reads as a separate, overhanging part. Offsets are taken in the coaming's own frame.
            Place(_lip, coamingCenter + tilt * new Vector3(0f, halfH * 0.33f, -t * 0.45f),
                  new Vector3(halfW * 2.1f, halfH * 0.055f, t * 0.9f), tilt);

            // ---- windscreen bow: ~8% of the view height thick, just inside the top edge, with the
            // rest of the bar overscanning above it so no sliver of sky leaks past the corner.
            Place(_bow, new Vector3(0f, 0.99f * halfH, d),
                  new Vector3(halfW * 2.2f, halfH * 0.32f, t), Quaternion.identity);

            // ---- A-pillars: coaming ends up to the ends of the bow, converging slightly. Each is
            // 0.14 * halfW wide, i.e. 7% of the full view width, so the forward view stays open.
            Pillar(_pillarR, 0.90f * halfW, -0.42f * halfH, 0.74f * halfW, 0.86f * halfH,
                   halfW * 0.14f, t);
            Pillar(_pillarL, -0.90f * halfW, -0.42f * halfH, -0.74f * halfW, 0.86f * halfH,
                   halfW * 0.14f, t);

            // ---- canopy side rails: full-height dark bars hugging the left/right edges.
            var railScale = new Vector3(halfW * 0.16f, halfH * 2.2f, t);
            Place(_railL, new Vector3(-0.97f * halfW, 0f, d), railScale, Quaternion.identity);
            Place(_railR, new Vector3(0.97f * halfW, 0f, d), railScale, Quaternion.identity);

            // ---- the nose out ahead, three times further out. Its own frustum half-extents scale
            // with the distance, so the wedge keeps the same screen footprint at any FOV: the deck's
            // top edge sits at -0.31 of the half-height, just above the coaming's -0.44, which is
            // what makes the pilot feel like they are looking OVER the nose.
            float nd = d * 3f;
            float nh = halfH * 3f;
            float nw = halfW * 3f;
            Place(_noseMain, new Vector3(0f, -0.56f * nh, nd),
                  new Vector3(nw * 0.90f, nh * 0.50f, nd * 0.50f), Quaternion.Euler(5f, 0f, 0f));
            Place(_noseTip, new Vector3(0f, -0.66f * nh, nd * 1.45f),
                  new Vector3(nw * 0.50f, nh * 0.34f, nd * 0.55f), Quaternion.Euler(8f, 0f, 0f));

            // ---- canopy tint, slightly nearer than the frame and covering the whole view.
            float gd = d * 0.9f;
            Place(_glass, new Vector3(0f, 0f, gd),
                  new Vector3(halfW * 1.9f, halfH * 1.9f, gd * 0.01f), Quaternion.identity);

            // ---- decorative panel glows, sitting on the raked coaming face.
            var glowScale = new Vector3(halfW * 0.18f, halfH * 0.05f, t * 0.25f);
            float glowOut = -(t * 0.5f + d * 0.012f);
            Place(_glowL, coamingCenter + tilt * new Vector3(-0.42f * halfW, halfH * 0.17f, glowOut),
                  glowScale, tilt);
            Place(_glowC, coamingCenter + tilt * new Vector3(0f, halfH * 0.10f, glowOut),
                  glowScale, tilt);
            Place(_glowR, coamingCenter + tilt * new Vector3(0.42f * halfW, halfH * 0.17f, glowOut),
                  glowScale, tilt);
        }

        /// <summary>Sets a piece's local pose. Null pieces (missing material) are skipped.</summary>
        private static void Place(Transform piece, Vector3 localPosition, Vector3 localScale,
                                  Quaternion localRotation)
        {
            if (piece == null) return;
            piece.localPosition = localPosition;
            piece.localRotation = localRotation;
            piece.localScale = localScale;
        }

        /// <summary>
        /// Lays a bar between two points in the frame plane, rotating it about Z so its local +Y
        /// runs from the bottom point to the top one. Slightly overlong so it beds into the coaming
        /// and the bow instead of leaving a seam.
        /// </summary>
        private void Pillar(Transform piece, float x0, float y0, float x1, float y1,
                            float width, float thickness)
        {
            if (piece == null) return;

            float dx = x1 - x0;
            float dy = y1 - y0;
            float length = Mathf.Sqrt(dx * dx + dy * dy);
            if (length <= 1e-5f) return;

            // A Z rotation of a maps local +Y to (-sin a, cos a), so this aligns +Y with (dx, dy).
            float angle = Mathf.Atan2(-dx, dy) * Mathf.Rad2Deg;

            Place(piece,
                  new Vector3((x0 + x1) * 0.5f, (y0 + y1) * 0.5f, FrameDistance()),
                  new Vector3(width, length * 1.06f, thickness),
                  Quaternion.Euler(0f, 0f, angle));
        }

        // ---------------------------------------------------------------- helpers

        /// <summary>(Re)applies the airframe colour to the two nose pieces.</summary>
        private void ApplyNoseColor()
        {
            Material noseMat = MaterialLibrary.Create(_bodyColor, 0.3f, 0.45f);
            if (noseMat != null && _noseMain != null) MaterialLibrary.Apply(_noseMain.gameObject, noseMat);

            Material tipMat = MaterialLibrary.Create(Shade(_bodyColor, 0.85f), 0.3f, 0.45f);
            if (tipMat != null && _noseTip != null) MaterialLibrary.Apply(_noseTip.gameObject, tipMat);
        }

        /// <summary>Multiplies RGB by <paramref name="factor"/>, keeping alpha.</summary>
        private static Color Shade(Color c, float factor)
        {
            return new Color(c.r * factor, c.g * factor, c.b * factor, c.a);
        }

        /// <summary>Cheap RGB equality, so re-tinting with the same colour costs nothing.</summary>
        private static bool SameColor(Color a, Color b)
        {
            return Mathf.Abs(a.r - b.r) < 0.004f &&
                   Mathf.Abs(a.g - b.g) < 0.004f &&
                   Mathf.Abs(a.b - b.b) < 0.004f;
        }
    }
}
