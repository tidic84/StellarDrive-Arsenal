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
                case "select":
                    Object.FindObjectsByType<ToolBelt>(FindObjectsSortMode.None).First().SelectTool(int.Parse(a[1]));
                    break;
                case "wall":
                    SpawnTestWall(a.Length > 1 ? float.Parse(a[1], CultureInfo.InvariantCulture) : 6f);
                    break;
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
                case "aim":
                    Svc.Get<Players.Services.LocalSelectedToolTracker>()?.SelectedTool?.SecondaryActionBegin();
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
                case "gunship":
                    SpawnGunship(a.Length > 1 ? float.Parse(a[1], CultureInfo.InvariantCulture) : 4f);
                    break;
                case "range":
                {
                    SpawnGunship(a.Length > 1 ? float.Parse(a[1], CultureInfo.InvariantCulture) : 4f);
                    var gsv = Svc.ShipsServer.GetShip(_gunshipId);
                    var ph = gsv.PhysicsState;
                    float dist = a.Length > 2 ? float.Parse(a[2], CultureInfo.InvariantCulture) : 16f;
                    SpawnWallAt(ph.SpaceId, ph.Position + ph.Rotation * new Vector3(0f, -0.3f, dist), ph.Rotation);
                    break;
                }
                case "gun":
                    Vehicles.GunnerSeat.DevSetButtons(new PartContext(_gunshipId, 1), byte.Parse(a[1]));
                    break;
                case "parts":
                    foreach (var mnt in Vehicles.ShipWeapons.Mounts)
                        LoggerInstance.Msg($"[DEV] mount {mnt.Ctx} kind {mnt.Kind} heat {mnt.Heat:F2} missiles {mnt.Missiles} lock {mnt.LockedTarget} visual {(mnt.Visual != null)}");
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

        private void SpawnWallAt(uint space, Vector3 position, Quaternion shipRot)
        {
            var settings = new PartSettingsProvider();
            HullPartSettings iron = (HullPartSettings)settings.GetPartSettingById(2);
            HullPartSettings wood = (HullPartSettings)settings.GetPartSettingById(75);
            HullPartSettings glass = (HullPartSettings)settings.GetPartSettingById(7);
            HullPartSettings floor = (HullPartSettings)settings.GetPartSettingById(1);
            var parts = new System.Collections.Generic.List<HullPart>();
            for (int x = -4; x <= 4; x++)
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

        private void SpawnGunship(float side)
        {
            TrackedPlayerServer p = Svc.PlayersServer.ActivePlayers.First();
            var settings = new PartSettingsProvider();
            HullPartSettings floor = (HullPartSettings)settings.GetPartSettingById(1);
            var hull = new System.Collections.Generic.List<HullPart>();
            for (int x = -2; x <= 2; x++)
                for (int z = -2; z <= 4; z++)
                    hull.Add(new HullPart(floor, new HullPartPlacement(new Vector3Int(x, 0, z), VoxelPartType.Floor, PartOrientation.Up, PartOrientation.Forward)));
            var parts = new[]
            {
                new StatefulPart(1, settings.GetPartSettingById(Content.ShipContent.GunnerSeatId), new FloatingPartPlacement(new Vector3(0, -0.46f, 0), PartOrientation.Forward)),
                new StatefulPart(2, settings.GetPartSettingById(Content.ShipContent.MachineGunId), new FloatingPartPlacement(new Vector3(-1.5f, -0.46f, 2.5f), PartOrientation.Forward)),
                new StatefulPart(3, settings.GetPartSettingById(Content.ShipContent.MissilePodId), new FloatingPartPlacement(new Vector3(0f, -0.46f, 3.2f), PartOrientation.Forward)),
                new StatefulPart(4, settings.GetPartSettingById(Content.ShipContent.LaserCannonId), new FloatingPartPlacement(new Vector3(1.5f, -0.46f, 2.5f), PartOrientation.Forward)),
                new StatefulPart(5, settings.GetPartSettingById(Content.ShipContent.ArmoryId), new FloatingPartPlacement(new Vector3(-1.5f, -0.46f, -1.5f), PartOrientation.Forward)),
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
            var cables = Svc.Get<Ships.Cables.CablesServerTracker>();
            (ushort part, byte seatSocket)[] wires = { (2, 6), (3, 7), (4, 8) };
            foreach (var (part, seatSocket) in wires)
                cables.AddCable(new CableState
                {
                    Type = CableType.Signal,
                    PlugA = new CablePlugSocket { EntityPartSocket = new EntityPartSocket(id, 1, seatSocket) },
                    PlugB = new CablePlugSocket { EntityPartSocket = new EntityPartSocket(id, part, 0) },
                });
            LoggerInstance.Msg($"[DEV] gunship {id}");
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
