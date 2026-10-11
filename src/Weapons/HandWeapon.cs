using System.Collections.Generic;
using System.Linq;
using Arsenal.Core;
using Arsenal.Damage;
using Arsenal.Visual;
using Core.Utils;
using Planets.Interface.Model;
using Players;
using Ships.Interface.Model.Parts;
using Ships.Interface.Services;
using TMPro;
using Tools;
using Tools.Interface.Model;
using UnityEngine;
using UnityEngine.InputSystem;
using WorldTracking.Interface.Values;

namespace Arsenal.Weapons
{
    /// <summary>
    /// A weapon held like the game's tools. Runs only for the local player (other players only get the
    /// visuals). Each firing mode is handled here; damage goes through Ballistics / HullDamage / PlayerDamage.
    /// </summary>
    public class HandWeapon : Tool, IToolAnimator
    {
        public uint ItemId;

        private HandWeaponDef _def;
        private Transform _muzzle, _barrels, _ammoProp;
        private TextMeshPro _display;
        private int _ammo;
        private float _fuel;
        private float _heat;
        private bool _overheated;
        private float _spin, _charge;
        private float _nextShot, _nextTick, _nextRelay;
        private float _reloadDone;
        private bool _trigger, _pendingShot, _wasTrigger;
        private bool _aiming;
        private float _aimBlend;
        private float _recoil;
        private float _stopFiringAt;
        private bool _firingNotified;
        private Camera _camera;
        private float _baseFov;
        private FMOD.Channel _loop;
        private LineRenderer _beam;
        private object _rampTarget;
        private float _ramp = 1f;
        private readonly List<ImpactFx> _impacts = new List<ImpactFx>();
        private readonly LockOn _lock = new LockOn();

        // Ammo / heat are kept per weapon type for the session, so switching tools changes nothing.
        private static readonly Dictionary<uint, (int ammo, float heat, float time)> Saved = new Dictionary<uint, (int, float, float)>();

        public override IToolAnimator Animator => this;
        public override bool BlocksPickInteractions => false;

        private bool Reloading => _reloadDone > 0f;
        private bool UsesHeat => _def.Ammo == AmmoMode.Heat;
        private bool UsesFuel => _def.Mode == FireMode.Flame;

        protected override void AfterAwake()
        {
            _def = WeaponDefs.ByItem(ItemId);
            if (_def == null) return; // the template itself (its item id is set after it wakes up)
            Transform[] all = GetComponentsInChildren<Transform>(true);
            _muzzle = all.FirstOrDefault(t => t.name == "Muzzle");
            _barrels = all.FirstOrDefault(t => t.name == "Barrels");
            _ammoProp = all.FirstOrDefault(t => t.name == "Ammo");
            _display = GetComponentInChildren<TextMeshPro>(true);
            if (Saved.TryGetValue(ItemId, out var s))
            {
                _ammo = s.ammo;
                _fuel = s.ammo;
                // heat bled off while the weapon was put away
                _heat = Mathf.Max(0f, s.heat - (Time.time - s.time) * _def.CoolPerSecond);
            }
            else
            {
                _ammo = _def.Magazine;
                _fuel = _def.Magazine;
            }
            CacheModel();
            UpdateDisplay();
        }

        public override void EnableTool()
        {
            base.EnableTool();
            _camera = Camera.main;
            _baseFov = _camera != null ? _camera.fieldOfView : 60f;
        }

        public override void DisableTool()
        {
            _trigger = false;
            _aiming = false;
            RestoreFov();
            StopContinuous();
            Save();
            WeaponHud.Hide();
            base.DisableTool();
        }

        private void OnDestroy()
        {
            RestoreFov();
            StopContinuous();
            if (_def != null)
            {
                Save();
                WeaponHud.Hide();
            }
        }

        private void Save() => Saved[ItemId] = (UsesFuel ? Mathf.CeilToInt(_fuel) : _ammo, _heat, Time.time);

        public override void PrimaryActionBegin()
        {
            _trigger = true;
            _pendingShot = true;
        }

        public override void PrimaryActionEnd() => _trigger = false;

        public override void SecondaryActionBegin()
        {
            if (_def.AimFov > 0f) _aiming = !_aiming;
        }

        public override void TertiaryActionBegin() => StartReload();

