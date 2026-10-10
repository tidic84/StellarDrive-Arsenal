using System.Collections.Generic;
using Arsenal.Core;
using Arsenal.Damage;
using Arsenal.Net;
using Arsenal.Visual;
using Planets.Interface.Model;
using Players;
using Ships;
using Ships.Interface.Model;
using Ships.Interface.Model.Parts;
using Ships.Interface.Model.Placement;
using Ships.Interface.Services;
using Tools.Utils;
using UnityEngine;
using WorldTracking.Interface.Values;

namespace Arsenal.Weapons
{
    internal struct ImpactFx
    {
        public Vector3 Pos, Normal;
        public ImpactKind Kind;
        public Color Tint;
    }

    /// <summary>
    /// Hit-scan bullets with penetration, run by the client that fires (like the game's own tools).
    /// The ray crosses existing holes freely, punches new ones when it has enough penetration left,
    /// stops (and dents the panel) otherwise. Results go to the server, which owns the damage.
    /// </summary>
    internal static class Ballistics
    {
        private static int _mask = -1;

        public static int Mask
        {
            get
            {
                if (_mask == -1)
                    _mask = LayerMask.GetMask("Default", "Interactable", "Planet", "Ship", "WorldObject");
                return _mask;
            }
        }

        public static readonly System.Random Rng = new System.Random();

        /// <summary>Fires one bullet in the client physics scene. Returns where it ended for the tracer.</summary>
        public static Vector3 Fire(Vector3 origin, Vector3 dir, BulletSpec spec, List<ImpactFx> impacts)
        {
            PhysicsScene scene = ClientScene.PhysicsScene;
            float penetration = spec.Penetration;
            float panelDamage = spec.PanelDamage;
            float playerDamage = spec.PlayerDamage;
            float travelled = 0f;
            Vector3 pos = origin;

            for (int bounce = 0; bounce < 12; bounce++)
            {
                float left = spec.Range - travelled;
                if (left <= 0f) break;
                if (!scene.Raycast(pos, dir, out RaycastHit hit, left, Mask, QueryTriggerInteraction.Ignore))
                    return pos + dir * left;
                travelled += hit.distance;
                Collider col = hit.collider;

                if (ShipPartsTagDetection.ColliderIsHull(col) && TryResolvePanel(hit, out PanelKey key, out HullPart part, out Vector2 local))
                {
                    float cos = Mathf.Abs(Vector3.Dot(dir, hit.normal));
                    float skip = Mathf.Min(0.6f, 0.12f / Mathf.Max(0.2f, cos));
                    bool holeAble = HullDamage.CanHaveHoles(part);
                    if (holeAble && HullDamage.TryGetClientHoles(key, out HoleMask existing) && existing.IsHoleAt(local.x, local.y))
                    {
                        // through an existing hole
                        pos = hit.point + dir * skip;
                        travelled += skip;
                        continue;
                    }
                    float resistance = Materials.Resistance(part.MaterialType) / Mathf.Max(0.35f, cos);
                    ImpactKind kind = Kind(part.MaterialType);
                    Color tint = part.Color.Front.AsColorType;
                    impacts.Add(new ImpactFx { Pos = hit.point, Normal = hit.normal, Kind = kind, Tint = tint });
                    if (penetration >= resistance)
                    {
                        var holes = new HoleMask();
                        if (holeAble) holes.PunchDisc(local.x, local.y, spec.HoleRadius, Rng);
                        HullDamage.ReportHit(key, holes, panelDamage);
                        if (holeAble) HullDamage.PredictHoles(key, holes);
                        penetration -= resistance;
                        panelDamage *= 0.75f;
                        playerDamage *= 0.8f;
                        Vector3 exit = hit.point + dir * skip;
                        impacts.Add(new ImpactFx { Pos = exit - dir * 0.08f, Normal = dir, Kind = kind, Tint = tint });
                        pos = exit;
                        travelled += skip;
                        continue;
                    }
                    // stopped: a dent only
                    HullDamage.ReportHit(key, new HoleMask(), panelDamage * 0.35f);
                    return hit.point;
                }

                if (TryGetOtherPlayer(hit, out uint playerId))
                {
                    impacts.Add(new ImpactFx { Pos = hit.point, Normal = hit.normal, Kind = ImpactKind.Flesh });
                    PlayerDamage.Report(playerId, playerDamage);
                    return hit.point;
                }

                IMinableObject minable = col.GetComponentInParent<IMinableObject>();
                if (minable != null)
                {
                    try { minable.ReduceHealth(spec.ObjectDamage); } catch { }
                    impacts.Add(new ImpactFx { Pos = hit.point, Normal = hit.normal, Kind = ImpactKind.Wood });
                    return hit.point;
                }

                bool ground = col.gameObject.layer == LayerMask.NameToLayer("Planet");
                impacts.Add(new ImpactFx { Pos = hit.point, Normal = hit.normal, Kind = ground ? ImpactKind.Ground : ImpactKind.Metal, Tint = new Color(0.6f, 0.6f, 0.6f) });
                return hit.point;
            }
            return pos;
        }

