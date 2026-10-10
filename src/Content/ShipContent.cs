using System;
using System.Collections.Generic;
using System.Linq;
using Arsenal.Core;
using Arsenal.Visual;
using Arsenal.Weapons;
using HarmonyLib;
using Items.Model;
using Research.Interface.Values;
using Ships;
using Ships.Interface.Model.Parts;
using Ships.Interface.Model.Parts.Common;
using Ships.Interface.Model.Parts.State;
using Ships.Interface.Model.Parts.StateTypes;
using Ships.Interface.Services;
using Ships.Interface.Settings;
using Ships.Services;
using UI.BuildMenu;
using UnityEngine;
using UnityEngine.Localization;
using Object = UnityEngine.Object;

namespace Arsenal.Content
{
    /// <summary>
    /// The ship parts of the mod. Each one is a copy of a native part (so sockets, cables, interactions,
    /// saving and network sync all come from the game), with our model and a few extra sockets:
    /// - gunner seat = advanced pilot seat + 3 fire outputs (sockets 6, 7, 8)
    /// - weapons = signal display (one signal input, its value synced to everyone) with a weapon model
    /// - armory = storage chest that comes stocked with the hand weapons
    /// </summary>
    internal static class ShipContent
    {
        public const ushort GunnerSeatId = 9201, MachineGunId = 9210, MissilePodId = 9211, LaserCannonId = 9212, ArmoryId = 9220;
        public const ushort ResearchLightId = 90, ResearchHeavyId = 91;
        public static readonly byte[] FireSockets = { 6, 7, 8 };

        private static readonly Dictionary<ushort, PartSettings> Parts = new Dictionary<ushort, PartSettings>();
        private static bool _building;

        public static bool IsWeapon(ushort settingsId) => settingsId == MachineGunId || settingsId == MissilePodId || settingsId == LaserCannonId;

        public static void Patch(HarmonyLib.Harmony h)
        {
            h.Patch(AccessTools.Method(typeof(PartSettingsProvider), "EnsureDictIsCached"),
                postfix: new HarmonyMethod(typeof(ShipContent), nameof(EnsureDictPostfix)));
            h.Patch(AccessTools.Method(typeof(BuildMenu), "Start"),
                prefix: new HarmonyMethod(typeof(ShipContent), nameof(BuildMenuStartPrefix)));
        }

        /// <summary>Parts are looked up through cached dictionaries; make sure ours are in every one.</summary>
        private static void EnsureDictPostfix(PartSettingsProvider __instance)
        {
            if (_building) return;
            var dict = Traverse.Create(__instance).Field("_partsDict").GetValue<Dictionary<ushort, PartSettings>>();
            if (dict == null) return;
            EnsureParts();
            foreach (var kv in Parts)
                if (kv.Value != null && !dict.ContainsKey(kv.Key))
                    dict[kv.Key] = kv.Value;
        }

        public static PartSettings Get(ushort id) => Parts.TryGetValue(id, out PartSettings p) ? p : null;

        public static void EnsureParts()
        {
            if (Parts.Count > 0 && Parts.Values.All(p => p != null)) return;
            _building = true;
            try
            {
                var all = Resources.FindObjectsOfTypeAll<PartSettings>().Where(p => p.id < 9000).GroupBy(p => p.id).ToDictionary(g => g.Key, g => g.First());
                if (!all.TryGetValue(29, out PartSettings seat) || !all.TryGetValue(82, out PartSettings display) || !all.TryGetValue(30, out PartSettings chest))
                    return;
                ItemSettings[] items = ItemList();
                Parts.Clear();
                Parts[GunnerSeatId] = BuildGunnerSeat(seat, items);
                Parts[MachineGunId] = BuildWeapon(display, MachineGunId, items);
                Parts[MissilePodId] = BuildWeapon(display, MissilePodId, items);
                Parts[LaserCannonId] = BuildWeapon(display, LaserCannonId, items);
                Parts[ArmoryId] = BuildArmory(chest, items);
                ArsenalMod.Log.Msg($"Registered {Parts.Count} ship parts.");
            }
            catch (Exception ex)
            {
                ArsenalMod.Log.Error($"Ship parts failed: {ex}");
            }
            finally
            {
                _building = false;
            }
        }

