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
            // bolted base plate + pivot ring
            k.Bevel(new Vector3(0, 0.03f, 0), new Vector3(w, 0.06f, d), 0.02f, Shade.HatchDark, Shade.Dark);
            for (int x = -1; x <= 1; x += 2)
                for (int z = -1; z <= 1; z += 2)
                    k.Cylinder(new Vector3(x * (w / 2 - 0.05f), 0.065f, z * (d / 2 - 0.05f)), 0.018f, 0.012f, 6, Shade.Grey, Shade.Grey);
            k.At(Vector3.zero, new Vector3(-90f, 0, 0));
            k.Cylinder(new Vector3(0, 0, 0.09f), 0.17f, 0.06f, 10, Shade.Light, Shade.HatchMid);
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

        /// <summary>Fire-control console bolted to the right of the advanced pilot seat, with three coloured triggers.</summary>
        public static GameObject GunnerConsole()
        {
            var k = new MeshKit();
            k.Bevel(new Vector3(0.43f, 0.35f, 0.0f), new Vector3(0.14f, 0.7f, 0.5f), 0.03f, Shade.HatchDark, Shade.Dark);
            k.Box(new Vector3(0.43f, 0.71f, 0.0f), new Vector3(0.16f, 0.03f, 0.52f), Shade.Light);
            k.Box(new Vector3(0.43f, 0.735f, -0.15f), new Vector3(0.05f, 0.02f, 0.05f), Shade.Red);
            k.Box(new Vector3(0.43f, 0.735f, 0.0f), new Vector3(0.05f, 0.02f, 0.05f), Shade.Yellow);
            k.Box(new Vector3(0.43f, 0.735f, 0.15f), new Vector3(0.05f, 0.02f, 0.05f), Shade.Blue);
            k.Box(new Vector3(0.501f, 0.45f, 0.0f), new Vector3(0.004f, 0.08f, 0.4f), Shade.Orange);
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
