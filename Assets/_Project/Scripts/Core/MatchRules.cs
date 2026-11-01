using System;
using System.Collections.Generic;

namespace NeonBrawl.Core
{
    public enum MatchMode { FreeForAll = 0, TeamDeathmatch = 1 }

    /// <summary>Room settings chosen by the host (ported from neon-brawl room settings).</summary>
    [Serializable]
    public struct MatchSettings
    {
        public MatchMode Mode;
        /// <summary>Kills needed to win (per player in FFA, per team in TDM). 0 = no limit.</summary>
        public int KillLimit;
        /// <summary>Match length in seconds. 0 = no time limit.</summary>
        public float TimeLimit;
        public int BotCount;
        /// <summary>0 easy, 1 normal, 2 hard.</summary>
        public int Difficulty;
        public bool Night;
        public bool Fog;

        public static MatchSettings Default => new MatchSettings
        {
            Mode = MatchMode.FreeForAll, KillLimit = 20, TimeLimit = 300f, BotCount = 6, Difficulty = 1, Night = false, Fog = true
        };
    }

    /// <summary>Score line for one fighter. Owned by <see cref="MatchRules"/>.</summary>
    public sealed class PlayerRecord
    {
        public int Id;
        public string Name;
        public int Team;
        public bool IsBot;
        public int Kills;
        public int Deaths;
        public int Streak;
        public int BestStreak;
        public float LastKillTime = -999f;
        public int MultiCount;
    }

    /// <summary>What a kill meant, so the presentation layer can show banners.</summary>
    public readonly struct KillOutcome
    {
        public readonly bool Valid;
        public readonly bool Suicide;
        public readonly bool TeamKill;
        public readonly int KillerStreak;
        public readonly int MultiKill;
        public readonly int VictimStreakEnded;

        public KillOutcome(bool valid, bool suicide, bool teamKill, int killerStreak, int multiKill, int victimStreakEnded)
        {
            Valid = valid;
            Suicide = suicide;
            TeamKill = teamKill;
            KillerStreak = killerStreak;
            MultiKill = multiKill;
            VictimStreakEnded = victimStreakEnded;
        }
    }

    /// <summary>
    /// Pure match rules: scores, streaks, end conditions, standings. No Unity dependency so the
    /// same rules run offline, on the online host, and in EditMode tests.
    /// </summary>
    public sealed class MatchRules
    {
        public const float MultiKillWindow = 3f;

        private readonly Dictionary<int, PlayerRecord> _byId = new Dictionary<int, PlayerRecord>(16);
        private readonly List<PlayerRecord> _players = new List<PlayerRecord>(16);
        private readonly int[] _teamKills = new int[2];

        public MatchRules(MatchSettings settings) { Settings = settings; }

        public MatchSettings Settings { get; }
        public IReadOnlyList<PlayerRecord> Players => _players;
        public bool IsTeamMode => Settings.Mode == MatchMode.TeamDeathmatch;

        public int TeamKills(int team) => team >= 0 && team < 2 ? _teamKills[team] : 0;

        /// <summary>Online late join: copy the host's team scores.</summary>
        public void SetTeamKills(int red, int blue)
        {
            _teamKills[0] = red;
            _teamKills[1] = blue;
        }

        public PlayerRecord Add(int id, string name, int team, bool isBot)
        {
            if (_byId.ContainsKey(id)) throw new ArgumentException("Duplicate player id " + id);
            var p = new PlayerRecord { Id = id, Name = name, Team = IsTeamMode ? team : -1, IsBot = isBot };
            _byId.Add(id, p);
            _players.Add(p);
            return p;
        }

        public void Remove(int id)
        {
            if (_byId.TryGetValue(id, out var p)) { _byId.Remove(id); _players.Remove(p); }
        }

        public PlayerRecord Get(int id) => _byId.TryGetValue(id, out var p) ? p : null;

