using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Arsenal.Content;
using Arsenal.Core;
using Arsenal.Damage;
using Arsenal.Net;
using Arsenal.Visual;
using FishNet;
using FishNet.Connection;
using Items.Model;
using Planets.Interface.Model;
using Players;
using Players.Services;
using Ships;
using Ships.Grid.Model;
using Ships.Interface.Model.Parts;
using Ships.Interface.Model.Placement;
using Ships.Interface.Services;
using UnityEngine;
using WorldTracking.Interface.Model.Spaces;
using WorldTracking.Interface.Services;
using WorldTracking.Interface.Values;
using Object = UnityEngine.Object;

namespace Arsenal.Weapons
{
    internal sealed class ExplosiveDef
    {
        public WeaponKind Kind;
        public uint ItemId;
        public string NameFr, NameEn;
        public byte Stack;
        public float Radius;
        public float Power; // panel damage at the centre
        public float PlayerDamage;
        public float Mass;
    }

    /// <summary>Grenades and breaching charges. Explosions are resolved by the server so every player sees the same holes.</summary>
    internal static class Explosives
    {
        public static readonly ExplosiveDef Grenade = new ExplosiveDef
        {
            Kind = WeaponKind.Grenade, ItemId = WeaponDefs.GrenadeId, NameFr = "Grenade", NameEn = "Grenade",
            Stack = 10, Radius = 4f, Power = 520f, PlayerDamage = 110f, Mass = 0.5f,
        };

        public static readonly ExplosiveDef Charge = new ExplosiveDef
        {
            Kind = WeaponKind.Charge, ItemId = WeaponDefs.ChargeId, NameFr = "Charge de brèche", NameEn = "Breaching charge",
            Stack = 5, Radius = 3.2f, Power = 1400f, PlayerDamage = 140f, Mass = 1.5f,
        };

        public static ExplosiveDef ByItem(uint id) => id == Grenade.ItemId ? Grenade : id == Charge.ItemId ? Charge : null;

        // ------------------------------------------------------------------ items

        public static IEnumerable<ItemSettings> BuildItems(ItemSettings[] existing)
        {
            foreach (ExplosiveDef def in new[] { Grenade, Charge })
            {
                if (existing.Any(i => i.id == def.ItemId)) continue;
                Model model = def.Kind == WeaponKind.Grenade ? WeaponModels.Grenade() : WeaponModels.Charge();
                GameObject prefab = ArsenalContent.BuildToolPrefab("Arsenal." + def.Kind, model, out _);
                model.Root.transform.localPosition = new Vector3(-0.1f, 0.12f, -0.02f);
                model.Root.transform.localEulerAngles = new Vector3(0f, 20f, 0f);
                prefab.AddComponent<ExplosiveTool>().ItemId = def.ItemId;

                var tool = ScriptableObject.CreateInstance<ToolSettings>();
                tool.name = "Arsenal." + def.Kind + "Tool";
                tool.prefab = prefab;
                var item = ScriptableObject.CreateInstance<ItemSettings>();
                item.name = "Arsenal." + def.Kind;
                item.id = def.ItemId;
                item.itemName = Loc.T(def.NameFr, def.NameEn);
                item.mass = def.Mass;
                item.maxStackSize = def.Stack;
                item.toolSettings = tool;
                item.icon = IconRenderer.Render(model.Root, new Vector3(0, -120f, 0));
                yield return item;
            }
        }

        // ------------------------------------------------------------------ network

        public static void Init()
        {
            ArsenalNet.OnServer(Op.ExplosionRequest, ServerOnExplosionRequest);
            ArsenalNet.OnServer(Op.PlaceCharge, ServerOnPlaceCharge);
            ArsenalNet.OnServer(Op.DetonateCharges, ServerOnDetonate);
            ArsenalNet.OnServer(Op.ConsumeItem, ServerOnConsume);
            ArsenalNet.OnServer(Op.GrenadeThrown, (c, r) =>
            {
                byte[] all = ((MemoryStream)r.BaseStream).ToArray();
                ArsenalNet.SendToAllExcept(c, Op.GrenadeRemote, w => w.Write(all));
            });
            ArsenalNet.OnClient(Op.GrenadeRemote, ClientOnRemoteGrenade);
            ArsenalNet.OnClient(Op.ChargeState, ClientOnChargeState);
            ArsenalNet.OnClientHello += conn => SendAllCharges(conn);
        }

        public static void Reset()
        {
            ServerCharges.Clear();
            foreach (ChargeVisual v in ClientCharges.Values)
                if (v != null) Object.Destroy(v.gameObject);
            ClientCharges.Clear();
            _nextChargeId = 1;
        }

