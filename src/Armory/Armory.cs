using System.Collections.Generic;
using System.IO;
using System.Linq;
using Arsenal.Content;
using Arsenal.Core;
using Arsenal.Net;
using Arsenal.Weapons;
using FishNet.Connection;
using Items.Model;
using Players;
using Players.Interface.Model;
using Ships;
using Ships.Interface.Model.Parts;
using UnityEngine;

namespace Arsenal.Armory
{
    /// <summary>One line of the armory's catalogue.</summary>
    internal sealed class ArmoryEntry
    {
        public uint ItemId;
        public byte Quantity = 1;
        public string Name, Description;
        public ArmoryCategory Category;
        public Rarity Rarity;
        public (uint item, byte qty)[] Cost;
        public (string label, float value)[] Stats; // 0..1 bars
        public WeaponKind Kind;
        public string Pierce;                       // what it goes through, null when not a piercing round
    }

    /// <summary>
    /// The armory turns materials into weapons. The catalogue is the same on every machine; the server checks
    /// the player is really at an armory and pays with their inventory.
    /// </summary>
    internal static class Armory
    {
        private const uint Iron = 101, Copper = 112, Fuel = 100;

        public static List<ArmoryEntry> Catalogue()
        {
            var list = new List<ArmoryEntry>();
            foreach (HandWeaponDef d in WeaponDefs.Guns)
                list.Add(new ArmoryEntry
                {
                    ItemId = d.ItemId, Name = d.Name, Description = d.Description, Category = d.Category, Rarity = d.Rarity, Cost = d.Cost, Kind = d.Kind,
                    Stats = Stats(d),
                    Pierce = PierceText(d),
                });
            list.Add(new ArmoryEntry
            {
                ItemId = WeaponDefs.GrenadeId, Quantity = 3, Name = Loc.T("Grenades ×3", "Grenades ×3"), Category = ArmoryCategory.Explosives, Rarity = Rarity.Common, Kind = WeaponKind.Grenade,
                Description = Loc.T("Grenades à main à retardement (3 s). Clic gauche pour lancer.", "Timed hand grenades (3 s). Left click to throw."),
                Cost = new[] { (Iron, (byte)3), (Copper, (byte)1) },
                Stats = new[] { (Loc.T("Dégâts", "Damage"), 0.45f), (Loc.T("Rayon", "Radius"), 0.45f), (Loc.T("Portée", "Range"), 0.2f), (Loc.T("Cadence", "Rate"), 0.3f) },
            });
            list.Add(new ArmoryEntry
            {
                ItemId = WeaponDefs.ChargeId, Quantity = 2, Name = Loc.T("Charges de brèche ×2", "Breaching charges ×2"), Category = ArmoryCategory.Explosives, Rarity = Rarity.Uncommon, Kind = WeaponKind.Charge,
                Description = Loc.T("Se colle aux murs et aux sols. Clic droit pour faire sauter toutes vos charges.", "Sticks to walls and floors. Right click blows all your charges."),
                Cost = new[] { (Iron, (byte)4), (Copper, (byte)2), (Fuel, (byte)1) },
                Stats = new[] { (Loc.T("Dégâts", "Damage"), 0.95f), (Loc.T("Rayon", "Radius"), 0.4f), (Loc.T("Portée", "Range"), 0.05f), (Loc.T("Cadence", "Rate"), 0.15f) },
            });
            return list;
        }

        /// <summary>"Traverse : verre 4 · bois 1 · fer 0", head-on panels of each hull material.</summary>
        private static string PierceText(HandWeaponDef d)
        {
            if (d.Mode == FireMode.Projectile) return Loc.T("Souffle : ouvre toutes les coques", "Blast: tears any hull open");
            if (d.Mode == FireMode.Beam) return Loc.T("Rayon : fait fondre toutes les coques", "Beam: melts through any hull");
            if (d.Mode == FireMode.Bolt) return Loc.T("Trait blaster : fait fondre un trou dans toutes les coques, abîme les machines", "Blaster bolt: melts a hole in any hull, damages machinery");
            float p = d.Bullet != null ? d.Bullet.Penetration : 0f;
            if (p <= 0f) return Loc.T("Ne traverse pas les coques", "Does not go through hulls");
            string Count(Ships.Interface.Model.Parts.HullMaterialType m) => Damage.Materials.PanelsThrough(p, m).ToString();
            return Loc.T("Traverse", "Goes through") + " :  " + Loc.T("verre", "glass") + " " + Count(Ships.Interface.Model.Parts.HullMaterialType.Glass) +
                   "  ·  " + Loc.T("bois", "wood") + " " + Count(Ships.Interface.Model.Parts.HullMaterialType.Wood) +
                   "  ·  " + Loc.T("fer", "iron") + " " + Count(Ships.Interface.Model.Parts.HullMaterialType.Metal);
        }

