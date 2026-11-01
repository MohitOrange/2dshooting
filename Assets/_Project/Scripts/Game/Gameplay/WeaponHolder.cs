using UnityEngine;

namespace NeonBrawl
{
    /// <summary>Runtime ammo state for one carried weapon.</summary>
    public sealed class WeaponSlot
    {
        public readonly WeaponStats Stats;
        public int Mag;
        public int Reserve;

        public WeaponSlot(WeaponStats stats)
        {
            Stats = stats;
            Mag = stats.MagSize;
            Reserve = stats.Reserve;
        }

        public bool CanReload => Mag < Stats.MagSize && (Stats.InfiniteReserve || Reserve > 0);
        public bool Dry => Mag <= 0 && !Stats.InfiniteReserve && Reserve <= 0;
    }

    /// <summary>
    /// Two slots like neon-brawl (pistol + one picked-up weapon), plus knife and grenades.
    /// Fires through <see cref="Combat"/> / <see cref="ProjectileSystem"/> and adds the juice
    /// (recoil, camera kick, muzzle flash, shells, sound). Perf: 0 B GC per shot.
    /// </summary>
    public sealed class WeaponHolder : MonoBehaviour
    {
        private GameContext _ctx;
        private Fighter _f;
        private WeaponSlot _pistol;
        private WeaponSlot _primary;
        private bool _usePrimary;
        private float _cooldown;
        private float _reload;
        private float _reloadTotal;
        private float _meleeCd;
        private float _grenadeCd;
        private float _emptyCd;

        public WeaponSlot Current => _usePrimary && _primary != null ? _primary : _pistol;
        public WeaponSlot Primary => _primary;
        public bool IsReloading => _reload > 0f;
        public float Reload01 => _reloadTotal > 0f ? 1f - Mathf.Clamp01(_reload / _reloadTotal) : 1f;
        public float MoveMultiplier => Current != null ? Current.Stats.MoveMultiplier : 1f;
        /// <summary>Changes whenever ammo or weapons change, so the HUD only redraws then.</summary>
        public int Version { get; private set; }

        public void Init(GameContext ctx, Fighter fighter)
        {
            _ctx = ctx;
            _f = fighter;
            ResetLoadout();
        }

        public void ResetLoadout()
        {
            _pistol = new WeaponSlot(_ctx.Config.Get(WeaponId.Pistol));
            _primary = null;
            _usePrimary = false;
            _cooldown = _reload = _meleeCd = _grenadeCd = 0f;
            Changed();
        }

        /// <summary>Equips a picked-up weapon. Returns the slot it replaced (to drop on the floor), or null.</summary>
        public WeaponSlot GivePrimary(WeaponId id, int mag = -1, int reserve = int.MinValue)
        {
            var old = _primary;
            _primary = new WeaponSlot(_ctx.Config.Get(id));
            if (mag >= 0) _primary.Mag = Mathf.Min(mag, _primary.Stats.MagSize);
            if (reserve != int.MinValue && !_primary.Stats.InfiniteReserve) _primary.Reserve = Mathf.Max(0, reserve);
            _usePrimary = true;
            _reload = 0f;
            _cooldown = 0.15f;
            Changed();
            return old;
        }

        public bool RefillAmmo()
        {
            if (_primary == null || _primary.Stats.InfiniteReserve || _primary.Reserve >= _primary.Stats.Reserve) return false;
            _primary.Reserve = _primary.Stats.Reserve;
            Changed();
            return true;
        }

        public void Tick(in FighterInput input, float dt)
        {
            if (_cooldown > 0f) _cooldown -= dt;
            if (_meleeCd > 0f) _meleeCd -= dt;
            if (_grenadeCd > 0f) _grenadeCd -= dt;
            if (_emptyCd > 0f) _emptyCd -= dt;
            if (_reload > 0f)
            {
                _reload -= dt;
                if (_reload <= 0f) FinishReload();
            }
            if (input.Swap) Swap();
            if (input.Reload) StartReload();
            if (input.Melee) DoMelee();
            if (input.Grenade) ThrowGrenade(input.ThrowDistance);
            if (input.Fire) TryFire();
        }

