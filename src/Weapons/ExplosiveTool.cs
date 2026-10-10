using System.Linq;
using Arsenal.Visual;
using Core.Utils;
using Tools;
using Tools.Interface.Model;
using UnityEngine;

namespace Arsenal.Weapons
{
    /// <summary>
    /// Grenade: left click throws one. Breaching charge: left click sticks one where you look,
    /// right click detonates all your charges.
    /// </summary>
    public class ExplosiveTool : Tool, IToolAnimator
    {
        public uint ItemId;

        private ExplosiveDef _def;
        private bool _wantsPrimary;
        private float _cooldownUntil;
        private float _swing;
        private Transform _model;

        public override IToolAnimator Animator => this;

        protected override void AfterAwake()
        {
            _def = Explosives.ByItem(ItemId);
            _model = GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name.EndsWith("Model"));
        }

        public override void PrimaryActionBegin() => _wantsPrimary = true;

        public override void SecondaryActionBegin()
        {
            if (_def.Kind != WeaponKind.Charge) return;
            Explosives.DetonateMine();
            Sounds.Beep(transform.position);
        }

        public override void UpdateChecks(CameraPhysicsData cam, float deltaTime)
        {
            if (!_wantsPrimary) return;
            _wantsPrimary = false;
            if (Time.time < _cooldownUntil) return;
            if (_def.Kind == WeaponKind.Grenade)
            {
                Vector3 from = cam.CameraPos + cam.Direction * 0.4f;
                Vector3 up = Vector3.ProjectOnPlane(Arsenal.Visual.Fx.Up, cam.Direction).normalized;
                Explosives.Throw(from, (cam.Direction + up * 0.12f).normalized * 13f, true);
                Explosives.Consume(ItemId);
                Sounds.Throw(from);
                _cooldownUntil = Time.time + 0.9f;
                _swing = 1f;
            }
            else if (Explosives.TryPlaceCharge(cam.CameraPos, cam.Direction))
            {
                _cooldownUntil = Time.time + 0.6f;
                _swing = 0.6f;
            }
        }

        private void Update()
        {
            _swing = Mathf.MoveTowards(_swing, 0f, Time.deltaTime * 2.2f);
            if (_model != null && _def.Kind == WeaponKind.Grenade)
                _model.gameObject.SetActive(_swing < 0.45f);
        }

        /// <summary>Throw / push motion.</summary>
        public Vector3 UpdateOffset()
        {
            float s = Mathf.Sin(_swing * Mathf.PI);
            return new Vector3(-0.05f * s, 0.08f * s, 0.15f * s);
        }
    }
}
