using System.Collections.Generic;
using NeonBrawl.Core;
using UnityEngine;

namespace NeonBrawl
{
    /// <summary>
    /// Hit detection for every weapon type. Detects hits locally and reports them to the match
    /// authority (<see cref="MatchController.ReportHit"/>), mirroring neon-brawl's model.
    /// Perf: 0 B GC; shared query buffers; one raycast per pellet.
    /// </summary>
    public static class Combat
    {
        private static readonly RaycastHit2D[] Hits = new RaycastHit2D[16];
        private static readonly Collider2D[] Overlaps = new Collider2D[48];
        private static readonly List<Fighter> Chained = new List<Fighter>(4);

        public static ContactFilter2D Filter(int mask)
        {
            var f = new ContactFilter2D { useLayerMask = true, layerMask = mask, useTriggers = false };
            return f;
        }

        public static bool HasLineOfSight(Vector2 a, Vector2 b) => Physics2D.Linecast(a, b, Layers.WallMask).collider == null;

        public static Vector2 Rotate(Vector2 v, float degrees)
        {
            float r = degrees * Mathf.Deg2Rad;
            float c = Mathf.Cos(r), s = Mathf.Sin(r);
            return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
        }

        public static bool RaycastFirst(Vector2 origin, Vector2 dir, float range, int mask, Collider2D ignore, out RaycastHit2D best)
        {
            int n = Physics2D.Raycast(origin, dir, Filter(mask), Hits, range);
            best = default;
            float bestD = float.MaxValue;
            bool found = false;
            for (int i = 0; i < n; i++)
            {
                var h = Hits[i];
                if (h.collider == ignore) continue;
                if (h.distance < bestD)
                {
                    bestD = h.distance;
                    best = h;
                    found = true;
                }
            }
            return found;
        }

        public static void Hitscan(GameContext ctx, Fighter shooter, Vector2 origin, Vector2 dir, WeaponStats w)
        {
            float half = w.SpreadDeg * 0.5f;
            // moving makes you a little less accurate
            if (shooter.Velocity.sqrMagnitude > 4f) half *= 1.35f;
            for (int p = 0; p < w.Pellets; p++)
            {
                Vector2 d = half > 0f ? Rotate(dir, Random.Range(-half, half)) : dir;
                Vector2 end = origin + d * w.Range;
                if (RaycastFirst(origin, d, w.Range, Layers.ShotMask, shooter.Collider, out var hit))
                {
                    end = hit.point;
                    var col = hit.collider;
                    if (col.gameObject.layer == Layers.Fighters && col.TryGetComponent(out Fighter victim))
                    {
                        ctx.Match.ReportHit(shooter, victim, w.Damage, d, w.Knockback, w.Id, hit.point);
                    }
                    else
                    {
                        bool prop = col.gameObject.layer == Layers.Props;
                        if (col.TryGetComponent<IDamageable>(out var dmg) && dmg.IsAlive) dmg.TakeDamage(w.Damage, shooter, d);
                        ctx.Fx.Impact(hit.point, hit.normal, prop);
                        if (!prop) ctx.Fx.BulletHole(hit.point);
                        // now and then a round sings off the wall
                        if (p == 0) ctx.Sfx.Play(!prop && Random.value < 0.22f ? SfxId.Ricochet : SfxId.HitWall, hit.point, 0.6f);
                    }
                }
                ctx.Fx.Tracer(origin, end, w.TracerColor, w.Id == WeaponId.Sniper ? 2.2f : 1f);
                if (p == 0) NearMiss(ctx, shooter, origin, end);
            }
        }

        /// <summary>A bullet that flies close past your head without hitting you makes a sharp whiz.</summary>
        private static void NearMiss(GameContext ctx, Fighter shooter, Vector2 from, Vector2 to)
        {
            var me = ctx.Match != null ? ctx.Match.Local : null;
            if (me == null || me == shooter || !me.Alive) return;
            Vector2 seg = to - from;
            float len2 = seg.sqrMagnitude;
            if (len2 < 0.01f) return;
            float t = Mathf.Clamp01(Vector2.Dot(me.Position - from, seg) / len2);
            if (t <= 0.05f || t >= 0.98f) return; // hit you, or stopped short
            Vector2 closest = from + seg * t;
            float d = Vector2.Distance(me.Position, closest);
            if (d > 0.35f && d < 1.4f) ctx.Sfx.Play(SfxId.Whiz, closest, 0.9f, 0.1f);
        }

