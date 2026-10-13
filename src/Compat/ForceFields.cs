using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Arsenal.Core;
using Arsenal.Damage;
using Arsenal.Net;
using Arsenal.Visual;
using FishNet.Connection;
using HarmonyLib;
using Ships;
using UnityEngine;

namespace Arsenal.Compat
{
    /// <summary>
    /// Compatibility with the force fields of the "TF Advanced" mod (StellarDrive-demo-TF-advanced), found by name
    /// so Arsenal runs the same without it. A field is a bubble around its ship: whatever is fired from outside
    /// stops on it — bullets, laser and blaster bolts, beams, flames, arcs, rockets and missiles (they go off on
    /// it), grenades (they bounce off) — and nothing behind it is holed or hurt. Every hit drains the field on the
    /// host, as its own hooks do for blasts. Fire from inside the bubble goes out freely.
    /// Client: the bubbles are the mod's "TFA_ForceFieldBubble" objects (a sphere of radius 0.5, scaled to the
    /// ship), present only while the field is up. Host: whether a ship's field holds comes from the mod's
    /// ForceField.IsUp, and the bubble is rebuilt from the ship's hull bounds the way the mod fits it.
    /// </summary>
    internal static class ForceFields
    {
        /// <summary>A bubble seen by this client.</summary>
        public struct Bubble
        {
            public Transform T;
            public uint ShipId;
            public Component Visuals;
        }

        /// <summary>Field points per point of panel damage (the mod's own rate for Arsenal hits), and per point of blast power.</summary>
        public const float PanelPoints = 0.25f, BlastPoints = 1f / 20f;

        private static bool _resolved;
        private static Type _visualsType;
        private static FieldInfo _bubbleField, _flashField, _rippleField, _impactField, _glowField;
        private static MethodInfo _isUp, _tryAbsorb;
        private static readonly List<Bubble> Bubbles = new List<Bubble>();
        private static float _nextScan;

        /// <summary>Whether the TF Advanced mod (and its force field) is loaded.</summary>
        public static bool Present => Resolve();

        private static bool Resolve()
        {
            if (_resolved) return _visualsType != null || _isUp != null;
            _resolved = true;
            try
            {
                _visualsType = AccessTools.TypeByName("StellarDriveDemoTFAdvanced.Power.ForceFieldVisuals");
                if (_visualsType != null)
                {
                    _bubbleField = AccessTools.Field(_visualsType, "_bubble");
                    _flashField = AccessTools.Field(_visualsType, "_flash");
                    _rippleField = AccessTools.Field(_visualsType, "_ripple");
                    _impactField = AccessTools.Field(_visualsType, "_impact");
                    _glowField = AccessTools.Field(_visualsType, "_glow");
                    if (_bubbleField == null) _visualsType = null;
                }
                Type field = AccessTools.TypeByName("StellarDriveDemoTFAdvanced.Power.ForceField");
                if (field != null)
                {
                    _isUp = AccessTools.Method(field, "IsUp");
                    _tryAbsorb = AccessTools.Method(field, "TryAbsorb");
                }
                if (_visualsType != null || _isUp != null)
                    ArsenalMod.Log.Msg($"TF Advanced force fields found: weapons stop on them (bubbles {(_visualsType != null ? "seen" : "not seen")}, host {(_isUp != null ? "drains them" : "cannot drain them")})");
            }
            catch (Exception ex)
            {
                ArsenalMod.Log.Warning($"TF Advanced force fields not usable: {ex.Message}");
                _visualsType = null; _isUp = null;
            }
            return _visualsType != null || _isUp != null;
        }

#if DEV
        /// <summary>Dev: test bubbles (transform, ship) and ships whose field the host treats as holding.</summary>
        public static readonly List<(Transform, uint)> DevBubbles = new List<(Transform, uint)>();
        public static readonly HashSet<uint> DevShielded = new HashSet<uint>();
        public static float DevAbsorbed;
#endif

        public static void Init()
        {
            ArsenalNet.OnServer(Op.ShieldHit, ServerOnShieldHit);
        }

        public static void Reset()
        {
            Bubbles.Clear();
            _nextScan = 0f;
        }

        // ------------------------------------------------------------------ client: the bubbles in sight

