using System.Collections.Generic;
using UnityEngine;

namespace Arsenal.Weapons
{
    /// <summary>Which reticle a scoped weapon shows when aimed (the model is hidden and the view goes through the lens).</summary>
    public enum ScopeKind { None, Sniper, Rail, Tactical, Launcher }

    /// <summary>
    /// Scope view: a dark tube with a clear round window and a reticle. The texture is built at the screen's
    /// height so lines stay crisp; the window itself is fully transparent (only a soft shadow at the rim).
    /// </summary>
    internal static class ScopeOverlay
    {
        /// <summary>A scope texture: its pixels are computed on a worker thread, then uploaded once on the main thread.</summary>
        private sealed class Entry
        {
            public Color32[] Pixels;
            public Texture2D Tex;
            public volatile bool Done;
        }

        private static readonly Dictionary<(ScopeKind, Color, int), Entry> Cache = new Dictionary<(ScopeKind, Color, int), Entry>();
        private static Texture2D _pixel;

        private static readonly Color Tube = new Color(0.035f, 0.04f, 0.047f, 1f);
        private static readonly Color Ink = new Color(0.03f, 0.035f, 0.04f, 1f);

        /// <summary>Window size relative to the screen height: sniper scopes are tight, tactical optics wide.</summary>
        private static float Size(ScopeKind kind)
        {
            switch (kind)
            {
                case ScopeKind.Tactical: return 1.22f;
                case ScopeKind.Launcher: return 1.12f;
                default: return 1.02f;
            }
        }

        private static Texture2D Pixel
        {
            get
            {
                if (_pixel != null) return _pixel;
                _pixel = new Texture2D(1, 1);
                _pixel.SetPixel(0, 0, Tube);
                _pixel.Apply();
                return _pixel;
            }
        }

        /// <summary>Aim blend at which the eye reaches the eyepiece: the weapon model hides and the scope view starts to open.</summary>
        public const float EyeAt = 0.62f;

