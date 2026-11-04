using System.Collections.Generic;
using UnityEngine;

namespace NeonBrawl
{
    /// <summary>
    /// Bot AI ported from neon-brawl's botThink (aim error, reaction time, view range, think rate,
    /// burst length per difficulty), extended for top-down play: line of sight, A* paths to pickups,
    /// strafing, dodging with dash, knife at close range, grenades at the last seen position.
    /// Perf: decisions at 3–6 Hz; 0 B GC; one path search per decision at most.
    /// </summary>
    public sealed class BotBrain : IFighterBrain
    {
        private readonly MatchController _m;
        private readonly BotTuning _t;
        private readonly System.Random _rng;
        private readonly List<Vector2Int> _path = new List<Vector2Int>(64);

        private int _pathIndex;
        private Vector2 _goal;
        private bool _hasGoal;
        private float _repathCd;
        private Fighter _target;
        private float _seenAt;
        private Vector2 _lastKnown;
        private float _lastKnownTime = -99f;
        private float _think;
        private float _burst;
        private float _pause;
        private float _strafeT;
        private int _strafeSign = 1;
        private float _errAngle;
        private Vector2 _aim = Vector2.right;
        private float _lastHealth = 100f;
        private Vector2 _stuckPos;
        private float _stuckT;
        private float _unstickT;
        private Vector2 _unstickDir;
        private bool _wantGrenade;
        private float _grenadeDist;

        public BotBrain(MatchController match, BotTuning tuning, int seed)
        {
            _m = match;
            _t = tuning;
            _rng = new System.Random(seed);
            _strafeSign = _rng.Next(2) == 0 ? -1 : 1;
        }

        private float R() => (float)_rng.NextDouble();

        public void Think(Fighter self, float dt, ref FighterInput input)
        {
            input.ClearPulses();
            input.Fire = false;
            Vector2 pos = self.Position;
            var weapon = self.Weapons.Current.Stats;

            if (_repathCd > 0f) _repathCd -= dt;
            _think -= dt;
            if (_think <= 0f)
            {
                _think = _t.ThinkInterval * (0.8f + 0.4f * R());
                Decide(self);
            }
            if (_target != null && !_target.Alive) _target = null;

            Vector2 move;
            Vector2 desiredAim = _aim;

            if (_target != null)
            {
                Vector2 to = _target.Position - pos;
                float dist = Mathf.Max(to.magnitude, 0.01f);
                Vector2 dirTo = to / dist;
                Vector2 lead = _target.Velocity * Mathf.Clamp(dist / 40f, 0f, 0.15f);
                desiredAim = Combat.Rotate((to + lead).normalized, _errAngle);

                float preferred = Mathf.Clamp(weapon.Range * 0.45f, 1.5f, 8f);
                if (weapon.Kind == FireKind.Flame || weapon.Id == WeaponId.Shotgun) preferred = Mathf.Min(preferred, 3f);
                float radial = dist > preferred + 1f ? 1f : (dist < preferred - 1f ? -0.8f : 0f);
                Vector2 perp = new Vector2(-dirTo.y, dirTo.x) * _strafeSign;
                _strafeT -= dt;
                if (_strafeT <= 0f)
                {
                    _strafeT = 0.6f + R() * 1.4f;
                    _strafeSign = -_strafeSign;
                }
                move = dirTo * radial + perp * 0.75f;
                if (move.sqrMagnitude > 0.01f && Physics2D.CircleCast(pos, 0.32f, move.normalized, 0.9f, Layers.SolidMask).collider != null)
                {
                    _strafeSign = -_strafeSign;
                    move = dirTo * radial - perp * 0.75f;
                }

                bool reacted = Time.time - _seenAt >= _t.ReactionTime;
                if (reacted && Vector2.Angle(_aim, to) < 14f && dist <= weapon.Range * 1.05f && self.Weapons.Current.Mag > 0)
                {
                    if (_pause > 0f) _pause -= dt;
                    else
                    {
                        input.Fire = true;
                        _burst += dt;
                        if (_burst >= _t.BurstTime)
                        {
                            _burst = 0f;
                            _pause = _t.BurstPause * (0.6f + 0.8f * R());
                        }
                    }
                }
                if (dist < 1.25f) input.Melee = true;
                if (self.Health < _lastHealth - 0.5f && R() < _t.DashChance * 0.35f)
                {
                    input.Dash = true;
                    move = perp;
                }
            }
            else
            {
                move = FollowPath(pos);
                if (move.sqrMagnitude > 0.01f) desiredAim = move.normalized;
                var cur = self.Weapons.Current;
                if (cur.Mag < cur.Stats.MagSize * 0.5f && cur.CanReload) input.Reload = true;
                if (self.Weapons.Primary != null && self.Weapons.Current != self.Weapons.Primary && !self.Weapons.Primary.Dry) input.Swap = true;
            }

            if (_wantGrenade)
            {
                _wantGrenade = false;
                Vector2 toKnown = _lastKnown - pos;
                if (toKnown.sqrMagnitude > 0.01f)
                {
                    _aim = toKnown.normalized;
                    desiredAim = _aim;
                    input.Grenade = true;
                    input.ThrowDistance = _grenadeDist;
                }
            }

            // stuck detection: nudge sideways and re-plan
            _stuckT += dt;
            if (_stuckT > 0.7f)
            {
                if (move.sqrMagnitude > 0.25f && (pos - _stuckPos).sqrMagnitude < 0.04f)
                {
                    _unstickT = 0.45f;
                    _unstickDir = Combat.Rotate(move.normalized, R() < 0.5f ? 100f : -100f);
                    _hasGoal = false;
                    _path.Clear();
                }
                _stuckT = 0f;
                _stuckPos = pos;
            }
            if (_unstickT > 0f)
            {
                _unstickT -= dt;
                move = _unstickDir;
            }

            _lastHealth = self.Health;
            _aim = RotateTowards(_aim, desiredAim, _t.AimTurnSpeed * dt);
            input.Aim = _aim;
            input.HasAim = true;
            input.Move = Vector2.ClampMagnitude(move, 1f);
        }

