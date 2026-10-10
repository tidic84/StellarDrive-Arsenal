using System.Collections.Generic;
using System.IO;
using Arsenal.Core;
using Arsenal.Damage;
using Arsenal.Net;
using Arsenal.Vehicles;
using Arsenal.Visual;
using Ships;
using Ships.Interface.Model.Parts;
using Ships.Interface.Services;
using UnityEngine;
using WorldTracking.Interface.Values;

namespace Arsenal.Weapons
{
    internal enum ProjectileKind : byte
    {
        None = 0,
        Grenade = 1,  // "Havoc" launcher: lobbed, blows up on impact
        Rocket = 2,   // "Titan": straight, accelerating
        Plasma = 3,   // ship plasma cannon: slow glowing ball
        Ion = 4,      // ship ion cannon: knocks ships out
        Torpedo = 5,  // "Leviathan": slow homing heavy warhead
    }

    internal sealed class ProjectileSpec
    {
        public ProjectileKind Kind;
        public WeaponKind Blast;     // explosion entry the server uses
        public float Speed, MaxSpeed, Accel, Gravity, TurnRate, Life, Radius;
        public float BlastRadius, BlastPower, BlastPlayerDamage;
        public float EmpSeconds, DirectPanelDamage, DirectPlayerDamage;
        public bool Homing;
        public float TrailEvery;
        public Color TrailColor = new Color(0.8f, 0.78f, 0.75f);
        public float TrailSize = 0.12f;
    }

    /// <summary>Anything a point-defence turret can shoot down.</summary>
    internal interface IThreat
    {
        Vector3 Position { get; }
        Vector3 Velocity { get; }
        uint ShooterRoot { get; }
        void ShootDown();
    }

    /// <summary>
    /// Flying projectiles. Like missiles: the client that fires flies it and asks the server for the
    /// blast; the others fly a copy from the launch data. Point defence can shoot them down.
    /// </summary>
    internal static class Projectiles
    {
        public static readonly Dictionary<ProjectileKind, ProjectileSpec> Specs = new Dictionary<ProjectileKind, ProjectileSpec>
        {
            [ProjectileKind.Grenade] = new ProjectileSpec
            {
                Kind = ProjectileKind.Grenade, Blast = WeaponKind.GrenadeLauncher, Speed = 30f, MaxSpeed = 30f, Gravity = 1f, Life = 8f, Radius = 0.05f,
                BlastRadius = 3.5f, BlastPower = 650f, BlastPlayerDamage = 95f, TrailEvery = 0.05f, TrailSize = 0.06f,
            },
            [ProjectileKind.Rocket] = new ProjectileSpec
            {
                Kind = ProjectileKind.Rocket, Blast = WeaponKind.RocketLauncher, Speed = 40f, MaxSpeed = 95f, Accel = 90f, Life = 8f, Radius = 0.08f,
                BlastRadius = 4.5f, BlastPower = 1500f, BlastPlayerDamage = 130f, TrailEvery = 0.025f, TrailSize = 0.14f,
            },
            [ProjectileKind.Plasma] = new ProjectileSpec
            {
                Kind = ProjectileKind.Plasma, Blast = WeaponKind.PlasmaCannon, Speed = 110f, MaxSpeed = 110f, Life = 6f, Radius = 0.25f,
                BlastRadius = 2.8f, BlastPower = 1100f, BlastPlayerDamage = 90f, TrailEvery = 0.03f, TrailColor = new Color(1f, 0.55f, 0.2f), TrailSize = 0.18f,
            },
            [ProjectileKind.Ion] = new ProjectileSpec
            {
                Kind = ProjectileKind.Ion, Blast = WeaponKind.IonCannon, Speed = 170f, MaxSpeed = 170f, Life = 5f, Radius = 0.18f,
                EmpSeconds = 8f, DirectPanelDamage = 25f, DirectPlayerDamage = 12f, TrailEvery = 0.03f, TrailColor = new Color(0.4f, 0.65f, 1f), TrailSize = 0.1f,
            },
            [ProjectileKind.Torpedo] = new ProjectileSpec
            {
                Kind = ProjectileKind.Torpedo, Blast = WeaponKind.Torpedo, Speed = 25f, MaxSpeed = 60f, Accel = 12f, TurnRate = 22f, Life = 35f, Radius = 0.35f,
                BlastRadius = 9f, BlastPower = 4200f, BlastPlayerDamage = 170f, Homing = true, TrailEvery = 0.035f, TrailSize = 0.15f,
            },
        };