        /// <summary>Removes one item from the local player's inventory (the server owns inventories).</summary>
        public static void Consume(uint itemId)
        {
            ArsenalNet.SendToServer(Op.ConsumeItem, w => w.Write(itemId));
        }

        private static void ServerOnConsume(NetworkConnection conn, BinaryReader r)
        {
            uint id = r.ReadUInt32();
            TrackedPlayerServer p = Svc.PlayersServer?.GetTrackedPlayerFromConnectionId(conn.ClientId);
            if (p == null) return;
            var cost = new[] { new ItemDrop { ItemId = id, Quantity = 1 } };
            if (p.HasItemsInInventory(cost)) p.RemoveItemsFromInventory(cost);
        }

        public static void RequestExplosion(Vector3 pos, ExplosiveDef def)
        {
            uint space = ClientOrigin.LocalPhysicsSpaceId;
            DamageObjectsAround(pos, def.Radius);
            ArsenalNet.SendToServer(Op.ExplosionRequest, w =>
            {
                w.Write(space);
                w.Write(pos);
                w.Write((byte)def.Kind);
            });
        }

        private static void ServerOnExplosionRequest(NetworkConnection conn, BinaryReader r)
        {
            uint space = r.ReadUInt32();
            Vector3 pos = r.ReadVector3();
            var kind = (WeaponKind)r.ReadByte();
            ExplosiveDef def = kind == WeaponKind.Charge ? Charge : kind == WeaponKind.ShipMissile ? Arsenal.Vehicles.Missiles.Warhead : Grenade;
            ServerExplode(space, pos, def.Radius, def.Power, def.PlayerDamage);
        }

        /// <summary>Trees and rocks near a client-side blast (they are only reachable from the client physics scene).</summary>
        private static void DamageObjectsAround(Vector3 pos, float radius)
        {
            var seen = new HashSet<IMinableObject>();
            var hits = new Collider[64];
            int n = ClientScene.PhysicsScene.OverlapSphere(pos, radius, hits, Ballistics.Mask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                IMinableObject m = hits[i].GetComponentInParent<IMinableObject>();
                if (m == null || !seen.Add(m)) continue;
                try { m.ReduceHealth(m.MaxHealth * 0.6f); } catch { }
            }
        }

        // ------------------------------------------------------------------ server explosion

        /// <summary>
        /// Damages every hull panel within the radius of every loaded ship (holes scale with proximity), hurts
        /// players, and tells everyone to show the blast.
        /// </summary>
        public static void ServerExplode(uint space, Vector3 pos, float radius, float power, float playerDamage)
        {
            IWorldTransposeServer transpose = Svc.Get<IWorldTransposeServer>();
            var rng = new System.Random();
            var hits = new List<(PanelKey key, HoleMask holes, float damage)>();

            foreach (TrackedShipServer ship in Svc.ShipsServer.Ships.ToList())
            {
                if (!HullDamage.TryGetServerWorldPose(ship, out Vector3 shipPos, out Quaternion shipRot, out var rootPhysics)) continue;
                Vector3 p;
                try { p = space == rootPhysics.SpaceId ? pos : transpose.Position(space, rootPhysics.SpaceId, pos); }
                catch { continue; }
                Vector3 local = Quaternion.Inverse(shipRot) * (p - shipPos);
                CoordBounds b = ship.GetHullCoordBounds();
                if (local.x < b.LowerCoord.x - radius - 1 || local.y < b.LowerCoord.y - radius - 1 || local.z < b.LowerCoord.z - radius - 1 ||
                    local.x > b.UpperCoord.x + radius + 1 || local.y > b.UpperCoord.y + radius + 1 || local.z > b.UpperCoord.z + radius + 1)
                    continue;

                var done = new HashSet<HullPartPosition>();
                int r = Mathf.CeilToInt(radius) + 1;
                Vector3Int c0 = Vector3Int.RoundToInt(local);
                for (int x = -r; x <= r; x++)
                    for (int y = -r; y <= r; y++)
                        for (int z = -r; z <= r; z++)
                        {
                            ShipVoxel voxel = ship.GetVoxel(c0 + new Vector3Int(x, y, z));
                            if (voxel == null) continue;
                            Consider(voxel.Floor);
                            Consider(voxel.ForwardWall);
                            Consider(voxel.RightWall);
                            Consider(voxel.InteriorPart);
                        }

                void Consider(HullPart? maybe)
                {
                    if (!maybe.HasValue) return;
                    HullPart part = maybe.Value;
                    HullPartPlacement pl = part.HullPlacement;
                    if (!done.Add(pl.Position)) return;
                    Vector3 rel = Quaternion.Inverse(pl.Rotation) * (local - pl.LocalPosition);
                    // distance to the 1x1 panel rectangle
                    float dx = Mathf.Max(0f, Mathf.Abs(rel.x) - 0.5f), dy = Mathf.Max(0f, Mathf.Abs(rel.y) - 0.5f);
                    float d = Mathf.Sqrt(dx * dx + dy * dy + rel.z * rel.z);
                    if (d >= radius) return;
                    float f = 1f - d / radius;
                    var holes = new HoleMask();
                    if (HullDamage.CanHaveHoles(part) && f > 0.25f)
                    {
                        float material = part.MaterialType == HullMaterialType.Glass ? 2f : part.MaterialType == HullMaterialType.Wood ? 1.35f : 1f;
                        float hr = Mathf.Clamp(0.08f + 0.7f * Mathf.Pow(f, 1.5f) * Mathf.Sqrt(power / 1000f) * material, 0.04f, 0.9f);
                        holes.PunchDisc(Mathf.Clamp(rel.x, -0.45f, 0.45f), Mathf.Clamp(rel.y, -0.45f, 0.45f), hr, rng);
                    }
                    hits.Add((new PanelKey(ship.Id, pl.Position), holes, power * f * f));
                }
            }
            foreach (var h in hits) HullDamage.ApplyServer(h.key, h.holes, h.damage);

            if (Svc.PlayersServer != null)
            {
                float reach = radius * 1.5f;
                foreach (TrackedPlayerServer pl in Svc.PlayersServer.ActivePlayers.ToList())
                {
                    Vector3 pp;
                    try { pp = pl.SpaceId == space ? pl.Position : transpose.Position(pl.SpaceId, space, pl.Position); }
                    catch { continue; }
                    float d = Vector3.Distance(pp, pos);
                    if (d < reach)
                        PlayerDamage.ApplyServer(pl.Id, playerDamage * Mathf.Pow(1f - d / reach, 2f));
                }
            }
            FxNet.SendExplosionToAll(space, pos, radius);
        }

