using UnityEngine;

namespace Sim.Runtime
{
    /// <summary>
    /// Central theme + drawing primitives for the IMGUI HUD, taken from the design mockup in
    /// <c>docs/design/hud/</c> (palette, panels, hairline borders, colour-coded bars, status tags).
    ///
    /// <para>
    /// KNOWN CONSTRAINT — TYPOGRAPHY IS APPROXIMATE: the mockup is set in Barlow Condensed (labels)
    /// and JetBrains Mono (readouts) with wide letter-spacing. This project may not add font assets,
    /// and IMGUI offers no letter-spacing control, so the type here can only APPROXIMATE the design
    /// using the built-in font: uppercase labels, a size/weight hierarchy and dim/bright colour steps.
    /// Everything else — colours, panel fills, 1px borders, bar geometry and colour code, layout and
    /// information hierarchy — matches the design.
    /// </para>
    ///
    /// <para>
    /// Everything is lazily initialised and cached: the 1x1 textures and the <see cref="GUIStyle"/>s
    /// are built once on the first <c>OnGUI</c> call (styles need <c>GUI.skin</c>, which only exists
    /// on the GUI thread). ALL members are therefore only safe to call from <c>OnGUI</c>.
    /// </para>
    ///
    /// Presentation only: nothing here reads or writes gameplay state.
    /// </summary>
    public static class HudTheme
    {
        // ------------------------------------------------------------------ palette
        // Exactly the design's colours. NOTE: the panel-fill colour is called PanelBg (not "Panel")
        // because C# cannot have a field and the Panel(Rect) drawing helper share one name.

        /// <summary>Screen background / punch-out colour (#07090a).</summary>
        public static readonly Color Bg = Hex("#07090a");

        /// <summary>Panel body fill (#0e1214).</summary>
        public static readonly Color PanelBg = Hex("#0e1214");

        /// <summary>Panel header / inset fill (#12181a).</summary>
        public static readonly Color PanelAlt = Hex("#12181a");

        /// <summary>Hairline border and divider colour (#2b3336).</summary>
        public static readonly Color Line = Hex("#2b3336");

        /// <summary>Amber accent (#e2a13f).</summary>
        public static readonly Color Amber = Hex("#e2a13f");

        /// <summary>Friendly / healthy green (#35c39a).</summary>
        public static readonly Color Ok = Hex("#35c39a");

        /// <summary>Hostile / critical red (#e05243).</summary>
        public static readonly Color Critical = Hex("#e05243");

        /// <summary>Primary text (#ece7dd).</summary>
        public static readonly Color Text = Hex("#ece7dd");

        /// <summary>Secondary / label text (#8f8a80).</summary>
        public static readonly Color TextDim = Hex("#8f8a80");

        /// <summary>Even dimmer text used for hints and inactive chips (#6f6a62).</summary>
        public static readonly Color TextFaint = Hex("#6f6a62");

        /// <summary>Dark inset used as a bar track (#0f1416).</summary>
        public static readonly Color Track = Hex("#0f1416");

        // ------------------------------------------------------------------ styles

        /// <summary>Large panel/mission title.</summary>
        public static GUIStyle Title;

        /// <summary>Small, wide, amber section heading (callers pass UPPERCASE text).</summary>
        public static GUIStyle SectionLabel;

        /// <summary>Ordinary body label.</summary>
        public static GUIStyle Label;

        /// <summary>Brighter, larger numeric readout.</summary>
        public static GUIStyle Value;

        /// <summary>Small dim caption.</summary>
        public static GUIStyle Small;

        /// <summary>Small dim caption, right aligned (bar readouts).</summary>
        public static GUIStyle SmallRight;

        /// <summary>Bold red warning text.</summary>
        public static GUIStyle Warning;

        /// <summary>Very large centred verdict text (end screen).</summary>
        public static GUIStyle Verdict;

        /// <summary>Centred body text (banners, hint strips).</summary>
        public static GUIStyle Centered;

