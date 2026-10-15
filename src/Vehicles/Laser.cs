using System.Collections.Generic;
using System.IO;
using System.Linq;
using Arsenal.Compat;
using Arsenal.Core;
using Arsenal.Damage;
using Arsenal.Net;
using Arsenal.Visual;
using Arsenal.Weapons;
using FishNet.Connection;
using Planets.Interface.Model;
using Ships;
using Ships.Interface.Model.Parts;
using Ships.Interface.Services;
using UnityEngine;

namespace Arsenal.Vehicles
{
    /// <summary>One colour of laser cannon: its bolts' look and how it shoots.</summary>
    internal sealed class LaserStyle
    {
        public string NameFr, NameEn, DescFr, DescEn;
        public Color Halo, Core, Lens, Ember;
        public float HaloGlow, BloomGlow;     // tuned per colour: the tonemapper washes bright red out sooner than green
        public float Width = 1f, Length = 6f;
        public float Speed, Interval, Heat;
        public float PanelDamage, PlayerDamage, ObjectDamage, HoleRadius;
        /// <summary>
        /// The bolt's physical punch on a ship, decided by the host: power against the machinery around the impact
        /// (thrusters, tanks, seats... see <see cref="Damage.PartDamage"/>) and the push it gives the ship (N·s).
        /// </summary>
        public float PartPower, Impulse;
        public float Pitch = 1f;              // of the blaster sound
        public bool Hand;                     // a hand blaster's bolt (its own sound and kill-feed weapon)
        public Weapons.WeaponKind Kind = Weapons.WeaponKind.ShipLaser;
        public Material HaloMat, BloomMat;
    }

    /// <summary>
    /// The laser cannons fire blaster bolts, starfighter style: a white-hot core in a coloured halo, a few
    /// metres long, flying slow enough to be seen and lighting up what they pass. A bolt stops on the first
    /// solid surface and burns it; on hull panels it melts a hole, and a bolt that meets an existing hole flies on.
    /// </summary>
    internal static class Laser
    {
        public const float Range = 520f;

