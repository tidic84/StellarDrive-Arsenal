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

        private static Mesh BuildMesh(HoleMask holes)
        {
            var v = new List<Vector3>(1024);
            var n = new List<Vector3>(1024);
            var t = new List<int>(2048);
            const int S = HoleMask.Size;
            const float c = HoleMask.CellSize;
            float z = HalfThickness;

            for (int y = 0; y < S; y++)
            {
                int x = 0;
                while (x < S)
                {
                    if (holes.Get(x, y)) { x++; continue; }
                    int start = x;
                    while (x < S && !holes.Get(x, y)) x++;
                    float x0 = start * c - 0.5f, x1 = x * c - 0.5f;
                    float y0 = y * c - 0.5f, y1 = (y + 1) * c - 0.5f;
                    // front (+z) and back (-z) faces of this solid run
                    Quad(v, n, t, new Vector3(x0, y1, z), new Vector3(x0, y0, z), new Vector3(x1, y0, z), new Vector3(x1, y1, z), Vector3.forward);
                    Quad(v, n, t, new Vector3(x1, y1, -z), new Vector3(x1, y0, -z), new Vector3(x0, y0, -z), new Vector3(x0, y1, -z), Vector3.back);
                }
            }

            // inner walls of the holes
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    if (holes.Get(x, y)) continue;
                    float x0 = x * c - 0.5f, x1 = x0 + c, y0 = y * c - 0.5f, y1 = y0 + c;
                    if (x + 1 < S && holes.Get(x + 1, y))
                        Quad(v, n, t, new Vector3(x1, y0, -z), new Vector3(x1, y1, -z), new Vector3(x1, y1, z), new Vector3(x1, y0, z), Vector3.right);
                    if (x - 1 >= 0 && holes.Get(x - 1, y))
                        Quad(v, n, t, new Vector3(x0, y0, z), new Vector3(x0, y1, z), new Vector3(x0, y1, -z), new Vector3(x0, y0, -z), Vector3.left);
                    if (y + 1 < S && holes.Get(x, y + 1))
                        Quad(v, n, t, new Vector3(x0, y1, -z), new Vector3(x0, y1, z), new Vector3(x1, y1, z), new Vector3(x1, y1, -z), Vector3.up);
                    if (y - 1 >= 0 && holes.Get(x, y - 1))
                        Quad(v, n, t, new Vector3(x0, y0, z), new Vector3(x0, y0, -z), new Vector3(x1, y0, -z), new Vector3(x1, y0, z), Vector3.down);
                }

            var mesh = new Mesh { name = "Flat (Arsenal holes)" };
            mesh.SetVertices(v);
            mesh.SetNormals(n);
            mesh.SetTriangles(t, 0);
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
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
