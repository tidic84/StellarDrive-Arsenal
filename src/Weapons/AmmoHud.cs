using UnityEngine;

namespace Arsenal.Weapons
{
    /// <summary>Ammo counter for the held gun: a small dark panel at the bottom right, like the game's HUD widgets.</summary>
    internal static class AmmoHud
    {
        private static Texture2D _px;
        private static GUIStyle _big, _small, _tiny;

        private static Texture2D Px
        {
            get
            {
                if (_px != null) return _px;
                _px = new Texture2D(1, 1);
                _px.SetPixel(0, 0, Color.white);
                _px.Apply();
                return _px;
            }
        }

        private static void Styles()
        {
            if (_big != null) return;
            _big = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, alignment = TextAnchor.LowerRight };
            _small = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, alignment = TextAnchor.LowerLeft };
            _tiny = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, alignment = TextAnchor.UpperLeft };
        }

        /// <param name="reload">0..1 while reloading, -1 otherwise.</param>
        public static void Draw(string name, int ammo, int magazine, float reload, bool automatic)
        {
            if (Event.current.type != EventType.Repaint) return;
            Styles();
            float s = Screen.height / 720f;
            _big.fontSize = Mathf.RoundToInt(34 * s);
            _small.fontSize = Mathf.RoundToInt(16 * s);
            _tiny.fontSize = Mathf.RoundToInt(12 * s);

            float w = 190f * s, h = 78f * s;
            var r = new Rect(Screen.width - w - 24f * s, Screen.height - h - 92f * s, w, h);
            Color ink = new Color(0.93f, 0.96f, 1f, 1f);
            Color warn = new Color(1f, 0.5f, 0.3f, 1f);

            // panel with an accent line on the left, as the game's info boxes
            GUI.color = new Color(0.06f, 0.11f, 0.16f, 0.72f);
            GUI.DrawTexture(r, Px);
            GUI.color = new Color(0.36f, 0.72f, 0.95f, 0.95f);
            GUI.DrawTexture(new Rect(r.x, r.y, 3f * s, r.height), Px);

            GUI.color = new Color(ink.r, ink.g, ink.b, 0.75f);
            GUI.Label(new Rect(r.x + 12f * s, r.y + 6f * s, w - 20f * s, 18f * s), name.ToUpperInvariant(), _tiny);
            GUI.Label(new Rect(r.xMax - 70f * s, r.y + 6f * s, 60f * s, 18f * s), automatic ? "AUTO" : "SEMI", _tiny);

            bool empty = ammo <= 0;
            GUI.color = reload >= 0f || empty ? warn : ink;
            var numRect = new Rect(r.x + 10f * s, r.y + 20f * s, 92f * s, 48f * s);
            GUI.Label(numRect, reload >= 0f ? "--" : ammo.ToString(), _big);
            GUI.color = new Color(ink.r, ink.g, ink.b, 0.7f);
            GUI.Label(new Rect(numRect.xMax + 4f * s, r.y + 20f * s, 80f * s, 40f * s), "/ " + magazine, _small);

            // one tick per round, or the reload progress
            var bar = new Rect(r.x + 14f * s, r.yMax - 10f * s, w - 26f * s, 4f * s);
            if (reload >= 0f)
            {
                GUI.color = new Color(1f, 1f, 1f, 0.15f);
                GUI.DrawTexture(bar, Px);
                GUI.color = warn;
                GUI.DrawTexture(new Rect(bar.x, bar.y, bar.width * Mathf.Clamp01(reload), bar.height), Px);
                GUI.color = warn;
                GUI.Label(new Rect(numRect.xMax + 4f * s, r.y + 40f * s, 90f * s, 20f * s), Loc.T("RECHARGE", "RELOAD"), _tiny);
            }
            else
            {
                float gap = magazine > 20 ? 1f * s : 2f * s;
                float tw = (bar.width - gap * (magazine - 1)) / magazine;
                for (int i = 0; i < magazine; i++)
                {
                    GUI.color = i < ammo ? new Color(ink.r, ink.g, ink.b, 0.9f) : new Color(1f, 1f, 1f, 0.15f);
                    GUI.DrawTexture(new Rect(bar.x + i * (tw + gap), bar.y, tw, bar.height), Px);
                }
                if (empty)
                {
                    GUI.color = warn;
                    GUI.Label(new Rect(numRect.xMax + 4f * s, r.y + 40f * s, 90f * s, 20f * s), Loc.T("R : RECHARGER", "R: RELOAD"), _tiny);
                }
            }
            GUI.color = Color.white;
        }
    }
}
