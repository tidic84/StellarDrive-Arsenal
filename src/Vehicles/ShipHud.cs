using Arsenal.Content;
using Arsenal.Core;
using Ships;
using Ships.Interface.Model.Parts;
using UnityEngine;

namespace Arsenal.Vehicles
{
    /// <summary>
    /// Pilot overlay while flying a ship with weapons: where each weapon points (300 m ahead), the missile
    /// lock and missiles left, the laser heat. Drawn in thin ink-like lines to sit with the game's HUD.
    /// </summary>
    internal sealed class ShipHud : MonoBehaviour
    {
        private static Texture2D _px;
        private static GUIStyle _label;

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
            if (!GunnerSeat.TryGetPilotedRoot(out uint root)) return;
            Camera cam = Camera.main;
            if (cam == null) return;
            if (_label == null)
                _label = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };

            foreach (ShipWeapons.Mount m in ShipWeapons.Mounts)
            {
                if (ShipWeapons.RootOf(m.Ctx.ShipId) != root) continue;
                if (!Svc.ShipsClient.TryGetTrackedShip(m.Ctx.ShipId, out TrackedShipClient ship) || !ship.IsRigidbodyLoaded) continue;
                if (!ship.TryGetStatefulPart(m.Ctx.PartId, out StatefulPart part)) continue;
                Vector3 aim = ShipWeapons.PartPoint(ship, part, new Vector3(0, 0.27f, 0.6f)) + ShipWeapons.PartForward(ship, part) * 300f;
                Vector3 sp = cam.WorldToScreenPoint(aim);
                if (sp.z <= 0) continue;
                var c = new Vector2(sp.x, Screen.height - sp.y);
                Color ink = new Color(0.95f, 0.98f, 1f, 0.9f);
                switch (m.Kind)
                {
                    case ShipContent.MachineGunId:
                        Cross(c, 9f, 4f, ink);
                        break;
                    case ShipContent.LaserCannonId:
                    case ShipContent.LaserGreenId:
                    case ShipContent.LaserBlueId:
                        Ring(c, 10f, ink);
                        Bar(new Rect(c.x + 16f, c.y - 10f, 5f, 20f), m.Heat, m.Overheated ? new Color(1f, 0.35f, 0.2f) : new Color(1f, 0.75f, 0.3f));
                        break;
                    case ShipContent.PlasmaCannonId:
                    case ShipContent.IonCannonId:
                        Cross(c, 12f, 6f, m.Kind == ShipContent.PlasmaCannonId ? new Color(1f, 0.7f, 0.4f, 0.9f) : new Color(0.6f, 0.8f, 1f, 0.9f));
                        break;
                    case ShipContent.RailgunId:
                        Cross(c, 16f, 10f, ink);
                        Ring(c, 5f, ink);
                        Bar(new Rect(c.x + 22f, c.y - 12f, 5f, 24f), Time.time < m.NextShot ? 1f - (m.NextShot - Time.time) / 4f : m.Charge,
                            Time.time < m.NextShot ? new Color(0.5f, 0.55f, 0.6f) : new Color(0.5f, 0.85f, 1f));
                        break;
                    case ShipContent.RocketPodId:
                        Cross(c, 14f, 8f, new Color(1f, 0.6f, 0.35f, 0.9f));
                        GUI.color = ink;
                        GUI.Label(new Rect(c.x + 16f, c.y - 10f, 120f, 20f), m.Rockets > 0 ? new string('|', m.Rockets) : Loc.T("RECHARGE", "RELOADING"), _label);
                        break;
                    case ShipContent.SamBatteryId:
                    {
                        // the battery aims by itself: show where its launcher points and the missiles left
                        if (m.Pitch == null) continue;
                        Vector3 sp2 = cam.WorldToScreenPoint(m.Pitch.position + m.Pitch.forward * 300f);
                        if (sp2.z <= 0) continue;
                        var c2 = new Vector2(sp2.x, Screen.height - sp2.y);
                        Ring(c2, 6f, new Color(1f, 0.85f, 0.3f, 0.8f));
                        GUI.color = ink;
                        GUI.Label(new Rect(c2.x + 12f, c2.y - 10f, 80f, 20f), new string('|', m.Missiles), _label);
                        continue;
                    }
                    case ShipContent.FlareDispenserId:
                    {
                        // no aim: salvos left, under the screen centre
                        var fc = new Vector2(Screen.width / 2f, Screen.height - 120f);
                        GUI.color = ink;
                        string salvos = m.Missiles > 0 ? new string('|', m.Missiles) : Loc.T("RECHARGE", "RELOADING");
                        GUI.Label(new Rect(fc.x - 90f, fc.y, 180f, 20f), Loc.T("LEURRES ", "FLARES ") + salvos, _label);
                        continue;
                    }
                    case ShipContent.PdcId:
                        // point defence aims by itself: show what it is tracking
                        if (m.Tracking != null && !(m.Tracking is Object o && o == null))
                        {
                            Vector3 tp = cam.WorldToScreenPoint(m.Tracking.Position);
                            if (tp.z > 0) Brackets(new Vector2(tp.x, Screen.height - tp.y), 14f, new Color(1f, 0.85f, 0.3f, 0.95f));
                        }
                        continue;
                    case ShipContent.MissilePodId:
                    case ShipContent.TorpedoId:
                    case ShipContent.HyperLauncherId:
                        Diamond(c, 8f, ink);
                        GUI.color = ink;
                        GUI.Label(new Rect(c.x + 14f, c.y - 10f, 80f, 20f), new string('|', m.Missiles), _label);
                        if (m.LockedTarget != 0 && ShipWeapons.TryGetShipMotion(m.LockedTarget, out Vector3 tcenter, out _, out _, out _))
                        {
                            Vector3 tp = cam.WorldToScreenPoint(tcenter);
                            if (tp.z > 0)
                            {
                                var tc = new Vector2(tp.x, Screen.height - tp.y);
                                Color lockCol = new Color(1f, 0.35f, 0.25f, 0.95f);
                                Brackets(tc, 26f, lockCol);
                                GUI.color = lockCol;
                                float range = Vector3.Distance(ShipWeapons.TargetPoint(ship), tcenter);
                                GUI.Label(new Rect(tc.x - 30f, tc.y + 28f, 200f, 20f), Loc.T("VERROUILLÉ", "LOCKED") + (range >= 1000f ? $"  {range / 1000f:0.0} km" : $"  {range:0} m"), _label);
                            }
                        }
                        break;
                }
            }
            GUI.color = Color.white;
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
    }
}