        private void StartReload()
        {
            if (UsesHeat || Reloading) return;
            if (UsesFuel ? _fuel >= _def.Magazine : _ammo >= _def.Magazine) return;
            _reloadDone = Time.time + _def.ReloadTime;
            _aiming = false;
            _charge = 0f;
            Sounds.Reload(transform.position);
            UpdateDisplay();
        }

        // ------------------------------------------------------------------ per frame

        private void Update()
        {
            if (!enabled || _def == null) return;
            Keyboard kb = Keyboard.current;
            if (kb != null && kb.rKey.wasPressedThisFrame) StartReload();
            float dt = Time.deltaTime;

            if (Reloading && Time.time >= _reloadDone)
            {
                _reloadDone = 0f;
                _ammo = _def.Magazine;
                _fuel = _def.Magazine;
                UpdateDisplay();
            }
            if (UsesHeat)
            {
                bool continuous = _def.Mode == FireMode.Beam && _trigger && !_overheated;
                if (!continuous) _heat = Mathf.Max(0f, _heat - _def.CoolPerSecond * dt);
                if (_overheated && _heat <= 0.2f) _overheated = false;
            }
            if (_def.SpinUp > 0f)
            {
                _spin = Mathf.MoveTowards(_spin, _trigger && !_overheated && !Effects.LocalJammed ? 1f : 0f, dt / _def.SpinUp);
                if (_barrels != null) _barrels.Rotate(0f, 0f, _spin * 1800f * dt, Space.Self);
                if (_spin > 0.05f)
                {
                    if (!_loop.hasHandle()) _loop = Sounds.Loop(SoundStyle.Charge, transform.position);
                    Sounds.SetPitch(_loop, 0.4f + _spin * 0.9f);
                    Sounds.Move(_loop, transform.position);
                }
                else Sounds.Stop(ref _loop);
            }
            if (_ammoProp != null) _ammoProp.gameObject.SetActive(!Reloading && _ammo > 0);
            if (_firingNotified && Time.time >= _stopFiringAt)
            {
                _firingNotified = false;
                FiringEffectListener?.NotifyToolFiringStop();
            }
            _recoil = Mathf.MoveTowards(_recoil, 0f, dt * Mathf.Max(0.02f, _def.RecoilKick) * 12f);
            _aimBlend = Mathf.MoveTowards(_aimBlend, _aiming ? 1f : 0f, dt * 6f);
            if (_def.Projectile == ProjectileKind.Sam && _camera != null)
            {
                // seeker: locks automatically on the ship it is pointed at (tighter cone when aiming)
                _lock.Cone = _aiming ? 6f : 9f;
                _lock.Update(_camera.transform.position, _camera.transform.forward, 0, dt, !Reloading && _ammo > 0);
                LockHud.Show(_lock);
            }
            WeaponHud.Show(_def, UsesFuel ? Mathf.CeilToInt(_fuel) : _ammo, _heat, _overheated, Reloading ? 1f - (_reloadDone - Time.time) / _def.ReloadTime : -1f, _def.Mode == FireMode.Charge ? _charge : -1f, Effects.LocalJammed);
        }

        private void LateUpdate()
        {
            if (_def == null || _camera == null || _def.AimFov <= 0f) return;
            if (_aimBlend > 0f)
                _camera.fieldOfView = Mathf.Lerp(_baseFov, _def.AimFov, _aimBlend);
            else if (Mathf.Abs(_camera.fieldOfView - _baseFov) > 0.01f && Mathf.Abs(_camera.fieldOfView - _def.AimFov) < Mathf.Abs(_baseFov - _def.AimFov))
                _camera.fieldOfView = _baseFov;
        }

        private void OnEnable() => UnityEngine.Rendering.RenderPipelineManager.beginCameraRendering += OnBeginCamera;

        private void OnDisable() => UnityEngine.Rendering.RenderPipelineManager.beginCameraRendering -= OnBeginCamera;

