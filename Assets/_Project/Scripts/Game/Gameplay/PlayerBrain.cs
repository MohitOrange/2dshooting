using UnityEngine;

namespace NeonBrawl
{
    /// <summary>
    /// Feeds the local player's devices (touch, mouse/keyboard, gamepad) into their fighter.
    /// Touch: a light aim assist bends the aim toward a visible enemy close to where your thumb points.
    /// AUTO SHOOT (setting + HUD button, on by default): with no aim input the fighter locks onto the
    /// nearest enemy it can see on screen and fires; while you aim yourself it fires whenever an enemy is
    /// in your sights. Mouse aim is never moved.
    /// Perf: 0 B GC per frame; the full enemy scan runs 10 times a second, plus one line-of-sight check
    /// per frame for the locked target.
    /// </summary>
    public sealed class PlayerBrain : IFighterBrain
    {
        private const float AssistAngle = 16f;      // degrees around your aim where enemies count
        private const float AssistStrength = 0.75f; // 0 = off, 1 = full snap
        private const float SightsAngle = 8f;       // auto shoot while aiming: enemy this close to the aim line
        private const float CloseSightsAngle = 26f; // ...or this close when the enemy is right next to you
        private const float ScanInterval = 0.1f;

        private readonly GameContext _ctx;
        private float _scan;
        private bool _manual;

        /// <summary>The enemy auto shoot is firing at, or null. The crosshair draws a lock marker on it.</summary>
        public Fighter AutoTarget { get; private set; }

        public PlayerBrain(GameContext ctx) { _ctx = ctx; }

        public void Think(Fighter self, float dt, ref FighterInput input)
        {
            var reader = _ctx.Input;
            reader.Sample(_ctx.Camera, self.Position, 6.5f, ref input);
            bool touchAim = reader.TouchActive && !reader.UsingMouse;
            if (touchAim && input.HasAim) AimAssist(self, ref input);

            if (_ctx.Prefs.AutoShoot && !reader.Typing)
            {
                AutoShoot(self, dt, ref input);
                return;
            }
            AutoTarget = null;
            // auto shoot off: thumb resting on FIRE still shoots the nearest enemy
            if (touchAim && !input.HasAim && reader.TouchAutoFire)
            {
                var match = _ctx.Match;
                var t = match != null ? Nearest(self, match, Reach(self), null) : null;
                if (t != null) LockOn(self, t, ref input);
            }
        }

        // ------------------------------------------------------------------ auto shoot

        private void AutoShoot(Fighter self, float dt, ref FighterInput input)
        {
            var match = _ctx.Match;
            if (match == null) { AutoTarget = null; return; }
            float reach = Reach(self);
            bool manual = input.HasAim;

            _scan -= dt;
            if (_scan <= 0f || manual != _manual || !StillValid(self, match, AutoTarget, reach * 1.15f))
            {
                _scan = ScanInterval;
                _manual = manual;
                AutoTarget = manual ? InSights(self, match, input.Aim, reach) : Nearest(self, match, reach, AutoTarget);
            }
            var t = AutoTarget;
            if (t == null) return;

            if (manual)
            {
                // you are aiming: only shoot while the enemy stays in your sights
                Vector2 to = t.Position - self.Position;
                if (Vector2.Angle(input.Aim, to) > Allowed(to.magnitude) * 1.4f) { AutoTarget = null; return; }
                input.Fire = true;
            }
            else LockOn(self, t, ref input);
        }

        private static void LockOn(Fighter self, Fighter t, ref FighterInput input)
        {
            Vector2 to = t.Position - self.Position;
            if (to.sqrMagnitude > 0.0001f) input.Aim = to.normalized;
            input.HasAim = true;
            input.Fire = true;
        }

        /// <summary>How far auto shoot reaches with the gun in hand (the knife only swings up close).</summary>
        private static float Reach(Fighter self)
        {
            var s = self.Weapons.Current.Stats;
            return s.Kind == FireKind.Melee ? s.Range + 0.6f : Mathf.Max(5f, s.Range * 0.92f);
        }

