using UnityEngine;

namespace Arsenal.Weapons
{
    /// <summary>Scope view of the anti-materiel rifle: a black mask with a round window, ink-style reticle.</summary>
    internal static class ScopeOverlay
    {
        private static Texture2D _mask;
        private static Texture2D _pixel;

        private static Texture2D Mask
        {
            get
            {
                if (_mask != null) return _mask;
                const int s = 512;
                _mask = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "Arsenal.ScopeMask" };
                var px = new Color32[s * s];
                float c = (s - 1) / 2f, r = s * 0.48f;
                for (int y = 0; y < s; y++)
                    for (int x = 0; x < s; x++)
                    {
                        float dx = x - c, dy = y - c;
                        float d = Mathf.Sqrt(dx * dx + dy * dy);
                        byte a = (byte)(Mathf.Clamp01((d - r) / 3f) * 255);
                        // thin ink ring at the rim
                        if (Mathf.Abs(d - r) < 2.5f) a = 255;
                        bool cross = (Mathf.Abs(dx) < 1.2f && Mathf.Abs(dy) > 14f) || (Mathf.Abs(dy) < 1.2f && Mathf.Abs(dx) > 14f);
                        bool thick = (Mathf.Abs(dx) < 3.5f && Mathf.Abs(dy) > r * 0.55f) || (Mathf.Abs(dy) < 3.5f && Mathf.Abs(dx) > r * 0.55f);
                        bool tick = Mathf.Abs(dx) < 1.2f && dy < -14f && Mathf.Abs(dy % 28f) < 1.5f && Mathf.Abs(dy) < r * 0.5f;
                        bool tickH = Mathf.Abs(dy) < 6f && Mathf.Abs(dx) > 14f && Mathf.Abs(dx % 28f) < 1.2f && Mathf.Abs(dx) < r * 0.5f;
                        if (d < r && (cross || thick || tick || tickH)) a = 230;
                        px[y * s + x] = new Color32(10, 12, 14, a);
                    }
                _mask.SetPixels32(px);
                _mask.Apply();
                return _mask;
            }
        }

        private static Texture2D Pixel
        {
            get
            {
                if (_pixel != null) return _pixel;
                _pixel = new Texture2D(1, 1);
                _pixel.SetPixel(0, 0, new Color(0.04f, 0.047f, 0.055f, 1f));
                _pixel.Apply();
                return _pixel;
            }
        }

        public static void Draw()
        {
            float h = Screen.height, w = Screen.width;
            float size = h * 1.02f;
            float x = (w - size) / 2f, y = (h - size) / 2f;
            GUI.DrawTexture(new Rect(x, y, size, size), Mask);
            if (x > 0)
            {
                GUI.DrawTexture(new Rect(0, 0, x + 1, h), Pixel);
                GUI.DrawTexture(new Rect(x + size - 1, 0, w - x - size + 1, h), Pixel);
            }
        }
    }
}
