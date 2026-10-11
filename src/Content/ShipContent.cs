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
        public const ushort PdcId = 9213, PlasmaCannonId = 9214, IonCannonId = 9215, RailgunId = 9216, TorpedoId = 9217, RocketPodId = 9218, SamBatteryId = 9219;
        public static readonly ushort[] WeaponIds = { MachineGunId, MissilePodId, LaserCannonId, PdcId, PlasmaCannonId, IonCannonId, RailgunId, TorpedoId, RocketPodId, SamBatteryId };
        public const ushort ResearchLightId = 90, ResearchHeavyId = 91, ResearchAdvancedId = 92;
        public static readonly byte[] FireSockets = { 6, 7, 8 };

        private static readonly Dictionary<ushort, PartSettings> Parts = new Dictionary<ushort, PartSettings>();
        private static bool _building;

        public static bool IsWeapon(ushort settingsId) => settingsId >= MachineGunId && settingsId <= SamBatteryId;

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
                foreach (ushort w in WeaponIds) Parts[w] = BuildWeapon(display, w, items);
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
                case PdcId:
                    p = Clone(display, id, "Défense rapprochée « Guardian »", "\"Guardian\" point defence",
                        "Tourelle gatling automatique : tant que son signal est actif, elle vise et abat les missiles, roquettes, torpilles et projectiles ennemis qui approchent (300 m).",
                        "Automatic gatling turret: while its signal is on it tracks and shoots down incoming missiles, rockets, torpedoes and projectiles (300 m).",
                        70f, items, (101, 16), (112, 8), (110, 2));
                    model = PartModels.Pdc();
                    boundsCenter = new Vector3(0, 0.22f, 0.12f);
                    boundsSize = new Vector3(0.5f, 0.44f, 0.8f);
                    break;
                case PlasmaCannonId:
                    p = Clone(display, id, "Canon à plasma P-400", "P-400 plasma cannon",
                        "Projette une boule de plasma lente qui explose à l'impact. Tire en continu tant que le signal est actif.",
                        "Throws a slow plasma ball that explodes on impact. Keeps firing while the signal is on.",
                        85f, items, (101, 18), (112, 10), (103, 2));
                    model = PartModels.PlasmaCannon();
                    boundsCenter = new Vector3(0, 0.22f, 0.1f);
                    boundsSize = new Vector3(0.6f, 0.46f, 0.9f);
                    break;
                case IonCannonId:
                    p = Clone(display, id, "Canon ionique « Disruptor »", "\"Disruptor\" ion cannon",
                        "Tire un éclair ionique qui coupe les commandes du vaisseau touché pendant 8 secondes. Peu de dégâts.",
                        "Fires an ion bolt that knocks out the controls of the ship it hits for 8 seconds. Little damage.",
                        75f, items, (101, 14), (112, 14), (102, 4));
                    model = PartModels.IonCannon();
                    boundsCenter = new Vector3(0, 0.2f, 0.08f);
                    boundsSize = new Vector3(0.5f, 0.42f, 0.85f);
                    break;
                case RailgunId:
                    p = Clone(display, id, "Railgun naval « Longinus »", "\"Longinus\" ship railgun",
                        "Se charge une seconde quand le signal s'active puis tire un projectile qui traverse plusieurs vaisseaux. Long rechargement.",
                        "Charges for a second when the signal turns on, then fires a slug that goes through several ships. Long reload.",
                        120f, items, (101, 30), (112, 20), (102, 8), (103, 3));
                    model = PartModels.Railgun();
                    boundsCenter = new Vector3(0, 0.2f, 0.27f);
                    boundsSize = new Vector3(0.5f, 0.42f, 1.15f);
                    break;
                case TorpedoId:
                    p = Clone(display, id, "Tube lance-torpilles « Leviathan »", "\"Leviathan\" torpedo tube",
                        "Verrouille un vaisseau et lance une torpille lente à tête lourde qui le poursuit. Énorme explosion. Une torpille, rechargement en 15 s.",
                        "Locks a ship and launches a slow heavy torpedo that chases it. Huge blast. One torpedo, reloads in 15 s.",
                        140f, items, (101, 30), (112, 12), (103, 4), (100, 4));
                    model = PartModels.TorpedoTube();
                    boundsCenter = new Vector3(0, 0.3f, 0.05f);
                    boundsSize = new Vector3(0.6f, 0.6f, 1.0f);
                    break;
                case RocketPodId:
                    p = Clone(display, id, "Lance-roquettes « Grêle »", "\"Hail\" rocket pod",
                        "Tire une salve de 9 roquettes non guidées tant que le signal est actif. Grosse explosion à l'impact, qui repousse les vaisseaux touchés. Rechargement complet en 12 s.",
                        "Fires a salvo of 9 unguided rockets while the signal is on. Big blast on impact that knocks ships off course. Full reload in 12 s.",
                        95f, items, (101, 22), (112, 8), (100, 4));
                    model = PartModels.RocketPod();
                    boundsCenter = new Vector3(0, 0.28f, 0.03f);
                    boundsSize = new Vector3(0.6f, 0.56f, 0.8f);
                    break;
                case SamBatteryId:
                    p = Clone(display, id, "Batterie sol-air « Faucon »", "\"Falcon\" SAM battery",
                        "Tourelle à radar qui pivote seule vers l'appareil le plus proche et le verrouille automatiquement. À chaque impulsion de signal, tire un missile qui le poursuit. 4 missiles, rechargement automatique.",
                        "Radar turret that turns by itself towards the nearest aircraft and locks it automatically. Each signal pulse fires a missile that chases it. 4 missiles, reloads by itself.",
                        110f, items, (101, 26), (112, 14), (103, 3), (102, 4));
                    model = PartModels.SamBattery();
                    boundsCenter = new Vector3(0, 0.3f, 0.05f);
                    boundsSize = new Vector3(0.6f, 0.6f, 0.8f);
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
                socket.localEulerAngles = Vector3.zero; // the game plugs cables in along the socket's forward: into the weapon
            }
            p.part = prefab;
            return p;
        }

        // ------------------------------------------------------------------ armory

        private static PartSettings BuildArmory(PartSettings chest, ItemSettings[] items)
        {
            PartSettings p = Clone(chest, ArmoryId, "Armurerie", "Armory",
                "Terminal de fabrication d'armes : transforme vos matériaux en armes de poing, fusils, armes lourdes, armes à énergie et explosifs. Sert aussi de coffre.",
                "Weapon fabrication terminal: turns your materials into sidearms, rifles, heavy weapons, energy weapons and explosives. Also works as a chest.",
                40f, items, (101, 30), (112, 10), (110, 6));
            GameObject prefab = ClonePrefab(chest.part, "Armory");
            Transform visuals = prefab.transform.Find("Visual") ?? prefab.transform;
            PartModels.ArmoryRack().transform.SetParent(visuals, false);
            p.part = prefab;
            return p;
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
                    new BuildMenuTabConfig.BuildMenuRow { parts = new[] { Parts[MachineGunId], Parts[MissilePodId], Parts[LaserCannonId], Parts[PdcId] } },
                    new BuildMenuTabConfig.BuildMenuRow { parts = new[] { Parts[PlasmaCannonId], Parts[IonCannonId], Parts[RailgunId], Parts[TorpedoId] } },
                    new BuildMenuTabConfig.BuildMenuRow { parts = new[] { Parts[RocketPodId], Parts[SamBatteryId] } },
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
                    new[] { Parts[MissilePodId], Parts[LaserCannonId], Parts[PdcId], Parts[RocketPodId] }, items, (101, 30), (112, 15), (103, 2));
                ResearchSettings advanced = MakeResearch(ResearchAdvancedId, Loc.T("Armement avancé", "Advanced weaponry"), FreeSpot(list, heavy.positionInTechTree), new[] { heavy },
                    new[] { Parts[PlasmaCannonId], Parts[IonCannonId], Parts[RailgunId], Parts[TorpedoId], Parts[SamBatteryId] }, items, (101, 40), (112, 25), (102, 10), (103, 4));
                list.list.Add(light);
                list.list.Add(heavy);
                list.list.Add(advanced);
                ArsenalMod.Log.Msg($"Research added: Weaponry, Heavy weaponry, Advanced weaponry at {advanced.positionInTechTree}.");
            }
        }

        /// <summary>First free cell of the tech tree next to (preferably below) the given one.</summary>
        private static Vector2Int FreeSpot(ResearchSettingsList list, Vector2Int near)
        {
            var used = new HashSet<Vector2Int>(list.list.Where(r => r != null).Select(r => r.positionInTechTree)) { new Vector2Int(4, 3), near };
            foreach (Vector2Int d in new[] { Vector2Int.up, Vector2Int.right, Vector2Int.left, new Vector2Int(1, 1), new Vector2Int(-1, 1), Vector2Int.down })
                if (!used.Contains(near + d)) return near + d;
            return near + new Vector2Int(0, 2);
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
