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
        private static int _ricochetDepth;

        /// <summary>Fires one bullet in the client physics scene. Returns where it ended for the tracer.</summary>
        public static Vector3 Fire(Vector3 origin, Vector3 dir, BulletSpec spec, List<ImpactFx> impacts, Rigidbody ignore = null, WeaponKind weapon = WeaponKind.Rifle)
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
                if (ignore != null && hit.rigidbody == ignore)
                {
                    // our own ship: the round leaves through the barrel clearance
                    pos = hit.point + dir * 0.05f;
                    travelled += 0.05f;
                    continue;
                }
                Collider col = hit.collider;
                if (spec.EmpSeconds > 0f)
                {
                    // EMP rounds: knock the ship out instead of piercing it
                    IShipPartHelper ship = col.GetComponentInParent<IShipPartHelper>();
                    if (ship != null)
                    {
                        Effects.RequestShipEmp(ship.ShipId, spec.EmpSeconds);
                        Fx.ElectricArc(hit.point, hit.point + hit.normal * 0.8f + Random.insideUnitSphere * 0.5f, new Color(0.4f, 0.65f, 1f), 0.3f, 0.04f);
                        impacts.Add(new ImpactFx { Pos = hit.point, Normal = hit.normal, Kind = ImpactKind.Metal, Tint = new Color(0.5f, 0.7f, 1f) });
                        return hit.point;
                    }
                }

#if DEV
                if (ShipPartsTagDetection.ColliderIsHull(col))
                {
                    bool ok = TryResolvePanel(hit, out PanelKey dk, out HullPart dp, out Vector2 dl);
                    ArsenalMod.Log.Msg($"[DEV] hull hit tag {col.tag} resolved {ok} {(ok ? dk + " seg " + dp.SegmentType + " mat " + dp.MaterialType + " local " + dl : "")}");
                }
