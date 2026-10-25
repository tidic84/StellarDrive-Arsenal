using System.Collections.Generic;
using Arsenal.Content;
using Arsenal.Core;
using Arsenal.Weapons;
using HarmonyLib;
using Managers.Interface.Model;
using Managers.Interface.Services;
using Players;
using Players.Interface.Model;
using Players.LocalClient.Controls.Capture;
using Players.Visuals.Camera;
using Ships;
using Ships.Cables;
using Ships.Interface.Model.Parts;
using Ships.Interface.Model.Parts.Common;
using Ships.Interface.Model.Parts.StateTypes;
using UnityEngine;
using UnityEngine.InputSystem;
using WorldTracking.Interface.Values;

namespace Arsenal.Vehicles
{
    /// <summary>
    /// The fire-control station: a seat (a copy of the game's basic pilot seat) from which a gunner mans the
    /// ship's rotating turrets, wherever they are on the ship, even from behind a wall. Seated there, the view
    /// moves to the selected turret, the mouse turns it (every turret the station controls aims at the same
    /// point), left click fires them, Q / E switch the view to the previous / next turret and the wheel zooms.
    ///
    /// The seat never flies the ship: it sends no flight inputs and the host keeps it "not in use" (so its
    /// gyroscope and the ship's flight orientation ignore it; a second player is still kept out of it). Its two
    /// signal outputs carry the triggers instead: socket 1 = left click (fire), socket 0 = right click (fire 2),
    /// to wire fixed cannons or missile launchers to the gunner.
    ///
    /// Which turrets a station controls: wire one of its outputs to a turret's input (directly or through a splitter)
    /// and that turret answers that station only, so several gunners can each man their own guns. Turrets wired to no
    /// station answer the nearest manned station that has none wired to it.
    /// </summary>
    internal static class GunnerStation
    {
        // ------------------------------------------------------------------ who mans what

        private struct Seat
        {
            public uint Root;
            public PartContext Station;
            public Vector3 Pos;
            public bool Local;
        }

        private static readonly List<Seat> Seats = new List<Seat>();
        private static int _seatsFrame = -1;

        public static bool IsStation(PlayerState st)
        {
            if (st.Type != PlayerStateType.Piloting || Svc.ShipsClient == null) return false;
            return Svc.ShipsClient.TryGetTrackedShip(st.EntityId, out TrackedShipClient s) && s.TryGetStatefulPart(st.PartId, out StatefulPart p)
                && p.Settings != null && p.Settings.id == ShipContent.GunnerStationId;
        }

        private static void RefreshSeats()
        {
            if (_seatsFrame == Time.frameCount) return;
            _seatsFrame = Time.frameCount;
            Seats.Clear();
            if (Svc.PlayersClient == null || Svc.ShipsClient == null) return;
            if (Svc.PlayersClient.TryGetLocalPlayerState(out PlayerState mine)) AddSeat(mine, true);
            foreach (TrackedPlayerOtherClient other in Svc.PlayersClient.OtherPlayers) AddSeat(other.State, false);
        }

        private static void AddSeat(PlayerState st, bool local)
        {
            if (!IsStation(st) || !Svc.ShipsClient.TryGetTrackedShip(st.EntityId, out TrackedShipClient ship) || !ship.IsRigidbodyLoaded) return;
            if (!ship.TryGetStatefulPart(st.PartId, out StatefulPart part)) return;
            Seats.Add(new Seat { Root = ShipWeapons.RootOf(ship.Id), Station = new PartContext(ship.Id, part.Id), Pos = ShipWeapons.PartPoint(ship, part, Vector3.zero), Local = local });
        }

        // which turrets are wired to which station: a cable from one of the station's outputs to the turret's input
        private static readonly Dictionary<PartContext, PartContext> WiredTo = new Dictionary<PartContext, PartContext>();
        private static readonly HashSet<PartContext> WiredStations = new HashSet<PartContext>();
        private static readonly Dictionary<uint, PartContext> StationGroups = new Dictionary<uint, PartContext>();
        private static float _wiringAt;
        private static ConnectionsClientTracker _connections;

