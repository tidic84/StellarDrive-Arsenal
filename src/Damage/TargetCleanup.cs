using System;
using System.Collections.Generic;
using System.Linq;
using Arsenal.Core;
using HarmonyLib;
using Saving.Services.Converters;
using Saving.Serialization.V12.Ships;
using Ships;
using Ships.Services;
using Ships.Interface.Model;
using Ships.Interface.Model.Parts;
using Ships.Interface.Model.Parts.State;
using SerializableShip = Saving.Serialization.V15.Ships.SerializableShip;

namespace Arsenal.Damage
{
    /// <summary>
    /// Keeps targeting computers from pointing at beacons that no longer exist. The game only clears them when a
    /// player dismantles a beacon by hand; a beacon blown up by a blast, or a whole ship wrecked until it is
    /// removed, left the targeter aimed at a ghost, and a world saved like that crashed on load. So: clear them
    /// when a beacon or a ship goes away, and drop dangling targets from the save before the world is built.
    /// </summary>
    internal static class TargetCleanup
    {
        public static void Patch(HarmonyLib.Harmony h)
        {
            h.Patch(AccessTools.Method(typeof(TrackedShipServer), nameof(TrackedShipServer.DestroyStatefulPart)),
                prefix: new HarmonyMethod(typeof(TargetCleanup), nameof(BeforeDestroyPart)));
            h.Patch(AccessTools.Method(typeof(ShipsServerTracker), nameof(ShipsServerTracker.RemoveShip)),
                postfix: new HarmonyMethod(typeof(TargetCleanup), nameof(AfterRemoveShip)));
            h.Patch(AccessTools.Method(typeof(ShipsConverter), nameof(ShipsConverter.ConvertToState)),
                prefix: new HarmonyMethod(typeof(TargetCleanup), nameof(BeforeLoadShips)));
        }

        private static void BeforeDestroyPart(TrackedShipServer __instance, ushort partId)
        {
            try
            {
                if (__instance.TryGetStatefulPart(partId, out StatefulPart part) && part.State is BeaconState)
                    ClearTargetsOn(__instance.Id, partId);
            }
            catch (Exception ex) { ArsenalMod.Log.Warning($"Clearing targets on a destroyed beacon failed: {ex.Message}"); }
        }

        private static void AfterRemoveShip(ShipState shipState)
        {
            try { ClearTargetsOn(shipState.Id, null); }
            catch (Exception ex) { ArsenalMod.Log.Warning($"Clearing targets on a removed ship failed: {ex.Message}"); }
            // its hull and machinery damage goes with it, or it would stay in the save forever
            HullDamage.ForgetShip(shipState.Id);
            PartDamage.ForgetShip(shipState.Id);
        }

        /// <summary>Clears every targeting computer aimed at a beacon of this ship (or at this one beacon).</summary>
        private static void ClearTargetsOn(uint shipId, ushort? beaconPartId)
        {
            foreach (TrackedShipServer ship in Svc.ShipsServer.Ships.ToList())
            {
                foreach (StatefulPart part in ship.GetAllStatefulParts<TargetingComputerState>().ToList())
                {
                    var state = (TargetingComputerState)part.State;
                    if (!state.HasTarget || state.Target.TargetType != TargetType.Beacon) continue;
                    if (state.Target.Beacon.ShipId != shipId) continue;
                    if (beaconPartId.HasValue && state.Target.Beacon.PartId != beaconPartId.Value) continue;
                    ship.ClearTarget(part.Id, state.Target);
                }
            }
        }

        private static void BeforeLoadShips(SerializableShip[] ships)
        {
            if (ships == null) return;
            try
            {
                var partsByShip = new Dictionary<uint, HashSet<ushort>>();
                foreach (SerializableShip ship in ships)
                {
                    var ids = new HashSet<ushort>();
                    if (ship.statefulFloatingParts != null) foreach (var p in ship.statefulFloatingParts) ids.Add(p.id);
                    if (ship.statefulHullParts != null) foreach (var p in ship.statefulHullParts) ids.Add(p.id);
                    partsByShip[ship.id] = ids;
                }
                int cleared = 0;
                foreach (SerializableShip ship in ships)
                {
                    if (ship.targeterStates == null) continue;
                    for (int i = 0; i < ship.targeterStates.Length; i++)
                    {
                        SerializableTargeterState t = ship.targeterStates[i];
                        if (!t.hasTarget || t.targetType != (byte)TargetType.Beacon) continue;
                        if (partsByShip.TryGetValue(t.targetShipId, out HashSet<ushort> ids) && ids.Contains(t.targetPartId)) continue;
                        ArsenalMod.Log.Msg($"Targeting computer {t.partId} of ship {ship.id} was aimed at beacon {t.targetPartId} of ship {t.targetShipId}, which no longer exists: target cleared.");
                        t.hasTarget = false;
                        t.targetShipId = 0;
                        t.targetPartId = 0;
                        ship.targeterStates[i] = t;
                        cleared++;
                    }
                }
                if (cleared > 0) ArsenalMod.Log.Msg($"Cleared {cleared} dangling target(s) from the save.");
            }
            catch (Exception ex) { ArsenalMod.Log.Error($"Checking saved targets failed: {ex.Message}"); }
        }
    }
}