        /// <summary>
        /// Final placement right before the camera renders: blends the model from its hip pose to a pose where
        /// the sights sit on the line of sight. Scoped weapons hide the model and show the scope instead.
        /// </summary>
        private void OnBeginCamera(UnityEngine.Rendering.ScriptableRenderContext ctx, Camera cam)
        {
            if (_model == null || cam != _camera) return;
            _model.localPosition = _holdPos;
            _model.localRotation = _holdRot;
            bool scoped = _def.Scope && _aimBlend > 0.85f;
            foreach (Renderer r in _renderers) r.enabled = !scoped;
            if (_aimBlend <= 0f || scoped) return;
            float k = Mathf.SmoothStep(0f, 1f, _aimBlend);
            Transform ct = cam.transform;
            Quaternion aimRot = ct.rotation;
            Vector3 sightTarget = ct.position + ct.rotation * new Vector3(0f, 0f, _def.AimDistance);
            Quaternion modelRot = Quaternion.Slerp(_model.rotation, aimRot, k);
            Vector3 aimPos = sightTarget - aimRot * _sightLocal;
            Vector3 hipPos = _model.position;
            _model.rotation = modelRot;
            _model.position = Vector3.Lerp(hipPos, aimPos, k) + ct.rotation * new Vector3(0f, -_recoil * 0.2f, -_recoil);
        }

        private Transform _model;
        private Renderer[] _renderers = new Renderer[0];
        private Vector3 _holdPos, _sightLocal;
        private Quaternion _holdRot;

        private void CacheModel()
        {
            _model = GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name.EndsWith("Model"));
            if (_model == null) return;
            _holdPos = _model.localPosition;
            _holdRot = _model.localRotation;
            _renderers = _model.GetComponentsInChildren<Renderer>(true);
            Transform sight = _model.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Sight");
            _sightLocal = sight != null ? sight.localPosition : Vector3.up * 0.06f;
        }

        private void OnGUI()
        {
            if (_def == null || !enabled || Assets.IsTemplate(this)) return;
            if (_def.Scope && _aimBlend > 0.85f) ScopeOverlay.Draw();
        }

        private void RestoreFov()
        {
            if (_camera != null && _aimBlend > 0f) _camera.fieldOfView = _baseFov;
            _aimBlend = 0f;
        }

        // ------------------------------------------------------------------ firing

        public override void UpdateChecks(CameraPhysicsData cam, float deltaTime)
        {
            if (_def == null) return;
            bool trigger = _trigger;
            bool pressed = _pendingShot;
            _pendingShot = false;
            bool released = _wasTrigger && !trigger;
            _wasTrigger = trigger;

            if (Effects.LocalJammed)
            {
                if (pressed) Sounds.DryFire(transform.position);
                StopContinuous();
                _charge = 0f;
                return;
            }

            switch (_def.Mode)
            {
                case FireMode.Charge:
                    UpdateCharge(cam, trigger, released);
                    return;
                case FireMode.Beam:
                    UpdateBeam(cam, trigger);
                    return;
                case FireMode.Flame:
                    UpdateFlame(cam, trigger);
                    return;
            }

            bool wants = _def.Automatic ? trigger : pressed;
            if (!wants || Reloading || Time.time < _nextShot) return;
            if (_def.SpinUp > 0f && _spin < 1f) return;
            if (!TryConsume()) return;
            _nextShot = Time.time + 60f / _def.RoundsPerMinute;
            switch (_def.Mode)
            {
                case FireMode.Projectile: FireProjectile(cam); break;
                case FireMode.Arc: FireArc(cam); break;
                default: FireBullets(cam, 1f); break;
            }
            Kick();
            UpdateDisplay();
        }

        /// <summary>Uses one round (or adds heat). False when empty / overheated.</summary>
        private bool TryConsume()
        {
            if (UsesHeat)
            {
                if (_overheated)
                {
                    if (Time.time >= _nextShot) Sounds.DryFire(transform.position);
                    _nextShot = Time.time + 0.3f;
                    return false;
                }
                _heat += _def.HeatPerShot;
                if (_heat >= 1f)
                {
                    _heat = 1f;
                    _overheated = true;
                    Fx.Puff(MuzzlePos(Vector3.zero), 0.08f, new Color(0.85f, 0.85f, 0.85f), 1.2f, 3);
                }
                return true;
            }
            if (_ammo <= 0)
            {
                Sounds.DryFire(transform.position);
                _nextShot = Time.time + 0.25f;
                StartReload();
                return false;
            }
            _ammo--;
            return true;
        }

        private Vector3 MuzzlePos(Vector3 fallback) => _muzzle != null ? _muzzle.position : fallback;

        private Vector3 Spread(Vector3 dir, float degrees)
        {
            return Quaternion.AngleAxis(Random.Range(0f, 360f), dir) *
                   Quaternion.AngleAxis(Random.Range(0f, degrees), Vector3.Cross(dir, Fx.Up).normalized) * dir;
        }

