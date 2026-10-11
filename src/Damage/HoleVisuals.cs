using System.Collections.Generic;
using Arsenal.Core;
using HarmonyLib;
using Ships;
using Ships.Interface.Model;
using Ships.Interface.Model.Parts;
using Ships.Interface.Model.Placement;
using Ships.Parts.Common.Hull;
using Ships.Services;
using Ships.Visuals;
using UnityEngine;

namespace Arsenal.Damage
{
    /// <summary>
    /// Draws holes by swapping the game's flat hull segment mesh for a perforated copy while the chunk mesh is built.
    /// The game then applies its own colours, UVs and materials to it, so damaged panels keep the native look.
    /// </summary>
    internal static class HoleVisuals
    {

        private static readonly Dictionary<(HullMeshSegmentType, HoleMask), Mesh> MeshCache = new Dictionary<(HullMeshSegmentType, HoleMask), Mesh>();
        private static readonly Dictionary<ShipHullVisuals, uint> VisualOwners = new Dictionary<ShipHullVisuals, uint>();

        // Build context, set by the patches below.
        private static bool _inChunk;
        private static uint _chunkShipId;
        private static bool _hasCurrentPart;
        private static HullPartPosition _currentPart;

        public static void Patch(HarmonyLib.Harmony h)
        {
            h.Patch(AccessTools.Method(typeof(ShipHullVisualChunk), nameof(ShipHullVisualChunk.UpdateMesh)),
                prefix: new HarmonyMethod(typeof(HoleVisuals), nameof(ChunkPrefix)),
                finalizer: new HarmonyMethod(typeof(HoleVisuals), nameof(ChunkFinalizer)));
            h.Patch(AccessTools.Method(typeof(HullMeshBuilder), "AddHullSegment"),
                prefix: new HarmonyMethod(typeof(HoleVisuals), nameof(SegmentPrefix)),
                finalizer: new HarmonyMethod(typeof(HoleVisuals), nameof(SegmentFinalizer)));
            h.Patch(AccessTools.Method(typeof(HullMeshSegmentsSettings), nameof(HullMeshSegmentsSettings.GetSegmentMesh)),
                postfix: new HarmonyMethod(typeof(HoleVisuals), nameof(GetSegmentMeshPostfix)));
        }

        public static void ClearCaches()
        {
            VisualOwners.Clear();
            foreach (Mesh m in MeshCache.Values)
                if (m != null) Object.Destroy(m);
            MeshCache.Clear();
        }

        private static void ChunkPrefix(ShipHullVisualChunk __instance)
        {
            _inChunk = false;
            ShipHullVisuals hv = __instance.transform.parent != null ? __instance.transform.parent.GetComponent<ShipHullVisuals>() : null;
            if (hv != null && TryGetShipId(hv, out uint id))
            {
                _inChunk = true;
                _chunkShipId = id;
            }
        }

        private static System.Exception ChunkFinalizer(System.Exception __exception)
        {
            _inChunk = false;
            return __exception;
        }

        private static void SegmentPrefix(HullPart part)
        {
            _hasCurrentPart = true;
            _currentPart = part.HullPlacement.Position;
        }

        private static System.Exception SegmentFinalizer(System.Exception __exception)
        {
            _hasCurrentPart = false;
            return __exception;
        }

        private static void GetSegmentMeshPostfix(HullMeshSegmentType type, ref Mesh __result)
        {
            if (!_inChunk || !_hasCurrentPart || SegmentShapes.ReadingGameMesh) return;
            if (!HullDamage.TryGetClientHoles(new PanelKey(_chunkShipId, _currentPart), out HoleMask holes) || holes.IsEmpty) return;
            __result = GetMesh(holes, type);
        }

        private static bool TryGetShipId(ShipHullVisuals hv, out uint id)
        {
            if (VisualOwners.TryGetValue(hv, out id)) return true;
            ShipsClientTracker ships = Svc.ShipsClient;
            if (ships == null) return false;
            foreach (TrackedShipClient ship in ships.Ships)
            {
                if (!ship.IsVisualLoaded || ship.VisualObject == null) continue;
                if (ship.VisualObject.GetComponent<ShipHullVisuals>() == hv)
                {
                    VisualOwners[hv] = ship.Id;
                    id = ship.Id;
                    return true;
                }
            }
            return false;
        }

