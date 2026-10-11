using System.Collections.Generic;
using UnityEngine;

namespace Arsenal.Visual
{
    internal enum ImpactKind : byte
    {
        Metal,
        Wood,
        Glass,
        Ground,
        Flesh,
    }

    /// <summary>
    /// All transient effects, driven by one runner. Style follows the game: flat-shaded low-poly bits,
    /// round "cartoon" smoke puffs and the mining laser's own spark particles and additive glow.
    /// </summary>
    internal sealed class Fx : MonoBehaviour
    {
        private abstract class Item
        {
            public float Age, Life;
            public abstract bool Step(float dt); // false = done
            public abstract void Kill();
        }

        private sealed class Tracer : Item
        {
            public LineRenderer Line;
            public Vector3 From, To;
            public float Speed, Length;

            public override bool Step(float dt)
            {
                Age += dt;
                float total = (To - From).magnitude;
                float head = Mathf.Min(total, Age * Speed);
                float tail = Mathf.Max(0f, head - Length);
                Vector3 dir = total > 0.001f ? (To - From) / total : Vector3.forward;
                Line.SetPosition(0, From + dir * tail);
                Line.SetPosition(1, From + dir * head);
                return tail < total && Age < Life;
            }

            public override void Kill() => Destroy(Line.gameObject);
        }

        private sealed class Body : Item
        {
            public Transform T;
            public Vector3 Velocity, Spin;
            public float Gravity, Drag;
            public Vector3 BaseScale;
            public bool Puff; // grow then shrink instead of falling
            public Light Light;
            public float LightIntensity;

            public override bool Step(float dt)
            {
                Age += dt;
                float k = Age / Life;
                Velocity += Vector3.down * Gravity * dt;
                Velocity *= Mathf.Clamp01(1f - Drag * dt);
                T.position += Velocity * dt;
                T.rotation = Quaternion.Euler(Spin * dt) * T.rotation;
                if (Puff)
                    T.localScale = BaseScale * (Mathf.Sin(Mathf.Min(1f, k * 1.6f) * Mathf.PI * 0.5f) * (1f - Mathf.Pow(k, 3f)) + 0.02f);
                else if (k > 0.7f)
                    T.localScale = BaseScale * Mathf.Clamp01((1f - k) / 0.3f);
                if (Light != null) Light.intensity = LightIntensity * (1f - k) * (1f - k);
                return Age < Life;
            }

            public override void Kill() => Destroy(T.gameObject);
        }

        private static Fx _instance;
        private readonly List<Item> _items = new List<Item>();
        private static Mesh _cube, _ico, _star;
        private static readonly Dictionary<Color, Material> Tints = new Dictionary<Color, Material>();
        private static Material _tracerMat, _flashMat, _fireMat;
        private static ParticleSystem _sparks;

