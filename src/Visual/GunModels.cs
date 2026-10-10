using UnityEngine;

namespace Arsenal.Visual
{
    /// <summary>
    /// The second wave of hand weapons. Same rules as WeaponModels: metres, +z forward, grip near the origin,
    /// the game's tool atlas and palette, plus small glowing parts for the energy weapons.
    /// </summary>
    internal static class GunModels
    {
        private static Transform Point(Transform parent, string name, Vector3 pos, Vector3 euler = default)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = pos;
            t.localEulerAngles = euler;
            return t;
        }

        private static Model Finish(string name, MeshKit body, MeshKit glow, Color glowColor, Vector3 muzzle, Vector3 sight, Vector3 rightHand, Vector3 leftHand)
        {
            var m = new Model { Root = new GameObject(name) };
            WeaponModels.MeshChild(m.Root.transform, "Body", body.Build("Arsenal." + name), Assets.ToolAtlas);
            if (glow != null)
                WeaponModels.MeshChild(m.Root.transform, "Glow", glow.Build("Arsenal." + name + ".Glow"), Assets.Glow(glowColor, 4f));
            m.Muzzle = Point(m.Root.transform, "Muzzle", muzzle);
            Point(m.Root.transform, "Sight", sight);
            m.RightHand = Point(m.Root.transform, "RightHandPos", rightHand, new Vector3(8.5f, 29.4f, 284.5f));
            m.LeftHandFirstPerson = Point(m.Root.transform, "LeftHandPos_firstPerson", leftHand, new Vector3(356.7f, 15.4f, 347f));
            m.LeftHandThirdPerson = Point(m.Root.transform, "LeftHandPos_thirdPerson", leftHand, new Vector3(8.6f, 24.3f, 10.2f));
            return m;
        }

        private static void PistolGrip(MeshKit k, Shade shade, float z = -0.03f)
        {
            k.At(new Vector3(0, -0.005f, z), new Vector3(-14f, 0, 0));
            k.Bevel(new Vector3(0, -0.05f, 0), new Vector3(0.032f, 0.1f, 0.045f), 0.006f, shade, Shade.Dark);
            k.Box(new Vector3(0, -0.1f, 0), new Vector3(0.034f, 0.008f, 0.047f), Shade.Black);
            k.At(Vector3.zero);
        }

        private static void TriggerGuard(MeshKit k, float z = 0.03f)
        {
            k.Box(new Vector3(0, -0.012f, z), new Vector3(0.012f, 0.022f, 0.006f), Shade.Black);
            k.Box(new Vector3(0, -0.026f, z + 0.02f), new Vector3(0.01f, 0.005f, 0.05f), Shade.Dark);
        }

        private static void Stock(MeshKit k, Shade shade, float zBack = -0.2f)
        {
            k.Bevel(new Vector3(0, 0f, zBack), new Vector3(0.04f, 0.08f, 0.18f), 0.01f, shade, Shade.Light);
            k.Box(new Vector3(0, -0.005f, zBack - 0.093f), new Vector3(0.044f, 0.095f, 0.012f), Shade.Black);
        }

        // ------------------------------------------------------------------ sidearms

