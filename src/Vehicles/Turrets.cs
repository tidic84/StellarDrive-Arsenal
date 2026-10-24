using System.Collections.Generic;
using Arsenal.Content;
using Arsenal.Core;
using Arsenal.Weapons;
using Ships;
using Ships.Interface.Model.Parts;
using UnityEngine;

namespace Arsenal.Vehicles
{
    /// <summary>One kind of rotating turret: its bolts, how it shoots and how fast it turns.</summary>
    internal sealed class TurretDef
    {
        public ushort Id;
        public string NameFr, NameEn, DescFr, DescEn;
        public string HudName;
        public byte Style;                   // its bolts (index in Laser.Styles)
        public float Interval;               // between two shots
        public int PerShot = 1;              // barrels firing together (the others take the next shot)
        public float Heat;                   // per shot: it overheats at 1 and cools for a while
        public float YawSpeed, PitchSpeed;   // degrees per second
        public float MinElevation, MaxElevation; // gun elevation above the mount plane (degrees)
        public float AutoRange;              // on its own: how far it engages a ship
        public float Recoil;                 // how far a barrel kicks back when it fires (m)
        public float Shake;                  // camera shake each shot for the gunner (0 = none)
        public float CamBack, CamUp;         // gunner view: behind and above the gun pivot
        public float SalvoGap;               // > 0: each trigger pull fires every barrel in turn, this far apart (s)
    }

    internal static class Turrets
    {
        public static readonly TurretDef[] All =
        {
            new TurretDef
            {
                Id = ShipContent.QuadTurretId, HudName = "CORSAIR",
                NameFr = "Tourelle laser quadruple « Corsaire »", NameEn = "\"Corsair\" quad laser turret",
                DescFr = "Tourelle sphérique à quatre canons laser rouges qui tirent par paires, rapide à pointer. Pilotez-la depuis un poste de tir ; seule, elle vise et tire sur le vaisseau le plus proche tant que son signal est actif.",
                DescEn = "Ball turret with four red laser barrels firing in pairs, quick to traverse. Man it from a fire-control station; on its own it tracks and fires at the nearest ship while its signal is on.",
                Style = Laser.QuadBolt, Interval = 0.12f, PerShot = 2, Heat = 0.035f,
                YawSpeed = 150f, PitchSpeed = 120f, MinElevation = -12f, MaxElevation = 85f, AutoRange = 800f,
                Recoil = 0.07f, Shake = 0.05f, CamBack = 2.6f, CamUp = 1.05f,
            },
            new TurretDef
            {
                Id = ShipContent.TurbolaserId, HudName = "IMPERATOR",
                NameFr = "Turbolaser double « Imperator »", NameEn = "\"Imperator\" twin turbolaser",
                DescFr = "Tourelle de vaisseau capital : deux longs canons tirent en alternance de gros traits verts qui portent à 1,6 km, percent la coque et ravagent la machinerie. Lente à pointer. Pilotable depuis un poste de tir, ou autonome sur signal.",
                DescEn = "Capital-ship turret: two long barrels take turns firing big green bolts that carry 1.6 km, breach the hull and wreck the machinery. Slow to traverse. Manned from a fire-control station, or on its own while its signal is on.",
                Style = Laser.TurboBolt, Interval = 0.55f, Heat = 0.09f,
                YawSpeed = 50f, PitchSpeed = 38f, MinElevation = -6f, MaxElevation = 72f, AutoRange = 1500f,
                Recoil = 0.32f, Shake = 0.25f, CamBack = 4.6f, CamUp = 1.7f,
            },
            new TurretDef
            {
                Id = ShipContent.IonTurretId, HudName = "TEMPEST",
                NameFr = "Tourelle ionique « Tempête »", NameEn = "\"Tempest\" ion turret",
                DescFr = "Deux canons à bobines tirent ensemble des traits ioniques qui abîment peu mais coupent les commandes du vaisseau touché pendant 6 secondes. Pilotable depuis un poste de tir, ou autonome sur signal.",
                DescEn = "Two coil barrels fire ion bolts together: little damage, but the ship they hit loses its controls for 6 seconds. Manned from a fire-control station, or on its own while its signal is on.",
                Style = Laser.IonBolt, Interval = 1.3f, PerShot = 2, Heat = 0.12f,
                YawSpeed = 70f, PitchSpeed = 55f, MinElevation = -8f, MaxElevation = 80f, AutoRange = 1100f,
                Recoil = 0.12f, Shake = 0.12f, CamBack = 3.4f, CamUp = 1.35f,
            },
            new TurretDef
            {
                Id = ShipContent.ColossusId, HudName = "COLOSSUS",
                NameFr = "Turbolaser lourd « Colosse »", NameEn = "\"Colossus\" heavy turbolaser",
                DescFr = "Le plus gros canon de l'arsenal : un énorme trait rouge toutes les 2,6 s qui explose à l'impact, éventre la coque et repousse le vaisseau touché. Portée 2 km, pointage très lent. Pilotable depuis un poste de tir, ou autonome sur signal.",
                DescEn = "The biggest gun of the arsenal: one huge red bolt every 2.6 s that blows up on impact, guts the hull and shoves the ship it hits. 2 km range, very slow to traverse. Manned from a fire-control station, or on its own while its signal is on.",
                Style = Laser.ColossusBolt, Interval = 2.6f, Heat = 0.2f,
                YawSpeed = 26f, PitchSpeed = 20f, MinElevation = -5f, MaxElevation = 60f, AutoRange = 2000f,
                Recoil = 0.6f, Shake = 0.55f, CamBack = 6.2f, CamUp = 2.3f,
            },
            new TurretDef
            {
                Id = ShipContent.AnnihilatorId, HudName = "ANNIHILATOR",
                NameFr = "Batterie triple « Annihilateur »", NameEn = "\"Annihilator\" triple battery",
                DescFr = "Tourelle de cuirassé de 5 m × 5 m : trois canons géants tirent l'un après l'autre une salve de traits verts colossaux qui explosent à l'impact. Portée 3 km, une salve toutes les 4,5 s, pointage extrêmement lent. Pilotable depuis un poste de tir, ou autonome sur signal.",
                DescEn = "A 5 m × 5 m battleship turret: three giant barrels fire one after the other a salvo of colossal green bolts that blow up where they land. 3 km range, one salvo every 4.5 s, extremely slow to traverse. Manned from a fire-control station, or on its own while its signal is on.",
                Style = Laser.AnnihilatorBolt, Interval = 4.5f, SalvoGap = 0.22f, Heat = 0.3f,
                YawSpeed = 14f, PitchSpeed = 10f, MinElevation = -4f, MaxElevation = 55f, AutoRange = 3000f,
                Recoil = 1.1f, Shake = 0.9f, CamBack = 13f, CamUp = 5f,
            },
        };