        private static ItemSettings[] ItemList()
        {
            ItemSettingsList list = Resources.FindObjectsOfTypeAll<ItemSettingsList>().FirstOrDefault();
            return list != null ? Traverse.Create(list).Field("items").GetValue<ItemSettings[]>() : new ItemSettings[0];
        }

        private static PartSettings Clone(PartSettings source, ushort id, string fr, string en, string descFr, string descEn, float mass, ItemSettings[] items, params (uint item, int qty)[] cost)
        {
            PartSettings p = Object.Instantiate(source);
            p.name = "Arsenal.Part" + id;
            p.id = id;
            p.fullLabel = Loc.T(fr, en);
            p.description = Loc.T(descFr, descEn);
            p.localizedDescription = new LocalizedString();
            p.mass = mass;
            var list = new List<ItemInstance>();
            foreach (var (item, qty) in cost)
            {
                ItemSettings s = items.FirstOrDefault(i => i.id == item);
                if (s != null) list.Add(new ItemInstance(s, qty));
            }
            Traverse.Create(p).Field("buildingCost").SetValue(list);
            Object.DontDestroyOnLoad(p);
            return p;
        }

        private static GameObject ClonePrefab(GameObject source, string name)
        {
            GameObject go = Object.Instantiate(source, Assets.TemplateRoot);
            go.name = name;
            return go;
        }

        // ------------------------------------------------------------------ gunner seat

        private static PartSettings BuildGunnerSeat(PartSettings seat, ItemSettings[] items)
        {
            PartSettings p = Clone(seat, GunnerSeatId, "Poste de tir", "Gunner seat",
                "Siège de pilotage avancé avec trois sorties de tir : clic gauche (Tir 1), clic droit (Tir 2), clic molette (Tir 3). Reliez-les aux armes avec des câbles de signal.",
                "Advanced pilot seat with three fire outputs: left click (Fire 1), right click (Fire 2), middle click (Fire 3). Wire them to weapons with signal cables.",
                28f, items, (101, 10), (112, 4));
            GameObject prefab = ClonePrefab(seat.part, "GunnerSeat");
            Transform template = prefab.GetComponentsInChildren<SocketObject>(true).OrderBy(s => s.Id).Last().transform;
            float[] zs = { -0.25f, 0f, 0.25f };
            for (int i = 0; i < FireSockets.Length; i++)
            {
                Transform s = Object.Instantiate(template.gameObject, prefab.transform).transform;
                s.name = "FireSocket" + (i + 1);
                s.localPosition = new Vector3(-0.5f, 0.45f, zs[i]);
                s.localEulerAngles = new Vector3(0, 90f, 0);
                Traverse.Create(s.GetComponent<SocketObject>()).Field("id").SetValue(FireSockets[i]);
            }
            Transform visuals = prefab.transform.Find("Visuals") ?? prefab.transform;
            GameObject console = PartModels.GunnerConsole();
            console.transform.SetParent(visuals, false);
            p.part = prefab;
            return p;
        }

        // ------------------------------------------------------------------ weapons

