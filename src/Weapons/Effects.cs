using System.Collections.Generic;
using System.IO;
using System.Linq;
using Arsenal.Core;
using Arsenal.Net;
using Arsenal.Visual;
using FishNet.Connection;
using HarmonyLib;
using Managers.Interface.Model;
using Players;
using Ships;
using UnityEngine;

namespace Arsenal.Weapons
{
    /// <summary>
    /// Lasting effects, decided by the server and mirrored on every client:
    /// - burning players take damage over time;
    /// - an EMP'd ship loses its flight controls, fire buttons and ship weapons;
    /// - a jammed player cannot fire their weapons.
    /// </summary>
    internal static class Effects
    {
        private sealed class Burn
        {
            public float Until, NextTick;
            public NetworkConnection Attacker;
            public WeaponKind Weapon;
        }

        // server
        private static readonly Dictionary<uint, float> ShipEmp = new Dictionary<uint, float>();     // root ship -> until
        private static readonly Dictionary<uint, Burn> Burns = new Dictionary<uint, Burn>();         // player -> burn
        // client mirror (local Time.time)
        private static readonly Dictionary<uint, float> ClientShipEmp = new Dictionary<uint, float>();
        private static readonly Dictionary<uint, float> ClientBurning = new Dictionary<uint, float>();
        private static float _localJamUntil;
        private static float _nextVisual;
        private static readonly List<Ships.Interface.Model.Parts.StatefulPart> PartsBuffer = new List<Ships.Interface.Model.Parts.StatefulPart>();

        private const float BurnDamagePerSecond = 9f;

        public static void Init(HarmonyLib.Harmony h)
        {
            ArsenalNet.OnServer(Op.EmpRequest, (conn, r) =>
            {
                uint shipId = r.ReadUInt32();
                float secs = Mathf.Clamp(r.ReadSingle(), 0f, 15f);
                ServerEmpShip(shipId, secs);
            });
            ArsenalNet.OnClient(Op.EffectState, ClientOnState);
            h.Patch(AccessTools.Method(typeof(TrackedShipServer), nameof(TrackedShipServer.SetFlightInputs)),
                prefix: new HarmonyMethod(typeof(Effects), nameof(FlightInputsPrefix)));
        }

        public static void Reset()
        {
            ShipEmp.Clear();
            Burns.Clear();
            ClientShipEmp.Clear();
            ClientBurning.Clear();
            _localJamUntil = 0f;
        }

        // ------------------------------------------------------------------ server

        private static uint ServerRoot(uint shipId) =>
            Svc.ShipsServer != null && Svc.ShipsServer.TryGetTrackedRootShip(shipId, out TrackedShipServer root) && root != null ? root.Id : shipId;

        public static bool ServerIsEmp(uint shipId) => ShipEmp.TryGetValue(ServerRoot(shipId), out float until) && Time.time < until;

        public static void ServerEmpShip(uint shipId, float seconds)
        {
            uint root = ServerRoot(shipId);
            ShipEmp[root] = Mathf.Max(ShipEmp.TryGetValue(root, out float u) ? u : 0f, Time.time + seconds);
            Broadcast(EffectType.ShipEmp, root, ShipEmp[root] - Time.time);
        }

        public static void ServerBurn(uint playerId, float seconds, NetworkConnection attacker, WeaponKind weapon)
        {
            if (!Burns.TryGetValue(playerId, out Burn b)) Burns[playerId] = b = new Burn { NextTick = Time.time + 0.5f };
            b.Until = Mathf.Max(b.Until, Time.time + seconds);
            b.Attacker = attacker;
            b.Weapon = weapon;
            Broadcast(EffectType.Burning, playerId, b.Until - Time.time);
        }

        public static void ServerJamPlayer(uint playerId, float seconds)
        {
            TrackedPlayerServer p = PlayerDamage.Find(playerId);
            NetworkConnection conn = p != null ? PlayerDamage.Connection(p) : null;
            if (conn != null) ArsenalNet.SendTo(conn, Op.EffectState, w => WriteState(w, EffectType.Jammed, playerId, seconds));
        }

        private static readonly List<uint> Expired = new List<uint>();
        private static readonly List<KeyValuePair<uint, Burn>> Ticking = new List<KeyValuePair<uint, Burn>>();

        /// <summary>Drops the entries whose time is up, so the "nothing going on" shortcuts work again.</summary>
        private static void Purge(Dictionary<uint, float> until)
        {
            if (until.Count == 0) return;
            Expired.Clear();
            foreach (KeyValuePair<uint, float> kv in until)
                if (Time.time >= kv.Value) Expired.Add(kv.Key);
            foreach (uint id in Expired) until.Remove(id);
        }

