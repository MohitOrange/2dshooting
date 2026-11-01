using System.Collections.Generic;
using UnityEngine;

namespace NeonBrawl
{
    /// <summary>
    /// Rockets, bouncing plasma bolts and grenades, simulated by one manager (no per-object Update).
    /// Grenades fake height with a z value: they arc, bounce on the floor, ricochet off walls and roll.
    /// Perf: pooled; 0 B GC after warm-up; one cast per projectile per frame.
    /// </summary>
    public sealed class ProjectileSystem : MonoBehaviour
    {
        private enum Kind { Rocket, Plasma, Grenade }

        private sealed class Shot
        {
            public Kind Kind;
            public Vector2 Pos, Vel;
            public float Z, Vz, Life, Fuse, TrailT, BaseScale;
            public int Bounces;
            public Fighter Owner;
            public WeaponStats Stats;
            public GameObject Go;
            public Transform T;
            public SpriteRenderer Body, Glow, Shadow;
        }

        private static readonly RaycastHit2D[] Hits = new RaycastHit2D[8];
        private readonly List<Shot> _pool = new List<Shot>(32);
        private readonly List<Shot> _active = new List<Shot>(32);
        private GameContext _ctx;

        public void Init(GameContext ctx) => _ctx = ctx;

        public void SpawnShot(Fighter owner, WeaponStats s, Vector2 pos, Vector2 dir)
        {
            var p = Get();
            p.Kind = s.Id == WeaponId.Plasma ? Kind.Plasma : Kind.Rocket;
            p.Owner = owner;
            p.Stats = s;
            p.Pos = pos;
            p.Vel = dir * s.ProjectileSpeed;
            p.Life = s.Range / Mathf.Max(1f, s.ProjectileSpeed);
            p.Bounces = s.Bounces;
            p.Z = 0f;
            var art = _ctx.Art;
            if (p.Kind == Kind.Rocket)
            {
                Setup(p.Body, art.Square, art.Lit, new Color(0.36f, 0.4f, 0.24f), new Vector2(0.44f, 0.14f));
                Setup(p.Glow, art.Glow, art.Additive, new Color(1f, 0.55f, 0.15f, 0.9f), new Vector2(0.7f, 0.45f));
            }
            else
            {
                var bolt = _ctx.Catalog != null ? ArtCatalog.At(_ctx.Catalog.Fx1, ArtIds.FlashGreen) : null;
                if (bolt != null) Setup(p.Body, bolt, art.Additive, Color.white, new Vector2(0.75f, 0.5f));
                else Setup(p.Body, art.Glow, art.Additive, new Color(0.55f, 1f, 0.6f, 1f), new Vector2(0.7f, 0.26f));
                Setup(p.Glow, art.Glow, art.Additive, new Color(0.2f, 1f, 0.4f, 0.45f), new Vector2(1.1f, 0.7f));
            }
            Setup(p.Shadow, art.Square, art.Solid, new Color(0f, 0f, 0f, 0.25f), new Vector2(0.4f, 0.12f));
            p.Shadow.enabled = p.Kind == Kind.Rocket;
            Activate(p);
        }

        public void SpawnGrenade(Fighter owner, Vector2 pos, Vector2 vel)
        {
            var g = _ctx.Config.Grenade;
            var p = Get();
            p.Kind = Kind.Grenade;
            p.Owner = owner;
            p.Stats = _ctx.Config.Get(WeaponId.Grenade);
            p.Pos = pos;
            p.Vel = vel;
            p.Z = 0.35f;
            p.Vz = 5.5f;
            p.Fuse = g.Fuse;
            var art = _ctx.Art;
            var painted = _ctx.Catalog != null ? _ctx.Catalog.PickupGrenade : null;
            p.BaseScale = painted != null ? 0.55f : 0.24f;
            if (painted != null) Setup(p.Body, painted, art.Lit, Color.white, new Vector2(p.BaseScale, p.BaseScale));
            else Setup(p.Body, art.Circle, art.Lit, new Color(0.24f, 0.36f, 0.2f), new Vector2(p.BaseScale, p.BaseScale));
            Setup(p.Glow, art.Circle, art.Additive, new Color(1f, 0.2f, 0.15f, 1f), new Vector2(0.08f, 0.08f));
            Setup(p.Shadow, art.Circle, art.Solid, new Color(0f, 0f, 0f, 0.3f), new Vector2(0.22f, 0.22f));
            p.Shadow.enabled = true;
            Activate(p);
        }