        /// <summary>Small centred text used inside status tags.</summary>
        public static GUIStyle TagText;

        // Sized/anchored variants of the verdict face. These are exposed as methods rather than
        // fields because the call sites need a specific size+anchor pairing, and Draw() temporarily
        // overwrites a style's colour — sharing one instance across differently aligned call sites
        // would make the alignment order-dependent.

        /// <summary>24px bold, left aligned — big numeric readouts (speed, altitude).</summary>
        public static GUIStyle Verdict24() { Ensure(); return _verdict24; }

        /// <summary>28px bold, left aligned — the missile-warning band.</summary>
        public static GUIStyle Verdict28() { Ensure(); return _verdict28; }

        /// <summary>28px bold, centred — the end-screen star rating.</summary>
        public static GUIStyle Verdict28C() { Ensure(); return _verdict28C; }

        // ------------------------------------------------------------------ internals

        private static Texture2D _white;
        private static GUIStyle _verdict24;
        private static GUIStyle _verdict28;
        private static GUIStyle _verdict28C;

        /// <summary>
        /// Builds the cached texture and styles on first use. Must be called at the top of every
        /// <c>OnGUI</c> before any other member is used.
        /// </summary>
        public static void Ensure()
        {
            if (_white == null)
            {
                _white = new Texture2D(1, 1);
                _white.SetPixel(0, 0, Color.white);
                _white.Apply();
                // Never serialised into a scene or asset.
                _white.hideFlags = HideFlags.DontSave;
            }

            if (Label != null) return;

            Label = Make(12, FontStyle.Normal, Text, TextAnchor.MiddleLeft);
            Title = Make(17, FontStyle.Bold, Text, TextAnchor.UpperLeft);
            Title.wordWrap = true;
            SectionLabel = Make(11, FontStyle.Bold, Amber, TextAnchor.MiddleLeft);
            Value = Make(16, FontStyle.Bold, Text, TextAnchor.MiddleLeft);
            Small = Make(10, FontStyle.Normal, TextDim, TextAnchor.MiddleLeft);
            SmallRight = Make(10, FontStyle.Normal, TextDim, TextAnchor.MiddleRight);
            Warning = Make(14, FontStyle.Bold, Critical, TextAnchor.MiddleLeft);
            Verdict = Make(40, FontStyle.Bold, Text, TextAnchor.MiddleCenter);
            Centered = Make(12, FontStyle.Normal, Text, TextAnchor.MiddleCenter);
            TagText = Make(10, FontStyle.Bold, Amber, TextAnchor.MiddleCenter);

            _verdict24 = Make(24, FontStyle.Bold, Text, TextAnchor.MiddleLeft);
            _verdict28 = Make(28, FontStyle.Bold, Text, TextAnchor.MiddleLeft);
            _verdict28C = Make(28, FontStyle.Bold, Text, TextAnchor.MiddleCenter);
        }

        /// <summary>Creates a padding-free style derived from the built-in label skin.</summary>
        private static GUIStyle Make(int size, FontStyle weight, Color color, TextAnchor anchor)
        {
            var s = new GUIStyle(GUI.skin.label)
            {
                fontSize = size,
                fontStyle = weight,
                alignment = anchor,
                wordWrap = false,
                richText = false
            };
            s.padding = new RectOffset(0, 0, 0, 0);
            s.margin = new RectOffset(0, 0, 0, 0);
            s.normal.textColor = color;
            return s;
        }

        // ------------------------------------------------------------------ colour helper

        /// <summary>Parses "#rrggbb" / "#rrggbbaa" (leading '#' optional) into a colour.</summary>
        public static Color Hex(string hex)
        {
            if (string.IsNullOrEmpty(hex)) return Color.magenta;
            string s = hex[0] == '#' ? hex.Substring(1) : hex;
            if (s.Length != 6 && s.Length != 8) return Color.magenta;

            byte r = (byte)Byte2(s, 0);
            byte g = (byte)Byte2(s, 2);
            byte b = (byte)Byte2(s, 4);
            byte a = s.Length == 8 ? (byte)Byte2(s, 6) : (byte)255;
            return new Color32(r, g, b, a);
        }

