using UnityEngine;

namespace Arsenal.Visual
{
    /// <summary>
    /// The big rotating turrets, in the films' spirit and the game's chunky style. Each one has a fixed base,
    /// a "Head" that yaws around the mount's up axis and a "Pitch" cradle that tilts the guns. Every barrel sits
    /// in its own "Recoil" (it kicks back when it fires) holding its "Muzzle"; "Lens" is the glow that shows heat.
    /// Base on the local y = 0 plane, guns along +z.
    /// </summary>
    internal static class TurretModels
    {
        private static Transform Point(Transform parent, string name, Vector3 pos)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = pos;
            return t;
        }

        private static GameObject Mesh(Transform parent, string name, MeshKit k, string meshName) =>
            WeaponModels.MeshChild(parent, name, k.Build(meshName), Assets.ToolAtlas);

        private static GameObject GlowMesh(Transform parent, string name, MeshKit k, string meshName, Color c, float intensity = 3f) =>
            WeaponModels.MeshChild(parent, name, k.Build(meshName), Assets.Glow(c, intensity));

        /// <summary>Around the vertical axis: the profile is (radius, height).</summary>
        private static void Up(MeshKit k, Vector3 at, System.Action body) => GunParts.With(k, at, new Vector3(-90f, 0f, 0f), body);

        /// <summary>Armoured plinth: an octagonal deck of the given radius, a skirt of bolts and a stepped turret ring.</summary>
        private static void Plinth(MeshKit k, float r, float h, float ring)
        {
            var oct = new Vector2[8];
            for (int i = 0; i < 8; i++)
            {
                float a = (i + 0.5f) * Mathf.PI / 4f;
                oct[i] = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
            }
            var inner = new Vector2[8];
            for (int i = 0; i < 8; i++) inner[i] = oct[i] * 0.9f;
            Up(k, Vector3.zero, () =>
            {
                k.Prism(oct, 0f, h * 0.7f, Shade.HatchDark, Shade.Dark);
                k.Prism(inner, h * 0.7f, h, Shade.Dark, Shade.Black);
            });
            // hex bolts round the deck
            for (int i = 0; i < 16; i++)
            {
                float a = i * Mathf.PI / 8f;
                Up(k, new Vector3(Mathf.Cos(a) * r * 0.8f, h, Mathf.Sin(a) * r * 0.8f), () =>
                    k.Lathe(Vector2.zero, new[] { new Vector2(r * 0.03f, 0f), new Vector2(r * 0.03f, r * 0.02f), new Vector2(r * 0.018f, r * 0.03f) }, 6, Shade.Grey, null, Shade.Grey));
            }
            // turret ring: a light race with a dark gap under the head
            Up(k, Vector3.zero, () =>
                k.Lathe(Vector2.zero, new[] { new Vector2(ring, h), new Vector2(ring, h + ring * 0.12f), new Vector2(ring * 0.94f, h + ring * 0.16f), new Vector2(ring * 0.86f, h + ring * 0.16f) }, 32, Shade.Light, null, Shade.Grey));
            // hazard ticks on the deck edge, front and back
            foreach (float z in new[] { -1f, 1f })
                for (int i = -2; i <= 2; i++)
                    k.Box(new Vector3(i * r * 0.12f, h * 0.35f, z * r * 0.925f), new Vector3(r * 0.06f, h * 0.4f, 0.006f), i % 2 == 0 ? Shade.Yellow : Shade.Black);
        }

        private static Renderer Lens(Transform parent, Vector3 pos, Vector3 size, Color c)
        {
            var k = new MeshKit();
            k.Box(Vector3.zero, size, Shade.White);
            GameObject go = GlowMesh(parent, "Lens", k, "Arsenal.TurretLens" + size.x + "x" + size.y + "x" + size.z, c);
            go.transform.localPosition = pos;
            return go.GetComponent<Renderer>();
        }

        // ------------------------------------------------------------------ quad laser