        private void TryFire()
        {
            var cur = Current;
            if (_reload > 0f || _cooldown > 0f) return;
            if (cur.Mag <= 0)
            {
                if (cur.CanReload) StartReload();
                else if (cur == _primary)
                {
                    _usePrimary = false;
                    _cooldown = 0.2f;
                    Changed();
                }
                else if (_emptyCd <= 0f)
                {
                    _emptyCd = 0.3f;
                    _ctx.Sfx.Play(SfxId.Empty, _f.Position, 0.6f);
                }
                return;
            }
            Fire(cur.Stats, false);
            cur.Mag--;
            _cooldown = cur.Stats.Interval;
            Changed();
            if (cur.Mag <= 0 && cur.CanReload) StartReload();
        }

        // ------------------------------------------------------------------ online

        private WeaponId _shown = WeaponId.Pistol;

        /// <summary>The gun in this fighter's hands (a puppet's comes from the network).</summary>
        public WeaponId ShownId => _shown;

        /// <summary>Puppet: shows the gun the owner is holding.</summary>
        public void NetShow(WeaponId id)
        {
            if (id == _shown || id > WeaponId.Knife) return;
            _shown = id;
            _f.Visual.SetWeapon(_ctx.Config.Get(id));
        }

        /// <summary>Puppet: plays another player's shot (looks and sounds only; their device reports the hits).</summary>
        public void ReplayFire(WeaponId id, Vector2 dir)
        {
            if (id > WeaponId.Tesla) return;
            NetShow(id);
            _f.NetAim(dir);
            Fire(_ctx.Config.Get(id), true);
        }

        public void ReplayMelee(Vector2 dir)
        {
            _f.NetAim(dir);
            DoMelee(true);
        }

        public void ReplayGrenade(Vector2 pos, Vector2 vel)
        {
            _f.CancelSpawnProtection();
            _ctx.Projectiles.SpawnGrenade(_f, pos, vel);
            _ctx.Sfx.Play(SfxId.Throw, _f.Position, 0.8f);
        }

        private void Fire(WeaponStats s, bool replay)
        {
            Vector2 dir = _f.AimDir;
            Vector2 muzzle = MuzzlePoint(s);
            _f.CancelSpawnProtection();
            _f.Visual.Recoil(s.Recoil);
            if (_f.IsLocal)
            {
                _ctx.CameraRig.Kick(-dir * s.CameraKick);
                _ctx.CameraRig.AddShake(s.Shake);
                if (s.Shake >= 0.15f) _ctx.Haptics.Pulse(18);
            }
            bool visible = _f.IsLocal || _f.Visual.isActiveAndEnabled;
            if (visible)
            {
                _ctx.Fx.MuzzleFlash(muzzle, dir, s);
                if (s.EjectShell)
                {
                    bool shotgun = s.Id == WeaponId.Shotgun;
                    _ctx.Fx.Shell(_f.Position + dir * 0.2f, dir, shotgun);
                    // the casing lands a moment later (brass tinkle; shotgun hulls are a dull plastic tap)
                    _ctx.Sfx.PlayLater(shotgun ? SfxId.ShellPlastic : SfxId.Shell, _f.Position, Random.Range(0.28f, 0.42f), _f.IsLocal ? 1f : 0.6f);
                }
            }
            _ctx.Sfx.Play(SfxSystem.ShotFor(s), muzzle, 1f);
            // pump-action and bolt-action guns cycle after each shot
            if (s.Id == WeaponId.Shotgun) _ctx.Sfx.PlayLater(SfxId.Pump, _f.Position, 0.3f, 0.9f);
            else if (s.Id == WeaponId.Sniper) _ctx.Sfx.PlayLater(SfxId.Bolt, _f.Position, 0.42f, 0.9f);

            switch (s.Kind)
            {
                case FireKind.Hitscan: Combat.Hitscan(_ctx, _f, muzzle, dir, s); break;
                case FireKind.Projectile: _ctx.Projectiles.SpawnShot(_f, s, muzzle, dir); break;
                case FireKind.Flame: Combat.FlameTick(_ctx, _f, muzzle, dir, s); break;
                case FireKind.Chain: Combat.Tesla(_ctx, _f, muzzle, dir, s); break;
            }
            if (!replay) _ctx.Match?.Net?.OnShot(_f, ShotKind.Fire, s, muzzle, dir);
        }

