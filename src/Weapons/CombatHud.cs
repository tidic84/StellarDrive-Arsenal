using System.Collections.Generic;
using System.IO;
using Arsenal.Core;
using Arsenal.Net;
using Arsenal.Ui;
using Arsenal.Visual;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using WorldTracking.Interface.Values;

namespace Arsenal.Weapons
{
    /// <summary>
    /// PvP feedback: hit markers for the shooter, a damage direction indicator for the victim, kill
    /// confirmation, a kill feed, and overlays for burning / jammed states.
    /// </summary>
    internal sealed class CombatHud : MonoBehaviour
    {
        private struct Indicator
        {
            public Vector3 From; // in local physics space
            public bool HasSource;
            public float Time;
            public float Damage;
        }

        private static CombatHud _instance;
        private static float _hitTime = -10f, _killTime = -10f;
        private static bool _headshot;
        private static string _killName = "";
        private static readonly List<Indicator> Indicators = new List<Indicator>();
        private static float _hurtFlash;
        private static Texture2D _px, _ring;

        private Canvas _canvas;
        private RectTransform _feed;
        private readonly List<(RectTransform row, float until)> _rows = new List<(RectTransform, float)>();

        public static void Init()
        {
            ArsenalNet.OnClient(Op.Hurt, r =>
            {
                float dmg = r.ReadSingle();
                bool show = r.ReadBoolean();
                uint space = r.ReadUInt32();
                Vector3 from = FxNet.ToLocal(space, r.ReadVector3());
                Indicators.Add(new Indicator { From = from, HasSource = show, Time = Time.time, Damage = dmg });
                if (Indicators.Count > 6) Indicators.RemoveAt(0);
                _hurtFlash = Mathf.Min(1f, _hurtFlash + dmg / 40f);
                Sounds.Hurt();
            });
            ArsenalNet.OnClient(Op.KillConfirm, r =>
            {
                _killName = r.ReadString();
                _killTime = Time.time;
                Sounds.HitTick(true, false);
            });
            ArsenalNet.OnClient(Op.KillFeed, r =>
            {
                string killer = r.ReadString();
                string victim = r.ReadString();
                var weapon = (WeaponKind)r.ReadByte();
                Ensure()?.AddFeed(killer, victim, weapon);
            });
        }

        public static CombatHud Ensure()
        {
            if (_instance != null) return _instance;
            var go = new GameObject("Arsenal.CombatHud");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<CombatHud>();
            return _instance;
        }

        public static void Reset()
        {
            Indicators.Clear();
            _hurtFlash = 0f;
            if (_instance != null)
            {
                foreach (var (row, _) in _instance._rows)
                    if (row != null) Destroy(row.gameObject);
                _instance._rows.Clear();
                if (_instance._canvas != null) Destroy(_instance._canvas.gameObject);
                _instance._canvas = null;
            }
        }

        /// <summary>The local player's shot hit someone.</summary>
        public static void Hit(bool headshot)
        {
            Ensure();
            _hitTime = Time.time;
            _headshot = headshot;
            Sounds.HitTick(false, headshot);
        }

        // ------------------------------------------------------------------ kill feed (uGUI, game fonts and frames)

