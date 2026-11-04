using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace NeonBrawl
{
    /// <summary>A power-up orb lying on the map.</summary>
    public sealed class PowerOrb
    {
        public int Id;
        public PowerId Type;
        public Vector2 Pos;
        public float Life, Phase, RequestT, Born;
        public GameObject Go;
        public Transform Badge, Beam;
        public SpriteRenderer Glow, Ring, BadgeR, BeamR, Shadow;
        public Light2D Light;
    }

    /// <summary>
    /// Power-ups in a match. Glowing orbs drop from the sky at random open spots; walk over one to put it
    /// in your POWER slot (one at a time), tap POWER to use it. Host-authoritative like the pickups:
    /// offline and on the host this decides spawns, who takes what and who gets frozen; online clients
    /// ask the host and mirror what it says. Bots grab orbs too and use them when it makes sense.
    /// </summary>
    public sealed class PowerSystem
    {
        private const float FirstSpawn = 5f, SpawnEvery = 8f, OrbLife = 30f, TakeRadius = 0.9f;

        private readonly GameContext _ctx;
        private readonly MatchController _m;
        private readonly Transform _root;
        private readonly List<PowerOrb> _orbs = new List<PowerOrb>(8);
        private readonly List<Fighter> _frozen = new List<Fighter>(12);
        private readonly PowerId[] _choices = new PowerId[4];
        private int _nextId = 1;
        private float _spawnT = FirstSpawn, _botT;
        /// <summary>Dev flag -nbpowers: orbs every 3 s, your player uses what it picks up, and every event is logged.</summary>
        private readonly bool _test = CommandLine.Has("-nbpowers");

        private void Log(string s)
        {
            if (_test) Debug.Log("[Powers] " + s);
        }

        /// <summary>False on online clients: the host decides.</summary>
        public bool Authority = true;

        /// <summary>Host: an orb appeared.</summary>
        public event Action<PowerOrb> Spawned;
        /// <summary>Host: an orb was taken by a fighter (null = it faded away).</summary>
        public event Action<PowerOrb, Fighter> Taken;
        /// <summary>A fighter this device controls used a power (online: tell the others).</summary>
        public event Action<Fighter, PowerId, int> Used;
        /// <summary>Host: FROST NOVA froze these fighters for this long.</summary>
        public event Action<List<Fighter>, float> Froze;
        /// <summary>The local player used POWER PICK: show these choices, call back with the pick.</summary>
        public event Action<PowerId[], int, int, Action<PowerId>> ChoiceNeeded;

        public PowerSystem(GameContext ctx, MatchController m, Transform root)
        {
            _ctx = ctx;
            _m = m;
            _root = root;
        }

        public IReadOnlyList<PowerOrb> Orbs => _orbs;

        public PowerOrb Nearest(Vector2 from, float maxDist)
        {
            PowerOrb best = null;
            float bestD = maxDist * maxDist;
            for (int i = 0; i < _orbs.Count; i++)
            {
                float d = (_orbs[i].Pos - from).sqrMagnitude;
                if (d < bestD) { bestD = d; best = _orbs[i]; }
            }
            return best;
        }

        private int MaxOrbs => Mathf.Clamp(2 + _m.Fighters.Count / 4, 2, 5);

        // ================================================================== frame

        public void Tick(List<Fighter> fighters, float dt)
        {
            Animate(dt);
            if (Authority)
            {
                _spawnT -= dt;
                if (_spawnT <= 0f)
                {
                    _spawnT = _test ? 3f : SpawnEvery + Random.Range(-2f, 2f);
                    if (_orbs.Count < MaxOrbs) SpawnRandom();
                }
                for (int i = _orbs.Count - 1; i >= 0; i--)
                {
                    var o = _orbs[i];
                    o.Life -= dt;
                    if (o.Life <= 0f) { Taken?.Invoke(o, null); Remove(o, null); }
                }
                for (int f = 0; f < fighters.Count; f++)
                {
                    var fighter = fighters[f];
                    if (!fighter.Alive || fighter.IsPuppet || fighter.Powers.HasHeld) continue;
                    var o = InReach(fighter, fighter.Position);
                    if (o != null) Grant(fighter, o);
                }
                _botT -= dt;
                if (_botT <= 0f)
                {
                    _botT = 0.35f;
                    for (int f = 0; f < fighters.Count; f++) if (fighters[f].IsBot && !fighters[f].IsPuppet) BotThink(fighters[f]);
                }
            }
            else AskHost();
            // test runs: the (bot-driven) local player uses whatever it holds after a second
            var me = _m.Local;
            if (_test && me != null && me.Alive && me.Powers.HasHeld && Time.time - me.Powers.HeldSince > 1f) UseHeld(me);
        }

        private PowerOrb InReach(Fighter f, Vector2 pos)
        {
            float r = Mathf.Max(TakeRadius, f.Powers.MagnetRadius);
            float r2 = r * r;
            for (int i = 0; i < _orbs.Count; i++)
                if (Time.time - _orbs[i].Born > 0.45f && (_orbs[i].Pos - pos).sqrMagnitude < r2) return _orbs[i];
            return null;
        }

        /// <summary>Client: ask the host for the orb under the local player.</summary>
        private void AskHost()
        {
            var me = _m.Local;
            if (me == null || !me.Alive || me.Powers.HasHeld || _m.Net == null) return;
            var o = InReach(me, me.Position);
            if (o == null || Time.unscaledTime < o.RequestT) return;
            o.RequestT = Time.unscaledTime + 0.4f;
            _m.Net.RequestOrb(o.Id);
        }

        /// <summary>Host: a remote player asked for an orb.</summary>
        public void HostGrant(Fighter f, int id)
        {
            var o = Find(id);
            if (o == null || f == null || !f.Alive || f.Powers.HasHeld) return;
            float r = Mathf.Max(TakeRadius, f.Powers.MagnetRadius) + 1.4f;
            if ((o.Pos - f.NetLatestPos).sqrMagnitude > r * r) return;
            Grant(f, o);
        }

        private void Grant(Fighter f, PowerOrb o)
        {
            Log(f.DisplayName + " took orb " + o.Id + " " + o.Type);
            f.Powers.Give(o.Type, LevelFor(f, o.Type));
            Taken?.Invoke(o, f);
            Remove(o, f);
        }

        /// <summary>Your levels from the upgrade screen; bots get stronger with difficulty; others say theirs.</summary>
        private int LevelFor(Fighter f, PowerId id)
        {
            if (f.IsLocal && !f.IsBot) return _ctx.Profile.PowerLevel(id);
            if (f.IsBot) return Mathf.Clamp(1 + _m.Settings.Difficulty, 1, 3);
            return 1;
        }

        // ================================================================== using

        /// <summary>The local player pressed POWER.</summary>
        public void UseHeld(Fighter f)
        {
            if (f == null || !f.Alive || f.Powers.Frozen || !f.Powers.TakeHeld(out var id, out int level)) return;
            if (id == PowerId.Pick)
            {
                OfferChoices(f, level);
                return;
            }
            Use(f, id, level);
        }

        private void OfferChoices(Fighter f, int level)
        {
            int n = Powers.PickChoices(level);
            // n different powers, never POWER PICK itself
            int filled = 0;
            int guard = 0;
            while (filled < n && guard++ < 50)
            {
                var id = (PowerId)Random.Range(0, Powers.Count);
                if (id == PowerId.Pick) continue;
                bool dup = false;
                for (int k = 0; k < filled; k++) if (_choices[k] == id) dup = true;
                if (!dup) _choices[filled++] = id;
            }
            _ctx.Sfx.PlayUi(SfxId.UiOpen, 0.8f);
            if (f.IsLocal && !f.IsBot && ChoiceNeeded != null)
            {
                int bonus = Powers.PickBonus(level);
                ChoiceNeeded(_choices, filled, level, pick =>
                {
                    if (f != null && f.Alive) Use(f, pick, Mathf.Min(Powers.MaxLevel, _ctx.Profile.PowerLevel(pick) + bonus));
                });
                return;
            }
            var chosen = _choices[Random.Range(0, filled)];
            Use(f, chosen, f.IsBot ? level : _ctx.Profile.PowerLevel(chosen));
        }

        /// <summary>A fighter this device controls uses a power: start it here, then tell the others.</summary>
        public void Use(Fighter f, PowerId id, int level)
        {
            if (f == null || !f.Alive) return;
            Log(f.DisplayName + " used " + id + " lv" + level + " (here)");
            f.Powers.Activate(id, level);
            if (Authority) ApplyAuthority(f, id, level);
            Used?.Invoke(f, id, level);
            if (f.IsLocal && !f.IsBot)
            {
                var d = Powers.Get(id);
                _ctx.Events.Announce(d.Name + "!", d.Color, false);
            }
        }

        /// <summary>A power used on another device (online).</summary>
        public void NetUse(Fighter f, PowerId id, int level)
        {
            if (f == null || id >= PowerId.Count) return;
            if (Authority)
            {
                // the host keeps the slot of remote players: using anything empties it
                f.Powers.TakeHeld(out _, out _);
                level = Mathf.Clamp(level, 1, Powers.MaxLevel);
            }
            Log(f.DisplayName + " used " + id + " lv" + level + " (from the network)");
            if (!f.Alive) return;
            f.Powers.Activate(id, level);
            if (Authority) ApplyAuthority(f, id, level);
        }

        /// <summary>The parts only the match owner decides: who freezes, the instant heal.</summary>
        private void ApplyAuthority(Fighter f, PowerId id, int level)
        {
            var def = Powers.Get(id);
            switch (id)
            {
                case PowerId.Surge:
                    f.Heal(f.MaxHealth);
                    break;
                case PowerId.Frost:
                {
                    float r = def.ValueAt(level), r2 = r * r;
                    float secs = def.DurationAt(level);
                    _frozen.Clear();
                    var all = _m.Fighters;
                    for (int i = 0; i < all.Count; i++)
                    {
                        var v = all[i];
                        if (v == f || !v.Alive || !_m.AreEnemies(f, v) || v.Powers.Shielded) continue;
                        Vector2 vp = v.IsPuppet ? v.NetLatestPos : v.Position;
                        if ((vp - f.Position).sqrMagnitude > r2) continue;
                        v.Powers.Freeze(secs);
                        _frozen.Add(v);
                    }
                    if (_frozen.Count > 0) Froze?.Invoke(_frozen, secs);
                    Log("frost froze " + _frozen.Count + " for " + secs.ToString("0.0") + " s");
                    if (f.IsLocal && !f.IsBot && _frozen.Count > 0) _ctx.Events.RaiseToast(_frozen.Count == 1 ? "1 ENEMY FROZEN" : _frozen.Count + " ENEMIES FROZEN");
                    break;
                }
            }
        }

        // ================================================================== bots

        private void BotThink(Fighter b)
        {
            var p = b.Powers;
            if (!b.Alive || !p.HasHeld || p.Frozen || _m.CurrentPhase != MatchController.Phase.Playing) return;
            float held = Time.time - p.HeldSince;
            if (held < 0.8f) return;
            bool use;
            Fighter near = NearestEnemy(b, out float dist);
            switch (p.Held)
            {
                case PowerId.Frost: use = near != null && dist < Powers.Get(PowerId.Frost).ValueAt(p.HeldLevel) * 0.85f; break;
                case PowerId.Shield:
                case PowerId.Surge: use = b.Health < 55f || (Time.time - b.LastDamageTime < 0.8f && b.Health < 85f); break;
                case PowerId.Rage:
                case PowerId.Ammo:
                case PowerId.Eagle: use = near != null && dist < 10f; break;
                default: use = held > 2.5f; break;
            }
            if (use || held > 14f) UseHeld(b);
        }

        private Fighter NearestEnemy(Fighter self, out float dist)
        {
            Fighter best = null;
            float bestD = float.MaxValue;
            var all = _m.Fighters;
            for (int i = 0; i < all.Count; i++)
            {
                var f = all[i];
                if (f == self || !f.Alive || !_m.AreEnemies(self, f)) continue;
                float d = (f.Position - self.Position).sqrMagnitude;
                if (d < bestD) { bestD = d; best = f; }
            }
            dist = Mathf.Sqrt(bestD);
            return best;
        }

        // ================================================================== orbs

        private void SpawnRandom()
        {
            var map = _m.Map;
            for (int tries = 0; tries < 40; tries++)
            {
                int x = Random.Range(1, map.Width - 1), y = Random.Range(1, map.Height - 1);
                if (!map.Grid.IsWalkable(x, y) || !map.Grid.IsWalkable(x + 1, y) || !map.Grid.IsWalkable(x - 1, y) ||
                    !map.Grid.IsWalkable(x, y + 1) || !map.Grid.IsWalkable(x, y - 1)) continue;
                Vector2 pos = MapData.CellCenter(x, y);
                if (TooClose(pos)) continue;
                // POWER PICK is the rare one
                var type = (PowerId)Random.Range(0, Powers.Count);
                if (type == PowerId.Pick && Random.value < 0.5f) type = (PowerId)Random.Range(0, Powers.Count);
                var o = Create(_nextId++, type, pos);
                Log("orb " + o.Id + " " + type + " at " + pos);
                Spawned?.Invoke(o);
                return;
            }
        }

        private bool TooClose(Vector2 pos)
        {
            var all = _m.Fighters;
            for (int i = 0; i < all.Count; i++) if (all[i].Alive && (all[i].Position - pos).sqrMagnitude < 9f) return true;
            for (int i = 0; i < _orbs.Count; i++) if ((_orbs[i].Pos - pos).sqrMagnitude < 36f) return true;
            var items = _m.Pickups.Items;
            for (int i = 0; i < items.Count; i++) if ((items[i].Position - pos).sqrMagnitude < 2.25f) return true;
            return false;
        }

        /// <summary>Host/offline: drops an orb at a spot (tests and scripted moments).</summary>
        public PowerOrb SpawnAt(PowerId type, Vector2 pos)
        {
            if (!Authority) return null;
            var o = Create(_nextId++, type, pos);
            Spawned?.Invoke(o);
            return o;
        }

        public PowerOrb Find(int id)
        {
            for (int i = 0; i < _orbs.Count; i++) if (_orbs[i].Id == id) return _orbs[i];
            return null;
        }

        /// <summary>Client: the host made an orb.</summary>
        public void NetSpawn(int id, PowerId type, Vector2 pos, float life)
        {
            if (Find(id) != null || type >= PowerId.Count) return;
            Log("orb " + id + " " + type + " appeared (host)");
            var o = Create(id, type, pos);
            o.Life = life;
        }

        /// <summary>Client: an orb was taken (or faded when <paramref name="f"/> is null).</summary>
        public void NetTaken(int id, Fighter f, PowerId type)
        {
            var o = Find(id);
            Log("orb " + id + (f != null ? " taken by " + f.DisplayName : " faded"));
            if (f != null && f == _m.Local && !f.Powers.HasHeld) f.Powers.Give(type, _ctx.Profile.PowerLevel(type));
            if (o != null) Remove(o, f);
        }

        private PowerOrb Create(int id, PowerId type, Vector2 pos)
        {
            var art = _ctx.Art;
            var def = Powers.Get(type);
            Color c = def.Color;
            var go = new GameObject("Power_" + def.Short);
            go.transform.SetParent(_root, false);
            go.transform.position = pos;
            var o = new PowerOrb { Id = id, Type = type, Pos = pos, Go = go, Life = OrbLife, Phase = Random.value * 6f, Born = Time.time };
            o.Shadow = SR(go.transform, art.Glow, art.Lit, new Color(0f, 0f, 0f, 0.35f), new Vector2(0f, -0.05f), new Vector2(0.9f, 0.45f), SortOrder.Pickups);
            o.Glow = SR(go.transform, art.Glow, art.Additive, new Color(c.r, c.g, c.b, 0.55f), Vector2.zero, new Vector2(1.8f, 1.8f), SortOrder.Pickups + 1);
            o.Ring = SR(go.transform, art.ThinRing, art.Additive, new Color(c.r, c.g, c.b, 0.8f), Vector2.zero, new Vector2(1.1f, 1.1f), SortOrder.Pickups + 2);
            o.Beam = new GameObject("Beam").transform;
            o.Beam.SetParent(go.transform, false);
            o.BeamR = SR(o.Beam, art.Glow, art.Additive, new Color(c.r, c.g, c.b, 0.0f), new Vector2(0f, 1.6f), new Vector2(0.55f, 4.2f), SortOrder.Fx - 1);
            o.Badge = new GameObject("Badge").transform;
            o.Badge.SetParent(go.transform, false);
            o.BadgeR = SR(o.Badge, PowerArt.Badge(type), art.Unlit, Color.white, Vector2.zero, new Vector2(0.78f, 0.78f), SortOrder.Pickups + 4);
            var lightGo = new GameObject("Light");
            lightGo.transform.SetParent(go.transform, false);
            o.Light = lightGo.AddComponent<Light2D>();
            o.Light.lightType = Light2D.LightType.Point;
            o.Light.color = c;
            o.Light.intensity = 0.9f;
            o.Light.pointLightOuterRadius = 1.8f;
            o.Light.pointLightInnerRadius = 0.2f;
            _orbs.Add(o);

            // arrival: a beam of light hits the ground with a ring and sparkles
            var fx = _ctx.Fx;
            Color clear = new Color(c.r, c.g, c.b, 0f);
            fx.Spawn(art.Glow, art.Additive, pos + new Vector2(0f, 2.5f), 0f, Vector2.zero, 0.5f, 1f, 1f, new Color(1f, 1f, 1f, 0.9f), clear, SortOrder.Fx, 0f, 0f, new Vector2(0.5f, 7f));
            fx.Spawn(art.Ring, art.Additive, pos, 0f, Vector2.zero, 0.5f, 0.3f, 2.6f, c, clear, SortOrder.Fx);
            for (int k = 0; k < 8; k++)
            {
                Vector2 d = Random.insideUnitCircle.normalized;
                fx.Spawn(PowerArt.Get(PowerArt.Fx.Twinkle), art.Additive, pos + d * 0.3f, 0f, d * Random.Range(1.5f, 3f), 0.55f, 0.35f, 0.05f, Color.white, clear, SortOrder.Fx, 3f);
            }
            fx.LightFlash(pos, c, 3f, 1.6f, 0.4f);
            _ctx.Sfx.Play(SfxId.PowerSpawn, pos, 0.8f);
            return o;
        }

        private void Remove(PowerOrb o, Fighter by)
        {
            _orbs.Remove(o);
            var def = Powers.Get(o.Type);
            Color c = def.Color;
            var art = _ctx.Art;
            var fx = _ctx.Fx;
            Color clear = new Color(c.r, c.g, c.b, 0f);
            if (by != null)
            {
                // the badge flies into whoever took it
                Vector2 to = by.Position;
                for (int k = 0; k < 6; k++)
                {
                    Vector2 from = o.Pos + Random.insideUnitCircle * 0.3f;
                    fx.Spawn(art.Glow, art.Additive, from, 0f, (to - from) * 4f, 0.25f, 0.45f, 0.1f, c, clear, SortOrder.FxTop, 2f);
                }
                fx.Spawn(art.Ring, art.Additive, o.Pos, 0f, Vector2.zero, 0.35f, 0.4f, 1.8f, Color.white, clear, SortOrder.FxTop);
                fx.Spawn(PowerArt.Badge(o.Type), art.Unlit, o.Pos, 0f, (to - o.Pos) * 3f + Vector2.up, 0.35f, 0.8f, 0.2f, Color.white, new Color(1f, 1f, 1f, 0f), SortOrder.FxTop);
                bool local = by == _m.Local;
                _ctx.Sfx.Play(SfxId.PowerPick, o.Pos, local ? 1f : 0.5f);
                if (local && !by.IsBot)
                {
                    _ctx.Events.RaiseToast("+ " + def.Name + "  ·  TAP POWER");
                    _ctx.Haptics.Pulse(25);
                }
            }
            else fx.SpawnPoof(o.Pos, c);
            Object.Destroy(o.Go);
        }

        private void Animate(float dt)
        {
            for (int i = 0; i < _orbs.Count; i++)
            {
                var o = _orbs[i];
                o.Phase += dt;
                float age = Time.time - o.Born;
                float drop = Mathf.Clamp01(age / 0.35f);
                float bob = Mathf.Sin(o.Phase * 3f) * 0.09f;
                // falls in from above, then floats
                o.Badge.localPosition = new Vector3(0f, 0.32f + bob + (1f - drop) * (1f - drop) * 3f, 0f);
                float pop = drop < 1f ? Mathf.Lerp(0.4f, 1f, drop) : 1f + Mathf.Sin(o.Phase * 5f) * 0.04f;
                o.Badge.localScale = new Vector3(pop, pop, 1f);
                float pulse = 1f + Mathf.Sin(o.Phase * 4f) * 0.1f;
                o.Glow.transform.localScale = new Vector3(1.8f * pulse, 1.8f * pulse, 1f);
                o.Ring.transform.Rotate(0f, 0f, 90f * dt);
                float rs = 1.05f + Mathf.Sin(o.Phase * 2.5f) * 0.08f;
                o.Ring.transform.localScale = new Vector3(rs, rs, 1f);
                Color c = Powers.Get(o.Type).Color;
                o.BeamR.color = new Color(c.r, c.g, c.b, 0.14f + Mathf.Sin(o.Phase * 2f) * 0.05f);
                o.Light.intensity = 0.8f + Mathf.Sin(o.Phase * 4f) * 0.2f;
                // the last seconds: blink before fading away
                bool show = o.Life > 4f || Mathf.Sin(o.Life * 18f) > -0.2f;
                if (o.BadgeR.enabled != show) o.BadgeR.enabled = show;
                // a twinkle now and then
                if (Random.value < dt * 2.5f)
                    _ctx.Fx.Spawn(PowerArt.Get(PowerArt.Fx.Twinkle), _ctx.Art.Additive, o.Pos + Random.insideUnitCircle * 0.55f + new Vector2(0f, 0.3f), 0f,
                        Vector2.up * 0.3f, 0.6f, 0.05f, 0.28f, Color.white, new Color(c.r, c.g, c.b, 0f), SortOrder.Fx);
            }
        }

        /// <summary>Late join: every orb on the map right now.</summary>
        public void ForEachOrb(Action<PowerOrb> a)
        {
            for (int i = 0; i < _orbs.Count; i++) a(_orbs[i]);
        }

        private static SpriteRenderer SR(Transform parent, Sprite s, Material m, Color c, Vector2 pos, Vector2 scale, int order)
        {
            var go = new GameObject("Sprite");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = new Vector3(scale.x, scale.y, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = s;
            sr.sharedMaterial = m;
            sr.color = c;
            sr.sortingOrder = order;
            return sr;
        }
    }
}
