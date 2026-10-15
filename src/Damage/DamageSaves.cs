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
                    kv.Value.Holes.ToSaveString())).ToList();
                if (lines.Count == 0)
                {
                    if (File.Exists(path)) File.Delete(path);
                    return;
                }
                File.WriteAllLines(path, new[] { "# Arsenal hull damage v2: ship x y z type health holes (packed round holes in hex)" }.Concat(lines));
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
                int bad = 0;
                if (path != null && File.Exists(path))
                {
                    foreach (string line in File.ReadAllLines(path))
                    {
                        if (line.StartsWith("#") || string.IsNullOrWhiteSpace(line)) continue;
                        // one damaged line costs that panel only, not the damage of the whole world
                        try
                        {
                            string[] p = line.Split(' ');
                            if (p.Length < 7) { bad++; continue; }
                            var key = new PanelKey(uint.Parse(p[0], CultureInfo.InvariantCulture),
                                new HullPartPosition(new Vector3Int(int.Parse(p[1], CultureInfo.InvariantCulture), int.Parse(p[2], CultureInfo.InvariantCulture), int.Parse(p[3], CultureInfo.InvariantCulture)),
                                    (VoxelPartType)int.Parse(p[4], CultureInfo.InvariantCulture)));
                            var st = new PanelState
                            {
                                Health = float.Parse(p[5], CultureInfo.InvariantCulture),
                                // v1 lines hold four 64-bit cell masks, v2 lines one list of round holes
                                Holes = p.Length >= 10
                                    ? HoleMask.FromLegacyCells(ulong.Parse(p[6], CultureInfo.InvariantCulture), ulong.Parse(p[7], CultureInfo.InvariantCulture), ulong.Parse(p[8], CultureInfo.InvariantCulture), ulong.Parse(p[9], CultureInfo.InvariantCulture))
                                    : HoleMask.FromSaveString(p[6]),
                            };
                            states.Add(new KeyValuePair<PanelKey, PanelState>(key, st));
                        }
                        catch (Exception)
                        {
                            bad++;
                        }
                    }
                }
                HullDamage.LoadServerStates(states);
                if (states.Count > 0) ArsenalMod.Log.Msg($"Loaded damage of {states.Count} hull panels.");
                if (bad > 0) ArsenalMod.Log.Warning($"Skipped {bad} unreadable line(s) in {FileName}.");
            }
            catch (Exception ex)
            {
                ArsenalMod.Log.Error($"Loading hull damage failed: {ex.Message}");
            }
        }
    }
}