        public static Model LaserPistol()
        {
            var k = new MeshKit();
            k.Bevel(new Vector3(0, 0.035f, 0.04f), new Vector3(0.036f, 0.046f, 0.2f), 0.012f, Shade.White, Shade.Light);
            k.Box(new Vector3(0, 0.06f, 0.02f), new Vector3(0.024f, 0.006f, 0.12f), Shade.Dark);
            for (int i = 0; i < 5; i++) k.Box(new Vector3(0, 0.064f, -0.02f + i * 0.018f), new Vector3(0.026f, 0.004f, 0.006f), Shade.Black);
            k.Cylinder(new Vector3(0, 0.035f, 0.15f), 0.014f, 0.02f, 8, Shade.Black, Shade.Dark);
            k.Box(new Vector3(0, 0.006f, 0.04f), new Vector3(0.03f, 0.016f, 0.15f), Shade.Dark);
            TriggerGuard(k);
            PistolGrip(k, Shade.HatchDark);
            var g = new MeshKit();
            g.Box(new Vector3(0.0185f, 0.035f, 0.05f), new Vector3(0.002f, 0.008f, 0.12f), Shade.White);
            g.Box(new Vector3(-0.0185f, 0.035f, 0.05f), new Vector3(0.002f, 0.008f, 0.12f), Shade.White);
            g.Cylinder(new Vector3(0, 0.035f, 0.161f), 0.008f, 0.004f, 8, Shade.White, Shade.White);
            return Finish("LaserPistolModel", k, g, new Color(0.3f, 0.9f, 1f), new Vector3(0, 0.035f, 0.165f), new Vector3(0, 0.066f, -0.04f), new Vector3(0.012f, -0.04f, -0.045f), new Vector3(-0.02f, -0.05f, -0.02f));
        }

        public static Model Gauss()
        {
            var k = new MeshKit();
            // frame and long barrel with magnetic rails
            k.Bevel(new Vector3(0, 0.03f, 0.0f), new Vector3(0.034f, 0.05f, 0.1f), 0.01f, Shade.HatchDark, Shade.Dark);
            k.Cylinder(new Vector3(0, 0.04f, 0.13f), 0.011f, 0.2f, 8, Shade.Black, Shade.Dark);
            k.Box(new Vector3(0, 0.057f, 0.13f), new Vector3(0.012f, 0.008f, 0.2f), Shade.Light);
            k.Box(new Vector3(0, 0.023f, 0.13f), new Vector3(0.012f, 0.008f, 0.18f), Shade.Light);
            for (int i = 0; i < 4; i++) k.Cylinder(new Vector3(0, 0.04f, 0.07f + i * 0.045f), 0.017f, 0.01f, 8, Shade.Yellow, Shade.Yellow);
            // drum
            k.Cylinder(new Vector3(0, 0.032f, 0.02f), 0.026f, 0.05f, 6, Shade.Grey, Shade.HatchMid);
            k.Box(new Vector3(0, 0.07f, -0.045f), new Vector3(0.012f, 0.02f, 0.015f), Shade.Black); // hammer
            k.Box(new Vector3(0, 0.068f, 0.21f), new Vector3(0.006f, 0.01f, 0.01f), Shade.Orange); // front sight
            TriggerGuard(k, 0.02f);
            PistolGrip(k, Shade.Orange, -0.05f);
            return Finish("GaussModel", k, null, Color.white, new Vector3(0, 0.04f, 0.235f), new Vector3(0, 0.07f, -0.04f), new Vector3(0.012f, -0.04f, -0.065f), new Vector3(-0.02f, -0.05f, -0.04f));
        }

        public static Model Smg()
        {
            var k = new MeshKit();
            k.Bevel(new Vector3(0, 0.025f, 0.03f), new Vector3(0.042f, 0.06f, 0.22f), 0.01f, Shade.HatchDark, Shade.Dark);
            k.Cylinder(new Vector3(0, 0.03f, 0.17f), 0.016f, 0.08f, 8, Shade.Black, Shade.Grey);
            for (int i = 0; i < 3; i++) k.Box(new Vector3(0, 0.03f, 0.14f + i * 0.025f), new Vector3(0.036f, 0.008f, 0.006f), Shade.Black);
            k.Box(new Vector3(0.022f, 0.035f, 0.04f), new Vector3(0.003f, 0.02f, 0.09f), Shade.Green);
            k.Box(new Vector3(0, 0.062f, 0.02f), new Vector3(0.02f, 0.012f, 0.05f), Shade.Black);
            // long straight magazine in front of the trigger
            k.Box(new Vector3(0, -0.06f, 0.07f), new Vector3(0.026f, 0.13f, 0.03f), Shade.Dark, Shade.Black);
            k.Box(new Vector3(0, -0.128f, 0.07f), new Vector3(0.028f, 0.008f, 0.032f), Shade.Green);
            TriggerGuard(k, 0.0f);
            PistolGrip(k, Shade.HatchDark, -0.05f);
            // wire stock
            k.Box(new Vector3(0.015f, 0.03f, -0.15f), new Vector3(0.005f, 0.005f, 0.16f), Shade.Black);
            k.Box(new Vector3(-0.015f, 0.03f, -0.15f), new Vector3(0.005f, 0.005f, 0.16f), Shade.Black);
            k.Box(new Vector3(0, 0.0f, -0.23f), new Vector3(0.036f, 0.07f, 0.008f), Shade.Black);
            return Finish("SmgModel", k, null, Color.white, new Vector3(0, 0.03f, 0.215f), new Vector3(0, 0.071f, 0.02f), new Vector3(0.012f, -0.04f, -0.065f), new Vector3(-0.025f, -0.06f, 0.07f));
        }

