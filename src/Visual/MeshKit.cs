using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Arsenal.Visual
{
    /// <summary>
    /// A surface finish: an area of the game's hand-drawn tool atlas (ink, flat or hatched) and a colour of the
    /// game's ship palette (the PlanetObject shader multiplies the atlas by the palette colour picked by uv2).
    /// </summary>
    internal enum Shade
    {
        Black,
        Dark,
        Light,
        White,
        HatchDark,
        HatchMid,
        HatchLight,
        Grey,
        Yellow,
        Blue,
        Orange,
        Red,
        Green,
    }

    /// <summary>
    /// Builds low-poly meshes out of boxes, prisms and cylinders, textured like the game's own tools.
    /// </summary>
    internal sealed class MeshKit
    {
        private const float TexelsPerMetre = 0.55f; // uv units per metre on faces

        // Areas of the 2048x2048 MiningLaser albedo (uv space).
        private static readonly Rect Ink = Rect.MinMaxRect(0.445f, 0.905f, 0.62f, 0.99f);
        private static readonly Rect Flat = Rect.MinMaxRect(0.163f, 0.505f, 0.305f, 0.558f);
        private static readonly Rect Bright = Rect.MinMaxRect(0.725f, 0.787f, 0.804f, 0.871f);
        private static readonly Rect HatchA = Rect.MinMaxRect(0.092f, 0.237f, 0.223f, 0.308f);
        private static readonly Rect HatchB = Rect.MinMaxRect(0.487f, 0.707f, 0.718f, 0.738f);
        private static readonly Rect HatchC = Rect.MinMaxRect(0.412f, 0.777f, 0.458f, 0.848f);

        // Palette columns of ShipColorPalette (row 0): 1 grey .7, 2 dark .3, 3 light .9, 4 red, 5 yellow, 6 green, 7 blue, 10 orange.
        private static readonly Dictionary<Shade, (Rect area, int palette)> Finishes = new Dictionary<Shade, (Rect, int)>
        {
            [Shade.Black] = (Ink, 2),
            [Shade.Dark] = (Flat, 2),
            [Shade.Light] = (Flat, 3),
            [Shade.White] = (Bright, 3),
            [Shade.HatchDark] = (HatchA, 2),
            [Shade.HatchMid] = (HatchB, 1),
            [Shade.HatchLight] = (HatchC, 3),
            [Shade.Grey] = (Flat, 1),
            [Shade.Yellow] = (Bright, 5),
            [Shade.Blue] = (Bright, 7),
            [Shade.Orange] = (Bright, 10),
            [Shade.Red] = (Bright, 4),
            [Shade.Green] = (Bright, 6),
        };

        /// <summary>The same colour without hatching (hatching looks wrong on small fan-shaped caps).</summary>
        public static Shade FlatOf(Shade s) => s switch
        {
            Shade.HatchDark => Shade.Dark,
            Shade.HatchMid => Shade.Grey,
            Shade.HatchLight => Shade.Light,
            _ => s,
        };

        private readonly List<Vector3> _v = new List<Vector3>();
        private readonly List<Vector3> _n = new List<Vector3>();
        private readonly List<Vector2> _uv = new List<Vector2>();
        private readonly List<Vector2> _uv2 = new List<Vector2>();
        private readonly List<int> _t = new List<int>();

        public Matrix4x4 Transform = Matrix4x4.identity;

        public MeshKit At(Vector3 pos, Vector3 euler = default)
        {
            Transform = Matrix4x4.TRS(pos, Quaternion.Euler(euler), Vector3.one);
            return this;
        }

        /// <summary>Axis-aligned box (in the current transform), each face its own flat-shaded quad.</summary>
        public MeshKit Box(Vector3 center, Vector3 size, Shade shade, Shade? sides = null)
        {
            Vector3 h = size * 0.5f;
            Vector3[] c =
            {
                center + new Vector3(-h.x, -h.y, -h.z), center + new Vector3(h.x, -h.y, -h.z),
                center + new Vector3(h.x, h.y, -h.z), center + new Vector3(-h.x, h.y, -h.z),
                center + new Vector3(-h.x, -h.y, h.z), center + new Vector3(h.x, -h.y, h.z),
                center + new Vector3(h.x, h.y, h.z), center + new Vector3(-h.x, h.y, h.z),
            };
            Shade s = sides ?? shade;
            Quad(c[3], c[2], c[1], c[0], shade); // back (-z)
            Quad(c[4], c[5], c[6], c[7], shade); // front (+z)
            Quad(c[7], c[6], c[2], c[3], shade); // top
            Quad(c[0], c[1], c[5], c[4], s);     // bottom
            Quad(c[0], c[4], c[7], c[3], s);     // left
            Quad(c[5], c[1], c[2], c[6], s);     // right
            return this;
        }

        /// <summary>Box with its top edges chamfered, which reads better with the game's edge outlines.</summary>
        public MeshKit Bevel(Vector3 center, Vector3 size, float bevel, Shade shade, Shade? sides = null)
        {
            bevel = Mathf.Min(bevel, size.x * 0.45f, size.y * 0.45f);
            Vector3 h = size * 0.5f;
            float zf = center.z + h.z, zb = center.z - h.z;
            // octagon-ish profile in x/y, extruded along z
            var p = new[]
            {
                new Vector2(-h.x + bevel, -h.y), new Vector2(h.x - bevel, -h.y),
                new Vector2(h.x, -h.y + bevel), new Vector2(h.x, h.y - bevel),
                new Vector2(h.x - bevel, h.y), new Vector2(-h.x + bevel, h.y),
                new Vector2(-h.x, h.y - bevel), new Vector2(-h.x, -h.y + bevel),
            };
            Prism(p.Select(q => q + new Vector2(center.x, center.y)).ToArray(), zb, zf, FlatOf(shade), sides ?? shade);
            return this;
        }

        /// <summary>Extrudes a convex polygon (counter-clockwise in x/y) between z0 and z1.</summary>
        public MeshKit Prism(Vector2[] poly, float z0, float z1, Shade caps, Shade sides)
        {
            int n = poly.Length;
            // caps as fans
            Vector3 c0 = Vector3.zero;
            foreach (Vector2 q in poly) c0 += new Vector3(q.x, q.y, 0);
            c0 /= n;
            for (int i = 0; i < n; i++)
            {
                Vector2 a = poly[i], b = poly[(i + 1) % n];
                Tri(new Vector3(c0.x, c0.y, z1), new Vector3(a.x, a.y, z1), new Vector3(b.x, b.y, z1), caps);
                Tri(new Vector3(c0.x, c0.y, z0), new Vector3(b.x, b.y, z0), new Vector3(a.x, a.y, z0), caps);
                Quad(new Vector3(a.x, a.y, z0), new Vector3(b.x, b.y, z0), new Vector3(b.x, b.y, z1), new Vector3(a.x, a.y, z1), sides);
            }
            return this;
        }

        /// <summary>Cylinder along +z.</summary>
        public MeshKit Cylinder(Vector3 center, float radius, float length, int segments, Shade caps, Shade sides)
        {
            var poly = new Vector2[segments];
            for (int i = 0; i < segments; i++)
            {
                float a = (i + 0.5f) / segments * Mathf.PI * 2f;
                poly[i] = new Vector2(center.x + Mathf.Cos(a) * radius, center.y + Mathf.Sin(a) * radius);
            }
            return Prism(poly, center.z - length * 0.5f, center.z + length * 0.5f, caps, sides);
        }

        /// <summary>Cone/frustum along +z from radius r0 (at z0) to r1 (at z1).</summary>
        public MeshKit Frustum(Vector3 baseCenter, float r0, float r1, float length, int segments, Shade shade)
        {
            for (int i = 0; i < segments; i++)
            {
                float a0 = (float)i / segments * Mathf.PI * 2f, a1 = (float)(i + 1) / segments * Mathf.PI * 2f;
                Vector3 d0 = new Vector3(Mathf.Cos(a0), Mathf.Sin(a0)), d1 = new Vector3(Mathf.Cos(a1), Mathf.Sin(a1));
                Vector3 p00 = baseCenter + d0 * r0, p01 = baseCenter + d1 * r0;
                Vector3 p10 = baseCenter + d0 * r1 + Vector3.forward * length, p11 = baseCenter + d1 * r1 + Vector3.forward * length;
                if (r1 > 0.0001f) Quad(p00, p01, p11, p10, shade);
                else Tri(p00, p01, p10, shade);
                Tri(baseCenter, p01, p00, shade);
            }
            return this;
        }

        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Shade shade)
        {
            a = Transform.MultiplyPoint3x4(a); b = Transform.MultiplyPoint3x4(b);
            c = Transform.MultiplyPoint3x4(c); d = Transform.MultiplyPoint3x4(d);
            Vector3 normal = Vector3.Cross(b - a, c - a).normalized;
            int i = _v.Count;
            _v.Add(a); _v.Add(b); _v.Add(c); _v.Add(d);
            for (int k = 0; k < 4; k++) _n.Add(normal);
            float w = (b - a).magnitude, hgt = (d - a).magnitude;
            AddUvs(shade, new[] { new Vector2(0, 0), new Vector2(w, 0), new Vector2(w, hgt), new Vector2(0, hgt) });
            // Same convention as the game's meshes: the face points along cross(b - a, c - a).
            _t.Add(i); _t.Add(i + 1); _t.Add(i + 2);
            _t.Add(i); _t.Add(i + 2); _t.Add(i + 3);
        }

        public void Tri(Vector3 a, Vector3 b, Vector3 c, Shade shade)
        {
            a = Transform.MultiplyPoint3x4(a); b = Transform.MultiplyPoint3x4(b); c = Transform.MultiplyPoint3x4(c);
            Vector3 normal = Vector3.Cross(b - a, c - a).normalized;
            int i = _v.Count;
            _v.Add(a); _v.Add(b); _v.Add(c);
            for (int k = 0; k < 3; k++) _n.Add(normal);
            float w = (b - a).magnitude;
            Vector3 ab = (b - a).normalized;
            float cx = Vector3.Dot(c - a, ab), cy = (c - a - ab * cx).magnitude;
            AddUvs(shade, new[] { Vector2.zero, new Vector2(w, 0), new Vector2(cx, cy) });
            _t.Add(i); _t.Add(i + 1); _t.Add(i + 2);
        }

        private void AddUvs(Shade shade, Vector2[] local)
        {
            (Rect r, int palette) = Finishes[shade];
            var pal = new Vector2((palette + 0.5f) / 32f, 0.5f / 32f);
            for (int i = 0; i < local.Length; i++) _uv2.Add(pal);
            float minX = local.Min(p => p.x), minY = local.Min(p => p.y);
            float spanX = Mathf.Max(0.0001f, local.Max(p => p.x) - minX), spanY = Mathf.Max(0.0001f, local.Max(p => p.y) - minY);
            float su = Mathf.Min(spanX * TexelsPerMetre, r.width), sv = Mathf.Min(spanY * TexelsPerMetre, r.height);
            foreach (Vector2 p in local)
                _uv.Add(new Vector2(r.xMin + (p.x - minX) / spanX * su, r.yMin + (p.y - minY) / spanY * sv));
        }

        public Mesh Build(string name)
        {
            var m = new Mesh { name = name };
            if (_v.Count > 65000) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            m.SetVertices(_v);
            m.SetNormals(_n);
            m.SetUVs(0, _uv);
            m.SetUVs(1, _uv2);
            m.SetTriangles(_t, 0);
            m.RecalculateBounds();
            m.RecalculateTangents();
            return m;
        }
    }
}
