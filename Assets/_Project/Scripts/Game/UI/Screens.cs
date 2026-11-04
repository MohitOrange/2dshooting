using System;
using System.Collections.Generic;
using NeonBrawl.Core;
using UnityEngine;
using UnityEngine.UI;

namespace NeonBrawl
{
    /// <summary>
    /// Header shared by the menu screens: back button + title on the left, coins/gems/settings on the right.
    /// Everything sits on one row (same top, same height, centred on one line) with even gaps.
    /// </summary>
    public sealed class TopBar
    {
        /// <summary>The layout grid every screen shares: side margin, row top, row height, gap.</summary>
        public const float Margin = 32f, RowTop = 26f, RowH = 88f, Gap = 18f, PillW = 250f;

        public Text Coins, Gems, Title;
        /// <summary>The coin and gem icons in the pills (where reward flights land).</summary>
        public RectTransform CoinIcon, GemIcon;

        public static TopBar Build(RectTransform safe, GameContext ctx, string title, Action onBack)
        {
            var bar = new TopBar();
            var cat = ctx.Catalog;
            var topLeft = new Vector2(0f, 1f);
            var topRight = new Vector2(1f, 1f);
            if (onBack != null)
            {
                var back = UIKit.GlassButton(safe, "Back", new Vector2(110f, RowH), onBack);
                var brt = (RectTransform)back.transform;
                UIKit.Place(brt, topLeft, topLeft, new Vector2(Margin, -RowTop), new Vector2(110f, RowH));
                var ic = UIKit.Icon(brt, "Icon", HudArt.Get(HudArt.Icon.Back), new Vector2(60f, 60f));
                UIKit.Place(ic.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(60f, 60f));
                back.GetComponent<ButtonJuice>().Sound = SfxId.UiBack;
            }
            if (!string.IsNullOrEmpty(title))
            {
                bar.Title = UIKit.Title(safe, "Title", title, 64, UIKit.Cream, UIKit.Gold, TextAnchor.MiddleLeft);
                UIKit.Place(bar.Title.rectTransform, topLeft, topLeft, new Vector2(onBack != null ? Margin + 110f + 26f : Margin, -RowTop + 2f), new Vector2(900f, RowH));
            }
            var settings = UIKit.GlassButton(safe, "Settings", new Vector2(RowH, RowH), () => ctx.UI.ShowSettings(true));
            var srt = (RectTransform)settings.transform;
            UIKit.Place(srt, topRight, topRight, new Vector2(-Margin, -RowTop), new Vector2(RowH, RowH));
            var gear = UIKit.Icon(srt, "Icon", cat != null ? cat.Icon(ArtIds.IconGear) : null, new Vector2(58f, 58f));
            UIKit.Place(gear.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(58f, 58f));
            float gemsRight = Margin + RowH + Gap;
            bar.Gems = UIKit.CurrencyPill(safe, "Gems", cat != null ? cat.Reward(ArtIds.RewardGem) : null, UIKit.Sky, gemsRight, RowTop, RowH, PillW, () => ctx.UI.ShowShop());
            bar.Coins = UIKit.CurrencyPill(safe, "Coins", cat != null ? cat.Reward(ArtIds.RewardCoin) : null, UIKit.Gold, gemsRight + PillW + Gap + 24f, RowTop, RowH, PillW, () => ctx.UI.ShowShop());
            bar.CoinIcon = (RectTransform)bar.Coins.transform.parent.Find("Icon");
            bar.GemIcon = (RectTransform)bar.Gems.transform.parent.Find("Icon");
            return bar;
        }

        public void Refresh(PlayerProfile p)
        {
            Coins.text = UIKit.Thousands(p.Data.Coins);
            Gems.text = UIKit.Thousands(p.Data.Gems);
        }
    }

    /// <summary>Base for full-screen menu pages: refreshes the wallet when shown or when it changes.</summary>
    public abstract class ScreenView : MonoBehaviour
    {
        protected GameContext Ctx;
        protected TopBar Bar;
        /// <summary>While rewards fly into the wallet, the pills count up themselves instead of jumping.</summary>
        protected bool WalletHeld;

        protected virtual void OnEnable()
        {
            if (Ctx == null) return;
            Ctx.Profile.Changed += OnProfileChanged;
            OnProfileChanged();
            OnShow();
        }

        protected virtual void OnDisable()
        {
            if (Ctx != null) Ctx.Profile.Changed -= OnProfileChanged;
        }

        protected virtual void OnShow() { }

        protected virtual void OnProfileChanged()
        {
            if (!WalletHeld) Bar?.Refresh(Ctx.Profile);
        }

        protected static void Dim(RectTransform root, float alphaTop, float alphaBottom)
        {
            var dim = UIKit.GradientFill(root, "Dim", UIKit.Dim(0.02f, 0.03f, 0.08f, alphaTop * 0.85f), UIKit.Dim(0.03f, 0.02f, 0.06f, alphaBottom * 0.85f), true);
            dim.transform.SetAsFirstSibling();
        }
    }

    // =====================================================================================
    /// <summary>
    /// A window onto a map: the sharp high-resolution render of the real map (baked by
    /// Neon Brawl ▸ Art ▸ Bake Map Previews), or, if it is missing, its ground tiles with a few props.
    /// </summary>
    public static class MapThumb
    {
        private static readonly Sprite[] Baked = new Sprite[8];

        private static Sprite Preview(int index)
        {
            if (index < 0 || index >= Baked.Length) return null;
            if (Baked[index] == null) Baked[index] = Resources.Load<Sprite>("MapPreviews/map_" + index);
            return Baked[index];
        }

        public static RectTransform Build(Transform parent, GameContext ctx, int mapIndex, Vector2 size)
        {
            var art = ctx.Art;
            var cat = ctx.Catalog;
            var def = MapLayouts.Get(mapIndex);
            var mask = UIKit.Rect(parent, "Thumb");
            mask.sizeDelta = size;
            mask.gameObject.AddComponent<RectMask2D>();
            var shot = Preview(mapIndex);
            if (shot != null)
            {
                // fill the window (crop the longer side), keep the picture's shape
                var img = UIKit.Image(mask, "Shot", shot, Color.white);
                var rt = img.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                var fit = img.gameObject.AddComponent<AspectRatioFitter>();
                fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                fit.aspectRatio = shot.rect.width / Mathf.Max(1f, shot.rect.height);
                img.gameObject.AddComponent<KenBurns>();
                var vshade = UIKit.GradientFill(mask, "Shade", new Color(0f, 0f, 0f, 0f), new Color(0f, 0f, 0f, 0.4f));
                vshade.raycastTarget = false;
                return mask;
            }
            var theme = cat != null ? cat.Theme(def.Theme) : null;
            var look = ThemeLook.For(def.Theme);
            if (theme == null || theme.Tiles.Length < 4)
            {
                var bg = UIKit.Image(mask, "Bg", art.Square, def.Accent * 0.5f);
                UIKit.Stretch(bg.rectTransform);
                return mask;
            }
            float tile = size.y / 2f;
            int cols = Mathf.CeilToInt(size.x / tile);
            for (int y = 0; y < 2; y++)
                for (int x = 0; x < cols; x++)
                {
                    int t = y == 0 && x % 3 == 1 ? look.Path : look.Ground;
                    var img = UIKit.Image(mask, "Tile", theme.Tiles[t], Color.white);
                    UIKit.Place(img.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(x * tile, y * tile), new Vector2(tile + 1f, tile + 1f));
                }
            Sprite tree = look.Trees.Length > 0 ? ArtCatalog.At(theme.Props, look.Trees[0]) : null;
            Sprite rock = look.Rocks.Length > 0 ? ArtCatalog.At(theme.Props, look.Rocks[0]) : null;
            Sprite crate = look.Crates.Length > 0 ? ArtCatalog.At(theme.Props, look.Crates[0]) : ArtCatalog.At(cat.Shared, ArtIds.SharedCrate);
            Sprite cover = look.Covers.Length > 0 ? ArtCatalog.At(theme.Props, look.Covers[0]) : null;
            Prop(mask, tree, new Vector2(0.14f, 0.08f), size.y * 0.95f);
            Prop(mask, rock, new Vector2(0.84f, 0.22f), size.y * 0.7f);
            Prop(mask, cover, new Vector2(0.5f, 0.06f), size.y * 0.42f);
            Prop(mask, crate, new Vector2(0.62f, 0.12f), size.y * 0.36f);
            var shade = UIKit.GradientFill(mask, "Shade", new Color(0f, 0f, 0f, 0f), new Color(0f, 0f, 0f, 0.55f));
            shade.raycastTarget = false;
            return mask;
        }

        private static void Prop(RectTransform mask, Sprite s, Vector2 anchor, float size)
        {
            if (s == null) return;
            var img = UIKit.Icon(mask, "Prop", s, new Vector2(size, size));
            UIKit.Place(img.rectTransform, anchor, new Vector2(0.5f, 0f), Vector2.zero, new Vector2(size, size));
        }
    }

    /// <summary>A very slow drift and zoom on a map picture so the card feels alive.</summary>
    public sealed class KenBurns : MonoBehaviour
    {
        private float _t;

        private void OnEnable() => _t = UnityEngine.Random.value * 20f;

        private void Update()
        {
            _t += Time.unscaledDeltaTime;
            float s = 1.06f + Mathf.Sin(_t * 0.12f) * 0.04f;
            transform.localScale = new Vector3(s, s, 1f);
            ((RectTransform)transform).anchoredPosition = new Vector2(Mathf.Sin(_t * 0.09f) * 12f, Mathf.Cos(_t * 0.11f) * 5f);
        }
    }

    // =====================================================================================
    /// <summary>
    /// Home: the night camp lobby behind (your soldier by the campfire), framed by one even grid. The top row
    /// holds the profile and the wallet, the sharp logo sits top-left, the selected map + PLAY + ONLINE are
    /// stacked in the right column (one width, one right edge), and four glass buttons of one size form the
    /// dock bottom-left (locker, shop, daily gift, missions). Every piece keeps the same margin to the edge.
    /// </summary>
    public sealed class HomeView : ScreenView
    {
        // the grid: the side margin and the top row come from TopBar; one gap between stacked pieces
        private const float M = TopBar.Margin, Gap = 20f;
        private const float ColW = 600f, MapH = 300f, PlayH = 180f, OnlineH = 112f;
        private const float TileW = 170f, TileH = 150f, ProfileW = 520f, XpW = 370f;

