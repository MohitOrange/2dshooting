using UnityEngine;
using UnityEngine.UI;

namespace NeonBrawl
{
    /// <summary>
    /// POWER-UPS: all ten powers as cards (badge, what it does, level pips, how long it lasts now and after
    /// the next level). UPGRADE spends coins; every level costs double the one before. Opened from the
    /// POWERS tile on the home dock.
    /// </summary>
    public sealed class PowersView : ScreenView
    {
        private const int Cols = 5;
        private const float CardW = 340f, CardH = 392f, GapX = 22f, GapY = 22f;

        private sealed class Card
        {
            public RectTransform Root;
            public Image Badge, Glow, Flash;
            public Image[] Pips;
            public Text Level, Stat, Next, Cost;
            public Button Upgrade;
            public Image UpgradeFace, CoinIcon;
            public float Punch;
            public Vector2 Home;
        }

        private readonly Card[] _cards = new Card[Powers.Count];

        public void Build(GameContext ctx, RectTransform safe)
        {
            Ctx = ctx;
            var art = ctx.Art;
            var cat = ctx.Catalog;
            Dim((RectTransform)transform, 0.9f, 0.85f);
            Embers.Add((RectTransform)transform, art, 18, new Color(1f, 0.6f, 0.9f, 0.3f));
            Bar = TopBar.Build(safe, ctx, "POWER-UPS", () => ctx.UI.Back());

            var hint = UIKit.Label(safe, "Hint", "Grab glowing orbs in a match, then tap POWER.  Upgrades make every power last longer.", 24,
                new Color(0.78f, 0.84f, 0.96f), TextAnchor.MiddleCenter);
            UIKit.Place(hint.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -146f), new Vector2(1700f, 36f));

