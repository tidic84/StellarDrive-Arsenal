using System.Collections.Generic;
using System.Linq;
using Arsenal.Content;
using Arsenal.Core;
using Arsenal.Damage;
using Arsenal.Net;
using Arsenal.Visual;
using Arsenal.Weapons;
using Planets.Interface.Model;
using Players;
using Players.Interface.Model;
using Ships;
using Ships.Interface.Model.Parts;
using Ships.Interface.Model.Parts.State;
using Ships.Interface.Model.State;
using Ships.Interface.Services;
using Ships.Parts.Common.Model.Reactions;
using UnityEngine;
using WorldTracking.Interface.Values;

namespace Arsenal.Vehicles
{
    /// <summary>
    /// Runs the ship-mounted weapons. Every client sees the weapon's signal (the game syncs part states);
    /// the "owner" client (whoever pilots that ship, otherwise the host) does the shooting, reports damage
    /// to the server and relays the effects to the others.
    /// </summary>
    internal sealed class ShipWeapons : MonoBehaviour
    {
        internal sealed class Mount
        {
            public PartContext Ctx;
            public ushort Kind;
            public float NextShot;
            public int Barrel;
            public int Missiles = 4;
            public float ReloadAt;
            public float Heat;
            public bool Overheated;
            public bool LastSignal;
            public bool Firing;
            public float NextBurn, NextRelay;
            public FMOD.Channel Loop;
            public LineRenderer Beam;
            public Light BeamLight;
            public Transform Visual;
            public Transform[] Points; // muzzles / tubes on the visual model
            public Renderer Lens;
            public int SeenFrame;
            public uint LockedTarget;
            public float Charge;          // railgun charge 0..1
            public FMOD.Channel ChargeLoop;
            public Transform Head, Pitch, Barrels, Tube;
            public float Spin;            // gatling barrel speed
            public IThreat Tracking;      // point-defence target
            public Transform Radar;
            public LockOn Lock;           // SAM battery
            public int Rockets = -1;      // rocket pod (-1 = not loaded yet)
            public int FlaresLeft;        // flare dispenser: flares still to throw in the current salvo
            public float NextSalvo;
        }

        // muzzle offsets in part space (must match PartModels)
        private static readonly Vector3[] GunMuzzles = { new Vector3(-0.065f, 0.27f, 0.8f), new Vector3(0.065f, 0.27f, 0.8f) };
        private static readonly Vector3[] PodTubes = { new Vector3(-0.11f, 0.42f, 0.36f), new Vector3(0.11f, 0.42f, 0.36f), new Vector3(-0.11f, 0.24f, 0.36f), new Vector3(0.11f, 0.24f, 0.36f) };
        private static readonly Vector3 LaserMuzzle = new Vector3(0, 0.27f, 0.67f);

        public static readonly BulletSpec GunBullet = new BulletSpec { PlayerDamage = 28, PanelDamage = 16, Penetration = 80, HoleRadius = 0.04f, Range = 700f, ObjectDamage = 1f };

#if DEV
        public static readonly HashSet<ushort> DevForceOn = new HashSet<ushort>();
        public static bool DevPdcSelf;
        private static bool DevForced(ushort partId) => DevForceOn.Contains(partId);
#else
        private const bool DevPdcSelf = false;
        private static bool DevForced(ushort partId) => false;
#endif

        private static ShipWeapons _instance;
        private readonly Dictionary<PartContext, Mount> _mounts = new Dictionary<PartContext, Mount>();
        private readonly List<ImpactFx> _impacts = new List<ImpactFx>();
        private static Material _beamMat;

        public static IEnumerable<Mount> Mounts => _instance != null ? _instance._mounts.Values : Enumerable.Empty<Mount>();

        public static void Ensure()
        {
            if (_instance != null) return;
            var go = new GameObject("Arsenal.ShipWeapons");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<ShipWeapons>();
            go.AddComponent<ShipHud>();
        }

        public static void ResetAll()
        {
            if (_instance == null) return;
            foreach (Mount m in _instance._mounts.Values) StopBeam(m);
            _instance._mounts.Clear();
            Claimed.Clear();
            LastSynced.Clear();
            _beamMat = null;
        }

        private void Update()
        {
            if (!ArsenalMod.InGame || Svc.ShipsClient == null) return;
            GunnerSeat.ClientUpdate();
            int frame = Time.frameCount;
            foreach (TrackedShipClient ship in Svc.ShipsClient.Ships.ToList())
            {
                if (!ship.IsRigidbodyLoaded) continue;
                bool? owner = null;
                foreach (StatefulPart part in ship.StatefulParts)
                {
                    if (part.Settings == null || !ShipContent.IsWeapon(part.Settings.id)) continue;
                    var ctx = new PartContext(ship.Id, part.Id);
                    if (!_mounts.TryGetValue(ctx, out Mount m))
                        _mounts[ctx] = m = new Mount { Ctx = ctx, Kind = part.Settings.id };
                    m.SeenFrame = frame;
                    double signal = part.State is SignalDisplayState s ? s.SignalValue : 0.0;
                    bool on = (signal > 0.5 || DevForced(part.Id)) && !Effects.IsShipEmp(ship.Id);
                    owner ??= IsOwner(ship);
                    ResolveVisual(m);
                    try
                    {
                        switch (m.Kind)
                        {
                            case ShipContent.MachineGunId: UpdateGun(ship, part, m, on, owner.Value); break;
                            case ShipContent.MissilePodId: UpdatePod(ship, part, m, on, owner.Value); break;
                            case ShipContent.LaserCannonId: UpdateLaser(ship, part, m, on, owner.Value); break;
                            case ShipContent.PdcId: UpdatePdc(ship, part, m, on, owner.Value); break;
                            case ShipContent.PlasmaCannonId: UpdateBolt(ship, part, m, on, owner.Value, ProjectileKind.Plasma, 0.75f, WeaponKind.PlasmaCannon); break;
                            case ShipContent.IonCannonId: UpdateBolt(ship, part, m, on, owner.Value, ProjectileKind.Ion, 2.2f, WeaponKind.IonCannon); break;
                            case ShipContent.RailgunId: UpdateRailgun(ship, part, m, on, owner.Value); break;
                            case ShipContent.TorpedoId: UpdateTorpedo(ship, part, m, on, owner.Value); break;
                            case ShipContent.RocketPodId: UpdateRocketPod(ship, part, m, on, owner.Value); break;
                            case ShipContent.SamBatteryId: UpdateSamBattery(ship, part, m, on, owner.Value); break;
                            case ShipContent.FlareDispenserId: UpdateFlares(ship, part, m, on, owner.Value); break;
                            case ShipContent.HyperLauncherId: UpdateHyper(ship, part, m, on, owner.Value); break;
                        }
                    }
                    catch (System.Exception ex)
                    {
                        ArsenalMod.Log.Error($"Weapon {m.Kind} on ship {ship.Id}: {ex.Message}");
                    }
                    m.LastSignal = on;
                }
            }
            foreach (PartContext gone in _mounts.Where(kv => kv.Value.SeenFrame != frame).Select(kv => kv.Key).ToList())
            {
                StopBeam(_mounts[gone]);
                _mounts.Remove(gone);
            }
        }

