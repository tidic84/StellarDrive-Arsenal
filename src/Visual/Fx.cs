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
        Shield,   // a force field (TF Advanced) took the hit
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
            _trauma = Mathf.Max(0f, _trauma - Time.deltaTime * 1.6f);
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
            if (kind == ImpactKind.Shield) { Compat.ForceFields.Effect(pos, normal, tint, scale); return; }
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

        /// <summary>
        /// A cinematic blast in the game's low-poly style: a white-hot flash and a light that cools to orange, a fireball of
        /// blobs that cool from yellow to dark red, burning debris trailing smoke, a ground dust ring,
        /// a smoke column that rises and lightens, and a camera shake that depends on distance.
        /// </summary>
        public static void Explosion(Vector3 pos, float radius)
        {
            radius = Mathf.Max(0.5f, radius);
            Vector3 up = Up;
            Shake(pos, radius);
            float s = radius * 0.5f; // visual size: the damage radius reaches well past the visible fireball

            // flash and the light that cools down with the fireball
            GlowFlash(pos, new Color(1f, 0.93f, 0.75f), s * 1.6f, 0.12f, 0f, 0f, 22f);
            var lightGo = new GameObject("Arsenal.BlastLight");
            lightGo.transform.position = pos + up * radius * 0.3f;
            Light l = lightGo.AddComponent<Light>();
            l.type = LightType.Point;
            l.range = radius * 12f;
            l.shadows = LightShadows.None;
            Instance._items.Add(new LightPulse { L = l, Life = 1.1f, Peak = 22f });

            // fireball: blobs thrown out (mostly upwards), cooling as they go
            int blobs = Mathf.Clamp((int)(10 + radius * 3f), 10, 24);
            for (int i = 0; i < blobs; i++)
            {
                Vector3 dir = (Random.onUnitSphere + up * 0.55f).normalized;
                Blob(FireMat, pos + dir * s * Random.Range(0f, 0.25f), dir * s * Random.Range(2.5f, 5f), up * s * 1.6f,
                    s * Random.Range(0.3f, 0.6f), Random.Range(0.55f, 1.05f), Random.Range(0f, 0.06f), 4f, true);
            }

            // burning debris arcing out, each leaving a smoke trail
            int trails = Mathf.Clamp((int)(3 + radius), 3, 9);
            for (int i = 0; i < trails; i++)
            {
                Vector3 dir = (Random.onUnitSphere + up * 1.1f).normalized;
                GameObject ember = MeshObject("Arsenal.Brand", IcoMesh, GlowMat(new Color(1f, 0.55f, 0.15f), 10f), pos, Random.rotation, Vector3.one * s * 0.12f);
                Instance._items.Add(new Trail
                {
                    T = ember.transform,
                    Velocity = dir * s * Random.Range(5f, 9f),
                    Life = Random.Range(0.9f, 1.6f),
                    BaseScale = ember.transform.localScale,
                    Size = s * 0.14f,
                });
            }

            // dust rolling out along the ground
            Vector3 side = Vector3.Cross(up, Mathf.Abs(Vector3.Dot(up, Vector3.forward)) > 0.9f ? Vector3.right : Vector3.forward).normalized;
            int dust = Mathf.Clamp((int)(6 + radius), 6, 12);
            for (int i = 0; i < dust; i++)
            {
                Vector3 dir = Quaternion.AngleAxis(i * 360f / dust + Random.Range(-12f, 12f), up) * side;
                Blob(Tint(Color.white), pos + dir * s * 0.3f - up * s * 0.2f, dir * s * Random.Range(3f, 4.5f), up * s * 0.15f,
                    s * Random.Range(0.12f, 0.22f), Random.Range(1.4f, 2.2f), Random.Range(0.02f, 0.1f), 2.2f, false, new Color(0.6f, 0.55f, 0.47f), new Color(0.76f, 0.72f, 0.66f));
            }

            // smoke column: sooty at first, rising, spreading and lightening as it cools
            int smoke = Mathf.Clamp((int)(10 + radius * 3f), 10, 24);
            for (int i = 0; i < smoke; i++)
            {
                Vector3 off = Random.insideUnitSphere * s * 0.6f;
                Blob(Tint(Color.white), pos + off, (off + up * s * Random.Range(0.8f, 2f)) * 1.2f, up * s * Random.Range(0.4f, 0.9f),
                    s * Random.Range(0.22f, 0.42f), Random.Range(2.8f, 4.6f), Random.Range(0.12f, 0.45f), 1.4f, false, new Color(0.4f, 0.38f, 0.36f), new Color(0.8f, 0.79f, 0.77f));
            }

            EmitSparks(pos, up, 80);
            Debris(pos, up, new Color(0.25f, 0.24f, 0.23f), 14, 0.06f * Mathf.Max(1f, radius * 0.3f), radius * 4.5f);
        }

        /// <summary>One swelling blob: fire (emission cooling from white-hot to dark red) or smoke/dust (colour ramp).</summary>
        private static void Blob(Material mat, Vector3 pos, Vector3 velocity, Vector3 lift, float size, float life, float delay, float drag, bool fire,
            Color from = default, Color to = default)
        {
            GameObject go = MeshObject(fire ? "Arsenal.Fireball" : "Arsenal.Smoke", IcoMesh, mat, pos, Random.rotation, Vector3.one * 0.001f);
            go.SetActive(delay <= 0f);
            Instance._items.Add(new BlobItem
            {
                T = go.transform,
                R = go.GetComponent<MeshRenderer>(),
                Velocity = velocity,
                Lift = lift,
                Drag = drag,
                Spin = Random.insideUnitSphere * (fire ? 160f : 30f),
                Size = size,
                Life = life + delay,
                Delay = delay,
                Fire = fire,
                From = from,
                To = to,
            });
        }

        private sealed class BlobItem : Item
        {
            public Transform T;
            public MeshRenderer R;
            public Vector3 Velocity, Lift, Spin;
            public float Drag, Size, Delay;
            public bool Fire;
            public Color From, To;
            private MaterialPropertyBlock _block;

            // white-hot core -> yellow -> orange -> deep red -> soot
            private static readonly Color[] Heat =
            {
                new Color(1f, 0.95f, 0.8f) * 20f, new Color(1f, 0.72f, 0.28f) * 13f, new Color(1f, 0.4f, 0.1f) * 7f,
                new Color(0.55f, 0.13f, 0.04f) * 2.2f, new Color(0.08f, 0.06f, 0.05f) * 0.1f,
            };

            private static Color Ramp(Color[] keys, float k)
            {
                float f = Mathf.Clamp01(k) * (keys.Length - 1);
                int i = Mathf.Min(keys.Length - 2, (int)f);
                return Color.Lerp(keys[i], keys[i + 1], f - i);
            }

            public override bool Step(float dt)
            {
                Age += dt;
                if (Age < Delay) return true;
                if (!T.gameObject.activeSelf) T.gameObject.SetActive(true);
                float k = (Age - Delay) / (Life - Delay);
                Velocity += Lift * dt;
                Velocity *= Mathf.Clamp01(1f - Drag * dt);
                T.position += Velocity * dt;
                T.rotation = Quaternion.Euler(Spin * dt) * T.rotation;
                float grow = Fire ? 1f - Mathf.Pow(1f - Mathf.Clamp01(k / 0.25f), 3f) : Mathf.Lerp(0.35f, 1f, 1f - Mathf.Pow(1f - k, 2.5f));
                float shrink = Fire ? Mathf.Clamp01((1f - k) / 0.35f) : Mathf.Clamp01((1f - k) / 0.2f);
                T.localScale = Vector3.one * (Size * 2f * grow * Mathf.Sqrt(shrink) + 0.001f);
                if (_block == null) _block = new MaterialPropertyBlock();
                R.GetPropertyBlock(_block);
                if (Fire)
                {
                    _block.SetColor("_EmissionColor", Ramp(Heat, k));
                    Color b = Color.Lerp(new Color(1f, 0.85f, 0.6f), new Color(0.12f, 0.1f, 0.09f), Mathf.Clamp01(k * 1.3f));
                    _block.SetColor("_BaseColor", b);
                    _block.SetColor("_Color", b);
                }
                else
                {
                    Color c = Color.Lerp(From, To, Mathf.Clamp01(k * 1.4f));
                    _block.SetColor("_Color", c);
                    _block.SetColor("_BaseColor", c);
                }
                R.SetPropertyBlock(_block);
                return Age < Life;
            }

            public override void Kill() => Destroy(T.gameObject);
        }

        /// <summary>The blast's light: a hard white peak that cools to orange and dies.</summary>
        private sealed class LightPulse : Item
        {
            public Light L;
            public float Peak;

            public override bool Step(float dt)
            {
                Age += dt;
                float k = Age / Life;
                L.intensity = Peak * Mathf.Pow(1f - k, 2.2f);
                L.color = Color.Lerp(new Color(1f, 0.92f, 0.8f), new Color(1f, 0.42f, 0.12f), Mathf.Clamp01(k * 2.5f));
                return Age < Life;
            }

            public override void Kill() => Destroy(L.gameObject);
        }

        /// <summary>A burning chunk on a ballistic arc that leaves small smoke puffs behind it.</summary>
        private sealed class Trail : Item
        {
            public Transform T;
            public Vector3 Velocity, BaseScale;
            public float Size;
            private float _next;

            public override bool Step(float dt)
            {
                Age += dt;
                Velocity += -Up * 9.81f * dt;
                Velocity *= Mathf.Clamp01(1f - 0.3f * dt);
                T.position += Velocity * dt;
                float k = Age / Life;
                T.localScale = BaseScale * Mathf.Clamp01((1f - k) / 0.4f);
                if (Age >= _next)
                {
                    _next = Age + 0.045f;
                    Blob(Tint(Color.white), T.position, Random.insideUnitSphere * Size, Up * Size * 2f, Size * Mathf.Lerp(1f, 0.5f, k),
                        Random.Range(0.7f, 1.1f), 0f, 2f, false, new Color(0.45f, 0.43f, 0.41f), new Color(0.82f, 0.81f, 0.79f));
                }
                return Age < Life;
            }

            public override void Kill() => Destroy(T.gameObject);
        }

        // ------------------------------------------------------------------ camera shake

        private static float _trauma;
        private static bool _shakeHooked;
        private static Quaternion _shakeSaved;
        private static Camera _shaken;

        /// <summary>Adds camera shake from a blast: strong up close, fading with distance relative to the blast's size.</summary>
        public static void Shake(Vector3 pos, float radius)
        {
            Camera cam = Camera.main;
            if (cam == null) return;
            float d = Vector3.Distance(cam.transform.position, pos);
            float add = Mathf.Clamp01(radius * 2.5f / (d + radius)) * Mathf.Clamp01(radius / 3f + 0.3f);
            _trauma = Mathf.Clamp01(_trauma + add);
            if (_shakeHooked) return;
            _shakeHooked = true;
            UnityEngine.Rendering.RenderPipelineManager.beginCameraRendering += (ctx, c) =>
            {
                if (_trauma <= 0f || c != Camera.main) return;
                float t = Time.time * 28f, s = _trauma * _trauma;
                _shaken = c;
                _shakeSaved = c.transform.localRotation;
                c.transform.localRotation *= Quaternion.Euler((Mathf.PerlinNoise(t, 0.1f) - 0.5f) * 5f * s, (Mathf.PerlinNoise(0.7f, t) - 0.5f) * 5f * s, (Mathf.PerlinNoise(t, t) - 0.5f) * 3f * s);
            };
            UnityEngine.Rendering.RenderPipelineManager.endCameraRendering += (ctx, c) =>
            {
                if (c != _shaken) return;
                c.transform.localRotation = _shakeSaved;
                _shaken = null;
            };
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

        /// <summary>A glowing ball that swells and dies at once, with a light pulse (blaster muzzles and hits).</summary>
        public static void GlowFlash(Vector3 pos, Color color, float size, float life, float lightRange = 0f, float lightIntensity = 0f, float glow = 10f)
        {
            GameObject go = MeshObject("Arsenal.GlowFlash", IcoMesh, GlowMat(color, glow), pos, Random.rotation, Vector3.one * 0.01f);
            Light l = null;
            if (lightRange > 0f)
            {
                l = go.AddComponent<Light>();
                l.type = LightType.Point;
                l.color = color;
                l.range = lightRange;
                l.intensity = lightIntensity;
                l.shadows = LightShadows.None;
            }
            Instance._items.Add(new Body { T = go.transform, Life = life, BaseScale = Vector3.one * size, Puff = true, Spin = Random.insideUnitSphere * 300f, Light = l, LightIntensity = lightIntensity });
        }

        /// <summary>Molten droplets: glowing chips thrown off a surface that fall and fade.</summary>
        public static void Embers(Vector3 pos, Vector3 normal, Color color, int count, float size, float speed)
        {
            Material m = GlowMat(color, 8f);
            for (int i = 0; i < count; i++)
            {
                Vector3 s = Vector3.one * size * Random.Range(0.6f, 1.4f);
                GameObject go = MeshObject("Arsenal.Ember", CubeMesh, m, pos, Random.rotation, s);
                Instance._items.Add(new Body
                {
                    T = go.transform,
                    Velocity = (normal + Random.insideUnitSphere * 0.9f).normalized * speed * Random.Range(0.4f, 1.2f),
                    Spin = Random.insideUnitSphere * 500f,
                    Gravity = 9.81f,
                    Drag = 0.8f,
                    Life = Random.Range(0.4f, 0.9f),
                    BaseScale = s,
                });
            }
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