        /// <summary>
        /// Every half second: the signal groups of every station's two outputs, then each turret whose input is in one
        /// of them. A cable, or a chain of them through splitters, puts both sockets in the same group.
        /// </summary>
        private static void RefreshWiring()
        {
            if (Time.time < _wiringAt) return;
            _wiringAt = Time.time + 0.5f;
            WiredTo.Clear();
            WiredStations.Clear();
            StationGroups.Clear();
            if (_connections == null) _connections = Svc.Get<ConnectionsClientTracker>();
            if (_connections == null || Svc.ShipsClient == null) return;
            foreach (TrackedShipClient ship in Svc.ShipsClient.Ships)
                foreach (StatefulPart part in ship.StatefulParts)
                {
                    if (part.Settings == null || part.Settings.id != ShipContent.GunnerStationId) continue;
                    for (byte socket = 0; socket < 2; socket++)
                        if (_connections.TryGetSignalGroupId(new EntityPartSocket(ship.Id, part.Id, socket), out uint group))
                            StationGroups[group] = new PartContext(ship.Id, part.Id);
                }
            if (StationGroups.Count == 0) return;
            foreach (ShipWeapons.Mount m in ShipWeapons.Mounts)
            {
                if (!Turrets.IsTurret(m.Kind)) continue;
                if (_connections.TryGetSignalGroupId(new EntityPartSocket(m.Ctx.ShipId, m.Ctx.PartId, ShipContent.WeaponSocketId), out uint group)
                    && StationGroups.TryGetValue(group, out PartContext station))
                {
                    WiredTo[m.Ctx] = station;
                    WiredStations.Add(station);
                }
            }
        }

        /// <summary>The station this turret is wired to, if any.</summary>
        public static bool TryGetWiredStation(ShipWeapons.Mount m, out PartContext station)
        {
            RefreshWiring();
            return WiredTo.TryGetValue(m.Ctx, out station);
        }

        /// <summary>
        /// Is a gunner at the controls of this turret, and is it us? A turret wired to a station answers that station
        /// only (nobody there: nobody controls it). An unwired turret answers the nearest manned station that has no
        /// turret wired to it — so with no cables at all, one station still controls every turret of the ship.
        /// </summary>
        public static bool TryGetGunner(ShipWeapons.Mount m, uint root, out bool local)
        {
            RefreshSeats();
            RefreshWiring();
            local = false;
            if (Seats.Count == 0 || m.Visual == null) return false;
            if (WiredTo.TryGetValue(m.Ctx, out PartContext wired))
            {
                foreach (Seat s in Seats)
                    if (s.Station.Equals(wired))
                    {
                        local = s.Local;
                        return true;
                    }
                return false;
            }
            float best = float.MaxValue;
            bool found = false;
            foreach (Seat s in Seats)
            {
                if (s.Root != root || WiredStations.Contains(s.Station)) continue;
                float d = (s.Pos - m.Visual.position).sqrMagnitude;
                if (d >= best) continue;
                best = d;
                local = s.Local;
                found = true;
            }
            return found;
        }

        // ------------------------------------------------------------------ the local gunner

        public static bool Active { get; private set; }
        public static uint Root { get; private set; }
        /// <summary>Where every turret this station controls is aiming (client physics space).</summary>
        public static Vector3 AimPoint { get; private set; }
        public static bool Trigger { get; private set; }
        public static bool Trigger2 { get; private set; }
        public static ShipWeapons.Mount Selected { get; private set; }
        public static readonly List<ShipWeapons.Mount> Controlled = new List<ShipWeapons.Mount>();
        public static int ZoomLevel { get; private set; }
        public static float AimDistance { get; private set; }
        /// <summary>Some turrets are wired to the station we sit at (it controls those only).</summary>
        public static bool StationWired => Active && WiredStations.Contains(_station);
        private static PartContext _station;

        private static readonly float[] Zooms = { 1f, 2.5f, 6f };
        private static float _zoom = 1f;           // the magnification shown, easing towards the chosen level
        private static float _yaw, _elevation;     // the view, in the selected turret's mount frame
        private static float _shake;
        private static float _camFrac = 1f;        // how far out the eye may go (1 = all the way, less when something is in the way)
        private static int _camFrame = -1;
        private const float CamRadius = 0.25f;
        private static Camera _main, _distant;
        private static float _mainFov, _distantFov;
        private static readonly RaycastHit[] Hits = new RaycastHit[32];

