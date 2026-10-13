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
            MissileTrail.Attach(go, 1f, 5f, 2.5f);
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

        public static bool TryGet(int owner, uint id, out MissileSim sim) => Flying.TryGetValue(Key(owner, id), out sim) && sim != null;

        public static void InterceptLocal(int owner, uint id)
        {
            if (Flying.TryGetValue(Key(owner, id), out MissileSim sim) && sim != null) sim.Boom();
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

    internal sealed class MissileSim : MonoBehaviour, IThreat
    {
        public Vector3 Velocity;
        public uint Target;
        public uint ShooterRoot;
        public bool Owner;
        public uint Id;
        public int OwnerClient;

        private float _age;
        private const float Thrust = 90f;
        private const float TurnRate = 75f;
        private const float Life = 14f;
        private const float Fuse = 3f;
        private const float DecoyChance = 0.35f;

        public FlareSim Decoy;
        private readonly HashSet<uint> _seenFlares = new HashSet<uint>();

        public void SetDecoy(FlareSim flare)
        {
            Target = 0;
            Decoy = flare;
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

            // guidance: lead pursuit towards the target's centre of mass, unless a flare pulled the seeker off
            Vector3 dir = Velocity.sqrMagnitude > 0.01f ? Velocity.normalized : transform.forward;
            if (Decoy != null && Decoy.Burning)
            {
                Vector3 to = Decoy.transform.position - transform.position;
                if (to.sqrMagnitude < Fuse * Fuse * 0.5f)
                {
                    Boom();
                    return;
                }
                dir = Vector3.RotateTowards(dir, to.normalized, TurnRate * Mathf.Deg2Rad * dt, 0f);
            }
            else if (Decoy != null) Decoy = null;
            if (Owner && Target != 0 && Decoy == null && _age > 0.25f)
            {
                FlareSim flare = Flares.Seduce(transform.position, dir, DecoyChance, _seenFlares);
                if (flare != null)
                {
                    SetDecoy(flare);
                    Flares.ReportDecoy(true, OwnerClient, Id, flare);
                }
            }
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
            if (dist > 0.0001f && Ballistics.Raycast(transform.position, step / dist, dist, out RaycastHit hit, out uint hitPlayer, 0.12f))
            {
                if (Ballistics.HitShield)
                {
                    // a force field: the missile goes off on its shell
                    transform.position = hit.point - step / dist * 0.1f;
                    Compat.ForceFields.Effect(hit.point, hit.normal, Compat.ForceFields.Glow(Ballistics.Shield), 1.6f);
                    Boom();
                    return;
                }
                if (hitPlayer != 0)
                {
                    transform.position = hit.point;
                    Boom();
                    return;
                }
                bool own = _age < 0.6f && ShipWeapons.IsOwnShip(hit, ShooterRoot);
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
        }

        Vector3 IThreat.Position => transform.position;
        Vector3 IThreat.Velocity => Velocity;
        uint IThreat.ShooterRoot => ShooterRoot;
        uint IThreat.HomingTarget => Target;

        private void OnEnable() => Projectiles.Threats.Add(this);
        private void OnDisable() => Projectiles.Threats.Remove(this);

        public void ShootDown()
        {
            if (Owner) Boom();
            else Projectiles.RequestIntercept(true, OwnerClient, Id);
        }

        private bool _boomed;

        public void Boom()
        {
            if (_boomed) return;
            _boomed = true;
            Missiles.Detonated(this);
            Destroy(gameObject);
        }
    }
}
