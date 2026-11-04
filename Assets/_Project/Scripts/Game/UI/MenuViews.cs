using System;
using System.Collections.Generic;
using NeonBrawl.Core;
using UnityEngine;
using UnityEngine.UI;

namespace NeonBrawl
{
    /// <summary>A compact row: label, then ◄ value ► on a dark pill (560 px wide).</summary>
    public sealed class OptionRow
    {
        private readonly string[] _labels;
        private readonly Text _value;
        private readonly Action<int> _changed;
        private readonly GameObject _left, _right;
        public int Index { get; private set; }

        public OptionRow(Transform parent, string label, string[] values, int index, float x, float y, Action<int> changed)
        {
            _labels = values;
            _changed = changed;
            Index = Mathf.Clamp(index, 0, values.Length - 1);
            var l = UIKit.Label(parent, label, label, 26, new Color(0.78f, 0.84f, 0.95f), TextAnchor.MiddleLeft);
            UIKit.Place(l.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(x, y), new Vector2(220f, 50f));
            var pill = UIKit.Image(parent, "Pill", UIKit.Art.RoundRect, new Color(0f, 0f, 0f, 0.45f), true);
            UIKit.Place(pill.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(x + 220f, y), new Vector2(330f, 62f));
            var left = UIKit.ArtButton(parent, "Prev", "<", ArtIds.PanelBlue, new Vector2(62f, 62f), 34, () => Step(-1));
            UIKit.Place((RectTransform)left.transform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(x + 220f, y), new Vector2(62f, 62f));
            _value = UIKit.Label(parent, "Value", "", 28, Color.white, TextAnchor.MiddleCenter);
            UIKit.Place(_value.rectTransform, new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), new Vector2(x + 385f, y), new Vector2(200f, 50f));
            var right = UIKit.ArtButton(parent, "Next", ">", ArtIds.PanelBlue, new Vector2(62f, 62f), 34, () => Step(1));
            UIKit.Place((RectTransform)right.transform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(x + 488f, y), new Vector2(62f, 62f));
            _left = left.gameObject;
            _right = right.gameObject;
            _value.text = _labels[Index];
        }

        /// <summary>Shows a value without calling back (the room host changed it).</summary>
        public void SetIndex(int index)
        {
            Index = Mathf.Clamp(index, 0, _labels.Length - 1);
            _value.text = _labels[Index];
        }

        /// <summary>Hides the arrows for players who can only look (online room guests).</summary>
        public void SetEditable(bool editable)
        {
            if (_left.activeSelf != editable) _left.SetActive(editable);
            if (_right.activeSelf != editable) _right.SetActive(editable);
        }

