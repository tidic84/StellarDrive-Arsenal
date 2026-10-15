using System.Collections.Generic;
using System.IO;
using Arsenal.Core;
using Arsenal.Net;
using Arsenal.Visual;
using Arsenal.Weapons;
using FishNet.Connection;
using Ships;
using UnityEngine;
using WorldTracking.Interface.Values;

namespace Arsenal.Vehicles
{
    /// <summary>
    /// Radar warning for the pilot: beeps and on-screen markers when a lock-on (hand SAM launcher, SAM battery,
    /// missile pod, torpedo tube) builds up or holds on the piloted ship, and an alarm when a guided missile
    /// is homing on it. Every client reports the locks it runs a few times a second; the server relays them.
    /// </summary>
    internal sealed class ThreatWarning : MonoBehaviour
    {
        private enum Level : byte { None = 0, Search = 1, Locked = 2, Missile = 3 }

        private struct LockEntry
        {
            public uint Target;
            public byte Level;
            public Vector3 From;
        }

        private sealed class Report
        {
            public readonly List<LockEntry> Entries = new List<LockEntry>();
            public float Until;
        }

        private const float SendEvery = 0.25f;
        private const float ReportLife = 0.7f;

#if DEV
        public static uint DevWatch; // warn as if piloting this ship
#endif

        private static ThreatWarning _instance;
        private static readonly Dictionary<uint, LockEntry> Pending = new Dictionary<uint, LockEntry>(); // this client's locks since the last send
        private static readonly Report Local = new Report();
        private static readonly Dictionary<int, Report> Remote = new Dictionary<int, Report>();
        private static float _nextSend;
        private static bool _sentSomething;

        // what the pilot is warned about this frame
        private static Level _level;
        private static readonly List<Vector3> LockSources = new List<Vector3>();
        private static readonly List<Vector3> LockedSources = new List<Vector3>();
        private static readonly List<Vector3> MissilePositions = new List<Vector3>();
        private static float _missileDist;
        private static float _nextTone;
        private static bool _alt;
        private static GUIStyle _label;

        public static void Init()
        {
            ArsenalNet.OnServer(Op.LockReport, (conn, r) =>
            {
                byte[] all = ((MemoryStream)r.BaseStream).ToArray();
                ArsenalNet.SendToAllExcept(conn, Op.LockWarn, w =>
                {
                    w.Write(conn.ClientId);
                    w.Write(all);
                });
            });
            ArsenalNet.OnClient(Op.LockWarn, ClientOnWarn);
        }

        public static void Ensure()
        {
            if (_instance != null) return;
            var go = new GameObject("Arsenal.ThreatWarning");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<ThreatWarning>();
        }

        public static void Reset()
        {
            Pending.Clear();
            Local.Entries.Clear();
            Remote.Clear();
            _level = Level.None;
            _sentSomething = false;
        }

        /// <summary>A lock-on run by this client is on <paramref name="target"/> this frame (building up, or held).</summary>
        public static void ReportLock(uint target, bool locked, Vector3 from)
        {
            if (target == 0 || Svc.ShipsClient == null) return;
            uint root = ShipWeapons.RootOf(target);
            byte level = locked ? (byte)Level.Locked : (byte)Level.Search;
            if (Pending.TryGetValue(root, out LockEntry e) && e.Level >= level) return;
            Pending[root] = new LockEntry { Target = root, Level = level, From = from };
        }

        // ------------------------------------------------------------------ network

        private static void Flush()
        {
            if (Time.time < _nextSend) return;
            _nextSend = Time.time + SendEvery;
            Local.Entries.Clear();
            Local.Entries.AddRange(Pending.Values);
            Local.Until = Time.time + ReportLife;
            Pending.Clear();
            // empty reports only to clear what the others still show
            if (Local.Entries.Count == 0 && !_sentSomething) return;
            _sentSomething = Local.Entries.Count > 0;
            uint space = ClientOrigin.LocalPhysicsSpaceId;
            ArsenalNet.SendToServer(Op.LockReport, w =>
            {
                w.Write(space);
                w.Write((byte)Mathf.Min(Local.Entries.Count, 255));
                for (int i = 0; i < Local.Entries.Count && i < 255; i++)
                {
                    w.Write(Local.Entries[i].Target);
                    w.Write(Local.Entries[i].Level);
                    w.Write(Local.Entries[i].From);
                }
            }, FishNet.Transporting.Channel.Unreliable);
        }

        private static void ClientOnWarn(BinaryReader r)
        {
            int sender = r.ReadInt32();
            uint space = r.ReadUInt32();
            int n = r.ReadByte();
            if (!Remote.TryGetValue(sender, out Report rep)) Remote[sender] = rep = new Report();
            rep.Entries.Clear();
            rep.Until = Time.time + ReportLife;
            for (int i = 0; i < n; i++)
            {
                uint target = r.ReadUInt32();
                byte level = r.ReadByte();
                Vector3 from = FxNet.ToLocal(space, r.ReadVector3());
                rep.Entries.Add(new LockEntry { Target = target, Level = level, From = from });
            }
        }

        // ------------------------------------------------------------------ evaluation

