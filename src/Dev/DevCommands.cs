#if DEV
using System.Globalization;
using System.Linq;
using Arsenal.Core;
using Items.Model;
using Players;
using Players.Inventory;
using Players.LocalClient.Controls;
using Ships;
using Ships.Interface.Model;
using Ships.Interface.Model.Gases;
using Ships.Interface.Model.Parts;
using Ships.Interface.Model.Parts.Common;
using Ships.Interface.Model.Placement;
using Ships.Interface.Services;
using Ships.Interface.Settings;
using Ships.Services;
using UnityEngine;
using WorldObjects.Interface.Services;
using WorldTracking.Interface.Services;

namespace Arsenal
{
    public partial class ArsenalMod
    {
        private uint _testShipId;

        partial void DevCommandExtra(string[] a)
        {
            switch (a[0])
            {
                case "give":
                {
                    TrackedPlayerServer p = Svc.PlayersServer.ActivePlayers.First();
                    for (int i = 1; i < a.Length; i++)
                    {
                        string[] parts = a[i].Split('x');
                        uint id = uint.Parse(parts[0]);
                        byte q = parts.Length > 1 ? byte.Parse(parts[1]) : (byte)1;
                        p.AddItemToInventory(new ItemDrop { ItemId = id, Quantity = q });
                    }
                    break;
                }
                case "armfab":
                    Armory.Armory.RequestFabricate(uint.Parse(a[1]));
                    break;
                case "giveall":
                    foreach (TrackedPlayerServer pl in Svc.PlayersServer.ActivePlayers)
                        for (int i = 1; i < a.Length; i++)
                        {
                            string[] parts = a[i].Split('x');
                            pl.AddItemToInventory(new ItemDrop { ItemId = uint.Parse(parts[0]), Quantity = parts.Length > 1 ? byte.Parse(parts[1]) : (byte)1 });
                        }
                    break;
                case "lookship":
                    LookAtShip(uint.Parse(a[1]), new Vector3(float.Parse(a[2], CultureInfo.InvariantCulture), float.Parse(a[3], CultureInfo.InvariantCulture), float.Parse(a[4], CultureInfo.InvariantCulture)));
                    break;
                case "net":
                    LoggerInstance.Msg($"[DEV] server={Net.ArsenalNet.IsServer} client={Net.ArsenalNet.IsClient} me={Weapons.Explosives.LocalClientId} players={Svc.PlayersClient?.TotalPlayers} ships={string.Join(",", Svc.ShipsClient.Ships.Select(s => s.Id))}");
                    foreach (var o in Svc.PlayersClient.OtherPlayers)
                        LoggerInstance.Msg($"[DEV] other player {o.Id} {o.PlayerName} at {o.Position} physics {o.IsPhysicsLoaded}");
                    break;
                case "research":
                    for (int i = 1; i < a.Length; i++)
                        Svc.Get<Managers.Interface.Services.IResearchNetworkService>().ResearchTech(ushort.Parse(a[i]));
                    break;
                case "sockets":
                    foreach (var sio in Object.FindObjectsByType<Ships.Cables.VisualObjects.SocketInteractionObject>(FindObjectsSortMode.None))
                        if (a.Length > 1 || sio.EntityPartSocket.EntityId == _gunshipId)
                            LoggerInstance.Msg($"[DEV] socket ship {sio.EntityPartSocket.EntityId} part {sio.EntityPartSocket.Socket.PartId} id {sio.EntityPartSocket.Socket.SocketId} at {sio.transform.position}");
                    break;
                case "socketray":
                {
                    // like the game's plug detector: first thing hit when looking at each socket from 1.5 m behind/above
                    int mask = Tools.Common.RaycastMasks.BuildDetectionLayer | (1 << LayerMask.NameToLayer("Interactable"));
                    foreach (var sio in Object.FindObjectsByType<Ships.Cables.VisualObjects.SocketInteractionObject>(FindObjectsSortMode.None))
                    {
                        if (a.Length < 2 && sio.EntityPartSocket.EntityId != _gunshipId) continue;
                        Vector3 target = sio.transform.position;
                        Vector3 from = a.Length > 1 ? target - sio.transform.forward * 1.2f + sio.transform.up * 0.5f : Camera.main.transform.position;
                        bool hit = WorldTracking.Interface.Values.ClientScene.PhysicsScene.Raycast(from, (target - from).normalized, out RaycastHit h, 50f, mask);
                        LoggerInstance.Msg($"[DEV] part {sio.EntityPartSocket.Socket.PartId} socket {sio.EntityPartSocket.Socket.SocketId}: first hit {(hit ? h.collider.name + (h.collider.GetComponent<Ships.Cables.VisualObjects.SocketInteractionObject>() == sio ? " = SOCKET" : "") : "none")}");
                    }
                    break;
                }
                case "fonts":
                    foreach (var f in Resources.FindObjectsOfTypeAll<TMPro.TMP_FontAsset>())
                        LoggerInstance.Msg($"[DEV] font {f.name} mode={f.atlasPopulationMode} glyphs={f.glyphTable?.Count} chars={f.characterTable?.Count} fallback={f.fallbackFontAssetTable?.Count}");
                    foreach (var it in Resources.FindObjectsOfTypeAll<ItemSettings>())
                        if (it.id > 9000) LoggerInstance.Msg($"[DEV] item {it.id} icon={(it.icon ? it.icon.name + " " + it.icon.width : "null")}");
                    break;
                case "glyphs":
                    foreach (var f in Resources.FindObjectsOfTypeAll<TMPro.TMP_FontAsset>().Where(x => x.name.Contains("Mulish") || x.name.Contains("Anta")))
                    {
                        foreach (char ch in "l-bea")
                        {
                            bool has = f.characterLookupTable.TryGetValue(ch, out var tc);
                            LoggerInstance.Msg($"[DEV] {f.name} '{ch}' has={has} glyph={(has ? tc.glyph?.glyphRect.ToString() + " atlas " + tc.glyph?.atlasIndex : "-")} atlases={f.atlasTextures?.Length} multi={f.isMultiAtlasTexturesEnabled}");
                        }
                    }
                    break;
                case "saveicon":
                {
                    var it = Resources.FindObjectsOfTypeAll<ItemSettings>().First(x => x.id == uint.Parse(a[1]));
                    var tex = it.icon;
                    var rt = RenderTexture.GetTemporary(tex.width, tex.height);
                    Graphics.Blit(tex, rt);
                    var prev = RenderTexture.active; RenderTexture.active = rt;
                    var copy = new Texture2D(tex.width, tex.height, TextureFormat.RGBA32, false);
                    copy.ReadPixels(new Rect(0, 0, tex.width, tex.height), 0, 0); copy.Apply();
                    RenderTexture.active = prev; RenderTexture.ReleaseTemporary(rt);
                    System.IO.File.WriteAllBytes(System.IO.Path.Combine(MelonLoader.Utils.MelonEnvironment.UserDataDirectory, "icon_" + a[1] + ".png"), copy.EncodeToPNG());
                    break;
                }
                case "save":
                    Svc.Get<Saving.Services.GameSaver>().SaveGame(new Saving.Interface.Model.SaveGameParams { IsAutomatic = false });
                    break;
                case "sit":
                    Svc.Get<Managers.Interface.Services.IShipInteractionNetworkService>().StartInteractWithPart(
                        new Managers.Interface.Model.PartInteractNetworkRequest { ShipId = a.Length > 2 ? uint.Parse(a[2]) : _gunshipId, PartId = a.Length > 1 ? ushort.Parse(a[1]) : (ushort)1 });
                    break;
                case "click":
                    MelonLoader.MelonCoroutines.Start(Click(a[1], float.Parse(a[2], CultureInfo.InvariantCulture)));
                    break;
                case "sound":
                    // sound <ship|hand|pew|hit|shield>: WAV and spectrogram in UserData
                    Visual.Sounds.DevDump(a[1], MelonLoader.Utils.MelonEnvironment.UserDataDirectory);
                    LoggerInstance.Msg($"[DEV] sound {a[1]} written");
                    break;
                case "key":
                    // key <Key name>: taps a keyboard key (e.g. key Escape to leave the pause menu)
                    MelonLoader.MelonCoroutines.Start(TapKey((UnityEngine.InputSystem.Key)System.Enum.Parse(typeof(UnityEngine.InputSystem.Key), a[1], true)));
                    break;
                case "tab":
                {
                    var bm = Object.FindObjectsByType<UI.BuildMenu.BuildMenu>(FindObjectsSortMode.None).FirstOrDefault(b => b.isActiveAndEnabled);
                    for (int i = 0; i < int.Parse(a[1]); i++) bm?.OnSwitchTabDirection(1f);
                    break;
                }
                case "rstat":
                {
                    var rp = Svc.Get<Research.Interface.Services.IResearchProvider>();
                    foreach (ushort rid in new ushort[] { 1, 2, 12, 90, 91 })
                        LoggerInstance.Msg($"[DEV] research {rid}: {rp?.IsResearched(rid)}");
                    break;
                }
                case "hurtother":
                    foreach (var o in Svc.PlayersClient.OtherPlayers)
                        Weapons.PlayerDamage.Report(o.Id, float.Parse(a[1], CultureInfo.InvariantCulture), Weapons.WeaponKind.Rifle, a.Length > 2);
                    break;
                case "hp":
                    LoggerInstance.Msg($"[DEV] health {Svc.Get<Players.Services.LocalPlayerHealthProxy>()?.CurrentHealth}");
                    break;
                case "move":
                {
                    // nudge the local player sideways (cheat-free: teleport the client rigidbody prediction isn't possible, so use the server)
                    TrackedPlayerServer me = Svc.PlayersServer?.GetTrackedPlayerFromConnectionId(Weapons.Explosives.LocalClientId);
                    if (me != null) me.ServerRigidbody.position += me.ServerRigidbody.rotation * new Vector3(float.Parse(a[1], CultureInfo.InvariantCulture), 0, float.Parse(a[2], CultureInfo.InvariantCulture));
                    break;
                }
                case "select":
                    Object.FindObjectsByType<ToolBelt>(FindObjectsSortMode.None).First().SelectTool(int.Parse(a[1]));
                    break;
                case "target":
                {
                    // target <distance> <height>: a test wall ship in front of the player, up in the air
                    TrackedPlayerServer tp = Svc.PlayersServer.ActivePlayers.First();
                    var tph = tp.PhysicsState;
                    Vector3 tup = tph.Rotation * Vector3.up;
                    Vector3 tfwd = Vector3.ProjectOnPlane(tph.Rotation * Vector3.forward, tup).normalized;
                    SpawnWallAt(tph.SpaceId, tph.Position + tfwd * float.Parse(a[1], CultureInfo.InvariantCulture) + tup * float.Parse(a[2], CultureInfo.InvariantCulture), Quaternion.LookRotation(tfwd, tup), a.Length > 3 && a[3] == "hollow");
                    break;
                }
                case "armshow":
                    // armshow <itemId>: the open armory jumps to that weapon
                    Armory.ArmoryUi.DevShow(uint.Parse(a[1]));
                    break;
                case "channels":
                {
                    FMODUnity.RuntimeManager.CoreSystem.getChannelsPlaying(out int playing, out int real);
                    LoggerInstance.Msg($"[DEV] FMOD channels playing {playing} (real {real})");
                    foreach (var (style, lch) in Visual.Sounds.DevLoops)
                        if (lch.isPlaying(out bool on) == FMOD.RESULT.OK && on) LoggerInstance.Msg($"[DEV]   arsenal loop still playing: {style}");
                    break;
                }
                case "lang":
                    // lang en|fr: switches the game's locale (for screenshots in either language)
                    UnityEngine.Localization.Settings.LocalizationSettings.SelectedLocale =
                        UnityEngine.Localization.Settings.LocalizationSettings.AvailableLocales.Locales.FirstOrDefault(l => l.Identifier.Code.StartsWith(a[1]));
                    break;
                case "hud":
                    // hud off|on: hides every root canvas (game and mod HUD) for clean screenshots
                    foreach (Canvas cv in Resources.FindObjectsOfTypeAll<Canvas>().Where(cv => cv.isRootCanvas && cv.gameObject.scene.IsValid()))
                        cv.enabled = a.Length > 1 && a[1] == "on";
                    break;
                case "fov":
                    Camera.main.fieldOfView = float.Parse(a[1], CultureInfo.InvariantCulture);
                    break;
                case "boomg":
                    _testShipId = _gunshipId;
                    goto case "boom";
                case "boom":
                {
                    // boom <x> <y> <z> [kind]: blast at a point of the test wall (ship-local)
                    if (!Svc.ShipsClient.TryGetTrackedShip(_testShipId, out TrackedShipClient tw) || !tw.IsRigidbodyLoaded) break;
                    Vector3 bp = tw.ShipRigidBody.transform.TransformPoint(new Vector3(float.Parse(a[1], CultureInfo.InvariantCulture), float.Parse(a[2], CultureInfo.InvariantCulture), float.Parse(a[3], CultureInfo.InvariantCulture)));
                    Weapons.Explosives.RequestExplosion(bp, a.Length > 4 ? (Weapons.WeaponKind)byte.Parse(a[4]) : Weapons.WeaponKind.Grenade);
                    break;
                }
                case "shipparts":
                    // shipparts <shipId>: its parts as the server has them, and the cables plugged into it
                    if (Svc.ShipsServer.TryGetTrackedShip(uint.Parse(a[1]), out TrackedShipServer sps))
                        foreach (var sp in sps.StatefulParts)
                            LoggerInstance.Msg($"[DEV] ship {a[1]} part {sp.Id} settings {sp.Settings?.id} {sp.Settings?.name}");
                    foreach (var c in Svc.Get<Ships.Cables.CablesServerTracker>().Cables)
                        LoggerInstance.Msg($"[DEV] cable {c.Id}: {c.State.PlugA} -> {c.State.PlugB}");
                    break;
                case "fps":
                    // fps <seconds>: average and worst frame over that time, and how many effects are alive
                    MelonLoader.MelonCoroutines.Start(MeasureFps(float.Parse(a[1], CultureInfo.InvariantCulture)));
                    break;
                case "rmship":
                {
                    // rmship: removes the test wall ship (its damage must go with it)
                    if (Svc.ShipsServer.TryGetTrackedShip(_testShipId, out TrackedShipServer rs)) Svc.ShipsServer.RemoveShip(rs.State);
                    int left = Damage.HullDamage.ServerStates.Keys.Count(k => k.ShipId == _testShipId);
                    LoggerInstance.Msg($"[DEV] removed ship {_testShipId}: {left} damaged panels left for it");
                    break;
                }
                case "noknock":
                    Damage.ShipKnockback.DevNoKnock = !Damage.ShipKnockback.DevNoKnock;
                    break;
                case "hullparts":
                    foreach (var hp in Resources.FindObjectsOfTypeAll<HullPartSettings>().OrderBy(h => h.id))
                        LoggerInstance.Msg($"[DEV] hull {hp.id} {hp.name} seg={hp.hullMeshSegmentType} mat={hp.hullMaterialType} own={hp.hasItsOwnMesh} fwd={hp.defaultForward} up={hp.defaultUp}");
                    break;
                case "shapes":
                {
                    TrackedPlayerServer sp = Svc.PlayersServer.ActivePlayers.First();
                    var sph = sp.PhysicsState;
                    Vector3 sup = sph.Rotation * Vector3.up;
                    Vector3 sfwd = Vector3.ProjectOnPlane(sph.Rotation * Vector3.forward, sup).normalized;
                    // rotate 180: the pieces face back towards the player
                    SpawnShapesAt(sph.SpaceId, sph.Position + sfwd * float.Parse(a[1], CultureInfo.InvariantCulture) + sup * 0.6f, Quaternion.LookRotation(-sfwd, sup));
                    break;
                }
                case "hover":
                    Damage.ShipKnockback.DevHover.Add(_testShipId);
                    break;
                case "wall":
                    SpawnTestWall(a.Length > 1 ? float.Parse(a[1], CultureInfo.InvariantCulture) : 6f);
                    break;
                case "shield":
                {
                    // shield [off] [gunship]: a TF-Advanced-style force field bubble round the test wall (or the gunship)
                    uint sid = a.Contains("gunship") ? _gunshipId : _testShipId;
                    foreach (var (t, _) in Compat.ForceFields.DevBubbles) if (t != null) Object.Destroy(t.gameObject);
                    Compat.ForceFields.DevBubbles.Clear();
                    Compat.ForceFields.DevShielded.Clear();
                    Compat.ForceFields.DevAbsorbed = 0f;
                    if (a.Contains("off") || !Svc.ShipsClient.TryGetTrackedShip(sid, out TrackedShipClient sc) || !sc.IsRigidbodyLoaded) { LoggerInstance.Msg("[DEV] shield off"); break; }
                    Transform root = sc.ShipRigidBody.transform;
                    var bounds = new Bounds(); bool any = false;
                    foreach (Renderer r in root.GetComponentsInChildren<Renderer>())
                    {
                        Bounds b = r.bounds;
                        for (int i = 0; i < 8; i++)
                        {
                            Vector3 p = root.InverseTransformPoint(b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1)));
                            if (!any) { bounds = new Bounds(p, Vector3.zero); any = true; } else bounds.Encapsulate(p);
                        }
                    }
                    var bubble = new GameObject("TFA_ForceFieldBubble");
                    bubble.transform.SetParent(root, false);
                    bubble.transform.localPosition = bounds.center;
                    bubble.transform.localScale = Vector3.Max(bounds.size * 1.3f, Vector3.one * 2f);
                    Compat.ForceFields.DevBubbles.Add((bubble.transform, sid));
                    Compat.ForceFields.DevShielded.Add(sid);
                    LoggerInstance.Msg($"[DEV] shield on ship {sid}: bubble {bubble.transform.localScale} at {bounds.center}");
                    break;
                }
                case "sheet":
                    // contact sheet of models: sheet guns | sheet parts [filter]
                    Dev.ModelSheet.Render(a[1], a.Length > 2 ? a[2] : null);
                    break;
                case "cutouts":
                    // cutouts <filter|all> <size> <pitch> <yaw>: transparent renders of the hand weapons
                    Dev.ModelSheet.Cutouts(a[1] == "all" ? null : a[1], int.Parse(a[2]), Quaternion.Euler(float.Parse(a[3], CultureInfo.InvariantCulture), float.Parse(a[4], CultureInfo.InvariantCulture), 0f));
                    break;
                case "partcutouts":
                    // partcutouts <size> <pitch> <yaw>: transparent renders of the ship weapon parts
                    Dev.ModelSheet.PartCutouts(int.Parse(a[1]), Quaternion.Euler(float.Parse(a[2], CultureInfo.InvariantCulture), float.Parse(a[3], CultureInfo.InvariantCulture), 0f));
                    break;
                case "particon":
                {
                    // render a part prefab with the game's thumbnail renderer: particon <partId> [yaw] [pitch]
                    var ps = new PartSettingsProvider().GetPartSettingById(ushort.Parse(a[1]));
                    var tex = new Texture2D(512, 512, UnityEngine.Experimental.Rendering.GraphicsFormat.R8G8B8A8_UNorm, UnityEngine.Experimental.Rendering.TextureCreationFlags.None);
                    GameObject copy = Object.Instantiate(ps.part);
                    copy.transform.position = new Vector3(0, -20000f, 0);
                    foreach (Collider c in copy.GetComponentsInChildren<Collider>(true)) c.enabled = false;
                    float yaw = a.Length > 2 ? float.Parse(a[2], CultureInfo.InvariantCulture) : 150f, pitch = a.Length > 3 ? float.Parse(a[3], CultureInfo.InvariantCulture) : 25f;
                    Svc.Get<Rendering.Services.ObjectThumbnailRenderer>().Render(copy, Quaternion.Euler(pitch, 0, 0) * Quaternion.Euler(0, yaw, 0), tex);
                    var rt = RenderTexture.GetTemporary(512, 512);
                    Graphics.Blit(tex, rt);
                    var prev = RenderTexture.active; RenderTexture.active = rt;
                    var png = new Texture2D(512, 512, TextureFormat.RGBA32, false);
                    png.ReadPixels(new Rect(0, 0, 512, 512), 0, 0); png.Apply();
                    RenderTexture.active = prev; RenderTexture.ReleaseTemporary(rt);
                    System.IO.File.WriteAllBytes(System.IO.Path.Combine(MelonLoader.Utils.MelonEnvironment.UserDataDirectory, "part_" + a[1] + ".png"), png.EncodeToPNG());
                    break;
                }
                case "turn":
                {
                    var lc = Object.FindObjectsByType<PlayerLookControls>(FindObjectsSortMode.None).First();
                    lc.Rotation = Quaternion.AngleAxis(float.Parse(a[1], CultureInfo.InvariantCulture), lc.Rotation * Vector3.up) * lc.Rotation;
                    break;
                }
                case "lookat":
                    LookAtShip(_testShipId, new Vector3(a.Length > 1 ? float.Parse(a[1], CultureInfo.InvariantCulture) : 0f, a.Length > 2 ? float.Parse(a[2], CultureInfo.InvariantCulture) : 1f, 0.5f));
                    break;
                case "lookg":
                    LookAtShip(_gunshipId, new Vector3(float.Parse(a[1], CultureInfo.InvariantCulture), float.Parse(a[2], CultureInfo.InvariantCulture), float.Parse(a[3], CultureInfo.InvariantCulture)));
                    break;
                case "fire":
                {
                    var tool = Svc.Get<Players.Services.LocalSelectedToolTracker>()?.SelectedTool;
                    if (tool == null) { LoggerInstance.Msg("[DEV] no tool selected"); break; }
                    float secs = a.Length > 1 ? float.Parse(a[1], CultureInfo.InvariantCulture) : 0f;
                    tool.PrimaryActionBegin();
                    MelonLoader.MelonCoroutines.Start(ReleaseAfter(tool, secs));
                    break;
                }
                case "tp":
                {
                    // host only: tp <right> <forward> [other]: move a player (physics is server-authoritative), along the host camera
                    TrackedPlayerServer sp = a.Length > 3 && a[3] == "other"
                        ? Svc.PlayersServer?.ActivePlayers.FirstOrDefault(p => p.Id != Svc.PlayersClient.LocalPlayer.Id)
                        : Svc.PlayersServer?.ActivePlayers.FirstOrDefault(p => p.Id == Svc.PlayersClient.LocalPlayer.Id);
                    if (sp == null) { LoggerInstance.Msg("[DEV] tp: no such player on this server"); break; }
                    Vector3 upv = Visual.Fx.Up;
                    Vector3 fwd = Vector3.ProjectOnPlane(Camera.main.transform.forward, upv).normalized;
                    Vector3 right = Vector3.Cross(upv, fwd);
                    Vector3 offset = right * float.Parse(a[1], CultureInfo.InvariantCulture) + fwd * float.Parse(a[2], CultureInfo.InvariantCulture);
                    var st = sp.PhysicsState;
                    st.Position += offset;
                    st.Velocity = Vector3.zero;
                    var ns = HarmonyLib.Traverse.Create(sp).Property("NetworkState").GetValue();
                    HarmonyLib.Traverse.Create(ns).Method("ServerSetPhysicsState", st).GetValue();
                    sp.ServerRigidbody.position += offset;
                    LoggerInstance.Msg($"[DEV] moved player {sp.Id} to {st.Position}");
                    break;
                }
                case "tpship":
                {
                    // host only: tpship <x> <y> <z>: put the other player at that point of the test gunship (ship coordinates)
                    TrackedPlayerServer sp = Svc.PlayersServer?.ActivePlayers.FirstOrDefault(p => p.Id != Svc.PlayersClient.LocalPlayer.Id);
                    if (sp == null || !Svc.ShipsClient.TryGetTrackedShip(_gunshipId, out TrackedShipClient tship) || !tship.IsRigidbodyLoaded) { LoggerInstance.Msg("[DEV] tpship: no player or ship"); break; }
                    float G(int i) => float.Parse(a[i], CultureInfo.InvariantCulture);
                    var st = sp.PhysicsState;
                    st.Position = tship.ShipRigidBody.transform.TransformPoint(new Vector3(G(1), G(2), G(3)));
                    st.Velocity = Vector3.zero;
                    var ns = HarmonyLib.Traverse.Create(sp).Property("NetworkState").GetValue();
                    HarmonyLib.Traverse.Create(ns).Method("ServerSetPhysicsState", st).GetValue();
                    sp.ServerRigidbody.position = st.Position;
                    LoggerInstance.Msg($"[DEV] moved player {sp.Id} to {st.Position}");
                    break;
                }
                case "lookplayer":
                {
                    // lookplayer [height]: aim at the other player as drawn (feet + height along their up)
                    var o = Svc.PlayersClient.OtherPlayers.FirstOrDefault(p => p.IsVisualLoaded);
                    if (o == null) { LoggerInstance.Msg("[DEV] no other player visible"); break; }
                    float h = a.Length > 1 ? float.Parse(a[1], CultureInfo.InvariantCulture) : 1f;
                    Vector3 target = o.VisualPos + o.VisualRotation * Vector3.up * h;
                    var look = Object.FindObjectsByType<PlayerLookControls>(FindObjectsSortMode.None).First();
                    Camera cam = Camera.main;
                    look.Rotation = Quaternion.LookRotation(target - cam.transform.position, look.Rotation * Vector3.up);
                    LoggerInstance.Msg($"[DEV] looking at player {o.Id} visual {o.VisualPos} raw {o.Position} rb {(o.IsPhysicsLoaded && o.ClientRigidbody ? o.ClientRigidbody.position.ToString() : "-")} dist {Vector3.Distance(cam.transform.position, target):0.0}");
                    break;
                }
                case "aim":
                    Svc.Get<Players.Services.LocalSelectedToolTracker>()?.SelectedTool?.SecondaryActionBegin();
                    break;
                case "clearbar":
                {
                    // empties toolbar slots 1-4 (the given weapons then land there)
                    var tb = Svc.Get<PlayerToolbarProvider>();
                    TrackedPlayerServer me = Svc.PlayersServer.ActivePlayers.First();
                    for (byte i = 1; i < 5; i++)
                    {
                        var slot = tb.GetSlot(i);
                        if (!slot.HasItem) continue;
                        var drop = new[] { new ItemDrop { ItemId = slot.ItemId, Quantity = (byte)Mathf.Min(255, slot.Quantity) } };
                        if (me.HasItemsInInventory(drop)) me.RemoveItemsFromInventory(drop);
                    }
                    break;
                }
                case "levelview":
                {
                    // levelview <pitch>: looks horizontally, then that many degrees down
                    var lc = Object.FindObjectsByType<PlayerLookControls>(FindObjectsSortMode.None).First();
                    Vector3 up = Visual.Fx.Up;
                    Vector3 fwd = Vector3.ProjectOnPlane(lc.Rotation * Vector3.forward, up).normalized;
                    lc.Rotation = Quaternion.AngleAxis(float.Parse(a[1], CultureInfo.InvariantCulture), Vector3.Cross(up, fwd)) * Quaternion.LookRotation(fwd, up);
                    break;
                }
                case "lookitem":
                {
                    // lookitem: aims at the nearest dropped Arsenal item
                    var lc = Object.FindObjectsByType<PlayerLookControls>(FindObjectsSortMode.None).First();
                    Vector3 eye = Camera.main.transform.position;
                    Transform item = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Where(t => t.name.EndsWith(".Dropped(Clone)"))
                        .OrderBy(t => Vector3.Distance(t.position, eye)).FirstOrDefault();
                    if (item == null) { LoggerInstance.Msg("[DEV] no dropped item"); break; }
                    lc.Rotation = Quaternion.LookRotation(item.position - eye, Visual.Fx.Up);
                    LoggerInstance.Msg($"[DEV] looking at {item.name} {Vector3.Distance(item.position, eye):F2} m");
                    break;
                }
                case "pickitem":
                {
                    // pickitem: picks up the nearest world object, as pressing the interact key on it does
                    Vector3 eye = Camera.main.transform.position;
                    var pick = Object.FindObjectsByType<WorldObjects.Physics.PickableWorldObject>(FindObjectsSortMode.None)
                        .OrderBy(p => Vector3.Distance(p.transform.position, eye)).FirstOrDefault();
                    if (pick == null) { LoggerInstance.Msg("[DEV] nothing to pick"); break; }
                    LoggerInstance.Msg($"[DEV] picking {pick.name} at {Vector3.Distance(pick.transform.position, eye):F2} m");
                    pick.EnableHover();
                    pick.DisableHover();
                    pick.Interact(Players.Interface.Model.InteractionType.Interact);
                    break;
                }
                case "dropslot":
                    // dropslot <n>: drops toolbar slot n in the world, like dragging it out of the inventory
                    Svc.Get<Players.Interface.Services.IPlayerInventoryOperationService>()?.MoveItemInWorld(new ItemSlotId { Type = InventoryType.ToolBar, SlotId = byte.Parse(a[1]) });
                    break;
                case "inv":
                {
                    var tb = Svc.Get<PlayerToolbarProvider>();
                    for (byte i = 0; i < 5; i++)
                        LoggerInstance.Msg($"[DEV] toolbar {i}: {tb.GetSlot(i)}");
                }
                    break;
                case "hold":
                {
                    var tool = Svc.Get<Players.Services.LocalSelectedToolTracker>()?.SelectedTool;
                    Transform model = tool?.Transform.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name.EndsWith("Model"));
                    if (model == null) break;
                    float F(int i) => float.Parse(a[i], CultureInfo.InvariantCulture);
                    model.localPosition = new Vector3(F(1), F(2), F(3));
                    if (a.Length > 6) model.localEulerAngles = new Vector3(F(4), F(5), F(6));
                    LoggerInstance.Msg($"[DEV] model {model.name} at {model.localPosition} {model.localEulerAngles}; visuals fp={tool.Visuals.firstPersonPosition} rot={tool.Visuals.firstPersonRotation.eulerAngles}");
                    break;
                }
                case "holdpos":
                {
                    // holdpos x y z [ex ey ez]: moves the held weapon's hip pose (the one HandWeapon puts back every frame)
                    var weapon = Svc.Get<Players.Services.LocalSelectedToolTracker>()?.SelectedTool as Weapons.HandWeapon;
                    if (weapon == null) break;
                    float F(int i) => float.Parse(a[i], CultureInfo.InvariantCulture);
                    var tr = HarmonyLib.Traverse.Create(weapon);
                    tr.Field("_holdPos").SetValue(new Vector3(F(1), F(2), F(3)));
                    if (a.Length > 6) tr.Field("_holdRot").SetValue(Quaternion.Euler(F(4), F(5), F(6)));
                    break;
                }
                case "ik":
                {
                    // ik: for the held tool (game or mod), each hand target vs its bone and shoulder, in the tool visuals' frame
                    var tool = Svc.Get<Players.Services.LocalSelectedToolTracker>()?.SelectedTool;
                    if (tool == null) break;
                    Transform vis = tool.Visuals.transform;
                    Animator an = Object.FindObjectsByType<Animator>(FindObjectsSortMode.None)
                        .Where(x => x.isHuman && x.GetBoneTransform(HumanBodyBones.RightHand) != null)
                        .OrderBy(x => Vector3.Distance(x.GetBoneTransform(HumanBodyBones.RightHand).position, vis.position)).FirstOrDefault();
                    if (an == null) break;
                    void Report(string label, Transform target, HumanBodyBones hand, HumanBodyBones shoulder)
                    {
                        if (target == null) return;
                        Transform h = an.GetBoneTransform(hand), s = an.GetBoneTransform(shoulder);
                        Quaternion rel = Quaternion.Inverse(target.rotation) * h.rotation;
                        LoggerInstance.Msg($"[DEV] {tool.Transform.name} {label}: target {vis.InverseTransformPoint(target.position):F3} (tool {target.parent.name} local {target.localPosition:F3} {target.localEulerAngles:F0}), bone off {Vector3.Distance(h.position, target.position):F3}, shoulder->target {Vector3.Distance(s.position, target.position):F3}, bone-in-target euler {rel.eulerAngles:F0}");
                    }
                    LoggerInstance.Msg($"[DEV] frame: L shoulder {vis.InverseTransformPoint(an.GetBoneTransform(HumanBodyBones.LeftUpperArm).position):F3} R shoulder {vis.InverseTransformPoint(an.GetBoneTransform(HumanBodyBones.RightUpperArm).position):F3} camera {vis.InverseTransformPoint(Camera.main.transform.position):F3} fwd {vis.InverseTransformDirection(Camera.main.transform.forward):F2}");
                    Report("R", tool.Visuals.rightHandTargetTransform, HumanBodyBones.RightHand, HumanBodyBones.RightUpperArm);
                    Report("L1", tool.Visuals.leftHandFirstPersonTargetTransform, HumanBodyBones.LeftHand, HumanBodyBones.LeftUpperArm);
                    Report("L3", tool.Visuals.leftHandThirdPersonTargetTransform, HumanBodyBones.LeftHand, HumanBodyBones.LeftUpperArm);
                    break;
                }
                case "arms":
                {
                    // arms: shoulders, elbows and reach of the local player's arms, in the held weapon's frame
                    var tool = Svc.Get<Players.Services.LocalSelectedToolTracker>()?.SelectedTool;
                    Transform model = tool?.Transform.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name.EndsWith("Model"));
                    if (model == null) break;
                    foreach (Animator an in Object.FindObjectsByType<Animator>(FindObjectsSortMode.None).Where(an => an.isHuman && an.GetBoneTransform(HumanBodyBones.LeftUpperArm) != null))
                    {
                        Transform ls = an.GetBoneTransform(HumanBodyBones.LeftUpperArm), le = an.GetBoneTransform(HumanBodyBones.LeftLowerArm), lh = an.GetBoneTransform(HumanBodyBones.LeftHand);
                        Transform rs = an.GetBoneTransform(HumanBodyBones.RightUpperArm), re = an.GetBoneTransform(HumanBodyBones.RightLowerArm), rh = an.GetBoneTransform(HumanBodyBones.RightHand);
                        if (Vector3.Distance(ls.position, model.position) > 1.5f) continue;
                        LoggerInstance.Msg($"[DEV] {an.name}: L shoulder {model.InverseTransformPoint(ls.position):F3} hand {model.InverseTransformPoint(lh.position):F3} reach {Vector3.Distance(ls.position, le.position) + Vector3.Distance(le.position, lh.position):F3}; R shoulder {model.InverseTransformPoint(rs.position):F3} hand {model.InverseTransformPoint(rh.position):F3} reach {Vector3.Distance(rs.position, re.position) + Vector3.Distance(re.position, rh.position):F3}");
                    }
                    break;
                }
                case "hand":
                {
                    // hand <l|r> x y z ex ey ez: moves a hand target of the held weapon (left: both first and third person)
                    var tool = Svc.Get<Players.Services.LocalSelectedToolTracker>()?.SelectedTool;
                    Transform model = tool?.Transform.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name.EndsWith("Model"));
                    if (model == null) break;
                    float F(int i) => float.Parse(a[i], CultureInfo.InvariantCulture);
                    foreach (Transform t in model.GetComponentsInChildren<Transform>(true).Where(t => a[1] == "l" ? t.name.StartsWith("LeftHandPos") : t.name == "RightHandPos"))
                    {
                        t.localPosition = new Vector3(F(2), F(3), F(4));
                        t.localEulerAngles = new Vector3(F(5), F(6), F(7));
                    }
                    break;
                }
                case "sun":
                    // sun: points the directional lights along the view (to look at things at night); repeats each frame for 60 s
                    MelonLoader.MelonCoroutines.Start(Sun());
                    break;
                case "viewcam":
                    // viewcam <name> <yaw> <pitch> <dist>: renders the held weapon (with the arms) from around it, in its own frame
                    MelonLoader.MelonCoroutines.Start(ViewCam(a[1], float.Parse(a[2], CultureInfo.InvariantCulture), float.Parse(a[3], CultureInfo.InvariantCulture), float.Parse(a[4], CultureInfo.InvariantCulture)));
                    break;
                case "handdir":
                {
                    // handdir <l|r> x y z fx fy fz ux uy uz: places a hand so the bone's x axis (fingers) points along f and its y axis along u (weapon frame)
                    var tool = Svc.Get<Players.Services.LocalSelectedToolTracker>()?.SelectedTool;
                    Transform model = tool?.Transform.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name.EndsWith("Model"));
                    if (model == null) break;
                    float F(int i) => float.Parse(a[i], CultureInfo.InvariantCulture);
                    Vector3 fx = new Vector3(F(5), F(6), F(7)).normalized, uy = new Vector3(F(8), F(9), F(10));
                    uy = Vector3.ProjectOnPlane(uy, fx).normalized;
                    Quaternion bone = Quaternion.LookRotation(Vector3.Cross(fx, uy), uy);
                    Quaternion local = bone * Quaternion.Euler(-90f, 0f, 0f);
                    foreach (Transform t in model.GetComponentsInChildren<Transform>(true).Where(t => a[1] == "l" ? t.name.StartsWith("LeftHandPos") : t.name == "RightHandPos"))
                    {
                        t.localPosition = new Vector3(F(2), F(3), F(4));
                        t.localRotation = local;
                    }
                    LoggerInstance.Msg($"[DEV] hand {a[1]} euler {local.eulerAngles:F1}");
                    break;
                }
                case "handaxes":
                {
                    // handaxes <l|r> x y z  ax ay az  bx by bz: puts the hand bone at x y z with its x axis along a and its y axis along b
                    // (weapon frame), through the offset the IK keeps between the target and the bone (measured now)
                    var tool = Svc.Get<Players.Services.LocalSelectedToolTracker>()?.SelectedTool;
                    if (tool == null) break;
                    float F(int i) => float.Parse(a[i], CultureInfo.InvariantCulture);
                    bool left = a[1] == "l";
                    Transform target = left ? tool.Visuals.leftHandFirstPersonTargetTransform : tool.Visuals.rightHandTargetTransform;
                    Animator an = Object.FindObjectsByType<Animator>(FindObjectsSortMode.None).Where(x => x.isHuman && x.GetBoneTransform(HumanBodyBones.RightHand) != null)
                        .OrderBy(x => Vector3.Distance(x.GetBoneTransform(HumanBodyBones.RightHand).position, target.position)).First();
                    Transform bone = an.GetBoneTransform(left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
                    Quaternion offset = Quaternion.Inverse(target.rotation) * bone.rotation;
                    Vector3 ax = new Vector3(F(5), F(6), F(7)).normalized, by = Vector3.ProjectOnPlane(new Vector3(F(8), F(9), F(10)), ax).normalized;
                    Quaternion boneLocal = Quaternion.LookRotation(Vector3.Cross(ax, by), by);
                    Quaternion local = boneLocal * Quaternion.Inverse(offset);
                    foreach (Transform t in target.parent.GetComponentsInChildren<Transform>(true).Where(t => left ? t.name.StartsWith("LeftHandPos") : t.name == "RightHandPos"))
                    {
                        t.localPosition = new Vector3(F(2), F(3), F(4));
                        t.localRotation = local;
                    }
                    LoggerInstance.Msg($"[DEV] handaxes {a[1]}: euler {local.eulerAngles:F1} (offset {offset.eulerAngles:F0})");
                    break;
                }
                case "rigs":
                {
                    // rigs: every humanoid near the held tool, its left hand (weapon frame) and whether its skin is drawn
                    var tool = Svc.Get<Players.Services.LocalSelectedToolTracker>()?.SelectedTool;
                    Transform model = tool?.Transform.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name.EndsWith("Model"));
                    if (model == null) break;
                    foreach (Animator an in Object.FindObjectsByType<Animator>(FindObjectsSortMode.None).Where(x => x.isHuman))
                    {
                        Transform lh = an.GetBoneTransform(HumanBodyBones.LeftHand);
                        if (lh == null || Vector3.Distance(lh.position, model.position) > 2f) continue;
                        string skins = string.Join(",", an.GetComponentsInChildren<SkinnedMeshRenderer>(true).Select(s => $"{s.name}:{(s.enabled && s.gameObject.activeInHierarchy ? "on" : "off")}/{s.shadowCastingMode}/L{s.gameObject.layer}"));
                        Transform mid = an.GetBoneTransform(HumanBodyBones.LeftMiddleDistal) ?? an.GetBoneTransform(HumanBodyBones.LeftMiddleProximal);
                        Transform thumb = an.GetBoneTransform(HumanBodyBones.LeftThumbDistal) ?? an.GetBoneTransform(HumanBodyBones.LeftThumbProximal);
                        if (mid != null) LoggerInstance.Msg($"[DEV] fingers {model.InverseTransformDirection(mid.position - lh.position).normalized:F2} thumb {(thumb != null ? model.InverseTransformDirection(thumb.position - lh.position).normalized.ToString("F2") : "-")} | bone x{model.InverseTransformDirection(lh.right):F2} y{model.InverseTransformDirection(lh.up):F2} z{model.InverseTransformDirection(lh.forward):F2}");
                        LoggerInstance.Msg($"[DEV] rig {an.transform.parent?.name}/{an.name} L hand {model.InverseTransformPoint(lh.position):F3} y{model.InverseTransformDirection(lh.up):F2} z{model.InverseTransformDirection(lh.forward):F2} skins {skins}");
                    }
                    break;
                }
                case "handcheck":
                    // handcheck: for every held weapon model in the scene, how far each hand bone is from its target
                    foreach (Transform target in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Where(t => t.name == "RightHandPos" || t.name.StartsWith("LeftHandPos")))
                    {
                        HumanBodyBones which = target.name == "RightHandPos" ? HumanBodyBones.RightHand : HumanBodyBones.LeftHand;
                        Transform bone = Object.FindObjectsByType<Animator>(FindObjectsSortMode.None).Where(an => an.isHuman && an.GetBoneTransform(which) != null)
                            .Select(an => an.GetBoneTransform(which)).OrderBy(b => Vector3.Distance(b.position, target.position)).FirstOrDefault();
                        Transform root = target.root;
                        Transform model = target.parent;
                        if (bone == null) continue;
                        LoggerInstance.Msg($"[DEV] {root.name}/{model.name} {target.name}: bone {Vector3.Distance(bone.position, target.position):F3} m, local {model.InverseTransformPoint(bone.position):F3}, angle {Quaternion.Angle(bone.rotation, target.rotation):F0}, axes x{model.InverseTransformDirection(bone.right):F2} y{model.InverseTransformDirection(bone.up):F2} z{model.InverseTransformDirection(bone.forward):F2}");
                    }
                    break;
                case "partlist":
                    foreach (PartSettings ps in Resources.FindObjectsOfTypeAll<PartSettings>().OrderBy(s => s.id))
                        if (a.Length < 2 || ps.name.IndexOf(a[1], System.StringComparison.OrdinalIgnoreCase) >= 0)
                            LoggerInstance.Msg($"[DEV] part settings {ps.id} {ps.name}");
                    break;
                case "physset":
                    foreach (var ps in Resources.FindObjectsOfTypeAll<Ships.Values.ShipPhysicsSettings>())
                        foreach (var fi in ps.GetType().GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance))
                            LoggerInstance.Msg($"[DEV] physics {ps.name}.{fi.Name} = {fi.GetValue(ps)}");
                    break;
                case "shipvel":
                    // shipvel <seconds>: logs the test ship's synced speed every 0.1 s
                    MelonLoader.MelonCoroutines.Start(LogShipVel(float.Parse(a[1], CultureInfo.InvariantCulture)));
                    break;
                case "shipblast":
                    // shipblast <x> <y> <z> [rocket]: a hypersonic (or rocket) blast pinned at that point of the test ship (its own frame), flying along +z
                    Weapons.Explosives.ServerExplodeOnShip(_testShipId, new Vector3(float.Parse(a[1], CultureInfo.InvariantCulture), float.Parse(a[2], CultureInfo.InvariantCulture), float.Parse(a[3], CultureInfo.InvariantCulture)), Vector3.forward, a.Length > 4 && a[4] == "rocket" ? Weapons.WeaponKind.RocketLauncher : Weapons.WeaponKind.HyperMissile);
                    break;
                case "tgt":
                    TargetTest(a);
                    break;
                case "heavyship":
                    SpawnGunship(a.Length > 1 ? float.Parse(a[1], CultureInfo.InvariantCulture) : 4f, true);
                    break;
                case "force":
                    // force a weapon part on (by part id), or "force off"
                    if (a[1] == "off") Vehicles.ShipWeapons.DevForceOn.Clear();
                    else Vehicles.ShipWeapons.DevForceOn.Add(ushort.Parse(a[1]));
                    break;
                case "los":
                {
                    // what lies between the gunship's SAM battery and the test ship
                    if (!Svc.ShipsClient.TryGetTrackedShip(_testShipId, out TrackedShipClient lt) || !lt.IsRigidbodyLoaded) break;
                    var sam = Vehicles.ShipWeapons.Mounts.FirstOrDefault(mm => mm.Kind == Content.ShipContent.SamBatteryId && mm.Pitch != null);
                    if (sam == null) break;
                    Vector3 lf = sam.Pitch.position, lto = Vehicles.ShipWeapons.TargetPoint(lt);
                    uint me = Vehicles.ShipWeapons.RootOf(sam.Ctx.ShipId);
                    LoggerInstance.Msg($"[DEV] los from {lf} to {lto} ({Vector3.Distance(lf, lto):0} m, angle to aim {Vector3.Angle(sam.Pitch.forward, lto - lf):0}): canSee {Vehicles.ShipWeapons.CanSee(lf, lt, me)} airborne {Vehicles.ShipWeapons.Airborne(lt)} candidate {sam.Lock?.Candidate} locked {sam.Lock?.Locked}");
                    var hits = new RaycastHit[16];
                    int nh = WorldTracking.Interface.Values.ClientScene.PhysicsScene.Raycast(lf, (lto - lf).normalized, hits, Vector3.Distance(lf, lto), Weapons.Ballistics.Mask, QueryTriggerInteraction.Ignore);
                    for (int i = 0; i < nh; i++)
                        LoggerInstance.Msg($"[DEV]   hit {hits[i].collider.name} layer {hits[i].collider.gameObject.layer} at {hits[i].distance:0.0} root {Vehicles.ShipWeapons.HitRoot(hits[i])}");
                    break;
                }
                case "track":
                {
                    // is the test ship still tracked / loaded on this client, and how far is it
                    bool tracked = Svc.ShipsClient.TryGetTrackedShip(_testShipId, out TrackedShipClient tk);
                    var gps = Svc.ShipsServer.GetShip(_gunshipId).PhysicsState;
                    var ts = Svc.ShipsServer.GetShip(_testShipId).PhysicsState;
                    LoggerInstance.Msg($"[DEV] track {_testShipId}: tracked {tracked} loaded {(tracked && tk.IsRigidbodyLoaded)} spaces {gps.SpaceId}/{ts.SpaceId} dist {(gps.SpaceId == ts.SpaceId ? Vector3.Distance(gps.Position, ts.Position) : -1f):0} m");
                    if (Vehicles.ShipWeapons.TryGetShipMotion(_testShipId, out Vector3 mc, out Vector3 mv, out _, out _) && Svc.ShipsClient.TryGetTrackedShip(_gunshipId, out TrackedShipClient gk) && gk.IsRigidbodyLoaded)
                        LoggerInstance.Msg($"[DEV]   seen from the gunship: {Vector3.Distance(Vehicles.ShipWeapons.TargetPoint(gk), mc):0} m, {mv.magnitude:0} m/s");
                    {
                        var tr = Svc.Get<WorldTracking.Interface.Services.IWorldTransposeServer>();
                        Vector3 sp = gps.SpaceId == ts.SpaceId ? ts.Position : tr.Position(ts.SpaceId, gps.SpaceId, ts.Position);
                        var csp = Svc.Get<WorldTracking.Interface.Services.ISpacesClientProvider>();
                        if (csp != null && csp.TryGetSpace(tk.PhysicsState.SpaceId, out var spc)) LoggerInstance.Msg($"[DEV]   space {tk.PhysicsState.SpaceId}: {spc.GetType().Name} {spc}");
                        LoggerInstance.Msg($"[DEV]   server: raw {ts.Position} vel {ts.Velocity.magnitude:0}, in gunship space {Vector3.Distance(gps.Position, sp):0} m; client raw {tk.PhysicsState.Position} space {tk.PhysicsState.SpaceId}");
                    }
                    break;
                }
                case "flyfire":
                    // flyfire <speed> [side|away]: the test ship starts racing and the heavy ship's hypersonic launcher (part 10) fires in the same frame
                    Vehicles.ShipWeapons.DevForceOn.Add(10);
                    goto case "fly";
                case "fly":
                {
                    // fly <speed> [side|away]: the test ship cruises at a fixed speed, across the player's view or away from it ("fly 0" stops)
                    float spd = float.Parse(a[1], CultureInfo.InvariantCulture);
                    if (spd <= 0f) { Damage.ShipKnockback.DevFly.Remove(_testShipId); break; }
                    var srv = Svc.ShipsServer.GetShip(_testShipId);
                    var tp = Svc.PlayersServer.ActivePlayers.First().PhysicsState;
                    Vector3 pu = tp.Rotation * Vector3.up, pf = Vector3.ProjectOnPlane(tp.Rotation * Vector3.forward, pu).normalized;
                    Vector3 flyDir = a.Length > 2 && a[2] == "away" ? pf : Vector3.Cross(pu, pf).normalized;
                    Damage.ShipKnockback.DevFly[_testShipId] = flyDir * spd;
                    LoggerInstance.Msg($"[DEV] test ship {_testShipId} flies at {spd:0} m/s, now at {srv.PhysicsState.Position}");
                    break;
                }
                case "rwr":
                    // warn as if piloting the test ship (rwr off = back to normal)
                    Vehicles.ThreatWarning.DevWatch = a.Length > 1 && a[1] == "off" ? 0 : _testShipId;
                    break;
                case "flares":
                {
                    // a flare salvo thrown from the test ship
                    if (!Svc.ShipsClient.TryGetTrackedShip(_testShipId, out TrackedShipClient fs) || !fs.IsRigidbodyLoaded) break;
                    Vector3 fp = Vehicles.ShipWeapons.TargetPoint(fs);
                    var batch = new System.Collections.Generic.List<(Vector3, Vector3)>();
                    for (int i = 0; i < 6; i++)
                    {
                        Vector3 d = (Visual.Fx.Up + Random.insideUnitSphere * 0.8f).normalized;
                        batch.Add((fp + d * 2.5f, d * 22f));
                    }
                    Vehicles.Flares.Launch(batch);
                    break;
                }
                case "pdcself":
                    Vehicles.ShipWeapons.DevPdcSelf = !Vehicles.ShipWeapons.DevPdcSelf;
                    break;
                case "gunship":
                    SpawnGunship(a.Length > 1 ? float.Parse(a[1], CultureInfo.InvariantCulture) : 4f);
                    break;
                case "turretship":
                    // turretship [side] [forward]: a big deck with the fire-control station (part 1) and the four turrets (parts 2-5)
                    SpawnTurretShip(a.Length > 1 ? float.Parse(a[1], CultureInfo.InvariantCulture) : 0f, a.Length > 2 ? float.Parse(a[2], CultureInfo.InvariantCulture) : 6f);
                    break;
                case "gunner":
                {
                    // gunner <yaw> <elevation> [fire] [select n]: steers the fire-control view as the mouse would (yaw/elevation in the selected turret's mount frame)
                    float F(int i) => float.Parse(a[i], CultureInfo.InvariantCulture);
                    Vehicles.GunnerStation.DevView(F(1), F(2), a.Contains("fire"), a.Contains("select") ? int.Parse(a[System.Array.IndexOf(a, "select") + 1]) : -1);
                    break;
                }
                case "partcam":
                {
                    // partcam <partId> <yaw> <pitch> <dist> <name> [height]: renders a part of the test gunship from around it (its own frame)
                    float F(int i) => float.Parse(a[i], CultureInfo.InvariantCulture);
                    if (!Svc.ShipsClient.TryGetVisualPart(new PartContext(_gunshipId, ushort.Parse(a[1])), out Ships.Parts.Common.Model.Reactions.IShipPartVisual pv) || !(pv is Component pc)) { LoggerInstance.Msg("[DEV] no such part"); break; }
                    Transform pt = pc.transform;
                    Vector3 center = pt.TransformPoint(new Vector3(0f, a.Length > 6 ? F(6) : 0.4f, 0f));
                    Quaternion dir = pt.rotation * Quaternion.Euler(F(3), F(2), 0f);
                    MelonLoader.MelonCoroutines.Start(RenderFrom(a[5], center - dir * Vector3.forward * F(4), center, pt.up));
                    break;
                }
                case "cable":
                {
                    // cable <partA> <socketA> <partB> <socketB>: a signal cable between two parts of the test gunship (host)
                    EntityPartSocket S(int i) => new EntityPartSocket(_gunshipId, ushort.Parse(a[i]), byte.Parse(a[i + 1]));
                    uint cid = Svc.Get<Ships.Cables.CablesServerTracker>().AddCable(new CableState { Type = CableType.Signal, PlugA = new CablePlugSocket { EntityPartSocket = S(1) }, PlugB = new CablePlugSocket { EntityPartSocket = S(3) } });
                    LoggerInstance.Msg($"[DEV] cable {cid}; weapon socket id {Content.ShipContent.WeaponSocketId}");
                    break;
                }
                case "turretseat":
                    _turretSeat = ushort.Parse(a[1]);
                    break;
                case "unsit":
                    Svc.Get<Managers.Interface.Services.IShipInteractionNetworkService>().StopInteract();
                    break;
                case "gunnership":
                    // gunnership [fire]: points the fire-control view at the test ship
                    if (Svc.ShipsClient.TryGetTrackedShip(_testShipId, out TrackedShipClient gt) && gt.IsRigidbodyLoaded)
                        Vehicles.GunnerStation.DevAimAt(Vehicles.ShipWeapons.TargetPoint(gt), a.Contains("fire"));
                    break;
                case "gunnerzoom":
                    Vehicles.GunnerStation.DevZoom(int.Parse(a[1]));
                    break;
                case "consolepos":
                {
                    // consolepos x y z: moves the console of every fire-control station in the scene (for fitting it to the seat)
                    float F(int i) => float.Parse(a[i], CultureInfo.InvariantCulture);
                    foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Where(t => t.name.StartsWith("GunnerConsoleModel")))
                        t.localPosition = new Vector3(F(1), F(2), F(3));
                    break;
                }
                case "range":
                {
                    SpawnGunship(a.Length > 1 ? float.Parse(a[1], CultureInfo.InvariantCulture) : 4f);
                    var gsv = Svc.ShipsServer.GetShip(_gunshipId);
                    var ph = gsv.PhysicsState;
                    float dist = a.Length > 2 ? float.Parse(a[2], CultureInfo.InvariantCulture) : 16f;
                    SpawnWallAt(ph.SpaceId, ph.Position + ph.Rotation * new Vector3(0f, -0.3f, dist), ph.Rotation);
                    break;
                }
                case "parts":
                    foreach (var mnt in Vehicles.ShipWeapons.Mounts)
                        LoggerInstance.Msg($"[DEV] mount {mnt.Ctx} kind {mnt.Kind} heat {mnt.Heat:F2} missiles {mnt.Missiles} lock {mnt.LockedTarget} auto {mnt.AutoTarget} yaw {mnt.Yaw:0} elev {mnt.Elevation:0} visual {(mnt.Visual != null)}");
                    if (Svc.ShipsClient.TryGetTrackedShip(_gunshipId, out TrackedShipClient gs))
                        foreach (var sp in gs.StatefulParts)
                            LoggerInstance.Msg($"[DEV] part {sp.Id} {sp.Settings?.id} {sp.State}");
                    break;
                case "holes":
                    foreach (var kv in Damage.HullDamage.ServerStates)
                        LoggerInstance.Msg($"[DEV] {kv.Key} hp={kv.Value.Health:F0} holes={kv.Value.Holes.Count}");
                    break;
            }
        }

        /// <summary>Holds a mouse button through the input system, as if the player pressed it.</summary>
        private System.Collections.IEnumerator Click(string button, float secs)
        {
            var mouse = UnityEngine.InputSystem.Mouse.current;
            var b = button == "right" ? UnityEngine.InputSystem.LowLevel.MouseButton.Right : button == "middle" ? UnityEngine.InputSystem.LowLevel.MouseButton.Middle : UnityEngine.InputSystem.LowLevel.MouseButton.Left;
            float end = Time.realtimeSinceStartup + secs;
            while (Time.realtimeSinceStartup < end)
            {
                UnityEngine.InputSystem.InputSystem.QueueStateEvent(mouse, new UnityEngine.InputSystem.LowLevel.MouseState().WithButton(b, true));
                yield return null;
            }
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(mouse, new UnityEngine.InputSystem.LowLevel.MouseState());
        }

        private static System.Collections.IEnumerator ViewCam(string name, float yaw, float pitch, float dist)
        {
            yield return new WaitForEndOfFrame();
            var tool = Svc.Get<Players.Services.LocalSelectedToolTracker>()?.SelectedTool;
            Transform model = tool?.Transform.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name.EndsWith("Model"));
            Camera main = Camera.main;
            if (model == null || main == null) yield break;
            var go = new GameObject("Arsenal.ViewCam");
            Camera cam = go.AddComponent<Camera>();
            cam.CopyFrom(main);
            cam.fieldOfView = 30f;
            cam.nearClipPlane = 0.01f;
            Vector3 center = model.TransformPoint(new Vector3(0f, -0.02f, 0.02f));
            Quaternion dir = model.rotation * Quaternion.Euler(pitch, yaw, 0f);
            go.transform.position = center - dir * Vector3.forward * dist;
            go.transform.rotation = Quaternion.LookRotation(center - go.transform.position, model.up);
            var rt = new RenderTexture(800, 600, 24);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(800, 600, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 800, 600), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(MelonLoader.Utils.MelonEnvironment.UserDataDirectory, "view_" + name + ".png"), tex.EncodeToPNG());
            cam.targetTexture = null;
            Object.Destroy(go);
            Object.Destroy(rt);
        }

        private static System.Collections.IEnumerator RenderFrom(string name, Vector3 from, Vector3 at, Vector3 up)
        {
            yield return new WaitForEndOfFrame();
            Camera main = Camera.main;
            if (main == null) yield break;
            var go = new GameObject("Arsenal.PartCam");
            Camera cam = go.AddComponent<Camera>();
            cam.CopyFrom(main);
            cam.fieldOfView = 40f;
            cam.nearClipPlane = 0.02f;
            go.transform.position = from;
            go.transform.rotation = Quaternion.LookRotation(at - from, up);
            var rt = new RenderTexture(960, 640, 24);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(960, 640, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 960, 640), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(MelonLoader.Utils.MelonEnvironment.UserDataDirectory, "part_" + name + ".png"), tex.EncodeToPNG());
            cam.targetTexture = null;
            Object.Destroy(go);
            Object.Destroy(rt);
        }

        private static System.Collections.IEnumerator Sun()
        {
            float end = Time.realtimeSinceStartup + 60f;
            while (Time.realtimeSinceStartup < end)
            {
                Camera cam = Camera.main;
                if (cam != null)
                    foreach (Light l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Where(l => l.type == LightType.Directional))
                        l.transform.rotation = cam.transform.rotation * Quaternion.Euler(25f, 20f, 0f);
                yield return null;
            }
        }

        private System.Collections.IEnumerator TapKey(UnityEngine.InputSystem.Key key)
        {
            var kb = UnityEngine.InputSystem.Keyboard.current;
            for (int i = 0; i < 3; i++)
            {
                UnityEngine.InputSystem.InputSystem.QueueStateEvent(kb, new UnityEngine.InputSystem.LowLevel.KeyboardState(key));
                yield return null;
            }
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(kb, new UnityEngine.InputSystem.LowLevel.KeyboardState());
        }

        private System.Collections.IEnumerator ReleaseAfter(Tools.Interface.Model.IInteractableTool tool, float secs)
        {
            float end = Time.realtimeSinceStartup + secs;
            yield return null;
            while (Time.realtimeSinceStartup < end) yield return null;
            tool.PrimaryActionEnd();
        }

        private void SpawnTestWall(float distance)
        {
            TrackedPlayerServer p = Svc.PlayersServer.ActivePlayers.First();
            var phys = p.PhysicsState;
            Quaternion rot = phys.Rotation;
            Vector3 fwd = Vector3.ProjectOnPlane(rot * Vector3.forward, rot * Vector3.up).normalized;
            Quaternion shipRot = Quaternion.LookRotation(fwd, rot * Vector3.up);
            SpawnWallAt(phys.SpaceId, phys.Position + fwd * distance + shipRot * new Vector3(0, 0.6f, -0.5f), shipRot);
        }

        /// <summary>Dev: a floor with one of each angled hull piece on it, facing the player.</summary>
        private void SpawnShapesAt(uint space, Vector3 position, Quaternion shipRot)
        {
            var settings = new PartSettingsProvider();
            HullPartSettings S(ushort id) => (HullPartSettings)settings.GetPartSettingById(id);
            var parts = new System.Collections.Generic.List<HullPart>();
            for (int x = -3; x <= 3; x++)
                for (int z = -1; z <= 1; z++)
                    parts.Add(new HullPart(S(1), new HullPartPlacement(new Vector3Int(x, 0, z), VoxelPartType.Floor, PartOrientation.Up, PartOrientation.Forward)));
            void Add(int x, ushort id, VoxelPartType type)
            {
                HullPartSettings s = S(id);
                parts.Add(new HullPart(s, new HullPartPlacement(new Vector3Int(x, 0, 0), type, s.defaultForward, s.defaultUp)));
            }
            Add(-3, 9, VoxelPartType.Interior);    // iron slope
            Add(-2, 10, VoxelPartType.Interior);   // iron slope corner
            Add(-1, 5, VoxelPartType.Interior);    // iron pivoted wall
            Add(0, 12, VoxelPartType.ForwardWall); // iron wall corner
            Add(1, 72, VoxelPartType.Interior);    // wood slope
            Add(2, 33, VoxelPartType.Interior);    // glass slope
            Add(3, 11, VoxelPartType.Interior);    // iron slope corner, inverted
            uint shipId = Svc.Get<IWorldObjectsIdsServerProvider>().CreateUniqueIdForObject();
            var snap = new ShipStateSnapshot
            {
                Id = shipId,
                RelativeSpaceId = Svc.Get<WorldTracking.Interface.Services.ISpacesServerProvider>().GetOrCreateShipRelativeSpace(shipId),
                SpaceId = space,
                Position = position,
                Rotation = shipRot,
                Velocity = Vector3.zero,
                RoomGasState = new RoomGasState[0],
                HullParts = parts.ToArray(),
                StatefulParts = new StatefulPart[0],
            };
            Svc.ShipsServer.LoadShip(snap);
            _testShipId = shipId;
            LoggerInstance.Msg($"[DEV] shapes ship {shipId} with {parts.Count} parts");
        }

        private void SpawnWallAt(uint space, Vector3 position, Quaternion shipRot, bool hollow = false)
        {
            var settings = new PartSettingsProvider();
            HullPartSettings iron = (HullPartSettings)settings.GetPartSettingById(2);
            HullPartSettings wood = (HullPartSettings)settings.GetPartSettingById(75);
            HullPartSettings glass = (HullPartSettings)settings.GetPartSettingById(7);
            HullPartSettings floor = (HullPartSettings)settings.GetPartSettingById(1);
            var parts = new System.Collections.Generic.List<HullPart>();
            // hollow: two big plates 14 m apart, the centre of mass in the empty middle (a large hull seen from far)
            if (hollow)
                for (int x = -6; x <= 6; x++)
                    for (int z = -6; z <= 6; z++)
                        foreach (int y in new[] { 0, 14 })
                            parts.Add(new HullPart(iron, new HullPartPlacement(new Vector3Int(x, y, z), VoxelPartType.Floor, PartOrientation.Up, PartOrientation.Forward)));
            for (int x = -4; x <= 4 && !hollow; x++)
            {
                HullPartSettings s = x < -1 ? wood : x > 1 ? glass : iron;
                if (x == -4 || x == 4) { for (int z = 0; z <= 5; z++) parts.Add(new HullPart(floor, new HullPartPlacement(new Vector3Int(x, 0, z), VoxelPartType.Floor, PartOrientation.Up, PartOrientation.Forward))); continue; }
                for (int y = 0; y < 3; y++)
                    parts.Add(new HullPart(s, new HullPartPlacement(new Vector3Int(x, y, 0), VoxelPartType.ForwardWall, PartOrientation.Forward, PartOrientation.Up)));
                // a second iron wall behind the middle, to see penetration
                if (x >= -1 && x <= 1)
                    for (int y = 0; y < 3; y++)
                        parts.Add(new HullPart(iron, new HullPartPlacement(new Vector3Int(x, y, 2), VoxelPartType.ForwardWall, PartOrientation.Forward, PartOrientation.Up)));
                for (int z = 0; z <= 5; z++)
                    parts.Add(new HullPart(floor, new HullPartPlacement(new Vector3Int(x, 0, z), VoxelPartType.Floor, PartOrientation.Up, PartOrientation.Forward)));
            }
            uint id = Svc.Get<IWorldObjectsIdsServerProvider>().CreateUniqueIdForObject();
            var snap = new ShipStateSnapshot
            {
                Id = id,
                RelativeSpaceId = Svc.Get<WorldTracking.Interface.Services.ISpacesServerProvider>().GetOrCreateShipRelativeSpace(id),
                SpaceId = space,
                Position = position,
                Rotation = shipRot,
                Velocity = Vector3.zero,
                RoomGasState = new RoomGasState[0],
                HullParts = parts.ToArray(),
                StatefulParts = new StatefulPart[0],
            };
            Svc.ShipsServer.LoadShip(snap);
            _testShipId = id;
            LoggerInstance.Msg($"[DEV] test wall ship {id} with {parts.Count} parts");
        }

        private uint _gunshipId;

        private void SpawnGunship(float side, bool heavy = false)
        {
            TrackedPlayerServer p = Svc.PlayersServer.ActivePlayers.First();
            var settings = new PartSettingsProvider();
            HullPartSettings floor = (HullPartSettings)settings.GetPartSettingById(1);
            var hull = new System.Collections.Generic.List<HullPart>();
            for (int x = -2; x <= 2; x++)
                for (int z = -2; z <= 4; z++)
                    hull.Add(new HullPart(floor, new HullPartPlacement(new Vector3Int(x, 0, z), VoxelPartType.Floor, PartOrientation.Up, PartOrientation.Forward)));
            var parts = heavy ? new[]
            {
                new StatefulPart(1, settings.GetPartSettingById(Content.ShipContent.AdvancedPilotSeatId), new FloatingPartPlacement(new Vector3(0, -0.46f, 0), PartOrientation.Forward)),
                new StatefulPart(2, settings.GetPartSettingById(Content.ShipContent.PlasmaCannonId), new FloatingPartPlacement(new Vector3(-1.5f, -0.46f, 2.5f), PartOrientation.Forward)),
                new StatefulPart(3, settings.GetPartSettingById(Content.ShipContent.RailgunId), new FloatingPartPlacement(new Vector3(0f, -0.46f, 3.2f), PartOrientation.Forward)),
                new StatefulPart(4, settings.GetPartSettingById(Content.ShipContent.TorpedoId), new FloatingPartPlacement(new Vector3(1.5f, -0.46f, 2.5f), PartOrientation.Forward)),
                new StatefulPart(5, settings.GetPartSettingById(Content.ShipContent.ArmoryId), new FloatingPartPlacement(new Vector3(-1.5f, -0.46f, -1.5f), PartOrientation.Forward)),
                new StatefulPart(6, settings.GetPartSettingById(Content.ShipContent.IonCannonId), new FloatingPartPlacement(new Vector3(-2.2f, -0.46f, 0.8f), PartOrientation.Forward)),
                new StatefulPart(7, settings.GetPartSettingById(Content.ShipContent.PdcId), new FloatingPartPlacement(new Vector3(1.5f, -0.46f, -1.5f), PartOrientation.Forward)),
                new StatefulPart(8, settings.GetPartSettingById(Content.ShipContent.RocketPodId), new FloatingPartPlacement(new Vector3(-2.2f, -0.46f, 2.6f), PartOrientation.Forward)),
                new StatefulPart(9, settings.GetPartSettingById(Content.ShipContent.SamBatteryId), new FloatingPartPlacement(new Vector3(2.2f, -0.46f, 0.8f), PartOrientation.Forward)),
                new StatefulPart(10, settings.GetPartSettingById(Content.ShipContent.HyperLauncherId), new FloatingPartPlacement(new Vector3(0f, -0.46f, -1.6f), PartOrientation.Forward)),
            } : new[]
            {
                new StatefulPart(1, settings.GetPartSettingById(Content.ShipContent.AdvancedPilotSeatId), new FloatingPartPlacement(new Vector3(0, -0.46f, 0), PartOrientation.Forward)),
                new StatefulPart(2, settings.GetPartSettingById(Content.ShipContent.MachineGunId), new FloatingPartPlacement(new Vector3(-1.5f, -0.46f, 2.5f), PartOrientation.Forward)),
                new StatefulPart(3, settings.GetPartSettingById(Content.ShipContent.MissilePodId), new FloatingPartPlacement(new Vector3(0f, -0.46f, 3.2f), PartOrientation.Forward)),
                new StatefulPart(4, settings.GetPartSettingById(Content.ShipContent.LaserCannonId), new FloatingPartPlacement(new Vector3(1.5f, -0.46f, 2.5f), PartOrientation.Forward)),
                new StatefulPart(5, settings.GetPartSettingById(Content.ShipContent.ArmoryId), new FloatingPartPlacement(new Vector3(-1.5f, -0.46f, -1.5f), PartOrientation.Forward)),
                new StatefulPart(6, settings.GetPartSettingById(Content.ShipContent.FlareDispenserId), new FloatingPartPlacement(new Vector3(1.5f, -0.46f, -1.5f), PartOrientation.Forward)),
                new StatefulPart(7, settings.GetPartSettingById(Content.ShipContent.LaserGreenId), new FloatingPartPlacement(new Vector3(-2.2f, -0.46f, 0.8f), PartOrientation.Forward)),
                new StatefulPart(8, settings.GetPartSettingById(Content.ShipContent.LaserBlueId), new FloatingPartPlacement(new Vector3(2.2f, -0.46f, 0.8f), PartOrientation.Forward)),
            };
            var phys = p.PhysicsState;
            Quaternion rot = phys.Rotation;
            Vector3 up = rot * Vector3.up;
            Vector3 fwd = Vector3.ProjectOnPlane(rot * Vector3.forward, up).normalized;
            Quaternion shipRot = Quaternion.LookRotation(fwd, up);
            uint id = Svc.Get<IWorldObjectsIdsServerProvider>().CreateUniqueIdForObject();
            var snap = new ShipStateSnapshot
            {
                Id = id,
                RelativeSpaceId = Svc.Get<WorldTracking.Interface.Services.ISpacesServerProvider>().GetOrCreateShipRelativeSpace(id),
                SpaceId = phys.SpaceId,
                Position = phys.Position + shipRot * new Vector3(side, 0.7f, -1f),
                Rotation = shipRot,
                Velocity = Vector3.zero,
                RoomGasState = new RoomGasState[0],
                HullParts = hull.ToArray(),
                StatefulParts = parts,
            };
            Svc.ShipsServer.LoadShip(snap);
            _gunshipId = id;
            LoggerInstance.Msg($"[DEV] gunship {id}");
        }

        private static ushort _turretSeat = Content.ShipContent.GunnerStationId;

        private void SpawnTurretShip(float side, float forward)
        {
            TrackedPlayerServer p = Svc.PlayersServer.ActivePlayers.First();
            var settings = new PartSettingsProvider();
            HullPartSettings floor = (HullPartSettings)settings.GetPartSettingById(1);
            var hull = new System.Collections.Generic.List<HullPart>();
            for (int x = -4; x <= 4; x++)
                for (int z = -4; z <= 13; z++)
                    hull.Add(new HullPart(floor, new HullPartPlacement(new Vector3Int(x, 0, z), VoxelPartType.Floor, PartOrientation.Up, PartOrientation.Forward)));
            var parts = new[]
            {
                new StatefulPart(1, settings.GetPartSettingById(_turretSeat), new FloatingPartPlacement(new Vector3(0, -0.46f, -3f), PartOrientation.Forward)),
                new StatefulPart(2, settings.GetPartSettingById(Content.ShipContent.QuadTurretId), new FloatingPartPlacement(new Vector3(-2.5f, -0.46f, -1f), PartOrientation.Forward)),
                new StatefulPart(3, settings.GetPartSettingById(Content.ShipContent.IonTurretId), new FloatingPartPlacement(new Vector3(2.5f, -0.46f, -1f), PartOrientation.Forward)),
                new StatefulPart(4, settings.GetPartSettingById(Content.ShipContent.TurbolaserId), new FloatingPartPlacement(new Vector3(-2.2f, -0.46f, 3.5f), PartOrientation.Forward)),
                new StatefulPart(5, settings.GetPartSettingById(Content.ShipContent.ColossusId), new FloatingPartPlacement(new Vector3(2.2f, -0.46f, 3.8f), PartOrientation.Forward)),
                new StatefulPart(6, settings.GetPartSettingById(_turretSeat), new FloatingPartPlacement(new Vector3(2.5f, -0.46f, -3.2f), PartOrientation.Forward)),
                new StatefulPart(7, settings.GetPartSettingById(Content.ShipContent.AnnihilatorId), new FloatingPartPlacement(new Vector3(0f, -0.46f, 9.5f), PartOrientation.Forward)),
            };
            var phys = p.PhysicsState;
            Quaternion rot = phys.Rotation;
            Vector3 up = rot * Vector3.up;
            Vector3 fwd = Vector3.ProjectOnPlane(rot * Vector3.forward, up).normalized;
            Quaternion shipRot = Quaternion.LookRotation(fwd, up);
            uint id = Svc.Get<IWorldObjectsIdsServerProvider>().CreateUniqueIdForObject();
            Svc.ShipsServer.LoadShip(new ShipStateSnapshot
            {
                Id = id,
                RelativeSpaceId = Svc.Get<WorldTracking.Interface.Services.ISpacesServerProvider>().GetOrCreateShipRelativeSpace(id),
                SpaceId = phys.SpaceId,
                Position = phys.Position + shipRot * new Vector3(side, 0.7f, forward),
                Rotation = shipRot,
                Velocity = Vector3.zero,
                RoomGasState = new RoomGasState[0],
                HullParts = hull.ToArray(),
                StatefulParts = parts,
            });
            _gunshipId = id;
            LoggerInstance.Msg($"[DEV] turret ship {id}");
        }

        private System.Collections.IEnumerator MeasureFps(float secs)
        {
            float start = Time.realtimeSinceStartup, worst = 0f;
            int frames = 0, maxFx = 0;
            while (Time.realtimeSinceStartup - start < secs)
            {
                yield return null;
                frames++;
                worst = Mathf.Max(worst, Time.unscaledDeltaTime);
                maxFx = Mathf.Max(maxFx, Visual.Fx.DevAlive);
            }
            float avg = (Time.realtimeSinceStartup - start) / Mathf.Max(1, frames);
            LoggerInstance.Msg($"[DEV] fps avg {1f / avg:0} ({avg * 1000f:0.0} ms), worst frame {worst * 1000f:0.0} ms, effects alive max {maxFx}, pooled {Visual.Fx.DevPooled}, GC {System.GC.CollectionCount(0)}");
        }

        private System.Collections.IEnumerator LogShipVel(float secs)
        {
            float end = Time.time + secs, next = 0f;
            while (Time.time < end)
            {
                if (Time.time >= next && Svc.ShipsServer.TryGetTrackedShip(_testShipId, out TrackedShipServer s))
                {
                    next = Time.time + 0.1f;
                    LoggerInstance.Msg($"[DEV] ship {_testShipId} speed {s.PhysicsState.Velocity.magnitude:0.0} m/s");
                }
                yield return null;
            }
        }

        private static uint _tgtShooter, _tgtBeaconShip;

        /// <summary>
        /// tgt spawn &lt;targeterSettings&gt; &lt;beaconSettings&gt; | tgt state | tgt killbeacon | tgt killship:
        /// a targeting computer on one ship aimed at a beacon on another, then the beacon or its ship goes away.
        /// </summary>
        private void TargetTest(string[] a)
        {
            switch (a[1])
            {
                case "spawn":
                {
                    var settings = new PartSettingsProvider();
                    TrackedPlayerServer p = Svc.PlayersServer.ActivePlayers.First();
                    HullPartSettings floor = (HullPartSettings)settings.GetPartSettingById(1);
                    uint Spawn(ushort settingsId, float side)
                    {
                        var hull = new System.Collections.Generic.List<HullPart>();
                        for (int x = -1; x <= 1; x++)
                            for (int z = -1; z <= 1; z++)
                                hull.Add(new HullPart(floor, new HullPartPlacement(new Vector3Int(x, 0, z), VoxelPartType.Floor, PartOrientation.Up, PartOrientation.Forward)));
                        var phys = p.PhysicsState;
                        Quaternion rot = phys.Rotation;
                        Vector3 up = rot * Vector3.up;
                        Quaternion shipRot = Quaternion.LookRotation(Vector3.ProjectOnPlane(rot * Vector3.forward, up).normalized, up);
                        uint id = Svc.Get<IWorldObjectsIdsServerProvider>().CreateUniqueIdForObject();
                        Svc.ShipsServer.LoadShip(new ShipStateSnapshot
                        {
                            Id = id,
                            RelativeSpaceId = Svc.Get<ISpacesServerProvider>().GetOrCreateShipRelativeSpace(id),
                            SpaceId = phys.SpaceId,
                            Position = phys.Position + shipRot * new Vector3(side, 0.7f, 3f),
                            Rotation = shipRot,
                            Velocity = Vector3.zero,
                            RoomGasState = new RoomGasState[0],
                            HullParts = hull.ToArray(),
                            StatefulParts = new[] { new StatefulPart(1, settings.GetPartSettingById(settingsId), new FloatingPartPlacement(new Vector3(0, -0.46f, 0), PartOrientation.Forward)) },
                        });
                        return id;
                    }
                    _tgtShooter = Spawn(ushort.Parse(a[2]), -4f);
                    _tgtBeaconShip = Spawn(ushort.Parse(a[3]), 4f);
                    Svc.ShipsServer.TryGetTrackedShip(_tgtShooter, out TrackedShipServer shooter);
                    shooter.SelectTarget(1, new Target { TargetType = TargetType.Beacon, Beacon = new PartContext(_tgtBeaconShip, 1) });
                    LoggerInstance.Msg($"[DEV] tgt shooter {_tgtShooter} aims at beacon 1 of ship {_tgtBeaconShip}");
                    goto case "state";
                }
                case "state":
                    foreach (TrackedShipServer ship in Svc.ShipsServer.Ships)
                        foreach (StatefulPart part in ship.GetAllStatefulParts<Ships.Interface.Model.Parts.State.TargetingComputerState>())
                        {
                            var s = (Ships.Interface.Model.Parts.State.TargetingComputerState)part.State;
                            LoggerInstance.Msg($"[DEV] tgt ship {ship.Id} targeter {part.Id}: hasTarget={s.HasTarget}{(s.HasTarget ? $" {s.Target.TargetType} {s.Target.Beacon}" : "")}");
                        }
                    break;
                case "killbeacon":
                    Svc.ShipsServer.TryGetTrackedShip(_tgtBeaconShip, out TrackedShipServer bs);
                    bs.DestroyStatefulPart(1);
                    goto case "state";
                case "killship":
                    Svc.ShipsServer.TryGetTrackedShip(_tgtBeaconShip, out TrackedShipServer ks);
                    Svc.ShipsServer.RemoveShip(ks.State);
                    goto case "state";
            }
        }

        private void LookAtShip(uint shipId, Vector3 local)
        {
            if (!Svc.ShipsClient.TryGetTrackedShip(shipId, out TrackedShipClient ship) || !ship.IsRigidbodyLoaded)
            {
                LoggerInstance.Msg("[DEV] test ship not loaded");
                return;
            }
            Transform rb = ship.ShipRigidBody.transform;
            Vector3 target = rb.TransformPoint(local);
            var look = Object.FindObjectsByType<PlayerLookControls>(FindObjectsSortMode.None).First();
            Camera cam = Camera.main;
            Vector3 up = look.Rotation * Vector3.up;
            look.Rotation = Quaternion.LookRotation(target - cam.transform.position, up);
            LoggerInstance.Msg($"[DEV] looking at {target} from {cam.transform.position}");
        }
    }
}
#endif