        public static readonly List<IThreat> Threats = new List<IThreat>();
        private static readonly Dictionary<uint, ProjectileSim> Flying = new Dictionary<uint, ProjectileSim>();
        private static uint _nextId = 1;

        public static void Init()
        {
            ArsenalNet.OnServer(Op.LaunchProjectile, (conn, r) =>
            {
                byte[] all = ((MemoryStream)r.BaseStream).ToArray();
                ArsenalNet.SendToAllExcept(conn, Op.ProjectileSpawn, w => w.Write(all));
            });
            ArsenalNet.OnServer(Op.ProjectileDone, (conn, r) =>
            {
                byte[] all = ((MemoryStream)r.BaseStream).ToArray();
                ArsenalNet.SendToAllExcept(conn, Op.ProjectileGone, w => w.Write(all));
            });
            ArsenalNet.OnServer(Op.InterceptRequest, (conn, r) =>
            {
                byte[] all = ((MemoryStream)r.BaseStream).ToArray();
                ArsenalNet.SendToAll(Op.Intercept, w => w.Write(all));
            });
            ArsenalNet.OnClient(Op.ProjectileSpawn, ClientOnSpawn);
            ArsenalNet.OnClient(Op.ProjectileGone, r =>
            {
                uint key = Key(r.ReadInt32(), r.ReadUInt32());
                if (Flying.TryGetValue(key, out ProjectileSim s) && s != null) Object.Destroy(s.gameObject);
                Flying.Remove(key);
            });
            ArsenalNet.OnClient(Op.Intercept, ClientOnIntercept);
        }

        public static void Reset()
        {
            foreach (ProjectileSim s in Flying.Values)
                if (s != null) Object.Destroy(s.gameObject);
            Flying.Clear();
            Threats.Clear();
        }

        public static uint Key(int owner, uint id) => (uint)(owner * 100000) + id;

        public static void Launch(ProjectileKind kind, Vector3 from, Vector3 velocity, uint target, uint shooterRoot)
        {
            uint id = _nextId++;
            int owner = Explosives.LocalClientId;
            Spawn(kind, from, velocity, target, shooterRoot, true, owner, id);
            uint space = ClientOrigin.LocalPhysicsSpaceId;
            ArsenalNet.SendToServer(Op.LaunchProjectile, w =>
            {
                w.Write((byte)kind);
                w.Write(owner);
                w.Write(id);
                w.Write(space);
                w.Write(from);
                w.Write(velocity);
                w.Write(target);
                w.Write(shooterRoot);
            });
        }

        private static void ClientOnSpawn(BinaryReader r)
        {
            var kind = (ProjectileKind)r.ReadByte();
            int owner = r.ReadInt32();
            uint id = r.ReadUInt32();
            uint space = r.ReadUInt32();
            Vector3 from = FxNet.ToLocal(space, r.ReadVector3());
            Vector3 v = r.ReadVector3();
            Vector3 vel = FxNet.ToLocalDir(space, v.normalized) * v.magnitude;
            uint target = r.ReadUInt32();
            uint shooter = r.ReadUInt32();
            Spawn(kind, from, vel, target, shooter, false, owner, id);
        }

        private static void Spawn(ProjectileKind kind, Vector3 from, Vector3 vel, uint target, uint shooterRoot, bool owner, int ownerClient, uint id)
        {
            if (!Specs.TryGetValue(kind, out ProjectileSpec spec)) return;
            GameObject go = Visual(kind);
            go.transform.SetPositionAndRotation(from, Quaternion.LookRotation(vel.sqrMagnitude > 0.01f ? vel : Vector3.forward));
            var sim = go.AddComponent<ProjectileSim>();
            sim.Spec = spec;
            sim.Velocity = vel;
            sim.Target = target;
            sim.ShooterRootId = shooterRoot;
            sim.Owner = owner;
            sim.OwnerClient = ownerClient;
            sim.Id = id;
            Vector3 up = Fx.Up;
            sim.GravityVector = ClientOrigin.LocalPhysicsSpace is WorldTracking.Interface.Model.Spaces.PlanetSurfaceSpace ? -up * 9.81f * spec.Gravity : Vector3.zero;
            Flying[Key(ownerClient, id)] = sim;
            if (!owner)
            {
                // the shooter plays its own launch sound; the others hear it here
                if (kind == ProjectileKind.Torpedo) Sounds.MissileLaunch(from);
                else if (kind == ProjectileKind.Plasma || kind == ProjectileKind.Ion) Sounds.Shot(spec.Blast, from);
            }
        }

