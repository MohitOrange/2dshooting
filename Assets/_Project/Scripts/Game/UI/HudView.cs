using System.Collections.Generic;
using NeonBrawl.Core;
using UnityEngine;
using UnityEngine.UI;

namespace NeonBrawl
{
    /// <summary>
    /// In-match HUD: health/armor bars with a delayed "ghost" bar and green→red color, timer panel and
    /// score, kill feed with gun icons, weapon panel (painted gun, ammo, reload, grenades), big center
    /// banners with a punch, toasts, damage vignette, low-health pulse, death/respawn panel,
    /// scoreboard, pickup prompt and FPS. Text only changes when values change (no per-frame garbage).
    /// </summary>
    public sealed class HudView : MonoBehaviour
    {
        private const int FeedLines = 5;

        private sealed class FeedLine
        {
            public CanvasGroup Group;
            public Text Killer, Victim;
            public Image Gun;
            public float Time;
        }

        private GameContext _ctx;
        private MatchController _match;
        private Image _hpFill, _hpGhost, _armorFill, _gunIcon, _nadeIcon;
        private Text _hpText, _timer, _score, _weaponName, _ammo, _reserve, _grenades, _center, _toast, _death, _deathSub, _prompt, _fps;
        private Image _reloadFill, _vignette, _lowHp, _timerBg, _annBand, _annRays, _annRing;
        private RectTransform _centerRt, _timerRt, _ammoRt;
        private Vector2 _centerHome;
        private bool _annBig, _annDigit, _lowAmmo;
        private Color _annColor = Color.white;
        private float _timerPunch;
        private HudFeedback _feedback;
        private GameObject _deathPanel, _scoreboard, _ammoPanel;
        private FeedLine[] _feed;
        private ScoreTable _board;
        private TouchControls _touch;
        private int _autoShown = -1;
        private MatchChatPanel _chat;
        private VoiceHud _voice;
        private readonly List<PlayerRecord> _standings = new List<PlayerRecord>(12);

        private float _shownHp = 1f, _ghostHp = 1f, _ghostDelay;
        private float _centerT, _toastT, _vignetteA, _scorePunch;
        private int _lastHp = -1, _lastSeconds = -1, _lastAmmoVersion = -1, _lastGrenades = -1, _lastRespawn = -1, _lastScoreHash = -1;
        private float _fpsT;
        private int _fpsFrames;

