using System.Collections.Generic;
using System.IO;
using Arsenal.Core;
using Arsenal.Net;
using Arsenal.Visual;
using Arsenal.Weapons;
using FishNet.Connection;
using Ships;
using UnityEngine;
using WorldTracking.Interface.Values;

namespace Arsenal.Vehicles
{
    /// <summary>
    /// Air-to-air missiles. The launching client flies the missile and asks the server for the explosion;
    /// other players fly a copy from the same launch data so they see it too.
    /// </summary>
    internal static class Missiles
    {
        public static readonly ExplosiveDef Warhead = new ExplosiveDef
        {
            Kind = WeaponKind.ShipMissile, Radius = 4.5f, Power = 1300f, PlayerDamage = 130f,
        };

        private static readonly Dictionary<uint, MissileSim> Flying = new Dictionary<uint, MissileSim>();
        private static uint _nextId = 1;
        private static Mesh _mesh;

        public static Mesh Mesh => _mesh != null ? _mesh : (_mesh = PartModels.Missile());

        public static void Init()
        {
            ArsenalNet.OnServer(Op.LaunchMissile, (conn, r) =>
            {
                byte[] all = ((MemoryStream)r.BaseStream).ToArray();
                ArsenalNet.SendToAllExcept(conn, Op.MissileSpawn, w => w.Write(all));
            });
            ArsenalNet.OnServer(Op.MissileExplode, (conn, r) =>
            {
                byte[] all = ((MemoryStream)r.BaseStream).ToArray();
                ArsenalNet.SendToAllExcept(conn, Op.MissileGone, w => w.Write(all));
            });
            ArsenalNet.OnClient(Op.MissileSpawn, ClientOnSpawn);
            ArsenalNet.OnClient(Op.MissileGone, r =>
            {
                int owner = r.ReadInt32();
                uint id = r.ReadUInt32();
                uint key = Key(owner, id);
                if (Flying.TryGetValue(key, out MissileSim sim) && sim != null) Object.Destroy(sim.gameObject);
                Flying.Remove(key);
            });
        }

        public static void Reset()
        {
            foreach (MissileSim s in Flying.Values)
                if (s != null) Object.Destroy(s.gameObject);
            Flying.Clear();
        }

        private static uint Key(int owner, uint id) => (uint)(owner * 100000) + id;

        public static void Launch(Vector3 from, Vector3 velocity, uint target, uint shooterRoot, bool owner, uint remoteId, int remoteOwner = -1)
        {
            uint id = owner ? _nextId++ : remoteId;
            int ownerId = owner ? Explosives.LocalClientId : remoteOwner;
            var go = new GameObject("Arsenal.Missile");
            go.transform.SetPositionAndRotation(from, Quaternion.LookRotation(velocity.sqrMagnitude > 0.01f ? velocity : Vector3.forward));
            go.AddComponent<MeshFilter>().sharedMesh = Mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = Assets.ToolAtlas;
            var sim = go.AddComponent<MissileSim>();
            sim.Velocity = velocity;
            sim.Target = target;
            sim.ShooterRoot = shooterRoot;
            sim.Owner = owner;
            sim.Id = id;
            sim.OwnerClient = ownerId;
            Flying[Key(ownerId, id)] = sim;
            Sounds.MissileLaunch(from);
            Fx.MuzzleFlash(from, velocity.normalized, 0.6f);
            Fx.Puff(from, 0.35f, new Color(0.75f, 0.73f, 0.7f), 1.2f, 4);
            if (owner)
            {
                uint space = ClientOrigin.LocalPhysicsSpaceId;
                ArsenalNet.SendToServer(Op.LaunchMissile, w =>
                {
                    w.Write(ownerId);
                    w.Write(id);
                    w.Write(space);
                    w.Write(from);
                    w.Write(velocity);
                    w.Write(target);
                    w.Write(shooterRoot);
                });
            }
        }

        private static void ClientOnSpawn(BinaryReader r)
        {
            int owner = r.ReadInt32();
            uint id = r.ReadUInt32();
            uint space = r.ReadUInt32();
            Vector3 from = FxNet.ToLocal(space, r.ReadVector3());
            Vector3 v = r.ReadVector3();
            Vector3 vel = FxNet.ToLocalDir(space, v.normalized) * v.magnitude;
            uint target = r.ReadUInt32();
            uint shooter = r.ReadUInt32();
            Launch(from, vel, target, shooter, false, id, owner);
        }

