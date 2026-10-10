using Arsenal.Visual;
using UnityEngine;

namespace Arsenal.Weapons
{
    /// <summary>What one shot looks and sounds like, for every weapon. Used for our own shots and relayed ones.</summary>
    internal static class WeaponFx
    {
        public static void Shot(WeaponKind kind, Vector3 from, Vector3 to, Transform follow = null)
        {
            Vector3 dir = (to - from).sqrMagnitude > 0.0001f ? (to - from).normalized : Vector3.forward;
            HandWeaponDef def = WeaponDefs.ByKind(kind);
            TracerStyle style;
            Color color;
            float muzzle, width;
            if (def != null)
            {
                style = def.Tracer;
                color = def.TracerColor;
                muzzle = def.MuzzleSize;
                width = kind == WeaponKind.Marksman ? 0.04f : 0.022f;
            }
            else
            {
                (style, color, muzzle, width) = ShipStyle(kind);
            }

            switch (style)
            {
                case TracerStyle.Bullet:
                    if (muzzle > 0f) Fx.MuzzleFlash(from, dir, muzzle, follow);
                    Fx.BulletTracer(from, to, width, kind == WeaponKind.ShipGun || kind == WeaponKind.Pdc ? 450f : 320f);
                    break;
                case TracerStyle.Laser:
                    Fx.ColoredTracer(from, to, width * 1.4f, color, 520f, 5f, 8f);
                    Fx.Flame(from, dir, muzzle * 0.5f);
                    break;
                case TracerStyle.Plasma:
                    Fx.ColoredTracer(from, to, 0.06f, color, 140f, 1.4f, 9f);
                    break;
                case TracerStyle.Ion:
                    Fx.ColoredTracer(from, to, 0.045f, color, 600f, 3f, 9f);
                    Fx.ElectricArc(from, from + dir * 0.4f + Random.insideUnitSphere * 0.08f, color, 0.08f, 0.015f);
                    break;
                case TracerStyle.Rail:
                    float big = kind == WeaponKind.ShipRailgun ? 3f : 1f;
                    Fx.Streak(from, to, 0.05f * big, color, 0.5f);
                    Fx.Streak(from, to, 0.015f * big, Color.white, 0.25f);
                    // spiral of smoke along the path
                    float len = Mathf.Min((to - from).magnitude, 120f);
                    Vector3 side = Vector3.Cross(dir, Fx.Up).normalized;
                    if (side.sqrMagnitude < 0.01f) side = Vector3.Cross(dir, Vector3.right).normalized;
                    Vector3 up = Vector3.Cross(side, dir);
                    for (float d = 1.5f; d < len; d += 1.4f)
                    {
                        float a = d * 1.3f;
                        Vector3 p = from + dir * d + (side * Mathf.Cos(a) + up * Mathf.Sin(a)) * 0.12f * big;
                        Fx.Puff(p, 0.07f * big, new Color(0.85f, 0.9f, 0.95f), 1.4f, 1);
                    }
                    Fx.MuzzleFlash(from, dir, muzzle, follow);
                    break;
            }
            Sounds.Shot(kind, from);
        }

        private static (TracerStyle, Color, float, float) ShipStyle(WeaponKind kind) => kind switch
        {
            WeaponKind.PulseTurret => (TracerStyle.Laser, new Color(1f, 0.3f, 0.25f), 0.25f, 0.05f),
            WeaponKind.ShipRailgun => (TracerStyle.Rail, new Color(0.5f, 0.85f, 1f), 0.8f, 0.1f),
            WeaponKind.Pdc => (TracerStyle.Bullet, Color.white, 0.25f, 0.03f),
            _ => (TracerStyle.Bullet, Color.white, 0.45f, 0.05f),
        };
    }
}