        private static PartSettings BuildWeapon(PartSettings display, ushort id, ItemSettings[] items)
        {
            PartSettings p;
            GameObject model;
            Vector3 boundsCenter, boundsSize;
            switch (id)
            {
                case MachineGunId:
                    p = Clone(display, id, "Mitrailleuse lourde", "Heavy machine gun",
                        "Tire tant que son entrée de signal est active (> 0,5). Balles perforantes : traversent une à deux plaques de fer.",
                        "Fires while its signal input is on (> 0.5). Armour-piercing rounds: go through one or two iron plates.",
                        60f, items, (101, 14), (112, 4));
                    model = PartModels.MachineGun(out _);
                    boundsCenter = new Vector3(0, 0.2f, 0.22f);
                    boundsSize = new Vector3(0.6f, 0.4f, 1.1f);
                    break;
                case MissilePodId:
                    p = Clone(display, id, "Lance-missiles air-air", "Air-to-air missile pod",
                        "Verrouille le vaisseau le plus proche dans son axe et tire un missile autoguidé à chaque impulsion de signal. 4 missiles, rechargement automatique.",
                        "Locks the nearest ship in front of it and fires a homing missile on each signal pulse. 4 missiles, reloads by itself.",
                        90f, items, (101, 20), (112, 8), (103, 1));
                    model = PartModels.MissilePod(out _);
                    boundsCenter = new Vector3(0, 0.27f, 0.08f);
                    boundsSize = new Vector3(0.6f, 0.55f, 0.76f);
                    break;
                default:
                    p = Clone(display, id, "Canon laser", "Laser cannon",
                        "Rayon continu tant que le signal est actif. Fait fondre la coque et perce des trous. Surchauffe après quelques secondes.",
                        "Continuous beam while the signal is on. Melts through hulls. Overheats after a few seconds.",
                        80f, items, (101, 12), (112, 10), (102, 6), (103, 2));
                    model = PartModels.LaserCannon(out _, out _);
                    boundsCenter = new Vector3(0, 0.2f, 0.18f);
                    boundsSize = new Vector3(0.6f, 0.45f, 0.98f);
                    break;
            }
            p.size = Vector3.one;
            GameObject prefab = ClonePrefab(display.part, "Arsenal.Weapon" + id);
            ShipPartBounds bounds = prefab.GetComponent<ShipPartBounds>();
            if (bounds != null)
            {
                bounds.center = boundsCenter;
                bounds.bounds = boundsSize;
            }
            Transform visuals = prefab.transform.Find("Visuals");
            // hide the display screen, keep its component happy
            foreach (Transform t in visuals.Cast<Transform>().ToList())
                t.gameObject.SetActive(false);
            model.transform.SetParent(visuals, false);
            // interaction / physics box around the model
            BoxCollider box = prefab.transform.Find("Interactions")?.GetComponentInChildren<BoxCollider>(true);
            if (box != null)
            {
                box.transform.localPosition = Vector3.zero;
                box.center = boundsCenter;
                box.size = boundsSize;
            }
            // signal input at the back of the base
            Transform socket = prefab.GetComponentsInChildren<SocketObject>(true).FirstOrDefault()?.transform;
            if (socket != null)
            {
                socket.localPosition = new Vector3(0f, 0.07f, -0.37f);
                socket.localEulerAngles = new Vector3(0, 180f, 0);
            }
            p.part = prefab;
            return p;
        }

        // ------------------------------------------------------------------ armory

        private static PartSettings BuildArmory(PartSettings chest, ItemSettings[] items)
        {
            PartSettings p = Clone(chest, ArmoryId, "Armurerie", "Armory",
                "Coffre livré avec un pistolet, un fusil d'assaut, un fusil anti-matériel, des grenades et des charges de brèche.",
                "Chest delivered with a sidearm, an assault rifle, an anti-materiel rifle, grenades and breaching charges.",
                40f, items, (101, 30), (112, 10), (110, 6));
            GameObject prefab = ClonePrefab(chest.part, "Armory");
            Transform visuals = prefab.transform.Find("Visual") ?? prefab.transform;
            PartModels.ArmoryRack().transform.SetParent(visuals, false);
            p.part = prefab;
            return p;
        }

