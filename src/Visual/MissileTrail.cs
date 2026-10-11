using UnityEngine;

namespace Arsenal.Visual
{
    /// <summary>
    /// The exhaust of a flying missile: a hot glowing core, a long fading vapour streak that stays visible
    /// from far away, an exhaust light and a thick trail of round smoke puffs that hang in the air and billow
    /// out for seconds. When the missile dies the streaks are left behind and fade on their own.
    /// </summary>
    internal sealed class MissileTrail : MonoBehaviour
    {
        public float Size = 1f;                       // overall scale of the smoke and glow
        public float SmokeLife = 5f;                  // seconds a puff hangs in the air
        public float StreakTime = 2.5f;               // seconds of vapour streak behind the missile
        public Color Smoke = new Color(0.86f, 0.85f, 0.82f);
        public Color Core = new Color(1f, 0.55f, 0.2f);
        public Color Streak = new Color(1f, 0.92f, 0.85f);
        public float Nozzle = 0.45f;                  // distance of the motor behind the transform

        private TrailRenderer _core, _streak;
        private Light _light;
        private Vector3 _last;
        private bool _hasLast;
        private float _carry, _nextFlame;

        public static MissileTrail Attach(GameObject go, float size, float smokeLife, float streakTime)
        {
            var t = go.AddComponent<MissileTrail>();
            t.Size = size;
            t.SmokeLife = smokeLife;
            t.StreakTime = streakTime;
            return t;
        }

        private void Start()
        {
            _core = MakeTrail("Arsenal.TrailCore", Fx.GlowMat(Core, 9f), 0.18f, 0.2f * Size, 0f);
            _streak = MakeTrail("Arsenal.TrailStreak", Fx.GlowMat(Streak, 1.6f), StreakTime, 0.1f * Size, 0.7f * Size);
            var lg = new GameObject("Arsenal.TrailLight");
            lg.transform.SetParent(transform, false);
            lg.transform.localPosition = new Vector3(0, 0, -Nozzle);
            _light = lg.AddComponent<Light>();
            _light.type = LightType.Point;
            _light.color = Core;
            _light.range = 9f * Size;
            _light.shadows = LightShadows.None;
        }

        /// <summary>A ribbon that starts at <paramref name="w0"/> on the missile, swells to <paramref name="w1"/> then thins out at its tail.</summary>
        private TrailRenderer MakeTrail(string name, Material mat, float time, float w0, float w1)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0, 0, -Nozzle);
            var tr = go.AddComponent<TrailRenderer>();
            tr.sharedMaterial = mat;
            tr.time = time;
            tr.minVertexDistance = 0.4f * Size;
            tr.numCapVertices = 2;
            tr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            tr.receiveShadows = false;
            tr.widthMultiplier = 1f;
            tr.widthCurve = w1 > w0
                ? new AnimationCurve(new Keyframe(0f, w0), new Keyframe(0.35f, w1), new Keyframe(1f, 0f))
                : new AnimationCurve(new Keyframe(0f, w0), new Keyframe(1f, 0f));
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.6f, 0.4f), new GradientAlphaKey(0f, 1f) });
            tr.colorGradient = g;
            return tr;
        }

        private void LateUpdate()
        {
            Vector3 nozzle = transform.position - transform.forward * Nozzle;
            if (!_hasLast)
            {
                _last = nozzle;
                _hasLast = true;
                return;
            }
            float dt = Time.deltaTime;
            Vector3 path = nozzle - _last;
            float len = path.magnitude;
            float speed = dt > 0f ? len / dt : 0f;
            // puffs every metre or so at missile speeds, further apart (but never more than 10 a frame) when much faster
            float spacing = Mathf.Max(0.8f, 0.7f * Size, speed * 0.007f, len / 10f);
            _carry += len;
            Vector3 dir = len > 0.0001f ? path / len : -transform.forward;
            float along = len - _carry;
            while (_carry >= spacing)
            {
                _carry -= spacing;
                along += spacing;
                Vector3 p = _last + dir * Mathf.Clamp(along, 0f, len);
                float r = 0.38f * Size * Random.Range(0.8f, 1.3f);
                Fx.Puff(p, r, Smoke * Random.Range(0.92f, 1.05f), SmokeLife, 1, Random.insideUnitSphere * 0.35f * Size);
            }
            if (Time.time >= _nextFlame)
            {
                _nextFlame = Time.time + 0.025f;
                Fx.Flame(nozzle, -transform.forward * 4f, 0.13f * Size);
            }
            if (_light != null) _light.intensity = (4f + Random.value * 3f) * Mathf.Min(1.5f, Size);
            _last = nozzle;
        }

        private void OnDestroy()
        {
            // leave the streaks hanging in the air: they fade out and clean themselves up
            foreach (TrailRenderer tr in new[] { _core, _streak })
            {
                if (tr == null) continue;
                tr.transform.SetParent(null, true);
                tr.emitting = false;
                tr.autodestruct = true;
            }
        }
    }
}
