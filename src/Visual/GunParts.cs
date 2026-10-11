using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Arsenal.Visual
{
    /// <summary>
    /// Reusable gun details built with MeshKit: grips, guards, rails, barrels, muzzles, sights, magazines.
    /// Coordinates are metres, +z forward, y up; outlines are given in the (z, y) side plane.
    /// </summary>
    internal static class GunParts
    {
        // ------------------------------------------------------------------ outline helpers

        public static Vector2 P(float z, float y) => new Vector2(z, y);

        /// <summary>Rotates outline points around a pivot in the side plane (degrees, counter-clockwise).</summary>
        public static Vector2[] Rot(IEnumerable<Vector2> pts, Vector2 pivot, float degrees)
        {
            float a = degrees * Mathf.Deg2Rad, c = Mathf.Cos(a), s = Mathf.Sin(a);
            return pts.Select(p =>
            {
                Vector2 d = p - pivot;
                return pivot + new Vector2(d.x * c - d.y * s, d.x * s + d.y * c);
            }).ToArray();
        }

        public static Vector2[] Move(IEnumerable<Vector2> pts, float dz, float dy) => pts.Select(p => p + new Vector2(dz, dy)).ToArray();

        /// <summary>Same outline, both faces, centred on x = 0.</summary>
        public static void Side(MeshKit k, IEnumerable<Vector2> outline, float width, Shade faces, Shade walls, float chamfer = 0.003f, float x = 0f) =>
            k.Profile(outline.ToArray(), x - width * 0.5f, x + width * 0.5f, faces, walls, chamfer);

        /// <summary>A thin decal-like plate standing proud of both sides of a part (panels, stripes, ports).</summary>
        public static void Plates(MeshKit k, IEnumerable<Vector2> outline, float halfWidth, Shade shade, float thickness = 0.0015f, bool both = true)
        {
            Vector2[] o = outline.ToArray();
            k.Profile(o, halfWidth, halfWidth + thickness, shade, shade, 0f);
            if (both) k.Profile(o, -halfWidth - thickness, -halfWidth, shade, shade, 0f);
        }

        public static Vector2[] Rect(float z0, float y0, float z1, float y1) => new[] { P(z0, y0), P(z1, y0), P(z1, y1), P(z0, y1) };

        /// <summary>Rectangle with its four corners cut.</summary>
        public static Vector2[] RectC(float z0, float y0, float z1, float y1, float c) => new[]
        {
            P(z0 + c, y0), P(z1 - c, y0), P(z1, y0 + c), P(z1, y1 - c), P(z1 - c, y1), P(z0 + c, y1), P(z0, y1 - c), P(z0, y0 + c),
        };

        // ------------------------------------------------------------------ transforms

        public static void With(MeshKit k, Vector3 pos, Vector3 euler, System.Action body)
        {
            Matrix4x4 saved = k.Transform;
            k.Transform = saved * Matrix4x4.TRS(pos, Quaternion.Euler(euler), Vector3.one);
            body();
            k.Transform = saved;
        }

        // ------------------------------------------------------------------ parts

        /// <summary>
        /// Ergonomic pistol grip whose top-front corner sits at (z, y), raked back by <paramref name="rake"/> degrees,
        /// with finger grooves, a beavertail and stippled side panels.
        /// </summary>
        public static void Grip(MeshKit k, float z, float y, Shade shade, float rake = 16f, float height = 0.1f, float width = 0.031f, Shade? panel = null)
        {
            float h = height;
            var outline = new List<Vector2>
            {
                P(z - 0.052f, y + 0.002f), P(z + 0.002f, y + 0.002f),
                P(z + 0.002f, y - 0.018f), P(z + 0.007f, y - 0.03f), P(z + 0.002f, y - 0.042f), P(z + 0.007f, y - 0.055f),
                P(z + 0.002f, y - 0.067f), P(z + 0.006f, y - h + 0.01f), P(z + 0.004f, y - h),
                P(z - 0.044f, y - h), P(z - 0.048f, y - h + 0.012f), P(z - 0.05f, y - 0.03f), P(z - 0.058f, y - 0.008f),
            };
            Vector2[] o = Rot(outline, P(z, y), -rake);
            Side(k, o, width, shade, MeshKit.FlatOf(shade), 0.0035f);
            // stippled side panels
            var pn = Rot(RectC(z - 0.042f, y - h + 0.016f, z - 0.006f, y - 0.016f, 0.006f), P(z, y), -rake);
            Plates(k, pn, width * 0.5f, panel ?? Shade.HatchDark, 0.0012f);
            // base plate
            var bp = Rot(Rect(z - 0.047f, y - h - 0.007f, z + 0.006f, y - h + 0.001f), P(z, y), -rake);
            Side(k, bp, width + 0.003f, Shade.Black, Shade.Black, 0.002f);
        }

        /// <summary>Trigger guard (an open U band) and a curved trigger; the trigger sits at z.</summary>
        public static void TriggerGroup(MeshKit k, float z, float y, float length = 0.055f)
        {
            float front = z + length * 0.55f, back = z - length * 0.45f;
            var band = new List<Vector2>
            {
                P(front, y), P(front + 0.006f, y), P(front + 0.006f, y - 0.022f), P(front - 0.006f, y - 0.031f), P(back, y - 0.031f), P(back, y - 0.025f),
                P(front - 0.008f, y - 0.025f), P(front, y - 0.019f),
            };
            Side(k, band, 0.011f, Shade.Dark, Shade.Black, 0.002f);
            var trig = new List<Vector2> { P(z + 0.004f, y), P(z + 0.01f, y), P(z + 0.008f, y - 0.012f), P(z + 0.002f, y - 0.021f), P(z - 0.002f, y - 0.02f), P(z + 0.003f, y - 0.01f) };
            Side(k, trig, 0.006f, Shade.Black, Shade.Black, 0.001f);
        }

        /// <summary>Picatinny-style rail on top of something, from z0 to z1 with its base at height y.</summary>
        public static void Rail(MeshKit k, float y, float z0, float z1, float width = 0.022f, Shade shade = Shade.Black)
        {
            k.ChamferBox(new Vector3(0, y + 0.003f, (z0 + z1) * 0.5f), new Vector3(width, 0.006f, z1 - z0), 0.0015f, shade, shade);
            for (float z = z0 + 0.006f; z < z1 - 0.004f; z += 0.01f)
                k.Box(new Vector3(0, y + 0.0075f, z), new Vector3(width + 0.002f, 0.003f, 0.005f), Shade.Dark);
        }

        /// <summary>Smooth barrel with a few steps (profile of radius at z positions).</summary>
        public static void Barrel(MeshKit k, float y, float z0, float z1, float r, Shade shade = Shade.Dark, float bore = 0.0f)
        {
            k.Lathe(new Vector2(0, y), new[] { P(r, z0), P(r, z1) }, 14, shade, MeshKit.FlatOf(shade), Shade.Black);
            if (bore > 0f) k.Tube(new Vector3(0, y, z1 + 0.0005f), r * 0.98f, bore, 0.003f, 14, shade, Shade.Black);
        }

        /// <summary>Muzzle brake: a short fat sleeve with side ports and a dark bore, front face at z.</summary>
        public static void MuzzleBrake(MeshKit k, float y, float z, float r, float length, Shade shade = Shade.Dark)
        {
            k.Tube(new Vector3(0, y, z - length * 0.5f), r, r * 0.45f, length, 16, shade, Shade.Black);
            for (int i = 0; i < 3; i++)
            {
                float zz = z - length * (0.25f + i * 0.25f);
                k.Box(new Vector3(0, y, zz), new Vector3(r * 2.08f, r * 0.7f, length * 0.12f), Shade.Black);
            }
            k.Lathe(new Vector2(0, y), new[] { P(r * 0.8f, z - length - 0.004f), P(r, z - length) }, 16, shade);
        }

        /// <summary>Flash hider: slotted cone, front face at z.</summary>
        public static void FlashHider(MeshKit k, float y, float z, float r, float length)
        {
            k.Tube(new Vector3(0, y, z - length * 0.5f), r, r * 0.55f, length, 12, Shade.Dark, Shade.Black);
            for (int i = 0; i < 4; i++)
                GunParts.With(k, new Vector3(0, y, z - length * 0.35f), new Vector3(0, 0, i * 90f + 45f), () =>
                    k.Box(new Vector3(0, r, 0), new Vector3(r * 0.35f, r * 0.2f, length * 0.6f), Shade.Black));
        }

        /// <summary>Small screw head on the right (+1) or left (-1) face at x.</summary>
        public static void Screw(MeshKit k, float x, float y, float z, float r = 0.0028f)
        {
            With(k, new Vector3(x, y, z), new Vector3(0, x >= 0 ? 90f : -90f, 0), () =>
            {
                k.Lathe(Vector2.zero, new[] { P(r, -0.0005f), P(r, 0.0006f), P(r * 0.7f, 0.0012f) }, 8, Shade.Grey, null, Shade.Grey);
                k.Box(new Vector3(0, 0, 0.0012f), new Vector3(r * 1.4f, r * 0.3f, 0.0004f), Shade.Black);
            });
        }

        public static void Screws(MeshKit k, float halfWidth, params Vector2[] zy)
        {
            foreach (Vector2 p in zy)
            {
                Screw(k, halfWidth, p.y, p.x);
                Screw(k, -halfWidth, p.y, p.x);
            }
        }

        /// <summary>Row of dark vent slots cut across a part (both sides and top), centred at y.</summary>
        public static void Vents(MeshKit k, float y, float z0, float z1, int count, float width, float height, float slot = 0.006f)
        {
            for (int i = 0; i < count; i++)
            {
                float z = Mathf.Lerp(z0, z1, count == 1 ? 0.5f : (float)i / (count - 1));
                k.Box(new Vector3(0, y, z), new Vector3(width, height, slot), Shade.Black);
            }
        }

        /// <summary>Curved box magazine hanging from (z, y) (top front corner), curving forward.</summary>
        public static void CurvedMag(MeshKit k, float z, float y, float length, float depth, float width, float curve, Shade shade, Shade? baseShade = null)
        {
            var front = new List<Vector2>();
            var back = new List<Vector2>();
            int steps = 8;
            for (int i = 0; i <= steps; i++)
            {
                float t = (float)i / steps;
                float bend = curve * t * t;
                front.Add(P(z + bend, y - length * t));
                back.Add(P(z - depth + bend * 1.15f, y - length * t));
            }
            var outline = new List<Vector2>();
            outline.AddRange(back.AsEnumerable().Reverse());
            outline.AddRange(front);
            Side(k, outline, width, shade, Shade.Black, 0.003f);
            // ribs and base plate
            Vector2 b0 = back[steps], f0 = front[steps];
            Side(k, new[] { b0 + P(-0.003f, -0.006f), f0 + P(0.003f, -0.006f), f0 + P(0.003f, 0.002f), b0 + P(-0.003f, 0.002f) }, width + 0.004f, baseShade ?? Shade.Black, Shade.Black, 0.002f);
            for (int i = 2; i < steps; i += 2)
                Plates(k, new[] { back[i] + P(0.004f, 0.002f), front[i] + P(-0.004f, 0.002f), front[i] + P(-0.004f, -0.002f), back[i] + P(0.004f, -0.002f) }, width * 0.5f, Shade.Black, 0.001f);
        }

        /// <summary>Straight box magazine hanging from (z, y).</summary>
        public static void BoxMag(MeshKit k, float z, float y, float length, float depth, float width, Shade shade, float rake = 0f, Shade? baseShade = null)
        {
            Vector2[] o = Rot(RectC(z - depth, y - length, z, y + 0.005f, 0.003f), P(z, y), -rake);
            Side(k, o, width, shade, Shade.Black, 0.0025f);
            Vector2[] bp = Rot(RectC(z - depth - 0.003f, y - length - 0.006f, z + 0.003f, y - length + 0.001f, 0.002f), P(z, y), -rake);
            Side(k, bp, width + 0.004f, baseShade ?? Shade.Black, Shade.Black, 0.0015f);
        }

        /// <summary>Open reflex sight on a rail: base, side wings, hood and a red dot. The dot is at (z, y + 0.0145).</summary>
        public static void ReflexSight(MeshKit k, float z, float y)
        {
            Side(k, new[] { P(z - 0.03f, y), P(z + 0.026f, y), P(z + 0.022f, y + 0.008f), P(z - 0.026f, y + 0.008f) }, 0.028f, Shade.Dark, Shade.Black, 0.002f);
            // hood: two posts and a roof, open in the middle to see through
            foreach (float x in new[] { -0.0155f, 0.0155f })
                k.ChamferBox(new Vector3(x, y + 0.022f, z - 0.004f), new Vector3(0.004f, 0.03f, 0.014f), 0.0015f, Shade.Dark, Shade.Black);
            k.ChamferBox(new Vector3(0, y + 0.0385f, z - 0.004f), new Vector3(0.035f, 0.004f, 0.014f), 0.0015f, Shade.Dark, Shade.Black);
            k.Box(new Vector3(0, y + 0.0075f, z - 0.024f), new Vector3(0.012f, 0.006f, 0.006f), Shade.Black); // battery knob
            k.Box(new Vector3(0, y + 0.0145f, z + 0.004f), new Vector3(0.003f, 0.003f, 0.003f), Shade.Red);
            k.Box(new Vector3(0, y + 0.011f, z + 0.004f), new Vector3(0.0012f, 0.006f, 0.0012f), Shade.Dark);
        }

        /// <summary>Telescopic sight along z at height y (rings included), eyepiece at z0.</summary>
        public static void Scope(MeshKit k, float y, float z0, float z1, float tube = 0.017f, Shade body = Shade.Black, Shade lens = Shade.Blue)
        {
            float len = z1 - z0;
            k.Lathe(new Vector2(0, y), new[]
            {
                P(tube * 1.45f, z0), P(tube * 1.5f, z0 + 0.012f), P(tube * 1.45f, z0 + len * 0.18f), P(tube, z0 + len * 0.3f),
                P(tube, z0 + len * 0.62f), P(tube * 1.55f, z0 + len * 0.8f), P(tube * 1.6f, z1),
            }, 16, body, Shade.Black, lens);
            k.Lathe(new Vector2(0, y), new[] { P(tube * 1.3f, z0 - 0.0005f), P(tube * 1.3f, z0 + 0.0005f) }, 16, lens, lens, lens);
            // turrets
            With(k, new Vector3(0, y, z0 + len * 0.46f), new Vector3(-90f, 0, 0), () => k.Lathe(Vector2.zero, new[] { P(tube * 0.7f, 0f), P(tube * 0.7f, tube * 1.8f) }, 10, Shade.Dark, null, Shade.Grey));
            With(k, new Vector3(0, y, z0 + len * 0.46f), new Vector3(0, 90f, 0), () => k.Lathe(Vector2.zero, new[] { P(tube * 0.6f, 0f), P(tube * 0.6f, tube * 1.7f) }, 10, Shade.Dark, null, Shade.Grey));
        }

        /// <summary>Scope rings clamping onto a rail at height yRail, scope axis at yScope.</summary>
        public static void ScopeRings(MeshKit k, float yRail, float yScope, float tube, params float[] zs)
        {
            foreach (float z in zs)
            {
                k.Tube(new Vector3(0, yScope, z), tube * 1.3f, tube * 0.98f, 0.012f, 14, Shade.Dark, Shade.Black);
                k.ChamferBox(new Vector3(0, (yRail + yScope - tube) * 0.5f + 0.002f, z), new Vector3(0.016f, yScope - tube - yRail + 0.004f, 0.012f), 0.002f, Shade.Dark, Shade.Black);
            }
        }

        /// <summary>Iron sights: front post at zf, rear notch at zr, both on top of a surface at y.</summary>
        public static void IronSights(MeshKit k, float y, float zf, float zr, Shade dot = Shade.Yellow)
        {
            Side(k, new[] { P(zf - 0.008f, y), P(zf + 0.004f, y), P(zf + 0.002f, y + 0.009f), P(zf - 0.003f, y + 0.009f) }, 0.004f, Shade.Black, Shade.Black, 0.0008f);
            k.Box(new Vector3(0, y + 0.0075f, zf - 0.0015f), new Vector3(0.0045f, 0.0025f, 0.0025f), dot);
            foreach (float x in new[] { -0.0065f, 0.0065f })
                k.ChamferBox(new Vector3(x, y + 0.004f, zr), new Vector3(0.007f, 0.008f, 0.008f), 0.0012f, Shade.Black, Shade.Black);
        }

        /// <summary>A glowing strip / lens part goes in the glow kit; this adds its dark recessed frame to the body.</summary>
        public static void GlowFrame(MeshKit body, MeshKit glow, Vector3 center, Vector3 size)
        {
            body.Box(center, size + new Vector3(0.002f, 0.002f, 0.002f), Shade.Black);
            glow.Box(center + new Vector3(Mathf.Sign(center.x) * 0.0012f, 0, 0), new Vector3(size.x, size.y * 0.7f, size.z * 0.92f), Shade.White);
        }
    }
}