        public static void FlameTick(GameContext ctx, Fighter shooter, Vector2 origin, Vector2 dir, WeaponStats w)
        {
            float reach = w.Range;
            var wall = Physics2D.Raycast(origin, dir, w.Range, Layers.WallMask);
            if (wall.collider != null) reach = wall.distance;
            ctx.Fx.Flame(origin, dir, reach, w.SpreadDeg);

            int n = Physics2D.OverlapCircle(origin, reach + 0.4f, Filter(Layers.FighterMask | Layers.PropMask), Overlaps);
            for (int i = 0; i < n; i++)
            {
                var col = Overlaps[i];
                Vector2 to = (Vector2)col.bounds.center - origin;
                if (Vector2.Angle(dir, to) > w.SpreadDeg + 12f && to.sqrMagnitude > 0.36f) continue;
                if (!HasLineOfSight(origin, col.bounds.center)) continue;
                if (col.TryGetComponent(out Fighter v))
                {
                    if (v == shooter || !v.Alive) continue;
                    if (ctx.Match.ReportHit(shooter, v, w.Damage, to.normalized, w.Knockback, w.Id, v.Position))
                        v.Ignite(shooter, w.BurnTime);
                }
                else if (col.TryGetComponent<IDamageable>(out var d) && d.IsAlive)
                {
                    d.TakeDamage(w.Damage * 0.5f, shooter, to.normalized);
                }
            }
        }

        public static void Tesla(GameContext ctx, Fighter shooter, Vector2 origin, Vector2 dir, WeaponStats w)
        {
            Vector2 end = origin + dir * w.Range;
            Fighter first = null;
            if (RaycastFirst(origin, dir, w.Range, Layers.ShotMask, shooter.Collider, out var hit))
            {
                end = hit.point;
                if (hit.collider.TryGetComponent(out Fighter v) && v.Alive) first = v;
                else if (hit.collider.TryGetComponent<IDamageable>(out var d) && d.IsAlive) d.TakeDamage(w.Damage, shooter, dir);
            }
            if (first == null)
            {
                first = FindAssistTarget(ctx, shooter, origin, dir, Vector2.Distance(origin, end), 10f);
                if (first != null) end = first.Position;
            }
            ctx.Fx.Lightning(origin, end, w.TracerColor);
            if (first == null)
            {
                ctx.Fx.Impact(end, -dir, false);
                return;
            }
            ctx.Match.ReportHit(shooter, first, w.Damage, dir, w.Knockback, w.Id, first.Position);

            Chained.Clear();
            Chained.Add(first);
            var prev = first;
            for (int c = 0; c < w.ChainCount; c++)
            {
                Fighter next = null;
                float best = w.ChainRange * w.ChainRange;
                var all = ctx.Match.Fighters;
                for (int i = 0; i < all.Count; i++)
                {
                    var f = all[i];
                    if (!f.Alive || f == shooter || Chained.Contains(f) || !ctx.Match.AreEnemies(shooter, f)) continue;
                    float d2 = (f.Position - prev.Position).sqrMagnitude;
                    if (d2 < best && HasLineOfSight(prev.Position, f.Position))
                    {
                        best = d2;
                        next = f;
                    }
                }
                if (next == null) break;
                ctx.Fx.Lightning(prev.Position, next.Position, w.TracerColor);
                ctx.Match.ReportHit(shooter, next, w.Damage * 0.7f, (next.Position - prev.Position).normalized, w.Knockback, w.Id, next.Position);
                Chained.Add(next);
                prev = next;
            }
        }

