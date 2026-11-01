using System.Collections.Generic;
using NeonBrawl.Core;
using NUnit.Framework;
using UnityEngine;

namespace NeonBrawl.Tests
{
    public sealed class MatchRulesTests
    {
        private static MatchRules Ffa(int killLimit = 3) =>
            new MatchRules(new MatchSettings { Mode = MatchMode.FreeForAll, KillLimit = killLimit, TimeLimit = 60f });

        [Test]
        public void Kill_CountsForKillerAndDeathForVictim()
        {
            var r = Ffa();
            r.Add(1, "A", 0, false);
            r.Add(2, "B", 0, true);
            var o = r.RecordKill(1, 2, 0f);
            Assert.IsTrue(o.Valid);
            Assert.AreEqual(1, r.Get(1).Kills);
            Assert.AreEqual(1, r.Get(2).Deaths);
        }

        [Test]
        public void Suicide_AddsDeathOnly()
        {
            var r = Ffa();
            r.Add(1, "A", 0, false);
            var o = r.RecordKill(1, 1, 0f);
            Assert.IsTrue(o.Suicide);
            Assert.AreEqual(0, r.Get(1).Kills);
            Assert.AreEqual(1, r.Get(1).Deaths);
        }

        [Test]
        public void MultiKill_WithinWindow_AndResetsAfter()
        {
            var r = Ffa(10);
            r.Add(1, "A", 0, false);
            for (int i = 2; i <= 5; i++) r.Add(i, "V" + i, 0, true);
            Assert.AreEqual(1, r.RecordKill(1, 2, 0f).MultiKill);
            Assert.AreEqual(2, r.RecordKill(1, 3, 1f).MultiKill);
            Assert.AreEqual(3, r.RecordKill(1, 4, 2.5f).MultiKill);
            Assert.AreEqual(1, r.RecordKill(1, 5, 10f).MultiKill);
            Assert.AreEqual("TRIPLE KILL", MatchRules.MultiKillName(3));
        }

        [Test]
        public void Streak_EndsOnDeath()
        {
            var r = Ffa(10);
            r.Add(1, "A", 0, false);
            r.Add(2, "B", 0, true);
            r.RecordKill(1, 2, 0f);
            r.RecordKill(1, 2, 5f);
            var o = r.RecordKill(2, 1, 10f);
            Assert.AreEqual(2, o.VictimStreakEnded);
            Assert.AreEqual(0, r.Get(1).Streak);
            Assert.AreEqual(2, r.Get(1).BestStreak);
        }

        [Test]
        public void KillLimit_EndsMatch()
        {
            var r = Ffa(2);
            r.Add(1, "A", 0, false);
            r.Add(2, "B", 0, true);
            r.RecordKill(1, 2, 0f);
            Assert.IsFalse(r.IsKillLimitReached());
            r.RecordKill(1, 2, 1f);
            Assert.IsTrue(r.IsKillLimitReached());
        }

        [Test]
        public void Standings_SortByKillsThenFewerDeaths()
        {
            var r = Ffa(50);
            r.Add(1, "A", 0, false);
            r.Add(2, "B", 0, true);
            r.Add(3, "C", 0, true);
            r.RecordKill(2, 1, 0f);
            r.RecordKill(3, 1, 0f);
            r.RecordKill(3, 2, 0f);
            var list = new List<PlayerRecord>();
            r.GetStandings(list);
            Assert.AreEqual(3, list[0].Id);
            Assert.AreEqual(2, list[1].Id);
            Assert.AreEqual(1, list[2].Id);
        }

