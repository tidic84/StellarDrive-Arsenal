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
        }

        // muzzle offsets in part space (must match PartModels)
        private static readonly Vector3[] GunMuzzles = { new Vector3(-0.065f, 0.27f, 0.8f), new Vector3(0.065f, 0.27f, 0.8f) };
        private static readonly Vector3[] PodTubes = { new Vector3(-0.11f, 0.42f, 0.36f), new Vector3(0.11f, 0.42f, 0.36f), new Vector3(-0.11f, 0.24f, 0.36f), new Vector3(0.11f, 0.24f, 0.36f) };
        private static readonly Vector3 LaserMuzzle = new Vector3(0, 0.27f, 0.67f);

        public static readonly BulletSpec GunBullet = new BulletSpec { PlayerDamage = 28, PanelDamage = 16, Penetration = 50, HoleRadius = 0.045f, Range = 700f, ObjectDamage = 1f };

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
                    bool on = signal > 0.5;
                    owner ??= IsOwner(ship);
                    ResolveVisual(m);
                    try
                    {
                        switch (m.Kind)
                        {
                            case ShipContent.MachineGunId: UpdateGun(ship, part, m, on, owner.Value); break;
                            case ShipContent.MissilePodId: UpdatePod(ship, part, m, on, owner.Value); break;
                            case ShipContent.LaserCannonId: UpdateLaser(ship, part, m, on, owner.Value); break;
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

        /// <summary>Physics-space pose of a point of the part (from the ship rigidbody, same space as the raycasts).</summary>
        public static Vector3 PartPoint(TrackedShipClient ship, StatefulPart part, Vector3 local) =>
            ship.ShipRigidBody.transform.TransformPoint(part.LocalPosition + part.LocalRotation * local);

        public static Vector3 PartForward(TrackedShipClient ship, StatefulPart part) =>
            ship.ShipRigidBody.transform.TransformDirection(part.LocalRotation * Vector3.forward);

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
            string pointName = m.Kind == ShipContent.MissilePodId ? "Tube" : "Muzzle";
            m.Points = c.GetComponentsInChildren<Transform>(true).Where(t => t.name == pointName).ToArray();
            m.Lens = c.GetComponentsInChildren<Renderer>(true).FirstOrDefault(r => r.name == "Lens");
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
#if DEV
            if (Time.frameCount % 20 == 0)
                ArsenalMod.Log.Msg($"[DEV] gun origin {origin} visual {from} end {end} fwd {fwd} rb {ship.ShipRigidBody.transform.position} part {part.LocalPosition}");
#endif
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

        /// <summary>Nearest other ship inside a 25 degree cone in front of the pod (up to 1.5 km).</summary>
        public static uint FindTarget(TrackedShipClient self, Vector3 from, Vector3 fwd)
        {
            uint myRoot = RootOf(self.Id);
            uint best = 0;
            float bestScore = float.MaxValue;
            foreach (TrackedShipClient s in Svc.ShipsClient.Ships)
            {
                if (!s.IsRigidbodyLoaded || RootOf(s.Id) == myRoot || RootOf(s.Id) != s.Id) continue;
                Vector3 to = TargetPoint(s) - from;
                float d = to.magnitude;
                if (d < 5f || d > 1500f) continue;
                float angle = Vector3.Angle(fwd, to);
                if (angle > 25f) continue;
                float score = angle * 20f + d * 0.05f;
                if (score < bestScore)
                {
                    bestScore = score;
                    best = s.Id;
                }
            }
            return best;
        }

        /// <summary>Centre of mass of a ship in the client physics scene.</summary>
        public static Vector3 TargetPoint(TrackedShipClient s)
        {
            Rigidbody rb = s.ShipRigidBody.GetComponent<Rigidbody>();
            return rb != null ? rb.worldCenterOfMass : s.ShipRigidBody.transform.position;
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
                uint space = ClientOrigin.LocalPhysicsSpaceId;
                ArsenalNet.SendToServer(Op.FxRelay, w =>
                {
                    w.Write((byte)FxType.Beam);
                    w.Write(space);
                    w.Write(from);
                    w.Write(end);
                }, FishNet.Transporting.Channel.Unreliable);
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
        }
    }
}