        // ------------------------------------------------------------------ helpers

        public static uint RootOf(uint shipId) =>
            Svc.ShipsClient.TryGetTrackedRootShip(shipId, out TrackedShipClient root) && root != null ? root.Id : shipId;

        /// <summary>The pilot of this ship (any seat on the same rigid assembly) shoots; nobody piloting: the host.</summary>
        public static bool IsOwner(TrackedShipClient ship)
        {
            uint root = RootOf(ship.Id);
            if (GunnerSeat.TryGetPilotedRoot(out uint mine) && mine == root) return true;
            foreach (TrackedPlayerOtherClient other in Svc.PlayersClient.OtherPlayers)
            {
                PlayerState st = other.State;
                if (st.Type == PlayerStateType.Piloting && RootOf(st.EntityId) == root) return false;
            }
            return ArsenalNet.IsServer;
        }

        /// <summary>The local player is at the controls of this ship.</summary>
        public static bool PilotedByMe(TrackedShipClient ship) => GunnerSeat.TryGetPilotedRoot(out uint r) && r == RootOf(ship.Id);

        /// <summary>Physics-space pose of a point of the part (from the ship rigidbody, same space as the raycasts).</summary>
        public static Vector3 PartPoint(TrackedShipClient ship, StatefulPart part, Vector3 local) =>
            ship.ShipRigidBody.transform.TransformPoint(part.LocalPosition + part.LocalRotation * local);

        public static Vector3 PartForward(TrackedShipClient ship, StatefulPart part) =>
            ship.ShipRigidBody.transform.TransformDirection(part.LocalRotation * Vector3.forward);

        /// <summary>Did this hit land on the shooter's own ship? (some part colliders have no rigidbody: go through the part)</summary>
        public static bool IsOwnShip(RaycastHit hit, uint shooterRoot) => shooterRoot != 0 && HitRoot(hit) == shooterRoot;

        /// <summary>Root ship assembly a hit landed on (0 = not a ship: terrain, rocks...).</summary>
        public static uint HitRoot(RaycastHit hit)
        {
            if (hit.rigidbody != null)
                foreach (TrackedShipClient s in Svc.ShipsClient.Ships)
                    if (s.IsRigidbodyLoaded && s.ShipRigidBody == hit.rigidbody.gameObject)
                        return RootOf(s.Id);
            IShipPartHelper helper = hit.collider.GetComponentInParent<IShipPartHelper>();
            return helper != null ? RootOf(helper.ShipId) : 0;
        }

        private static readonly RaycastHit[] LosHits = new RaycastHit[16];

        /// <summary>Nothing but the two ships themselves between the points (terrain, rocks, other ships block).</summary>
        public static bool ClearLine(Vector3 from, Vector3 to, uint shooterRoot, uint targetRoot)
        {
            Vector3 d = to - from;
            float len = d.magnitude;
            if (len < 0.01f) return true;
            int n = ClientScene.PhysicsScene.Raycast(from, d / len, LosHits, len, Ballistics.Mask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                uint r = HitRoot(LosHits[i]);
                if (r == 0 || (r != shooterRoot && r != targetRoot)) return false;
            }
            return true;
        }

        /// <summary>Can a sensor at <paramref name="from"/> see this ship? Its centre, or a point above it (a ship half behind a ridge still shows).</summary>
        public static bool CanSee(Vector3 from, TrackedShipClient target, uint shooterRoot)
        {
            uint root = RootOf(target.Id);
            Vector3 p = TargetPoint(target);
            return ClearLine(from, p, shooterRoot, root) || ClearLine(from, p + Fx.Up * 1.5f, shooterRoot, root);
        }

        public static Rigidbody RootBody(TrackedShipClient ship)
        {
            uint root = RootOf(ship.Id);
            if (root != ship.Id && Svc.ShipsClient.TryGetTrackedShip(root, out TrackedShipClient r) && r.IsRigidbodyLoaded)
                return r.ShipRigidBody.GetComponent<Rigidbody>();
            return ship.ShipRigidBody.GetComponent<Rigidbody>();
        }

        private static void ResolveVisual(Mount m)
        {
            if (m.Visual != null) return;
            if (!Svc.ShipsClient.TryGetVisualPart(m.Ctx, out IShipPartVisual v) || !(v is Component c)) return;
            m.Visual = c.transform;
            string pointName = m.Kind == ShipContent.MissilePodId || m.Kind == ShipContent.RocketPodId || m.Kind == ShipContent.SamBatteryId || m.Kind == ShipContent.HyperLauncherId ? "Tube" : "Muzzle";
            m.Points = c.GetComponentsInChildren<Transform>(true).Where(t => t.name == pointName).ToArray();
            m.Lens = c.GetComponentsInChildren<Renderer>(true).FirstOrDefault(r => r.name == "Lens");
            Transform[] all = c.GetComponentsInChildren<Transform>(true);
            m.Head = all.FirstOrDefault(t => t.name == "Head" && t.GetComponent<Renderer>() == null);
            m.Pitch = all.FirstOrDefault(t => t.name == "Pitch");
            m.Barrels = all.FirstOrDefault(t => t.name == "Barrels" && t.GetComponent<Renderer>() == null);
            if (m.Kind == ShipContent.TorpedoId) m.Tube = all.FirstOrDefault(t => t.name == "Tube");
            m.Radar = all.FirstOrDefault(t => t.name == "Radar");
        }