        private static (string, float)[] Stats(HandWeaponDef d)
        {
            float dmg, rate, pen, range;
            if (d.Mode == FireMode.Projectile)
            {
                ProjectileSpec s = Projectiles.Specs[d.Projectile];
                dmg = s.BlastPower / 1500f;
                pen = s.BlastRadius / 6f;
                range = 0.4f;
            }
            else
            {
                float perShot = d.Bullet.PlayerDamage * d.Pellets;
                if (d.Mode == FireMode.Beam) perShot = d.Bullet.PlayerDamage * 4f * 2.5f;
                if (d.Mode == FireMode.Flame) perShot = d.Bullet.PlayerDamage * 4f + 9f;
                dmg = perShot / 110f;
                pen = d.Mode == FireMode.Beam ? 0.7f : d.Mode == FireMode.Bolt ? 0.35f + d.Bullet.HoleRadius * 6f : d.Mode == FireMode.Flame ? 0.1f : Mathf.Log(1f + d.Bullet.Penetration) / Mathf.Log(321f);
                range = (d.Mode == FireMode.Bullet || d.Mode == FireMode.Charge || d.Mode == FireMode.Bolt ? d.Bullet.Range : d.Reach) / 1200f;
            }
            rate = d.Mode == FireMode.Beam || d.Mode == FireMode.Flame ? 1f : d.RoundsPerMinute / 1300f;
            return new[]
            {
                (Loc.T("Dégâts", "Damage"), Mathf.Clamp01(dmg)),
                (Loc.T("Cadence", "Rate"), Mathf.Clamp01(rate)),
                (d.Mode == FireMode.Projectile ? Loc.T("Rayon", "Radius") : Loc.T("Pénétration", "Penetration"), Mathf.Clamp01(pen)),
                (Loc.T("Portée", "Range"), Mathf.Clamp(range, 0.03f, 1f)),
            };
        }

        public static void Init()
        {
            ArsenalNet.OnServer(Op.Fabricate, ServerOnFabricate);
            ArsenalNet.OnClient(Op.FabricateResult, r =>
            {
                bool ok = r.ReadBoolean();
                uint item = r.ReadUInt32();
                string msg = r.ReadString();
                ArmoryUi.OnResult(ok, item, msg);
            });
        }

        public static void RequestFabricate(uint itemId)
        {
            ArsenalNet.SendToServer(Op.Fabricate, w => w.Write(itemId));
        }

        private static void ServerOnFabricate(NetworkConnection conn, BinaryReader r)
        {
            uint itemId = r.ReadUInt32();
            TrackedPlayerServer p = Svc.PlayersServer?.GetTrackedPlayerFromConnectionId(conn.ClientId);
            ArmoryEntry e = Catalogue().FirstOrDefault(x => x.ItemId == itemId);
            if (p == null || e == null) return;
            if (!AtArmory(p))
            {
                Reply(conn, false, itemId, Loc.T("Vous devez être à une armurerie.", "You must be at an armory."));
                return;
            }
            ItemDrop[] cost = e.Cost.Select(c => new ItemDrop { ItemId = c.item, Quantity = c.qty }).ToArray();
            if (!p.IsUsingCheatFreeBuild)
            {
                if (!p.HasItemsInInventory(cost))
                {
                    Reply(conn, false, itemId, Loc.T("Matériaux insuffisants.", "Not enough materials."));
                    return;
                }
                p.RemoveItemsFromInventory(cost);
            }
            p.AddItemToInventory(new ItemDrop { ItemId = itemId, Quantity = e.Quantity });
            Reply(conn, true, itemId, "");
        }

        private static bool AtArmory(TrackedPlayerServer p)
        {
            PlayerState st = p.State;
            if (st.Type != PlayerStateType.InteractingWithChest) return false;
            return Svc.ShipsServer.TryGetTrackedShip(st.EntityId, out TrackedShipServer ship) &&
                   ship.TryGetStatefulPart(st.PartId, out StatefulPart part) && part.Settings != null && part.Settings.id == ShipContent.ArmoryId;
        }

        private static void Reply(NetworkConnection conn, bool ok, uint item, string msg) =>
            ArsenalNet.SendTo(conn, Op.FabricateResult, w =>
            {
                w.Write(ok);
                w.Write(item);
                w.Write(msg ?? "");
            });
    }
}