#endif
                if (ShipPartsTagDetection.ColliderIsHull(col) && TryResolvePanel(hit, out PanelKey key, out HullPart part, out Vector2 local, out Vector3 shipLocal))
                {
                    HullMaterialType mat = part.MaterialType;
                    float cos = Mathf.Abs(Vector3.Dot(dir, hit.normal));
                    float skip = Mathf.Min(0.6f, (Materials.Thickness + 0.04f) / Mathf.Max(0.2f, cos));
                    bool holeAble = HullDamage.CanHaveHoles(part);
                    // only a round that fits inside an existing hole goes through untouched; one that clips the rim bites into it
                    float roundR = Mathf.Max(0.005f, spec.HoleRadius * Materials.HoleScale(mat) * 0.8f);
                    if (holeAble && HullDamage.TryGetClientHoles(key, out HoleMask existing) && existing.Distance(local.x, local.y) < -roundR)
                    {
                        // through an existing hole
                        pos = hit.point + dir * skip;
                        travelled += skip;
                        continue;
                    }
                    // a slanted panel is thicker along the round's path
                    float resistance = Materials.Resistance(mat) / Mathf.Max(0.3f, cos);
                    float overmatch = penetration / Mathf.Max(0.01f, Materials.Resistance(mat));
                    ImpactKind kind = Kind(mat);
                    Color tint = part.Color.Front.AsColorType;
                    impacts.Add(new ImpactFx { Pos = hit.point, Normal = hit.normal, Kind = kind, Tint = tint });

                    // glancing hit on a hard surface: the round skips off
                    if (cos < Materials.RicochetCos(mat) && overmatch < 3f && penetration > 0f)
                    {
                        HullDamage.ReportHit(key, new HoleMask(), panelDamage * 0.15f);
                        if (_ricochetDepth >= 2) return hit.point;
                        Vector3 glance = (Vector3.Reflect(dir, hit.normal) + Random.insideUnitSphere * 0.12f).normalized;
                        if (Vector3.Dot(glance, hit.normal) < 0.05f) glance = (glance + hit.normal * 0.1f).normalized;
                        var rest = new BulletSpec
                        {
                            PlayerDamage = playerDamage * 0.6f, PanelDamage = panelDamage * 0.5f, Penetration = penetration * 0.5f, HoleRadius = spec.HoleRadius * 0.8f,
                            Range = Mathf.Max(0f, spec.Range - travelled), ObjectDamage = spec.ObjectDamage * 0.5f, BurnSeconds = spec.BurnSeconds,
                        };
                        _ricochetDepth++;
                        Vector3 end2;
                        try { end2 = Fire(hit.point + hit.normal * 0.02f, glance, rest, impacts, ignore, weapon); }
                        finally { _ricochetDepth--; }
                        Fx.BulletTracer(hit.point, end2, 0.02f, 300f);
                        Fx.EmitSparks(hit.point, hit.normal, 10);
                        return hit.point;
                    }
                    if (penetration >= resistance)
                    {
                        // through: hole sized by calibre, material and how easily it went through
                        HoleMask holes = default;
                        if (holeAble && spec.HoleRadius > 0f)
                        {
                            float ease = Mathf.Clamp(0.7f + 0.12f * Mathf.Min(penetration / resistance, 4f), 0.75f, 1.2f);
                            float r = spec.HoleRadius * Materials.HoleScale(mat) * ease;
                            // biting into the rim of an existing hole widens it
                            if (HullDamage.TryGetClientHoles(key, out HoleMask around)) { float dh = around.Distance(local.x, local.y); if (dh < r) r += Mathf.Max(0f, -dh) * 0.5f + r * 0.35f; }
                            holes = holes.PunchDisc(local.x, local.y, r);
                            float shatter = Materials.ShatterScale(mat);
                            if (shatter > 0f) holes = holes.PunchDisc(local.x, local.y, r * shatter, Rng);
                            PunchAcrossSeams(key, part, shipLocal, r * Mathf.Max(1f, shatter), panelDamage * 0.2f);
                        }
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
                    // stopped inside the panel: a dent (harder materials take less)
                    HullDamage.ReportHit(key, new HoleMask(), panelDamage * Materials.DentDamage(mat));
                    return hit.point;
                }

                if (TryGetOtherPlayer(hit, out uint playerId))
                {
                    bool head = IsHeadshot(hit);
                    impacts.Add(new ImpactFx { Pos = hit.point, Normal = hit.normal, Kind = ImpactKind.Flesh });
                    PlayerDamage.Report(playerId, playerDamage * (head ? 1.6f : 1f), weapon, head, spec.BurnSeconds, spec.EmpSeconds);
                    if (spec.ThroughPlayers)
                    {
                        pos = hit.point + dir * 0.8f;
                        travelled += 0.8f;
                        playerDamage *= 0.8f;
                        continue;
                    }
                    return hit.point;
                }

                IMinableObject minable = col.GetComponentInParent<IMinableObject>();
                if (minable != null)
                {
                    try { minable.ReduceHealth(spec.ObjectDamage); } catch { }
                    impacts.Add(new ImpactFx { Pos = hit.point, Normal = hit.normal, Kind = ImpactKind.Wood });
                    return hit.point;
                }

                bool ground = IsGround(col);
                impacts.Add(new ImpactFx { Pos = hit.point, Normal = hit.normal, Kind = ground ? ImpactKind.Ground : ImpactKind.Metal, Tint = new Color(0.6f, 0.6f, 0.6f) });
                return hit.point;
            }
            return pos;
        }

        /// <summary>Planet terrain colliders (on the Default layer) or anything on the Planet layer.</summary>
        public static bool IsGround(Collider col) =>
            col.gameObject.layer == LayerMask.NameToLayer("Planet") || col.name.StartsWith("PlanetMeshCollider");

        public static ImpactKind Kind(HullMaterialType m) => m switch
        {
            HullMaterialType.Wood => ImpactKind.Wood,
            HullMaterialType.Glass => ImpactKind.Glass,
            _ => ImpactKind.Metal,
        };

        /// <summary>Maps a raycast hit on a hull collider to the panel, like the recycler does, plus the hit point on the panel plane.</summary>
        public static bool TryResolvePanel(RaycastHit hit, out PanelKey key, out HullPart part, out Vector2 local) =>
            TryResolvePanel(hit, out key, out part, out local, out _);

        /// <summary>Same, also giving the hit point in ship coordinates.</summary>
        public static bool TryResolvePanel(RaycastHit hit, out PanelKey key, out HullPart part, out Vector2 local, out Vector3 shipLocal)
        {
            key = default;
            part = default;
            local = default;
            shipLocal = default;
            IShipPartTargeter targeter = hit.collider.GetComponentInParent<IShipPartTargeter>();
            if (targeter == null) return false;
            PartHit partHit = targeter.ConvertHitToLocalHit(hit);
            shipLocal = partHit.Pos;
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

        /// <summary>
        /// A hole near the edge of a panel carries on into the panels next to it in the same plane, so a round
        /// hole on a seam is cut in both panels instead of ending in a half circle.
        /// </summary>
        public static void PunchAcrossSeams(PanelKey key, HullPart part, Vector3 shipCenter, float radius, float damage)
        {
            HullPartPlacement pl = part.HullPlacement;
            Vector3 c = Quaternion.Inverse(pl.Rotation) * (shipCenter - pl.LocalPosition);
            if (SegmentShapes.Get(part).Inside(new Vector2(c.x, c.y)) >= radius) return; // fully inside
            if (!Svc.ShipsClient.TryGetTrackedShip(key.ShipId, out TrackedShipClient ship)) return;
            Vector3Int ax = Vector3Int.RoundToInt(pl.Rotation * Vector3.right), ay = Vector3Int.RoundToInt(pl.Rotation * Vector3.up);
            for (int i = -1; i <= 1; i++)
                for (int j = -1; j <= 1; j++)
                {
                    if (i == 0 && j == 0) continue;
                    Vector3Int coord = key.Pos.VoxelCoord + ax * i + ay * j;
                    HullPart? n = ship.GetVoxel(coord)?.GetPartFromType(key.Pos.Type);
                    if (!n.HasValue || !HullDamage.CanHaveHoles(n.Value)) continue;
                    HullPartPlacement np = n.Value.HullPlacement;
                    Vector3 lc = Quaternion.Inverse(np.Rotation) * (shipCenter - np.LocalPosition);
                    SegmentShape shape = SegmentShapes.Get(n.Value);
                    if (Mathf.Abs(lc.z - shape.ZCenter) > 0.06f) continue; // not in the same plane
                    if (shape.DistanceTo(new Vector2(lc.x, lc.y)) >= radius) continue;
                    var nk = new PanelKey(key.ShipId, np.Position);
                    HoleMask holes = HoleMask.Disc(lc.x, lc.y, radius);
                    HullDamage.ReportHit(nk, holes, damage);
                    HullDamage.PredictHoles(nk, holes);
                }
        }


        /// <summary>Top of another player's body (along the local up).</summary>
        public static bool IsHeadshot(RaycastHit hit)
        {
            if (hit.rigidbody == null) return false;
            float h = Vector3.Dot(hit.point - hit.rigidbody.worldCenterOfMass, Fx.Up);
            return h > 0.5f;
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