        /// <summary>
        /// "Corsair" quad laser: the freighter gunner's ball turret. A yoke on the turntable carries the ball, which
        /// tilts with its four long blaster barrels on two side pylons (flared flash suppressors, cooling sleeves).
        /// </summary>
        public static GameObject Quad()
        {
            var root = new GameObject("QuadTurretModel");
            var k = new MeshKit();
            Plinth(k, 0.5f, 0.08f, 0.38f);
            Mesh(root.transform, "Base", k, "Arsenal.QuadBase");

            Transform head = Point(root.transform, "Head", new Vector3(0, 0.13f, 0));
            var h = new MeshKit();
            Up(h, Vector3.zero, () => h.Lathe(Vector2.zero, new[] { new Vector2(0.37f, 0f), new Vector2(0.37f, 0.05f), new Vector2(0.33f, 0.08f) }, 28, Shade.Dark, null, Shade.HatchDark));
            // yoke arms either side of the ball
            foreach (float x in new[] { -0.36f, 0.36f })
            {
                GunParts.Side(h, new[] { GunParts.P(-0.2f, 0.06f), GunParts.P(0.18f, 0.06f), GunParts.P(0.1f, 0.42f), GunParts.P(-0.12f, 0.42f) }, 0.06f, Shade.HatchLight, Shade.Light, 0.012f, x);
                GunParts.With(h, new Vector3(x, 0.37f, 0f), new Vector3(0, 90f, 0), () => h.Lathe(Vector2.zero, new[] { new Vector2(0.075f, -0.04f), new Vector2(0.075f, 0.04f) }, 16, Shade.Grey, Shade.Dark, Shade.Dark));
            }
            // ammo/power feed behind the ball
            h.ChamferBox(new Vector3(0, 0.15f, -0.25f), new Vector3(0.36f, 0.16f, 0.14f), 0.02f, Shade.Dark, Shade.Black);
            for (int i = 0; i < 4; i++)
                h.Box(new Vector3(-0.12f + i * 0.08f, 0.235f, -0.25f), new Vector3(0.03f, 0.01f, 0.1f), Shade.Grey);
            Mesh(head, "HeadBody", h, "Arsenal.QuadHead");
            Lens(head, new Vector3(0, 0.15f, -0.322f), new Vector3(0.22f, 0.03f, 0.008f), new Color(1f, 0.2f, 0.1f));

            Transform pitch = Point(head, "Pitch", new Vector3(0, 0.37f, 0));
            var b = new MeshKit();
            // the ball: a sphere around the gun axis with an armour band and the gunner's window at the front
            b.Lathe(Vector2.zero, Sphere(0.3f, 10), 24, Shade.Light, null, null);
            b.Lathe(Vector2.zero, new[] { new Vector2(0.305f, -0.06f), new Vector2(0.305f, 0.06f) }, 24, Shade.Grey, null, null);
            b.Tube(new Vector3(0, 0, 0.27f), 0.13f, 0.1f, 0.05f, 24, Shade.Dark, Shade.Black);
            for (int i = 0; i < 4; i++)
            {
                float a = i * 45f;
                GunParts.With(b, new Vector3(0, 0, 0.29f), new Vector3(0, 0, a), () => b.Box(Vector3.zero, new Vector3(0.2f, 0.012f, 0.012f), Shade.Black));
            }
            // side pylons on the trunnions
            foreach (float x in new[] { -0.47f, 0.47f })
            {
                b.ChamferBox(new Vector3(x, 0f, 0.0f), new Vector3(0.1f, 0.3f, 0.36f), 0.02f, Shade.HatchMid, Shade.Grey);
                GunParts.With(b, new Vector3(x * 0.87f, 0f, 0f), new Vector3(0, 90f, 0), () => b.Lathe(Vector2.zero, new[] { new Vector2(0.05f, -0.05f), new Vector2(0.05f, 0.05f) }, 14, Shade.Dark, Shade.Dark, Shade.Dark));
                b.Box(new Vector3(x + Mathf.Sign(x) * 0.051f, 0f, 0.02f), new Vector3(0.004f, 0.08f, 0.22f), Shade.Orange);
            }
            Mesh(pitch, "Ball", b, "Arsenal.QuadBall");
            var glass = new MeshKit();
            glass.Cylinder(Vector3.zero, 0.1f, 0.01f, 20, Shade.White, Shade.White);
            GameObject win = WeaponModels.MeshChild(pitch, "Window", glass.Build("Arsenal.QuadWindow"), Assets.Glow(new Color(0.35f, 0.55f, 0.75f), 0.6f));
            win.transform.localPosition = new Vector3(0, 0, 0.285f);

            Mesh barrel = QuadBarrel();

            foreach (float x in new[] { -0.47f, 0.47f })
                foreach (float y in new[] { 0.09f, -0.09f })
                {
                    Transform recoil = Point(pitch, "Recoil", new Vector3(x, y, 0f));
                    WeaponModels.MeshChild(recoil, "Barrel", barrel, Assets.ToolAtlas);
                    Point(recoil, "Muzzle", new Vector3(0, 0, 1.13f));

                }
            return root;
        }

        private static Mesh QuadBarrel()
        {
            var k = new MeshKit();
            k.Cylinder(new Vector3(0, 0, 0.32f), 0.05f, 0.36f, 12, Shade.Dark, Shade.HatchDark);
            k.Lathe(Vector2.zero, new[] { new Vector2(0.026f, 0.5f), new Vector2(0.026f, 1.02f) }, 12, Shade.Black);
            for (int i = 0; i < 4; i++)
                k.Cylinder(new Vector3(0, 0, 0.17f + i * 0.1f), 0.056f, 0.022f, 12, Shade.Grey, Shade.Grey);
            k.Cylinder(new Vector3(0, 0, 0.7f), 0.034f, 0.05f, 12, Shade.Dark, Shade.Dark);
            // flared flash suppressor
            k.Lathe(Vector2.zero, new[] { new Vector2(0.03f, 1.0f), new Vector2(0.034f, 1.04f), new Vector2(0.055f, 1.1f), new Vector2(0.055f, 1.12f) }, 14, Shade.Dark, null, Shade.Black);
            return k.Build("Arsenal.QuadBarrel");
        }