        // ------------------------------------------------------------------ grenades

        public static void Throw(Vector3 from, Vector3 velocity, bool owner)
        {
            var go = new GameObject("Arsenal.Grenade");
            go.transform.position = from;
            go.AddComponent<MeshFilter>().sharedMesh = GrenadeMeshShared;
            go.AddComponent<MeshRenderer>().sharedMaterial = Assets.ToolAtlas;
            var sim = go.AddComponent<GrenadeSim>();
            sim.Velocity = velocity;
            sim.Owner = owner;
            Vector3 up = (Svc.Get<PlayerUpProvider>()?.WorldPlayerUp ?? Vector3.up).normalized;
            sim.Gravity = ClientOrigin.LocalPhysicsSpace is PlanetSurfaceSpace ? -up * 9.81f : Vector3.zero;
#if DEV
            ArsenalMod.Log.Msg($"[DEV] grenade from {from} vel {velocity} gravity {sim.Gravity} raw up {Svc.Get<PlayerUpProvider>()?.WorldPlayerUp} space {ClientOrigin.LocalPhysicsSpace}");
#endif
            if (owner)
            {
                uint space = ClientOrigin.LocalPhysicsSpaceId;
                ArsenalNet.SendToServer(Op.GrenadeThrown, w =>
                {
                    w.Write(space);
                    w.Write(from);
                    w.Write(velocity);
                }, FishNet.Transporting.Channel.Unreliable);
            }
        }

        private static void ClientOnRemoteGrenade(BinaryReader r)
        {
            uint space = r.ReadUInt32();
            Vector3 from = FxNet.ToLocal(space, r.ReadVector3());
            Vector3 v = r.ReadVector3();
            Vector3 vel = FxNet.ToLocalDir(space, v.normalized) * v.magnitude;
            Throw(from, vel, false);
        }

        private static Mesh _grenadeMesh, _chargeMesh;
        public static Mesh GrenadeMeshShared => _grenadeMesh != null ? _grenadeMesh : (_grenadeMesh = WeaponModels.GrenadeMesh());
        public static Mesh ChargeMeshShared => _chargeMesh != null ? _chargeMesh : (_chargeMesh = WeaponModels.ChargeMesh());

        // ------------------------------------------------------------------ charges

        internal struct ChargeData
        {
            public uint Id;
            public int Owner;
            public uint ShipId;   // 0 = stuck to the world
            public uint Space;    // world charges: their space
            public Vector3 Pos;   // ship-local or space position
            public Vector3 Normal;
        }