        private static readonly Dictionary<ushort, TurretDef> ById = new Dictionary<ushort, TurretDef>();

        public static TurretDef Get(ushort id)
        {
            if (ById.Count == 0)
                foreach (TurretDef d in All) ById[d.Id] = d;
            return ById.TryGetValue(id, out TurretDef def) ? def : null;
        }

        public static bool IsTurret(ushort id) => Get(id) != null;
    }

    internal sealed partial class ShipWeapons
    {
        private const float TurretAimTolerance = 1.6f; // on its own, a turret fires once its guns are this close (degrees)

        /// <summary>
        /// A rotating turret. Manned from a fire-control station, it aims where the gunner looks and fires when they
        /// do (the gunner's client shoots). Otherwise, while its signal is on, the owner's copy picks the nearest
        /// ship it can see in reach, leads it and fires once its guns are on it. Whoever shoots also tells the others
        /// where the guns point, so everyone sees the turret turn.
        /// </summary>
        private void UpdateTurret(TrackedShipClient ship, StatefulPart part, Mount m, bool on, bool owner)
        {
            TurretDef def = Turrets.Get(m.Kind);
            if (def == null) return;
            float dt = Time.deltaTime;
            uint root = RootOf(ship.Id);
            LaserStyle s = Laser.Style(def.Style);
            bool powered = !Effects.IsShipEmp(ship.Id);
            bool manned = GunnerStation.TryGetGunner(m, root, out bool mine);
            bool shooter = manned ? mine : owner;
            m.Manned = manned;

            m.Heat = Mathf.Max(0f, m.Heat - dt / 5f);
            if (m.Overheated && m.Heat < 0.3f) m.Overheated = false;
            m.Charge = Mathf.Max(0f, m.Charge - dt * 4f);
            if (m.Lens != null)
                m.Lens.material.SetColor(EmissionId, Color.Lerp(s.Lens, new Color(1f, 0.55f, 0.2f), m.Heat) * ((powered ? 1.5f : 0.2f) + m.Charge * 8f));

            bool fire = false;
            if (m.Pitch != null && m.Head != null)
            {
                Vector3 pivot = m.Pitch.position;
                if (shooter && powered)
                {
                    if (manned)
                    {
                        AimAt(m, def, GunnerStation.AimPoint - pivot);
                        fire = GunnerStation.Trigger;
                    }
                    else if (on)
                    {
                        if (Scan(m)) m.AutoTarget = FindTurretTarget(ship, m, def, pivot);
                        if (m.AutoTarget != 0 && TryLead(m.AutoTarget, root, pivot, s.Speed, out Vector3 aim))
                        {
                            AimAt(m, def, aim - pivot);
                            fire = Vector3.Angle(m.Pitch.forward, aim - pivot) < TurretAimTolerance && (aim - pivot).magnitude < def.AutoRange;
                        }
                    }
                    else m.AutoTarget = 0;
                    TurretNet.Report(m);
                }
                else if (!shooter && Time.time - m.RemoteAt < 3f)
                {
                    m.TargetYaw = m.RemoteYaw;
                    m.TargetElevation = m.RemoteElevation;
                }
                if (powered)
                {
                    m.Yaw = Mathf.MoveTowardsAngle(m.Yaw, m.TargetYaw, def.YawSpeed * dt);
                    m.Elevation = Mathf.MoveTowards(m.Elevation, Mathf.Clamp(m.TargetElevation, def.MinElevation, def.MaxElevation), def.PitchSpeed * dt);
                }
                m.Head.localRotation = Quaternion.Euler(0f, m.Yaw, 0f);
                m.Pitch.localRotation = Quaternion.Euler(-m.Elevation, 0f, 0f);
            }

            // barrels slide back when they fire and run out again
            if (m.Recoils != null)
                for (int i = 0; i < m.Recoils.Length; i++)
                {
                    if (m.Recoils[i] == null) continue;
                    m.Kick[i] = Mathf.Max(0f, m.Kick[i] - dt * (m.Kick[i] > 0.5f ? 9f : 2.2f));
                    m.Recoils[i].localPosition = m.RecoilRest[i] - Vector3.forward * (def.Recoil * Mathf.SmoothStep(0f, 1f, m.Kick[i]));
                }

            bool salvo = def.SalvoGap > 0f;
            if (!shooter || !powered || m.Points == null || m.Points.Length == 0) m.SalvoLeft = 0;
            if (!shooter || !(fire || m.SalvoLeft > 0) || !powered || (m.Overheated && m.SalvoLeft == 0) || Time.time < m.NextShot || m.Points == null || m.Points.Length == 0) return;
            int barrels = m.Points.Length;
            bool newShot = !salvo || m.SalvoLeft == 0;
            if (salvo)
            {
                // a salvo, once started, goes out barrel after barrel: middle, left, right
                if (m.SalvoLeft == 0) m.SalvoLeft = barrels;
                m.SalvoLeft--;
                m.NextShot = Time.time + (m.SalvoLeft > 0 ? def.SalvoGap : def.Interval);
            }
            else
            {
                // steady rate while firing; after a pause the next shot waits a full interval (no double shot)
                m.NextShot = (Time.time - m.NextShot > def.Interval ? Time.time : m.NextShot) + def.Interval;
            }
            if (newShot)
            {
                m.Heat += def.Heat;
                if (m.Heat >= 1f)
                {
                    m.Heat = 1f;
                    m.Overheated = true;
                }
            }
            m.Charge = 1f;
            TryGetShipMotion(root, out _, out Vector3 shipVel, out _, out _);
            for (int k = 0; k < Mathf.Min(def.PerShot, barrels); k++)
            {
                // pairs fire across the diagonal (top left with bottom right), as a quad gun does
                int b = salvo ? SalvoOrder(barrels - 1 - m.SalvoLeft, barrels)
                    : def.PerShot == 2 && barrels == 4 ? (m.Barrel % 2 == 0 ? (k == 0 ? 0 : 3) : (k == 0 ? 1 : 2)) : (m.Barrel * def.PerShot + k) % barrels;
                Transform muzzle = m.Points[b];
                if (muzzle == null) continue;
                Vector3 dir = muzzle.forward;
                Vector3 vel = shipVel + dir * s.Speed;
                Laser.Fire(muzzle.position, vel, root, true, def.Style);
                FxNet.RelayBolt(muzzle.position, vel, root, def.Style);
                if (m.Kick != null && b < m.Kick.Length) m.Kick[b] = 1f;
            }
            m.Barrel++;
            if (manned && mine) GunnerStation.Fired(def);
        }

