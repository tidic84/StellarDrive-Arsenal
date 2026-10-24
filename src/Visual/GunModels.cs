using UnityEngine;
using static Arsenal.Visual.GunParts;

namespace Arsenal.Visual
{
    /// <summary>
    /// The second wave of hand weapons. Same rules as WeaponModels: metres, +z forward, grip near the origin,
    /// the game's tool atlas and palette, plus glowing parts (a separate emissive mesh) for energy weapons.
    /// </summary>
    internal static class GunModels
    {
        private static Model Finish(string name, MeshKit body, MeshKit glow, Color glowColor, Vector3 muzzle, Vector3 sight, Vector3 rightHand, Vector3 leftHand)
        {
            var m = new Model { Root = new GameObject(name) };
            WeaponModels.MeshChild(m.Root.transform, "Body", body.Build("Arsenal." + name), Assets.ToolAtlas);
            if (glow != null)
                WeaponModels.MeshChild(m.Root.transform, "Glow", glow.Build("Arsenal." + name + ".Glow"), Assets.Glow(glowColor, 4f));
            m.Muzzle = WeaponModels.Point(m.Root.transform, "Muzzle", muzzle, Vector3.zero);
            WeaponModels.Point(m.Root.transform, "Sight", sight, Vector3.zero);
            WeaponModels.Hands(m, rightHand, leftHand);
            return m;
        }

        /// <summary>Butt stock with a cut-out, attached at zFront (top at yTop).</summary>
        private static void SkeletonStock(MeshKit k, float zFront, float yTop, float length, float width, Shade shade)
        {
            float z1 = zFront, z0 = zFront - length;
            Side(k, new[] { P(z0, yTop - 0.012f), P(z1, yTop - 0.008f), P(z1, yTop + 0.006f), P(z0 + 0.01f, yTop + 0.008f) }, width, shade, MeshKit.FlatOf(shade), 0.004f);
            Side(k, new[] { P(z0 + 0.01f, yTop - 0.09f), P(z0 + 0.06f, yTop - 0.09f), P(z1, yTop - 0.045f), P(z1, yTop - 0.03f), P(z0 + 0.055f, yTop - 0.072f), P(z0 + 0.01f, yTop - 0.072f) }, width * 0.9f, shade, MeshKit.FlatOf(shade), 0.0035f);
            Side(k, new[] { P(z0, yTop - 0.094f), P(z0 + 0.022f, yTop - 0.094f), P(z0 + 0.022f, yTop + 0.006f), P(z0, yTop + 0.008f) }, width, shade, MeshKit.FlatOf(shade), 0.004f);
            Side(k, new[] { P(z0 - 0.014f, yTop - 0.098f), P(z0, yTop - 0.098f), P(z0, yTop + 0.01f), P(z0 - 0.014f, yTop + 0.01f) }, width + 0.004f, Shade.Black, Shade.Black, 0.004f);
        }

        // ------------------------------------------------------------------ sidearms

        public static Model LaserPistol()
        {
            var k = new MeshKit();
            var g = new MeshKit();
            // flowing body: rounded back, long emitter housing
            var body = new System.Collections.Generic.List<Vector2> { P(-0.05f, 0.004f), P(0.14f, 0.004f), P(0.155f, 0.018f), P(0.155f, 0.044f), P(0.13f, 0.058f), P(-0.02f, 0.058f) };
            body.AddRange(MeshKit.Arc(P(-0.025f, 0.035f), 0.025f, 100f, 200f, 5));
            Side(k, body, 0.034f, Shade.White, Shade.Light, 0.007f);
            Plates(k, RectC(-0.02f, 0.014f, 0.13f, 0.024f, 0.004f), 0.017f, Shade.Dark, 0.0015f);
            GlowFrame(k, g, new Vector3(0.0185f, 0.04f, 0.06f), new Vector3(0.002f, 0.008f, 0.1f));
            GlowFrame(k, g, new Vector3(-0.0185f, 0.04f, 0.06f), new Vector3(0.002f, 0.008f, 0.1f));
            // heat sink fins on top
            for (int i = 0; i < 6; i++) k.ChamferBox(new Vector3(0, 0.059f, 0.0f + i * 0.016f), new Vector3(0.024f, 0.012f, 0.006f), 0.0015f, Shade.Dark, Shade.Black);
            // emitter
            k.Tube(new Vector3(0, 0.032f, 0.16f), 0.016f, 0.009f, 0.014f, 16, Shade.Dark, Shade.Black);
            g.Lathe(new Vector2(0, 0.032f), new[] { P(0.009f, 0.16f), P(0.009f, 0.164f) }, 12, Shade.White, Shade.White, Shade.White);
            // power cell under the frame, guard, grip
            k.ChamferBox(new Vector3(0, -0.002f, 0.085f), new Vector3(0.026f, 0.014f, 0.06f), 0.004f, Shade.Dark, Shade.Black);
            g.Box(new Vector3(0.0135f, -0.002f, 0.085f), new Vector3(0.001f, 0.004f, 0.04f), Shade.White);
            TriggerGroup(k, 0.03f, 0.004f);
            Grip(k, -0.009f, 0.004f, Shade.HatchDark, 18f, 0.094f, 0.03f);
            k.ChamferBox(new Vector3(0, 0.062f, -0.035f), new Vector3(0.018f, 0.012f, 0.02f), 0.002f, Shade.Black);
            return Finish("LaserPistolModel", k, g, new Color(0.3f, 0.9f, 1f), new Vector3(0, 0.032f, 0.168f), new Vector3(0, 0.07f, -0.035f), new Vector3(0.012f, -0.04f, -0.045f), new Vector3(-0.02f, -0.05f, -0.02f));
        }

        public static Model Gauss()
        {
            var k = new MeshKit();
            var g = new MeshKit();
            // frame with top strap
            Side(k, new[] { P(-0.06f, 0.002f), P(0.06f, 0.002f), P(0.07f, 0.012f), P(0.07f, 0.06f), P(-0.045f, 0.064f), P(-0.06f, 0.05f) }, 0.032f, Shade.HatchDark, Shade.Dark, 0.004f);
            // fluted drum
            k.Lathe(new Vector2(0, 0.032f), new[] { P(0.022f, -0.022f), P(0.026f, -0.016f), P(0.026f, 0.022f), P(0.022f, 0.028f) }, 12, Shade.HatchMid, Shade.Grey, Shade.Grey);
            for (int i = 0; i < 6; i++)
                With(k, new Vector3(0, 0.032f, 0.003f), new Vector3(0, 0, i * 60f + 30f), () => k.Box(new Vector3(0, 0.0258f, 0f), new Vector3(0.006f, 0.002f, 0.036f), Shade.Black));
            // long magnetic barrel: rails above and below, coil rings
            Barrel(k, 0.04f, 0.06f, 0.27f, 0.009f, Shade.Dark, 0.005f);
            // rails welded along the barrel (they overlap it: no daylight between rail and barrel)
            k.ChamferBox(new Vector3(0, 0.053f, 0.165f), new Vector3(0.014f, 0.012f, 0.21f), 0.002f, Shade.Light, Shade.Grey);
            k.ChamferBox(new Vector3(0, 0.027f, 0.155f), new Vector3(0.014f, 0.012f, 0.19f), 0.002f, Shade.Light, Shade.Grey);
            for (int i = 0; i < 5; i++)
            {
                k.Tube(new Vector3(0, 0.04f, 0.09f + i * 0.04f), 0.019f, 0.009f, 0.012f, 14, Shade.Yellow, Shade.Black);
                g.Lathe(new Vector2(0, 0.04f), new[] { P(0.0195f, 0.0855f + i * 0.04f), P(0.0195f, 0.0875f + i * 0.04f) }, 14, Shade.White);
            }
            k.ChamferBox(new Vector3(0, 0.04f, 0.272f), new Vector3(0.03f, 0.04f, 0.012f), 0.004f, Shade.Dark, Shade.Black);
            // hammer, sights
            Side(k, new[] { P(-0.066f, 0.05f), P(-0.056f, 0.05f), P(-0.054f, 0.066f), P(-0.07f, 0.07f) }, 0.01f, Shade.Black, Shade.Black, 0.001f);
            IronSights(k, 0.059f, 0.265f, -0.04f, Shade.Orange);
            TriggerGroup(k, 0.012f, 0.003f, 0.05f);
            Grip(k, -0.02f, 0.003f, Shade.Orange, 22f, 0.095f, 0.032f, Shade.Dark);
            Screws(k, 0.016f, P(0.05f, 0.012f), P(-0.045f, 0.015f));
            return Finish("GaussModel", k, g, new Color(1f, 0.75f, 0.3f), new Vector3(0, 0.04f, 0.28f), new Vector3(0, 0.068f, -0.04f), new Vector3(0.012f, -0.04f, -0.065f), new Vector3(-0.02f, -0.05f, -0.04f));
        }

