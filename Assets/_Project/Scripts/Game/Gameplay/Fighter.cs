using NeonBrawl.Core;
using UnityEngine;

namespace NeonBrawl
{
    /// <summary>
    /// A player or bot in the arena. Owns health/armor/alive state; damage is applied only through
    /// <see cref="MatchController.ReportHit"/> (the match authority), so online play can reuse it.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class Fighter : MonoBehaviour
    {
        public int Id { get; private set; }
        public string DisplayName { get; private set; }
        public int Team { get; private set; }
        public bool IsBot { get; private set; }
        public bool IsLocal { get; private set; }
        public Color OutfitColor { get; private set; }

        public float Health { get; private set; }
        public float Armor { get; private set; }
        public bool Alive { get; private set; }
        public int Grenades { get; set; }
        public float SpawnProtection { get; private set; }
        public Vector2 AimDir { get; private set; } = Vector2.right;
        public float LastDamageTime { get; private set; } = -99f;
        public Fighter LastAttacker { get; private set; }
        public float DeathTime { get; private set; }

        /// <summary>Freezes control (countdown, match end).</summary>
        public bool Frozen { get; set; }
        /// <summary>Weapon pickup the fighter stands on but cannot auto-take (shows the PICK UP button).</summary>
        public Pickup NearbyWeaponPickup { get; set; }
        public IFighterBrain Brain { get; set; }

        [System.NonSerialized] public FighterInput Input;

        public Rigidbody2D Body { get; private set; }
        public CircleCollider2D Collider { get; private set; }
        public FighterMotor Motor { get; private set; }
        public FighterVisual Visual { get; private set; }
        public WeaponHolder Weapons { get; private set; }
        public FighterPowers Powers { get; private set; }
        /// <summary>When this fighter last took a step / fired (the minimap shows footprints and gunfire).</summary>
        public float LastStepTime { get; private set; } = -99f;
        public float LastShotTime = -99f;

        public Vector2 Position => Body.position;
        public Vector2 Velocity => IsPuppet ? _netVel : Body.linearVelocity;

        /// <summary>Online: the Netcode client that controls this fighter (bots belong to the host).</summary>
        public ulong Owner { get; private set; }
        /// <summary>Online: driven by another device. Its pose comes from the network and is smoothed here.</summary>
        public bool IsPuppet { get; private set; }
        /// <summary>Last time a network hit changed this fighter's health (snapshots must not undo it).</summary>
        public float NetHitTime { get; private set; } = -99f;
        public Vector2 NetLatestPos => _nsCount > 0 ? _ns[_nsCount - 1].P : Position;
        public float MaxHealth => _ctx.Config.Player.MaxHealth;
        public float Health01 => Health / MaxHealth;
        public float Armor01 => Armor / _ctx.Config.Player.MaxArmor;

        private GameContext _ctx;
        private float _burnTime;
        private float _burnTick;
        private Fighter _burnOwner;
        private float _stepTimer;

        private struct NetSample { public float T; public Vector2 P; public Vector2 A; }
        private readonly NetSample[] _ns = new NetSample[16];
        private int _nsCount;
        private float _nsMinT;
        private Vector2 _netVel;

        public static Fighter Create(GameContext ctx, Transform parent, int id, string name, int team, bool isBot, bool isLocal,
            FighterLook look, Color nameColor, IFighterBrain brain)
        {
            var go = new GameObject(name) { layer = Layers.Fighters };
            go.transform.SetParent(parent, false);
            var rb = go.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.freezeRotation = true;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            rb.linearDamping = 0f;
            rb.mass = 1f;
            var col = go.AddComponent<CircleCollider2D>();
            col.radius = ctx.Config.Player.Radius;

            var f = go.AddComponent<Fighter>();
            f._ctx = ctx;
            f.Body = rb;
            f.Collider = col;
            f.Motor = go.AddComponent<FighterMotor>();
            f.Visual = go.AddComponent<FighterVisual>();
            f.Weapons = go.AddComponent<WeaponHolder>();
            f.Id = id;
            f.DisplayName = name;
            f.Team = team;
            f.IsBot = isBot;
            f.IsLocal = isLocal;
            f.OutfitColor = look.Outfit;
            f.Brain = brain;
            f.Motor.Init(rb, ctx.Config.Player);
            f.Visual.Build(ctx.Art, ctx.Catalog, look, name, nameColor);
            f.Weapons.Init(ctx, f);
            f.Powers = go.AddComponent<FighterPowers>();
            f.Powers.Init(ctx, f);
            f.Alive = false;
            f.Visual.SetDead(true);
            f.Visual.SetVisible(false);
            return f;
        }

        public void Spawn(Vector2 position)
        {
            var t = _ctx.Config.Player;
            Alive = true;
            Health = t.MaxHealth;
            Armor = 0f;
            Grenades = t.StartGrenades;
            SpawnProtection = t.SpawnProtection;
            LastAttacker = null;
            NearbyWeaponPickup = null;
            _burnTime = 0f;
            Powers.ClearAll();
            gameObject.layer = Layers.Fighters;
            Motor.Teleport(position);
            Motor.ResetDash();
            if (IsPuppet) NetSnap(position, Time.unscaledTime);
            Weapons.ResetLoadout();
            Visual.SetDead(false);
            Visual.SnapBars(1f);
            _ctx.Fx.SpawnPoof(position, OutfitColor);
            _ctx.Sfx.Play(SfxId.Respawn, position, 0.5f);
        }

        public void CancelSpawnProtection() => SpawnProtection = 0f;

        // ------------------------------------------------------------------ online

        /// <summary>Online setup: who controls this fighter, and whether this device only mirrors it.</summary>
        public void SetNetwork(ulong owner, bool puppet)
        {
            Owner = owner;
            IsPuppet = puppet;
            if (!puppet) return;
            Brain = null;
            Motor.enabled = false;
            Body.bodyType = RigidbodyType2D.Kinematic;
            Body.interpolation = RigidbodyInterpolation2D.None;
            Body.linearVelocity = Vector2.zero;
        }

        /// <summary>Adds a pose sample (local clock). Samples older than the last teleport are ignored.</summary>
        public void NetPush(float localTime, Vector2 pos, Vector2 aim)
        {
            if (localTime < _nsMinT) return;
            if (_nsCount > 0 && localTime <= _ns[_nsCount - 1].T) return;
            if (_nsCount == _ns.Length)
            {
                System.Array.Copy(_ns, 1, _ns, 0, _ns.Length - 1);
                _nsCount--;
            }
            _ns[_nsCount++] = new NetSample { T = localTime, P = pos, A = aim };
        }

        /// <summary>
        /// Host side of a client's pose: accepts it only if the player could have got there
        /// (dash and knockback included), so a hacked client cannot teleport.
        /// </summary>
        public bool NetPushChecked(float localTime, Vector2 pos, Vector2 aim)
        {
            if (_nsCount > 0)
            {
                var last = _ns[_nsCount - 1];
                float dt = Mathf.Max(0.02f, localTime - last.T);
                float max = 40f * dt + 2f;
                if ((pos - last.P).sqrMagnitude > max * max) return false;
            }
            NetPush(localTime, pos, aim);
            return true;
        }

        /// <summary>Jump straight to a position (spawn) and forget older samples.</summary>
        public void NetSnap(Vector2 pos, float localTime)
        {
            _nsCount = 0;
            _nsMinT = localTime;
            _ns[_nsCount++] = new NetSample { T = localTime, P = pos, A = AimDir };
            _netVel = Vector2.zero;
            transform.position = pos;
            Body.position = pos;
        }

        /// <summary>Host's numbers for health, armor and spawn shield.</summary>
        public void NetSyncStats(float hp, float armor, bool shielded)
        {
            if (Time.unscaledTime - NetHitTime > 0.35f)
            {
                Health = Alive ? Mathf.Max(1f, hp) : 0f;
                Armor = armor;
            }
            if (!shielded) SpawnProtection = 0f;
            else if (SpawnProtection <= 0f) SpawnProtection = 0.25f;
        }

        /// <summary>A hit the host applied: same reactions as <see cref="ApplyDamage"/> without deciding anything.</summary>
        public void NetHit(float hp, float armor, Vector2 dir, float knockback, Vector2 point, float dealt, Fighter attacker)
        {
            Health = hp;
            Armor = armor;
            NetHitTime = Time.unscaledTime;
            LastDamageTime = Time.time;
            if (attacker != null && attacker != this) LastAttacker = attacker;
            if (!IsPuppet && dir.sqrMagnitude > 0.0001f && knockback > 0f) Motor.AddKnockback(dir.normalized * knockback);
            Visual.OnHit();
            if (dir.sqrMagnitude > 0.0001f) _ctx.Fx.Blood(point, dir, dealt);
        }

        /// <summary>Late join: put a fighter where the host says, without spawn effects.</summary>
        public void NetRevive(Vector2 pos, float hp, float armor)
        {
            Alive = true;
            Health = Mathf.Max(1f, hp);
            Armor = armor;
            gameObject.layer = Layers.Fighters;
            Motor.Teleport(pos);
            if (IsPuppet) NetSnap(pos, Time.unscaledTime);
            Visual.SetDead(false);
            Visual.SnapBars(Health01);
        }

        /// <summary>The player left the match.</summary>
        public void NetRemove()
        {
            Alive = false;
            Powers.ClearAll();
            NearbyWeaponPickup = null;
            gameObject.SetActive(false);
        }

        public void NetAim(Vector2 dir)
        {
            if (dir.sqrMagnitude > 0.0001f) AimDir = dir.normalized;
            Visual.SetAim(AimDir);
        }

        private void PuppetTick(float dt)
        {
            if (SpawnProtection > 0f) SpawnProtection -= dt;
            if (_nsCount > 0)
            {
                float rt = Time.unscaledTime - NetMatch.InterpDelay;
                Vector2 pos, aim;
                int last = _nsCount - 1;
                if (rt <= _ns[0].T) { pos = _ns[0].P; aim = _ns[0].A; }
                else if (rt >= _ns[last].T)
                {
                    // coast briefly on the last motion so one late packet does not freeze the player
                    float over = Mathf.Min(rt - _ns[last].T, 0.1f);
                    Vector2 v = last > 0 ? (_ns[last].P - _ns[last - 1].P) / Mathf.Max(0.02f, _ns[last].T - _ns[last - 1].T) : Vector2.zero;
                    pos = _ns[last].P + v * over;
                    aim = _ns[last].A;
                }
                else
                {
                    int i = 0;
                    while (i < last - 1 && _ns[i + 1].T <= rt) i++;
                    float k = (rt - _ns[i].T) / Mathf.Max(0.0001f, _ns[i + 1].T - _ns[i].T);
                    pos = Vector2.Lerp(_ns[i].P, _ns[i + 1].P, k);
                    aim = Vector2.Lerp(_ns[i].A, _ns[i + 1].A, k);
                    if (i > 1)
                    {
                        // forget samples already played
                        System.Array.Copy(_ns, i - 1, _ns, 0, _nsCount - (i - 1));
                        _nsCount -= i - 1;
                    }
                }
                Vector2 prev = transform.position;
                transform.position = pos;
                Body.position = pos;
                if (dt > 0f) _netVel = Vector2.Lerp(_netVel, (pos - prev) / dt, 0.35f);
                if (aim.sqrMagnitude > 0.0001f) AimDir = aim.normalized;
            }
            Visual.SetAim(AimDir);
            float speed01 = Alive ? Mathf.Clamp01(_netVel.magnitude / _ctx.Config.Player.MoveSpeed) : 0f;
            if (Alive) TickFootsteps(dt, speed01);
            Visual.Tick(dt, speed01, Health01, Armor01, Alive && SpawnProtection > 0f);
        }

        /// <summary>Authority only. Applies armor split and hit reactions. Returns damage dealt.</summary>
        public float ApplyDamage(float amount, Vector2 dir, float knockback, Fighter attacker, Vector2 point)
        {
            var split = DamageModel.Apply(amount, Armor, _ctx.Config.Player.ArmorAbsorb);
            Armor = Mathf.Max(0f, Armor - split.Armor);
            Health = Mathf.Max(0f, Health - split.Health);
            LastDamageTime = Time.time;
            if (attacker != null && attacker != this) LastAttacker = attacker;
            if (dir.sqrMagnitude > 0.0001f && knockback > 0f) Motor.AddKnockback(dir.normalized * knockback);
            Visual.OnHit();
            if (dir.sqrMagnitude > 0.0001f) _ctx.Fx.Blood(point, dir, amount);
            return split.Health + split.Armor;
        }

        /// <summary>Authority only.</summary>
        public void Die(Vector2 dir, float force)
        {
            Alive = false;
            Health = 0f;
            DeathTime = Time.time;
            _burnTime = 0f;
            Powers.ClearAll();
            NearbyWeaponPickup = null;
            gameObject.layer = Layers.Corpses;
            Motor.SetMove(Vector2.zero);
            Vector2 d = dir.sqrMagnitude > 0.0001f ? dir.normalized : Random.insideUnitCircle.normalized;
            Motor.AddKnockback(d * (force + 5f));
            Visual.SetDead(true);
            _ctx.Fx.DeathBurst(Position, d, OutfitColor);
            _ctx.Sfx.Play(SfxId.Death, Position, 0.9f);
        }

        /// <summary>LIFE SURGE: this hit cannot finish the fighter.</summary>
        public void KeepAlive()
        {
            if (Alive && Health < 1f) Health = 1f;
        }

        public bool Heal(float amount)
        {
            if (Health >= MaxHealth) return false;
            Health = Mathf.Min(MaxHealth, Health + amount);
            return true;
        }

        public bool AddArmor(float amount)
        {
            float max = _ctx.Config.Player.MaxArmor;
            if (Armor >= max) return false;
            Armor = Mathf.Min(max, Armor + amount);
            return true;
        }

        public void Ignite(Fighter owner, float duration)
        {
            _burnOwner = owner;
            _burnTime = Mathf.Max(_burnTime, duration);
        }

        /// <summary>Hides or shows the fighter for the local player's line of sight.</summary>
        public void SetSeen(bool seen) => Visual.SetVisible(seen);

        private void Update()
        {
            if (_ctx == null) return;
            float dt = Time.deltaTime;
            if (IsPuppet)
            {
                PuppetTick(dt);
                return;
            }
            float speed01 = 0f;
            if (Alive)
            {
                if (SpawnProtection > 0f) SpawnProtection -= dt;

                bool stunned = Frozen || Powers.Frozen;
                if (stunned || dt <= 0f)
                {
                    Input = new FighterInput { Aim = AimDir };
                }
                else
                {
                    Brain?.Think(this, dt, ref Input);
                }
                if (Input.HasAim && Input.Aim.sqrMagnitude > 0.0001f) AimDir = Input.Aim.normalized;
                Visual.SetAim(AimDir);

                if (!stunned && dt > 0f)
                {
                    Weapons.Tick(Input, dt);
                    if (Input.Power && _ctx.Match != null) _ctx.Match.PowerUps.UseHeld(this);
                    if (Input.Dash)
                    {
                        Vector2 d = Input.Move.sqrMagnitude > 0.04f ? Input.Move : AimDir;
                        if (Motor.TryDash(d))
                        {
                            _ctx.Fx.DashTrail(Position, d.normalized, OutfitColor);
                            _ctx.Sfx.Play(SfxId.Dash, Position, 0.7f);
                        }
                    }
                    if (Input.Interact && _ctx.Match != null) _ctx.Match.Pickups.TrySwapWeapon(this);
                }
                Motor.SetMove(stunned ? Vector2.zero : Input.Move * (Weapons.MoveMultiplier * Powers.SpeedMult));

                speed01 = Mathf.Clamp01(Velocity.magnitude / _ctx.Config.Player.MoveSpeed);
                TickBurn(dt);
                TickFootsteps(dt, speed01);
            }
            Visual.Tick(dt, speed01, Health01, Armor01, Alive && SpawnProtection > 0f);
        }

        private void TickBurn(float dt)
        {
            if (_burnTime <= 0f) return;
            _burnTime -= dt;
            _burnTick -= dt;
            if (Time.frameCount % 3 == 0) _ctx.Fx.Ember(Position);
            if (_burnTick <= 0f)
            {
                _burnTick = 0.25f;
                var fl = _ctx.Config.Get(WeaponId.Flamethrower);
                _ctx.Match?.ReportHit(_burnOwner, this, fl.BurnDps * 0.25f, Vector2.zero, 0f, WeaponId.Flamethrower, Position);
            }
        }

        private void TickFootsteps(float dt, float speed01)
        {
            if (speed01 < 0.5f || !Visual.isActiveAndEnabled) return;
            _stepTimer -= dt;
            if (_stepTimer > 0f) return;
            _stepTimer = Motor.IsDashing ? 0.03f : (Powers.SpeedMult > 1f ? 0.19f : 0.26f);
            LastStepTime = Time.time;
            _ctx.Fx.Dust(Position - Velocity.normalized * 0.25f, Motor.IsDashing ? 0.9f : 0.5f);
            if (Motor.IsDashing) return;
            // footsteps: always yours, others only when you can see them (no hearing through walls)
            var step = _ctx.Sfx.StepSound;
            if (IsLocal) _ctx.Sfx.Play(step, Position, 0.8f, 0.08f);
            else if (Visual.Alpha > 0.4f) _ctx.Sfx.Play(step, Position, 0.45f, 0.08f);
        }
    }
}