        private static GameObject Visual(ProjectileKind kind)
        {
            switch (kind)
            {
                case ProjectileKind.Plasma: return Fx.Orb(new Color(1f, 0.5f, 0.15f), 0.22f);
                case ProjectileKind.Ion: return Fx.Orb(new Color(0.35f, 0.6f, 1f), 0.14f);
            }
            var go = new GameObject("Arsenal.Projectile");
            go.AddComponent<MeshRenderer>().sharedMaterial = Assets.ToolAtlas;
            var mf = go.AddComponent<MeshFilter>();
            var child = go.transform;
            switch (kind)
            {
                case ProjectileKind.Grenade:
                    mf.sharedMesh = Explosives.GrenadeMeshShared;
                    break;
                case ProjectileKind.Rocket:
                    mf.sharedMesh = Missiles.Mesh;
                    child.localScale = Vector3.one * 0.7f;
                    break;
                case ProjectileKind.Torpedo:
                    mf.sharedMesh = Missiles.Mesh;
                    child.localScale = new Vector3(2.4f, 2.4f, 2.2f);
                    break;
            }
            return go;
        }

        /// <summary>The flying copy ended (or was shot down): tell the others.</summary>
        public static void Done(ProjectileSim sim)
        {
            Flying.Remove(Key(sim.OwnerClient, sim.Id));
            if (!sim.Owner) return;
            ArsenalNet.SendToServer(Op.ProjectileDone, w =>
            {
                w.Write(sim.OwnerClient);
                w.Write(sim.Id);
            });
        }

        /// <summary>Point defence hit a projectile someone else is flying: ask its owner to blow it up.</summary>
        public static void RequestIntercept(bool missile, int ownerClient, uint id)
        {
            ArsenalNet.SendToServer(Op.InterceptRequest, w =>
            {
                w.Write(missile);
                w.Write(ownerClient);
                w.Write(id);
            });
        }

        private static void ClientOnIntercept(BinaryReader r)
        {
            bool missile = r.ReadBoolean();
            int owner = r.ReadInt32();
            uint id = r.ReadUInt32();
            if (owner != Explosives.LocalClientId) return;
            if (missile) Missiles.InterceptLocal(owner, id);
            else if (Flying.TryGetValue(Key(owner, id), out ProjectileSim sim) && sim != null) sim.Detonate();
        }
    }

    internal sealed class ProjectileSim : MonoBehaviour, IThreat
    {
        public ProjectileSpec Spec;
        public Vector3 Velocity;
        public Vector3 GravityVector;
        public uint Target;
        public uint ShooterRootId;
        public bool Owner;
        public int OwnerClient;
        public uint Id;

        private float _age, _nextTrail;
        private bool _done;

        public Vector3 Position => transform.position;
        Vector3 IThreat.Velocity => Velocity;
        public uint ShooterRoot => ShooterRootId;

        private void OnEnable() => Projectiles.Threats.Add(this);
        private void OnDisable() => Projectiles.Threats.Remove(this);

        public void ShootDown()
        {
            if (Owner) Detonate();
            else Projectiles.RequestIntercept(false, OwnerClient, Id);
        }