        private static Vector3 VisualPoint(Mount m, int index, Vector3 fallback)
        {
            if (m.Points != null && index < m.Points.Length && m.Points[index] != null) return m.Points[index].position;
            return fallback;
        }

        // ------------------------------------------------------------------ machine gun

        private void UpdateGun(TrackedShipClient ship, StatefulPart part, Mount m, bool on, bool owner)
        {
            if (!owner || !on) return;
            if (Time.time < m.NextShot) return;
            m.NextShot = Mathf.Max(m.NextShot + 60f / 900f, Time.time - 0.05f);
            int b = m.Barrel = (m.Barrel + 1) % 2;
            Vector3 origin = PartPoint(ship, part, GunMuzzles[b]);
            Vector3 fwd = PartForward(ship, part);
            Vector3 dir = Quaternion.AngleAxis(Random.Range(0f, 360f), fwd) * Quaternion.AngleAxis(Random.Range(0f, 0.45f), Vector3.Cross(fwd, Fx.Up).normalized) * fwd;
            _impacts.Clear();
            Vector3 end = Ballistics.Fire(origin, dir, GunBullet, _impacts, RootBody(ship));
            Vector3 from = VisualPoint(m, b, origin);
            Fx.MuzzleFlash(from, fwd, 0.45f, m.Points != null && b < m.Points.Length ? m.Points[b] : null);
            Fx.BulletTracer(from, end, 0.05f, 450f);
            Ballistics.PlayImpacts(_impacts, 1.3f);
            Sounds.Shot(WeaponKind.ShipGun, from);
            Ballistics.RelayShot(WeaponKind.ShipGun, from, end, _impacts);
        }

        // ------------------------------------------------------------------ missiles

        private void UpdatePod(TrackedShipClient ship, StatefulPart part, Mount m, bool on, bool owner)
        {
            // reload one missile at a time
            if (m.Missiles < 4)
            {
                if (m.ReloadAt <= 0f) m.ReloadAt = Time.time + 5f;
                else if (Time.time >= m.ReloadAt)
                {
                    m.Missiles++;
                    m.ReloadAt = m.Missiles < 4 ? Time.time + 5f : 0f;
                }
            }
            if (m.Points != null)
                for (int i = 0; i < m.Points.Length; i++)
                    if (m.Points[i] != null) m.Points[i].gameObject.SetActive(!owner || i < m.Missiles);

            Vector3 fwd = PartForward(ship, part);
            Vector3 center = PartPoint(ship, part, new Vector3(0, 0.33f, 0.4f));
            m.LockedTarget = owner ? FindTarget(ship, center, fwd) : 0;
            if (owner && (on || PilotedByMe(ship))) ThreatWarning.ReportLock(m.LockedTarget, true, center);
            if (!owner || !on || m.LastSignal) return; // fires on the rising edge of the signal
            if (m.Missiles <= 0 || Time.time < m.NextShot) return;
            m.NextShot = Time.time + 0.3f;
            m.Missiles--;
            int tube = 3 - m.Missiles;
            Vector3 from = PartPoint(ship, part, PodTubes[Mathf.Clamp(tube, 0, 3)] + Vector3.forward * 0.3f);
            Rigidbody body = RootBody(ship);
            Vector3 vel = (body != null ? body.velocity : Vector3.zero) + fwd * 30f;
            Missiles.Launch(from, vel, m.LockedTarget, RootOf(ship.Id), true, 0);
        }

        /// <summary>Nearest visible ship inside a cone in front of the launcher (missile pod: 25 degrees, 1.5 km).</summary>
        public static uint FindTarget(TrackedShipClient self, Vector3 from, Vector3 fwd, float cone = 25f, float range = 1500f)
        {
            uint myRoot = RootOf(self.Id);
            uint best = 0;
            float bestScore = float.MaxValue;
            foreach (TrackedShipClient s in Svc.ShipsClient.Ships)
            {
                if (!s.IsRigidbodyLoaded || RootOf(s.Id) == myRoot || RootOf(s.Id) != s.Id) continue;
                Vector3 to = TargetPoint(s) - from;
                float d = to.magnitude;
                if (d < 5f || d > range) continue;
                float angle = Vector3.Angle(fwd, to);
                if (angle > cone) continue;
                float score = angle * 20f + d * 0.05f;
                if (score < bestScore && CanSee(from, s, myRoot))
                {
                    bestScore = score;
                    best = s.Id;
                }
            }
            return best;
        }

        private struct SyncedPose
        {
            public uint Space;
            public Vector3 Position;
            public float ChangedAt;
        }

        private static readonly Dictionary<uint, SyncedPose> LastSynced = new Dictionary<uint, SyncedPose>();