        /// <summary>Asks the game to rebuild the chunk holding this panel.</summary>
        public static void Refresh(PanelKey key)
        {
            ShipsClientTracker ships = Svc.ShipsClient;
            if (ships == null || !ships.TryGetTrackedShip(key.ShipId, out TrackedShipClient ship) || !ship.IsVisualLoaded) return;
            HullPart? part = ship.GetVoxel(key.Pos.VoxelCoord)?.GetPartFromType(key.Pos.Type);
            if (!part.HasValue) return;
            ShipHullVisuals hv = ship.VisualObject.GetComponent<ShipHullVisuals>();
            if (hv == null) return;
            VisualOwners[hv] = ship.Id;
            hv.UpdateHullPart(new HullPartUpdateEvent(part.Value, part.Value.Color));
        }

        // ------------------------------------------------------------------ mesh

        public static Mesh GetMesh(HoleMask holes, HullMeshSegmentType type)
        {
            if (MeshCache.TryGetValue((type, holes), out Mesh m) && m != null) return m;
            if (MeshCache.Count > 512) ClearMeshCacheOnly();
            m = BuildMesh(holes, SegmentShapes.Get(type));
            MeshCache[(type, holes)] = m;
            return m;
        }

        private static void ClearMeshCacheOnly()
        {
            foreach (Mesh old in MeshCache.Values)
                if (old != null) Object.Destroy(old);
            MeshCache.Clear();
        }

