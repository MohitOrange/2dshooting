using UnityEngine;
using UnityEngine.UI;

namespace NeonBrawl
{
    /// <summary>
    /// Voice controls in online matches, next to chat/scores/pause: MIC on/off, SPEAKER on/off and who you
    /// talk to (ALL / TEAM in team battles). Players who are talking show under the chat feed.
    /// </summary>
    public sealed class VoiceHud : MonoBehaviour
    {
        private const int TalkLines = 3;

        private GameContext _ctx;
        private GameObject _controls;
        private Image _micSlash, _spkSlash, _micGlow;
        private Text _channel;
        private GameObject _blocked;
        private Button _channelBtn;
        private readonly GameObject[] _talk = new GameObject[TalkLines];
        private readonly Text[] _talkText = new Text[TalkLines];
        private bool _shown;
        private float _level;

        public static VoiceHud Build(RectTransform safe, GameContext ctx)
        {
            var root = UIKit.Rect(safe, "Voice");
            UIKit.Stretch(root);
            var v = root.gameObject.AddComponent<VoiceHud>();
            v._ctx = ctx;
            v._controls = UIKit.Rect(root, "Controls").gameObject;
            UIKit.Stretch((RectTransform)v._controls.transform);
            var ct = v._controls.transform;
            v._micSlash = RoundButton(ct, "Mic", HudArt.Get(HudArt.Icon.Mic), -356f, () => ctx.Voice.SetMic(!ctx.Voice.Transmitting), out v._micGlow);
            v._blocked = UIKit.Badge(v._micSlash.transform.parent, "!", UIKit.Red, new Vector2(-10f, -10f), Vector2.one, 30).transform.parent.gameObject;
            v._blocked.SetActive(false);
            v._spkSlash = RoundButton(ct, "Speaker", HudArt.Get(HudArt.Icon.Speaker), -468f, () => ctx.Voice.SetSpeaker(!ctx.Voice.SpeakerOn), out _);
            v._channelBtn = UIKit.ArtButton(ct, "Channel", "ALL", ArtIds.PanelBlue, new Vector2(150f, 92f), 34, () =>
                ctx.Voice.SetTalkTo(ctx.Voice.TalkTo == VoiceService.Target.All ? VoiceService.Target.Team : VoiceService.Target.All));
            UIKit.Place((RectTransform)v._channelBtn.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-580f, -16f), new Vector2(150f, 92f));
            v._channel = v._channelBtn.transform.Find("Label").GetComponent<Text>();

            for (int i = 0; i < TalkLines; i++)
            {
                var bg = UIKit.Image(root, "Talking" + i, UIKit.Art.RoundRect, new Color(0f, 0f, 0f, 0.5f), true);
                UIKit.Place(bg.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -420f - i * 46f), new Vector2(260f, 40f));
                var ic = UIKit.Icon(bg.transform, "Mic", HudArt.Get(HudArt.Icon.Mic), new Vector2(34f, 34f), new Color(0.45f, 1f, 0.5f));
                UIKit.Place(ic.rectTransform, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(24f, 0f), new Vector2(34f, 34f));
                var t = UIKit.Label(bg.transform, "Name", "", 24, new Color(0.75f, 1f, 0.75f), TextAnchor.MiddleLeft);
                UIKit.Place(t.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(48f, 0f), new Vector2(200f, 36f));
                v._talk[i] = bg.gameObject;
                v._talkText[i] = t;
                bg.gameObject.SetActive(false);
            }
            v._controls.SetActive(false);
            return v;
        }

        /// <summary>Round dark button with a white icon; returns the red "off" slash drawn over it.</summary>
        private static Image RoundButton(Transform parent, string name, Sprite icon, float x, System.Action onClick, out Image glow)
        {
            var b = UIKit.Panel9(parent, name + "Btn", ArtIds.PanelDark, new Color(1f, 1f, 1f, 0.9f), 2.4f, true);
            UIKit.Place(b.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(x, -16f), new Vector2(100f, 92f));
            glow = UIKit.Image(b.transform, "Glow", UIKit.Art.Glow, new Color(0.3f, 1f, 0.4f, 0f));
            UIKit.Place(glow.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(120f, 120f));
            var ic = UIKit.Icon(b.transform, "Icon", icon, new Vector2(58f, 58f));
            UIKit.Place(ic.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(58f, 58f));
            var slashLine = UIKit.Image(b.transform, "SlashLine", UIKit.Art.Square, new Color(0.04f, 0.05f, 0.08f));
            UIKit.Place(slashLine.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(16f, 78f));
            slashLine.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            var slash = UIKit.Image(slashLine.transform, "Slash", UIKit.Art.Square, new Color(1f, 0.28f, 0.25f));
            UIKit.Stretch(slash.rectTransform, 3f);
            var btn = b.gameObject.AddComponent<Button>();
            btn.targetGraphic = b;
            b.gameObject.AddComponent<ButtonJuice>();
            btn.onClick.AddListener(() => onClick());
            return slashLine;
        }

        public void Bind(MatchController match)
        {
            _shown = match != null && match.Online && _ctx.Prefs.VoiceChat;
            _controls.SetActive(_shown);
            for (int i = 0; i < TalkLines; i++) _talk[i].SetActive(false);
            Refresh();
        }

        private void OnEnable()
        {
            if (_ctx != null && _ctx.Voice != null) _ctx.Voice.Changed += Refresh;
        }

        private void OnDisable()
        {
            if (_ctx != null && _ctx.Voice != null) _ctx.Voice.Changed -= Refresh;
        }

        private void Refresh()
        {
            if (this == null || _ctx == null || _ctx.Voice == null) return;
            var v = _ctx.Voice;
            bool shown = _shown && _ctx.Prefs.VoiceChat;
            if (_controls.activeSelf != shown) _controls.SetActive(shown);
            _micSlash.gameObject.SetActive(!v.Transmitting);
            if (_blocked.activeSelf != v.MicBlocked) _blocked.SetActive(v.MicBlocked);
            _spkSlash.gameObject.SetActive(!v.SpeakerOn);
            _channel.text = v.TalkTo == VoiceService.Target.Team ? "TEAM" : "ALL";
            _channelBtn.interactable = v.HasTeamChannel || v.TalkTo == VoiceService.Target.Team;
            for (int i = 0; i < TalkLines; i++)
            {
                bool on = shown && i < v.Talking.Count;
                if (_talk[i].activeSelf != on) _talk[i].SetActive(on);
                if (on) _talkText[i].text = v.Talking[i];
            }
        }

        private void Update()
        {
            if (!_shown || _ctx.Voice == null) return;
            // green glow while your mic is live; it swells with your voice so you can see the mic hears you
            var voice = _ctx.Voice;
            float level = voice.SelfLevel;
            _level = Mathf.Max(level, _level - Time.unscaledDeltaTime * 2.5f);
            float a = voice.Transmitting ? 0.3f + Mathf.Sin(Time.unscaledTime * 6f) * 0.08f + _level * 0.6f : 0f;
            var c = _micGlow.color;
            if (!Mathf.Approximately(c.a, a)) { c.a = a; _micGlow.color = c; }
            float k = 1f + _level * 0.45f;
            _micGlow.rectTransform.localScale = new Vector3(k, k, 1f);
        }
    }
}
