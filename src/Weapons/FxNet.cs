using System;
using System.IO;
using Arsenal.Core;
using Arsenal.Net;
using Arsenal.Vehicles;
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
        Flame = 4,
        Arc = 5,
        Bolt = 6,
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

        public static void RelayBeam(Vector3 from, Vector3 to, byte style)
        {
            uint space = ClientOrigin.LocalPhysicsSpaceId;
            ArsenalNet.SendToServer(Op.FxRelay, w =>
            {
                w.Write((byte)FxType.Beam);
                w.Write(space);
                w.Write(from);
                w.Write(to);
                w.Write(style);
            }, FishNet.Transporting.Channel.Unreliable);
        }

        /// <summary>A blaster bolt: the others fly their own copy of it (for the eyes only).</summary>
        public static void RelayBolt(Vector3 from, Vector3 velocity, uint shooterRoot, byte style)
        {
            uint space = ClientOrigin.LocalPhysicsSpaceId;
            ArsenalNet.SendToServer(Op.FxRelay, w =>
            {
                w.Write((byte)FxType.Bolt);
                w.Write(space);
                w.Write(from);
                w.Write(velocity);
                w.Write(shooterRoot);
                w.Write(style);
            }, FishNet.Transporting.Channel.Unreliable);
        }

        public static void RelayFlame(Vector3 from, Vector3 dir)
        {
            uint space = ClientOrigin.LocalPhysicsSpaceId;
            ArsenalNet.SendToServer(Op.FxRelay, w =>
            {
                w.Write((byte)FxType.Flame);
                w.Write(space);
                w.Write(from);
                w.Write(dir);
            }, FishNet.Transporting.Channel.Unreliable);
        }

        public static void RelayArc(System.Collections.Generic.List<Vector3> points)
        {
            uint space = ClientOrigin.LocalPhysicsSpaceId;
            ArsenalNet.SendToServer(Op.FxRelay, w =>
            {
                w.Write((byte)FxType.Arc);
                w.Write(space);
                w.Write((byte)Mathf.Min(points.Count, 8));
                for (int i = 0; i < points.Count && i < 8; i++) w.Write(points[i]);
            }, FishNet.Transporting.Channel.Unreliable);
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
                    WeaponFx.Shot(weapon, from, to);
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
                    byte style = r.ReadByte();
                    LaserFx.RemoteBeam(from, to, style);
                    break;
                }
                case FxType.Bolt:
                {
                    Vector3 from = ToLocal(space, r.ReadVector3());
                    Vector3 v = r.ReadVector3();
                    uint shooter = r.ReadUInt32();
                    byte style = r.ReadByte();
                    if (v.sqrMagnitude > 0.01f) Laser.Fire(from, ToLocalDir(space, v.normalized) * v.magnitude, shooter, false, style);
                    break;
                }
                case FxType.Flame:
                {
                    Vector3 from = ToLocal(space, r.ReadVector3());
                    Vector3 dir = ToLocalDir(space, r.ReadVector3());
                    for (int i = 0; i < 6; i++)
                        Fx.Flame(from + dir * UnityEngine.Random.Range(0.1f, 0.5f), (dir + UnityEngine.Random.insideUnitSphere * 0.15f) * UnityEngine.Random.Range(9f, 14f), UnityEngine.Random.Range(0.06f, 0.12f));
                    break;
                }
                case FxType.Arc:
                {
                    int n = r.ReadByte();
                    Vector3 prev = ToLocal(space, r.ReadVector3());
                    if (n > 0) Sounds.Shot(WeaponKind.Tesla, prev);
                    for (int i = 1; i < n; i++)
                    {
                        Vector3 p = ToLocal(space, r.ReadVector3());
                        Fx.ElectricArc(prev, p, new Color(0.55f, 0.8f, 1f), 0.18f, 0.04f);
                        prev = p;
                    }
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