        /// <summary>Barrel fired n-th in a salvo: the middle one first on an odd count, then outwards left and right.</summary>
        private static int SalvoOrder(int n, int barrels)
        {
            if (barrels != 3) return n % barrels;
            return n == 0 ? 1 : n == 1 ? 0 : 2;
        }

        /// <summary>Points the guns' target angles (in the mount's frame) along a world direction.</summary>
        private static void AimAt(Mount m, TurretDef def, Vector3 dir)
        {
            if (dir.sqrMagnitude < 0.0001f) return;
            Vector3 local = m.Head.parent.InverseTransformDirection(dir);
            m.TargetYaw = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
            m.TargetElevation = Mathf.Clamp(Mathf.Atan2(local.y, new Vector2(local.x, local.z).magnitude) * Mathf.Rad2Deg, def.MinElevation, def.MaxElevation);
        }

        /// <summary>
        /// Nearest other ship the turret can see, in reach and above its mount (within its elevation), that is flying or
        /// has someone at its controls: ships parked on the ground (the crew's own, often) are left alone.
        /// </summary>
        private static uint FindTurretTarget(TrackedShipClient self, Mount m, TurretDef def, Vector3 from)
        {
            uint myRoot = RootOf(self.Id);
            Transform frame = m.Head.parent;
            uint best = 0;
            float bestD = def.AutoRange;
            foreach (TrackedShipClient s in Svc.ShipsClient.Ships)
            {
                if (!s.IsRigidbodyLoaded || RootOf(s.Id) != s.Id || s.Id == myRoot) continue;
                Vector3 to = TargetPoint(s) - from;
                float d = to.magnitude;
                if (d < 8f || d >= bestD || !(Airborne(s) || Crewed(s.Id))) continue;
                Vector3 local = frame.InverseTransformDirection(to / d);
                float elevation = Mathf.Asin(Mathf.Clamp(local.y, -1f, 1f)) * Mathf.Rad2Deg;
                if (elevation < def.MinElevation - 2f || elevation > def.MaxElevation + 2f || !CanSee(from, s, myRoot)) continue;
                bestD = d;
                best = s.Id;
            }
            return best;
        }