        public void Clear()
        {
            for (int i = _active.Count - 1; i >= 0; i--) Despawn(_active[i], i);
        }

        private void Update()
        {
            if (_ctx == null || _ctx.Match == null) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                var p = _active[i];
                bool alive = p.Kind == Kind.Grenade ? StepGrenade(p, dt) : StepShot(p, dt);
                if (!alive) Despawn(p, i);
                else UpdateVisual(p);
            }
        }

        private bool StepShot(Shot p, float dt)
        {
            p.Life -= dt;
            Vector2 step = p.Vel * dt;
            float dist = step.magnitude;
            if (p.Life <= 0f || dist < 0.0001f)
            {
                if (p.Kind == Kind.Rocket) Detonate(p, p.Pos);
                else _ctx.Fx.Impact(p.Pos, -p.Vel.normalized, false);
                return false;
            }
            Vector2 dir = step / dist;
            int n = Physics2D.CircleCast(p.Pos, 0.09f, dir, Combat.Filter(Layers.ShotMask), Hits, dist);
            RaycastHit2D best = default;
            float bestD = float.MaxValue;
            bool found = false;
            for (int i = 0; i < n; i++)
            {
                var h = Hits[i];
                if (p.Owner != null && h.collider == p.Owner.Collider) continue;
                if (h.distance < bestD) { bestD = h.distance; best = h; found = true; }
            }

            if (p.Kind == Kind.Rocket)
            {
                p.TrailT -= dt;
                if (p.TrailT <= 0f)
                {
                    p.TrailT = 0.025f;
                    _ctx.Fx.SmokeTrail(p.Pos - dir * 0.25f);
                }
            }

            if (!found)
            {
                p.Pos += step;
                return true;
            }

            var col = best.collider;
            Vector2 at = best.centroid;
            if (col.TryGetComponent(out Fighter victim))
            {
                if (p.Kind == Kind.Rocket) { Detonate(p, at); return false; }
                _ctx.Match.ReportHit(p.Owner, victim, p.Stats.Damage, dir, p.Stats.Knockback, p.Stats.Id, best.point);
                _ctx.Fx.PlasmaPop(at, p.Stats.TracerColor);
                return false;
            }

            if (p.Kind == Kind.Rocket)
            {
                Detonate(p, at + best.normal * 0.15f);
                return false;
            }
            if (col.TryGetComponent<IDamageable>(out var d) && d.IsAlive) d.TakeDamage(p.Stats.Damage, p.Owner, dir);
            if (p.Bounces > 0)
            {
                p.Bounces--;
                p.Pos = at + best.normal * 0.03f;
                p.Vel = Vector2.Reflect(p.Vel, best.normal);
                _ctx.Fx.PlasmaPop(at, p.Stats.TracerColor);
                _ctx.Sfx.Play(SfxId.HitWall, at, 0.4f);
                return true;
            }
            _ctx.Fx.PlasmaPop(at, p.Stats.TracerColor);
            return false;
        }

        private bool StepGrenade(Shot p, float dt)
        {
            p.Fuse -= dt;
            p.Vz -= 22f * dt;
            p.Z += p.Vz * dt;
            if (p.Z <= 0f)
            {
                p.Z = 0f;
                if (p.Vz < -2.5f)
                {
                    p.Vz = -p.Vz * 0.42f;
                    _ctx.Sfx.Play(SfxId.GrenadeBounce, p.Pos, 0.5f);
                }
                else p.Vz = 0f;
            }
            float friction = p.Z > 0.01f ? 0.4f : 6.5f;
            p.Vel *= Mathf.Exp(-friction * dt);
            Vector2 step = p.Vel * dt;
            if (step.sqrMagnitude > 0.000001f)
            {
                var hit = Physics2D.CircleCast(p.Pos, 0.1f, step.normalized, step.magnitude, Layers.SolidMask);
                if (hit.collider != null)
                {
                    p.Pos = hit.centroid + hit.normal * 0.02f;
                    p.Vel = Vector2.Reflect(p.Vel, hit.normal) * 0.55f;
                    _ctx.Sfx.Play(SfxId.GrenadeBounce, p.Pos, 0.6f);
                }
                else p.Pos += step;
            }
            if (p.Fuse <= 0f)
            {
                var g = _ctx.Config.Grenade;
                Combat.Explode(_ctx, p.Pos, g.Radius, g.Damage, g.Knockback, p.Owner, WeaponId.Grenade);
                return false;
            }
            return true;
        }