        private Text _name, _level, _xp, _mapName, _mapInfo;
        private RectTransform _xpFill, _thumbHolder;
        private int _thumbMap = -1;
        private DailyPopup _daily;
        private RewardFlight _flight;
        private GameObject _dailyBadge, _missionBadge, _dailyGlow, _friendBadge;
        private Text _friendBadgeText;
        private float _dailyOpenAt = -1f;
        private int _flightsLeft, _shownCoins, _shownGems;
        private static bool _dailyAutoShown;

        private static readonly Vector2 Mid = new Vector2(0.5f, 0.5f);

        public void Build(GameContext ctx, RectTransform safe)
        {
            Ctx = ctx;
            var art = ctx.Art;
            var root = (RectTransform)transform;

            // keep the camp visible: soft bands top and bottom, dark corners
            var vignette = UIKit.Image(root, "Vignette", art.Vignette, new Color(0f, 0f, 0.02f, 0.85f));
            UIKit.Stretch(vignette.rectTransform, -80f);
            var top = UIKit.GradientFill(root, "TopBand", new Color(0f, 0f, 0.02f, 0.72f), new Color(0f, 0f, 0.02f, 0f));
            top.rectTransform.anchorMin = new Vector2(0f, 0.74f);
            var bottom = UIKit.GradientFill(root, "BottomBand", new Color(0f, 0f, 0.02f, 0f), new Color(0f, 0f, 0.02f, 0.75f));
            bottom.rectTransform.anchorMax = new Vector2(1f, 0.32f);
            vignette.transform.SetAsFirstSibling();
            top.transform.SetSiblingIndex(1);
            bottom.transform.SetSiblingIndex(2);
            Embers.Add(root, art, 22, new Color(1f, 0.7f, 0.35f, 0.45f));

            Bar = TopBar.Build(safe, ctx, null, null);
            BuildProfile(safe, ctx);
            BuildLogo(safe, art);
            BuildRightColumn(safe, ctx);
            BuildDock(safe, ctx);
            FriendsStrip.Build(safe, ctx, new Vector2(M, M + TileH + Gap + 6f));

            // the rewards popup lives in the safe area; the wallet pills are moved above it so you see coins land
            _daily = DailyPopup.Build(safe, ctx, this);
            Bar.Coins.transform.parent.SetAsLastSibling();
            Bar.Gems.transform.parent.SetAsLastSibling();
            _flight = RewardFlight.Create(root); // last: rewards fly over everything
        }

        /// <summary>Top row, left: rank badge with your level, your name and the XP bar. Tap for your profile.</summary>
        private void BuildProfile(RectTransform safe, GameContext ctx)
        {
            var art = ctx.Art;
            var cat = ctx.Catalog;
            var chip = UIKit.GlassButton(safe, "Profile", new Vector2(ProfileW, TopBar.RowH), () => ctx.UI.ShowProfile());
            var rt = (RectTransform)chip.transform;
            UIKit.Place(rt, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(M, -TopBar.RowTop), new Vector2(ProfileW, TopBar.RowH));
            rt.gameObject.AddComponent<SlideIn>().From = new Vector2(-600f, 0f);
            var badge = UIKit.Icon(rt, "Rank", cat != null ? cat.Reward(ArtIds.RankGold) : null, new Vector2(100f, 100f));
            UIKit.Place(badge.rectTransform, new Vector2(0f, 0.5f), Mid, new Vector2(58f, -2f), new Vector2(100f, 100f));
            _level = UIKit.Label(badge.transform, "Level", "1", 34, Color.white, TextAnchor.MiddleCenter);
            UIKit.Stretch(_level.rectTransform);
            _name = UIKit.Label(rt, "Name", "PLAYER", 34, Color.white, TextAnchor.MiddleLeft);
            UIKit.Place(_name.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(118f, 17f), new Vector2(XpW, 40f));
            var xpBg = UIKit.Image(rt, "XpBar", art.Pill, new Color(0.3f, 0.4f, 0.62f, 0.38f), true);
            UIKit.Place(xpBg.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(118f, -19f), new Vector2(XpW, 26f));
            var fill = UIKit.Image(xpBg.transform, "Fill", art.Pill, Color.white, true);
            _xpFill = fill.rectTransform;
            UIKit.Place(_xpFill, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(3f, 0f), new Vector2(20f, 20f));
            var g = fill.gameObject.AddComponent<UIGradient>();
            g.Top = new Color(0.7f, 0.97f, 1f);
            g.Bottom = new Color(0.12f, 0.55f, 1f);
            _xp = UIKit.Label(xpBg.transform, "Xp", "", 19, Color.white, TextAnchor.MiddleCenter);
            UIKit.Stretch(_xp.rectTransform);
            _xp.rectTransform.anchoredPosition = new Vector2(0f, 1f);
        }

        /// <summary>Top-left, under the profile: the logo on slowly turning light rays.</summary>
        private void BuildLogo(RectTransform safe, ArtLibrary art)
        {
            const float w = 560f, h = 280f;
            var logoRoot = UIKit.Rect(safe, "LogoRoot");
            UIKit.Place(logoRoot, new Vector2(0f, 1f), Mid, new Vector2(M + w * 0.5f, -(TopBar.RowTop + TopBar.RowH + 34f + h * 0.5f)), new Vector2(w, h));
            var rays = UIKit.Image(logoRoot, "Rays", GlassArt.Rays, new Color(1f, 0.78f, 0.45f, 0.17f));
            UIKit.Place(rays.rectTransform, Mid, Mid, Vector2.zero, new Vector2(840f, 840f));
            rays.gameObject.AddComponent<Spin>().Speed = 6f;
            var glow = UIKit.Image(logoRoot, "Glow", art.Glow, new Color(1f, 0.5f, 0.2f, 0.3f));
            UIKit.Place(glow.rectTransform, Mid, Mid, Vector2.zero, new Vector2(760f, 400f));
            var logo = UIKit.Logo(logoRoot, w);
            UIKit.Place(logo, Mid, Mid, Vector2.zero, new Vector2(600f, 300f));
            logo.gameObject.AddComponent<Bob>().Amount = 6f;
            logoRoot.gameObject.AddComponent<PopIn>().Duration = 0.45f;
        }

        /// <summary>Right column, one width and one right edge: the selected map, PLAY, ONLINE ROOMS.</summary>
        private void BuildRightColumn(RectTransform safe, GameContext ctx)
        {
            var art = ctx.Art;
            var br = new Vector2(1f, 0f);
            float onlineY = M, playY = onlineY + OnlineH + Gap, mapY = playY + PlayH + Gap;

            // map card: a rounded window onto the chosen map, the match setup, its name and CHANGE
            var card = UIKit.GlassButton(safe, "MapCard", new Vector2(ColW, MapH), () => ctx.UI.ShowPlay());
            var crt = (RectTransform)card.transform;
            UIKit.Place(crt, br, br, new Vector2(-M, mapY), new Vector2(ColW, MapH));
            crt.gameObject.AddComponent<SlideIn>().From = new Vector2(760f, 0f);
            var frame = UIKit.Image(crt, "Frame", art.RoundRect, Color.white, true);
            UIKit.Stretch(frame.rectTransform, 12f);
            frame.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            _thumbHolder = UIKit.Rect(frame.transform, "ThumbHolder");
            UIKit.Stretch(_thumbHolder);
            var shade = UIKit.GradientFill(frame.transform, "Shade", new Color(0f, 0f, 0.03f, 0f), new Color(0f, 0f, 0.03f, 0.85f));
            shade.rectTransform.anchorMax = new Vector2(1f, 0.6f);
            var chip = UIKit.Image(frame.transform, "Mode", art.Pill, new Color(0.02f, 0.03f, 0.08f, 0.78f), true);
            UIKit.Place(chip.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(14f, -14f), new Vector2(330f, 46f));
            _mapInfo = UIKit.Label(chip.transform, "T", "", 22, UIKit.Cream, TextAnchor.MiddleCenter);
            UIKit.Stretch(_mapInfo.rectTransform, 4f);
            _mapName = UIKit.Title(frame.transform, "MapName", "", 50, Color.white, UIKit.Cream, TextAnchor.MiddleLeft);
            UIKit.Place(_mapName.rectTransform, Vector2.zero, Vector2.zero, new Vector2(22f, 12f), new Vector2(ColW - 24f - 44f, 72f));
            var change = UIKit.Image(frame.transform, "Change", art.Pill, Color.white, true);
            UIKit.Place(change.rectTransform, Vector2.one, Vector2.one, new Vector2(-14f, -14f), new Vector2(150f, 46f));
            var cg = change.gameObject.AddComponent<UIGradient>();
            cg.Top = new Color(1f, 0.95f, 0.66f);
            cg.Bottom = new Color(1f, 0.64f, 0.2f);
            var ct = UIKit.Label(change.transform, "T", "CHANGE", 24, UIKit.TextOn(ArtIds.PanelGold), TextAnchor.MiddleCenter, false);
            UIKit.Stretch(ct.rectTransform, 6f);

            // PLAY: the biggest thing on screen, gently breathing, with the match type inside it
            var playHolder = UIKit.Rect(safe, "PlayHolder");
            UIKit.Place(playHolder, br, Mid, new Vector2(-M - ColW * 0.5f, playY + PlayH * 0.5f), new Vector2(ColW, PlayH));
            playHolder.gameObject.AddComponent<Pulse>().Amount = 0.018f;
            UIKit.DropShadow(playHolder, 0.6f, 12f);
            var play = UIKit.ArtButton(playHolder, "Play", null, ArtIds.PanelGold, new Vector2(ColW, PlayH), 0, () => ctx.UI.ShowPlay());
            var prt = (RectTransform)play.transform;
            UIKit.Stretch(prt);
            Shine.Add(prt, art);
            Color ink = UIKit.TextOn(ArtIds.PanelGold);
            var pl = UIKit.Label(prt, "Label", "PLAY", 112, ink, TextAnchor.MiddleCenter, false);
            UIKit.Place(pl.rectTransform, Mid, Mid, new Vector2(0f, 18f), new Vector2(ColW - 80f, 120f));
            var lift = pl.gameObject.AddComponent<Shadow>();
            lift.effectColor = new Color(1f, 1f, 1f, 0.5f);
            lift.effectDistance = new Vector2(0f, -3f);
            var sub = UIKit.Label(prt, "Sub", "VS BOTS  ·  OFFLINE", 26, new Color(ink.r, ink.g, ink.b, 0.78f), TextAnchor.MiddleCenter, false);
            UIKit.Place(sub.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 24f), new Vector2(ColW - 100f, 34f));