        /// <summary>Gun tip, pulled back if the barrel is inside a wall (then the shot hits that wall).</summary>
        public Vector2 MuzzlePoint(WeaponStats s)
        {
            Vector2 c = _f.Position;
            Vector2 dir = _f.AimDir;
            float reach = _f.Visual.GunBase + s.GunLength;
            var hit = Physics2D.Raycast(c, dir, reach, Layers.SolidMask);
            return hit.collider != null ? hit.point - dir * 0.03f : c + dir * reach;
        }

        private void StartReload()
        {
            var cur = Current;
            if (_reload > 0f || !cur.CanReload) return;
            _reloadTotal = _reload = cur.Stats.ReloadTime;
            _ctx.Sfx.Play(SfxId.Reload, _f.Position, 0.7f);
            Changed();
        }

        private void FinishReload()
        {
            var cur = Current;
            int need = cur.Stats.MagSize - cur.Mag;
            if (cur.Stats.InfiniteReserve) cur.Mag = cur.Stats.MagSize;
            else
            {
                int take = Mathf.Min(need, cur.Reserve);
                cur.Mag += take;
                cur.Reserve -= take;
            }
            _reload = 0f;
            Changed();
        }

        private void Swap()
        {
            if (_primary == null) return;
            _usePrimary = !_usePrimary;
            _reload = 0f;
            _cooldown = Mathf.Max(_cooldown, 0.2f);
            _ctx.Sfx.Play(SfxId.Click, _f.Position, 0.6f);
            Changed();
        }

        private void DoMelee(bool replay = false)
        {
            if (_meleeCd > 0f && !replay) return;
            var knife = _ctx.Config.Get(WeaponId.Knife);
            _meleeCd = knife.Interval;
            _f.CancelSpawnProtection();
            _f.Visual.Swing();
            _ctx.Sfx.Play(SfxId.Swing, _f.Position, 0.8f);
            Combat.Melee(_ctx, _f, _f.AimDir, knife);
            if (!replay) _ctx.Match?.Net?.OnShot(_f, ShotKind.Melee, knife, _f.Position, _f.AimDir);
        }

        private void ThrowGrenade(float distance)
        {
            if (_grenadeCd > 0f || _f.Grenades <= 0) return;
            var g = _ctx.Config.Grenade;
            _f.Grenades--;
            _grenadeCd = 0.6f;
            _f.CancelSpawnProtection();
            float d = Mathf.Clamp(distance, 2f, g.MaxThrowDistance);
            Vector2 dir = _f.AimDir;
            Vector2 pos = _f.Position + dir * 0.45f;
            Vector2 vel = dir * Mathf.Min(d * 1.4f, g.ThrowSpeed * 1.4f);
            _ctx.Projectiles.SpawnGrenade(_f, pos, vel);
            _ctx.Sfx.Play(SfxId.Throw, _f.Position, 0.8f);
            _ctx.Match?.Net?.OnShot(_f, ShotKind.Grenade, _ctx.Config.Get(WeaponId.Grenade), pos, vel);
            Changed();
        }

        private void Changed()
        {
            Version++;
            if (_f != null && Current != null)
            {
                _shown = Current.Stats.Id;
                _f.Visual.SetWeapon(Current.Stats);
            }
        }
    }
}