        public static Model Smg()
        {
            var k = new MeshKit();
            // compact angular receiver
            Side(k, new[] { P(-0.07f, -0.012f), P(0.12f, -0.012f), P(0.15f, 0.01f), P(0.15f, 0.046f), P(0.13f, 0.06f), P(-0.06f, 0.06f), P(-0.07f, 0.05f) }, 0.042f, Shade.HatchDark, Shade.Dark, 0.005f);
            Plates(k, new[] { P(-0.04f, 0.026f), P(0.11f, 0.026f), P(0.125f, 0.04f), P(-0.04f, 0.04f) }, 0.021f, Shade.Green, 0.0015f);
            Plates(k, RectC(0.0f, 0.03f, 0.05f, 0.04f, 0.002f), 0.0225f, Shade.Black, 0.001f, false);
            k.Lathe(new Vector2(-0.024f, 0.045f), new[] { P(0.004f, -0.05f), P(0.004f, -0.03f), P(0.006f, -0.028f), P(0.006f, -0.02f) }, 8, Shade.Black, Shade.Black, Shade.Black); // charging handle
            // shrouded barrel with vents
            k.Tube(new Vector3(0, 0.026f, 0.19f), 0.017f, 0.006f, 0.08f, 14, Shade.Dark, Shade.Black);
            for (int i = 0; i < 4; i++) k.Box(new Vector3(0, 0.026f, 0.165f + i * 0.016f), new Vector3(0.036f, 0.006f, 0.007f), Shade.Black);
            Rail(k, 0.06f, -0.055f, 0.12f, 0.02f);
            IronSights(k, 0.069f, 0.11f, -0.045f, Shade.Green);
            // straight magazine in front of the trigger
            BoxMag(k, 0.085f, -0.012f, 0.14f, 0.028f, 0.026f, Shade.Dark, -3f, Shade.Green);
            TriggerGroup(k, 0.02f, -0.012f, 0.05f);
            Grip(k, -0.015f, -0.012f, Shade.HatchDark, 14f, 0.09f, 0.03f);
            // folding wire stock
            foreach (float x in new[] { -0.016f, 0.016f })
                With(k, new Vector3(x, 0.035f, -0.07f), new Vector3(180f, 0, 0), () => k.Lathe(Vector2.zero, new[] { P(0.0035f, 0f), P(0.0035f, 0.17f) }, 8, Shade.Black, Shade.Black, Shade.Black));
            k.ChamferBox(new Vector3(0, 0.005f, -0.243f), new Vector3(0.04f, 0.075f, 0.01f), 0.004f, Shade.Black, Shade.Black);
            Screws(k, 0.021f, P(-0.05f, 0.0f), P(0.1f, 0.0f));
            return Finish("SmgModel", k, null, Color.white, new Vector3(0, 0.026f, 0.232f), new Vector3(0, 0.078f, -0.045f), new Vector3(0.012f, -0.04f, -0.065f), new Vector3(-0.025f, -0.06f, 0.07f));
        }

        // ------------------------------------------------------------------ rifles

        public static Model PlasmaShotgun()
        {
            var k = new MeshKit();
            var g = new MeshKit();
            // fat receiver
            Side(k, new[] { P(-0.11f, -0.016f), P(0.2f, -0.016f), P(0.22f, 0.0f), P(0.22f, 0.05f), P(0.2f, 0.066f), P(-0.09f, 0.066f), P(-0.11f, 0.05f) }, 0.062f, Shade.HatchLight, Shade.Light, 0.008f);
            Plates(k, RectC(-0.06f, 0.0f, 0.18f, 0.016f, 0.004f), 0.031f, Shade.Orange, 0.0015f);
            Plates(k, RectC(0.0f, 0.03f, 0.1f, 0.05f, 0.004f), 0.031f, Shade.Dark, 0.0015f);
            // triple-bore muzzle block
            k.ChamferBox(new Vector3(0, 0.027f, 0.265f), new Vector3(0.078f, 0.064f, 0.09f), 0.012f, Shade.HatchDark, Shade.Dark);
            foreach (Vector2 b in new[] { P(-0.019f, 0.016f), P(0.019f, 0.016f), P(0.0f, 0.045f) })
            {
                k.Tube(new Vector3(b.x, b.y, 0.312f), 0.013f, 0.009f, 0.006f, 12, Shade.Black, Shade.Black);
                g.Lathe(new Vector2(b.x, b.y), new[] { P(0.009f, 0.305f), P(0.009f, 0.306f) }, 10, Shade.White, Shade.White, Shade.White);
            }
            for (int i = 0; i < 3; i++) k.Box(new Vector3(0, 0.027f, 0.24f + i * 0.018f), new Vector3(0.08f, 0.012f, 0.007f), Shade.Black);
            // pump with grooves
            k.ChamferBox(new Vector3(0, -0.032f, 0.16f), new Vector3(0.054f, 0.034f, 0.12f), 0.01f, Shade.Dark, Shade.Black);
            for (int i = 0; i < 5; i++) k.Box(new Vector3(0, -0.032f, 0.115f + i * 0.022f), new Vector3(0.056f, 0.036f, 0.006f), Shade.Black);
            // glowing plasma canister in a cage on top
            k.ChamferBox(new Vector3(0, 0.072f, 0.0f), new Vector3(0.036f, 0.012f, 0.17f), 0.003f, Shade.Dark, Shade.Black);
            g.Lathe(new Vector2(0, 0.092f), new[] { P(0.0f, -0.07f), P(0.016f, -0.064f), P(0.016f, 0.064f), P(0.0f, 0.07f) }, 12, Shade.White);
            foreach (float z in new[] { -0.075f, -0.025f, 0.025f, 0.075f })
                k.Tube(new Vector3(0, 0.092f, z), 0.02f, 0.0165f, 0.008f, 12, Shade.Dark, Shade.Black);
            // holographic sight on a solid mount bridging the cage, high enough to see over the canister
            k.ChamferBox(new Vector3(0, 0.116f, 0.02f), new Vector3(0.032f, 0.018f, 0.062f), 0.002f, Shade.Dark, Shade.Black);
            ReflexSight(k, 0.02f, 0.124f, g);
            TriggerGroup(k, -0.03f, -0.016f, 0.06f);
            Grip(k, -0.065f, -0.016f, Shade.HatchDark, 16f, 0.092f, 0.032f);
            SkeletonStock(k, -0.11f, 0.056f, 0.2f, 0.044f, Shade.HatchLight);
            return Finish("PlasmaShotgunModel", k, g, new Color(1f, 0.55f, 0.15f), new Vector3(0, 0.027f, 0.32f), new Vector3(0, 0.124f + ReflexDot, 0.026f), new Vector3(0.012f, -0.05f, -0.085f), new Vector3(-0.03f, -0.04f, 0.16f));
        }

