using Arsenal.Ui;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Arsenal.Weapons
{
    /// <summary>
    /// Held-weapon panel at the bottom right, built from the game's own HUD pieces (cell sprite, accent
    /// line, Anta / Mulish fonts): name and rarity, firing mode, rounds or heat, charge and reload.
    /// </summary>
    internal static class WeaponHud
    {
        private static Canvas _canvas;
        private static RectTransform _root;
        private static TextMeshProUGUI _name, _rarity, _mode, _count, _max, _state;
        private static Image _bar, _rarityStripe;
        private static RectTransform _barRect;
        private static HandWeaponDef _shown;

        private static void Build()
        {
            _canvas = UiKit.MakeCanvas("Arsenal.WeaponHud", 4);
            _canvas.GetComponent<GraphicRaycaster>().enabled = false;
            _root = UiKit.Rect(_canvas.transform, "Panel", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-120f, 290f), new Vector2(820f, 250f), new Vector2(1f, 0f));
            UiKit.Image(_root, "unit_bg", new Color(UiKit.Panel.r, UiKit.Panel.g, UiKit.Panel.b, 0.86f), 2f);
            RectTransform contour = UiKit.Fill(_root, "Contour");
            UiKit.Image(contour, "unit_borders", new Color(UiKit.Accent.r, UiKit.Accent.g, UiKit.Accent.b, 0.6f), 2f);
            RectTransform stripe = UiKit.Rect(_root, "Rarity", new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(14f, 0f), new Vector2(10f, -40f), new Vector2(0f, 0.5f));
            _rarityStripe = UiKit.Image(stripe, null, Color.white, 1f);

            _name = UiKit.Label(UiKit.Rect(_root, "Name", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(40f, -18f), new Vector2(-330f, 56f), new Vector2(0f, 1f)), "", 44, UiKit.Text);
            _name.enableAutoSizing = true;
            _name.fontSizeMin = 26;
            _name.fontSizeMax = 44;
            _rarity = UiKit.Label(UiKit.Rect(_root, "RarityText", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(40f, -72f), new Vector2(-260f, 40f), new Vector2(0f, 1f)), "", 30, UiKit.TextDim, false);
            _mode = UiKit.Label(UiKit.Rect(_root, "Mode", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-30f, -22f), new Vector2(240f, 44f), new Vector2(1f, 1f)), "", 34, UiKit.Accent, true, TextAlignmentOptions.Right);

            _count = UiKit.Label(UiKit.Rect(_root, "Count", new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(40f, 40f), new Vector2(260f, 120f), new Vector2(0f, 0f)), "", 110, UiKit.Text, true, TextAlignmentOptions.BottomLeft);
            _max = UiKit.Label(UiKit.Rect(_root, "Max", new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(290f, 52f), new Vector2(200f, 60f), new Vector2(0f, 0f)), "", 52, UiKit.TextDim, true, TextAlignmentOptions.BottomLeft);
            _state = UiKit.Label(UiKit.Rect(_root, "State", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-30f, 56f), new Vector2(360f, 50f), new Vector2(1f, 0f)), "", 36, UiKit.Warning, true, TextAlignmentOptions.BottomRight);

            _bar = UiKit.Bar(_root, "Bar", new Vector2(40f, 28f), new Vector2(740f, 14f), 1f, UiKit.Text, new Vector2(0f, 0f));
            _barRect = (RectTransform)_bar.transform;
        }

        public static void Show(HandWeaponDef def, int ammo, float heat, bool overheated, float reload, float charge, bool jammed)
        {
            if (_canvas == null) Build();
            if (!_canvas.gameObject.activeSelf) _canvas.gameObject.SetActive(true);
            if (_shown != def)
            {
                _shown = def;
                _name.text = def.Name.ToUpperInvariant();
                Color rc = WeaponDefs.RarityColor(def.Rarity);
                _rarity.text = WeaponDefs.RarityName(def.Rarity) + " · " + WeaponDefs.CategoryName(def.Category);
                _rarity.color = rc;
                _rarityStripe.color = rc;
                _mode.text = ModeName(def);
            }

            bool heatMode = def.Ammo == AmmoMode.Heat;
            float fill;
            Color barColor;
            if (heatMode)
            {
                _count.text = Mathf.RoundToInt(heat * 100f) + "%";
                _max.text = Loc.T("chaleur", "heat");
                fill = heat;
                barColor = overheated ? UiKit.Warning : Color.Lerp(UiKit.Accent, UiKit.Warning, heat);
            }
            else
            {
                _count.text = reload >= 0f ? "--" : ammo.ToString();
                _max.text = "/ " + def.Magazine;
                fill = reload >= 0f ? reload : def.Magazine > 0 ? (float)ammo / def.Magazine : 0f;
                barColor = reload >= 0f || ammo == 0 ? UiKit.Warning : UiKit.Text;
            }
            // keep the unit right after the number, whatever its width
            _max.rectTransform.anchoredPosition = new Vector2(40f + _count.GetPreferredValues(_count.text).x + 18f, 52f);
            if (charge >= 0f && charge > 0.01f)
            {
                fill = charge;
                barColor = Color.Lerp(UiKit.Accent, Color.white, charge);
            }
            _count.color = (heatMode && overheated) || (!heatMode && (ammo == 0 || reload >= 0f)) ? UiKit.Warning : UiKit.Text;
            ((RectTransform)_bar.transform).anchorMax = new Vector2(Mathf.Clamp01(fill), 1f);
            _bar.color = barColor;

            if (jammed) _state.text = Loc.T("BROUILLÉ", "JAMMED");
            else if (heatMode && overheated) _state.text = Loc.T("SURCHAUFFE", "OVERHEATED");
            else if (reload >= 0f) _state.text = Loc.T("RECHARGEMENT", "RELOADING");
            else if (charge > 0.01f) _state.text = charge >= 1f ? Loc.T("CHARGÉ", "CHARGED") : Loc.T("CHARGE", "CHARGING");
            else if (!heatMode && ammo == 0) _state.text = Loc.T("R : RECHARGER", "R: RELOAD");
            else _state.text = "";
            _state.color = charge > 0.01f && !jammed ? UiKit.Accent : UiKit.Warning;
        }

        public static void Hide()
        {
            if (_canvas != null) _canvas.gameObject.SetActive(false);
        }

        public static void Reset()
        {
            if (_canvas != null) Object.Destroy(_canvas.gameObject);
            _canvas = null;
            _shown = null;
        }

        private static string ModeName(HandWeaponDef def) => def.Mode switch
        {
            FireMode.Charge => Loc.T("CHARGÉ", "CHARGED"),
            FireMode.Beam => Loc.T("RAYON", "BEAM"),
            FireMode.Flame => Loc.T("FLAMMES", "FLAME"),
            FireMode.Arc => Loc.T("ARC", "ARC"),
            FireMode.Projectile => Loc.T("LANCEUR", "LAUNCHER"),
            FireMode.Bolt => def.Automatic ? Loc.T("BLASTER AUTO", "AUTO BLASTER") : "BLASTER",
            _ => def.Pellets > 1 ? Loc.T("DISPERSION", "SPREAD") : def.Automatic ? "AUTO" : "SEMI",
        };
    }
}