        /// <summary>Profile of a sphere around the z axis (radius, z), pole to pole.</summary>
        private static Vector2[] Sphere(float r, int steps)
        {
            var p = new Vector2[steps + 1];
            for (int i = 0; i <= steps; i++)
            {
                float a = Mathf.PI * i / steps;
                p[i] = new Vector2(Mathf.Sin(a) * r, -Mathf.Cos(a) * r);
            }
            return p;
        }

        // ------------------------------------------------------------------ twin turbolaser

        /// <summary>
        /// "Imperator" twin turbolaser: the capital ship's broadside turret. A low wedge-shaped armoured house with
        /// a sloped glacis, a sensor dome and green capacitor strips; two long heavy barrels come out of the mantlet.
        /// </summary>
        public static GameObject Turbolaser()
        {
            var root = new GameObject("TurbolaserModel");
            var k = new MeshKit();
            Plinth(k, 0.85f, 0.12f, 0.68f);
            Mesh(root.transform, "Base", k, "Arsenal.TurboBase");

            Transform head = Point(root.transform, "Head", new Vector3(0, 0.2f, 0));
            var h = new MeshKit();
            Up(h, Vector3.zero, () => h.Lathe(Vector2.zero, new[] { new Vector2(0.66f, 0f), new Vector2(0.66f, 0.06f) }, 32, Shade.Dark, null, Shade.HatchDark));
            Vector2[] house = { GunParts.P(-0.78f, 0.04f), GunParts.P(0.5f, 0.04f), GunParts.P(0.72f, 0.2f), GunParts.P(0.6f, 0.46f), GunParts.P(-0.5f, 0.54f), GunParts.P(-0.82f, 0.3f) };
            h.Profile(house, -0.62f, 0.62f, Shade.HatchLight, Shade.Light, 0.035f);
            // side armour skirts with panel lines and bolts
            foreach (float x in new[] { -1f, 1f })
            {
                Vector2[] skirt = { GunParts.P(-0.7f, 0.06f), GunParts.P(0.45f, 0.06f), GunParts.P(0.6f, 0.2f), GunParts.P(0.5f, 0.38f), GunParts.P(-0.62f, 0.4f) };
                h.Profile(skirt, x > 0 ? 0.62f : -0.67f, x > 0 ? 0.67f : -0.62f, Shade.HatchMid, Shade.Grey, 0.012f);
                for (int i = 0; i < 4; i++)
                    GunParts.Screw(h, x * 0.672f, 0.33f, -0.5f + i * 0.3f, 0.012f);
                for (int i = 0; i < 6; i++)
                    h.Box(new Vector3(x * 0.674f, 0.14f, -0.5f + i * 0.17f), new Vector3(0.004f, 0.06f, 0.08f), Shade.Black);
            }
            // front: dark mantlet slot the barrels come out of
            h.Box(new Vector3(0, 0.27f, 0.62f), new Vector3(0.82f, 0.24f, 0.12f), Shade.Black);
            // roof: sensor dome, hatches, antenna
            Up(h, new Vector3(-0.15f, 0.5f, -0.2f), () => h.Lathe(Vector2.zero, new[] { new Vector2(0.15f, 0f), new Vector2(0.15f, 0.04f), new Vector2(0.11f, 0.11f), new Vector2(0.0f, 0.14f) }, 20, Shade.Grey, null, null));
            h.ChamferBox(new Vector3(0.28f, 0.53f, -0.15f), new Vector3(0.22f, 0.04f, 0.3f), 0.012f, Shade.Dark, Shade.Black);
            Up(h, new Vector3(0.4f, 0.52f, -0.42f), () => h.Lathe(Vector2.zero, new[] { new Vector2(0.012f, 0f), new Vector2(0.012f, 0.3f) }, 8, Shade.Black, null, Shade.Black));
            // rear vents
            for (int i = 0; i < 5; i++)
                h.Box(new Vector3(-0.32f + i * 0.16f, 0.24f, -0.795f), new Vector3(0.11f, 0.14f, 0.012f), Shade.Black);
            Mesh(head, "HeadBody", h, "Arsenal.TurboHead");
            Color green = new Color(0.15f, 1f, 0.12f);
            Lens(head, new Vector3(0.676f, 0.25f, -0.1f), new Vector3(0.006f, 0.04f, 0.7f), green);
            Lens(head, new Vector3(-0.676f, 0.25f, -0.1f), new Vector3(0.006f, 0.04f, 0.7f), green);

            Transform pitch = Point(head, "Pitch", new Vector3(0, 0.27f, 0.5f));
            var c = new MeshKit();
            // mantlet: the armoured block that tilts with the barrels, on its trunnion
            c.ChamferBox(new Vector3(0, 0, 0.08f), new Vector3(0.72f, 0.22f, 0.34f), 0.03f, Shade.HatchDark, Shade.Dark);
            GunParts.With(c, Vector3.zero, new Vector3(0, 90f, 0), () => c.Lathe(Vector2.zero, new[] { new Vector2(0.09f, -0.4f), new Vector2(0.09f, 0.4f) }, 16, Shade.Grey, Shade.Dark, Shade.Dark));
            Mesh(pitch, "Mantlet", c, "Arsenal.TurboMantlet");
            Mesh barrel = TurboBarrel();
            foreach (float x in new[] { -0.2f, 0.2f })
            {
                Transform recoil = Point(pitch, "Recoil", new Vector3(x, 0f, 0f));
                WeaponModels.MeshChild(recoil, "Barrel", barrel, Assets.ToolAtlas);
                var e = new MeshKit();
                e.Cylinder(Vector3.zero, 0.05f, 0.01f, 16, Shade.White, Shade.White);
                GlowMesh(recoil, "Emitter", e, "Arsenal.TurboEmitter", green, 4f).transform.localPosition = new Vector3(0, 0, 2.18f);
                Point(recoil, "Muzzle", new Vector3(0, 0, 2.22f));
            }
            return root;
        }

