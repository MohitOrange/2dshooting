using UnityEngine;

namespace NeonBrawl
{
    /// <summary>
    /// One fighter's power-ups: the power waiting in its slot, the effects running now (each with its own
    /// timer), being frozen by a FROST NOVA, and how all of that looks (aura, shield bubble, ice block,
    /// magnet field, trails and sparkles). Gameplay reads the simple flags (SpeedMult, Ghost, Shielded …).
    /// Perf: no allocations per frame; effect objects are made the first time a power is used.
    /// </summary>
    public sealed class FighterPowers : MonoBehaviour
    {
        private GameContext _ctx;
        private Fighter _f;
        private readonly float[] _until = new float[Powers.Count];
        private readonly float[] _length = new float[Powers.Count];
        private readonly byte[] _level = new byte[Powers.Count];
        private float _frozenUntil, _frozenLength, _revealUntil, _fxT, _flakeT;
        private bool _any;

        private SpriteRenderer _aura, _bubble, _ice, _field, _mark;
        private Transform _arcs;
        private readonly SpriteRenderer[] _arcSr = new SpriteRenderer[3];
        private bool _built;

        /// <summary>A power is waiting in the slot (the local player's POWER button, or a bot's pocket).</summary>
        public bool HasHeld { get; private set; }
        public PowerId Held { get; private set; }
        public int HeldLevel { get; private set; }
        public float HeldSince { get; private set; }
        /// <summary>Changes when the slot or running effects change (the HUD redraws then).</summary>
        public int Version { get; private set; }
        /// <summary>The local player's EAGLE EYE marks this enemy.</summary>
        public bool Marked { get; set; }

        public void Init(GameContext ctx, Fighter f)
        {
            _ctx = ctx;
            _f = f;
        }

        // ------------------------------------------------------------------ queries

        public bool Active(PowerId id) => Time.time < _until[(int)id];
        public int LevelOf(PowerId id) => Mathf.Max(1, _level[(int)id]);
        public float Left(PowerId id) => Mathf.Max(0f, _until[(int)id] - Time.time);
        public float Left01(PowerId id) => _length[(int)id] > 0f ? Mathf.Clamp01(Left(id) / _length[(int)id]) : 0f;

        public float SpeedMult => Active(PowerId.Turbo) ? Powers.TurboSpeed(LevelOf(PowerId.Turbo)) : 1f;
        /// <summary>Cloaked: enemies cannot see this fighter (firing shows it for a moment).</summary>
        public bool Ghost => Active(PowerId.Ghost) && Time.time >= _revealUntil;
        public bool Shielded => Active(PowerId.Shield);
        public bool InfiniteAmmo => Active(PowerId.Ammo);
        public bool Surging => Active(PowerId.Surge);
        public float DamageMult => Active(PowerId.Rage) ? 2f : 1f;
        public bool Eagle => Active(PowerId.Eagle);
        public float MagnetRadius => Active(PowerId.Magnet) ? Powers.Get(PowerId.Magnet).ValueAt(LevelOf(PowerId.Magnet)) : 0f;
        public bool Frozen => Time.time < _frozenUntil;
        public float Frozen01 => _frozenLength > 0f ? Mathf.Clamp01((_frozenUntil - Time.time) / _frozenLength) : 0f;
        public bool AnyActive => _any;

        // ------------------------------------------------------------------ slot

        public void Give(PowerId id, int level)
        {
            HasHeld = true;
            Held = id;
            HeldLevel = Mathf.Clamp(level, 1, Powers.MaxLevel);
            HeldSince = Time.time;
            Version++;
        }

        public bool TakeHeld(out PowerId id, out int level)
        {
            id = Held;
            level = HeldLevel;
            if (!HasHeld) return false;
            HasHeld = false;
            Version++;
            return true;
        }

        // ------------------------------------------------------------------ effects

        /// <summary>Starts a power on this device (look + local rules). The match decides who else hears of it.</summary>
        public void Activate(PowerId id, int level)
        {
            var def = Powers.Get(id);
            level = Mathf.Clamp(level, 1, Powers.MaxLevel);
            int i = (int)id;
            float dur = def.DurationAt(level);
            if (id != PowerId.Pick && id != PowerId.Frost)
            {
                _level[i] = (byte)level;
                _length[i] = dur;
                _until[i] = Time.time + dur;
                _any = true;
            }
            if (id == PowerId.Ammo) _f.Weapons.FillMagazine();
            if (id == PowerId.Ghost) _revealUntil = 0f;
            Version++;
            Build();
            Burst(def.Color, id == PowerId.Frost ? def.ValueAt(level) : 0f);
            float vol = _f.IsLocal ? 1f : 0.7f;
            _ctx.Sfx.Play(id == PowerId.Frost ? SfxId.Freeze : SfxId.PowerUse, _f.Position, vol);
            if (_f.IsLocal) _ctx.Haptics.Pulse(40);
        }

