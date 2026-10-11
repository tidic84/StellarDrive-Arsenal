using System.Collections.Generic;
using HarmonyLib;
using Ships.Physics;
using UnityEngine;

namespace Arsenal.Damage
{
    /// <summary>
    /// Blasts push ships. The server's ship physics resets each root rigidbody's velocity from the synced
    /// state every tick and then applies the flight forces (ShipRigidbody.ApplyForces); the impulses queued
    /// here are added right after, so the simulation keeps them and the new velocity is synced to everyone.
    /// Applied at the side of the hull facing the blast, an off-centre hit also sets the ship spinning.
    /// </summary>
    internal static class ShipKnockback
    {
        /// <summary>Impulse (N·s) per unit of blast power at point-blank range.</summary>
        public const float ImpulsePerPower = 9f;
        /// <summary>Most a single blast can change a ship's speed, so tiny ships don't get launched into orbit.</summary>
        private const float MaxDeltaV = 28f;
        private const float MaxSpin = 1.1f; // rad/s added

        private static readonly Dictionary<uint, List<(Vector3 v, Vector3 point, bool kinetic)>> Pending = new Dictionary<uint, List<(Vector3, Vector3, bool)>>();
        private static readonly Dictionary<int, uint> BodyIds = new Dictionary<int, uint>();
#if DEV
        public static readonly HashSet<uint> DevHover = new HashSet<uint>();
        public static readonly Dictionary<uint, Vector3> DevFly = new Dictionary<uint, Vector3>(); // forced velocity (server physics space)
        public static bool DevNoKnock;
        private static float _devKnockedAt = -10f;
#endif
        private static readonly AccessTools.FieldRef<ShipRigidbody, Rigidbody> RigidbodyField = AccessTools.FieldRefAccess<ShipRigidbody, Rigidbody>("_rigidbody");

        public static void Patch(HarmonyLib.Harmony h)
        {
            h.Patch(AccessTools.Method(typeof(ShipRigidbody), nameof(ShipRigidbody.ApplyForces)),
                postfix: new HarmonyMethod(typeof(ShipKnockback), nameof(ApplyForcesPostfix)));
        }

        public static void Reset()
        {
            Pending.Clear();
            BodyIds.Clear();
        }

        /// <summary>Server: push the root ship <paramref name="rootId"/> (impulse and point in its space).</summary>
        public static void Queue(uint rootId, Vector3 impulse, Vector3 point) => Add(rootId, impulse, point, false);

        /// <summary>
        /// Server: a kinetic hit (hypersonic missile) that changes the ship's speed by <paramref name="deltaV"/>
        /// whatever its mass, up to <see cref="KineticRefMass"/>; heavier ships move less. A fixed impulse barely
        /// moved a real ship in flight: its pilot's stabiliser soaked up the spin, and the speed change was tiny.
        /// </summary>
        public static void QueueKinetic(uint rootId, Vector3 deltaV, Vector3 point) => Add(rootId, deltaV, point, true);

        public const float KineticRefMass = 4000f;
        private const float KineticMaxSpin = 0.5f;

        private static void Add(uint rootId, Vector3 v, Vector3 point, bool kinetic)
        {
#if DEV
            if (DevNoKnock) return;
#endif
            if (!Pending.TryGetValue(rootId, out var list)) Pending[rootId] = list = new List<(Vector3, Vector3, bool)>();
            list.Add((v, point, kinetic));
        }

        private static void ApplyForcesPostfix(ShipRigidbody __instance)
        {
#if DEV
            if (DevHover.Count == 0 && DevFly.Count == 0 && Pending.Count == 0) return;
#else
            if (Pending.Count == 0) return;
#endif
            if (!BodyIds.TryGetValue(__instance.GetInstanceID(), out uint id))
            {
                // server bodies are named "ship {id} rigidBody server"
                string[] parts = __instance.name.Split(' ');
                if (parts.Length < 2 || !uint.TryParse(parts[1], out id)) return;
                BodyIds[__instance.GetInstanceID()] = id;
            }
#if DEV
            if (DevHover.Contains(id) && Time.time > _devKnockedAt + 2f)
            {
                Rigidbody hb = RigidbodyField(__instance);
                if (hb != null && !hb.isKinematic) { hb.linearVelocity *= 0.5f; hb.angularVelocity *= 0.5f; }
            }
            if (DevFly.TryGetValue(id, out Vector3 fly))
            {
                Rigidbody fb = RigidbodyField(__instance);
                if (fb != null && !fb.isKinematic) { fb.linearVelocity = fly; fb.angularVelocity = Vector3.zero; }
            }
#endif
            if (!Pending.TryGetValue(id, out var list)) return;
#if DEV
            _devKnockedAt = Time.time;
#endif
            Pending.Remove(id);
            Rigidbody rb = RigidbodyField(__instance);
            if (rb == null || rb.isKinematic) return;
            float mass = Mathf.Max(1f, rb.mass);
            Vector3 dvBefore = rb.linearVelocity, spinBefore = rb.angularVelocity;
            foreach (var (v, point, kinetic) in list)
            {
                Vector3 j = kinetic
                    ? v * mass * Mathf.Min(1f, Mathf.Sqrt(KineticRefMass / mass))
                    : Vector3.ClampMagnitude(v, mass * MaxDeltaV);
                // velocity change applied now (forces would only act inside the step)
                rb.linearVelocity += j / mass;
                Vector3 arm = point - rb.worldCenterOfMass;
                Vector3 torque = Vector3.Cross(arm, j);
                Quaternion q = rb.rotation * rb.inertiaTensorRotation;
                Vector3 local = Quaternion.Inverse(q) * torque;
                Vector3 inertia = Vector3.Max(rb.inertiaTensor, Vector3.one * 0.01f);
                Vector3 dw = q * new Vector3(local.x / inertia.x, local.y / inertia.y, local.z / inertia.z);
                rb.angularVelocity += Vector3.ClampMagnitude(dw, kinetic ? KineticMaxSpin : MaxSpin);
            }
#if DEV
            ArsenalMod.Log.Msg($"[DEV] knockback ship {id}: mass {mass:0} dv {(rb.linearVelocity - dvBefore).magnitude:0.0} m/s, spin {(rb.angularVelocity - spinBefore).magnitude:0.00} rad/s");
#endif
        }
    }
}
