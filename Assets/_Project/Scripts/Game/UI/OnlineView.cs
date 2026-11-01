using System;
using System.Collections.Generic;
using NeonBrawl.Core;
using UnityEngine;
using UnityEngine.UI;

namespace NeonBrawl
{
    /// <summary>
    /// Online: pick a name, then QUICK MATCH, CREATE ROOM or JOIN with a code. Inside a room: the code to
    /// share, who is here (up to 12), the host's map and rules, and START (host) or waiting (guests).
    /// Redraws whenever <see cref="NetSession.Changed"/> fires.
    /// </summary>
    public sealed class OnlineView : ScreenView
    {
        private static readonly int[] BotOptions = { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 };
        private static readonly float[] TimeOptions = { 120f, 180f, 300f, 480f, 600f };
        private static readonly int[] KillOptions = { 10, 20, 30, 50, 0 };

        private sealed class Row
        {
            public GameObject Root;
            public Image Dot;
            public Text Name, Tag;
            public GameObject Add;
        }

        private NetSession _net;
        private GameObject _entry, _room, _busy;
        private InputField _name, _codeField;
        private Text _busyText, _code, _players, _mapName, _wait, _startLabel, _hint;
        private RectTransform _spinner, _thumbHolder;
        private Button _start;
        private GameObject _mapPrev, _mapNext, _copy, _invite;
        private readonly List<Row> _rows = new List<Row>(NetSession.MaxPlayers);
        private OptionRow _mode, _time, _kills, _bots, _skill, _night, _fog;
        private int _thumbMap = -1;
        private bool _wasInRoom;

        public void Build(GameContext ctx, RectTransform safe)
        {
            Ctx = ctx;
            _net = ctx.Net;
            var root = (RectTransform)transform;
            Dim(root, 0.82f, 0.7f);
            Bar = TopBar.Build(safe, ctx, "ONLINE", GoBack);
            BuildEntry(safe);
            BuildRoom(safe);
            BuildBusy(safe);
        }

        // ================================================================== entry page