            // ONLINE ROOMS: same width, same right edge
            var onHolder = UIKit.Rect(safe, "OnlineHolder");
            UIKit.Place(onHolder, br, br, new Vector2(-M, onlineY), new Vector2(ColW, OnlineH));
            onHolder.gameObject.AddComponent<SlideIn>().From = new Vector2(760f, 0f);
            UIKit.DropShadow(onHolder, 0.5f, 10f);
            var online = UIKit.ArtButton(onHolder, "Online", null, ArtIds.PanelBlue, new Vector2(ColW, OnlineH), 0, () => ctx.App.OpenOnline());
            var ort = (RectTransform)online.transform;
            UIKit.Stretch(ort);
            var oic = UIKit.Icon(ort, "Icon", HudArt.Get(HudArt.Icon.Person), new Vector2(68f, 68f));
            UIKit.Place(oic.rectTransform, new Vector2(0f, 0.5f), Mid, new Vector2(72f, 2f), new Vector2(68f, 68f));
            var ot = UIKit.Label(ort, "Label", "ONLINE ROOMS", 42, Color.white, TextAnchor.MiddleCenter);
            UIKit.Place(ot.rectTransform, Mid, Mid, new Vector2(30f, 13f), new Vector2(ColW - 220f, 50f));
            var os = UIKit.Label(ort, "Sub", "PLAY WITH FRIENDS", 22, UIKit.Sky, TextAnchor.MiddleCenter);
            UIKit.Place(os.rectTransform, Mid, Mid, new Vector2(30f, -25f), new Vector2(ColW - 220f, 30f));
            UIKit.Badge(ort, "NEW", UIKit.Green, new Vector2(-40f, 54f), new Vector2(1f, 0.5f));
        }