        // ------------------------------------------------------------------ rifles

        public static Model PlasmaShotgun()
        {
            var k = new MeshKit();
            k.Bevel(new Vector3(0, 0.025f, 0.04f), new Vector3(0.06f, 0.075f, 0.34f), 0.018f, Shade.HatchLight, Shade.Light);
            k.Box(new Vector3(0.031f, 0.03f, 0.08f), new Vector3(0.004f, 0.035f, 0.18f), Shade.Orange);
            // wide muzzle with three bores
            k.Bevel(new Vector3(0, 0.025f, 0.25f), new Vector3(0.075f, 0.06f, 0.08f), 0.015f, Shade.Dark, Shade.HatchDark);
            k.Cylinder(new Vector3(-0.02f, 0.025f, 0.292f), 0.011f, 0.006f, 8, Shade.Black, Shade.Black);
            k.Cylinder(new Vector3(0.02f, 0.025f, 0.292f), 0.011f, 0.006f, 8, Shade.Black, Shade.Black);
            k.Cylinder(new Vector3(0, 0.045f, 0.292f), 0.011f, 0.006f, 8, Shade.Black, Shade.Black);
            // pump
            k.Bevel(new Vector3(0, -0.025f, 0.16f), new Vector3(0.05f, 0.035f, 0.12f), 0.01f, Shade.Dark, Shade.Black);
            // plasma canister on top
            k.Box(new Vector3(0, 0.072f, 0.0f), new Vector3(0.03f, 0.012f, 0.14f), Shade.Dark);
            TriggerGuard(k, -0.02f);
            PistolGrip(k, Shade.HatchDark, -0.07f);
            Stock(k, Shade.HatchLight, -0.22f);
            var g = new MeshKit();
            g.At(Vector3.zero, new Vector3(0, 0, 0));
            g.Cylinder(new Vector3(0, 0.09f, 0.0f), 0.017f, 0.12f, 8, Shade.White, Shade.White);
            return Finish("PlasmaShotgunModel", k, g, new Color(1f, 0.55f, 0.15f), new Vector3(0, 0.03f, 0.3f), new Vector3(0, 0.11f, -0.02f), new Vector3(0.012f, -0.05f, -0.085f), new Vector3(-0.03f, -0.04f, 0.16f));
        }

