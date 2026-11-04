using System;
using UnityEngine;
using UnityEngine.UI;

namespace NeonBrawl
{
    /// <summary>
    /// Power-ups on the HUD: the POWER button (shows the power waiting in your slot, glows, rings down
    /// while a power runs), a row of running-power timers next to the health bar, a "[C] POWER" card on PC,
    /// a soft colored glow around the screen while a power is on you, and the POWER PICK popup where you
    /// tap the power you want.
    /// </summary>
    public sealed class PowerHud : MonoBehaviour
    {
        private const int Slots = 4;
        private const float PickTime = 6f;

        private sealed class Timer
        {
            public GameObject Root;
            public Image Badge, Ring;
            public Text Secs;
            public int Power = -1;
        }

        private GameContext _ctx;
        private MatchController _match;
        private TouchControls _touch;
        private readonly Timer[] _timers = new Timer[Slots];
        private Image _tint;
        private GameObject _pcCard;
        private Image _pcBadge;
        private Text _pcName;

        // the POWER PICK popup
        private GameObject _pick;
        private Image _pickBar;
        private readonly GameObject[] _cards = new GameObject[4];
        private readonly Image[] _cardBadge = new Image[4];
        private readonly Text[] _cardName = new Text[4], _cardAbout = new Text[4], _cardStat = new Text[4];
        private readonly PowerId[] _offer = new PowerId[4];
        private int _offerCount;
        private float _pickLeft;
        private Action<PowerId> _onPick;

        private int _shownVersion = -1;
        private bool _shownHeld;
        private PowerId _lastUsed = PowerId.Count;
        private float _punch;

        public static PowerHud Build(RectTransform root, RectTransform safe, GameContext ctx, TouchControls touch)
        {
            var art = ctx.Art;
            var go = UIKit.Rect(safe, "Powers");
            UIKit.Stretch(go);
            var h = go.gameObject.AddComponent<PowerHud>();
            h._ctx = ctx;
            h._touch = touch;

            // colored glow at the screen edges while a power is on you
            h._tint = UIKit.Image(root, "PowerTint", art.Vignette, new Color(1f, 1f, 1f, 0f));
            UIKit.Stretch(h._tint.rectTransform, -80f);
            h._tint.transform.SetSiblingIndex(2);

            // running timers: right of the health panel
            for (int i = 0; i < Slots; i++)
            {
                var t = new Timer();
                var rt = UIKit.Rect(go, "Timer" + i);
                // a column right of the minimap (clear of the clock and the score line)
                UIKit.Place(rt, new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), new Vector2(322f, -196f - i * 92f), new Vector2(82f, 82f));
                var bg = UIKit.Image(rt, "Bg", art.Circle, new Color(0.03f, 0.05f, 0.09f, 0.85f));
                UIKit.Stretch(bg.rectTransform);
                t.Ring = UIKit.Image(rt, "Ring", art.Ring, Color.white);
                t.Ring.type = Image.Type.Filled;
                t.Ring.fillMethod = Image.FillMethod.Radial360;
                t.Ring.fillOrigin = (int)Image.Origin360.Top;
                t.Ring.fillClockwise = false;
                UIKit.Stretch(t.Ring.rectTransform, -3f);
                t.Badge = UIKit.Icon(rt, "Badge", null, new Vector2(64f, 64f));
                UIKit.Place(t.Badge.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(62f, 62f));
                var sb = UIKit.Image(rt, "SecsBg", art.Pill != null ? art.Pill : art.RoundRect, new Color(0.03f, 0.05f, 0.09f, 0.9f), true);
                UIKit.Place(sb.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), new Vector2(0f, 2f), new Vector2(54f, 28f));
                t.Secs = UIKit.Label(sb.transform, "Secs", "", 22, Color.white, TextAnchor.MiddleCenter);
                UIKit.Stretch(t.Secs.rectTransform);
                t.Root = rt.gameObject;
                t.Root.SetActive(false);
                h._timers[i] = t;
            }