        private void AddFeed(string killer, string victim, WeaponKind weapon)
        {
            if (_canvas == null)
            {
                _canvas = UiKit.MakeCanvas("Arsenal.KillFeed", 5);
                _feed = UiKit.Rect(_canvas.transform, "Feed", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-80f, -330f), new Vector2(1100f, 600f), new Vector2(1f, 1f));
            }
            string weaponName = WeaponDefs.ByKind(weapon)?.Name ?? ShipWeaponName(weapon);
            RectTransform row = UiKit.Rect(_feed, "Row", new Vector2(1f, 1f), new Vector2(1f, 1f), Vector2.zero, new Vector2(1100f, 84f), new Vector2(1f, 1f));
            UiKit.Image(row, "unit_bg", new Color(UiKit.Panel.r, UiKit.Panel.g, UiKit.Panel.b, 0.82f), 2f);
            RectTransform accent = UiKit.Rect(row, "Accent", new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-4f, 0f), new Vector2(8f, -16f), new Vector2(1f, 0.5f));
            UiKit.Image(accent, null, UiKit.Accent, 1f);
            RectTransform txt = UiKit.Fill(row, "Text");
            txt.offsetMin = new Vector2(24f, 0f);
            txt.offsetMax = new Vector2(-30f, 0f);
            string acc = ColorUtility.ToHtmlStringRGB(UiKit.Accent);
            string warn = ColorUtility.ToHtmlStringRGB(UiKit.Warning);
            string line = string.IsNullOrEmpty(killer)
                ? $"<color=#{warn}>{victim}</color>  <color=#{acc}>[{weaponName}]</color>"
                : $"{killer}  <color=#{acc}>[{weaponName}]</color>  <color=#{warn}>{victim}</color>";
            TextMeshProUGUI t = UiKit.Label(txt, line, 40, UiKit.Text, true, TextAlignmentOptions.Right);
            t.richText = true;
            _rows.Insert(0, (row, Time.time + 7f));
            Relayout();
        }

        private static string ShipWeaponName(WeaponKind w) => w switch
        {
            WeaponKind.ShipGun => Loc.T("Mitrailleuse lourde", "Heavy machine gun"),
            WeaponKind.ShipMissile => Loc.T("Missile", "Missile"),
            WeaponKind.ShipLaser => Loc.T("Canon laser", "Laser cannon"),
            WeaponKind.PulseTurret => "L-20",
            WeaponKind.PlasmaCannon => "P-400",
            WeaponKind.ShipRailgun => "Longinus",
            WeaponKind.Torpedo => "Leviathan",
            WeaponKind.HyperMissile => Loc.T("Foudre", "Lightning"),
            WeaponKind.IonCannon => "Disruptor",
            WeaponKind.Pdc => "Guardian",
            WeaponKind.Capital => "Oblivion",
            WeaponKind.Grenade => Loc.T("Grenade", "Grenade"),
            WeaponKind.Charge => Loc.T("Charge", "Charge"),
            _ => "?",
        };

        private void Relayout()
        {
            for (int i = 0; i < _rows.Count; i++)
                if (_rows[i].row != null) _rows[i].row.anchoredPosition = new Vector2(0f, -i * 96f);
        }

        private void Update()
        {
            bool changed = false;
            for (int i = _rows.Count - 1; i >= 0; i--)
            {
                if (Time.time < _rows[i].until && i < 6) continue;
                if (_rows[i].row != null) Destroy(_rows[i].row.gameObject);
                _rows.RemoveAt(i);
                changed = true;
            }
            if (changed) Relayout();
            _hurtFlash = Mathf.MoveTowards(_hurtFlash, 0f, Time.deltaTime * 1.5f);
        }

        // ------------------------------------------------------------------ markers and overlays (IMGUI)

        private static Texture2D Px
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

        /// <summary>Soft red frame used for the hurt flash and burning.</summary>
        private static Texture2D Vignette
        {
            get
            {
                if (_ring != null) return _ring;
                const int s = 128;
                _ring = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
                for (int y = 0; y < s; y++)
                    for (int x = 0; x < s; x++)
                    {
                        float dx = (x - s / 2f) / (s / 2f), dy = (y - s / 2f) / (s / 2f);
                        float d = Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy));
                        float a = Mathf.Clamp01((d - 0.55f) / 0.45f);
                        _ring.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
                    }
                _ring.Apply();
                return _ring;
            }
        }

        private static void Line(Vector2 a, Vector2 b, float width, Color c)
        {
            GUI.color = c;
            Vector2 d = b - a;
            float angle = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
            Matrix4x4 m = GUI.matrix;
            GUIUtility.RotateAroundPivot(angle, a);
            GUI.DrawTexture(new Rect(a.x, a.y - width / 2f, d.magnitude, width), Px);
            GUI.matrix = m;
        }

        private static GUIStyle _jamStyle, _killStyle;

        /// <summary>A bold centred label, made once and resized with the screen.</summary>
        private static GUIStyle Label(ref GUIStyle style, int size)
        {
            if (style == null) style = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            style.fontSize = size;
            return style;
        }

        private void OnGUI()
        {
            if (!ArsenalMod.InGame || Event.current.type != EventType.Repaint) return;
            float s = Screen.height / 720f;
            var c = new Vector2(Screen.width / 2f, Screen.height / 2f);

            // overlays
            if (_hurtFlash > 0.01f)
            {
                GUI.color = new Color(0.85f, 0.1f, 0.05f, _hurtFlash * 0.55f);
                GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Vignette);
            }
            if (Effects.LocalBurning)
            {
                GUI.color = new Color(1f, 0.45f, 0.1f, 0.35f + Mathf.Sin(Time.time * 18f) * 0.08f);
                GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Vignette);
            }
            if (Effects.LocalJammed)
            {
                // scrambled scanlines
                for (int i = 0; i < 26; i++)
                {
                    float y = Random.value * Screen.height;
                    GUI.color = new Color(0.4f, 0.65f, 1f, Random.Range(0.05f, 0.22f));
                    GUI.DrawTexture(new Rect(0, y, Screen.width, Random.Range(1f, 4f) * s), Px);
                }
                GUI.color = new Color(0.55f, 0.75f, 1f, 0.9f);
                GUIStyle style = Label(ref _jamStyle, Mathf.RoundToInt(16 * s));
                GUI.Label(new Rect(c.x - 200 * s, c.y + 60 * s, 400 * s, 30 * s), Loc.T($"ÉQUIPEMENT BROUILLÉ  {Effects.LocalJamLeft:0.0}s", $"GEAR JAMMED  {Effects.LocalJamLeft:0.0}s"), style);
            }

            // damage direction indicators: an arc segment around the crosshair pointing at the source
            Camera cam = Camera.main;
            for (int i = Indicators.Count - 1; i >= 0; i--)
            {
                Indicator ind = Indicators[i];
                float age = Time.time - ind.Time;
                if (age > 2f)
                {
                    Indicators.RemoveAt(i);
                    continue;
                }
                if (!ind.HasSource || cam == null) continue;
                Vector3 to = ind.From - cam.transform.position;
                float x = Vector3.Dot(to, cam.transform.right), z = Vector3.Dot(to, cam.transform.forward);
                float ang = Mathf.Atan2(x, z); // 0 = in front
                float alpha = Mathf.Clamp01(1f - age / 2f) * Mathf.Clamp(0.4f + ind.Damage / 40f, 0.4f, 1f);
                float r0 = 90f * s, half = 0.32f;
                Color col = new Color(1f, 0.18f, 0.12f, alpha);
                for (int k = 0; k < 6; k++)
                {
                    float a0 = ang - half + k * (half * 2f / 6f), a1 = a0 + half * 2f / 6f;
                    Vector2 p0 = c + new Vector2(Mathf.Sin(a0), -Mathf.Cos(a0)) * r0;
                    Vector2 p1 = c + new Vector2(Mathf.Sin(a1), -Mathf.Cos(a1)) * r0;
                    Line(p0, p1, 6f * s, col);
                }
                Vector2 tip = c + new Vector2(Mathf.Sin(ang), -Mathf.Cos(ang)) * (r0 + 14f * s);
                Vector2 b0 = c + new Vector2(Mathf.Sin(ang - 0.06f), -Mathf.Cos(ang - 0.06f)) * (r0 + 2f * s);
                Vector2 b1 = c + new Vector2(Mathf.Sin(ang + 0.06f), -Mathf.Cos(ang + 0.06f)) * (r0 + 2f * s);
                Line(b0, tip, 4f * s, col);
                Line(b1, tip, 4f * s, col);
            }

            // hit marker
            float hitAge = Time.time - _hitTime, killAge = Time.time - _killTime;
            bool kill = killAge < 0.6f;
            if (hitAge < 0.25f || kill)
            {
                float a = kill ? Mathf.Clamp01(1f - killAge / 0.6f) : Mathf.Clamp01(1f - hitAge / 0.25f);
                Color col = kill ? new Color(1f, 0.2f, 0.15f, a) : _headshot ? new Color(1f, 0.65f, 0.2f, a) : new Color(1f, 1f, 1f, a);
                float inner = (kill ? 9f : 6f) * s, outer = (kill ? 22f : 14f) * s, w = (kill ? 3.5f : 2.5f) * s;
                foreach (var dir in new[] { new Vector2(1, 1), new Vector2(-1, 1), new Vector2(1, -1), new Vector2(-1, -1) })
                {
                    Vector2 n = dir.normalized;
                    Line(c + n * inner, c + n * outer, w, col);
                }
            }
            if (killAge < 1.6f && !string.IsNullOrEmpty(_killName))
            {
                float a = Mathf.Clamp01(1.6f - killAge);
                GUI.color = new Color(1f, 0.35f, 0.25f, a);
                GUIStyle style = Label(ref _killStyle, Mathf.RoundToInt(15 * s));
                GUI.Label(new Rect(c.x - 250 * s, c.y + 34 * s, 500 * s, 26 * s), Loc.T("ÉLIMINÉ : ", "ELIMINATED: ") + _killName, style);
            }
            GUI.color = Color.white;
        }
    }
}
