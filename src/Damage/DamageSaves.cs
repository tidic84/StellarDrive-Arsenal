using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using HarmonyLib;
using Managers.Interface;
using Saving.Services;
using Ships.Interface.Model;
using Ships.Interface.Model.Placement;
using UnityEngine;

namespace Arsenal.Damage
{
    /// <summary>
    /// Keeps hull damage (health and holes) with the world: a small text file next to world.json, written
    /// whenever the game saves and read back when the world loads. Without the mod the world loads fine;
    /// the panels are simply whole again.
    /// </summary>
    internal static class DamageSaves
    {
        private const string FileName = "arsenal-damage.txt";

        public static void Init(HarmonyLib.Harmony h)
        {
            h.Patch(AccessTools.Method(typeof(GameSaver), "SaveGame", new[] { typeof(string) }),
                postfix: new HarmonyMethod(typeof(DamageSaves), nameof(AfterSave)));
            h.Patch(AccessTools.Method(typeof(GameLoader), nameof(GameLoader.LoadState)),
                postfix: new HarmonyMethod(typeof(DamageSaves), nameof(AfterLoad)));
        }

        private static void AfterSave(string worldDirectoryFullName)
        {
            try
            {
                string path = Path.Combine(worldDirectoryFullName, FileName);
                var lines = HullDamage.ServerStates.Select(kv => string.Join(" ",
                    kv.Key.ShipId, kv.Key.Pos.VoxelCoord.x, kv.Key.Pos.VoxelCoord.y, kv.Key.Pos.VoxelCoord.z, (int)kv.Key.Pos.Type,
                    kv.Value.Health.ToString("R", CultureInfo.InvariantCulture),
                    kv.Value.Holes.A, kv.Value.Holes.B, kv.Value.Holes.C, kv.Value.Holes.D)).ToList();
                if (lines.Count == 0)
                {
                    if (File.Exists(path)) File.Delete(path);
                    return;
                }
                File.WriteAllLines(path, new[] { "# Arsenal hull damage v1: ship x y z type health holesA holesB holesC holesD" }.Concat(lines));
            }
            catch (Exception ex)
            {
                ArsenalMod.Log.Error($"Saving hull damage failed: {ex.Message}");
            }
        }

        private static void AfterLoad()
        {
            try
            {
                string world = MainGameSceneLoadData.SaveFileFullName;
                string dir = string.IsNullOrEmpty(world) ? null : Path.GetDirectoryName(world);
                string path = dir == null ? null : Path.Combine(dir, FileName);
                var states = new List<KeyValuePair<PanelKey, PanelState>>();
                if (path != null && File.Exists(path))
                {
                    foreach (string line in File.ReadAllLines(path))
                    {
                        if (line.StartsWith("#") || string.IsNullOrWhiteSpace(line)) continue;
                        string[] p = line.Split(' ');
                        if (p.Length < 10) continue;
                        var key = new PanelKey(uint.Parse(p[0]), new HullPartPosition(new Vector3Int(int.Parse(p[1]), int.Parse(p[2]), int.Parse(p[3])), (VoxelPartType)int.Parse(p[4])));
                        var st = new PanelState
                        {
                            Health = float.Parse(p[5], CultureInfo.InvariantCulture),
                            Holes = new HoleMask { A = ulong.Parse(p[6]), B = ulong.Parse(p[7]), C = ulong.Parse(p[8]), D = ulong.Parse(p[9]) },
                        };
                        states.Add(new KeyValuePair<PanelKey, PanelState>(key, st));
                    }
                }
                HullDamage.LoadServerStates(states);
                if (states.Count > 0) ArsenalMod.Log.Msg($"Loaded damage of {states.Count} hull panels.");
            }
            catch (Exception ex)
            {
                ArsenalMod.Log.Error($"Loading hull damage failed: {ex.Message}");
            }
        }
    }
}
