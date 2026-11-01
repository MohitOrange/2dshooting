using UnityEngine;
using UnityEngine.UI;

namespace NeonBrawl
{
    /// <summary>
    /// Your profile: the soldier (drag to spin), your name (tap to edit), level, rank and XP, and your
    /// match stats. Opened from the name chip on the home screen.
    /// </summary>
    public sealed class ProfileView : ScreenView
    {
        private CharacterPreview _preview;
        private InputField _name;
        private Text _level, _rank, _xp, _next;
        private Image _xpFill, _badge;
        private readonly Text[] _stats = new Text[6];

        private static readonly string[] StatNames = { "MATCHES", "WINS", "WIN RATE", "KILLS", "KILLS / MATCH", "WARDROBE" };

        public void Build(GameContext ctx, RectTransform safe)
        {
            Ctx = ctx;
            var art = ctx.Art;
            var cat = ctx.Catalog;
            Dim((RectTransform)transform, 0.9f, 0.8f);
            Embers.Add((RectTransform)transform, art, 16, new Color(1f, 0.8f, 0.4f, 0.3f));
            Bar = TopBar.Build(safe, ctx, "PROFILE", () => ctx.UI.Back());

            // left: soldier and name
            var card = UIKit.Panel9(safe, "Card", ArtIds.PanelDark, Color.white, 2.8f, true);
            UIKit.Place(card.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-500f, -40f), new Vector2(720f, 820f));
            card.gameObject.AddComponent<SlideIn>().From = new Vector2(-600f, 0f);
            _preview = CharacterPreview.Create(card.transform, art, cat, 270f, true);
            UIKit.Place((RectTransform)_preview.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 120f), Vector2.zero);
            var hint = UIKit.Label(card.transform, "Hint", "DRAG TO SPIN", 22, new Color(0.62f, 0.7f, 0.85f), TextAnchor.MiddleCenter);
            UIKit.Place(hint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), new Vector2(0f, 330f), new Vector2(500f, 30f));
            var nl = UIKit.Label(card.transform, "NameLabel", "YOUR NAME  (TAP TO EDIT)", 24, UIKit.Sky, TextAnchor.MiddleCenter);
            UIKit.Place(nl.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), new Vector2(0f, 284f), new Vector2(600f, 34f));
            _name = OnlineView.Field(card.transform, "Name", "TYPE A NAME", 48, new Vector2(600f, 100f));
            UIKit.Place((RectTransform)_name.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), new Vector2(0f, 212f), new Vector2(600f, 100f));
            _name.characterLimit = 14;
            _name.onValidateInput = (s, i, c) => char.IsLetterOrDigit(c) || c == ' ' || c == '_' || c == '-' || c == '.' ? char.ToUpperInvariant(c) : '\0';
            _name.onEndEdit.AddListener(SaveName);
            var customize = UIKit.ArtButton(card.transform, "Customize", "CUSTOMIZE", ArtIds.PanelBlue, new Vector2(420f, 100f), 40, () => ctx.UI.ShowLocker(),
                cat != null ? cat.Icon(ArtIds.IconShirt) : null);
            UIKit.Place((RectTransform)customize.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 34f), new Vector2(420f, 100f));

            // right top: level, rank, xp
            var lvl = UIKit.Panel9(safe, "Level", ArtIds.PanelSlate, Color.white, 2.8f);
            UIKit.Place(lvl.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(420f, 230f), new Vector2(980f, 300f));
            lvl.gameObject.AddComponent<SlideIn>().From = new Vector2(600f, 0f);
            _badge = UIKit.Icon(lvl.transform, "Badge", null, new Vector2(200f, 200f));
            UIKit.Place(_badge.rectTransform, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(150f, 6f), new Vector2(200f, 200f));
            _badge.gameObject.AddComponent<Bob>().Amount = 5f;
            _level = UIKit.Title(lvl.transform, "Level", "LEVEL 1", 66, UIKit.Cream, UIKit.Gold, TextAnchor.MiddleLeft);
            UIKit.Place(_level.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(290f, -70f), new Vector2(620f, 84f));
            _rank = UIKit.Label(lvl.transform, "Rank", "", 34, UIKit.Sky, TextAnchor.MiddleLeft);
            UIKit.Place(_rank.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(294f, -132f), new Vector2(620f, 44f));
            var xpBg = UIKit.Image(lvl.transform, "XpBg", art.Pill != null ? art.Pill : art.RoundRect, new Color(0f, 0f, 0f, 0.6f), true);
            UIKit.Place(xpBg.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(294f, 62f), new Vector2(630f, 40f));
            _xpFill = UIKit.Image(xpBg.transform, "Fill", art.Pill != null ? art.Pill : art.RoundRect, new Color(0.45f, 0.85f, 1f), true);
            _xpFill.type = Image.Type.Filled;
            _xpFill.fillMethod = Image.FillMethod.Horizontal;
            UIKit.Stretch(_xpFill.rectTransform, 5f);
            _xp = UIKit.Label(xpBg.transform, "Xp", "", 24, Color.white, TextAnchor.MiddleCenter);
            UIKit.Stretch(_xp.rectTransform);
            _next = UIKit.Label(lvl.transform, "Next", "", 22, new Color(0.7f, 0.76f, 0.88f), TextAnchor.MiddleLeft);
            UIKit.Place(_next.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0.5f), new Vector2(298f, 34f), new Vector2(630f, 30f));

            // right bottom: stats
            for (int i = 0; i < StatNames.Length; i++)
            {
                int col = i % 3, row = i / 3;
                var s = UIKit.Panel9(safe, "Stat" + i, ArtIds.PanelDark2, Color.white, 2.6f);
                UIKit.Place(s.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(100f + col * 324f, -100f - row * 230f), new Vector2(304f, 210f));
                s.gameObject.AddComponent<PopIn>().Delay = 0.05f * i;
                var t = UIKit.Label(s.transform, "Name", StatNames[i], 24, new Color(0.66f, 0.74f, 0.88f), TextAnchor.MiddleCenter);
                UIKit.Place(t.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -44f), new Vector2(280f, 36f));
                _stats[i] = UIKit.Title(s.transform, "Value", "0", 64, Color.white, UIKit.Cream);
                UIKit.Place(_stats[i].rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -22f), new Vector2(280f, 90f));
            }
        }

        protected override void OnShow()
        {
            var p = Ctx.Profile;
            _preview.Set(p.Look(p.OutfitColor));
            _name.text = p.Data.Name;
            Refresh();
        }

        protected override void OnProfileChanged()
        {
            base.OnProfileChanged();
            if (_level != null) Refresh();
        }

        private void Refresh()
        {
            var p = Ctx.Profile;
            var d = p.Data;
            int level = d.Level;
            _level.text = "LEVEL " + level;
            _rank.text = RankName(level) + " RANK";
            int need = PlayerProfile.XpToNext(level);
            _xpFill.fillAmount = p.Xp01;
            _xp.text = d.Xp + " / " + need + " XP";
            _next.text = "Next level pays 50 coins + 2 gems";
            var cat = Ctx.Catalog;
            if (cat != null) _badge.sprite = cat.Reward(RankBadge(level));

            int owned = 0, total = 0;
            foreach (var it in Items.All)
            {
                if (it.Free) continue;
                total++;
                if (p.Owns(it)) owned++;
            }
            _stats[0].text = UIKit.Thousands(d.Matches);
            _stats[1].text = UIKit.Thousands(d.Wins);
            _stats[2].text = d.Matches > 0 ? Mathf.RoundToInt(100f * d.Wins / d.Matches) + "%" : "–";
            _stats[3].text = UIKit.Thousands(d.Kills);
            _stats[4].text = d.Matches > 0 ? (d.Kills / (float)d.Matches).ToString("0.0") : "–";
            _stats[5].text = owned + "/" + total;
        }

        private void SaveName(string v)
        {
            var p = Ctx.Profile;
            string clean = NetIO.CleanName(v);
            if (clean == p.Data.Name) { _name.text = clean; return; }
            p.Data.Name = clean;
            p.Save();
            _name.text = clean;
            Ctx.UI.Toast("Name saved: " + clean);
            Ctx.Sfx.PlayUi(SfxId.UiCoin, 0.6f);
        }

        /// <summary>Ranks by level (plan): Bronze 1–9, Silver 10–19, Gold 20–34, Platinum 35–49, Diamond 50–74, Master 75+.</summary>
        public static string RankName(int level) =>
            level >= 75 ? "MASTER" : level >= 50 ? "DIAMOND" : level >= 35 ? "PLATINUM" : level >= 20 ? "GOLD" : level >= 10 ? "SILVER" : "BRONZE";

        public static int RankBadge(int level) =>
            level >= 35 ? ArtIds.RankStar : level >= 20 ? ArtIds.RankGold : level >= 10 ? ArtIds.RankBlue : ArtIds.RankRed;
    }
}
