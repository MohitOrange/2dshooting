using System;
using System.Collections.Generic;
using UnityEngine;

namespace NeonBrawl
{
    public enum ItemKind { Outfit, Headgear, Hair, Back, Map }
    public enum Rarity { Common, Rare, Epic, Legendary }

    /// <summary>Something the player can own: a wardrobe piece or a map. Free items have no price.</summary>
    public sealed class ShopItem
    {
        public ItemKind Kind;
        public int Index;
        public string Name;
        public int Coins;
        public int Gems;
        public Rarity Rarity;

        public string Key => Kind + ":" + Index;
        public bool Free => Coins <= 0 && Gems <= 0;

        public static Color RarityColor(Rarity r)
        {
            switch (r)
            {
                case Rarity.Rare: return new Color(0.3f, 0.62f, 1f);
                case Rarity.Epic: return new Color(0.72f, 0.4f, 1f);
                case Rarity.Legendary: return new Color(1f, 0.74f, 0.2f);
                default: return new Color(0.7f, 0.74f, 0.8f);
            }
        }
    }

    /// <summary>Prices from Docs/GAME_PLAN.md §10. Rewards are small on purpose so unlocks feel earned.</summary>
    public static class Items
    {
        public static readonly List<ShopItem> All = new List<ShopItem>(48);

        static Items()
        {
            // outfits
            int[] outfitCoins = { 0, 0, 500, 1200, 800, 0, 600, 1500 };
            int[] outfitGems = { 0, 0, 0, 0, 0, 120, 0, 0 };
            for (int i = 0; i < 8; i++) Add(ItemKind.Outfit, i, ArtCatalog.BodyNames[i], outfitCoins[i], outfitGems[i]);
            // headgear (0 = none)
            int[] headCoins = { 0, 0, 800, 300, 300, 300, 800, 300, 800, 0, 800, 0, 0, 0, 800, 0 };
            int[] headGems = { 0, 0, 0, 0, 0, 0, 0, 0, 0, 120, 0, 300, 120, 300, 0, 120 };
            for (int i = 0; i < 16; i++) Add(ItemKind.Headgear, i, ArtCatalog.HeadNames[i], headCoins[i], headGems[i]);
            // hair (0 = none)
            Add(ItemKind.Hair, 0, "NONE", 0, 0);
            for (int i = 1; i <= 8; i++) Add(ItemKind.Hair, i, ArtCatalog.HairNames[i - 1], i <= 2 ? 0 : 200, 0);
            // back items (0 = none)
            Add(ItemKind.Back, 0, "NONE", 0, 0);
            int[] backCoins = { 600, 900, 1200, 0 };
            int[] backGems = { 0, 0, 0, 200 };
            for (int i = 1; i <= 4; i++) Add(ItemKind.Back, i, ArtCatalog.BackNames[i - 1], backCoins[i - 1], backGems[i - 1]);
            // maps
            for (int i = 0; i < MapLayouts.All.Length; i++)
                Add(ItemKind.Map, i, MapLayouts.All[i].Name, MapLayouts.All[i].PriceCoins, 0);
        }

        private static void Add(ItemKind kind, int index, string name, int coins, int gems)
        {
            Rarity r = gems >= 200 ? Rarity.Legendary : gems > 0 || coins > 1000 ? Rarity.Epic : coins >= 600 ? Rarity.Rare : Rarity.Common;
            All.Add(new ShopItem { Kind = kind, Index = index, Name = name, Coins = coins, Gems = gems, Rarity = r });
        }

        public static ShopItem Find(ItemKind kind, int index)
        {
            for (int i = 0; i < All.Count; i++)
                if (All[i].Kind == kind && All[i].Index == index) return All[i];
            return null;
        }

        public static void OfKind(ItemKind kind, List<ShopItem> into)
        {
            into.Clear();
            for (int i = 0; i < All.Count; i++)
                if (All[i].Kind == kind) into.Add(All[i]);
        }
    }

    /// <summary>Saved player data (PlayerPrefs JSON). Move to Cloud Save before big spending features.</summary>
    [Serializable]
    public sealed class ProfileData
    {
        public int Version = 1;
        public string Name = "PLAYER";
        public int Coins = 300;
        public int Gems = 10;
        public int Level = 1;
        public int Xp;
        public List<string> Owned = new List<string>();
        public int Body = 1, Head = 1, Hair, Back, Outfit, Skin = 1;
        public int Matches, Wins, Kills;
        public bool NoAds;
        public string AdDay = "";
        public int AdsToday;
        public int LastMap;
        public int DailyStreak;
        public string DailyLast = "";
        public string MissionDay = "";
        public List<int> MissionIds = new List<int>();
        public List<int> MissionProgress = new List<int>();
        public List<bool> MissionClaimed = new List<bool>();
        public bool MissionBonus;
        public List<string> Orders = new List<string>(); // store transaction IDs already granted
    }