        public static void Reset()
        {
            if (Active) Leave();
            Seats.Clear();
            _seatsFrame = -1;
        }

#if DEV
        private static bool _devFire;

        public static void DevView(float yaw, float elevation, bool fire, int select)
        {
            if (select >= 0 && select < Controlled.Count) Select(Controlled[select], true);
            _yaw = yaw;
            _elevation = elevation;
            _devFire = fire;
            ArsenalMod.Log.Msg($"[DEV] gunner: {Controlled.Count} turrets, selected {(Selected != null ? Turrets.Get(Selected.Kind).HudName : "none")}, aim {AimPoint} at {AimDistance:0} m, eye out {_camFrac:F2}, camera {(Camera.main != null && Selected != null ? Vector3.Distance(Camera.main.transform.position, Selected.Pitch.position).ToString("0.0") + " m from the turret" : "?")}");
        }

        public static void DevAimAt(Vector3 point, bool fire)
        {
            if (Selected == null) return;
            Vector3 local = Selected.Head.parent.InverseTransformDirection(point - Selected.Pitch.position);
            _yaw = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
            _elevation = Mathf.Asin(Mathf.Clamp(local.normalized.y, -1f, 1f)) * Mathf.Rad2Deg;
            _devFire = fire;
            ArsenalMod.Log.Msg($"[DEV] gunner aims at {point} ({_yaw:0} / {_elevation:0})");
        }

        public static void DevZoom(int level) => ZoomLevel = Mathf.Clamp(level, 0, Zooms.Length - 1);
#endif


