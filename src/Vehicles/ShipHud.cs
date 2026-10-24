using System.Collections.Generic;
using Arsenal.Content;
using Arsenal.Core;
using Arsenal.Weapons;
using Ships;
using Ships.Interface.Model.Parts;
using UnityEngine;

namespace Arsenal.Vehicles
{
    /// <summary>
    /// Pilot overlay while flying a ship with weapons, drawn in thin ink-like lines to sit with the game's HUD.
    /// However many weapons the ship carries, the fixed ones share one reticle where they converge (300 m ahead),
    /// with one status line per kind of weapon beside it (count, heat or charge, missiles left); locks and
    /// point-defence targets are marked once each.
    /// </summary>
    internal sealed class ShipHud : MonoBehaviour
    {
        private static Texture2D _px;
        private static GUIStyle _label;

        private sealed class Status
        {
            public string Name;
            public int Count;
            public int Ammo = -1;      // missiles / rockets / salvos left, summed over the group
            public float Bar = -1f;    // heat (the hottest) or charge (the readiest) of the group
            public Color BarColor;
        }

        private static readonly List<Status> _status = new List<Status>();
        private static readonly List<Vector2> _aims = new List<Vector2>();
        private static readonly List<Vector2> _samMarks = new List<Vector2>();
        private static readonly HashSet<uint> _locks = new HashSet<uint>();
        private static readonly HashSet<IThreat> _tracked = new HashSet<IThreat>();

        internal static Texture2D Px
        {
            get
            {
                if (_px != null) return _px;
                _px = new Texture2D(1, 1);
                _px.SetPixel(0, 0, Color.white);
                _px.Apply();
                return _px;
            }
        }

        private void OnGUI()
        {
            if (!ArsenalMod.InGame || Event.current.type != EventType.Repaint) return;
            if (!Pilot.TryGetPilotedRoot(out uint root)) return;
            Camera cam = Camera.main;
            if (cam == null) return;
            if (_label == null)
                _label = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };

            _status.Clear();
            _aims.Clear();
            _samMarks.Clear();
            _locks.Clear();
            _tracked.Clear();
            Color ink = new Color(0.95f, 0.98f, 1f, 0.9f);
            TrackedShipClient anyShip = null;