    /// <summary>The local player's wallet, wardrobe and progress.</summary>
    public sealed class PlayerProfile
    {
        private const string Key = "nb.profile";
        public const int MaxAdsPerDay = 5;
        public const int AdCoins = 25;

        public static readonly Color[] OutfitColors =
        {
            new Color(0.25f, 0.62f, 1f), new Color(0.92f, 0.3f, 0.25f), new Color(0.35f, 0.78f, 0.3f), new Color(0.98f, 0.8f, 0.2f),
            new Color(0.62f, 0.4f, 0.9f), new Color(1f, 0.55f, 0.2f), new Color(0.2f, 0.82f, 0.8f), new Color(0.95f, 0.45f, 0.7f),
            new Color(0.88f, 0.88f, 0.9f), new Color(0.55f, 0.4f, 0.28f), new Color(0.65f, 0.9f, 0.25f), new Color(0.25f, 0.3f, 0.6f)
        };

        /// <summary>Multipliers for the painted head art (it is already skin-colored).</summary>
        public static readonly Color[] SkinTints =
        {
            Color.white, new Color(0.96f, 0.88f, 0.8f), new Color(0.86f, 0.72f, 0.6f),
            new Color(0.72f, 0.56f, 0.43f), new Color(0.55f, 0.4f, 0.3f), new Color(0.42f, 0.3f, 0.22f)
        };

        public ProfileData Data { get; private set; }
        public event Action Changed;

        public static PlayerProfile Load()
        {
            var p = new PlayerProfile();
            string json = PlayerPrefs.GetString(Key, "");
            try { p.Data = string.IsNullOrEmpty(json) ? new ProfileData() : JsonUtility.FromJson<ProfileData>(json); }
            catch { p.Data = null; }
            if (p.Data == null) p.Data = new ProfileData();
            if (p.Data.Owned == null) p.Data.Owned = new List<string>();
            return p;
        }

        public void Save()
        {
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(Data));
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        public bool Owns(ShopItem item) => item == null || item.Free || Data.Owned.Contains(item.Key);
        public bool OwnsMap(int index) => Owns(Items.Find(ItemKind.Map, index));

        public bool CanAfford(ShopItem item) => item.Gems > 0 ? Data.Gems >= item.Gems : Data.Coins >= item.Coins;

        public bool TryBuy(ShopItem item)
        {
            if (Owns(item)) return true;
            if (!CanAfford(item)) return false;
            if (item.Gems > 0) Data.Gems -= item.Gems;
            else Data.Coins -= item.Coins;
            Data.Owned.Add(item.Key);
            Save();
            return true;
        }

        public void Equip(ShopItem item)
        {
            if (!Owns(item)) return;
            switch (item.Kind)
            {
                case ItemKind.Outfit: Data.Body = item.Index; break;
                case ItemKind.Headgear: Data.Head = item.Index; break;
                case ItemKind.Hair: Data.Hair = item.Index; break;
                case ItemKind.Back: Data.Back = item.Index; break;
                case ItemKind.Map: Data.LastMap = item.Index; break;
            }
            Save();
        }

        public bool IsEquipped(ShopItem item)
        {
            switch (item.Kind)
            {
                case ItemKind.Outfit: return Data.Body == item.Index;
                case ItemKind.Headgear: return Data.Head == item.Index;
                case ItemKind.Hair: return Data.Hair == item.Index;
                case ItemKind.Back: return Data.Back == item.Index;
                default: return Data.LastMap == item.Index;
            }
        }

        public void AddCoins(int n) { Data.Coins = Mathf.Max(0, Data.Coins + n); Save(); }
        public void AddGems(int n) { Data.Gems = Mathf.Max(0, Data.Gems + n); Save(); }

        public static int XpToNext(int level) => 100 + (level - 1) * 60;
        public float Xp01 => Mathf.Clamp01(Data.Xp / (float)XpToNext(Data.Level));

        /// <summary>Adds XP; returns how many levels were gained (each pays 50 coins + 2 gems).</summary>
        public int AddXp(int xp)
        {
            int levels = 0;
            Data.Xp += xp;
            while (Data.Xp >= XpToNext(Data.Level))
            {
                Data.Xp -= XpToNext(Data.Level);
                Data.Level++;
                Data.Coins += 50;
                Data.Gems += 2;
                levels++;
            }
            Save();
            return levels;
        }