        /// <summary>Caught in a FROST NOVA: no moving or shooting for a moment.</summary>
        public void Freeze(float seconds)
        {
            if (_f == null || !_f.Alive) return;
            if (CommandLine.Has("-nbpowers")) Debug.Log("[Powers] " + _f.DisplayName + " frozen " + seconds.ToString("0.0") + " s");
            _frozenLength = seconds;
            _frozenUntil = Time.time + seconds;
            _any = true;
            Build();
            _ice.enabled = true;
            _ice.transform.localScale = Vector3.one * 0.4f;
            var fx = _ctx.Fx;
            var art = _ctx.Art;
            Color ice = Powers.Get(PowerId.Frost).Color;
            fx.Spawn(art.Ring, art.Additive, _f.Position, 0f, Vector2.zero, 0.35f, 0.3f, 1.6f, ice, new Color(ice.r, ice.g, ice.b, 0f), SortOrder.FxTop);
            for (int k = 0; k < 6; k++)
            {
                Vector2 d = Random.insideUnitCircle.normalized;
                fx.Spawn(PowerArt.Get(PowerArt.Fx.Flake), art.Additive, _f.Position + d * 0.2f, Random.value * 360f, d * Random.Range(1f, 2.4f), 0.6f, 0.28f, 0.1f,
                    Color.white, new Color(ice.r, ice.g, ice.b, 0f), SortOrder.FxTop, 3f, Random.Range(-200f, 200f));
            }
            if (_f.IsLocal)
            {
                _ctx.Events.RaiseToast("FROZEN!");
                _ctx.Haptics.Pulse(60);
            }
            Version++;
        }

        /// <summary>Firing while cloaked shows you for a moment.</summary>
        public void Reveal(float seconds)
        {
            if (Active(PowerId.Ghost)) _revealUntil = Time.time + seconds;
        }

        /// <summary>Death, respawn or match end: everything stops and the slot empties.</summary>
        public void ClearAll()
        {
            for (int i = 0; i < _until.Length; i++) _until[i] = 0f;
            _frozenUntil = 0f;
            HasHeld = false;
            Marked = false;
            _any = false;
            Version++;
            if (_built) HideAll();
        }

        // ------------------------------------------------------------------ look

        private void Build()
        {
            if (_built) return;
            _built = true;
            var art = _ctx.Art;
            _aura = Sprite("Aura", art.Glow, art.Additive, SortOrder.Fighters - 1, 2.4f);
            _field = Sprite("Field", art.ThinRing, art.Additive, SortOrder.Shadows + 1, 1f);
            _mark = Sprite("Mark", art.Ring, art.Additive, SortOrder.Fx + 2, 1.5f);
            _bubble = Sprite("Bubble", PowerArt.Get(PowerArt.Fx.Bubble), art.Additive, SortOrder.Fx + 1, 1.55f);
            _ice = Sprite("Ice", PowerArt.Get(PowerArt.Fx.Ice), art.Lit, SortOrder.Fx + 1, 1.25f);
            _arcs = new GameObject("Arcs").transform;
            _arcs.SetParent(transform, false);
            for (int k = 0; k < 3; k++)
            {
                var go = new GameObject("Arc");
                go.transform.SetParent(_arcs, false);
                go.transform.localRotation = Quaternion.Euler(0f, 0f, k * 120f);
                go.transform.localScale = Vector3.one * 1.3f;
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = PowerArt.Get(PowerArt.Fx.Arc);
                sr.sharedMaterial = art.Additive;
                sr.sortingOrder = SortOrder.Fx;
                _arcSr[k] = sr;
            }
            HideAll();
        }

        private SpriteRenderer Sprite(string name, Sprite s, Material m, int order, float scale)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.localScale = new Vector3(scale, scale, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = s;
            sr.sharedMaterial = m;
            sr.sortingOrder = order;
            return sr;
        }

        private void HideAll()
        {
            _aura.enabled = _field.enabled = _mark.enabled = _bubble.enabled = _ice.enabled = false;
            _arcs.gameObject.SetActive(false);
            _f.Visual.AlphaScale = 1f;
            _f.Visual.SetTint(Color.white);
        }

