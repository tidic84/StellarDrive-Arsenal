using Arsenal.Damage;
using Arsenal.Visual;
using Arsenal.Weapons;
using Planets.Interface.Model;
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
        public float Pitch = 1f;              // of the blaster sound
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
                PanelDamage = 30f, PlayerDamage = 18f, ObjectDamage = 1.6f, HoleRadius = 0.06f,
            },
            new LaserStyle
            {
                NameFr = "Canon laser vert « Chasseur »", NameEn = "\"Hunter\" green laser cannon",
                DescFr = "Canon d'intercepteur : traits laser verts, plus rapides et plus nerveux mais moins puissants. Chauffe moins vite.",
                DescEn = "Interceptor cannon: green laser bolts, faster and quicker-firing but lighter. Heats up more slowly.",
                Halo = new Color(0.05f, 1f, 0.04f), Core = new Color(0.85f, 1f, 0.82f), Lens = new Color(0.15f, 1f, 0.1f), Ember = new Color(0.6f, 1f, 0.3f),
                HaloGlow = 1.3f, BloomGlow = 0.5f, Width = 0.85f, Length = 5f,
                Speed = 200f, Interval = 0.11f, Heat = 0.035f,
                PanelDamage = 20f, PlayerDamage = 12f, ObjectDamage = 1.1f, HoleRadius = 0.05f, Pitch = 1.15f,
            },
            new LaserStyle
            {
                NameFr = "Canon laser bleu « Turbo »", NameEn = "\"Turbo\" blue laser cannon",
                DescFr = "Canon lourd : gros traits laser bleus, plus lents et espacés mais qui frappent fort et ouvrent de larges brèches. Surchauffe vite.",
                DescEn = "Heavy cannon: big blue laser bolts, slower and spaced out but hitting hard and opening wide breaches. Overheats quickly.",
                Halo = new Color(0.04f, 0.3f, 1f), Core = new Color(0.82f, 0.9f, 1f), Lens = new Color(0.1f, 0.45f, 1f), Ember = new Color(0.45f, 0.7f, 1f),
                HaloGlow = 2.2f, BloomGlow = 1f, Width = 1.6f, Length = 7.5f,
                Speed = 150f, Interval = 0.32f, Heat = 0.11f,
                PanelDamage = 60f, PlayerDamage = 36f, ObjectDamage = 3.2f, HoleRadius = 0.1f, Pitch = 0.78f,
            },
        };

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

        /// <summary>Fire a bolt. Only the owner's copy does damage; the others are for the eyes.</summary>
        public static void Fire(Vector3 from, Vector3 velocity, uint shooterRoot, bool owner, byte style)
        {
            LaserStyle s = Style(style);
            var go = new GameObject("Arsenal.BlasterBolt");
            go.transform.position = from;
            var bolt = go.AddComponent<BlasterBolt>();
            bolt.Init(from, velocity, shooterRoot, owner, style);

            // muzzle: hot white pop inside a coloured bloom that lights the ship up
            Vector3 dir = velocity.normalized;
            Fx.GlowFlash(from + dir * 0.05f, s.Core, 0.16f * s.Width, 0.06f, glow: 10f);
            Fx.GlowFlash(from + dir * 0.15f, s.Halo, 0.45f * s.Width, 0.09f, 9f, 7f, s.HaloGlow * 1.4f);
            Sounds.Blaster(from, s.Pitch);
        }

        /// <summary>A bolt reached a surface. Returns true if it went through an existing hole and flies on.</summary>
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
                    // glass gives way at once, wood and iron not always
                    float chance = part.MaterialType == HullMaterialType.Glass ? 1f : part.MaterialType == HullMaterialType.Wood ? 0.85f : 0.55f;
                    if (holeAble && Random.value < chance) holes = holes.PunchDisc(local.x, local.y, s.HoleRadius, Ballistics.Rng);
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
            Splash(hit.point, hit.normal, dir, s);
            return false;
        }

        /// <summary>A bolt struck another player.</summary>
        public static void HitPlayer(uint playerId, Vector3 point, Vector3 dir, bool owner, LaserStyle s)
        {
            if (owner)
            {
                bool head = Ballistics.IsHeadshot(playerId, point);
                PlayerDamage.Report(playerId, s.PlayerDamage * (head ? 1.6f : 1f), WeaponKind.ShipLaser, head);
            }
            Fx.GlowFlash(point, s.Core, 0.25f * s.Width, 0.07f, glow: 10f);
            Fx.GlowFlash(point, s.Halo, 0.7f * s.Width, 0.15f, 10f, 7f, s.HaloGlow * 1.4f);
            Fx.Puff(point, 0.1f, new Color(0.25f, 0.23f, 0.22f), 0.8f, 1, -dir * 0.5f);
            Sounds.BlasterHit(point);
        }

        /// <summary>The bolt splashes: a flash that lights the surface, sparks, molten droplets, a wisp of smoke.</summary>
        private static void Splash(Vector3 pos, Vector3 normal, Vector3 dir, LaserStyle s)
        {
            Vector3 p = pos + normal * 0.06f;
            Fx.GlowFlash(p, s.Core, 0.3f * s.Width, 0.08f, glow: 10f);
            Fx.GlowFlash(p, s.Halo, 0.9f * s.Width, 0.18f, 14f, 9f, s.HaloGlow * 1.4f);
            Fx.EmitSparks(pos, Vector3.Reflect(dir, normal) * 0.5f + normal * 0.5f, (int)(18 * s.Width));
            Fx.Embers(pos, normal, s.Ember, (int)(5 * s.Width), 0.035f, 5f);
            Fx.Puff(pos + normal * 0.1f, 0.12f * s.Width, new Color(0.25f, 0.23f, 0.22f), 1f, 1, normal * 0.6f);
            Sounds.BlasterHit(pos);
        }
    }

    /// <summary>One flying bolt: three glowing lines (wide faint bloom, coloured halo, white core) and a light.</summary>
    internal sealed class BlasterBolt : MonoBehaviour
    {
        private LaserStyle _s;
        private Vector3 _start, _head, _velocity;
        private float _travelled, _age;
        private uint _shooterRoot;
        private bool _owner, _stopped;
        private float _tailGone; // after a hit, how much of the bolt is left to slide into the surface
        private LineRenderer _bloom, _halo, _core;
        private Light _light;

        public void Init(Vector3 from, Vector3 velocity, uint shooterRoot, bool owner, byte style)
        {
            _s = Laser.Style(style);
            _start = _head = from;
            _velocity = velocity;
            _shooterRoot = shooterRoot;
            _owner = owner;
            _bloom = Line("Bloom", Laser.TintedGlow(ref _s.BloomMat, _s.Halo, _s.BloomGlow), 0.6f * _s.Width);
            _halo = Line("Halo", Laser.TintedGlow(ref _s.HaloMat, _s.Halo, _s.HaloGlow), 0.3f * _s.Width);
            _core = Line("Core", Fx.GlowMat(_s.Core, 6f), 0.05f * _s.Width);
            _light = gameObject.AddComponent<Light>();
            _light.type = LightType.Point;
            _light.color = Color.Lerp(_s.Halo, Color.white, 0.15f);
            _light.range = 9f * Mathf.Sqrt(_s.Width);
            _light.intensity = 5f;
            _light.shadows = LightShadows.None;
            Draw(_head, _head);
        }

        private LineRenderer Line(string name, Material mat, float width)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.sharedMaterial = mat;
            lr.positionCount = 2;
            lr.useWorldSpace = true;
            lr.startWidth = lr.endWidth = width;
            lr.numCapVertices = 4;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            return lr;
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
                    Destroy(gameObject);
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
            _halo.startWidth = _halo.endWidth = 0.3f * _s.Width * f;
            transform.position = Vector3.Lerp(tail, head, 0.7f);
        }
    }
}