        public static Model Railgun()
        {
            var k = new MeshKit();
            k.Bevel(new Vector3(0, 0.02f, 0.0f), new Vector3(0.064f, 0.08f, 0.34f), 0.016f, Shade.HatchMid, Shade.Grey);
            // two parallel rails with a gap, held by clamps
            k.Box(new Vector3(0, 0.055f, 0.42f), new Vector3(0.05f, 0.012f, 0.62f), Shade.Light);
            k.Box(new Vector3(0, 0.005f, 0.42f), new Vector3(0.05f, 0.012f, 0.62f), Shade.Light);
            for (int i = 0; i < 5; i++)
            {
                k.Box(new Vector3(0.028f, 0.03f, 0.2f + i * 0.12f), new Vector3(0.008f, 0.07f, 0.03f), Shade.Dark);
                k.Box(new Vector3(-0.028f, 0.03f, 0.2f + i * 0.12f), new Vector3(0.008f, 0.07f, 0.03f), Shade.Dark);
            }
            // capacitor block and scope
            k.Bevel(new Vector3(0, -0.05f, 0.05f), new Vector3(0.05f, 0.06f, 0.16f), 0.01f, Shade.Yellow, Shade.Dark);
            k.Cylinder(new Vector3(0, 0.1f, -0.02f), 0.022f, 0.22f, 8, Shade.Black, Shade.HatchDark);
            k.Cylinder(new Vector3(0, 0.1f, 0.1f), 0.028f, 0.04f, 8, Shade.Blue, Shade.Dark);
            k.Box(new Vector3(0, 0.07f, -0.02f), new Vector3(0.018f, 0.03f, 0.02f), Shade.Dark);
            PistolGrip(k, Shade.HatchDark, -0.08f);
            Stock(k, Shade.HatchMid, -0.27f);
            var g = new MeshKit();
            for (int i = 0; i < 5; i++) g.Box(new Vector3(0, 0.03f, 0.2f + i * 0.12f), new Vector3(0.03f, 0.03f, 0.012f), Shade.White);
            return Finish("RailgunModel", k, g, new Color(0.4f, 0.85f, 1f), new Vector3(0, 0.03f, 0.74f), new Vector3(0, 0.1f, -0.12f), new Vector3(0.012f, -0.05f, -0.1f), new Vector3(-0.03f, -0.05f, 0.08f));
        }

        // ------------------------------------------------------------------ heavy

        public static Model Minigun()
        {
            var k = new MeshKit();
            // housing and battery
            k.Bevel(new Vector3(0, 0.02f, -0.02f), new Vector3(0.11f, 0.12f, 0.24f), 0.025f, Shade.HatchDark, Shade.Dark);
            k.Bevel(new Vector3(0.0f, -0.07f, -0.06f), new Vector3(0.08f, 0.06f, 0.16f), 0.012f, Shade.Yellow, Shade.Dark);
            k.Box(new Vector3(0, 0.1f, -0.02f), new Vector3(0.02f, 0.05f, 0.02f), Shade.Black);
            k.Box(new Vector3(0, 0.125f, 0.04f), new Vector3(0.02f, 0.012f, 0.14f), Shade.Black); // carry handle
            k.Box(new Vector3(0, 0.1f, 0.1f), new Vector3(0.02f, 0.05f, 0.02f), Shade.Black);
            k.Cylinder(new Vector3(0, 0.02f, 0.13f), 0.05f, 0.06f, 10, Shade.Dark, Shade.Grey);
            PistolGrip(k, Shade.HatchDark, -0.13f);
            k.Box(new Vector3(-0.07f, 0.0f, 0.05f), new Vector3(0.02f, 0.03f, 0.08f), Shade.Black); // side handle
            var m = Finish("MinigunModel", k, null, Color.white, new Vector3(0, 0.02f, 0.52f), new Vector3(0, 0.13f, 0f), new Vector3(0.012f, -0.04f, -0.15f), new Vector3(-0.08f, -0.02f, 0.05f));
            // spinning barrel cluster
            var b = new MeshKit();
            for (int i = 0; i < 6; i++)
            {
                float a = i * Mathf.PI / 3f;
                b.Cylinder(new Vector3(Mathf.Cos(a) * 0.028f, Mathf.Sin(a) * 0.028f, 0.18f), 0.009f, 0.36f, 6, Shade.Black, Shade.Dark);
            }
            b.Cylinder(new Vector3(0, 0, 0.1f), 0.042f, 0.015f, 10, Shade.Grey, Shade.Grey);
            b.Cylinder(new Vector3(0, 0, 0.33f), 0.042f, 0.015f, 10, Shade.Grey, Shade.Grey);
            GameObject barrels = WeaponModels.MeshChild(m.Root.transform, "Barrels", b.Build("Arsenal.MinigunBarrels"), Assets.ToolAtlas);
            barrels.transform.localPosition = new Vector3(0, 0.02f, 0.16f);
            return m;
        }