        /// <summary>Server tick: burning players take their damage.</summary>
        public static void ServerUpdate()
        {
            if (!ArsenalNet.IsServer) return;
            Purge(ShipEmp);
            if (Burns.Count == 0) return;
            Ticking.Clear();
            Ticking.AddRange(Burns); // damage can reach back into Burns (a death clears effects)
            foreach (KeyValuePair<uint, Burn> kv in Ticking)
            {
                if (Time.time >= kv.Value.Until)
                {
                    Burns.Remove(kv.Key);
                    continue;
                }
                if (Time.time < kv.Value.NextTick) continue;
                kv.Value.NextTick = Time.time + 0.5f;
                PlayerDamage.ApplyServer(kv.Key, BurnDamagePerSecond * 0.5f, kv.Value.Attacker, kv.Value.Weapon, 0, Vector3.zero, showSource: false);
            }
        }

        /// <summary>EMP: pilots' stick inputs are dropped while the ship is down.</summary>
        private static void FlightInputsPrefix(TrackedShipServer __instance, ref FlightControlsNetworkInputs inputs)
        {
            if (ShipEmp.Count > 0 && ServerIsEmp(__instance.Id)) inputs = default;
        }

        private static void Broadcast(EffectType type, uint id, float seconds) =>
            ArsenalNet.SendToAll(Op.EffectState, w => WriteState(w, type, id, seconds));

        private static void WriteState(BinaryWriter w, EffectType type, uint id, float seconds)
        {
            w.Write((byte)type);
            w.Write(id);
            w.Write(seconds);
        }

        // ------------------------------------------------------------------ client

        public static void RequestShipEmp(uint shipId, float seconds)
        {
            ArsenalNet.SendToServer(Op.EmpRequest, w =>
            {
                w.Write(shipId);
                w.Write(seconds);
            });
        }

        private static void ClientOnState(BinaryReader r)
        {
            var type = (EffectType)r.ReadByte();
            uint id = r.ReadUInt32();
            float secs = r.ReadSingle();
            switch (type)
            {
                case EffectType.ShipEmp:
                    ClientShipEmp[id] = Time.time + secs;
                    break;
                case EffectType.Burning:
                    ClientBurning[id] = Time.time + secs;
                    break;
                case EffectType.Jammed:
                    _localJamUntil = Mathf.Max(_localJamUntil, Time.time + secs);
                    Sounds.Zapped(Camera.main != null ? Camera.main.transform.position : Vector3.zero);
                    break;
            }
        }

        /// <summary>Is this ship (or the rigid assembly it belongs to) knocked out right now?</summary>
        public static bool IsShipEmp(uint shipId)
        {
            if (ClientShipEmp.Count == 0) return false;
            uint root = Vehicles.ShipWeapons.RootOf(shipId);
            return ClientShipEmp.TryGetValue(root, out float until) && Time.time < until;
        }

        public static float ShipEmpLeft(uint rootId) => ClientShipEmp.TryGetValue(rootId, out float until) ? Mathf.Max(0f, until - Time.time) : 0f;

        public static bool LocalJammed => Time.time < _localJamUntil;
        public static float LocalJamLeft => Mathf.Max(0f, _localJamUntil - Time.time);

        public static bool IsBurning(uint playerId) => ClientBurning.TryGetValue(playerId, out float until) && Time.time < until;

        /// <summary>Client, every frame: sparks on EMP'd ships, flames on burning players.</summary>
        public static void ClientUpdate()
        {
            if (Time.time < _nextVisual) return;
            _nextVisual = Time.time + 0.12f;
            Purge(ClientShipEmp);
            Purge(ClientBurning);
            if (Svc.ShipsClient != null)
                foreach (KeyValuePair<uint, float> kv in ClientShipEmp)
                {
                    if (!Svc.ShipsClient.TryGetTrackedShip(kv.Key, out TrackedShipClient ship) || !ship.IsRigidbodyLoaded) continue;
                    PartsBuffer.Clear();
                    PartsBuffer.AddRange(ship.StatefulParts);
                    if (PartsBuffer.Count == 0) continue;
                    var part = PartsBuffer[Random.Range(0, PartsBuffer.Count)];
                    Vector3 p = ship.ShipRigidBody.transform.TransformPoint(part.LocalPosition + Random.insideUnitSphere * 0.4f);
                    Fx.ElectricArc(p, p + Random.onUnitSphere * Random.Range(0.4f, 1.2f), new Color(0.4f, 0.65f, 1f), 0.15f);
                    Fx.EmitSparks(p, Random.onUnitSphere, 4);
                }
            if (Svc.PlayersClient != null)
                foreach (TrackedPlayerOtherClient other in Svc.PlayersClient.OtherPlayers)
                {
                    if (!IsBurning(other.Id) || !other.IsVisualLoaded) continue;
                    Vector3 at = other.VisualPos + Fx.Up * Random.Range(0.4f, 1.6f) + Random.insideUnitSphere * 0.25f;
                    Fx.Flame(at, Fx.Up, 0.25f);
                }
        }

        public static bool LocalBurning
        {
            get
            {
                uint? me = Svc.PlayersClient?.LocalPlayer?.Id;
                return me.HasValue && IsBurning(me.Value);
            }
        }
    }

    internal enum EffectType : byte
    {
        ShipEmp = 1,
        Burning = 2,
        Jammed = 3,
    }
}