        private static void Refresh()
        {
            if (Time.unscaledTime < _nextScan) return;
            _nextScan = Time.unscaledTime + 0.25f;
            Bubbles.Clear();
#if DEV
            foreach (var (t, id) in DevBubbles) if (t != null) Bubbles.Add(new Bubble { T = t, ShipId = id });
#endif
            if (!Resolve() || _visualsType == null) return;
            foreach (UnityEngine.Object o in UnityEngine.Object.FindObjectsOfType(_visualsType))
            {
                if (!(o is Component c) || !(_bubbleField.GetValue(c) is GameObject go) || go == null) continue;
                uint ship = 0;
                Transform root = go.transform.parent;
                if (root != null && root.name.StartsWith("ship ")) uint.TryParse(root.name.Substring(5).Split(' ')[0], out ship);
                Bubbles.Add(new Bubble { T = go.transform, ShipId = ship, Visuals = c });
            }
        }

        /// <summary>
        /// Where a segment fired from <paramref name="origin"/> first enters a force field from outside, within
        /// <paramref name="maxDist"/>. A segment starting inside a bubble leaves it freely (its own ship firing out).
        /// </summary>
        public static bool Entry(Vector3 origin, Vector3 dir, float maxDist, out float dist, out Vector3 normal, out Bubble bubble)
        {
            dist = maxDist; normal = -dir; bubble = default;
            Refresh();
            bool found = false;
            foreach (Bubble b in Bubbles)
            {
                if (b.T == null || !b.T.gameObject.activeInHierarchy) continue;
                Vector3 o = b.T.InverseTransformPoint(origin);
                if (o.sqrMagnitude <= 0.25f) continue;
                Vector3 d = b.T.InverseTransformVector(dir * maxDist);
                float a = Vector3.Dot(d, d);
                if (a < 1e-10f) continue;
                float half = Vector3.Dot(o, d), c = o.sqrMagnitude - 0.25f, disc = half * half - a * c;
                if (disc < 0f) continue;
                float t = (-half - Mathf.Sqrt(disc)) / a;
                if (t < 0f || t > 1f || t * maxDist >= dist) continue;
                dist = t * maxDist;
                Vector3 local = o + d * t;
                // the normal of a stretched sphere: through the inverse transpose of its scale
                normal = b.T.worldToLocalMatrix.transpose.MultiplyVector(local).normalized;
                bubble = b;
                found = true;
            }
            return found;
        }

        /// <summary>Whether a point lies inside a force field this client sees (and which).</summary>
        public static bool Inside(Vector3 point, out Bubble bubble)
        {
            bubble = default;
            Refresh();
            foreach (Bubble b in Bubbles)
            {
                if (b.T == null || !b.T.gameObject.activeInHierarchy) continue;
                if (b.T.InverseTransformPoint(point).sqrMagnitude <= 0.25f) { bubble = b; return true; }
            }
            return false;
        }

        /// <summary>The colour the field glows in (cyan, gold or violet after its generator).</summary>
        public static Color Glow(Bubble b)
        {
            try { if (_glowField != null && b.Visuals != null) return (Color)_glowField.GetValue(b.Visuals); }
            catch { }
            return new Color(0.35f, 0.85f, 1f);
        }

        /// <summary>
        /// Something struck the field: it flashes and ripples from the impact, sparks fly and it crackles. The
        /// shooter's copy tells the host, which drains the field by <paramref name="panelDamage"/> × <see cref="PanelPoints"/>.
        /// </summary>
        public static void Struck(Bubble b, Vector3 point, Vector3 normal, float size, bool owner, float panelDamage)
        {
            Effect(point, normal, Glow(b), size);
            if (owner) Drain(b, panelDamage);
        }