        private void BuildEntry(RectTransform safe)
        {
            var art = Ctx.Art;
            var cat = Ctx.Catalog;
            var page = UIKit.Rect(safe, "Entry");
            UIKit.Stretch(page);
            _entry = page.gameObject;

            // who you are
            var card = UIKit.Panel9(page, "NameCard", ArtIds.PanelDark, Color.white, 2.8f);
            UIKit.Place(card.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-470f, -40f), new Vector2(700f, 560f));
            card.gameObject.AddComponent<SlideIn>().From = new Vector2(-500f, 0f);
            var person = UIKit.Icon(card.transform, "Person", cat != null ? cat.Icon(ArtIds.IconPerson) : null, new Vector2(110f, 110f));
            UIKit.Place(person.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -90f), new Vector2(110f, 110f));
            var nl = UIKit.Label(card.transform, "NameLabel", "YOUR NAME", 30, UIKit.Sky, TextAnchor.MiddleCenter);
            UIKit.Place(nl.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -178f), new Vector2(600f, 40f));
            _name = Field(card.transform, "Name", "TYPE A NAME", 50, new Vector2(580f, 100f));
            UIKit.Place((RectTransform)_name.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -258f), new Vector2(580f, 100f));
            _name.characterLimit = 14;
            _name.onValidateInput = (s, i, c) => char.IsLetterOrDigit(c) || c == ' ' || c == '_' || c == '-' || c == '.' ? char.ToUpperInvariant(c) : '\0';
            _name.onEndEdit.AddListener(v =>
            {
                Ctx.Profile.Data.Name = NetIO.CleanName(v);
                Ctx.Profile.Save();
                _name.text = Ctx.Profile.Data.Name;
                Ctx.Friends?.SyncName();
            });
            var info = UIKit.Label(card.transform, "Info", "UP TO 12 PLAYERS\nBOTS FILL THE EMPTY SPOTS\nWIN MORE COINS THAN OFFLINE", 26,
                new Color(0.82f, 0.88f, 1f), TextAnchor.MiddleCenter);
            info.lineSpacing = 1.25f;
            UIKit.Place(info.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(620f, 150f));

            // what to do
            var quickRt = UIKit.Rect(page, "QuickHolder");
            UIKit.Place(quickRt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(390f, 160f), new Vector2(780f, 190f));
            quickRt.gameObject.AddComponent<Pulse>().Amount = 0.02f;
            var quick = UIKit.ArtButton(quickRt, "Quick", "QUICK MATCH", ArtIds.PanelGold, new Vector2(780f, 190f), 72, () => _net.QuickMatch(),
                cat != null ? cat.Icon(ArtIds.IconPlay) : null);
            UIKit.Stretch((RectTransform)quick.transform);
            Shine.Add((RectTransform)quick.transform, art);
            var qs = UIKit.Label(page, "QuickSub", "Jump into a public room", 26, UIKit.Cream, TextAnchor.MiddleCenter);
            UIKit.Place(qs.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(390f, 40f), new Vector2(780f, 36f));
            quickRt.gameObject.AddComponent<SlideIn>().From = new Vector2(600f, 0f);

            var create = UIKit.ArtButton(page, "Create", "CREATE ROOM", ArtIds.PanelBlue, new Vector2(780f, 120f), 46, () => _net.CreateRoom(),
                cat != null ? cat.Icon(ArtIds.IconFlagBlue) : null);
            UIKit.Place((RectTransform)create.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(390f, -60f), new Vector2(780f, 120f));
            create.gameObject.AddComponent<SlideIn>().From = new Vector2(600f, 0f);
            var cs = UIKit.Label(page, "CreateSub", "Private room: share the code with friends", 26, UIKit.Cream, TextAnchor.MiddleCenter);
            UIKit.Place(cs.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(390f, -142f), new Vector2(780f, 36f));

            var joinBg = UIKit.Panel9(page, "JoinRow", ArtIds.PanelDark2, Color.white, 2.6f);
            UIKit.Place(joinBg.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(390f, -270f), new Vector2(780f, 140f));
            joinBg.gameObject.AddComponent<SlideIn>().From = new Vector2(600f, 0f);
            _codeField = Field(joinBg.transform, "Code", "ROOM CODE", 50, new Vector2(420f, 100f));
            UIKit.Place((RectTransform)_codeField.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(24f, 0f), new Vector2(420f, 100f));
            _codeField.characterLimit = 8;
            _codeField.onValidateInput = (s, i, c) => char.IsLetterOrDigit(c) ? char.ToUpperInvariant(c) : '\0';
            var join = UIKit.ArtButton(joinBg.transform, "Join", "JOIN", ArtIds.PanelOrange, new Vector2(290f, 100f), 46, () => _net.JoinRoom(_codeField.text));
            UIKit.Place((RectTransform)join.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-24f, 0f), new Vector2(290f, 100f));
        }

        // ================================================================== room page

        private void BuildRoom(RectTransform safe)
        {
            var art = Ctx.Art;
            var cat = Ctx.Catalog;
            var page = UIKit.Rect(safe, "Room");
            UIKit.Stretch(page);
            _room = page.gameObject;

            // left: code + players
            var left = UIKit.Panel9(page, "People", ArtIds.PanelDark, Color.white, 2.8f);
            UIKit.Place(left.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-520f, 0f), new Vector2(800f, 600f));
            left.gameObject.AddComponent<SlideIn>().From = new Vector2(-500f, 0f);
            var codeL = UIKit.Label(left.transform, "CodeLabel", "ROOM CODE", 26, UIKit.Sky, TextAnchor.MiddleLeft);
            UIKit.Place(codeL.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(36f, -40f), new Vector2(300f, 36f));
            _code = UIKit.Title(left.transform, "Code", "", 76, UIKit.Cream, UIKit.Gold, TextAnchor.MiddleLeft);
            UIKit.Place(_code.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(32f, -100f), new Vector2(460f, 90f));
            var copy = UIKit.ArtButton(left.transform, "Copy", "COPY", ArtIds.PanelBlue, new Vector2(170f, 80f), 32, CopyCode);
            UIKit.Place((RectTransform)copy.transform, new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(-262f, -96f), new Vector2(170f, 80f));
            _copy = copy.gameObject;
            // invite friends without leaving the game (leaving the app for 10 s drops the room)
            var invite = UIKit.ArtButton(left.transform, "Invite", "INVITE", ArtIds.PanelGold, new Vector2(214f, 80f), 34,
                () => Ctx.UI.ShowFriends(Ctx.Friends.Friends.Count > 0 ? FriendsPopup.TabFriends : FriendsPopup.TabAdd));
            UIKit.Place((RectTransform)invite.transform, new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(-32f, -96f), new Vector2(214f, 80f));
            _invite = invite.gameObject;
            _players = UIKit.Label(left.transform, "Players", "PLAYERS", 28, Color.white, TextAnchor.MiddleLeft);
            UIKit.Place(_players.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(36f, -170f), new Vector2(700f, 40f));
            for (int i = 0; i < NetSession.MaxPlayers; i++) _rows.Add(BuildRow(left.transform, i, art));

            // right: map + rules
            var right = UIKit.Panel9(page, "Rules", ArtIds.PanelDark, Color.white, 2.8f);
            UIKit.Place(right.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(430f, 0f), new Vector2(1020f, 600f));
            right.gameObject.AddComponent<SlideIn>().From = new Vector2(500f, 0f);
            _thumbHolder = UIKit.Rect(right.transform, "Thumb");
            UIKit.Place(_thumbHolder, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(30f, -30f), new Vector2(360f, 210f));
            _mapName = UIKit.Label(right.transform, "MapName", "", 32, Color.white, TextAnchor.MiddleCenter);
            UIKit.Place(_mapName.rectTransform, new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), new Vector2(210f, -270f), new Vector2(360f, 44f));
            var prev = UIKit.ArtButton(right.transform, "MapPrev", "<", ArtIds.PanelBlue, new Vector2(90f, 80f), 40, () => StepMap(-1));
            UIKit.Place((RectTransform)prev.transform, new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), new Vector2(110f, -340f), new Vector2(90f, 80f));
            var next = UIKit.ArtButton(right.transform, "MapNext", ">", ArtIds.PanelBlue, new Vector2(90f, 80f), 40, () => StepMap(1));
            UIKit.Place((RectTransform)next.transform, new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), new Vector2(310f, -340f), new Vector2(90f, 80f));
            var ml = UIKit.Label(right.transform, "MapLabel", "MAP", 24, UIKit.Sky, TextAnchor.MiddleCenter);
            UIKit.Place(ml.rectTransform, new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), new Vector2(210f, -340f), new Vector2(100f, 40f));
            _mapPrev = prev.gameObject;
            _mapNext = next.gameObject;
            _hint = UIKit.Label(right.transform, "Hint", "", 22, new Color(0.75f, 0.8f, 0.92f), TextAnchor.MiddleCenter);
            UIKit.Place(_hint.rectTransform, new Vector2(0f, 0f), new Vector2(0.5f, 0f), new Vector2(210f, 40f), new Vector2(360f, 90f));

            float x = 430f, y = -52f, step = 76f;
            _mode = new OptionRow(right.transform, "MODE", new[] { "FREE FOR ALL", "TEAM BATTLE" }, 0, x, y, i => Edit(s => s.Mode = (MatchMode)i));
            _time = new OptionRow(right.transform, "TIME", new[] { "2 MIN", "3 MIN", "5 MIN", "8 MIN", "10 MIN" }, 2, x, y -= step, i => Edit(s => s.TimeLimit = TimeOptions[i]));
            _kills = new OptionRow(right.transform, "KILL LIMIT", new[] { "10", "20", "30", "50", "NONE" }, 1, x, y -= step, i => Edit(s => s.KillLimit = KillOptions[i]));
            _bots = new OptionRow(right.transform, "BOTS", Array.ConvertAll(BotOptions, b => b == 0 ? "NONE" : b.ToString()), 6, x, y -= step, i => Edit(s => s.BotCount = BotOptions[i]));
            _skill = new OptionRow(right.transform, "BOT SKILL", new[] { "EASY", "NORMAL", "HARD" }, 1, x, y -= step, i => Edit(s => s.Difficulty = i));
            _night = new OptionRow(right.transform, "TIME OF DAY", new[] { "DAY", "NIGHT" }, 0, x, y -= step, i => Edit(s => s.Night = i == 1));
            _fog = new OptionRow(right.transform, "LINE OF SIGHT", new[] { "ON", "OFF" }, 0, x, y -= step, i => Edit(s => s.Fog = i == 0));

            // bottom: leave / status / start
            var leave = UIKit.ArtButton(page, "Leave", "LEAVE", ArtIds.PanelOrange, new Vector2(300f, 120f), 44, () => _net.Leave(),
                cat != null ? cat.Icon(ArtIds.IconHome) : null);
            UIKit.Place((RectTransform)leave.transform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(30f, 24f), new Vector2(300f, 120f));
            _wait = UIKit.Label(page, "Wait", "", 34, UIKit.Cream, TextAnchor.MiddleCenter);
            UIKit.Place(_wait.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(120f, 60f), new Vector2(660f, 50f));
            _wait.resizeTextForBestFit = true;
            _wait.resizeTextMinSize = 22;
            _wait.resizeTextMaxSize = 34;
            _wait.horizontalOverflow = HorizontalWrapMode.Wrap;
            _wait.verticalOverflow = VerticalWrapMode.Truncate;
            var holder = UIKit.Rect(page, "StartHolder");
            UIKit.Place(holder, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-30f, 20f), new Vector2(480f, 130f));
            holder.gameObject.AddComponent<Pulse>().Amount = 0.03f;
            _start = UIKit.ArtButton(holder, "Start", "START", ArtIds.PanelGold, new Vector2(480f, 130f), 60, () => _net.HostStartMatch(),
                cat != null ? cat.Icon(ArtIds.IconPlay) : null);
            UIKit.Stretch((RectTransform)_start.transform);
            Shine.Add((RectTransform)_start.transform, art);
            _startLabel = _start.transform.Find("Label").GetComponent<Text>();
            RoomChatPanel.Build(page, Ctx);
        }

        private Row BuildRow(Transform parent, int i, ArtLibrary art)
        {
            int col = i / 6, line = i % 6;
            var bg = UIKit.Image(parent, "Row" + i, art.RoundRect, new Color(0f, 0f, 0f, 0.42f), true, true);
            UIKit.Place(bg.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(30f + col * 375f, -224f - line * 64f), new Vector2(360f, 56f));
            var r = new Row { Root = bg.gameObject };
            int index = i;
            var click = bg.gameObject.AddComponent<Button>();
            click.targetGraphic = bg;
            click.onClick.AddListener(() => ToggleMute(index));
            r.Dot = UIKit.Image(bg.transform, "Dot", art.Circle, Color.white);
            UIKit.Place(r.Dot.rectTransform, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(30f, 0f), new Vector2(30f, 30f));
            r.Name = UIKit.Label(bg.transform, "Name", "", 26, Color.white, TextAnchor.MiddleLeft);
            UIKit.Place(r.Name.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(56f, 0f), new Vector2(220f, 44f));
            r.Name.resizeTextForBestFit = true;
            r.Name.resizeTextMinSize = 16;
            r.Name.resizeTextMaxSize = 26;
            r.Tag = UIKit.Label(bg.transform, "Tag", "", 20, UIKit.Gold, TextAnchor.MiddleRight);
            UIKit.Place(r.Tag.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-14f, 0f), new Vector2(90f, 40f));
            // "+": send a friend request to someone in the room
            var add = FriendsArt.Plus(bg.transform, 46f, () =>
            {
                if (index >= _net.Members.Count) return;
                var m = _net.Members[index];
                Ctx.Friends.AddById(m.PlayerId, m.Name);
                _rows[index].Add.SetActive(false);
            });
            UIKit.Place((RectTransform)add.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-6f, 0f), new Vector2(46f, 46f));
            r.Add = add.gameObject;
            return r;
        }

        private void BuildBusy(RectTransform safe)
        {
            var art = Ctx.Art;
            var dim = UIKit.Image(safe, "Busy", art.Square, UIKit.Dim(0.01f, 0.02f, 0.05f, 0.9f), false, true);
            UIKit.Stretch(dim.rectTransform, -400f);
            _busy = dim.gameObject;
            var ring = UIKit.Image(dim.transform, "Spinner", art.ThinRing, UIKit.Gold);
            UIKit.Place(ring.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 80f), new Vector2(150f, 150f));
            _spinner = ring.rectTransform;
            var dot = UIKit.Image(ring.transform, "Dot", art.Circle, Color.white);
            UIKit.Place(dot.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -6f), new Vector2(26f, 26f));
            _busyText = UIKit.Title(dim.transform, "Text", "", 56, UIKit.Cream, UIKit.Gold);
            UIKit.Place(_busyText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -60f), new Vector2(1400f, 80f));
            var cancel = UIKit.ArtButton(dim.transform, "Cancel", "CANCEL", ArtIds.PanelDark, new Vector2(320f, 100f), 40, () => _net.Cancel());
            UIKit.Place((RectTransform)cancel.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -190f), new Vector2(320f, 100f));
            _busy.SetActive(false);
        }

        internal static InputField Field(Transform parent, string name, string placeholder, int fontSize, Vector2 size)
        {
            var bg = UIKit.Image(parent, name, UIKit.Art.RoundRect, new Color(0f, 0f, 0f, 0.6f), true, true);
            bg.rectTransform.sizeDelta = size;
            var text = UIKit.Label(bg.transform, "Text", "", fontSize, Color.white, TextAnchor.MiddleCenter, false);
            UIKit.Stretch(text.rectTransform, 14f);
            text.supportRichText = false;
            var ph = UIKit.Label(bg.transform, "Placeholder", placeholder, fontSize, new Color(1f, 1f, 1f, 0.3f), TextAnchor.MiddleCenter, false);
            UIKit.Stretch(ph.rectTransform, 14f);
            var f = bg.gameObject.AddComponent<InputField>();
            f.textComponent = text;
            f.placeholder = ph;
            f.targetGraphic = bg;
            f.caretColor = UIKit.Gold;
            f.customCaretColor = true;
            f.caretWidth = 4;
            f.selectionColor = new Color(1f, 0.82f, 0.3f, 0.35f);
            return f;
        }

        // ================================================================== behaviour

        protected override void OnShow()
        {
            var p = Ctx.Profile;
            if (string.IsNullOrEmpty(p.Data.Name) || p.Data.Name == "PLAYER")
            {
                p.Data.Name = "BRAWLER" + UnityEngine.Random.Range(100, 1000);
                p.Save();
                Ctx.Friends?.SyncName();
            }
            _name.text = p.Data.Name;
            Ctx.Voice?.AskMicEarly();
            _net.Changed += Refresh;
            _thumbMap = -1;
            Refresh();
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            if (_net != null) _net.Changed -= Refresh;
        }

        /// <summary>Back: leave the room, or stop connecting, or return to the previous page.</summary>
        public void GoBack()
        {
            if (_net.InRoom) _net.Leave();
            else if (_net.State == NetSession.Status.Busy) _net.Cancel();
            else Ctx.UI.Back();
        }

        private void Refresh()
        {
            if (this == null) return;
            bool busy = _net.State == NetSession.Status.Busy;
            bool room = _net.InRoom;
            _busy.SetActive(busy);
            _busyText.text = _net.StatusText;
            _entry.SetActive(!room);
            _room.SetActive(room);
            if (Bar.Title != null) Bar.Title.text = room ? (_net.QuickRoom ? "PUBLIC ROOM" : "ROOM") : "ONLINE";
            if (room && !_wasInRoom)
            {
                Ctx.Sfx.PlayUi(SfxId.UiOpen, 0.8f);
                if (!_net.QuickRoom) Ctx.UI.Toast("Keep the game open while you wait. Tap INVITE to call friends.");
            }
            _wasInRoom = room;
            if (!room) return;

            bool host = _net.IsHost;
            _code.text = string.IsNullOrEmpty(_net.Code) ? "…" : _net.Code;
            _copy.SetActive(!string.IsNullOrEmpty(_net.Code) && _net.Code != "LAN");
            _invite.SetActive(_copy.activeSelf);
            var hub = Ctx.Friends;
            var members = _net.Members;
            int bots = Mathf.Min(_net.Settings.BotCount, NetSession.MaxPlayers - members.Count);
            _players.text = "PLAYERS  " + members.Count + " / " + NetSession.MaxPlayers + (bots > 0 ? "     + " + bots + " BOTS" : "");
            for (int i = 0; i < _rows.Count; i++)
            {
                var r = _rows[i];
                bool used = i < members.Count;
                if (r.Root.activeSelf != used) r.Root.SetActive(used);
                if (!used) continue;
                var m = members[i];
                bool me = m.ClientId == _net.LocalId;
                r.Name.text = m.Name;
                r.Name.color = me ? new Color(1f, 0.92f, 0.35f) : Color.white;
                r.Dot.color = m.Look.Outfit;
                bool muted = !me && Ctx.Chat.IsMuted(m.ClientId);
                bool friend = !me && !string.IsNullOrEmpty(m.PlayerId) && hub.IsFriend(m.PlayerId);
                bool canAdd = !me && !friend && !string.IsNullOrEmpty(m.PlayerId) && hub.State == FriendsHub.Phase.Ready && !hub.IsPending(m.PlayerId);
                if (r.Add.activeSelf != canAdd) r.Add.SetActive(canAdd);
                r.Tag.text = muted ? "MUTED" : (i == 0 ? "HOST" : (me ? "YOU" : (friend ? "FRIEND" : "")));
                r.Tag.color = muted ? UIKit.Red : (i == 0 ? UIKit.Gold : (friend ? FriendsArt.Online : UIKit.Sky));
                r.Tag.rectTransform.anchoredPosition = new Vector2(canAdd ? -58f : -14f, 0f);
            }

            var s = _net.Settings;
            _mode.SetIndex((int)s.Mode);
            _time.SetIndex(Mathf.Max(0, Array.IndexOf(TimeOptions, s.TimeLimit)));
            _kills.SetIndex(Mathf.Max(0, Array.IndexOf(KillOptions, s.KillLimit)));
            _bots.SetIndex(Mathf.Clamp(s.BotCount, 0, BotOptions.Length - 1));
            _skill.SetIndex(s.Difficulty);
            _night.SetIndex(s.Night ? 1 : 0);
            _fog.SetIndex(s.Fog ? 0 : 1);
            _mode.SetEditable(host); _time.SetEditable(host); _kills.SetEditable(host); _bots.SetEditable(host);
            _skill.SetEditable(host); _night.SetEditable(host); _fog.SetEditable(host);
            _mapPrev.SetActive(host);
            _mapNext.SetActive(host);
            _hint.text = host ? "Your unlocked maps are\nplayable by everyone" : "The host picks the map\nand the rules";
            if (_thumbMap != _net.MapIndex)
            {
                _thumbMap = _net.MapIndex;
                for (int i = _thumbHolder.childCount - 1; i >= 0; i--) Destroy(_thumbHolder.GetChild(i).gameObject);
                var thumb = MapThumb.Build(_thumbHolder, Ctx, _thumbMap, new Vector2(360f, 210f));
                UIKit.Stretch(thumb);
                _mapName.text = MapLayouts.Get(_thumbMap).Name;
                Ctx.App.PreviewMap(_thumbMap);
            }
            _start.gameObject.SetActive(host);
            UpdateWaitText();
        }

        private void UpdateWaitText()
        {
            float auto = _net.AutoStartLeft;
            string t;
            if (_net.MatchRunning) t = "MATCH IN PROGRESS…";
            else if (auto > 0f) t = "STARTING IN " + Mathf.CeilToInt(auto);
            else if (_net.IsHost) t = _net.Members.Count < 2 ? "SHARE THE CODE, OR START WITH BOTS" : "READY WHEN YOU ARE";
            else t = "WAITING FOR THE HOST TO START…";
            if (_wait.text != t) _wait.text = t;
            if (_startLabel != null)
            {
                string label = _net.MatchRunning ? "IN MATCH" : (auto > 0f ? "START NOW" : "START");
                if (_startLabel.text != label) _startLabel.text = label;
                _start.interactable = !_net.MatchRunning;
            }
        }

        private void Update()
        {
            if (_busy.activeSelf) _spinner.Rotate(0f, 0f, -300f * Time.unscaledDeltaTime);
            if (_room.activeSelf) UpdateWaitText();
        }

        private void Edit(Action<MatchSettingsBox> change)
        {
            if (!_net.IsHost) return;
            var box = new MatchSettingsBox { S = _net.Settings };
            change(box);
            _net.HostSetSettings(box.S, _net.MapIndex);
        }

        /// <summary>Lets the option lambdas change a struct.</summary>
        private sealed class MatchSettingsBox
        {
            public MatchSettings S;
            public MatchMode Mode { set => S.Mode = value; }
            public float TimeLimit { set => S.TimeLimit = value; }
            public int KillLimit { set => S.KillLimit = value; }
            public int BotCount { set => S.BotCount = value; }
            public int Difficulty { set => S.Difficulty = value; }
            public bool Night { set => S.Night = value; }
            public bool Fog { set => S.Fog = value; }
        }

        private void StepMap(int d)
        {
            if (!_net.IsHost) return;
            int n = MapLayouts.All.Length;
            int m = _net.MapIndex;
            for (int i = 0; i < n; i++)
            {
                m = (m + d + n) % n;
                if (Ctx.Profile.OwnsMap(m)) break;
            }
            _net.HostSetSettings(_net.Settings, m);
        }

        /// <summary>Tap a player to mute or unmute their chat (this phone only).</summary>
        private void ToggleMute(int row)
        {
            var members = _net.Members;
            if (row >= members.Count) return;
            var m = members[row];
            if (m.ClientId == _net.LocalId) { Ctx.UI.Toast("Tap another player to mute their chat"); return; }
            bool muted = Ctx.Chat.ToggleMute(m.ClientId);
            Ctx.Voice?.MutePlayer(m.ClientId, muted);
            Ctx.UI.Toast(m.Name + (muted ? " muted (chat and voice). Tap again to unmute." : " unmuted"));
            Refresh();
        }

        private void CopyCode()
        {
            GUIUtility.systemCopyBuffer = _net.Code;
            Ctx.UI.Toast("Code " + _net.Code + " copied. Send it to your friends!");
        }
    }
}
