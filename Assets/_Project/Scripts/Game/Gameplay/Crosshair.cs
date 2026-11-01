using UnityEngine;

namespace NeonBrawl
{
    /// <summary>
    /// World-space crosshair for the local player (at the mouse, or ahead of the aim stick) with a
    /// hit-marker X that flashes white on hits and red on kills. When auto shoot locks onto an enemy, four
    /// red ticks spin around that enemy and (without a mouse) the crosshair sits on them.
    /// </summary>
    public sealed class Crosshair : MonoBehaviour
    {
        private GameContext _ctx;
        private Fighter _f;
        private SpriteRenderer _ring;
        private SpriteRenderer _dot;
        private SpriteRenderer[] _marks;
        private float _hitT;
        private bool _kill;
        private float _pulse;
        private Transform _lock;
        private SpriteRenderer[] _lockMarks;
        private Fighter _locked;
        private float _lockT;

        public void Init(GameContext ctx, Fighter fighter)
        {
            _ctx = ctx;
            _f = fighter;
            var art = ctx.Art;
            _ring = SR(art.ThinRing, new Vector3(0.55f, 0.55f, 1f), 0f, new Color(1f, 1f, 1f, 0.85f));
            _dot = SR(art.Circle, new Vector3(0.07f, 0.07f, 1f), 0f, new Color(1f, 1f, 1f, 0.95f));
            _marks = new SpriteRenderer[4];
            for (int i = 0; i < 4; i++)
            {
                _marks[i] = SR(art.Square, new Vector3(0.24f, 0.05f, 1f), 45f + 90f * i, Color.white);
                _marks[i].enabled = false;
            }
            _lock = new GameObject("AutoLock").transform;
            _lock.SetParent(transform.parent, false);
            _lockMarks = new SpriteRenderer[4];
            for (int i = 0; i < 4; i++)
            {
                float ang = (45f + 90f * i) * Mathf.Deg2Rad;
                _lockMarks[i] = SR(art.Square, new Vector3(0.26f, 0.07f, 1f), 45f + 90f * i, new Color(1f, 0.3f, 0.25f, 0.95f), _lock);
                _lockMarks[i].transform.localPosition = new Vector3(Mathf.Cos(ang), Mathf.Sin(ang), 0f) * 0.62f;
            }
            _lock.gameObject.SetActive(false);
            ctx.Events.LocalHitConfirmed += OnHit;
        }

        private SpriteRenderer SR(Sprite s, Vector3 scale, float rot, Color c, Transform parent = null)
        {
            var go = new GameObject("X");
            go.transform.SetParent(parent != null ? parent : transform, false);
            go.transform.localScale = scale;
            go.transform.localRotation = Quaternion.Euler(0f, 0f, rot);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = s;
            sr.sharedMaterial = _ctx.Art.Solid;
            sr.color = c;
            sr.sortingOrder = SortOrder.Crosshair;
            return sr;
        }

        private void OnDestroy()
        {
            if (_ctx != null) _ctx.Events.LocalHitConfirmed -= OnHit;
            if (_lock != null) Destroy(_lock.gameObject);
        }

        private void OnHit(bool killed)
        {
            _hitT = killed ? 0.35f : 0.14f;
            _kill = killed;
            _pulse = 1f;
        }

        /// <summary>Spinning red ticks around the enemy auto shoot is firing at; they snap in when it locks on.</summary>
        private void UpdateLock(Fighter target)
        {
            bool on = target != null && target.Alive;
            if (_lock.gameObject.activeSelf != on) _lock.gameObject.SetActive(on);
            if (!on) { _locked = null; return; }
            if (target != _locked)
            {
                _locked = target;
                _lockT = 0f;
            }
            _lockT = Mathf.Min(1f, _lockT + Time.deltaTime * 6f);
            float s = Mathf.Lerp(1.9f, 1f, 1f - (1f - _lockT) * (1f - _lockT));
            _lock.position = target.Position;
            _lock.localScale = new Vector3(s, s, 1f);
            _lock.localRotation = Quaternion.Euler(0f, 0f, -Time.time * 90f);
        }

        private void LateUpdate()
        {
            bool show = _f != null && _f.Alive && (_ctx.Input.UsingMouse || _f.Input.HasAim);
            _ring.enabled = _dot.enabled = show;
            var target = show && _f.Brain is PlayerBrain pb ? pb.AutoTarget : null;
            UpdateLock(target);
            if (!show)
            {
                for (int i = 0; i < 4; i++) _marks[i].enabled = false;
                return;
            }
            Vector2 p;
            if (_ctx.Input.UsingMouse) p = _ctx.Input.MouseWorld;
            else if (target != null) p = target.Position;
            else
            {
                float dist = Mathf.Clamp(_f.Weapons.Current.Stats.Range * 0.4f, 3f, 6f);
                p = _f.Position + _f.AimDir * dist;
            }
            transform.position = p;

            _pulse = Mathf.MoveTowards(_pulse, 0f, Time.deltaTime * 6f);
            float spread = _f.Weapons.Current.Stats.SpreadDeg;
            float s = 0.45f + spread * 0.025f + _pulse * 0.2f;
            _ring.transform.localScale = new Vector3(s, s, 1f);
            _ring.color = _f.Weapons.IsReloading ? new Color(1f, 0.8f, 0.3f, 0.6f) : new Color(1f, 1f, 1f, 0.85f);

            if (_hitT > 0f)
            {
                _hitT -= Time.deltaTime;
                float a = Mathf.Clamp01(_hitT / 0.14f);
                Color c = _kill ? new Color(1f, 0.25f, 0.2f, a) : new Color(1f, 1f, 1f, a);
                float off = 0.18f + (1f - a) * 0.08f;
                for (int i = 0; i < 4; i++)
                {
                    float ang = (45f + 90f * i) * Mathf.Deg2Rad;
                    _marks[i].enabled = true;
                    _marks[i].color = c;
                    _marks[i].transform.localPosition = new Vector3(Mathf.Cos(ang) * off, Mathf.Sin(ang) * off, 0f);
                }
            }
            else if (_marks[0].enabled)
            {
                for (int i = 0; i < 4; i++) _marks[i].enabled = false;
            }
        }
    }
}