        /// <summary>0 = red (the original cannon), 1 = green, 2 = blue.</summary>
        public static readonly LaserStyle[] Styles =
        {
            new LaserStyle
            {
                NameFr = "Canon laser rouge", NameEn = "Red laser cannon",
                DescFr = "Tire des salves de traits laser rouges tant que le signal est actif. Chaque tir fait fondre la coque et peut la percer. Surchauffe après une longue rafale.",
                DescEn = "Fires a stream of red laser bolts while the signal is on. Each bolt melts the hull and can burn through. Overheats after a long burst.",
                Halo = new Color(1f, 0.02f, 0.01f), Core = new Color(1f, 0.85f, 0.82f), Lens = new Color(1f, 0.15f, 0.08f), Ember = new Color(1f, 0.45f, 0.15f),
                HaloGlow = 1.8f, BloomGlow = 0.7f,
                Speed = 170f, Interval = 0.16f, Heat = 0.06f,
                PanelDamage = 30f, PlayerDamage = 20f, ObjectDamage = 1.6f, HoleRadius = 0.06f, PartPower = 180f, Impulse = 700f,
            },
            new LaserStyle
            {
                NameFr = "Canon laser vert « Chasseur »", NameEn = "\"Hunter\" green laser cannon",
                DescFr = "Canon d'intercepteur : traits laser verts, plus rapides et plus nerveux mais moins puissants. Chauffe moins vite.",
                DescEn = "Interceptor cannon: green laser bolts, faster and quicker-firing but lighter. Heats up more slowly.",
                Halo = new Color(0.05f, 1f, 0.04f), Core = new Color(0.85f, 1f, 0.82f), Lens = new Color(0.15f, 1f, 0.1f), Ember = new Color(0.6f, 1f, 0.3f),
                HaloGlow = 1.3f, BloomGlow = 0.5f, Width = 0.85f, Length = 5f,
                Speed = 200f, Interval = 0.11f, Heat = 0.035f,
                PanelDamage = 20f, PlayerDamage = 14f, ObjectDamage = 1.1f, HoleRadius = 0.05f, PartPower = 120f, Impulse = 450f, Pitch = 1.15f,
            },
            new LaserStyle
            {
                NameFr = "Canon laser bleu « Turbo »", NameEn = "\"Turbo\" blue laser cannon",
                DescFr = "Canon lourd : gros traits laser bleus, plus lents et espacés mais qui frappent fort et ouvrent de larges brèches. Surchauffe vite.",
                DescEn = "Heavy cannon: big blue laser bolts, slower and spaced out but hitting hard and opening wide breaches. Overheats quickly.",
                Halo = new Color(0.04f, 0.3f, 1f), Core = new Color(0.82f, 0.9f, 1f), Lens = new Color(0.1f, 0.45f, 1f), Ember = new Color(0.45f, 0.7f, 1f),
                HaloGlow = 2.2f, BloomGlow = 1f, Width = 1.6f, Length = 7.5f,
                Speed = 150f, Interval = 0.32f, Heat = 0.11f,
                PanelDamage = 60f, PlayerDamage = 40f, ObjectDamage = 3.2f, HoleRadius = 0.1f, PartPower = 380f, Impulse = 1500f, Pitch = 0.78f,
            },
            // 3-6: the hand blasters' bolts — short, thin, slow enough to see coming
            new LaserStyle
            {
                Halo = new Color(1f, 0.02f, 0.01f), Core = new Color(1f, 0.85f, 0.82f), Lens = new Color(1f, 0.15f, 0.08f), Ember = new Color(1f, 0.45f, 0.15f),
                HaloGlow = 1.8f, BloomGlow = 0.7f, Width = 0.55f, Length = 1.6f,
                Speed = 110f, PanelDamage = 16f, PlayerDamage = 34f, ObjectDamage = 0.9f, HoleRadius = 0.04f, PartPower = 60f, Impulse = 40f,
                Pitch = 0.9f, Hand = true, Kind = Weapons.WeaponKind.BlasterPistol,
            },
            new LaserStyle
            {
                Halo = new Color(1f, 0.02f, 0.01f), Core = new Color(1f, 0.85f, 0.82f), Lens = new Color(1f, 0.15f, 0.08f), Ember = new Color(1f, 0.45f, 0.15f),
                HaloGlow = 1.8f, BloomGlow = 0.7f, Width = 0.45f, Length = 1.8f,
                Speed = 125f, PanelDamage = 10f, PlayerDamage = 19f, ObjectDamage = 0.6f, HoleRadius = 0.032f, PartPower = 40f, Impulse = 25f,
                Pitch = 1.05f, Hand = true, Kind = Weapons.WeaponKind.BlasterRifle,
            },
            new LaserStyle
            {
                Halo = new Color(0.05f, 1f, 0.04f), Core = new Color(0.85f, 1f, 0.82f), Lens = new Color(0.15f, 1f, 0.1f), Ember = new Color(0.6f, 1f, 0.3f),
                HaloGlow = 1.3f, BloomGlow = 0.5f, Width = 0.4f, Length = 1.5f,
                Speed = 140f, PanelDamage = 8f, PlayerDamage = 15f, ObjectDamage = 0.5f, HoleRadius = 0.028f, PartPower = 30f, Impulse = 18f,
                Pitch = 1.15f, Hand = true, Kind = Weapons.WeaponKind.BlasterCarbine,
            },
            new LaserStyle
            {
                Halo = new Color(0.04f, 0.3f, 1f), Core = new Color(0.82f, 0.9f, 1f), Lens = new Color(0.1f, 0.45f, 1f), Ember = new Color(0.45f, 0.7f, 1f),
                HaloGlow = 2.2f, BloomGlow = 1f, Width = 0.6f, Length = 2.2f,
                Speed = 105f, PanelDamage = 22f, PlayerDamage = 24f, ObjectDamage = 1.2f, HoleRadius = 0.045f, PartPower = 90f, Impulse = 70f,
                Pitch = 0.75f, Hand = true, Kind = Weapons.WeaponKind.HeavyBlaster,
            },
        };