        private void FireBullets(CameraPhysicsData cam, float power)
        {
            float spread = Mathf.Lerp(_def.SpreadDegrees, _def.AimSpreadDegrees, _aimBlend);
            Vector3 muzzle = MuzzlePos(cam.CameraPos);
            BulletSpec spec = _def.Bullet;
            if (power < 0.999f)
            {
                spec = new BulletSpec
                {
                    PlayerDamage = spec.PlayerDamage * power, PanelDamage = spec.PanelDamage * power, Penetration = spec.Penetration * power,
                    HoleRadius = spec.HoleRadius * Mathf.Lerp(0.5f, 1f, power), Range = spec.Range, ObjectDamage = spec.ObjectDamage * power,
                    BurnSeconds = spec.BurnSeconds, EmpSeconds = spec.EmpSeconds, ThroughPlayers = spec.ThroughPlayers && power > 0.6f,
                };
            }
            for (int i = 0; i < _def.Pellets; i++)
            {
                Vector3 dir = Spread(cam.Direction, spread).normalized;
                _impacts.Clear();
                Vector3 end = Ballistics.Fire(cam.CameraPos, dir, spec, _impacts, null, _def.Kind);
                Ballistics.PlayImpacts(_impacts, _def.Pellets > 1 ? 0.6f : 1f);
                if (i == 0) WeaponFx.Shot(_def.Kind, muzzle, end, _muzzle);
                else Fx.ColoredTracer(muzzle, end, 0.05f, _def.TracerColor, 140f, 1.2f, 9f);
                if (i < 3) Ballistics.RelayShot(_def.Kind, muzzle, end, _impacts);
            }
            NotifyFiring();
        }

        private void FireProjectile(CameraPhysicsData cam)
        {
            Vector3 dir = Spread(cam.Direction, Mathf.Lerp(_def.SpreadDegrees, _def.AimSpreadDegrees, _aimBlend)).normalized;
            ProjectileSpec spec = Projectiles.Specs[_def.Projectile];
            Vector3 lob = _def.Projectile == ProjectileKind.Grenade ? Vector3.ProjectOnPlane(Fx.Up, dir).normalized * 0.12f : Vector3.zero;
            Vector3 v = (dir + lob).normalized * spec.Speed;
            uint target = _def.Projectile == ProjectileKind.Sam ? _lock.Locked : 0;
            Projectiles.Launch(_def.Projectile, cam.CameraPos + dir * 0.7f, v, target, 0);
            Vector3 muzzle = MuzzlePos(cam.CameraPos);
            Fx.MuzzleFlash(muzzle, dir, _def.MuzzleSize, _muzzle);
            Fx.Puff(muzzle, 0.15f, new Color(0.8f, 0.78f, 0.75f), 1.2f, 3, dir * 2f);
            if (_def.Projectile == ProjectileKind.Rocket || _def.Projectile == ProjectileKind.Sam)
                Fx.Puff(muzzle - dir * 1.1f, 0.3f, new Color(0.8f, 0.78f, 0.75f), 1.5f, 5, -dir * 4f); // back-blast
            Sounds.Shot(_def.Kind, muzzle);
            NotifyFiring();
        }

        private void Kick()
        {
            _recoil = Mathf.Min(_recoil + _def.RecoilKick, _def.RecoilKick * 2.5f);
        }

        private void NotifyFiring()
        {
            _stopFiringAt = Time.time + 0.12f;
            if (!_firingNotified)
            {
                _firingNotified = true;
                FiringEffectListener?.NotifyToolFiringBegin();
            }
        }

        // ------------------------------------------------------------------ charge (railgun)

        private void UpdateCharge(CameraPhysicsData cam, bool trigger, bool released)
        {
            if (Reloading) return;
            if (trigger && Time.time >= _nextShot)
            {
                if (_ammo <= 0)
                {
                    if (_charge == 0f) StartReload();
                    return;
                }
                _charge = Mathf.Min(1f, _charge + Time.fixedDeltaTime / _def.ChargeTime);
                if (!_loop.hasHandle()) _loop = Sounds.Loop(SoundStyle.Charge, transform.position);
                Sounds.SetPitch(_loop, 0.6f + _charge * 1.2f);
                Sounds.Move(_loop, transform.position);
                if (Random.value < _charge * 0.5f) Fx.ElectricArc(MuzzlePos(cam.CameraPos), MuzzlePos(cam.CameraPos) + Random.insideUnitSphere * 0.15f, _def.TracerColor, 0.05f, 0.01f);
                return;
            }
            if (released && _charge > 0.2f && _ammo > 0)
            {
                _ammo--;
                FireBullets(cam, Mathf.Lerp(0.35f, 1f, _charge));
                _recoil = _def.RecoilKick * (0.5f + _charge);
                _nextShot = Time.time + 60f / _def.RoundsPerMinute;
                UpdateDisplay();
            }
            _charge = 0f;
            Sounds.Stop(ref _loop);
        }