            foreach (ShipWeapons.Mount m in ShipWeapons.Mounts)
            {
                if (ShipWeapons.RootOf(m.Ctx.ShipId) != root) continue;
                if (!Svc.ShipsClient.TryGetTrackedShip(m.Ctx.ShipId, out TrackedShipClient ship) || !ship.IsRigidbodyLoaded) continue;
                if (!ship.TryGetStatefulPart(m.Ctx.PartId, out StatefulPart part)) continue;
                anyShip = ship;
                bool fixedAim = true;
                Status s;
                switch (m.Kind)
                {
                    case ShipContent.MachineGunId:
                        StatusFor("MITRAILLEUSE", "MACHINE GUN");
                        break;
                    case ShipContent.LaserCannonId:
                    case ShipContent.LaserGreenId:
                    case ShipContent.LaserBlueId:
                        s = StatusFor("LASER", "LASER");
                        if (m.Heat > s.Bar || m.Overheated)
                        {
                            s.Bar = Mathf.Max(s.Bar, m.Heat);
                            s.BarColor = m.Overheated ? new Color(1f, 0.35f, 0.2f) : new Color(1f, 0.75f, 0.3f);
                        }
                        break;
                    case ShipContent.PlasmaCannonId:
                        StatusFor("PLASMA", "PLASMA");
                        break;
                    case ShipContent.IonCannonId:
                        StatusFor("ION", "ION");
                        break;
                    case ShipContent.RailgunId:
                    {
                        s = StatusFor("RAILGUN", "RAILGUN");
                        bool reloading = Time.time < m.NextShot;
                        float v = reloading ? 1f - (m.NextShot - Time.time) / 4f : m.Charge;
                        if (v > s.Bar)
                        {
                            s.Bar = v;
                            s.BarColor = reloading ? new Color(0.5f, 0.55f, 0.6f) : new Color(0.5f, 0.85f, 1f);
                        }
                        break;
                    }
                    case ShipContent.RocketPodId:
                        AddAmmo(StatusFor("ROQUETTES", "ROCKETS"), Mathf.Max(0, m.Rockets));
                        break;
                    case ShipContent.MissilePodId:
                        AddAmmo(StatusFor("MISSILES", "MISSILES"), m.Missiles);
                        if (m.LockedTarget != 0) _locks.Add(m.LockedTarget);
                        break;
                    case ShipContent.TorpedoId:
                        AddAmmo(StatusFor("TORPILLES", "TORPEDOES"), m.Missiles);
                        if (m.LockedTarget != 0) _locks.Add(m.LockedTarget);
                        break;
                    case ShipContent.HyperLauncherId:
                        AddAmmo(StatusFor("HYPERSONIQUE", "HYPERSONIC"), m.Missiles);
                        if (m.LockedTarget != 0) _locks.Add(m.LockedTarget);
                        break;
                    case ShipContent.SamBatteryId:
                    {
                        // aims by itself: a small ring where its launcher points, batteries aiming together share one
                        fixedAim = false;
                        AddAmmo(StatusFor("SOL-AIR", "SAM"), m.Missiles);
                        if (m.Pitch == null) break;
                        Vector3 sp2 = cam.WorldToScreenPoint(m.Pitch.position + m.Pitch.forward * 300f);
                        if (sp2.z > 0) Merge(_samMarks, new Vector2(sp2.x, Screen.height - sp2.y));
                        break;
                    }
                    case ShipContent.FlareDispenserId:
                        fixedAim = false;
                        AddAmmo(StatusFor("LEURRES", "FLARES"), m.Missiles);
                        break;
                    case ShipContent.PdcId:
                        // point defence aims by itself: show what it is tracking
                        fixedAim = false;
                        StatusFor("DÉFENSE", "DEFENCE");
                        if (m.Tracking != null && !(m.Tracking is Object o && o == null)) _tracked.Add(m.Tracking);
                        break;
                    default:
                        fixedAim = false;
                        if (Turrets.IsTurret(m.Kind))
                        {
                            // turrets turn by themselves or answer a gunner: a ring where their guns point, like the SAM's
                            s = StatusFor(m.Manned ? "TOURELLES (ARTILLEUR)" : "TOURELLES", m.Manned ? "TURRETS (GUNNER)" : "TURRETS");
                            if (m.Heat > s.Bar || m.Overheated)
                            {
                                s.Bar = Mathf.Max(s.Bar, m.Heat);
                                s.BarColor = m.Overheated ? new Color(1f, 0.35f, 0.2f) : new Color(1f, 0.75f, 0.3f);
                            }
                            if (m.Pitch == null) break;
                            Vector3 tp = cam.WorldToScreenPoint(m.Pitch.position + m.Pitch.forward * 300f);
                            if (tp.z > 0) Merge(_samMarks, new Vector2(tp.x, Screen.height - tp.y));
                        }
                        break;
                }
                if (!fixedAim) continue;
                Vector3 aim = ShipWeapons.PartPoint(ship, part, new Vector3(0, 0.27f, 0.6f)) + ShipWeapons.PartForward(ship, part) * 300f;
                Vector3 sp = cam.WorldToScreenPoint(aim);
                if (sp.z > 0) _aims.Add(new Vector2(sp.x, Screen.height - sp.y));
            }
            if (_status.Count == 0) return;

            // One reticle where the fixed weapons converge; a faint dot for any weapon pointing well apart.
            Vector2 c = new Vector2(Screen.width / 2f, Screen.height / 2f);
            if (_aims.Count > 0)
            {
                c = Vector2.zero;
                foreach (Vector2 a in _aims) c += a;
                c /= _aims.Count;
                foreach (Vector2 a in _aims)
                    if ((a - c).sqrMagnitude > 30f * 30f)
                        Ring(a, 3f, new Color(ink.r, ink.g, ink.b, 0.45f));
                Ring(c, 10f, ink);
                Cross(c, 18f, 13f, ink);
            }
            foreach (Vector2 mk in _samMarks) Ring(mk, 6f, new Color(1f, 0.85f, 0.3f, 0.8f));

            // Status column right of the reticle, one line per kind of weapon.
            float y = c.y - (_status.Count - 1) * 9f;
            foreach (Status s in _status)
            {
                float x = c.x + 28f;
                string text = s.Count > 1 ? $"{s.Name} ×{s.Count}" : s.Name;
                if (s.Ammo >= 0)
                    text += "  " + (s.Ammo > 0 ? new string('|', Mathf.Min(s.Ammo, 24)) : Loc.T("RECHARGE", "RELOADING"));
                GUI.color = new Color(0f, 0f, 0f, 0.6f);
                GUI.Label(new Rect(x + 1f, y - 9f, 280f, 20f), text, _label);
                GUI.color = ink;
                GUI.Label(new Rect(x, y - 10f, 280f, 20f), text, _label);
                if (s.Bar >= 0f)
                    BarH(new Rect(x + _label.CalcSize(new GUIContent(text)).x + 4f, y - 3f, 30f, 6f), s.Bar, s.BarColor);
                y += 18f;
            }

