using UnityEngine;

namespace NeonBrawl
{
    /// <summary>
    /// An emote face or a short speech bubble that pops up over a fighter's head for a few seconds.
    /// Follows the fighter and fades with its line-of-sight visibility. Created on first use.
    /// </summary>
    public sealed class ChatBubble : MonoBehaviour
    {
        private const float Life = 2.8f;
        private const float EmoteHeight = 1.45f;
        private const float TextHeight = 1.62f;

        /// <summary>Extra offset from the default spot above the head (e.g. to dodge menu art).</summary>
        public Vector2 Shift;

        private FighterVisual _visual;
        private Transform _root;
        private SpriteRenderer _bg, _tail, _emote;
        private TextMesh _text;
        private MeshRenderer _textR;
        private float _t = Life;
        private bool _isEmote;

        public static ChatBubble For(Fighter f, ArtLibrary art) => For(f.Visual, art);

        /// <summary>Also used for the soldiers in the menu camp, which have no Fighter.</summary>
        public static ChatBubble For(FighterVisual v, ArtLibrary art)
        {
            var b = v.GetComponent<ChatBubble>();
            if (b == null)
            {
                b = v.gameObject.AddComponent<ChatBubble>();
                b.Build(v, art);
            }
            return b;
        }

        private void Build(FighterVisual visual, ArtLibrary art)
        {
            _visual = visual;
            _root = new GameObject("Bubble").transform;
            _root.SetParent(transform, false);
            _root.localPosition = new Vector3(0f, EmoteHeight, 0f);

            _tail = Part("Tail", art.Square, SortOrder.WorldUi + 3);
            _tail.transform.localPosition = new Vector3(0f, -0.31f, 0f);
            _tail.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            _tail.transform.localScale = new Vector3(0.2f, 0.2f, 1f);
            _bg = Part("Bg", art.RoundRect, SortOrder.WorldUi + 3);
            _bg.drawMode = SpriteDrawMode.Sliced;
            _bg.sharedMaterial = art.Solid;
            _tail.sharedMaterial = art.Solid;
            _emote = Part("Emote", null, SortOrder.WorldUi + 5);
            // plain unlit sprite: the Solid material would paint the face as a flat white blob
            _emote.sharedMaterial = art.Unlit;

            var font = UIKit.Font;
            var go = new GameObject("Text");
            go.transform.SetParent(_root, false);
            go.transform.localPosition = new Vector3(0f, -0.01f, 0f);
            _text = go.AddComponent<TextMesh>();
            _text.font = font;
            _text.fontSize = 48;
            _text.characterSize = 0.05f;
            _text.anchor = TextAnchor.MiddleCenter;
            _text.alignment = TextAlignment.Center;
            _text.fontStyle = UIKit.Strong;
            _textR = go.GetComponent<MeshRenderer>();
            _textR.sharedMaterial = font.material;
            _textR.sortingOrder = SortOrder.WorldUi + 5;
            _root.gameObject.SetActive(false);
        }

        private SpriteRenderer Part(string n, Sprite s, int order)
        {
            var go = new GameObject(n);
            go.transform.SetParent(_root, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = s;
            sr.sortingOrder = order;
            return sr;
        }

        public void ShowEmote(int emote)
        {
            _isEmote = true;
            _emote.sprite = EmoteArt.Get(emote);
            _emote.enabled = true;
            _bg.enabled = _tail.enabled = false;
            _text.text = "";
            Restart();
        }

        public void ShowText(string text)
        {
            if (text.Length > 28) text = text.Substring(0, 26) + "…";
            _isEmote = false;
            _emote.enabled = false;
            _bg.enabled = _tail.enabled = true;
            _text.text = text;
            _root.gameObject.SetActive(true); // inactive renderers report empty bounds
            _root.localScale = Vector3.one;
            float w = Mathf.Max(0.7f, _textR.bounds.size.x + 0.36f);
            if (_textR.bounds.size.x <= 0.001f) w = 0.3f + text.Length * 0.15f;
            _bg.size = new Vector2(w, 0.62f);
            Restart();
        }

        private void Restart()
        {
            _t = 0f;
            _root.gameObject.SetActive(true);
            Apply();
        }

        private void LateUpdate()
        {
            if (_t >= Life) return;
            _t += Time.deltaTime;
            if (_t >= Life) { _root.gameObject.SetActive(false); return; }
            Apply();
        }

        private void Apply()
        {
            // pop in with a small overshoot, bob, then fade out
            float pop = _t < 0.12f ? Mathf.Lerp(0.2f, 1.18f, _t / 0.12f) : (_t < 0.24f ? Mathf.Lerp(1.18f, 1f, (_t - 0.12f) / 0.12f) : 1f);
            float s = (_isEmote ? 0.78f : 1f) * pop;
            _root.localScale = new Vector3(s, s, 1f);
            _root.localPosition = new Vector3(Shift.x, (_isEmote ? EmoteHeight + Mathf.Sin(_t * 7f) * 0.04f : TextHeight) + Shift.y, 0f);
            _root.localRotation = Quaternion.Euler(0f, 0f, _isEmote ? Mathf.Sin(_t * 9f) * 6f * Mathf.Max(0f, 1f - _t) : 0f);
            float a = Mathf.Clamp01((Life - _t) / 0.4f) * (_visual != null ? _visual.Alpha : 1f);
            _emote.color = new Color(1f, 1f, 1f, a);
            _bg.color = new Color(1f, 0.98f, 0.92f, 0.96f * a);
            _tail.color = _bg.color;
            _text.color = new Color(0.12f, 0.08f, 0.05f, a);
        }
    }
}