        /// <summary>Style indices of the hand blasters' bolts.</summary>
        public const byte PistolBolt = 3, RifleBolt = 4, CarbineBolt = 5, HeavyBolt = 6;

        public static LaserStyle Style(byte style) => Styles[style < Styles.Length ? style : 0];

        public static byte StyleOf(ushort partId) => partId == Content.ShipContent.LaserGreenId ? (byte)1 : partId == Content.ShipContent.LaserBlueId ? (byte)2 : (byte)0;

        /// <summary>A glow tinted all through (the shared glow keeps a white base, which washes the colour out to pastel).</summary>
        public static Material TintedGlow(ref Material cache, Color c, float intensity)
        {
            if (cache != null) return cache;
            cache = Assets.Glow(c, intensity);
            cache.SetColor("_BaseColor", c);
            cache.SetColor("_Color", c);
            return cache;
        }

        public static void Init()
        {
            ArsenalNet.OnServer(Op.BoltImpact, ServerOnBoltImpact);
        }

        /// <summary>Fire a bolt. Only the owner's copy does damage; the others are for the eyes.</summary>
        public static void Fire(Vector3 from, Vector3 velocity, uint shooterRoot, bool owner, byte style)
        {
            LaserStyle s = Style(style);
            BlasterBolt.Take(from).Init(from, velocity, shooterRoot, owner, style);

            // muzzle: hot white pop inside a coloured bloom that lights the gun up
            Vector3 dir = velocity.normalized;
            Fx.GlowFlash(from + dir * 0.05f, s.Core, 0.1f * s.Width, 0.05f, glow: 10f);
            Fx.GlowFlash(from + dir * 0.12f, s.Halo, 0.28f * s.Width, 0.08f, 9f, 7f, s.HaloGlow * 1.4f);
            if (s.Hand) Sounds.HandBlaster(from, s.Pitch);
            else Sounds.Blaster(from, s.Pitch);
        }

        /// <summary>
        /// A bolt reached a surface. Returns true if it went through an existing hole and flies on. A bolt melts a
        /// hole wherever it lands on a hull (glass bursts wide, iron melts least), and on a ship it also wrecks the
        /// machinery around the impact and pushes the ship (decided by the host, see <see cref="ServerImpact"/>).
        /// </summary>
        public static bool Hit(RaycastHit hit, Vector3 dir, bool owner, LaserStyle s)
        {
            Collider col = hit.collider;
            if (ShipPartsTagDetection.ColliderIsHull(col) && Ballistics.TryResolvePanel(hit, out PanelKey key, out HullPart part, out Vector2 local))
            {
                bool holeAble = HullDamage.CanHaveHoles(part);
                if (holeAble && HullDamage.TryGetClientHoles(key, out HoleMask existing) && existing.IsHoleAt(local.x, local.y))
                    return true;
                if (owner)
                {
                    var holes = new HoleMask();
                    if (holeAble) holes = holes.PunchDisc(local.x, local.y, s.HoleRadius * Materials.HoleScale(part.MaterialType), Ballistics.Rng);
                    HullDamage.ReportHit(key, holes, s.PanelDamage);
                    HullDamage.PredictHoles(key, holes);
                }
            }
            else if (owner)
            {
                IMinableObject minable = col.GetComponentInParent<IMinableObject>();
                if (minable != null)
                    try { minable.ReduceHealth(s.ObjectDamage); } catch { }
            }
            if (owner) ReportImpact(hit, dir, s);
            Splash(hit.point, hit.normal, dir, s);
            return false;
        }

