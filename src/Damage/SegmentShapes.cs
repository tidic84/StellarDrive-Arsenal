using System.Collections.Generic;
using System.Linq;
using Ships.Interface.Model.Parts;
using Ships.Parts.Common.Hull;
using UnityEngine;

namespace Arsenal.Damage
{
    /// <summary>
    /// The outline of each kind of hull piece. The game draws every piece (flat panel, wall/floor corner,
    /// slope, slope corner) as a slab lying in the X/Y plane of its own frame, placed with the part's placement;
    /// only the outline changes (square, triangle, 1 x 1.41 rectangle...). The outlines are read from the game's
    /// own segment meshes, so holes, hit tests and breaking work the same on every shape.
    /// </summary>
    internal sealed class SegmentShape
    {
        public Vector2[] Outline;   // convex, counter-clockwise, in the piece's X/Y plane
        public float ZCenter;       // the slab's middle plane
        public float HalfThickness;
        public Rect Bounds;

        /// <summary>Signed distance to the outline (positive inside).</summary>
        public float Inside(Vector2 p)
        {
            float d = float.MaxValue;
            for (int i = 0; i < Outline.Length; i++)
            {
                Vector2 a = Outline[i], b = Outline[(i + 1) % Outline.Length];
                Vector2 e = (b - a).normalized;
                Vector2 n = new Vector2(-e.y, e.x); // inward for a CCW outline
                d = Mathf.Min(d, Vector2.Dot(p - a, n));
            }
            return d;
        }

        /// <summary>How far a point of the plane is from the piece (0 when on it).</summary>
        public float DistanceTo(Vector2 p) => Mathf.Max(0f, -Inside(p));

        /// <summary>Share of the piece's area taken by holes (0..1).</summary>
        public float Coverage(HoleMask holes)
        {
            if (holes.IsEmpty) return 0f;
            const int n = 32;
            int inside = 0, gone = 0;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    var p = new Vector2(Bounds.xMin + (x + 0.5f) / n * Bounds.width, Bounds.yMin + (y + 0.5f) / n * Bounds.height);
                    if (Inside(p) <= 0f) continue;
                    inside++;
                    if (holes.IsHoleAt(p.x, p.y)) gone++;
                }
            return inside > 0 ? gone / (float)inside : 0f;
        }
    }

    internal static class SegmentShapes
    {
        private static readonly Dictionary<HullMeshSegmentType, SegmentShape> Shapes = new Dictionary<HullMeshSegmentType, SegmentShape>();

        private static readonly SegmentShape Square = Make(new[] { new Vector2(-0.5f, -0.5f), new Vector2(0.5f, -0.5f), new Vector2(0.5f, 0.5f), new Vector2(-0.5f, 0.5f) }, 0f, 0.04f);

        public static SegmentShape Get(HullMeshSegmentType type)
        {
            if (Shapes.TryGetValue(type, out SegmentShape s) && s != null) return s;
            s = FromGameMesh(type) ?? Square;
            Shapes[type] = s;
            return s;
        }

        public static SegmentShape Get(HullPart part) => Get(part.SegmentType);

        /// <summary>
        /// True while we read a game mesh. GetSegmentMesh is patched to hand back holed meshes, and building one needs
        /// this very shape, so the patch must leave the call alone then, or it loops until the stack overflows.
        /// </summary>
        public static bool ReadingGameMesh { get; private set; }

        private static SegmentShape FromGameMesh(HullMeshSegmentType type)
        {
            HullMeshSegmentsSettings settings = Resources.FindObjectsOfTypeAll<HullMeshSegmentsSettings>().FirstOrDefault();
            Mesh mesh = null;
            if (settings != null)
            {
                ReadingGameMesh = true;
                try { mesh = settings.GetSegmentMesh(type); }
                finally { ReadingGameMesh = false; }
            }
            if (mesh == null || !mesh.isReadable) return null;
            Vector3[] v = mesh.vertices;
            if (v.Length < 3) return null;
            float zMin = v.Min(p => p.z), zMax = v.Max(p => p.z);
            Vector2[] hull = ConvexHull(v.Select(p => new Vector2(p.x, p.y)).ToList());
            if (hull.Length < 3) return null;
            SegmentShape s = Make(hull, (zMin + zMax) * 0.5f, Mathf.Max(0.02f, (zMax - zMin) * 0.5f));
#if DEV
            ArsenalMod.Log.Msg($"[DEV] segment {type}: {hull.Length} corners, bounds {s.Bounds}, z {s.ZCenter:0.000} ±{s.HalfThickness:0.000}");
#endif
            return s;
        }

        private static SegmentShape Make(Vector2[] outline, float z, float half)
        {
            float x0 = outline.Min(p => p.x), x1 = outline.Max(p => p.x), y0 = outline.Min(p => p.y), y1 = outline.Max(p => p.y);
            return new SegmentShape { Outline = outline, ZCenter = z, HalfThickness = half, Bounds = Rect.MinMaxRect(x0, y0, x1, y1) };
        }

        /// <summary>Andrew's monotone chain; counter-clockwise, without collinear points.</summary>
        private static Vector2[] ConvexHull(List<Vector2> pts)
        {
            pts = pts.Select(p => new Vector2(Mathf.Round(p.x * 10000f) / 10000f, Mathf.Round(p.y * 10000f) / 10000f)).Distinct().OrderBy(p => p.x).ThenBy(p => p.y).ToList();
            if (pts.Count < 3) return pts.ToArray();
            float Cross(Vector2 o, Vector2 a, Vector2 b) => (a.x - o.x) * (b.y - o.y) - (a.y - o.y) * (b.x - o.x);
            var lower = new List<Vector2>();
            foreach (Vector2 p in pts)
            {
                while (lower.Count >= 2 && Cross(lower[lower.Count - 2], lower[lower.Count - 1], p) <= 1e-7f) lower.RemoveAt(lower.Count - 1);
                lower.Add(p);
            }
            var upper = new List<Vector2>();
            for (int i = pts.Count - 1; i >= 0; i--)
            {
                Vector2 p = pts[i];
                while (upper.Count >= 2 && Cross(upper[upper.Count - 2], upper[upper.Count - 1], p) <= 1e-7f) upper.RemoveAt(upper.Count - 1);
                upper.Add(p);
            }
            lower.RemoveAt(lower.Count - 1);
            upper.RemoveAt(upper.Count - 1);
            return lower.Concat(upper).ToArray();
        }
    }
}