        private void Detonate(Shot p, Vector2 at)
        {
            var s = p.Stats;
            Combat.Explode(_ctx, at, s.SplashRadius, s.Damage, s.Knockback, p.Owner, s.Id);
        }

        private void UpdateVisual(Shot p)
        {
            if (p.Kind == Kind.Grenade)
            {
                float s = 1f + p.Z * 0.4f;
                p.T.position = new Vector3(p.Pos.x, p.Pos.y + p.Z * 0.35f, 0f);
                p.Body.transform.localScale = new Vector3(p.BaseScale * s, p.BaseScale * s, 1f);
                p.T.Rotate(0f, 0f, p.Vel.magnitude * 90f * Time.deltaTime);
                float blinkRate = p.Fuse < 0.6f ? 22f : 8f;
                p.Glow.enabled = Mathf.Sin(Time.time * blinkRate) > 0f;
                p.Shadow.transform.position = new Vector3(p.Pos.x + 0.06f, p.Pos.y - 0.08f, 0f);
                return;
            }
            float ang = Mathf.Atan2(p.Vel.y, p.Vel.x) * Mathf.Rad2Deg;
            p.T.SetPositionAndRotation(new Vector3(p.Pos.x, p.Pos.y, 0f), Quaternion.Euler(0f, 0f, ang));
            if (p.Shadow.enabled) p.Shadow.transform.position = new Vector3(p.Pos.x + 0.1f, p.Pos.y - 0.14f, 0f);
        }

        private Shot Get()
        {
            if (_pool.Count > 0)
            {
                var s = _pool[_pool.Count - 1];
                _pool.RemoveAt(_pool.Count - 1);
                return s;
            }
            var go = new GameObject("Projectile");
            go.transform.SetParent(transform, false);
            var shot = new Shot { Go = go, T = go.transform };
            shot.Body = Child(go.transform, "Body", SortOrder.Projectiles + 1);
            shot.Glow = Child(go.transform, "Glow", SortOrder.Projectiles + 2);
            shot.Shadow = Child(transform, "Shadow", SortOrder.Shadows);
            shot.Glow.transform.localPosition = new Vector3(-0.2f, 0f, 0f);
            return shot;
        }

        private static SpriteRenderer Child(Transform parent, string n, int order)
        {
            var go = new GameObject(n);
            go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = order;
            return sr;
        }

        private static void Setup(SpriteRenderer sr, Sprite s, Material m, Color c, Vector2 scale)
        {
            sr.sprite = s;
            sr.sharedMaterial = m;
            sr.color = c;
            sr.transform.localScale = new Vector3(scale.x, scale.y, 1f);
            sr.enabled = true;
        }

        private void Activate(Shot p)
        {
            p.TrailT = 0f;
            p.Glow.transform.localPosition = p.Kind == Kind.Rocket ? new Vector3(-0.28f, 0f, 0f) : Vector3.zero;
            p.T.rotation = Quaternion.identity;
            p.Go.SetActive(true);
            p.Shadow.gameObject.SetActive(p.Shadow.enabled);
            _active.Add(p);
            UpdateVisual(p);
        }

        private void Despawn(Shot p, int index)
        {
            _active.RemoveAt(index);
            p.Go.SetActive(false);
            p.Shadow.gameObject.SetActive(false);
            p.Owner = null;
            _pool.Add(p);
        }
    }
}