        private static Mesh TurboBarrel()
        {
            var k = new MeshKit();
            // breech sleeve, long barrel stepping down, cooling rings and a fat emitter at the end
            k.Lathe(Vector2.zero, new[] { new Vector2(0.11f, 0.2f), new Vector2(0.11f, 0.7f), new Vector2(0.085f, 0.76f), new Vector2(0.085f, 1.3f), new Vector2(0.068f, 1.34f), new Vector2(0.068f, 2.05f) }, 16, Shade.Light, null, null);
            for (int i = 0; i < 3; i++)
                k.Cylinder(new Vector3(0, 0, 0.32f + i * 0.14f), 0.122f, 0.035f, 16, Shade.Dark, Shade.Dark);
            for (int i = 0; i < 4; i++)
                k.Cylinder(new Vector3(0, 0, 1.45f + i * 0.13f), 0.08f, 0.03f, 16, Shade.Grey, Shade.Grey);
            k.Tube(new Vector3(0, 0, 2.1f), 0.1f, 0.055f, 0.16f, 18, Shade.Dark, Shade.Black);
            k.Box(new Vector3(0, 0.087f, 1.0f), new Vector3(0.02f, 0.006f, 0.4f), Shade.Black);
            return k.Build("Arsenal.TurboBarrel");
        }

        // ------------------------------------------------------------------ ion turret

        /// <summary>
        /// "Tempest" ion turret: a rounded dome on a round plinth, twin barrels wound with glowing blue coils
        /// ending in emitter bulbs; both barrels fire together.
        /// </summary>
        public static GameObject Ion()
        {
            var root = new GameObject("IonTurretModel");
            var k = new MeshKit();
            Plinth(k, 0.62f, 0.1f, 0.5f);
            Mesh(root.transform, "Base", k, "Arsenal.IonBase");

            Transform head = Point(root.transform, "Head", new Vector3(0, 0.16f, 0));
            var h = new MeshKit();
            Up(h, Vector3.zero, () =>
            {
                h.Lathe(Vector2.zero, new[] { new Vector2(0.52f, 0f), new Vector2(0.52f, 0.1f), new Vector2(0.46f, 0.24f), new Vector2(0.32f, 0.36f), new Vector2(0.14f, 0.41f), new Vector2(0f, 0.42f) }, 28, Shade.Light, Shade.Dark, null);
                h.Lathe(Vector2.zero, new[] { new Vector2(0.525f, 0.03f), new Vector2(0.525f, 0.07f) }, 28, Shade.Dark, null, null);
            });
            // generator fins round the back of the dome
            for (int i = 0; i < 7; i++)
            {
                float a = (150f + i * 10f) * Mathf.Deg2Rad;
                GunParts.With(h, new Vector3(Mathf.Sin(a) * 0.42f, 0.18f, Mathf.Cos(a) * 0.42f), new Vector3(0, a * Mathf.Rad2Deg, 0), () =>
                    h.ChamferBox(Vector3.zero, new Vector3(0.02f, 0.22f, 0.16f), 0.006f, Shade.Grey, Shade.Dark));
            }
            Mesh(head, "HeadBody", h, "Arsenal.IonHead");
            Color blue = new Color(0.35f, 0.65f, 1f);
            Lens(head, new Vector3(0, 0.33f, -0.3f), new Vector3(0.18f, 0.02f, 0.06f), blue);

            Transform pitch = Point(head, "Pitch", new Vector3(0, 0.3f, 0.12f));
            var c = new MeshKit();
            c.ChamferBox(new Vector3(0, 0, 0.05f), new Vector3(0.52f, 0.24f, 0.44f), 0.04f, Shade.HatchDark, Shade.Dark);
            GunParts.With(c, Vector3.zero, new Vector3(0, 90f, 0), () => c.Lathe(Vector2.zero, new[] { new Vector2(0.08f, -0.3f), new Vector2(0.08f, 0.3f) }, 16, Shade.Grey, Shade.Dark, Shade.Dark));
            Mesh(pitch, "Breech", c, "Arsenal.IonBreech");
            Mesh barrel = IonBarrel();
            var coil = new MeshKit();
            coil.Tube(Vector3.zero, 0.105f, 0.06f, 0.05f, 20, Shade.White, Shade.White);
            Mesh coilMesh = coil.Build("Arsenal.IonCoil");
            Material coilMat = Assets.Glow(blue, 6f);
            foreach (float x in new[] { -0.17f, 0.17f })
            {
                Transform recoil = Point(pitch, "Recoil", new Vector3(x, 0f, 0f));
                WeaponModels.MeshChild(recoil, "Barrel", barrel, Assets.ToolAtlas);
                for (int i = 0; i < 5; i++)
                    WeaponModels.MeshChild(recoil, "Coil", coilMesh, coilMat).transform.localPosition = new Vector3(0, 0, 0.42f + i * 0.17f);
                var e = new MeshKit();
                e.Lathe(Vector2.zero, Sphere(0.075f, 8), 16, Shade.White, null, null);
                GlowMesh(recoil, "Emitter", e, "Arsenal.IonEmitter", blue, 4f).transform.localPosition = new Vector3(0, 0, 1.38f);
                Point(recoil, "Muzzle", new Vector3(0, 0, 1.48f));
            }
            return root;
        }