        /// <summary>The moment a power starts: shock ring, flash, sparks and twinkles in its color.</summary>
        private void Burst(Color c, float novaRadius)
        {
            var fx = _ctx.Fx;
            var art = _ctx.Art;
            Vector2 p = _f.Position;
            bool seen = _f.IsLocal || _f.Visual.Alpha > 0.3f || novaRadius > 0f;
            if (!seen) return;
            Color clear = new Color(c.r, c.g, c.b, 0f);
            if (novaRadius > 0f)
            {
                // FROST NOVA: an ice wave that rolls out to the edge of its reach
                fx.Spawn(art.Ring, art.Additive, p, 0f, Vector2.zero, 0.45f, 0.4f, novaRadius * 2.3f, new Color(c.r, c.g, c.b, 0.95f), clear, SortOrder.FxTop);
                fx.Spawn(art.ThinRing, art.Additive, p, 0f, Vector2.zero, 0.6f, 0.3f, novaRadius * 2.1f, Color.white, clear, SortOrder.FxTop);
                fx.Spawn(art.Glow, art.Additive, p, 0f, Vector2.zero, 0.5f, 2f, novaRadius * 2.4f, new Color(c.r, c.g, c.b, 0.55f), clear, SortOrder.Fx);
                for (int k = 0; k < 22; k++)
                {
                    float a = k / 22f * Mathf.PI * 2f;
                    Vector2 d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                    fx.Spawn(PowerArt.Get(PowerArt.Fx.Flake), art.Additive, p + d * 0.4f, Random.value * 360f, d * novaRadius * Random.Range(1.6f, 2.1f), 0.55f, 0.35f, 0.15f,
                        Color.white, clear, SortOrder.FxTop, 2f, Random.Range(-300f, 300f));
                }
                fx.LightFlash(p, c, novaRadius * 1.4f, 2.4f, 0.35f);
                _ctx.CameraRig.AddShake(0.25f);
                return;
            }
            fx.Spawn(art.Ring, art.Additive, p, 0f, Vector2.zero, 0.45f, 0.3f, 3.2f, new Color(c.r, c.g, c.b, 0.95f), clear, SortOrder.FxTop);
            fx.Spawn(art.ThinRing, art.Additive, p, 0f, Vector2.zero, 0.6f, 0.4f, 4.2f, new Color(1f, 1f, 1f, 0.8f), clear, SortOrder.FxTop);
            fx.Spawn(art.Glow, art.Additive, p, 0f, Vector2.zero, 0.4f, 3.4f, 0.8f, new Color(1f, 1f, 1f, 0.8f), clear, SortOrder.Fx);
            for (int k = 0; k < 14; k++)
            {
                Vector2 d = Random.insideUnitCircle.normalized;
                fx.Spawn(art.Spark, art.Additive, p + d * 0.3f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg, d * Random.Range(4f, 8f), Random.Range(0.25f, 0.4f), 1f, 0.4f,
                    Color.white, clear, SortOrder.FxTop, 5f, 0f, new Vector2(0.35f, 0.6f));
            }
            for (int k = 0; k < 6; k++)
            {
                Vector2 d = Random.insideUnitCircle * 1.1f;
                fx.Spawn(PowerArt.Get(PowerArt.Fx.Twinkle), art.Additive, p + d, 0f, Vector2.up * 0.6f, Random.Range(0.4f, 0.7f), 0.1f, 0.5f, Color.white, clear, SortOrder.FxTop);
            }
            fx.LightFlash(p, c, 4f, 2f, 0.3f);
            if (_f.IsLocal) _ctx.CameraRig.AddShake(0.12f);
        }