        /// <summary>
        /// Where a ship is and how it moves, in our physics space. A ship more than ~2 km away (or racing off)
        /// is moved by the game into a physics space of its own and its rigidbody unloads here, so it is then
        /// read from its synced state, carried over from its space and pushed forward by its velocity since the
        /// last update (at thousands of m/s the raw updates jump hundreds of metres).
        /// </summary>
        public static bool TryGetShipMotion(uint id, out Vector3 center, out Vector3 velocity, out Vector3 origin, out Quaternion rotation)
        {
            center = velocity = origin = Vector3.zero;
            rotation = Quaternion.identity;
            if (Svc.ShipsClient == null || !Svc.ShipsClient.TryGetTrackedShip(id, out TrackedShipClient s)) return false;
            ShipPhysicsState st = s.PhysicsState;
            uint local = ClientOrigin.LocalPhysicsSpaceId;
            if (s.IsRigidbodyLoaded)
            {
                // the client rigidbody is driven from synced states and can read 0 m/s: the synced velocity is the truth
                Rigidbody rb = s.ShipRigidBody.GetComponent<Rigidbody>();
                center = TargetPoint(s);
                velocity = st.SpaceId == local && st.Velocity.sqrMagnitude > 0.0001f ? st.Velocity : rb != null ? rb.velocity : Vector3.zero;
                origin = s.ShipRigidBody.transform.position;
                rotation = s.ShipRigidBody.transform.rotation;
                return true;
            }
            try
            {
                Quaternion turn = st.SpaceId == local ? Quaternion.identity : Svc.TransposeClient.Rotation(st.SpaceId, local, Quaternion.identity);
                origin = st.SpaceId == local ? st.Position : Svc.TransposeClient.Position(st.SpaceId, local, st.Position);
                rotation = turn * st.Rotation;
                velocity = turn * st.Velocity;
            }
            catch (System.Exception)
            {
                return false;
            }
            if (!LastSynced.TryGetValue(id, out SyncedPose last) || last.Space != st.SpaceId || last.Position != st.Position)
                LastSynced[id] = last = new SyncedPose { Space = st.SpaceId, Position = st.Position, ChangedAt = Time.time };
            origin += velocity * Mathf.Min(Time.time - last.ChangedAt, 0.5f);
            center = origin + rotation * s.CenterOfMass;
            return true;
        }

        /// <summary>
        /// Nearest ship in a cone in front of the launcher, out to long range: also sees ships flying in other
        /// physics spaces (far or very fast). Terrain is still checked along the line of sight.
        /// </summary>
        public static uint FindFarTarget(TrackedShipClient self, Vector3 from, Vector3 fwd, float cone, float range)
        {
            uint myRoot = RootOf(self.Id);
            uint best = 0;
            float bestScore = float.MaxValue;
            foreach (TrackedShipClient s in Svc.ShipsClient.Ships)
            {
                if (RootOf(s.Id) == myRoot || RootOf(s.Id) != s.Id) continue;
                if (!TryGetShipMotion(s.Id, out Vector3 c, out _, out _, out _)) continue;
                Vector3 to = c - from;
                float d = to.magnitude;
                if (d < 10f || d > range) continue;
                float angle = Vector3.Angle(fwd, to);
                if (angle > cone) continue;
                float score = angle * 20f + d * 0.004f;
                if (score >= bestScore) continue;
                if (s.IsRigidbodyLoaded ? !CanSee(from, s, myRoot) : !ClearLine(from, c, myRoot, s.Id)) continue;
                bestScore = score;
                best = s.Id;
            }
            return best;
        }

        /// <summary>Centre of mass of a ship in the client physics scene.</summary>
        public static Vector3 TargetPoint(TrackedShipClient s)
        {
            Rigidbody rb = s.ShipRigidBody.GetComponent<Rigidbody>();
            return rb != null ? rb.worldCenterOfMass : s.ShipRigidBody.transform.position;
        }

        // ------------------------------------------------------------------ point defence

        private const float PdcRange = 300f;
        public static readonly BulletSpec RailSlug = new BulletSpec { PlayerDamage = 200, PanelDamage = 300, Penetration = 900, HoleRadius = 0.09f, Range = 1500f, ObjectDamage = 12f, ThroughPlayers = true };
        private static readonly Dictionary<IThreat, float> Claimed = new Dictionary<IThreat, float>(); // threats already shot down, waiting for their owner

        private void UpdatePdc(TrackedShipClient ship, StatefulPart part, Mount m, bool on, bool owner)
        {
            float dt = Time.deltaTime;
            uint root = RootOf(ship.Id);
            Vector3 basePos = PartPoint(ship, part, new Vector3(0, 0.3f, 0));
            Vector3 up = ship.ShipRigidBody.transform.TransformDirection(part.LocalRotation * Vector3.up);

            // keep the current target while it is valid, otherwise take the closest one
            if (!on || !ValidThreat(m.Tracking, root, basePos, up)) m.Tracking = null;
            if (on && m.Tracking == null)
            {
                float best = float.MaxValue;
                foreach (IThreat t in Projectiles.Threats)
                {
                    if (!ValidThreat(t, root, basePos, up)) continue;
                    float d = (t.Position - basePos).sqrMagnitude;
                    if (d < best)
                    {
                        best = d;
                        m.Tracking = t;
                    }
                }
            }

            Vector3 aimDir = PartForward(ship, part);
            Vector3 aimPoint = Vector3.zero;
            if (m.Tracking != null)
            {
                float dist = (m.Tracking.Position - basePos).magnitude;
                aimPoint = m.Tracking.Position + m.Tracking.Velocity * (dist / 900f);
                aimDir = (aimPoint - basePos).normalized;
            }
            bool aligned = AimHead(m, aimDir, dt);
            bool firing = on && m.Tracking != null && aligned;
            m.Spin = Mathf.MoveTowards(m.Spin, firing ? 1f : 0f, dt * 2.5f);
            if (m.Barrels != null) m.Barrels.Rotate(0f, 0f, m.Spin * 1800f * dt, Space.Self);
            if (!firing || !owner || Time.time < m.NextShot) return;

            m.NextShot = Mathf.Max(m.NextShot + 0.045f, Time.time - 0.05f);
            Vector3 from = VisualPoint(m, 0, basePos + aimDir * 0.5f);
            float range = (aimPoint - from).magnitude;
            Vector3 end = aimPoint + Random.insideUnitSphere * Mathf.Clamp(range * 0.01f, 0.3f, 3f);
            _impacts.Clear();
            WeaponFx.Shot(WeaponKind.Pdc, from, end, m.Points != null && m.Points.Length > 0 ? m.Points[0] : null);
            Ballistics.RelayShot(WeaponKind.Pdc, from, end, _impacts);
            float chance = 0.1f * Mathf.Clamp(150f / Mathf.Max(1f, range), 0.35f, 1.6f);
            if (Random.value < chance)
            {
                IThreat t = m.Tracking;
                Claimed[t] = Time.time + 2f;
                m.Tracking = null;
#if DEV
                ArsenalMod.Log.Msg($"[DEV] PDC shot down a threat at {range:0} m");
#endif
                t.ShootDown();
            }
        }