        private static int Byte2(string s, int index)
        {
            return Digit(s[index]) * 16 + Digit(s[index + 1]);
        }

        private static int Digit(char c)
        {
            if (c >= '0' && c <= '9') return c - '0';
            if (c >= 'a' && c <= 'f') return c - 'a' + 10;
            if (c >= 'A' && c <= 'F') return c - 'A' + 10;
            return 0;
        }

        /// <summary>The design's bar colour code: green above 50%, amber 20–50%, red below 20%.</summary>
        public static Color BarColor(float fraction01)
        {
            if (fraction01 > 0.5f) return Ok;
            if (fraction01 >= 0.2f) return Amber;
            return Critical;
        }

        // ------------------------------------------------------------------ drawing primitives

        /// <summary>Fills a rect with a flat colour.</summary>
        public static void Fill(Rect r, Color c)
        {
            if (_white == null) return;
            Color prev = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, _white);
            GUI.color = prev;
        }

        /// <summary>Draws a 1px horizontal divider across the given rect's top edge.</summary>
        public static void HLine(Rect r)
        {
            Fill(new Rect(r.x, r.y, r.width, 1f), Line);
        }

        /// <summary>Strokes a 1px border around a rect.</summary>
        public static void Border(Rect r, Color c)
        {
            Fill(new Rect(r.x, r.y, r.width, 1f), c);
            Fill(new Rect(r.x, r.yMax - 1f, r.width, 1f), c);
            Fill(new Rect(r.x, r.y, 1f, r.height), c);
            Fill(new Rect(r.xMax - 1f, r.y, 1f, r.height), c);
        }

        /// <summary>A design panel: flat dark fill with a 1px hairline border.</summary>
        public static void Panel(Rect r)
        {
            Fill(r, PanelBg);
            Border(r, Line);
        }

        /// <summary>An inset box (bar track colour) with a hairline border.</summary>
        public static void Inset(Rect r)
        {
            Fill(r, Track);
            Border(r, Line);
        }

        /// <summary>
        /// A panel header strip: alternate fill, a bottom hairline, an amber section label on the
        /// left and an optional dim caption on the right.
        /// </summary>
        public static void Header(Rect r, string left, string right)
        {
            Fill(r, PanelAlt);
            Fill(new Rect(r.x, r.yMax - 1f, r.width, 1f), Line);
            if (!string.IsNullOrEmpty(left))
                GUI.Label(new Rect(r.x + 8f, r.y, r.width - 16f, r.height), left, SectionLabel);
            if (!string.IsNullOrEmpty(right))
                GUI.Label(new Rect(r.x + 8f, r.y, r.width - 16f, r.height), right, SmallRight);
        }

        /// <summary>Draws text in the given style temporarily tinted to <paramref name="c"/>.</summary>
        public static void Draw(Rect r, string text, GUIStyle style, Color c)
        {
            if (style == null) return;
            Color prev = style.normal.textColor;
            style.normal.textColor = c;
            GUI.Label(r, text, style);
            style.normal.textColor = prev;
        }

