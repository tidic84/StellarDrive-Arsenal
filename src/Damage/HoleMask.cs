using System;
using System.IO;
using Ships.Interface.Model.Placement;

namespace Arsenal.Damage
{
    /// <summary>Which cells of a 1x1 m hull panel are punched out: a 16x16 grid (6.25 cm cells), one bit per cell.</summary>
    internal struct HoleMask : IEquatable<HoleMask>
    {
        public const int Size = 16;
        public const int Cells = Size * Size;
        public const float CellSize = 1f / Size;

        public ulong A, B, C, D;

        public bool IsEmpty => (A | B | C | D) == 0;

        public bool Get(int x, int y)
        {
            if (x < 0 || y < 0 || x >= Size || y >= Size) return false;
            int i = y * Size + x;
            return (Word(i >> 6) & (1UL << (i & 63))) != 0;
        }

        public void Set(int x, int y)
        {
            if (x < 0 || y < 0 || x >= Size || y >= Size) return;
            int i = y * Size + x;
            ulong bit = 1UL << (i & 63);
            switch (i >> 6)
            {
                case 0: A |= bit; break;
                case 1: B |= bit; break;
                case 2: C |= bit; break;
                default: D |= bit; break;
            }
        }

        private ulong Word(int w) => w == 0 ? A : w == 1 ? B : w == 2 ? C : D;

        public int Count => PopCount(A) + PopCount(B) + PopCount(C) + PopCount(D);

        public HoleMask Union(HoleMask o) => new HoleMask { A = A | o.A, B = B | o.B, C = C | o.C, D = D | o.D };

        /// <summary>Cell under a panel-local point (x, y in -0.5..0.5).</summary>
        public static bool CellAt(float px, float py, out int x, out int y)
        {
            x = (int)Math.Floor((px + 0.5f) * Size);
            y = (int)Math.Floor((py + 0.5f) * Size);
            if (x < 0 || y < 0 || x >= Size || y >= Size) return false;
            return true;
        }

        public bool IsHoleAt(float px, float py) => CellAt(px, py, out int x, out int y) && Get(x, y);

        /// <summary>Punches a roughly round hole of the given radius (metres) around a panel-local point.</summary>
        public void PunchDisc(float px, float py, float radius, Random rng = null)
        {
            float r2 = radius * radius;
            int span = (int)Math.Ceiling(radius * Size) + 1;
            CellAt(Math.Max(-0.4999f, Math.Min(0.4999f, px)), Math.Max(-0.4999f, Math.Min(0.4999f, py)), out int cx, out int cy);
            for (int y = cy - span; y <= cy + span; y++)
                for (int x = cx - span; x <= cx + span; x++)
                {
                    float mx = (x + 0.5f) * CellSize - 0.5f - px;
                    float my = (y + 0.5f) * CellSize - 0.5f - py;
                    float d2 = mx * mx + my * my;
                    // Ragged edges: cells near the rim only break part of the time.
                    float rim = rng != null ? (float)rng.NextDouble() * 0.35f + 0.8f : 1f;
                    if (d2 <= r2 * rim || (x == cx && y == cy))
                        Set(x, y);
                }
        }

        public void Write(BinaryWriter w)
        {
            w.Write(A); w.Write(B); w.Write(C); w.Write(D);
        }

        public static HoleMask Read(BinaryReader r) => new HoleMask { A = r.ReadUInt64(), B = r.ReadUInt64(), C = r.ReadUInt64(), D = r.ReadUInt64() };

        public bool Equals(HoleMask o) => A == o.A && B == o.B && C == o.C && D == o.D;
        public override bool Equals(object obj) => obj is HoleMask o && Equals(o);
        public override int GetHashCode() => HashCode.Combine(A, B, C, D);

        private static int PopCount(ulong v)
        {
            int c = 0;
            while (v != 0) { v &= v - 1; c++; }
            return c;
        }
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