        private static bool ValidThreat(IThreat t, uint myRoot, Vector3 basePos, Vector3 up)
        {
            if (t == null || (t is Object o && o == null)) return false;
            if (t.ShooterRoot == myRoot && !DevPdcSelf) return false;
            if (Claimed.TryGetValue(t, out float until))
            {
                if (Time.time < until) return false;
                Claimed.Remove(t);
            }
            Vector3 to = t.Position - basePos;
            float d = to.magnitude;
            return d > (DevPdcSelf ? 25f : 2f) && d < PdcRange && Vector3.Dot(to / d, up) > -0.2f // the hull is in the way below the mount
                   && ClearLine(basePos, t.Position, myRoot, 0);
        }

        /// <summary>Turns the turret head and gun towards a world direction. True once the gun points there.</summary>
        private static bool AimHead(Mount m, Vector3 dir, float dt)
        {
            if (m.Head == null || m.Pitch == null) return true;
            Vector3 local = m.Head.parent.InverseTransformDirection(dir);
            float yaw = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
            float pitch = -Mathf.Atan2(local.y, new Vector2(local.x, local.z).magnitude) * Mathf.Rad2Deg;
            pitch = Mathf.Clamp(pitch, -85f, 12f);
            m.Head.localRotation = Quaternion.RotateTowards(m.Head.localRotation, Quaternion.Euler(0f, yaw, 0f), 400f * dt);
            m.Pitch.localRotation = Quaternion.RotateTowards(m.Pitch.localRotation, Quaternion.Euler(pitch, 0f, 0f), 300f * dt);
            return Vector3.Angle(m.Pitch.forward, dir) < 8f;
        }

        // ------------------------------------------------------------------ plasma / ion

        private static readonly Dictionary<ProjectileKind, Vector3> BoltMuzzles = new Dictionary<ProjectileKind, Vector3>
        {
            [ProjectileKind.Plasma] = new Vector3(0, 0.29f, 0.65f),
            [ProjectileKind.Ion] = new Vector3(0, 0.27f, 0.52f),
        };

        private void UpdateBolt(TrackedShipClient ship, StatefulPart part, Mount m, bool on, bool owner, ProjectileKind kind, float interval, WeaponKind weapon)
        {
            float ready = Mathf.Clamp01(1f - (m.NextShot - Time.time) / interval);
            if (m.Lens != null)
            {
                Color c = kind == ProjectileKind.Plasma ? new Color(1f, 0.5f, 0.15f) : new Color(0.4f, 0.65f, 1f);
                m.Lens.material.SetColor("_EmissionColor", c * (owner ? 1f + ready * 5f : 3f));
            }
            if (!owner || !on || Time.time < m.NextShot) return;
            m.NextShot = Time.time + interval;
            Vector3 origin = PartPoint(ship, part, BoltMuzzles[kind]);
            Vector3 fwd = PartForward(ship, part);
            Rigidbody body = RootBody(ship);
            ProjectileSpec spec = Projectiles.Specs[kind];
            Projectiles.Launch(kind, origin, (body != null ? body.velocity : Vector3.zero) + fwd * spec.Speed, 0, RootOf(ship.Id));
            Vector3 from = VisualPoint(m, 0, origin);
            Fx.MuzzleFlash(from, fwd, 0.5f, m.Points != null && m.Points.Length > 0 ? m.Points[0] : null);
            Sounds.Shot(weapon, from);
        }

        // ------------------------------------------------------------------ railgun

        private void UpdateRailgun(TrackedShipClient ship, StatefulPart part, Mount m, bool on, bool owner)
        {
            float dt = Time.deltaTime;
            bool charging = owner && on && Time.time >= m.NextShot;
            m.Charge = charging ? m.Charge + dt / 1.2f : Mathf.Max(0f, m.Charge - dt);
            if (m.Lens != null)
            {
                float reload = Time.time < m.NextShot ? 0.15f : 1f;
                m.Lens.material.SetColor("_EmissionColor", new Color(0.5f, 0.85f, 1f) * (1f + m.Charge * 10f) * reload);
            }
            Vector3 origin = PartPoint(ship, part, new Vector3(0, 0.27f, 0.8f));
            if (charging && !m.ChargeLoop.hasHandle()) m.ChargeLoop = Sounds.Loop(SoundStyle.Charge, origin);
            if (m.ChargeLoop.hasHandle())
            {
                if (!charging) Sounds.Stop(ref m.ChargeLoop);
                else
                {
                    Sounds.Move(m.ChargeLoop, origin);
                    Sounds.SetPitch(m.ChargeLoop, 0.6f + m.Charge);
                }
            }
            if (m.Charge < 1f) return;

            m.Charge = 0f;
            m.NextShot = Time.time + 4f;
            Sounds.Stop(ref m.ChargeLoop);
            Vector3 fwd = PartForward(ship, part);
            _impacts.Clear();
            Vector3 end = Ballistics.Fire(origin, fwd, RailSlug, _impacts, RootBody(ship), WeaponKind.ShipRailgun);
            Vector3 from = VisualPoint(m, 0, origin);
            WeaponFx.Shot(WeaponKind.ShipRailgun, from, end, m.Points != null && m.Points.Length > 0 ? m.Points[0] : null);
            Ballistics.PlayImpacts(_impacts, 2.2f);
            Ballistics.RelayShot(WeaponKind.ShipRailgun, from, end, _impacts);
        }

        // ------------------------------------------------------------------ torpedo

