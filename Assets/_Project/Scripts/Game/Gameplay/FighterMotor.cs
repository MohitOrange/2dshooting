using UnityEngine;

namespace NeonBrawl
{
    /// <summary>
    /// Top-down movement: snappy acceleration, dash roll, and physics knockback that decays.
    /// Runs in FixedUpdate on a dynamic Rigidbody2D so walls and other fighters push back.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class FighterMotor : MonoBehaviour
    {
        private const float MaxKnockback = 22f;

        private Rigidbody2D _rb;
        private PlayerTuning _t;
        private Vector2 _move;
        private Vector2 _vel;
        private Vector2 _knock;
        private Vector2 _dashDir;
        private float _dashTime;
        private float _dashCooldown;

        public bool IsDashing => _dashTime > 0f;
        public float DashReady01 => _t == null || _t.DashCooldown <= 0f ? 1f : 1f - Mathf.Clamp01(_dashCooldown / _t.DashCooldown);
        public Vector2 Velocity => _rb.linearVelocity;

        public void Init(Rigidbody2D rb, PlayerTuning tuning)
        {
            _rb = rb;
            _t = tuning;
        }

        /// <summary>Desired movement; magnitude 0..1 already includes weapon speed multipliers.</summary>
        public void SetMove(Vector2 move) => _move = move;

        public bool TryDash(Vector2 direction)
        {
            if (_dashCooldown > 0f || direction.sqrMagnitude < 0.0001f) return false;
            _dashDir = direction.normalized;
            _dashTime = _t.DashTime;
            _dashCooldown = _t.DashCooldown;
            return true;
        }

        public void AddKnockback(Vector2 impulse)
        {
            _knock = Vector2.ClampMagnitude(_knock + impulse, MaxKnockback);
        }

        public void Teleport(Vector2 position)
        {
            _vel = _knock = Vector2.zero;
            _dashTime = 0f;
            _rb.position = position;
            transform.position = position;
            _rb.linearVelocity = Vector2.zero;
        }

        public void ResetDash() => _dashCooldown = 0f;

        private void FixedUpdate()
        {
            if (_t == null) return;
            float dt = Time.fixedDeltaTime;
            if (_dashTime > 0f)
            {
                _dashTime -= dt;
                _vel = _dashDir * _t.DashSpeed;
            }
            else
            {
                _vel = Vector2.MoveTowards(_vel, _move * _t.MoveSpeed, _t.Acceleration * dt);
            }
            if (_dashCooldown > 0f) _dashCooldown -= dt;

            _knock *= Mathf.Exp(-_t.KnockbackDamping * dt);
            if (_knock.sqrMagnitude < 0.0025f) _knock = Vector2.zero;
            _rb.linearVelocity = _vel + _knock;
        }
    }
}