        /// <summary>Called by <see cref="ShipWeapons"/> before it runs the turrets.</summary>
        public static void Tick()
        {
            bool seated = Svc.PlayersClient != null && Svc.PlayersClient.TryGetLocalPlayerState(out PlayerState st) && IsStation(st)
                && Svc.ShipsClient.TryGetTrackedShip(st.EntityId, out TrackedShipClient _);
            if (!seated)
            {
                if (Active) Leave();
                return;
            }
            Svc.PlayersClient.TryGetLocalPlayerState(out PlayerState me);
            if (!Active)
            {
                Active = true;
                Selected = null;
                ZoomLevel = 0;
                _main = Camera.main;
                _mainFov = _main != null ? _main.fieldOfView : 60f;
                PlayerDistantCamera dc = Object.FindObjectOfType<PlayerDistantCamera>();
                _distant = dc != null ? dc.GetComponentInChildren<Camera>() : null;
                _distantFov = _distant != null ? _distant.fieldOfView : 60f;
            }
            Root = ShipWeapons.RootOf(me.EntityId);
            _station = new PartContext(me.EntityId, me.PartId);
            HidePilotingPrompt();

            bool controls = Cursor.lockState == CursorLockMode.Locked && !Typing();
            Mouse mouse = Mouse.current;
            Keyboard kb = Keyboard.current;
            Trigger = controls && mouse != null && mouse.leftButton.isPressed;
            Trigger2 = controls && mouse != null && mouse.rightButton.isPressed;
#if DEV
            Trigger |= _devFire;
#endif

            Controlled.Clear();
            foreach (ShipWeapons.Mount m in ShipWeapons.Mounts)
                if (Turrets.IsTurret(m.Kind) && m.Head != null && m.Pitch != null && ShipWeapons.RootOf(m.ShipId) == Root && TryGetGunner(m, Root, out bool local) && local)
                    Controlled.Add(m);
            Controlled.Sort((a, b) => a.ShipId != b.ShipId ? a.ShipId.CompareTo(b.ShipId) : a.Ctx.PartId.CompareTo(b.Ctx.PartId));
            if (Controlled.Count == 0)
            {
                Selected = null;
                return;
            }

            if (Selected == null || !Controlled.Contains(Selected))
                Select(Controlled[0], Selected == null);
            if (controls && kb != null && Controlled.Count > 1)
            {
                int i = Controlled.IndexOf(Selected);
                if (kb.eKey.wasPressedThisFrame) Select(Controlled[(i + 1) % Controlled.Count], false);
                else if (kb.qKey.wasPressedThisFrame) Select(Controlled[(i + Controlled.Count - 1) % Controlled.Count], false);
            }
            if (controls && mouse != null)
            {
                float wheel = mouse.scroll.ReadValue().y;
                if (wheel > 0.1f) ZoomLevel = Mathf.Min(ZoomLevel + 1, Zooms.Length - 1);
                else if (wheel < -0.1f) ZoomLevel = Mathf.Max(ZoomLevel - 1, 0);
                Vector2 d = mouse.delta.ReadValue() * (0.15f * Sensitivity() / _zoom);
                TurretDef def = Turrets.Get(Selected.Kind);
                _yaw = Mathf.Repeat(_yaw + d.x + 180f, 360f) - 180f;
                _elevation = Mathf.Clamp(_elevation + d.y, def.MinElevation, def.MaxElevation);
            }
            ApplyFov();
            _shake = Mathf.MoveTowards(_shake, 0f, Time.deltaTime * 2.5f);

            // what the crosshair rests on: the nearest thing along the view that is not our own ship
            ViewPose(out Vector3 pos, out Quaternion rot);
            Vector3 dir = rot * Vector3.forward;
            float dist = 2000f;
            int n = ClientScene.PhysicsScene.Raycast(pos, dir, Hits, dist, Ballistics.Mask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
                if (Hits[i].distance < dist && ShipWeapons.HitRoot(Hits[i]) != Root) dist = Hits[i].distance;
            AimDistance = dist;
            AimPoint = pos + dir * dist;
        }

        /// <summary>Moves the view to a turret, keeping the current direction (or, the first time, the turret's own).</summary>
        private static void Select(ShipWeapons.Mount m, bool fromGuns)
        {
            Vector3 dir = fromGuns || Selected == null || Selected.Head == null ? m.Pitch.forward : ViewDir(Selected);
            Selected = m;
            _camFrac = 1f;
            TurretDef def = Turrets.Get(m.Kind);
            Vector3 local = m.Head.parent.InverseTransformDirection(dir);
            _yaw = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
            _elevation = Mathf.Clamp(Mathf.Asin(Mathf.Clamp(local.y, -1f, 1f)) * Mathf.Rad2Deg, def.MinElevation, def.MaxElevation);
        }

        private static Vector3 ViewDir(ShipWeapons.Mount m) => m.Head.parent.rotation * Quaternion.Euler(-_elevation, _yaw, 0f) * Vector3.forward;

        /// <summary>The gunner's eye: above and behind the selected turret's guns, looking along the view.</summary>
        private static void ViewPose(out Vector3 pos, out Quaternion rot)
        {
            ShipWeapons.Mount m = Selected;
            TurretDef def = Turrets.Get(m.Kind);
            Transform frame = m.Head.parent;
            Vector3 up = frame.up;
            Vector3 dir = ViewDir(m);
            Vector3 back = dir * def.CamBack;
            float rise = Vector3.Dot(back, up);
            if (rise > 0f) back -= up * rise; // looking up: the eye stays above the hull instead of sinking behind the turret
            Vector3 eye = m.Pitch.position + up * def.CamUp - back;
            // something between the turret and the eye (a wall, a deck above, a hill): the eye comes in closer to the
            // turret at once, and backs out again smoothly once the way is clear
            Vector3 anchor = m.Pitch.position + up * (def.CamUp * 0.35f);
            Vector3 reach = eye - anchor;
            float full = reach.magnitude;
            if (_camFrame != Time.frameCount)
            {
                _camFrame = Time.frameCount;
                float clear = 1f;
                if (full > 0.01f && ClientScene.PhysicsScene.SphereCast(anchor, CamRadius, reach / full, out RaycastHit hit, full, Ballistics.Mask, QueryTriggerInteraction.Ignore))
                    clear = Mathf.Clamp01((hit.distance - 0.1f) / full);
                _camFrac = clear < _camFrac ? clear : Mathf.MoveTowards(_camFrac, clear, Time.deltaTime * 1.2f);
            }
            pos = anchor + reach * _camFrac;
            rot = Quaternion.LookRotation(dir, up);
            if (_shake > 0f)
            {
                float a = _shake * _shake;
                rot *= Quaternion.Euler(Random.Range(-1f, 1f) * a * 1.5f, Random.Range(-1f, 1f) * a * 1.5f, 0f);
                pos += Random.insideUnitSphere * a * 0.08f;
            }
        }

        /// <summary>A turret this station controls just fired: the view kicks.</summary>
        public static void Fired(TurretDef def)
        {
            if (Selected != null && Selected.Kind == def.Id) _shake = Mathf.Max(_shake, def.Shake);
        }

        private static System.Type _promptType;
        private static bool _promptLooked;
        private static GameObject _prompt;
        private static bool _promptHidden;
        private static float _promptRetryAt;

        /// <summary>The game's flying tutorial ("thrust up", "roll"...) would wait forever at a station that does not fly: it is put away meanwhile.</summary>
        private static void HidePilotingPrompt()
        {
            if (_prompt == null)
            {
                if (!_promptLooked)
                {
                    _promptLooked = true;
                    _promptType = AccessTools.TypeByName("PilotingTutorialPrompts");
                }
                if (_promptType == null || Time.unscaledTime < _promptRetryAt) return;
                _promptRetryAt = Time.unscaledTime + 2f; // not there (tutorial done): looked for again now and then only
                Object found = Object.FindObjectOfType(_promptType);
                _prompt = found is Component c ? c.gameObject : null;
                _promptHidden = false;
                if (_prompt == null) return;
            }
            if (_prompt.activeSelf)
            {
                _prompt.SetActive(false);
                _promptHidden = true;
            }
        }

        private static void Leave()
        {
            if (_prompt != null && _promptHidden) _prompt.SetActive(true);
            _prompt = null;
            _promptHidden = false;
            _promptRetryAt = 0f;
            Active = false;
            Selected = null;
            Controlled.Clear();
            Trigger = Trigger2 = false;
            ZoomLevel = 0;
            ApplyFov();
            _main = _distant = null;
        }

        private static void ApplyFov()
        {
            float goal = Active ? Zooms[ZoomLevel] : 1f;
            _zoom = Active ? Mathf.Lerp(_zoom, goal, 1f - Mathf.Exp(-Time.unscaledDeltaTime * 12f)) : 1f;
            if (Mathf.Abs(_zoom - goal) < 0.01f) _zoom = goal;
            float z = _zoom;
            if (_main != null) _main.fieldOfView = Fov(_mainFov, z);
            if (_distant != null) _distant.fieldOfView = Fov(_distantFov, z);
        }

        private static float Fov(float baseFov, float zoom) =>
            zoom <= 1f ? baseFov : 2f * Mathf.Atan(Mathf.Tan(baseFov * 0.5f * Mathf.Deg2Rad) / zoom) * Mathf.Rad2Deg;

        private static Traverse _prefs;
        private static float _prefsRetryAt;

        /// <summary>The game's mouse sensitivity setting, read from its pilot controls (1 when it cannot be read).</summary>
        private static float Sensitivity()
        {
            try
            {
                if (_prefs == null)
                {
                    // looked up again at most once a second: finding it scans the scene
                    if (Time.unscaledTime < _prefsRetryAt) return 1f;
                    _prefsRetryAt = Time.unscaledTime + 1f;
                    PlayerPilotControlsCapture capture = Object.FindObjectOfType<PlayerPilotControlsCapture>();
                    object p = capture != null ? Traverse.Create(capture).Field("_preferences").GetValue() : null;
                    if (p == null) return 1f;
                    _prefs = Traverse.Create(p).Property("CameraSensitivity");
                }
                return Mathf.Clamp(_prefs.GetValue<float>(), 0.05f, 10f);
            }
            catch
            {
                _prefs = null;
                return 1f;
            }
        }

        private static bool Typing()
        {
            GameObject sel = UnityEngine.EventSystems.EventSystem.current != null ? UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject : null;
            if (sel == null) return false;
            TMPro.TMP_InputField tmp = sel.GetComponent<TMPro.TMP_InputField>();
            return tmp != null && tmp.isFocused;
        }

        // ------------------------------------------------------------------ patches

        public static void Patch(HarmonyLib.Harmony h)
        {
            h.Patch(AccessTools.Method(typeof(TrackedShipServer), nameof(TrackedShipServer.StartInteracting)),
                prefix: new HarmonyMethod(typeof(GunnerStation), nameof(StartInteractingPrefix)),
                postfix: new HarmonyMethod(typeof(GunnerStation), nameof(StartInteractingPostfix)));
            h.Patch(AccessTools.Method(typeof(PlayerPilotControlsCapture), nameof(PlayerPilotControlsCapture.UpdatePilotInputs)),
                prefix: new HarmonyMethod(typeof(GunnerStation), nameof(UpdatePilotInputsPrefix)));
            // last of all, so a third-person view from another mod (TF Advanced) does not move the eye back over the seat
            h.Patch(AccessTools.Method(typeof(PlayerCamerasUpdater), nameof(PlayerCamerasUpdater.UpdateCameras)),
                postfix: new HarmonyMethod(typeof(GunnerStation), nameof(UpdateCamerasPostfix)) { priority = Priority.Last });
            PatchThirdPerson(h);
        }

        /// <summary>
        /// TF Advanced's third-person view turns while piloting, and its mouse wheel sets the view distance: both
        /// are held off at a station (its wheel would zoom out its view while ours zooms in).
        /// </summary>
        private static void PatchThirdPerson(HarmonyLib.Harmony h)
        {
            System.Type tp = AccessTools.TypeByName("StellarDriveDemoTFAdvanced.TFItems.Devices.ThirdPerson");
            if (tp == null) return;
            try
            {
                System.Reflection.MethodInfo update = AccessTools.Method(tp, "Update");
                if (update != null) h.Patch(update, prefix: new HarmonyMethod(typeof(GunnerStation), nameof(SkipAtStation)));
                System.Reflection.MethodInfo place = AccessTools.Method(tp, "TryPlace");
                if (place != null) h.Patch(place, prefix: new HarmonyMethod(typeof(GunnerStation), nameof(NoPlaceAtStation)));
            }
            catch (System.Exception e)
            {
                ArsenalMod.Log.Warning($"TF Advanced third-person view not held off at stations: {e.Message}");
            }
        }

        private static bool SkipAtStation() => !Active;

        private static bool NoPlaceAtStation(ref bool __result)
        {
            if (!Active) return true;
            __result = false;
            return false;
        }

        private static bool IsStationPart(TrackedShipServer ship, ushort partId) =>
            ship.State.TryGetPart(partId, out StatefulPart part) && part.Settings != null && part.Settings.id == ShipContent.GunnerStationId;

        /// <summary>Host: the station is kept "not in use", so it is the players' states that say whether someone sits there.</summary>
        private static bool StartInteractingPrefix(TrackedShipServer __instance, ushort partId, ref bool __result)
        {
            if (!IsStationPart(__instance, partId) || Svc.PlayersServer == null) return true;
            foreach (TrackedPlayerServer p in Svc.PlayersServer.ActivePlayers)
            {
                PlayerState st = p.State;
                if (st.Type == PlayerStateType.Piloting && st.EntityId == __instance.Id && st.PartId == partId)
                {
                    __result = false;
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Host: a seat in use is a pilot to the game (its gyroscope steadies the ship, the first one in use sets the
        /// flight orientation): the station goes straight back to "not in use".
        /// </summary>
        private static void StartInteractingPostfix(TrackedShipServer __instance, ushort partId, bool __result)
        {
            if (!__result || !IsStationPart(__instance, partId)) return;
            if (__instance.State.TryGetPart(partId, out StatefulPart part) && part.State is IDirectlyInteractivePart seat)
                __instance.UpdatePartState(partId, (IStatefulPartState)seat.WithUseState(false));
        }

        /// <summary>At the station the flight controls carry the triggers only (on the seat's two signal outputs).</summary>
        private static bool UpdatePilotInputsPrefix()
        {
            if (!Active) return true;
            Svc.Get<IShipInteractionNetworkService>()?.UpdateFlightControls(new FlightControlsNetworkInputs
            {
                MoveInput = new Vector3(0f, Trigger2 ? 1f : 0f, Trigger ? 1f : 0f),
                RotationInput = Vector3.zero,
            });
            return false;
        }

        private static readonly AccessTools.FieldRef<PlayerCamerasUpdater, PlayerCamera> CameraRef = AccessTools.FieldRefAccess<PlayerCamerasUpdater, PlayerCamera>("_camera");
        private static readonly AccessTools.FieldRef<PlayerCamerasUpdater, PlayerDistantCamera> DistantRef = AccessTools.FieldRefAccess<PlayerCamerasUpdater, PlayerDistantCamera>("_distantCamera");

        private static void UpdateCamerasPostfix(PlayerCamerasUpdater __instance, PlayerState playerState)
        {
            if (!Active || playerState.Type != PlayerStateType.Piloting || Selected == null || Selected.Head == null || Selected.Pitch == null) return;
            ViewPose(out Vector3 pos, out Quaternion rot);
            CameraRef(__instance)?.UpdateCamera(pos, rot);
            DistantRef(__instance)?.UpdateCamera(pos, rot);
        }
    }

    /// <summary>
    /// The gunner's sight, drawn in the same thin ink as the pilot HUD: the crosshair, where each controlled
    /// turret's guns really point, a lead pip on the ship nearest the crosshair, and the selected turret's status.
    /// </summary>
    internal sealed class GunnerHud : MonoBehaviour
    {
        private static GUIStyle _label, _small;

        private void OnGUI()
        {
            if (!ArsenalMod.InGame || !GunnerStation.Active || Event.current.type != EventType.Repaint) return;
            Camera cam = Camera.main;
            if (cam == null) return;
            if (_label == null)
            {
                _label = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
                _small = new GUIStyle(_label) { fontSize = 12, fontStyle = FontStyle.Normal };
            }
            Color ink = new Color(0.95f, 0.98f, 1f, 0.9f);
            Color hot = new Color(1f, 0.35f, 0.2f, 0.95f);
            Vector2 c = new Vector2(Screen.width / 2f, Screen.height / 2f);
            ShipWeapons.Mount sel = GunnerStation.Selected;
            if (sel == null)
            {
                Text(new Rect(c.x - 450f, c.y + 60f, 900f, 24f), Loc.T("AUCUNE TOURELLE POUR CE POSTE : reliez une sortie du poste à l'entrée d'une tourelle", "NO TURRET FOR THIS STATION: wire one of its outputs to a turret's input"), ink, _label);
                return;
            }
            TurretDef def = Turrets.Get(sel.Kind);
            LaserStyle style = Laser.Style(def.Style);

            // crosshair: open ring with ticks, the targeting computer's look
            ShipHud.Ring(c, 22f, new Color(ink.r, ink.g, ink.b, 0.55f));
            ShipHud.Cross(c, 36f, 26f, ink);
            ShipHud.Line(c + new Vector2(-3f, 0f), c + new Vector2(3f, 0f), 2f, ink);

            // where the guns of each turret really point (they lag behind the view while they traverse)
            foreach (ShipWeapons.Mount m in GunnerStation.Controlled)
            {
                if (m.Pitch == null) continue;
                Vector3 p = cam.WorldToScreenPoint(m.Pitch.position + m.Pitch.forward * GunnerStation.AimDistance);
                if (p.z <= 0f) continue;
                var g = new Vector2(p.x, Screen.height - p.y);
                bool on = (g - c).sqrMagnitude < 6f * 6f;
                ShipHud.Ring(g, m == sel ? 6f : 4f, m.Overheated ? hot : on ? new Color(0.4f, 1f, 0.5f, 0.95f) : new Color(1f, 0.85f, 0.3f, 0.9f));
            }

            // the ship nearest the crosshair: brackets, range, and where to shoot to meet it
            uint target = NearestToCrosshair(cam, c, out Vector3 tc);
            if (target != 0)
            {
                Vector3 sp = cam.WorldToScreenPoint(tc);
                var tp = new Vector2(sp.x, Screen.height - sp.y);
                Color tcol = new Color(1f, 0.4f, 0.3f, 0.95f);
                ShipHud.Brackets(tp, 24f, tcol);
                float range = Vector3.Distance(sel.Pitch.position, tc);
                Text(new Rect(tp.x - 100f, tp.y + 26f, 200f, 20f), range >= 1000f ? $"{range / 1000f:0.00} km" : $"{range:0} m", range > style.Range ? new Color(0.7f, 0.7f, 0.7f, 0.9f) : tcol, _small);
                if (ShipWeapons.TryLead(target, GunnerStation.Root, sel.Pitch.position, style.Speed, out Vector3 lead))
                {
                    Vector3 lp = cam.WorldToScreenPoint(lead);
                    if (lp.z > 0f)
                    {
                        var l = new Vector2(lp.x, Screen.height - lp.y);
                        ShipHud.Line(tp, l, 1f, new Color(tcol.r, tcol.g, tcol.b, 0.5f));
                        ShipHud.Diamond(l, 7f, tcol);
                    }
                }
            }

            // the selected turret: name, which one of how many, heat, zoom
            float y = 26f; // top of the screen: the turret itself fills the bottom of the view
            int idx = GunnerStation.Controlled.IndexOf(sel) + 1;
            string title = GunnerStation.Controlled.Count > 1 ? $"[Q]    {def.HudName}  {idx}/{GunnerStation.Controlled.Count}    [E]" : def.HudName;
            Text(new Rect(c.x - 250f, y, 500f, 24f), title, ink, _label);
            var bar = new Rect(c.x - 80f, y + 26f, 160f, 6f);
            GUI.color = new Color(1f, 1f, 1f, 0.25f);
            GUI.DrawTexture(bar, ShipHud.Px);
            GUI.color = sel.Overheated ? hot : Color.Lerp(new Color(0.5f, 0.9f, 1f), new Color(1f, 0.7f, 0.3f), sel.Heat);
            GUI.DrawTexture(new Rect(bar.x, bar.y, bar.width * Mathf.Clamp01(sel.Heat), bar.height), ShipHud.Px);
            if (sel.Overheated) Text(new Rect(c.x - 150f, y + 34f, 300f, 20f), Loc.T("SURCHAUFFE", "OVERHEATED"), hot, _small);
            else Text(new Rect(c.x - 250f, y + 34f, 500f, 20f), GunnerStation.StationWired
                ? Loc.T($"{GunnerStation.Controlled.Count} tourelle(s) reliée(s) à ce poste", $"{GunnerStation.Controlled.Count} turret(s) wired to this station")
                : Loc.T($"{GunnerStation.Controlled.Count} tourelle(s) du vaisseau, aucune reliée par câble", $"{GunnerStation.Controlled.Count} turret(s) of the ship, none wired"), new Color(ink.r, ink.g, ink.b, 0.6f), _small);
            if (GunnerStation.ZoomLevel > 0)
                Text(new Rect(c.x + 30f, c.y + 24f, 80f, 20f), GunnerStation.ZoomLevel == 1 ? "×2.5" : "×6", ink, _small);
            if (Effects.IsShipEmp(GunnerStation.Root))
                Text(new Rect(c.x - 200f, c.y - 70f, 400f, 24f), Loc.T("SYSTÈMES IONISÉS", "SYSTEMS IONISED"), new Color(0.5f, 0.75f, 1f), _label);
            Text(new Rect(c.x - 400f, Screen.height - 34f, 800f, 20f),
                Loc.T("Clic gauche : tirer · Clic droit : tir 2 · Molette : zoom · Q / E : tourelle", "Left click: fire · Right click: fire 2 · Wheel: zoom · Q / E: turret"),
                new Color(ink.r, ink.g, ink.b, 0.55f), _small);
            GUI.color = Color.white;
        }

        private static uint NearestToCrosshair(Camera cam, Vector2 c, out Vector3 center)
        {
            center = Vector3.zero;
            uint best = 0;
            float bestD = 140f * 140f;
            foreach (TrackedShipClient s in Svc.ShipsClient.Ships)
            {
                if (ShipWeapons.RootOf(s.Id) != s.Id || s.Id == GunnerStation.Root) continue;
                if (!ShipWeapons.TryGetShipMotion(s.Id, out Vector3 tc, out _, out _, out _)) continue;
                Vector3 sp = cam.WorldToScreenPoint(tc);
                if (sp.z <= 0f || sp.z > 4000f) continue;
                float d = (new Vector2(sp.x, Screen.height - sp.y) - c).sqrMagnitude;
                if (d >= bestD) continue;
                bestD = d;
                best = s.Id;
                center = tc;
            }
            return best;
        }

        private static void Text(Rect r, string s, Color col, GUIStyle style)
        {
            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.Label(new Rect(r.x + 1f, r.y + 1f, r.width, r.height), s, style);
            GUI.color = col;
            GUI.Label(r, s, style);
        }
    }
}
