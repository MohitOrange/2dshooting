using System;
using System.Collections.Generic;
using UnityEngine;

namespace NeonBrawl
{
    public enum MissionKind { Kills, Matches, Wins, Top3, Streak, Multi, Revenge, WeaponKills }

    /// <summary>One mission from the pool: what to do, how many times, and the coin reward.</summary>
    public sealed class MissionDef
    {
        public readonly MissionKind Kind;
        public readonly WeaponId Weapon;
        public readonly int Target, Coins;
        public readonly string Text;

        public MissionDef(MissionKind kind, int target, int coins, string text, WeaponId weapon = WeaponId.Pistol)
        {
            Kind = kind;
            Target = target;
            Coins = coins;
            Text = text;
            Weapon = weapon;
        }
    }

    /// <summary>
    /// Three daily missions, the same all day (seeded by the date) and new ones at midnight. Slot 1 is
    /// always an easy one. Progress lives in the profile; each mission pays a few coins and finishing all
    /// three pays a small gem bonus. Offline it uses the device clock (server time once online).
    /// </summary>
    public static class Missions
    {
        public const int Slots = 3;
        public const int BonusGems = 3;
        private const int EasyCount = 3;

        public static readonly MissionDef[] Pool =
        {
            // easy (slot 1 always comes from these)
            new MissionDef(MissionKind.Kills, 15, 30, "Get 15 kills"),
            new MissionDef(MissionKind.Matches, 3, 25, "Play 3 matches"),
            new MissionDef(MissionKind.Kills, 25, 40, "Get 25 kills"),
            // the rest
            new MissionDef(MissionKind.Wins, 1, 40, "Win a match"),
            new MissionDef(MissionKind.Top3, 2, 35, "Finish in the top 3 twice"),
            new MissionDef(MissionKind.Streak, 5, 35, "Reach a 5-kill streak"),
            new MissionDef(MissionKind.Multi, 2, 30, "Get 2 multi-kills"),
            new MissionDef(MissionKind.Revenge, 2, 30, "Take REVENGE 2 times"),
            new MissionDef(MissionKind.WeaponKills, 5, 30, "Get 5 shotgun kills", WeaponId.Shotgun),
            new MissionDef(MissionKind.WeaponKills, 3, 35, "Get 3 sniper kills", WeaponId.Sniper),
            new MissionDef(MissionKind.WeaponKills, 2, 35, "Get 2 grenade kills", WeaponId.Grenade),
            new MissionDef(MissionKind.WeaponKills, 2, 40, "Get 2 knife kills", WeaponId.Knife),
            new MissionDef(MissionKind.WeaponKills, 6, 30, "Get 6 SMG kills", WeaponId.Smg),
            new MissionDef(MissionKind.WeaponKills, 6, 30, "Get 6 assault rifle kills", WeaponId.Rifle),
            new MissionDef(MissionKind.WeaponKills, 1, 40, "Blow up an enemy with a barrel", WeaponId.Barrel),
            new MissionDef(MissionKind.WeaponKills, 5, 25, "Get 5 pistol kills", WeaponId.Pistol),
        };

        private static string Today => DateTime.Now.ToString("yyyy-MM-dd");

        /// <summary>Picks today's missions if the day changed (no save needed: the pick is the same all day).</summary>
        public static void EnsureToday(PlayerProfile p)
        {
            var d = p.Data;
            if (d.MissionDay == Today && d.MissionIds != null && d.MissionIds.Count == Slots
                && d.MissionProgress != null && d.MissionProgress.Count == Slots && d.MissionClaimed != null && d.MissionClaimed.Count == Slots)
                return;
            d.MissionDay = Today;
            d.MissionIds = new List<int>(Slots);
            d.MissionProgress = new List<int> { 0, 0, 0 };
            d.MissionClaimed = new List<bool> { false, false, false };
            d.MissionBonus = false;
            var now = DateTime.Now;
            var rng = new System.Random(now.Year * 1000 + now.DayOfYear);
            d.MissionIds.Add(rng.Next(EasyCount));
            while (d.MissionIds.Count < Slots)
            {
                int id = EasyCount + rng.Next(Pool.Length - EasyCount);
                if (!d.MissionIds.Contains(id)) d.MissionIds.Add(id);
            }
        }

        public static MissionDef Def(PlayerProfile p, int slot)
        {
            EnsureToday(p);
            return Pool[Mathf.Clamp(p.Data.MissionIds[slot], 0, Pool.Length - 1)];
        }

        public static int Progress(PlayerProfile p, int slot) { EnsureToday(p); return p.Data.MissionProgress[slot]; }
        public static bool Done(PlayerProfile p, int slot) => Progress(p, slot) >= Def(p, slot).Target;
        public static bool Claimed(PlayerProfile p, int slot) { EnsureToday(p); return p.Data.MissionClaimed[slot]; }
        public static bool CanClaim(PlayerProfile p, int slot) => Done(p, slot) && !Claimed(p, slot);

        public static int ClaimedCount(PlayerProfile p)
        {
            int n = 0;
            for (int i = 0; i < Slots; i++) if (Claimed(p, i)) n++;
            return n;
        }

        public static bool BonusReady(PlayerProfile p) => ClaimedCount(p) == Slots && !p.Data.MissionBonus;
        public static bool BonusClaimed(PlayerProfile p) { EnsureToday(p); return p.Data.MissionBonus; }

        public static bool AnyClaimable(PlayerProfile p)
        {
            for (int i = 0; i < Slots; i++) if (CanClaim(p, i)) return true;
            return BonusReady(p);
        }

        /// <summary>
        /// Adds progress to today's missions of this kind (Streak keeps the best value instead of adding).
        /// Returns a mission this call completed, or null. Saves only when something completed.
        /// </summary>
        public static MissionDef Report(PlayerProfile p, MissionKind kind, int amount, WeaponId weapon = WeaponId.Pistol)
        {
            if (amount <= 0) return null;
            EnsureToday(p);
            var d = p.Data;
            MissionDef completed = null;
            for (int i = 0; i < Slots; i++)
            {
                var def = Pool[Mathf.Clamp(d.MissionIds[i], 0, Pool.Length - 1)];
                if (def.Kind != kind || (kind == MissionKind.WeaponKills && def.Weapon != weapon)) continue;
                int before = d.MissionProgress[i];
                int after = Mathf.Min(def.Target, kind == MissionKind.Streak ? Mathf.Max(before, amount) : before + amount);
                if (after == before) continue;
                d.MissionProgress[i] = after;
                if (before < def.Target && after >= def.Target) completed = def;
            }
            if (completed != null) p.Save();
            return completed;
        }

        /// <summary>Pays a finished mission; returns the coins (0 if it can't be claimed).</summary>
        public static int Claim(PlayerProfile p, int slot)
        {
            if (!CanClaim(p, slot)) return 0;
            int coins = Def(p, slot).Coins;
            p.Data.Coins += coins;
            p.Data.MissionClaimed[slot] = true;
            p.Save();
            return coins;
        }

        public static int ClaimBonus(PlayerProfile p)
        {
            if (!BonusReady(p)) return 0;
            p.Data.Gems += BonusGems;
            p.Data.MissionBonus = true;
            p.Save();
            return BonusGems;
        }
    }
}