        /// <summary>
        /// A hull piece with round holes, any shape: marching squares over the signed distance to "solid"
        /// (inside the piece's outline and outside every hole), on a ~1.6 cm grid, with edge points interpolated so
        /// circles come out smooth and straight edges stay straight. Full cells are merged into strips; cells crossed
        /// by an edge get their solid part plus a wall following it (hole walls and the piece's own rim).
        /// </summary>
        private static Mesh BuildMesh(HoleMask holes, SegmentShape shape)
        {
            Rect b = shape.Bounds;
            const float target = 1f / 64f;
            int nx = Mathf.Max(8, Mathf.CeilToInt(b.width / target)), ny = Mathf.Max(8, Mathf.CeilToInt(b.height / target));
            float csx = b.width / nx, csy = b.height / ny;
            var v = new List<Vector3>(4096);
            var n = new List<Vector3>(4096);
            var t = new List<int>(8192);
            float zf = shape.ZCenter + shape.HalfThickness, zb = shape.ZCenter - shape.HalfThickness;
            float Solid(Vector2 p) => Mathf.Min(shape.Inside(p), holes.Distance(p.x, p.y));

            var f = new float[nx + 1, ny + 1];
            for (int j = 0; j <= ny; j++)
                for (int i = 0; i <= nx; i++)
                {
                    float d = Solid(new Vector2(b.xMin + i * csx, b.yMin + j * csy));
                    f[i, j] = Mathf.Abs(d) < 0.00001f ? -0.00001f : d; // exactly on an edge counts as outside
                }

            var poly = new List<(Vector2 p, bool edge)>(8);
            for (int j = 0; j < ny; j++)
            {
                int runStart = -1;
                for (int i = 0; i <= nx; i++)
                {
                    bool full = i < nx && f[i, j] > 0f && f[i + 1, j] > 0f && f[i + 1, j + 1] > 0f && f[i, j + 1] > 0f;
                    if (full)
                    {
                        if (runStart < 0) runStart = i;
                        continue;
                    }
                    if (runStart >= 0)
                    {
                        float x0 = b.xMin + runStart * csx, x1 = b.xMin + i * csx, y0 = b.yMin + j * csy, y1 = y0 + csy;
                        Quad(v, n, t, new Vector3(x0, y1, zf), new Vector3(x0, y0, zf), new Vector3(x1, y0, zf), new Vector3(x1, y1, zf), Vector3.forward);
                        Quad(v, n, t, new Vector3(x1, y1, zb), new Vector3(x1, y0, zb), new Vector3(x0, y0, zb), new Vector3(x0, y1, zb), Vector3.back);
                        runStart = -1;
                    }
                    if (i == nx) break;
                    float a = f[i, j], bb = f[i + 1, j], c = f[i + 1, j + 1], dd = f[i, j + 1];
                    if (a <= 0f && bb <= 0f && c <= 0f && dd <= 0f) continue;
                    Vector2 p0 = new Vector2(b.xMin + i * csx, b.yMin + j * csy);
                    Vector2[] corner = { p0, p0 + new Vector2(csx, 0), p0 + new Vector2(csx, csy), p0 + new Vector2(0, csy) };
                    float[] val = { a, bb, c, dd };
                    poly.Clear();
                    for (int k = 0; k < 4; k++)
                    {
                        int k2 = (k + 1) & 3;
                        if (val[k] > 0f) poly.Add((corner[k], false));
                        if ((val[k] > 0f) != (val[k2] > 0f))
                        {
                            float s = val[k] / (val[k] - val[k2]);
                            poly.Add((Vector2.Lerp(corner[k], corner[k2], s), true));
                        }
                    }
                    if (poly.Count < 3) continue;
                    for (int k = 1; k + 1 < poly.Count; k++)
                    {
                        Tri(v, n, t, V(poly[0].p, zf), V(poly[k].p, zf), V(poly[k + 1].p, zf), Vector3.forward);
                        Tri(v, n, t, V(poly[0].p, zb), V(poly[k + 1].p, zb), V(poly[k].p, zb), Vector3.back);
                    }
                    // walls along edges (hole rims and the piece's outline), facing away from the solid
                    for (int k = 0; k < poly.Count; k++)
                    {
                        var e0 = poly[k];
                        var e1 = poly[(k + 1) % poly.Count];
                        if (!e0.edge || !e1.edge) continue;
                        Vector2 dir = e1.p - e0.p;
                        if (dir.sqrMagnitude < 1e-10f) continue;
                        Vector2 mid = (e0.p + e1.p) * 0.5f;
                        Vector2 nrm = new Vector2(dir.y, -dir.x).normalized;
                        if (Solid(mid + nrm * 0.004f) > Solid(mid - nrm * 0.004f)) nrm = -nrm;
                        Quad(v, n, t, V(e0.p, zb), V(e1.p, zb), V(e1.p, zf), V(e0.p, zf), new Vector3(nrm.x, nrm.y, 0f));
                    }
                }
            }

            var mesh = new Mesh { name = "Hull segment (Arsenal holes)" };
            if (v.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(v);
            mesh.SetNormals(n);
            mesh.SetTriangles(t, 0);
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Vector3 V(Vector2 p, float z) => new Vector3(p.x, p.y, z);

        private static void Tri(List<Vector3> v, List<Vector3> n, List<int> t, Vector3 a, Vector3 b, Vector3 c, Vector3 normal)
        {
            int i = v.Count;
            v.Add(a); v.Add(b); v.Add(c);
            n.Add(normal); n.Add(normal); n.Add(normal);
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), normal) >= 0f) { t.Add(i); t.Add(i + 1); t.Add(i + 2); }
            else { t.Add(i); t.Add(i + 2); t.Add(i + 1); }
        }

        /// <summary>Adds a quad, flipping the winding if needed so it faces <paramref name="normal"/> (the game's convention).</summary>
        private static void Quad(List<Vector3> v, List<Vector3> n, List<int> t, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal)
        {
            int i = v.Count;
            v.Add(a); v.Add(b); v.Add(c); v.Add(d);
            n.Add(normal); n.Add(normal); n.Add(normal); n.Add(normal);
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), normal) >= 0f)
            {
                t.Add(i); t.Add(i + 1); t.Add(i + 2);
                t.Add(i); t.Add(i + 2); t.Add(i + 3);
            }
            else
            {
                t.Add(i); t.Add(i + 2); t.Add(i + 1);
                t.Add(i); t.Add(i + 3); t.Add(i + 2);
            }
        }
    }
}