        public static Model Railgun()
        {
            var k = new MeshKit();
            var g = new MeshKit();
            // receiver with capacitor bulge
            Side(k, new[] { P(-0.14f, -0.02f), P(0.16f, -0.02f), P(0.18f, 0.0f), P(0.18f, 0.06f), P(-0.12f, 0.06f), P(-0.14f, 0.045f) }, 0.064f, Shade.HatchMid, Shade.Grey, 0.007f);
            k.ChamferBox(new Vector3(0, -0.05f, 0.07f), new Vector3(0.052f, 0.05f, 0.16f), 0.01f, Shade.Yellow, Shade.Dark);
            for (int i = 0; i < 4; i++) k.Box(new Vector3(0, -0.05f, 0.015f + i * 0.036f), new Vector3(0.054f, 0.04f, 0.006f), Shade.Black);
            // twin rails held by clamps
            k.ChamferBox(new Vector3(0, 0.054f, 0.44f), new Vector3(0.046f, 0.012f, 0.6f), 0.003f, Shade.Light, Shade.HatchLight);
            k.ChamferBox(new Vector3(0, 0.006f, 0.44f), new Vector3(0.046f, 0.012f, 0.6f), 0.003f, Shade.Light, Shade.HatchLight);
            k.ChamferBox(new Vector3(0, 0.03f, 0.44f), new Vector3(0.012f, 0.012f, 0.6f), 0.002f, Shade.Black, Shade.Black);
            for (int i = 0; i < 5; i++)
            {
                float z = 0.2f + i * 0.12f;
                foreach (float x in new[] { -0.028f, 0.028f })
                    k.ChamferBox(new Vector3(x, 0.03f, z), new Vector3(0.008f, 0.07f, 0.032f), 0.002f, Shade.Dark, Shade.Black);
                g.Box(new Vector3(0, 0.03f, z), new Vector3(0.026f, 0.024f, 0.012f), Shade.White);
            }
            Rail(k, 0.06f, -0.12f, 0.16f);
            Scope(k, 0.105f, -0.13f, 0.12f, 0.016f, Shade.Black, Shade.Blue);
            ScopeRings(k, 0.066f, 0.105f, 0.016f, -0.04f, 0.07f);
            TriggerGroup(k, -0.05f, -0.02f, 0.06f);
            Grip(k, -0.085f, -0.02f, Shade.HatchDark, 16f, 0.092f, 0.032f);
            SkeletonStock(k, -0.14f, 0.05f, 0.18f, 0.044f, Shade.HatchMid);
            Screws(k, 0.032f, P(-0.11f, 0.0f), P(0.15f, 0.0f), P(0.15f, 0.045f));
            return Finish("RailgunModel", k, g, new Color(0.4f, 0.85f, 1f), new Vector3(0, 0.03f, 0.75f), new Vector3(0, 0.105f, -0.13f), new Vector3(0.012f, -0.05f, -0.1f), new Vector3(-0.03f, -0.05f, 0.08f));
        }

        // ------------------------------------------------------------------ heavy

        public static Model Minigun()
        {
            var k = new MeshKit();
            // motor housing with ribs, battery pack and carry handle
            k.ChamferBox(new Vector3(0, 0.02f, -0.03f), new Vector3(0.11f, 0.12f, 0.24f), 0.025f, Shade.HatchDark, Shade.Dark);
            for (int i = 0; i < 5; i++) k.Box(new Vector3(0, 0.02f, -0.11f + i * 0.03f), new Vector3(0.112f, 0.09f, 0.008f), Shade.Black);
            k.ChamferBox(new Vector3(0, -0.07f, 0.0f), new Vector3(0.084f, 0.06f, 0.14f), 0.012f, Shade.Yellow, Shade.Dark);
            k.Box(new Vector3(0.043f, -0.07f, 0.0f), new Vector3(0.002f, 0.03f, 0.08f), Shade.Black);
            Side(k, new[] { P(-0.06f, 0.08f), P(-0.04f, 0.08f), P(-0.035f, 0.125f), P(0.08f, 0.125f), P(0.085f, 0.08f), P(0.105f, 0.08f), P(0.1f, 0.14f), P(-0.055f, 0.14f) }, 0.022f, Shade.Black, Shade.Black, 0.004f);
            // barrel bearing and front ring
            k.Lathe(new Vector2(0, 0.02f), new[] { P(0.04f, 0.09f), P(0.052f, 0.1f), P(0.052f, 0.15f), P(0.045f, 0.16f) }, 16, Shade.Grey, Shade.Grey, Shade.Grey);
            Grip(k, -0.11f, -0.04f, Shade.HatchDark, 14f, 0.09f, 0.032f);
            k.ChamferBox(new Vector3(-0.075f, 0.0f, 0.05f), new Vector3(0.024f, 0.034f, 0.08f), 0.006f, Shade.Black, Shade.Black); // side handle
            var m = Finish("MinigunModel", k, null, Color.white, new Vector3(0, 0.02f, 0.53f), new Vector3(0, 0.15f, 0f), new Vector3(0.012f, -0.04f, -0.15f), new Vector3(-0.08f, -0.02f, 0.05f));
            // spinning barrel cluster
            var b = new MeshKit();
            for (int i = 0; i < 6; i++)
            {
                float a = i * Mathf.PI / 3f;
                Vector2 c = new Vector2(Mathf.Cos(a) * 0.028f, Mathf.Sin(a) * 0.028f);
                b.Lathe(c, new[] { P(0.009f, 0.0f), P(0.009f, 0.36f) }, 10, Shade.Black, Shade.Dark, Shade.Black);
                b.Tube(new Vector3(c.x, c.y, 0.362f), 0.0095f, 0.005f, 0.004f, 10, Shade.Dark, Shade.Black);
            }
            foreach (float z in new[] { 0.06f, 0.2f, 0.33f })
                b.Lathe(Vector2.zero, new[] { P(0.042f, z - 0.008f), P(0.042f, z + 0.008f) }, 16, Shade.Grey, Shade.Grey, Shade.Grey);
            b.Lathe(Vector2.zero, new[] { P(0.012f, 0f), P(0.012f, 0.33f) }, 10, Shade.Dark, Shade.Dark, Shade.Dark);
            GameObject barrels = WeaponModels.MeshChild(m.Root.transform, "Barrels", b.Build("Arsenal.MinigunBarrels"), Assets.ToolAtlas);
            barrels.transform.localPosition = new Vector3(0, 0.02f, 0.16f);
            return m;
        }

