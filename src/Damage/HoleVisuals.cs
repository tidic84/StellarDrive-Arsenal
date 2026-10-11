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
        private const float HalfThickness = 0.04f; // the game's flat segment is 1 x 1 x 0.08

        private static readonly Dictionary<HoleMask, Mesh> MeshCache = new Dictionary<HoleMask, Mesh>();
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
            if (!_inChunk || !_hasCurrentPart || type != HullMeshSegmentType.Flat) return;
            if (!HullDamage.TryGetClientHoles(new PanelKey(_chunkShipId, _currentPart), out HoleMask holes) || holes.IsEmpty) return;
            __result = GetMesh(holes);
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

        public static Mesh GetMesh(HoleMask holes)
        {
            if (MeshCache.TryGetValue(holes, out Mesh m) && m != null) return m;
            if (MeshCache.Count > 512) ClearMeshCacheOnly();
            m = BuildMesh(holes);
            MeshCache[holes] = m;
            return m;
        }

        private static void ClearMeshCacheOnly()
        {
            foreach (Mesh old in MeshCache.Values)
                if (old != null) Object.Destroy(old);
            MeshCache.Clear();
        }

        /// <summary>
        /// Panel with round holes: marching squares over the distance to the nearest hole edge, on a 64 x 64 grid
        /// (1.6 cm), with edge points interpolated so circles come out smooth. Full cells are merged into strips,
        /// cells crossed by a hole edge get their solid part plus a wall following the edge.
        /// </summary>
        private static Mesh BuildMesh(HoleMask holes)
        {
            const int N = 64;
            const float cs = 1f / N;
            var v = new List<Vector3>(4096);
            var n = new List<Vector3>(4096);
            var t = new List<int>(8192);
            float z = HalfThickness;

            var f = new float[N + 1, N + 1];
            for (int j = 0; j <= N; j++)
                for (int i = 0; i <= N; i++)
                {
                    float d = holes.Distance(i * cs - 0.5f, j * cs - 0.5f);
                    f[i, j] = Mathf.Abs(d) < 0.0001f ? 0.0001f : d; // never exactly on the edge
                }

            var poly = new List<(Vector2 p, bool edge)>(8);
            for (int j = 0; j < N; j++)
            {
                int runStart = -1;
                for (int i = 0; i <= N; i++)
                {
                    bool full = i < N && f[i, j] > 0f && f[i + 1, j] > 0f && f[i + 1, j + 1] > 0f && f[i, j + 1] > 0f;
                    if (full)
                    {
                        if (runStart < 0) runStart = i;
                        continue;
                    }
                    if (runStart >= 0)
                    {
                        // one strip for the run of solid cells
                        float x0 = runStart * cs - 0.5f, x1 = i * cs - 0.5f, y0 = j * cs - 0.5f, y1 = y0 + cs;
                        Quad(v, n, t, new Vector3(x0, y1, z), new Vector3(x0, y0, z), new Vector3(x1, y0, z), new Vector3(x1, y1, z), Vector3.forward);
                        Quad(v, n, t, new Vector3(x1, y1, -z), new Vector3(x1, y0, -z), new Vector3(x0, y0, -z), new Vector3(x0, y1, -z), Vector3.back);
                        runStart = -1;
                    }
                    if (i == N) break;
                    float a = f[i, j], b = f[i + 1, j], c = f[i + 1, j + 1], dd = f[i, j + 1];
                    if (a <= 0f && b <= 0f && c <= 0f && dd <= 0f) continue; // all hole
                    Vector2 p0 = new Vector2(i * cs - 0.5f, j * cs - 0.5f);
                    Vector2[] corner = { p0, p0 + new Vector2(cs, 0), p0 + new Vector2(cs, cs), p0 + new Vector2(0, cs) };
                    float[] val = { a, b, c, dd };
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
                        Tri(v, n, t, V(poly[0].p, z), V(poly[k].p, z), V(poly[k + 1].p, z), Vector3.forward);
                        Tri(v, n, t, V(poly[0].p, -z), V(poly[k + 1].p, -z), V(poly[k].p, -z), Vector3.back);
                    }
                    // walls along the hole edge, facing into the hole
                    for (int k = 0; k < poly.Count; k++)
                    {
                        var e0 = poly[k];
                        var e1 = poly[(k + 1) % poly.Count];
                        if (!e0.edge || !e1.edge) continue;
                        Vector2 dir = e1.p - e0.p;
                        if (dir.sqrMagnitude < 1e-10f) continue;
                        Vector2 mid = (e0.p + e1.p) * 0.5f;
                        Vector2 nrm = new Vector2(dir.y, -dir.x).normalized;
                        if (holes.Distance(mid.x + nrm.x * 0.004f, mid.y + nrm.y * 0.004f) > holes.Distance(mid.x - nrm.x * 0.004f, mid.y - nrm.y * 0.004f)) nrm = -nrm;
                        Vector3 wn = new Vector3(nrm.x, nrm.y, 0f);
                        Quad(v, n, t, V(e0.p, -z), V(e1.p, -z), V(e1.p, z), V(e0.p, z), wn);
                    }
                }
            }

            var mesh = new Mesh { name = "Flat (Arsenal holes)" };
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
