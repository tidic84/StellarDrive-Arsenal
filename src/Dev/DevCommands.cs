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
                    LookAtTestShip(a.Length > 1 ? float.Parse(a[1], CultureInfo.InvariantCulture) : 0f, a.Length > 2 ? float.Parse(a[2], CultureInfo.InvariantCulture) : 1f);
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
            var phys = p.PhysicsState;
            Quaternion rot = phys.Rotation;
            Vector3 fwd = Vector3.ProjectOnPlane(rot * Vector3.forward, rot * Vector3.up).normalized;
            Quaternion shipRot = Quaternion.LookRotation(fwd, rot * Vector3.up);
            uint id = Svc.Get<IWorldObjectsIdsServerProvider>().CreateUniqueIdForObject();
            var snap = new ShipStateSnapshot
            {
                Id = id,
                RelativeSpaceId = Svc.Get<WorldTracking.Interface.Services.ISpacesServerProvider>().GetOrCreateShipRelativeSpace(id),
                SpaceId = phys.SpaceId,
                Position = phys.Position + fwd * distance + shipRot * new Vector3(0, 0.6f, -0.5f),
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

        private void LookAtTestShip(float x, float y)
        {
            if (!Svc.ShipsClient.TryGetTrackedShip(_testShipId, out TrackedShipClient ship) || !ship.IsRigidbodyLoaded)
            {
                LoggerInstance.Msg("[DEV] test ship not loaded");
                return;
            }
            Transform rb = ship.ShipRigidBody.transform;
            Vector3 target = rb.TransformPoint(new Vector3(x, y, 0.5f));
            var look = Object.FindObjectsByType<PlayerLookControls>(FindObjectsSortMode.None).First();
            Camera cam = Camera.main;
            Vector3 up = look.Rotation * Vector3.up;
            look.Rotation = Quaternion.LookRotation(target - cam.transform.position, up);
            LoggerInstance.Msg($"[DEV] looking at {target} from {cam.transform.position}");
        }
    }
}
#endif
