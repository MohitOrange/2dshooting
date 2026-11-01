using UnityEngine;
using UnityEngine.UI;

namespace NeonBrawl
{
    /// <summary>
    /// Rewards popup on Home with two tabs.
    /// GIFT: the 7-day calendar; today's card glows with turning rays, CLAIM pops it and the coins/gems
    /// fly into the wallet, "x2" doubles the gift after a rewarded ad, then a countdown to tomorrow.
    /// MISSIONS: today's three missions with progress bars, CLAIM when done (PLAY jumps to a match
    /// otherwise) and a gem bonus for finishing all three. The wallet pills stay above the dim.
    /// </summary>
    public sealed class DailyPopup : MonoBehaviour
    {
        public const int TabGift = 0, TabMissions = 1;

        private sealed class Card
        {
            public RectTransform Root;
            public Image Bg, Glow, Rays, Icon, Shade;
            public Text Day, Amount;
            public GameObject Claimed, TodayTag;
        }

        private sealed class MissionRow
        {
            public RectTransform Root;
            public Image Icon, Fill, Shade;
            public Text Title, Count, Reward;
            public GameObject Claim, Play, Done;
        }

        private GameContext _ctx;
        private HomeView _home;
        private CanvasGroup _group;
        private RectTransform _panel, _giftPage, _missionPage, _punch;
        private Card[] _cards;
        private MissionRow[] _rows;
        private MissionRow _bonus;
        private GameObject _claim, _claim2, _tabGiftBadge, _tabMissionBadge;
        private Image _tabGift, _tabMissions, _flash;
        private Text _title, _next, _missionNext, _streak;
        private float _closeT = -1f, _claimT = -1f, _punchT = -1f, _nextTick;
        private int _claimDay, _oldCoins, _oldGems, _tab;

        public bool IsOpen => gameObject.activeSelf;

        public static DailyPopup Build(RectTransform parent, GameContext ctx, HomeView home)
        {
            var root = UIKit.Rect(parent, "DailyPopup");
            UIKit.Stretch(root);
            var p = root.gameObject.AddComponent<DailyPopup>();
            p.Init(ctx, home, root);
            root.gameObject.SetActive(false);
            return p;
        }

        private void Init(GameContext ctx, HomeView home, RectTransform root)
        {
            _ctx = ctx;
            _home = home;
            var art = ctx.Art;
            var cat = ctx.Catalog;
            _group = root.gameObject.AddComponent<CanvasGroup>();

            var dim = UIKit.Image(root, "Dim", art.Square, UIKit.Dim(0f, 0f, 0.03f, 0.72f), false, true);
            UIKit.Stretch(dim.rectTransform, -400f); // reaches past the safe area to the screen edges

            var panel = UIKit.Panel9(root, "Panel", ArtIds.PanelDark, Color.white, 3f, true);
            _panel = panel.rectTransform;
            UIKit.Place(_panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -30f), new Vector2(1480f, 780f));
            _panel.gameObject.AddComponent<PopIn>().Duration = 0.32f;

            var ribbon = UIKit.Panel9(_panel, "Ribbon", ArtIds.PanelOrange, Color.white, 3f);
            UIKit.Place(ribbon.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, 4f), new Vector2(720f, 124f));
            _title = UIKit.Title(ribbon.transform, "Title", "DAILY REWARDS", 64, Color.white, UIKit.Cream);
            UIKit.Stretch(_title.rectTransform);

            var close = UIKit.ArtButton(_panel, "Close", "X", ArtIds.PanelOrange, new Vector2(88f, 88f), 46, Close);
            UIKit.Place((RectTransform)close.transform, new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(-30f, -30f), new Vector2(88f, 88f));
            close.GetComponent<ButtonJuice>().Sound = SfxId.UiBack;

            _tabGift = Tab("GIFT", cat != null ? cat.Reward(ArtIds.RewardChest) : null, -180f, () => ShowTab(TabGift), out _tabGiftBadge);
            _tabMissions = Tab("MISSIONS", cat != null ? cat.Icon(ArtIds.IconTrophy) : null, 180f, () => ShowTab(TabMissions), out _tabMissionBadge);

