using System;

namespace NeonBrawl
{
    /// <summary>One day's gift on the 7-day calendar.</summary>
    public readonly struct DailyGift
    {
        public readonly int Coins, Gems;
        public readonly bool Chest;

        public DailyGift(int coins, int gems, bool chest = false)
        {
            Coins = coins;
            Gems = gems;
            Chest = chest;
        }
    }

    /// <summary>Card state on the calendar.</summary>
    public enum DailyState { Claimed, Today, Later }

    /// <summary>
    /// 7-day login calendar. One claim per calendar day; missing a day starts again at day 1; after day 7
    /// a new week begins. Gifts stay small (the economy is meant to feel earned). Uses the device clock,
    /// which is fine offline; switch to server time when online accounts arrive.
    /// </summary>
    public static class DailyRewards
    {
        public const int Days = 7;

        public static readonly DailyGift[] Gifts =
        {
            new DailyGift(25, 0), new DailyGift(40, 0), new DailyGift(0, 3), new DailyGift(60, 0),
            new DailyGift(80, 0), new DailyGift(0, 5), new DailyGift(150, 10, true)
        };

        private static string Today => DateTime.Now.ToString("yyyy-MM-dd");
        private static string Yesterday => DateTime.Now.AddDays(-1).ToString("yyyy-MM-dd");

        public static bool CanClaim(PlayerProfile p) => p.Data.DailyLast != Today;

        /// <summary>Days in a row so far; 0 once a day was missed.</summary>
        public static int Streak(PlayerProfile p)
        {
            var d = p.Data;
            return d.DailyLast == Today || d.DailyLast == Yesterday ? d.DailyStreak : 0;
        }

        /// <summary>Cards of the current week already claimed (0..7).</summary>
        public static int ClaimedThisWeek(PlayerProfile p)
        {
            int s = Streak(p);
            if (s == 0) return 0;
            int inWeek = s % Days;
            // a finished week stays shown as complete until tomorrow, then a fresh week starts
            if (inWeek == 0) return p.Data.DailyLast == Today ? Days : 0;
            return inWeek;
        }

        public static DailyState StateOf(PlayerProfile p, int day)
        {
            int claimed = ClaimedThisWeek(p);
            if (day < claimed) return DailyState.Claimed;
            return day == claimed && CanClaim(p) ? DailyState.Today : DailyState.Later;
        }

        /// <summary>The card today's claim pays (only meaningful while <see cref="CanClaim"/>).</summary>
        public static int NextDay(PlayerProfile p) => Streak(p) % Days;

        /// <summary>Pays today's gift (times <paramref name="multiplier"/> after a rewarded ad) and saves.</summary>
        public static DailyGift Claim(PlayerProfile p, int multiplier = 1)
        {
            if (!CanClaim(p)) return default;
            var d = p.Data;
            int s = Streak(p);
            var g = Gifts[s % Days];
            d.Coins += g.Coins * multiplier;
            d.Gems += g.Gems * multiplier;
            d.DailyStreak = s + 1;
            d.DailyLast = Today;
            p.Save();
            return g;
        }

        public static TimeSpan UntilTomorrow => DateTime.Today.AddDays(1) - DateTime.Now;
    }
}
