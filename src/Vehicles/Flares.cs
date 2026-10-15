using System.Collections.Generic;
using System.IO;
using Arsenal.Core;
using Arsenal.Net;
using Arsenal.Visual;
using Arsenal.Weapons;
using UnityEngine;
using WorldTracking.Interface.Values;

namespace Arsenal.Vehicles
{
    /// <summary>
    /// Decoy flares. The client that fires them flies them and tells the others, so every client has the
    /// same flares. A guided missile rolls once for each hot flare that enters its seeker view; the
    /// missile's owner decides, then tells the others which flare their copy now chases.
    /// </summary>
    internal static class Flares
    {
        public const float HotTime = 3.2f;     // seconds a flare can still fool a seeker
        public const float BurnTime = 4.5f;
        private const float SeekerRange = 450f;
        private const float SeekerCone = 55f;  // degrees either side of the missile's nose

        public static readonly List<FlareSim> Active = new List<FlareSim>();
        private static readonly Dictionary<ulong, FlareSim> ByKey = new Dictionary<ulong, FlareSim>();
        private static uint _nextId = 1;

        public static void Init()
        {
            ArsenalNet.OnServer(Op.FlareLaunch, (conn, r) =>
            {
                byte[] all = ((MemoryStream)r.BaseStream).ToArray();
                ArsenalNet.SendToAllExcept(conn, Op.FlareSpawn, w => w.Write(all));
            });
            ArsenalNet.OnServer(Op.DecoyReport, (conn, r) =>
            {
                byte[] all = ((MemoryStream)r.BaseStream).ToArray();
                ArsenalNet.SendToAllExcept(conn, Op.Decoy, w => w.Write(all));
            });
            ArsenalNet.OnClient(Op.FlareSpawn, r =>
            {
                int owner = r.ReadInt32();
                uint space = r.ReadUInt32();
                int n = r.ReadByte();
                for (int i = 0; i < n; i++)
                {
                    uint id = r.ReadUInt32();
                    Vector3 from = FxNet.ToLocal(space, r.ReadVector3());
                    Vector3 v = r.ReadVector3();
                    Spawn(owner, id, from, FxNet.ToLocalDir(space, v.normalized) * v.magnitude);
                }
            });
            ArsenalNet.OnClient(Op.Decoy, r =>
            {
                bool missile = r.ReadBoolean();
                int owner = r.ReadInt32();
                uint id = r.ReadUInt32();
                ByKey.TryGetValue(Projectiles.Key(r.ReadInt32(), r.ReadUInt32()), out FlareSim flare);
                if (missile && Missiles.TryGet(owner, id, out MissileSim m)) m.SetDecoy(flare);
                else if (!missile && Projectiles.TryGet(owner, id, out ProjectileSim p)) p.SetDecoy(flare);
            });
        }

        public static void Reset()
        {
            foreach (FlareSim f in Active)
                if (f != null) Object.Destroy(f.gameObject);
            Active.Clear();
            ByKey.Clear();
        }

        /// <summary>Fires flares from this client (positions and velocities in the local physics space).</summary>
        public static void Launch(IList<(Vector3 from, Vector3 vel)> flares)
        {
            int owner = Explosives.LocalClientId;
            var ids = new uint[flares.Count];
            for (int i = 0; i < flares.Count; i++)
            {
                ids[i] = _nextId++;
                Spawn(owner, ids[i], flares[i].from, flares[i].vel);
            }
            uint space = ClientOrigin.LocalPhysicsSpaceId;
            ArsenalNet.SendToServer(Op.FlareLaunch, w =>
            {
                w.Write(owner);
                w.Write(space);
                w.Write((byte)flares.Count);
                for (int i = 0; i < flares.Count; i++)
                {
                    w.Write(ids[i]);
                    w.Write(flares[i].from);
                    w.Write(flares[i].vel);
                }
            });
        }