            _giftPage = UIKit.Rect(_panel, "GiftPage");
            UIKit.Stretch(_giftPage);
            _missionPage = UIKit.Rect(_panel, "MissionPage");
            UIKit.Stretch(_missionPage);
            BuildGiftPage(art, cat);
            BuildMissionPage(art, cat);

            _flash = UIKit.Image(root, "Flash", art.Glow, new Color(1f, 0.9f, 0.6f, 0f));
            UIKit.Place(_flash.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(520f, 520f));
        }

        private Image Tab(string label, Sprite icon, float x, System.Action onClick, out GameObject badge)
        {
            var b = UIKit.ArtButton(_panel, label, label, ArtIds.PanelOrange, new Vector2(340f, 86f), 36, onClick, icon);
            UIKit.Place((RectTransform)b.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(x, -112f), new Vector2(340f, 86f));
            badge = UIKit.Badge(b.transform, "!", UIKit.Red, new Vector2(-12f, -8f), Vector2.one, 26).transform.parent.gameObject;
            badge.AddComponent<Pulse>().Amount = 0.12f;
            return (Image)b.targetGraphic;
        }

        // ------------------------------------------------------------- gift page

        private void BuildGiftPage(ArtLibrary art, ArtCatalog cat)
        {
            var sub = UIKit.Label(_giftPage, "Sub", "Come back every day. Day 7 is a BIG CHEST!", 30, UIKit.Cream, TextAnchor.MiddleCenter);
            UIKit.Place(sub.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -192f), new Vector2(1300f, 40f));
            _streak = UIKit.Label(_giftPage, "Streak", "", 26, UIKit.Gold, TextAnchor.MiddleCenter);
            UIKit.Place(_streak.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -232f), new Vector2(1000f, 36f));

            _cards = new Card[DailyRewards.Days];
            const float w = 164f, wide = 300f, h = 260f, gap = 16f;
            float total = w * 6f + wide + gap * 6f;
            float x = -total * 0.5f;
            for (int i = 0; i < DailyRewards.Days; i++)
            {
                float cw = i == DailyRewards.Days - 1 ? wide : w;
                _cards[i] = MakeCard(i, new Vector2(x + cw * 0.5f, -24f), new Vector2(cw, h), art, cat);
                x += cw + gap;
            }

            _claim = UIKit.ArtButton(_giftPage, "Claim", "CLAIM", ArtIds.PanelGold, new Vector2(440f, 116f), 58, () => ClaimGift(1)).gameObject;
            UIKit.Place((RectTransform)_claim.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), new Vector2(-240f, 100f), new Vector2(440f, 116f));
            Shine.Add((RectTransform)_claim.transform, art);
            _claim2 = UIKit.ArtButton(_giftPage, "ClaimX2", "x2  WATCH AD", ArtIds.PanelBlue, new Vector2(440f, 116f), 40, ClaimGiftDouble,
                cat != null ? cat.Icon(ArtIds.IconPlay) : null).gameObject;
            UIKit.Place((RectTransform)_claim2.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), new Vector2(240f, 100f), new Vector2(440f, 116f));
            _next = UIKit.Title(_giftPage, "Next", "", 44, UIKit.Cream, UIKit.Gold);
            UIKit.Place(_next.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), new Vector2(0f, 100f), new Vector2(1200f, 70f));
        }

        private Card MakeCard(int day, Vector2 pos, Vector2 size, ArtLibrary art, ArtCatalog cat)
        {
            var c = new Card();
            c.Root = UIKit.Rect(_giftPage, "Day" + (day + 1));
            UIKit.Place(c.Root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, size);
            c.Glow = UIKit.Image(c.Root, "Glow", art.Glow, new Color(1f, 0.75f, 0.25f, 0.85f));
            UIKit.Place(c.Glow.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size * 1.9f);
            c.Glow.gameObject.AddComponent<Pulse>().Amount = 0.08f;
            c.Bg = UIKit.Panel9(c.Root, "Bg", ArtIds.PanelBlue, Color.white, 2.6f);
            UIKit.Stretch(c.Bg.rectTransform);

            var g = DailyRewards.Gifts[day];
            Sprite icon = g.Chest ? (cat != null ? cat.Reward(ArtIds.RewardChest3) : null)
                : g.Gems > 0 ? (cat != null ? cat.Reward(ArtIds.RewardGem) : null)
                : (cat != null ? cat.Reward(ArtIds.RewardCoin) : null);
            float iconSize = g.Chest ? 170f : 104f;
            // rays sit under the text so the day label and amount always read
            c.Rays = UIKit.Image(c.Root, "Rays", cat != null ? ArtCatalog.At(cat.Fx1, ArtIds.StarOrange) : art.Star, new Color(1f, 0.95f, 0.7f, 0.75f));
            UIKit.Place(c.Rays.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 8f), new Vector2(iconSize * 2.1f, iconSize * 2.1f));
            c.Rays.gameObject.AddComponent<Spin>().Speed = 30f;
            c.Day = UIKit.Label(c.Root, "Day", "DAY " + (day + 1), 28, Color.white, TextAnchor.MiddleCenter);
            UIKit.Place(c.Day.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -12f), new Vector2(size.x, 40f));
            c.Icon = UIKit.Icon(c.Root, "Icon", icon, new Vector2(iconSize, iconSize), icon == null ? (Color?)(g.Gems > 0 ? UIKit.Sky : UIKit.Gold) : null);
            UIKit.Place(c.Icon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 8f), new Vector2(iconSize, iconSize));
            c.Icon.gameObject.AddComponent<Bob>().Amount = 5f;

            string amount = g.Chest ? g.Coins + " COINS + " + g.Gems + " GEMS" : g.Gems > 0 ? g.Gems + " GEMS" : g.Coins + " COINS";
            c.Amount = UIKit.Label(c.Root, "Amount", amount, g.Chest ? 26 : 28, Color.white, TextAnchor.MiddleCenter);
            UIKit.Place(c.Amount.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 16f), new Vector2(size.x, 40f));

            c.Shade = UIKit.Image(c.Root, "Shade", art.RoundRect, UIKit.Dim(0f, 0f, 0.02f, 0.55f), true);
            UIKit.Stretch(c.Shade.rectTransform, 6f);
            var done = UIKit.Badge(c.Root, "CLAIMED", UIKit.Green, new Vector2(0f, 4f), new Vector2(0.5f, 0.5f), 24);
            c.Claimed = done.transform.parent.gameObject;
            c.Claimed.transform.localRotation = Quaternion.Euler(0f, 0f, 12f);
            var today = UIKit.Badge(c.Root, "TODAY", UIKit.Red, new Vector2(-26f, 14f), new Vector2(1f, 1f), 22);
            c.TodayTag = today.transform.parent.gameObject;
            return c;
        }

        // ------------------------------------------------------------- missions page

        private void BuildMissionPage(ArtLibrary art, ArtCatalog cat)
        {
            _rows = new MissionRow[Missions.Slots];
            for (int i = 0; i < Missions.Slots; i++)
            {
                int slot = i;
                _rows[i] = MakeRow(art, cat, 150f - i * 132f, 118f, false, () => ClaimMission(slot));
            }
            _bonus = MakeRow(art, cat, -240f, 100f, true, ClaimBonus);
            _bonus.Title.text = "BONUS: FINISH ALL 3 MISSIONS";
            _bonus.Reward.text = Missions.BonusGems.ToString();
            _bonus.Icon.sprite = cat != null ? cat.Reward(ArtIds.RewardChest2) : art.Circle;
            _missionNext = UIKit.Label(_missionPage, "Next", "", 28, UIKit.Cream, TextAnchor.MiddleCenter);
            UIKit.Place(_missionNext.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), new Vector2(1200f, 40f));
        }

        private MissionRow MakeRow(ArtLibrary art, ArtCatalog cat, float y, float height, bool bonus, System.Action onClaim)
        {
            var r = new MissionRow();
            var bg = UIKit.Panel9(_missionPage, bonus ? "Bonus" : "Mission", bonus ? ArtIds.PanelWood : ArtIds.PanelDark2, Color.white, 2.6f);
            r.Root = bg.rectTransform;
            UIKit.Place(r.Root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, y), new Vector2(1320f, height));
            r.Icon = UIKit.Icon(r.Root, "Icon", null, new Vector2(150f, 80f));
            UIKit.Place(r.Icon.rectTransform, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(100f, 0f), new Vector2(150f, bonus ? 86f : 80f));
            r.Title = UIKit.Label(r.Root, "Title", "", 34, Color.white, TextAnchor.MiddleLeft);
            UIKit.Place(r.Title.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(196f, 20f), new Vector2(720f, 44f));
            var barBg = UIKit.Image(r.Root, "BarBg", art.RoundRect, new Color(0f, 0f, 0f, 0.6f), true);
            UIKit.Place(barBg.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(196f, -24f), new Vector2(600f, 26f));
            r.Fill = UIKit.Image(barBg.transform, "Fill", art.Square, UIKit.Gold);
            r.Fill.type = Image.Type.Filled;
            r.Fill.fillMethod = Image.FillMethod.Horizontal;
            UIKit.Stretch(r.Fill.rectTransform, 4f);
            r.Count = UIKit.Label(r.Root, "Count", "", 28, UIKit.Cream, TextAnchor.MiddleLeft);
            UIKit.Place(r.Count.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(814f, -24f), new Vector2(220f, 36f));

            var icon = cat != null ? cat.Reward(bonus ? ArtIds.RewardGem : ArtIds.RewardCoin) : null;
            var ri = UIKit.Icon(r.Root, "RewardIcon", icon, new Vector2(58f, 58f), icon == null ? (Color?)(bonus ? UIKit.Sky : UIKit.Gold) : null);
            UIKit.Place(ri.rectTransform, new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-356f, 0f), new Vector2(58f, 58f));
            r.Reward = UIKit.Title(r.Root, "Reward", "", 42, Color.white, UIKit.Cream, TextAnchor.MiddleLeft);
            UIKit.Place(r.Reward.rectTransform, new Vector2(1f, 0.5f), new Vector2(0f, 0.5f), new Vector2(-320f, 0f), new Vector2(90f, 56f));

            var claim = UIKit.ArtButton(r.Root, "Claim", bonus ? "OPEN" : "CLAIM", ArtIds.PanelGold, new Vector2(200f, 84f), 36, onClaim);
            UIKit.Place((RectTransform)claim.transform, new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-130f, 0f), new Vector2(200f, 84f));
            Shine.Add((RectTransform)claim.transform, art);
            r.Claim = claim.gameObject;
            if (!bonus)
            {
                var play = UIKit.ArtButton(r.Root, "Play", "PLAY", ArtIds.PanelBlue, new Vector2(200f, 84f), 34, GoPlay,
                    cat != null ? cat.Icon(ArtIds.IconPlay) : null);
                UIKit.Place((RectTransform)play.transform, new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-130f, 0f), new Vector2(200f, 84f));
                r.Play = play.gameObject;
            }
            r.Shade = UIKit.Image(r.Root, "Shade", art.RoundRect, UIKit.Dim(0f, 0f, 0.02f, 0.5f), true);
            UIKit.Stretch(r.Shade.rectTransform, 6f);
            var done = UIKit.Badge(r.Root, "DONE", UIKit.Green, new Vector2(-130f, 0f), new Vector2(1f, 0.5f), 30);
            r.Done = done.transform.parent.gameObject;
            r.Done.transform.localRotation = Quaternion.Euler(0f, 0f, 10f);
            return r;
        }

        private Sprite MissionIcon(MissionDef d)
        {
            var cat = _ctx.Catalog;
            if (cat == null) return _ctx.Art.Circle;
            switch (d.Kind)
            {
                case MissionKind.Matches: return cat.Icon(ArtIds.IconFlagRed);
                case MissionKind.Wins: return cat.Icon(ArtIds.IconTrophy);
                case MissionKind.Top3: return cat.Icon(ArtIds.IconMedal);
                case MissionKind.Streak: return cat.Reward(ArtIds.RankRed);
                case MissionKind.Multi: return cat.Icon(ArtIds.IconStar);
                case MissionKind.Revenge: return cat.Reward(ArtIds.RankGold);
                case MissionKind.WeaponKills:
                    if (d.Weapon == WeaponId.Grenade) return cat.PickupGrenade;
                    if (d.Weapon == WeaponId.Barrel) return ArtCatalog.At(cat.Shared, ArtIds.SharedBarrelRed);
                    var gun = WeaponArt.Get(cat, d.Weapon);
                    if (gun.Valid) return gun.Sprite;
                    return d.Weapon == WeaponId.Knife ? cat.HudPart(ArtIds.HudKnife) : cat.Icon(ArtIds.IconStar);
                default:
                    var rifle = WeaponArt.Get(cat, WeaponId.Rifle);
                    return rifle.Valid ? rifle.Sprite : cat.Icon(ArtIds.IconStar);
            }
        }

        // ------------------------------------------------------------- open / tabs / close

        /// <summary>Opens on a tab; -1 picks the tab with something to claim (the gift first).</summary>
        public void Open(int tab = -1)
        {
            var p = _ctx.Profile;
            Missions.EnsureToday(p);
            if (tab < 0) tab = !DailyRewards.CanClaim(p) && Missions.AnyClaimable(p) ? TabMissions : TabGift;
            if (!IsOpen)
            {
                gameObject.SetActive(true);
                _ctx.Sfx.PlayUi(SfxId.UiOpen, 0.8f);
            }
            _group.alpha = 1f;
            _group.blocksRaycasts = true;
            _closeT = _claimT = _punchT = -1f;
            _flash.color = Color.clear;
            ShowTab(tab);
        }

        private void ShowTab(int tab)
        {
            _tab = tab;
            _giftPage.gameObject.SetActive(tab == TabGift);
            _missionPage.gameObject.SetActive(tab == TabMissions);
            var cat = _ctx.Catalog;
            var on = cat != null ? cat.Panel(ArtIds.PanelOrange) : null;
            var off = cat != null ? cat.Panel(ArtIds.PanelDark2) : null;
            if (on != null && off != null)
            {
                _tabGift.sprite = tab == TabGift ? on : off;
                _tabMissions.sprite = tab == TabMissions ? on : off;
            }
            _title.text = tab == TabGift ? "DAILY REWARDS" : "DAILY MISSIONS";
            Refresh();
        }

        public void Close()
        {
            if (!IsOpen || _closeT >= 0f) return;
            _closeT = 0f;
            _group.blocksRaycasts = false;
        }

        private void GoPlay()
        {
            gameObject.SetActive(false);
            _ctx.UI.ShowPlay();
        }

        private void Refresh()
        {
            var p = _ctx.Profile;
            RefreshGift(p);
            RefreshMissions(p);
            _tabGiftBadge.SetActive(DailyRewards.CanClaim(p));
            _tabMissionBadge.SetActive(Missions.AnyClaimable(p));
            _nextTick = 0f;
        }

        private void RefreshGift(PlayerProfile p)
        {
            var cat = _ctx.Catalog;
            for (int i = 0; i < _cards.Length; i++)
            {
                var c = _cards[i];
                var st = DailyRewards.StateOf(p, i);
                int panel = st == DailyState.Today ? ArtIds.PanelGold : st == DailyState.Claimed ? ArtIds.PanelDark2 : ArtIds.PanelBlue;
                var s = cat != null ? cat.Panel(panel) : null;
                if (s != null) c.Bg.sprite = s;
                Color text = UIKit.TextOn(panel);
                c.Day.color = text;
                c.Amount.color = text;
                c.Day.GetComponent<Outline>().enabled = text == Color.white;
                c.Amount.GetComponent<Outline>().enabled = text == Color.white;
                c.Glow.gameObject.SetActive(st == DailyState.Today);
                c.Rays.gameObject.SetActive(st == DailyState.Today || (i == DailyRewards.Days - 1 && st == DailyState.Later));
                c.Shade.gameObject.SetActive(st == DailyState.Claimed);
                c.Claimed.SetActive(st == DailyState.Claimed);
                c.TodayTag.SetActive(st == DailyState.Today);
                if (c.Root != _punch) c.Root.localScale = Vector3.one;
            }
            bool can = DailyRewards.CanClaim(p);
            _claim.SetActive(can && _claimT < 0f);
            _claim2.SetActive(can && _claimT < 0f && _ctx.Ads != null && _ctx.Ads.RewardedReady);
            _next.gameObject.SetActive(!can);
            int streak = DailyRewards.Streak(p);
            _streak.text = streak > 0 ? "STREAK: " + streak + (streak == 1 ? " DAY" : " DAYS IN A ROW") : "START YOUR STREAK TODAY!";
        }

        private void RefreshMissions(PlayerProfile p)
        {
            for (int i = 0; i < _rows.Length; i++)
            {
                var r = _rows[i];
                var d = Missions.Def(p, i);
                int prog = Missions.Progress(p, i);
                bool done = prog >= d.Target, claimed = Missions.Claimed(p, i);
                r.Title.text = d.Text.ToUpperInvariant();
                r.Icon.sprite = MissionIcon(d);
                r.Fill.fillAmount = prog / (float)d.Target;
                r.Fill.color = done ? UIKit.Green : UIKit.Gold;
                r.Count.text = prog + " / " + d.Target;
                r.Reward.text = d.Coins.ToString();
                r.Claim.SetActive(done && !claimed);
                r.Play.SetActive(!done);
                r.Shade.gameObject.SetActive(claimed);
                r.Done.SetActive(claimed);
            }
            int n = Missions.ClaimedCount(p);
            bool bonusDone = Missions.BonusClaimed(p);
            _bonus.Fill.fillAmount = n / (float)Missions.Slots;
            _bonus.Fill.color = n == Missions.Slots ? UIKit.Green : UIKit.Gold;
            _bonus.Count.text = n + " / " + Missions.Slots;
            _bonus.Claim.SetActive(Missions.BonusReady(p));
            _bonus.Shade.gameObject.SetActive(bonusDone);
            _bonus.Done.SetActive(bonusDone);
        }

        // ------------------------------------------------------------- claims

        private void ClaimGiftDouble()
        {
            if (_claimT >= 0f || !DailyRewards.CanClaim(_ctx.Profile)) return;
            _ctx.Ads.ShowRewarded("daily_x2", ok => { if (ok) ClaimGift(2); });
        }

        private void ClaimGift(int multiplier)
        {
            var p = _ctx.Profile;
            if (_claimT >= 0f || !DailyRewards.CanClaim(p)) return;
            _claimDay = DailyRewards.NextDay(p);
            _oldCoins = p.Data.Coins;
            _oldGems = p.Data.Gems;
            _home.HoldWallet(_oldCoins, _oldGems);
            DailyRewards.Claim(p, multiplier);
            _ctx.Sfx.PlayUi(SfxId.UiLevelUp, 0.9f);
            _ctx.Haptics.Pulse(30);
            if (multiplier > 1) _ctx.UI.Toast("DOUBLE REWARD!");
            _claimT = 0f;
            Punch(_cards[_claimDay].Root);
            _claim.SetActive(false);
            _claim2.SetActive(false);
        }

        private void ClaimMission(int slot)
        {
            var p = _ctx.Profile;
            if (!Missions.CanClaim(p, slot)) return;
            int oc = p.Data.Coins, og = p.Data.Gems;
            Vector3 from = _rows[slot].Claim.transform.position;
            _home.HoldWallet(oc, og);
            Missions.Claim(p, slot);
            _ctx.Sfx.PlayUi(SfxId.UiLevelUp, 0.8f);
            _ctx.Haptics.Pulse(25);
            Punch(_rows[slot].Root);
            _home.FlyToWallet(from, oc, og);
            Refresh();
        }

        private void ClaimBonus()
        {
            var p = _ctx.Profile;
            if (!Missions.BonusReady(p)) return;
            int oc = p.Data.Coins, og = p.Data.Gems;
            Vector3 from = _bonus.Claim.transform.position;
            _home.HoldWallet(oc, og);
            Missions.ClaimBonus(p);
            _ctx.Sfx.PlayUi(SfxId.UiLevelUp, 1f);
            _ctx.Haptics.Pulse(35);
            Punch(_bonus.Root);
            _home.FlyToWallet(from, oc, og);
            Refresh();
        }

        private void Punch(RectTransform target)
        {
            if (_punch != null) _punch.localScale = Vector3.one;
            _punch = target;
            _punchT = 0f;
            _flash.rectTransform.position = target.position;
        }

        // ------------------------------------------------------------- update

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;

            if (_punchT >= 0f && _punch != null)
            {
                _punchT += dt;
                float big = _punch.rect.width > 600f ? 1.06f : 1.28f; // rows punch less than cards
                float s = _punchT < 0.1f ? Mathf.Lerp(1f, big, _punchT / 0.1f) : Mathf.Lerp(big, 1f, Mathf.Clamp01((_punchT - 0.1f) / 0.25f));
                _punch.localScale = new Vector3(s, s, 1f);
                float f = Mathf.Clamp01(1f - _punchT / 0.45f);
                _flash.color = new Color(1f, 0.9f, 0.6f, UIKit.Shade(f) * 0.95f);
                _flash.rectTransform.localScale = Vector3.one * (0.6f + _punchT * 2f);
                if (_punchT > 0.45f)
                {
                    _punchT = -1f;
                    _punch.localScale = Vector3.one;
                    _punch = null;
                    _flash.color = Color.clear;
                }
            }

            if (_claimT >= 0f)
            {
                float before = _claimT;
                _claimT += dt;
                if (before < 0.2f && _claimT >= 0.2f) RefreshGift(_ctx.Profile); // card turns CLAIMED mid-punch
                if (before < 0.45f && _claimT >= 0.45f)
                {
                    _claimT = -1f;
                    _home.FlyToWallet(_cards[_claimDay].Icon.rectTransform.position, _oldCoins, _oldGems);
                    Refresh();
                }
            }

            if (_closeT >= 0f)
            {
                _closeT += dt;
                float a = 1f - Mathf.Clamp01(_closeT / 0.25f);
                _group.alpha = a;
                _panel.localScale = Vector3.one * Mathf.Lerp(0.9f, 1f, a);
                if (_closeT >= 0.25f)
                {
                    _closeT = -1f;
                    gameObject.SetActive(false);
                }
                return;
            }

            _nextTick -= dt;
            if (_nextTick > 0f) return;
            _nextTick = 0.5f;
            var p = _ctx.Profile;
            var left = DailyRewards.UntilTomorrow;
            if (left.TotalSeconds <= 0.5) { Missions.EnsureToday(p); Refresh(); return; }
            string clock = ((int)left.TotalHours).ToString("00") + ":" + left.Minutes.ToString("00") + ":" + left.Seconds.ToString("00");
            if (_tab == TabGift && _next.gameObject.activeSelf)
            {
                if (DailyRewards.CanClaim(p)) { Refresh(); return; }
                _next.text = "NEXT GIFT IN  " + clock;
            }
            else if (_tab == TabMissions) _missionNext.text = "NEW MISSIONS IN  " + clock;
        }
    }
}
