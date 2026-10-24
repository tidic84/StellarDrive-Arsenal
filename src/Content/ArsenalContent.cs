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
            FitToArms(def.Kind.ToString(), model);
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
            item.itemObject = BuildDroppedVisual("Arsenal." + def.Kind, def.Model());
            IconRenderer.RenderLater(item, model.Root, new Vector3(0, -72f, -24f));
            return item;
        }

        // Shoulders of the first-person arms in the tool visuals' frame, measured in game: the body stands
        // bladed, left shoulder forward. A straight arm reaches 0.42 m; past ~0.36 m it looks locked.
        private static readonly Vector3 LeftShoulder = new Vector3(-0.346f, -0.1f, -0.134f), RightShoulder = new Vector3(-0.299f, -0.125f, -0.423f);
        private const float Reach = 0.36f;

        /// <summary>
        /// Moves the gun as little as possible so both hand targets are within reach: otherwise the hand IK stops
        /// at full stretch and the hands float next to the grip and the handguard.
        /// </summary>
        public static void FitToArms(string name, Model model)
        {
            Transform root = model.Root.transform;
            // through the local transforms: the template sits 100 km down, where world positions lose the centimetres
            Vector3 right = root.localPosition + root.localRotation * model.RightHand.localPosition;
            Vector3 left = root.localPosition + root.localRotation * model.LeftHandFirstPerson.localPosition;
            Vector3 shift = Vector3.zero;
            for (int i = 0; i < 60; i++)
                shift = Pull(Pull(shift, right, RightShoulder), left, LeftShoulder);
            root.localPosition += shift;
            float r = Vector3.Distance(right + shift, RightShoulder), l = Vector3.Distance(left + shift, LeftShoulder);
            if (shift.sqrMagnitude > 0f || r > Reach + 0.01f || l > Reach + 0.01f)
                ArsenalMod.Log.Msg($"{name}: moved {shift.x:F3} {shift.y:F3} {shift.z:F3} to reach the hands (right {r:F2} m, left {l:F2} m)");
        }

        private static Vector3 Pull(Vector3 shift, Vector3 hand, Vector3 shoulder)
        {
            Vector3 d = hand + shift - shoulder;
            return d.magnitude <= Reach ? shift : shift - d.normalized * (d.magnitude - Reach);
        }

        /// <summary>
        /// What the item looks like once dragged out of the inventory: the game drops it as a world object and
        /// only takes the looks from here (its rigidbody, collider and pick up are the same for every item).
        /// The model is centred so it sits in the middle of that collider.
        /// </summary>
        public static GameObject BuildDroppedVisual(string name, Model model)
        {
            var root = new GameObject(name + ".Dropped");
            root.transform.SetParent(Assets.TemplateRoot, false);
            model.Root.transform.SetParent(root.transform, false);
            Renderer[] renderers = model.Root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length > 0)
            {
                Bounds b = renderers[0].bounds;
                foreach (Renderer r in renderers) b.Encapsulate(r.bounds);
                model.Root.transform.localPosition = -root.transform.InverseTransformPoint(b.center);
            }
            return root;
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