        private static Mesh IonBarrel()
        {
            var k = new MeshKit();
            k.Lathe(Vector2.zero, new[] { new Vector2(0.09f, 0.2f), new Vector2(0.09f, 0.34f), new Vector2(0.05f, 0.38f), new Vector2(0.05f, 1.3f), new Vector2(0.07f, 1.33f) }, 16, Shade.Dark, null, Shade.Black);
            // spacer discs between the coils
            for (int i = 0; i < 6; i++)
                k.Cylinder(new Vector3(0, 0, 0.335f + i * 0.17f), 0.115f, 0.02f, 16, Shade.Light, Shade.Grey);
            return k.Build("Arsenal.IonBarrel");
        }

        // ------------------------------------------------------------------ heavy turbolaser

        /// <summary>
        /// "Colossus" heavy turbolaser: a tall armoured house with a rangefinder bar across its roof and red
        /// capacitor banks down its flanks, one huge stepped barrel with a slotted muzzle brake.
        /// </summary>
        public static GameObject Colossus()
        {
            var root = new GameObject("ColossusModel");
            var k = new MeshKit();
            Plinth(k, 1.1f, 0.14f, 0.9f);
            Mesh(root.transform, "Base", k, "Arsenal.ColossusBase");

            Transform head = Point(root.transform, "Head", new Vector3(0, 0.24f, 0));
            var h = new MeshKit();
            Up(h, Vector3.zero, () => h.Lathe(Vector2.zero, new[] { new Vector2(0.88f, 0f), new Vector2(0.88f, 0.07f) }, 36, Shade.Dark, null, Shade.HatchDark));
            Vector2[] house = { GunParts.P(-1.0f, 0.05f), GunParts.P(0.72f, 0.05f), GunParts.P(0.95f, 0.28f), GunParts.P(0.82f, 0.74f), GunParts.P(-0.62f, 0.82f), GunParts.P(-1.04f, 0.48f) };
            h.Profile(house, -0.72f, 0.72f, Shade.HatchLight, Shade.Light, 0.045f);
            foreach (float x in new[] { -1f, 1f })
            {
                // capacitor bank housings on the flanks, with vents under them
                h.ChamferBox(new Vector3(x * 0.8f, 0.42f, -0.2f), new Vector3(0.16f, 0.36f, 1.0f), 0.03f, Shade.HatchDark, Shade.Dark);
                for (int i = 0; i < 6; i++)
                    h.Box(new Vector3(x * 0.882f, 0.42f, -0.6f + i * 0.16f), new Vector3(0.004f, 0.26f, 0.05f), Shade.Black);
                for (int i = 0; i < 5; i++)
                    GunParts.Screw(h, x * 0.722f, 0.15f, -0.8f + i * 0.35f, 0.016f);
            }
            h.Box(new Vector3(0, 0.38f, 0.85f), new Vector3(0.6f, 0.4f, 0.12f), Shade.Black); // mantlet slot
            // rangefinder across the roof, lenses at both ends
            GunParts.With(h, new Vector3(0, 0.95f, -0.35f), new Vector3(0, 90f, 0), () =>
            {
                h.Lathe(Vector2.zero, new[] { new Vector2(0.07f, -0.66f), new Vector2(0.07f, 0.66f) }, 16, Shade.Grey, Shade.Dark, Shade.Dark);
                h.Tube(new Vector3(0, 0, -0.66f), 0.1f, 0.06f, 0.12f, 16, Shade.Dark, Shade.Black);
                h.Tube(new Vector3(0, 0, 0.66f), 0.1f, 0.06f, 0.12f, 16, Shade.Dark, Shade.Black);
            });
            foreach (float x in new[] { -0.3f, 0.3f })
                h.ChamferBox(new Vector3(x, 0.86f, -0.35f), new Vector3(0.08f, 0.1f, 0.12f), 0.01f, Shade.Dark, Shade.Black);
            h.ChamferBox(new Vector3(0.3f, 0.84f, 0.2f), new Vector3(0.3f, 0.05f, 0.34f), 0.015f, Shade.Dark, Shade.Black); // hatch
            for (int i = 0; i < 6; i++)
                h.Box(new Vector3(-0.4f + i * 0.16f, 0.3f, -1.0f), new Vector3(0.11f, 0.2f, 0.012f), Shade.Black); // rear vents
            Mesh(head, "HeadBody", h, "Arsenal.ColossusHead");
            Color red = new Color(1f, 0.2f, 0.08f);
            Lens(head, new Vector3(0.884f, 0.6f, -0.2f), new Vector3(0.006f, 0.05f, 0.9f), red);
            Lens(head, new Vector3(-0.884f, 0.6f, -0.2f), new Vector3(0.006f, 0.05f, 0.9f), red);

            Transform pitch = Point(head, "Pitch", new Vector3(0, 0.42f, 0.68f));
            var c = new MeshKit();
            c.ChamferBox(new Vector3(0, 0, -0.05f), new Vector3(0.56f, 0.46f, 0.6f), 0.05f, Shade.HatchDark, Shade.Dark);
            GunParts.With(c, Vector3.zero, new Vector3(0, 90f, 0), () => c.Lathe(Vector2.zero, new[] { new Vector2(0.14f, -0.36f), new Vector2(0.14f, 0.36f) }, 18, Shade.Grey, Shade.Dark, Shade.Dark));
            Mesh(pitch, "Breech", c, "Arsenal.ColossusBreech");
            Transform recoil = Point(pitch, "Recoil", Vector3.zero);
            var b = new MeshKit();
            b.Lathe(Vector2.zero, new[]
            {
                new Vector2(0.2f, 0.2f), new Vector2(0.2f, 0.95f), new Vector2(0.16f, 1.0f), new Vector2(0.16f, 1.9f),
                new Vector2(0.13f, 1.95f), new Vector2(0.13f, 2.85f), new Vector2(0.17f, 2.9f), new Vector2(0.17f, 3.18f),
            }, 20, Shade.Light, null, Shade.Black);
            for (int i = 0; i < 4; i++)
                b.Cylinder(new Vector3(0, 0, 0.35f + i * 0.17f), 0.215f, 0.045f, 20, Shade.Dark, Shade.Dark);
            for (int i = 0; i < 3; i++)
                b.Cylinder(new Vector3(0, 0, 2.1f + i * 0.22f), 0.145f, 0.04f, 20, Shade.Grey, Shade.Grey);
            // muzzle brake slots
            for (int i = 0; i < 3; i++)
                foreach (float x in new[] { -1f, 1f })
                    b.Box(new Vector3(x * 0.165f, 0, 2.96f + i * 0.07f), new Vector3(0.012f, 0.12f, 0.035f), Shade.Black);
            WeaponModels.MeshChild(recoil, "Barrel", b.Build("Arsenal.ColossusBarrel"), Assets.ToolAtlas);
            var e = new MeshKit();
            e.Cylinder(Vector3.zero, 0.1f, 0.01f, 20, Shade.White, Shade.White);
            GlowMesh(recoil, "Emitter", e, "Arsenal.ColossusEmitter", red, 4f).transform.localPosition = new Vector3(0, 0, 3.12f);
            Point(recoil, "Muzzle", new Vector3(0, 0, 3.2f));
            return root;
        }

