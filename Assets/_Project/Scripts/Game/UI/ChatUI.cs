using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace NeonBrawl
{
    /// <summary>Pieces shared by the room chat and the in-match emote panel.</summary>
    public static class ChatUI
    {
        public static Button EmoteButton(Transform parent, int emote, float size, Action onClick)
        {
            var bg = UIKit.Image(parent, "Emote" + emote, UIKit.Art.Circle, new Color(1f, 1f, 1f, 0.07f), false, true);
            bg.rectTransform.sizeDelta = new Vector2(size, size);
            var face = UIKit.Icon(bg.transform, "Face", EmoteArt.Get(emote), new Vector2(size * 0.9f, size * 0.9f));
            UIKit.Place(face.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size * 0.9f, size * 0.9f));
            var b = bg.gameObject.AddComponent<Button>();
            b.targetGraphic = bg;
            var colors = b.colors;
            colors.highlightedColor = new Color(1f, 1f, 1f, 2.5f);
            colors.pressedColor = new Color(1f, 1f, 1f, 4f);
            b.colors = colors;
            bg.gameObject.AddComponent<ButtonJuice>();
            b.onClick.AddListener(() => onClick());
            return b;
        }

        public static Button Chip(Transform parent, string text, Vector2 size, Action onClick)
        {
            int fontSize = Mathf.RoundToInt(Mathf.Min(28f, size.y * 0.44f));
            var b = UIKit.ArtButton(parent, "Chip", text, ArtIds.PanelBlue, size, fontSize, onClick);
            var label = b.transform.Find("Label").GetComponent<Text>();
            label.rectTransform.offsetMin = new Vector2(12f, 6f);
            label.rectTransform.offsetMax = new Vector2(-12f, -6f);
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 16;
            label.resizeTextMaxSize = fontSize;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            return b;
        }

        /// <summary>Name in the player's color, then the line (phrases in gold). Emote lines are the name only.</summary>
        public static string Rich(in ChatLine l)
        {
            Color c = Color.Lerp(l.Color, Color.white, 0.3f);
            string name = "<color=#" + ColorUtility.ToHtmlStringRGB(c) + ">" + l.Name + "</color>";
            switch (l.Kind)
            {
                case ChatKind.Emote: return name;
                case ChatKind.Phrase: return name + "  <color=#ffd34d>" + ChatHub.TextOf(l) + "</color>";
                default: return name + "  " + l.Text;
            }
        }

        public static InputField Field(Transform parent, string placeholder, int fontSize, Vector2 size)
        {
            var f = OnlineView.Field(parent, "ChatField", placeholder, fontSize, size);
            f.characterLimit = ChatHub.MaxLength;
            f.textComponent.alignment = TextAnchor.MiddleLeft;
            ((Text)f.placeholder).alignment = TextAnchor.MiddleLeft;
            f.lineType = InputField.LineType.SingleLine;
            f.onValidateInput = (s, i, c) => c == '<' || c == '>' || char.IsSurrogate(c) ? '\0' : c;
            return f;
        }

        /// <summary>True when an InputField's edit ended with Enter / the phone keyboard's Done (not by tapping away).</summary>
        public static bool Submitted(InputField f)
        {
            var kb = Keyboard.current;
            if (kb != null && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)) return true;
            var tsk = f.touchScreenKeyboard;
            return tsk != null && tsk.status == TouchScreenKeyboard.Status.Done;
        }

        /// <summary>One line of chat text with an optional emote face after the name.</summary>
        public sealed class LineSlot
        {
            public RectTransform Root;
            public Text Text;
            public Image Face;
            public CanvasGroup Group;

            public static LineSlot Make(Transform parent, string name, int fontSize, float height, bool backing)
            {
                var s = new LineSlot();
                if (backing)
                {
                    var bg = UIKit.Image(parent, name, UIKit.Art.RoundRect, new Color(0f, 0f, 0f, 0.5f), true);
                    s.Root = bg.rectTransform;
                }
                else s.Root = UIKit.Rect(parent, name);
                s.Group = s.Root.gameObject.AddComponent<CanvasGroup>();
                s.Group.blocksRaycasts = false;
                s.Text = UIKit.Label(s.Root, "Text", "", fontSize, Color.white, TextAnchor.MiddleLeft);
                UIKit.Place(s.Text.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(backing ? 14f : 0f, 0f), new Vector2(900f, height));
                s.Face = UIKit.Icon(s.Root, "Face", EmoteArt.Get(0), new Vector2(height * 1.2f, height * 1.2f));
                s.Face.rectTransform.anchorMin = s.Face.rectTransform.anchorMax = new Vector2(0f, 0.5f);
                s.Face.enabled = false;
                return s;
            }

            public void Set(in ChatLine l, bool backing)
            {
                Text.text = Rich(l);
                float w = Text.preferredWidth;
                Text.rectTransform.sizeDelta = new Vector2(w + 4f, Text.rectTransform.sizeDelta.y);
                bool face = l.Kind == ChatKind.Emote;
                Face.enabled = face;
                float left = backing ? 14f : 0f;
                float h = Face.rectTransform.sizeDelta.y;
                if (face)
                {
                    Face.sprite = EmoteArt.Get(l.Index);
                    Face.rectTransform.anchoredPosition = new Vector2(left + w + 10f + h * 0.5f, 0f);
                }
                if (backing) Root.sizeDelta = new Vector2(left * 2f + w + (face ? h + 10f : 0f), Root.sizeDelta.y);
            }
        }
    }

    /// <summary>
    /// Room page chat: a CHAT button with an unread count, a one-line peek at the newest message, and a
    /// drawer with the history, emote faces, quick phrases and a text box.
    /// </summary>
    public sealed class RoomChatPanel : MonoBehaviour
    {
        private const int Shown = 7;

        private GameContext _ctx;
        private GameObject _drawer, _badge, _peek, _inputRow, _off;
        private Text _badgeText;
        private ChatUI.LineSlot _peekLine;
        private readonly List<ChatUI.LineSlot> _lines = new List<ChatUI.LineSlot>(Shown);
        private InputField _field;
        private float _peekT;
        private int _typingFrame = -10;

        public static RoomChatPanel Build(RectTransform page, GameContext ctx)
        {
            var root = UIKit.Rect(page, "Chat");
            UIKit.Stretch(root);
            var p = root.gameObject.AddComponent<RoomChatPanel>();
            p._ctx = ctx;
            p.BuildButton(root);
            p.BuildDrawer(root);
            return p;
        }

        private void BuildButton(RectTransform root)
        {
            var btn = UIKit.ArtButton(root, "ChatButton", "CHAT", ArtIds.PanelBlue, new Vector2(250f, 120f), 42, Toggle, HudArt.Get(HudArt.Icon.Chat));
            UIKit.Place((RectTransform)btn.transform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(350f, 24f), new Vector2(250f, 120f));
            var badge = UIKit.Image(btn.transform, "Unread", UIKit.Art.Circle, UIKit.Red);
            UIKit.Place(badge.rectTransform, new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(-12f, -10f), new Vector2(54f, 54f));
            _badgeText = UIKit.Label(badge.transform, "N", "", 28, Color.white, TextAnchor.MiddleCenter);
            UIKit.Stretch(_badgeText.rectTransform);
            badge.gameObject.AddComponent<Pulse>().Amount = 0.08f;
            _badge = badge.gameObject;
            _badge.SetActive(false);

            _peekLine = ChatUI.LineSlot.Make(root, "Peek", 28, 50f, true);
            UIKit.Place(_peekLine.Root, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(30f, 156f), new Vector2(400f, 54f));
            _peek = _peekLine.Root.gameObject;
            _peek.SetActive(false);
        }

        private void BuildDrawer(RectTransform root)
        {
            var d = UIKit.Panel9(root, "Drawer", ArtIds.PanelDark, Color.white, 2.8f, true);
            UIKit.Place(d.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(430f, 0f), new Vector2(1020f, 600f));
            d.gameObject.AddComponent<PopIn>().Duration = 0.2f;
            _drawer = d.gameObject;
            var title = UIKit.Title(d.transform, "Title", "ROOM CHAT", 44, UIKit.Cream, UIKit.Gold, TextAnchor.MiddleLeft);
            UIKit.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(34f, -42f), new Vector2(500f, 60f));
            var close = UIKit.ArtButton(d.transform, "Close", "X", ArtIds.PanelOrange, new Vector2(84f, 72f), 38, Toggle);
            UIKit.Place((RectTransform)close.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-20f, -14f), new Vector2(84f, 72f));

            var listBg = UIKit.Image(d.transform, "List", UIKit.Art.RoundRect, new Color(0f, 0f, 0f, 0.35f), true);
            UIKit.Place(listBg.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -84f), new Vector2(960f, Shown * 36f + 16f));
            for (int i = 0; i < Shown; i++)
            {
                var s = ChatUI.LineSlot.Make(listBg.transform, "Line" + i, 26, 36f, false);
                UIKit.Place(s.Root, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(18f, -8f - i * 36f), new Vector2(920f, 36f));
                _lines.Add(s);
            }

            for (int i = 0; i < EmoteArt.Count; i++)
            {
                int e = i;
                var b = ChatUI.EmoteButton(d.transform, e, 92f, () => Send(ChatKind.Emote, e));
                UIKit.Place((RectTransform)b.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2((i - 3.5f) * 112f, -398f), new Vector2(92f, 92f));
            }
            var phrases = ChatHub.RoomPhrases;
            for (int i = 0; i < phrases.Length; i++)
            {
                int ph = phrases[i];
                var c = ChatUI.Chip(d.transform, ChatHub.Phrases[ph], new Vector2(152f, 62f), () => Send(ChatKind.Phrase, ph));
                UIKit.Place((RectTransform)c.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2((i - 2.5f) * 160f, -480f), new Vector2(152f, 62f));
            }

            var row = UIKit.Rect(d.transform, "InputRow");
            UIKit.Place(row, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 18f), new Vector2(960f, 76f));
            _inputRow = row.gameObject;
            _field = ChatUI.Field(row, "SAY SOMETHING…", 30, new Vector2(700f, 76f));
            UIKit.Place((RectTransform)_field.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(700f, 76f));
            _field.onEndEdit.AddListener(v => { if (ChatUI.Submitted(_field)) SendText(); });
            var send = UIKit.ArtButton(row, "Send", "SEND", ArtIds.PanelGold, new Vector2(240f, 76f), 36, SendText);
            UIKit.Place((RectTransform)send.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(240f, 76f));
            var off = UIKit.Label(d.transform, "Off", "Text chat is off in Settings. Emotes and phrases still work.", 26, UIKit.Sky, TextAnchor.MiddleCenter);
            UIKit.Place(off.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 56f), new Vector2(960f, 40f));
            _off = off.gameObject;
            _drawer.SetActive(false);
        }

        private void OnEnable()
        {
            if (_ctx == null) return;
            _ctx.Chat.Added += OnAdded;
            _drawer.SetActive(false);
            _peek.SetActive(false);
            RefreshBadge();
        }

        private void OnDisable()
        {
            if (_ctx == null) return;
            _ctx.Chat.Added -= OnAdded;
            _ctx.Input.Typing = false;
        }

        private void Toggle()
        {
            bool open = !_drawer.activeSelf;
            _drawer.SetActive(open);
            if (open)
            {
                _ctx.Chat.Unread = 0;
                _peek.SetActive(false);
                bool text = _ctx.Prefs.TextChat;
                _inputRow.SetActive(text);
                _off.SetActive(!text);
                RefreshLines();
            }
            RefreshBadge();
        }

        private void Send(ChatKind kind, int index)
        {
            if (_ctx.Chat.Say(kind, index)) Haptics();
        }

        private void SendText()
        {
            string t = _field.text;
            if (string.IsNullOrWhiteSpace(t)) return;
            if (_ctx.Chat.Say(ChatKind.Text, 0, t))
            {
                _field.text = "";
                Haptics();
            }
            if (!Application.isMobilePlatform) _field.ActivateInputField();
        }

        private void Haptics() => _ctx.Haptics?.Pulse(8);

        private void OnAdded(ChatLine l)
        {
            if (_drawer.activeSelf)
            {
                _ctx.Chat.Unread = 0;
                RefreshLines();
            }
            else if (!l.Mine)
            {
                _peekLine.Set(l, true);
                _peek.SetActive(true);
                _peekT = 5f;
            }
            RefreshBadge();
        }

        private void RefreshLines()
        {
            var all = _ctx.Chat.Lines;
            int start = Mathf.Max(0, all.Count - Shown);
            for (int i = 0; i < Shown; i++)
            {
                int k = start + i;
                var s = _lines[i];
                bool used = k < all.Count;
                s.Root.gameObject.SetActive(used);
                if (used) s.Set(all[k], false);
            }
        }

        private void RefreshBadge()
        {
            int n = _ctx.Chat.Unread;
            bool show = n > 0 && !_drawer.activeSelf;
            _badge.SetActive(show);
            if (show) _badgeText.text = n > 9 ? "9+" : n.ToString();
        }

        private void Update()
        {
            if (_field.isFocused) _typingFrame = Time.frameCount;
            _ctx.Input.Typing = Time.frameCount - _typingFrame <= 2;
            if (_peekT > 0f)
            {
                _peekT -= Time.unscaledDeltaTime;
                _peekLine.Group.alpha = Mathf.Clamp01(_peekT / 0.4f);
                if (_peekT <= 0f) _peek.SetActive(false);
            }
        }
    }

    /// <summary>
    /// In a match: a chat button next to pause/scoreboard that opens emote faces, quick phrases and a text box;
    /// recent lines fade in under the health bar. PC: T or Enter to type, 1–8 for emotes.
    /// </summary>
    public sealed class MatchChatPanel : MonoBehaviour
    {
        private const int FeedLines = 4;
        private const float FeedLife = 6f;

        private GameContext _ctx;
        private GameObject _panel, _inputRow;
        private InputField _field;
        private readonly ChatUI.LineSlot[] _feed = new ChatUI.LineSlot[FeedLines];
        private readonly float[] _feedT = new float[FeedLines];
        private readonly ChatLine[] _feedData = new ChatLine[FeedLines];
        private float _idle;
        private int _typingFrame = -10;

        public static MatchChatPanel Build(RectTransform safe, GameContext ctx)
        {
            var root = UIKit.Rect(safe, "Chat");
            UIKit.Stretch(root);
            var p = root.gameObject.AddComponent<MatchChatPanel>();
            p._ctx = ctx;
            p.BuildButton(safe);
            p.BuildPanel(root);
            p.BuildFeed(root);
            return p;
        }

        private void BuildButton(RectTransform safe)
        {
            var b = UIKit.Panel9(safe, "ChatBtn", ArtIds.PanelDark, new Color(1f, 1f, 1f, 0.9f), 2.4f, true);
            UIKit.Place(b.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-244f, -16f), new Vector2(100f, 92f));
            var ic = UIKit.Icon(b.transform, "Icon", HudArt.Get(HudArt.Icon.Chat), new Vector2(58f, 58f));
            UIKit.Place(ic.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(62f, 62f));
            var btn = b.gameObject.AddComponent<Button>();
            btn.targetGraphic = b;
            b.gameObject.AddComponent<ButtonJuice>();
            btn.onClick.AddListener(() => Open(!_panel.activeSelf, false));
        }

        private void BuildPanel(RectTransform root)
        {
            var p = UIKit.Panel9(root, "Panel", ArtIds.PanelDark, new Color(1f, 1f, 1f, 0.95f), 2.6f, true);
            UIKit.Place(p.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-20f, -118f), new Vector2(600f, 600f));
            p.gameObject.AddComponent<PopIn>().Duration = 0.18f;
            // its own layer above the touch buttons (they are built later and would cover it), taking taps first
            var layer = p.gameObject.AddComponent<Canvas>();
            layer.overrideSorting = true;
            layer.sortingOrder = 20;
            p.gameObject.AddComponent<GraphicRaycaster>();
            // solid fill so the buttons underneath never show through the faces
            var backing = UIKit.Image(p.transform, "Backing", _ctx.Art.RoundRect, new Color(0.08f, 0.09f, 0.13f, 1f), true);
            UIKit.Stretch(backing.rectTransform, 10f);
            backing.raycastTarget = true;
            backing.transform.SetAsFirstSibling();
            _panel = p.gameObject;
            for (int i = 0; i < EmoteArt.Count; i++)
            {
                int e = i;
                var b = ChatUI.EmoteButton(p.transform, e, 112f, () => Send(ChatKind.Emote, e));
                UIKit.Place((RectTransform)b.transform, new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), new Vector2(102f + (i % 4) * 132f, -78f - (i / 4) * 128f), new Vector2(112f, 112f));
                var key = UIKit.Label(b.transform, "Key", (i + 1).ToString(), 20, new Color(1f, 1f, 1f, 0.55f), TextAnchor.LowerRight);
                UIKit.Place(key.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-2f, 0f), new Vector2(30f, 26f));
                key.gameObject.SetActive(!Application.isMobilePlatform);
            }
            var phrases = ChatHub.MatchPhrases;
            for (int i = 0; i < phrases.Length; i++)
            {
                int ph = phrases[i];
                var c = ChatUI.Chip(p.transform, ChatHub.Phrases[ph], new Vector2(270f, 62f), () => Send(ChatKind.Phrase, ph));
                UIKit.Place((RectTransform)c.transform, new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), new Vector2(160f + (i % 2) * 280f, -316f - (i / 2) * 72f), new Vector2(270f, 62f));
            }
            var row = UIKit.Rect(p.transform, "InputRow");
            UIKit.Place(row, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 22f), new Vector2(550f, 66f));
            _inputRow = row.gameObject;
            _field = ChatUI.Field(row, "TYPE…", 28, new Vector2(390f, 66f));
            UIKit.Place((RectTransform)_field.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(390f, 66f));
            _field.onEndEdit.AddListener(v => { if (ChatUI.Submitted(_field)) SendText(); });
            var send = UIKit.ArtButton(row, "Send", "SEND", ArtIds.PanelGold, new Vector2(150f, 66f), 30, SendText);
            UIKit.Place((RectTransform)send.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(150f, 66f));
            _panel.SetActive(false);
        }

        private void BuildFeed(RectTransform root)
        {
            for (int i = 0; i < FeedLines; i++)
            {
                var s = ChatUI.LineSlot.Make(root, "ChatLine" + i, 26, 40f, true);
                UIKit.Place(s.Root, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -186f - i * 48f), new Vector2(300f, 42f));
                s.Group.alpha = 0f;
                _feed[i] = s;
            }
        }

        public void Bind()
        {
            for (int i = 0; i < FeedLines; i++) { _feedT[i] = 0f; _feed[i].Group.alpha = 0f; }
            Open(false, false);
        }

        private void OnEnable()
        {
            if (_ctx != null) _ctx.Chat.Added += OnAdded;
        }

        private void OnDisable()
        {
            if (_ctx == null) return;
            _ctx.Chat.Added -= OnAdded;
            _ctx.Input.Typing = false;
            if (_panel != null) _panel.SetActive(false);
        }

        private bool TextAllowed => _ctx.Match != null && _ctx.Match.Online && _ctx.Prefs.TextChat;

        private void Open(bool open, bool typing)
        {
            if (open && typing && !TextAllowed) typing = false;
            _panel.SetActive(open);
            _idle = 0f;
            if (!open)
            {
                if (_field.isFocused) _field.DeactivateInputField();
                return;
            }
            _inputRow.SetActive(TextAllowed);
            var prt = (RectTransform)_panel.transform;
            prt.sizeDelta = new Vector2(600f, TextAllowed ? 600f : 520f);
            if (typing)
            {
                _field.Select();
                _field.ActivateInputField();
            }
        }

        private void Send(ChatKind kind, int index)
        {
            if (_ctx.Chat.Say(kind, index)) _ctx.Haptics?.Pulse(8);
            Open(false, false);
        }

        private void SendText()
        {
            string t = _field.text;
            _field.text = "";
            if (!string.IsNullOrWhiteSpace(t)) _ctx.Chat.Say(ChatKind.Text, 0, t);
            Open(false, false);
        }

        private void OnAdded(ChatLine l)
        {
            for (int i = FeedLines - 1; i > 0; i--)
            {
                _feedT[i] = _feedT[i - 1];
                _feedData[i] = _feedData[i - 1];
                if (_feedT[i] > 0f) _feed[i].Set(_feedData[i], true);
            }
            _feedData[0] = l;
            _feedT[0] = FeedLife;
            _feed[0].Set(l, true);
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            for (int i = 0; i < FeedLines; i++)
            {
                if (_feedT[i] <= 0f) { if (_feed[i].Group.alpha != 0f) _feed[i].Group.alpha = 0f; continue; }
                _feedT[i] -= dt;
                _feed[i].Group.alpha = Mathf.Clamp01(_feedT[i] / 0.5f) * Mathf.Clamp01((FeedLife - _feedT[i]) / 0.15f);
            }

            bool focused = _field.isFocused;
            if (focused) _typingFrame = Time.frameCount;
            _ctx.Input.Typing = Time.frameCount - _typingFrame <= 2;

            if (_panel.activeSelf && _ctx.UI != null && _ctx.UI.PauseVisible) Open(false, false);

            if (_panel.activeSelf && !focused)
            {
                _idle += dt;
                if (_idle > 7f) Open(false, false);
            }

            // keyboard: T / Enter to type, 1-8 emotes
            var kb = Keyboard.current;
            if (kb == null || _ctx.Input.Typing || _ctx.UI.PauseVisible) return;
            if (kb.tKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame) Open(true, true);
            for (int i = 0; i < EmoteArt.Count; i++)
                if (kb[Key.Digit1 + i].wasPressedThisFrame) { Send(ChatKind.Emote, i); break; }
        }
    }
}
