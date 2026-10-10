using System;
using System.IO;
using Arsenal.Core;
using Arsenal.Net;
using Arsenal.Visual;
using FishNet.Connection;
using UnityEngine;
using WorldTracking.Interface.Values;

namespace Arsenal.Weapons
{
    internal enum FxType : byte
    {
        Shot = 1,
        Explosion = 2,
        Beam = 3,
    }

    /// <summary>Cosmetic effects shown to the other players. Positions travel in the sender's physics space.</summary>
    internal static class FxNet
    {
        public static void Init()
        {
            ArsenalNet.OnServer(Op.FxRelay, ServerRelay);
            ArsenalNet.OnClient(Op.Fx, ClientOnFx);
        }

        private static void ServerRelay(NetworkConnection sender, BinaryReader r)
        {
            byte[] rest = ((MemoryStream)r.BaseStream).ToArray();
            long offset = r.BaseStream.Position;
            // ToArray returns the whole underlying segment from its origin; our stream was created on (data, 1, len-1)
            ArsenalNet.SendToAllExcept(sender, Op.Fx, w => w.Write(rest, (int)offset, rest.Length - (int)offset), FishNet.Transporting.Channel.Unreliable);
        }

        /// <summary>Server-originated effects (explosions computed by the host) go to everyone.</summary>
        public static void SendExplosionToAll(uint space, Vector3 pos, float radius)
        {
            ArsenalNet.SendToAll(Op.Fx, w =>
            {
                w.Write((byte)FxType.Explosion);
                w.Write(space);
                w.Write(pos);
                w.Write(radius);
            });
        }

        public static void RelayExplosion(Vector3 pos, float radius)
        {
            uint space = ClientOrigin.LocalPhysicsSpaceId;
            ArsenalNet.SendToServer(Op.FxRelay, w =>
            {
                w.Write((byte)FxType.Explosion);
                w.Write(space);
                w.Write(pos);
                w.Write(radius);
            });
        }

        private static void ClientOnFx(BinaryReader r)
        {
            var type = (FxType)r.ReadByte();
            uint space = r.ReadUInt32();
            switch (type)
            {
                case FxType.Shot:
                {
                    var weapon = (WeaponKind)r.ReadByte();
                    Vector3 from = ToLocal(space, r.ReadVector3());
                    Vector3 to = ToLocal(space, r.ReadVector3());
                    int n = r.ReadByte();
                    Fx.BulletTracer(from, to, weapon == WeaponKind.Marksman ? 0.04f : 0.025f);
                    Fx.MuzzleFlash(from, (to - from).normalized, weapon == WeaponKind.Marksman ? 0.35f : 0.18f);
                    Sounds.Shot(weapon, from);
                    for (int i = 0; i < n; i++)
                    {
                        Vector3 p = ToLocal(space, r.ReadVector3());
                        Vector3 normal = ToLocalDir(space, r.ReadVector3());
                        var kind = (ImpactKind)r.ReadByte();
                        var tint = new Color(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
                        Fx.Impact(p, normal, kind, tint);
                    }
                    break;
                }
                case FxType.Explosion:
                {
                    Vector3 p = ToLocal(space, r.ReadVector3());
                    float radius = r.ReadSingle();
                    Fx.Explosion(p, radius);
                    Sounds.Explosion(p, radius);
                    break;
                }
                case FxType.Beam:
                {
                    Vector3 from = ToLocal(space, r.ReadVector3());
                    Vector3 to = ToLocal(space, r.ReadVector3());
                    LaserFx.RemoteBeam(from, to);
                    break;
                }
            }
        }

        public static Vector3 ToLocal(uint fromSpace, Vector3 p)
        {
            uint local = ClientOrigin.LocalPhysicsSpaceId;
            if (fromSpace == local) return p;
            try
            {
                return Svc.TransposeClient.Position(fromSpace, local, p);
            }
            catch (Exception)
            {
                return p;
            }
        }

        public static Vector3 ToLocalDir(uint fromSpace, Vector3 d)
        {
            uint local = ClientOrigin.LocalPhysicsSpaceId;
            if (fromSpace == local) return d;
            try
            {
                return Svc.TransposeClient.Rotation(fromSpace, local, Quaternion.LookRotation(d)) * Vector3.forward;
            }
            catch (Exception)
            {
                return d;
            }
        }
    }
}
