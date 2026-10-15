using System.Collections.Generic;
using System.IO;
using System.Linq;
using Arsenal.Core;
using Arsenal.Net;
using FishNet.Connection;
using Ships;
using Ships.Interface.Model.Joints;
using Ships.Interface.Model.Parts;
using Ships.Interface.Model.Placement;
using Ships.Interface.Model.State;
using Ships.Services;
using UnityEngine;
using WorldObjects.Interface.Model;

namespace Arsenal.Damage
{
    internal struct PanelState
    {
        public float Health;   // hit points left
        public HoleMask Holes;
    }

    /// <summary>
    /// Server-authoritative damage of hull panels. Clients report hits (they did the raycast), the server
    /// accumulates health loss and holes, breaks the panel through the game's own destruction when it gives
    /// up, and broadcasts the result. Every client keeps a mirror used to draw the holes.
    /// </summary>
    internal static class HullDamage
    {
        // server side
        private static readonly Dictionary<PanelKey, PanelState> Server = new Dictionary<PanelKey, PanelState>();
        // client side mirror (what is drawn)
        private static readonly Dictionary<PanelKey, HoleMask> Client = new Dictionary<PanelKey, HoleMask>();

        // server: panel states changed this frame, sent together at the end of it (a torpedo changes hundreds)
        private static readonly Dictionary<PanelKey, (bool broken, HoleMask holes)> Outgoing = new Dictionary<PanelKey, (bool, HoleMask)>();
        private static readonly List<KeyValuePair<PanelKey, (bool broken, HoleMask holes)>> OutgoingList = new List<KeyValuePair<PanelKey, (bool, HoleMask)>>();

        private static readonly System.Random Rng = new System.Random();

        public static void Init()
        {
            ArsenalNet.OnServer(Op.PanelHit, ServerOnPanelHit);
            ArsenalNet.OnClient(Op.PanelState, ClientOnPanelState);
            ArsenalNet.OnClient(Op.PanelStates, ClientOnPanelStates);
            ArsenalNet.OnClient(Op.PanelSnapshot, ClientOnSnapshot);
            ArsenalNet.OnClientHello += SendSnapshot;
        }

        public static void ResetClient() => Client.Clear();

        public static void ResetAll()
        {
            Server.Clear();
            Client.Clear();
            Outgoing.Clear();
        }

        /// <summary>The hole sets the panels show right now (the mesh cache keeps these).</summary>
        public static IEnumerable<HoleMask> ClientMasks => Client.Values;

        public static IReadOnlyDictionary<PanelKey, PanelState> ServerStates => Server;

        public static void LoadServerStates(IEnumerable<KeyValuePair<PanelKey, PanelState>> states)
        {
            Server.Clear();
            foreach (var kv in states) Server[kv.Key] = kv.Value;
            // the host's own client may already be connected: give everyone the loaded holes
            if (ArsenalNet.IsServer && Server.Count > 0)
                foreach (FishNet.Connection.NetworkConnection c in FishNet.InstanceFinder.ServerManager.Clients.Values)
                    SendSnapshot(c);
        }

        // ------------------------------------------------------------------ client API

        public static bool TryGetClientHoles(PanelKey key, out HoleMask mask) => Client.TryGetValue(key, out mask);

        /// <summary>Reports a hit to the server. Holes are only kept on flat panels.</summary>
        public static void ReportHit(PanelKey key, HoleMask newHoles, float damage)
        {
            ArsenalNet.SendToServer(Op.PanelHit, w =>
            {
                key.Write(w);
                newHoles.Write(w);
                w.Write(damage);
            });
        }

        /// <summary>Shows our own holes right away; the server's answer overwrites this mirror shortly after.</summary>
        public static void PredictHoles(PanelKey key, HoleMask add)
        {
            if (add.IsEmpty) return;
            Client.TryGetValue(key, out HoleMask cur);
            HoleMask next = cur.Union(add);
            if (next.Equals(cur)) return;
            Client[key] = next;
            HoleVisuals.Refresh(key);
        }

        /// <summary>A hull part was removed (recycled, destroyed, ship deleted): forget its damage on both sides.</summary>
        public static void Forget(uint shipId, HullPartPosition pos)
        {
            var key = new PanelKey(shipId, pos);
            Server.Remove(key);
            Client.Remove(key);
        }

        /// <summary>A whole ship is gone: drop all its damage (otherwise it stays in the save forever).</summary>
        public static void ForgetShip(uint shipId)
        {
            Server.Keys.Where(k => k.ShipId == shipId).ToList().ForEach(k => Server.Remove(k));
            Client.Keys.Where(k => k.ShipId == shipId).ToList().ForEach(k => Client.Remove(k));
            Outgoing.Keys.Where(k => k.ShipId == shipId).ToList().ForEach(k => Outgoing.Remove(k));
        }

        // ------------------------------------------------------------------ server

        private static void ServerOnPanelHit(NetworkConnection sender, BinaryReader r)
        {
            PanelKey key = PanelKey.Read(r);
            HoleMask add = HoleMask.Read(r);
            float damage = Mathf.Clamp(r.ReadSingle(), 0f, 100000f);
            ApplyServer(key, add, damage);
        }

