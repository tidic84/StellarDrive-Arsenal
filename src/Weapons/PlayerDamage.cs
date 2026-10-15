using System.IO;
using System.Reflection;
using Arsenal.Core;
using Arsenal.Net;
using FishNet.Connection;
using HarmonyLib;
using Players;
using UnityEngine;
using WorldTracking.Interface.Services;
using WorldTracking.Interface.Values;

namespace Arsenal.Weapons
{
    /// <summary>
    /// Damage to players. Shooters report hits; the server applies them through the game's own damage path
    /// (so regeneration and death behave natively), tells the victim where it came from, confirms kills to
    /// the shooter and announces them in the kill feed.
    /// </summary>
    internal static class PlayerDamage
    {
        public static void Init()
        {
            ArsenalNet.OnServer(Op.PlayerHit, ServerOnHit);
        }

        /// <summary>Client: report a hit on another player.</summary>
        public static void Report(uint playerId, float damage, WeaponKind weapon, bool headshot = false, float burnSeconds = 0f, float empSeconds = 0f)
        {
#if DEV
            ArsenalMod.Log.Msg($"[DEV] player hit {playerId} {damage:0.0} {weapon}{(headshot ? " head" : "")}");
#endif
            uint space = ClientOrigin.LocalPhysicsSpaceId;
            Vector3 from = Camera.main != null ? Camera.main.transform.position : Vector3.zero;
            ArsenalNet.SendToServer(Op.PlayerHit, w =>
            {
                w.Write(playerId);
                w.Write(damage);
                w.Write((byte)weapon);
                w.Write(headshot);
                w.Write(burnSeconds);
                w.Write(empSeconds);
                w.Write(space);
                w.Write(from);
            });
            CombatHud.Hit(headshot);
        }

        private static void ServerOnHit(NetworkConnection sender, BinaryReader r)
        {
            uint id = r.ReadUInt32();
            float dmg = Mathf.Clamp(r.ReadSingle(), 0f, 500f);
            var weapon = (WeaponKind)r.ReadByte();
            bool headshot = r.ReadBoolean();
            float burn = Mathf.Clamp(r.ReadSingle(), 0f, 10f);
            float emp = Mathf.Clamp(r.ReadSingle(), 0f, 10f);
            uint space = r.ReadUInt32();
            Vector3 from = r.ReadVector3();
            ApplyServer(id, dmg, sender, weapon, space, from);
            if (burn > 0f) Effects.ServerBurn(id, burn, sender, weapon);
            if (emp > 0f) Effects.ServerJamPlayer(id, emp);
        }

        public static TrackedPlayerServer Find(uint playerId)
        {
            if (Svc.PlayersServer == null) return null;
            foreach (TrackedPlayerServer p in Svc.PlayersServer.ActivePlayers)
                if (p.Id == playerId) return p;
            return null;
        }

        // the game keeps these private: looked up once, then read directly (burns and flames hit several times a second)
        private static PropertyInfo _netState, _health, _name, _owner;
        private static FieldInfo _updater;
        private static MethodInfo _collisionDamage;

        private static T StateValue<T>(TrackedPlayerServer p, ref PropertyInfo prop, string name)
        {
            if (_netState == null) _netState = AccessTools.Property(typeof(TrackedPlayerServer), "NetworkState");
            object st = _netState?.GetValue(p);
            if (st == null) return default;
            if (prop == null) prop = AccessTools.Property(st.GetType(), name);
            return prop != null && prop.GetValue(st) is T v ? v : default;
        }

        public static float Health(TrackedPlayerServer p) => StateValue<float>(p, ref _health, "Health");

        public static string Name(TrackedPlayerServer p) => StateValue<string>(p, ref _name, "PlayerName") ?? "?";

        public static NetworkConnection Connection(TrackedPlayerServer p) => StateValue<NetworkConnection>(p, ref _owner, "Owner");

        /// <summary>Hurts the player through the game's own collision damage (regeneration and death stay native).</summary>
        private static bool CollisionDamage(TrackedPlayerServer p, float damage)
        {
            if (_updater == null) _updater = AccessTools.Field(typeof(TrackedPlayerServer), "_statusUpdater");
            object updater = _updater?.GetValue(p);
            if (updater == null) return false;
            if (_collisionDamage == null) _collisionDamage = AccessTools.Method(updater.GetType(), "OnCollisionDamage", new[] { typeof(float) });
            if (_collisionDamage == null) return false;
            _collisionDamage.Invoke(updater, new object[] { damage });
            return true;
        }

        public static TrackedPlayerServer FromConnection(NetworkConnection conn) =>
            conn == null ? null : Svc.PlayersServer?.GetTrackedPlayerFromConnectionId(conn.ClientId);

        /// <summary>
        /// Server: damages a player. <paramref name="attacker"/> may be null (unknown source);
        /// <paramref name="fromSpace"/>/<paramref name="from"/> locate the source for the victim's indicator.
        /// </summary>
        public static void ApplyServer(uint playerId, float damage, NetworkConnection attacker, WeaponKind weapon, uint fromSpace, Vector3 from, bool showSource = true)
        {
            TrackedPlayerServer p = Find(playerId);
            if (p == null || p.IsCheatFlying || damage <= 0f) return;
            float before = Health(p);
            if (before <= 0f) return;
            if (!CollisionDamage(p, damage)) return;
            float after = Health(p);

            NetworkConnection victim = Connection(p);
            if (victim != null)
            {
                // the victim converts the source position into its own space
                ArsenalNet.SendTo(victim, Op.Hurt, w =>
                {
                    w.Write(damage);
                    w.Write(showSource);
                    w.Write(fromSpace);
                    w.Write(from);
                });
            }

            if (after <= 0f)
            {
                TrackedPlayerServer killer = FromConnection(attacker);
                bool self = killer != null && killer.Id == playerId;
                if (attacker != null && !self)
                    ArsenalNet.SendTo(attacker, Op.KillConfirm, w => w.Write(Name(p)));
                string killerName = killer != null ? Name(killer) : "";
                string victimName = Name(p);
                ArsenalNet.SendToAll(Op.KillFeed, w =>
                {
                    w.Write(self ? "" : killerName);
                    w.Write(victimName);
                    w.Write((byte)weapon);
                });
            }
        }
    }
}