        private static void Spawn(int owner, uint id, Vector3 from, Vector3 vel)
        {
            GameObject go = Fx.Orb(new Color(1f, 0.9f, 0.7f), 0.13f);
            go.name = "Arsenal.Flare";
            go.transform.position = from;
            var sim = go.AddComponent<FlareSim>();
            sim.Velocity = vel;
            sim.OwnerClient = owner;
            sim.Id = id;
            sim.GravityVector = ClientOrigin.LocalPhysicsSpace is WorldTracking.Interface.Model.Spaces.PlanetSurfaceSpace ? -Fx.Up * 9.81f * 0.35f : Vector3.zero;
            Active.Add(sim);
            ByKey[Projectiles.Key(owner, id)] = sim;
            Sounds.FlarePop(from);
            Fx.MuzzleFlash(from, vel.normalized, 0.25f);
        }

        public static void Forget(FlareSim f)
        {
            Active.Remove(f);
            ByKey.Remove(Projectiles.Key(f.OwnerClient, f.Id));
        }

        /// <summary>
        /// Seeker check, run by the missile's owner every frame: each hot flare in view gets one roll.
        /// Returns the flare that pulled the missile off, or null.
        /// </summary>
        public static FlareSim Seduce(Vector3 pos, Vector3 fwd, float chance, HashSet<ulong> seen)
        {
            foreach (FlareSim f in Active)
            {
                if (f == null || f.Age > HotTime) continue;
                ulong key = Projectiles.Key(f.OwnerClient, f.Id);
                if (seen.Contains(key)) continue;
                Vector3 to = f.transform.position - pos;
                float d = to.magnitude;
                if (d > SeekerRange || d < 1f || Vector3.Angle(fwd, to) > SeekerCone) continue;
                seen.Add(key);
#if DEV
                ArsenalMod.Log.Msg($"[DEV] seeker sees flare {key} at {d:0} m, {Vector3.Angle(fwd, to):0} deg");
#endif
                if (Random.value < chance)
                {
#if DEV
                    ArsenalMod.Log.Msg($"[DEV] missile decoyed by flare {key} at {d:0} m");
#endif
                    return f;
                }
            }
            return null;
        }

        public static void ReportDecoy(bool missile, int owner, uint id, FlareSim flare)
        {
            ArsenalNet.SendToServer(Op.DecoyReport, w =>
            {
                w.Write(missile);
                w.Write(owner);
                w.Write(id);
                w.Write(flare.OwnerClient);
                w.Write(flare.Id);
            });
        }
    }

    /// <summary>A burning flare: thrown out, slowed by drag, sinking slowly on a planet, trailing smoke.</summary>
    internal sealed class FlareSim : MonoBehaviour
    {
        public Vector3 Velocity;
        public Vector3 GravityVector;
        public int OwnerClient;
        public uint Id;
        public float Age { get; private set; }
        public bool Burning => this != null && Age < Flares.BurnTime;

        private Light _light;
        private float _nextPuff;

        private void Start()
        {
            _light = GetComponent<Light>();
            if (_light != null)
            {
                _light.range = 22f;
                _light.color = new Color(1f, 0.75f, 0.45f);
            }
        }

        private void Update()
        {
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            Age += dt;
            if (Age > Flares.BurnTime)
            {
                Flares.Forget(this);
                Destroy(gameObject);
                return;
            }
            Velocity *= Mathf.Exp(-0.9f * dt);
            Velocity += GravityVector * dt;
            Vector3 step = Velocity * dt;
            float dist = step.magnitude;
            if (dist > 0.0001f && ClientScene.PhysicsScene.Raycast(transform.position, step / dist, out RaycastHit hit, dist, Ballistics.Mask, QueryTriggerInteraction.Ignore))
            {
                // lands and burns out on the ground
                transform.position = hit.point + hit.normal * 0.1f;
                Velocity = Vector3.zero;
                GravityVector = Vector3.zero;
            }
            else transform.position += step;

            float k = 1f - Age / Flares.BurnTime;
            float flicker = 0.75f + Random.value * 0.5f;
            transform.localScale = Vector3.one * 0.26f * Mathf.Lerp(0.4f, 1f, k) * flicker;
            if (_light != null) _light.intensity = 7f * k * flicker;
            if (Age >= _nextPuff)
            {
                _nextPuff = Age + 0.045f;
                Fx.Puff(transform.position, 0.16f + Age * 0.04f, new Color(0.88f, 0.86f, 0.83f), 2.4f, 1, -Velocity * 0.1f);
            }
        }

        private void OnDestroy() => Flares.Forget(this);
    }
}