        public static Model Flamethrower()
        {
            var k = new MeshKit();
            var g = new MeshKit();
            // body and long nozzle with a perforated heat shield
            Side(k, new[] { P(-0.12f, 0.0f), P(0.2f, 0.0f), P(0.22f, 0.02f), P(0.22f, 0.05f), P(-0.1f, 0.06f), P(-0.12f, 0.045f) }, 0.05f, Shade.HatchDark, Shade.Dark, 0.006f);
            Barrel(k, 0.035f, 0.22f, 0.43f, 0.012f, Shade.Dark);
            k.Tube(new Vector3(0, 0.035f, 0.33f), 0.024f, 0.021f, 0.18f, 16, Shade.HatchMid, Shade.Black);
            for (int i = 0; i < 6; i++) k.Box(new Vector3(0, 0.035f, 0.26f + i * 0.026f), new Vector3(0.05f, 0.01f, 0.008f), Shade.Black);
            k.Tube(new Vector3(0, 0.035f, 0.445f), 0.02f, 0.012f, 0.03f, 14, Shade.Black, Shade.Black);
            // pilot light under the nozzle
            Barrel(k, 0.006f, 0.38f, 0.45f, 0.004f, Shade.Black);
            g.Lathe(new Vector2(0, 0.006f), new[] { P(0.0f, 0.45f), P(0.006f, 0.452f), P(0.0f, 0.462f) }, 8, Shade.White);
            // twin orange fuel tanks with caps and straps
            foreach (float x in new[] { -0.032f, 0.032f })
            {
                k.Lathe(new Vector2(x, -0.045f), new[] { P(0.0f, -0.08f), P(0.022f, -0.076f), P(0.03f, -0.06f), P(0.03f, 0.13f), P(0.022f, 0.146f), P(0.0f, 0.15f) }, 16, Shade.Orange);
                k.Lathe(new Vector2(x, -0.045f), new[] { P(0.008f, 0.15f), P(0.008f, 0.162f) }, 10, Shade.Black, Shade.Black, Shade.Black);
            }
            foreach (float z in new[] { -0.02f, 0.09f })
                k.ChamferBox(new Vector3(0, -0.045f, z), new Vector3(0.13f, 0.064f, 0.012f), 0.008f, Shade.Black, Shade.Black);
            // hose to the body
            With(k, new Vector3(0.05f, -0.02f, 0.16f), new Vector3(-60f, 0, 0), () => k.Lathe(Vector2.zero, new[] { P(0.005f, 0f), P(0.005f, 0.06f) }, 8, Shade.Black, Shade.Black, Shade.Black));
            k.ChamferBox(new Vector3(0, -0.02f, 0.2f), new Vector3(0.03f, 0.05f, 0.032f), 0.006f, Shade.Dark, Shade.Black); // front grip
            TriggerGroup(k, -0.05f, 0.0f, 0.055f);
            Grip(k, -0.08f, 0.0f, Shade.HatchDark, 16f, 0.092f, 0.032f);
            return Finish("FlamethrowerModel", k, g, new Color(1f, 0.6f, 0.2f), new Vector3(0, 0.035f, 0.47f), new Vector3(0, 0.07f, 0.0f), new Vector3(0.012f, -0.05f, -0.1f), new Vector3(-0.025f, -0.04f, 0.16f));
        }

        // ------------------------------------------------------------------ explosives

        public static Model GrenadeLauncher()
        {
            var k = new MeshKit();
            Side(k, new[] { P(-0.1f, -0.016f), P(0.05f, -0.016f), P(0.05f, 0.08f), P(-0.08f, 0.07f), P(-0.1f, 0.05f) }, 0.05f, Shade.HatchDark, Shade.Dark, 0.006f);
            // big revolving drum: six chambers with visible shells
            k.Lathe(new Vector2(0, 0.035f), new[] { P(0.06f, 0.05f), P(0.07f, 0.06f), P(0.07f, 0.16f), P(0.06f, 0.17f) }, 6, Shade.Dark, Shade.Dark, Shade.HatchMid);
            for (int i = 0; i < 6; i++)
            {
                float a = i * Mathf.PI / 3f + Mathf.PI / 6f;
                Vector2 c = new Vector2(Mathf.Cos(a) * 0.04f, 0.035f + Mathf.Sin(a) * 0.04f);
                k.Tube(new Vector3(c.x, c.y, 0.171f), 0.017f, 0.0135f, 0.004f, 12, Shade.Dark, Shade.Black);
                k.Lathe(c, new[] { P(0.013f, 0.165f), P(0.013f, 0.168f) }, 10, Shade.Yellow, Shade.Yellow, Shade.Yellow);
            }
            k.Lathe(new Vector2(0, 0.035f), new[] { P(0.012f, 0.17f), P(0.012f, 0.18f) }, 10, Shade.Grey, Shade.Grey, Shade.Grey);
            // short wide barrel and its top strap
            k.Tube(new Vector3(0, 0.035f, 0.27f), 0.03f, 0.022f, 0.18f, 16, Shade.HatchDark, Shade.Black);
            k.Tube(new Vector3(0, 0.035f, 0.355f), 0.034f, 0.022f, 0.012f, 16, Shade.Dark, Shade.Black);
            // solid top rib over the frame and the drum, and a front sight post standing on the barrel
            k.ChamferBox(new Vector3(0, 0.092f, 0.08f), new Vector3(0.03f, 0.04f, 0.2f), 0.003f, Shade.Yellow, Shade.Dark);
            Side(k, new[] { P(-0.012f, 0.072f), P(0.008f, 0.075f), P(0.008f, 0.1f), P(-0.012f, 0.1f) }, 0.024f, Shade.Dark, Shade.Black, 0.002f);
            Side(k, new[] { P(0.256f, 0.06f), P(0.276f, 0.06f), P(0.274f, 0.112f), P(0.262f, 0.112f) }, 0.014f, Shade.Dark, Shade.Black, 0.002f);
            IronSights(k, 0.112f, 0.27f, 0.0f);
            k.ChamferBox(new Vector3(0, -0.035f, 0.25f), new Vector3(0.032f, 0.05f, 0.05f), 0.007f, Shade.Dark, Shade.Black);
            TriggerGroup(k, 0.01f, -0.016f, 0.055f);
            Grip(k, -0.025f, -0.016f, Shade.HatchDark, 16f, 0.092f, 0.032f);
            SkeletonStock(k, -0.1f, 0.05f, 0.17f, 0.044f, Shade.HatchMid);
            return Finish("GrenadeLauncherModel", k, null, Color.white, new Vector3(0, 0.035f, 0.365f), new Vector3(0, 0.121f, 0.0f), new Vector3(0.012f, -0.05f, -0.085f), new Vector3(-0.03f, -0.05f, 0.17f));
        }

        /// <summary>Shoulder tube shared by the rocket launchers: flared bells at both ends, bands and rings.</summary>
        private static void LauncherTube(MeshKit k, float y, float r, float z0, float z1, Shade body, Shade band)
        {
            k.Lathe(new Vector2(0, y), new[]
            {
                P(r * 1.3f, z0), P(r * 1.15f, z0 + 0.05f), P(r, z0 + 0.09f), P(r, z1 - 0.07f), P(r * 1.12f, z1 - 0.04f), P(r * 1.12f, z1),
            }, 18, body);
            k.Tube(new Vector3(0, y, z1 - 0.002f), r * 1.12f, r * 0.9f, 0.004f, 18, Shade.Dark, Shade.Black);
            k.Tube(new Vector3(0, y, z0 + 0.002f), r * 1.3f, r * 0.9f, 0.004f, 18, Shade.Dark, Shade.Black);
            // inner liner seen from both ends
            k.Tube(new Vector3(0, y, (z0 + z1) * 0.5f), r * 0.9f, r * 0.88f, z1 - z0 - 0.01f, 18, Shade.Black, Shade.Black);
            k.Lathe(new Vector2(0, y), new[] { P(r * 1.04f, z1 - 0.2f), P(r * 1.04f, z1 - 0.16f) }, 18, band, band, band);
            foreach (float z in new[] { z0 + 0.25f, z1 - 0.3f })
                k.Lathe(new Vector2(0, y), new[] { P(r * 1.06f, z - 0.01f), P(r * 1.06f, z + 0.01f) }, 18, Shade.Black, Shade.Black, Shade.Black);
        }

