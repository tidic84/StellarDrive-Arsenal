using System.Collections.Generic;
using Arsenal.Core;
using Arsenal.Vehicles;
using Arsenal.Visual;
using Ships;
using UnityEngine;

namespace Arsenal.Weapons
{
    /// <summary>
    /// Automatic target lock for guided missiles: picks the ship closest to the aiming axis inside a cone,
    /// builds up a lock while it stays there (blips getting faster), then holds it with a steady tone.
    /// Used by the hand-held SAM launcher and by the ship SAM battery.
    /// </summary>
    internal sealed class LockOn
    {
        public float Cone = 8f;          // degrees
        public float Range = 1600f;      // metres
        public float MinRange = 15f;     // ignores the ship you are standing on / flying
        public float LockTime = 1.1f;    // seconds on target to lock
        public float KeepCone = 18f;     // a locked target is kept while it stays inside this cone
        public System.Func<TrackedShipClient, bool> Filter; // extra condition on targets (null = any ship)

        public uint Candidate { get; private set; }
        public uint Locked { get; private set; }
        public float Progress { get; private set; }
        public Vector3 Origin { get; private set; }

        public const float LosGrace = 0.5f; // a target hidden longer than this (behind a hill, a ship...) is dropped

        private float _nextTone;
        private float _hiddenFor;

        public void Clear()
        {
            Candidate = Locked = 0;
            Progress = 0f;
            _hiddenFor = 0f;
        }

        /// <summary>Runs one frame. <paramref name="excludeRoot"/>: the shooter's own ship (0 = none).</summary>
        public void Update(Vector3 from, Vector3 fwd, uint excludeRoot, float dt, bool sounds)
        {
            Origin = from;
            if (Svc.ShipsClient == null)
            {
                Clear();
                return;
            }
            // keep the current target while it stays in the wider cone and in sight
            uint keep = Locked != 0 ? Locked : Candidate;
            if (keep != 0 && !Valid(keep, from, fwd, excludeRoot, Locked != 0 ? KeepCone : Cone * 1.3f)) keep = 0;
            if (keep != 0)
            {
                _hiddenFor = Seen(keep, from, excludeRoot) ? 0f : _hiddenFor + dt;
                if (_hiddenFor > LosGrace) keep = 0;
            }
            if (keep == 0) _hiddenFor = 0f;
            if (keep == 0)
            {
                keep = Best(from, fwd, excludeRoot);
                if (keep != Candidate) Progress = 0f;
            }
            if (keep == 0)
            {
                Clear();
                return;
            }
            Candidate = keep;
            if (Locked != keep)
            {
                Locked = 0;
                // the lock only builds up while the target is actually visible
                if (_hiddenFor <= 0f) Progress += dt / LockTime;
                if (Progress >= 1f)
                {
                    Progress = 1f;
                    Locked = keep;
                }
            }
            ThreatWarning.ReportLock(keep, Locked != 0, from);
            if (!sounds || Time.time < _nextTone) return;
            Sounds.LockTone(from, Locked != 0);
            _nextTone = Time.time + (Locked != 0 ? 0.11f : Mathf.Lerp(0.45f, 0.12f, Progress));
        }

        private bool Valid(uint id, Vector3 from, Vector3 fwd, uint excludeRoot, float cone)
        {
            if (!Svc.ShipsClient.TryGetTrackedShip(id, out TrackedShipClient s) || !s.IsRigidbodyLoaded) return false;
            if (excludeRoot != 0 && ShipWeapons.RootOf(id) == excludeRoot) return false;
            if (Filter != null && !Filter(s)) return false;
            Vector3 to = ShipWeapons.TargetPoint(s) - from;
            float d = to.magnitude;
            return d >= MinRange && d <= Range && Vector3.Angle(fwd, to) <= cone;
        }

        private static bool Seen(uint id, Vector3 from, uint excludeRoot) =>
            Svc.ShipsClient.TryGetTrackedShip(id, out TrackedShipClient s) && s.IsRigidbodyLoaded && ShipWeapons.CanSee(from, s, excludeRoot);

