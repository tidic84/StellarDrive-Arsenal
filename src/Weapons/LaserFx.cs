using Arsenal.Vehicles;
using Arsenal.Visual;
using UnityEngine;

namespace Arsenal.Weapons
{
    /// <summary>Laser beams fired by other players (relayed ten times a second).</summary>
    internal static class LaserFx
    {
        public static void RemoteBeam(Vector3 from, Vector3 to)
        {
            Fx.Beam(from, to, 0.09f, ShipWeapons.BeamMaterial, 0.12f);
            Fx.EmitSparks(to, (from - to).normalized, 4);
        }
    }
}