        public static Model Flamethrower()
        {
            var k = new MeshKit();
            k.Bevel(new Vector3(0, 0.03f, 0.05f), new Vector3(0.05f, 0.06f, 0.3f), 0.014f, Shade.HatchDark, Shade.Dark);
            k.Cylinder(new Vector3(0, 0.035f, 0.32f), 0.02f, 0.26f, 8, Shade.Black, Shade.Grey);
            k.Cylinder(new Vector3(0, 0.035f, 0.45f), 0.03f, 0.03f, 8, Shade.Black, Shade.HatchDark);
            k.Box(new Vector3(0, 0.0f, 0.44f), new Vector3(0.012f, 0.02f, 0.03f), Shade.Dark); // pilot light holder
            // two fuel tanks under the body
            k.At(Vector3.zero);
            k.Cylinder(new Vector3(0.03f, -0.045f, 0.05f), 0.03f, 0.22f, 10, Shade.Orange, Shade.Orange);
            k.Cylinder(new Vector3(-0.03f, -0.045f, 0.05f), 0.03f, 0.22f, 10, Shade.Orange, Shade.Orange);
            k.Box(new Vector3(0, -0.045f, 0.0f), new Vector3(0.1f, 0.01f, 0.01f), Shade.Black);
            k.Box(new Vector3(0, -0.045f, 0.12f), new Vector3(0.1f, 0.01f, 0.01f), Shade.Black);
            k.Box(new Vector3(0, -0.01f, 0.2f), new Vector3(0.03f, 0.05f, 0.03f), Shade.Dark); // front grip
            PistolGrip(k, Shade.HatchDark, -0.08f);
            var g = new MeshKit();
            g.Cylinder(new Vector3(0, -0.005f, 0.462f), 0.006f, 0.01f, 6, Shade.White, Shade.White);
            return Finish("FlamethrowerModel", k, g, new Color(1f, 0.6f, 0.2f), new Vector3(0, 0.035f, 0.47f), new Vector3(0, 0.07f, 0.0f), new Vector3(0.012f, -0.05f, -0.1f), new Vector3(-0.025f, -0.04f, 0.2f));
        }

        // ------------------------------------------------------------------ explosives

        public static Model GrenadeLauncher()
        {
            var k = new MeshKit();
            k.Bevel(new Vector3(0, 0.03f, -0.02f), new Vector3(0.05f, 0.06f, 0.16f), 0.012f, Shade.HatchDark, Shade.Dark);
            // big revolving drum of six tubes
            k.Cylinder(new Vector3(0, 0.035f, 0.11f), 0.065f, 0.12f, 6, Shade.Dark, Shade.HatchMid);
            for (int i = 0; i < 6; i++)
            {
                float a = i * Mathf.PI / 3f + Mathf.PI / 6f;
                k.Cylinder(new Vector3(Mathf.Cos(a) * 0.04f, 0.035f + Mathf.Sin(a) * 0.04f, 0.172f), 0.016f, 0.006f, 8, Shade.Black, Shade.Black);
            }
            k.Box(new Vector3(0.0f, 0.105f, 0.11f), new Vector3(0.05f, 0.012f, 0.12f), Shade.Yellow);
            k.Cylinder(new Vector3(0, 0.035f, 0.26f), 0.03f, 0.18f, 8, Shade.Black, Shade.HatchDark);
            k.Box(new Vector3(0, -0.035f, 0.24f), new Vector3(0.03f, 0.05f, 0.06f), Shade.Dark); // front grip
            PistolGrip(k, Shade.HatchDark, -0.06f);
            Stock(k, Shade.HatchMid, -0.2f);
            return Finish("GrenadeLauncherModel", k, null, Color.white, new Vector3(0, 0.035f, 0.36f), new Vector3(0, 0.115f, 0.0f), new Vector3(0.012f, -0.05f, -0.085f), new Vector3(-0.03f, -0.05f, 0.24f));
        }