        private static readonly Dictionary<uint, ChargeData> ServerCharges = new Dictionary<uint, ChargeData>();
        private static readonly Dictionary<uint, ChargeVisual> ClientCharges = new Dictionary<uint, ChargeVisual>();
        private static uint _nextChargeId = 1;

        public static int LocalClientId => InstanceFinder.ClientManager != null && InstanceFinder.ClientManager.Connection != null ? InstanceFinder.ClientManager.Connection.ClientId : -1;

        /// <summary>Sticks a charge where the player looks. Returns false if nothing is in reach.</summary>
        public static bool TryPlaceCharge(Vector3 camPos, Vector3 dir)
        {
            if (!ClientScene.PhysicsScene.Raycast(camPos, dir, out RaycastHit hit, 3.5f, Ballistics.Mask, QueryTriggerInteraction.Ignore))
                return false;
            uint shipId = 0;
            Vector3 pos = hit.point, normal = hit.normal;
            IShipPartHelper helper = hit.collider.GetComponentInParent<IShipPartHelper>();
            if (helper != null && Svc.ShipsClient.TryGetTrackedShip(helper.ShipId, out TrackedShipClient ship) && ship.IsRigidbodyLoaded)
            {
                Transform t = ship.ShipRigidBody.transform;
                shipId = helper.ShipId;
                pos = t.InverseTransformPoint(hit.point);
                normal = t.InverseTransformDirection(hit.normal);
            }
            uint space = ClientOrigin.LocalPhysicsSpaceId;
            ArsenalNet.SendToServer(Op.PlaceCharge, w =>
            {
                w.Write(shipId);
                w.Write(space);
                w.Write(pos);
                w.Write(normal);
            });
            Sounds.Beep(hit.point);
            return true;
        }

        public static void DetonateMine()
        {
            ArsenalNet.SendToServer(Op.DetonateCharges, w => { });
        }

        private static void ServerOnPlaceCharge(NetworkConnection conn, BinaryReader r)
        {
            var c = new ChargeData
            {
                Id = _nextChargeId++,
                Owner = conn.ClientId,
                ShipId = r.ReadUInt32(),
                Space = r.ReadUInt32(),
                Pos = r.ReadVector3(),
                Normal = r.ReadVector3(),
            };
            TrackedPlayerServer p = Svc.PlayersServer?.GetTrackedPlayerFromConnectionId(conn.ClientId);
            var cost = new[] { new ItemDrop { ItemId = Charge.ItemId, Quantity = 1 } };
            if (p == null || !p.HasItemsInInventory(cost)) return;
            p.RemoveItemsFromInventory(cost);
            ServerCharges[c.Id] = c;
            BroadcastCharge(c, false);
        }

        private static void ServerOnDetonate(NetworkConnection conn, BinaryReader r)
        {
            List<ChargeData> mine = ServerCharges.Values.Where(c => c.Owner == conn.ClientId).ToList();
            foreach (ChargeData c in mine)
            {
                ServerCharges.Remove(c.Id);
                BroadcastCharge(c, true);
                uint space = c.Space;
                Vector3 pos = c.Pos + c.Normal * 0.06f;
                if (c.ShipId != 0)
                {
                    if (!Svc.ShipsServer.TryGetTrackedShip(c.ShipId, out TrackedShipServer ship)) continue;
                    if (!HullDamage.TryGetServerWorldPose(ship, out Vector3 sp, out Quaternion sr, out var root)) continue;
                    pos = sp + sr * (c.Pos + c.Normal * 0.06f);
                    space = root.SpaceId;
                }
                ServerExplode(space, pos, Charge.Radius, Charge.Power, Charge.PlayerDamage);
            }
        }

        private static void BroadcastCharge(ChargeData c, bool removed)
        {
            ArsenalNet.SendToAll(Op.ChargeState, w => WriteCharge(w, c, removed));
        }

        private static void SendAllCharges(NetworkConnection conn)
        {
            foreach (ChargeData c in ServerCharges.Values.ToList())
                ArsenalNet.SendTo(conn, Op.ChargeState, w => WriteCharge(w, c, false));
        }

        private static void WriteCharge(BinaryWriter w, ChargeData c, bool removed)
        {
            w.Write(c.Id);
            w.Write(removed);
            w.Write(c.Owner);
            w.Write(c.ShipId);
            w.Write(c.Space);
            w.Write(c.Pos);
            w.Write(c.Normal);
        }