        public static Model RocketLauncher()
        {
            var k = new MeshKit();
            LauncherTube(k, 0.08f, 0.062f, -0.32f, 0.64f, Shade.HatchLight, Shade.Orange);
            // optical sight box on the left, shoulder rest, grips
            k.ChamferBox(new Vector3(-0.085f, 0.13f, 0.12f), new Vector3(0.034f, 0.046f, 0.1f), 0.006f, Shade.Dark, Shade.Black);
            k.ChamferBox(new Vector3(-0.07f, 0.112f, 0.12f), new Vector3(0.03f, 0.02f, 0.07f), 0.003f, Shade.Black, Shade.Black); // bracket onto the tube
            k.Tube(new Vector3(-0.085f, 0.135f, 0.172f), 0.012f, 0.008f, 0.006f, 12, Shade.Black, Shade.Blue);
            k.Tube(new Vector3(-0.085f, 0.135f, 0.068f), 0.01f, 0.006f, 0.006f, 12, Shade.Black, Shade.Blue);
            Side(k, new[] { P(0.08f, 0.09f), P(0.16f, 0.09f), P(0.16f, 0.115f), P(0.08f, 0.115f) }, 0.016f, Shade.Dark, Shade.Black, 0.002f, -0.065f);
            Side(k, new[] { P(-0.22f, -0.01f), P(-0.08f, -0.01f), P(-0.08f, 0.02f), P(-0.22f, 0.02f) }, 0.05f, Shade.Dark, Shade.Black, 0.006f);
            k.ChamferBox(new Vector3(0, 0.0f, 0.2f), new Vector3(0.03f, 0.06f, 0.04f), 0.006f, Shade.Dark, Shade.Black);
            Side(k, new[] { P(-0.03f, 0.02f), P(0.04f, 0.02f), P(0.04f, 0.0f), P(-0.03f, 0.0f) }, 0.036f, Shade.Dark, Shade.Black, 0.004f);
            TriggerGroup(k, 0.01f, 0.0f, 0.05f);
            Grip(k, -0.004f, 0.0f, Shade.HatchDark, 12f, 0.088f, 0.03f);
            var m = Finish("RocketLauncherModel", k, null, Color.white, new Vector3(0, 0.08f, 0.66f), new Vector3(-0.085f, 0.135f, 0.06f), new Vector3(0.012f, -0.04f, -0.045f), new Vector3(-0.025f, -0.05f, 0.2f));
            // rocket nose showing in the tube (hidden while reloading)
            var r = new MeshKit();
            r.Lathe(Vector2.zero, new[] { P(0.05f, -0.04f), P(0.05f, 0.0f), P(0.04f, 0.05f), P(0.018f, 0.09f), P(0.0f, 0.1f) }, 14, Shade.Red, Shade.Red);
            r.Lathe(Vector2.zero, new[] { P(0.051f, -0.03f), P(0.051f, -0.02f) }, 14, Shade.Yellow, Shade.Yellow, Shade.Yellow);
            GameObject nose = WeaponModels.MeshChild(m.Root.transform, "Ammo", r.Build("Arsenal.RocketNose"), Assets.ToolAtlas);
            nose.transform.localPosition = new Vector3(0, 0.08f, 0.55f);
            return m;
        }

        /// <summary>"Eagle" SAM launcher: long tube, seeker unit with a glowing lens, battery/cooling unit, grips.</summary>
        public static Model SamLauncher()
        {
            var k = new MeshKit();
            var g = new MeshKit();
            LauncherTube(k, 0.085f, 0.058f, -0.4f, 0.72f, Shade.HatchMid, Shade.Yellow);
            // seeker / IFF unit on the left: housing, antenna grid, lens
            k.ChamferBox(new Vector3(-0.088f, 0.14f, 0.2f), new Vector3(0.05f, 0.07f, 0.16f), 0.008f, Shade.Dark, Shade.Black);
            k.ChamferBox(new Vector3(-0.07f, 0.11f, 0.2f), new Vector3(0.04f, 0.03f, 0.12f), 0.004f, Shade.Black, Shade.Black); // bracket onto the tube
            for (int i = 0; i < 4; i++) k.Box(new Vector3(-0.114f, 0.14f, 0.14f + i * 0.04f), new Vector3(0.003f, 0.05f, 0.006f), Shade.Black);
            k.Tube(new Vector3(-0.088f, 0.145f, 0.283f), 0.02f, 0.014f, 0.008f, 14, Shade.Black, Shade.Black);
            g.Lathe(new Vector2(-0.088f, 0.145f), new[] { P(0.014f, 0.28f), P(0.014f, 0.282f) }, 14, Shade.White, Shade.White, Shade.White);
            k.Tube(new Vector3(-0.088f, 0.15f, 0.114f), 0.012f, 0.007f, 0.012f, 12, Shade.Black, Shade.Black); // eyepiece
            Side(k, new[] { P(0.12f, 0.1f), P(0.28f, 0.1f), P(0.28f, 0.112f), P(0.12f, 0.112f) }, 0.03f, Shade.Dark, Shade.Black, 0.002f, -0.065f);
            // antenna on top with a blinking tip
            With(k, new Vector3(0, 0.145f, 0.05f), new Vector3(-90f, 0, 0), () => k.Lathe(Vector2.zero, new[] { P(0.004f, 0f), P(0.004f, 0.05f), P(0.0f, 0.054f) }, 8, Shade.Black, Shade.Black));
            g.Box(new Vector3(0, 0.2f, 0.05f), new Vector3(0.006f, 0.006f, 0.006f), Shade.White);
            // battery / coolant unit under the front
            k.Lathe(new Vector2(0, 0.0f), new[] { P(0.0f, 0.2f), P(0.02f, 0.205f), P(0.024f, 0.22f), P(0.024f, 0.32f), P(0.02f, 0.335f), P(0.0f, 0.34f) }, 14, Shade.Yellow);
            k.ChamferBox(new Vector3(0, 0.02f, 0.27f), new Vector3(0.026f, 0.03f, 0.08f), 0.005f, Shade.Dark, Shade.Black);
            // shoulder rest, front grip, trigger grip
            Side(k, new[] { P(-0.3f, -0.012f), P(-0.12f, -0.012f), P(-0.12f, 0.03f), P(-0.3f, 0.03f) }, 0.055f, Shade.Dark, Shade.Black, 0.007f);
            k.ChamferBox(new Vector3(0, 0.0f, 0.12f), new Vector3(0.03f, 0.06f, 0.04f), 0.006f, Shade.Dark, Shade.Black);
            Side(k, new[] { P(-0.03f, 0.03f), P(0.04f, 0.03f), P(0.04f, 0.0f), P(-0.03f, 0.0f) }, 0.036f, Shade.Dark, Shade.Black, 0.004f);
            TriggerGroup(k, 0.01f, 0.0f, 0.05f);
            Grip(k, -0.004f, 0.0f, Shade.HatchDark, 12f, 0.088f, 0.03f);
            Screws(k, 0.0255f, P(0.25f, 0.025f), P(-0.25f, 0.01f));
            var m = Finish("SamLauncherModel", k, g, new Color(1f, 0.3f, 0.25f), new Vector3(0, 0.085f, 0.74f), new Vector3(-0.088f, 0.15f, 0.105f), new Vector3(0.012f, -0.04f, -0.045f), new Vector3(-0.025f, -0.05f, 0.12f));
            // missile seeker dome showing in the tube
            var r = new MeshKit();
            r.Lathe(Vector2.zero, new[] { P(0.048f, -0.05f), P(0.048f, -0.01f), P(0.044f, 0.02f), P(0.032f, 0.045f), P(0.016f, 0.058f), P(0.0f, 0.062f) }, 16, Shade.White, Shade.White);
            r.Lathe(Vector2.zero, new[] { P(0.049f, -0.04f), P(0.049f, -0.03f) }, 16, Shade.Yellow, Shade.Yellow, Shade.Yellow);
            GameObject dome = WeaponModels.MeshChild(m.Root.transform, "Ammo", r.Build("Arsenal.SamNose"), Assets.ToolAtlas);
            dome.transform.localPosition = new Vector3(0, 0.085f, 0.64f);
            return m;
        }

