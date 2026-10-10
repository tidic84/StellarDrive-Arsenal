using Arsenal.Vehicles;
using Arsenal.Visual;
using UnityEngine;

namespace Arsenal.Weapons
{
    /// <summary>Beams fired by other players (relayed ten times a second).</summary>
    internal static class LaserFx
    {
        /// <param name="style">0 = ship laser cannon, 1 = disintegrator, 2 = capital beam.</param>
        public static void RemoteBeam(Vector3 from, Vector3 to, byte style)
        {
            switch (style)
            {
                case 1:
                    Fx.Beam(from, to, 0.08f, Fx.GlowMat(new Color(0.75f, 0.3f, 1f), 10f), 0.12f);
                    break;
                case 2:
                    Fx.Beam(from, to, 1.2f, Fx.GlowMat(new Color(1f, 0.85f, 0.5f), 12f), 0.12f);
                    Fx.Beam(from, to, 0.4f, Fx.GlowMat(Color.white, 14f), 0.12f);
                    break;
                default:
                    Fx.Beam(from, to, 0.09f, ShipWeapons.BeamMaterial, 0.12f);
                    break;
            }
            Fx.EmitSparks(to, (from - to).normalized, 4);
        }
    }
}