        [Test]
        public void TeamKill_DoesNotScore_AndTeamsBalance()
        {
            var r = new MatchRules(new MatchSettings { Mode = MatchMode.TeamDeathmatch, KillLimit = 5 });
            r.Add(1, "A", r.SmallerTeam(), false);
            r.Add(2, "B", r.SmallerTeam(), true);
            r.Add(3, "C", r.SmallerTeam(), true);
            Assert.AreEqual(0, r.Get(1).Team);
            Assert.AreEqual(1, r.Get(2).Team);
            Assert.AreEqual(0, r.Get(3).Team);
            var o = r.RecordKill(1, 3, 0f);
            Assert.IsTrue(o.TeamKill);
            Assert.AreEqual(0, r.Get(1).Kills);
            r.RecordKill(1, 2, 0f);
            Assert.AreEqual(1, r.TeamKills(0));
            Assert.IsFalse(r.AreEnemies(1, 3));
            Assert.IsTrue(r.AreEnemies(1, 2));
        }
    }

    public sealed class DamageModelTests
    {
        [Test]
        public void Armor_AbsorbsHalfUntilEmpty()
        {
            var s = DamageModel.Apply(40f, 100f, 0.5f);
            Assert.AreEqual(20f, s.Armor, 0.001f);
            Assert.AreEqual(20f, s.Health, 0.001f);
            var s2 = DamageModel.Apply(40f, 5f, 0.5f);
            Assert.AreEqual(5f, s2.Armor, 0.001f);
            Assert.AreEqual(35f, s2.Health, 0.001f);
        }

        [Test]
        public void Cap_LimitsSingleHit()
        {
            Assert.AreEqual(115f, DamageModel.Cap(500f, 115f));
            Assert.AreEqual(10f, DamageModel.Cap(10f, 115f));
        }

        [Test]
        public void Splash_FallsOffAndStopsAtRadius()
        {
            Assert.AreEqual(1f, DamageModel.SplashFalloff(0f, 3f), 0.001f);
            Assert.AreEqual(0.35f, DamageModel.SplashFalloff(3f, 3f), 0.001f);
            Assert.AreEqual(0f, DamageModel.SplashFalloff(3.1f, 3f), 0.001f);
        }
    }

    public sealed class GridPathfinderTests
    {
        private static GridPathfinder Grid(string[] rows)
        {
            int h = rows.Length, w = rows[0].Length;
            var g = new GridPathfinder(w, h);
            for (int r = 0; r < h; r++)
                for (int x = 0; x < w; x++)
                    g.SetWalkable(x, h - 1 - r, rows[r][x] != '#');
            return g;
        }

        [Test]
        public void FindsPathAroundWall()
        {
            var g = Grid(new[]
            {
                ".....",
                ".###.",
                ".#...",
                ".#.#.",
                ".....",
            });
            var path = new List<Vector2Int>();
            Assert.IsTrue(g.FindPath(new Vector2Int(2, 2), new Vector2Int(0, 4), path));
            Assert.AreEqual(new Vector2Int(2, 2), path[0]);
            Assert.AreEqual(new Vector2Int(0, 4), path[path.Count - 1]);
            foreach (var c in path) Assert.IsTrue(g.IsWalkable(c.x, c.y));
        }

        [Test]
        public void ReturnsFalseWhenSealed()
        {
            var g = Grid(new[]
            {
                "..#..",
                "..#..",
                "..#..",
            });
            var path = new List<Vector2Int>();
            Assert.IsFalse(g.FindPath(new Vector2Int(0, 0), new Vector2Int(4, 0), path));
        }

        [Test]
        public void NoCornerCutting()
        {
            var g = Grid(new[]
            {
                ".#",
                "..",
            });
            var path = new List<Vector2Int>();
            Assert.IsTrue(g.FindPath(new Vector2Int(0, 1), new Vector2Int(1, 0), path));
            Assert.AreEqual(3, path.Count, "must go around the corner, not diagonally through it");
        }

        [Test]
        public void SmoothPath_RemovesStraightLineWaypoints()
        {
            var g = Grid(new[] { "........" });
            var path = new List<Vector2Int>();
            Assert.IsTrue(g.FindPath(new Vector2Int(0, 0), new Vector2Int(7, 0), path));
            g.SmoothPath(path, 0.3f);
            Assert.AreEqual(2, path.Count);
        }
    }
}
