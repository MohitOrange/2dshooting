using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace NeonBrawl
{
    /// <summary>
    /// uGUI builder used by every screen. Buttons and panels use the painted U1 kit (9-sliced), icons
    /// come from U2/U4, and everything falls back to code-drawn rounded shapes when art is missing.
    /// </summary>
    public static class UIKit
    {
        public static readonly Color Panel = new Color(0.06f, 0.08f, 0.13f, 0.86f);
        public static readonly Color Green = new Color(0.3f, 0.78f, 0.32f);
        public static readonly Color Orange = new Color(1f, 0.58f, 0.2f);
        public static readonly Color Blue = new Color(0.26f, 0.55f, 0.95f);
        public static readonly Color Red = new Color(0.9f, 0.3f, 0.26f);
        public static readonly Color Grey = new Color(0.4f, 0.42f, 0.46f);
        public static readonly Color Gold = new Color(1f, 0.82f, 0.3f);
        public static readonly Color Ink = new Color(0.03f, 0.04f, 0.08f);
        public static readonly Color Cream = new Color(1f, 0.96f, 0.86f);
        public static readonly Color Sky = new Color(0.62f, 0.85f, 1f);

        private static Font _font;
        private static bool _gameFont;

        /// <summary>
        /// Lilita One (SIL Open Font License, Resources/Fonts) for every label, in menus and in the world, so
        /// text looks and measures the same on every phone. Unity's built-in font only if the file is missing.
        /// </summary>
        public static Font Font
        {
            get
            {
                if (_font != null) return _font;
                _font = Resources.Load<Font>("Fonts/LilitaOne-Regular");
                _gameFont = _font != null;
                if (!_gameFont) _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return _font;
            }
        }

        /// <summary>The game font is already heavy; fake bold would only make it blurry and wider.</summary>
        public static FontStyle Strong { get { _ = Font; return _gameFont ? FontStyle.Normal : FontStyle.Bold; } }
        public static FontStyle Display { get { _ = Font; return _gameFont ? FontStyle.Italic : FontStyle.BoldAndItalic; } }

        public static ArtLibrary Art;
        public static ArtCatalog Cat;
        public static SfxSystem Sfx;
        public static Haptics Haptics;

        // ------------------------------------------------------------------ layout

        public static RectTransform Rect(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static RectTransform Place(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        public static RectTransform Stretch(RectTransform rt, float inset = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(inset, inset);
            rt.offsetMax = new Vector2(-inset, -inset);
            return rt;
        }

        // ------------------------------------------------------------------ primitives

        public static Image Image(Transform parent, string name, Sprite sprite, Color color, bool sliced = false, bool raycast = false)
        {
            var rt = Rect(parent, name);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.type = sliced ? UnityEngine.UI.Image.Type.Sliced : UnityEngine.UI.Image.Type.Simple;
            img.raycastTarget = raycast;
            return img;
        }

        /// <summary>An icon that keeps its aspect ratio.</summary>
        public static Image Icon(Transform parent, string name, Sprite sprite, Vector2 size, Color? tint = null)
        {
            var img = Image(parent, name, sprite != null ? sprite : Art.Circle, tint ?? Color.white);
            img.preserveAspect = true;
            img.rectTransform.sizeDelta = size;
            return img;
        }

        public static Text Label(Transform parent, string name, string text, int size, Color color, TextAnchor align, bool outline = true)
        {
            var rt = Rect(parent, name);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = Font;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.fontStyle = Strong;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.supportRichText = true;
            if (outline)
            {
                var o = rt.gameObject.AddComponent<Outline>();
                o.effectColor = new Color(0f, 0f, 0f, 0.75f);
                o.effectDistance = new Vector2(2f, -2f);
            }
            var fit = rt.gameObject.AddComponent<FitLabel>();
            fit.MinSize = Mathf.Max(10, Mathf.RoundToInt(size * 0.45f));
            return t;
        }

        /// <summary>Chunky game title text: italic bold, gradient, thick outline and drop shadow.</summary>
        public static Text Title(Transform parent, string name, string text, int size, Color top, Color bottom, TextAnchor align = TextAnchor.MiddleCenter)
        {
            var t = Label(parent, name, text, size, Color.white, align, false);
            t.fontStyle = Display;
            var g = t.gameObject.AddComponent<UIGradient>();
            g.Top = top;
            g.Bottom = bottom;
            var o = t.gameObject.AddComponent<Outline>();
            o.effectColor = new Color(0.05f, 0.03f, 0.08f, 1f);
            o.effectDistance = new Vector2(size * 0.045f, -size * 0.045f);
            var s = t.gameObject.AddComponent<Shadow>();
            s.effectColor = new Color(0f, 0f, 0f, 0.6f);
            s.effectDistance = new Vector2(0f, -size * 0.09f);
            return t;
        }

        // ------------------------------------------------------------------ painted kit

        /// <summary>A 9-sliced painted panel (U1). <paramref name="thickness"/> scales the painted border.</summary>
        public static Image Panel9(Transform parent, string name, int panelId, Color tint, float thickness = 3.2f, bool raycast = false)
        {
            var s = Cat != null ? Cat.Panel(panelId) : null;
            var img = Image(parent, name, s != null ? s : Art.RoundRect, s != null ? tint : Multiply(FallbackColor(panelId), tint), true, raycast);
            if (s != null) img.pixelsPerUnitMultiplier = 1f / thickness;
            return img;
        }

        private static Color Multiply(Color a, Color b) => new Color(a.r * b.r, a.g * b.g, a.b * b.b, a.a * b.a);

        private static Color FallbackColor(int panelId)
        {
            switch (panelId)
            {
                case ArtIds.PanelBlue: return Blue;
                case ArtIds.PanelGold: case ArtIds.PanelSand: return Gold;
                case ArtIds.PanelWood: case ArtIds.PanelWood2: case ArtIds.PanelOrange: return Orange;
                case ArtIds.PanelTan: case ArtIds.PanelBeige: return new Color(0.85f, 0.75f, 0.55f);
                default: return Panel;
            }
        }

        /// <summary>Text color that reads on a given painted panel.</summary>
        public static Color TextOn(int panelId)
        {
            switch (panelId)
            {
                case ArtIds.PanelGold: case ArtIds.PanelSand: case ArtIds.PanelTan: case ArtIds.PanelBeige: return new Color(0.28f, 0.14f, 0.04f);
                default: return Color.white;
            }
        }

        public static Button Button(Transform parent, string name, string label, Color color, Vector2 size, int fontSize, Action onClick)
        {
            int panel = color == Green || color == Gold ? ArtIds.PanelGold : color == Blue ? ArtIds.PanelBlue : color == Red ? ArtIds.PanelOrange : ArtIds.PanelDark;
            return ArtButton(parent, name, label, panel, size, fontSize, onClick);
        }

        /// <summary>Painted button: U1 panel, optional icon on the left, squash + click sound.</summary>
        public static Button ArtButton(Transform parent, string name, string label, int panelId, Vector2 size, int fontSize, Action onClick, Sprite icon = null)
        {
            var img = Panel9(parent, name, panelId, Color.white, 3.2f, true);
            img.rectTransform.sizeDelta = size;
            var btn = img.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            var colors = btn.colors;
            colors.highlightedColor = new Color(1.08f, 1.08f, 1.08f);
            colors.pressedColor = new Color(0.82f, 0.82f, 0.82f);
            colors.disabledColor = new Color(0.45f, 0.45f, 0.48f, 0.85f);
            btn.colors = colors;
            Color tc = TextOn(panelId);
            float iconSize = Mathf.Min(size.y * 0.62f, 96f);
            if (icon != null)
            {
                var ic = Icon(img.transform, "Icon", icon, new Vector2(iconSize, iconSize));
                Place(ic.rectTransform, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(size.y * 0.5f + 6f, 2f), new Vector2(iconSize, iconSize));
            }
            if (!string.IsNullOrEmpty(label))
            {
                var text = Label(img.transform, "Label", label, fontSize, tc, TextAnchor.MiddleCenter, false);
                Stretch(text.rectTransform);
                // keep the text inside the painted frame (it shrinks to fit, see FitLabel)
                float pad = Mathf.Clamp(size.y * 0.16f, 8f, 22f);
                text.rectTransform.offsetMin = new Vector2(icon != null ? iconSize + 14f : pad, 0f);
                text.rectTransform.offsetMax = new Vector2(-pad, 0f);
                if (tc == Color.white)
                {
                    var o = text.gameObject.AddComponent<Outline>();
                    o.effectColor = new Color(0f, 0f, 0f, 0.7f);
                    o.effectDistance = new Vector2(2f, -2f);
                }
                else
                {
                    var o = text.gameObject.AddComponent<Shadow>();
                    o.effectColor = new Color(1f, 1f, 1f, 0.45f);
                    o.effectDistance = new Vector2(0f, -2f);
                }
            }
            img.gameObject.AddComponent<ButtonJuice>();
            if (onClick != null) btn.onClick.AddListener(() => onClick());
            return btn;
        }

        /// <summary>Square icon button (bottom nav, top bar): painted panel, big icon, small caption.</summary>
        public static Button IconButton(Transform parent, string name, Sprite icon, string caption, int panelId, Vector2 size, Action onClick)
        {
            var b = ArtButton(parent, name, null, panelId, size, 20, onClick);
            float s = Mathf.Min(size.x, size.y) * (string.IsNullOrEmpty(caption) ? 0.66f : 0.56f);
            var ic = Icon(b.transform, "Icon", icon, new Vector2(s, s));
            Place(ic.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, string.IsNullOrEmpty(caption) ? 0f : 12f), new Vector2(s, s));
            if (!string.IsNullOrEmpty(caption))
            {
                var t = Label(b.transform, "Caption", caption, Mathf.RoundToInt(size.y * 0.15f), TextOn(panelId), TextAnchor.LowerCenter);
                Place(t.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 8f), new Vector2(size.x, 30f));
            }
            return b;
        }

        /// <summary>A small rounded badge with text (e.g. "SOON", "NEW", a price).</summary>
        public static Text Badge(Transform parent, string text, Color bg, Vector2 pos, Vector2 anchor, int size = 22)
        {
            var img = Image(parent, "Badge", Art.Pill != null ? Art.Pill : Art.RoundRect, bg, true);
            var t = Label(img.transform, "T", text, size, Color.white, TextAnchor.MiddleCenter);
            Stretch(t.rectTransform);
            float w = Mathf.Max(60f, t.preferredWidth + 28f);
            Place(img.rectTransform, anchor, new Vector2(0.5f, 0.5f), pos, new Vector2(w, size + 16f));
            img.transform.localRotation = Quaternion.Euler(0f, 0f, -8f);
            return t;
        }

        /// <summary>
        /// The project renders in linear color space, where a half-transparent dark layer looks much lighter
        /// than its alpha says. This converts the darkness you want into the alpha that produces it.
        /// </summary>
        public static float Shade(float perceived) => 1f - Mathf.Pow(1f - Mathf.Clamp01(perceived), 2.2f);

        public static Color Dim(float r, float g, float b, float perceived) => new Color(r, g, b, Shade(perceived));

        /// <summary>Full-screen vertical gradient (dim layers behind menus).</summary>
        public static Image GradientFill(Transform parent, string name, Color top, Color bottom, bool raycast = false)
        {
            var img = Image(parent, name, Art.Square, Color.white, false, raycast);
            Stretch(img.rectTransform);
            var g = img.gameObject.AddComponent<UIGradient>();
            g.Top = top;
            g.Bottom = bottom;
            return img;
        }

        // ------------------------------------------------------------------ glass kit (lobby, top bars)

        /// <summary>
        /// A dark glass card with a soft drop shadow. Returns a container (move, slide or scale it as one piece)
        /// holding the Shadow and the Face; add content to the container so it draws above the face.
        /// </summary>
        public static RectTransform GlassCard(Transform parent, string name, Vector2 size, out Image face, bool pill = false, float shadow = 0.55f)
        {
            var root = Rect(parent, name);
            root.sizeDelta = size;
            if (shadow > 0f) DropShadow(root, shadow);
            face = Image(root, "Face", pill ? GlassArt.Pill : GlassArt.Card, Color.white, true, true);
            if (pill) face.pixelsPerUnitMultiplier = 128f / Mathf.Max(8f, size.y);
            Stretch(face.rectTransform);
            return root;
        }

        /// <summary>Soft shadow under a box: a child stretched past its edges and dropped a little.</summary>
        public static Image DropShadow(RectTransform box, float alpha = 0.55f, float drop = 10f)
        {
            var sh = Image(box, "Shadow", GlassArt.Shadow, new Color(0f, 0f, 0f, alpha), true);
            sh.pixelsPerUnitMultiplier = GlassArt.ShadowScale;
            Stretch(sh.rectTransform, -GlassArt.ShadowSpread);
            sh.rectTransform.anchoredPosition = new Vector2(0f, -drop);
            sh.transform.SetAsFirstSibling();
            return sh;
        }

        /// <summary>Glass card that works as a button (press squash + sound). Content goes on the returned transform.</summary>
        public static Button GlassButton(Transform parent, string name, Vector2 size, Action onClick, bool pill = false)
        {
            var root = GlassCard(parent, name, size, out var face, pill);
            var btn = root.gameObject.AddComponent<Button>();
            btn.targetGraphic = face;
            var colors = btn.colors;
            colors.highlightedColor = new Color(1.12f, 1.12f, 1.15f);
            colors.pressedColor = new Color(0.8f, 0.82f, 0.88f);
            btn.colors = colors;
            root.gameObject.AddComponent<ButtonJuice>();
            if (onClick != null) btn.onClick.AddListener(() => onClick());
            return btn;
        }

        /// <summary>
        /// Coin or gem amount in every top bar: a glass capsule centred on the bar row, the icon overlapping its
        /// left end and a gold "+" (opens the shop) at the right. <paramref name="right"/> is the capsule's right
        /// edge from the safe area's right side; <paramref name="rowTop"/>/<paramref name="rowH"/> the bar row.
        /// </summary>
        public static Text CurrencyPill(Transform parent, string name, Sprite icon, Color accent, float right, float rowTop, float rowH, float width, Action onPlus)
        {
            const float h = 72f;
            var root = GlassCard(parent, name, new Vector2(width, h), out var face, true, 0.45f);
            Place(root, new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(-right, -rowTop - rowH * 0.5f), new Vector2(width, h));
            var ic = Icon(root, "Icon", icon, new Vector2(84f, 84f));
            Place(ic.rectTransform, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(16f, 0f), new Vector2(84f, 84f));
            ic.gameObject.AddComponent<Bob>().Amount = 2.5f;
            float plusW = onPlus != null ? 64f : 0f;
            var t = Label(root, "Amount", "0", 36, accent, TextAnchor.MiddleCenter);
            Place(t.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(62f, 1f), new Vector2(width - 62f - plusW - 10f, h));
            if (onPlus != null)
            {
                var plus = Image(root, "Plus", Art.Circle, Gold, false, true);
                Place(plus.rectTransform, new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-38f, 0f), new Vector2(52f, 52f));
                var g = plus.gameObject.AddComponent<UIGradient>();
                g.Top = new Color(1f, 0.97f, 0.75f);
                g.Bottom = new Color(1f, 0.62f, 0.2f);
                var pt = Label(plus.transform, "T", "+", 44, new Color(0.3f, 0.15f, 0.03f), TextAnchor.MiddleCenter, false);
                Stretch(pt.rectTransform);
                pt.rectTransform.anchoredPosition = new Vector2(0f, 3f);
                var btn = plus.gameObject.AddComponent<Button>();
                btn.targetGraphic = plus;
                btn.onClick.AddListener(() => onPlus());
                plus.gameObject.AddComponent<ButtonJuice>();
                // the whole capsule opens the shop too (bigger target on phones)
                var whole = root.gameObject.AddComponent<Button>();
                whole.targetGraphic = face;
                whole.transition = Selectable.Transition.None;
                whole.onClick.AddListener(() => onPlus());
            }
            return t;
        }

        /// <summary>
        /// The game logo built from the game font (sharp at any size): NEON in cool blue over BRAWL in hot
        /// orange, slanted, on a dark ink splash with a neon rim. Designed at 600×300 and scaled to
        /// <paramref name="width"/>. Returns the root (centre pivot) for placing.
        /// </summary>
        public static RectTransform Logo(Transform parent, float width)
        {
            var root = Rect(parent, "Logo");
            root.sizeDelta = new Vector2(600f, 300f);
            float k = width / 600f;
            root.localScale = new Vector3(k, k, 1f);
            var splat = Image(root, "Splat", GlassArt.Splat, Color.white);
            Place(splat.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -6f), new Vector2(700f, 360f));
            splat.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 4f);
            var words = Rect(root, "Words");
            Stretch(words);
            words.localRotation = Quaternion.Euler(0f, 0f, 6f);
            var neon = LogoWord(words, "NEON", 138, new Color(0.7f, 0.97f, 1f), new Color(0.1f, 0.45f, 1f));
            Place(neon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-34f, 62f), new Vector2(560f, 150f));
            var brawl = LogoWord(words, "BRAWL", 168, new Color(1f, 0.96f, 0.5f), new Color(1f, 0.42f, 0.08f));
            Place(brawl.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(18f, -66f), new Vector2(620f, 180f));
            return root;
        }

        private static Text LogoWord(Transform parent, string word, int size, Color top, Color bottom)
        {
            var t = Label(parent, word, word, size, Color.white, TextAnchor.MiddleCenter, false);
            t.fontStyle = Display;
            var g = t.gameObject.AddComponent<UIGradient>();
            g.Top = top;
            g.Bottom = bottom;
            // thick ink outline all round (diagonals + sides), then a deep drop
            var ink = new Color(0.03f, 0.02f, 0.07f, 1f);
            float d = size * 0.05f;
            var o1 = t.gameObject.AddComponent<Outline>();
            o1.effectColor = ink;
            o1.effectDistance = new Vector2(d, d);
            var o2 = t.gameObject.AddComponent<Outline>();
            o2.effectColor = ink;
            o2.effectDistance = new Vector2(d * 1.3f, 0f);
            var o3 = t.gameObject.AddComponent<Outline>();
            o3.effectColor = ink;
            o3.effectDistance = new Vector2(0f, d * 1.3f);
            var s = t.gameObject.AddComponent<Shadow>();
            s.effectColor = new Color(0f, 0f, 0f, 0.65f);
            s.effectDistance = new Vector2(0f, -size * 0.1f);
            return t;
        }

        public static string FormatTime(float seconds)
        {
            int s = Mathf.CeilToInt(Mathf.Max(0f, seconds));
            return (s / 60) + ":" + (s % 60).ToString("00");
        }

        public static string Thousands(int n) => n >= 10000 ? (n / 1000) + "K" : n.ToString("N0");
    }

    /// <summary>Vertical two-color gradient multiplied into any Graphic (text or image).</summary>
    public sealed class UIGradient : BaseMeshEffect
    {
        public Color Top = Color.white;
        public Color Bottom = new Color(0.8f, 0.8f, 0.8f);
        private static readonly List<UIVertex> Verts = new List<UIVertex>(256);

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive() || vh.currentVertCount == 0) return;
            Verts.Clear();
            vh.GetUIVertexStream(Verts);
            float min = float.MaxValue, max = float.MinValue;
            for (int i = 0; i < Verts.Count; i++)
            {
                float y = Verts[i].position.y;
                if (y < min) min = y;
                if (y > max) max = y;
            }
            float range = Mathf.Max(0.001f, max - min);
            for (int i = 0; i < Verts.Count; i++)
            {
                var v = Verts[i];
                Color c = Color.Lerp(Bottom, Top, (v.position.y - min) / range);
                Color32 o = v.color;
                v.color = new Color32((byte)(o.r * c.r), (byte)(o.g * c.g), (byte)(o.b * c.b), (byte)(o.a * c.a));
                Verts[i] = v;
            }
            vh.Clear();
            vh.AddUIVertexTriangleStream(Verts);
        }
    }

    /// <summary>
    /// Springy press: squashes on touch (with a soft tap sound), springs back past full size and wobbles
    /// into place on release, grows a little under the mouse. Works while the game is paused.
    /// </summary>
    /// <summary>
    /// Shrinks a one-line label until it fits its box (names, prices, translated words, long room codes),
    /// so text never spills out of a button. Code that sets a new font size later is respected as the new
    /// maximum. Wrapped or best-fit labels are left alone.
    /// </summary>
    [RequireComponent(typeof(Text))]
    public sealed class FitLabel : MonoBehaviour
    {
        public int MinSize = 12;

        private Text _text;
        private int _max, _applied = -1;
        private string _lastText;
        private float _lastWidth = -1f;
        private bool _busy;

        private void Awake()
        {
            _text = GetComponent<Text>();
            _text.RegisterDirtyVerticesCallback(Fit);
        }

        private void OnDestroy()
        {
            if (_text != null) _text.UnregisterDirtyVerticesCallback(Fit);
        }

        private void OnEnable() => Fit();

        private void OnRectTransformDimensionsChange() => Fit();

        public void Fit()
        {
            if (_busy || _text == null) return;
            if (_text.resizeTextForBestFit || _text.horizontalOverflow != HorizontalWrapMode.Overflow)
            {
                // the label was switched to wrapping after it was shrunk in its default 100 px box: undo that
                if (_applied >= 0 && _text.fontSize == _applied && _max > 0)
                {
                    _busy = true;
                    _text.fontSize = _max;
                    _busy = false;
                }
                _applied = -1;
                return;
            }
            if (_text.fontSize != _applied) { _max = _text.fontSize; _lastText = null; } // someone else set a size
            float avail = ((RectTransform)transform).rect.width;
            if (avail <= 1f) return;
            string s = _text.text;
            if (ReferenceEquals(s, _lastText) && Mathf.Abs(avail - _lastWidth) < 0.5f) return;
            _busy = true;
            _lastText = s;
            _lastWidth = avail;
            _text.fontSize = _max;
            float w = _text.preferredWidth;
            if (w > avail + 0.5f) _text.fontSize = Mathf.Max(Mathf.Min(MinSize, _max), Mathf.FloorToInt(_max * avail / w));
            _applied = _text.fontSize;
            _busy = false;
        }
    }

    public sealed class ButtonJuice : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerEnterHandler, IPointerExitHandler
    {
        private float _target = 1f;
        private float _scale = 1f;
        private float _vel;
        private bool _down, _over;
        public float BaseScale = 1f;
        public SfxId Sound = SfxId.UiTap;

        public void OnPointerDown(PointerEventData e)
        {
            _down = true;
            _target = 0.88f;
            if (UIKit.Sfx != null) UIKit.Sfx.PlayUi(Sound, 1f);
            UIKit.Haptics?.Pulse(8);
        }

        public void OnPointerUp(PointerEventData e)
        {
            _down = false;
            _target = _over ? 1.05f : 1f;
            _vel += 4.5f; // kick the spring: overshoot, then settle
        }

        public void OnPointerEnter(PointerEventData e)
        {
            _over = true;
            if (!_down && e.pointerId < 0) _target = 1.05f; // mouse only, not touch
        }

        public void OnPointerExit(PointerEventData e)
        {
            _over = false;
            if (!_down) _target = 1f;
        }

        private void OnDisable()
        {
            _down = _over = false;
            _target = _scale = 1f;
            _vel = 0f;
            transform.localScale = new Vector3(BaseScale, BaseScale, 1f);
        }

        private void Update()
        {
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            // damped spring (stiffness 380, damping 18)
            float force = (_target - _scale) * 380f - _vel * 18f;
            _vel += force * dt;
            _scale += _vel * dt;
            float s = _scale * BaseScale;
            transform.localScale = new Vector3(s, s, 1f);
        }
    }

    /// <summary>
    /// Fits the 1920×1080 layout on every screen shape: wide phones (19.5:9, 20:9) scale by height,
    /// narrower screens (16:10, 4:3 tablets) scale by width so nothing overlaps or gets cut off.
    /// </summary>
    public sealed class AdaptiveCanvas : MonoBehaviour
    {
        public CanvasScaler Scaler;
        private Canvas _canvas;
        private int _w, _h;

        private void Update()
        {
            if (Scaler == null) return;
            if (_canvas == null) _canvas = Scaler.GetComponent<Canvas>();
            // the canvas pixel size (= screen, or the target camera when rendering off-screen)
            var px = _canvas.pixelRect.size;
            int w = Mathf.RoundToInt(px.x), h = Mathf.RoundToInt(px.y);
            if (w == _w && h == _h) return;
            _w = w;
            _h = h;
            float aspect = _w / (float)Mathf.Max(1, _h);
            Scaler.matchWidthOrHeight = aspect >= 1.76f ? 1f : 0f;
        }
    }

    /// <summary>Screen entrance: fades in and settles from a slight zoom when the screen is shown.</summary>
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class ScreenFade : MonoBehaviour
    {
        private CanvasGroup _g;
        private float _t = 1f;
        public float Duration = 0.22f;

        private void OnEnable()
        {
            if (_g == null) _g = GetComponent<CanvasGroup>();
            _t = 0f;
            _g.alpha = 0f;
        }

        private void Update()
        {
            if (_t >= 1f) return;
            _t = Mathf.Min(1f, _t + Time.unscaledDeltaTime / Duration);
            float e = 1f - (1f - _t) * (1f - _t);
            _g.alpha = e;
            float s = Mathf.Lerp(1.04f, 1f, e);
            transform.localScale = new Vector3(s, s, 1f);
        }
    }

    /// <summary>Keeps HUD elements inside the phone's safe area (notches, rounded corners).</summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class SafeArea : MonoBehaviour
    {
        private Rect _last;

        private void Update()
        {
            var sa = Screen.safeArea;
            if (sa == _last || Screen.width <= 0 || Screen.height <= 0) return;
            _last = sa;
            var rt = (RectTransform)transform;
            rt.anchorMin = new Vector2(sa.xMin / Screen.width, sa.yMin / Screen.height);
            rt.anchorMax = new Vector2(sa.xMax / Screen.width, sa.yMax / Screen.height);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }
    }

    /// <summary>Pops a RectTransform in with an overshoot (unscaled time, works while paused).</summary>
    public sealed class PopIn : MonoBehaviour
    {
        private float _t;
        public float Duration = 0.28f;
        public float Delay;

        private void OnEnable()
        {
            _t = -Delay;
            transform.localScale = Delay > 0f ? Vector3.zero : transform.localScale;
        }

        private void Update()
        {
            if (_t >= Duration) return;
            _t += Time.unscaledDeltaTime;
            if (_t < 0f) return;
            float x = Mathf.Clamp01(_t / Duration);
            float s = 1f + 0.2f * Mathf.Sin(x * Mathf.PI) * (1f - x) * 2f;
            s *= Mathf.Lerp(0.6f, 1f, Mathf.Sqrt(x));
            transform.localScale = new Vector3(s, s, 1f);
        }
    }

    /// <summary>Slides a RectTransform in from an offset when enabled.</summary>
    public sealed class SlideIn : MonoBehaviour
    {
        public Vector2 From = new Vector2(0f, -120f);
        public float Duration = 0.35f;
        public float Delay;
        private Vector2 _home;
        private bool _hasHome;
        private float _t;

        private void OnEnable()
        {
            var rt = (RectTransform)transform;
            if (!_hasHome) { _home = rt.anchoredPosition; _hasHome = true; }
            _t = -Delay;
            rt.anchoredPosition = _home + From;
        }

        private void Update()
        {
            if (_t >= Duration) return;
            _t += Time.unscaledDeltaTime;
            float x = Mathf.Clamp01(_t / Duration);
            float e = 1f - Mathf.Pow(1f - x, 3f);
            ((RectTransform)transform).anchoredPosition = Vector2.LerpUnclamped(_home + From, _home, e);
        }
    }

    /// <summary>Gentle up/down float.</summary>
    public sealed class Bob : MonoBehaviour
    {
        public float Amount = 8f;
        public float Speed = 2f;
        private float _phase;
        private Vector2 _home;
        private bool _hasHome;

        private void OnEnable()
        {
            var rt = (RectTransform)transform;
            if (!_hasHome) { _home = rt.anchoredPosition; _hasHome = true; }
            _phase = UnityEngine.Random.value * 6f;
        }

        private void Update()
        {
            _phase += Time.unscaledDeltaTime * Speed;
            ((RectTransform)transform).anchoredPosition = _home + new Vector2(0f, Mathf.Sin(_phase) * Amount);
        }
    }

    /// <summary>Breathing scale pulse (call-to-action buttons).</summary>
    public sealed class Pulse : MonoBehaviour
    {
        public float Amount = 0.04f;
        public float Speed = 3f;

        private void Update()
        {
            float s = 1f + Mathf.Sin(Time.unscaledTime * Speed) * Amount;
            transform.localScale = new Vector3(s, s, 1f);
        }
    }

    /// <summary>Constant rotation.</summary>
    public sealed class Spin : MonoBehaviour
    {
        public float Speed = 30f;
        private void Update() => transform.Rotate(0f, 0f, Speed * Time.unscaledDeltaTime);
    }

    /// <summary>A soft light bar sweeping across a button every few seconds (needs a RectMask2D parent).</summary>
    public sealed class Shine : MonoBehaviour
    {
        public float Every = 2.6f;
        private RectTransform _bar;
        private float _t;

        public static void Add(RectTransform target, ArtLibrary art)
        {
            if (target.GetComponent<RectMask2D>() == null) target.gameObject.AddComponent<RectMask2D>();
            var bar = UIKit.Image(target, "Shine", art.Glow, new Color(1f, 1f, 1f, 0.55f));
            bar.rectTransform.anchorMin = new Vector2(0f, -0.2f);
            bar.rectTransform.anchorMax = new Vector2(0f, 1.2f);
            bar.rectTransform.sizeDelta = new Vector2(90f, 0f);
            bar.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -18f);
            var s = target.gameObject.AddComponent<Shine>();
            s._bar = bar.rectTransform;
            s._t = UnityEngine.Random.value * s.Every;
        }

        private void Update()
        {
            if (_bar == null) return;
            _t += Time.unscaledDeltaTime;
            if (_t > Every) _t = 0f;
            float w = ((RectTransform)transform).rect.width;
            float x = Mathf.Lerp(-120f, w + 120f, Mathf.Clamp01(_t / 0.7f));
            _bar.anchoredPosition = new Vector2(x, 0f);
        }
    }

    /// <summary>Floating embers drifting up behind the menus.</summary>
    public sealed class Embers : MonoBehaviour
    {
        private RectTransform[] _p;
        private Image[] _img;
        private Vector2[] _vel;
        private float[] _life;

        public static void Add(RectTransform parent, ArtLibrary art, int count, Color color)
        {
            var e = parent.gameObject.AddComponent<Embers>();
            e._p = new RectTransform[count];
            e._img = new Image[count];
            e._vel = new Vector2[count];
            e._life = new float[count];
            for (int i = 0; i < count; i++)
            {
                var img = UIKit.Image(parent, "Ember", art.Glow, color);
                img.rectTransform.anchorMin = img.rectTransform.anchorMax = Vector2.zero;
                e._p[i] = img.rectTransform;
                e._img[i] = img;
                e.Respawn(i, true);
            }
        }

        private void Respawn(int i, bool anywhere)
        {
            var size = ((RectTransform)transform).rect.size;
            if (size.x < 10f) size = new Vector2(1920f, 1080f);
            float s = UnityEngine.Random.Range(10f, 34f);
            _p[i].sizeDelta = new Vector2(s, s);
            _p[i].anchoredPosition = new Vector2(UnityEngine.Random.Range(0f, size.x), anywhere ? UnityEngine.Random.Range(0f, size.y) : -20f);
            _vel[i] = new Vector2(UnityEngine.Random.Range(-15f, 15f), UnityEngine.Random.Range(25f, 70f));
            _life[i] = UnityEngine.Random.Range(5f, 12f);
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            for (int i = 0; i < _p.Length; i++)
            {
                _life[i] -= dt;
                if (_life[i] <= 0f) { Respawn(i, false); continue; }
                _p[i].anchoredPosition += _vel[i] * dt + new Vector2(Mathf.Sin(Time.unscaledTime + i) * 8f * dt, 0f);
                var c = _img[i].color;
                c.a = Mathf.Clamp01(_life[i] / 2f) * (0.35f + 0.25f * Mathf.Sin(Time.unscaledTime * 3f + i));
                _img[i].color = c;
            }
        }
    }
}
