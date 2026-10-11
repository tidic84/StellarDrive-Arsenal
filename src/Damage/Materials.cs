using Ships.Interface.Model.Parts;

namespace Arsenal.Damage
{
    /// <summary>
    /// How each hull material stands up to weapons. Glass is brittle (anything goes through, holes come with a
    /// shattered rim), wood is soft (splinters, bigger ragged holes), iron is hard (small clean holes, rounds that
    /// lack penetration only dent it, and shallow hits glance off).
    /// </summary>
    internal static class Materials
    {
        /// <summary>Hit points of a whole 1 x 1 panel.</summary>
        public static float MaxHealth(HullMaterialType m) => m switch
        {
            HullMaterialType.Glass => 40f,
            HullMaterialType.Wood => 170f,
            _ => 460f,
        };

        /// <summary>Penetration a round needs to go through the panel head-on (more at an angle).</summary>
        public static float Resistance(HullMaterialType m) => m switch
        {
            HullMaterialType.Glass => 4f,
            HullMaterialType.Wood => 14f,
            _ => 38f,
        };

        /// <summary>Hole size compared with the round's calibre.</summary>
        public static float HoleScale(HullMaterialType m) => m switch
        {
            HullMaterialType.Glass => 1.35f,
            HullMaterialType.Wood => 1.15f,
            _ => 0.9f,
        };

        /// <summary>Glass shatters around the hole: an extra ring this many times the hole radius.</summary>
        public static float ShatterScale(HullMaterialType m) => m == HullMaterialType.Glass ? 2.4f : 0f;

        /// <summary>
        /// Below this |cos| between the round and the surface normal (very shallow hits) a round glances off
        /// instead of biting in, unless it has lots of spare penetration. 0 = never.
        /// </summary>
        public static float RicochetCos(HullMaterialType m) => m switch
        {
            HullMaterialType.Glass => 0f,
            HullMaterialType.Wood => 0.16f,
            _ => 0.34f,
        };

        /// <summary>Share of the round's panel damage dealt when it is stopped by the panel (a dent).</summary>
        public static float DentDamage(HullMaterialType m) => m switch
        {
            HullMaterialType.Glass => 1f,
            HullMaterialType.Wood => 0.55f,
            _ => 0.3f,
        };

        /// <summary>How far through a panel the round travels before coming out (metres).</summary>
        public const float Thickness = 0.08f;

        /// <summary>A panel breaks apart once this share of its area is gone.</summary>
        public const float BreakHoleRatio = 0.5f;

        /// <summary>How many panels of this material a round with this penetration goes through head-on.</summary>
        public static int PanelsThrough(float penetration, HullMaterialType m)
        {
            float r = Resistance(m);
            int n = 0;
            while (penetration >= r && n < 9)
            {
                penetration -= r;
                n++;
            }
            return n;
        }
    }
}