        // ------------------------------------------------------------------ triple battery

        /// <summary>
        /// "Annihilator" triple battery: a battleship turret 5 m across. A massive armoured house with a sloped glacis,
        /// a fire-control tower and a rangefinder bar on its roof, green capacitor banks down its flanks; three giant
        /// stepped barrels with muzzle brakes come out of one mantlet.
        /// </summary>
        public static GameObject Annihilator()
        {
            var root = new GameObject("AnnihilatorModel");
            var k = new MeshKit();
            Plinth(k, 2.6f, 0.32f, 2.15f);
            Mesh(root.transform, "Base", k, "Arsenal.AnnihilatorBase");

            Transform head = Point(root.transform, "Head", new Vector3(0, 0.5f, 0));
            var h = new MeshKit();
            Up(h, Vector3.zero, () => h.Lathe(Vector2.zero, new[] { new Vector2(2.08f, 0f), new Vector2(2.08f, 0.12f), new Vector2(1.95f, 0.16f) }, 48, Shade.Dark, null, Shade.HatchDark));
            Vector2[] house = { GunParts.P(-2.3f, 0.1f), GunParts.P(1.5f, 0.1f), GunParts.P(2.05f, 0.62f), GunParts.P(1.82f, 1.72f), GunParts.P(-1.45f, 1.95f), GunParts.P(-2.4f, 1.15f) };
            h.Profile(house, -1.75f, 1.75f, Shade.HatchLight, Shade.Light, 0.08f);
            foreach (float x in new[] { -1f, 1f })
            {
                // flank armour, capacitor banks and their vents
                Vector2[] skirt = { GunParts.P(-2.1f, 0.14f), GunParts.P(1.4f, 0.14f), GunParts.P(1.85f, 0.6f), GunParts.P(1.6f, 1.25f), GunParts.P(-1.9f, 1.3f) };
                h.Profile(skirt, x > 0 ? 1.75f : -1.88f, x > 0 ? 1.88f : -1.75f, Shade.HatchMid, Shade.Grey, 0.03f);
                h.ChamferBox(new Vector3(x * 1.98f, 0.95f, -0.45f), new Vector3(0.2f, 0.42f, 2.3f), 0.04f, Shade.HatchDark, Shade.Dark);
                for (int i = 0; i < 10; i++)
                    h.Box(new Vector3(x * 2.085f, 0.95f, -1.5f + i * 0.23f), new Vector3(0.006f, 0.3f, 0.07f), Shade.Black);
                for (int i = 0; i < 7; i++)
                    GunParts.Screw(h, x * 1.885f, 0.35f, -1.8f + i * 0.55f, 0.03f);
                for (int i = 0; i < 6; i++)
                    h.Box(new Vector3(x * 1.89f, 0.22f, -1.6f + i * 0.6f), new Vector3(0.006f, 0.06f, 0.3f), i % 2 == 0 ? Shade.Yellow : Shade.Black);
            }
            h.Box(new Vector3(0, 0.95f, 1.86f), new Vector3(3.1f, 0.95f, 0.3f), Shade.Black); // mantlet slot
            // fire-control tower at the back of the roof, with its window band and antennas
            h.ChamferBox(new Vector3(-0.55f, 2.25f, -1.05f), new Vector3(1.2f, 0.62f, 1.0f), 0.06f, Shade.HatchLight, Shade.Light);
            h.ChamferBox(new Vector3(-0.55f, 2.6f, -1.05f), new Vector3(0.9f, 0.1f, 0.75f), 0.03f, Shade.Dark, Shade.Black);
            foreach (float x in new[] { -0.9f, -0.2f })
                Up(h, new Vector3(x, 2.65f, -1.2f), () => h.Lathe(Vector2.zero, new[] { new Vector2(0.025f, 0f), new Vector2(0.025f, 0.8f), new Vector2(0.06f, 0.82f), new Vector2(0.06f, 0.86f) }, 8, Shade.Black, null, Shade.Black));
            // rangefinder bar across the roof, lens hoods at both ends
            GunParts.With(h, new Vector3(0, 2.05f, 0.25f), new Vector3(0, 90f, 0), () =>
            {
                h.Lathe(Vector2.zero, new[] { new Vector2(0.14f, -1.75f), new Vector2(0.14f, 1.75f) }, 18, Shade.Grey, Shade.Dark, Shade.Dark);
                h.Tube(new Vector3(0, 0, -1.75f), 0.22f, 0.13f, 0.26f, 18, Shade.Dark, Shade.Black);
                h.Tube(new Vector3(0, 0, 1.75f), 0.22f, 0.13f, 0.26f, 18, Shade.Dark, Shade.Black);
            });
            foreach (float x in new[] { -0.9f, 0.9f })
                h.ChamferBox(new Vector3(x, 1.95f, 0.25f), new Vector3(0.18f, 0.2f, 0.26f), 0.02f, Shade.Dark, Shade.Black);
            // roof hatches and rear vents
            h.ChamferBox(new Vector3(0.9f, 1.92f, -0.8f), new Vector3(0.6f, 0.08f, 0.6f), 0.025f, Shade.Dark, Shade.Black);
            for (int i = 0; i < 8; i++)
                h.Box(new Vector3(-1.2f + i * 0.34f, 0.65f, -2.39f), new Vector3(0.24f, 0.5f, 0.02f), Shade.Black);
            Mesh(head, "HeadBody", h, "Arsenal.AnnihilatorHead");
            Color green = new Color(0.2f, 1f, 0.25f);
            Lens(head, new Vector3(2.09f, 1.22f, -0.45f), new Vector3(0.008f, 0.08f, 2.1f), green);
            Lens(head, new Vector3(-2.09f, 1.22f, -0.45f), new Vector3(0.008f, 0.08f, 2.1f), green);
            Lens(head, new Vector3(-0.55f, 2.3f, -0.54f), new Vector3(0.95f, 0.1f, 0.008f), new Color(0.4f, 0.85f, 1f)); // tower window

            Transform pitch = Point(head, "Pitch", new Vector3(0, 0.95f, 1.55f));
            var c = new MeshKit();
            c.ChamferBox(new Vector3(0, 0, -0.2f), new Vector3(3.05f, 0.9f, 1.3f), 0.08f, Shade.HatchDark, Shade.Dark);
            GunParts.With(c, Vector3.zero, new Vector3(0, 90f, 0), () => c.Lathe(Vector2.zero, new[] { new Vector2(0.3f, -1.7f), new Vector2(0.3f, 1.7f) }, 22, Shade.Grey, Shade.Dark, Shade.Dark));
            Mesh(pitch, "Mantlet", c, "Arsenal.AnnihilatorMantlet");
            var b = new MeshKit();
            b.Lathe(Vector2.zero, new[]
            {
                new Vector2(0.42f, 0.3f), new Vector2(0.42f, 1.8f), new Vector2(0.34f, 1.9f), new Vector2(0.34f, 3.6f),
                new Vector2(0.28f, 3.7f), new Vector2(0.28f, 5.6f), new Vector2(0.37f, 5.7f), new Vector2(0.37f, 6.3f),
            }, 24, Shade.Light, null, Shade.Black);
            for (int i = 0; i < 5; i++)
                b.Cylinder(new Vector3(0, 0, 0.55f + i * 0.26f), 0.45f, 0.08f, 24, Shade.Dark, Shade.Dark);
            for (int i = 0; i < 4; i++)
                b.Cylinder(new Vector3(0, 0, 3.95f + i * 0.4f), 0.31f, 0.07f, 24, Shade.Grey, Shade.Grey);
            for (int i = 0; i < 4; i++)
                foreach (float x in new[] { -1f, 1f })
                    b.Box(new Vector3(x * 0.36f, 0, 5.8f + i * 0.13f), new Vector3(0.025f, 0.26f, 0.07f), Shade.Black);
            b.Box(new Vector3(0, 0.345f, 2.7f), new Vector3(0.05f, 0.012f, 1.4f), Shade.Black);
            Mesh barrel = b.Build("Arsenal.AnnihilatorBarrel");
            var e = new MeshKit();
            e.Cylinder(Vector3.zero, 0.22f, 0.02f, 24, Shade.White, Shade.White);
            Mesh emitter = e.Build("Arsenal.AnnihilatorEmitter");
            Material glow = Assets.Glow(green, 4f);
            foreach (float x in new[] { -1f, 0f, 1f })
            {
                Transform recoil = Point(pitch, "Recoil", new Vector3(x, 0f, 0f));
                WeaponModels.MeshChild(recoil, "Barrel", barrel, Assets.ToolAtlas);
                WeaponModels.MeshChild(recoil, "Emitter", emitter, glow).transform.localPosition = new Vector3(0, 0, 6.24f);
                Point(recoil, "Muzzle", new Vector3(0, 0, 6.4f));
            }
            return root;
        }

