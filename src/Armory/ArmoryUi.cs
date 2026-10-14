using System.Collections.Generic;
using System.Linq;
using Arsenal.Content;
using Arsenal.Core;
using Arsenal.Ui;
using Arsenal.Visual;
using Arsenal.Weapons;
using HarmonyLib;
using Items.Model;
using Players.Interface.Model;
using Players.Inventory;
using Ships;
using Ships.Interface.Model.Parts;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Arsenal.Armory
{
    /// <summary>
    /// The armory terminal. Opening the armory part (it is a chest) shows this window next to the player's
    /// inventory in place of the chest grid. Everything is built from the game's menu pieces: framed window,
    /// tabs, part cells, info column and cost lines as in the build menu.
    /// </summary>
    internal sealed class ArmoryUi : MonoBehaviour
    {
        private static ArmoryUi _instance;

        private Canvas _canvas;
        private RectTransform _window, _grid;
        private readonly List<(ArmoryCategory cat, Image fill, TextMeshProUGUI label)> _tabs = new List<(ArmoryCategory, Image, TextMeshProUGUI)>();
        private readonly List<(ArmoryEntry entry, Image bg, Image contour)> _cells = new List<(ArmoryEntry, Image, Image)>();
        private List<ArmoryEntry> _catalogue;
        private ArmoryCategory _category = ArmoryCategory.Sidearms;
        private ArmoryEntry _selected;
        private bool _open;
        private float _refreshAt;

        // info column
        private RawImage _bigIcon;
        private TextMeshProUGUI _infoName, _infoRarity, _infoDesc, _infoPierce, _status, _buttonLabel;
        private Image _buttonFill;
        private RectTransform _statsRoot, _costRoot;

        // native chest window we sit next to
        private RectTransform _nativeChest, _nativeInventory;
        private Vector2 _nativeInventoryPos;

        public static void Ensure()
        {
            if (_instance != null) return;
            var go = new GameObject("Arsenal.ArmoryUi");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<ArmoryUi>();
        }

        public static void ResetAll()
        {
            if (_instance == null) return;
            _instance.Close();
            if (_instance._canvas != null) Destroy(_instance._canvas.gameObject);
            _instance._canvas = null;
        }

        private void Update()
        {
            bool atArmory = ArsenalMod.InGame && LocalAtArmory();
            if (atArmory && !_open) Open();
            else if (!atArmory && _open) Close();
            if (_open && Time.time >= _refreshAt)
            {
                _refreshAt = Time.time + 0.4f;
                ShowInfo(); // inventory may have changed
                HookNativeWindow();
            }
        }

        private static bool LocalAtArmory()
        {
            if (Svc.PlayersClient == null || !Svc.PlayersClient.TryGetLocalPlayerState(out PlayerState st)) return false;
            if (st.Type != PlayerStateType.InteractingWithChest) return false;
            return Svc.ShipsClient.TryGetTrackedShip(st.EntityId, out TrackedShipClient ship) &&
                   ship.TryGetStatefulPart(st.PartId, out StatefulPart part) && part.Settings != null && part.Settings.id == ShipContent.ArmoryId;
        }

        // ------------------------------------------------------------------ open / close

        private void Open()
        {
            _open = true;
            if (_canvas == null) Build();
            _canvas.gameObject.SetActive(true);
            _catalogue = Armory.Catalogue();
            SelectCategory(_category);
            HookNativeWindow();
        }

        private void Close()
        {
            _open = false;
            if (_canvas != null) _canvas.gameObject.SetActive(false);
            if (_nativeChest != null) _nativeChest.gameObject.SetActive(true);
            if (_nativeInventory != null) _nativeInventory.anchoredPosition = _nativeInventoryPos;
            _nativeChest = _nativeInventory = null;
        }

        /// <summary>Hides the chest grid of the native storage window and slides the inventory to the right.</summary>
        private void HookNativeWindow()
        {
            if (_nativeInventory != null) return;
            Canvas storage = FindObjectsByType<Canvas>(FindObjectsSortMode.None).FirstOrDefault(c => c.name.StartsWith("CanvasStorageContainer") && c.isActiveAndEnabled);
            if (storage == null) return;
            _nativeChest = storage.transform.Find("Chest/Wrapper") as RectTransform ?? storage.GetComponentsInChildren<RectTransform>(true).FirstOrDefault(t => t.name == "Wrapper");
            _nativeInventory = storage.GetComponentsInChildren<RectTransform>(true).FirstOrDefault(t => t.name == "Inventory");
            if (_nativeChest != null) _nativeChest.gameObject.SetActive(false);
            if (_nativeInventory != null)
            {
                _nativeInventoryPos = _nativeInventory.anchoredPosition;
                _nativeInventory.anchoredPosition = new Vector2(1250f, -60f);
            }
        }

        // ------------------------------------------------------------------ build

        private void Build()
        {
            _canvas = UiKit.MakeCanvas("Arsenal.Armory", 6);
            _window = UiKit.Window(_canvas.transform, "Armory", new Vector2(-560f, 40f), new Vector2(2300f, 1800f));

            // header
            UiKit.Label(UiKit.Rect(_window, "Title", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(70f, -40f), new Vector2(1200f, 90f), new Vector2(0f, 1f)),
                Loc.T("Armurerie", "Armory"), 72, UiKit.Text);
            UiKit.Label(UiKit.Rect(_window, "Subtitle", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-70f, -60f), new Vector2(900f, 60f), new Vector2(1f, 1f)),
                Loc.T("Fabrication d'armes", "Weapon fabrication"), 40, UiKit.TextDim, true, TextAlignmentOptions.Right);

            // tabs
            RectTransform tabs = UiKit.Rect(_window, "Tabs", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(70f, -150f), new Vector2(-140f, 86f), new Vector2(0f, 1f));
            var cats = new[] { ArmoryCategory.Sidearms, ArmoryCategory.Rifles, ArmoryCategory.Heavy, ArmoryCategory.Energy, ArmoryCategory.Explosives };
            for (int i = 0; i < cats.Length; i++)
            {
                ArmoryCategory c = cats[i];
                RectTransform t = UiKit.Rect(tabs, "Tab" + c, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(i * 420f, 0f), new Vector2(400f, 86f), new Vector2(0f, 1f));
                UiKit.TabButton(t, WeaponDefs.CategoryName(c), () => SelectCategory(c), out Image fill, out TextMeshProUGUI label);
                _tabs.Add((c, fill, label));
            }
            UiKit.Line(_window, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -240f), 2160f);

            // grid
            _grid = UiKit.Rect(_window, "Grid", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(70f, -290f), new Vector2(1240f, 1440f), new Vector2(0f, 1f));

            // info column
            RectTransform info = UiKit.Rect(_window, "Info", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-60f, -280f), new Vector2(880f, InfoHeight), new Vector2(1f, 1f));
            UiKit.Image(info, null, new Color(0f, 0f, 0f, 0.18f), 1f);
            RectTransform iconBg = UiKit.Rect(info, "IconBg", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(420f, 300f), new Vector2(0.5f, 1f));
            UiKit.Image(iconBg, "unit_bg", new Color(UiKit.Accent.r, UiKit.Accent.g, UiKit.Accent.b, 0.18f), 2f);
            UiKit.Image(UiKit.Fill(iconBg, "Contour"), "unit_borders", new Color(UiKit.Accent.r, UiKit.Accent.g, UiKit.Accent.b, 0.7f), 2f);
            RectTransform shadow = UiKit.Rect(iconBg, "Shadow", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(6f, -6f), new Vector2(280f, 280f));
            UiKit.Raw(shadow, null, new Color(0f, 0f, 0f, 0.5f));
            RectTransform big = UiKit.Rect(iconBg, "Icon", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(280f, 280f));
            _bigIcon = UiKit.Raw(big, null, Color.white);
            shadow.GetComponent<RawImage>().enabled = false;

            _infoName = UiKit.Label(UiKit.Rect(info, "Name", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -350f), new Vector2(-60f, 90f), new Vector2(0.5f, 1f)), "", 58, UiKit.Accent, true, TextAlignmentOptions.Center);
            // one line: long English names shrink instead of wrapping over the rarity line
            _infoName.enableAutoSizing = true;
            _infoName.fontSizeMin = 34f;
            _infoName.fontSizeMax = 58f;
            _infoRarity = UiKit.Label(UiKit.Rect(info, "Rarity", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -440f), new Vector2(-60f, 56f), new Vector2(0.5f, 1f)), "", 42, UiKit.TextDim, false, TextAlignmentOptions.Center);
            _infoDesc = UiKit.Label(UiKit.Rect(info, "Desc", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -510f), new Vector2(-90f, 150f), new Vector2(0.5f, 1f)), "", 40, UiKit.Text, false, TextAlignmentOptions.TopLeft);
            _infoPierce = UiKit.Label(UiKit.Rect(info, "Pierce", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -662f), new Vector2(-90f, 50f), new Vector2(0.5f, 1f)), "", 36, UiKit.Accent, true, TextAlignmentOptions.Left);
            _infoPierce.enableWordWrapping = true; // long blaster lines wrap and push the stats down
            _statsRoot = UiKit.Rect(info, "Stats", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -720f), new Vector2(-90f, 250f), new Vector2(0.5f, 1f));
            _costRoot = UiKit.Rect(info, "Cost", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -970f), new Vector2(-90f, 240f), new Vector2(0.5f, 1f));

            RectTransform button = UiKit.Rect(info, "Fabricate", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 76f), new Vector2(560f, 96f), new Vector2(0.5f, 0f));
            UiKit.TabButton(button, Loc.T("Fabriquer", "Fabricate"), Fabricate, out _buttonFill, out _buttonLabel);
            _buttonLabel.fontSize = 52;
            _status = UiKit.Label(UiKit.Rect(info, "Status", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 18f), new Vector2(-60f, 50f), new Vector2(0.5f, 0f)), "", 40, UiKit.TextDim, false, TextAlignmentOptions.Center);
        }

        private void SelectCategory(ArmoryCategory c)
        {
            _category = c;
            foreach (var (cat, fill, label) in _tabs)
            {
                bool on = cat == c;
                fill.color = on ? UiKit.Accent : new Color(UiKit.Accent.r, UiKit.Accent.g, UiKit.Accent.b, 0f);
                label.color = on ? UiKit.Text : UiKit.Accent;
            }
            foreach (Transform child in _grid) Destroy(child.gameObject);
            _cells.Clear();
            List<ArmoryEntry> entries = _catalogue.Where(e => e.Category == c).ToList();
            for (int i = 0; i < entries.Count; i++)
            {
                ArmoryEntry e = entries[i];
                int col = i % 5, row = i / 5;
                RectTransform cell = UiKit.Rect(_grid, "Cell" + e.ItemId, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(col * 245f, -row * 270f), new Vector2(225f, 225f), new Vector2(0f, 1f));
                UiKit.CellButton(cell, () => Select(e), out Image bg, out Image contour);
                RectTransform rare = UiKit.Rect(cell, "Rarity", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -10f), new Vector2(-40f, 8f), new Vector2(0.5f, 1f));
                UiKit.Image(rare, null, WeaponDefs.RarityColor(e.Rarity), 1f);
                RectTransform icon = UiKit.Rect(cell, "Icon", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 14f), new Vector2(165f, 165f));
                UiKit.Raw(icon, ItemIcon(e.ItemId), Color.white);
                RectTransform name = UiKit.Rect(cell, "Name", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 6f), new Vector2(-12f, 46f), new Vector2(0.5f, 0f));
                TextMeshProUGUI t = UiKit.Label(name, ShortName(e.Name), 36, UiKit.Text, true, TextAlignmentOptions.Center);
                t.enableAutoSizing = true;
                t.fontSizeMin = 24;
                t.fontSizeMax = 36;
                _cells.Add((e, bg, contour));
            }
            Select(entries.FirstOrDefault());
        }