        public static void Detonated(MissileSim sim)
        {
            Flying.Remove(Key(sim.OwnerClient, sim.Id));
            if (!sim.Owner) return;
            Explosives.RequestExplosion(sim.transform.position, Warhead);
            ArsenalNet.SendToServer(Op.MissileExplode, w =>
            {
                w.Write(sim.OwnerClient);
                w.Write(sim.Id);
            });
        }
    }

    internal sealed class MissileSim : MonoBehaviour
    {
        public Vector3 Velocity;
        public uint Target;
        public uint ShooterRoot;
        public bool Owner;
        public uint Id;
        public int OwnerClient;

        private float _age;
        private float _nextPuff;
        private Light _flame;
        private const float Thrust = 90f;
        private const float TurnRate = 75f;
        private const float Life = 14f;
        private const float Fuse = 3f;

        private void Start()
        {
            var lg = new GameObject("Flame");
            lg.transform.SetParent(transform, false);
            lg.transform.localPosition = new Vector3(0, 0, -0.5f);
            _flame = lg.AddComponent<Light>();
            _flame.type = LightType.Point;
            _flame.color = new Color(1f, 0.6f, 0.3f);
            _flame.range = 5f;
            _flame.intensity = 4f;
            _flame.shadows = LightShadows.None;
        }

        private void Update()
        {
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            _age += dt;
            if (_age > Life)
            {
                Boom();
                return;
            }

            // guidance: lead pursuit towards the target's centre of mass
            Vector3 dir = Velocity.sqrMagnitude > 0.01f ? Velocity.normalized : transform.forward;
            TrackedShipClient target = null;
            if (Target != 0 && Svc.ShipsClient.TryGetTrackedShip(Target, out target) && target.IsRigidbodyLoaded && _age > 0.25f)
            {
                Rigidbody rb = target.ShipRigidBody.GetComponent<Rigidbody>();
                Vector3 tp = ShipWeapons.TargetPoint(target);
                Vector3 tv = rb != null ? rb.velocity : Vector3.zero;
                float speed = Mathf.Max(20f, Velocity.magnitude);
                Vector3 lead = tp + tv * Mathf.Clamp((tp - transform.position).magnitude / speed, 0f, 3f);
                Vector3 want = (lead - transform.position).normalized;
                dir = Vector3.RotateTowards(dir, want, TurnRate * Mathf.Deg2Rad * dt, 0f);
            }
            float accel = _age > 0.2f ? Thrust : 10f;
            float maxSpeed = 170f + (target != null && target.ShipRigidBody.GetComponent<Rigidbody>() is Rigidbody trb ? trb.velocity.magnitude : 0f);
            float newSpeed = Mathf.Min(maxSpeed, Velocity.magnitude + accel * dt);
            Velocity = dir * newSpeed;

            Vector3 step = Velocity * dt;
            float dist = step.magnitude;
            if (dist > 0.0001f && ClientScene.PhysicsScene.SphereCast(transform.position, 0.12f, step / dist, out RaycastHit hit, dist, Ballistics.Mask, QueryTriggerInteraction.Ignore))
            {
                bool own = _age < 0.6f && hit.rigidbody != null && IsShooter(hit.rigidbody);
                if (!own)
                {
                    transform.position = hit.point - step / dist * 0.1f;
                    Boom();
                    return;
                }
            }
            transform.position += step;
            transform.rotation = Quaternion.LookRotation(dir) * Quaternion.Euler(0, 0, _age * 360f);

            // proximity fuse
            if (target != null && _age > 0.8f && (ShipWeapons.TargetPoint(target) - transform.position).sqrMagnitude < Fuse * Fuse)
            {
                Boom();
                return;
            }

            if (_age >= _nextPuff)
            {
                _nextPuff = _age + 0.035f;
                Fx.Puff(transform.position - dir * 0.5f, 0.12f + Mathf.Min(0.2f, _age * 0.05f), new Color(0.8f, 0.78f, 0.75f), 1.6f, 1, -dir * 2f);
            }
            if (_flame != null) _flame.intensity = 3f + Random.value * 2f;
        }

        private bool IsShooter(Rigidbody rb)
        {
            foreach (TrackedShipClient s in Svc.ShipsClient.Ships)
                if (s.IsRigidbodyLoaded && ShipWeapons.RootOf(s.Id) == ShooterRoot && s.ShipRigidBody == rb.gameObject)
                    return true;
            return false;
        }

        private void Boom()
        {
            Missiles.Detonated(this);
            Destroy(gameObject);
        }
    }
}