        public void Build(GameContext ctx, RectTransform safe)
        {
            _ctx = ctx;
            var art = ctx.Art;
            var cat = ctx.Catalog;
            var root = (RectTransform)transform;

            _vignette = UIKit.Image(root, "DamageVignette", art.Vignette, new Color(0.9f, 0.05f, 0.05f, 0f));
            UIKit.Stretch(_vignette.rectTransform, -60f);
            _lowHp = UIKit.Image(root, "LowHealth", art.Vignette, new Color(0.7f, 0f, 0f, 0f));
            UIKit.Stretch(_lowHp.rectTransform, -120f);
            _vignette.transform.SetAsFirstSibling();
            _lowHp.transform.SetAsFirstSibling();
            _feedback = HudFeedback.Build(root, ctx);
            _feedback.transform.SetSiblingIndex(2); // above the vignettes, under every panel

            // health + armor (top-left)
            var hp = UIKit.Panel9(safe, "Health", ArtIds.PanelDark, new Color(1f, 1f, 1f, 0.92f), 2.4f);
            UIKit.Place(hp.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, -16f), new Vector2(520f, 120f));
            var heart = UIKit.Icon(hp.transform, "Heart", cat != null ? ArtCatalog.At(cat.PickupIcons, ArtIds.PickMedkit) : art.Cross, new Vector2(64f, 64f),
                cat != null ? (Color?)null : new Color(1f, 0.35f, 0.35f));
            UIKit.Place(heart.rectTransform, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(50f, 14f), new Vector2(64f, 64f));
            heart.gameObject.AddComponent<Pulse>().Amount = 0.06f;
            var barBg = UIKit.Image(hp.transform, "BarBg", art.RoundRect, new Color(0f, 0f, 0f, 0.65f), true);
            UIKit.Place(barBg.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(92f, 16f), new Vector2(320f, 40f));
            _hpGhost = Fill(barBg.transform, art, new Color(1f, 0.95f, 0.75f));
            _hpFill = Fill(barBg.transform, art, new Color(0.35f, 0.92f, 0.35f));
            var hg = _hpFill.gameObject.AddComponent<UIGradient>();
            hg.Top = Color.white;
            hg.Bottom = new Color(0.62f, 0.62f, 0.62f);
            var shield = UIKit.Icon(hp.transform, "Shield", cat != null ? ArtCatalog.At(cat.PickupIcons, ArtIds.PickShield) : art.Circle, new Vector2(40f, 40f));
            UIKit.Place(shield.rectTransform, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(50f, -34f), new Vector2(40f, 40f));
            var armorBg = UIKit.Image(hp.transform, "ArmorBg", art.RoundRect, new Color(0f, 0f, 0f, 0.65f), true);
            UIKit.Place(armorBg.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(92f, -32f), new Vector2(320f, 20f));
            _armorFill = Fill(armorBg.transform, art, new Color(0.35f, 0.65f, 1f));
            _hpText = UIKit.Title(hp.transform, "Value", "100", 50, Color.white, new Color(0.85f, 0.9f, 1f), TextAnchor.MiddleRight);
            UIKit.Place(_hpText.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-14f, 6f), new Vector2(100f, 60f));

