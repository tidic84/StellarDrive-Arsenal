using System.IO;
using Arsenal.Net;
using FishNet.Connection;
using Ships.Interface.Model.Parts;
using UnityEngine;

namespace Arsenal.Vehicles
{
    /// <summary>
    /// Where the rotating turrets point. The client that shoots with a turret (its gunner, or the ship's owner when
    /// it fights on its own) sends the guns' target angles, in the mount's frame, up to ten times a second while
    /// they move and once a second otherwise; the host passes them on and the others turn their copy to match.
    /// </summary>
    internal static class TurretNet
    {
        public static void Init()
        {
            ArsenalNet.OnServer(Op.TurretAim, ServerRelay);
            ArsenalNet.OnClient(Op.TurretAims, ClientOnAim);
        }

        public static void Report(ShipWeapons.Mount m)
        {
            float since = Time.time - m.SentAt;
            if (since < 0.1f) return;
            bool moved = Mathf.Abs(Mathf.DeltaAngle(m.SentYaw, m.TargetYaw)) > 0.25f || Mathf.Abs(m.SentElevation - m.TargetElevation) > 0.25f;
            if (!moved && since < 1f) return;
            m.SentAt = Time.time;
            m.SentYaw = m.TargetYaw;
            m.SentElevation = m.TargetElevation;
            uint ship = m.Ctx.ShipId;
            ushort part = m.Ctx.PartId;
            float yaw = m.TargetYaw, elevation = m.TargetElevation;
            ArsenalNet.SendToServer(Op.TurretAim, w =>
            {
                w.Write(ship);
                w.Write(part);
                w.Write(yaw);
                w.Write(elevation);
            }, FishNet.Transporting.Channel.Unreliable);
        }

        private static void ServerRelay(NetworkConnection sender, BinaryReader r)
        {
            uint ship = r.ReadUInt32();
            ushort part = r.ReadUInt16();
            float yaw = r.ReadSingle(), elevation = r.ReadSingle();
            ArsenalNet.SendToAllExcept(sender, Op.TurretAims, w =>
            {
                w.Write(ship);
                w.Write(part);
                w.Write(yaw);
                w.Write(elevation);
            }, FishNet.Transporting.Channel.Unreliable);
        }

        private static void ClientOnAim(BinaryReader r)
        {
            var ctx = new PartContext(r.ReadUInt32(), r.ReadUInt16());
            float yaw = r.ReadSingle(), elevation = r.ReadSingle();
            if (!ShipWeapons.TryGetMount(ctx, out ShipWeapons.Mount m)) return;
            m.RemoteYaw = yaw;
            m.RemoteElevation = elevation;
            m.RemoteAt = Time.time;
        }
    }
}