        /// <summary>A bolt stopped on a force field (TF Advanced): it splashes on the shell, which takes the hit.</summary>
        public static void HitShield(ForceFields.Bubble bubble, Vector3 point, Vector3 normal, bool owner, LaserStyle s)
        {
            ForceFields.Struck(bubble, point, normal, Mathf.Clamp(s.Width * 1.2f, 0.5f, 1.6f), owner, s.PanelDamage);
            Fx.GlowFlash(point + normal * 0.05f, s.Halo, 0.35f * s.Width, 0.12f, 10f, 7f, s.HaloGlow * 1.2f);
            Sounds.BlasterHit(point);
        }

        /// <summary>The bolt's punch on a ship, pinned to the ship's frame so the host lands it on its own pose.</summary>
        private static void ReportImpact(RaycastHit hit, Vector3 dir, LaserStyle s)
        {
            if (s.PartPower <= 0f && s.Impulse <= 0f) return;
            IShipPartHelper helper = hit.collider != null ? hit.collider.GetComponentInParent<IShipPartHelper>() : null;
            if (helper == null || !Svc.ShipsClient.TryGetTrackedShip(helper.ShipId, out TrackedShipClient ship) || !ship.IsRigidbodyLoaded) return;
            Transform t = ship.ShipRigidBody.transform;
            Vector3 local = t.InverseTransformPoint(hit.point), localDir = t.InverseTransformDirection(dir);
            byte style = (byte)System.Array.IndexOf(Styles, s);
            ArsenalNet.SendToServer(Op.BoltImpact, w =>
            {
                w.Write(ship.Id);
                w.Write(local);
                w.Write(localDir);
                w.Write(style);
            });
        }

        private static void ServerOnBoltImpact(NetworkConnection sender, BinaryReader r)
        {
            uint shipId = r.ReadUInt32();
            Vector3 local = r.ReadVector3(), dir = r.ReadVector3();
            byte style = r.ReadByte();
            ServerImpact(shipId, local, dir, Style(style));
        }

        /// <summary>
        /// Host: a bolt struck a ship at <paramref name="local"/> (its frame). The machinery within reach takes the
        /// bolt's power (enough hits destroy a thruster or a tank), and the ship gets a shove along the bolt from
        /// that point (off-centre hits set it turning). A force field takes the hit instead.
        /// </summary>
        public static void ServerImpact(uint shipId, Vector3 local, Vector3 dir, LaserStyle s)
        {
            if (Svc.ShipsServer == null || !Svc.ShipsServer.TryGetTrackedShip(shipId, out TrackedShipServer ship) || dir.sqrMagnitude < 1e-6f) return;
            if (ForceFields.ServerShields(ship, local))
            {
                ForceFields.ServerAbsorb(ship.Id, s.PanelDamage * ForceFields.PanelPoints);
                return;
            }
            if (s.PartPower > 0f) PartDamage.ApplyBlast(ship, local, 0.8f, s.PartPower);
            if (s.Impulse > 0f && HullDamage.TryGetServerWorldPose(ship, out Vector3 pos, out Quaternion rot, out _))
            {
                uint root = Svc.ShipsServer.TryGetTrackedRootShip(ship.Id, out TrackedShipServer r) && r != null ? r.Id : ship.Id;
                ShipKnockback.Queue(root, rot * dir.normalized * s.Impulse, pos + rot * local);
            }
        }

        /// <summary>A bolt struck another player: it burns (damage; a ship gun's bolt sets them smouldering a moment).</summary>
        public static void HitPlayer(uint playerId, Vector3 point, Vector3 dir, bool owner, LaserStyle s)
        {
            if (owner)
            {
                bool head = Ballistics.IsHeadshot(playerId, point);
                PlayerDamage.Report(playerId, s.PlayerDamage * (head ? 1.6f : 1f), s.Kind, head, s.Hand ? 0f : 1.5f);
            }
            Fx.GlowFlash(point, s.Core, 0.18f * s.Width, 0.07f, glow: 10f);
            Fx.GlowFlash(point, s.Halo, 0.45f * s.Width, 0.15f, 10f, 7f, s.HaloGlow * 1.4f);
            Fx.Embers(point, -dir, s.Ember, (int)(6 * s.Width) + 2, 0.03f, 4f);
            Fx.Puff(point, 0.1f, new Color(0.25f, 0.23f, 0.22f), 0.8f, 1, -dir * 0.5f);
            Sounds.BlasterHit(point);
        }

