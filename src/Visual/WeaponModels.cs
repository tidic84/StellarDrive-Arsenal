using UnityEngine;
using static Arsenal.Visual.GunParts;

namespace Arsenal.Visual
{
    /// <summary>A built model: its root, the muzzle and where the hands go.</summary>
    internal sealed class Model
    {
        public GameObject Root;
        public Transform Muzzle;
        public Transform RightHand;
        public Transform LeftHandFirstPerson;
        public Transform LeftHandThirdPerson;
        public Transform Display; // little ammo screen, may be null
    }

    /// <summary>
    /// Guns in the game's style: chunky industrial shapes with chamfered silhouettes, dark hatched bodies,
    /// black details and light panels, all on the mining laser's hand-drawn atlas. Units are metres, +z forward.
    /// </summary>
    internal static class WeaponModels
    {
        internal static Transform Point(Transform parent, string name, Vector3 pos, Vector3 euler)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = pos;
            t.localEulerAngles = euler;
            return t;
        }

        public static GameObject MeshChild(Transform parent, string name, Mesh mesh, Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        /// <summary>
        /// A left hand well ahead of the grip carries the gun from below: <paramref name="left"/> is then where the palm
        /// rests, and the IK target (the wrist) goes a little behind it and towards the centre line.
        /// </summary>
        internal static void Hands(Model m, Vector3 right, Vector3 left, Vector3? leftEuler = null)
        {
            if (leftEuler == null && left.z > right.z + 0.1f)
            {
                leftEuler = UnderSupport;
                left += new Vector3(0.02f, 0f, -0.04f);
            }
            m.RightHand = Point(m.Root.transform, "RightHandPos", right, new Vector3(8.5f, 29.4f, 284.5f));
            m.LeftHandFirstPerson = Point(m.Root.transform, "LeftHandPos_firstPerson", left, leftEuler ?? new Vector3(356.7f, 15.4f, 347f));
            m.LeftHandThirdPerson = Point(m.Root.transform, "LeftHandPos_thirdPerson", left, leftEuler ?? new Vector3(8.6f, 24.3f, 10.2f));
        }

        /// <summary>The turn the game's hand IK keeps between a left-hand target and the hand bone (measured in game, the same for every tool).</summary>
        private static readonly Quaternion LeftBoneOffset = Quaternion.Euler(85f, 103f, 106f);

        /// <summary>
        /// Left-hand target angles (weapon frame) for a hand bone whose y axis (the fingers) points along <paramref name="fingers"/>
        /// and whose z axis comes out of the palm along <paramref name="palm"/>. The fingers stay straight: the IK does not close them.
        /// </summary>
        internal static Vector3 LeftHand(Vector3 fingers, Vector3 palm) => (Quaternion.LookRotation(palm, fingers) * Quaternion.Inverse(LeftBoneOffset)).eulerAngles;

        internal static readonly Vector3 PistolSupport = new Vector3(30.5f, 0f, 90f),   // palm on the left grip panel, fingers forward-down round the front
            RevolverSupport = new Vector3(39.7f, 0f, 90f),                               // same, steeper for a raked grip
            UnderSupport = LeftHand(new Vector3(0.4f, 0.1f, 0.9f), new Vector3(0f, 1f, -0.1f)); // palm up under the handguard, fingers forward along its left side

        // ------------------------------------------------------------------ sidearm

        public static Model Pistol()
        {
            var k = new MeshKit();
            // slide: angled nose, flat top, raised rear
            Side(k, new[]
            {
                P(-0.062f, 0.017f), P(0.128f, 0.017f), P(0.134f, 0.024f), P(0.134f, 0.046f), P(0.122f, 0.055f),
                P(-0.05f, 0.055f), P(-0.062f, 0.05f),
            }, 0.032f, Shade.HatchLight, Shade.Light, 0.004f);
            // rear and front serrations, ejection port
            for (int i = 0; i < 5; i++)
                Plates(k, Rect(-0.052f + i * 0.007f, 0.022f, -0.049f + i * 0.007f, 0.05f), 0.016f, Shade.Black, 0.0012f);
            for (int i = 0; i < 3; i++)
                Plates(k, Rect(0.1f + i * 0.007f, 0.026f, 0.103f + i * 0.007f, 0.048f), 0.016f, Shade.Black, 0.0012f);
            k.ChamferBox(new Vector3(0.006f, 0.05f, 0.03f), new Vector3(0.02f, 0.012f, 0.05f), 0.002f, Shade.Dark, Shade.Black);
            k.Box(new Vector3(0.008f, 0.0565f, 0.03f), new Vector3(0.012f, 0.002f, 0.04f), Shade.Black);
            // barrel showing at the nose
            k.Tube(new Vector3(0, 0.036f, 0.135f), 0.0085f, 0.0045f, 0.006f, 14, Shade.Dark, Shade.Black);
            // frame with dust cover and accessory rail
            Side(k, new[] { P(-0.045f, 0.002f), P(0.124f, 0.002f), P(0.124f, 0.017f), P(-0.05f, 0.017f) }, 0.029f, Shade.Dark, Shade.Black, 0.003f);
            for (int i = 0; i < 3; i++) k.Box(new Vector3(0, -0.0005f, 0.09f + i * 0.011f), new Vector3(0.024f, 0.004f, 0.005f), Shade.Black);
            TriggerGroup(k, 0.03f, 0.003f);
            Grip(k, -0.008f, 0.002f, Shade.HatchDark, 16f, 0.098f);
            // hammer, slide stop, sights
            Side(k, new[] { P(-0.066f, 0.044f), P(-0.06f, 0.044f), P(-0.06f, 0.056f), P(-0.068f, 0.059f) }, 0.01f, Shade.Black, Shade.Black, 0.001f);
            k.ChamferBox(new Vector3(-0.0165f, 0.019f, 0.03f), new Vector3(0.003f, 0.006f, 0.022f), 0.001f, Shade.Black);
            IronSights(k, 0.055f, 0.118f, -0.046f);
            Screws(k, 0.0145f, P(0.0f, 0.009f), P(-0.035f, -0.02f));

            var m = new Model { Root = new GameObject("PistolModel") };
            MeshChild(m.Root.transform, "Body", k.Build("Arsenal.Pistol"), Assets.ToolAtlas);
            m.Muzzle = Point(m.Root.transform, "Muzzle", new Vector3(0, 0.036f, 0.14f), Vector3.zero);
            Point(m.Root.transform, "Sight", new Vector3(0, 0.064f, -0.046f), Vector3.zero);
            Hands(m, new Vector3(0.012f, -0.04f, -0.045f), new Vector3(-0.02f, -0.05f, -0.02f));
            m.Display = Point(m.Root.transform, "Display", new Vector3(0.0165f, 0.01f, 0.07f), new Vector3(0, -90f, 0));
            return m;
        }

        // ------------------------------------------------------------------ AR-47

        public static Model Rifle()
        {
            var k = new MeshKit();
            // upper receiver with sloped front
            Side(k, new[]
            {
                P(-0.085f, 0.014f), P(0.2f, 0.014f), P(0.2f, 0.05f), P(0.17f, 0.058f), P(-0.07f, 0.058f), P(-0.085f, 0.05f),
            }, 0.046f, Shade.HatchLight, Shade.Light, 0.005f);
            // ejection port + forward assist + yellow side panel
            Plates(k, RectC(0.03f, 0.026f, 0.1f, 0.048f, 0.003f), 0.023f, Shade.Yellow, 0.0015f);
            Plates(k, RectC(0.045f, 0.031f, 0.085f, 0.043f, 0.002f), 0.0245f, Shade.Black, 0.0012f, false);
            k.Lathe(new Vector2(0.026f, 0.05f), new[] { P(0.006f, -0.03f), P(0.006f, -0.012f), P(0.004f, -0.01f) }, 10, Shade.Dark, Shade.Dark, Shade.Black);
            // lower receiver and magazine well
            Side(k, new[]
            {
                P(-0.075f, -0.016f), P(0.03f, -0.016f), P(0.05f, -0.03f), P(0.125f, -0.03f), P(0.14f, -0.012f), P(0.14f, 0.014f), P(-0.075f, 0.014f),
            }, 0.04f, Shade.HatchDark, Shade.Dark, 0.004f);
            Screws(k, 0.02f, P(-0.05f, 0.0f), P(0.11f, -0.02f));
            // handguard: chamfered square tube with vents, rail on top
            k.ChamferBox(new Vector3(0, 0.032f, 0.31f), new Vector3(0.052f, 0.052f, 0.22f), 0.012f, Shade.HatchDark, Shade.Dark);
            for (int i = 0; i < 5; i++)
                Plates(k, RectC(0.225f + i * 0.04f, 0.02f, 0.25f + i * 0.04f, 0.044f, 0.004f), 0.026f, Shade.Black, 0.0012f);
            k.ChamferBox(new Vector3(0, 0.032f, 0.205f), new Vector3(0.058f, 0.058f, 0.012f), 0.006f, Shade.Grey, Shade.Black);
            Rail(k, 0.058f, -0.07f, 0.41f);
            // barrel, gas block, flash hider
            Barrel(k, 0.03f, 0.42f, 0.5f, 0.0095f);
            k.ChamferBox(new Vector3(0, 0.038f, 0.44f), new Vector3(0.022f, 0.03f, 0.02f), 0.003f, Shade.Black, Shade.Black);
            FlashHider(k, 0.03f, 0.545f, 0.014f, 0.045f);
            // banana magazine, grip, guard
            CurvedMag(k, 0.122f, -0.028f, 0.115f, 0.05f, 0.03f, 0.04f, Shade.Dark, Shade.Black);
            TriggerGroup(k, 0.01f, -0.016f, 0.06f);
            Grip(k, -0.02f, -0.016f, Shade.HatchDark, 18f, 0.09f, 0.03f);
            // buffer tube and adjustable stock with cheek rest
            Barrel(k, 0.03f, -0.2f, -0.085f, 0.013f, Shade.Dark);
            Side(k, new[]
            {
                P(-0.3f, -0.05f), P(-0.27f, -0.05f), P(-0.17f, 0.005f), P(-0.15f, 0.005f), P(-0.15f, 0.052f), P(-0.3f, 0.06f),
            }, 0.042f, Shade.HatchLight, Shade.Light, 0.005f);
            Side(k, new[] { P(-0.305f, -0.056f), P(-0.293f, -0.056f), P(-0.293f, 0.066f), P(-0.305f, 0.066f) }, 0.046f, Shade.Black, Shade.Black, 0.003f);
            Plates(k, RectC(-0.26f, 0.015f, -0.17f, 0.045f, 0.004f), 0.021f, Shade.Dark, 0.0012f);
            ReflexSight(k, 0.02f, 0.066f);

            var m = new Model { Root = new GameObject("RifleModel") };
            MeshChild(m.Root.transform, "Body", k.Build("Arsenal.Rifle"), Assets.ToolAtlas);
            m.Muzzle = Point(m.Root.transform, "Muzzle", new Vector3(0, 0.03f, 0.55f), Vector3.zero);
            Point(m.Root.transform, "Sight", new Vector3(0, 0.066f + ReflexDot, 0.026f), Vector3.zero);
            Hands(m, new Vector3(0.012f, -0.05f, -0.06f), new Vector3(-0.03f, -0.012f, 0.21f));
            m.Display = Point(m.Root.transform, "Display", new Vector3(0.0255f, 0.037f, 0.115f), new Vector3(0, -90f, 0));
            return m;
        }

        // ------------------------------------------------------------------ anti-materiel rifle

        public static Model Marksman()
        {
            var k = new MeshKit();
            // massive receiver
            Side(k, new[]
            {
                P(-0.12f, -0.012f), P(0.2f, -0.012f), P(0.22f, 0.0f), P(0.22f, 0.05f), P(0.2f, 0.062f), P(-0.1f, 0.062f), P(-0.12f, 0.05f),
            }, 0.058f, Shade.HatchMid, Shade.Grey, 0.006f);
            Plates(k, RectC(0.0f, 0.012f, 0.16f, 0.05f, 0.005f), 0.029f, Shade.Orange, 0.0015f);
            Plates(k, RectC(0.02f, 0.02f, 0.08f, 0.042f, 0.003f), 0.0305f, Shade.Black, 0.0012f, false);
            // bolt handle
            With(k, new Vector3(0.03f, 0.04f, -0.06f), new Vector3(0, 90f, 0), () => k.Lathe(Vector2.zero, new[] { P(0.004f, 0f), P(0.004f, 0.025f), P(0.009f, 0.028f), P(0.009f, 0.038f), P(0.0f, 0.042f) }, 10, Shade.Black, Shade.Black));
            Screws(k, 0.029f, P(-0.09f, 0.0f), P(0.19f, 0.0f), P(0.19f, 0.048f));
            // fluted heavy barrel with a long brake
            k.Lathe(new Vector2(0, 0.03f), new[] { P(0.02f, 0.22f), P(0.02f, 0.26f), P(0.016f, 0.28f), P(0.016f, 0.72f) }, 16, Shade.HatchDark, null, Shade.Black);
            for (int i = 0; i < 6; i++)
                With(k, new Vector3(0, 0.03f, 0.5f), new Vector3(0, 0, i * 60f), () => k.Box(new Vector3(0, 0.0158f, 0f), new Vector3(0.004f, 0.002f, 0.36f), Shade.Black));
            k.ChamferBox(new Vector3(0, 0.03f, 0.765f), new Vector3(0.056f, 0.036f, 0.09f), 0.008f, Shade.HatchDark, Shade.Dark);
            for (int i = 0; i < 3; i++) k.Box(new Vector3(0, 0.03f, 0.74f + i * 0.026f), new Vector3(0.058f, 0.022f, 0.012f), Shade.Black);
            k.Tube(new Vector3(0, 0.03f, 0.811f), 0.012f, 0.009f, 0.003f, 12, Shade.Dark, Shade.Black);
            // scope on rings
            Rail(k, 0.062f, -0.1f, 0.2f);
            Scope(k, 0.108f, -0.13f, 0.17f, 0.018f);
            ScopeRings(k, 0.068f, 0.108f, 0.018f, -0.03f, 0.1f);
            // box magazine, grip, guard
            BoxMag(k, 0.13f, -0.012f, 0.07f, 0.075f, 0.042f, Shade.Dark);
            TriggerGroup(k, -0.02f, -0.012f, 0.06f);
            Grip(k, -0.05f, -0.012f, Shade.HatchDark, 16f, 0.095f, 0.032f);
            // skeleton stock with cheek riser and padded butt
            Side(k, new[] { P(-0.38f, 0.035f), P(-0.12f, 0.035f), P(-0.12f, 0.06f), P(-0.37f, 0.064f) }, 0.044f, Shade.HatchLight, Shade.Light, 0.005f);
            Side(k, new[] { P(-0.37f, -0.065f), P(-0.31f, -0.065f), P(-0.15f, -0.012f), P(-0.12f, -0.012f), P(-0.12f, 0.004f), P(-0.16f, 0.004f), P(-0.32f, -0.045f), P(-0.37f, -0.045f) }, 0.04f, Shade.HatchLight, Shade.Light, 0.004f);
            Side(k, new[] { P(-0.37f, -0.07f), P(-0.345f, -0.07f), P(-0.345f, 0.066f), P(-0.37f, 0.07f) }, 0.044f, Shade.HatchLight, Shade.Light, 0.004f);
            Side(k, new[] { P(-0.39f, -0.075f), P(-0.37f, -0.075f), P(-0.37f, 0.072f), P(-0.39f, 0.072f) }, 0.05f, Shade.Black, Shade.Black, 0.005f);
            k.ChamferBox(new Vector3(0, 0.074f, -0.27f), new Vector3(0.036f, 0.022f, 0.1f), 0.006f, Shade.Dark, Shade.Black);
            // folded bipod under the barrel
            With(k, new Vector3(0, 0.005f, 0.32f), new Vector3(-4f, 0, 0), () =>
            {
                k.ChamferBox(new Vector3(0, 0.004f, 0f), new Vector3(0.03f, 0.014f, 0.03f), 0.003f, Shade.Dark, Shade.Black);
                foreach (float x in new[] { -0.011f, 0.011f })
                {
                    k.Lathe(new Vector2(x, -0.004f), new[] { P(0.004f, 0.0f), P(0.004f, 0.17f), P(0.006f, 0.175f), P(0.006f, 0.19f) }, 8, Shade.Black, Shade.Black, Shade.Black);
                }
            });

            var m = new Model { Root = new GameObject("MarksmanModel") };
            MeshChild(m.Root.transform, "Body", k.Build("Arsenal.Marksman"), Assets.ToolAtlas);
            m.Muzzle = Point(m.Root.transform, "Muzzle", new Vector3(0, 0.03f, 0.82f), Vector3.zero);
            Point(m.Root.transform, "Sight", new Vector3(0, 0.108f, -0.13f), Vector3.zero);
            Hands(m, new Vector3(0.012f, -0.05f, -0.08f), new Vector3(-0.03f, -0.02f, 0.2f));
            m.Display = Point(m.Root.transform, "Display", new Vector3(0.0315f, 0.035f, 0.13f), new Vector3(0, -90f, 0));
            return m;
        }

        // ------------------------------------------------------------------ explosives

        /// <summary>Hand grenade: segmented body, yellow band, fuse head, spoon and ring. Pivot at its centre.</summary>
        public static Mesh GrenadeMesh()
        {
            var k = new MeshKit();
            With(k, Vector3.zero, new Vector3(-90f, 0, 0), () =>
            {
                // body of revolution along +z (= up)
                k.Lathe(Vector2.zero, new[]
                {
                    P(0.0f, -0.036f), P(0.018f, -0.035f), P(0.029f, -0.026f), P(0.033f, -0.012f), P(0.033f, 0.012f), P(0.029f, 0.026f), P(0.016f, 0.034f), P(0.012f, 0.036f),
                }, 16, Shade.HatchDark, null, Shade.Dark);
                k.Lathe(Vector2.zero, new[] { P(0.0338f, -0.006f), P(0.0338f, 0.006f) }, 16, Shade.Yellow, Shade.Yellow, Shade.Yellow);
                foreach (float z in new[] { -0.02f, 0.017f })
                    k.Lathe(Vector2.zero, new[] { P(0.032f, z - 0.0015f), P(0.0335f, z), P(0.032f, z + 0.0015f) }, 16, Shade.Black);
                // fuse head
                k.Lathe(Vector2.zero, new[] { P(0.011f, 0.035f), P(0.011f, 0.05f), P(0.008f, 0.054f), P(0.0f, 0.055f) }, 12, Shade.Grey, Shade.Grey);
            });
            // spoon down the side, pin and ring
            Side(k, new[] { P(-0.006f, 0.052f), P(0.006f, 0.052f), P(0.006f, 0.046f), P(0.036f, 0.03f), P(0.036f, -0.01f), P(0.031f, -0.01f), P(0.031f, 0.026f), P(-0.006f, 0.046f) }, 0.012f, Shade.Light, Shade.Grey, 0.001f, 0f);
            k.Tube(new Vector3(-0.024f, 0.048f, 0f), 0.009f, 0.0065f, 0.002f, 12, Shade.Grey, Shade.Grey);
            k.Box(new Vector3(-0.012f, 0.048f, 0f), new Vector3(0.016f, 0.002f, 0.002f), Shade.Grey);
            return k.Build("Arsenal.Grenade");
        }

        /// <summary>Breaching charge: a block with a strap, a timer and wires. Pivot on its back face (sticks to walls).</summary>
        public static Mesh ChargeMesh()
        {
            var k = new MeshKit();
            k.ChamferBox(new Vector3(0, 0, 0.022f), new Vector3(0.2f, 0.12f, 0.044f), 0.008f, Shade.HatchLight, Shade.Light);
            k.ChamferBox(new Vector3(0, 0, 0.002f), new Vector3(0.206f, 0.126f, 0.006f), 0.003f, Shade.Dark, Shade.Black); // adhesive back plate
            k.ChamferBox(new Vector3(0.062f, 0, 0.024f), new Vector3(0.026f, 0.126f, 0.05f), 0.004f, Shade.Orange, Shade.Orange); // strap
            k.ChamferBox(new Vector3(-0.062f, 0, 0.024f), new Vector3(0.026f, 0.126f, 0.05f), 0.004f, Shade.Orange, Shade.Orange);
            // timer box with a screen and keypad
            k.ChamferBox(new Vector3(0, 0.0f, 0.052f), new Vector3(0.07f, 0.06f, 0.016f), 0.004f, Shade.Dark, Shade.Black);
            k.Box(new Vector3(0, 0.012f, 0.0605f), new Vector3(0.05f, 0.018f, 0.002f), Shade.Black);
            for (int x = 0; x < 3; x++)
                for (int y = 0; y < 2; y++)
                    k.Box(new Vector3(-0.016f + x * 0.016f, -0.012f - y * 0.012f, 0.0605f), new Vector3(0.01f, 0.007f, 0.003f), Shade.Grey);
            k.Box(new Vector3(0.028f, 0.022f, 0.062f), new Vector3(0.006f, 0.006f, 0.004f), Shade.Red);
            // wires
            k.Lathe(new Vector2(-0.04f, 0.03f), new[] { P(0.002f, 0.025f), P(0.002f, 0.05f) }, 6, Shade.Blue, Shade.Blue, Shade.Blue);
            With(k, new Vector3(-0.035f, 0.032f, 0.05f), new Vector3(0, 90f, 0), () => k.Lathe(Vector2.zero, new[] { P(0.002f, 0f), P(0.002f, 0.06f) }, 6, Shade.Yellow, Shade.Yellow, Shade.Yellow));
            With(k, new Vector3(-0.035f, -0.03f, 0.05f), new Vector3(0, 90f, 0), () => k.Lathe(Vector2.zero, new[] { P(0.002f, 0f), P(0.002f, 0.06f) }, 6, Shade.Red, Shade.Red, Shade.Red));
            return k.Build("Arsenal.Charge");
        }

        public static Model Grenade()
        {
            var m = new Model { Root = new GameObject("GrenadeModel") };
            GameObject body = MeshChild(m.Root.transform, "Body", GrenadeMesh(), Assets.ToolAtlas);
            body.transform.localPosition = new Vector3(0, 0.02f, 0.03f);
            m.Muzzle = Point(m.Root.transform, "Muzzle", new Vector3(0, 0.02f, 0.06f), Vector3.zero);
            Hands(m, new Vector3(0.02f, -0.01f, 0.0f), new Vector3(-0.15f, -0.12f, -0.05f));
            return m;
        }

        public static Model Charge()
        {
            var m = new Model { Root = new GameObject("ChargeModel") };
            GameObject body = MeshChild(m.Root.transform, "Body", ChargeMesh(), Assets.ToolAtlas);
            body.transform.localPosition = new Vector3(0, 0.03f, 0.04f);
            body.transform.localEulerAngles = new Vector3(-70f, 0, 0);
            m.Muzzle = Point(m.Root.transform, "Muzzle", new Vector3(0, 0.03f, 0.1f), Vector3.zero);
            Hands(m, new Vector3(0.05f, -0.01f, 0.0f), new Vector3(-0.07f, -0.01f, 0.03f));
            return m;
        }
    }
}