            var grid = UIKit.Rect(safe, "Grid");
            float gw = Cols * CardW + (Cols - 1) * GapX, gh = 2 * CardH + GapY;
            UIKit.Place(grid, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -66f), new Vector2(gw, gh));
            for (int i = 0; i < Powers.Count; i++)
            {
                int col = i % Cols, row = i / Cols;
                _cards[i] = BuildCard(grid, ctx, (PowerId)i, new Vector2(col * (CardW + GapX), -row * (CardH + GapY)), cat, art);
                var s = _cards[i].Root.gameObject.AddComponent<PopIn>();
                s.Delay = 0.03f * i;
            }
        }

        private Card BuildCard(RectTransform grid, GameContext ctx, PowerId id, Vector2 pos, ArtCatalog cat, ArtLibrary art)
        {
            var def = Powers.Get(id);
            Color c = def.Color;
            var card = new Card();
            var holder = UIKit.Rect(grid, def.Short);
            UIKit.Place(holder, new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), pos + new Vector2(CardW * 0.5f, -CardH * 0.5f), new Vector2(CardW, CardH));
            var root = UIKit.GlassCard(holder, "Card", new Vector2(CardW, CardH), out var face);
            UIKit.Stretch(root);
            card.Root = holder;
            card.Home = holder.anchoredPosition;
            var stripe = UIKit.Image(root, "Accent", art.Pill, c, true);
            UIKit.Place(stripe.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -8f), new Vector2(CardW - 90f, 7f));

            card.Glow = UIKit.Image(root, "Glow", art.Glow, new Color(c.r, c.g, c.b, 0.35f));
            UIKit.Place(card.Glow.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -96f), new Vector2(250f, 220f));
            card.Badge = UIKit.Icon(root, "Badge", PowerArt.Badge(id), new Vector2(132f, 132f));
            UIKit.Place(card.Badge.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -96f), new Vector2(132f, 132f));
            card.Badge.gameObject.AddComponent<Bob>().Amount = 4f;
            card.Level = UIKit.Label(root, "Level", "LV 1", 22, Color.white, TextAnchor.MiddleCenter);
            var lvBg = UIKit.Image(root, "LevelBg", art.Pill, new Color(0.03f, 0.05f, 0.09f, 0.9f), true);
            UIKit.Place(lvBg.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-16f, -22f), new Vector2(82f, 34f));
            card.Level.transform.SetParent(lvBg.transform, false);
            UIKit.Stretch(card.Level.rectTransform);

            var name = UIKit.Label(root, "Name", def.Name, 30, Color.Lerp(c, Color.white, 0.45f), TextAnchor.MiddleCenter);
            UIKit.Place(name.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -184f), new Vector2(CardW - 24f, 40f));
            var about = UIKit.Label(root, "About", def.About, 20, new Color(0.74f, 0.8f, 0.92f), TextAnchor.UpperCenter, false);
            UIKit.Place(about.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -206f), new Vector2(CardW - 40f, 50f));
            about.horizontalOverflow = HorizontalWrapMode.Wrap;

            // level pips
            card.Pips = new Image[Powers.MaxLevel];
            float pw = 38f, pg = 8f, total = Powers.MaxLevel * pw + (Powers.MaxLevel - 1) * pg;
            for (int k = 0; k < Powers.MaxLevel; k++)
            {
                var pip = UIKit.Image(root, "Pip" + k, art.Pill, Color.white, true);
                UIKit.Place(pip.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 0.5f), new Vector2(-total * 0.5f + k * (pw + pg), 148f), new Vector2(pw, 12f));
                card.Pips[k] = pip;
            }
            card.Stat = UIKit.Label(root, "Stat", "", 26, Color.white, TextAnchor.MiddleRight);
            UIKit.Place(card.Stat.rectTransform, new Vector2(0.5f, 0f), new Vector2(1f, 0.5f), new Vector2(-6f, 116f), new Vector2(150f, 34f));
            card.Next = UIKit.Label(root, "Next", "", 26, new Color(0.45f, 1f, 0.5f), TextAnchor.MiddleLeft);
            UIKit.Place(card.Next.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 0.5f), new Vector2(6f, 116f), new Vector2(160f, 34f));

            // UPGRADE (coins)
            var btn = UIKit.ArtButton(root, "Upgrade", null, ArtIds.PanelGold, new Vector2(CardW - 44f, 78f), 0, () => TryUpgrade(id));
            var brt = (RectTransform)btn.transform;
            UIKit.Place(brt, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 20f), new Vector2(CardW - 44f, 78f));
            card.Upgrade = btn;
            card.UpgradeFace = (Image)btn.targetGraphic;
            card.CoinIcon = UIKit.Icon(brt, "Coin", cat != null ? cat.Reward(ArtIds.RewardCoin) : art.Circle, new Vector2(48f, 48f));
            UIKit.Place(card.CoinIcon.rectTransform, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(46f, 2f), new Vector2(48f, 48f));
            card.Cost = UIKit.Label(brt, "Cost", "", 32, UIKit.TextOn(ArtIds.PanelGold), TextAnchor.MiddleCenter, false);
            UIKit.Place(card.Cost.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(22f, 2f), new Vector2(CardW - 130f, 60f));

            card.Flash = UIKit.Image(root, "Flash", art.Glow, new Color(1f, 1f, 1f, 0f));
            UIKit.Stretch(card.Flash.rectTransform, -40f);
            card.Flash.raycastTarget = false;
            return card;
        }

        protected override void OnShow() => RefreshAll();

        protected override void OnProfileChanged()
        {
            base.OnProfileChanged();
            if (_cards[0] != null) RefreshAll();
        }

        private void RefreshAll()
        {
            for (int i = 0; i < _cards.Length; i++) Refresh(i);
        }

        private void Refresh(int i)
        {
            var card = _cards[i];
            var id = (PowerId)i;
            var def = Powers.Get(id);
            int lv = Ctx.Profile.PowerLevel(id);
            bool max = lv >= Powers.MaxLevel;
            int cost = Powers.UpgradeCost(lv);
            bool afford = !max && Ctx.Profile.Data.Coins >= cost;
            card.Level.text = max ? "MAX" : "LV " + lv;
            for (int k = 0; k < card.Pips.Length; k++)
                card.Pips[k].color = k < lv ? def.Color : new Color(1f, 1f, 1f, 0.14f);
            // the number that grows: how long it lasts (POWER PICK: how many powers you may choose from)
            bool choices = id == PowerId.Pick;
            card.Stat.text = Powers.Stat(def, lv, !choices);
            card.Next.text = max ? "" : "→ " + Powers.Stat(def, lv + 1, !choices);
            card.Stat.rectTransform.anchoredPosition = new Vector2(max ? 75f : -6f, 116f);
            card.Cost.text = max ? "MAXED" : UIKit.Thousands(cost);
            card.CoinIcon.enabled = !max;
            card.Upgrade.interactable = !max;
            card.UpgradeFace.color = max ? new Color(0.55f, 0.58f, 0.66f) : afford ? Color.white : new Color(0.75f, 0.72f, 0.68f);
            card.Cost.color = afford || max ? UIKit.TextOn(ArtIds.PanelGold) : new Color(0.55f, 0.2f, 0.15f);
        }

        private void TryUpgrade(PowerId id)
        {
            var card = _cards[(int)id];
            int before = Ctx.Profile.PowerLevel(id);
            if (before >= Powers.MaxLevel) return;
            if (!Ctx.Profile.TryUpgradePower(id))
            {
                Ctx.Sfx.PlayUi(SfxId.UiError, 0.8f);
                Ctx.UI.Toast("Not enough coins: play matches or open the SHOP");
                card.Punch = -0.6f;
                return;
            }
            Ctx.Sfx.PlayUi(SfxId.UiLevelUp, 0.9f);
            Ctx.Haptics?.Pulse(30);
            card.Punch = 1f;
            var def = Powers.Get(id);
            Ctx.UI.Toast(def.Name + " is now level " + (before + 1) + "!");
            RefreshAll();
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            for (int i = 0; i < _cards.Length; i++)
            {
                var c = _cards[i];
                if (c.Punch > 0f)
                {
                    // level up: white flash, the badge pops and spins once
                    c.Punch = Mathf.Max(0f, c.Punch - dt * 1.8f);
                    float k = c.Punch;
                    c.Flash.color = new Color(1f, 1f, 1f, k * 0.8f);
                    float s = 1f + Mathf.Sin(k * Mathf.PI) * 0.35f;
                    c.Badge.rectTransform.localScale = new Vector3(s, s, 1f);
                    c.Badge.rectTransform.localRotation = Quaternion.Euler(0f, 0f, (1f - k) * 360f);
                    c.Glow.rectTransform.localScale = Vector3.one * (1f + k * 0.8f);
                    if (c.Punch <= 0f) c.Badge.rectTransform.localRotation = Quaternion.identity;
                }
                else if (c.Punch < 0f)
                {
                    // can't afford: a little shake
                    c.Punch = Mathf.Min(0f, c.Punch + dt * 2f);
                    c.Root.anchoredPosition = c.Home + new Vector2(Mathf.Sin(c.Punch * 60f) * 10f * -c.Punch, 0f);
                }
                float g = 0.3f + Mathf.Sin(Time.unscaledTime * 2f + i) * 0.08f;
                var gc = c.Glow.color;
                if (c.Punch <= 0f) c.Glow.color = new Color(gc.r, gc.g, gc.b, g);
            }
        }
    }
}

namespace NeonBrawl
{
    /// <summary>Home dock tile: shows each power badge in turn with a little pop.</summary>
    public sealed class PowerCycle : MonoBehaviour
    {
        private Image _img;
        private int _i;
        private float _t;

        private void Awake() => _img = GetComponent<Image>();

        private void Update()
        {
            _t += Time.unscaledDeltaTime;
            float s = 1f + Mathf.Max(0f, 0.25f - _t) * 1.4f;
            transform.localScale = new Vector3(s, s, 1f);
            if (_t < 1.1f) return;
            _t = 0f;
            _i = (_i + 1) % Powers.Count;
            if (_img != null) _img.sprite = PowerArt.Badge((PowerId)_i);
        }
    }
}