        /// <summary>Server: a newly built armory gets stocked.</summary>
        public static void OnServerPartAdded(uint shipId, StatefulPart part)
        {
            if (part.Settings == null || part.Settings.id != ArmoryId) return;
            if (!(part.State is StorageState storage)) return;
            for (byte i = 0; i < storage.SlotCount; i++)
                if (storage.GetSlot(i).HasItem) return; // already stocked (loaded from a save)
            IStoragePart s = storage;
            (uint id, byte qty)[] stock =
            {
                (WeaponDefs.PistolId, 1), (WeaponDefs.RifleId, 1), (WeaponDefs.MarksmanId, 1),
                (WeaponDefs.GrenadeId, 10), (WeaponDefs.ChargeId, 5),
            };
            for (byte i = 0; i < stock.Length; i++)
                s = s.WithUpdatedSlot(i, new InventorySlot { HasItem = true, ItemId = stock[i].id, Quantity = stock[i].qty });
            if (Svc.ShipsServer.TryGetTrackedShip(shipId, out TrackedShipServer ship))
                ship.UpdatePartState(part.Id, (IStatefulPartState)s);
        }

        // ------------------------------------------------------------------ build menu + research

        private static void BuildMenuStartPrefix(BuildMenu __instance)
        {
            EnsureParts();
            var tabs = Traverse.Create(__instance).Field("tabsConfig").GetValue<BuildMenuTabConfig[]>();
            if (tabs == null || tabs.Any(t => t.name == "Arsenal") || Parts.Count == 0) return;
            var tab = new BuildMenuTabConfig
            {
                name = "Arsenal",
                icon = tabs.Last().icon,
                rows = new[]
                {
                    new BuildMenuTabConfig.BuildMenuRow { parts = new[] { Parts[GunnerSeatId], Parts[ArmoryId] } },
                    new BuildMenuTabConfig.BuildMenuRow { parts = new[] { Parts[MachineGunId], Parts[MissilePodId], Parts[LaserCannonId] } },
                },
            };
            Traverse.Create(__instance).Field("tabsConfig").SetValue(tabs.Concat(new[] { tab }).ToArray());
        }

        public static void RegisterResearch()
        {
            EnsureParts();
            if (Parts.Count == 0) return;
            foreach (ResearchSettingsList list in Resources.FindObjectsOfTypeAll<ResearchSettingsList>())
            {
                if (list.list.Any(r => r != null && r.id == ResearchLightId)) continue;
                ResearchSettings controls = list.GetResearchSettingById(12) ?? list.list.First();
                ItemSettings[] items = ItemList();
                ResearchSettings light = MakeResearch(ResearchLightId, Loc.T("Armement", "Weaponry"), new Vector2Int(4, 3), new[] { controls },
                    new[] { Parts[GunnerSeatId], Parts[ArmoryId], Parts[MachineGunId] }, items, (101, 25), (112, 10));
                ResearchSettings heavy = MakeResearch(ResearchHeavyId, Loc.T("Armement lourd", "Heavy weaponry"), new Vector2Int(4, 4), new[] { light },
                    new[] { Parts[MissilePodId], Parts[LaserCannonId] }, items, (101, 30), (112, 15), (103, 2));
                list.list.Add(light);
                list.list.Add(heavy);
                ArsenalMod.Log.Msg("Research added: Weaponry, Heavy weaponry.");
            }
        }

        private static ResearchSettings MakeResearch(ushort id, string name, Vector2Int pos, ResearchSettings[] req, PartSettings[] parts, ItemSettings[] items, params (uint item, int qty)[] cost)
        {
            var r = ScriptableObject.CreateInstance<ResearchSettings>();
            r.name = "Arsenal.Research" + id;
            r.id = id;
            r.techName = name;
            r.unlockedParts = parts;
            r.unlockedFeatures = new Research.Interface.Model.ResearchUnlockableFeature[0];
            r.unlockedGuideEntriesIds = new uint[0];
            r.requirements = req;
            r.resourceCost = cost.Select(c => new ItemInstance(items.First(i => i.id == c.item), c.qty)).ToArray();
            r.positionInTechTree = pos;
            r.availableInDemo = true;
            Object.DontDestroyOnLoad(r);
            return r;
        }
    }
}