        /// <summary>
        /// Draws the scope for an aim blend (0..1) in two beats: while the weapon comes up, a dark tube closes in from the
        /// screen edges (the eye nearing the eyepiece); at <see cref="EyeAt"/> the window is a small circle, then it opens
        /// onto the magnified view with its reticle.
        /// </summary>
        public static void Draw(ScopeKind kind, Color tint, float aimBlend)
        {
            if (kind == ScopeKind.None || aimBlend <= 0.3f) return;
            Texture2D view = Texture(kind, tint);
            if (view == null) return;
            float h = Screen.height;
            if (aimBlend < EyeAt)
            {
                float c = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.3f, EyeAt, aimBlend));
                Window(EmptyTube, h * Size(kind) * Mathf.Lerp(3.4f, 0.28f, c), Mathf.Sqrt(c));
            }
            else
            {
                float o = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(EyeAt, 1f, aimBlend));
                Window(view, h * Size(kind) * Mathf.Lerp(0.28f, 1f, 1f - (1f - o) * (1f - o)), 1f);
            }
        }

        /// <summary>A round window of that size centred on screen, everything around it dark.</summary>
        private static void Window(Texture2D tex, float size, float alpha)
        {
            float h = Screen.height, w = Screen.width;
            size = Mathf.Round(size);
            float x = (w - size) / 2f, y = (h - size) / 2f;
            Color saved = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, alpha);
            GUI.DrawTexture(new Rect(x, y, size, size), tex);
            if (x > 0)
            {
                GUI.DrawTexture(new Rect(0, 0, x + 1, h), Pixel);
                GUI.DrawTexture(new Rect(x + size - 1, 0, w - x - size + 1, h), Pixel);
            }
            if (y > 0)
            {
                GUI.DrawTexture(new Rect(0, 0, w, y + 1), Pixel);
                GUI.DrawTexture(new Rect(0, y + size - 1, w, h - y - size + 1), Pixel);
            }
            GUI.color = saved;
        }

        private static Texture2D _tube;

        /// <summary>The empty tube (dark rim, clear window, no reticle) used while the eye nears the eyepiece. Small: it is only ever seen blurred by motion.</summary>
        private static Texture2D EmptyTube
        {
            get
            {
                if (_tube != null) return _tube;
                const int s = 256;
                _tube = new Texture2D(s, s, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear, name = "Arsenal.Scope.Tube" };
                _tube.SetPixels32(Compute(ScopeKind.None, Color.white, s));
                _tube.Apply(true);
                return _tube;
            }
        }

        private static int TexSize(ScopeKind kind) => Mathf.Clamp(Mathf.CeilToInt(Screen.height * Size(kind) / 64f) * 64, 512, 2560);

        /// <summary>Starts building this scope in the background (call when the weapon is taken in hand) so aiming never stalls.</summary>
        public static void Prewarm(ScopeKind kind, Color tint)
        {
            if (kind != ScopeKind.None) GetEntry(kind, tint, TexSize(kind));
        }

        /// <summary>True once the scope can be drawn; until then the weapon keeps its model on screen.</summary>
        public static bool Ready(ScopeKind kind, Color tint) => kind == ScopeKind.None || Texture(kind, tint) != null;

        private static Texture2D Texture(ScopeKind kind, Color tint)
        {
            Entry e = GetEntry(kind, tint, TexSize(kind));
            if (e.Tex == null && e.Done && e.Pixels != null)
            {
                int s = (int)Mathf.Sqrt(e.Pixels.Length);
                e.Tex = new Texture2D(s, s, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear, name = "Arsenal.Scope." + kind };
                e.Tex.SetPixels32(e.Pixels);
                e.Tex.Apply(true);
                e.Pixels = null;
            }
            return e.Tex;
        }

        private static Entry GetEntry(ScopeKind kind, Color tint, int s)
        {
            if (Cache.TryGetValue((kind, tint, s), out Entry e)) return e;
            e = new Entry();
            Cache[(kind, tint, s)] = e;
            System.Threading.ThreadPool.QueueUserWorkItem(_ =>
            {
                try { e.Pixels = Compute(kind, tint, s); }
                catch (System.Exception ex) { ArsenalMod.Log.Warning($"Scope {kind} failed: {ex.Message}"); }
                e.Done = true;
            });
            return e;
        }

        // ------------------------------------------------------------------ texture

        /// <summary>Pure maths (no Unity objects), safe on a worker thread.</summary>
        private static Color32[] Compute(ScopeKind kind, Color tint, int s)
        {
            var px = new Color32[s * s];
            float c = (s - 1) / 2f, r = s * 0.49f;
            float u = r / 240f; // reticle unit: sizes below are given for a 240 px window radius
            float line = Mathf.Max(0.7f, 0.55f * u); // hair-line half width, never thinner than a pixel
            for (int iy = 0; iy < s; iy++)
                for (int ix = 0; ix < s; ix++)
                {
                    var p = new Vector2(ix - c, iy - c); // y up (texture row 0 is the bottom of the rect)
                    float d = p.magnitude;
                    // tube: opaque outside, a thin bright rim, then a soft eye-relief shadow fading to a clear centre
                    float outside = Mathf.Clamp01((d - r) / 1.5f + 0.5f);
                    float shade = Mathf.Pow(Mathf.Clamp01((d - r * 0.78f) / (r * 0.22f)), 2.2f) * 0.8f;
                    Color col = Tube;
                    col.a = Mathf.Max(outside, shade);
                    if (d < r)
                    {
                        float rim = Cov(Mathf.Abs(d - (r - 2.5f * u)), 0.6f * u) * 0.35f;
                        col = Over(col, Color.Lerp(tint, Color.white, 0.5f), rim);
                        Reticle(kind, p / u, d / u, r / u, line / u, u, out float ink, out float glow);
                        col = Over(col, Ink, ink);
                        col = Over(col, tint, glow);
                    }
                    px[iy * s + ix] = col;
                }
            return px;
        }

        /// <summary>Paints <paramref name="top"/> over <paramref name="under"/> with the given coverage.</summary>
        private static Color Over(Color under, Color top, float cov)
        {
            if (cov <= 0f) return under;
            float a = cov + under.a * (1f - cov);
            if (a <= 0f) return Color.clear;
            Color o = (top * cov + under * under.a * (1f - cov)) / a;
            o.a = a;
            return o;
        }

        /// <summary>Anti-aliased coverage of a stroke given the distance to its centre line and its half width (pixels).</summary>
        private static float Cov(float dist, float half) => Mathf.Clamp01(half + 0.5f - dist);

        private static float Seg(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return (p - (a + ab * t)).magnitude;
        }

        /// <summary>
        /// Reticle coverage at point p (reticle units, y up). <paramref name="ink"/> is dark etched lines,
        /// <paramref name="glow"/> the illuminated parts drawn in the weapon's colour. Distances are turned back into
        /// pixels with <paramref name="u"/> before computing coverage so every stroke is anti-aliased.
        /// </summary>
        private static void Reticle(ScopeKind kind, Vector2 p, float d, float R, float hair, float u, out float ink, out float glow)
        {
            ink = 0f;
            glow = 0f;
            float ax = Mathf.Abs(p.x), ay = Mathf.Abs(p.y);
            float L(float dist, float half) => Cov(dist * u, half * u);
            switch (kind)
            {
                case ScopeKind.Sniper:
                {
                    // duplex crosshair: thin in the middle, thick posts at the edge
                    ink = Mathf.Max(ink, L(ax, hair) * (ay > 3f ? 1f : 0f), L(ay, hair) * (ax > 3f ? 1f : 0f));
                    if (ay > R * 0.58f) ink = Mathf.Max(ink, L(ax, 3.2f));
                    if (ax > R * 0.58f) ink = Mathf.Max(ink, L(ay, 3.2f));
                    // mil dots on both axes
                    for (int i = 1; i <= 4; i++)
                    {
                        float m = i * 26f;
                        ink = Mathf.Max(ink, L((p - new Vector2(m, 0)).magnitude, 2f), L((p - new Vector2(-m, 0)).magnitude, 2f), L((p - new Vector2(0, m)).magnitude, 2f));
                    }
                    // holdover ladder under the centre, getting shorter
                    for (int i = 1; i <= 4; i++)
                        ink = Mathf.Max(ink, L(Seg(p, new Vector2(-14f + i * 2f, -i * 26f), new Vector2(14f - i * 2f, -i * 26f)), hair));
                    glow = L(d, 1.6f);
                    break;
                }
                case ScopeKind.Rail:
                {
                    // fine cross with an open centre, an illuminated ring and a range ladder
                    if (d > 20f)
                        ink = Mathf.Max(L(ax, hair), L(ay, hair));
                    if (ay > R * 0.62f) ink = Mathf.Max(ink, L(ax, 2.6f));
                    if (ax > R * 0.62f) ink = Mathf.Max(ink, L(ay, 2.6f));
                    glow = Mathf.Max(L(Mathf.Abs(d - 14f), 0.9f), L(d, 1.4f));
                    for (int i = 1; i <= 3; i++)
                    {
                        float yy = -18f - i * 18f, half = 16f - i * 3.5f;
                        glow = Mathf.Max(glow, L(Seg(p, new Vector2(-half, yy), new Vector2(-4f, yy)), 0.8f), L(Seg(p, new Vector2(4f, yy), new Vector2(half, yy)), 0.8f));
                    }
                    // corner brackets around the ring
                    foreach (Vector2 q in new[] { new Vector2(1, 1), new Vector2(-1, 1), new Vector2(1, -1), new Vector2(-1, -1) })
                    {
                        Vector2 a = q * 26f;
                        glow = Mathf.Max(glow, L(Seg(p, a, a - new Vector2(q.x * 7f, 0)), 0.8f), L(Seg(p, a, a - new Vector2(0, q.y * 7f)), 0.8f));
                    }
                    break;
                }
                case ScopeKind.Tactical:
                {
                    // illuminated chevron inside a horseshoe, open at the bottom; dark posts at the edges
                    float ang = Mathf.Atan2(p.x, -p.y) * Mathf.Rad2Deg; // 0 = straight down
                    if (Mathf.Abs(ang) > 32f) glow = L(Mathf.Abs(d - 36f), 1.3f);
                    glow = Mathf.Max(glow, L(Seg(p, Vector2.zero, new Vector2(-9f, -9f)), 1.2f), L(Seg(p, Vector2.zero, new Vector2(9f, -9f)), 1.2f));
                    glow = Mathf.Max(glow, L((p - new Vector2(0, -24f)).magnitude, 1.4f), L((p - new Vector2(0, -34f)).magnitude, 1.2f));
                    if (ax > R * 0.62f) ink = L(ay, 2.2f);
                    if (p.y < -R * 0.62f) ink = Mathf.Max(ink, L(ax, 2.2f));
                    if (p.y > R * 0.75f) ink = Mathf.Max(ink, L(ax, 1.2f));
                    break;
                }
                case ScopeKind.Launcher:
                {
                    // small lit cross, target box corners, a stadia line with ticks and range chevrons below
                    glow = Mathf.Max(L(Seg(p, new Vector2(-6f, 0), new Vector2(6f, 0)), 0.9f), L(Seg(p, new Vector2(0, -6f), new Vector2(0, 6f)), 0.9f));
                    const float bx = 58f, by = 40f, cl = 13f;
                    foreach (Vector2 q in new[] { new Vector2(1, 1), new Vector2(-1, 1), new Vector2(1, -1), new Vector2(-1, -1) })
                    {
                        Vector2 a = new Vector2(q.x * bx, q.y * by);
                        glow = Mathf.Max(glow, L(Seg(p, a, a - new Vector2(q.x * cl, 0)), 1.1f), L(Seg(p, a, a - new Vector2(0, q.y * cl)), 1.1f));
                    }
                    if (ax > 18f && ax < R * 0.92f)
                    {
                        ink = L(ay, hair);
                        float tick = Mathf.Abs(Mathf.Repeat(ax + 15f, 30f) - 15f);
                        if (ay < 5f) ink = Mathf.Max(ink, L(tick, hair));
                    }
                    if (p.y > 18f && p.y < R * 0.92f) ink = Mathf.Max(ink, L(ax, hair));
                    for (int i = 1; i <= 3; i++)
                    {
                        float yy = -by - 6f - i * 16f, half = 7f;
                        glow = Mathf.Max(glow, L(Seg(p, new Vector2(-half, yy + 4f), new Vector2(0, yy)), 0.8f), L(Seg(p, new Vector2(half, yy + 4f), new Vector2(0, yy)), 0.8f));
                    }
                    break;
                }
            }
        }
    }
}