        private void LateUpdate()
        {
            if (!_built || _f == null) return;
            if (!_any && !Marked)
            {
                if (_aura.enabled || _ice.enabled || _bubble.enabled || _mark.enabled || _field.enabled) HideAll();
                return;
            }
            float t = Time.time, dt = Time.deltaTime;
            bool alive = _f.Alive;
            bool anyNow = false;
            for (int i = 0; i < _until.Length; i++) if (t < _until[i]) { anyNow = true; break; }
            bool frozen = alive && Frozen;
            if (!anyNow && !frozen) _any = false;
            float seen = _f.IsLocal ? 1f : _f.Visual.Alpha;

            // aura: the strongest running power's color, breathing
            Color aura = default;
            bool hasAura = false;
            for (int i = 0; i < _until.Length && alive; i++)
            {
                if (t >= _until[i] || i == (int)PowerId.Ghost) continue;
                aura = Powers.All[i].Color;
                hasAura = true;
            }
            _aura.enabled = hasAura && seen > 0.05f;
            if (_aura.enabled)
            {
                float pulse = 0.38f + Mathf.Sin(t * 7f) * 0.1f;
                _aura.color = new Color(aura.r, aura.g, aura.b, pulse * seen);
                float s = 2.2f + Mathf.Sin(t * 5f) * 0.15f;
                _aura.transform.localScale = new Vector3(s, s, 1f);
            }

            // IRON SHIELD: a turning bubble that flickers in its last second
            bool shield = alive && Shielded;
            _bubble.enabled = shield && seen > 0.05f;
            if (_bubble.enabled)
            {
                Color c = Powers.Get(PowerId.Shield).Color;
                float left = Left(PowerId.Shield);
                float a = left < 1f && Mathf.Sin(t * 30f) > 0f ? 0.35f : 0.9f;
                _bubble.color = new Color(c.r, c.g, c.b, a * seen);
                _bubble.transform.localRotation = Quaternion.Euler(0f, 0f, t * 40f);
                float s = 1.55f + Mathf.Sin(t * 6f) * 0.05f;
                _bubble.transform.localScale = new Vector3(s, s, 1f);
            }

            // FROST: an ice block that grows in, then cracks away
            if (frozen)
            {
                _ice.enabled = true;
                Color c = Powers.Get(PowerId.Frost).Color;
                float s = Mathf.MoveTowards(_ice.transform.localScale.x, 1.3f, dt * 6f);
                _ice.transform.localScale = new Vector3(s, s, 1f);
                _ice.color = new Color(c.r * 0.8f + 0.2f, c.g * 0.8f + 0.2f, 1f, 0.85f * Mathf.Max(0.3f, seen));
                _flakeT -= dt;
                if (_flakeT <= 0f && seen > 0.2f)
                {
                    _flakeT = 0.12f;
                    _ctx.Fx.Spawn(PowerArt.Get(PowerArt.Fx.Flake), _ctx.Art.Additive, _f.Position + new Vector2(Random.Range(-0.6f, 0.6f), 0.7f), Random.value * 360f,
                        new Vector2(Random.Range(-0.3f, 0.3f), -0.8f), 0.8f, 0.18f, 0.08f, Color.white, new Color(1f, 1f, 1f, 0f), SortOrder.FxTop, 0f, 90f);
                }
            }
            else if (_ice.enabled)
            {
                _ice.enabled = false;
                // shatter
                var art = _ctx.Art;
                Color c = Powers.Get(PowerId.Frost).Color;
                for (int k = 0; k < 8 && seen > 0.2f; k++)
                {
                    Vector2 d = Random.insideUnitCircle.normalized;
                    _ctx.Fx.Spawn(art.Square, art.Additive, _f.Position + d * 0.3f, Random.value * 360f, d * Random.Range(2f, 4f), 0.45f, 0.18f, 0.06f,
                        Color.white, new Color(c.r, c.g, c.b, 0f), SortOrder.FxTop, 4f, Random.Range(-500f, 500f));
                }
                if (seen > 0.2f) _ctx.Sfx.Play(SfxId.HitWall, _f.Position, 0.5f);
            }

            // LOOT MAGNET: the reach drawn on the floor, three arcs spinning around you
            float mr = alive ? MagnetRadius : 0f;
            _field.enabled = mr > 0f && seen > 0.05f;
            _arcs.gameObject.SetActive(_field.enabled);
            if (_field.enabled)
            {
                Color c = Powers.Get(PowerId.Magnet).Color;
                _field.color = new Color(c.r, c.g, c.b, (0.22f + Mathf.Sin(t * 4f) * 0.06f) * seen);
                float d = mr * 2f * (1f + Mathf.Sin(t * 3f) * 0.02f);
                _field.transform.localScale = new Vector3(d, d, 1f);
                _arcs.localRotation = Quaternion.Euler(0f, 0f, -t * 220f);
                var arcColor = new Color(c.r, c.g, c.b, 0.7f * seen);
                for (int k = 0; k < _arcSr.Length; k++) _arcSr[k].color = arcColor;
            }

            // EAGLE EYE target mark (seen by the local player only)
            _mark.enabled = Marked && alive;
            if (_mark.enabled)
            {
                float s = 1.4f + Mathf.Sin(t * 8f) * 0.12f;
                _mark.transform.localScale = new Vector3(s, s, 1f);
                _mark.color = new Color(1f, 0.25f, 0.2f, 0.85f);
            }

            // tint and cloak
            Color tint = Color.white;
            if (frozen) tint = new Color(0.55f, 0.85f, 1f);
            else if (alive && Active(PowerId.Rage)) tint = new Color(1f, 0.62f + Mathf.Sin(t * 10f) * 0.08f, 0.55f);
            _f.Visual.SetTint(tint);
            // you (and your team) see a see-through cloak; enemies are hidden by the line-of-sight code
            _f.Visual.AlphaScale = alive && Active(PowerId.Ghost) ? (Time.time < _revealUntil ? 0.85f : 0.38f) : 1f;

            if (alive) TrailFx(dt, seen);
        }