        public static void Melee(GameContext ctx, Fighter attacker, Vector2 dir, WeaponStats w)
        {
            Vector2 center = attacker.Position + dir * 0.55f;
            ctx.Fx.Slash(attacker.Position + dir * 0.45f, dir);
            int n = Physics2D.OverlapCircle(center, 0.75f, Filter(Layers.FighterMask | Layers.PropMask), Overlaps);
            bool hitAny = false;
            for (int i = 0; i < n; i++)
            {
                var col = Overlaps[i];
                if (col.TryGetComponent(out Fighter v))
                {
                    if (v == attacker || !v.Alive) continue;
                    Vector2 to = v.Position - attacker.Position;
                    if (to.sqrMagnitude > 0.36f && Vector2.Angle(dir, to) > 75f) continue;
                    if (!HasLineOfSight(attacker.Position, v.Position)) continue;
                    bool backstab = Vector2.Dot(v.AimDir, to.normalized) > 0.35f;
                    float dmg = backstab ? w.Damage * 2f : w.Damage;
                    if (ctx.Match.ReportHit(attacker, v, dmg, to.normalized, w.Knockback, WeaponId.Knife, v.Position)) hitAny = true;
                }
                else if (col.TryGetComponent<IDamageable>(out var d) && d.IsAlive)
                {
                    d.TakeDamage(w.Damage, attacker, dir);
                    hitAny = true;
                }
            }
            if (hitAny) ctx.Sfx.Play(SfxId.HitFlesh, center, 1f);
        }

        /// <summary>Explosion with distance falloff, wall shielding, knockback and full juice.</summary>
        public static void Explode(GameContext ctx, Vector2 pos, float radius, float damage, float knockback, Fighter owner, WeaponId weapon, IDamageable source = null)
        {
            ctx.Fx.Explosion(pos, radius);
            ctx.Sfx.Play(SfxId.Explosion, pos, 1f);
            ctx.CameraRig.ShakeFromExplosion(pos, radius);

            int n = Physics2D.OverlapCircle(pos, radius + 0.4f, Filter(Layers.FighterMask | Layers.PropMask), Overlaps);
            for (int i = 0; i < n; i++)
            {
                var col = Overlaps[i];
                Vector2 target = col.bounds.center;
                if (!HasLineOfSight(pos, target)) continue;
                float dist = Vector2.Distance(pos, target);
                float f = DamageModel.SplashFalloff(dist, radius + 0.4f);
                if (f <= 0f) continue;
                Vector2 dir = target - pos;
                if (dir.sqrMagnitude < 0.0001f) dir = Random.insideUnitCircle.normalized;
                dir.Normalize();
                if (col.TryGetComponent(out Fighter v))
                {
                    if (!v.Alive) continue;
                    float dmg = damage * f * (v == owner ? 0.5f : 1f);
                    ctx.Match.ReportHit(owner, v, dmg, dir, knockback * Mathf.Lerp(0.45f, 1f, f), weapon, v.Position);
                }
                else if (col.TryGetComponent<IDamageable>(out var d) && d != source && d.IsAlive)
                {
                    // barrel chains are the host's own world damage, whoever lit the first one
                    if (weapon == WeaponId.Barrel && d is Destructible prop) prop.ApplyDamage(damage * f, owner, dir);
                    else d.TakeDamage(damage * f, owner, dir);
                }
            }
        }

        private static Fighter FindAssistTarget(GameContext ctx, Fighter shooter, Vector2 origin, Vector2 dir, float range, float coneDeg)
        {
            Fighter best = null;
            float bestAngle = coneDeg;
            var all = ctx.Match.Fighters;
            for (int i = 0; i < all.Count; i++)
            {
                var f = all[i];
                if (!f.Alive || f == shooter || !ctx.Match.AreEnemies(shooter, f)) continue;
                Vector2 to = f.Position - origin;
                if (to.sqrMagnitude > range * range) continue;
                float a = Vector2.Angle(dir, to);
                if (a < bestAngle && HasLineOfSight(origin, f.Position))
                {
                    bestAngle = a;
                    best = f;
                }
            }
            return best;
        }
    }
}
