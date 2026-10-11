using UnityEngine;

namespace Arsenal.Visual
{
    /// <summary>
    /// Ship-mounted weapons. Base on the local y = 0 plane (the surface they are placed on), barrel along +z.
    /// Same chunky industrial style and atlas finishes as the game's ship parts.
    /// </summary>
    internal static class PartModels
    {
        private static Transform Point(Transform parent, string name, Vector3 pos)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = pos;
            return t;
        }

        private static void Mount(MeshKit k, float w, float d)
        {
            // bolted base plate with a raised deck, hex bolts and a stepped pivot ring
            k.ChamferBox(new Vector3(0, 0.022f, 0), new Vector3(w, 0.044f, d), 0.014f, Shade.HatchDark, Shade.Dark);
            k.ChamferBox(new Vector3(0, 0.05f, 0), new Vector3(w - 0.08f, 0.012f, d - 0.08f), 0.006f, Shade.Dark, Shade.Black);
            for (int x = -1; x <= 1; x += 2)
                for (int z = -1; z <= 1; z += 2)
                    GunParts.With(k, new Vector3(x * (w / 2 - 0.035f), 0.044f, z * (d / 2 - 0.035f)), new Vector3(-90f, 0, 0), () =>
                        k.Lathe(Vector2.zero, new[] { new Vector2(0.016f, 0f), new Vector2(0.016f, 0.01f), new Vector2(0.01f, 0.014f) }, 6, Shade.Grey, null, Shade.Grey));
            GunParts.With(k, Vector3.zero, new Vector3(-90f, 0, 0), () =>
                k.Lathe(Vector2.zero, new[] { new Vector2(0.18f, 0.056f), new Vector2(0.18f, 0.075f), new Vector2(0.165f, 0.082f), new Vector2(0.15f, 0.082f), new Vector2(0.15f, 0.12f) }, 24, Shade.Light, null, Shade.Grey));
            // ring of bolt heads on the pivot
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI / 4f;
                GunParts.With(k, new Vector3(Mathf.Cos(a) * 0.168f, 0.082f, Mathf.Sin(a) * 0.168f), new Vector3(-90f, 0, 0), () =>
                    k.Lathe(Vector2.zero, new[] { new Vector2(0.006f, 0f), new Vector2(0.006f, 0.005f) }, 6, Shade.Black, null, Shade.Black));
            }
            k.At(Vector3.zero);
        }

        /// <summary>Twin heavy machine gun on a turret block.</summary>
        public static GameObject MachineGun(out Transform[] muzzles)
        {
            var k = new MeshKit();
            Mount(k, 0.5f, 0.5f);
            // receiver block
            k.Bevel(new Vector3(0, 0.26f, 0.0f), new Vector3(0.3f, 0.2f, 0.42f), 0.04f, Shade.HatchLight, Shade.Light);
            k.Box(new Vector3(0, 0.365f, -0.02f), new Vector3(0.2f, 0.02f, 0.3f), Shade.Dark);
            k.Box(new Vector3(0.155f, 0.26f, 0.05f), new Vector3(0.012f, 0.06f, 0.2f), Shade.Yellow);
            // ammo box on the side
            k.Bevel(new Vector3(-0.21f, 0.22f, -0.04f), new Vector3(0.12f, 0.16f, 0.24f), 0.015f, Shade.HatchDark, Shade.Dark);
            k.Box(new Vector3(-0.272f, 0.22f, -0.04f), new Vector3(0.004f, 0.05f, 0.1f), Shade.Yellow);
            // twin barrels with cooling jackets
            foreach (float x in new[] { -0.065f, 0.065f })
            {
                k.Cylinder(new Vector3(x, 0.27f, 0.45f), 0.026f, 0.62f, 8, Shade.Black, Shade.Dark);
                k.Cylinder(new Vector3(x, 0.27f, 0.32f), 0.042f, 0.3f, 8, Shade.Dark, Shade.HatchDark);
                for (int i = 0; i < 3; i++)
                    k.Cylinder(new Vector3(x, 0.27f, 0.22f + i * 0.1f), 0.046f, 0.02f, 8, Shade.Grey, Shade.Grey);
                k.Cylinder(new Vector3(x, 0.27f, 0.76f), 0.034f, 0.05f, 8, Shade.Black, Shade.HatchDark);
            }
            var root = new GameObject("MachineGunModel");
            WeaponModels.MeshChild(root.transform, "Body", k.Build("Arsenal.MachineGun"), Assets.ToolAtlas);
            muzzles = new[] { Point(root.transform, "Muzzle", new Vector3(-0.065f, 0.27f, 0.8f)), Point(root.transform, "Muzzle", new Vector3(0.065f, 0.27f, 0.8f)) };
            return root;
        }

        /// <summary>Four-tube air-to-air missile launcher.</summary>
        public static GameObject MissilePod(out Transform[] tubes)
        {
            var k = new MeshKit();
            Mount(k, 0.56f, 0.6f);
            k.Bevel(new Vector3(0, 0.33f, 0.04f), new Vector3(0.5f, 0.38f, 0.74f), 0.05f, Shade.HatchLight, Shade.Light);
            k.Box(new Vector3(0, 0.33f, 0.415f), new Vector3(0.46f, 0.34f, 0.01f), Shade.Dark);
            k.Box(new Vector3(0.252f, 0.33f, 0.04f), new Vector3(0.006f, 0.08f, 0.5f), Shade.Orange);
            k.Box(new Vector3(-0.252f, 0.33f, 0.04f), new Vector3(0.006f, 0.08f, 0.5f), Shade.Orange);
            k.Box(new Vector3(0, 0.525f, -0.1f), new Vector3(0.12f, 0.02f, 0.2f), Shade.Dark);
            var root = new GameObject("MissilePodModel");
            tubes = new Transform[4];
            int n = 0;
            foreach (float y in new[] { 0.42f, 0.24f })
                foreach (float x in new[] { -0.11f, 0.11f })
                {
                    k.Cylinder(new Vector3(x, y, 0.42f), 0.07f, 0.02f, 10, Shade.Black, Shade.Dark);
                    tubes[n++] = Point(root.transform, "Tube", new Vector3(x, y, 0.36f));
                }
            WeaponModels.MeshChild(root.transform, "Body", k.Build("Arsenal.MissilePod"), Assets.ToolAtlas);
            // missile noses visible in the tubes (hidden as they are fired)
            Mesh nose = MissileNose();
            foreach (Transform t in tubes)
            {
                GameObject m = WeaponModels.MeshChild(t, "Nose", nose, Assets.ToolAtlas);
                m.transform.localPosition = new Vector3(0, 0, 0.0f);
            }
            return root;
        }

        private static Mesh MissileNose()
        {
            var k = new MeshKit();
            k.Frustum(new Vector3(0, 0, 0), 0.05f, 0.0f, 0.1f, 8, Shade.Red);
            return k.Build("Arsenal.MissileNose");
        }

        /// <summary>The missile in flight, nose along +z.</summary>
        public static Mesh Missile()
        {
            var k = new MeshKit();
            k.Cylinder(new Vector3(0, 0, 0), 0.05f, 0.8f, 8, Shade.Light, Shade.HatchLight);
            k.Cylinder(new Vector3(0, 0, 0.2f), 0.052f, 0.06f, 8, Shade.Yellow, Shade.Yellow);
            k.Frustum(new Vector3(0, 0, 0.4f), 0.05f, 0.0f, 0.18f, 8, Shade.Red);
            k.Cylinder(new Vector3(0, 0, -0.41f), 0.04f, 0.04f, 8, Shade.Black, Shade.Dark);
            for (int i = 0; i < 4; i++)
            {
                k.At(Vector3.zero, new Vector3(0, 0, i * 90f + 45f));
                k.Box(new Vector3(0, 0.085f, -0.3f), new Vector3(0.008f, 0.07f, 0.16f), Shade.Dark);
                k.Box(new Vector3(0, 0.07f, 0.12f), new Vector3(0.006f, 0.04f, 0.08f), Shade.Dark);
            }
            k.At(Vector3.zero);
            return k.Build("Arsenal.Missile");
        }

        /// <summary>Laser cannon: coil body, cooling fins, glowing lens.</summary>
        public static GameObject LaserCannon(out Transform muzzle, out Renderer lens)
        {
            var k = new MeshKit();
            Mount(k, 0.5f, 0.5f);
            k.Bevel(new Vector3(0, 0.25f, -0.1f), new Vector3(0.28f, 0.2f, 0.3f), 0.04f, Shade.HatchDark, Shade.Dark);
            k.Cylinder(new Vector3(0, 0.27f, 0.25f), 0.12f, 0.5f, 10, Shade.Light, Shade.HatchLight);
            for (int i = 0; i < 4; i++)
                k.Cylinder(new Vector3(0, 0.27f, 0.08f + i * 0.1f), 0.135f, 0.03f, 10, Shade.Blue, Shade.Dark);
            // cooling fins
            for (int i = 0; i < 5; i++)
            {
                k.Box(new Vector3(0.16f, 0.25f, -0.2f + i * 0.045f), new Vector3(0.05f, 0.14f, 0.012f), Shade.Grey);
                k.Box(new Vector3(-0.16f, 0.25f, -0.2f + i * 0.045f), new Vector3(0.05f, 0.14f, 0.012f), Shade.Grey);
            }
            k.Frustum(new Vector3(0, 0.27f, 0.5f), 0.1f, 0.065f, 0.12f, 10, Shade.Dark);
            k.Cylinder(new Vector3(0, 0.27f, 0.635f), 0.07f, 0.03f, 10, Shade.Black, Shade.Black);
            var root = new GameObject("LaserCannonModel");
            WeaponModels.MeshChild(root.transform, "Body", k.Build("Arsenal.LaserCannon"), Assets.ToolAtlas);
            var lk = new MeshKit();
            lk.Cylinder(Vector3.zero, 0.05f, 0.01f, 10, Shade.White, Shade.White);
            GameObject lensGo = WeaponModels.MeshChild(root.transform, "Lens", lk.Build("Arsenal.Lens"), Assets.Glow(new Color(0.3f, 0.8f, 1f), 3f));
            lensGo.transform.localPosition = new Vector3(0, 0.27f, 0.652f);
            lens = lensGo.GetComponent<Renderer>();
            muzzle = Point(root.transform, "Muzzle", new Vector3(0, 0.27f, 0.67f));
            return root;
        }

        private static Renderer Lens(Transform parent, Vector3 pos, float radius, Color color)
        {
            var lk = new MeshKit();
            lk.Cylinder(Vector3.zero, radius, 0.01f, 10, Shade.White, Shade.White);
            GameObject go = WeaponModels.MeshChild(parent, "Lens", lk.Build("Arsenal.Lens" + radius), Assets.Glow(color, 3f));
            go.transform.localPosition = pos;
            return go.GetComponent<Renderer>();
        }

        /// <summary>"Guardian" point-defence gatling: a turning head on a fixed base. Head yaws, Pitch tilts the gun.</summary>
        public static GameObject Pdc()
        {
            var root = new GameObject("PdcModel");
            var k = new MeshKit();
            Mount(k, 0.5f, 0.5f);
            k.At(Vector3.zero, new Vector3(-90f, 0, 0));
            k.Cylinder(new Vector3(0, 0, 0.1f), 0.13f, 0.08f, 10, Shade.Dark, Shade.HatchDark);
            k.At(Vector3.zero);
            WeaponModels.MeshChild(root.transform, "Base", k.Build("Arsenal.PdcBase"), Assets.ToolAtlas);

            Transform head = Point(root.transform, "Head", new Vector3(0, 0.12f, 0));
            var h = new MeshKit();
            h.Bevel(new Vector3(0, 0.08f, -0.02f), new Vector3(0.36f, 0.16f, 0.3f), 0.04f, Shade.HatchLight, Shade.Light);
            h.Box(new Vector3(0.181f, 0.08f, -0.02f), new Vector3(0.006f, 0.05f, 0.2f), Shade.Yellow);
            h.Box(new Vector3(-0.181f, 0.08f, -0.02f), new Vector3(0.006f, 0.05f, 0.2f), Shade.Yellow);
            h.Box(new Vector3(0, 0.17f, -0.1f), new Vector3(0.08f, 0.04f, 0.08f), Shade.Dark); // sensor
            WeaponModels.MeshChild(head, "Head", h.Build("Arsenal.PdcHead"), Assets.ToolAtlas);
            Lens(head, new Vector3(0, 0.17f, -0.055f), 0.025f, new Color(1f, 0.3f, 0.2f));

            Transform pitch = Point(head, "Pitch", new Vector3(0, 0.1f, 0.08f));
            var g = new MeshKit();
            g.Cylinder(new Vector3(0, 0, 0.05f), 0.07f, 0.14f, 10, Shade.Dark, Shade.HatchDark);
            g.Cylinder(new Vector3(0, 0, 0.13f), 0.075f, 0.02f, 10, Shade.Grey, Shade.Grey);
            WeaponModels.MeshChild(pitch, "Gun", g.Build("Arsenal.PdcGun"), Assets.ToolAtlas);
            Transform spin = Point(pitch, "Barrels", Vector3.zero);
            var b = new MeshKit();
            for (int i = 0; i < 6; i++)
            {
                float a = i * Mathf.PI / 3f;
                b.Cylinder(new Vector3(Mathf.Cos(a) * 0.038f, Mathf.Sin(a) * 0.038f, 0.3f), 0.013f, 0.36f, 6, Shade.Black, Shade.Dark);
            }
            b.Cylinder(new Vector3(0, 0, 0.42f), 0.058f, 0.03f, 10, Shade.Dark, Shade.Grey);
            WeaponModels.MeshChild(spin, "Barrels", b.Build("Arsenal.PdcBarrels"), Assets.ToolAtlas);
            Point(pitch, "Muzzle", new Vector3(0, 0, 0.5f));
            return root;
        }

        /// <summary>"P-400" plasma cannon: fat containment chamber with orange coils.</summary>
        public static GameObject PlasmaCannon()
        {
            var root = new GameObject("PlasmaCannonModel");
            var k = new MeshKit();
            Mount(k, 0.56f, 0.56f);
            k.Bevel(new Vector3(0, 0.25f, -0.12f), new Vector3(0.34f, 0.22f, 0.3f), 0.05f, Shade.HatchDark, Shade.Dark);
            k.Cylinder(new Vector3(0, 0.29f, 0.12f), 0.15f, 0.36f, 12, Shade.Light, Shade.HatchLight);
            for (int i = 0; i < 3; i++)
                k.Cylinder(new Vector3(0, 0.29f, 0.0f + i * 0.12f), 0.165f, 0.04f, 12, Shade.Orange, Shade.Dark);
            k.Frustum(new Vector3(0, 0.29f, 0.3f), 0.12f, 0.08f, 0.2f, 12, Shade.Dark);
            k.Cylinder(new Vector3(0, 0.29f, 0.52f), 0.09f, 0.04f, 12, Shade.Black, Shade.HatchDark);
            k.Box(new Vector3(0.18f, 0.2f, -0.12f), new Vector3(0.03f, 0.1f, 0.2f), Shade.Yellow);
            WeaponModels.MeshChild(root.transform, "Body", k.Build("Arsenal.PlasmaCannon"), Assets.ToolAtlas);
            Lens(root.transform, new Vector3(0, 0.29f, 0.542f), 0.07f, new Color(1f, 0.5f, 0.15f));
            Point(root.transform, "Muzzle", new Vector3(0, 0.29f, 0.6f));
            return root;
        }

        /// <summary>"Disruptor" ion cannon: three blue emitter prongs around a core.</summary>
        public static GameObject IonCannon()
        {
            var root = new GameObject("IonCannonModel");
            var k = new MeshKit();
            Mount(k, 0.5f, 0.5f);
            k.Bevel(new Vector3(0, 0.25f, -0.08f), new Vector3(0.3f, 0.2f, 0.36f), 0.04f, Shade.HatchLight, Shade.Light);
            k.Cylinder(new Vector3(0, 0.27f, 0.2f), 0.06f, 0.4f, 10, Shade.Dark, Shade.Dark);
            for (int i = 0; i < 3; i++)
            {
                float a = i * Mathf.PI * 2f / 3f + Mathf.PI / 2f;
                var off = new Vector3(Mathf.Cos(a) * 0.11f, 0.27f + Mathf.Sin(a) * 0.11f, 0.24f);
                k.Box(off, new Vector3(0.04f, 0.04f, 0.42f), Shade.Blue, Shade.Dark);
            }
            for (int i = 0; i < 2; i++)
                k.Cylinder(new Vector3(0, 0.27f, 0.12f + i * 0.16f), 0.15f, 0.025f, 12, Shade.Grey, Shade.Grey);
            WeaponModels.MeshChild(root.transform, "Body", k.Build("Arsenal.IonCannon"), Assets.ToolAtlas);
            Lens(root.transform, new Vector3(0, 0.27f, 0.405f), 0.05f, new Color(0.4f, 0.65f, 1f));
            Point(root.transform, "Muzzle", new Vector3(0, 0.27f, 0.48f));
            return root;
        }

        /// <summary>"Longinus" ship railgun: long twin rails with capacitor banks.</summary>
        public static GameObject Railgun()
        {
            var root = new GameObject("RailgunModel");
            var k = new MeshKit();
            Mount(k, 0.5f, 0.5f);
            k.Bevel(new Vector3(0, 0.25f, -0.12f), new Vector3(0.32f, 0.22f, 0.3f), 0.04f, Shade.HatchDark, Shade.Dark);
            // capacitor banks
            for (int i = 0; i < 3; i++)
            {
                k.Cylinder(new Vector3(0.2f, 0.22f, -0.2f + i * 0.08f), 0.03f, 0.06f, 8, Shade.Blue, Shade.Light);
                k.Cylinder(new Vector3(-0.2f, 0.22f, -0.2f + i * 0.08f), 0.03f, 0.06f, 8, Shade.Blue, Shade.Light);
            }
            // rails
            foreach (float y in new[] { 0.21f, 0.33f })
                k.Box(new Vector3(0, y, 0.33f), new Vector3(0.14f, 0.035f, 0.92f), Shade.Light, Shade.HatchLight);
            k.Box(new Vector3(0, 0.27f, 0.33f), new Vector3(0.03f, 0.085f, 0.9f), Shade.Black);
            for (int i = 0; i < 4; i++)
                k.Box(new Vector3(0, 0.27f, 0.0f + i * 0.22f), new Vector3(0.17f, 0.17f, 0.03f), Shade.Dark);
            k.Box(new Vector3(0.072f, 0.33f, 0.33f), new Vector3(0.004f, 0.02f, 0.7f), Shade.Yellow);
            WeaponModels.MeshChild(root.transform, "Body", k.Build("Arsenal.Railgun"), Assets.ToolAtlas);
            Lens(root.transform, new Vector3(0, 0.362f, -0.12f), 0.06f, new Color(0.5f, 0.85f, 1f)).transform.localEulerAngles = new Vector3(-90f, 0, 0); // charge light on top of the breech
            Point(root.transform, "Muzzle", new Vector3(0, 0.27f, 0.8f));
            return root;
        }

        /// <summary>"Leviathan" torpedo tube: one big tube with a visible warhead.</summary>
        public static GameObject TorpedoTube()
        {
            var root = new GameObject("TorpedoTubeModel");
            var k = new MeshKit();
            Mount(k, 0.6f, 0.7f);
            k.Bevel(new Vector3(0, 0.2f, -0.05f), new Vector3(0.44f, 0.14f, 0.6f), 0.04f, Shade.HatchDark, Shade.Dark);
            k.Cylinder(new Vector3(0, 0.38f, 0.05f), 0.2f, 0.9f, 12, Shade.Dark, Shade.HatchLight);
            for (int i = 0; i < 3; i++)
                k.Cylinder(new Vector3(0, 0.38f, -0.25f + i * 0.3f), 0.215f, 0.04f, 12, Shade.Grey, Shade.Grey);
            k.Cylinder(new Vector3(0, 0.38f, 0.505f), 0.16f, 0.012f, 12, Shade.Black, Shade.Black);
            k.Box(new Vector3(0.205f, 0.38f, 0.05f), new Vector3(0.012f, 0.06f, 0.5f), Shade.Red);
            k.Box(new Vector3(-0.205f, 0.38f, 0.05f), new Vector3(0.012f, 0.06f, 0.5f), Shade.Red);
            WeaponModels.MeshChild(root.transform, "Body", k.Build("Arsenal.TorpedoTube"), Assets.ToolAtlas);
            Transform tube = Point(root.transform, "Tube", new Vector3(0, 0.38f, 0.45f));
            var n = new MeshKit();
            n.Frustum(Vector3.zero, 0.13f, 0.03f, 0.14f, 10, Shade.Red);
            WeaponModels.MeshChild(tube, "Nose", n.Build("Arsenal.TorpedoNose"), Assets.ToolAtlas);
            Point(root.transform, "Muzzle", new Vector3(0, 0.38f, 0.75f));
            return root;
        }

        /// <summary>"Grêle" rocket pod: armoured box of 3 x 3 launch tubes with rocket noses showing (each "Tube").</summary>
        public static GameObject RocketPod()
        {
            var root = new GameObject("RocketPodModel");
            var k = new MeshKit();
            Mount(k, 0.56f, 0.6f);
            // armoured housing: chamfered box, side armour plates with bolts, hazard stripes
            k.ChamferBox(new Vector3(0, 0.33f, 0.02f), new Vector3(0.44f, 0.38f, 0.62f), 0.035f, Shade.HatchLight, Shade.Light);
            foreach (float x in new[] { -0.226f, 0.226f })
            {
                k.ChamferBox(new Vector3(x, 0.33f, 0.0f), new Vector3(0.016f, 0.3f, 0.5f), 0.006f, Shade.Dark, Shade.HatchDark);
                for (int i = 0; i < 3; i++)
                    for (int j = 0; j < 2; j++)
                        GunParts.Screw(k, x + Mathf.Sign(x) * 0.008f, 0.22f + j * 0.22f, -0.2f + i * 0.2f, 0.007f);
                for (int i = 0; i < 4; i++)
                    k.Box(new Vector3(x + Mathf.Sign(x) * 0.009f, 0.155f, -0.2f + i * 0.13f), new Vector3(0.002f, 0.02f, 0.06f), i % 2 == 0 ? Shade.Yellow : Shade.Black);
            }
            // front face with the tube mouths
            k.ChamferBox(new Vector3(0, 0.33f, 0.34f), new Vector3(0.4f, 0.34f, 0.03f), 0.012f, Shade.Dark, Shade.Black);
            // top: sensor box and lifting eyes
            k.ChamferBox(new Vector3(0.1f, 0.535f, -0.15f), new Vector3(0.1f, 0.03f, 0.14f), 0.008f, Shade.Dark, Shade.Black);
            foreach (float z in new[] { -0.2f, 0.2f })
                GunParts.With(k, new Vector3(-0.12f, 0.535f, z), new Vector3(0, 90f, 0), () => k.Tube(Vector3.zero, 0.022f, 0.012f, 0.012f, 12, Shade.Grey, Shade.Black));
            // rear: cable socket housing
            k.ChamferBox(new Vector3(0, 0.25f, -0.3f), new Vector3(0.2f, 0.12f, 0.04f), 0.01f, Shade.Dark, Shade.Black);
            Mesh nose = RocketNose(0.04f);
            for (int r = 0; r < 3; r++)
                for (int c = 0; c < 3; c++)
                {
                    Vector3 p = new Vector3(-0.12f + c * 0.12f, 0.22f + r * 0.11f, 0f);
                    k.Tube(new Vector3(p.x, p.y, 0.355f), 0.05f, 0.042f, 0.02f, 16, Shade.Grey, Shade.Black);
                    k.Lathe(new Vector2(p.x, p.y), new[] { new Vector2(0.042f, 0.2f), new Vector2(0.042f, 0.365f) }, 16, Shade.Black);
                    Transform t = Point(root.transform, "Tube", new Vector3(p.x, p.y, 0.3f));
                    WeaponModels.MeshChild(t, "Nose", nose, Assets.ToolAtlas);
                }
            WeaponModels.MeshChild(root.transform, "Body", k.Build("Arsenal.RocketPod"), Assets.ToolAtlas);
            return root;
        }

        private static Mesh RocketNose(float r)
        {
            var k = new MeshKit();
            k.Lathe(Vector2.zero, new[] { new Vector2(r, -0.05f), new Vector2(r, 0.0f), new Vector2(r * 0.8f, 0.03f), new Vector2(r * 0.35f, 0.055f), new Vector2(0f, 0.062f) }, 12, Shade.Red);
            k.Lathe(Vector2.zero, new[] { new Vector2(r * 1.01f, -0.03f), new Vector2(r * 1.01f, -0.02f) }, 12, Shade.Yellow, Shade.Yellow, Shade.Yellow);
            return k.Build("Arsenal.RocketNose" + r);
        }

        /// <summary>
        /// "Faucon" SAM battery: a yawing Head with a radar mast (Radar spins) and a Pitch cradle carrying four
        /// missile canisters ("Tube", seeker domes showing). Barrel axis along +z.
        /// </summary>
        public static GameObject SamBattery()
        {
            var root = new GameObject("SamBatteryModel");
            var k = new MeshKit();
            Mount(k, 0.56f, 0.56f);
            WeaponModels.MeshChild(root.transform, "Base", k.Build("Arsenal.SamBase"), Assets.ToolAtlas);

            Transform head = Point(root.transform, "Head", new Vector3(0, 0.12f, 0));
            var h = new MeshKit();
            // turntable and yoke
            GunParts.With(h, Vector3.zero, new Vector3(-90f, 0, 0), () =>
                h.Lathe(Vector2.zero, new[] { new Vector2(0.17f, 0f), new Vector2(0.17f, 0.04f), new Vector2(0.14f, 0.055f) }, 24, Shade.Dark, null, Shade.HatchDark));
            foreach (float x in new[] { -0.17f, 0.17f })
                GunParts.Side(h, new[] { GunParts.P(-0.1f, 0.04f), GunParts.P(0.1f, 0.04f), GunParts.P(0.06f, 0.24f), GunParts.P(-0.06f, 0.24f) }, 0.035f, Shade.HatchLight, Shade.Light, 0.008f, x);
            // radar mast behind the cradle
            h.ChamferBox(new Vector3(0, 0.12f, -0.15f), new Vector3(0.08f, 0.14f, 0.08f), 0.01f, Shade.Dark, Shade.Black);
            GunParts.With(h, new Vector3(0, 0.19f, -0.15f), new Vector3(-90f, 0, 0), () => h.Lathe(Vector2.zero, new[] { new Vector2(0.018f, 0f), new Vector2(0.018f, 0.18f) }, 10, Shade.Grey, null, Shade.Grey));
            WeaponModels.MeshChild(head, "Head", h.Build("Arsenal.SamHead"), Assets.ToolAtlas);
            Lens(head, new Vector3(0, 0.12f, -0.107f), 0.02f, new Color(1f, 0.3f, 0.2f)).transform.localEulerAngles = new Vector3(0, 180f, 0);

            Transform radar = Point(head, "Radar", new Vector3(0, 0.38f, -0.15f));
            var r = new MeshKit();
            // dish (shallow bowl facing +z) on a little arm, and its feed horn
            r.Lathe(Vector2.zero, new[] { new Vector2(0.0f, -0.03f), new Vector2(0.07f, -0.02f), new Vector2(0.125f, 0.004f), new Vector2(0.125f, 0.016f) }, 20, Shade.Light, null, Shade.Dark);
            r.Lathe(Vector2.zero, new[] { new Vector2(0.006f, -0.006f), new Vector2(0.006f, 0.07f), new Vector2(0.014f, 0.08f) }, 8, Shade.Black, null, Shade.Black);
            r.ChamferBox(new Vector3(0, -0.04f, -0.03f), new Vector3(0.03f, 0.05f, 0.03f), 0.005f, Shade.Dark, Shade.Black);
            WeaponModels.MeshChild(radar, "Dish", r.Build("Arsenal.SamRadar"), Assets.ToolAtlas);

            Transform pitch = Point(head, "Pitch", new Vector3(0, 0.2f, 0.02f));
            var c = new MeshKit();
            // trunnion and the 2 x 2 canister pack
            GunParts.With(c, Vector3.zero, new Vector3(0, 90f, 0), () => c.Lathe(Vector2.zero, new[] { new Vector2(0.04f, -0.19f), new Vector2(0.04f, 0.19f) }, 14, Shade.Grey, Shade.Dark, Shade.Dark));
            c.ChamferBox(new Vector3(0, 0f, 0.05f), new Vector3(0.28f, 0.03f, 0.5f), 0.008f, Shade.Dark, Shade.Black);
            Mesh dome = SamDome();
            foreach (float y in new[] { -0.075f, 0.075f })
                foreach (float x in new[] { -0.075f, 0.075f })
                {
                    c.ChamferBox(new Vector3(x, y, 0.05f), new Vector3(0.13f, 0.13f, 0.56f), 0.014f, Shade.HatchMid, Shade.Grey);
                    c.Tube(new Vector3(x, y, 0.335f), 0.05f, 0.044f, 0.012f, 16, Shade.Dark, Shade.Black);
                    c.Lathe(new Vector2(x, y), new[] { new Vector2(0.044f, 0.2f), new Vector2(0.044f, 0.336f) }, 16, Shade.Black);
                    c.Box(new Vector3(x + Mathf.Sign(x) * 0.066f, y, 0.05f), new Vector3(0.002f, 0.03f, 0.3f), Shade.Yellow);
                    Transform t = Point(pitch, "Tube", new Vector3(x, y, 0.3f));
                    WeaponModels.MeshChild(t, "Dome", dome, Assets.ToolAtlas);
                }
            // strap bands around the pack
            foreach (float z in new[] { -0.12f, 0.2f })
                c.ChamferBox(new Vector3(0, 0f, z), new Vector3(0.29f, 0.29f, 0.03f), 0.012f, Shade.Dark, Shade.Black);
            WeaponModels.MeshChild(pitch, "Pack", c.Build("Arsenal.SamPack"), Assets.ToolAtlas);
            Point(pitch, "Muzzle", new Vector3(0, 0f, 0.4f));
            return root;
        }

        private static Mesh SamDome()
        {
            var k = new MeshKit();
            k.Lathe(Vector2.zero, new[] { new Vector2(0.04f, -0.06f), new Vector2(0.04f, 0.0f), new Vector2(0.034f, 0.025f), new Vector2(0.018f, 0.042f), new Vector2(0f, 0.046f) }, 14, Shade.White);
            k.Lathe(Vector2.zero, new[] { new Vector2(0.041f, -0.02f), new Vector2(0.041f, -0.01f) }, 14, Shade.Yellow, Shade.Yellow, Shade.Yellow);
            return k.Build("Arsenal.SamDome");
        }

        /// <summary>Fire-control console bolted to the right of the advanced pilot seat, with three coloured triggers.</summary>
        public static GameObject GunnerConsole()
        {
            var k = new MeshKit();
            k.Bevel(new Vector3(-0.43f, 0.35f, 0.0f), new Vector3(0.14f, 0.7f, 0.5f), 0.03f, Shade.HatchDark, Shade.Dark);
            k.Box(new Vector3(-0.43f, 0.71f, 0.0f), new Vector3(0.16f, 0.03f, 0.52f), Shade.Light);
            k.Box(new Vector3(-0.43f, 0.735f, -0.15f), new Vector3(0.05f, 0.02f, 0.05f), Shade.Red);
            k.Box(new Vector3(-0.43f, 0.735f, 0.0f), new Vector3(0.05f, 0.02f, 0.05f), Shade.Yellow);
            k.Box(new Vector3(-0.43f, 0.735f, 0.15f), new Vector3(0.05f, 0.02f, 0.05f), Shade.Blue);
            k.Box(new Vector3(-0.501f, 0.45f, 0.0f), new Vector3(0.004f, 0.08f, 0.4f), Shade.Orange);
            var root = new GameObject("GunnerConsoleModel");
            WeaponModels.MeshChild(root.transform, "Body", k.Build("Arsenal.GunnerConsole"), Assets.ToolAtlas);
            return root;
        }

        /// <summary>Weapon rack shown on top of the armory chest.</summary>
        public static GameObject ArmoryRack()
        {
            var root = new GameObject("ArmoryRackModel");
            var k = new MeshKit();
            k.Box(new Vector3(0, 0.62f, -0.2f), new Vector3(0.9f, 0.05f, 0.06f), Shade.Dark);
            k.Box(new Vector3(-0.42f, 0.75f, -0.2f), new Vector3(0.05f, 0.3f, 0.05f), Shade.Dark);
            k.Box(new Vector3(0.42f, 0.75f, -0.2f), new Vector3(0.05f, 0.3f, 0.05f), Shade.Dark);
            k.Box(new Vector3(0, 0.89f, -0.2f), new Vector3(0.9f, 0.04f, 0.05f), Shade.Yellow);
            WeaponModels.MeshChild(root.transform, "Rack", k.Build("Arsenal.Rack"), Assets.ToolAtlas);
            Model rifle = WeaponModels.Rifle();
            rifle.Root.transform.SetParent(root.transform, false);
            rifle.Root.transform.localPosition = new Vector3(-0.15f, 0.76f, -0.16f);
            rifle.Root.transform.localEulerAngles = new Vector3(0, 90f, 80f);
            Model rifle2 = WeaponModels.Marksman();
            rifle2.Root.transform.SetParent(root.transform, false);
            rifle2.Root.transform.localPosition = new Vector3(0.2f, 0.76f, -0.16f);
            rifle2.Root.transform.localEulerAngles = new Vector3(0, 90f, 80f);
            rifle2.Root.transform.localScale = Vector3.one * 0.7f;
            return root;
        }
    }
}