        // ------------------------------------------------------------------ fire-control station

        /// <summary>
        /// The gunner's fire-control console, set in front of the seat: a pedestal, a sloped desk with the green
        /// targeting screen and two firing grips with red triggers.
        /// </summary>
        public static GameObject Console()
        {
            var root = new GameObject("GunnerConsoleModel");
            var k = new MeshKit();
            k.ChamferBox(new Vector3(0, 0.3f, 0f), new Vector3(0.22f, 0.6f, 0.18f), 0.02f, Shade.HatchDark, Shade.Dark);
            k.ChamferBox(new Vector3(0, 0.02f, 0f), new Vector3(0.4f, 0.04f, 0.32f), 0.01f, Shade.Dark, Shade.Black);
            // sloped desk
            GunParts.With(k, new Vector3(0, 0.66f, -0.02f), new Vector3(-28f, 0, 0), () =>
            {
                k.ChamferBox(Vector3.zero, new Vector3(0.62f, 0.05f, 0.34f), 0.012f, Shade.HatchMid, Shade.Grey);
                k.Box(new Vector3(0, 0.027f, 0.03f), new Vector3(0.3f, 0.006f, 0.2f), Shade.Black);
                for (int i = 0; i < 5; i++)
                {
                    k.Box(new Vector3(-0.26f, 0.03f, -0.1f + i * 0.05f), new Vector3(0.03f, 0.01f, 0.02f), i % 2 == 0 ? Shade.Yellow : Shade.Grey);
                    k.Box(new Vector3(0.26f, 0.03f, -0.1f + i * 0.05f), new Vector3(0.03f, 0.01f, 0.02f), i == 2 ? Shade.Orange : Shade.Grey);
                }
            });
            // firing grips with triggers
            foreach (float x in new[] { -0.19f, 0.19f })
            {
                GunParts.With(k, new Vector3(x, 0.66f, -0.2f), new Vector3(-75f, 0, 0), () =>
                {
                    k.Lathe(Vector2.zero, new[] { new Vector2(0.018f, 0f), new Vector2(0.018f, 0.05f), new Vector2(0.024f, 0.06f), new Vector2(0.024f, 0.16f), new Vector2(0.018f, 0.18f) }, 12, Shade.Black, Shade.Black, Shade.Dark);
                });
                k.Box(new Vector3(x, 0.72f, -0.235f), new Vector3(0.012f, 0.03f, 0.02f), Shade.Red);
            }
            // a little periscope stalk at the back of the desk
            k.ChamferBox(new Vector3(0, 0.8f, 0.12f), new Vector3(0.12f, 0.14f, 0.08f), 0.012f, Shade.Dark, Shade.Black);
            WeaponModels.MeshChild(root.transform, "Body", k.Build("Arsenal.GunnerConsole"), Assets.ToolAtlas);
            var s = new MeshKit();
            s.Box(Vector3.zero, new Vector3(0.28f, 0.004f, 0.18f), Shade.White);
            GameObject screen = WeaponModels.MeshChild(root.transform, "Screen", s.Build("Arsenal.GunnerScreen"), Assets.Glow(new Color(0.2f, 1f, 0.35f), 1.6f));
            screen.transform.localPosition = new Vector3(0, 0.66f, -0.02f) + Quaternion.Euler(-28f, 0, 0) * new Vector3(0, 0.031f, 0.03f);
            screen.transform.localEulerAngles = new Vector3(-28f, 0, 0);
            return root;
        }
    }
}