        /// <summary>The bolt splashes: a flash that lights the surface, sparks, molten droplets, a wisp of smoke.</summary>
        private static void Splash(Vector3 pos, Vector3 normal, Vector3 dir, LaserStyle s)
        {
            Vector3 p = pos + normal * 0.06f;
            Fx.GlowFlash(p, s.Core, 0.2f * s.Width, 0.08f, glow: 10f);
            Fx.GlowFlash(p, s.Halo, 0.6f * s.Width, 0.18f, 14f, 9f, s.HaloGlow * 1.4f);
            Fx.EmitSparks(pos, Vector3.Reflect(dir, normal) * 0.5f + normal * 0.5f, (int)(18 * s.Width) + 3);
            Fx.Embers(pos, normal, s.Ember, (int)(5 * s.Width) + 1, 0.035f, 5f);
            Fx.Puff(pos + normal * 0.1f, 0.12f * s.Width, new Color(0.25f, 0.23f, 0.22f), 1f, 1, normal * 0.6f);
            Sounds.BlasterHit(pos);
        }
    }

    /// <summary>One flying bolt: three glowing lines (wide faint bloom, coloured halo, white core) and a light.</summary>
    internal sealed class BlasterBolt : MonoBehaviour
    {
        private const float BloomWidth = 0.22f, HaloWidth = 0.1f, CoreWidth = 0.028f;
        private LaserStyle _s;
        private Vector3 _start, _head, _velocity;
        private float _travelled, _age;
        private uint _shooterRoot;
        private bool _owner, _stopped;
        private float _tailGone; // after a hit, how much of the bolt is left to slide into the surface
        private LineRenderer _bloom, _halo, _core;
        private Light _light;

        // spent bolts wait here for the next shot (a cannon fires several a second, each one four objects)
        private static readonly Stack<BlasterBolt> Pool = new Stack<BlasterBolt>();
        private const int MaxPooled = 256;

        public static BlasterBolt Take(Vector3 from)
        {
            while (Pool.Count > 0)
            {
                BlasterBolt b = Pool.Pop();
                if (b == null) continue; // went with the scene
                b.transform.position = from;
                b.gameObject.SetActive(true);
                return b;
            }
            var go = new GameObject("Arsenal.BlasterBolt");
            go.transform.position = from;
            return go.AddComponent<BlasterBolt>();
        }

        private void Recycle()
        {
            if (Pool.Count >= MaxPooled)
            {
                Destroy(gameObject);
                return;
            }
            gameObject.SetActive(false);
            Pool.Push(this);
        }

        public void Init(Vector3 from, Vector3 velocity, uint shooterRoot, bool owner, byte style)
        {
            _s = Laser.Style(style);
            _start = _head = from;
            _velocity = velocity;
            _shooterRoot = shooterRoot;
            _owner = owner;
            _stopped = false;
            _travelled = _age = _tailGone = 0f;
            // thin as in the films: a needle of white light in a slim coloured sheath
            if (_bloom == null)
            {
                _bloom = Line("Bloom");
                _halo = Line("Halo");
                _core = Line("Core");
                _light = gameObject.AddComponent<Light>();
                _light.type = LightType.Point;
                _light.shadows = LightShadows.None;
            }
            Style(_bloom, Laser.TintedGlow(ref _s.BloomMat, _s.Halo, _s.BloomGlow), BloomWidth * _s.Width);
            Style(_halo, Laser.TintedGlow(ref _s.HaloMat, _s.Halo, _s.HaloGlow), HaloWidth * _s.Width);
            Style(_core, Fx.GlowMat(_s.Core, 6f), CoreWidth * _s.Width);
            _light.color = Color.Lerp(_s.Halo, Color.white, 0.15f);
            _light.range = 7f * Mathf.Sqrt(_s.Width);
            _light.intensity = 5f;
            Draw(_head, _head);
        }

