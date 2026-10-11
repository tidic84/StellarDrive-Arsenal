using System;
using System.Collections.Generic;
using System.IO;
using Ships.Interface.Model.Placement;

namespace Arsenal.Damage
{
    /// <summary>
    /// The holes in a 1 x 1 m hull panel: a set of round holes (centre and radius, about 1 mm precision).
    /// Overlapping circles make torn shapes; holes swallowed by bigger ones are dropped. Immutable value:
    /// every change returns a new set, so it can key mesh caches and be compared cheaply.
    /// </summary>
    internal readonly struct HoleMask : IEquatable<HoleMask>
    {
        public const int MaxHoles = 72;
        private const float PosScale = 255.75f; // x, y in -2..2 -> 0..1023 (centres may lie outside the panel: holes on seams, blasts)
        private const float RadScale = 1000f;   // radius in millimetres (12 bits: up to 4 m)

        // x: 10 bits | y: 10 bits | radius mm: 12 bits
        private readonly uint[] _p;
        private readonly int _hash;

        private HoleMask(uint[] packed)
        {
            _p = packed;
            int h = 17;
            if (packed != null)
                foreach (uint u in packed) h = h * 31 + (int)u;
            _hash = h;
        }

        public bool IsEmpty => _p == null || _p.Length == 0;
        public int Count => _p?.Length ?? 0;

        public (float x, float y, float r) this[int i]
        {
            get
            {
                uint u = _p[i];
                return ((u & 1023) / PosScale - 2f, ((u >> 10) & 1023) / PosScale - 2f, (u >> 20) / RadScale);
            }
        }

        private static uint Pack(float x, float y, float r)
        {
            uint px = (uint)Math.Max(0, Math.Min(1023, Math.Round((x + 2f) * PosScale)));
            uint py = (uint)Math.Max(0, Math.Min(1023, Math.Round((y + 2f) * PosScale)));
            uint pr = (uint)Math.Max(1, Math.Min(4095, Math.Round(r * RadScale)));
            return px | (py << 10) | (pr << 20);
        }

        /// <summary>A set holding one round hole (panel-local centre, metres).</summary>
        public static HoleMask Disc(float x, float y, float radius) => new HoleMask(new[] { Pack(x, y, radius) });

        /// <summary>
        /// Adds a round hole. With <paramref name="rng"/>, big holes (blasts) get a few smaller lobes around
        /// the rim, so they look torn rather than drilled; small ones (bullets) stay perfectly round.
        /// </summary>
        public HoleMask PunchDisc(float px, float py, float radius, Random rng = null)
        {
            var add = new List<uint> { Pack(px, py, radius) };
            if (rng != null && radius >= 0.09f)
            {
                int lobes = 3 + rng.Next(3);
                for (int i = 0; i < lobes; i++)
                {
                    double a = rng.NextDouble() * Math.PI * 2;
                    float d = radius * (0.55f + (float)rng.NextDouble() * 0.35f);
                    float lr = radius * (0.35f + (float)rng.NextDouble() * 0.25f);
                    add.Add(Pack(px + (float)Math.Cos(a) * d, py + (float)Math.Sin(a) * d, lr));
                }
            }
            return Union(new HoleMask(add.ToArray()));
        }

        public bool IsHoleAt(float px, float py)
        {
            if (_p == null) return false;
            for (int i = 0; i < _p.Length; i++)
            {
                (float x, float y, float r) = this[i];
                float dx = px - x, dy = py - y;
                if (dx * dx + dy * dy <= r * r) return true;
            }
            return false;
        }

        /// <summary>Signed distance to the nearest hole edge (negative inside a hole).</summary>
        public float Distance(float px, float py)
        {
            float best = 10f;
            if (_p == null) return best;
            for (int i = 0; i < _p.Length; i++)
            {
                (float x, float y, float r) = this[i];
                float dx = px - x, dy = py - y;
                float d = (float)Math.Sqrt(dx * dx + dy * dy) - r;
                if (d < best) best = d;
            }
            return best;
        }

        /// <summary>Share of the panel's area that is gone (0..1), sampled on a 32 x 32 grid.</summary>
        public float Coverage()
        {
            if (IsEmpty) return 0f;
            const int n = 32;
            int gone = 0;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                    if (IsHoleAt((x + 0.5f) / n - 0.5f, (y + 0.5f) / n - 0.5f)) gone++;
            return gone / (float)(n * n);
        }

        /// <summary>Both sets of holes; holes inside bigger ones are dropped and the count is capped by merging close holes.</summary>
        public HoleMask Union(HoleMask o)
        {
            if (o.IsEmpty) return this;
            if (IsEmpty) return o;
            var list = new List<(float x, float y, float r)>(Count + o.Count);
            for (int i = 0; i < Count; i++) list.Add(this[i]);
            for (int i = 0; i < o.Count; i++) list.Add(o[i]);
            // drop holes swallowed by another one
            for (int i = list.Count - 1; i >= 0; i--)
                for (int j = 0; j < list.Count; j++)
                {
                    if (i == j) continue;
                    float dx = list[i].x - list[j].x, dy = list[i].y - list[j].y;
                    if (Math.Sqrt(dx * dx + dy * dy) + list[i].r <= list[j].r + 0.001f && (list[i].r < list[j].r || i > j))
                    {
                        list.RemoveAt(i);
                        break;
                    }
                }
            // too many: replace the two closest holes by the circle around both
            while (list.Count > MaxHoles)
            {
                int bi = 0, bj = 1;
                float best = float.MaxValue;
                for (int i = 0; i < list.Count; i++)
                    for (int j = i + 1; j < list.Count; j++)
                    {
                        float dx = list[i].x - list[j].x, dy = list[i].y - list[j].y;
                        float gap = (float)Math.Sqrt(dx * dx + dy * dy) - list[i].r - list[j].r;
                        if (gap < best) { best = gap; bi = i; bj = j; }
                    }
                var a = list[bi];
                var b = list[bj];
                float ddx = b.x - a.x, ddy = b.y - a.y, dist = (float)Math.Sqrt(ddx * ddx + ddy * ddy);
                float r = (dist + a.r + b.r) * 0.5f;
                float t = dist > 0.0001f ? (r - a.r) / dist : 0f;
                list.RemoveAt(bj);
                list[bi] = (a.x + ddx * t, a.y + ddy * t, r);
            }
            var packed = new uint[list.Count];
            for (int i = 0; i < list.Count; i++) packed[i] = Pack(list[i].x, list[i].y, list[i].r);
            Array.Sort(packed);
            return new HoleMask(packed);
        }

        public void Write(BinaryWriter w)
        {
            w.Write((byte)Count);
            for (int i = 0; i < Count; i++) w.Write(_p[i]);
        }

        public static HoleMask Read(BinaryReader r)
        {
            int n = r.ReadByte();
            if (n == 0) return default;
            var p = new uint[n];
            for (int i = 0; i < n; i++) p[i] = r.ReadUInt32();
            return new HoleMask(p);
        }

        /// <summary>Text form for the save file: packed holes in hex, comma separated ("-" when none).</summary>
        public string ToSaveString()
        {
            if (IsEmpty) return "-";
            var parts = new string[Count];
            for (int i = 0; i < Count; i++) parts[i] = _p[i].ToString("x");
            return string.Join(",", parts);
        }

        public static HoleMask FromSaveString(string s)
        {
            if (string.IsNullOrEmpty(s) || s == "-") return default;
            string[] parts = s.Split(',');
            var p = new uint[parts.Length];
            for (int i = 0; i < parts.Length; i++) p[i] = Convert.ToUInt32(parts[i], 16);
            return new HoleMask(p);
        }

        /// <summary>Converts the old 16 x 16 cell masks (saves made before round holes) into round holes.</summary>
        public static HoleMask FromLegacyCells(ulong a, ulong b, ulong c, ulong d)
        {
            ulong[] words = { a, b, c, d };
            var list = new List<uint>();
            for (int i = 0; i < 256; i++)
                if ((words[i >> 6] & (1UL << (i & 63))) != 0)
                    list.Add(Pack((i % 16 + 0.5f) / 16f - 0.5f, (i / 16 + 0.5f) / 16f - 0.5f, 0.045f));
            HoleMask result = default;
            foreach (uint u in list) result = result.Union(new HoleMask(new[] { u }));
            return result;
        }

        public bool Equals(HoleMask o)
        {
            if (Count != o.Count || _hash != o._hash) return false;
            for (int i = 0; i < Count; i++)
                if (_p[i] != o._p[i]) return false;
            return true;
        }

        public override bool Equals(object obj) => obj is HoleMask o && Equals(o);
        public override int GetHashCode() => _hash;
    }

    internal struct PanelKey : IEquatable<PanelKey>
    {
        public uint ShipId;
        public HullPartPosition Pos;

        public PanelKey(uint shipId, HullPartPosition pos)
        {
            ShipId = shipId;
            Pos = pos;
        }

        public bool Equals(PanelKey o) => ShipId == o.ShipId && Pos == o.Pos;
        public override bool Equals(object obj) => obj is PanelKey o && Equals(o);
        public override int GetHashCode() => HashCode.Combine(ShipId, Pos);
        public override string ToString() => $"ship {ShipId} {Pos.Type} {Pos.VoxelCoord}";

        public void Write(BinaryWriter w)
        {
            w.Write(ShipId);
            w.Write(Pos.VoxelCoord.x); w.Write(Pos.VoxelCoord.y); w.Write(Pos.VoxelCoord.z);
            w.Write((byte)Pos.Type);
        }

        public static PanelKey Read(BinaryReader r)
        {
            uint ship = r.ReadUInt32();
            var c = new UnityEngine.Vector3Int(r.ReadInt32(), r.ReadInt32(), r.ReadInt32());
            var t = (Ships.Interface.Model.VoxelPartType)r.ReadByte();
            return new PanelKey(ship, new HullPartPosition(c, t));
        }
    }
}
