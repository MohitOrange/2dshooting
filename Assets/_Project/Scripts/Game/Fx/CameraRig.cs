using UnityEngine;

namespace NeonBrawl
{
    /// <summary>
    /// Follow camera with aim look-ahead, scope zoom, trauma-based shake (noise, not random jitter),
    /// recoil kick, kill-cam on the killer, and a slow fly-over for the menu background.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class CameraRig : MonoBehaviour
    {
        public const float BaseSize = 6f;

        private Camera _cam;
        private GameContext _ctx;
        private Fighter _follow;
        private Fighter _temp;
        private float _tempUntil;
        private Vector2 _pos;
        private Vector2 _vel;
        private Vector2 _kick;
        private float _trauma;
        private float _zoom;
        private float _size = BaseSize;
        private float _seed;
        private bool _demo;
        private Vector2 _demoCenter;
        private Vector2 _demoExtent;
        private float _demoT;
        private bool _hold;
        private bool _hasBounds;
        private Rect _bounds;
        private Vector2 _holdCenter;
        private float _holdSize;
        private float _holdEase = 6f;
        private float _punch;

        public Fighter Following => _follow;

        public void Init(GameContext ctx, Camera cam)
        {
            _ctx = ctx;
            _cam = cam;
            _cam.orthographic = true;
            _seed = Random.value * 100f;
            _pos = transform.position;
        }

        public void Follow(Fighter f, bool snap)
        {
            _follow = f;
            _demo = false;
            _hold = false;
            _temp = null;
            if (snap && f != null)
            {
                _pos = f.Position;
                _vel = Vector2.zero;
            }
        }

        /// <summary>Shows another fighter for a moment (kill-cam).</summary>
        public void FollowTemporary(Fighter f, float seconds)
        {
            if (f == null) return;
            _temp = f;
            _tempUntil = Time.time + seconds;
        }

        public void SetDemoPan(Vector2 center, Vector2 extent)
        {
            _demo = true;
            _hold = false;
            _demoCenter = center;
            _demoExtent = extent;
        }

        /// <summary>Lobby: hold the camera on a spot with a slow breathing drift (snaps there).</summary>
        public void Hold(Vector2 center, float size, bool intro = false, Vector2 introFocus = default)
        {
            _hold = true;
            _hasBounds = false;
            _demo = false;
            _follow = null;
            _temp = null;
            _holdCenter = center;
            _holdSize = size;
            _vel = Vector2.zero;
            // intro: start close on the focus point, then glide out to the full view
            _pos = intro ? Vector2.Lerp(center, introFocus, 0.7f) : center;
            _size = intro ? size * 0.42f : size;
            _holdEase = intro ? 1.4f : 6f;
        }

        /// <summary>Keeps the view inside the map (plus a small margin) so no empty space shows past the edges.</summary>
        public void SetBounds(Rect world) { _bounds = world; _hasBounds = true; }

        public void ClearBounds() => _hasBounds = false;

        public void SetZoom(float size) => _zoom = size;

        public void Kick(Vector2 v) => _kick += v;

        /// <summary>Quick zoom-in that springs back (kill confirm).</summary>
        public void Punch(float amount = 0.07f) => _punch = Mathf.Max(_punch, amount);

        public void AddShake(float amount)
        {
            float k = _ctx != null ? _ctx.Prefs.ShakeStrength : 1f;
            _trauma = Mathf.Min(1f, _trauma + amount * k);
        }

        public void ShakeFromExplosion(Vector2 pos, float radius)
        {
            float d = Vector2.Distance(pos, _pos);
            float f = Mathf.Clamp01(1f - d / (radius * 5f));
            if (f <= 0f) return;
            AddShake(0.75f * f);
            Kick((_pos - pos).normalized * 0.3f * f);
            if (f > 0.35f) _ctx.Haptics.Pulse(Mathf.RoundToInt(40f + 50f * f));
        }

        private void LateUpdate()
        {
            float dt = Time.deltaTime;
            float udt = Time.unscaledDeltaTime;
            Fighter t = _temp != null && Time.time < _tempUntil ? _temp : _follow;
            Vector2 desired = _pos;
            if (t != null)
            {
                desired = t.Position;
                if (t == _follow && t.Alive)
                {
                    float look = _zoom > 0f ? 4f : 2.2f;
                    if (t.IsLocal && _ctx.Input.UsingMouse)
                    {
                        Vector2 toMouse = _ctx.Input.MouseWorld - t.Position;
                        desired += Vector2.ClampMagnitude(toMouse * 0.3f, look);
                    }
                    else if (t.Input.HasAim || t.IsBot)
                    {
                        desired += t.AimDir * look;
                    }
                }
            }
            else if (_hold)
            {
                float ht = Time.unscaledTime;
                desired = _holdCenter + new Vector2(Mathf.Sin(ht * 0.13f) * 0.35f, Mathf.Sin(ht * 0.17f + 1f) * 0.2f);
            }
            else if (_demo)
            {
                _demoT += udt * 0.06f;
                desired = _demoCenter + new Vector2(Mathf.Cos(_demoT) * _demoExtent.x, Mathf.Sin(_demoT * 1.3f) * _demoExtent.y);
            }

            if (_hasBounds && !_hold)
            {
                float halfH = _size, halfW = _size * Mathf.Max(0.5f, _cam.aspect);
                desired.x = _bounds.width > halfW * 2f ? Mathf.Clamp(desired.x, _bounds.xMin + halfW, _bounds.xMax - halfW) : _bounds.center.x;
                desired.y = _bounds.height > halfH * 2f ? Mathf.Clamp(desired.y, _bounds.yMin + halfH, _bounds.yMax - halfH) : _bounds.center.y;
            }
            _pos = Vector2.SmoothDamp(_pos, desired, ref _vel, _hold ? 0.55f : 0.13f, Mathf.Infinity, Mathf.Max(dt, 0.0001f));
            _kick = Vector2.Lerp(_kick, Vector2.zero, 1f - Mathf.Exp(-14f * udt));

            _trauma = Mathf.Max(0f, _trauma - udt * 1.5f);
            float sh = _trauma * _trauma;
            float time = Time.unscaledTime * 22f;
            Vector2 shake = new Vector2(Mathf.PerlinNoise(_seed, time) - 0.5f, Mathf.PerlinNoise(_seed + 7f, time) - 0.5f) * (1.1f * sh);
            float roll = (Mathf.PerlinNoise(_seed + 13f, time) - 0.5f) * 5f * sh;

            float aspect = Mathf.Max(0.5f, _cam.aspect);
            float baseSize = Mathf.Max(BaseSize, 10f / aspect);
            float targetSize = _hold ? _holdSize : _zoom > 0f ? Mathf.Max(_zoom, baseSize) : baseSize;
            _size = Mathf.Lerp(_size, targetSize, 1f - Mathf.Exp(-(_hold ? _holdEase : 6f) * udt));
            _punch = Mathf.MoveTowards(_punch, 0f, udt * 0.22f);
            _cam.orthographicSize = _size * (1f - _punch);

            transform.SetPositionAndRotation(new Vector3(_pos.x + shake.x + _kick.x, _pos.y + shake.y + _kick.y, -10f), Quaternion.Euler(0f, 0f, roll));
        }
    }
}