        /// <summary>
        /// "Lightning" hypersonic launcher (legendary): dark slim tube wrapped in glowing blue capacitor rings,
        /// long-range seeker with a blue lens, finned heat sink, the slim hypersonic missile's nose showing.
        /// </summary>
        public static Model HyperLauncher()
        {
            var k = new MeshKit();
            var g = new MeshKit();
            LauncherTube(k, 0.085f, 0.05f, -0.42f, 0.8f, Shade.HatchDark, Shade.Blue);
            // capacitor rings: dark collars with a glowing core band
            for (int i = 0; i < 4; i++)
            {
                float z = -0.08f + i * 0.09f;
                k.Lathe(new Vector2(0, 0.085f), new[] { P(0.062f, z - 0.016f), P(0.064f, z - 0.01f), P(0.064f, z + 0.01f), P(0.062f, z + 0.016f) }, 18, Shade.Dark, Shade.Black, Shade.Black);
                g.Lathe(new Vector2(0, 0.085f), new[] { P(0.0645f, z - 0.004f), P(0.0645f, z + 0.004f) }, 18, Shade.White, Shade.White, Shade.White);
            }
            // glowing rails along both sides of the tube
            foreach (float x in new[] { -0.051f, 0.051f })
                g.Box(new Vector3(x, 0.085f, 0.45f), new Vector3(0.003f, 0.006f, 0.22f), Shade.White);
            // heat sink fins under the front
            for (int i = 0; i < 6; i++)
                k.ChamferBox(new Vector3(0, 0.03f, 0.36f + i * 0.03f), new Vector3(0.05f, 0.02f, 0.008f), 0.002f, Shade.Grey, Shade.Dark);
            // long-range seeker on the left: angular housing, blue lens, antenna strip
            k.ChamferBox(new Vector3(-0.08f, 0.14f, 0.24f), new Vector3(0.046f, 0.06f, 0.2f), 0.008f, Shade.Black, Shade.HatchDark);
            k.ChamferBox(new Vector3(-0.065f, 0.105f, 0.24f), new Vector3(0.04f, 0.025f, 0.14f), 0.004f, Shade.Black, Shade.Black); // bracket onto the tube
            k.Tube(new Vector3(-0.08f, 0.145f, 0.343f), 0.019f, 0.013f, 0.008f, 14, Shade.Dark, Shade.Black);
            g.Lathe(new Vector2(-0.08f, 0.145f), new[] { P(0.013f, 0.34f), P(0.013f, 0.342f) }, 14, Shade.White, Shade.White, Shade.White);
            g.Box(new Vector3(-0.1035f, 0.14f, 0.24f), new Vector3(0.002f, 0.006f, 0.15f), Shade.White);
            k.Tube(new Vector3(-0.08f, 0.15f, 0.136f), 0.012f, 0.007f, 0.012f, 12, Shade.Black, Shade.Black); // eyepiece
            // holographic sight on top: base, two posts, ring with a glowing reticle
            k.ChamferBox(new Vector3(0, 0.155f, 0.03f), new Vector3(0.034f, 0.032f, 0.05f), 0.004f, Shade.Dark, Shade.Black);
            foreach (float x in new[] { -0.021f, 0.021f })
                k.Box(new Vector3(x, 0.182f, 0.04f), new Vector3(0.005f, 0.028f, 0.008f), Shade.Black);
            k.Tube(new Vector3(0, 0.205f, 0.04f), 0.024f, 0.02f, 0.006f, 18, Shade.Dark, Shade.Black);
            g.Box(new Vector3(0, 0.205f, 0.04f), new Vector3(0.003f, 0.003f, 0.001f), Shade.White);
            // power cell behind it, shoulder rest, grips
            k.ChamferBox(new Vector3(0, 0.155f, -0.12f), new Vector3(0.05f, 0.03f, 0.14f), 0.006f, Shade.Dark, Shade.Black);
            g.Box(new Vector3(0, 0.171f, -0.12f), new Vector3(0.03f, 0.002f, 0.1f), Shade.White);
            Side(k, new[] { P(-0.32f, -0.012f), P(-0.12f, -0.012f), P(-0.12f, 0.03f), P(-0.32f, 0.03f) }, 0.055f, Shade.Black, Shade.HatchDark, 0.007f);
            k.ChamferBox(new Vector3(0, 0.0f, 0.15f), new Vector3(0.03f, 0.06f, 0.04f), 0.006f, Shade.Dark, Shade.Black);
            Side(k, new[] { P(-0.03f, 0.03f), P(0.04f, 0.03f), P(0.04f, 0.0f), P(-0.03f, 0.0f) }, 0.036f, Shade.Dark, Shade.Black, 0.004f);
            TriggerGroup(k, 0.01f, 0.0f, 0.05f);
            Grip(k, -0.004f, 0.0f, Shade.Black, 12f, 0.088f, 0.03f, Shade.HatchDark);
            Screws(k, 0.0255f, P(0.25f, 0.025f), P(-0.25f, 0.01f));
            var m = Finish("HyperLauncherModel", k, g, new Color(0.4f, 0.75f, 1f), new Vector3(0, 0.085f, 0.82f), new Vector3(0, 0.205f, -0.02f), new Vector3(0.012f, -0.04f, -0.045f), new Vector3(-0.025f, -0.05f, 0.15f));
            // the missile's needle nose in the tube
            var r = new MeshKit();
            r.Lathe(Vector2.zero, new[] { P(0.04f, -0.05f), P(0.04f, 0.0f), P(0.028f, 0.05f), P(0.012f, 0.1f), P(0.0f, 0.12f) }, 14, Shade.Dark, Shade.Dark);
            r.Lathe(Vector2.zero, new[] { P(0.041f, -0.035f), P(0.041f, -0.02f) }, 14, Shade.Blue, Shade.Blue, Shade.Blue);
            GameObject nose = WeaponModels.MeshChild(m.Root.transform, "Ammo", r.Build("Arsenal.HyperNose"), Assets.ToolAtlas);
            nose.transform.localPosition = new Vector3(0, 0.085f, 0.7f);
            return m;
        }

        // ------------------------------------------------------------------ energy

        public static Model EmpCarbine()
        {
            var k = new MeshKit();
            var g = new MeshKit();
            Side(k, new[] { P(-0.11f, -0.014f), P(0.18f, -0.014f), P(0.2f, 0.005f), P(0.2f, 0.05f), P(0.17f, 0.062f), P(-0.09f, 0.062f), P(-0.11f, 0.048f) }, 0.056f, Shade.HatchLight, Shade.Light, 0.008f);
            Plates(k, RectC(-0.07f, 0.012f, 0.16f, 0.024f, 0.003f), 0.028f, Shade.Blue, 0.0015f);
            GlowFrame(k, g, new Vector3(0.029f, 0.04f, 0.04f), new Vector3(0.002f, 0.008f, 0.14f));
            GlowFrame(k, g, new Vector3(-0.029f, 0.04f, 0.04f), new Vector3(0.002f, 0.008f, 0.14f));
            // emitter: neck, finned coil and dish
            Barrel(k, 0.03f, 0.2f, 0.27f, 0.016f, Shade.Dark);
            for (int i = 0; i < 4; i++) k.Tube(new Vector3(0, 0.03f, 0.215f + i * 0.016f), 0.026f, 0.016f, 0.006f, 14, Shade.Blue, Shade.Black);
            k.Lathe(new Vector2(0, 0.03f), new[] { P(0.02f, 0.27f), P(0.05f, 0.29f), P(0.052f, 0.3f), P(0.03f, 0.302f) }, 18, Shade.Dark, null, Shade.Black);
            for (int i = 0; i < 4; i++)
                With(k, new Vector3(0, 0.03f, 0.27f), new Vector3(0, 0, i * 90f + 45f), () => k.ChamferBox(new Vector3(0, 0.045f, 0f), new Vector3(0.006f, 0.03f, 0.05f), 0.002f, Shade.Light, Shade.Grey));
            g.Lathe(new Vector2(0, 0.03f), new[] { P(0.03f, 0.298f), P(0.03f, 0.3f) }, 18, Shade.White, Shade.White, Shade.White);
            // cell pack on top, grip, stock
            k.ChamferBox(new Vector3(0, 0.077f, 0.0f), new Vector3(0.032f, 0.03f, 0.08f), 0.006f, Shade.Dark, Shade.Black);
            g.Box(new Vector3(0, 0.0925f, 0.0f), new Vector3(0.02f, 0.002f, 0.06f), Shade.White);
            // holographic sight on a solid mount over the cell pack, so the pack stays out of the view
            k.ChamferBox(new Vector3(0, 0.098f, 0.0f), new Vector3(0.03f, 0.022f, 0.05f), 0.002f, Shade.Dark, Shade.Black);
            ReflexSight(k, 0.0f, 0.108f, g);
            TriggerGroup(k, -0.03f, -0.014f, 0.055f);
            Grip(k, -0.065f, -0.014f, Shade.HatchDark, 16f, 0.092f, 0.032f);
            SkeletonStock(k, -0.11f, 0.05f, 0.17f, 0.042f, Shade.HatchLight);
            return Finish("EmpCarbineModel", k, g, new Color(0.35f, 0.6f, 1f), new Vector3(0, 0.03f, 0.31f), new Vector3(0, 0.108f + ReflexDot, 0.006f), new Vector3(0.012f, -0.05f, -0.085f), new Vector3(-0.03f, -0.04f, 0.16f));
        }