            // PC: the power in your slot, left of the ammo panel
            var card = UIKit.Panel9(safe, "PowerCard", ArtIds.PanelDark, new Color(1f, 1f, 1f, 0.92f), 2.4f);
            UIKit.Place(card.rectTransform, new Vector2(0.5f, 0f), new Vector2(1f, 0f), new Vector2(-296f, 14f), new Vector2(250f, 130f));
            h._pcCard = card.gameObject;
            h._pcBadge = UIKit.Icon(card.transform, "Badge", null, new Vector2(92f, 92f));
            UIKit.Place(h._pcBadge.rectTransform, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(66f, 4f), new Vector2(92f, 92f));
            h._pcBadge.gameObject.AddComponent<Pulse>().Amount = 0.06f;
            h._pcName = UIKit.Label(card.transform, "Name", "", 22, UIKit.Cream, TextAnchor.MiddleLeft);
            UIKit.Place(h._pcName.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(118f, 0f), new Vector2(124f, 100f));
            h._pcName.horizontalOverflow = HorizontalWrapMode.Wrap;
            h._pcCard.SetActive(false);

            h.BuildPick(root, art);
            return h;
        }

        private void BuildPick(RectTransform safe, ArtLibrary art)
        {
            var root = UIKit.Rect(safe, "PowerPick");
            UIKit.Stretch(root);
            _pick = root.gameObject;
            var dim = UIKit.Image(root, "Dim", art.Square, UIKit.Dim(0f, 0f, 0.03f, 0.35f));
            UIKit.Stretch(dim.rectTransform, -200f);
            var title = UIKit.Title(root, "Title", "PICK YOUR POWER", 64, Color.white, Powers.Get(PowerId.Pick).Color);
            UIKit.Place(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 300f), new Vector2(1000f, 90f));
            var barBg = UIKit.Image(root, "BarBg", art.Pill != null ? art.Pill : art.RoundRect, new Color(0f, 0f, 0f, 0.6f), true);
            UIKit.Place(barBg.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 240f), new Vector2(560f, 22f));
            _pickBar = UIKit.Image(barBg.transform, "Bar", art.Pill != null ? art.Pill : art.RoundRect, Powers.Get(PowerId.Pick).Color, true);
            _pickBar.type = Image.Type.Filled;
            _pickBar.fillMethod = Image.FillMethod.Horizontal;
            UIKit.Stretch(_pickBar.rectTransform, 4f);
            for (int i = 0; i < 4; i++)
            {
                int k = i;
                var b = UIKit.GlassButton(root, "Card" + i, new Vector2(270f, 360f), () => Choose(k));
                var rt = (RectTransform)b.transform;
                UIKit.Place(rt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(270f, 360f));
                var glow = UIKit.Image(rt, "Glow", art.Glow, new Color(1f, 1f, 1f, 0.25f));
                UIKit.Place(glow.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -110f), new Vector2(260f, 240f));
                _cardBadge[i] = UIKit.Icon(rt, "Badge", null, new Vector2(150f, 150f));
                UIKit.Place(_cardBadge[i].rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -110f), new Vector2(150f, 150f));
                _cardBadge[i].gameObject.AddComponent<Bob>().Amount = 4f;
                _cardName[i] = UIKit.Label(rt, "Name", "", 30, Color.white, TextAnchor.MiddleCenter);
                UIKit.Place(_cardName[i].rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -218f), new Vector2(250f, 44f));
                _cardAbout[i] = UIKit.Label(rt, "About", "", 21, new Color(0.78f, 0.84f, 0.95f), TextAnchor.UpperCenter, false);
                UIKit.Place(_cardAbout[i].rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -246f), new Vector2(240f, 60f));
                _cardAbout[i].horizontalOverflow = HorizontalWrapMode.Wrap;
                _cardStat[i] = UIKit.Label(rt, "Stat", "", 24, UIKit.Gold, TextAnchor.MiddleCenter);
                UIKit.Place(_cardStat[i].rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), new Vector2(0f, 30f), new Vector2(250f, 34f));
                _cards[i] = rt.gameObject;
            }
            _pick.SetActive(false);
        }

        public void Bind(MatchController match)
        {
            if (_match != null && _match.PowerUps != null) _match.PowerUps.ChoiceNeeded -= OnChoice;
            _match = match;
            if (match != null && match.PowerUps != null) match.PowerUps.ChoiceNeeded += OnChoice;
            _pick.SetActive(false);
            _onPick = null;
            _shownVersion = -1;
            _lastUsed = PowerId.Count;
            for (int i = 0; i < Slots; i++) { _timers[i].Root.SetActive(false); _timers[i].Power = -1; }
            _tint.color = new Color(1f, 1f, 1f, 0f);
        }

        private void OnDestroy()
        {
            if (_match != null && _match.PowerUps != null) _match.PowerUps.ChoiceNeeded -= OnChoice;
        }

        // ------------------------------------------------------------------ POWER PICK

        private void OnChoice(PowerId[] choices, int count, int level, Action<PowerId> pick)
        {
            _offerCount = Mathf.Clamp(count, 1, 4);
            _onPick = pick;
            _pickLeft = PickTime;
            float w = 290f;
            for (int i = 0; i < 4; i++)
            {
                bool on = i < _offerCount;
                _cards[i].SetActive(on);
                if (!on) continue;
                var id = choices[i];
                _offer[i] = id;
                var d = Powers.Get(id);
                int lv = Mathf.Min(Powers.MaxLevel, _ctx.Profile.PowerLevel(id) + Powers.PickBonus(level));
                _cardBadge[i].sprite = PowerArt.Badge(id);
                _cardName[i].text = d.Name;
                _cardName[i].color = Color.Lerp(d.Color, Color.white, 0.35f);
                _cardAbout[i].text = d.About;
                _cardStat[i].text = "LV " + lv + "  ·  " + (d.Duration > 0f ? Powers.Stat(d, lv, true) : Powers.Stat(d, lv, false));
                var rt = (RectTransform)_cards[i].transform;
                rt.anchoredPosition = new Vector2((i - (_offerCount - 1) * 0.5f) * w, -10f);
                var pop = _cards[i].GetComponent<PopIn>();
                if (pop == null) pop = _cards[i].AddComponent<PopIn>();
                pop.Delay = 0.06f * i;
                pop.enabled = false;
                pop.enabled = true;
            }
            _pick.SetActive(true);
            _pick.transform.SetAsLastSibling(); // over the touch buttons
            _ctx.Sfx.PlayUi(SfxId.UiOpen, 0.9f);
        }

        private void Choose(int i)
        {
            if (_onPick == null || i >= _offerCount) return;
            var cb = _onPick;
            _onPick = null;
            _pick.SetActive(false);
            _ctx.Sfx.PlayUi(SfxId.UiTap, 0.8f);
            cb(_offer[i]);
        }

        // ------------------------------------------------------------------ frame

        private void Update()
        {
            if (_match == null) return;
            var me = _match.Local;
            float t = Time.unscaledTime;

            if (_pick.activeSelf)
            {
                _pickLeft -= Time.unscaledDeltaTime;
                _pickBar.fillAmount = Mathf.Clamp01(_pickLeft / PickTime);
                if (_pickLeft <= 0f || me == null || !me.Alive) Choose(UnityEngine.Random.Range(0, _offerCount));
            }
            if (me == null) return;
            var p = me.Powers;
            bool touch = _ctx.Input.TouchActive;

            // the slot (POWER button on phones, card on PC)
            if (p.Version != _shownVersion)
            {
                bool newHeld = p.HasHeld && !_shownHeld;
                _shownVersion = p.Version;
                _shownHeld = p.HasHeld;
                if (newHeld) _punch = 1f;
                var plate = _touch.PowerPlate;
                if (plate != null)
                {
                    plate.sprite = HudArt.Plate(p.HasHeld ? HudArt.Accent.Orange : HudArt.Accent.Steel);
                    plate.color = p.HasHeld ? Color.white : new Color(1f, 1f, 1f, 0.6f);
                    _touch.PowerIcon.enabled = p.HasHeld;
                    if (p.HasHeld) _touch.PowerIcon.sprite = PowerArt.Badge(p.Held);
                    _touch.PowerLabel.text = p.HasHeld ? Powers.Get(p.Held).Short : "POWER";
                }
                _pcBadge.sprite = p.HasHeld ? PowerArt.Badge(p.Held) : null;
                _pcName.text = p.HasHeld ? Powers.Get(p.Held).Name + "\n<color=#ffd257>[C] USE</color>" : "";
                RefreshTimers(p);
            }
            bool card = p.HasHeld && !touch && me.Alive;
            if (_pcCard.activeSelf != card) _pcCard.SetActive(card);

            if (_touch.PowerPlate != null)
            {
                // halo breathes in the power's color while one waits; the plate punches when it arrives
                Color hc = p.HasHeld ? Powers.Get(p.Held).Color : Color.white;
                float ha = p.HasHeld ? 0.45f + Mathf.Sin(t * 5f) * 0.2f : 0f;
                _touch.PowerGlow.color = new Color(hc.r, hc.g, hc.b, ha);
                _punch = Mathf.Max(0f, _punch - Time.unscaledDeltaTime * 3f);
                float s = 1f + _punch * _punch * 0.35f + (p.HasHeld ? Mathf.Sin(t * 5f) * 0.03f : 0f);
                _touch.PowerPlate.rectTransform.localScale = new Vector3(s, s, 1f);
                // ring: time left of the power you used last
                bool running = _lastUsed < PowerId.Count && p.Active(_lastUsed);
                _touch.PowerRing.fillAmount = running ? p.Left01(_lastUsed) : 0f;
                if (running) _touch.PowerRing.color = Powers.Get(_lastUsed).Color;
            }

            UpdateTimers(p, t);
            UpdateTint(me, p, t);
        }

        private void RefreshTimers(FighterPowers p)
        {
            // keep slots for powers still running, add new ones in order
            for (int i = 0; i < Slots; i++)
                if (_timers[i].Power >= 0 && !p.Active((PowerId)_timers[i].Power)) _timers[i].Power = -1;
            for (int id = 0; id < Powers.Count; id++)
            {
                if (!p.Active((PowerId)id)) continue;
                bool has = false;
                for (int i = 0; i < Slots; i++) if (_timers[i].Power == id) has = true;
                if (has) continue;
                for (int i = 0; i < Slots; i++)
                {
                    if (_timers[i].Power >= 0) continue;
                    _timers[i].Power = id;
                    _timers[i].Badge.sprite = PowerArt.Badge((PowerId)id);
                    _timers[i].Ring.color = Powers.All[id].Color;
                    _lastUsed = (PowerId)id;
                    var pop = _timers[i].Root.GetComponent<PopIn>();
                    if (pop == null) pop = _timers[i].Root.AddComponent<PopIn>();
                    pop.enabled = false;
                    pop.enabled = true;
                    break;
                }
            }
        }

        private void UpdateTimers(FighterPowers p, float t)
        {
            int frozenSlot = -1;
            for (int i = 0; i < Slots; i++)
            {
                var tm = _timers[i];
                bool on = tm.Power >= 0 && p.Active((PowerId)tm.Power);
                if (!on && tm.Power >= 0) { tm.Power = -1; }
                if (tm.Root.activeSelf != on) tm.Root.SetActive(on);
                if (!on) { if (frozenSlot < 0) frozenSlot = i; continue; }
                var id = (PowerId)tm.Power;
                tm.Ring.fillAmount = p.Left01(id);
                float left = p.Left(id);
                tm.Secs.text = Mathf.CeilToInt(left).ToString();
                // flash in the last 1.5 s
                float a = left < 1.5f && Mathf.Sin(t * 20f) > 0f ? 0.4f : 1f;
                tm.Badge.color = new Color(1f, 1f, 1f, a);
            }
        }

        private void UpdateTint(Fighter me, FighterPowers p, float t)
        {
            Color c = default;
            float a = 0f;
            if (!me.Alive) { }
            else if (p.Frozen) { c = Powers.Get(PowerId.Frost).Color; a = 0.55f; }
            else if (p.Active(PowerId.Ghost)) { c = Powers.Get(PowerId.Ghost).Color; a = 0.4f; }
            else if (p.Active(PowerId.Rage)) { c = Powers.Get(PowerId.Rage).Color; a = 0.32f; }
            else if (p.Eagle) { c = Powers.Get(PowerId.Eagle).Color; a = 0.28f; }
            else if (p.Surging) { c = Powers.Get(PowerId.Surge).Color; a = 0.24f; }
            else if (p.Shielded) { c = Powers.Get(PowerId.Shield).Color; a = 0.24f; }
            else if (p.Active(PowerId.Turbo)) { c = Powers.Get(PowerId.Turbo).Color; a = 0.18f; }
            if (a > 0f) a *= 0.8f + Mathf.Sin(t * 4f) * 0.2f;
            var cur = _tint.color;
            float na = Mathf.MoveTowards(cur.a, a, Time.unscaledDeltaTime * 2f);
            _tint.color = a > 0f ? new Color(c.r, c.g, c.b, na) : new Color(cur.r, cur.g, cur.b, na);
        }
    }
}
