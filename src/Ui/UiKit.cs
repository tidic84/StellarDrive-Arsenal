using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Arsenal.Ui
{
    /// <summary>
    /// Builds uGUI with the game's own look: its sprites (menu frame, cells, tabs, lines), its two fonts
    /// (Anta for titles and labels, Mulish for text) and its colours. Layout units are the game's
    /// 3840 x 2160 reference canvas.
    /// </summary>
    internal static class UiKit
    {
        // colours read from the game's menus
        public static readonly Color PanelDark = new Color(0.031f, 0.102f, 0.149f, 0.851f);
        public static readonly Color Panel = new Color(0.051f, 0.166f, 0.243f, 1f);
        public static readonly Color Cell = new Color(0.153f, 0.279f, 0.361f, 1f);
        public static readonly Color CellHover = new Color(0.368f, 0.451f, 0.504f, 1f);
        public static readonly Color Accent = new Color(0.196f, 0.529f, 0.745f, 1f);
        public static readonly Color AccentLine = new Color(0.290f, 0.529f, 0.729f, 1f);
        public static readonly Color Text = new Color(0.95f, 0.95f, 0.95f, 1f);
        public static readonly Color TextDim = new Color(0.62f, 0.72f, 0.80f, 1f);
        public static readonly Color Warning = new Color(1f, 0.55f, 0.32f, 1f);
        public static readonly Color Good = new Color(0.45f, 0.85f, 0.55f, 1f);

        private static readonly Dictionary<string, Sprite> Sprites = new Dictionary<string, Sprite>();
        private static TMP_FontAsset _title, _body;

        private static readonly Dictionary<string, float> Missing = new Dictionary<string, float>(); // name -> when to look again

        public static void ClearCache()
        {
            Sprites.Clear();
            Missing.Clear();
            _title = _body = null;
        }

        public static Sprite Sprite(string name)
        {
            if (Sprites.TryGetValue(name, out Sprite s) && s != null) return s;
            // a sprite the game does not have (yet): scanning every sprite again on each call is costly, so wait a bit
            if (Missing.TryGetValue(name, out float retry) && Time.unscaledTime < retry) return null;
            s = Resources.FindObjectsOfTypeAll<Sprite>().FirstOrDefault(x => x != null && x.name == name);
            if (s != null)
            {
                Sprites[name] = s;
                Missing.Remove(name);
            }
            else Missing[name] = Time.unscaledTime + 2f;
            return s;
        }

        public static TMP_FontAsset TitleFont => _title != null ? _title : (_title = Font("Anta"));
        public static TMP_FontAsset BodyFont => _body != null ? _body : (_body = Font("Mulish"));

        private const string Charset =
            " !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~" +
            "àâäçéèêëîïôöùûüÿœæÀÂÄÇÉÈÊËÎÏÔÖÙÛÜŸŒÆ«»·–—…’×°%";

        private static TMP_FontAsset Font(string part)
        {
            TMP_FontAsset f = Resources.FindObjectsOfTypeAll<TMP_FontAsset>().FirstOrDefault(x => x != null && x.name.Contains(part)) ??
                              Resources.FindObjectsOfTypeAll<TMP_FontAsset>().FirstOrDefault();
            if (f != null) CompleteGlyphs(f);
            return f;
        }

        /// <summary>
        /// The game's fonts are dynamic atlases that only hold the characters the game itself displayed.
        /// Add ours if the font can, and fall back on the game's full LiberationSans for anything else.
        /// </summary>
        private static void CompleteGlyphs(TMP_FontAsset f)
        {
            try
            {
                f.TryAddCharacters(Charset, out string missing);
                if (string.IsNullOrEmpty(missing)) return;
            }
            catch
            {
                // no source font data: fallback below
            }
            TMP_FontAsset full = Resources.FindObjectsOfTypeAll<TMP_FontAsset>()
                .FirstOrDefault(x => x != null && x.atlasPopulationMode == AtlasPopulationMode.Static && x.characterTable != null && x.characterTable.Count > 150);
            if (full == null || full == f) return;
            if (f.fallbackFontAssetTable == null) f.fallbackFontAssetTable = new List<TMP_FontAsset>();
            if (!f.fallbackFontAssetTable.Contains(full)) f.fallbackFontAssetTable.Add(full);
        }

        /// <summary>A screen-space canvas scaled like the game's (3840 x 2160 reference).</summary>
        public static Canvas MakeCanvas(string name, int sortingOrder)
        {
            var go = new GameObject(name);
            Object.DontDestroyOnLoad(go);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(3840, 2160);
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            EnsureEventSystem();
            return canvas;
        }

        public static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            if (Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Any()) return;
            var es = new GameObject("Arsenal.EventSystem");
            Object.DontDestroyOnLoad(es);
            es.AddComponent<EventSystem>();
            es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
        }

        public static RectTransform Rect(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, Vector2 size, Vector2? pivot = null)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot ?? new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        /// <summary>Fills the parent.</summary>
        public static RectTransform Fill(Transform parent, string name, float inset = 0f)
        {
            RectTransform rt = Rect(parent, name, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-inset * 2, -inset * 2));
            return rt;
        }

        public static Image Image(RectTransform rt, string sprite, Color color, float ppu = 2f, bool raycast = false)
        {
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite != null ? Sprite(sprite) : null;
            img.type = img.sprite != null && img.sprite.border != Vector4.zero ? UnityEngine.UI.Image.Type.Sliced : UnityEngine.UI.Image.Type.Simple;
            img.pixelsPerUnitMultiplier = ppu;
            img.color = color;
            img.raycastTarget = raycast;
            return img;
        }

        public static TextMeshProUGUI Label(RectTransform rt, string text, float size, Color color, bool title = true, TextAlignmentOptions align = TextAlignmentOptions.Left)
        {
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.font = title ? TitleFont : BodyFont;
            t.fontSize = size;
            t.color = color;
            t.text = text;
            t.alignment = align;
            t.raycastTarget = false;
            t.enableWordWrapping = !title;
            t.fontFeatures?.RemoveAll(f => f.ToString() == "liga"); // the dynamic atlas drops the glyph after a ligature
            t.overflowMode = TextOverflowModes.Overflow;
            return t;
        }

        /// <summary>The game's framed menu: dark panel + the frame with hatched corners.</summary>
        public static RectTransform Window(Transform parent, string name, Vector2 pos, Vector2 size, Color? fill = null)
        {
            RectTransform w = Rect(parent, name, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, size);
            Image(w, null, fill ?? Panel, 1f, true);
            RectTransform border = Rect(w, "Border", Vector2.zero, Vector2.one, Vector2.zero, new Vector2(12, 12));
            Image(border, "menu_parts_border", Color.white, 2f);
            border.SetAsLastSibling();
            return w;
        }

        /// <summary>A clickable cell like the build menu's part buttons.</summary>
        public static Button CellButton(RectTransform rt, Action onClick, out Image background, out Image contour)
        {
            background = Image(Fill(rt, "Background"), "unit_bg", new Color(Accent.r, Accent.g, Accent.b, 0.25f), 2f, true);
            contour = Image(Fill(rt, "Contour"), "unit_borders", Accent, 2f);
            var b = rt.gameObject.AddComponent<Button>();
            b.targetGraphic = background;
            b.transition = Selectable.Transition.None;
            b.onClick.AddListener(() => onClick());
            return b;
        }

        /// <summary>A tab-shaped button (game's tab sprites).</summary>
        public static Button TabButton(RectTransform rt, string text, Action onClick, out Image fill, out TextMeshProUGUI label)
        {
            fill = Image(Fill(rt, "BackgroundImage"), "tab_full", Accent, 2f, true);
            RectTransform lr = Fill(rt, "Text");
            lr.offsetMin = new Vector2(30, 0);
            label = Label(lr, text, 46, Text, true, TextAlignmentOptions.Center);
            Image(Fill(rt, "ContourLine"), "tab_borders", Accent, 2f);
            var b = rt.gameObject.AddComponent<Button>();
            b.targetGraphic = fill;
            b.transition = Selectable.Transition.None;
            b.onClick.AddListener(() => onClick());
            return b;
        }

        public static RawImage Raw(RectTransform rt, Texture tex, Color color)
        {
            var r = rt.gameObject.AddComponent<RawImage>();
            r.texture = tex;
            r.color = color;
            r.raycastTarget = false;
            return r;
        }

        /// <summary>Thin underline as under the build menu tabs.</summary>
        public static void Line(Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, float width)
        {
            RectTransform l = Rect(parent, "Line", anchorMin, anchorMax, pos, new Vector2(width, 12));
            Image(l, "menu_parts_line", AccentLine, 2f);
        }

        /// <summary>A simple filled bar (stats, heat).</summary>
        public static Image Bar(Transform parent, string name, Vector2 pos, Vector2 size, float fill, Color color, Vector2? anchor = null)
        {
            Vector2 a = anchor ?? new Vector2(0f, 1f);
            RectTransform bg = Rect(parent, name, a, a, pos, size, new Vector2(0f, 0.5f));
            Image(bg, "unit_bg", new Color(1f, 1f, 1f, 0.08f), 4f);
            RectTransform fg = Rect(bg, "Fill", new Vector2(0f, 0f), new Vector2(Mathf.Clamp01(fill), 1f), Vector2.zero, Vector2.zero, new Vector2(0f, 0.5f));
            fg.offsetMin = Vector2.zero;
            fg.offsetMax = Vector2.zero;
            return Image(fg, "unit_bg", color, 4f);
        }
    }
}