        public static Model Tesla()
        {
            var k = new MeshKit();
            var g = new MeshKit();
            Side(k, new[] { P(-0.1f, -0.014f), P(0.07f, -0.014f), P(0.09f, 0.005f), P(0.09f, 0.06f), P(-0.08f, 0.066f), P(-0.1f, 0.05f) }, 0.06f, Shade.HatchDark, Shade.Dark, 0.008f);
            // copper coil around a ceramic core
            k.Lathe(new Vector2(0, 0.03f), new[] { P(0.022f, 0.08f), P(0.022f, 0.22f) }, 14, Shade.White, Shade.Light, Shade.Light);
            for (int i = 0; i < 8; i++) k.Tube(new Vector3(0, 0.03f, 0.095f + i * 0.016f), 0.034f, 0.022f, 0.009f, 14, Shade.Orange, Shade.Orange);
            k.Tube(new Vector3(0, 0.03f, 0.085f), 0.04f, 0.02f, 0.01f, 16, Shade.Dark, Shade.Black);
            k.Tube(new Vector3(0, 0.03f, 0.225f), 0.04f, 0.02f, 0.01f, 16, Shade.Dark, Shade.Black);
            // two prongs with insulator rings and glowing tips
            foreach (float x in new[] { -0.026f, 0.026f })
            {
                k.ChamferBox(new Vector3(x, 0.03f, 0.27f), new Vector3(0.01f, 0.01f, 0.09f), 0.002f, Shade.Grey, Shade.Dark);
                for (int i = 0; i < 3; i++) k.Lathe(new Vector2(x, 0.03f), new[] { P(0.009f, 0.24f + i * 0.02f), P(0.009f, 0.248f + i * 0.02f) }, 10, Shade.White, Shade.White, Shade.White);
                g.Lathe(new Vector2(x, 0.03f), new[] { P(0.0f, 0.312f), P(0.008f, 0.316f), P(0.008f, 0.322f), P(0.0f, 0.328f) }, 10, Shade.White);
            }
            // capacitor bank on top
            k.ChamferBox(new Vector3(0, 0.074f, -0.02f), new Vector3(0.036f, 0.024f, 0.08f), 0.006f, Shade.Yellow, Shade.Dark);
            for (int i = 0; i < 3; i++) k.Lathe(new Vector2(0.0f, 0.087f), new[] { P(0.007f, -0.05f + i * 0.026f), P(0.007f, -0.036f + i * 0.026f) }, 8, Shade.Blue, Shade.Blue, Shade.Blue);
            TriggerGroup(k, -0.03f, -0.014f, 0.055f);
            Grip(k, -0.065f, -0.014f, Shade.HatchDark, 16f, 0.092f, 0.032f);
            SkeletonStock(k, -0.1f, 0.052f, 0.15f, 0.044f, Shade.HatchMid);
            return Finish("TeslaModel", k, g, new Color(0.55f, 0.8f, 1f), new Vector3(0, 0.03f, 0.32f), new Vector3(0, 0.1f, -0.02f), new Vector3(0.012f, -0.05f, -0.085f), new Vector3(-0.03f, -0.04f, 0.14f));
        }

        public static Model Disintegrator()
        {
            var k = new MeshKit();
            var g = new MeshKit();
            // swept alien shell, dark with violet seams
            Side(k, new[]
            {
                P(-0.12f, -0.01f), P(0.12f, -0.01f), P(0.26f, 0.012f), P(0.3f, 0.03f), P(0.26f, 0.05f), P(0.1f, 0.075f), P(-0.06f, 0.07f), P(-0.12f, 0.045f),
            }, 0.058f, Shade.Black, Shade.HatchDark, 0.01f);
            // exposed core behind slots
            g.Lathe(new Vector2(0, 0.03f), new[] { P(0.0f, -0.06f), P(0.016f, -0.05f), P(0.016f, 0.2f), P(0.0f, 0.21f) }, 10, Shade.White);
            for (int i = 0; i < 6; i++)
            {
                float z = -0.04f + i * 0.045f;
                g.Box(new Vector3(0.0295f, 0.032f, z), new Vector3(0.002f, 0.022f, 0.026f), Shade.White);
                g.Box(new Vector3(-0.0295f, 0.032f, z), new Vector3(0.002f, 0.022f, 0.026f), Shade.White);
                k.Box(new Vector3(0, 0.032f, z), new Vector3(0.0605f, 0.028f, 0.032f), Shade.Black);
            }
            // three blades around the emitter
            for (int i = 0; i < 3; i++)
            {
                Vector3 e = new Vector3(0, 0, i * 120f + 90f);
                With(k, new Vector3(0, 0.03f, 0.27f), e, () =>
                    Side(k, new[] { P(-0.06f, 0.02f), P(0.04f, 0.02f), P(0.08f, 0.035f), P(0.05f, 0.042f), P(-0.04f, 0.036f) }, 0.006f, Shade.Dark, Shade.Black, 0.0015f));
                With(g, new Vector3(0, 0.03f, 0.27f), e, () => g.Box(new Vector3(0, 0.043f, 0.0f), new Vector3(0.002f, 0.003f, 0.07f), Shade.White));
            }
            k.Tube(new Vector3(0, 0.03f, 0.3f), 0.014f, 0.008f, 0.01f, 12, Shade.Dark, Shade.Black);
            g.Lathe(new Vector2(0, 0.03f), new[] { P(0.0f, 0.303f), P(0.008f, 0.305f), P(0.0f, 0.315f) }, 10, Shade.White);
            g.Box(new Vector3(0.0295f, 0.058f, 0.06f), new Vector3(0.001f, 0.002f, 0.2f), Shade.White);
            g.Box(new Vector3(-0.0295f, 0.058f, 0.06f), new Vector3(0.001f, 0.002f, 0.2f), Shade.White);
            Grip(k, -0.08f, -0.01f, Shade.Black, 18f, 0.09f, 0.03f, Shade.HatchDark);
            TriggerGroup(k, -0.045f, -0.01f, 0.05f);
            return Finish("DisintegratorModel", k, g, new Color(0.8f, 0.35f, 1f), new Vector3(0, 0.03f, 0.32f), new Vector3(0, 0.09f, -0.04f), new Vector3(0.012f, -0.05f, -0.1f), new Vector3(-0.03f, -0.04f, 0.12f));
        }

        // ------------------------------------------------------------------ blasters