        private void Update()
        {
            if (_done) return;
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            _age += dt;
            if (_age > Spec.Life)
            {
                Detonate();
                return;
            }
            Vector3 dir = Velocity.sqrMagnitude > 0.01f ? Velocity.normalized : transform.forward;
            if (Spec.Homing && Target != 0 && _age > 0.5f && Svc.ShipsClient.TryGetTrackedShip(Target, out TrackedShipClient t) && t.IsRigidbodyLoaded)
            {
                Vector3 want = (ShipWeapons.TargetPoint(t) - transform.position).normalized;
                dir = Vector3.RotateTowards(dir, want, Spec.TurnRate * Mathf.Deg2Rad * dt, 0f);
            }
            float speed = Mathf.Min(Spec.MaxSpeed, Velocity.magnitude + Spec.Accel * dt);
            Velocity = Spec.Gravity > 0f ? Velocity + GravityVector * dt : dir * speed;

            Vector3 step = Velocity * dt;
            float dist = step.magnitude;
            if (dist > 0.0001f && ClientScene.PhysicsScene.SphereCast(transform.position, Spec.Radius, step / dist, out RaycastHit hit, dist, Ballistics.Mask, QueryTriggerInteraction.Ignore))
            {
                bool own = _age < 0.4f && ShipWeapons.IsOwnShip(hit, ShooterRootId);
                if (!own)
                {
                    transform.position = hit.point - step / dist * Mathf.Max(0.05f, Spec.Radius);
                    Impact(hit);
                    return;
                }
            }
            transform.position += step;
            if (Velocity.sqrMagnitude > 0.01f && Spec.Kind != ProjectileKind.Grenade)
                transform.rotation = Quaternion.LookRotation(Velocity);
            else
                transform.Rotate(new Vector3(400f, 130f, 0f) * dt, Space.Self);

            if (Spec.Homing && Target != 0 && _age > 1f && Svc.ShipsClient.TryGetTrackedShip(Target, out TrackedShipClient tt) && tt.IsRigidbodyLoaded &&
                (ShipWeapons.TargetPoint(tt) - transform.position).sqrMagnitude < 4f * 4f)
            {
                Detonate();
                return;
            }
            if (_age >= _nextTrail && Spec.TrailEvery > 0f)
            {
                _nextTrail = _age + Spec.TrailEvery;
                if (Spec.Kind == ProjectileKind.Plasma || Spec.Kind == ProjectileKind.Ion)
                    Fx.Flame(transform.position, -dir * 2f, Spec.TrailSize);
                else
                    Fx.Puff(transform.position - dir * 0.3f, Spec.TrailSize, Spec.TrailColor, 1.2f, 1, -dir);
            }
        }

        private void Impact(RaycastHit hit)
        {
#if DEV
            ArsenalMod.Log.Msg($"[DEV] projectile {Spec.Kind} hit {hit.collider.name} (rb {(hit.rigidbody ? hit.rigidbody.name : "-")}, layer {hit.collider.gameObject.layer}) age {_age:0.00} dist {hit.distance:0.0}");
#endif
            if (Owner)
            {
                // direct effects of the bolt itself
                if (Spec.EmpSeconds > 0f)
                {
                    IShipPartHelper helper = hit.collider.GetComponentInParent<IShipPartHelper>();
                    if (helper != null) Effects.RequestShipEmp(helper.ShipId, Spec.EmpSeconds);
                    if (Ballistics.TryGetOtherPlayer(hit, out uint pid))
                        PlayerDamage.Report(pid, Spec.DirectPlayerDamage, Spec.Blast, false, 0f, Spec.EmpSeconds * 0.5f);
                }
                if (Spec.DirectPanelDamage > 0f && ShipPartsTagDetection.ColliderIsHull(hit.collider) && Ballistics.TryResolvePanel(hit, out PanelKey key, out HullPart part, out Vector2 local))
                {
                    var holes = new HoleMask();
                    if (HullDamage.CanHaveHoles(part)) holes.PunchDisc(local.x, local.y, 0.06f, Ballistics.Rng);
                    HullDamage.ReportHit(key, holes, Spec.DirectPanelDamage);
                    HullDamage.PredictHoles(key, holes);
                }
            }
            if (Spec.Kind == ProjectileKind.Ion)
            {
                Fx.ElectricArc(hit.point, hit.point + hit.normal * 1.2f + Random.insideUnitSphere, new Color(0.4f, 0.65f, 1f), 0.3f, 0.05f);
                Fx.EmitSparks(hit.point, hit.normal, 20);
                Sounds.Zapped(hit.point);
            }
            Detonate();
        }

        public void Detonate()
        {
#if DEV
            if (!_done) ArsenalMod.Log.Msg($"[DEV] projectile {Spec.Kind} detonates at age {_age:0.00}");
#endif
            DetonateNow();
        }

        private void DetonateNow()
        {
            if (_done) return;
            _done = true;
            if (Owner && Spec.BlastRadius > 0f)
                Explosives.RequestExplosion(transform.position, Spec.Blast);
            Projectiles.Done(this);
            Destroy(gameObject);
        }
    }
}