        /// <summary>Team with fewer players (ties go to team 0). Used to balance joins.</summary>
        public int SmallerTeam()
        {
            int a = 0, b = 0;
            for (int i = 0; i < _players.Count; i++)
            {
                if (_players[i].Team == 0) a++;
                else if (_players[i].Team == 1) b++;
            }
            return b < a ? 1 : 0;
        }

        public bool AreEnemies(int idA, int idB)
        {
            if (idA == idB) return false;
            if (!IsTeamMode) return true;
            var a = Get(idA);
            var b = Get(idB);
            return a == null || b == null || a.Team != b.Team;
        }

        /// <summary>Records a death. killerId may equal victimId (suicide) or be -1 (world).</summary>
        public KillOutcome RecordKill(int killerId, int victimId, float time)
        {
            var victim = Get(victimId);
            if (victim == null) return default;

            int ended = victim.Streak;
            victim.Deaths++;
            victim.Streak = 0;
            victim.MultiCount = 0;

            var killer = Get(killerId);
            if (killer == null || killerId == victimId)
                return new KillOutcome(true, true, false, 0, 0, ended);

            if (IsTeamMode && killer.Team == victim.Team)
                return new KillOutcome(true, false, true, killer.Streak, 0, ended);

            killer.Kills++;
            killer.Streak++;
            if (killer.Streak > killer.BestStreak) killer.BestStreak = killer.Streak;
            killer.MultiCount = time - killer.LastKillTime <= MultiKillWindow ? killer.MultiCount + 1 : 1;
            killer.LastKillTime = time;
            if (IsTeamMode && killer.Team >= 0 && killer.Team < 2) _teamKills[killer.Team]++;

            return new KillOutcome(true, false, false, killer.Streak, killer.MultiCount, ended);
        }

        public bool IsKillLimitReached()
        {
            int limit = Settings.KillLimit;
            if (limit <= 0) return false;
            if (IsTeamMode) return _teamKills[0] >= limit || _teamKills[1] >= limit;
            for (int i = 0; i < _players.Count; i++)
                if (_players[i].Kills >= limit) return true;
            return false;
        }

        public bool IsTimeUp(float elapsed) => Settings.TimeLimit > 0f && elapsed >= Settings.TimeLimit;

        /// <summary>Fills <paramref name="into"/> best first: kills desc, deaths asc, then join order.</summary>
        public void GetStandings(List<PlayerRecord> into)
        {
            into.Clear();
            into.AddRange(_players);
            // Insertion sort: stable, allocation-free, and n <= 12.
            for (int i = 1; i < into.Count; i++)
            {
                var item = into[i];
                int j = i - 1;
                while (j >= 0 && Compare(into[j], item) > 0)
                {
                    into[j + 1] = into[j];
                    j--;
                }
                into[j + 1] = item;
            }
        }

        /// <summary>Negative when a ranks above b.</summary>
        public static int Compare(PlayerRecord a, PlayerRecord b)
        {
            if (a.Kills != b.Kills) return b.Kills - a.Kills;
            return a.Deaths - b.Deaths;
        }

        /// <summary>0 or 1 for the winning team, -1 for a draw.</summary>
        public int WinningTeam()
        {
            if (_teamKills[0] == _teamKills[1]) return -1;
            return _teamKills[0] > _teamKills[1] ? 0 : 1;
        }

        public static string StreakName(int streak)
        {
            switch (streak)
            {
                case 3: return "ON FIRE";
                case 5: return "RAMPAGE";
                case 7: return "UNSTOPPABLE";
                case 10: return "LEGENDARY";
                default: return null;
            }
        }

        public static string MultiKillName(int count)
        {
            if (count < 2) return null;
            switch (count)
            {
                case 2: return "DOUBLE KILL";
                case 3: return "TRIPLE KILL";
                case 4: return "MULTI KILL";
                default: return "MEGA KILL";
            }
        }
    }
}