        public static Model RocketLauncher()
        {
            var k = new MeshKit();
            // shoulder tube
            k.Cylinder(new Vector3(0, 0.08f, 0.15f), 0.065f, 0.95f, 10, Shade.Black, Shade.HatchLight);
            k.Cylinder(new Vector3(0, 0.08f, 0.6f), 0.075f, 0.08f, 10, Shade.Black, Shade.Orange);
            k.Cylinder(new Vector3(0, 0.08f, -0.3f), 0.075f, 0.06f, 10, Shade.Black, Shade.Dark);
            k.Box(new Vector3(0, 0.08f, 0.1f), new Vector3(0.14f, 0.012f, 0.03f), Shade.Yellow);
            // sight on the left, grips under
            k.Box(new Vector3(-0.08f, 0.13f, 0.1f), new Vector3(0.03f, 0.04f, 0.08f), Shade.Dark);
            k.Box(new Vector3(-0.08f, 0.13f, 0.142f), new Vector3(0.02f, 0.02f, 0.002f), Shade.Blue);
            k.Box(new Vector3(0, -0.01f, 0.2f), new Vector3(0.03f, 0.06f, 0.04f), Shade.Dark);
            PistolGrip(k, Shade.HatchDark, -0.02f);
            var m = Finish("RocketLauncherModel", k, null, Color.white, new Vector3(0, 0.08f, 0.65f), new Vector3(-0.08f, 0.13f, 0.06f), new Vector3(0.012f, -0.04f, -0.045f), new Vector3(-0.025f, -0.05f, 0.2f));
            // rocket nose showing in the tube (hidden while reloading)
            var r = new MeshKit();
            r.Frustum(Vector3.zero, 0.05f, 0f, 0.1f, 8, Shade.Red);
            GameObject nose = WeaponModels.MeshChild(m.Root.transform, "Ammo", r.Build("Arsenal.RocketNose"), Assets.ToolAtlas);
            nose.transform.localPosition = new Vector3(0, 0.08f, 0.58f);
            return m;
        }

        // ------------------------------------------------------------------ energy

        public static Model EmpCarbine()
        {
            var k = new MeshKit();
            k.Bevel(new Vector3(0, 0.025f, 0.04f), new Vector3(0.055f, 0.07f, 0.32f), 0.016f, Shade.HatchLight, Shade.Light);
            k.Box(new Vector3(0.029f, 0.03f, 0.04f), new Vector3(0.004f, 0.03f, 0.16f), Shade.Blue);
            // emitter dish with fins
            k.Cylinder(new Vector3(0, 0.03f, 0.25f), 0.018f, 0.08f, 8, Shade.Dark, Shade.Grey);
            k.Cylinder(new Vector3(0, 0.03f, 0.3f), 0.05f, 0.02f, 12, Shade.Dark, Shade.Dark);
            for (int i = 0; i < 4; i++)
            {
                k.At(new Vector3(0, 0.03f, 0.27f), new Vector3(0, 0, i * 90f + 45f));
                k.Box(new Vector3(0, 0.045f, 0f), new Vector3(0.006f, 0.03f, 0.05f), Shade.Light);
            }
            k.At(Vector3.zero);
            k.Box(new Vector3(0, 0.075f, 0.0f), new Vector3(0.026f, 0.025f, 0.05f), Shade.Dark);
            TriggerGuard(k, -0.02f);
            PistolGrip(k, Shade.HatchDark, -0.07f);
            Stock(k, Shade.HatchLight, -0.21f);
            var g = new MeshKit();
            g.Cylinder(new Vector3(0, 0.03f, 0.312f), 0.04f, 0.004f, 12, Shade.White, Shade.White);
            g.Box(new Vector3(0.0315f, 0.03f, 0.04f), new Vector3(0.002f, 0.01f, 0.12f), Shade.White);
            return Finish("EmpCarbineModel", k, g, new Color(0.35f, 0.6f, 1f), new Vector3(0, 0.03f, 0.32f), new Vector3(0, 0.088f, -0.01f), new Vector3(0.012f, -0.05f, -0.085f), new Vector3(-0.03f, -0.04f, 0.16f));
        }