        private void UpdateTorpedo(TrackedShipClient ship, StatefulPart part, Mount m, bool on, bool owner)
        {
            if (m.Missiles > 1) m.Missiles = 1;
            if (m.Missiles < 1)
            {
                if (m.ReloadAt <= 0f) m.ReloadAt = Time.time + 15f;
                else if (Time.time >= m.ReloadAt)
                {
                    m.Missiles = 1;
                    m.ReloadAt = 0f;
                }
            }
            if (m.Tube != null) m.Tube.gameObject.SetActive(!owner || m.Missiles > 0);

            Vector3 fwd = PartForward(ship, part);
            Vector3 from = PartPoint(ship, part, new Vector3(0, 0.38f, 0.9f));
            m.LockedTarget = owner ? FindTarget(ship, from, fwd) : 0;
            if (owner && (on || PilotedByMe(ship))) ThreatWarning.ReportLock(m.LockedTarget, true, from);
            if (!owner || !on || m.LastSignal || m.Missiles <= 0) return;
            m.Missiles = 0;
            Rigidbody body = RootBody(ship);
            Vector3 vel = (body != null ? body.velocity : Vector3.zero) + fwd * Projectiles.Specs[ProjectileKind.Torpedo].Speed;
            Projectiles.Launch(ProjectileKind.Torpedo, from, vel, m.LockedTarget, RootOf(ship.Id));
            Sounds.MissileLaunch(from);
            Fx.Puff(from, 0.5f, new Color(0.8f, 0.78f, 0.75f), 1.5f, 4, fwd * 2f);
        }

        // ------------------------------------------------------------------ hypersonic launcher

        private const int HyperRounds = 2;

        private void UpdateHyper(TrackedShipClient ship, StatefulPart part, Mount m, bool on, bool owner)
        {
            if (m.Missiles > HyperRounds) m.Missiles = HyperRounds;
            if (m.Missiles < HyperRounds)
            {
                if (m.ReloadAt <= 0f) m.ReloadAt = Time.time + 10f;
                else if (Time.time >= m.ReloadAt)
                {
                    m.Missiles++;
                    m.ReloadAt = m.Missiles < HyperRounds ? Time.time + 10f : 0f;
                }
            }
            if (m.Points != null)
                for (int i = 0; i < m.Points.Length; i++)
                    if (m.Points[i] != null) m.Points[i].gameObject.SetActive(!owner || i < m.Missiles);
            if (m.Lens != null)
                m.Lens.material.SetColor("_EmissionColor", new Color(0.4f, 0.75f, 1f) * (m.LockedTarget != 0 ? 3f + Mathf.PingPong(Time.time * 16f, 6f) : 1.5f));

            Vector3 fwd = PartForward(ship, part);
            Vector3 center = PartPoint(ship, part, new Vector3(0, 0.345f, 0.7f));
            m.LockedTarget = owner ? FindFarTarget(ship, center, fwd, 30f, 8000f) : 0;
            if (owner && (on || PilotedByMe(ship))) ThreatWarning.ReportLock(m.LockedTarget, true, center);
            if (!owner || !on || m.LastSignal || m.Missiles <= 0 || Time.time < m.NextShot) return; // rising edge of the signal
            m.NextShot = Time.time + 0.4f;
            m.Missiles--;
            float x = m.Missiles == 1 ? 0.11f : -0.11f; // right rail first (the model hides missiles from the end of the list)
            Vector3 from = PartPoint(ship, part, new Vector3(x, 0.345f, 0.75f));
            Rigidbody body = RootBody(ship);
            Vector3 vel = (body != null ? body.velocity : Vector3.zero) + fwd * Projectiles.Specs[ProjectileKind.Hyper].Speed;
            Projectiles.Launch(ProjectileKind.Hyper, from, vel, m.LockedTarget, RootOf(ship.Id));
            Sounds.MissileLaunch(from);
            Sounds.Shot(SoundStyle.Rail, from);
            Fx.MuzzleFlash(from, fwd, 0.7f);
            Fx.Puff(PartPoint(ship, part, new Vector3(x, 0.4f, -0.55f)), 0.35f, new Color(0.8f, 0.78f, 0.75f), 1.8f, 5, -fwd * 6f); // back-blast on the deflector
        }

        // ------------------------------------------------------------------ rocket pod

        private static readonly Vector3[] PodRocketTubes = BuildRocketTubes();

        private static Vector3[] BuildRocketTubes()
        {
            var t = new Vector3[9];
            for (int r = 0; r < 3; r++)
                for (int c = 0; c < 3; c++)
                    t[r * 3 + c] = new Vector3(-0.12f + c * 0.12f, 0.22f + r * 0.11f, 0.42f);
            return t;
        }

        private void UpdateRocketPod(TrackedShipClient ship, StatefulPart part, Mount m, bool on, bool owner)
        {
            if (m.Rockets < 0) m.Rockets = 9;
            // reloads the whole pod once emptied, or after a pause in firing
            if (m.Rockets < 9 && (m.Rockets == 0 || !on))
            {
                if (m.ReloadAt <= 0f) m.ReloadAt = Time.time + (m.Rockets == 0 ? 12f : 8f);
                else if (Time.time >= m.ReloadAt)
                {
                    m.Rockets = 9;
                    m.ReloadAt = 0f;
                    if (owner) Sounds.Reload(PartPoint(ship, part, new Vector3(0, 0.3f, 0)));
                }
            }
            else if (on) m.ReloadAt = 0f;
            if (m.Points != null)
                for (int i = 0; i < m.Points.Length; i++)
                    if (m.Points[i] != null) m.Points[i].gameObject.SetActive(!owner || i >= 9 - m.Rockets);

            if (!owner || !on || m.Rockets <= 0 || Time.time < m.NextShot) return;
            m.NextShot = Time.time + 0.13f;
            int tube = Mathf.Clamp(9 - m.Rockets, 0, 8);
            m.Rockets--;
            Vector3 from = PartPoint(ship, part, PodRocketTubes[tube]);
            Vector3 fwd = PartForward(ship, part);
            Vector3 dir = Quaternion.AngleAxis(Random.Range(0f, 360f), fwd) * Quaternion.AngleAxis(Random.Range(0f, 1.2f), Vector3.Cross(fwd, Fx.Up).normalized) * fwd;
            Rigidbody body = RootBody(ship);
            Vector3 vel = (body != null ? body.velocity : Vector3.zero) + dir * Projectiles.Specs[ProjectileKind.Rocket].Speed;
            Projectiles.Launch(ProjectileKind.Rocket, from, vel, 0, RootOf(ship.Id));
            Fx.Puff(from, 0.18f, new Color(0.8f, 0.78f, 0.75f), 1.2f, 2, dir * 2f);
            Fx.Puff(PartPoint(ship, part, PodRocketTubes[tube] - new Vector3(0, 0, 0.75f)), 0.25f, new Color(0.8f, 0.78f, 0.75f), 1.4f, 2, -dir * 4f); // back-blast
            Sounds.Shot(WeaponKind.RocketLauncher, from);
        }