        /// <summary>Bottom-left dock: four glass buttons of one size with even gaps, each with its own colour.</summary>
        private void BuildDock(RectTransform safe, GameContext ctx)
        {
            var art = ctx.Art;
            var cat = ctx.Catalog;
            string[] labels = { "LOCKER", "SHOP", "DAILY", "MISSIONS", "FRIENDS", "POWERS" };
            Sprite[] icons =
            {
                cat != null ? cat.Icon(ArtIds.IconShirt) : null,
                cat != null ? cat.Icon(ArtIds.IconCart) : null,
                cat != null ? cat.Reward(ArtIds.RewardChest) : null,
                cat != null ? cat.Icon(ArtIds.IconTrophy) : null,
                HudArt.Get(HudArt.Icon.Person),
                PowerArt.Badge(PowerId.Turbo),
            };
            Color[] accents =
            {
                new Color(0.42f, 0.9f, 0.45f), new Color(0.38f, 0.68f, 1f), new Color(1f, 0.78f, 0.3f), new Color(1f, 0.46f, 0.36f),
                new Color(0.78f, 0.56f, 1f), new Color(1f, 0.42f, 0.72f),
            };
            Action[] actions =
            {
                () => ctx.UI.ShowLocker(), () => ctx.UI.ShowShop(),
                () => _daily.Open(DailyPopup.TabGift), () => _daily.Open(DailyPopup.TabMissions),
                () => ctx.UI.ShowFriends(ctx.Friends.Incoming.Count > 0 ? FriendsPopup.TabRequests : FriendsPopup.TabFriends),
                () => ctx.UI.ShowPowers(),
            };
            for (int i = 0; i < labels.Length; i++)
            {
                var pos = new Vector2(M + i * (TileW + Gap), M);
                if (i == 2)
                {
                    // today's gift waits: a warm glow breathes behind the DAILY button
                    var glow = UIKit.Image(safe, "DailyGlow", art.Glow, new Color(1f, 0.72f, 0.2f, 0.7f));
                    UIKit.Place(glow.rectTransform, Vector2.zero, Mid, pos + new Vector2(TileW * 0.5f, TileH * 0.5f), new Vector2(320f, 300f));
                    glow.gameObject.AddComponent<Pulse>().Amount = 0.12f;
                    _dailyGlow = glow.gameObject;
                }
                var b = UIKit.GlassButton(safe, labels[i], new Vector2(TileW, TileH), actions[i]);
                var rt = (RectTransform)b.transform;
                UIKit.Place(rt, Vector2.zero, Vector2.zero, pos, new Vector2(TileW, TileH));
                Color a = accents[i];
                var stripe = UIKit.Image(rt, "Accent", art.Pill, a, true);
                UIKit.Place(stripe.rectTransform, new Vector2(0.5f, 1f), Mid, new Vector2(0f, -7f), new Vector2(TileW - 76f, 6f));
                var halo = UIKit.Image(rt, "Halo", art.Glow, new Color(a.r, a.g, a.b, 0.32f));
                UIKit.Place(halo.rectTransform, Mid, Mid, new Vector2(0f, 18f), new Vector2(150f, 130f));
                var ic = UIKit.Icon(rt, "Icon", icons[i], new Vector2(84f, 84f));
                UIKit.Place(ic.rectTransform, Mid, Mid, new Vector2(0f, 18f), new Vector2(84f, 84f));
                var cap = UIKit.Label(rt, "Caption", labels[i], 26, UIKit.Cream, TextAnchor.MiddleCenter);
                UIKit.Place(cap.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 12f), new Vector2(TileW - 20f, 32f));
                var s = rt.gameObject.AddComponent<SlideIn>();
                s.From = new Vector2(0f, -260f);
                s.Delay = 0.05f * i;
                if (i == 5)
                {
                    // the power tile cycles through all ten badges and says NEW
                    ic.gameObject.AddComponent<PowerCycle>();
                    UIKit.Badge(rt, "NEW", UIKit.Green, new Vector2(-14f, -12f), Vector2.one, 22);
                }
                else if (i >= 2)
                {
                    var text = UIKit.Badge(rt, "!", UIKit.Red, new Vector2(-14f, -12f), Vector2.one, 30);
                    var badge = text.transform.parent.gameObject;
                    badge.AddComponent<Pulse>().Amount = 0.12f;
                    if (i == 2) _dailyBadge = badge;
                    else if (i == 3) _missionBadge = badge;
                    else { _friendBadge = badge; _friendBadgeText = text; badge.SetActive(false); }
                }
            }
        }

        /// <summary>
        /// Freezes the wallet pills at the amounts before a claim; <see cref="FlyToWallet"/> then counts them up.
        /// A claim during another flight keeps counting from what is shown.
        /// </summary>
        public void HoldWallet(int coins, int gems)
        {
            if (!WalletHeld)
            {
                _shownCoins = coins;
                _shownGems = gems;
            }
            WalletHeld = true;
            Bar.Coins.text = UIKit.Thousands(_shownCoins);
            Bar.Gems.text = UIKit.Thousands(_shownGems);
        }

        /// <summary>Coins and gems burst from a point and fly into the pills, which count up as they land.</summary>
        public void FlyToWallet(Vector3 fromWorld, int oldCoins, int oldGems)
        {
            var d = Ctx.Profile.Data;
            int dc = d.Coins - oldCoins, dg = d.Gems - oldGems;
            var cat = Ctx.Catalog;
            if (dc <= 0 && dg <= 0)
            {
                if (_flightsLeft <= 0) ReleaseWallet();
                return;
            }
            if (dc > 0)
            {
                _flightsLeft++;
                _flight.Fly(cat != null ? cat.Reward(ArtIds.RewardCoin) : Ctx.Art.Circle, fromWorld, Bar.CoinIcon, Mathf.Clamp(dc / 6, 6, 16),
                    (a, n) =>
                    {
                        _shownCoins += dc * a / n - dc * (a - 1) / n;
                        Bar.Coins.text = UIKit.Thousands(_shownCoins);
                        if ((a & 1) == 1 || a == n) Ctx.Sfx.PlayUi(SfxId.UiCoin, 0.4f);
                    }, OnFlightDone);
            }
            if (dg > 0)
            {
                _flightsLeft++;
                _flight.Fly(cat != null ? cat.Reward(ArtIds.RewardGem) : Ctx.Art.Circle, fromWorld, Bar.GemIcon, Mathf.Clamp(dg * 2, 4, 10),
                    (a, n) =>
                    {
                        _shownGems += dg * a / n - dg * (a - 1) / n;
                        Bar.Gems.text = UIKit.Thousands(_shownGems);
                        Ctx.Sfx.PlayUi(SfxId.UiTap, 0.5f);
                    }, OnFlightDone);
            }
        }

        private void OnFlightDone()
        {
            if (--_flightsLeft > 0) return;
            ReleaseWallet();
        }

        private void ReleaseWallet()
        {
            _flightsLeft = 0;
            WalletHeld = false;
            Bar.Refresh(Ctx.Profile);
            RefreshDaily();
        }

        private void RefreshDaily()
        {
            var p = Ctx.Profile;
            bool gift = DailyRewards.CanClaim(p);
            _dailyBadge.SetActive(gift);
            _dailyGlow.SetActive(gift);
            _missionBadge.SetActive(Missions.AnyClaimable(p));
        }

        protected override void OnShow()
        {
            Ctx.App.EnterLobby();
            int map = Ctx.Profile.Data.LastMap;
            if (map != _thumbMap)
            {
                _thumbMap = map;
                for (int i = _thumbHolder.childCount - 1; i >= 0; i--) Destroy(_thumbHolder.GetChild(i).gameObject);
                var thumb = MapThumb.Build(_thumbHolder, Ctx, map, new Vector2(ColW - 24f, MapH - 24f));
                UIKit.Stretch(thumb);
            }
            _mapName.text = MapLayouts.Get(map).Name;
            var s = Ctx.Prefs.LastSettings;
            _mapInfo.text = (s.Mode == MatchMode.TeamDeathmatch ? "TEAM BATTLE" : "FREE FOR ALL") + "  ·  " + s.BotCount + (s.BotCount == 1 ? " BOT" : " BOTS");
            RefreshDaily();
            // first visit of the session with a gift waiting: open the calendar once the camp intro settles
            if (!_dailyAutoShown && DailyRewards.CanClaim(Ctx.Profile))
            {
                _dailyAutoShown = true;
                _dailyOpenAt = Time.unscaledTime + 1.4f;
            }
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            _dailyOpenAt = -1f;
            if (WalletHeld) { WalletHeld = false; Bar.Refresh(Ctx.Profile); }
        }

        private void LateUpdate()
        {
            // friend requests waiting: a number on the FRIENDS button
            int requests = Ctx.Friends != null ? Ctx.Friends.Incoming.Count : 0;
            if (_friendBadge != null && _friendBadge.activeSelf != requests > 0) _friendBadge.SetActive(requests > 0);
            if (requests > 0 && _friendBadgeText.text != requests.ToString()) _friendBadgeText.text = requests.ToString();
            if (_dailyOpenAt > 0f && Time.unscaledTime >= _dailyOpenAt)
            {
                _dailyOpenAt = -1f;
                _daily.Open();
            }
        }


        protected override void OnProfileChanged()
        {
            base.OnProfileChanged();
            var p = Ctx.Profile;
            _name.text = p.Data.Name;
            _level.text = p.Data.Level.ToString();
            float xp = p.Xp01;
            _xpFill.gameObject.SetActive(xp > 0.001f);
            _xpFill.sizeDelta = new Vector2(Mathf.Max(20f, (XpW - 6f) * xp), 20f);
            _xp.text = p.Data.Xp + " / " + PlayerProfile.XpToNext(p.Data.Level) + " XP";
            if (_dailyBadge != null && !WalletHeld) RefreshDaily();
        }
    }

    // =====================================================================================
    /// <summary>Offline setup: pick one of the 5 maps (buy locked ones), match options, START.</summary>
    public sealed class PlayView : ScreenView
    {
        private static readonly int[] BotOptions = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 };
        private static readonly float[] TimeOptions = { 120f, 180f, 300f, 480f, 600f };
        private static readonly int[] KillOptions = { 10, 20, 30, 50, 0 };

        private readonly List<MapCard> _cards = new List<MapCard>(5);
        private MatchSettings _s;
        private bool _autoPlay;

        private sealed class MapCard
        {
            public RectTransform Root;
            public Image Frame, Lock;
            public GameObject LockGroup;
            public Text Price;
        }

        public void Build(GameContext ctx, RectTransform safe)
        {
            Ctx = ctx;
            var art = ctx.Art;
            var cat = ctx.Catalog;
            var root = (RectTransform)transform;
            Dim(root, 0.9f, 0.75f);
            Bar = TopBar.Build(safe, ctx, "CHOOSE MAP", () => ctx.UI.Back());

            var row = UIKit.Rect(safe, "Maps");
            UIKit.Place(row, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 150f), new Vector2(1700f, 300f));
            for (int i = 0; i < MapLayouts.All.Length; i++) _cards.Add(BuildCard(row, i, art, cat));

            // options
            var panel = UIKit.Panel9(safe, "Options", ArtIds.PanelDark, Color.white, 2.8f);
            UIKit.Place(panel.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-290f, 24f), new Vector2(1220f, 380f));
            panel.gameObject.AddComponent<SlideIn>().From = new Vector2(0f, -300f);
            _s = ctx.Prefs.LastSettings;
            float y = -48f;
            const float step = 80f;
            float left = 30f, right = 630f;
            new OptionRow(panel.transform, "MODE", new[] { "FREE FOR ALL", "TEAM BATTLE" }, (int)_s.Mode, left, y, i => _s.Mode = (MatchMode)i);
            new OptionRow(panel.transform, "BOTS", Array.ConvertAll(BotOptions, b => b.ToString()), Array.IndexOf(BotOptions, Mathf.Clamp(_s.BotCount, 1, 11)), right, y, i => _s.BotCount = BotOptions[i]);
            y -= step;
            new OptionRow(panel.transform, "BOT SKILL", new[] { "EASY", "NORMAL", "HARD" }, _s.Difficulty, left, y, i => _s.Difficulty = i);
            new OptionRow(panel.transform, "TIME", new[] { "2 MIN", "3 MIN", "5 MIN", "8 MIN", "10 MIN" }, Mathf.Max(0, Array.IndexOf(TimeOptions, _s.TimeLimit)), right, y, i => _s.TimeLimit = TimeOptions[i]);
            y -= step;
            new OptionRow(panel.transform, "KILL LIMIT", new[] { "10", "20", "30", "50", "NONE" }, Mathf.Max(0, Array.IndexOf(KillOptions, _s.KillLimit)), left, y, i => _s.KillLimit = KillOptions[i]);
            new OptionRow(panel.transform, "TIME OF DAY", new[] { "DAY", "NIGHT" }, _s.Night ? 1 : 0, right, y, i => _s.Night = i == 1);
            y -= step;
            new OptionRow(panel.transform, "LINE OF SIGHT", new[] { "ON", "OFF" }, _s.Fog ? 0 : 1, left, y, i => _s.Fog = i == 0);
            new OptionRow(panel.transform, "YOU PLAY AS", new[] { "PLAYER", "AI (DEMO)" }, 0, right, y, i => _autoPlay = i == 1);

            var holder = UIKit.Rect(safe, "StartHolder");
            UIKit.Place(holder, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(640f, 60f), new Vector2(460f, 200f));
            holder.gameObject.AddComponent<Pulse>().Amount = 0.03f;
            var start = UIKit.ArtButton(holder, "Start", "START", ArtIds.PanelGold, new Vector2(460f, 200f), 70, StartMatch, cat != null ? cat.Icon(ArtIds.IconPlay) : null);
            UIKit.Stretch((RectTransform)start.transform);
            Shine.Add((RectTransform)start.transform, art);
        }

        private MapCard BuildCard(RectTransform row, int index, ArtLibrary art, ArtCatalog cat)
        {
            var def = MapLayouts.All[index];
            var c = new MapCard();
            var frame = UIKit.Panel9(row, "Card" + index, ArtIds.PanelDark, Color.white, 2.6f, true);
            UIKit.Place(frame.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2((index - 2) * 335f, 0f), new Vector2(315f, 290f));
            c.Root = frame.rectTransform;
            c.Frame = frame;
            var btn = frame.gameObject.AddComponent<Button>();
            btn.targetGraphic = frame;
            int captured = index;
            btn.onClick.AddListener(() => OnCard(captured));
            frame.gameObject.AddComponent<ButtonJuice>();
            var slide = frame.gameObject.AddComponent<SlideIn>();
            slide.From = new Vector2(0f, 260f);
            slide.Delay = index * 0.05f;

            var thumb = MapThumb.Build(frame.transform, Ctx, index, new Vector2(283f, 170f));
            UIKit.Place(thumb, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -16f), new Vector2(283f, 170f));
            var nm = UIKit.Label(frame.transform, "Name", def.Name, 28, Color.white, TextAnchor.MiddleCenter);
            UIKit.Place(nm.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 58f), new Vector2(300f, 36f));
            var tag = UIKit.Label(frame.transform, "Tag", def.Tagline, 18, new Color(0.82f, 0.88f, 1f), TextAnchor.MiddleCenter, true);
            UIKit.Place(tag.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 26f), new Vector2(300f, 26f));

            // lock overlay with price
            var lockGroup = UIKit.Image(frame.transform, "Locked", art.RoundRect, new Color(0f, 0f, 0f, 0.62f), true);
            UIKit.Stretch(lockGroup.rectTransform, 10f);
            c.LockGroup = lockGroup.gameObject;
            var coin = UIKit.Icon(lockGroup.transform, "Coin", cat != null ? cat.Reward(ArtIds.RewardCoin) : null, new Vector2(56f, 56f));
            UIKit.Place(coin.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-70f, 30f), new Vector2(56f, 56f));
            c.Price = UIKit.Label(lockGroup.transform, "Price", def.PriceCoins.ToString("N0"), 40, UIKit.Gold, TextAnchor.MiddleLeft);
            UIKit.Place(c.Price.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f), new Vector2(-34f, 30f), new Vector2(180f, 50f));
            var unlock = UIKit.Label(lockGroup.transform, "Unlock", "TAP TO UNLOCK", 22, Color.white, TextAnchor.MiddleCenter);
            UIKit.Place(unlock.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -22f), new Vector2(300f, 30f));
            return c;
        }

        protected override void OnShow()
        {
            RefreshCards();
            Ctx.App.PreviewMap(Ctx.Profile.Data.LastMap);
        }

        private void RefreshCards()
        {
            var p = Ctx.Profile;
            for (int i = 0; i < _cards.Count; i++)
            {
                var c = _cards[i];
                bool owned = p.OwnsMap(i);
                bool selected = p.Data.LastMap == i;
                c.LockGroup.SetActive(!owned);
                c.Frame.sprite = Ctx.Catalog != null ? Ctx.Catalog.Panel(selected ? ArtIds.PanelGold : ArtIds.PanelDark) : Ctx.Art.RoundRect;
                c.Root.GetComponent<ButtonJuice>().BaseScale = selected ? 1.07f : 1f;
            }
        }

        private void OnCard(int i)
        {
            var p = Ctx.Profile;
            var item = Items.Find(ItemKind.Map, i);
            if (!p.Owns(item))
            {
                if (p.TryBuy(item))
                {
                    Ctx.Sfx.PlayUi(SfxId.UiOpen, 1f);
                    Ctx.UI.Toast(MapLayouts.All[i].Name + " UNLOCKED!");
                }
                else
                {
                    Ctx.Sfx.PlayUi(SfxId.UiError, 1f);
                    Ctx.UI.Toast("Need " + (item.Coins - p.Data.Coins).ToString("N0") + " more coins — play matches to earn them!");
                    return;
                }
            }
            p.Data.LastMap = i;
            p.Save();
            RefreshCards();
            Ctx.App.PreviewMap(i);
        }

        private void StartMatch()
        {
            if (!Ctx.Profile.OwnsMap(Ctx.Profile.Data.LastMap)) { Ctx.UI.Toast("Unlock this map first"); return; }
            Ctx.App.StartMatch(_s, _autoPlay, Ctx.Profile.Data.LastMap);
        }
    }

    // =====================================================================================
    /// <summary>Locker: try on outfits, headgear, hair and back items; buy with coins or gems; pick colors.</summary>
    public sealed class LockerView : ScreenView
    {
        private static readonly ItemKind[] Tabs = { ItemKind.Outfit, ItemKind.Headgear, ItemKind.Hair, ItemKind.Back };
        private static readonly string[] TabNames = { "OUTFIT", "HEADGEAR", "HAIR", "BACK" };

        private CharacterPreview _preview;
        private Text _itemName, _rarity;
        private Button _action;
        private Text _actionLabel;
        private Image _actionIcon;
        private RectTransform _grid;
        private readonly List<Image> _tabFrames = new List<Image>(4);
        private readonly List<ShopItem> _items = new List<ShopItem>(24);
        private readonly List<Image> _cardFrames = new List<Image>(24);
        private readonly List<GameObject> _cardChecks = new List<GameObject>(24);
        private readonly List<GameObject> _cardPrices = new List<GameObject>(24);
        private int _tab;
        private ShopItem _selected;

        public void Build(GameContext ctx, RectTransform safe)
        {
            Ctx = ctx;
            var art = ctx.Art;
            var cat = ctx.Catalog;
            Dim((RectTransform)transform, 0.92f, 0.8f);
            Bar = TopBar.Build(safe, ctx, "LOCKER", () => ctx.UI.Back());

            _preview = CharacterPreview.Create(safe, art, cat, 320f, true);
            UIKit.Place((RectTransform)_preview.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-560f, 40f), Vector2.zero);
            _itemName = UIKit.Title(safe, "ItemName", "", 46, Color.white, new Color(0.8f, 0.86f, 1f));
            UIKit.Place(_itemName.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-560f, -230f), new Vector2(700f, 60f));
            _rarity = UIKit.Label(safe, "Rarity", "", 26, Color.white, TextAnchor.MiddleCenter);
            UIKit.Place(_rarity.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-560f, -276f), new Vector2(500f, 34f));

            // outfit colors + skin tones
            for (int i = 0; i < PlayerProfile.OutfitColors.Length; i++)
            {
                int ci = i;
                var dot = UIKit.Image(safe, "Color" + i, art.Circle, PlayerProfile.OutfitColors[i], false, true);
                UIKit.Place(dot.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-560f + (i - 5.5f) * 58f, -336f), new Vector2(50f, 50f));
                var b = dot.gameObject.AddComponent<Button>();
                b.onClick.AddListener(() => { ctx.Profile.Data.Outfit = ci; ctx.Profile.Save(); RefreshPreview(); });
                dot.gameObject.AddComponent<ButtonJuice>();
            }
            for (int i = 0; i < PlayerProfile.SkinTints.Length; i++)
            {
                int si = i;
                Color c = new Color(1f, 0.84f, 0.68f) * PlayerProfile.SkinTints[i];
                c.a = 1f;
                var dot = UIKit.Image(safe, "Skin" + i, art.Circle, c, false, true);
                UIKit.Place(dot.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-560f + (i - 2.5f) * 62f, -398f), new Vector2(50f, 50f));
                var b = dot.gameObject.AddComponent<Button>();
                b.onClick.AddListener(() => { ctx.Profile.Data.Skin = si; ctx.Profile.Save(); RefreshPreview(); });
                dot.gameObject.AddComponent<ButtonJuice>();
            }

            // right side: tabs + grid + action
            var panel = UIKit.Panel9(safe, "ItemsPanel", ArtIds.PanelDark, Color.white, 2.8f);
            UIKit.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(340f, -50f), new Vector2(1120f, 800f));
            panel.gameObject.AddComponent<SlideIn>().From = new Vector2(600f, 0f);
            for (int t = 0; t < Tabs.Length; t++)
            {
                int ti = t;
                var tb = UIKit.ArtButton(panel.transform, "Tab" + t, TabNames[t], ArtIds.PanelSlate, new Vector2(250f, 78f), 28, () => SelectTab(ti));
                UIKit.Place((RectTransform)tb.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f + t * 266f, -22f), new Vector2(250f, 78f));
                _tabFrames.Add((Image)tb.targetGraphic);
            }
            var view = UIKit.Rect(panel.transform, "Viewport");
            UIKit.Place(view, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -114f), new Vector2(1070f, 520f));
            view.gameObject.AddComponent<RectMask2D>();
            var hit = view.gameObject.AddComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0.001f);
            _grid = UIKit.Rect(view, "Grid");
            _grid.anchorMin = new Vector2(0f, 1f);
            _grid.anchorMax = new Vector2(1f, 1f);
            _grid.pivot = new Vector2(0.5f, 1f);
            _grid.anchoredPosition = Vector2.zero;
            _grid.sizeDelta = Vector2.zero; // stretch exactly to the viewport width
            var gl = _grid.gameObject.AddComponent<GridLayoutGroup>();
            gl.cellSize = new Vector2(196f, 236f);
            gl.spacing = new Vector2(18f, 18f);
            gl.padding = new RectOffset(10, 10, 6, 6);
            gl.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            gl.constraintCount = 5;
            var fit = _grid.gameObject.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var scroll = view.gameObject.AddComponent<ScrollRect>();
            scroll.content = _grid;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.scrollSensitivity = 30f;

            _action = UIKit.ArtButton(panel.transform, "Action", "EQUIP", ArtIds.PanelGold, new Vector2(520f, 120f), 50, OnAction);
            UIKit.Place((RectTransform)_action.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 26f), new Vector2(520f, 120f));
            _actionLabel = _action.GetComponentInChildren<Text>();
            _actionIcon = UIKit.Icon(_action.transform, "Cost", null, new Vector2(70f, 70f));
            UIKit.Place(_actionIcon.rectTransform, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(70f, 2f), new Vector2(70f, 70f));
        }

        protected override void OnShow() => SelectTab(_tab);

        private void SelectTab(int t)
        {
            _tab = t;
            for (int i = 0; i < _tabFrames.Count; i++)
                _tabFrames[i].sprite = Ctx.Catalog != null ? Ctx.Catalog.Panel(i == t ? ArtIds.PanelGold : ArtIds.PanelSlate) : Ctx.Art.RoundRect;
            Items.OfKind(Tabs[t], _items);
            for (int i = _grid.childCount - 1; i >= 0; i--) Destroy(_grid.GetChild(i).gameObject);
            _cardFrames.Clear();
            _cardChecks.Clear();
            _cardPrices.Clear();
            for (int i = 0; i < _items.Count; i++) BuildItemCard(_items[i], i);
            _selected = null;
            for (int i = 0; i < _items.Count; i++)
                if (Ctx.Profile.IsEquipped(_items[i])) { _selected = _items[i]; break; }
            if (_selected == null && _items.Count > 0) _selected = _items[0];
            RefreshCards();
            RefreshPreview();
        }

        private void BuildItemCard(ShopItem item, int index)
        {
            var cat = Ctx.Catalog;
            var art = Ctx.Art;
            int panel = item.Rarity == Rarity.Legendary ? ArtIds.PanelSand : item.Rarity == Rarity.Epic ? ArtIds.PanelSteel : item.Rarity == Rarity.Rare ? ArtIds.PanelBlue : ArtIds.PanelSlate;
            var frame = UIKit.Panel9(_grid, "Item", panel, Color.white, 2.4f, true);
            if (item.Rarity == Rarity.Epic) frame.color = new Color(0.85f, 0.65f, 1f);
            var btn = frame.gameObject.AddComponent<Button>();
            btn.targetGraphic = frame;
            btn.onClick.AddListener(() => Select(item));
            frame.gameObject.AddComponent<ButtonJuice>();
            _cardFrames.Add(frame);

            // icon
            var iconRoot = UIKit.Rect(frame.transform, "IconRoot");
            UIKit.Place(iconRoot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 22f), new Vector2(150f, 140f));
            var look = Ctx.Profile.Look(Ctx.Profile.OutfitColor);
            switch (item.Kind)
            {
                case ItemKind.Outfit:
                    if (cat != null && item.Index < cat.BodyTint.Length)
                    {
                        var a = UIKit.Icon(iconRoot, "Cloth", cat.BodyTint[item.Index], new Vector2(140f, 140f), look.Outfit);
                        a.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                        var d = UIKit.Icon(iconRoot, "Detail", ArtCatalog.At(cat.BodyDetail, item.Index), new Vector2(140f, 140f));
                        d.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                    }
                    break;
                case ItemKind.Headgear:
                    if (cat != null)
                    {
                        UIKit.Icon(iconRoot, "Head", ArtCatalog.At(cat.Heads, 0), new Vector2(110f, 110f), look.Skin).rectTransform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                        if (item.Index > 0) UIKit.Icon(iconRoot, "Gear", ArtCatalog.At(cat.Heads, item.Index), new Vector2(118f, 118f)).rectTransform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                    }
                    break;
                case ItemKind.Hair:
                    if (cat != null)
                    {
                        UIKit.Icon(iconRoot, "Head", ArtCatalog.At(cat.Heads, 0), new Vector2(110f, 110f), look.Skin).rectTransform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                        if (item.Index > 0) UIKit.Icon(iconRoot, "Hair", ArtCatalog.At(cat.Hair, item.Index - 1), new Vector2(118f, 118f), look.Skin).rectTransform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                    }
                    break;
                case ItemKind.Back:
                    if (cat != null && item.Index > 0) UIKit.Icon(iconRoot, "Back", ArtCatalog.At(cat.Back, item.Index - 1), new Vector2(120f, 130f));
                    break;
            }
            if (item.Index == 0 && item.Kind == ItemKind.Back)
            {
                var none = UIKit.Label(iconRoot, "None", "NONE", 30, new Color(1f, 1f, 1f, 0.7f), TextAnchor.MiddleCenter);
                UIKit.Stretch(none.rectTransform);
            }

            var name = UIKit.Label(frame.transform, "Name", item.Name, 19, Color.white, TextAnchor.MiddleCenter);
            UIKit.Place(name.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 46f), new Vector2(190f, 26f));

            // price row or owned check
            var price = UIKit.Rect(frame.transform, "Price");
            UIKit.Place(price, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 10f), new Vector2(180f, 36f));
            bool gems = item.Gems > 0;
            var pi = UIKit.Icon(price, "Icon", cat != null ? cat.Reward(gems ? ArtIds.RewardGem : ArtIds.RewardCoin) : null, new Vector2(34f, 34f));
            UIKit.Place(pi.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-46f, 0f), new Vector2(34f, 34f));
            var pt = UIKit.Label(price, "Amount", (gems ? item.Gems : item.Coins).ToString("N0"), 26, gems ? UIKit.Sky : UIKit.Gold, TextAnchor.MiddleLeft);
            UIKit.Place(pt.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f), new Vector2(-24f, 0f), new Vector2(120f, 36f));
            _cardPrices.Add(price.gameObject);
            var check = UIKit.Icon(frame.transform, "Equipped", cat != null ? cat.Icon(ArtIds.IconStar) : null, new Vector2(54f, 54f));
            UIKit.Place(check.rectTransform, new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(-24f, -24f), new Vector2(54f, 54f));
            _cardChecks.Add(check.gameObject);
        }

        private void Select(ShopItem item)
        {
            _selected = item;
            if (Ctx.Profile.Owns(item))
            {
                Ctx.Profile.Equip(item);
                Ctx.Sfx.PlayUi(SfxId.Pickup, 0.7f);
            }
            RefreshCards();
            RefreshPreview();
        }

        private void RefreshCards()
        {
            var p = Ctx.Profile;
            for (int i = 0; i < _items.Count; i++)
            {
                var it = _items[i];
                bool owned = p.Owns(it);
                _cardPrices[i].SetActive(!owned);
                _cardChecks[i].SetActive(p.IsEquipped(it));
                _cardFrames[i].transform.GetChild(0).localScale = _selected == it ? new Vector3(1.12f, 1.12f, 1f) : Vector3.one;
            }
        }

        private void RefreshPreview()
        {
            var p = Ctx.Profile;
            var look = p.Look(p.OutfitColor);
            if (_selected != null)
            {
                switch (_selected.Kind)
                {
                    case ItemKind.Outfit: look.Body = _selected.Index; break;
                    case ItemKind.Headgear: look.Head = _selected.Index; break;
                    case ItemKind.Hair: look.HairStyle = _selected.Index; if (_selected.Index > 0) look.Head = 0; break;
                    case ItemKind.Back: look.Back = _selected.Index; break;
                }
            }
            _preview.Set(look);
            if (_selected == null) return;
            _itemName.text = _selected.Name;
            _rarity.text = _selected.Rarity.ToString().ToUpperInvariant();
            _rarity.color = ShopItem.RarityColor(_selected.Rarity);
            bool owned = p.Owns(_selected);
            bool gems = _selected.Gems > 0;
            _actionIcon.enabled = !owned;
            if (!owned) _actionIcon.sprite = Ctx.Catalog != null ? Ctx.Catalog.Reward(gems ? ArtIds.RewardGem : ArtIds.RewardCoin) : null;
            _actionLabel.text = owned ? (p.IsEquipped(_selected) ? "EQUIPPED" : "EQUIP") : "BUY  " + (gems ? _selected.Gems : _selected.Coins).ToString("N0");
            _action.interactable = !(owned && p.IsEquipped(_selected));
        }

        private void OnAction()
        {
            if (_selected == null) return;
            var p = Ctx.Profile;
            if (!p.Owns(_selected))
            {
                if (!p.TryBuy(_selected))
                {
                    Ctx.Sfx.PlayUi(SfxId.UiError, 1f);
                    Ctx.UI.Toast(_selected.Gems > 0 ? "Not enough gems" : "Not enough coins — win matches to earn more!");
                    return;
                }
                Ctx.Sfx.PlayUi(SfxId.UiCoin, 1f);
                Ctx.Sfx.PlayUi(SfxId.UiOpen, 1f);
                Ctx.UI.Toast(_selected.Name + " UNLOCKED!");
            }
            if (_selected.Kind == ItemKind.Hair && _selected.Index > 0) p.Data.Head = 0;
            p.Equip(_selected);
            RefreshCards();
            RefreshPreview();
        }

        protected override void OnProfileChanged()
        {
            base.OnProfileChanged();
        }
    }

    // =====================================================================================
    /// <summary>Shop: free coins (rewarded ad), gem packs and Remove Ads (store seam), coin/gem chests.</summary>
    public sealed class ShopView : ScreenView
    {
        private Text _adLeft;
        private GameObject _chestPopup;
        private Image _chestImage, _rewardIcon;
        private Text _rewardText;
        private float _chestT = -1f;
        private string _pendingText;
        private Sprite _pendingIcon;
        private readonly Dictionary<string, Button> _buy = new Dictionary<string, Button>();

        public void Build(GameContext ctx, RectTransform safe)
        {
            Ctx = ctx;
            var art = ctx.Art;
            var cat = ctx.Catalog;
            Dim((RectTransform)transform, 0.92f, 0.82f);
            Embers.Add((RectTransform)transform, art, 18, new Color(1f, 0.85f, 0.4f, 0.35f));
            Bar = TopBar.Build(safe, ctx, "SHOP", () => ctx.UI.Back());

            // one grid under the top bar, same side margins as the bar, centred in the space that is left:
            // row 1 = three equal columns, row 2 = six half columns (two gem packs sit exactly under one card)
            var area = UIKit.Rect(safe, "Grid");
            UIKit.Stretch(area);
            area.offsetMin = new Vector2(TopBar.Margin, TopBar.Margin);
            area.offsetMax = new Vector2(-TopBar.Margin, -(TopBar.RowTop + TopBar.RowH + 34f));
            var block = UIKit.Rect(area, "Block");
            block.anchorMin = new Vector2(0f, 0.5f);
            block.anchorMax = new Vector2(1f, 0.5f);
            block.pivot = new Vector2(0.5f, 0.5f);
            block.sizeDelta = new Vector2(0f, RowTopH + Gap + RowLowH);
            block.anchoredPosition = Vector2.zero;
            var top = Row(block, true);
            var low = Row(block, false);

            // free coins (rewarded video)
            var free = Card(top, 3, 0, 1, RowTopH, ArtIds.PanelBlue, "FREE COINS", cat != null ? cat.Reward(ArtIds.RewardCoin) : null, new Color(1f, 0.85f, 0.3f), 0);
            _adLeft = SubLine(free, "", ArtIds.PanelBlue);
            var watch = UIKit.ArtButton(free, "Watch", "WATCH  +" + PlayerProfile.AdCoins, ArtIds.PanelGold, new Vector2(320f, 88f), 36, WatchAd,
                cat != null ? cat.Icon(ArtIds.IconPlay) : null);
            UIKit.Place((RectTransform)watch.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 26f), new Vector2(320f, 88f));
            Shine.Add((RectTransform)watch.transform, art);

            // chests
            var wood = Card(top, 3, 1, 1, RowTopH, ArtIds.PanelWood, "WOODEN CHEST", cat != null ? cat.Reward(ArtIds.RewardChest2) : null, new Color(1f, 0.7f, 0.35f), 1);
            SubLine(wood, "COMMON OR RARE GEAR", ArtIds.PanelWood);
            Price(wood, cat != null ? cat.Reward(ArtIds.RewardCoin) : null, "500", () => OpenChest(false), new Vector2(300f, 88f));
            var royal = Card(top, 3, 2, 1, RowTopH, ArtIds.PanelSand, "ROYAL CHEST", cat != null ? cat.Reward(ArtIds.RewardChestIron) : null, new Color(1f, 1f, 0.85f), 2);
            SubLine(royal, "EPIC OR LEGENDARY GEAR", ArtIds.PanelSand);
            Price(royal, cat != null ? cat.Reward(ArtIds.RewardGem) : null, "60", () => OpenChest(true), new Vector2(300f, 88f));

            // gem packs and Remove Ads (real money, Unity IAP)
            var ids = IapStore.GemPackIds;
            for (int i = 0; i < ids.Length; i++)
            {
                bool blue = i < 2;
                var g = Card(low, 6, i, 1, RowLowH, ArtIds.PanelSlate, IapStore.GemPackAmounts[i].ToString("N0") + " GEMS",
                    cat != null ? cat.Reward(blue ? ArtIds.RewardGem : ArtIds.RewardRuby) : null, blue ? new Color(0.35f, 0.7f, 1f) : new Color(1f, 0.35f, 0.4f), 3 + i);
                string id = ids[i];
                _buy[id] = Price(g, null, ctx.Store.PriceOf(id), () => BuyProduct(id), new Vector2(230f, 80f));
            }
            var noAds = Card(low, 6, 4, 2, RowLowH, ArtIds.PanelOrange, "REMOVE ADS", cat != null ? cat.Icon(ArtIds.IconShield) : null, new Color(0.5f, 0.75f, 1f), 7);
            SubLine(noAds, "NO ADS BETWEEN MATCHES", ArtIds.PanelOrange);
            _buy[IapStore.RemoveAds] = Price(noAds, null, ctx.Store.PriceOf(IapStore.RemoveAds), () => BuyProduct(IapStore.RemoveAds), new Vector2(300f, 80f));
            UIKit.Badge(noAds, "+" + IapStore.RemoveAdsGems + " GEMS", UIKit.Blue, new Vector2(-74f, -34f), new Vector2(1f, 1f));

            BuildChestPopup((RectTransform)transform, art, cat);
        }

        private const float RowTopH = 420f, RowLowH = 370f, Gap = 24f;

        private static RectTransform Row(RectTransform block, bool top)
        {
            var r = UIKit.Rect(block, top ? "RowTop" : "RowLow");
            r.anchorMin = new Vector2(0f, top ? 1f : 0f);
            r.anchorMax = new Vector2(1f, top ? 1f : 0f);
            r.pivot = new Vector2(0.5f, top ? 1f : 0f);
            r.sizeDelta = new Vector2(0f, top ? RowTopH : RowLowH);
            r.anchoredPosition = Vector2.zero;
            return r;
        }

        /// <summary>
        /// A shop card in column <paramref name="col"/> (spanning <paramref name="span"/>) of an <paramref name="n"/>-column
        /// row with even gaps: soft shadow, painted panel, title at the top and the icon on a coloured glow.
        /// Returns the card container; content added to it draws above the panel.
        /// </summary>
        private RectTransform Card(RectTransform row, int n, int col, int span, float h, int panel, string title, Sprite icon, Color glow, int order)
        {
            var c = UIKit.Rect(row, title);
            c.anchorMin = new Vector2(col / (float)n, 0f);
            c.anchorMax = new Vector2((col + span) / (float)n, 1f);
            c.offsetMin = new Vector2(col * Gap / n, 0f);
            c.offsetMax = new Vector2(-(n - col - span) * Gap / n, 0f);
            c.gameObject.AddComponent<PopIn>().Delay = 0.03f * order;
            UIKit.DropShadow(c, 0.55f, 12f);
            var face = UIKit.Panel9(c, "Panel", panel, Color.white, 3f);
            UIKit.Stretch(face.rectTransform);
            Color ink = UIKit.TextOn(panel);
            bool dark = ink != Color.white;
            var t = UIKit.Label(c, "Title", title, n == 3 ? 40 : 34, ink, TextAnchor.MiddleCenter, !dark);
            UIKit.Place(t.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(0f, 52f));
            t.rectTransform.anchorMin = new Vector2(0f, 1f);
            t.rectTransform.anchorMax = new Vector2(1f, 1f);
            t.rectTransform.sizeDelta = new Vector2(-36f, 52f);
            if (dark)
            {
                var lift = t.gameObject.AddComponent<Shadow>();
                lift.effectColor = new Color(1f, 1f, 1f, 0.45f);
                lift.effectDistance = new Vector2(0f, -2f);
            }
            if (icon != null)
            {
                // icon centred in the space between the text lines and the buy button
                float size = n == 3 ? 170f : 140f;
                float y = (h * 0.5f - 104f + (-h * 0.5f + 118f)) * 0.5f;
                var halo = UIKit.Image(c, "Glow", Ctx.Art.Glow, new Color(glow.r, glow.g, glow.b, dark ? 0.55f : 0.4f));
                UIKit.Place(halo.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, y), new Vector2(size * 1.9f, size * 1.6f));
                var ic = UIKit.Icon(c, "Icon", icon, new Vector2(size, size));
                UIKit.Place(ic.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, y), new Vector2(size, size));
                ic.gameObject.AddComponent<Bob>().Amount = 5f;
            }
            return c;
        }

        /// <summary>The small line under a card title (what you get, or how many free videos are left).</summary>
        private static Text SubLine(RectTransform card, string text, int panel)
        {
            Color ink = UIKit.TextOn(panel);
            bool dark = ink != Color.white;
            var s = UIKit.Label(card, "Sub", text, 22, dark ? new Color(ink.r, ink.g, ink.b, 0.85f) : UIKit.Cream, TextAnchor.MiddleCenter, !dark);
            s.rectTransform.anchorMin = new Vector2(0f, 1f);
            s.rectTransform.anchorMax = new Vector2(1f, 1f);
            s.rectTransform.pivot = new Vector2(0.5f, 1f);
            s.rectTransform.sizeDelta = new Vector2(-40f, 30f);
            s.rectTransform.anchoredPosition = new Vector2(0f, -70f);
            return s;
        }

        private Button Price(Transform card, Sprite icon, string text, Action onClick, Vector2 size)
        {
            var b = UIKit.ArtButton(card, "Buy", text, ArtIds.PanelGold, size, 34, onClick, icon);
            UIKit.Place((RectTransform)b.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 26f), size);
            var label = b.transform.Find("Label");
            if (label != null)
            {
                var t = label.GetComponent<Text>();
                t.resizeTextForBestFit = true;
                t.resizeTextMinSize = 18;
                t.resizeTextMaxSize = 34;
            }
            return b;
        }

        protected override void OnShow()
        {
            int left = Ctx.Profile.AdsLeftToday;
            _adLeft.text = left > 0 ? left + " LEFT TODAY" : "COME BACK TOMORROW";
            _chestPopup.SetActive(false);
            Ctx.Store.Changed -= RefreshPrices;
            Ctx.Store.Changed += RefreshPrices;
            RefreshPrices();
        }


        /// <summary>Local prices from the store (e.g. ₹85.00), or … while loading, N/A when offline.</summary>
        private void RefreshPrices()
        {
            if (this == null) return;
            foreach (var kv in _buy)
            {
                var label = kv.Value.transform.Find("Label");
                if (label != null) label.GetComponent<Text>().text = Ctx.Store.PriceOf(kv.Key);
                kv.Value.interactable = !(kv.Key == IapStore.RemoveAds && Ctx.Profile.Data.NoAds);
            }
        }

        private void WatchAd()
        {
            var p = Ctx.Profile;
            if (p.AdsLeftToday <= 0) { Ctx.Sfx.PlayUi(SfxId.UiError, 1f); Ctx.UI.Toast("No more free coins today"); return; }
            if (!Ctx.Ads.RewardedReady) { Ctx.Sfx.PlayUi(SfxId.UiError, 1f); Ctx.UI.Toast("No video ready yet. Try again in a moment."); return; }
            Ctx.Ads.ShowRewarded("free_coins", ok =>
            {
                if (!ok) return;
                p.CountAd();
                p.AddCoins(PlayerProfile.AdCoins);
                Ctx.Sfx.PlayUi(SfxId.UiCoin, 1f);
                Ctx.UI.Toast("+" + PlayerProfile.AdCoins + " COINS");
                OnShow();
            });
        }

        private void BuyProduct(string id)
        {
            if (!Ctx.Store.Ready)
            {
                Ctx.Sfx.PlayUi(SfxId.UiError, 1f);
                Ctx.UI.Toast("The store isn't available right now. Check your connection.");
                return;
            }
            int gemsBefore = Ctx.Profile.Data.Gems;
            Ctx.Store.Buy(id, ok =>
            {
                if (!ok) return;
                Ctx.Sfx.PlayUi(SfxId.UiCoin, 1f);
                int got = Ctx.Profile.Data.Gems - gemsBefore;
                Ctx.UI.Toast(id == IapStore.RemoveAds ? "Ads removed! Thank you!  +" + got + " GEMS" : "Thank you!  +" + got + " GEMS");
                RefreshPrices();
            });
        }

        /// <summary>Chests unlock a random wardrobe item you don't own yet (coins back if you own everything).</summary>
        private void OpenChest(bool royal)
        {
            var p = Ctx.Profile;
            int cost = royal ? 60 : 500;
            if (royal ? p.Data.Gems < cost : p.Data.Coins < cost)
            {
                Ctx.Sfx.PlayUi(SfxId.UiError, 1f);
                Ctx.UI.Toast(royal ? "Not enough gems" : "Not enough coins");
                return;
            }
            var pool = new List<ShopItem>();
            foreach (var it in Items.All)
            {
                if (it.Kind == ItemKind.Map || p.Owns(it)) continue;
                bool fancy = it.Rarity == Rarity.Epic || it.Rarity == Rarity.Legendary;
                if (fancy == royal) pool.Add(it);
            }
            if (royal) p.Data.Gems -= cost; else p.Data.Coins -= cost;
            if (pool.Count == 0)
            {
                int back = royal ? 40 : 300;
                if (royal) p.Data.Gems += back; else p.Data.Coins += back;
                _pendingText = "YOU OWN EVERYTHING!\n+" + back + (royal ? " GEMS" : " COINS");
                _pendingIcon = Ctx.Catalog != null ? Ctx.Catalog.Reward(royal ? ArtIds.RewardGem : ArtIds.RewardCoin) : null;
            }
            else
            {
                var win = pool[UnityEngine.Random.Range(0, pool.Count)];
                p.Data.Owned.Add(win.Key);
                _pendingText = win.Rarity.ToString().ToUpperInvariant() + "\n" + win.Name;
                _pendingIcon = IconFor(win);
            }
            p.Save();
            _chestImage.sprite = Ctx.Catalog != null ? Ctx.Catalog.Reward(royal ? ArtIds.RewardChestIron : ArtIds.RewardChest2) : Ctx.Art.Square;
            _rewardIcon.enabled = false;
            _rewardText.text = "";
            _chestPopup.SetActive(true);
            _chestT = 0f;
            Ctx.Sfx.PlayUi(SfxId.Beep, 0.8f);
        }

        private Sprite IconFor(ShopItem it)
        {
            var cat = Ctx.Catalog;
            if (cat == null) return null;
            switch (it.Kind)
            {
                case ItemKind.Outfit: return ArtCatalog.At(cat.BodyDetail, it.Index);
                case ItemKind.Headgear: return ArtCatalog.At(cat.Heads, it.Index);
                case ItemKind.Hair: return ArtCatalog.At(cat.Hair, it.Index - 1);
                case ItemKind.Back: return ArtCatalog.At(cat.Back, it.Index - 1);
                default: return null;
            }
        }

        private void BuildChestPopup(RectTransform root, ArtLibrary art, ArtCatalog cat)
        {
            var dim = UIKit.Image(root, "ChestPopup", art.Square, UIKit.Dim(0f, 0f, 0.02f, 0.85f), false, true);
            UIKit.Stretch(dim.rectTransform);
            _chestPopup = dim.gameObject;
            Embers.Add(dim.rectTransform, art, 30, new Color(1f, 0.85f, 0.35f, 0.5f));
            var rays = UIKit.Image(dim.transform, "Rays", cat != null ? ArtCatalog.At(cat.Fx1, ArtIds.StarOrange) : art.Star, new Color(1f, 1f, 1f, 0.6f));
            UIKit.Place(rays.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 60f), new Vector2(700f, 700f));
            rays.gameObject.AddComponent<Spin>().Speed = 25f;
            _chestImage = UIKit.Icon(dim.transform, "Chest", null, new Vector2(320f, 320f));
            UIKit.Place(_chestImage.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 60f), new Vector2(320f, 320f));
            _rewardIcon = UIKit.Icon(dim.transform, "Reward", null, new Vector2(260f, 260f));
            UIKit.Place(_rewardIcon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 80f), new Vector2(260f, 260f));
            _rewardIcon.gameObject.AddComponent<PopIn>().Duration = 0.4f;
            _rewardText = UIKit.Title(dim.transform, "RewardText", "", 54, Color.white, UIKit.Gold);
            UIKit.Place(_rewardText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -190f), new Vector2(1200f, 140f));
            var ok = UIKit.ArtButton(dim.transform, "Ok", "AWESOME!", ArtIds.PanelGold, new Vector2(420f, 110f), 44, () => _chestPopup.SetActive(false));
            UIKit.Place((RectTransform)ok.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 60f), new Vector2(420f, 110f));
            _chestPopup.SetActive(false);
        }

        private void Update()
        {
            if (_chestT < 0f || !_chestPopup.activeSelf) return;
            _chestT += Time.unscaledDeltaTime;
            var rt = _chestImage.rectTransform;
            if (_chestT < 1.1f)
            {
                float k = _chestT / 1.1f;
                rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(_chestT * 45f) * 10f * k);
                float s = 1f + k * 0.15f;
                rt.localScale = new Vector3(s, s, 1f);
            }
            else if (_chestImage.enabled)
            {
                _chestImage.enabled = false;
                _rewardIcon.enabled = _pendingIcon != null;
                _rewardIcon.sprite = _pendingIcon;
                _rewardIcon.gameObject.SetActive(false); // restart its PopIn
                _rewardIcon.gameObject.SetActive(true);
                _rewardText.text = _pendingText;
                Ctx.Sfx.PlayUi(SfxId.UiLevelUp, 1f);
                Ctx.Sfx.PlayUi(SfxId.Explosion, 0.3f);
                Ctx.Haptics.Pulse(40);
                _chestT = -1f;
                rt.localRotation = Quaternion.identity;
                rt.localScale = Vector3.one;
            }
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            if (Ctx != null) Ctx.Store.Changed -= RefreshPrices;
            if (_chestPopup != null) _chestPopup.SetActive(false);
            if (_chestImage != null) _chestImage.enabled = true;
        }
    }

    // =====================================================================================
    /// <summary>Between menu and match: loading art, map name, a tip and a filling bar.</summary>
    public sealed class LoadingView : MonoBehaviour
    {
        private static readonly string[] Tips =
        {
            "Crates break! Don't hide behind one for too long.",
            "Shoot red barrels near enemies for a big boom.",
            "Dash (Space) through gaps to dodge rockets.",
            "Pick up guns with E — your old one drops on the floor.",
            "Trees hide you from far away. Walk under them!",
            "Water stops you, but not bullets. Watch the river.",
            "Grenades bounce off walls. Bank them around corners.",
            "Headshots don't exist here — aim for the middle!",
        };

        private Text _map, _tip;
        private Image _fill;
        private float _t, _duration;
        private Action _done;

        public void Build(GameContext ctx, RectTransform safe)
        {
            var art = ctx.Art;
            var cat = ctx.Catalog;
            var root = (RectTransform)transform;
            var bg = UIKit.GradientFill(root, "Bg", new Color(0.05f, 0.04f, 0.1f, 1f), new Color(0.01f, 0.01f, 0.03f, 1f), true);
            bg.transform.SetAsFirstSibling();
            Embers.Add(root, art, 24, new Color(1f, 0.6f, 0.3f, 0.45f));
            var frame = UIKit.Panel9(safe, "ArtFrame", ArtIds.PanelWood, Color.white, 3f);
            UIKit.Place(frame.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 110f), new Vector2(1500f, 450f));
            var mask = UIKit.Rect(frame.transform, "Mask");
            UIKit.Stretch(mask, 22f);
            mask.gameObject.AddComponent<RectMask2D>();
            var pic = UIKit.Image(mask, "Art", cat != null && cat.LoadingArt != null ? cat.LoadingArt : art.Square, cat != null && cat.LoadingArt != null ? Color.white : new Color(0.2f, 0.25f, 0.35f));
            UIKit.Stretch(pic.rectTransform);
            var logo = cat != null ? cat.Logo : null;
            if (logo != null)
            {
                var l = UIKit.Icon(safe, "Logo", logo, new Vector2(420f, 200f));
                UIKit.Place(l.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, 6f), new Vector2(420f, 200f));
            }
            _map = UIKit.Title(safe, "Map", "", 66, UIKit.Cream, UIKit.Gold);
            UIKit.Place(_map.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 230f), new Vector2(1400f, 80f));
            _tip = UIKit.Label(safe, "Tip", "", 30, new Color(0.8f, 0.88f, 1f), TextAnchor.MiddleCenter);
            UIKit.Place(_tip.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 170f), new Vector2(1500f, 40f));
            var barBg = UIKit.Image(safe, "BarBg", art.RoundRect, new Color(0f, 0f, 0f, 0.7f), true);
            UIKit.Place(barBg.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 90f), new Vector2(1100f, 34f));
            _fill = UIKit.Image(barBg.transform, "Fill", art.Square, new Color(1f, 0.75f, 0.25f));
            _fill.type = Image.Type.Filled;
            _fill.fillMethod = Image.FillMethod.Horizontal;
            UIKit.Stretch(_fill.rectTransform, 5f);
            var g = _fill.gameObject.AddComponent<UIGradient>();
            g.Top = new Color(1f, 0.95f, 0.7f);
            g.Bottom = new Color(1f, 0.55f, 0.15f);
        }

        public void Begin(string mapName, float seconds, Action done)
        {
            _map.text = mapName;
            _tip.text = "TIP: " + Tips[UnityEngine.Random.Range(0, Tips.Length)];
            _t = 0f;
            _duration = seconds;
            _done = done;
            _fill.fillAmount = 0f;
        }

        private void Update()
        {
            if (_done == null) return;
            _t += Time.unscaledDeltaTime;
            _fill.fillAmount = Mathf.SmoothStep(0f, 1f, _t / _duration);
            if (_t >= _duration)
            {
                var d = _done;
                _done = null;
                d();
            }
        }
    }

    // =====================================================================================
    /// <summary>Global overlays: toast messages and the test ad (stands in for Unity Ads).</summary>
    public sealed class OverlayView : MonoBehaviour
    {
        private Text _toast;
        private RectTransform _toastRt;
        private float _toastT;
        private GameObject _ad;
        private Text _adCount;
        private float _adT = -1f;
        private Action<bool> _adDone;
        private GameObject _consent;
        private Action<bool> _consentAnswer;

        /// <summary>Friends popup and the invite card live here, above every screen.</summary>
        public FriendsPopup Friends { get; private set; }
        public InviteCard Invite { get; private set; }

        public void Build(GameContext ctx, RectTransform safe)
        {
            var art = ctx.Art;
            BuildConsent(safe, art);
            Friends = FriendsPopup.Build(safe, ctx);
            Invite = InviteCard.Build(safe, ctx);
            var bg = UIKit.Panel9(safe, "Toast", ArtIds.PanelDark, Color.white, 2.6f);
            _toastRt = bg.rectTransform;
            UIKit.Place(_toastRt, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(1100f, 90f));
            _toast = UIKit.Label(bg.transform, "Text", "", 32, Color.white, TextAnchor.MiddleCenter);
            UIKit.Stretch(_toast.rectTransform);
            bg.gameObject.SetActive(false);

            var ad = UIKit.Image((RectTransform)transform, "TestAd", art.Square, new Color(0.02f, 0.02f, 0.04f, 0.97f), false, true);
            UIKit.Stretch(ad.rectTransform);
            _ad = ad.gameObject;
            var t = UIKit.Title(ad.transform, "Title", "TEST AD", 90, Color.white, UIKit.Sky);
            UIKit.Place(t.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 160f), new Vector2(1200f, 120f));
            var s = UIKit.Label(ad.transform, "Sub", "Real video ads appear here once Unity Ads is connected.", 30, new Color(0.8f, 0.85f, 1f), TextAnchor.MiddleCenter);
            UIKit.Place(s.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 60f), new Vector2(1400f, 40f));
            _adCount = UIKit.Title(ad.transform, "Count", "3", 160, UIKit.Cream, UIKit.Gold);
            UIKit.Place(_adCount.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -120f), new Vector2(400f, 200f));
            _ad.SetActive(false);
        }

        /// <summary>First launch: may ad partners personalize ads? Both answers look alike; either one plays on.</summary>
        private void BuildConsent(RectTransform safe, ArtLibrary art)
        {
            var dim = UIKit.Image(safe, "Consent", art.Square, UIKit.Dim(0.01f, 0.02f, 0.05f, 0.8f), false, true);
            UIKit.Stretch(dim.rectTransform, -400f);
            _consent = dim.gameObject;
            var panel = UIKit.Panel9(dim.transform, "Panel", ArtIds.PanelDark, Color.white, 3f);
            UIKit.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1180f, 600f));
            panel.gameObject.AddComponent<PopIn>();
            var title = UIKit.Title(panel.transform, "Title", "BEFORE YOU PLAY", 64, UIKit.Cream, UIKit.Gold);
            UIKit.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(1000f, 90f));
            var body = UIKit.Label(panel.transform, "Body",
                "Neon Brawl is free because of ads.\n\nMay our ad partners use your device's advertising ID to show ads that match your interests? " +
                "You will see ads either way. You can change this any time in Settings.", 32, Color.white, TextAnchor.UpperCenter);
            body.horizontalOverflow = HorizontalWrapMode.Wrap;
            body.lineSpacing = 1.15f;
            UIKit.Place(body.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(1040f, 260f));
            var yes = UIKit.ArtButton(panel.transform, "Yes", "YES, PERSONALIZE", ArtIds.PanelBlue, new Vector2(480f, 110f), 38, () => AnswerConsent(true));
            UIKit.Place((RectTransform)yes.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-260f, 40f), new Vector2(480f, 110f));
            var no = UIKit.ArtButton(panel.transform, "No", "NO, THANKS", ArtIds.PanelBlue, new Vector2(480f, 110f), 38, () => AnswerConsent(false));
            UIKit.Place((RectTransform)no.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(260f, 40f), new Vector2(480f, 110f));
            _consent.SetActive(false);
        }

        public void AskConsent(Action<bool> answer)
        {
            _consentAnswer = answer;
            _consent.SetActive(true);
            transform.SetAsLastSibling();
        }

        private void AnswerConsent(bool yes)
        {
            _consent.SetActive(false);
            var a = _consentAnswer;
            _consentAnswer = null;
            a?.Invoke(yes);
        }

        public void Toast(string text)
        {
            _toast.text = text;
            _toastT = 2.2f;
            _toastRt.gameObject.SetActive(true);
            _toastRt.localScale = Vector3.one;
            transform.SetAsLastSibling();
        }

        public void ShowAd(Action<bool> done)
        {
            _adDone = done;
            _adT = 3f;
            _ad.SetActive(true);
            transform.SetAsLastSibling();
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (_toastT > 0f)
            {
                _toastT -= dt;
                float age = 2.2f - _toastT;
                float s = age < 0.12f ? Mathf.Lerp(0.6f, 1.05f, age / 0.12f) : age < 0.2f ? Mathf.Lerp(1.05f, 1f, (age - 0.12f) / 0.08f) : 1f;
                _toastRt.localScale = new Vector3(s, s, 1f);
                if (_toastT <= 0f) _toastRt.gameObject.SetActive(false);
            }
            if (_adT > 0f)
            {
                _adT -= dt;
                _adCount.text = Mathf.CeilToInt(Mathf.Max(0f, _adT)).ToString();
                if (_adT <= 0f)
                {
                    _ad.SetActive(false);
                    var d = _adDone;
                    _adDone = null;
                    d?.Invoke(true);
                }
            }
        }
    }
}
