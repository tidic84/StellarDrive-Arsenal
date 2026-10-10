using System.Collections.Generic;
using System.IO;
using System.Linq;
using Arsenal.Content;
using Arsenal.Core;
using Arsenal.Net;
using FishNet.Connection;
using HarmonyLib;
using MelonLoader;
using Players.Interface.Model;
using Ships;
using Ships.Cables;
using Ships.Interface.Model.Parts;
using Ships.Interface.Model.Parts.Common;
using Ships.Interface.Model.Parts.State;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Arsenal.Vehicles
{
    /// <summary>
    /// The gunner seat is an advanced pilot seat with three more signal outputs (sockets 6-8) that follow
    /// the pilot's fire buttons. The pilot's client sends button changes; the server feeds them into the
    /// game's own signal network, so any cable/logic setup works (splitters, gates, several weapons...).
    /// </summary>
    internal static class GunnerSeat
    {
        private static readonly byte[] SeatSockets = { 0, 1, 2, 3, 4, 5, 6, 7, 8 };

        // server: (ship, part) -> buttons bitmask
        private static readonly Dictionary<PartContext, byte> Buttons = new Dictionary<PartContext, byte>();

        // client
        private static byte _lastSent;
        private static PartContext _lastSeat;
        private static float _resendAt;
        private static MelonPreferences_Entry<string> _key1, _key2, _key3;

        public static void Init(MelonPreferences_Category prefs)
        {
            _key1 = prefs.CreateEntry("GunnerFire1", "<Mouse>/leftButton", "Gunner fire 1", "Input control path for Fire 1 in the gunner seat.");
            _key2 = prefs.CreateEntry("GunnerFire2", "<Mouse>/rightButton", "Gunner fire 2", "Input control path for Fire 2 in the gunner seat.");
            _key3 = prefs.CreateEntry("GunnerFire3", "<Mouse>/middleButton", "Gunner fire 3", "Input control path for Fire 3 in the gunner seat.");
            ArsenalNet.OnServer(Op.GunnerInput, ServerOnInput);
        }

        public static void Patch(HarmonyLib.Harmony h)
        {
            h.Patch(AccessTools.PropertyGetter(typeof(PilotSeatAdvState), nameof(PilotSeatAdvState.EmissionSockets)),
                postfix: new HarmonyMethod(typeof(GunnerSeat), nameof(EmissionSocketsPostfix)));
            h.Patch(AccessTools.Method(typeof(SignalConnectionGroupValues), "GetSocketSignal"),
                postfix: new HarmonyMethod(typeof(GunnerSeat), nameof(GetSocketSignalPostfix)));
        }

#if DEV
        public static void DevSetButtons(PartContext seat, byte mask)
        {
            if (mask == 0) Buttons.Remove(seat);
            else Buttons[seat] = mask;
        }
#endif

        public static void Reset()
        {
            Buttons.Clear();
            _lastSent = 0;
        }

        /// <summary>Advanced seats now emit on 0-8; plain advanced seats simply have nothing plugged in 6-8.</summary>
        private static void EmissionSocketsPostfix(ref byte[] __result) => __result = SeatSockets;

        private static void GetSocketSignalPostfix(EntityPartSocket entityPartSocket, ref double __result)
        {
            byte socket = entityPartSocket.Socket.SocketId;
            if (socket < 6 || socket > 8) return;
            if (!Buttons.TryGetValue(entityPartSocket.PartContext, out byte mask)) return;
            __result = (mask & (1 << (socket - 6))) != 0 ? 1.0 : 0.0;
        }

        private static void ServerOnInput(NetworkConnection conn, BinaryReader r)
        {
            var seat = new PartContext(r.ReadUInt32(), r.ReadUInt16());
            byte mask = r.ReadByte();
            if (mask == 0) Buttons.Remove(seat);
            else Buttons[seat] = mask;
        }

        /// <summary>Client, every frame: read the fire buttons while piloting a gunner seat.</summary>
        public static void ClientUpdate()
        {
            if (!TryGetPilotedGunnerSeat(out PartContext seat))
            {
                if (_lastSent != 0) Send(_lastSeat, 0);
                return;
            }
            byte mask = 0;
            if (Pressed(_key1.Value)) mask |= 1;
            if (Pressed(_key2.Value)) mask |= 2;
            if (Pressed(_key3.Value)) mask |= 4;
            if (mask != _lastSent || !seat.Equals(_lastSeat) || (mask != 0 && Time.time >= _resendAt))
                Send(seat, mask);
        }

        private static void Send(PartContext seat, byte mask)
        {
            _lastSent = mask;
            _lastSeat = seat;
            _resendAt = Time.time + 0.5f;
            ArsenalNet.SendToServer(Op.GunnerInput, w =>
            {
                w.Write(seat.ShipId);
                w.Write(seat.PartId);
                w.Write(mask);
            });
        }

        private static readonly Dictionary<string, InputControl> Controls = new Dictionary<string, InputControl>();

        private static bool Pressed(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            if (!Controls.TryGetValue(path, out InputControl c) || c == null || c.device == null || !c.device.added)
            {
                c = InputSystem.FindControl(path);
                Controls[path] = c;
            }
            return c is UnityEngine.InputSystem.Controls.ButtonControl b && b.isPressed;
        }

        public static bool TryGetPilotedGunnerSeat(out PartContext seat)
        {
            seat = default;
            if (Svc.PlayersClient == null || !Svc.PlayersClient.TryGetLocalPlayerState(out PlayerState state)) return false;
            if (state.Type != PlayerStateType.Piloting) return false;
            if (!Svc.ShipsClient.TryGetTrackedShip(state.EntityId, out TrackedShipClient ship)) return false;
            if (!ship.TryGetStatefulPart(state.PartId, out StatefulPart part) || part.Settings == null || part.Settings.id != ShipContent.GunnerSeatId) return false;
            seat = new PartContext(state.EntityId, state.PartId);
            return true;
        }

        /// <summary>The ship (root) the local player is piloting, gunner seat or not.</summary>
        public static bool TryGetPilotedRoot(out uint rootId)
        {
            rootId = 0;
            if (Svc.PlayersClient == null || !Svc.PlayersClient.TryGetLocalPlayerState(out PlayerState state)) return false;
            if (state.Type != PlayerStateType.Piloting) return false;
            rootId = Svc.ShipsClient.TryGetTrackedRootShip(state.EntityId, out TrackedShipClient root) && root != null ? root.Id : state.EntityId;
            return true;
        }
    }
}
