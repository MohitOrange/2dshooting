using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace NeonBrawl
{
    /// <summary>Small pieces shared by the friends popup, the home strip and the invite card.</summary>
    public static class FriendsArt
    {
        private static readonly Color[] Palette =
        {
            new Color(0.95f, 0.45f, 0.35f), new Color(0.35f, 0.7f, 1f), new Color(0.5f, 0.85f, 0.4f), new Color(1f, 0.75f, 0.25f),
            new Color(0.75f, 0.5f, 1f), new Color(0.3f, 0.85f, 0.8f), new Color(1f, 0.5f, 0.75f), new Color(0.6f, 0.65f, 0.75f),
        };

        public static readonly Color Online = new Color(0.35f, 0.95f, 0.4f);
        public static readonly Color Offline = new Color(0.55f, 0.58f, 0.65f);

        public static Color ColorFor(string name)
        {
            int h = 0;
            if (!string.IsNullOrEmpty(name)) foreach (char c in name) h = h * 31 + c;
            return Palette[(h & 0x7fffffff) % Palette.Length];
        }

        /// <summary>Round avatar with the first letter of the name and an online dot.</summary>
        public static Image Avatar(Transform parent, string name, bool online, float size, out Image dot)
        {
            var art = UIKit.Art;
            var c = UIKit.Image(parent, "Avatar", art.Circle, ColorFor(name));
            c.rectTransform.sizeDelta = new Vector2(size, size);
            var ring = UIKit.Image(c.transform, "Ring", art.Ring, new Color(1f, 1f, 1f, 0.55f));
            UIKit.Stretch(ring.rectTransform);
            string shown = UgsBoot.ShortName(name);
            var letter = UIKit.Label(c.transform, "Letter", shown.Length > 0 ? shown.Substring(0, 1) : "?", Mathf.RoundToInt(size * 0.5f), Color.white, TextAnchor.MiddleCenter);
            UIKit.Stretch(letter.rectTransform);
            dot = UIKit.Image(c.transform, "Dot", art.Circle, online ? Online : Offline);
            float d = size * 0.3f;
            UIKit.Place(dot.rectTransform, new Vector2(1f, 0f), new Vector2(0.5f, 0.5f), new Vector2(-d * 0.35f, d * 0.35f), new Vector2(d, d));
            var dr = UIKit.Image(dot.transform, "Edge", art.Ring, new Color(0.05f, 0.06f, 0.1f, 1f));
            UIKit.Stretch(dr.rectTransform, -2f);
            return c;
        }

        /// <summary>Round gold "+" button (invite / add).</summary>
        public static Button Plus(Transform parent, float size, Action onClick)
        {
            var b = UIKit.ArtButton(parent, "Plus", "+", ArtIds.PanelGold, new Vector2(size, size), Mathf.RoundToInt(size * 0.62f), onClick);
            var label = b.transform.Find("Label");
            if (label != null) ((RectTransform)label).offsetMin = ((RectTransform)label).offsetMax = Vector2.zero;
            return b;
        }
    }

    // =====================================================================================
    /// <summary>
    /// FRIENDS popup with three tabs. FRIENDS: everyone you added, online first, with INVITE (+) or JOIN
    /// when they are in a room. REQUESTS: accept or decline, and cancel the ones you sent.
    /// ADD FRIEND: your code to share, add by code, and people you played with recently.
    /// </summary>
    public sealed class FriendsPopup : MonoBehaviour
    {
        public const int TabFriends = 0, TabRequests = 1, TabAdd = 2;
        private const float RowW = 1360f, RowH = 104f;

        private GameContext _ctx;
        private RectTransform _panel;
        private readonly Image[] _tabs = new Image[3];
        private readonly RectTransform[] _pages = new RectTransform[3];
        private RectTransform _friendList, _requestList, _recentList;
        private Text _friendEmpty, _requestEmpty, _recentEmpty, _myCode, _addResult, _requestBadge;
        private GameObject _requestBadgeRoot;
        private InputField _codeField;
        private int _tab;
        private bool _dirty;
        private readonly HashSet<string> _confirmRemove = new HashSet<string>();

        public bool IsOpen => gameObject.activeSelf;

        public static FriendsPopup Build(RectTransform parent, GameContext ctx)
        {
            var root = UIKit.Rect(parent, "FriendsPopup");
            UIKit.Stretch(root);
            var p = root.gameObject.AddComponent<FriendsPopup>();
            p.Init(ctx, root);
            root.gameObject.SetActive(false);
            return p;
        }

        private void Init(GameContext ctx, RectTransform root)
        {
            _ctx = ctx;
            var art = ctx.Art;
            var dim = UIKit.Image(root, "Dim", art.Square, UIKit.Dim(0f, 0f, 0.03f, 0.75f), false, true);
            UIKit.Stretch(dim.rectTransform, -400f);

            var panel = UIKit.Panel9(root, "Panel", ArtIds.PanelDark, Color.white, 3f, true);
            _panel = panel.rectTransform;
            UIKit.Place(_panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -24f), new Vector2(1500f, 820f));
            _panel.gameObject.AddComponent<PopIn>().Duration = 0.28f;
            var solid = UIKit.Image(_panel, "Backing", art.RoundRect, new Color(0.07f, 0.08f, 0.12f, 0.92f), true);
            UIKit.Stretch(solid.rectTransform, 12f);
            solid.transform.SetAsFirstSibling();

            var ribbon = UIKit.Panel9(_panel, "Ribbon", ArtIds.PanelBlue, Color.white, 3f);
            UIKit.Place(ribbon.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, 4f), new Vector2(640f, 120f));
            var title = UIKit.Title(ribbon.transform, "Title", "FRIENDS", 64, Color.white, UIKit.Sky);
            UIKit.Stretch(title.rectTransform);

            var close = UIKit.ArtButton(_panel, "Close", "X", ArtIds.PanelOrange, new Vector2(88f, 88f), 46, Close);
            UIKit.Place((RectTransform)close.transform, new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(-30f, -30f), new Vector2(88f, 88f));
            close.GetComponent<ButtonJuice>().Sound = SfxId.UiBack;

            string[] names = { "FRIENDS", "REQUESTS", "ADD FRIEND" };
            for (int i = 0; i < 3; i++)
            {
                int t = i;
                var b = UIKit.ArtButton(_panel, "Tab" + i, names[i], ArtIds.PanelSlate, new Vector2(380f, 84f), 34, () => ShowTab(t));
                UIKit.Place((RectTransform)b.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2((i - 1) * 400f, -112f), new Vector2(380f, 84f));
                _tabs[i] = (Image)b.targetGraphic;
                if (i == TabRequests)
                {
                    _requestBadge = UIKit.Badge(b.transform, "0", UIKit.Red, new Vector2(-14f, -8f), Vector2.one, 26);
                    _requestBadgeRoot = _requestBadge.transform.parent.gameObject;
                    _requestBadgeRoot.AddComponent<Pulse>().Amount = 0.1f;
                }
                _pages[i] = UIKit.Rect(_panel, "Page" + i);
                UIKit.Stretch(_pages[i]);
            }

            _friendList = List(_pages[TabFriends], new Vector2(0f, -170f), 600f, out _friendEmpty);
            _requestList = List(_pages[TabRequests], new Vector2(0f, -170f), 600f, out _requestEmpty);
            BuildAddPage(_pages[TabAdd], art);
        }

        private void BuildAddPage(RectTransform page, ArtLibrary art)
        {
            // your code
            var mine = UIKit.Image(page, "Mine", art.RoundRect, new Color(0f, 0f, 0f, 0.4f), true);
            UIKit.Place(mine.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-350f, -168f), new Vector2(660f, 230f));
            var ml = UIKit.Label(mine.transform, "L", "YOUR FRIEND CODE", 28, UIKit.Sky, TextAnchor.MiddleCenter);
            UIKit.Place(ml.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -14f), new Vector2(620f, 40f));
            _myCode = UIKit.Title(mine.transform, "Code", "…", 54, UIKit.Cream, UIKit.Gold);
            UIKit.Place(_myCode.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -56f), new Vector2(620f, 74f));
            var copy = UIKit.ArtButton(mine.transform, "Copy", "COPY CODE", ArtIds.PanelBlue, new Vector2(300f, 76f), 32, CopyCode);
            UIKit.Place((RectTransform)copy.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 18f), new Vector2(300f, 76f));

            // add by code
            var add = UIKit.Image(page, "Add", art.RoundRect, new Color(0f, 0f, 0f, 0.4f), true);
            UIKit.Place(add.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(350f, -168f), new Vector2(660f, 230f));
            var al = UIKit.Label(add.transform, "L", "ADD A FRIEND BY CODE", 28, UIKit.Sky, TextAnchor.MiddleCenter);
            UIKit.Place(al.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -14f), new Vector2(620f, 40f));
            _codeField = OnlineView.Field(add.transform, "CodeField", "NAME#1234", 40, new Vector2(400f, 84f));
            UIKit.Place((RectTransform)_codeField.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -62f), new Vector2(400f, 84f));
            _codeField.characterLimit = 30;
            _codeField.onValidateInput = (s, i, c) => char.IsLetterOrDigit(c) || c == '#' || c == '_' || c == '-' || c == '.' || c == ' ' ? char.ToUpperInvariant(c) : '\0';
            var send = UIKit.ArtButton(add.transform, "Send", "SEND", ArtIds.PanelGold, new Vector2(200f, 84f), 36, SendRequest);
            UIKit.Place((RectTransform)send.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -62f), new Vector2(200f, 84f));
            _addResult = UIKit.Label(add.transform, "Result", "Ask your friend for their code", 24, new Color(0.8f, 0.85f, 0.95f), TextAnchor.MiddleCenter);
            UIKit.Place(_addResult.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 22f), new Vector2(620f, 40f));

            var rl = UIKit.Label(page, "RecentLabel", "PLAYED WITH RECENTLY", 28, UIKit.Gold, TextAnchor.MiddleLeft);
            UIKit.Place(rl.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -424f), new Vector2(RowW, 40f));
            _recentList = List(page, new Vector2(0f, -452f), 320f, out _recentEmpty);
        }

        private RectTransform List(RectTransform page, Vector2 top, float height, out Text empty)
        {
            var view = UIKit.Rect(page, "Viewport");
            UIKit.Place(view, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), top, new Vector2(RowW + 20f, height));
            view.gameObject.AddComponent<RectMask2D>();
            var hit = view.gameObject.AddComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0.001f);
            var content = UIKit.Rect(view, "Content");
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;
            var vl = content.gameObject.AddComponent<VerticalLayoutGroup>();
            vl.spacing = 10f;
            vl.padding = new RectOffset(10, 10, 4, 4);
            vl.childAlignment = TextAnchor.UpperCenter;
            vl.childControlHeight = vl.childControlWidth = false;
            vl.childForceExpandHeight = vl.childForceExpandWidth = false;
            var fit = content.gameObject.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var scroll = view.gameObject.AddComponent<ScrollRect>();
            scroll.content = content;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.scrollSensitivity = 30f;
            empty = UIKit.Label(view, "Empty", "", 30, new Color(0.78f, 0.84f, 0.95f), TextAnchor.MiddleCenter);
            empty.horizontalOverflow = HorizontalWrapMode.Wrap;
            UIKit.Stretch(empty.rectTransform, 40f);
            return content;
        }

        // ------------------------------------------------------------- open / close

        public void Open(int tab)
        {
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            _ctx.Sfx.PlayUi(SfxId.UiOpen, 0.7f);
            _ctx.Friends.Changed += MarkDirty;
            if (_ctx.Friends.State == FriendsHub.Phase.Failed || _ctx.Friends.State == FriendsHub.Phase.Offline) _ctx.Friends.Connect();
            _confirmRemove.Clear();
            ShowTab(tab);
        }

        public void Close()
        {
            if (!gameObject.activeSelf) return;
            _ctx.Friends.Changed -= MarkDirty;
            gameObject.SetActive(false);
        }

        private void OnDisable()
        {
            if (_ctx != null) _ctx.Friends.Changed -= MarkDirty;
        }

        private void MarkDirty() => _dirty = true;

        private void LateUpdate()
        {
            if (!_dirty) return;
            _dirty = false;
            Redraw();
        }

        private void ShowTab(int t)
        {
            _tab = t;
            for (int i = 0; i < 3; i++)
            {
                _pages[i].gameObject.SetActive(i == t);
                var s = _ctx.Catalog != null ? _ctx.Catalog.Panel(i == t ? ArtIds.PanelGold : ArtIds.PanelSlate) : null;
                if (s != null) _tabs[i].sprite = s;
                var label = _tabs[i].transform.Find("Label");
                if (label != null) label.GetComponent<Text>().color = i == t ? UIKit.TextOn(ArtIds.PanelGold) : new Color(0.82f, 0.87f, 0.96f);
            }
            Redraw();
        }

        // ------------------------------------------------------------- drawing

        private void Redraw()
        {
            var hub = _ctx.Friends;
            int requests = hub.Incoming.Count;
            _requestBadgeRoot.SetActive(requests > 0);
            _requestBadge.text = requests.ToString();
            _myCode.text = string.IsNullOrEmpty(hub.MyCode) ? "…" : UgsBoot.Display(hub.MyCode);

            string status = hub.State == FriendsHub.Phase.Ready ? null
                : hub.State == FriendsHub.Phase.Connecting ? "Connecting to friends…"
                : "Friends need the internet. Check your connection.";

            if (_tab == TabFriends)
            {
                Clear(_friendList);
                foreach (var f in hub.Friends) FriendRow(_friendList, f);
                _friendEmpty.text = status ?? (hub.Friends.Count == 0 ? "No friends yet.\nOpen ADD FRIEND and share your code." : "");
            }
            else if (_tab == TabRequests)
            {
                Clear(_requestList);
                if (hub.Incoming.Count > 0) Header(_requestList, "WANT TO BE YOUR FRIEND");
                foreach (var f in hub.Incoming) IncomingRow(_requestList, f);
                if (hub.Outgoing.Count > 0) Header(_requestList, "YOU ASKED");
                foreach (var f in hub.Outgoing) OutgoingRow(_requestList, f);
                _requestEmpty.text = status ?? (hub.Incoming.Count + hub.Outgoing.Count == 0 ? "No requests right now." : "");
            }
            else
            {
                Clear(_recentList);
                foreach (var f in hub.Recent) RecentRow(_recentList, f);
                _recentEmpty.text = hub.Recent.Count == 0 ? "People you meet in online rooms show up here." : "";
            }
        }

        private static void Clear(RectTransform list)
        {
            for (int i = list.childCount - 1; i >= 0; i--) Destroy(list.GetChild(i).gameObject);
        }

        private static void Header(RectTransform list, string text)
        {
            var rt = UIKit.Rect(list, "Header");
            rt.sizeDelta = new Vector2(RowW, 44f);
            var t = UIKit.Label(rt, "T", text, 26, UIKit.Gold, TextAnchor.MiddleLeft);
            UIKit.Stretch(t.rectTransform);
            t.rectTransform.offsetMin = new Vector2(12f, 0f);
        }

        private RectTransform Row(RectTransform list, FriendInfo f, bool online, string status, Color statusColor)
        {
            var bg = UIKit.Image(list, "Row", UIKit.Art.RoundRect, new Color(1f, 1f, 1f, online ? 0.09f : 0.05f), true);
            var rt = bg.rectTransform;
            rt.sizeDelta = new Vector2(RowW, RowH);
            var av = FriendsArt.Avatar(rt, f.Name, online, 78f, out _);
            UIKit.Place(av.rectTransform, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(60f, 0f), new Vector2(78f, 78f));
            var name = UIKit.Label(rt, "Name", UgsBoot.Display(f.Name), 34, Color.white, TextAnchor.MiddleLeft);
            UIKit.Place(name.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(118f, 17f), new Vector2(620f, 44f));
            var st = UIKit.Label(rt, "Status", status, 24, statusColor, TextAnchor.MiddleLeft);
            UIKit.Place(st.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(118f, -22f), new Vector2(620f, 34f));
            return rt;
        }

        private static Button RightButton(RectTransform row, string label, int panel, float width, float right, Action onClick)
        {
            var b = UIKit.ArtButton(row, label, label, panel, new Vector2(width, 76f), 32, onClick);
            UIKit.Place((RectTransform)b.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-right, 0f), new Vector2(width, 76f));
            return b;
        }

        private void FriendRow(RectTransform list, FriendInfo f)
        {
            var rt = Row(list, f, f.Online, f.StatusText, f.Online ? FriendsArt.Online : FriendsArt.Offline);
            float right = 20f;
            bool confirm = _confirmRemove.Contains(f.Id);
            RightButton(rt, confirm ? "SURE?" : "REMOVE", confirm ? ArtIds.PanelOrange : ArtIds.PanelDark, 170f, right, () =>
            {
                if (_confirmRemove.Remove(f.Id)) _ctx.Friends.Remove(f);
                else _confirmRemove.Add(f.Id);
                Redraw();
            });
            right += 190f;
            if (f.InRoom && f.Activity.Room != _ctx.Net.Code)
            {
                RightButton(rt, "JOIN", ArtIds.PanelGold, 220f, right, () => { Close(); _ctx.App.JoinFriendRoom(f.Activity.Room); });
                right += 240f;
            }
            if (f.Online)
                RightButton(rt, "+ INVITE", ArtIds.PanelBlue, 230f, right, () => { _ctx.Friends.Invite(f); Close(); });
        }

        private void IncomingRow(RectTransform list, FriendInfo f)
        {
            var rt = Row(list, f, f.Online, "WANTS TO BE FRIENDS", UIKit.Gold);
            RightButton(rt, "DECLINE", ArtIds.PanelDark, 210f, 20f, () => _ctx.Friends.Decline(f));
            RightButton(rt, "ACCEPT", ArtIds.PanelGold, 230f, 250f, () => _ctx.Friends.Accept(f));
        }

        private void OutgoingRow(RectTransform list, FriendInfo f)
        {
            var rt = Row(list, f, false, "WAITING FOR AN ANSWER", FriendsArt.Offline);
            RightButton(rt, "CANCEL", ArtIds.PanelDark, 210f, 20f, () => _ctx.Friends.CancelRequest(f));
        }

        private void RecentRow(RectTransform list, FriendInfo f)
        {
            var hub = _ctx.Friends;
            bool friend = hub.IsFriend(f.Id), pending = hub.IsPending(f.Id);
            var rt = Row(list, f, false, friend ? "FRIEND" : pending ? "REQUEST SENT" : "PLAYED WITH YOU", friend ? FriendsArt.Online : FriendsArt.Offline);
            if (!friend && !pending) RightButton(rt, "+ ADD", ArtIds.PanelGold, 200f, 20f, () => hub.AddById(f.Id, f.Name));
        }

        // ------------------------------------------------------------- add page

        private void CopyCode()
        {
            var code = _ctx.Friends.MyCode;
            if (string.IsNullOrEmpty(code)) return;
            GUIUtility.systemCopyBuffer = code;
            _ctx.UI.Toast("Code copied: " + UgsBoot.Display(code));
        }

        private void SendRequest()
        {
            string code = _codeField.text;
            _addResult.text = "Sending…";
            _addResult.color = UIKit.Sky;
            _ctx.Friends.AddByCode(code, (ok, message) =>
            {
                if (this == null) return;
                _addResult.text = message;
                _addResult.color = ok ? FriendsArt.Online : UIKit.Red;
                _ctx.Sfx.PlayUi(ok ? SfxId.UiCoin : SfxId.UiError, 0.6f);
                if (ok) _codeField.text = "";
            });
        }
    }

    // =====================================================================================
    /// <summary>"MOHIT invites you!" card that slides down from the top, with JOIN and LATER.</summary>
    public sealed class InviteCard : MonoBehaviour
    {
        private const float Life = 25f;

        private GameContext _ctx;
        private RectTransform _card;
        private Text _text, _room;
        private Image _timer;
        private Transform _avatarHolder;
        private FriendInvite _invite;
        private float _t;

        public static InviteCard Build(RectTransform parent, GameContext ctx)
        {
            var root = UIKit.Rect(parent, "InviteCard");
            UIKit.Place(root, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(1000f, 170f));
            var c = root.gameObject.AddComponent<InviteCard>();
            c.Init(ctx, root);
            root.gameObject.SetActive(false);
            return c;
        }

        private void Init(GameContext ctx, RectTransform root)
        {
            _ctx = ctx;
            _card = root;
            var bg = UIKit.Panel9(root, "Bg", ArtIds.PanelBlue, Color.white, 3f, true);
            UIKit.Stretch(bg.rectTransform);
            UIKit.DropShadow(root, 0.6f, 10f);
            _avatarHolder = UIKit.Rect(root, "AvatarHolder");
            UIKit.Place((RectTransform)_avatarHolder, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(80f, 4f), new Vector2(100f, 100f));
            _text = UIKit.Label(root, "Text", "", 36, Color.white, TextAnchor.MiddleLeft);
            UIKit.Place(_text.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(146f, 22f), new Vector2(470f, 50f));
            _room = UIKit.Label(root, "Room", "", 24, UIKit.Cream, TextAnchor.MiddleLeft);
            UIKit.Place(_room.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(146f, -24f), new Vector2(470f, 34f));
            var join = UIKit.ArtButton(root, "Join", "JOIN", ArtIds.PanelGold, new Vector2(200f, 96f), 44, Join);
            UIKit.Place((RectTransform)join.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-170f, 4f), new Vector2(200f, 96f));
            var later = UIKit.ArtButton(root, "Later", "LATER", ArtIds.PanelDark, new Vector2(140f, 96f), 30, Hide);
            UIKit.Place((RectTransform)later.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-20f, 4f), new Vector2(140f, 96f));
            later.GetComponent<ButtonJuice>().Sound = SfxId.UiBack;
            _timer = UIKit.Image(root, "Timer", ctx.Art.Square, new Color(1f, 0.85f, 0.3f, 0.9f));
            UIKit.Place(_timer.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(18f, 14f), new Vector2(964f, 6f));
        }

        public void Show(FriendInvite inv)
        {
            _invite = inv;
            _t = 0f;
            for (int i = _avatarHolder.childCount - 1; i >= 0; i--) Destroy(_avatarHolder.GetChild(i).gameObject);
            var av = FriendsArt.Avatar(_avatarHolder, inv.FromName, true, 96f, out _);
            UIKit.Stretch(av.rectTransform);
            _text.text = UgsBoot.ShortName(inv.FromName) + " invites you!";
            _room.text = "ROOM " + inv.Room + "  ·  tap JOIN to play together";
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
        }

        private void Join()
        {
            gameObject.SetActive(false);
            _ctx.App.JoinFriendRoom(_invite.Room);
        }

        public void Hide() => gameObject.SetActive(false);

        private void Update()
        {
            _t += Time.unscaledDeltaTime;
            float slide = Mathf.Clamp01(_t / 0.25f);
            _card.anchoredPosition = new Vector2(0f, Mathf.Lerp(200f, -24f, 1f - (1f - slide) * (1f - slide)));
            _timer.rectTransform.sizeDelta = new Vector2(964f * Mathf.Clamp01(1f - _t / Life), 6f);
            if (_t >= Life) Hide();
        }
    }

    // =====================================================================================
    /// <summary>
    /// Home: friends who are online right now, each with a "+" to invite them (or JOIN when they are
    /// in a room), and a chip that opens the friends popup. Redraws when friends change.
    /// </summary>
    public sealed class FriendsStrip : MonoBehaviour
    {
        private const int MaxChips = 3;
        private const float ChipW = 300f, ChipH = 104f, Gap = 16f;

        private GameContext _ctx;
        private RectTransform _row;
        private Text _title;
        private bool _dirty = true;

        public static FriendsStrip Build(RectTransform parent, GameContext ctx, Vector2 bottomLeft)
        {
            var root = UIKit.Rect(parent, "FriendsStrip");
            UIKit.Place(root, Vector2.zero, Vector2.zero, bottomLeft, new Vector2(MaxChips * (ChipW + Gap) + 120f, ChipH + 46f));
            var s = root.gameObject.AddComponent<FriendsStrip>();
            s._ctx = ctx;
            s._title = UIKit.Label(root, "Title", "FRIENDS", 26, UIKit.Cream, TextAnchor.MiddleLeft);
            UIKit.Place(s._title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(6f, 0f), new Vector2(700f, 38f));
            s._row = UIKit.Rect(root, "Row");
            UIKit.Place(s._row, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(MaxChips * (ChipW + Gap) + 120f, ChipH));
            var slide = root.gameObject.AddComponent<SlideIn>();
            slide.From = new Vector2(-700f, 0f);
            slide.Delay = 0.15f;
            return s;
        }

        private void OnEnable()
        {
            if (_ctx == null) return;
            _ctx.Friends.Changed += MarkDirty;
            _dirty = true;
        }

        private void OnDisable()
        {
            if (_ctx != null) _ctx.Friends.Changed -= MarkDirty;
        }

        private void MarkDirty() => _dirty = true;

        private void LateUpdate()
        {
            if (!_dirty) return;
            _dirty = false;
            Redraw();
        }

        private void Redraw()
        {
            for (int i = _row.childCount - 1; i >= 0; i--) Destroy(_row.GetChild(i).gameObject);
            var hub = _ctx.Friends;
            int online = hub.OnlineCount;
            _title.text = hub.State != FriendsHub.Phase.Ready ? "FRIENDS" : online > 0 ? "FRIENDS  ·  " + online + " ONLINE" : "FRIENDS  ·  NOBODY ONLINE";
            _title.color = online > 0 ? FriendsArt.Online : UIKit.Cream;

            int shown = 0;
            float x = 0f;
            if (hub.State == FriendsHub.Phase.Ready)
                for (int i = 0; i < hub.Friends.Count && shown < MaxChips; i++)
                {
                    var f = hub.Friends[i];
                    if (!f.Online) continue;
                    FriendChip(f, x);
                    x += ChipW + Gap;
                    shown++;
                }
            int more = online - shown;
            // the chip that opens everything: invite / add when nobody is online, "+N" otherwise
            var open = UIKit.GlassButton(_row, "Open", new Vector2(shown == 0 ? 380f : 110f, ChipH),
                () => _ctx.UI.ShowFriends(hub.Friends.Count == 0 ? FriendsPopup.TabAdd : FriendsPopup.TabFriends));
            UIKit.Place((RectTransform)open.transform, Vector2.zero, Vector2.zero, new Vector2(x, 0f), new Vector2(shown == 0 ? 380f : 110f, ChipH));
            if (shown == 0)
            {
                var plus = UIKit.Image(open.transform, "PlusBg", _ctx.Art.Circle, UIKit.Gold);
                UIKit.Place(plus.rectTransform, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(56f, 0f), new Vector2(68f, 68f));
                var pt = UIKit.Label(plus.transform, "P", "+", 50, UIKit.TextOn(ArtIds.PanelGold), TextAnchor.MiddleCenter, false);
                UIKit.Stretch(pt.rectTransform);
                var t = UIKit.Label(open.transform, "T", hub.Friends.Count == 0 ? "ADD FRIENDS" : "INVITE FRIENDS", 32, Color.white, TextAnchor.MiddleLeft);
                UIKit.Place(t.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(104f, 14f), new Vector2(260f, 40f));
                var s = UIKit.Label(open.transform, "S", hub.Incoming.Count > 0 ? hub.Incoming.Count + " NEW REQUEST" + (hub.Incoming.Count > 1 ? "S" : "") : "Share your code", 22,
                    hub.Incoming.Count > 0 ? UIKit.Gold : UIKit.Sky, TextAnchor.MiddleLeft);
                UIKit.Place(s.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(104f, -22f), new Vector2(260f, 32f));
            }
            else
            {
                var t = UIKit.Label(open.transform, "T", more > 0 ? "+" + more : "ALL", 34, Color.white, TextAnchor.MiddleCenter);
                UIKit.Stretch(t.rectTransform);
            }
        }

        private void FriendChip(FriendInfo f, float x)
        {
            var chip = UIKit.GlassButton(_row, "Chip", new Vector2(ChipW, ChipH), () => _ctx.UI.ShowFriends(FriendsPopup.TabFriends));
            var rt = (RectTransform)chip.transform;
            UIKit.Place(rt, Vector2.zero, Vector2.zero, new Vector2(x, 0f), new Vector2(ChipW, ChipH));
            var av = FriendsArt.Avatar(rt, f.Name, true, 70f, out _);
            UIKit.Place(av.rectTransform, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(50f, 0f), new Vector2(70f, 70f));
            var n = UIKit.Label(rt, "Name", UgsBoot.ShortName(f.Name), 28, Color.white, TextAnchor.MiddleLeft);
            UIKit.Place(n.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(92f, 14f), new Vector2(124f, 36f));
            var st = UIKit.Label(rt, "Status", f.InRoom ? "IN A ROOM" : f.Activity != null && f.Activity.State == "match" ? "PLAYING" : "ONLINE", 19,
                FriendsArt.Online, TextAnchor.MiddleLeft);
            UIKit.Place(st.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(92f, -20f), new Vector2(124f, 28f));
            if (f.InRoom && f.Activity.Room != _ctx.Net.Code)
            {
                var join = UIKit.ArtButton(rt, "Join", "JOIN", ArtIds.PanelGold, new Vector2(80f, 64f), 24, () => _ctx.App.JoinFriendRoom(f.Activity.Room));
                UIKit.Place((RectTransform)join.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-12f, 0f), new Vector2(80f, 64f));
            }
            else
            {
                var plus = FriendsArt.Plus(rt, 64f, () => _ctx.Friends.Invite(f));
                UIKit.Place((RectTransform)plus.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-14f, 0f), new Vector2(64f, 64f));
            }
        }
    }
}