        private void Step(int d)
        {
            Index = (Index + d + _labels.Length) % _labels.Length;
            _value.text = _labels[Index];
            _changed?.Invoke(Index);
        }
    }

    /// <summary>
    /// Settings, shared by the menu and the pause screen: tabs on the left (GAME, AUDIO, CONTROLS, PRIVACY),
    /// rows on the right with switches, sliders and option pickers. Everything saves at once.
    /// </summary>
    public sealed class SettingsView : MonoBehaviour
    {
        private static readonly string[] Tabs = { "GAME", "AUDIO", "CONTROLS", "PRIVACY", "ABOUT" };

        private GameContext _ctx;
        private readonly RectTransform[] _pages = new RectTransform[5];
        private readonly Button[] _tabs = new Button[5];
        private int _tab;

        public void Build(GameContext ctx, RectTransform safe)
        {
            _ctx = ctx;
            var art = ctx.Art;
            var root = (RectTransform)transform;
            var dim = UIKit.Image(root, "Dim", art.Square, UIKit.Dim(0f, 0f, 0.02f, 0.75f), false, true);
            UIKit.Stretch(dim.rectTransform);
            dim.transform.SetAsFirstSibling();
            var panel = UIKit.Panel9(safe, "Panel", ArtIds.PanelDark, Color.white, 3f, true);
            UIKit.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -24f), new Vector2(1480f, 860f));
            panel.gameObject.AddComponent<PopIn>();
            var ribbon = UIKit.Panel9(panel.transform, "Ribbon", ArtIds.PanelOrange, Color.white, 3f);
            UIKit.Place(ribbon.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(500f, 104f));
            var head = UIKit.Title(ribbon.transform, "Head", "SETTINGS", 54, Color.white, UIKit.Cream);
            UIKit.Stretch(head.rectTransform);
            var gear = UIKit.Icon(ribbon.transform, "Gear", ctx.Catalog != null ? ctx.Catalog.Icon(ArtIds.IconGear) : null, new Vector2(88f, 88f));
            UIKit.Place(gear.rectTransform, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-6f, 0f), new Vector2(92f, 92f));
            gear.gameObject.AddComponent<Spin>().Speed = 40f;

            // tabs
            var side = UIKit.Image(panel.transform, "Tabs", art.RoundRect, new Color(0f, 0f, 0f, 0.3f), true);
            UIKit.Place(side.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(34f, -90f), new Vector2(330f, 600f));
            for (int i = 0; i < Tabs.Length; i++)
            {
                int k = i;
                var b = UIKit.ArtButton(side.transform, Tabs[i], Tabs[i], ArtIds.PanelSlate, new Vector2(290f, 96f), 36, () => ShowTab(k));
                UIKit.Place((RectTransform)b.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -20f - i * 113f), new Vector2(290f, 96f));
                _tabs[i] = b;
            }

            // pages
            var area = UIKit.Rect(panel.transform, "Pages");
            UIKit.Place(area, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(400f, -90f), new Vector2(1040f, 600f));
            for (int i = 0; i < _pages.Length; i++)
            {
                _pages[i] = UIKit.Rect(area, Tabs[i]);
                UIKit.Stretch(_pages[i]);
            }
            var p = ctx.Prefs;

            // GAME
            var g = new Rows(_pages[0]) { Height = Application.isMobilePlatform ? 116f : 100f };
            g.Segment("SCREEN SHAKE", "How much the camera shakes on hits and blasts", new[] { "OFF", "LOW", "FULL" },
                p.ShakeStrength <= 0f ? 0 : (p.ShakeStrength < 0.9f ? 1 : 2), i => { p.ShakeStrength = i == 0 ? 0f : (i == 1 ? 0.5f : 1f); p.Save(); });
            g.Toggle("VIBRATION", "Buzz on hits, kills and explosions", p.Vibration, v => { p.Vibration = v; p.Save(); });
            g.Toggle("BLOOD", "Off shows sparks instead", p.Blood, v => { p.Blood = v; p.Save(); });
            g.Toggle("MINIMAP", "Map in the corner: footsteps and gunfire near you", p.Minimap, v => { p.Minimap = v; p.Save(); ctx.UI.RefreshMinimap(); });
            g.Toggle("SHOW FPS", "Frame rate in the corner of the match", p.ShowFps, v => { p.ShowFps = v; p.Save(); });
            if (!Application.isMobilePlatform)
                g.Toggle("FULL SCREEN", "PC: play in a window or full screen", Screen.fullScreen, v =>
                    Screen.fullScreenMode = v ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);

            // AUDIO
            var a = new Rows(_pages[1]);
            a.Slider("SOUND EFFECTS", "Guns, hits, menus", p.SfxVolume, v => { p.SfxVolume = v; p.Save(); });
            a.Slider("MUSIC", "Camp and battle music", Mathf.Clamp01(p.MusicVolume / 0.7f), v => { p.MusicVolume = v * 0.7f; p.Save(); });
            a.Slider("VOICE CHAT", "How loud other players' voices are", p.VoiceVolume, v => { p.VoiceVolume = v; p.Save(); ctx.Voice?.ApplyVolume(); });

            // CONTROLS
            var c = new Rows(_pages[2]);
            c.Segment("TOUCH CONTROLS", "Auto shows sticks on phones only", new[] { "AUTO", "ALWAYS" }, p.ForceTouchControls ? 1 : 0,
                i => { p.ForceTouchControls = i == 1; p.Save(); ctx.App.RefreshTouch(); });
            c.Toggle("AUTO SHOOT", "Fire by itself at enemies you can see (X on PC)", p.AutoShoot, v => { p.AutoShoot = v; p.Save(); });
            c.Action("BUTTON LAYOUT", "Move, resize and fade the sticks and buttons", "EDIT", () => ctx.UI.ShowControlsEditor());
            c.Note("KEYBOARD AND MOUSE",
                "WASD  move     MOUSE  aim     LEFT CLICK  fire     RIGHT CLICK / G  grenade\n" +
                "SPACE  dash     R  reload     Q  swap gun     F  knife     E  pick up\n" +
                "TAB  scores     T / ENTER  chat     1-8  emotes     V  talk     X  auto shoot     C  power     ESC  menu");

            // PRIVACY
            var pr = new Rows(_pages[3]);
            pr.Toggle("TEXT CHAT", "Show typed messages from other players", p.TextChat, v => { p.TextChat = v; p.Save(); });
            pr.Toggle("VOICE CHAT", "Hear and talk to players in online matches", p.VoiceChat, v => { p.VoiceChat = v; p.Save(); ctx.Voice?.ApplySettings(); });
            pr.Toggle("PERSONAL ADS", "Ads based on your interests (you see ads either way)", p.AdConsent != 0, v =>
            {
                p.AdConsent = v ? 1 : 0;
                p.Save();
                ctx.App.SetAdConsent(v);
            });
            if (Application.isMobilePlatform)
                pr.Action("AD CHOICES", "Google's consent options for EU and UK players", "OPEN", () =>
                {
                    if (ctx.App.AdPrivacyOptionsAvailable) ctx.App.ShowAdPrivacyOptions();
                    else ctx.UI.Toast("No extra ad choices are needed where you live.");
                });
            pr.Action("PRIVACY POLICY", "What the game collects and why", "READ", () => Application.OpenURL(AdIds.PrivacyPolicyUrl));

            // ABOUT: who made the game
            AboutPage.Build(_pages[4], ctx);

            var close = UIKit.ArtButton(panel.transform, "Close", "DONE", ArtIds.PanelGold, new Vector2(340f, 104f), 46, () => ctx.UI.ShowSettings(false));
            UIKit.Place((RectTransform)close.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-40f, 34f), new Vector2(340f, 104f));
            ShowTab(0);
        }

        private void ShowTab(int i)
        {
            _tab = i;
            for (int k = 0; k < _pages.Length; k++)
            {
                _pages[k].gameObject.SetActive(k == i);
                var img = (Image)_tabs[k].targetGraphic;
                var s = _ctx.Catalog != null ? _ctx.Catalog.Panel(k == i ? ArtIds.PanelGold : ArtIds.PanelSlate) : null;
                if (s != null) img.sprite = s;
                var label = _tabs[k].transform.Find("Label");
                if (label == null) continue;
                label.GetComponent<Text>().color = k == i ? UIKit.TextOn(ArtIds.PanelGold) : new Color(0.82f, 0.87f, 0.96f);
                var outline = label.GetComponent<Outline>();
                if (outline != null) outline.effectColor = k == i ? new Color(1f, 1f, 1f, 0.4f) : new Color(0f, 0f, 0f, 0.7f);
            }
        }

        /// <summary>Stacks setting rows: name + hint on the left, the control on the right.</summary>
        private sealed class Rows
        {
            private readonly RectTransform _page;
            private float _y = -12f;
            /// <summary>Row height; a little shorter on pages with many rows.</summary>
            public float Height = 116f;

            public Rows(RectTransform page) { _page = page; }

            private RectTransform Row(string title, string hint, float height = 0f)
            {
                if (height <= 0f) height = Height;
                var bg = UIKit.Image(_page, title, UIKit.Art.RoundRect, new Color(1f, 1f, 1f, 0.05f), true);
                UIKit.Place(bg.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, _y), new Vector2(1040f, height - 14f));
                var t = UIKit.Label(bg.transform, "Title", title, 34, UIKit.Cream, TextAnchor.MiddleLeft);
                UIKit.Place(t.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(30f, -10f), new Vector2(480f, 48f));
                var h = UIKit.Label(bg.transform, "Hint", hint, 22, new Color(0.62f, 0.68f, 0.8f), TextAnchor.UpperLeft, false);
                UIKit.Place(h.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(30f, height < 110f ? -52f : -58f), new Vector2(560f, height < 110f ? 32f : 40f));
                _y -= height;
                return bg.rectTransform;
            }

            public void Toggle(string title, string hint, bool value, Action<bool> changed)
            {
                var row = Row(title, hint);
                var t = UiToggle.Create(row, value, changed);
                UIKit.Place((RectTransform)t.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-34f, 0f), new Vector2(150f, 66f));
            }

            public void Slider(string title, string hint, float value01, Action<float> changed)
            {
                var row = Row(title, hint);
                var s = UiSlider.Create(row, value01, changed, 330f);
                var rt = (RectTransform)s.transform.parent;
                UIKit.Place(rt, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-34f, 0f), rt.sizeDelta);
            }

            public void Segment(string title, string hint, string[] options, int index, Action<int> changed)
            {
                var row = Row(title, hint);
                var seg = new UiSegment(row, options, index, changed, options.Length > 2 ? 132f : 170f);
                UIKit.Place(seg.Root, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-34f, 0f), seg.Root.sizeDelta);
            }

            public void Action(string title, string hint, string label, Action onClick)
            {
                var row = Row(title, hint);
                var b = UIKit.ArtButton(row, title + "Btn", label, ArtIds.PanelGold, new Vector2(220f, 76f), 32, onClick);
                UIKit.Place((RectTransform)b.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-34f, 0f), new Vector2(220f, 76f));
            }

            public void Note(string title, string text)
            {
                var row = Row(title, "", 250f);
                var t = UIKit.Label(row, "Text", text, 25, new Color(0.82f, 0.87f, 0.96f), TextAnchor.UpperLeft, false);
                t.lineSpacing = 1.35f;
                UIKit.Place(t.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(30f, -70f), new Vector2(990f, 170f));
            }
        }
    }

    public sealed class PauseView : MonoBehaviour
    {
        private Text _title, _quit, _hint;

        /// <summary>Online the match keeps running behind this menu, and quitting leaves the room.</summary>
        public void SetOnline(bool online, bool host)
        {
            _title.text = online ? "MENU" : "PAUSED";
            _quit.text = online ? (host ? "CLOSE ROOM" : "LEAVE ROOM") : "QUIT MATCH";
            _hint.text = online ? "The match keeps going while this is open" : "Quitting gives no coins";
        }

        public void Build(GameContext ctx, RectTransform safe)
        {
            var art = ctx.Art;
            var cat = ctx.Catalog;
            var root = (RectTransform)transform;
            var dim = UIKit.Image(root, "Dim", art.Square, UIKit.Dim(0.01f, 0.02f, 0.05f, 0.7f), false, true);
            UIKit.Stretch(dim.rectTransform);
            dim.transform.SetAsFirstSibling();
            var panel = UIKit.Panel9(safe, "Panel", ArtIds.PanelDark, Color.white, 3f);
            UIKit.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -20f), new Vector2(620f, 640f));
            panel.gameObject.AddComponent<PopIn>();
            var title = UIKit.Title(safe, "Title", "PAUSED", 100, Color.white, UIKit.Sky);
            UIKit.Place(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 360f), new Vector2(800f, 130f));
            _title = title;
            var resume = UIKit.ArtButton(panel.transform, "Resume", "RESUME", ArtIds.PanelGold, new Vector2(500f, 130f), 52, () => ctx.App.SetPaused(false), cat != null ? cat.Icon(ArtIds.IconPlay) : null);
            UIKit.Place((RectTransform)resume.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -50f), new Vector2(500f, 130f));
            var settings = UIKit.ArtButton(panel.transform, "Settings", "SETTINGS", ArtIds.PanelBlue, new Vector2(500f, 110f), 40, () => ctx.UI.ShowSettings(true), cat != null ? cat.Icon(ArtIds.IconGear) : null);
            UIKit.Place((RectTransform)settings.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -210f), new Vector2(500f, 110f));
            var quit = UIKit.ArtButton(panel.transform, "Quit", "QUIT MATCH", ArtIds.PanelOrange, new Vector2(500f, 110f), 40, () => ctx.App.QuitToMenu(), cat != null ? cat.Icon(ArtIds.IconHome) : null);
            UIKit.Place((RectTransform)quit.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -350f), new Vector2(500f, 110f));
            var hint = UIKit.Label(panel.transform, "Hint", "Quitting gives no coins", 22, new Color(0.7f, 0.75f, 0.85f), TextAnchor.MiddleCenter);
            UIKit.Place(hint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(560f, 30f));
            _quit = quit.transform.Find("Label").GetComponent<Text>();
            _hint = hint;
        }
    }

    /// <summary>End-of-match screen: banner, standings with rank badges, coin/XP rewards (x2 with an ad), play again.</summary>
    public sealed class ResultsView : MonoBehaviour
    {
        private Text _title, _sub, _coins, _xp, _levelUp, _place, _againLabel, _menuLabel;
        private Image _xpFill, _ribbon;
        private Button _double;
        private ScoreTable _table;
        private GameContext _ctx;
        private Embers _confetti;
        private readonly List<PlayerRecord> _standings = new List<PlayerRecord>(12);
        private MatchReward _reward;
        private float _t;
        private float _xpFrom, _xpTo;
        private bool _doubled, _bannerPlaced;
        private RectTransform _bannerSlot;
        private readonly Vector3[] _corners = new Vector3[4];

        public void Build(GameContext ctx, RectTransform safe)
        {
            _ctx = ctx;
            var art = ctx.Art;
            var cat = ctx.Catalog;
            var root = (RectTransform)transform;
            var dim = UIKit.GradientFill(root, "Dim", UIKit.Dim(0.02f, 0.03f, 0.08f, 0.85f), UIKit.Dim(0.04f, 0.02f, 0.06f, 0.7f), true);
            dim.transform.SetAsFirstSibling();
            var confettiRoot = UIKit.Rect(root, "Confetti");
            UIKit.Stretch(confettiRoot);
            Embers.Add(confettiRoot, art, 40, new Color(1f, 0.85f, 0.3f, 0.6f));
            _confetti = confettiRoot.GetComponent<Embers>();

            _ribbon = UIKit.Panel9(safe, "Ribbon", ArtIds.PanelOrange, Color.white, 3.2f);
            UIKit.Place(_ribbon.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(1000f, 150f));
            _ribbon.gameObject.AddComponent<PopIn>();
            _title = UIKit.Title(_ribbon.transform, "Title", "", 92, Color.white, UIKit.Cream);
            UIKit.Stretch(_title.rectTransform);
            _sub = UIKit.Label(safe, "Sub", "", 30, new Color(0.85f, 0.9f, 1f), TextAnchor.MiddleCenter);
            UIKit.Place(_sub.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -188f), new Vector2(1400f, 44f));

            var table = UIKit.Panel9(safe, "Table", ArtIds.PanelDark, Color.white, 2.8f);
            UIKit.Place(table.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-340f, -60f), new Vector2(940f, 600f));
            table.gameObject.AddComponent<SlideIn>().From = new Vector2(-500f, 0f);
            _table = new ScoreTable(table.transform, 13, 42f, 28, cat);

            // rewards
            var rw = UIKit.Panel9(safe, "Rewards", ArtIds.PanelWood, Color.white, 3f);
            UIKit.Place(rw.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(520f, -20f), new Vector2(620f, 520f));
            rw.gameObject.AddComponent<SlideIn>().From = new Vector2(500f, 0f);
            _place = UIKit.Title(rw.transform, "Place", "", 60, Color.white, UIKit.Gold);
            UIKit.Place(_place.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(560f, 80f));
            var coin = UIKit.Icon(rw.transform, "Coin", cat != null ? cat.Reward(ArtIds.RewardCoin) : null, new Vector2(110f, 110f));
            UIKit.Place(coin.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(-130f, -170f), new Vector2(110f, 110f));
            coin.gameObject.AddComponent<Bob>().Amount = 6f;
            _coins = UIKit.Title(rw.transform, "Coins", "+0", 80, UIKit.Cream, UIKit.Gold, TextAnchor.MiddleLeft);
            UIKit.Place(_coins.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, 0.5f), new Vector2(-60f, -170f), new Vector2(300f, 100f));
            _xp = UIKit.Label(rw.transform, "Xp", "", 30, UIKit.Sky, TextAnchor.MiddleCenter);
            UIKit.Place(_xp.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -262f), new Vector2(560f, 40f));
            var xpBg = UIKit.Image(rw.transform, "XpBg", art.RoundRect, new Color(0f, 0f, 0f, 0.6f), true);
            UIKit.Place(xpBg.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -306f), new Vector2(500f, 34f));
            _xpFill = UIKit.Image(xpBg.transform, "Fill", art.Square, new Color(0.45f, 0.85f, 1f));
            _xpFill.type = Image.Type.Filled;
            _xpFill.fillMethod = Image.FillMethod.Horizontal;
            UIKit.Stretch(_xpFill.rectTransform, 5f);
            _levelUp = UIKit.Title(rw.transform, "LevelUp", "", 40, Color.white, UIKit.Gold);
            UIKit.Place(_levelUp.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -352f), new Vector2(560f, 50f));
            _double = UIKit.ArtButton(rw.transform, "Double", "x2 COINS", ArtIds.PanelBlue, new Vector2(420f, 96f), 38, DoubleCoins, cat != null ? cat.Icon(ArtIds.IconPlay) : null);
            UIKit.Place((RectTransform)_double.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 26f), new Vector2(420f, 96f));
            Shine.Add((RectTransform)_double.transform, art);

            var again = UIKit.ArtButton(safe, "Again", "PLAY AGAIN", ArtIds.PanelGold, new Vector2(460f, 120f), 46, () => ctx.App.Rematch(), cat != null ? cat.Icon(ArtIds.IconPlay) : null);
            UIKit.Place((RectTransform)again.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(520f, 30f), new Vector2(460f, 120f));
            var menu = UIKit.ArtButton(safe, "Menu", "HOME", ArtIds.PanelDark, new Vector2(300f, 120f), 46, () => ctx.App.QuitToMenu(), cat != null ? cat.Icon(ArtIds.IconHome) : null);
            UIKit.Place((RectTransform)menu.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(150f, 30f), new Vector2(300f, 120f));
            _againLabel = again.transform.Find("Label").GetComponent<Text>();
            _againLabel.resizeTextForBestFit = true;
            _againLabel.resizeTextMinSize = 24;
            _againLabel.resizeTextMaxSize = 46;
            _menuLabel = menu.transform.Find("Label").GetComponent<Text>();

            // free strip under the standings and left of HOME: the only place a banner may appear
            _bannerSlot = UIKit.Rect(safe, "BannerSlot");
            _bannerSlot.anchorMin = new Vector2(0f, 0f);
            _bannerSlot.anchorMax = new Vector2(0.5f, 0f);
            _bannerSlot.offsetMin = new Vector2(24f, 8f);
            _bannerSlot.offsetMax = new Vector2(-24f, 172f);
        }

        public void Show(MatchController m, MatchReward reward, float xpBefore)
        {
            _reward = reward;
            _doubled = false;
            _t = 0f;
            _title.text = m.WinnerText(out bool won);
            _ribbon.sprite = _ctx.Catalog != null ? _ctx.Catalog.Panel(won ? ArtIds.PanelGold : ArtIds.PanelOrange) : _ctx.Art.RoundRect;
            _title.color = won ? new Color(0.35f, 0.18f, 0.04f) : Color.white;
            _confetti.enabled = won;
            _confetti.gameObject.SetActive(won);
            var me = m.Local != null ? m.Rules.Get(m.Local.Id) : null;
            _sub.text = me != null ? "KILLS " + me.Kills + "     DEATHS " + me.Deaths + "     BEST STREAK " + me.BestStreak : "";
            m.GetStandings(_standings);
            _table.Fill(_standings, me != null ? me.Id : -1, !m.Rules.IsTeamMode);
            _place.text = reward.Place > 0 && !m.Rules.IsTeamMode ? Ordinal(reward.Place) + " PLACE" : (won ? "TEAM WIN" : "GOOD FIGHT");
            _coins.text = "+0";
            _xp.text = "+" + reward.Xp + " XP  ·  LEVEL " + _ctx.Profile.Data.Level;
            _levelUp.text = reward.LevelsGained > 0 ? "LEVEL UP!  +50 COINS +2 GEMS" : "";
            _xpFrom = xpBefore;
            _xpTo = _ctx.Profile.Xp01;
            _xpFill.fillAmount = _xpFrom;
            _double.gameObject.SetActive(reward.Coins > 0 && _ctx.Profile.AdsLeftToday > 0);
            _againLabel.text = m.Online ? "BACK TO ROOM" : "PLAY AGAIN";
            _menuLabel.text = m.Online ? "LEAVE" : "HOME";
            _ctx.Sfx.PlayUi(won ? SfxId.KillDing : SfxId.Go, 1f);
            _bannerPlaced = false;
        }

        private void PlaceBanner()
        {
            if (_ctx.Profile.Data.NoAds) return;
            _bannerSlot.GetWorldCorners(_corners); // overlay canvas: world = screen pixels
            var r = Rect.MinMaxRect(_corners[0].x, _corners[0].y, _corners[2].x, _corners[2].y);
            _ctx.Ads.ShowBanner(r);
        }

        private void OnDisable()
        {
            if (_ctx != null && _ctx.Ads != null) _ctx.Ads.HideBanner();
        }

        private static string Ordinal(int n) => n + (n == 1 ? "ST" : n == 2 ? "ND" : n == 3 ? "RD" : "TH");

        private void DoubleCoins()
        {
            if (_doubled) return;
            if (!_ctx.Ads.RewardedReady)
            {
                _ctx.Sfx.PlayUi(SfxId.UiError, 1f);
                _ctx.UI.Toast("No video ready yet. Try again in a moment.");
                return;
            }
            _ctx.Ads.ShowRewarded("double_coins", ok =>
            {
                if (!ok) return;
                _doubled = true;
                _ctx.Profile.CountAd();
                _ctx.Profile.AddCoins(_reward.Coins);
                _reward.Coins *= 2;
                _t = 0.4f;
                _double.gameObject.SetActive(false);
                _ctx.Sfx.PlayUi(SfxId.KillDing, 1f);
            });
        }

        private void Update()
        {
            _t += Time.unscaledDeltaTime;
            if (!_bannerPlaced && _t > 0.6f) { _bannerPlaced = true; PlaceBanner(); }
            float k = Mathf.Clamp01((_t - 0.4f) / 1.1f);
            int shown = Mathf.RoundToInt(_reward.Coins * k);
            string s = "+" + shown;
            if (_coins.text != s)
            {
                _coins.text = s;
                if (k < 1f && Time.frameCount % 3 == 0) _ctx.Sfx.PlayUi(SfxId.HitTick, 0.35f);
            }
            float xk = Mathf.Clamp01((_t - 0.8f) / 1f);
            _xpFill.fillAmount = _xpTo >= _xpFrom ? Mathf.Lerp(_xpFrom, _xpTo, xk) : (xk < 0.5f ? Mathf.Lerp(_xpFrom, 1f, xk * 2f) : Mathf.Lerp(0f, _xpTo, (xk - 0.5f) * 2f));
        }
    }

    /// <summary>Standings table with real columns (rank badge, name, kills, deaths) so numbers line up.</summary>
    public sealed class ScoreTable
    {
        private readonly Text[] _rank, _name, _kills, _deaths;
        private readonly Image[] _badge;
        private readonly Image[] _rowBg;

        public ScoreTable(Transform parent, int rows, float rowHeight, int fontSize, ArtCatalog cat = null)
        {
            _rank = new Text[rows];
            _name = new Text[rows];
            _kills = new Text[rows];
            _deaths = new Text[rows];
            _badge = new Image[rows];
            _rowBg = new Image[rows];
            int[] ranks = { ArtIds.RankGold, ArtIds.RankRed, ArtIds.RankBlue };
            for (int i = 0; i < rows; i++)
            {
                float y = -26f - i * rowHeight;
                if (i > 0)
                {
                    _rowBg[i] = UIKit.Image(parent, "RowBg", UIKit.Art.RoundRect, new Color(1f, 1f, 1f, i % 2 == 0 ? 0.04f : 0.08f), true);
                    UIKit.Place(_rowBg[i].rectTransform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(22f, y - rowHeight * 0.5f), new Vector2(880f, rowHeight - 4f));
                }
                _rank[i] = Cell(parent, "Rank", 44f, y, 70f, rowHeight, fontSize, TextAnchor.MiddleLeft);
                _name[i] = Cell(parent, "Name", 120f, y, 450f, rowHeight, fontSize, TextAnchor.MiddleLeft);
                _kills[i] = Cell(parent, "Kills", 590f, y, 120f, rowHeight, fontSize, TextAnchor.MiddleCenter);
                _deaths[i] = Cell(parent, "Deaths", 730f, y, 140f, rowHeight, fontSize, TextAnchor.MiddleCenter);
                if (i >= 1 && i <= 3 && cat != null)
                {
                    _badge[i] = UIKit.Icon(parent, "Badge", cat.Reward(ranks[i - 1]), new Vector2(rowHeight, rowHeight));
                    UIKit.Place(_badge[i].rectTransform, new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), new Vector2(64f, y - rowHeight * 0.5f), new Vector2(rowHeight + 6f, rowHeight + 6f));
                }
            }
            Set(0, "#", "NAME", "KILLS", "DEATHS", UIKit.Gold);
        }

        private static Text Cell(Transform parent, string n, float x, float y, float w, float h, int size, TextAnchor align)
        {
            var t = UIKit.Label(parent, n, "", size, Color.white, align, false);
            UIKit.Place(t.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x, y), new Vector2(w, h));
            return t;
        }

        private void Set(int row, string rank, string name, string kills, string deaths, Color c)
        {
            _rank[row].text = rank; _name[row].text = name; _kills[row].text = kills; _deaths[row].text = deaths;
            _rank[row].color = _name[row].color = _kills[row].color = _deaths[row].color = c;
        }

        public void Fill(List<PlayerRecord> standings, int localId, bool crownLeader)
        {
            for (int i = 1; i < _rank.Length; i++)
            {
                int k = i - 1;
                bool has = k < standings.Count;
                if (_rowBg[i] != null) _rowBg[i].enabled = has;
                if (_badge[i] != null) _badge[i].enabled = has;
                if (!has) { Set(i, "", "", "", "", Color.white); continue; }
                var p = standings[k];
                Color c = p.Id == localId ? new Color(1f, 0.88f, 0.4f)
                    : p.Team == 0 ? new Color(1f, 0.55f, 0.48f)
                    : p.Team == 1 ? new Color(0.55f, 0.68f, 1f) : Color.white;
                if (_rowBg[i] != null) _rowBg[i].color = p.Id == localId ? new Color(1f, 0.8f, 0.3f, 0.22f) : new Color(1f, 1f, 1f, i % 2 == 0 ? 0.04f : 0.08f);
                string name = p.Name + (p.IsBot ? "  <size=18><color=#9aa4b8>BOT</color></size>" : "");
                Set(i, _badge[i] != null ? "" : (k + 1) + ".", name, p.Kills.ToString(), p.Deaths.ToString(), c);
            }
        }
    }
}