        /// <summary>Applies damage on the server (also used directly by server-side explosions).</summary>
        public static void ApplyServer(PanelKey key, HoleMask add, float damage)
        {
            ShipsServerTracker ships = Svc.ShipsServer;
            if (ships == null || !ships.TryGetTrackedShip(key.ShipId, out TrackedShipServer ship)) return;
            HullPart? part = ship.GetVoxel(key.Pos.VoxelCoord)?.GetPartFromType(key.Pos.Type);
            if (!part.HasValue) return;
            HullPart p = part.Value;

            float max = Materials.MaxHealth(p.MaterialType);
            if (!Server.TryGetValue(key, out PanelState st))
                st = new PanelState { Health = max };
            if (CanHaveHoles(p))
                st.Holes = st.Holes.Union(add);
            st.Health -= damage;
#if DEV
            if (p.SegmentType != HullMeshSegmentType.Flat) ArsenalMod.Log.Msg($"[DEV] {p.SegmentType} {key.Pos.VoxelCoord} hp {st.Health:0}/{max:0} holes {SegmentShapes.Get(p).Coverage(st.Holes):P0}");
#endif

            bool broken = st.Health <= 0f || SegmentShapes.Get(p).Coverage(st.Holes) >= Materials.BreakHoleRatio;
            if (broken)
            {
                Server.Remove(key);
                BreakPanel(ship, p);
                Outgoing[key] = (true, default);
                return;
            }
            Server[key] = st;
            Outgoing[key] = (false, st.Holes);
        }

        /// <summary>Server, once a frame: sends the panels that changed in one message.</summary>
        public static void FlushServer()
        {
            if (Outgoing.Count == 0) return;
            OutgoingList.Clear();
            OutgoingList.AddRange(Outgoing);
            Outgoing.Clear();
            ArsenalNet.SendToAll(Op.PanelStates, w =>
            {
                w.Write(OutgoingList.Count);
                foreach (var kv in OutgoingList)
                {
                    kv.Key.Write(w);
                    w.Write(kv.Value.broken);
                    if (!kv.Value.broken) kv.Value.holes.Write(w);
                }
            });
        }

        /// <summary>Every hull piece drawn from the game's segment meshes (flat, corners, slopes) can be holed; doors and other custom meshes cannot.</summary>
        public static bool CanHaveHoles(HullPart p) => !p.HullSettings.hasItsOwnMesh;

        private static void BreakPanel(TrackedShipServer ship, HullPart part)
        {
            HullPartPlacement placement = part.HullPlacement;
            if (TryGetServerWorldPose(ship, out Vector3 shipPos, out Quaternion shipRot, out ShipPhysicsState rootPhysics))
            {
                Vector3 pos = shipPos + shipRot * placement.LocalPosition;
                Vector3 outward = shipRot * (placement.Rotation * Vector3.forward);
                var debrisState = new WorldObjectPhysicsState
                {
                    SpaceId = rootPhysics.SpaceId,
                    Position = pos,
                    Rotation = shipRot * placement.Rotation,
                    Velocity = rootPhysics.Velocity + (outward * (Rng.Next(2) == 0 ? 1f : -1f) + Random.insideUnitSphere) * 1.5f,
                    AngularVelocity = Random.insideUnitSphere * 4f,
                };
                Svc.Get<ShipDebrisGenerationServerService>()?.GenerateDebris(part, debrisState);
            }
            ship.DestroyHullPart(placement.Position);
        }

        /// <summary>Server pose of a ship in its root's space (child ships on rotors are placed relative to their root).</summary>
        public static bool TryGetServerWorldPose(TrackedShipServer ship, out Vector3 pos, out Quaternion rot, out ShipPhysicsState rootPhysics)
        {
            pos = default;
            rot = Quaternion.identity;
            rootPhysics = ship.PhysicsState;
            ShipsServerTracker ships = Svc.ShipsServer;
            if (!ships.TryGetTrackedRootShip(ship.Id, out TrackedShipServer root) || root == null)
                root = ship;
            rootPhysics = root.PhysicsState;
            pos = rootPhysics.Position;
            rot = rootPhysics.Rotation;
            if (root.Id == ship.Id) return true;
            foreach (RootRelativeShipData data in ships.GetRootConnectedShips(root.Id))
            {
                if (data.Ship.Id != ship.Id) continue;
                pos = rootPhysics.Position + rootPhysics.Rotation * data.PositionRelativeToRoot;
                rot = rootPhysics.Rotation * data.RotationRelativeToRoot;
                return true;
            }
            return true;
        }

        private static void SendSnapshot(NetworkConnection conn)
        {
            if (Server.Count == 0) return;
            var list = new List<KeyValuePair<PanelKey, PanelState>>(Server);
            ArsenalNet.SendTo(conn, Op.PanelSnapshot, w =>
            {
                w.Write(list.Count);
                foreach (var kv in list)
                {
                    kv.Key.Write(w);
                    kv.Value.Holes.Write(w);
                }
            });
        }

        // ------------------------------------------------------------------ client

        private static void ClientOnPanelState(BinaryReader r) => ReadPanelState(r);

        private static void ClientOnPanelStates(BinaryReader r)
        {
            int n = r.ReadInt32();
            for (int i = 0; i < n; i++) ReadPanelState(r);
        }

        private static void ReadPanelState(BinaryReader r)
        {
            PanelKey key = PanelKey.Read(r);
            bool broken = r.ReadBoolean();
            if (broken)
            {
                Client.Remove(key);
                return; // the game removes the part itself and rebuilds the mesh
            }
            HoleMask holes = HoleMask.Read(r);
            bool changed = !Client.TryGetValue(key, out HoleMask old) || !old.Equals(holes);
            Client[key] = holes;
            if (changed) HoleVisuals.Refresh(key);
        }

        private static void ClientOnSnapshot(BinaryReader r)
        {
            int n = r.ReadInt32();
            var keys = new List<PanelKey>(n);
            for (int i = 0; i < n; i++)
            {
                PanelKey key = PanelKey.Read(r);
                Client[key] = HoleMask.Read(r);
                keys.Add(key);
            }
            foreach (PanelKey key in keys) HoleVisuals.Refresh(key);
        }
    }
}
