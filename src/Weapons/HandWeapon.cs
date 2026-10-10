using System.Collections.Generic;
using System.Linq;
using Arsenal.Visual;
using Core.Utils;
using TMPro;
using Tools;
using Tools.Interface.Model;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Arsenal.Weapons
{
    /// <summary>
    /// A gun held like the game's tools. Runs only for the local player (the game instantiates tools for the
    /// local player; other players only get the visuals). Shots are hit-scan with penetration (see Ballistics).
    /// </summary>
    public class HandWeapon : Tool, IToolAnimator
    {
        public uint ItemId;

        private HandWeaponDef _def;
        private Transform _muzzle;
        private TextMeshPro _display;
        private int _ammo;
        private float _nextShot;
        private float _reloadDone;
        private bool _trigger;
        private bool _pendingShot;
        private bool _aiming;
        private float _aimBlend;
        private float _recoil;
        private float _stopFiringAt;
        private bool _firingNotified;
        private Camera _camera;
        private float _baseFov;
        private readonly List<ImpactFx> _impacts = new List<ImpactFx>();

        // Ammo is kept per weapon type for the session, so switching tools does not refill the magazine.
        private static readonly Dictionary<uint, int> AmmoLeft = new Dictionary<uint, int>();

        public override IToolAnimator Animator => this;

        public override bool BlocksPickInteractions => false;

        private bool Reloading => _reloadDone > 0f;

        protected override void AfterAwake()
        {
            _def = WeaponDefs.ByItem(ItemId);
            if (_def == null) return; // the template itself (its item id is set after it wakes up)
            _muzzle = GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Muzzle");
            _display = GetComponentInChildren<TextMeshPro>(true);
            _ammo = AmmoLeft.TryGetValue(ItemId, out int a) ? a : _def.Magazine;
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
            AmmoLeft[ItemId] = _ammo;
            base.DisableTool();
        }

        private void OnDestroy()
        {
            RestoreFov();
            if (_def != null) AmmoLeft[ItemId] = _ammo;
        }

        public override void PrimaryActionBegin()
        {
            _trigger = true;
            _pendingShot = true;
        }

        public override void PrimaryActionEnd()
        {
            _trigger = false;
        }

        public override void SecondaryActionBegin()
        {
            if (_def.AimFov > 0f) _aiming = !_aiming;
        }

        public override void TertiaryActionBegin() => StartReload();

        private void StartReload()
        {
            if (Reloading || _ammo >= _def.Magazine) return;
            _reloadDone = Time.time + _def.ReloadTime;
            _aiming = false;
            Sounds.Reload(transform.position);
            UpdateDisplay();
        }

        private void Update()
        {
            if (!enabled || _def == null) return;
            Keyboard kb = Keyboard.current;
            if (kb != null && kb.rKey.wasPressedThisFrame) StartReload();

            if (Reloading && Time.time >= _reloadDone)
            {
                _reloadDone = 0f;
                _ammo = _def.Magazine;
                UpdateDisplay();
            }
            if (_firingNotified && Time.time >= _stopFiringAt)
            {
                _firingNotified = false;
                FiringEffectListener?.NotifyToolFiringStop();
            }
            _recoil = Mathf.MoveTowards(_recoil, 0f, Time.deltaTime * _def.RecoilKick * 12f);
            _aimBlend = Mathf.MoveTowards(_aimBlend, _aiming ? 1f : 0f, Time.deltaTime * 6f);
        }

        private void LateUpdate()
        {
            if (_def == null || _camera == null || _def.AimFov <= 0f) return;
            if (_aimBlend > 0f)
                _camera.fieldOfView = Mathf.Lerp(_baseFov, _def.AimFov, _aimBlend);
            else if (Mathf.Abs(_camera.fieldOfView - _baseFov) > 0.01f && Mathf.Abs(_camera.fieldOfView - _def.AimFov) < Mathf.Abs(_baseFov - _def.AimFov))
                _camera.fieldOfView = _baseFov;
        }

        private void OnEnable()
        {
            UnityEngine.Rendering.RenderPipelineManager.beginCameraRendering += OnBeginCamera;
        }

        private void OnDisable()
        {
            UnityEngine.Rendering.RenderPipelineManager.beginCameraRendering -= OnBeginCamera;
        }

        /// <summary>
        /// Final placement right before the camera renders: blends the model from its hip pose to a pose where
        /// the sights sit on the line of sight. The scoped rifle hides the model and shows the scope instead.
        /// </summary>
        private void OnBeginCamera(UnityEngine.Rendering.ScriptableRenderContext ctx, Camera cam)
        {
            if (_model == null || cam != _camera) return;
            _model.localPosition = _holdPos;
            _model.localRotation = _holdRot;
            bool scoped = _def.Kind == WeaponKind.Marksman && _aimBlend > 0.85f;
            if (_bodyRenderer != null) _bodyRenderer.enabled = !scoped;
            if (_aimBlend <= 0f || scoped) return;
            float k = Mathf.SmoothStep(0f, 1f, _aimBlend);
            Transform ct = cam.transform;
            Quaternion aimRot = ct.rotation;
            // where the sight point must be: centred, a few cm in front of the eye
            Vector3 sightTarget = ct.position + ct.rotation * new Vector3(0f, 0f, _def.Kind == WeaponKind.Pistol ? 0.32f : 0.26f);
            Quaternion modelRot = Quaternion.Slerp(_model.rotation, aimRot, k);
            Vector3 aimPos = sightTarget - aimRot * _sightLocal;
            Vector3 hipPos = _model.position;
            _model.rotation = modelRot;
            _model.position = Vector3.Lerp(hipPos, aimPos, k) + ct.rotation * new Vector3(0f, -_recoil * 0.2f, -_recoil);
        }

        private Transform _model;
        private Renderer _bodyRenderer;
        private Vector3 _holdPos, _sightLocal;
        private Quaternion _holdRot;

        private void CacheModel()
        {
            _model = GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name.EndsWith("Model"));
            if (_model == null) return;
            _holdPos = _model.localPosition;
            _holdRot = _model.localRotation;
            _bodyRenderer = _model.GetComponentInChildren<Renderer>(true);
            Transform sight = _model.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Sight");
            _sightLocal = sight != null ? sight.localPosition : Vector3.up * 0.06f;
        }

        private void OnGUI()
        {
            if (_def == null || !enabled || Arsenal.Visual.Assets.IsTemplate(this)) return;
            if (_def.Kind == WeaponKind.Marksman && _aimBlend > 0.85f) ScopeOverlay.Draw();
            float reload = Reloading ? 1f - (_reloadDone - Time.time) / _def.ReloadTime : -1f;
            AmmoHud.Draw(Loc.T(_def.NameFr, _def.NameEn), _ammo, _def.Magazine, reload, _def.Automatic);
        }

        private void RestoreFov()
        {
            if (_camera != null && _aimBlend > 0f) _camera.fieldOfView = _baseFov;
            _aimBlend = 0f;
        }

        public override void UpdateChecks(CameraPhysicsData cameraPhysics, float deltaTime)
        {
            if (_def == null) return;
            bool wants = _def.Automatic ? _trigger : _pendingShot;
            _pendingShot = false;
            if (!wants || Reloading || Time.time < _nextShot) return;
            if (_ammo <= 0)
            {
                Sounds.DryFire(transform.position);
                _nextShot = Time.time + 0.25f;
                StartReload();
                return;
            }
            _ammo--;
            _nextShot = Time.time + 60f / _def.RoundsPerMinute;
            Shoot(cameraPhysics);
            UpdateDisplay();
        }

        private void Shoot(CameraPhysicsData cam)
        {
            float spread = Mathf.Lerp(_def.SpreadDegrees, _def.AimSpreadDegrees, _aimBlend);
            Vector3 dir = Quaternion.AngleAxis(Random.Range(0f, 360f), cam.Direction) *
                          Quaternion.AngleAxis(Random.Range(0f, spread), Vector3.Cross(cam.Direction, Fx.Up).normalized) * cam.Direction;
            _impacts.Clear();
            Vector3 end = Ballistics.Fire(cam.CameraPos, dir.normalized, _def.Bullet, _impacts);

            Vector3 muzzle = _muzzle != null ? _muzzle.position : cam.CameraPos;
            float size = _def.Kind == WeaponKind.Marksman ? 0.35f : _def.Kind == WeaponKind.Rifle ? 0.2f : 0.15f;
            Fx.MuzzleFlash(muzzle, _muzzle != null ? _muzzle.forward : dir, size, _muzzle);
            Fx.BulletTracer(muzzle, end, _def.Kind == WeaponKind.Marksman ? 0.04f : 0.022f);
            Ballistics.PlayImpacts(_impacts);
            Sounds.Shot(_def.Kind, muzzle);
            Ballistics.RelayShot(_def.Kind, muzzle, end, _impacts);

            _recoil = Mathf.Min(_recoil + _def.RecoilKick, _def.RecoilKick * 2.5f);
            _stopFiringAt = Time.time + 0.12f;
            if (!_firingNotified)
            {
                _firingNotified = true;
                FiringEffectListener?.NotifyToolFiringBegin();
            }
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
            return new Vector3(0f, _recoil * 0.25f, -_recoil) * hip;
        }
    }
}