        private void Decide(Fighter self)
        {
            Vector2 pos = self.Position;
            Fighter best = null;
            float bestD = _t.ViewRange * _t.ViewRange;
            var all = _m.Fighters;
            for (int i = 0; i < all.Count; i++)
            {
                var f = all[i];
                if (f == self || !f.Alive || !_m.AreEnemies(self, f) || f.Powers.Ghost) continue;
                float d2 = (f.Position - pos).sqrMagnitude;
                if (d2 < bestD && Combat.HasLineOfSight(pos, f.Position))
                {
                    best = f;
                    bestD = d2;
                }
            }

            bool keep = _target != null && _target.Alive && !_target.Powers.Ghost &&
                        (_target.Position - pos).sqrMagnitude < _t.ViewRange * _t.ViewRange * 1.4f &&
                        Combat.HasLineOfSight(pos, _target.Position);
            if (!keep)
            {
                if (_target != null)
                {
                    _lastKnown = _target.Position;
                    _lastKnownTime = Time.time;
                }
                _target = best;
                if (best != null) _seenAt = Time.time;
            }
            _errAngle = (R() * 2f - 1f) * _t.AimErrorDeg;

            if (_target == null)
            {
                float sinceSeen = Time.time - _lastKnownTime;
                float d = Vector2.Distance(pos, _lastKnown);
                if (self.Grenades > 0 && sinceSeen < 2f && d > 3f && d < 9f && R() < _t.GrenadeChance)
                {
                    _wantGrenade = true;
                    _grenadeDist = d;
                }
                ChooseGoal(self);
            }
        }

        private void ChooseGoal(Fighter self)
        {
            Vector2 pos = self.Position;
            Pickup want = null;
            var pickups = _m.Pickups;
            if (self.Health < 45f) want = pickups.FindNearest(pos, PickupType.Health, 30f);
            if (want == null && self.Weapons.Primary == null) want = pickups.FindNearest(pos, PickupType.Weapon, 30f);
            if (want == null && self.Weapons.Primary != null && self.Weapons.Primary.Dry)
                want = pickups.FindNearest(pos, PickupType.Ammo, 25f) ?? pickups.FindNearest(pos, PickupType.Weapon, 25f);
            if (want == null && self.Armor < 1f && R() < 0.3f) want = pickups.FindNearest(pos, PickupType.Armor, 18f);

            if (want != null) { SetGoal(self, want.Position, false); return; }
            // power orbs are worth a detour when the slot is empty
            if (!self.Powers.HasHeld && _m.PowerUps != null)
            {
                var orb = _m.PowerUps.Nearest(pos, 20f);
                if (orb != null && R() < 0.85f) { SetGoal(self, orb.Pos, false); return; }
            }
            if (Time.time - _lastKnownTime < 4f) { SetGoal(self, _lastKnown, false); return; }

            bool reached = !_hasGoal || _pathIndex >= _path.Count;
            if (!reached && _repathCd > 0f) return;
            if (R() < 0.55f)
            {
                var e = NearestEnemy(self);
                if (e != null) { SetGoal(self, e.Position, true); return; }
            }
            var spawns = _m.Map.Spawns;
            SetGoal(self, spawns[_rng.Next(spawns.Count)], true);
        }

        private Fighter NearestEnemy(Fighter self)
        {
            Fighter best = null;
            float bestD = float.MaxValue;
            var all = _m.Fighters;
            for (int i = 0; i < all.Count; i++)
            {
                var f = all[i];
                if (f == self || !f.Alive || !_m.AreEnemies(self, f) || f.Powers.Ghost) continue;
                float d = (f.Position - self.Position).sqrMagnitude;
                if (d < bestD) { bestD = d; best = f; }
            }
            return best;
        }

        private void SetGoal(Fighter self, Vector2 goal, bool force)
        {
            if (!force && _hasGoal && (goal - _goal).sqrMagnitude < 1f && _pathIndex < _path.Count && _repathCd > 0f) return;
            _goal = goal;
            _repathCd = 1.2f;
            var map = _m.Map;
            if (map.Grid.FindPath(map.WorldToCell(self.Position), map.WorldToCell(goal), _path))
            {
                map.Grid.SmoothPath(_path, 0.42f);
                _pathIndex = 1;
                _hasGoal = true;
            }
            else
            {
                _path.Clear();
                _hasGoal = false;
            }
        }

        private Vector2 FollowPath(Vector2 pos)
        {
            while (_pathIndex < _path.Count)
            {
                var c = _path[_pathIndex];
                Vector2 wp = MapData.CellCenter(c.x, c.y);
                Vector2 d = wp - pos;
                if (d.sqrMagnitude < 0.16f)
                {
                    _pathIndex++;
                    continue;
                }
                return d.normalized;
            }
            return Vector2.zero;
        }

        private static Vector2 RotateTowards(Vector2 from, Vector2 to, float maxDeg)
        {
            float a = Vector2.SignedAngle(from, to);
            return Combat.Rotate(from, Mathf.Clamp(a, -maxDeg, maxDeg)).normalized;
        }
    }
}