        // ------------------------------------------------------------------ SAM battery

        private const float SamRange = 1600f;

        private void UpdateSamBattery(TrackedShipClient ship, StatefulPart part, Mount m, bool on, bool owner)
        {
            float dt = Time.deltaTime;
            uint root = RootOf(ship.Id);
            bool powered = !Effects.IsShipEmp(ship.Id);
            if (m.Radar != null && powered) m.Radar.Rotate(0f, (on ? 220f : 90f) * dt, 0f, Space.Self);
            if (m.Lock == null) m.Lock = new LockOn { Cone = 5f, KeepCone = 14f, LockTime = 1.4f, Range = SamRange, MinRange = 25f, Filter = Airborne };

            // reload one missile at a time
            if (m.Missiles < 4)
            {
                if (m.ReloadAt <= 0f) m.ReloadAt = Time.time + 8f;
                else if (Time.time >= m.ReloadAt)
                {
                    m.Missiles++;
                    m.ReloadAt = m.Missiles < 4 ? Time.time + 8f : 0f;
                }
            }
            if (m.Points != null)
                for (int i = 0; i < m.Points.Length; i++)
                    if (m.Points[i] != null) m.Points[i].gameObject.SetActive(!owner || i < m.Missiles);

            // pick what to aim at: the ship being locked, else the closest aircraft above the mount
            Vector3 basePos = PartPoint(ship, part, new Vector3(0, 0.32f, 0));
            Vector3 up = ship.ShipRigidBody.transform.TransformDirection(part.LocalRotation * Vector3.up);
            Vector3 aimDir = PartForward(ship, part);
            uint target = powered ? (m.Lock.Candidate != 0 ? m.Lock.Candidate : NearestAircraft(root, basePos, up)) : 0;
            if (target != 0 && Svc.ShipsClient.TryGetTrackedShip(target, out TrackedShipClient t) && t.IsRigidbodyLoaded)
            {
                Vector3 to = TargetPoint(t) - basePos;
                if (Vector3.Dot(to.normalized, up) > -0.15f) aimDir = to.normalized;
            }
            AimHead(m, aimDir, dt * 0.6f);
            Vector3 gunFwd = m.Pitch != null ? m.Pitch.forward : aimDir;

            if (powered && owner) m.Lock.Update(basePos, gunFwd, root, dt, GunnerSeat.TryGetPilotedRoot(out uint piloted) && piloted == root);
            else m.Lock.Clear();
            if (owner && GunnerSeat.TryGetPilotedRoot(out uint me) && me == root) LockHud.Show(m.Lock);

            if (!owner || !on || m.Lock.Locked == 0 || m.Missiles <= 0 || Time.time < m.NextShot) return;
            int tube = Mathf.Clamp(4 - m.Missiles, 0, 3);
            Vector3 from = m.Points != null && tube < m.Points.Length && m.Points[tube] != null ? m.Points[tube].position + gunFwd * 0.15f : basePos + gunFwd * 0.6f;
            // hold fire while the launcher is not on target yet, or the first stretch of the flight is blocked (ridge, rocks...)
            if (!Svc.ShipsClient.TryGetTrackedShip(m.Lock.Locked, out TrackedShipClient locked) || !locked.IsRigidbodyLoaded) return;
            Vector3 toLocked = TargetPoint(locked) - from;
            if (Vector3.Angle(gunFwd, toLocked) > 12f || !ClearLine(from, from + gunFwd * Mathf.Min(30f, toLocked.magnitude), root, RootOf(locked.Id)))
            {
#if DEV
                ArsenalMod.Log.Msg($"[DEV] SAM holds fire: off-axis {Vector3.Angle(gunFwd, toLocked):0} deg or launch path blocked");
                int nh = ClientScene.PhysicsScene.Raycast(from, gunFwd, LosHits, Mathf.Min(30f, toLocked.magnitude), Ballistics.Mask, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < nh; i++)
                    ArsenalMod.Log.Msg($"[DEV]   hit {LosHits[i].collider.name} layer {LosHits[i].collider.gameObject.layer} at {LosHits[i].distance:0.0} root {HitRoot(LosHits[i])} (me {root}, target {RootOf(locked.Id)})");
#endif
                m.NextShot = Time.time + 0.5f;
                return;
            }
            m.NextShot = Time.time + 1.2f;
            m.Missiles--;
            Rigidbody body = RootBody(ship);
            Vector3 vel = (body != null ? body.velocity : Vector3.zero) + gunFwd * Projectiles.Specs[ProjectileKind.Sam].Speed;
            Projectiles.Launch(ProjectileKind.Sam, from, vel, m.Lock.Locked, root);
            Sounds.MissileLaunch(from);
            Fx.Puff(from - gunFwd * 0.7f, 0.3f, new Color(0.8f, 0.78f, 0.75f), 1.6f, 4, -gunFwd * 4f);
        }

        // ------------------------------------------------------------------ flare dispenser

        private const int FlareSalvos = 4;