        public static Fx Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("Arsenal.Fx");
                    DontDestroyOnLoad(go);
                    _instance = go.AddComponent<Fx>();
                }
                return _instance;
            }
        }

        /// <summary>Local "up" (planets are round: Unity's Y axis is not up). Refreshed every frame.</summary>
        public static Vector3 Up { get; private set; } = Vector3.up;

        private void Update()
        {
            try
            {
                var upProvider = Arsenal.Core.Svc.Get<Players.Services.PlayerUpProvider>();
                if (upProvider != null)
                {
                    Vector3 u = upProvider.WorldPlayerUp;
                    if (u.sqrMagnitude > 0.01f) Up = u.normalized;
                }
            }
            catch
            {
                // no local player yet
            }
            float dt = Time.deltaTime;
            for (int i = _items.Count - 1; i >= 0; i--)
            {
                Item it = _items[i];
                bool alive;
                try { alive = it.Step(dt); }
                catch { alive = false; }
                if (!alive)
                {
                    try { it.Kill(); } catch { }
                    _items.RemoveAt(i);
                }
            }
        }

        public static void ClearAll()
        {
            if (_instance == null) return;
            foreach (Item it in _instance._items)
                try { it.Kill(); } catch { }
            _instance._items.Clear();
            Tints.Clear();
            GlowMats.Clear();
            _tracerMat = _flashMat = _fireMat = null;
            _sparks = null;
        }

        // ------------------------------------------------------------------ shared resources

        public static Mesh CubeMesh => _cube != null ? _cube : (_cube = new MeshKit().Box(Vector3.zero, Vector3.one, Shade.Light).Build("Arsenal.Cube"));

        public static Mesh IcoMesh => _ico != null ? _ico : (_ico = BuildIco());

        private static Mesh StarMesh => _star != null ? _star : (_star = BuildStar());

        public static Material Tint(Color c)
        {
            c.a = 1f;
            if (!Tints.TryGetValue(c, out Material m) || m == null)
                Tints[c] = m = Assets.Tinted(c);
            return m;
        }

        private static Material TracerMat => _tracerMat != null ? _tracerMat : (_tracerMat = Assets.Glow(new Color(1f, 0.6f, 0.3f), 3.5f));
        private static Material FlashMat => _flashMat != null ? _flashMat : (_flashMat = Assets.Glow(new Color(1f, 0.6f, 0.25f), 10f));
        private static Material FireMat => _fireMat != null ? _fireMat : (_fireMat = Assets.Glow(new Color(1f, 0.45f, 0.12f), 14f));

        private static Mesh BuildIco()
        {
            float t = (1f + Mathf.Sqrt(5f)) / 2f;
            var p = new[]
            {
                new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
                new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
                new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1),
            };
            int[] f =
            {
                0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
                3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1,
            };
            var kit = new MeshKit();
            for (int i = 0; i < f.Length; i += 3)
            {
                Vector3 a = p[f[i]].normalized * 0.5f, b = p[f[i + 1]].normalized * 0.5f, c = p[f[i + 2]].normalized * 0.5f;
                // icosahedron faces listed counter-clockwise from outside; our convention wants cross(b-a,c-a) outward
                if (Vector3.Dot(Vector3.Cross(b - a, c - a), a + b + c) < 0) kit.Tri(a, c, b, Shade.Light);
                else kit.Tri(a, b, c, Shade.Light);
            }
            return kit.Build("Arsenal.Ico");
        }

        private static Mesh BuildStar()
        {
            // two crossed diamonds facing +z and a cross in depth: reads as a comic muzzle flash
            var v = new List<Vector3>();
            var t = new List<int>();
            void Diamond(Vector3 up, Vector3 side, float len)
            {
                int i = v.Count;
                v.Add(Vector3.zero); v.Add(side * 0.25f + Vector3.forward * len * 0.3f); v.Add(Vector3.forward * len); v.Add(-side * 0.25f + Vector3.forward * len * 0.3f);
                t.AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3, i, i + 2, i + 1, i, i + 3, i + 2 });
            }
            Diamond(Vector3.up, Vector3.right, 1f);
            Diamond(Vector3.right, Vector3.up, 1f);
            for (int k = 0; k < 4; k++)
            {
                float a = k * Mathf.PI * 0.5f + Mathf.PI * 0.25f;
                Vector3 d = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0.15f);
                int i = v.Count;
                Vector3 s = Vector3.Cross(d, Vector3.forward).normalized * 0.08f;
                v.Add(s); v.Add(d * 0.45f); v.Add(-s);
                t.AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 1 });
            }
            var m = new Mesh { name = "Arsenal.Flash" };
            m.SetVertices(v);
            m.SetTriangles(t, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        private static GameObject MeshObject(string name, Mesh mesh, Material mat, Vector3 pos, Quaternion rot, Vector3 scale)
        {
            var go = new GameObject(name);
            go.transform.SetPositionAndRotation(pos, rot);
            go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        // ------------------------------------------------------------------ effects

        public static void BulletTracer(Vector3 from, Vector3 to, float width = 0.025f, float speed = 320f)
        {
            if ((to - from).sqrMagnitude < 0.25f) return;
            var go = new GameObject("Arsenal.Tracer");
            var lr = go.AddComponent<LineRenderer>();
            lr.sharedMaterial = TracerMat;
            lr.positionCount = 2;
            lr.useWorldSpace = true;
            // discreet: thin, short and dim, a hint of the bullet's path rather than a laser
            lr.startWidth = width * 0.7f;
            lr.endWidth = width * 0.3f;
            lr.numCapVertices = 1;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.SetPosition(0, from);
            lr.SetPosition(1, from);
            Instance._items.Add(new Tracer { Line = lr, From = from, To = to, Speed = speed * 1.2f, Length = Mathf.Min(4.5f, speed * 0.012f), Life = 3f });
        }

        public static void MuzzleFlash(Vector3 pos, Vector3 dir, float size, Transform follow = null)
        {
            Quaternion rot = Quaternion.LookRotation(dir) * Quaternion.Euler(0, 0, Random.Range(0f, 90f));
            GameObject go = MeshObject("Arsenal.Flash", StarMesh, FlashMat, pos, rot, Vector3.one * size);
            if (follow != null) go.transform.SetParent(follow, true);
            Light l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = new Color(1f, 0.7f, 0.4f);
            l.range = 4f * Mathf.Max(1f, size * 4f);
            l.intensity = 3f;
            l.shadows = LightShadows.None;
            Instance._items.Add(new Body { T = go.transform, Life = 0.05f, BaseScale = go.transform.localScale, Light = l, LightIntensity = 3f });
        }

        public static void Impact(Vector3 pos, Vector3 normal, ImpactKind kind, Color tint, float scale = 1f)
        {
            int sparks = kind == ImpactKind.Metal ? 14 : kind == ImpactKind.Glass ? 6 : 3;
            EmitSparks(pos, normal, (int)(sparks * scale));
            switch (kind)
            {
                case ImpactKind.Wood:
                    Debris(pos, normal, new Color(0.55f, 0.38f, 0.22f), (int)(5 * scale), 0.035f * scale, 3f);
                    Puff(pos + normal * 0.05f, 0.12f * scale, new Color(0.75f, 0.65f, 0.55f), 0.6f, 1);
                    break;
                case ImpactKind.Glass:
                    Debris(pos, normal, new Color(0.7f, 0.85f, 0.95f), (int)(8 * scale), 0.03f * scale, 3.5f);
                    break;
                case ImpactKind.Ground:
                    Debris(pos, normal, new Color(0.35f, 0.3f, 0.22f), (int)(4 * scale), 0.04f * scale, 3f);
                    Puff(pos + normal * 0.08f, 0.22f * scale, new Color(0.7f, 0.66f, 0.58f), 0.9f, 2);
                    break;
                case ImpactKind.Flesh:
                    Puff(pos, 0.12f * scale, new Color(0.6f, 0.15f, 0.12f), 0.4f, 2);
                    break;
                default:
                    Debris(pos, normal, tint, (int)(3 * scale), 0.025f * scale, 4f);
                    Puff(pos + normal * 0.05f, 0.1f * scale, new Color(0.8f, 0.8f, 0.78f), 0.5f, 1);
                    break;
            }
        }

        public static void EmitSparks(Vector3 pos, Vector3 normal, int count)
        {
            if (count <= 0) return;
            if (_sparks == null)
            {
                ParticleSystem tpl = Assets.SparksTemplate;
                if (tpl == null) return;
                _sparks = Instantiate(tpl);
                _sparks.name = "Arsenal.SparksEmitter";
                DontDestroyOnLoad(_sparks.gameObject);
                var em = _sparks.emission;
                em.enabled = false;
                var main = _sparks.main;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.maxParticles = 2000;
                _sparks.gameObject.SetActive(true);
                _sparks.Play();
            }
            _sparks.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(normal));
            var p = new ParticleSystem.EmitParams { applyShapeToPosition = true };
            for (int i = 0; i < count; i++)
            {
                p.position = pos;
                p.velocity = (normal + Random.insideUnitSphere * 0.9f).normalized * Random.Range(2f, 7f);
                _sparks.Emit(p, 1);
            }
        }

        /// <summary>Small flat-shaded chips flying away from a surface.</summary>
        public static void Debris(Vector3 pos, Vector3 normal, Color color, int count, float size, float speed)
        {
            Material m = Tint(color);
            for (int i = 0; i < count; i++)
            {
                Vector3 s = new Vector3(Random.Range(0.6f, 1.4f), Random.Range(0.3f, 0.8f), Random.Range(0.6f, 1.4f)) * size;
                GameObject go = MeshObject("Arsenal.Chip", CubeMesh, m, pos, Random.rotation, s);
                Instance._items.Add(new Body
                {
                    T = go.transform,
                    Velocity = (normal + Random.insideUnitSphere * 0.8f).normalized * speed * Random.Range(0.4f, 1.2f),
                    Spin = Random.insideUnitSphere * 720f,
                    Gravity = 9.81f,
                    Drag = 0.4f,
                    Life = Random.Range(0.9f, 1.8f),
                    BaseScale = s,
                });
            }
        }

        /// <summary>Round low-poly smoke puffs that swell and shrink, like the game's clouds and exhausts.</summary>
        public static void Puff(Vector3 pos, float radius, Color color, float life, int count, Vector3 drift = default)
        {
            Material m = Tint(color);
            for (int i = 0; i < count; i++)
            {
                float r = radius * Random.Range(0.7f, 1.3f);
                Vector3 p = pos + Random.insideUnitSphere * radius * 0.6f;
                GameObject go = MeshObject("Arsenal.Puff", IcoMesh, m, p, Random.rotation, Vector3.one * 0.01f);
                Instance._items.Add(new Body
                {
                    T = go.transform,
                    Velocity = drift + Random.insideUnitSphere * radius * 0.8f + Up * radius * 0.4f,
                    Spin = Random.insideUnitSphere * 40f,
                    Drag = 1.5f,
                    Life = life * Random.Range(0.8f, 1.25f),
                    BaseScale = Vector3.one * r * 2f,
                    Puff = true,
                });
            }
        }

        /// <summary>A blast: fireball, flash light, smoke ring, chips.</summary>
        public static void Explosion(Vector3 pos, float radius)
        {
            GameObject fire = MeshObject("Arsenal.Fireball", IcoMesh, FireMat, pos, Random.rotation, Vector3.one * 0.01f);
            Light l = fire.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = new Color(1f, 0.6f, 0.3f);
            l.range = radius * 6f;
            l.intensity = 8f;
            l.shadows = LightShadows.None;
            Instance._items.Add(new Body { T = fire.transform, Life = 0.35f, BaseScale = Vector3.one * radius * 0.8f, Puff = true, Spin = Random.insideUnitSphere * 90f, Light = l, LightIntensity = 8f });
            for (int i = 0; i < 6; i++)
            {
                GameObject core = MeshObject("Arsenal.Fire", IcoMesh, FireMat, pos + Random.insideUnitSphere * radius * 0.3f, Random.rotation, Vector3.one * 0.01f);
                Instance._items.Add(new Body { T = core.transform, Life = Random.Range(0.25f, 0.5f), BaseScale = Vector3.one * radius * Random.Range(0.25f, 0.45f), Puff = true, Velocity = Random.insideUnitSphere * radius * 2f, Drag = 3f });
            }
            Puff(pos, radius * 0.2f, new Color(0.5f, 0.48f, 0.46f), 1.6f, 8, Up * 0.8f);
            Puff(pos, radius * 0.14f, new Color(0.72f, 0.7f, 0.67f), 2.2f, 6, Up * 1.4f);
            EmitSparks(pos, Up, 60);
            Debris(pos, Up, new Color(0.25f, 0.24f, 0.23f), 12, 0.06f * Mathf.Max(1f, radius * 0.3f), radius * 4f);
        }

        private static readonly Dictionary<Color, Material> GlowMats = new Dictionary<Color, Material>();

        public static Material GlowMat(Color c, float intensity = 6f)
        {
            Color key = new Color(c.r, c.g, c.b, intensity / 100f);
            if (!GlowMats.TryGetValue(key, out Material m) || m == null)
                GlowMats[key] = m = Assets.Glow(c, intensity);
            return m;
        }

        /// <summary>A tracer of a given colour (laser bolts, plasma, ion, rail trails).</summary>
        public static void ColoredTracer(Vector3 from, Vector3 to, float width, Color color, float speed, float length, float intensity = 6f)
        {
            if ((to - from).sqrMagnitude < 0.04f) return;
            var go = new GameObject("Arsenal.Bolt");
            var lr = go.AddComponent<LineRenderer>();
            lr.sharedMaterial = GlowMat(color, intensity);
            lr.positionCount = 2;
            lr.useWorldSpace = true;
            lr.startWidth = width;
            lr.endWidth = width;
            lr.numCapVertices = 3;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.SetPosition(0, from);
            lr.SetPosition(1, from);
            Instance._items.Add(new Tracer { Line = lr, From = from, To = to, Speed = speed, Length = length, Life = 3f });
        }

        /// <summary>Straight line that fades out in place (rail shots).</summary>
        public static void Streak(Vector3 from, Vector3 to, float width, Color color, float life)
        {
            var go = new GameObject("Arsenal.Streak");
            var lr = go.AddComponent<LineRenderer>();
            lr.sharedMaterial = GlowMat(color, 8f);
            lr.positionCount = 2;
            lr.useWorldSpace = true;
            lr.startWidth = width;
            lr.endWidth = width;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.SetPosition(0, from);
            lr.SetPosition(1, to);
            Instance._items.Add(new Fade { Line = lr, Life = life, Width = width });
        }

        private sealed class Fade : Item
        {
            public LineRenderer Line;
            public float Width;

            public override bool Step(float dt)
            {
                Age += dt;
                float k = 1f - Age / Life;
                Line.startWidth = Line.endWidth = Width * k;
                return Age < Life;
            }

            public override void Kill() => Destroy(Line.gameObject);
        }

        /// <summary>A jagged electric arc between two points, re-jagged every frame while it lives.</summary>
        public static void ElectricArc(Vector3 from, Vector3 to, Color color, float life, float width = 0.03f)
        {
            var go = new GameObject("Arsenal.Arc");
            var lr = go.AddComponent<LineRenderer>();
            lr.sharedMaterial = GlowMat(color, 9f);
            lr.positionCount = 10;
            lr.useWorldSpace = true;
            lr.startWidth = width;
            lr.endWidth = width * 0.6f;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var arc = new Arc { Line = lr, From = from, To = to, Life = life, Width = width };
            arc.Jag();
            Light l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = color;
            l.range = 3f;
            l.intensity = 2f;
            l.shadows = LightShadows.None;
            go.transform.position = (from + to) * 0.5f;
            Instance._items.Add(arc);
        }

        private sealed class Arc : Item
        {
            public LineRenderer Line;
            public Vector3 From, To;
            public float Width;

            public void Jag()
            {
                float len = (To - From).magnitude;
                int n = Line.positionCount;
                for (int i = 0; i < n; i++)
                {
                    float k = i / (float)(n - 1);
                    Vector3 p = Vector3.Lerp(From, To, k);
                    if (i > 0 && i < n - 1) p += Random.insideUnitSphere * len * 0.08f;
                    Line.SetPosition(i, p);
                }
            }

            public override bool Step(float dt)
            {
                Age += dt;
                Jag();
                Line.startWidth = Width * (1f - Age / Life);
                return Age < Life;
            }

            public override void Kill() => Destroy(Line.gameObject);
        }

        /// <summary>A licking flame: a glowing blob that swells, rises and fades.</summary>
        public static void Flame(Vector3 pos, Vector3 drift, float size)
        {
            Material m = GlowMat(Random.value < 0.5f ? new Color(1f, 0.45f, 0.1f) : new Color(1f, 0.7f, 0.25f), 9f);
            GameObject go = MeshObject("Arsenal.Flame", IcoMesh, m, pos, Random.rotation, Vector3.one * 0.01f);
            Instance._items.Add(new Body
            {
                T = go.transform,
                Velocity = drift + Up * size * 2f + Random.insideUnitSphere * size,
                Spin = Random.insideUnitSphere * 200f,
                Drag = 2f,
                Life = Random.Range(0.25f, 0.45f),
                BaseScale = Vector3.one * size * Random.Range(0.8f, 1.4f),
                Puff = true,
            });
        }

        /// <summary>A glowing ball that follows a transform-less path (plasma, ion bolts) — the projectile code moves it.</summary>
        public static GameObject Orb(Color color, float radius)
        {
            GameObject go = MeshObject("Arsenal.Orb", IcoMesh, GlowMat(color, 10f), Vector3.zero, Quaternion.identity, Vector3.one * radius * 2f);
            Light l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = color;
            l.range = radius * 20f;
            l.intensity = 3f;
            l.shadows = LightShadows.None;
            return go;
        }

        /// <summary>A continuous beam segment that lives one frame (the laser cannon redraws it every frame).</summary>
        public static void Beam(Vector3 from, Vector3 to, float width, Material mat, float life = 0.001f)
        {
            var go = new GameObject("Arsenal.Beam");
            var lr = go.AddComponent<LineRenderer>();
            lr.sharedMaterial = mat;
            lr.positionCount = 2;
            lr.useWorldSpace = true;
            lr.startWidth = width;
            lr.endWidth = width;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.SetPosition(0, from);
            lr.SetPosition(1, to);
            Instance._items.Add(new Body { T = go.transform, Life = life, BaseScale = Vector3.one });
        }
    }
}
