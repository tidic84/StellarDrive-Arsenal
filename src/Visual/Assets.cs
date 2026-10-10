using System.Linq;
using UnityEngine;

namespace Arsenal.Visual
{
    /// <summary>Materials and effect templates borrowed from the game, so everything the mod draws matches it.</summary>
    internal static class Assets
    {
        private static Material _toolAtlas, _flatMetal, _spark, _glow;
        private static ParticleSystem _sparks;
        private static GameObject _root, _inactiveRoot;

        /// <summary>
        /// Holder for the prefabs built at runtime. It must stay active: the game reads part prefabs with
        /// GetComponentsInChildren (sockets, interactions, tool visuals), which skips inactive objects.
        /// It sits far below the world, and our own scripts do nothing while they are under it.
        /// </summary>
        public static Transform TemplateRoot
        {
            get
            {
                if (_root == null)
                {
                    _root = new GameObject("Arsenal.Templates");
                    _root.transform.position = new Vector3(0f, -100000f, 0f);
                    Object.DontDestroyOnLoad(_root);
                }
                return _root.transform;
            }
        }

        /// <summary>Holder for things that must not run at all (effect templates).</summary>
        public static Transform InactiveRoot
        {
            get
            {
                if (_inactiveRoot == null)
                {
                    _inactiveRoot = new GameObject("Arsenal.Inactive");
                    _inactiveRoot.SetActive(false);
                    Object.DontDestroyOnLoad(_inactiveRoot);
                }
                return _inactiveRoot.transform;
            }
        }

        public static bool IsTemplate(Component c) => _root != null && c.transform.IsChildOf(_root.transform);

        private static Material Find(string name) =>
            Resources.FindObjectsOfTypeAll<Material>().FirstOrDefault(m => m != null && m.name == name);

        /// <summary>The hand-drawn atlas material of the mining laser (ink lines + hatching + ship palette).</summary>
        public static Material ToolAtlas => _toolAtlas != null ? _toolAtlas : (_toolAtlas = Find("MiningLaser") ?? FlatMetal);

        /// <summary>Plain shaded metal (the paint tool's material); we tint copies of it.</summary>
        public static Material FlatMetal => _flatMetal != null ? _flatMetal : (_flatMetal = Find("PartMetal"));

        /// <summary>Additive spark material of the mining laser, for flashes and tracers.</summary>
        public static Material Spark => _spark != null ? _spark : (_spark = Find("LaserSparkMat"));

        public static Material Tinted(Color c)
        {
            var m = new Material(FlatMetal) { name = "Arsenal.Tinted" };
            m.SetColor("_Color", c);
            return m;
        }

        public static Material Glow(Color c, float intensity)
        {
            var m = new Material(Spark) { name = "Arsenal.Glow" };
            m.SetColor("_BaseColor", Color.white);
            m.SetColor("_EmissionColor", c * intensity);
            m.SetColor("_Color", Color.white);
            return m;
        }

        /// <summary>A copy of the mining laser's impact sparks, emitted on demand.</summary>
        public static ParticleSystem SparksTemplate
        {
            get
            {
                if (_sparks != null) return _sparks;
                ParticleSystem src = Resources.FindObjectsOfTypeAll<ParticleSystem>().FirstOrDefault(p => p != null && p.name == "Sparks" && p.transform.parent != null && p.transform.parent.name == "LaserSparkEffect");
                if (src == null) return null;
                _sparks = Object.Instantiate(src, InactiveRoot);
                _sparks.name = "Arsenal.Sparks";
                return _sparks;
            }
        }

        public static void ClearSceneRefs()
        {
            _toolAtlas = null;
            _flatMetal = null;
            _spark = null;
            _glow = null;
        }
    }
}
