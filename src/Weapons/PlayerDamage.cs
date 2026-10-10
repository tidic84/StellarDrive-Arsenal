using System.IO;
using Arsenal.Core;
using Arsenal.Net;
using FishNet.Connection;
using HarmonyLib;
using Players;
using UnityEngine;

namespace Arsenal.Weapons
{
    /// <summary>Damage to players goes through the game's own damage path (collision damage), so regen and death behave natively.</summary>
    internal static class PlayerDamage
    {
        public static void Init()
        {
            ArsenalNet.OnServer(Op.PlayerHit, ServerOnHit);
        }

        public static void Report(uint playerId, float damage)
        {
            ArsenalNet.SendToServer(Op.PlayerHit, w =>
            {
                w.Write(playerId);
                w.Write(damage);
            });
        }

        private static void ServerOnHit(NetworkConnection sender, BinaryReader r)
        {
            uint id = r.ReadUInt32();
            float dmg = Mathf.Clamp(r.ReadSingle(), 0f, 500f);
            ApplyServer(id, dmg);
        }

        public static void ApplyServer(uint playerId, float damage)
        {
            if (Svc.PlayersServer == null) return;
            foreach (TrackedPlayerServer p in Svc.PlayersServer.ActivePlayers)
            {
                if (p.Id != playerId) continue;
                if (p.IsCheatFlying) return;
                object updater = Traverse.Create(p).Field("_statusUpdater").GetValue();
                if (updater != null)
                    Traverse.Create(updater).Method("OnCollisionDamage", damage).GetValue();
                return;
            }
        }
    }
}