        // ------------------------------------------------------------------ beam (disintegrator)

        private void UpdateBeam(CameraPhysicsData cam, bool trigger)
        {
            if (!trigger || _overheated)
            {
                StopContinuous();
                return;
            }
            _heat += _def.HeatPerShot * Time.fixedDeltaTime;
            if (_heat >= 1f)
            {
                _heat = 1f;
                _overheated = true;
                StopContinuous();
                return;
            }
            Vector3 dir = cam.Direction;
            bool tick = Time.time >= _nextTick;
            if (tick) _nextTick = Time.time + 0.1f;
            Vector3 end = HandBeam(cam.CameraPos, dir, tick);
            Vector3 from = MuzzlePos(cam.CameraPos);
            if (_beam == null)
            {
                _beam = new GameObject("Arsenal.HandBeam").AddComponent<LineRenderer>();
                _beam.sharedMaterial = Fx.GlowMat(_def.TracerColor, 10f);
                _beam.positionCount = 2;
                _beam.useWorldSpace = true;
                _beam.numCapVertices = 3;
                _beam.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            float w = 0.035f + 0.02f * _ramp + Mathf.Sin(Time.time * 70f) * 0.01f;
            _beam.startWidth = w * 0.6f;
            _beam.endWidth = w;
            _beam.SetPosition(0, from);
            _beam.SetPosition(1, end);
            if (!_loop.hasHandle()) _loop = Sounds.Loop(SoundStyle.Beam, from);
            Sounds.SetPitch(_loop, 0.5f + _ramp * 0.15f);
            Sounds.Move(_loop, from);
            if (Time.time >= _nextRelay)
            {
                _nextRelay = Time.time + 0.1f;
                FxNet.RelayBeam(from, end, 1);
            }
            NotifyFiring();
        }

        /// <summary>The disintegrator: damage ramps up while the beam stays on the same thing.</summary>
        private Vector3 HandBeam(Vector3 origin, Vector3 dir, bool tick)
        {
            PhysicsScene scene = ClientScene.PhysicsScene;
            Vector3 pos = origin;
            for (int i = 0; i < 8; i++)
            {
                if (!scene.Raycast(pos, dir, out RaycastHit hit, _def.Reach, Ballistics.Mask, QueryTriggerInteraction.Ignore))
                {
                    _rampTarget = null;
                    _ramp = 1f;
                    return pos + dir * _def.Reach;
                }
                Collider col = hit.collider;
                object target = col;
                if (ShipPartsTagDetection.ColliderIsHull(col) && Ballistics.TryResolvePanel(hit, out PanelKey key, out HullPart part, out Vector2 local))
                {
                    bool holeAble = HullDamage.CanHaveHoles(part);
                    if (holeAble && HullDamage.TryGetClientHoles(key, out HoleMask existing) && existing.IsHoleAt(local.x, local.y))
                    {
                        pos = hit.point + dir * 0.12f;
                        continue;
                    }
                    target = key;
                    if (tick)
                    {
                        Ramp(target);
                        var holes = new HoleMask();
                        if (holeAble && Random.value < 0.25f * _ramp) holes.PunchDisc(local.x, local.y, 0.03f + 0.01f * _ramp, Ballistics.Rng);
                        HullDamage.ReportHit(key, holes, _def.Bullet.PanelDamage * _ramp);
                        HullDamage.PredictHoles(key, holes);
                        Fx.EmitSparks(hit.point, hit.normal, 5);
                        Fx.Flame(hit.point, hit.normal, 0.05f * _ramp);
                    }
                    return hit.point;
                }
                if (tick)
                {
                    if (Ballistics.TryGetOtherPlayer(hit, out uint player))
                    {
                        Ramp(player);
                        PlayerDamage.Report(player, _def.Bullet.PlayerDamage * _ramp * 4f, _def.Kind, false);
                    }
                    else
                    {
                        Ramp(target);
                        IMinableObject m = col.GetComponentInParent<IMinableObject>();
                        if (m != null)
                            try { m.ReduceHealth(_def.Bullet.ObjectDamage * _ramp); } catch { }
                    }
                    Fx.EmitSparks(hit.point, hit.normal, 4);
                }
                return hit.point;
            }
            return pos;
        }

        private void Ramp(object target)
        {
            if (Equals(target, _rampTarget)) _ramp = Mathf.Min(4f, _ramp + 0.18f);
            else
            {
                _rampTarget = target;
                _ramp = 1f;
            }
        }

        // ------------------------------------------------------------------ flamethrower

        private void UpdateFlame(CameraPhysicsData cam, bool trigger)
        {
            if (!trigger || Reloading)
            {
                StopContinuous();
                return;
            }
            if (_fuel <= 0f)
            {
                StopContinuous();
                StartReload();
                return;
            }
            _fuel = Mathf.Max(0f, _fuel - 20f * Time.fixedDeltaTime);
            Vector3 dir = cam.Direction;
            Vector3 from = MuzzlePos(cam.CameraPos);
            if (!_loop.hasHandle()) _loop = Sounds.Loop(SoundStyle.Flame, from);
            Sounds.Move(_loop, from);
            // visuals: a stream of flames along the cone
            for (int i = 0; i < 2; i++)
                Fx.Flame(from + dir * Random.Range(0.1f, 0.5f), Spread(dir, 8f) * Random.Range(9f, 14f), Random.Range(0.06f, 0.12f));
            if (Time.time >= _nextTick)
            {
                _nextTick = Time.time + 0.1f;
                FlameDamage(cam.CameraPos, dir);
            }
            if (Time.time >= _nextRelay)
            {
                _nextRelay = Time.time + 0.1f;
                FxNet.RelayFlame(from, dir);
            }
            NotifyFiring();
        }

        private void FlameDamage(Vector3 origin, Vector3 dir)
        {
            PhysicsScene scene = ClientScene.PhysicsScene;
            float reach = _def.Reach;
            // the stream stops at the first wall
            if (scene.Raycast(origin, dir, out RaycastHit wall, reach, Ballistics.Mask, QueryTriggerInteraction.Ignore))
            {
                reach = wall.distance;
                if (ShipPartsTagDetection.ColliderIsHull(wall.collider) && Ballistics.TryResolvePanel(wall, out PanelKey key, out HullPart part, out Vector2 local))
                {
                    float mult = part.MaterialType == HullMaterialType.Wood ? 4f : part.MaterialType == HullMaterialType.Glass ? 1.5f : 0.35f;
                    var holes = new HoleMask();
                    if (part.MaterialType == HullMaterialType.Wood && HullDamage.CanHaveHoles(part) && Random.value < 0.2f) holes.PunchDisc(local.x, local.y, 0.05f, Ballistics.Rng);
                    HullDamage.ReportHit(key, holes, _def.Bullet.PanelDamage * mult);
                    HullDamage.PredictHoles(key, holes);
                }
                IMinableObject m = wall.collider.GetComponentInParent<IMinableObject>();
                if (m != null)
                    try { m.ReduceHealth(_def.Bullet.ObjectDamage); } catch { }
            }
            var hit = new HashSet<uint>();
            var cols = new Collider[16];
            for (float d = 1f; d <= reach; d += 1.5f)
            {
                float r = Mathf.Lerp(0.3f, 1.3f, d / _def.Reach);
                int n = scene.OverlapSphere(origin + dir * d, r, cols, ~0, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < n; i++)
                {
                    Rigidbody rb = cols[i].attachedRigidbody;
                    if (rb == null || Svc.PlayersClient == null) continue;
                    foreach (TrackedPlayerOtherClient other in Svc.PlayersClient.OtherPlayers)
                        if (other.IsPhysicsLoaded && other.ClientRigidbody == rb && hit.Add(other.Id))
                            PlayerDamage.Report(other.Id, _def.Bullet.PlayerDamage * 4f, _def.Kind, false, _def.Bullet.BurnSeconds);
                }
            }
        }

        // ------------------------------------------------------------------ electric arc (tesla)

        private void FireArc(CameraPhysicsData cam)
        {
            Vector3 from = MuzzlePos(cam.CameraPos);
            var points = new List<Vector3> { from };
            var done = new HashSet<uint>();
            BulletSpec spec = _def.Bullet;
            PhysicsScene scene = ClientScene.PhysicsScene;
            Vector3 current;
            if (scene.Raycast(cam.CameraPos, cam.Direction, out RaycastHit hit, _def.Reach, Ballistics.Mask, QueryTriggerInteraction.Ignore))
            {
                current = hit.point;
                if (Ballistics.TryGetOtherPlayer(hit, out uint pid))
                {
                    done.Add(pid);
                    PlayerDamage.Report(pid, spec.PlayerDamage, _def.Kind, false, 0f, 1f);
                }
                else if (ShipPartsTagDetection.ColliderIsHull(hit.collider) && Ballistics.TryResolvePanel(hit, out PanelKey key, out _, out _))
                {
                    HullDamage.ReportHit(key, new HoleMask(), spec.PanelDamage);
                }
                else
                {
                    IMinableObject m = hit.collider.GetComponentInParent<IMinableObject>();
                    if (m != null)
                        try { m.ReduceHealth(spec.ObjectDamage); } catch { }
                }
                Fx.EmitSparks(hit.point, hit.normal, 10);
            }
            else
            {
                // nothing in the line of sight: the arc grabs the closest player near the aim
                current = cam.CameraPos + cam.Direction * _def.Reach * 0.5f;
                TrackedPlayerOtherClient near = ClosestPlayer(cam.CameraPos + cam.Direction * 6f, 4f, done);
                if (near != null)
                {
                    current = near.VisualPos + Fx.Up * 1f;
                    done.Add(near.Id);
                    PlayerDamage.Report(near.Id, spec.PlayerDamage, _def.Kind, false, 0f, 1f);
                }
            }
            points.Add(current);
            // jump to up to three more players close by
            for (int j = 0; j < 3; j++)
            {
                TrackedPlayerOtherClient next = ClosestPlayer(current, 6f, done);
                if (next == null) break;
                done.Add(next.Id);
                current = next.VisualPos + Fx.Up * 1f;
                points.Add(current);
                PlayerDamage.Report(next.Id, spec.PlayerDamage * 0.75f, _def.Kind, false, 0f, 1f);
            }
            for (int i = 0; i + 1 < points.Count; i++)
                Fx.ElectricArc(points[i], points[i + 1], _def.TracerColor, 0.18f, 0.04f);
            Sounds.Shot(_def.Kind, from);
            FxNet.RelayArc(points);
            NotifyFiring();
        }

        private static TrackedPlayerOtherClient ClosestPlayer(Vector3 around, float radius, HashSet<uint> skip)
        {
            if (Svc.PlayersClient == null) return null;
            TrackedPlayerOtherClient best = null;
            float bestD = radius;
            foreach (TrackedPlayerOtherClient p in Svc.PlayersClient.OtherPlayers)
            {
                if (!p.IsVisualLoaded || skip.Contains(p.Id)) continue;
                float d = Vector3.Distance(p.VisualPos + Fx.Up, around);
                if (d < bestD)
                {
                    bestD = d;
                    best = p;
                }
            }
            return best;
        }

        // ------------------------------------------------------------------ misc

        private void StopContinuous()
        {
            if (_def != null && _def.SpinUp > 0f && _spin > 0.05f) return; // the minigun winds down on its own
            Sounds.Stop(ref _loop);
            if (_beam != null) Destroy(_beam.gameObject);
            _beam = null;
            _rampTarget = null;
            _ramp = 1f;
        }

        private void UpdateDisplay()
        {
            if (_display == null || _def == null) return;
            _display.text = Reloading ? "--" : _ammo.ToString();
            _display.color = _ammo == 0 || Reloading ? new Color(1f, 0.45f, 0.25f) : new Color(0.55f, 0.95f, 1f);
        }

        /// <summary>Recoil kick-back at the hip, added to the game's tool sway (the aim pose is done at render time).</summary>
        public Vector3 UpdateOffset()
        {
            float hip = 1f - _aimBlend;
            Vector3 shake = _def != null && (_def.SpinUp > 0f && _spin > 0.9f && _trigger || _def.Mode == FireMode.Flame && _trigger)
                ? Random.insideUnitSphere * 0.004f : Vector3.zero;
            return new Vector3(0f, _recoil * 0.25f, -_recoil) * hip + shake;
        }
    }
}
