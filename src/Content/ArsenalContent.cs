using System;
using System.Collections.Generic;
using System.Linq;
using Arsenal.Visual;
using Arsenal.Weapons;
using HarmonyLib;
using Items.Model;
using TMPro;
using Tools.Interface.Model;
using UnityEngine;

namespace Arsenal.Content
{
    /// <summary>Adds the mod's items (guns, explosives) to the game's item list, with tool prefabs built at runtime.</summary>
    internal static class ArsenalContent
    {
        private static readonly Dictionary<uint, ItemSettings> Items = new Dictionary<uint, ItemSettings>();
        private static ToolVisuals _laserVisuals;
        private static Vector3 _laserRightHand;

        public static bool TryGet(uint id, out ItemSettings item) => Items.TryGetValue(id, out item) && item != null;

        public static void RegisterItems()
        {
            ItemSettingsList list = Resources.FindObjectsOfTypeAll<ItemSettingsList>().FirstOrDefault();
            if (list == null)
            {
                ArsenalMod.Log.Warning("Item list not found yet.");
                return;
            }
            var arr = Traverse.Create(list).Field("items").GetValue<ItemSettings[]>();
            ItemSettings laser = arr.FirstOrDefault(i => i.id == 2);
            if (laser != null && laser.toolSettings != null)
            {
                _laserVisuals = laser.toolSettings.prefab.GetComponentInChildren<ToolVisuals>(true);
                Transform hand = laser.toolSettings.prefab.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "RightHandPos");
                _laserRightHand = hand != null ? _laserVisuals.transform.InverseTransformPoint(hand.position) : new Vector3(-0.03f, -0.09f, -0.25f);
            }

            var added = new List<ItemSettings>();
            foreach (HandWeaponDef def in WeaponDefs.Guns)
                if (!arr.Any(i => i.id == def.ItemId))
                    added.Add(BuildGunItem(def));
            foreach (ItemSettings extra in Explosives.BuildItems(arr))
                added.Add(extra);
            if (added.Count == 0) return;

            Traverse.Create(list).Field("items").SetValue(arr.Concat(added).ToArray());
            Traverse.Create(list).Field("_dict").SetValue(null);
            foreach (ItemSettings i in added) Items[i.id] = i;
            ArsenalMod.Log.Msg($"Registered {added.Count} items: {string.Join(", ", added.Select(i => i.id + " " + i.itemName))}");
        }

        private static ItemSettings BuildGunItem(HandWeaponDef def)
        {
            Model model = def.Model();
            GameObject prefab = BuildToolPrefab("Arsenal." + def.Kind, model, out ToolVisuals visuals);
            model.Root.transform.localPosition = def.HoldPosition;
            model.Root.transform.localEulerAngles = def.HoldEuler;
            HandWeapon weapon = prefab.AddComponent<HandWeapon>();
            weapon.ItemId = def.ItemId;
            AddDisplay(model);

            var tool = ScriptableObject.CreateInstance<ToolSettings>();
            tool.name = "Arsenal." + def.Kind + "Tool";
            tool.prefab = prefab;

            var item = ScriptableObject.CreateInstance<ItemSettings>();
            item.name = "Arsenal." + def.Kind;
            item.id = def.ItemId;
            item.itemName = Loc.T(def.NameFr, def.NameEn);
            item.mass = def.Mass;
            item.maxStackSize = 1;
            item.toolSettings = tool;
            IconRenderer.RenderLater(item, model.Root, new Vector3(0, -72f, -24f));
            return item;
        }

        /// <summary>Tool root + "Visuals" child laid out like the mining laser (same hand pose and screen placement).</summary>
        public static GameObject BuildToolPrefab(string name, Model model, out ToolVisuals visuals)
        {
            var root = new GameObject(name);
            root.transform.SetParent(Assets.TemplateRoot, false);
            var vis = new GameObject("Visuals");
            vis.transform.SetParent(root.transform, false);
            visuals = vis.AddComponent<ToolVisuals>();
            if (_laserVisuals != null)
            {
                visuals.firstPersonPosition = _laserVisuals.firstPersonPosition;
                visuals.firstPersonRotation = _laserVisuals.firstPersonRotation;
                visuals.maxLookAngles = _laserVisuals.maxLookAngles;
                visuals.lookUpPosition = _laserVisuals.lookUpPosition;
                visuals.lookForwardPosition = _laserVisuals.lookForwardPosition;
                visuals.lookDownPosition = _laserVisuals.lookDownPosition;
                visuals.lookDownRotationOffset = _laserVisuals.lookDownRotationOffset;
            }
            model.Root.transform.SetParent(vis.transform, false);
            // Put our grip where the mining laser's grip is, so the game's hand IK lands on it.
            model.Root.transform.localPosition = _laserRightHand - model.RightHand.localPosition;
            visuals.rightHandTargetTransform = model.RightHand;
            visuals.leftHandFirstPersonTargetTransform = model.LeftHandFirstPerson;
            visuals.leftHandThirdPersonTargetTransform = model.LeftHandThirdPerson;
            var hint = new GameObject("BodyTransformHint").transform;
            hint.SetParent(vis.transform, false);
            hint.localEulerAngles = new Vector3(0, 60f, 0);
            return root;
        }

        private static void AddDisplay(Model model)
        {
            if (model.Display == null) return;
            TMP_FontAsset font = Resources.FindObjectsOfTypeAll<TMP_FontAsset>().FirstOrDefault(f => f.name.Contains("LiberationSans")) ??
                                 Resources.FindObjectsOfTypeAll<TMP_FontAsset>().FirstOrDefault();
            var go = new GameObject("AmmoDisplay");
            go.transform.SetParent(model.Display, false);
            var tmp = go.AddComponent<TextMeshPro>();
            if (font != null) tmp.font = font;
            tmp.rectTransform.sizeDelta = new Vector2(0.05f, 0.022f);
            tmp.enableAutoSizing = false;
            tmp.enableWordWrapping = false;
            tmp.overflowMode = TextOverflowModes.Overflow;
            tmp.fontSize = 0.16f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontStyle = FontStyles.Bold;
            tmp.text = "00";
            tmp.color = new Color(0.55f, 0.95f, 1f);
        }
    }
}
