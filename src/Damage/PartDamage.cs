using System.Collections.Generic;
using System.Linq;
using Ships;
using Ships.Interface.Model.Parts;
using UnityEngine;

namespace Arsenal.Damage
{
    /// <summary>
    /// Blasts also wreck the machinery bolted to a ship: thrusters, tanks, seats, batteries, consoles, chests,
    /// our own weapons... Each part has hit points from its size; once spent, it is destroyed the way the game
    /// destroys parts in crashes (TrackedShipServer.DestroyStatefulPart), so the game cleans up interactions,
    /// cables and mass, and plays its own destruction effect. Server only.
    /// </summary>
    internal static class PartDamage
    {
        private static readonly Dictionary<(uint ship, ushort part), float> Damage = new Dictionary<(uint, ushort), float>();

        public static void Reset() => Damage.Clear();

        public static void ForgetShip(uint shipId)
        {
            foreach (var k in Damage.Keys.Where(k => k.ship == shipId).ToList()) Damage.Remove(k);
        }

        /// <summary>Hit points of a part: bigger parts take more (a 1 m³ thruster ~ 1080, a small sensor ~ 400).</summary>
        public static float MaxHealth(StatefulPart part)
        {
            ShipPartBounds b = part.Settings != null && part.Settings.part != null ? part.Settings.part.GetComponent<ShipPartBounds>() : null;
            float volume = b != null ? Mathf.Abs(b.bounds.x * b.bounds.y * b.bounds.z) : 0.5f;
            return Mathf.Clamp(380f + 700f * volume, 380f, 2200f);
        }

        /// <summary>Applies a blast (centre in the ship's coordinates) to every part of the ship within reach.</summary>
        public static void ApplyBlast(TrackedShipServer ship, Vector3 shipLocalBlast, float radius, float power)
        {
            List<StatefulPart> parts = ship.StatefulParts.ToList();
            var destroyed = new List<ushort>();
            foreach (StatefulPart part in parts)
            {
                if (part.Settings == null || part.Settings.part == null) continue;
                ShipPartBounds b = part.Settings.part.GetComponent<ShipPartBounds>();
                Vector3 center = b != null ? b.center : Vector3.zero;
                Vector3 half = (b != null ? b.bounds : Vector3.one) * 0.5f;
                // blast position in the part's own frame, then distance to its bounding box
                Vector3 p = Quaternion.Inverse(part.LocalRotation) * (shipLocalBlast - part.LocalPosition) - center;
                Vector3 outside = new Vector3(Mathf.Max(0f, Mathf.Abs(p.x) - half.x), Mathf.Max(0f, Mathf.Abs(p.y) - half.y), Mathf.Max(0f, Mathf.Abs(p.z) - half.z));
                float d = outside.magnitude;
                if (d >= radius) continue;
                float f = 1f - d / radius;
                var key = (ship.Id, part.Id);
                Damage.TryGetValue(key, out float taken);
                taken += power * f * f * 0.8f;
                if (taken >= MaxHealth(part))
                {
                    Damage.Remove(key);
                    destroyed.Add(part.Id);
                }
                else Damage[key] = taken;
            }
            foreach (ushort id in destroyed)
            {
#if DEV
                ArsenalMod.Log.Msg($"[DEV] blast destroyed part {id} of ship {ship.Id}");
#endif
                try { ship.DestroyStatefulPart(id); }
                catch (System.Exception ex) { ArsenalMod.Log.Warning($"Could not destroy part {id} of ship {ship.Id}: {ex.Message}"); }
            }
        }
    }
}
