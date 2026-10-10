using Ships.Interface.Model.Parts;

namespace Arsenal.Damage
{
    /// <summary>How tough each hull material is against weapons.</summary>
    internal static class Materials
    {
        /// <summary>Hit points of a whole 1x1 panel.</summary>
        public static float MaxHealth(HullMaterialType m) => m switch
        {
            HullMaterialType.Glass => 40f,
            HullMaterialType.Wood => 160f,
            _ => 420f,
        };

        /// <summary>Penetration a projectile needs to go through the panel (at normal incidence).</summary>
        public static float Resistance(HullMaterialType m) => m switch
        {
            HullMaterialType.Glass => 3f,
            HullMaterialType.Wood => 10f,
            _ => 32f,
        };

        /// <summary>A panel breaks apart once this share of its cells is gone.</summary>
        public const float BreakHoleRatio = 0.55f;
    }
}