        /// <summary>
        /// A labelled gauge: dark track, colour-coded fill (green &gt;50%, amber 20–50%, red &lt;20%),
        /// 1px border, the label on the left and the readout on the right. Pass an empty label or
        /// readout to drop that side and give the track the space.
        /// </summary>
        public static void Bar(Rect r, float fraction01, string label, string readout)
        {
            float f = Mathf.Clamp01(fraction01);

            float labelW = string.IsNullOrEmpty(label) ? 0f : 42f;
            float readoutW = string.IsNullOrEmpty(readout) ? 0f : 40f;
            float trackW = Mathf.Max(6f, r.width - labelW - readoutW);
            var track = new Rect(r.x + labelW, r.y, trackW, r.height);

            if (labelW > 0f)
                GUI.Label(new Rect(r.x, r.y - 2f, labelW - 4f, r.height + 4f), label, Small);

            Fill(track, Track);
            Color fillColor = BarColor(f);
            if (f > 0f)
            {
                Fill(new Rect(track.x + 1f, track.y + 1f,
                              Mathf.Max(1f, (track.width - 2f) * f), track.height - 2f), fillColor);
            }
            Border(track, Line);

            if (readoutW > 0f)
                Draw(new Rect(track.xMax + 4f, r.y - 2f, readoutW - 4f, r.height + 4f),
                     readout, SmallRight, fillColor);
        }

        /// <summary>
        /// A small outlined status chip (DEVRİYE / ANGAJE / DÖNÜŞ / İKMAL / ART YAKICI / KAÇIŞ …):
        /// a very dark tint of the accent, a 1px accent border and the accent-coloured caption.
        /// </summary>
        public static void Tag(Rect r, string text, Color accent)
        {
            Fill(r, new Color(accent.r * 0.16f, accent.g * 0.16f, accent.b * 0.16f, 0.92f));
            Border(r, accent);
            Draw(r, text, TagText, accent);
        }

        /// <summary>
        /// A thin gapped cross with short ranging ticks, built purely from <see cref="Fill"/> rects
        /// (IMGUI has no line drawing here). <paramref name="size"/> is the full arm-to-arm span.
        /// </summary>
        public static void Crosshair(Vector2 centre, float size, Color c)
        {
            float arm = size * 0.5f;
            float gap = size * 0.16f;
            float armLen = Mathf.Max(1f, arm - gap);

            // Cross arms, left/right and up/down, with a gap around the centre.
            Fill(new Rect(centre.x - arm, centre.y - 0.5f, armLen, 1f), c);
            Fill(new Rect(centre.x + gap, centre.y - 0.5f, armLen, 1f), c);
            Fill(new Rect(centre.x - 0.5f, centre.y - arm, 1f, armLen), c);
            Fill(new Rect(centre.x - 0.5f, centre.y + gap, 1f, armLen), c);

            // Centre pip.
            Fill(new Rect(centre.x - 1f, centre.y - 1f, 2f, 2f), c);

            // Short ranging ticks just outside each arm.
            float tick = Mathf.Max(4f, size * 0.18f);
            Fill(new Rect(centre.x - arm - 7f, centre.y - tick * 0.5f, 1f, tick), c);
            Fill(new Rect(centre.x + arm + 6f, centre.y - tick * 0.5f, 1f, tick), c);
            Fill(new Rect(centre.x - tick * 0.5f, centre.y - arm - 7f, tick, 1f), c);
            Fill(new Rect(centre.x - tick * 0.5f, centre.y + arm + 6f, tick, 1f), c);
        }

        /// <summary>
        /// A warning triangle approximated with a handful of stacked <see cref="Fill"/> rects (there
        /// is no SVG in IMGUI) plus a punched-out exclamation mark. Deliberately simple and legible.
        /// </summary>
        public static void WarningTriangle(Rect r, Color c)
        {
            const int rows = 8;
            float rowH = r.height / rows;
            for (int i = 0; i < rows; i++)
            {
                float t = (i + 1) / (float)rows;
                float w = r.width * t;
                Fill(new Rect(r.x + (r.width - w) * 0.5f, r.y + i * rowH, w, rowH + 0.5f), c);
            }

            // Exclamation mark, punched out in the background colour.
            float barW = Mathf.Max(2f, r.width * 0.11f);
            Fill(new Rect(r.center.x - barW * 0.5f, r.y + r.height * 0.40f, barW, r.height * 0.28f), Bg);
            Fill(new Rect(r.center.x - barW * 0.5f, r.y + r.height * 0.76f, barW, barW), Bg);
        }
    }
}