        private uint Best(Vector3 from, Vector3 fwd, uint excludeRoot)
        {
            uint best = 0;
            float bestScore = float.MaxValue;
            foreach (TrackedShipClient s in Svc.ShipsClient.Ships)
            {
                if (!s.IsRigidbodyLoaded || ShipWeapons.RootOf(s.Id) != s.Id) continue; // whole assemblies only
                if (excludeRoot != 0 && s.Id == excludeRoot) continue;
                if (Filter != null && !Filter(s)) continue;
                Vector3 to = ShipWeapons.TargetPoint(s) - from;
                float d = to.magnitude;
                if (d < MinRange || d > Range) continue;
                float angle = Vector3.Angle(fwd, to);
                if (angle > Cone) continue;
                float score = angle * 30f + d * 0.02f;
                if (score < bestScore && ShipWeapons.CanSee(from, s, excludeRoot))
                {
                    bestScore = score;
                    best = s.Id;
                }
            }
            return best;
        }
    }

    /// <summary>Draws the lock brackets of the local player's lock-ons (registered every frame).</summary>
    internal sealed class LockHud : MonoBehaviour
    {
        private static LockHud _instance;
        private static readonly List<LockOn> Shown = new List<LockOn>();
        private static int _frame;
        private static GUIStyle _label;

        public static void Ensure()
        {
            if (_instance != null) return;
            var go = new GameObject("Arsenal.LockHud");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<LockHud>();
        }

        /// <summary>Show this lock-on this frame.</summary>
        public static void Show(LockOn l)
        {
            if (_frame != Time.frameCount)
            {
                _frame = Time.frameCount;
                Shown.Clear();
            }
            if (!Shown.Contains(l)) Shown.Add(l);
        }

        private void OnGUI()
        {
            if (Event.current.type != EventType.Repaint || _frame < Time.frameCount - 1 || Shown.Count == 0) return;
            Camera cam = Camera.main;
            if (cam == null || Svc.ShipsClient == null) return;
            if (_label == null) _label = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            foreach (LockOn l in Shown)
            {
                if (l.Candidate == 0 || !Svc.ShipsClient.TryGetTrackedShip(l.Candidate, out TrackedShipClient t) || !t.IsRigidbodyLoaded) continue;
                Vector3 p = ShipWeapons.TargetPoint(t);
                Vector3 sp = cam.WorldToScreenPoint(p);
                if (sp.z <= 0) continue;
                var c = new Vector2(sp.x, Screen.height - sp.y);
                bool locked = l.Locked != 0;
                // brackets close in on the target while the lock builds up
                float size = locked ? 24f : Mathf.Lerp(70f, 30f, l.Progress);
                Color col = locked ? new Color(1f, 0.3f, 0.22f, 0.95f) : new Color(1f, 0.85f, 0.3f, 0.6f + 0.35f * Mathf.PingPong(Time.time * 4f, 1f));
                ShipHud.Brackets(c, size, col);
                if (locked) ShipHud.Diamond(c, 7f, col);
                GUI.color = col;
                float dist = Vector3.Distance(l.Origin, p);
                string text = locked ? Loc.T("VERROUILLÉ", "LOCKED") : Loc.T("ACQUISITION", "ACQUIRING");
                GUI.Label(new Rect(c.x - 80f, c.y + size + 6f, 160f, 18f), $"{text}  {dist:0} m", _label);
                if (!locked)
                {
                    GUI.color = new Color(0f, 0f, 0f, 0.5f);
                    GUI.DrawTexture(new Rect(c.x - 30f, c.y + size + 26f, 60f, 4f), ShipHud.Px);
                    GUI.color = col;
                    GUI.DrawTexture(new Rect(c.x - 30f, c.y + size + 26f, 60f * l.Progress, 4f), ShipHud.Px);
                }
            }
            GUI.color = Color.white;
        }
    }
}
