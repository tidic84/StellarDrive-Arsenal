using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Saving.Serialization.V12.Cables;
using Saving.Serialization.V15;
using Saving.Services.Converters;
using SerializableShip = Saving.Serialization.V15.Ships.SerializableShip;

namespace Arsenal.Content
{
    /// <summary>
    /// Parts the mod no longer has, swapped out of a world before it is built. The gunner seat (an advanced pilot
    /// seat with three extra fire outputs) becomes the game's own advanced pilot seat, and the cables plugged into
    /// those fire outputs (sockets 6-8, which the game's seat does not have) are taken off.
    /// </summary>
    internal static class LegacyParts
    {
        private static readonly byte[] OldFireSockets = { 6, 7, 8 };

        public static void Patch(HarmonyLib.Harmony h)
        {
            h.Patch(AccessTools.Method(typeof(GameWorldConverter), nameof(GameWorldConverter.ConvertToState)),
                prefix: new HarmonyMethod(typeof(LegacyParts), nameof(BeforeLoadWorld)));
        }

        private static void BeforeLoadWorld(SerializableGameWorld gameWorld)
        {
#if DEV
            ArsenalMod.Log.Msg($"[DEV] world load: {gameWorld?.ships?.Length ?? -1} ships, {gameWorld?.cables?.Length ?? -1} cables");
#endif
            if (gameWorld?.ships == null) return;
            try
            {
                var seats = new HashSet<(uint ship, ushort part)>();
                foreach (SerializableShip ship in gameWorld.ships)
                {
                    if (ship?.statefulFloatingParts == null) continue;
                    for (int i = 0; i < ship.statefulFloatingParts.Length; i++)
                    {
                        if (ship.statefulFloatingParts[i].settingsId != ShipContent.LegacyGunnerSeatId) continue;
                        ship.statefulFloatingParts[i].settingsId = ShipContent.AdvancedPilotSeatId;
                        seats.Add((ship.id, ship.statefulFloatingParts[i].id));
                    }
                }
                if (seats.Count == 0) return;
                int cut = 0;
                if (gameWorld.cables != null)
                {
                    bool OnFireOutput(SerializablePlug p) => p != null && seats.Contains((p.shipId, p.partId)) && OldFireSockets.Contains(p.socketId);
                    SerializableCable[] kept = gameWorld.cables.Where(c => c == null || !(OnFireOutput(c.plugA) || OnFireOutput(c.plugB))).ToArray();
                    cut = gameWorld.cables.Length - kept.Length;
                    gameWorld.cables = kept;
                }
                ArsenalMod.Log.Msg($"Replaced {seats.Count} gunner seat(s) with advanced pilot seats; removed {cut} cable(s) from their fire outputs.");
            }
            catch (Exception ex)
            {
                ArsenalMod.Log.Error($"Replacing old gunner seats failed: {ex.Message}");
            }
        }
    }
}