        private LineRenderer Line(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.positionCount = 2;
            lr.useWorldSpace = true;
            lr.numCapVertices = 4;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            return lr;
        }

        private static void Style(LineRenderer lr, Material mat, float width)
        {
            lr.sharedMaterial = mat;
            lr.startWidth = lr.endWidth = width;
        }

        private void Update()
        {
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            _age += dt;
            float speed = _velocity.magnitude;
            Vector3 dir = speed > 0.01f ? _velocity / speed : Vector3.forward;

            if (_stopped)
            {
                // the rest of the bolt pours into the impact point
                _tailGone += speed * dt;
                if (_tailGone >= _s.Length)
                {
                    Recycle();
                    return;
                }
                Draw(_head - dir * (_s.Length - _tailGone), _head);
                _light.intensity *= 0.8f;
                return;
            }

            float step = speed * dt;
            Vector3 pos = _head;
            for (int i = 0; i < 6 && step > 0.001f; i++)
            {
                if (!Ballistics.Raycast(pos, dir, step, out RaycastHit hit, out uint player))
                {
                    pos += dir * step;
                    break;
                }
                if (Ballistics.HitShield)
                {
                    Laser.HitShield(Ballistics.Shield, hit.point, hit.normal, _owner, _s);
                    _head = hit.point;
                    _stopped = true;
                    break;
                }
                if (player != 0 && _s.Hand && (hit.point - _start).sqrMagnitude < 0.8f * 0.8f)
                {
                    // a hand bolt leaving the shooter's own gun (the others see it start beside his body)
                    float pass = hit.distance + 0.1f;
                    pos += dir * pass;
                    step -= pass;
                    continue;
                }
                if (player != 0)
                {
                    Laser.HitPlayer(player, hit.point, dir, _owner, _s);
                    _head = hit.point;
                    _stopped = true;
                    break;
                }
                float skip = 0.05f;
                if (!ShipWeapons.IsOwnShip(hit, _shooterRoot))
                {
                    if (!Laser.Hit(hit, dir, _owner, _s))
                    {
                        _head = hit.point;
                        _stopped = true;
                        break;
                    }
                    skip = Mathf.Min(0.6f, 0.12f / Mathf.Max(0.2f, Mathf.Abs(Vector3.Dot(dir, hit.normal))));
                }
                // own hull, or an existing hole: carry on just past it
                float moved = hit.distance + skip;
                pos = hit.point + dir * skip;
                step -= moved;
            }
            if (!_stopped)
            {
                _travelled += (pos - _head).magnitude;
                _head = pos;
            }
            float shown = Mathf.Min(_s.Length, (_head - _start).magnitude);
            Draw(_head - dir * shown, _head);
            if (!_stopped && (_travelled >= Laser.Range || _age > 5f))
            {
                // fades out at the end of its range
                _stopped = true;
            }
        }

        private void Draw(Vector3 tail, Vector3 head)
        {
            // a flicker in the halo keeps the bolt alive
            float f = 1f + Mathf.Sin(Time.time * 90f + GetInstanceID()) * 0.08f;
            _bloom.SetPosition(0, tail); _bloom.SetPosition(1, head);
            _halo.SetPosition(0, tail); _halo.SetPosition(1, head);
            _core.SetPosition(0, tail); _core.SetPosition(1, head);
            _halo.startWidth = _halo.endWidth = HaloWidth * _s.Width * f;
            transform.position = Vector3.Lerp(tail, head, 0.7f);
        }
    }
}