#if DEV
        /// <summary>Dev: shows that weapon's tab with it selected (for screenshots).</summary>
        public static void DevShow(uint item)
        {
            ArmoryEntry e = _instance?._catalogue.FirstOrDefault(x => x.ItemId == item);
            if (e == null) return;
            _instance.SelectCategory(e.Category);
            _instance.Select(e);
        }
#endif

        private static string ShortName(string n)
        {
            int q = n.IndexOf('«');
            if (q >= 0)
            {
                int e = n.IndexOf('»', q);
                if (e > q) return n.Substring(q + 1, e - q - 1).Trim();
            }
            q = n.IndexOf('"');
            if (q >= 0)
            {
                int e = n.IndexOf('"', q + 1);
                if (e > q) return n.Substring(q + 1, e - q - 1);
            }
            return n;
        }

        private void Select(ArmoryEntry e)
        {
            _selected = e;
            foreach (var (entry, bg, contour) in _cells)
            {
                bool on = entry == e;
                bg.color = new Color(UiKit.Accent.r, UiKit.Accent.g, UiKit.Accent.b, on ? 0.6f : 0.25f);
                contour.color = on ? Color.white : UiKit.Accent;
            }
            _status.text = "";
            ShowInfo();
        }

        private void ShowInfo()
        {
            if (_canvas == null) return;
            ArmoryEntry e = _selected;
            if (e == null)
            {
                _infoName.text = "";
                _infoRarity.text = "";
                _infoDesc.text = "";
                _infoPierce.text = "";
                _bigIcon.texture = null;
                return;
            }
            _bigIcon.texture = ItemIcon(e.ItemId);
            _bigIcon.transform.parent.Find("Shadow").GetComponent<RawImage>().texture = _bigIcon.texture;
            _bigIcon.transform.parent.Find("Shadow").GetComponent<RawImage>().enabled = _bigIcon.texture != null;
            _infoName.text = e.Name;
            _infoRarity.text = WeaponDefs.RarityName(e.Rarity) + " · " + WeaponDefs.CategoryName(e.Category);
            _infoRarity.color = WeaponDefs.RarityColor(e.Rarity);
            _infoDesc.text = e.Description;
            _infoPierce.text = e.Pierce ?? "";
            float y = LayoutText();

            foreach (Transform c in _statsRoot) Destroy(c.gameObject);
            for (int i = 0; i < e.Stats.Length; i++)
            {
                RectTransform row = UiKit.Rect(_statsRoot, "Stat", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -i * StatStep), new Vector2(0f, 54f), new Vector2(0.5f, 1f));
                UiKit.Label(UiKit.Rect(row, "Label", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(320f, 54f), new Vector2(0f, 0.5f)), e.Stats[i].label, 40, UiKit.Text);
                UiKit.Bar(row, "Bar", new Vector2(340f, 0f), new Vector2(440f, 20f), e.Stats[i].value, UiKit.Accent, new Vector2(0f, 0.5f));
            }

            y += e.Stats.Length * StatStep + 16f;
            Place(_costRoot, y, 0f);
            // cost rows tighten when a long description leaves less room above the button
            float costStep = Mathf.Clamp((InfoHeight - ButtonSpace - y) / Mathf.Max(1, e.Cost.Length), 70f, 76f);
            foreach (Transform c in _costRoot) Destroy(c.gameObject);
            var inv = Svc.Get<PlayerInventoryReaderProxy>();
            bool affordable = true;
            for (int i = 0; i < e.Cost.Length; i++)
            {
                (uint item, byte qty) = e.Cost[i];
                int have = inv != null ? inv.GetItemQuantity(item) : 0;
                bool enough = have >= qty;
                affordable &= enough;
                RectTransform row = UiKit.Rect(_costRoot, "Cost", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -i * costStep), new Vector2(0f, costStep - 6f), new Vector2(0.5f, 1f));
                RectTransform line = UiKit.Rect(row, "Line", new Vector2(0f, 0f), new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, 12f), new Vector2(0f, 0f));
                UiKit.Image(line, "materials_line", Color.white, 2f);
                RectTransform iconBg = UiKit.Rect(row, "IconBg", new Vector2(0f, 0f), new Vector2(0f, 0f), Vector2.zero, new Vector2(70f, 70f), new Vector2(0f, 0f));
                UiKit.Image(iconBg, "unit_bg", Color.white, 2f);
                RectTransform ic = UiKit.Rect(iconBg, "Icon", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(56f, 56f));
                UiKit.Raw(ic, ItemIcon(item), Color.white);
                UiKit.Label(UiKit.Rect(row, "Label", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(100f, 6f), new Vector2(450f, 50f), new Vector2(0f, 0.5f)), ItemName(item), 42, new Color(0.96f, 0.96f, 0.96f));
                UiKit.Label(UiKit.Rect(row, "Progress", new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, 6f), new Vector2(200f, 50f), new Vector2(1f, 0.5f)), $"{have}/{qty}", 42, enough ? UiKit.Good : UiKit.Warning, true, TextAlignmentOptions.Right);
            }
            _buttonFill.color = affordable ? UiKit.Accent : new Color(0.35f, 0.4f, 0.45f, 1f);
            _buttonLabel.color = affordable ? UiKit.Text : new Color(0.75f, 0.78f, 0.8f);
        }

        private const float InfoHeight = 1460f, ButtonSpace = 190f, StatStep = 54f, DescTop = 506f;

        /// <summary>
        /// Stacks description, penetration line and stats under the rarity line at their real heights
        /// (long descriptions use a smaller font) and returns where the stats end.
        /// </summary>
        private float LayoutText()
        {
            float width = ((RectTransform)_infoDesc.transform).rect.width;
            _infoDesc.fontSize = 40f;
            float h = _infoDesc.GetPreferredValues(_infoDesc.text, width, 0f).y;
            if (h > 160f)
            {
                _infoDesc.fontSize = 34f;
                h = _infoDesc.GetPreferredValues(_infoDesc.text, width, 0f).y;
            }
            float y = DescTop;
            Place((RectTransform)_infoDesc.transform, y, Mathf.Ceil(h));
            y += Mathf.Ceil(h) + 16f;
            bool pierce = !string.IsNullOrEmpty(_infoPierce.text);
            _infoPierce.gameObject.SetActive(pierce);
            if (pierce)
            {
                float ph = Mathf.Ceil(_infoPierce.GetPreferredValues(_infoPierce.text, width, 0f).y);
                Place((RectTransform)_infoPierce.transform, y, ph);
                y += ph + 10f;
            }
            Place(_statsRoot, y, 0f);
            return y;
        }

        /// <summary>Puts a top-anchored child of the info column at <paramref name="top"/> units down (height kept when 0).</summary>
        private static void Place(RectTransform rt, float top, float height)
        {
            rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, -top);
            if (height > 0f) rt.sizeDelta = new Vector2(rt.sizeDelta.x, height);
        }

        private void Fabricate()
        {
            if (_selected == null) return;
            Armory.RequestFabricate(_selected.ItemId);
            _status.text = Loc.T("Fabrication…", "Fabricating…");
            _status.color = UiKit.TextDim;
        }

        public static void OnResult(bool ok, uint item, string msg)
        {
            if (_instance == null || _instance._status == null) return;
            _instance._status.text = ok ? Loc.T("Ajouté à votre inventaire", "Added to your inventory") : msg;
            _instance._status.color = ok ? UiKit.Good : UiKit.Warning;
            if (ok) Sounds.Reload(Camera.main != null ? Camera.main.transform.position : Vector3.zero);
            _instance._refreshAt = 0f;
        }

        // ------------------------------------------------------------------ items

        private static ItemSettings[] Items()
        {
            ItemSettingsList list = Resources.FindObjectsOfTypeAll<ItemSettingsList>().FirstOrDefault();
            return list != null ? Traverse.Create(list).Field("items").GetValue<ItemSettings[]>() : new ItemSettings[0];
        }

        private static Texture ItemIcon(uint id) => Items().FirstOrDefault(i => i.id == id)?.icon;

        private static string ItemName(uint id) => Items().FirstOrDefault(i => i.id == id)?.itemName ?? id.ToString();
    }
}
