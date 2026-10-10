using Arsenal.Damage;
using Arsenal.Visual;
using Arsenal.Weapons;
using Planets.Interface.Model;
using Ships.Interface.Model.Parts;
using Ships.Interface.Services;
using UnityEngine;
using WorldTracking.Interface.Values;

namespace Arsenal.Vehicles
{
    /// <summary>
    /// The laser cannon's beam: stops on the first solid surface and burns it. On hull panels it melts a
    /// hole cell by cell; once through, the beam carries on to whatever is behind.
    /// </summary>
    internal static class Laser
    {
        private const float Range = 450f;
        private const float BurnInterval = 0.06f;
        private const float PanelDamagePerTick = 9f;
        private const float PlayerDamagePerTick = 5f;

        public static Vector3 Trace(Vector3 origin, Vector3 dir, Rigidbody ignore, ShipWeapons.Mount m)
        {
            PhysicsScene scene = ClientScene.PhysicsScene;
            Vector3 pos = origin;
            float travelled = 0f;
            bool burn = Time.time >= m.NextBurn;
            if (burn) m.NextBurn = Time.time + BurnInterval;

            for (int i = 0; i < 10; i++)
            {
                if (!scene.Raycast(pos, dir, out RaycastHit hit, Range - travelled, Ballistics.Mask, QueryTriggerInteraction.Ignore))
                    return pos + dir * (Range - travelled);
                travelled += hit.distance;
                if (ignore != null && hit.rigidbody == ignore)
                {
                    pos = hit.point + dir * 0.05f;
                    continue;
                }
                Collider col = hit.collider;
                if (ShipPartsTagDetection.ColliderIsHull(col) && Ballistics.TryResolvePanel(hit, out PanelKey key, out HullPart part, out Vector2 local))
                {
                    bool holeAble = HullDamage.CanHaveHoles(part);
                    if (holeAble && HullDamage.TryGetClientHoles(key, out HoleMask existing) && existing.IsHoleAt(local.x, local.y))
                    {
                        float skip = Mathf.Min(0.6f, 0.12f / Mathf.Max(0.2f, Mathf.Abs(Vector3.Dot(dir, hit.normal))));
                        pos = hit.point + dir * skip;
                        travelled += skip;
                        continue;
                    }
                    if (burn)
                    {
                        var holes = new HoleMask();
                        // glass gives way at once, wood and iron take a few ticks per cell
                        float chance = part.MaterialType == HullMaterialType.Glass ? 1f : part.MaterialType == HullMaterialType.Wood ? 0.6f : 0.3f;
                        if (holeAble && Random.value < chance) holes.PunchDisc(local.x, local.y, 0.05f, Ballistics.Rng);
                        HullDamage.ReportHit(key, holes, PanelDamagePerTick);
                        HullDamage.PredictHoles(key, holes);
                        Fx.EmitSparks(hit.point, hit.normal, 6);
                        if (Random.value < 0.3f) Fx.Puff(hit.point + hit.normal * 0.05f, 0.08f, new Color(0.35f, 0.33f, 0.32f), 0.8f, 1, hit.normal * 0.4f);
                    }
                    return hit.point;
                }
                if (burn)
                {
                    if (Ballistics.TryGetOtherPlayer(hit, out uint player)) PlayerDamage.Report(player, PlayerDamagePerTick, Arsenal.Weapons.WeaponKind.ShipLaser);
                    IMinableObject minable = col.GetComponentInParent<IMinableObject>();
                    if (minable != null)
                        try { minable.ReduceHealth(0.6f); } catch { }
                    Fx.EmitSparks(hit.point, hit.normal, 4);
                }
                return hit.point;
            }
            return pos;
        }
    }
}