        private void UpdateFlares(TrackedShipClient ship, StatefulPart part, Mount m, bool on, bool owner)
        {
            if (m.Missiles > FlareSalvos) m.Missiles = FlareSalvos;
            // reload one salvo at a time
            if (m.Missiles < FlareSalvos)
            {
                if (m.ReloadAt <= 0f) m.ReloadAt = Time.time + 10f;
                else if (Time.time >= m.ReloadAt)
                {
                    m.Missiles++;
                    m.ReloadAt = m.Missiles < FlareSalvos ? Time.time + 10f : 0f;
                }
            }
            if (m.Lens != null)
                m.Lens.material.SetColor("_EmissionColor", new Color(1f, 0.55f, 0.2f) * (m.FlaresLeft > 0 ? 9f : m.Missiles > 0 ? 2.5f : 0.4f));
            if (!owner) return;

            // a salvo goes out in pairs, one from each bank
            if (m.FlaresLeft > 0)
            {
                if (Time.time < m.NextShot) return;
                m.NextShot = Time.time + 0.12f;
                Rigidbody body = RootBody(ship);
                Vector3 shipVel = body != null ? body.velocity : Vector3.zero;
                var batch = new List<(Vector3, Vector3)>(2);
                for (int k = 0; k < 2 && m.FlaresLeft > 0; k++)
                {
                    int tube = (PartModels.FlareTubeCount - m.FlaresLeft) / 2 + (k == 0 ? 0 : 3);
                    m.FlaresLeft--;
                    Vector3 local = PartModels.FlareTube(tube, out Vector3 localDir);
                    Vector3 dir = ship.ShipRigidBody.transform.TransformDirection(part.LocalRotation * localDir);
                    dir = (dir + Random.insideUnitSphere * 0.15f).normalized;
                    batch.Add((PartPoint(ship, part, local + localDir * 0.1f), shipVel + dir * Random.Range(20f, 26f)));
                }
                Flares.Launch(batch);
                return;
            }
            // one salvo per pulse, or every 1.5 s while the signal stays on
            if (!on || m.Missiles <= 0 || (m.LastSignal && Time.time < m.NextSalvo) || Time.time < m.NextShot) return;
            m.Missiles--;
            m.FlaresLeft = PartModels.FlareTubeCount;
            m.NextSalvo = Time.time + 1.5f;
            m.NextShot = Time.time;
        }

        /// <summary>Flying, not sitting on the ground: nothing solid within a few metres under its centre of mass.</summary>
        public static bool Airborne(TrackedShipClient s)
        {
            Vector3 p = TargetPoint(s);
            Vector3 down = -Fx.Up;
            var hits = new RaycastHit[8];
            int n = ClientScene.PhysicsScene.Raycast(p, down, hits, 5f, Ballistics.Mask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
                if (!IsOwnShip(hits[i], s.Id)) return false; // ground, or something it rests on
            return true;
        }

        /// <summary>Closest other ship assembly above the mount, within SAM range.</summary>
        private static uint NearestAircraft(uint myRoot, Vector3 from, Vector3 up)
        {
            uint best = 0;
            float bestD = SamRange;
            foreach (TrackedShipClient s in Svc.ShipsClient.Ships)
            {
                if (!s.IsRigidbodyLoaded || RootOf(s.Id) != s.Id || s.Id == myRoot) continue;
                Vector3 to = TargetPoint(s) - from;
                float d = to.magnitude;
                if (d < 25f || d >= bestD || Vector3.Dot(to / d, up) < -0.15f || !Airborne(s) || !CanSee(from, s, myRoot)) continue;
                bestD = d;
                best = s.Id;
            }
            return best;
        }

        // ------------------------------------------------------------------ laser

        private void UpdateLaser(TrackedShipClient ship, StatefulPart part, Mount m, bool on, bool owner)
        {
            float dt = Time.deltaTime;
            bool firing = owner && on && !m.Overheated;
            if (firing)
            {
                m.Heat += dt / 6f;
                if (m.Heat >= 1f)
                {
                    m.Heat = 1f;
                    m.Overheated = true;
                }
            }
            else
            {
                m.Heat = Mathf.Max(0f, m.Heat - dt / 4f);
                if (m.Overheated && m.Heat < 0.25f) m.Overheated = false;
            }
            if (m.Lens != null)
                m.Lens.material.SetColor("_EmissionColor", Color.Lerp(new Color(0.3f, 0.8f, 1f), new Color(1f, 0.35f, 0.15f), m.Heat) * (firing ? 8f : 2f));

            if (!firing)
            {
                StopBeam(m);
                return;
            }
            Vector3 origin = PartPoint(ship, part, LaserMuzzle);
            Vector3 fwd = PartForward(ship, part);
            Vector3 end = Laser.Trace(origin, fwd, RootBody(ship), m);
            Vector3 from = VisualPoint(m, 0, origin);
            DrawBeam(m, from, end);
            if (!m.Loop.hasHandle()) m.Loop = Sounds.LaserLoop(from);
            else Sounds.Move(m.Loop, from);
            if (Time.time >= m.NextRelay)
            {
                m.NextRelay = Time.time + 0.1f;
                FxNet.RelayBeam(from, end, 0);
            }
        }

        public static Material BeamMaterial
        {
            get
            {
                if (_beamMat != null) return _beamMat;
                _beamMat = Assets.Glow(new Color(1f, 0.25f, 0.12f), 12f);
                return _beamMat;
            }
        }

        private static void DrawBeam(Mount m, Vector3 from, Vector3 to)
        {
            if (m.Beam == null)
            {
                var go = new GameObject("Arsenal.LaserBeam");
                m.Beam = go.AddComponent<LineRenderer>();
                m.Beam.sharedMaterial = BeamMaterial;
                m.Beam.positionCount = 2;
                m.Beam.useWorldSpace = true;
                m.Beam.numCapVertices = 3;
                m.Beam.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                m.BeamLight = new GameObject("Arsenal.LaserLight").AddComponent<Light>();
                m.BeamLight.type = LightType.Point;
                m.BeamLight.color = new Color(1f, 0.4f, 0.2f);
                m.BeamLight.range = 3f;
                m.BeamLight.intensity = 3f;
                m.BeamLight.shadows = LightShadows.None;
            }
            float w = 0.09f + Mathf.Sin(Time.time * 60f) * 0.015f;
            m.Beam.startWidth = w;
            m.Beam.endWidth = w * 0.8f;
            m.Beam.SetPosition(0, from);
            m.Beam.SetPosition(1, to);
            m.BeamLight.transform.position = to - (to - from).normalized * 0.2f;
        }

        public static void StopBeam(Mount m)
        {
            if (m.Beam != null) Destroy(m.Beam.gameObject);
            if (m.BeamLight != null) Destroy(m.BeamLight.gameObject);
            m.Beam = null;
            m.BeamLight = null;
            Sounds.Stop(ref m.Loop);
            Sounds.Stop(ref m.ChargeLoop);
        }
    }
}