        public static ImpactKind Kind(HullMaterialType m) => m switch
        {
            HullMaterialType.Wood => ImpactKind.Wood,
            HullMaterialType.Glass => ImpactKind.Glass,
            _ => ImpactKind.Metal,
        };

        /// <summary>Maps a raycast hit on a hull collider to the panel, like the recycler does, plus the hit point on the panel plane.</summary>
        public static bool TryResolvePanel(RaycastHit hit, out PanelKey key, out HullPart part, out Vector2 local)
        {
            key = default;
            part = default;
            local = default;
            IShipPartTargeter targeter = hit.collider.GetComponentInParent<IShipPartTargeter>();
            if (targeter == null) return false;
            PartHit partHit = targeter.ConvertHitToLocalHit(hit);
            PartOrientation? orientation = ShipPartsTagDetection.GuessPartOrientationFromTag(hit.collider);
            Vector3Int coord = orientation.HasValue ? RelativeDirectionsMap.Map[orientation.Value].GetCoord(partHit.Pos) : Vector3Int.RoundToInt(partHit.Pos);
            var pos = new HullPartPosition(coord, ShipPartsTagDetection.GetPartType(hit.collider));
            if (!Svc.ShipsClient.TryGetTrackedShip(targeter.ShipId, out TrackedShipClient ship)) return false;
            HullPart? p = ship.GetVoxel(coord)?.GetPartFromType(pos.Type);
            if (!p.HasValue) return false;
            part = p.Value;
            key = new PanelKey(targeter.ShipId, pos);
            HullPartPlacement placement = part.HullPlacement;
            Vector3 q = Quaternion.Inverse(placement.Rotation) * (partHit.Pos - placement.LocalPosition);
            local = new Vector2(q.x, q.y);
            return true;
        }

        public static bool TryGetOtherPlayer(RaycastHit hit, out uint playerId)
        {
            playerId = 0;
            if (hit.rigidbody == null || Svc.PlayersClient == null) return false;
            foreach (TrackedPlayerOtherClient other in Svc.PlayersClient.OtherPlayers)
            {
                if (other.IsPhysicsLoaded && other.ClientRigidbody == hit.rigidbody)
                {
                    playerId = other.Id;
                    return true;
                }
            }
            return false;
        }

        // ------------------------------------------------------------------ effects, local and relayed

        public static void PlayImpacts(List<ImpactFx> impacts, float scale = 1f)
        {
            foreach (ImpactFx i in impacts)
                Fx.Impact(i.Pos, i.Normal, i.Kind, i.Tint, scale);
        }

        /// <summary>Shows the shot to the other players (tracer + impacts).</summary>
        public static void RelayShot(WeaponKind weapon, Vector3 from, Vector3 to, List<ImpactFx> impacts)
        {
            uint space = ClientOrigin.LocalPhysicsSpaceId;
            ArsenalNet.SendToServer(Op.FxRelay, w =>
            {
                w.Write((byte)FxType.Shot);
                w.Write(space);
                w.Write((byte)weapon);
                w.Write(from);
                w.Write(to);
                w.Write((byte)Mathf.Min(impacts.Count, 16));
                for (int i = 0; i < impacts.Count && i < 16; i++)
                {
                    w.Write(impacts[i].Pos);
                    w.Write(impacts[i].Normal);
                    w.Write((byte)impacts[i].Kind);
                    w.Write(impacts[i].Tint.r); w.Write(impacts[i].Tint.g); w.Write(impacts[i].Tint.b);
                }
            }, FishNet.Transporting.Channel.Unreliable);
        }
    }
}
