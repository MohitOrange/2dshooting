using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace NeonBrawl
{
    /// <summary>An ON/OFF switch: a pill that turns green with a knob that slides across.</summary>
    public sealed class UiToggle : MonoBehaviour, IPointerClickHandler
    {
        private static readonly Color OnColor = new Color(0.26f, 0.78f, 0.34f);
        private static readonly Color OffColor = new Color(0.2f, 0.22f, 0.27f);

        private Image _track;
        private RectTransform _knob;
        private Text _label;
        private Action<bool> _changed;
        private float _t, _target;

        public bool Value { get; private set; }

        public static UiToggle Create(Transform parent, bool value, Action<bool> changed)
        {
            var art = UIKit.Art;
            var track = UIKit.Image(parent, "Toggle", art.Pill != null ? art.Pill : art.RoundRect, OffColor, true, true);
            track.rectTransform.sizeDelta = new Vector2(150f, 66f);
            var rim = UIKit.Image(track.transform, "Rim", art.Pill != null ? art.Pill : art.RoundRect, new Color(0f, 0f, 0f, 0.35f), true);
            UIKit.Stretch(rim.rectTransform, -3f);
            rim.transform.SetAsFirstSibling();
            var t = track.gameObject.AddComponent<UiToggle>();
            t._track = track;
            t._label = UIKit.Label(track.transform, "State", "", 24, Color.white, TextAnchor.MiddleCenter);
            UIKit.Stretch(t._label.rectTransform);
            t._label.rectTransform.offsetMin = new Vector2(18f, 0f);
            t._label.rectTransform.offsetMax = new Vector2(-18f, 0f);
            var knob = UIKit.Image(track.transform, "Knob", art.Circle, Color.white);
            UIKit.Place(knob.rectTransform, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(54f, 54f));
            var shade = UIKit.Image(knob.transform, "Shade", art.Ring, new Color(0f, 0f, 0f, 0.18f));
            UIKit.Stretch(shade.rectTransform);
            t._knob = knob.rectTransform;
            t._changed = changed;
            t.Set(value, false);
            t._t = t._target;
            t.Apply();
            return t;
        }

        public void Set(bool value, bool notify)
        {
            Value = value;
            _target = value ? 1f : 0f;
            _label.text = value ? "ON" : "OFF";
            _label.alignment = value ? TextAnchor.MiddleLeft : TextAnchor.MiddleRight;
            if (notify) _changed?.Invoke(value);
        }

        public void OnPointerClick(PointerEventData e)
        {
            Set(!Value, true);
            UIKit.Sfx?.PlayUi(SfxId.UiTap, 1f);
            UIKit.Haptics?.Pulse(8);
        }

        private void Update()
        {
            if (Mathf.Approximately(_t, _target)) return;
            _t = Mathf.MoveTowards(_t, _target, Time.unscaledDeltaTime * 7f);
            Apply();
        }

        private void Apply()
        {
            float e = _t * _t * (3f - 2f * _t);
            _knob.anchoredPosition = new Vector2(Mathf.Lerp(34f, 116f, e), 0f);
            _track.color = Color.Lerp(OffColor, OnColor, e);
        }
    }

    /// <summary>A volume-style slider: dark track, gold fill, round handle and a percent readout.</summary>
    public static class UiSlider
    {
        public static Slider Create(Transform parent, float value01, Action<float> changed, float width = 420f)
        {
            var art = UIKit.Art;
            var root = UIKit.Rect(parent, "Slider");
            root.sizeDelta = new Vector2(width + 110f, 66f);
            var bg = UIKit.Image(root, "Track", art.Pill != null ? art.Pill : art.RoundRect, new Color(0.06f, 0.07f, 0.1f, 0.95f), true, true);
            UIKit.Place(bg.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(width, 26f));
            var fillArea = UIKit.Rect(bg.transform, "FillArea");
            UIKit.Stretch(fillArea, 4f);
            var fill = UIKit.Image(fillArea, "Fill", art.Pill != null ? art.Pill : art.RoundRect, UIKit.Gold, true);
            UIKit.Stretch(fill.rectTransform);
            var handleArea = UIKit.Rect(bg.transform, "HandleArea");
            UIKit.Stretch(handleArea);
            handleArea.offsetMin = new Vector2(14f, 0f);
            handleArea.offsetMax = new Vector2(-14f, 0f);
            var handle = UIKit.Image(handleArea, "Handle", art.Circle, Color.white);
            handle.rectTransform.sizeDelta = new Vector2(52f, 52f);
            var ring = UIKit.Image(handle.transform, "Ring", art.Ring, new Color(0.85f, 0.55f, 0.1f));
            UIKit.Stretch(ring.rectTransform);
            var readout = UIKit.Label(root, "Percent", "", 28, Color.white, TextAnchor.MiddleRight);
            UIKit.Place(readout.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(100f, 50f));

            var s = bg.gameObject.AddComponent<Slider>();
            s.fillRect = fill.rectTransform;
            s.handleRect = handle.rectTransform;
            s.targetGraphic = handle;
            s.direction = Slider.Direction.LeftToRight;
            s.minValue = 0f;
            s.maxValue = 1f;
            s.value = Mathf.Clamp01(value01);
            readout.text = Mathf.RoundToInt(s.value * 100f) + "%";
            s.onValueChanged.AddListener(v =>
            {
                readout.text = Mathf.RoundToInt(v * 100f) + "%";
                changed?.Invoke(v);
            });
            return s;
        }
    }

    /// <summary>A row of options where the chosen one lights up gold (e.g. OFF / LOW / FULL).</summary>
    public sealed class UiSegment
    {
        private readonly Button[] _buttons;
        private readonly Action<int> _changed;
        public int Index { get; private set; }

        public RectTransform Root { get; }

        public UiSegment(Transform parent, string[] options, int index, Action<int> changed, float itemWidth = 150f)
        {
            _changed = changed;
            Root = UIKit.Rect(parent, "Segment");
            Root.sizeDelta = new Vector2(options.Length * (itemWidth + 8f) - 8f, 70f);
            _buttons = new Button[options.Length];
            for (int i = 0; i < options.Length; i++)
            {
                int k = i;
                var b = UIKit.ArtButton(Root, options[i], options[i], ArtIds.PanelSlate, new Vector2(itemWidth, 66f), 26, () => Pick(k));
                UIKit.Place((RectTransform)b.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(i * (itemWidth + 8f), 0f), new Vector2(itemWidth, 66f));
                _buttons[i] = b;
            }
            Show(index);
        }

        private void Pick(int i)
        {
            if (i == Index) return;
            Show(i);
            _changed?.Invoke(i);
        }

        public void Show(int index)
        {
            Index = Mathf.Clamp(index, 0, _buttons.Length - 1);
            for (int i = 0; i < _buttons.Length; i++)
            {
                bool on = i == Index;
                var img = (Image)_buttons[i].targetGraphic;
                var s = UIKit.Cat != null ? UIKit.Cat.Panel(on ? ArtIds.PanelGold : ArtIds.PanelSlate) : null;
                if (s != null) img.sprite = s;
                else img.color = on ? UIKit.Gold : UIKit.Panel;
                var label = _buttons[i].transform.Find("Label");
                if (label == null) continue;
                label.GetComponent<Text>().color = on ? UIKit.TextOn(ArtIds.PanelGold) : new Color(0.8f, 0.85f, 0.95f);
                var outline = label.GetComponent<Outline>(); // dark text on gold reads best with a light rim
                if (outline != null) outline.effectColor = on ? new Color(1f, 1f, 1f, 0.4f) : new Color(0f, 0f, 0f, 0.7f);
            }
        }
    }
}
