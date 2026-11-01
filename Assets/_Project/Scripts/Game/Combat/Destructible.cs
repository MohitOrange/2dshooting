using UnityEngine;

namespace NeonBrawl
{
    /// <summary>
    /// Explosive barrel or breakable crate: shakes and flashes when shot. Barrels explode (chain
    /// reactions included) and leave a scorch mark; crates burst into debris, opening the cover.
    /// Both come back later when nobody is standing on the spot.
    /// </summary>
    public sealed class Destructible : MonoBehaviour, IDamageable
    {
        public enum Kind { Explosive, Breakable }

        private GameContext _ctx;
        private Kind _kind;
        private SpriteRenderer _sr;
        private SpriteRenderer _shadow;
        private Collider2D _col;
        private Vector2Int _cell;
        private Vector3 _basePos;
        private float _maxHp;
        private float _hp;
        private float _shakeT;
        private float _fuse = -1f;
        private float _respawnAt;
        private Fighter _lastAttacker;

        public bool IsAlive { get; private set; }
        /// <summary>Position in the map's prop list (same on every device: built in the same order).</summary>
        public int Index { get; set; } = -1;

        public void Init(GameContext ctx, Vector2Int cell, SpriteRenderer sr, SpriteRenderer shadow, Collider2D col, Kind kind = Kind.Explosive)
        {
            _ctx = ctx;
            _kind = kind;
            _cell = cell;
            _sr = sr;
            _shadow = shadow;
            _col = col;
            _maxHp = kind == Kind.Explosive ? 30f : 75f;
            _basePos = transform.position;
            Restore();
            ctx.Match?.RegisterProp(this);
        }

        /// <summary>
        /// Bullets and blasts. Online, the host owns props: a client only forwards its own player's hits,
        /// and the host ignores the replayed shots of remote players (their devices report them).
        /// </summary>
        public void TakeDamage(float amount, Fighter attacker, Vector2 direction)
        {
            var m = _ctx.Match;
            var net = m != null ? m.Net : null;
            if (net != null)
            {
                if (!net.IsHost)
                {
                    if (!IsAlive || _fuse >= 0f) return;
                    if (attacker != null && attacker == m.Local)
                    {
                        _shakeT = 0.18f;
                        net.ClaimProp(Index, amount, direction);
                    }
                    return;
                }
                if (attacker != null && attacker.IsPuppet) return;
            }
            ApplyDamage(amount, attacker, direction);
        }

        /// <summary>Authority only (offline, or the host).</summary>
        public void ApplyDamage(float amount, Fighter attacker, Vector2 direction)
        {
            if (!IsAlive || _fuse >= 0f) return;
            if (attacker != null) _lastAttacker = attacker;
            _hp -= amount;
            _shakeT = 0.18f;
            if (_hp > 0f) return;
            if (_kind == Kind.Explosive) _fuse = 0.09f; // tiny delay makes chain reactions read as a ripple
            else Break(direction);
        }

        private void Update()
        {
            if (_ctx == null) return;
            float dt = Time.deltaTime;
            if (IsAlive)
            {
                if (_shakeT > 0f)
                {
                    _shakeT -= dt;
                    float k = _shakeT * (_kind == Kind.Explosive ? 0.4f : 0.25f);
                    transform.position = _basePos + new Vector3(Random.Range(-k, k), Random.Range(-k, k), 0f);
                    float hurt = 1f - Mathf.Clamp01(_hp / _maxHp);
                    var flash = _kind == Kind.Explosive ? new Color(1f, 0.6f, 0.5f) : new Color(1f, 0.85f, 0.7f);
                    _sr.color = Color.Lerp(Color.Lerp(Color.white, new Color(0.8f, 0.75f, 0.7f), hurt * 0.6f), flash, _shakeT / 0.18f);
                }
                else if (transform.position != _basePos)
                {
                    transform.position = _basePos;
                }
                if (_fuse >= 0f)
                {
                    _fuse -= dt;
                    if (_fuse < 0f) Explode();
                }
            }
            else if (Time.time >= _respawnAt && (_ctx.Match == null || _ctx.Match.IsAuthority)
                     && !Physics2D.OverlapCircle(_basePos, 0.6f, Layers.FighterMask))
            {
                Restore();
                _ctx.Fx.SpawnPoof(_basePos, _kind == Kind.Explosive ? new Color(1f, 0.5f, 0.3f) : new Color(1f, 0.85f, 0.55f));
                _ctx.Match?.OnPropChanged(this, true, Vector2.zero);
            }
        }

        /// <summary>Online client: the host broke or rebuilt this prop. Plays the effect, deals no damage.</summary>
        public void NetSetAlive(bool alive, Vector2 dir)
        {
            if (alive)
            {
                if (IsAlive) return;
                Restore();
                _ctx.Fx.SpawnPoof(_basePos, _kind == Kind.Explosive ? new Color(1f, 0.5f, 0.3f) : new Color(1f, 0.85f, 0.55f));
                return;
            }
            if (!IsAlive) return;
            if (_kind == Kind.Breakable) { Break(dir); return; }
            Disable(9999f);
            var g = _ctx.Config.Grenade;
            _ctx.Fx.Explosion(_basePos, g.BarrelRadius);
            _ctx.Sfx.Play(SfxId.Explosion, _basePos, 1f);
            _ctx.CameraRig.ShakeFromExplosion(_basePos, g.BarrelRadius);
        }

        /// <summary>Late join: match the host's state quietly.</summary>
        public void NetSetQuiet(bool alive)
        {
            if (alive == IsAlive) return;
            if (alive) Restore();
            else Disable(9999f);
        }

        private void Explode()
        {
            Disable(35f);
            _ctx.Match?.OnPropChanged(this, false, Vector2.zero);
            var g = _ctx.Config.Grenade;
            Combat.Explode(_ctx, _basePos, g.BarrelRadius, g.BarrelDamage, g.Knockback, _lastAttacker, WeaponId.Barrel, this);
        }

        private void Break(Vector2 dir)
        {
            Disable(28f);
            if (_ctx.Match != null && _ctx.Match.IsAuthority) _ctx.Match.OnPropChanged(this, false, dir);
            _ctx.Fx.Debris(_basePos, dir, new Color(0.62f, 0.44f, 0.26f), _sr.sprite);
            _ctx.Sfx.Play(SfxId.HitWall, _basePos, 1f);
            _ctx.CameraRig.ShakeFromExplosion(_basePos, 0.5f);
        }

        private void Disable(float respawn)
        {
            IsAlive = false;
            _fuse = -1f;
            _col.enabled = false;
            _sr.enabled = false;
            if (_shadow != null) _shadow.enabled = false;
            transform.position = _basePos;
            _ctx.Match?.SetCellWalkable(_cell, true);
            _respawnAt = Time.time + respawn;
        }

        private void Restore()
        {
            IsAlive = true;
            _hp = _maxHp;
            _fuse = -1f;
            _lastAttacker = null;
            _col.enabled = true;
            _sr.enabled = true;
            _sr.color = Color.white;
            if (_shadow != null) _shadow.enabled = true;
            _ctx.Match?.SetCellWalkable(_cell, false);
        }
    }
}