        private static float Allowed(float dist) => dist < 2.5f ? CloseSightsAngle : SightsAngle;

        private bool StillValid(Fighter self, MatchController match, Fighter t, float reach)
        {
            if (t == null || !Targetable(self, match, t)) return false;
            Vector2 to = t.Position - self.Position;
            return to.sqrMagnitude <= reach * reach && Combat.HasLineOfSight(self.Position, t.Position);
        }

        private static bool Targetable(Fighter self, MatchController match, Fighter f) =>
            f != self && f.Alive && f.SpawnProtection <= 0f && match.AreEnemies(self, f);

        /// <summary>Nearest enemy you can see on screen. The current target wins ties so the aim does not flicker.</summary>
        private Fighter Nearest(Fighter self, MatchController match, float reach, Fighter current)
        {
            Fighter best = null;
            float bestScore = reach * reach;
            var fighters = match.Fighters;
            for (int i = 0; i < fighters.Count; i++)
            {
                var f = fighters[i];
                if (!Targetable(self, match, f)) continue;
                float d = (f.Position - self.Position).sqrMagnitude;
                if (f == current) d *= 0.6f;
                if (d >= bestScore || !OnScreen(f.Position) || !Combat.HasLineOfSight(self.Position, f.Position)) continue;
                bestScore = d;
                best = f;
            }
            return best;
        }

        /// <summary>Enemy closest to where you are aiming, if it is within your sights.</summary>
        private Fighter InSights(Fighter self, MatchController match, Vector2 aim, float reach)
        {
            Fighter best = null;
            float bestScore = float.MaxValue;
            var fighters = match.Fighters;
            for (int i = 0; i < fighters.Count; i++)
            {
                var f = fighters[i];
                if (!Targetable(self, match, f)) continue;
                Vector2 to = f.Position - self.Position;
                float dist = to.magnitude;
                if (dist > reach) continue;
                float ang = Vector2.Angle(aim, to);
                if (ang > Allowed(dist)) continue;
                float score = ang + dist * 0.5f;
                if (score >= bestScore || !OnScreen(f.Position) || !Combat.HasLineOfSight(self.Position, f.Position)) continue;
                bestScore = score;
                best = f;
            }
            return best;
        }

        private bool OnScreen(Vector2 p)
        {
            var cam = _ctx.Camera;
            if (cam == null) return true;
            Vector3 v = cam.WorldToViewportPoint(p);
            return v.x > 0.02f && v.x < 0.98f && v.y > 0.03f && v.y < 0.97f;
        }

        // ------------------------------------------------------------------ touch aim assist

        private void AimAssist(Fighter self, ref FighterInput input)
        {
            var match = _ctx.Match;
            if (match == null || input.Aim.sqrMagnitude < 0.0001f) return;
            Vector2 aim = input.Aim.normalized;
            float range = Mathf.Max(6f, self.Weapons.Current.Stats.Range * 0.9f);
            Fighter best = null;
            float bestScore = float.MaxValue;
            var fighters = match.Fighters;
            for (int i = 0; i < fighters.Count; i++)
            {
                var f = fighters[i];
                if (f == self || !f.Alive || !match.AreEnemies(self, f)) continue;
                Vector2 to = f.Position - self.Position;
                float dist = to.magnitude;
                if (dist < 0.3f || dist > range) continue;
                float ang = Vector2.Angle(aim, to);
                if (ang > AssistAngle) continue;
                float score = ang + dist * 0.6f;
                if (score >= bestScore || !Combat.HasLineOfSight(self.Position, f.Position)) continue;
                bestScore = score;
                best = f;
            }
            if (best == null) return;
            Vector2 target = (best.Position - self.Position).normalized;
            input.Aim = Vector2.Lerp(aim, target, AssistStrength).normalized;
        }
    }
}