        public static Model Tesla()
        {
            var k = new MeshKit();
            k.Bevel(new Vector3(0, 0.025f, 0.02f), new Vector3(0.06f, 0.07f, 0.26f), 0.016f, Shade.HatchDark, Shade.Dark);
            // copper coil
            for (int i = 0; i < 6; i++) k.Cylinder(new Vector3(0, 0.03f, 0.08f + i * 0.022f), 0.034f, 0.012f, 10, Shade.Orange, Shade.Orange);
            k.Cylinder(new Vector3(0, 0.03f, 0.13f), 0.026f, 0.14f, 10, Shade.Dark, Shade.Dark);
            // two prongs
            k.Box(new Vector3(0.025f, 0.03f, 0.25f), new Vector3(0.01f, 0.01f, 0.1f), Shade.Grey);
            k.Box(new Vector3(-0.025f, 0.03f, 0.25f), new Vector3(0.01f, 0.01f, 0.1f), Shade.Grey);
            k.Box(new Vector3(0, 0.075f, -0.02f), new Vector3(0.03f, 0.02f, 0.06f), Shade.Yellow);
            PistolGrip(k, Shade.HatchDark, -0.07f);
            Stock(k, Shade.HatchMid, -0.19f);
            var g = new MeshKit();
            g.Cylinder(new Vector3(0.025f, 0.03f, 0.302f), 0.012f, 0.01f, 8, Shade.White, Shade.White);
            g.Cylinder(new Vector3(-0.025f, 0.03f, 0.302f), 0.012f, 0.01f, 8, Shade.White, Shade.White);
            return Finish("TeslaModel", k, g, new Color(0.55f, 0.8f, 1f), new Vector3(0, 0.03f, 0.31f), new Vector3(0, 0.085f, -0.02f), new Vector3(0.012f, -0.05f, -0.085f), new Vector3(-0.03f, -0.04f, 0.14f));
        }

        public static Model Disintegrator()
        {
            var k = new MeshKit();
            // asymmetric alien shell
            k.At(Vector3.zero, new Vector3(0, 0, 8f));
            k.Bevel(new Vector3(0, 0.03f, 0.05f), new Vector3(0.06f, 0.07f, 0.34f), 0.025f, Shade.Black, Shade.HatchDark);
            k.At(Vector3.zero);
            k.Frustum(new Vector3(0, 0.03f, 0.22f), 0.04f, 0.012f, 0.14f, 6, Shade.Black);
            for (int i = 0; i < 3; i++)
            {
                k.At(new Vector3(0, 0.03f, 0.0f + i * 0.08f), new Vector3(0, 0, i * 120f));
                k.Box(new Vector3(0, 0.055f, 0f), new Vector3(0.008f, 0.04f, 0.06f), Shade.Dark);
            }
            k.At(Vector3.zero);
            PistolGrip(k, Shade.Black, -0.08f);
            var g = new MeshKit();
            g.Cylinder(new Vector3(0, 0.03f, 0.06f), 0.02f, 0.2f, 6, Shade.White, Shade.White);
            g.Cylinder(new Vector3(0, 0.03f, 0.36f), 0.008f, 0.01f, 6, Shade.White, Shade.White);
            for (int i = 0; i < 3; i++)
            {
                g.At(new Vector3(0, 0.03f, 0.0f + i * 0.08f), new Vector3(0, 0, i * 120f));
                g.Box(new Vector3(0, 0.077f, 0f), new Vector3(0.004f, 0.006f, 0.05f), Shade.White);
            }
            g.At(Vector3.zero);
            return Finish("DisintegratorModel", k, g, new Color(0.8f, 0.35f, 1f), new Vector3(0, 0.03f, 0.37f), new Vector3(0, 0.09f, -0.04f), new Vector3(0.012f, -0.05f, -0.1f), new Vector3(-0.03f, -0.04f, 0.12f));
        }
    }
}