            foreach (IThreat t in _tracked)
            {
                Vector3 tp = cam.WorldToScreenPoint(t.Position);
                if (tp.z > 0) Brackets(new Vector2(tp.x, Screen.height - tp.y), 14f, new Color(1f, 0.85f, 0.3f, 0.95f));
            }

            foreach (uint target in _locks)
            {
                if (!ShipWeapons.TryGetShipMotion(target, out Vector3 tcenter, out _, out _, out _)) continue;
                Vector3 tp = cam.WorldToScreenPoint(tcenter);
                if (tp.z <= 0) continue;
                var tc = new Vector2(tp.x, Screen.height - tp.y);
                Color lockCol = new Color(1f, 0.35f, 0.25f, 0.95f);
                Brackets(tc, 26f, lockCol);
                GUI.color = lockCol;
                float range = Vector3.Distance(ShipWeapons.TargetPoint(anyShip), tcenter);
                GUI.Label(new Rect(tc.x - 30f, tc.y + 28f, 200f, 20f), Loc.T("VERROUILLÉ", "LOCKED") + (range >= 1000f ? $"  {range / 1000f:0.0} km" : $"  {range:0} m"), _label);
            }
            GUI.color = Color.white;
        }

        private static Status StatusFor(string fr, string en)
        {
            string name = Loc.T(fr, en);
            foreach (Status s in _status)
                if (s.Name == name)
                {
                    s.Count++;
                    return s;
                }
            var added = new Status { Name = name, Count = 1 };
            _status.Add(added);
            return added;
        }

        private static void AddAmmo(Status s, int ammo) => s.Ammo = Mathf.Max(0, s.Ammo) + Mathf.Max(0, ammo);

        private static void Merge(List<Vector2> marks, Vector2 p)
        {
            foreach (Vector2 q in marks)
                if ((q - p).sqrMagnitude < 25f * 25f) return;
            marks.Add(p);
        }

        internal static void Line(Vector2 a, Vector2 b, float width, Color c)
        {
            GUI.color = c;
            Vector2 d = b - a;
            float angle = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
            Matrix4x4 m = GUI.matrix;
            GUIUtility.RotateAroundPivot(angle, a);
            GUI.DrawTexture(new Rect(a.x, a.y - width / 2f, d.magnitude, width), Px);
            GUI.matrix = m;
        }

        internal static void Cross(Vector2 c, float r, float gap, Color col)
        {
            Line(c + new Vector2(-r, 0), c + new Vector2(-gap, 0), 2f, col);
            Line(c + new Vector2(gap, 0), c + new Vector2(r, 0), 2f, col);
            Line(c + new Vector2(0, -r), c + new Vector2(0, -gap), 2f, col);
            Line(c + new Vector2(0, gap), c + new Vector2(0, r), 2f, col);
        }

        internal static void Ring(Vector2 c, float r, Color col)
        {
            const int n = 16;
            for (int i = 0; i < n; i++)
            {
                float a0 = i * Mathf.PI * 2f / n, a1 = (i + 1) * Mathf.PI * 2f / n;
                Line(c + new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * r, c + new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * r, 2f, col);
            }
        }

        internal static void Diamond(Vector2 c, float r, Color col)
        {
            Line(c + new Vector2(0, -r), c + new Vector2(r, 0), 2f, col);
            Line(c + new Vector2(r, 0), c + new Vector2(0, r), 2f, col);
            Line(c + new Vector2(0, r), c + new Vector2(-r, 0), 2f, col);
            Line(c + new Vector2(-r, 0), c + new Vector2(0, -r), 2f, col);
        }

        internal static void Brackets(Vector2 c, float r, Color col)
        {
            float l = r * 0.45f;
            foreach (var s in new[] { new Vector2(-1, -1), new Vector2(1, -1), new Vector2(1, 1), new Vector2(-1, 1) })
            {
                Vector2 corner = c + s * r;
                Line(corner, corner - new Vector2(s.x * l, 0), 2.5f, col);
                Line(corner, corner - new Vector2(0, s.y * l), 2.5f, col);
            }
        }

        internal static void Bar(Rect r, float fill, Color col)
        {
            GUI.color = new Color(0f, 0f, 0f, 0.5f);
            GUI.DrawTexture(r, Px);
            GUI.color = col;
            float h = r.height * Mathf.Clamp01(fill);
            GUI.DrawTexture(new Rect(r.x, r.yMax - h, r.width, h), Px);
        }

        private static void BarH(Rect r, float fill, Color col)
        {
            GUI.color = new Color(1f, 1f, 1f, 0.25f);
            GUI.DrawTexture(r, Px);
            GUI.color = col;
            GUI.DrawTexture(new Rect(r.x, r.y, r.width * Mathf.Clamp01(fill), r.height), Px);
        }
    }
}