        /// <summary>Rewarded ads left today (resets each calendar day).</summary>
        public int AdsLeftToday
        {
            get
            {
                string today = DateTime.Now.ToString("yyyy-MM-dd");
                if (Data.AdDay != today) { Data.AdDay = today; Data.AdsToday = 0; }
                return Mathf.Max(0, MaxAdsPerDay - Data.AdsToday);
            }
        }

        public void CountAd() { _ = AdsLeftToday; Data.AdsToday++; Save(); }

        public FighterLook Look(Color outfit)
        {
            return new FighterLook
            {
                Outfit = outfit,
                Skin = SkinTints[Mathf.Clamp(Data.Skin, 0, SkinTints.Length - 1)],
                Hair = Color.black,
                Body = Data.Body,
                Head = Data.Head,
                HairStyle = Data.Hair,
                Back = Data.Back
            };
        }

        public Color OutfitColor => OutfitColors[Mathf.Clamp(Data.Outfit, 0, OutfitColors.Length - 1)];
    }

    /// <summary>What one match paid out (offline pays half of online, per the plan).</summary>
    public struct MatchReward
    {
        public const int XpPerKill = 6;

        public int Coins, Xp, Place, LevelsGained;
        public bool Won;
        public string Lines;

        public static MatchReward For(MatchController m, bool online)
        {
            var r = new MatchReward();
            if (m == null || m.Local == null || m.Local.IsBot) return r;
            var me = m.Rules.Get(m.Local.Id);
            if (me == null) return r;
            var standings = new List<NeonBrawl.Core.PlayerRecord>(12);
            m.GetStandings(standings);
            r.Place = standings.FindIndex(p => p.Id == me.Id) + 1;
            m.WinnerText(out r.Won);

            int finish = 8, perKill = 2;
            int streak = me.BestStreak >= 10 ? 10 : me.BestStreak >= 5 ? 5 : me.BestStreak >= 3 ? 2 : 0;
            int place = 0;
            if (m.Rules.IsTeamMode) place = r.Won ? 12 : 0;
            else place = r.Place == 1 ? 20 : r.Place == 2 ? 12 : r.Place == 3 ? 6 : 0;
            int total = finish + me.Kills * perKill + streak + place;
            if (!online) total = Mathf.Max(1, total / 2);
            r.Coins = total;
            r.Xp = 20 + me.Kills * XpPerKill + (r.Won ? 30 : 0);
            r.Lines = "Match " + (online ? finish : finish / 2) + "   Kills " + (online ? me.Kills * perKill : me.Kills) +
                      (streak > 0 ? "   Streak " + (online ? streak : streak / 2) : "") + (place > 0 ? "   Place " + (online ? place : place / 2) : "");
            return r;
        }
    }

    /// <summary>Ads: <see cref="AdMobAds"/> on phones, <see cref="MockAds"/> in Windows test builds.</summary>
    public interface IAdsService
    {
        bool RewardedReady { get; }
        /// <summary>Calls back with true only when the player watched to the end.</summary>
        void ShowRewarded(string placement, Action<bool> done);
        /// <summary>Shows a full-screen ad if one is loaded; calls back when it closes (or at once).</summary>
        void ShowInterstitial(string placement, Action done);
        /// <summary>Shows the small banner centred in a free screen area (pixels, origin bottom-left) if it fits.</summary>
        void ShowBanner(UnityEngine.Rect slotPx);
        void HideBanner();
    }

    /// <summary>Real-money purchases: <see cref="IapStore"/> (Unity IAP).</summary>
    public interface IStoreService
    {
        bool Ready { get; }
        /// <summary>Prices arrived from the store, or something was bought or restored.</summary>
        event Action Changed;
        string PriceOf(string productId);
        void Buy(string productId, Action<bool> done);
    }

    /// <summary>Test ads for Windows builds: the UI shows a 3-second fake ad, then grants the reward.</summary>
    public sealed class MockAds : IAdsService
    {
        public bool RewardedReady => true;
        public Action<string, Action<bool>> Present;
        public void ShowRewarded(string placement, Action<bool> done)
        {
            if (Present != null) Present(placement, done);
            else done?.Invoke(true);
        }
        public void ShowInterstitial(string placement, Action done) => done?.Invoke();
        public void ShowBanner(UnityEngine.Rect slotPx) { }
        public void HideBanner() { }
    }
}