        /// <summary>Someone (us or another player) sits at the controls or a station of this ship assembly.</summary>
        private static bool Crewed(uint rootId)
        {
            if (Svc.PlayersClient == null) return false;
            if (Svc.PlayersClient.TryGetLocalPlayerState(out Players.Interface.Model.PlayerState mine)
                && mine.Type == Players.Interface.Model.PlayerStateType.Piloting && RootOf(mine.EntityId) == rootId) return true;
            foreach (Players.TrackedPlayerOtherClient other in Svc.PlayersClient.OtherPlayers)
            {
                Players.Interface.Model.PlayerState st = other.State;
                if (st.Type == Players.Interface.Model.PlayerStateType.Piloting && RootOf(st.EntityId) == rootId) return true;
            }
            return false;
        }

        /// <summary>Where to aim so a bolt at that speed meets the ship (its motion relative to ours, two refinements).</summary>
        public static bool TryLead(uint target, uint myRoot, Vector3 from, float boltSpeed, out Vector3 aim)
        {
            aim = Vector3.zero;
            if (!TryGetShipMotion(target, out Vector3 c, out Vector3 v, out _, out _)) return false;
            TryGetShipMotion(myRoot, out _, out Vector3 mine, out _, out _);
            Vector3 rel = v - mine;
            aim = c;
            for (int i = 0; i < 3; i++)
                aim = c + rel * ((aim - from).magnitude / Mathf.Max(1f, boltSpeed));
            return true;
        }
    }
}
