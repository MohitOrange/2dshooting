using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace NeonBrawl
{
    /// <summary>
    /// The player's soldier drawn in the UI from the same layered sprites as in game (back item, body
    /// tint + detail, gun, head, hair/headgear), facing up, with idle breathing and a slow sway.
    /// Drag sideways to turn the soldier around; a tap makes them spin and cock the gun.
    /// </summary>
    public sealed class CharacterPreview : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        private float _turn, _spin, _hop, _dragDist;
        private bool _dragging;
        private ArtCatalog _cat;
        private ArtLibrary _art;
        private RectTransform _rig;
        private Image _back, _cloth, _detail, _gun, _head, _gear, _shadow;
        private float _unitsToPx;
        private float _phase;

        /// <summary>Builds the preview inside <paramref name="parent"/>; 1 world unit = <paramref name="unitPx"/> UI px.</summary>
        public static CharacterPreview Create(Transform parent, ArtLibrary art, ArtCatalog cat, float unitPx, bool pedestal)
        {
            var root = UIKit.Rect(parent, "CharacterPreview");
            var p = root.gameObject.AddComponent<CharacterPreview>();
            p._cat = cat;
            p._art = art;
            p._unitsToPx = unitPx;
            if (pedestal)
            {
                var ring = cat != null ? ArtCatalog.At(cat.Fx1, ArtIds.RingBlue) : null;
                var glow = UIKit.Image(root, "PedestalGlow", art.Glow, new Color(0.35f, 0.7f, 1f, 0.45f));
                UIKit.Place(glow.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(unitPx * 2.6f, unitPx * 1.3f));
                var r = UIKit.Image(root, "PedestalRing", ring != null ? ring : art.Ring, new Color(1f, 1f, 1f, 0.9f));
                UIKit.Place(r.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(unitPx * 2.1f, unitPx * 0.9f));
            }
            var hit = UIKit.Image(root, "TouchArea", art.Square, new Color(1f, 1f, 1f, 0f), false, true);
            UIKit.Place(hit.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(unitPx * 2.2f, unitPx * 2.2f));
            p._shadow = Layer(root, "Shadow", art.Circle, new Color(0f, 0f, 0f, 0.35f));
            p._rig = UIKit.Rect(root, "Rig");
            p._rig.localRotation = Quaternion.Euler(0f, 0f, 90f); // face up
            p._back = Layer(p._rig, "Back", null, Color.white);
            p._gun = Layer(p._rig, "Gun", null, Color.white);
            p._cloth = Layer(p._rig, "Cloth", null, Color.white);
            p._detail = Layer(p._rig, "Detail", null, Color.white);
            p._head = Layer(p._rig, "Head", null, Color.white);
            p._gear = Layer(p._rig, "Gear", null, Color.white);
            return p;
        }

        private static Image Layer(Transform parent, string n, Sprite s, Color c)
        {
            var img = UIKit.Image(parent, n, s, c);
            img.rectTransform.anchorMin = img.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            img.enabled = s != null; // an Image with no sprite draws a white box
            return img;
        }

        /// <summary>Places a sprite so its pivot sits at <paramref name="localUnits"/> (world units, rig space).</summary>
        private void Put(Image img, Sprite s, Color c, Vector2 localUnits, float scale = 1f, float rot = 0f, float squashY = 1f)
        {
            img.enabled = s != null;
            if (s == null) return;
            img.sprite = s;
            img.color = c;
            var rt = img.rectTransform;
            var size = s.rect.size / s.pixelsPerUnit * _unitsToPx * scale;
            rt.sizeDelta = new Vector2(size.x, size.y * squashY);
            rt.pivot = new Vector2(s.pivot.x / s.rect.width, s.pivot.y / s.rect.height);
            rt.anchoredPosition = localUnits * _unitsToPx;
            rt.localRotation = Quaternion.Euler(0f, 0f, rot);
        }

        public void Set(FighterLook look, WeaponId weapon = WeaponId.Rifle)
        {
            if (_cat == null || !_cat.HasCharacters)
            {
                Put(_cloth, _art.Circle, look.Outfit, Vector2.zero, 0.9f);
                Put(_head, _art.Circle, new Color(1f, 0.86f, 0.67f), Vector2.zero, 0.45f);
                _detail.enabled = _back.enabled = _gun.enabled = _gear.enabled = false;
                return;
            }
            int b = Mathf.Abs(look.Body) % _cat.BodyTint.Length;
            float gunBase = _cat.HandsForward - 0.12f;
            var back = ArtCatalog.At(_cat.Back, look.Back - 1);
            float bs = back != null ? 0.6f / Mathf.Max(0.01f, back.bounds.size.y) : 1f;
            Put(_back, back, Color.white, new Vector2(-0.3f, 0f), bs, -90f);
            Put(_cloth, _cat.BodyTint[b], look.Outfit, Vector2.zero);
            Put(_detail, ArtCatalog.At(_cat.BodyDetail, b), Color.white, Vector2.zero);
            Put(_head, _cat.Heads[0], look.Skin, Vector2.zero);
            Sprite gear = look.Head > 0 ? ArtCatalog.At(_cat.Heads, look.Head) : ArtCatalog.At(_cat.Hair, look.HairStyle - 1);
            Put(_gear, gear, look.Head > 0 ? Color.white : look.Skin, Vector2.zero);

            var gun = WeaponArt.Get(_cat, weapon);
            if (gun.Valid)
            {
                float len = 0.62f / Mathf.Max(0.2f, 1f - gun.Grip);
                float k = len / Mathf.Max(0.01f, gun.Sprite.bounds.size.x);
                Put(_gun, gun.Sprite, gun.Tint, new Vector2(gunBase + (0.5f - gun.Grip) * len, 0f), k, 0f, 1.05f);
            }
            else _gun.enabled = false;

            Put(_shadow, _art.Circle, new Color(0f, 0f, 0f, 0.35f), Vector2.zero, 1f);
            _shadow.rectTransform.sizeDelta = new Vector2(_unitsToPx * 1.1f, _unitsToPx * 0.55f);
            _shadow.rectTransform.anchoredPosition = new Vector2(0f, -_unitsToPx * 0.45f);
        }

        public void OnPointerDown(PointerEventData e)
        {
            _dragging = true;
            _dragDist = 0f;
        }

        public void OnDrag(PointerEventData e)
        {
            _turn -= e.delta.x * 0.55f;
            _dragDist += Mathf.Abs(e.delta.x) + Mathf.Abs(e.delta.y);
        }

        public void OnPointerUp(PointerEventData e)
        {
            _dragging = false;
            if (_dragDist > 12f) return;
            _spin = 360f;
            _hop = 1f;
            UIKit.Sfx?.PlayUi(SfxId.Reload, 0.7f);
            UIKit.Haptics?.Pulse(12);
        }

        private void Update()
        {
            if (_rig == null) return;
            float dt = Time.unscaledDeltaTime;
            _phase += dt;
            if (!_dragging) _turn = Mathf.LerpAngle(_turn, 0f, dt * 2.5f);
            _spin = Mathf.MoveTowards(_spin, 0f, dt * 900f);
            _hop = Mathf.MoveTowards(_hop, 0f, dt * 2.5f);
            float breathe = 1f + Mathf.Sin(_phase * 2.2f) * 0.018f + Mathf.Sin(_hop * Mathf.PI) * 0.12f;
            _rig.localScale = new Vector3(breathe, breathe, 1f);
            float sway = _dragging ? 0f : Mathf.Sin(_phase * 0.7f) * 9f;
            _rig.localRotation = Quaternion.Euler(0f, 0f, 90f + sway + _turn + _spin);
        }
    }
}