        private static void ClientOnChargeState(BinaryReader r)
        {
            uint id = r.ReadUInt32();
            bool removed = r.ReadBoolean();
            var c = new ChargeData { Id = id, Owner = r.ReadInt32(), ShipId = r.ReadUInt32(), Space = r.ReadUInt32(), Pos = r.ReadVector3(), Normal = r.ReadVector3() };
            if (removed)
            {
                if (ClientCharges.TryGetValue(id, out ChargeVisual v) && v != null) Object.Destroy(v.gameObject);
                ClientCharges.Remove(id);
                return;
            }
            var go = new GameObject("Arsenal.Charge " + id);
            go.AddComponent<MeshFilter>().sharedMesh = ChargeMeshShared;
            go.AddComponent<MeshRenderer>().sharedMaterial = Assets.ToolAtlas;
            var vis = go.AddComponent<ChargeVisual>();
            vis.Data = c;
            ClientCharges[id] = vis;
        }

        public static int MyChargeCount => ClientCharges.Values.Count(v => v != null && v.Data.Owner == LocalClientId);
    }

    /// <summary>A placed charge: follows its ship (or stays in its space) and blinks.</summary>
    internal sealed class ChargeVisual : MonoBehaviour
    {
        public Explosives.ChargeData Data;
        private Light _light;
        private float _t;

        private void Start()
        {
            var lgo = new GameObject("Blink");
            lgo.transform.SetParent(transform, false);
            lgo.transform.localPosition = new Vector3(-0.05f, 0.02f, 0.07f);
            _light = lgo.AddComponent<Light>();
            _light.type = LightType.Point;
            _light.color = new Color(1f, 0.15f, 0.1f);
            _light.range = 0.6f;
            _light.shadows = LightShadows.None;
            Place();
        }

        private void LateUpdate()
        {
            Place();
            _t += Time.deltaTime;
            _light.intensity = (_t % 1f) < 0.15f ? 2.5f : 0f;
        }

        private void Place()
        {
            Vector3 pos, normal;
            if (Data.ShipId != 0)
            {
                if (!Svc.ShipsClient.TryGetTrackedShip(Data.ShipId, out TrackedShipClient ship) || !ship.IsRigidbodyLoaded)
                {
                    transform.position = new Vector3(0, -100000f, 0);
                    return;
                }
                Transform t = ship.ShipRigidBody.transform;
                pos = t.TransformPoint(Data.Pos);
                normal = t.TransformDirection(Data.Normal);
            }
            else
            {
                pos = FxNet.ToLocal(Data.Space, Data.Pos);
                normal = FxNet.ToLocalDir(Data.Space, Data.Normal);
            }
            Vector3 up = Vector3.ProjectOnPlane(Fx.Up, normal);
            if (up.sqrMagnitude < 0.01f) up = Vector3.ProjectOnPlane(Vector3.forward, normal);
            transform.SetPositionAndRotation(pos, Quaternion.LookRotation(normal, up));
        }
    }

    /// <summary>A thrown grenade: simple ballistic flight with bounces against the client physics scene.</summary>
    internal sealed class GrenadeSim : MonoBehaviour
    {
        public Vector3 Velocity;
        public Vector3 Gravity;
        public bool Owner;
        private float _fuse = 3f;
        private Vector3 _spin;
        private const float Radius = 0.04f;

        private void Start()
        {
            _spin = UnityEngine.Random.insideUnitSphere * 720f;
        }

        private void Update()
        {
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            _fuse -= dt;
            if (_fuse <= 0f)
            {
                if (Owner) Explosives.RequestExplosion(transform.position, Explosives.Grenade);
                Destroy(gameObject);
                return;
            }
            Velocity += Gravity * dt;
            Vector3 step = Velocity * dt;
            float dist = step.magnitude;
            if (dist > 0.0001f && ClientScene.PhysicsScene.SphereCast(transform.position, Radius, step / dist, out RaycastHit hit, dist, Ballistics.Mask, QueryTriggerInteraction.Ignore))
            {
                transform.position += step / dist * Mathf.Max(0f, hit.distance - 0.005f);
                Vector3 vn = Vector3.Dot(Velocity, hit.normal) * hit.normal;
                Vector3 vt = Velocity - vn;
                if (vn.magnitude > 1.5f) Sounds.Bounce(transform.position);
                Velocity = vt * 0.55f - vn * 0.22f;
                _spin *= 0.6f;
                if (hit.rigidbody != null) Velocity += hit.rigidbody.GetPointVelocity(hit.point) * 0.8f;
            }
            else
            {
                transform.position += step;
            }
            transform.rotation = Quaternion.Euler(_spin * dt) * transform.rotation;
        }
    }
}