        /// <summary>Little particles while a power runs: speed streaks, gold sparks, green plusses, embers, wisps.</summary>
        private void TrailFx(float dt, float seen)
        {
            _fxT -= dt;
            if (_fxT > 0f || seen < 0.2f) return;
            _fxT = 0.06f;
            var fx = _ctx.Fx;
            var art = _ctx.Art;
            Vector2 p = _f.Position;
            if (Active(PowerId.Turbo) && _f.Velocity.sqrMagnitude > 4f)
            {
                Color c = Powers.Get(PowerId.Turbo).Color;
                Vector2 v = _f.Velocity.normalized;
                fx.Spawn(art.Circle, art.Additive, p - v * 0.2f, 0f, -v * 1.2f, 0.28f, 0.75f, 0.35f, new Color(c.r, c.g, c.b, 0.35f), new Color(c.r, c.g, c.b, 0f), SortOrder.Fx - 4);
                fx.Spawn(art.Spark, art.Additive, p + Random.insideUnitCircle * 0.4f, Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg, -v * 3f, 0.2f, 1f, 0.6f,
                    Color.white, new Color(c.r, c.g, c.b, 0f), SortOrder.Fx, 0f, 0f, new Vector2(0.8f, 0.25f));
            }
            if (Active(PowerId.Ammo) && Random.value < 0.6f)
            {
                Color c = Powers.Get(PowerId.Ammo).Color;
                Vector2 gun = p + _f.AimDir * 0.55f;
                fx.Spawn(art.Star, art.Additive, gun + Random.insideUnitCircle * 0.15f, Random.value * 360f, Random.insideUnitCircle * 0.8f, 0.35f, 0.22f, 0.05f, Color.white, new Color(c.r, c.g, c.b, 0f), SortOrder.FxTop, 2f, 300f);
            }
            if (Active(PowerId.Surge) && Random.value < 0.5f)
            {
                Color c = Powers.Get(PowerId.Surge).Color;
                fx.Spawn(art.Cross, art.Additive, p + new Vector2(Random.Range(-0.5f, 0.5f), Random.Range(-0.3f, 0.2f)), 0f, Vector2.up * 1.3f, 0.7f, 0.22f, 0.12f, c, new Color(c.r, c.g, c.b, 0f), SortOrder.FxTop);
            }
            if (Active(PowerId.Rage) && Random.value < 0.7f)
            {
                fx.Spawn(art.Glow, art.Additive, p + Random.insideUnitCircle * 0.45f, 0f, Vector2.up * 1.1f + Random.insideUnitCircle * 0.3f, 0.5f, 0.35f, 0.05f,
                    new Color(1f, 0.55f, 0.2f, 0.9f), new Color(1f, 0.1f, 0.05f, 0f), SortOrder.FxTop);
            }
            if (Active(PowerId.Ghost) && Random.value < 0.5f)
            {
                Color c = Powers.Get(PowerId.Ghost).Color;
                fx.Spawn(art.Smoke, art.Additive, p + Random.insideUnitCircle * 0.4f, Random.value * 360f, Random.insideUnitCircle * 0.4f, 0.6f, 0.3f, 0.7f,
                    new Color(c.r, c.g, c.b, 0.25f * seen), new Color(c.r, c.g, c.b, 0f), SortOrder.Fx, 1f, 60f);
            }
            if (Active(PowerId.Eagle) && _f.IsLocal && Random.value < 0.3f)
            {
                Color c = Powers.Get(PowerId.Eagle).Color;
                fx.Spawn(PowerArt.Get(PowerArt.Fx.Twinkle), art.Additive, p + Random.insideUnitCircle * 0.7f, 0f, Vector2.up * 0.4f, 0.5f, 0.1f, 0.35f, c, new Color(c.r, c.g, c.b, 0f), SortOrder.FxTop);
            }
        }

        private void Update()
        {
            // LIFE SURGE heals on the device that owns health (offline, or the online host)
            if (_f == null || !_f.Alive || !Surging) return;
            var m = _ctx.Match;
            if (m == null || !m.IsAuthority) return;
            _f.Heal(Powers.Get(PowerId.Surge).ValueAt(LevelOf(PowerId.Surge)) * Time.deltaTime);
        }
    }
}
