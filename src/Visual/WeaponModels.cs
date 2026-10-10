using UnityEngine;

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
    /// Low-poly guns in the game's style: chunky industrial shapes, dark hatched bodies, black details,
    /// light panels, all on the mining laser's hand-drawn atlas. Units are metres, +z is forward.
    /// </summary>
    internal static class WeaponModels
    {
        private static Transform Point(Transform parent, string name, Vector3 pos, Vector3 euler)
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

        public static Model Pistol()
        {
            var k = new MeshKit();
            // slide
            k.Bevel(new Vector3(0, 0.035f, 0.035f), new Vector3(0.034f, 0.038f, 0.19f), 0.008f, Shade.HatchLight, Shade.Light);
            k.Box(new Vector3(0, 0.056f, 0.035f), new Vector3(0.02f, 0.004f, 0.17f), Shade.Black);
            for (int i = 0; i < 4; i++)
                k.Box(new Vector3(0, 0.035f, -0.04f + i * 0.012f), new Vector3(0.036f, 0.026f, 0.004f), Shade.Black);
            // frame + trigger guard
            k.Box(new Vector3(0, 0.008f, 0.04f), new Vector3(0.03f, 0.018f, 0.16f), Shade.Dark);
            k.Box(new Vector3(0, -0.012f, 0.03f), new Vector3(0.012f, 0.022f, 0.006f), Shade.Black);
            k.Box(new Vector3(0, -0.026f, 0.05f), new Vector3(0.01f, 0.005f, 0.05f), Shade.Dark);
            k.Box(new Vector3(0, -0.012f, 0.073f), new Vector3(0.01f, 0.03f, 0.005f), Shade.Dark);
            // barrel
            k.Cylinder(new Vector3(0, 0.035f, 0.135f), 0.009f, 0.02f, 8, Shade.Black, Shade.Dark);
            // sights
            k.Box(new Vector3(0, 0.058f, 0.12f), new Vector3(0.005f, 0.008f, 0.008f), Shade.Yellow);
            k.Box(new Vector3(0, 0.058f, -0.05f), new Vector3(0.02f, 0.008f, 0.008f), Shade.Black);
            // grip, raked back
            k.At(new Vector3(0, -0.005f, -0.03f), new Vector3(-14f, 0, 0));
            k.Bevel(new Vector3(0, -0.05f, 0), new Vector3(0.032f, 0.1f, 0.045f), 0.006f, Shade.HatchDark, Shade.Dark);
            k.Box(new Vector3(0, -0.1f, 0), new Vector3(0.034f, 0.008f, 0.047f), Shade.Black);
            k.At(Vector3.zero);

            var m = new Model { Root = new GameObject("PistolModel") };
            MeshChild(m.Root.transform, "Body", k.Build("Arsenal.Pistol"), Assets.ToolAtlas);
            m.Muzzle = Point(m.Root.transform, "Muzzle", new Vector3(0, 0.035f, 0.15f), Vector3.zero);
            Point(m.Root.transform, "Sight", new Vector3(0, 0.063f, -0.05f), Vector3.zero);
            m.RightHand = Point(m.Root.transform, "RightHandPos", new Vector3(0.012f, -0.04f, -0.045f), new Vector3(8.5f, 29.4f, 284.5f));
            m.LeftHandFirstPerson = Point(m.Root.transform, "LeftHandPos_firstPerson", new Vector3(-0.02f, -0.05f, -0.02f), new Vector3(356.7f, 15.4f, 347f));
            m.LeftHandThirdPerson = Point(m.Root.transform, "LeftHandPos_thirdPerson", new Vector3(-0.02f, -0.05f, -0.02f), new Vector3(8.6f, 24.3f, 10.2f));
            m.Display = Point(m.Root.transform, "Display", new Vector3(0.0175f, 0.012f, 0.04f), new Vector3(0, -90f, 0));
            return m;
        }

        public static Model Rifle()
        {
            var k = new MeshKit();
            // receiver
            k.Bevel(new Vector3(0, 0.02f, 0.03f), new Vector3(0.05f, 0.07f, 0.3f), 0.012f, Shade.HatchLight, Shade.Light);
            k.Box(new Vector3(0, 0.058f, 0.03f), new Vector3(0.022f, 0.008f, 0.26f), Shade.Black); // top rail
            for (int i = 0; i < 6; i++)
                k.Box(new Vector3(0, 0.064f, -0.07f + i * 0.04f), new Vector3(0.026f, 0.005f, 0.012f), Shade.Dark);
            // light side panel with ejection port
            k.Box(new Vector3(0.026f, 0.03f, 0.06f), new Vector3(0.004f, 0.03f, 0.1f), Shade.Yellow);
            k.Box(new Vector3(0.028f, 0.032f, 0.06f), new Vector3(0.002f, 0.012f, 0.04f), Shade.Black);
            // handguard with vents
            k.Bevel(new Vector3(0, 0.02f, 0.27f), new Vector3(0.056f, 0.06f, 0.2f), 0.014f, Shade.HatchDark, Shade.Dark);
            for (int i = 0; i < 4; i++)
                k.Box(new Vector3(0, 0.02f, 0.21f + i * 0.04f), new Vector3(0.058f, 0.02f, 0.01f), Shade.Black);
            // barrel + muzzle brake
            k.Cylinder(new Vector3(0, 0.025f, 0.43f), 0.011f, 0.14f, 8, Shade.Black, Shade.Dark);
            k.Cylinder(new Vector3(0, 0.025f, 0.51f), 0.017f, 0.04f, 8, Shade.Black, Shade.HatchDark);
            // open reflex sight: base, hoop and a small dot on a post
            k.Box(new Vector3(0, 0.066f, 0.0f), new Vector3(0.03f, 0.006f, 0.05f), Shade.Dark);
            k.Box(new Vector3(-0.0155f, 0.084f, 0.012f), new Vector3(0.004f, 0.032f, 0.01f), Shade.Dark);
            k.Box(new Vector3(0.0155f, 0.084f, 0.012f), new Vector3(0.004f, 0.032f, 0.01f), Shade.Dark);
            k.Box(new Vector3(0, 0.1015f, 0.012f), new Vector3(0.035f, 0.004f, 0.01f), Shade.Dark);
            k.Box(new Vector3(0, 0.073f, 0.02f), new Vector3(0.0015f, 0.012f, 0.0015f), Shade.Dark);
            k.Box(new Vector3(0, 0.0805f, 0.02f), new Vector3(0.003f, 0.003f, 0.003f), Shade.Red);
            // stock
            k.Bevel(new Vector3(0, 0.0f, -0.2f), new Vector3(0.04f, 0.08f, 0.18f), 0.01f, Shade.HatchLight, Shade.Light);
            k.Box(new Vector3(0, -0.005f, -0.293f), new Vector3(0.044f, 0.095f, 0.012f), Shade.Black);
            // magazine (slanted) and grip
            k.At(new Vector3(0, -0.02f, 0.1f), new Vector3(12f, 0, 0));
            k.Bevel(new Vector3(0, -0.055f, 0), new Vector3(0.032f, 0.11f, 0.055f), 0.008f, Shade.Dark, Shade.Black);
            k.Box(new Vector3(0, -0.112f, 0), new Vector3(0.036f, 0.008f, 0.058f), Shade.Black);
            k.At(new Vector3(0, -0.02f, -0.04f), new Vector3(-16f, 0, 0));
            k.Bevel(new Vector3(0, -0.045f, 0), new Vector3(0.03f, 0.09f, 0.042f), 0.006f, Shade.HatchDark, Shade.Dark);
            k.At(Vector3.zero);
            // trigger guard
            k.Box(new Vector3(0, -0.028f, 0.02f), new Vector3(0.01f, 0.005f, 0.06f), Shade.Dark);

            var m = new Model { Root = new GameObject("RifleModel") };
            MeshChild(m.Root.transform, "Body", k.Build("Arsenal.Rifle"), Assets.ToolAtlas);
            m.Muzzle = Point(m.Root.transform, "Muzzle", new Vector3(0, 0.025f, 0.535f), Vector3.zero);
            Point(m.Root.transform, "Sight", new Vector3(0, 0.0805f, 0.02f), Vector3.zero);
            m.RightHand = Point(m.Root.transform, "RightHandPos", new Vector3(0.012f, -0.05f, -0.06f), new Vector3(8.5f, 29.4f, 284.5f));
            m.LeftHandFirstPerson = Point(m.Root.transform, "LeftHandPos_firstPerson", new Vector3(-0.03f, -0.02f, 0.27f), new Vector3(356.7f, 15.4f, 347f));
            m.LeftHandThirdPerson = Point(m.Root.transform, "LeftHandPos_thirdPerson", new Vector3(-0.03f, -0.02f, 0.27f), new Vector3(8.6f, 24.3f, 10.2f));
            m.Display = Point(m.Root.transform, "Display", new Vector3(0.0285f, 0.03f, 0.115f), new Vector3(0, -90f, 0));
            return m;
        }

        public static Model Marksman()
        {
            var k = new MeshKit();
            // long receiver
            k.Bevel(new Vector3(0, 0.02f, 0.02f), new Vector3(0.06f, 0.075f, 0.36f), 0.014f, Shade.HatchMid, Shade.Grey);
            k.Box(new Vector3(0.031f, 0.03f, 0.04f), new Vector3(0.004f, 0.035f, 0.16f), Shade.Orange);
            k.Box(new Vector3(0.033f, 0.03f, 0.02f), new Vector3(0.002f, 0.014f, 0.05f), Shade.Black);
            // heavy barrel, fluted look
            k.Cylinder(new Vector3(0, 0.03f, 0.47f), 0.016f, 0.56f, 6, Shade.Black, Shade.HatchDark);
            for (int i = 0; i < 3; i++)
                k.Cylinder(new Vector3(0, 0.03f, 0.3f + i * 0.1f), 0.019f, 0.012f, 6, Shade.Black, Shade.Dark);
            // big muzzle brake
            k.Box(new Vector3(0, 0.03f, 0.78f), new Vector3(0.06f, 0.035f, 0.07f), Shade.HatchDark, Shade.Dark);
            k.Box(new Vector3(0, 0.03f, 0.775f), new Vector3(0.062f, 0.02f, 0.015f), Shade.Black);
            k.Box(new Vector3(0, 0.03f, 0.795f), new Vector3(0.062f, 0.02f, 0.015f), Shade.Black);
            // scope on mounts
            k.Cylinder(new Vector3(0, 0.1f, 0.05f), 0.022f, 0.24f, 8, Shade.Black, Shade.HatchDark);
            k.Cylinder(new Vector3(0, 0.1f, 0.18f), 0.03f, 0.05f, 8, Shade.Blue, Shade.Dark);
            k.Cylinder(new Vector3(0, 0.1f, -0.08f), 0.027f, 0.04f, 8, Shade.Black, Shade.Dark);
            k.Box(new Vector3(0, 0.07f, -0.01f), new Vector3(0.018f, 0.03f, 0.02f), Shade.Dark);
            k.Box(new Vector3(0, 0.07f, 0.11f), new Vector3(0.018f, 0.03f, 0.02f), Shade.Dark);
            // stock with cheek rest
            k.Bevel(new Vector3(0, 0.0f, -0.25f), new Vector3(0.045f, 0.09f, 0.22f), 0.012f, Shade.HatchLight, Shade.Light);
            k.Box(new Vector3(0, 0.055f, -0.23f), new Vector3(0.04f, 0.02f, 0.1f), Shade.Dark);
            k.Box(new Vector3(0, -0.005f, -0.365f), new Vector3(0.05f, 0.1f, 0.014f), Shade.Black);
            // box magazine + grip
            k.Box(new Vector3(0, -0.045f, 0.08f), new Vector3(0.04f, 0.06f, 0.07f), Shade.Dark, Shade.Black);
            k.At(new Vector3(0, -0.02f, -0.06f), new Vector3(-16f, 0, 0));
            k.Bevel(new Vector3(0, -0.045f, 0), new Vector3(0.032f, 0.09f, 0.045f), 0.006f, Shade.HatchDark, Shade.Dark);
            k.At(Vector3.zero);
            // folded bipod
            k.At(new Vector3(0, 0.0f, 0.4f), new Vector3(-8f, 0, 0));
            k.Box(new Vector3(0.012f, -0.005f, 0.06f), new Vector3(0.008f, 0.008f, 0.14f), Shade.Black);
            k.Box(new Vector3(-0.012f, -0.005f, 0.06f), new Vector3(0.008f, 0.008f, 0.14f), Shade.Black);
            k.At(Vector3.zero);

            var m = new Model { Root = new GameObject("MarksmanModel") };
            MeshChild(m.Root.transform, "Body", k.Build("Arsenal.Marksman"), Assets.ToolAtlas);
            m.Muzzle = Point(m.Root.transform, "Muzzle", new Vector3(0, 0.03f, 0.82f), Vector3.zero);
            Point(m.Root.transform, "Sight", new Vector3(0, 0.1f, -0.1f), Vector3.zero);
            m.RightHand = Point(m.Root.transform, "RightHandPos", new Vector3(0.012f, -0.05f, -0.08f), new Vector3(8.5f, 29.4f, 284.5f));
            m.LeftHandFirstPerson = Point(m.Root.transform, "LeftHandPos_firstPerson", new Vector3(-0.03f, -0.02f, 0.28f), new Vector3(356.7f, 15.4f, 347f));
            m.LeftHandThirdPerson = Point(m.Root.transform, "LeftHandPos_thirdPerson", new Vector3(-0.03f, -0.02f, 0.28f), new Vector3(8.6f, 24.3f, 10.2f));
            m.Display = Point(m.Root.transform, "Display", new Vector3(0.0335f, 0.04f, 0.1f), new Vector3(0, -90f, 0));
            return m;
        }
    }
}
