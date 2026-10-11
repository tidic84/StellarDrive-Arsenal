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
        Sam = 6,      // surface-to-air missile: fast, agile, proximity fuse
        Hyper = 7,    // "Foudre" hypersonic missile: thousands of m/s, catches the fastest ships
    }

    internal sealed class ProjectileSpec
    {
        public ProjectileKind Kind;
        public WeaponKind Blast;     // explosion entry the server uses
        public float Speed, MaxSpeed, Accel, Gravity, TurnRate, Life, Radius;
        public float BlastRadius, BlastPower, BlastPlayerDamage;
        public float EmpSeconds, DirectPanelDamage, DirectPlayerDamage;
        public bool Homing;
        public float Fuse = 4f;        // proximity fuse distance to the target (homing)
        public float GuideAfter = 0.5f; // seconds of straight flight before the seeker steers
        public float DecoyChance;       // odds that one flare in its seeker view pulls it off (heat seekers)
        public bool Hypersonic;         // intercept guidance on the target's real velocity, swept proximity fuse, blast pinned to the ship hit
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
        uint HomingTarget { get; } // ship it is guided onto (0 = unguided, or decoyed)
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
                BlastRadius = 9f, BlastPower = 4200f, BlastPlayerDamage = 170f, Homing = true, DecoyChance = 0.2f, TrailEvery = 0.035f, TrailSize = 0.15f,
            },
            [ProjectileKind.Sam] = new ProjectileSpec
            {
                Kind = ProjectileKind.Sam, Blast = WeaponKind.SamLauncher, Speed = 40f, MaxSpeed = 175f, Accel = 160f, TurnRate = 150f, Life = 14f, Radius = 0.1f,
                BlastRadius = 5f, BlastPower = 1900f, BlastPlayerDamage = 120f, Homing = true, DecoyChance = 0.3f, Fuse = 6f, GuideAfter = 0.25f, TrailEvery = 0.02f, TrailSize = 0.1f,
            },
            [ProjectileKind.Hyper] = new ProjectileSpec
            {
                // launched at 400 m/s over the ship's own speed, then +6000 m/s every second up to 20 km/s
                Kind = ProjectileKind.Hyper, Blast = WeaponKind.HyperMissile, Speed = 400f, MaxSpeed = 20000f, Accel = 6000f, TurnRate = 70f, Life = 10f, Radius = 0.12f,
                BlastRadius = 6f, BlastPower = 2600f, BlastPlayerDamage = 150f, Homing = true, Hypersonic = true, DecoyChance = 0.12f, Fuse = 15f, GuideAfter = 0.15f,
                TrailSize = 0.14f, TrailColor = new Color(0.85f, 0.88f, 0.95f),
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

        public static bool TryGet(int owner, uint id, out ProjectileSim sim) => Flying.TryGetValue(Key(owner, id), out sim) && sim != null;

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
            switch (kind)
            {
                case ProjectileKind.Rocket: MissileTrail.Attach(go, 0.75f, 2.5f, 1.2f); break;
                case ProjectileKind.Sam: MissileTrail.Attach(go, 1f, 5f, 2.5f); break;
                case ProjectileKind.Torpedo: MissileTrail.Attach(go, 1.8f, 6f, 3f).Nozzle = 1f; break;
                case ProjectileKind.Hyper:
                {
                    // blue-white scramjet glow and a long vapour streak that shows where it went
                    MissileTrail t = MissileTrail.Attach(go, 1.3f, 4f, 4f);
                    t.Core = new Color(0.55f, 0.75f, 1f);
                    t.Streak = new Color(0.85f, 0.92f, 1f);
                    t.Nozzle = 0.55f;
                    break;
                }
            }
            if (!owner)
            {
                // the shooter plays its own launch sound; the others hear it here
                if (kind == ProjectileKind.Torpedo || kind == ProjectileKind.Hyper) Sounds.MissileLaunch(from);
                else if (kind == ProjectileKind.Plasma || kind == ProjectileKind.Ion) Sounds.Shot(spec.Blast, from);
            }
        }

        private static Mesh _hyperMesh;
        private static Mesh HyperMesh => _hyperMesh != null ? _hyperMesh : (_hyperMesh = PartModels.HyperMissile());

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
                case ProjectileKind.Sam:
                    mf.sharedMesh = Missiles.Mesh;
                    child.localScale = Vector3.one * 0.85f;
                    break;
                case ProjectileKind.Torpedo:
                    mf.sharedMesh = Missiles.Mesh;
                    child.localScale = new Vector3(2.4f, 2.4f, 2.2f);
                    break;
                case ProjectileKind.Hyper:
                    mf.sharedMesh = HyperMesh;
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
        private Vector3 _lastTargetPos, _targetVel;
        private bool _hasLast;
        private bool _done;

        public FlareSim Decoy;
        private readonly HashSet<uint> _seenFlares = new HashSet<uint>();

        public Vector3 Position => transform.position;
        Vector3 IThreat.Velocity => Velocity;
        public uint ShooterRoot => ShooterRootId;
        public uint HomingTarget => Spec.Homing ? Target : 0;

        /// <summary>A flare pulled the seeker off its ship.</summary>
        public void SetDecoy(FlareSim flare)
        {
            Target = 0;
            Decoy = flare;
        }

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
            if (Decoy != null && Decoy.Burning)
            {
                // chasing a flare: blows up on it
                Vector3 to = Decoy.transform.position - transform.position;
                if (to.sqrMagnitude < Spec.Fuse * Spec.Fuse * 0.5f)
                {
                    Detonate();
                    return;
                }
                dir = Vector3.RotateTowards(dir, to.normalized, Spec.TurnRate * Mathf.Deg2Rad * dt, 0f);
            }
            else if (Decoy != null) Decoy = null; // burnt out: flies on straight
            if (Owner && Spec.DecoyChance > 0f && Target != 0 && Decoy == null && _age > Spec.GuideAfter)
            {
                FlareSim flare = Flares.Seduce(transform.position, dir, Spec.DecoyChance, _seenFlares);
                if (flare != null)
                {
                    SetDecoy(flare);
                    Flares.ReportDecoy(false, OwnerClient, Id, flare);
                }
            }
            bool chasing = false;
            Vector3 tCenter = Vector3.zero, tVel = Vector3.zero, tOrigin = Vector3.zero;
            Quaternion tRot = Quaternion.identity;
            if (Spec.Hypersonic)
            {
                // works on ships in other physics spaces too (far away, or racing at thousands of m/s)
                chasing = Target != 0 && _age > Spec.GuideAfter && ShipWeapons.TryGetShipMotion(Target, out tCenter, out tVel, out tOrigin, out tRot);
                if (chasing)
                {
#if DEV
                    if (Owner && Mathf.Repeat(_age, 0.25f) < dt)
                    {
                        ArsenalMod.Log.Msg($"[DEV] hyper age {_age:0.00}: {Vector3.Distance(tCenter, transform.position):0} m to target, {Velocity.magnitude:0} m/s, target {tVel} ({tVel.magnitude:0} m/s), moved {(tCenter - _devLastT) / Mathf.Max(0.01f, _age - _devLastAge)}, off-axis {Vector3.Angle(dir, tCenter - transform.position):0} deg");
                        _devLastT = tCenter;
                        _devLastAge = _age;
                    }
#endif
                    _hyperAccel = Guidance(tCenter - transform.position, tVel - Velocity, tVel);
                }
                else _hyperAccel = dir * Spec.Accel;
            }
            else if (Spec.Homing && Target != 0 && _age > Spec.GuideAfter && Svc.ShipsClient.TryGetTrackedShip(Target, out TrackedShipClient t) && t.IsRigidbodyLoaded)
            {
                // lead pursuit: aim where the target will be when the missile gets there
                Vector3 tp = ShipWeapons.TargetPoint(t);
                Vector3 tv = _hasLast && Time.deltaTime > 0f ? (tp - _lastTargetPos) / Time.deltaTime : Vector3.zero;
                _targetVel = Vector3.Lerp(_targetVel, tv, 0.2f);
                _lastTargetPos = tp;
                _hasLast = true;
                float eta = Vector3.Distance(tp, transform.position) / Mathf.Max(20f, Velocity.magnitude);
                Vector3 want = (tp + _targetVel * Mathf.Min(eta, 4f) - transform.position).normalized;
                dir = Vector3.RotateTowards(dir, want, Spec.TurnRate * Mathf.Deg2Rad * dt, 0f);
            }
            float speed = Mathf.Min(Spec.MaxSpeed, Velocity.magnitude + Spec.Accel * dt);
            if (Spec.Hypersonic)
            {
                // vectored thrust: the motor accelerates the missile in any direction (turning a 10 km/s velocity
                // vector by rotating it would never converge)
                Vector3 a = _hyperAccel.sqrMagnitude > 1f ? _hyperAccel : dir * Spec.Accel;
                Velocity = Vector3.ClampMagnitude(Velocity + a * dt, Spec.MaxSpeed);
                dir = Velocity.normalized;
            }
            else Velocity = Spec.Gravity > 0f ? Velocity + GravityVector * dt : dir * speed;

            Vector3 step = Velocity * dt;
            float dist = step.magnitude;
            if (chasing && _age > Spec.GuideAfter + 0.1f && SweptFuse(tCenter, tVel, tOrigin, tRot, step, dt))
                return;
            if (dist > 0.0001f && ClientScene.PhysicsScene.SphereCast(transform.position, Spec.Radius, step / dist, out RaycastHit hit, dist, Ballistics.Mask, QueryTriggerInteraction.Ignore))
            {
                bool own = _age < 0.4f && ShipWeapons.IsOwnShip(hit, ShooterRootId);
                if (!own)
                {
                    transform.position = hit.point - step / dist * Mathf.Max(0.05f, Spec.Radius);
                    if (Spec.Hypersonic) PinTo(hit);
                    Impact(hit);
                    return;
                }
            }
            transform.position += step;
            if (Velocity.sqrMagnitude > 0.01f && Spec.Kind != ProjectileKind.Grenade)
                transform.rotation = Quaternion.LookRotation(Velocity);
            else
                transform.Rotate(new Vector3(400f, 130f, 0f) * dt, Space.Self);

            if (Spec.Homing && Target != 0 && _age > Spec.GuideAfter + 0.4f && Svc.ShipsClient.TryGetTrackedShip(Target, out TrackedShipClient tt) && tt.IsRigidbodyLoaded &&
                (ShipWeapons.TargetPoint(tt) - transform.position).sqrMagnitude < Spec.Fuse * Spec.Fuse)
            {
                Detonate();
                return;
            }
            if (_age >= _nextTrail && Spec.TrailEvery > 0f)
            {
                _nextTrail = _age + Spec.TrailEvery;
                if (Spec.Kind == ProjectileKind.Plasma || Spec.Kind == ProjectileKind.Ion)
                    Fx.Flame(transform.position, -dir * 2f, Spec.TrailSize);
                else if (Spec.Kind == ProjectileKind.Grenade)
                    Fx.Puff(transform.position - dir * 0.3f, Spec.TrailSize, Spec.TrailColor, 1.2f, 1, -dir);
                // rockets and missiles: their MissileTrail does the exhaust
            }
        }

        /// <summary>
        /// Acceleration command (m/s², at most the motor's thrust). Closing in: proportional navigation, which
        /// cancels the turning of the line of sight so the missile ends on a collision course whatever the speeds,
        /// with the spare thrust pushing along the line of sight. Not closing (the target outruns it, or it flew
        /// past): full thrust towards the intercept point until it closes again.
        /// </summary>
        private Vector3 Guidance(Vector3 rel, Vector3 relVel, Vector3 targetVel)
        {
            float dist = rel.magnitude;
            if (dist < 0.01f) return Vector3.zero;
            Vector3 u = rel / dist;
            float closing = -Vector3.Dot(relVel, u);
            if (closing < 50f)
            {
                float s = Mathf.Clamp(Velocity.magnitude + Spec.Accel, targetVel.magnitude + 500f, Spec.MaxSpeed);
                Vector3 wantVel = Intercept(rel, targetVel, s).normalized * s;
                return (wantVel - Velocity).normalized * Spec.Accel;
            }
            const float N = 4f;
            Vector3 losRate = Vector3.Cross(rel, relVel) / (dist * dist); // rad/s, axis of the line-of-sight rotation
            Vector3 lateral = N * closing * Vector3.Cross(losRate, u);
            lateral = Vector3.ClampMagnitude(lateral, Spec.Accel);
            float axial = Mathf.Sqrt(Mathf.Max(0f, Spec.Accel * Spec.Accel - lateral.sqrMagnitude));
            return lateral + u * axial;
        }

        /// <summary>Direction to fly to meet a target at <paramref name="rel"/> moving at <paramref name="v"/>, flying at <paramref name="speed"/>.</summary>
        private static Vector3 Intercept(Vector3 rel, Vector3 v, float speed)
        {
            // |rel + v t| = speed t  ->  (v.v - s^2) t^2 + 2 (rel.v) t + rel.rel = 0
            float a = Vector3.Dot(v, v) - speed * speed, b = 2f * Vector3.Dot(rel, v), c = Vector3.Dot(rel, rel);
            float t = -1f;
            if (Mathf.Abs(a) < 0.001f) t = b < 0f ? -c / b : -1f;
            else
            {
                float disc = b * b - 4f * a * c;
                if (disc >= 0f)
                {
                    float sq = Mathf.Sqrt(disc);
                    float t1 = (-b - sq) / (2f * a), t2 = (-b + sq) / (2f * a);
                    t = t1 > 0f && (t1 < t2 || t2 <= 0f) ? t1 : t2;
                }
            }
            if (t <= 0f) t = rel.magnitude / Mathf.Max(1f, speed); // can't catch it yet: aim ahead anyway, the motor keeps pushing
            return rel + v * Mathf.Min(t, 5f);
        }

        /// <summary>
        /// Proximity fuse over the whole frame: at thousands of m/s the missile and its target cross hundreds
        /// of metres per frame, so check their closest approach along both paths, not just the end positions.
        /// </summary>
        private bool SweptFuse(Vector3 targetCenter, Vector3 targetVel, Vector3 targetOrigin, Quaternion targetRot, Vector3 step, float dt)
        {
            Vector3 rel0 = targetCenter - transform.position;
            Vector3 relStep = step - targetVel * dt; // the missile's move as seen from the target
            float k = relStep.sqrMagnitude > 0.0001f ? Mathf.Clamp01(Vector3.Dot(rel0, relStep) / relStep.sqrMagnitude) : 0f;
            Vector3 miss = rel0 - relStep * k; // target centre minus missile, at the closest moment
            if (miss.sqrMagnitude > Spec.Fuse * Spec.Fuse) return false;
            // blow up on the side of the ship it came from, a little short of the centre
            Vector3 point = targetCenter - miss.normalized * Mathf.Min(miss.magnitude, 2f);
            transform.position = point;
            // where the missile is at that moment: the server moves the blast onto the nearest piece of hull
            _pinShip = Target;
            _pinLocal = Quaternion.Inverse(targetRot) * (targetCenter - miss - targetOrigin);
            _pinDir = Quaternion.Inverse(targetRot) * (Velocity - targetVel).normalized;
#if DEV
            ArsenalMod.Log.Msg($"[DEV] hypersonic fuse: missed centre by {miss.magnitude:0.0} m at {Velocity.magnitude:0} m/s (target {targetVel.magnitude:0} m/s)");
#endif
            Detonate();
            return true;
        }

        private Vector3 _hyperAccel;
#if DEV
        private Vector3 _devLastT;
        private float _devLastAge;
#endif
        private uint _pinShip;
        private Vector3 _pinLocal, _pinDir; // where it went off and which way it was flying, in the ship's frame

        private void PinTo(RaycastHit hit)
        {
            IShipPartHelper helper = hit.collider.GetComponentInParent<IShipPartHelper>();
            if (helper == null || !Svc.ShipsClient.TryGetTrackedShip(helper.ShipId, out TrackedShipClient s) || !s.IsRigidbodyLoaded) return;
            _pinShip = s.Id;
            _pinLocal = s.ShipRigidBody.transform.InverseTransformPoint(transform.position);
            _pinDir = s.ShipRigidBody.transform.InverseTransformDirection(Velocity.normalized);
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
                    if (HullDamage.CanHaveHoles(part)) holes = holes.PunchDisc(local.x, local.y, 0.06f, Ballistics.Rng);
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
            {
                if (_pinShip != 0) Explosives.RequestExplosionOnShip(_pinShip, _pinLocal, _pinDir, transform.position, Spec.Blast);
                else Explosives.RequestExplosion(transform.position, Spec.Blast);
            }
            Projectiles.Done(this);
            Destroy(gameObject);
        }
    }
}
