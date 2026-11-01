using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace NeonBrawl
{
    /// <summary>
    /// Touch layout editor: a copy of the match sticks and buttons over a grid. Tap a control to pick it,
    /// drag it anywhere on screen, and set its size and opacity on the toolbar (which jumps to the bottom
    /// when the picked control is near the top). The move stick can be floating or fixed.
    /// SAVE stores the layout and updates the match HUD; CANCEL or the back button drops the changes.
    /// </summary>
    public sealed class ControlsEditorView : MonoBehaviour
    {
        private const float Snap = 5f;

        private GameContext _ctx;
        private RectTransform _area, _bar, _frame, _tagRt;
        private CanvasGroup _barGroup;
        private TouchControls _preview;
        private Image _moveZone, _fireZone;
        private ControlLayout _work = ControlLayout.Defaults();
        private int _sel;
        private bool _dragging, _syncing;
        private Vector2 _grab;
        private Text _name, _hint, _tag, _sizeText, _alphaText;
        private Slider _size, _alpha;
        private UiSegment _moveMode;

        public void Build(GameContext ctx, RectTransform safe)
        {
            _ctx = ctx;
            var art = ctx.Art;
            var root = (RectTransform)transform;

            UIKit.GradientFill(root, "Dim", UIKit.Dim(0.02f, 0.04f, 0.09f, 0.88f), UIKit.Dim(0.03f, 0.02f, 0.07f, 0.92f), true).transform.SetAsFirstSibling();
            Grid(root, art);

            // outlines of the match HUD panels, so buttons can be kept clear of them
            var ghosts = UIKit.Rect(root, "HudGhosts");
            UIKit.Stretch(ghosts);
            ghosts.gameObject.AddComponent<SafeArea>();
            Ghost(ghosts, art, "HEALTH", new Vector2(0f, 1f), new Vector2(20f, -16f), new Vector2(520f, 120f));
            Ghost(ghosts, art, "TIMER", new Vector2(0.5f, 1f), new Vector2(0f, -12f), new Vector2(250f, 88f));
            Ghost(ghosts, art, "MENU", new Vector2(1f, 1f), new Vector2(-20f, -16f), new Vector2(324f, 92f));
            Ghost(ghosts, art, "AMMO", new Vector2(0.5f, 0f), new Vector2(0f, 14f), new Vector2(560f, 130f));

            _area = UIKit.Rect(root, "Area");
            UIKit.Stretch(_area);
            _preview = TouchControls.Build(_area, null, art, ctx.Catalog, true);
            _moveZone = _preview.Move.GetComponent<Image>();
            _fireZone = _preview.Aim.GetComponent<Image>();
            ZoneCaption(_moveZone);
            ZoneCaption(_fireZone);
            for (int i = 0; i < ControlLayout.Count; i++)
            {
                var slot = _preview.Slots[i];
                var hit = slot.gameObject.AddComponent<Image>(); // invisible grab area over the whole control
                hit.sprite = art.Square;
                hit.color = new Color(1f, 1f, 1f, 0f);
                var h = slot.gameObject.AddComponent<Handle>();
                h.View = this;
                h.Index = i;
            }
            BuildFrame(art);
            BuildBar(safe, art);
            safe.SetAsLastSibling(); // the toolbar stays above the controls
        }

        // ------------------------------------------------------------------ building

        private static void Grid(RectTransform root, ArtLibrary art)
        {
            var grid = UIKit.Rect(root, "Grid");
            UIKit.Stretch(grid);
            for (int i = 1; i < 16; i++)
            {
                var v = UIKit.Image(grid, "V", art.Square, new Color(1f, 1f, 1f, i == 8 ? 0.09f : 0.04f));
                v.rectTransform.anchorMin = new Vector2(i / 16f, 0f);
                v.rectTransform.anchorMax = new Vector2(i / 16f, 1f);
                v.rectTransform.sizeDelta = new Vector2(2f, 0f);
            }
            for (int i = 1; i < 9; i++)
            {
                var h = UIKit.Image(grid, "H", art.Square, new Color(1f, 1f, 1f, 0.04f));
                h.rectTransform.anchorMin = new Vector2(0f, i / 9f);
                h.rectTransform.anchorMax = new Vector2(1f, i / 9f);
                h.rectTransform.sizeDelta = new Vector2(0f, 2f);
            }
        }

        private static void ZoneCaption(Image zone)
        {
            var t = UIKit.Label(zone.transform, "Caption", "TOUCH AREA", 22, new Color(1f, 1f, 1f, 0.4f), TextAnchor.UpperCenter, false);
            UIKit.Place(t.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -12f), new Vector2(300f, 30f));
            t.gameObject.SetActive(false);
        }

        private static void Ghost(RectTransform parent, ArtLibrary art, string label, Vector2 corner, Vector2 pos, Vector2 size)
        {
            var g = UIKit.Image(parent, label + "Ghost", art.RoundRect, new Color(1f, 1f, 1f, 0.07f), true);
            UIKit.Place(g.rectTransform, corner, corner, pos, size);
            var t = UIKit.Label(g.transform, "Label", label, 26, new Color(1f, 1f, 1f, 0.28f), TextAnchor.MiddleCenter, false);
            UIKit.Stretch(t.rectTransform);
        }

        /// <summary>Gold corner brackets and a name tag around the picked control.</summary>
        private void BuildFrame(ArtLibrary art)
        {
            _frame = UIKit.Rect(_area, "Selection");
            var fill = UIKit.Image(_frame, "Fill", art.RoundRect, new Color(1f, 0.82f, 0.3f, 0.12f), true);
            UIKit.Stretch(fill.rectTransform);
            for (int c = 0; c < 4; c++)
            {
                var corner = new Vector2(c % 2, c / 2);
                Bracket(_frame, art, corner, new Vector2(40f, 7f));
                Bracket(_frame, art, corner, new Vector2(7f, 40f));
            }
            var tag = UIKit.Image(_frame, "Tag", art.Pill != null ? art.Pill : art.RoundRect, new Color(1f, 0.82f, 0.3f, 0.95f), true);
            _tagRt = tag.rectTransform;
            UIKit.Place(_tagRt, new Vector2(0.5f, 1f), new Vector2(0.5f, 0f), new Vector2(0f, 8f), new Vector2(200f, 40f));
            _tag = UIKit.Label(tag.transform, "Name", "", 22, UIKit.TextOn(ArtIds.PanelGold), TextAnchor.MiddleCenter, false);
            UIKit.Stretch(_tag.rectTransform);
            _frame.gameObject.AddComponent<Pulse>().Amount = 0.015f;
        }

        private static void Bracket(RectTransform frame, ArtLibrary art, Vector2 corner, Vector2 size)
        {
            var b = UIKit.Image(frame, "Corner", art.Square, UIKit.Gold);
            UIKit.Place(b.rectTransform, corner, corner, Vector2.zero, size);
        }

        private void BuildBar(RectTransform safe, ArtLibrary art)
        {
            var bar = UIKit.Panel9(safe, "Toolbar", ArtIds.PanelDark, Color.white, 2.6f, true);
            _bar = bar.rectTransform;
            _bar.sizeDelta = new Vector2(1280f, 200f);
            _barGroup = bar.gameObject.AddComponent<CanvasGroup>();
            DockBar(true);

            // left: what is picked
            _name = UIKit.Title(_bar, "Name", "", 44, Color.white, UIKit.Cream, TextAnchor.MiddleLeft);
            UIKit.Place(_name.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(34f, -18f), new Vector2(360f, 64f));
            _hint = UIKit.Label(_bar, "Hint", "Drag it anywhere.\nTap another control to pick it.", 22, new Color(0.65f, 0.72f, 0.85f), TextAnchor.UpperLeft, false);
            _hint.lineSpacing = 1.2f;
            UIKit.Place(_hint.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(34f, -96f), new Vector2(360f, 80f));
            _moveMode = new UiSegment(_bar, new[] { "FLOATING", "FIXED" }, 0, i => { _work.FixedMove = i == 1; ShowZones(); }, 166f);
            UIKit.Place(_moveMode.Root, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(30f, -96f), _moveMode.Root.sizeDelta);

            // middle: size and opacity
            _size = SliderRow(_bar, "SIZE", -58f, ControlLayout.MinSize, ControlLayout.MaxSize, SetSize, out _sizeText);
            _alpha = SliderRow(_bar, "OPACITY", -138f, ControlLayout.MinAlpha, ControlLayout.MaxAlpha, SetAlpha, out _alphaText);

            // right: save / reset / cancel
            var cat = _ctx.Catalog;
            var save = UIKit.ArtButton(_bar, "Save", "SAVE", ArtIds.PanelGold, new Vector2(300f, 92f), 40, Save, cat != null ? cat.Icon(ArtIds.IconPlay) : null);
            UIKit.Place((RectTransform)save.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-28f, -18f), new Vector2(300f, 92f));
            var reset = UIKit.ArtButton(_bar, "Reset", "RESET", ArtIds.PanelBlue, new Vector2(146f, 72f), 28, ResetAll);
            UIKit.Place((RectTransform)reset.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-182f, 18f), new Vector2(146f, 72f));
            var cancel = UIKit.ArtButton(_bar, "Cancel", "CANCEL", ArtIds.PanelSlate, new Vector2(146f, 72f), 28, Cancel);
            UIKit.Place((RectTransform)cancel.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-28f, 18f), new Vector2(146f, 72f));
        }

        private static Slider SliderRow(RectTransform bar, string title, float y, float min, float max, System.Action<float> changed, out Text readout)
        {
            var l = UIKit.Label(bar, title, title, 26, new Color(0.78f, 0.84f, 0.95f), TextAnchor.MiddleLeft);
            UIKit.Place(l.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(420f, y), new Vector2(140f, 50f));
            var s = UiSlider.Create(bar, 1f, changed, 250f);
            var rt = (RectTransform)s.transform.parent;
            UIKit.Place(rt, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(560f, y), rt.sizeDelta);
            s.minValue = min;
            s.maxValue = max;
            readout = rt.Find("Percent").GetComponent<Text>();
            return s;
        }

        // ------------------------------------------------------------------ open / close

        /// <summary>Starts editing a copy of the saved layout.</summary>
        public void Open()
        {
            _work = _ctx.Prefs.Layout.Clone();
            _preview.ApplyLayout(_work);
            _preview.SetAutoShoot(_ctx.Prefs.AutoShoot);
            _moveMode.Show(_work.FixedMove ? 1 : 0);
            _dragging = false;
            _barGroup.alpha = 1f;
            Select((int)ControlId.Move, false);
        }

        private void Save()
        {
            _ctx.Prefs.Layout = _work.Clone();
            _ctx.Prefs.Save();
            _ctx.UI.ApplyControlLayout();
            _ctx.UI.CloseControlsEditor();
            _ctx.UI.Toast("Controls saved");
        }

        /// <summary>Leaves without saving (CANCEL, back button, Esc).</summary>
        public void Cancel() => _ctx.UI.CloseControlsEditor();

        private void ResetAll()
        {
            _work = ControlLayout.Defaults();
            _preview.ApplyLayout(_work);
            _moveMode.Show(0);
            Select(_sel, false);
            _ctx.UI.Toast("Back to the default layout");
        }

        // ------------------------------------------------------------------ picking and dragging

        private Vector2 AreaSize => _area.rect.size;

        /// <summary>Pointer position measured from the bottom-left of the screen (canvas units).</summary>
        private Vector2 Local(PointerEventData e)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_area, e.position, e.pressEventCamera, out var p);
            return p - _area.rect.min;
        }

        private void Select(int i, bool sound)
        {
            _sel = i;
            var p = _work.Items[i];
            _syncing = true;
            _size.SetValueWithoutNotify(p.Size);
            _alpha.SetValueWithoutNotify(p.Alpha);
            _syncing = false;
            ShowValues(p);
            _name.text = ControlLayout.Names[i];
            _tag.text = ControlLayout.Names[i];
            _tagRt.sizeDelta = new Vector2(_tag.preferredWidth + 36f, 40f);
            bool move = i == (int)ControlId.Move;
            _moveMode.Root.gameObject.SetActive(move);
            _hint.gameObject.SetActive(!move);
            _preview.Slots[i].SetAsLastSibling();
            _frame.SetAsLastSibling();
            ShowZones();
            DockBar(ControlLayout.Center(p, AreaSize).y < AreaSize.y * 0.55f);
            if (sound)
            {
                _ctx.Sfx.PlayUi(SfxId.UiTap, 0.7f);
                UIKit.Haptics?.Pulse(8);
            }
        }

        /// <summary>Shows where the picked stick takes touches (the move stick appears wherever the thumb lands in it).</summary>
        private void ShowZones()
        {
            ShowZone(_moveZone, _sel == (int)ControlId.Move);
            ShowZone(_fireZone, _sel == (int)ControlId.Fire);
        }

        private static void ShowZone(Image zone, bool on)
        {
            zone.color = new Color(1f, 1f, 1f, on ? 0.04f : 0f);
            zone.transform.Find("Caption").gameObject.SetActive(on);
        }

        private void ShowValues(in ControlPlace p)
        {
            _sizeText.text = Mathf.RoundToInt(p.Size * 100f) + "%";
            _alphaText.text = Mathf.RoundToInt(p.Alpha * 100f) + "%";
        }

        private void Grab(int i, PointerEventData e)
        {
            if (i != _sel) Select(i, true);
            _grab = ControlLayout.Center(_work.Items[i], AreaSize) - Local(e);
            _dragging = true;
        }

        private void Drag(int i, PointerEventData e)
        {
            if (i != _sel) return;
            var p = _work.Items[i];
            Vector2 c = Local(e) + _grab;
            c.x = Mathf.Round(c.x / Snap) * Snap;
            c.y = Mathf.Round(c.y / Snap) * Snap;
            Put(i, c, p.Size, p.Alpha);
        }

        private void Drop()
        {
            _dragging = false;
            DockBar(ControlLayout.Center(_work.Items[_sel], AreaSize).y < AreaSize.y * 0.55f);
        }

        /// <summary>Stores a control at a center point, kept fully on screen.</summary>
        private void Put(int i, Vector2 center, float size, float alpha)
        {
            var area = AreaSize;
            Vector2 half = ControlLayout.BaseSize[i] * (size * 0.5f);
            float top = half.y + (i == (int)ControlId.Auto ? 18f * size : 0f); // the ON/OFF tag sits on top of AUTO
            center.x = Mathf.Clamp(center.x, half.x, Mathf.Max(half.x, area.x - half.x));
            center.y = Mathf.Clamp(center.y, half.y, Mathf.Max(half.y, area.y - top));
            _work.Items[i] = ControlLayout.FromCenter(center, area, size, alpha);
            _preview.Place(i, _work.Items[i]);
        }

        private void SetSize(float v)
        {
            if (_syncing) return;
            var p = _work.Items[_sel];
            Put(_sel, ControlLayout.Center(p, AreaSize), v, p.Alpha);
            ShowValues(_work.Items[_sel]);
        }

        private void SetAlpha(float v)
        {
            if (_syncing) return;
            var p = _work.Items[_sel];
            Put(_sel, ControlLayout.Center(p, AreaSize), p.Size, v);
            ShowValues(_work.Items[_sel]);
        }

        /// <summary>Toolbar at the top, or at the bottom while the picked control is up there.</summary>
        private void DockBar(bool top)
        {
            float y = top ? 1f : 0f;
            UIKit.Place(_bar, new Vector2(0.5f, y), new Vector2(0.5f, y), new Vector2(0f, top ? -16f : 16f), _bar.sizeDelta);
        }

        private void LateUpdate()
        {
            // the frame hugs the picked control
            var slot = _preview.Slots[_sel];
            float s = slot.localScale.x;
            _frame.anchorMin = slot.anchorMin;
            _frame.anchorMax = slot.anchorMax;
            _frame.pivot = slot.pivot;
            _frame.anchoredPosition = slot.anchoredPosition;
            _frame.sizeDelta = slot.sizeDelta * s + new Vector2(28f, 28f);
            // the name tag goes under the control near the top edge, and never off the sides
            Vector2 area = AreaSize;
            Vector2 c = ControlLayout.Center(_work.Items[_sel], area);
            float tw = _tagRt.sizeDelta.x;
            bool below = c.y + _frame.sizeDelta.y * 0.5f + 56f > area.y;
            _tagRt.anchorMin = _tagRt.anchorMax = new Vector2(0.5f, below ? 0f : 1f);
            _tagRt.pivot = new Vector2(0.5f, below ? 1f : 0f);
            _tagRt.anchoredPosition = new Vector2(Mathf.Clamp(c.x, tw * 0.5f + 8f, area.x - tw * 0.5f - 8f) - c.x, below ? -8f : 8f);
            // the toolbar fades while you drag so you can see under it
            float a = _dragging ? 0.3f : 1f;
            if (_barGroup.alpha != a) _barGroup.alpha = Mathf.MoveTowards(_barGroup.alpha, a, Time.unscaledDeltaTime * 6f);
        }

        /// <summary>Invisible grab area on each control in the editor.</summary>
        private sealed class Handle : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IInitializePotentialDragHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
        {
            public ControlsEditorView View;
            public int Index;

            public void OnPointerDown(PointerEventData e) => View.Grab(Index, e);
            public void OnPointerUp(PointerEventData e) => View.Drop();
            public void OnInitializePotentialDrag(PointerEventData e) => e.useDragThreshold = false;
            public void OnBeginDrag(PointerEventData e) { }
            public void OnDrag(PointerEventData e) => View.Drag(Index, e);
            public void OnEndDrag(PointerEventData e) => View.Drop();
        }
    }
}