        /// <summary>
        /// Heavy blaster pistol, a smuggler's sidearm: a dark slab frame, a scope on top, a finned cooling sleeve
        /// round the barrel and a conical flash hider, a red power cell glowing under the frame.
        /// </summary>
        public static Model BlasterPistol()
        {
            var k = new MeshKit();
            var g = new MeshKit();
            Side(k, new[] { P(-0.06f, 0.006f), P(0.06f, 0.006f), P(0.07f, 0.018f), P(0.07f, 0.055f), P(-0.04f, 0.06f), P(-0.065f, 0.045f) }, 0.032f, Shade.HatchDark, Shade.Dark, 0.004f);
            Plates(k, RectC(-0.045f, 0.018f, 0.055f, 0.048f, 0.004f), 0.017f, Shade.Black, 0.0015f);
            // barrel with its cooling sleeve and flash hider
            Barrel(k, 0.036f, 0.07f, 0.2f, 0.008f, Shade.Dark);
            k.Tube(new Vector3(0, 0.036f, 0.11f), 0.015f, 0.008f, 0.07f, 14, Shade.Grey, Shade.Black);
            for (int i = 0; i < 6; i++) k.Tube(new Vector3(0, 0.036f, 0.08f + i * 0.012f), 0.018f, 0.015f, 0.004f, 14, Shade.Dark, Shade.Black);
            k.Lathe(new Vector2(0, 0.036f), new[] { P(0.008f, 0.19f), P(0.014f, 0.215f), P(0.012f, 0.22f) }, 14, Shade.Dark, Shade.Black, Shade.Black);
            // scope on a short mount
            Scope(k, 0.088f, -0.03f, 0.07f, 0.011f);
            ScopeRings(k, 0.06f, 0.088f, 0.011f, -0.005f, 0.045f);
            // power cell and its glow
            k.ChamferBox(new Vector3(0, 0.0f, 0.035f), new Vector3(0.024f, 0.016f, 0.05f), 0.004f, Shade.Dark, Shade.Black);
            g.Box(new Vector3(0.0125f, 0.0f, 0.035f), new Vector3(0.001f, 0.006f, 0.034f), Shade.White);
            g.Box(new Vector3(-0.0125f, 0.0f, 0.035f), new Vector3(0.001f, 0.006f, 0.034f), Shade.White);
            TriggerGroup(k, 0.0f, 0.006f);
            Grip(k, -0.04f, 0.006f, Shade.HatchDark, 14f, 0.095f, 0.03f);
            return Finish("BlasterPistolModel", k, g, new Color(1f, 0.15f, 0.08f), new Vector3(0, 0.036f, 0.225f), new Vector3(0, 0.1f, -0.03f), new Vector3(0.012f, -0.04f, -0.07f), new Vector3(-0.02f, -0.05f, -0.04f));
        }

        /// <summary>
        /// Blaster rifle, a trooper's carbine: a slim black barrel shroud full of cooling holes, a side power cell,
        /// a wire stock and a long scope. The carbine is shorter and lighter, with green accents.
        /// </summary>
        public static Model BlasterRifle() => Rifle(false);
        public static Model BlasterCarbine() => Rifle(true);

        private static Model Rifle(bool carbine)
        {
            var k = new MeshKit();
            var g = new MeshKit();
            float front = carbine ? 0.3f : 0.4f;
            // receiver
            Side(k, new[] { P(-0.05f, 0.006f), P(0.08f, 0.006f), P(0.09f, 0.02f), P(0.09f, 0.06f), P(-0.03f, 0.066f), P(-0.05f, 0.05f) }, 0.036f, Shade.HatchDark, Shade.Dark, 0.004f);
            // barrel shroud with rows of cooling holes, emitter at the end
            k.Lathe(new Vector2(0, 0.036f), new[] { P(0.019f, 0.09f), P(0.019f, front - 0.04f), P(0.015f, front - 0.03f) }, 16, Shade.Black, Shade.Dark, Shade.Black);
            for (int row = 0; row < 3; row++)
                GunParts.With(k, new Vector3(0, 0.036f, 0f), new Vector3(0, 0, row * 120f), () =>
                    Vents(k, 0.0192f, 0.11f, front - 0.06f, carbine ? 7 : 10, 0.006f, 0.002f, 0.007f));
            Barrel(k, 0.036f, front - 0.04f, front, 0.008f, Shade.Dark);
            FlashHider(k, 0.036f, front + 0.015f, 0.012f, 0.03f);
            // side power cell, sticking out to the left
            k.ChamferBox(new Vector3(-0.034f, 0.03f, 0.05f), new Vector3(0.03f, 0.022f, 0.05f), 0.004f, Shade.Dark, Shade.Black);
            g.Box(new Vector3(-0.05f, 0.03f, 0.05f), new Vector3(0.001f, 0.006f, 0.034f), Shade.White);
            // scope, folding wire stock, grip
            Scope(k, 0.09f, -0.04f, 0.09f, 0.012f);
            ScopeRings(k, 0.066f, 0.09f, 0.012f, -0.01f, 0.06f);
            if (!carbine) SkeletonStock(k, -0.05f, 0.06f, 0.17f, 0.01f, Shade.Dark);
            GlowFrame(k, g, new Vector3(0.019f, 0.046f, 0.03f), new Vector3(0.002f, 0.006f, 0.05f));
            TriggerGroup(k, 0.0f, 0.006f);
            Grip(k, -0.03f, 0.006f, Shade.HatchDark, 16f, 0.09f, 0.03f);
            return Finish(carbine ? "BlasterCarbineModel" : "BlasterRifleModel", k, g, carbine ? new Color(0.2f, 1f, 0.15f) : new Color(1f, 0.15f, 0.08f),
                new Vector3(0, 0.036f, front + 0.03f), new Vector3(0, 0.104f, -0.04f), new Vector3(0.012f, -0.04f, -0.06f), new Vector3(-0.03f, -0.03f, 0.18f));
        }

        /// <summary>Heavy repeating blaster: a fat blue-lit gas chamber over the barrel, a cooling drum, a carry handle.</summary>
        public static Model HeavyBlaster()
        {
            var k = new MeshKit();
            var g = new MeshKit();
            Side(k, new[] { P(-0.08f, 0.0f), P(0.12f, 0.0f), P(0.14f, 0.02f), P(0.14f, 0.075f), P(-0.06f, 0.08f), P(-0.08f, 0.06f) }, 0.05f, Shade.HatchMid, Shade.Grey, 0.005f);
            // twin cooling cylinders and the barrel
            k.Lathe(new Vector2(0, 0.045f), new[] { P(0.03f, 0.14f), P(0.03f, 0.36f), P(0.022f, 0.38f) }, 18, Shade.Dark, Shade.Black, Shade.Black);
            for (int i = 0; i < 8; i++) k.Tube(new Vector3(0, 0.045f, 0.16f + i * 0.026f), 0.034f, 0.03f, 0.006f, 18, Shade.Grey, Shade.Black);
            Barrel(k, 0.045f, 0.38f, 0.43f, 0.012f, Shade.Dark);
            FlashHider(k, 0.045f, 0.455f, 0.017f, 0.04f);
            // gas chamber lit blue along its sides
            k.ChamferBox(new Vector3(0, 0.095f, 0.06f), new Vector3(0.03f, 0.042f, 0.14f), 0.005f, Shade.Dark, Shade.Black); // sits down into the frame
            g.Box(new Vector3(0.0155f, 0.1f, 0.06f), new Vector3(0.001f, 0.012f, 0.11f), Shade.White);
            g.Box(new Vector3(-0.0155f, 0.1f, 0.06f), new Vector3(0.001f, 0.012f, 0.11f), Shade.White);
            // carry handle, cooling drum under the frame, grip
            k.ChamferBox(new Vector3(0, 0.13f, 0.02f), new Vector3(0.012f, 0.008f, 0.08f), 0.002f, Shade.Black);
            foreach (float z in new[] { -0.012f, 0.052f })
                k.ChamferBox(new Vector3(0, 0.12f, z), new Vector3(0.01f, 0.016f, 0.01f), 0.002f, Shade.Black);
            k.Lathe(new Vector2(0, -0.015f), new[] { P(0.035f, 0.02f), P(0.035f, 0.1f) }, 16, Shade.HatchDark, Shade.Dark, Shade.Dark);
            TriggerGroup(k, -0.03f, 0.0f);
            Grip(k, -0.06f, 0.0f, Shade.HatchDark, 14f, 0.1f, 0.034f);
            return Finish("HeavyBlasterModel", k, g, new Color(0.15f, 0.45f, 1f), new Vector3(0, 0.045f, 0.475f), new Vector3(0, 0.15f, -0.02f), new Vector3(0.014f, -0.05f, -0.09f), new Vector3(-0.035f, -0.05f, 0.16f));
        }
    }
}