        private void Update()
        {
            if (!ArsenalMod.InGame || Svc.ShipsClient == null) return;
            Flush();
            Level before = _level;
            _level = Level.None;
            LockSources.Clear();
            LockedSources.Clear();
            MissilePositions.Clear();
            _missileDist = float.MaxValue;
            if (!Pilot.TryGetPilotedRoot(out uint me))
            {
#if DEV
                if (DevWatch == 0) return;
                me = DevWatch;
#else
                return;
#endif
            }

            Collect(Local, me);
            foreach (Report rep in Remote.Values) Collect(rep, me);

            Vector3 here = Camera.main != null ? Camera.main.transform.position : Vector3.zero;
            foreach (IThreat t in Projectiles.Threats)
            {
                if (t == null || (t is Object o && o == null)) continue;
                uint target = t.HomingTarget;
                if (target == 0 || t.ShooterRoot == me || ShipWeapons.RootOf(target) != me) continue;
                MissilePositions.Add(t.Position);
                _missileDist = Mathf.Min(_missileDist, Vector3.Distance(t.Position, here));
            }
            if (MissilePositions.Count > 0) _level = Level.Missile;
            else if (LockedSources.Count > 0) _level = Level.Locked;
            else if (LockSources.Count > 0) _level = Level.Search;
#if DEV
            if (_level != before) ArsenalMod.Log.Msg($"[DEV] RWR {before} -> {_level} (locks {LockSources.Count}/{LockedSources.Count}, missiles {MissilePositions.Count})");
#endif
            if (_level != before) _nextTone = 0f;
            Beep();
        }

        private static void Collect(Report rep, uint me)
        {
            if (Time.time > rep.Until) return;
            foreach (LockEntry e in rep.Entries)
            {
                if (ShipWeapons.RootOf(e.Target) != me) continue;
                if (e.Level >= (byte)Level.Locked) LockedSources.Add(e.From);
                else LockSources.Add(e.From);
            }
        }

        private static void Beep()
        {
            if (_level == Level.None || Time.time < _nextTone) return;
            switch (_level)
            {
                case Level.Search:
                    Sounds.Warning(WarningTone.Search);
                    _nextTone = Time.time + 0.9f;
                    break;
                case Level.Locked:
                    Sounds.Warning(WarningTone.Locked);
                    _nextTone = Time.time + 0.2f;
                    break;
                case Level.Missile:
                    // faster as it closes in
                    _alt = !_alt;
                    Sounds.Warning(WarningTone.Missile, _alt);
                    _nextTone = Time.time + Mathf.Lerp(0.08f, 0.22f, Mathf.InverseLerp(80f, 900f, _missileDist));
                    break;
            }
        }

        // ------------------------------------------------------------------ HUD

        private void OnGUI()
        {
            if (!ArsenalMod.InGame || Event.current.type != EventType.Repaint || _level == Level.None) return;
            Camera cam = Camera.main;
            if (cam == null) return;
            if (_label == null) _label = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            bool blink = Mathf.PingPong(Time.time * (_level == Level.Missile ? 6f : 3f), 1f) > 0.35f;
            Color red = new Color(1f, 0.25f, 0.18f, 0.95f);
            Color amber = new Color(1f, 0.8f, 0.3f, 0.9f);
            string text;
            Color col;
            switch (_level)
            {
                case Level.Missile:
                    text = Loc.T("MISSILE ENTRANT", "INCOMING MISSILE") + $"  {_missileDist:0} m";
                    col = red;
                    break;
                case Level.Locked:
                    text = Loc.T("VERROUILLÉ PAR L'ENNEMI", "ENEMY LOCK");
                    col = red;
                    break;
                default:
                    text = Loc.T("RADAR ENNEMI", "ENEMY RADAR");
                    col = amber;
                    break;
            }
            if (_level == Level.Search || blink)
            {
                GUI.color = new Color(0f, 0f, 0f, 0.45f);
                GUI.DrawTexture(new Rect(Screen.width / 2f - 170f, 70f, 340f, 30f), ShipHud.Px);
                GUI.color = col;
                GUI.Label(new Rect(Screen.width / 2f - 170f, 70f, 340f, 30f), text, _label);
            }

            // where it comes from: markers on a ring around the screen centre
            var c = new Vector2(Screen.width / 2f, Screen.height / 2f);
            float ring = Mathf.Min(Screen.width, Screen.height) * 0.32f;
            foreach (Vector3 p in LockSources) Marker(cam, c, ring, p, amber, false);
            foreach (Vector3 p in LockedSources) Marker(cam, c, ring, p, red, false);
            foreach (Vector3 p in MissilePositions) Marker(cam, c, ring, p, blink ? red : new Color(1f, 0.6f, 0.5f, 0.9f), true);
            GUI.color = Color.white;
        }

        private static void Marker(Camera cam, Vector2 c, float ring, Vector3 world, Color col, bool missile)
        {
            Vector3 local = cam.transform.InverseTransformPoint(world);
            var d = new Vector2(local.x, -local.y);
            if (d.sqrMagnitude < 0.0001f) d = Vector2.down;
            d.Normalize();
            Vector2 p = c + d * ring;
            if (missile)
            {
                // arrow head pointing outwards, towards the missile
                Vector2 side = new Vector2(-d.y, d.x) * 9f;
                ShipHud.Line(p + d * 12f, p - d * 6f + side, 3f, col);
                ShipHud.Line(p + d * 12f, p - d * 6f - side, 3f, col);
                ShipHud.Line(p - d * 6f + side, p - d * 6f - side, 3f, col);
                if (local.z > 0f)
                {
                    // and brackets on the missile itself when it is in view
                    Vector3 sp = cam.WorldToScreenPoint(world);
                    ShipHud.Brackets(new Vector2(sp.x, Screen.height - sp.y), 12f, col);
                }
            }
            else ShipHud.Diamond(p, 8f, col);
        }
    }
}