        /// <summary>
        /// The look and sound of a hit on a field, wherever it comes from (also replayed for the other players): a
        /// flash in the field's colour, sparks, a crackle, and the bubble's own impact ripple.
        /// </summary>
        public static void Effect(Vector3 point, Vector3 normal, Color glow, float size)
        {
            Fx.GlowFlash(point + normal * 0.05f, Color.Lerp(glow, Color.white, 0.5f), 0.25f * size, 0.08f, glow: 10f);
            Fx.GlowFlash(point + normal * 0.1f, glow, 0.9f * size, 0.22f, 12f, 8f, 1.6f);
            Fx.EmitSparks(point, normal, Mathf.RoundToInt(8 * size));
            Sounds.ShieldHit(point, size);
            Refresh();
            foreach (Bubble b in Bubbles)
            {
                if (b.T == null || b.Visuals == null || !b.T.gameObject.activeInHierarchy) continue;
                Vector3 local = b.T.InverseTransformPoint(point);
                if (Mathf.Abs(local.magnitude - 0.5f) > 0.08f) continue;
                try
                {
                    _flashField?.SetValue(b.Visuals, 1f);
                    _rippleField?.SetValue(b.Visuals, 0f);
                    _impactField?.SetValue(b.Visuals, local.normalized);
                }
                catch { }
                break;
            }
        }

        /// <summary>The shooter's copy of a hit tells the host, which drains the field by <paramref name="panelDamage"/> × <see cref="PanelPoints"/>.</summary>
        public static void Drain(Bubble b, float panelDamage)
        {
            if (panelDamage <= 0f || b.ShipId == 0) return;
            ArsenalNet.SendToServer(Op.ShieldHit, w =>
            {
                w.Write(b.ShipId);
                w.Write(panelDamage);
            }, FishNet.Transporting.Channel.Unreliable);
        }

        // ------------------------------------------------------------------ host

        private static void ServerOnShieldHit(NetworkConnection sender, BinaryReader r)
        {
            uint ship = r.ReadUInt32();
            float damage = Mathf.Clamp(r.ReadSingle(), 0f, 2000f);
            ServerAbsorb(ship, damage * PanelPoints);
        }

        /// <summary>Host: whether the force field of a ship (or of what it is docked to) holds.</summary>
        public static bool ServerUp(uint shipId)
        {
#if DEV
            if (DevShielded.Contains(shipId)) return true;
#endif
            if (!Resolve() || _isUp == null || Svc.ShipsServer == null) return false;
            try { return (bool)_isUp.Invoke(null, new object[] { Svc.ShipsServer, shipId }); }
            catch { return false; }
        }

        /// <summary>Host: the field of a ship takes <paramref name="points"/>. False when there is no field to take them.</summary>
        public static bool ServerAbsorb(uint shipId, float points)
        {
#if DEV
            if (DevShielded.Contains(shipId)) { DevAbsorbed += points; ArsenalMod.Log.Msg($"[DEV] shield of {shipId} took {points:0.0} (total {DevAbsorbed:0.0})"); return true; }
#endif
            if (!Resolve() || _tryAbsorb == null || Svc.ShipsServer == null || points <= 0f) return false;
            try { return (bool)_tryAbsorb.Invoke(null, new object[] { Svc.ShipsServer, shipId, points }); }
            catch { return false; }
        }

        /// <summary>
        /// Host: the bubble of a ship in its own frame (centre and half-size), fitted like the mod's: the hull's bounds
        /// grown by 30 %, at least a metre each way.
        /// </summary>
        public static void ServerBubble(TrackedShipServer ship, out Vector3 center, out Vector3 half)
        {
            var b = ship.GetHullCoordBounds();
            Vector3 lo = (Vector3)b.LowerCoord - Vector3.one * 0.5f, hi = (Vector3)b.UpperCoord + Vector3.one * 0.5f;
            center = (lo + hi) * 0.5f;
            half = Vector3.Max((hi - lo) * 0.65f, Vector3.one);
        }

        /// <summary>Host: whether a point given in the ship's frame lies inside its bubble.</summary>
        public static bool ServerInside(TrackedShipServer ship, Vector3 shipLocal)
        {
            ServerBubble(ship, out Vector3 c, out Vector3 h);
            Vector3 p = shipLocal - c;
            return (p.x * p.x) / (h.x * h.x) + (p.y * p.y) / (h.y * h.y) + (p.z * p.z) / (h.z * h.z) <= 1f;
        }

        /// <summary>Host: a ship whose field holds is shielded from a blast or hit at this point (in its frame) outside the bubble.</summary>
        public static bool ServerShields(TrackedShipServer ship, Vector3 shipLocal) => ServerUp(ship.Id) && !ServerInside(ship, shipLocal);
    }
}