            // timer + score (top-center)
            var timerBg = UIKit.Panel9(safe, "TimerBg", ArtIds.PanelDark, new Color(1f, 1f, 1f, 0.92f), 2.4f);
            UIKit.Place(timerBg.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -12f), new Vector2(250f, 88f));
            _timerBg = timerBg;
            _timer = UIKit.Title(timerBg.transform, "Timer", "5:00", 58, Color.white, new Color(0.8f, 0.88f, 1f));
            UIKit.Stretch(_timer.rectTransform);
            _timerRt = _timer.rectTransform;
            _score = UIKit.Label(safe, "Score", "", 28, UIKit.Gold, TextAnchor.UpperCenter);
            UIKit.Place(_score.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -106f), new Vector2(800f, 40f));

            // kill feed (top-right, under the buttons)
            _feed = new FeedLine[FeedLines];
            for (int i = 0; i < FeedLines; i++) _feed[i] = MakeFeedLine(safe, art, i);

            // weapon panel (bottom-center)
            var ammo = UIKit.Panel9(safe, "Ammo", ArtIds.PanelDark, new Color(1f, 1f, 1f, 0.92f), 2.4f);
            _ammoPanel = ammo.gameObject;
            UIKit.Place(ammo.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 14f), new Vector2(560f, 130f));
            _gunIcon = UIKit.Icon(ammo.transform, "Gun", null, new Vector2(210f, 80f));
            UIKit.Place(_gunIcon.rectTransform, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(128f, 8f), new Vector2(210f, 84f));
            _weaponName = UIKit.Label(ammo.transform, "Weapon", "PISTOL", 22, UIKit.Gold, TextAnchor.LowerCenter);
            UIKit.Place(_weaponName.rectTransform, new Vector2(0f, 0f), new Vector2(0.5f, 0f), new Vector2(128f, 14f), new Vector2(240f, 28f));
            _ammo = UIKit.Title(ammo.transform, "Count", "12", 66, Color.white, new Color(0.85f, 0.9f, 1f), TextAnchor.MiddleRight);
            UIKit.Place(_ammo.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(350f, 8f), new Vector2(110f, 80f));
            _ammoRt = _ammo.rectTransform;
            _reserve = UIKit.Label(ammo.transform, "Reserve", "/ ∞", 32, new Color(0.75f, 0.8f, 0.92f), TextAnchor.MiddleLeft);
            UIKit.Place(_reserve.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(354f, 0f), new Vector2(110f, 50f));
            _nadeIcon = UIKit.Icon(ammo.transform, "Nade", cat != null ? cat.PickupGrenade : art.Circle, new Vector2(56f, 56f));
            UIKit.Place(_nadeIcon.rectTransform, new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-60f, 12f), new Vector2(58f, 58f));
            _grenades = UIKit.Label(ammo.transform, "Nades", "x1", 26, new Color(0.75f, 1f, 0.55f), TextAnchor.MiddleCenter);
            UIKit.Place(_grenades.rectTransform, new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(-60f, 10f), new Vector2(70f, 32f));
            var rbg = UIKit.Image(ammo.transform, "ReloadBg", art.Square, new Color(0f, 0f, 0f, 0.5f));
            UIKit.Place(rbg.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 6f), new Vector2(500f, 8f));
            _reloadFill = Fill(rbg.transform, art, UIKit.Gold);

            // center banner (dark band, spinning rays, ring pulse for the countdown) + toast
            var annPos = new Vector2(0f, 190f);
            _annBand = UIKit.Image(safe, "AnnBand", art.Glow, new Color(0f, 0f, 0f, 0f));
            UIKit.Place(_annBand.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), annPos, new Vector2(1600f, 260f));
            _annRays = UIKit.Image(safe, "AnnRays", cat != null ? ArtCatalog.At(cat.Fx1, ArtIds.StarOrange) : art.Star, new Color(1f, 1f, 1f, 0f));
            UIKit.Place(_annRays.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), annPos, new Vector2(620f, 620f));
            _annRays.gameObject.AddComponent<Spin>().Speed = 40f;
            _annRing = UIKit.Image(safe, "AnnRing", art.Ring, new Color(1f, 1f, 1f, 0f));
            UIKit.Place(_annRing.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), annPos, new Vector2(300f, 300f));
            _center = UIKit.Title(safe, "Center", "", 110, Color.white, new Color(0.85f, 0.85f, 0.85f));
            _centerRt = _center.rectTransform;
            UIKit.Place(_centerRt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), annPos, new Vector2(1400f, 160f));
            _centerHome = annPos;
            _toast = UIKit.Label(safe, "Toast", "", 34, new Color(0.85f, 1f, 0.75f), TextAnchor.MiddleCenter);
            // above the action (the player stands at screen centre), under the score line
            UIKit.Place(_toast.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 320f), new Vector2(1000f, 50f));

            // death panel
            var dp = UIKit.Panel9(safe, "Death", ArtIds.PanelOrange, new Color(1f, 0.75f, 0.75f, 0.95f), 3f);
            _deathPanel = dp.gameObject;
            UIKit.Place(dp.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -160f), new Vector2(700f, 150f));
            _death = UIKit.Title(dp.transform, "Who", "", 46, Color.white, UIKit.Cream);
            UIKit.Place(_death.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -16f), new Vector2(680f, 60f));
            _deathSub = UIKit.Label(dp.transform, "Respawn", "", 32, Color.white, TextAnchor.LowerCenter);
            UIKit.Place(_deathSub.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 20f), new Vector2(680f, 44f));
            _deathPanel.AddComponent<PopIn>().Delay = 0.35f;
            _deathPanel.SetActive(false);

            // keyboard pickup prompt
            _prompt = UIKit.Label(safe, "Prompt", "", 32, UIKit.Gold, TextAnchor.MiddleCenter);
            UIKit.Place(_prompt.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 160f), new Vector2(900f, 44f));

            _fps = UIKit.Label(safe, "Fps", "", 22, new Color(0.7f, 1f, 0.7f), TextAnchor.UpperLeft);
            UIKit.Place(_fps.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(26f, -142f), new Vector2(200f, 30f));

            // scoreboard
            var board = UIKit.Panel9(safe, "Scoreboard", ArtIds.PanelDark, Color.white, 2.8f);
            _scoreboard = board.gameObject;
            UIKit.Place(board.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 10f), new Vector2(940f, 640f));
            var title = UIKit.Title(board.transform, "Title", "SCOREBOARD", 46, UIKit.Cream, UIKit.Gold);
            UIKit.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -14f), new Vector2(760f, 60f));
            var rows = UIKit.Rect(board.transform, "Rows");
            UIKit.Place(rows, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -76f), new Vector2(940f, 560f));
            _board = new ScoreTable(rows, 13, 40f, 28, cat);
            _scoreboard.SetActive(false);

            _touch = TouchControls.Build(root, ctx.Input, art, cat);
            _touch.ApplyLayout(ctx.Prefs.Layout);
            TouchControls.TopButtons(safe, ctx.Input, art, cat);
            _chat = MatchChatPanel.Build(safe, ctx);
            _voice = VoiceHud.Build(safe, ctx);

            ctx.Events.Killed += OnKilled;
            ctx.Events.Announced += OnAnnounce;
            ctx.Events.LocalDamaged += OnLocalDamaged;
            ctx.Events.Toast += OnToast;
        }

        private static Image Fill(Transform parent, ArtLibrary art, Color c)
        {
            var img = UIKit.Image(parent, "Fill", art.Square, c);
            img.type = Image.Type.Filled;
            img.fillMethod = Image.FillMethod.Horizontal;
            img.fillAmount = 1f;
            UIKit.Stretch(img.rectTransform, 4f);
            return img;
        }

        private static FeedLine MakeFeedLine(RectTransform safe, ArtLibrary art, int i)
        {
            var bg = UIKit.Image(safe, "Feed" + i, art.RoundRect, new Color(0f, 0f, 0f, 0.5f), true);
            var rt = bg.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-24f, -124f - i * 50f);
            var h = bg.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.childAlignment = TextAnchor.MiddleRight;
            h.spacing = 12f;
            h.padding = new RectOffset(16, 16, 4, 4);
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = false;
            var fit = bg.gameObject.AddComponent<ContentSizeFitter>();
            fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var line = new FeedLine { Group = bg.gameObject.AddComponent<CanvasGroup>() };
            line.Killer = UIKit.Label(bg.transform, "Killer", "", 26, Color.white, TextAnchor.MiddleRight);
            line.Gun = UIKit.Icon(bg.transform, "Gun", null, new Vector2(80f, 32f));
            var le = line.Gun.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = 84f;
            le.preferredHeight = 34f;
            line.Victim = UIKit.Label(bg.transform, "Victim", "", 26, Color.white, TextAnchor.MiddleLeft);
            line.Group.alpha = 0f;
            return line;
        }

        private void OnDestroy()
        {
            if (_ctx == null) return;
            _ctx.Events.Killed -= OnKilled;
            _ctx.Events.Announced -= OnAnnounce;
            _ctx.Events.LocalDamaged -= OnLocalDamaged;
            _ctx.Events.Toast -= OnToast;
        }

        public void Bind(MatchController match)
        {
            _match = match;
            for (int i = 0; i < FeedLines; i++) { _feed[i].Time = 0f; _feed[i].Group.alpha = 0f; }
            _center.text = "";
            _toast.text = "";
            _lastHp = _lastSeconds = _lastAmmoVersion = _lastGrenades = _lastRespawn = _lastScoreHash = -1;
            _shownHp = _ghostHp = 1f;
            _vignetteA = 0f;
            _centerT = 0f;
            _timerPunch = 0f;
            _lowAmmo = false;
            _annBand.color = _annRays.color = _annRing.color = new Color(0f, 0f, 0f, 0f);
            _feedback.Bind(match);
            _chat.Bind();
            _voice.Bind(match);
        }

        public void SetTouch(bool touch) => _touch.gameObject.SetActive(touch);

        /// <summary>Puts the sticks and buttons where the player's saved layout says.</summary>
        public void ApplyLayout() => _touch.ApplyLayout(_ctx.Prefs.Layout);

        private void ToggleAutoShoot()
        {
            var p = _ctx.Prefs;
            p.AutoShoot = !p.AutoShoot;
            p.Save();
            OnToast(p.AutoShoot ? "AUTO SHOOT ON" : "AUTO SHOOT OFF");
            _ctx.Sfx.PlayUi(SfxId.UiTap, 0.6f);
        }

        // ------------------------------------------------------------- events

        private void OnKilled(KillInfo k)
        {
            if (_match == null || _match.Spectator) return;
            // shift lines down (copy content; the objects stay in place)
            for (int i = FeedLines - 1; i > 0; i--)
            {
                var a = _feed[i];
                var b = _feed[i - 1];
                a.Killer.text = b.Killer.text;
                a.Victim.text = b.Victim.text;
                a.Gun.sprite = b.Gun.sprite;
                a.Gun.color = b.Gun.color;
                a.Gun.enabled = b.Gun.enabled;
                a.Time = b.Time;
            }
            var top = _feed[0];
            bool self = k.Killer == null || k.Killer == k.Victim;
            top.Killer.text = self ? "" : Colored(k.Killer);
            top.Victim.text = Colored(k.Victim);
            var gun = WeaponArt.Get(_ctx.Catalog, k.Weapon);
            if (gun.Valid)
            {
                top.Gun.sprite = gun.Sprite;
                top.Gun.color = gun.Tint;
                top.Gun.enabled = true;
            }
            else
            {
                Sprite icon = k.Weapon == WeaponId.Grenade && _ctx.Catalog != null ? _ctx.Catalog.PickupGrenade
                    : k.Weapon == WeaponId.Barrel && _ctx.Catalog != null ? ArtCatalog.At(_ctx.Catalog.Shared, ArtIds.SharedBarrelRed) : null;
                top.Gun.sprite = icon;
                top.Gun.color = Color.white;
                top.Gun.enabled = icon != null;
                if (icon == null) top.Killer.text += (top.Killer.text.Length > 0 ? "  " : "") + "<color=#ffcc55>[" + _ctx.Config.Get(k.Weapon).DisplayName + "]</color>";
            }
            top.Time = 4.5f;
            if (k.Killer != null && k.Killer.IsLocal && !self) _scorePunch = 0.35f;
            if (k.Victim.IsLocal)
                _death.text = !self ? "KILLED BY " + k.Killer.DisplayName.ToUpperInvariant() : "YOU DIED";
        }

        private string Colored(Fighter f)
        {
            Color c = f.IsLocal ? new Color(1f, 0.92f, 0.35f) : f.OutfitColor;
            return "<color=#" + ColorUtility.ToHtmlStringRGB(c) + ">" + f.DisplayName + "</color>";
        }

        private void OnAnnounce(string text, Color color, bool big)
        {
            if (_match != null && _match.Spectator) return;
            _center.text = text;
            _center.color = color;
            _center.fontSize = big ? 120 : 64;
            _centerT = 1.2f;
            _annBig = big;
            _annDigit = text.Length == 1 && char.IsDigit(text[0]);
            _annColor = color;
            _centerRt.localScale = Vector3.one;
        }

        private void OnToast(string text)
        {
            _toast.text = text;
            _toastT = 1.6f;
        }

        private void OnLocalDamaged(float amount, Vector2 from)
        {
            _vignetteA = Mathf.Min(0.75f, _vignetteA + 0.25f + amount * 0.008f);
        }

        // ------------------------------------------------------------- update

        private void Update()
        {
            if (_match == null) return;
            float udt = Time.unscaledDeltaTime;
            var local = _match.Local;
            var input = _ctx.Input;

            if (input.AutoShootPressed) ToggleAutoShoot();
            int auto = _ctx.Prefs.AutoShoot ? 1 : 0;
            if (auto != _autoShown)
            {
                _autoShown = auto;
                _touch.SetAutoShoot(auto == 1);
            }

            int secs = Mathf.CeilToInt(_match.TimeLeft);
            if (secs != _lastSeconds)
            {
                bool tick = _lastSeconds > secs && secs >= 1 && secs <= 10 && !_match.Spectator
                            && _match.CurrentPhase == MatchController.Phase.Playing;
                _lastSeconds = secs;
                _timer.text = UIKit.FormatTime(secs);
                _timer.color = secs <= 10 ? UIKit.Red : Color.white;
                if (tick)
                {
                    // final countdown: the clock thumps and beeps every second
                    _timerPunch = 1f;
                    _ctx.Sfx.PlayUi(SfxId.Beep, secs <= 3 ? 0.7f : 0.4f);
                    if (secs == 10) OnAnnounce("FINAL 10 SECONDS", new Color(1f, 0.4f, 0.3f), false);
                }
            }
            if (_timerPunch > 0f)
            {
                _timerPunch = Mathf.Max(0f, _timerPunch - udt * 2.6f);
                float tp = 1f + _timerPunch * _timerPunch * 0.35f;
                _timerRt.localScale = new Vector3(tp, tp, 1f);
                _timerBg.color = Color.Lerp(new Color(1f, 1f, 1f, 0.92f), new Color(1f, 0.35f, 0.3f, 1f), _timerPunch);
            }
            UpdateScore(local);
            if (_scorePunch > 0f)
            {
                _scorePunch -= udt;
                float sp = 1f + Mathf.Clamp01(_scorePunch / 0.35f) * 0.45f;
                _score.rectTransform.localScale = new Vector3(sp, sp, 1f);
                _score.color = Color.Lerp(UIKit.Gold, Color.white, Mathf.Clamp01(_scorePunch / 0.35f));
            }

            if (local != null)
            {
                float hp01 = local.Health01;
                _shownHp = Mathf.MoveTowards(_shownHp, hp01, udt * 2.5f);
                if (hp01 < _ghostHp - 0.001f)
                {
                    _ghostDelay -= udt;
                    if (_ghostDelay <= 0f) _ghostHp = Mathf.MoveTowards(_ghostHp, hp01, udt * 0.9f);
                }
                else { _ghostHp = hp01; _ghostDelay = 0.4f; }
                _hpFill.fillAmount = _shownHp;
                _hpGhost.fillAmount = _ghostHp;
                _hpFill.color = FighterVisual.HealthColor(_shownHp);
                _armorFill.fillAmount = local.Armor01;
                int hpInt = Mathf.CeilToInt(local.Health);
                if (hpInt != _lastHp) { _lastHp = hpInt; _hpText.text = hpInt.ToString(); }

                var w = local.Weapons;
                if (w.Version != _lastAmmoVersion)
                {
                    _lastAmmoVersion = w.Version;
                    var cur = w.Current;
                    _weaponName.text = cur.Stats.DisplayName;
                    _ammo.text = cur.Mag.ToString();
                    _reserve.text = "/ " + (cur.Stats.InfiniteReserve ? "∞" : cur.Reserve.ToString());
                    _lowAmmo = cur.Stats.MagSize > 3 && cur.Mag <= Mathf.Max(1, cur.Stats.MagSize / 4);
                    _ammo.color = cur.Mag == 0 ? UIKit.Red : _lowAmmo ? UIKit.Orange : Color.white;
                    if (!_lowAmmo) _ammoRt.localScale = Vector3.one;
                    var gun = WeaponArt.Get(_ctx.Catalog, cur.Stats.Id);
                    _gunIcon.enabled = gun.Valid;
                    if (gun.Valid)
                    {
                        _gunIcon.sprite = gun.Sprite;
                        _gunIcon.color = gun.Tint;
                    }
                    var other = w.Primary != null && w.Current != w.Primary ? w.Primary.Stats.Id : WeaponId.Pistol;
                    _touch.SetSwapIcon(WeaponArt.Get(_ctx.Catalog, other));
                }
                if (local.Grenades != _lastGrenades)
                {
                    _lastGrenades = local.Grenades;
                    _grenades.text = "x" + local.Grenades;
                    _nadeIcon.color = local.Grenades > 0 ? Color.white : new Color(1f, 1f, 1f, 0.35f);
                    _touch.GrenadeCount.text = local.Grenades.ToString();
                }
                _reloadFill.fillAmount = w.IsReloading ? w.Reload01 : 0f;
                if (_lowAmmo)
                {
                    float ap = w.IsReloading ? 1f : 1f + Mathf.Abs(Mathf.Sin(Time.unscaledTime * 7f)) * 0.14f;
                    _ammoRt.localScale = new Vector3(ap, ap, 1f);
                }
                _touch.DashFill.fillAmount = 1f - local.Motor.DashReady01;

                var near = local.NearbyWeaponPickup;
                bool showPrompt = near != null && local.Alive;
                if (_touch.PickupButton.activeSelf != (showPrompt && input.TouchActive)) _touch.PickupButton.SetActive(showPrompt && input.TouchActive);
                _prompt.text = showPrompt && !input.TouchActive ? "[E] PICK UP " + _ctx.Config.Get(near.Weapon).DisplayName : "";
                if (showPrompt && input.TouchActive) _touch.SetPickup(_ctx.Config.Get(near.Weapon).DisplayName, WeaponArt.Get(_ctx.Catalog, near.Weapon));

                bool dead = !local.Alive && _match.CurrentPhase == MatchController.Phase.Playing;
                if (_deathPanel.activeSelf != dead) _deathPanel.SetActive(dead);
                if (dead)
                {
                    int left = Mathf.CeilToInt(_ctx.Config.Player.RespawnDelay - (Time.time - local.DeathTime));
                    if (left != _lastRespawn) { _lastRespawn = left; _deathSub.text = "Back in the fight in " + Mathf.Max(0, left) + "..."; }
                }

                float low = local.Alive && hp01 < 0.3f ? (0.25f + 0.2f * Mathf.Sin(Time.unscaledTime * 6.5f)) * (1f - hp01 / 0.3f) + 0.15f : 0f;
                _lowHp.color = new Color(0.7f, 0f, 0f, low);
            }

            _vignetteA = Mathf.MoveTowards(_vignetteA, 0f, udt * 1.8f);
            _vignette.color = new Color(0.9f, 0.05f, 0.05f, _vignetteA);

            if (_centerT > 0f) AnimateBanner(udt);
            if (_toastT > 0f)
            {
                _toastT -= udt;
                var c = _toast.color;
                c.a = Mathf.Clamp01(_toastT / 0.4f);
                _toast.color = c;
                if (_toastT <= 0f) _toast.text = "";
            }

            for (int i = 0; i < FeedLines; i++)
            {
                var f = _feed[i];
                if (f.Time <= 0f) { if (f.Group.alpha > 0f) f.Group.alpha = 0f; continue; }
                f.Time -= udt;
                f.Group.alpha = Mathf.Clamp01(f.Time / 0.6f);
            }

            bool board = input.ScoreboardHeld && _match.CurrentPhase != MatchController.Phase.Ended;
            if (_scoreboard.activeSelf != board) _scoreboard.SetActive(board);
            if (board) FillBoard();

            if (_ctx.Prefs.ShowFps)
            {
                _fpsFrames++;
                _fpsT += udt;
                if (_fpsT >= 0.5f)
                {
                    _fps.text = Mathf.RoundToInt(_fpsFrames / _fpsT) + " FPS";
                    _fpsFrames = 0;
                    _fpsT = 0f;
                }
            }
            else if (_fps.text.Length > 0) _fps.text = "";
        }

        /// <summary>
        /// Big text slams in (overshoot + shake) over a soft dark band; streak names get spinning rays,
        /// countdown digits get an expanding ring.
        /// </summary>
        private void AnimateBanner(float udt)
        {
            _centerT -= udt;
            float age = 1.2f - _centerT;
            float s = _annBig
                ? (age < 0.1f ? Mathf.Lerp(2.4f, 0.9f, age / 0.1f) : age < 0.2f ? Mathf.Lerp(0.9f, 1f, (age - 0.1f) / 0.1f) : 1f)
                : (age < 0.12f ? Mathf.Lerp(1.4f, 1f, age / 0.12f) : 1f);
            _centerRt.localScale = new Vector3(s, s, 1f);
            float shake = _annBig && age < 0.3f ? (1f - age / 0.3f) * 14f : 0f;
            _centerRt.anchoredPosition = _centerHome + new Vector2(Random.Range(-shake, shake), Random.Range(-shake, shake));

            float fade = Mathf.Clamp01(_centerT / 0.3f);
            var c = _center.color;
            c.a = fade;
            _center.color = c;

            _annBand.color = new Color(0f, 0f, 0f, UIKit.Shade((_annBig ? 0.55f : 0.4f) * fade * Mathf.Clamp01(age / 0.08f)));
            _annBand.rectTransform.localScale = _annBig ? Vector3.one : new Vector3(0.7f, 0.55f, 1f);

            bool rays = _annBig && !_annDigit;
            float rs = 0.55f + 0.45f * Mathf.Clamp01(age / 0.18f);
            _annRays.rectTransform.localScale = new Vector3(rs, rs, 1f);
            _annRays.color = rays ? new Color(_annColor.r, _annColor.g, _annColor.b, UIKit.Shade(0.6f * fade)) : Color.clear;

            if (_annDigit)
            {
                float ring = 0.35f + age * 2.4f;
                _annRing.rectTransform.localScale = new Vector3(ring, ring, 1f);
                _annRing.color = new Color(1f, 1f, 1f, UIKit.Shade(Mathf.Clamp01(1f - age / 0.6f) * 0.7f));
            }
            else if (_annRing.color.a > 0f) _annRing.color = Color.clear;

            if (_centerT <= 0f)
            {
                _center.text = "";
                _centerRt.anchoredPosition = _centerHome;
                _annBand.color = _annRays.color = _annRing.color = Color.clear;
            }
        }

        private void UpdateScore(Fighter local)
        {
            var rules = _match.Rules;
            if (rules.IsTeamMode)
            {
                int hash = rules.TeamKills(0) * 1000 + rules.TeamKills(1);
                if (hash == _lastScoreHash) return;
                _lastScoreHash = hash;
                _score.text = "<color=#ff6655>RED " + rules.TeamKills(0) + "</color>   -   <color=#6699ff>" + rules.TeamKills(1) + " BLUE</color>";
                return;
            }
            rules.GetStandings(_standings);
            if (_standings.Count == 0) return;
            var top = _standings[0];
            var me = local != null ? rules.Get(local.Id) : null;
            int h = top.Kills * 100000 + (me != null ? me.Kills * 100 + me.Deaths : 0) + top.Id * 7;
            if (h == _lastScoreHash) return;
            _lastScoreHash = h;
            int limit = rules.Settings.KillLimit;
            string lim = limit > 0 ? " / " + limit : "";
            _score.text = me != null
                ? "YOU " + me.Kills + lim + "      LEADER " + top.Name + " " + top.Kills
                : "LEADER " + top.Name + " " + top.Kills + lim;
        }

        private void FillBoard()
        {
            _match.GetStandings(_standings);
            _board.Fill(_standings, _match.Local != null ? _match.Local.Id : -1, false);
        }
    }
}
