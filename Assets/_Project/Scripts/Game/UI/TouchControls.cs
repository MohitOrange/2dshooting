using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace NeonBrawl
{
    /// <summary>
    /// Virtual stick. Floating mode: the stick appears where the thumb lands inside its zone.
    /// Multi-touch safe (tracks its own pointer id).
    /// </summary>
    public sealed class VirtualJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        public RectTransform Base;
        public RectTransform Knob;
        public Image Ring;
        public float Radius = 120f;
        public bool Floating;
        public float IdleAlpha = 0.55f;
        /// <summary>The player's opacity for this stick; it shows fully while held.</summary>
        public CanvasGroup Group;
        public float Opacity = 1f;

        private Vector2 _home;
        private int _pointer = int.MinValue;

        public Vector2 Value { get; private set; }
        public bool Held { get; private set; }

        public void SetHome(Vector2 anchoredPos)
        {
            _home = anchoredPos;
            Base.anchoredPosition = anchoredPos;
        }

        public void OnPointerDown(PointerEventData e)
        {
            if (Held) return;
            _pointer = e.pointerId;
            Held = true;
            if (Floating)
            {
                RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)transform, e.position, e.pressEventCamera, out var local);
                var zone = (RectTransform)transform;
                Base.anchoredPosition = local + zone.rect.size * zone.pivot;
            }
            Track(e);
        }

        public void OnDrag(PointerEventData e)
        {
            if (e.pointerId == _pointer) Track(e);
        }

        public void OnPointerUp(PointerEventData e)
        {
            if (e.pointerId != _pointer) return;
            Release();
        }

        public void Release()
        {
            Held = false;
            _pointer = int.MinValue;
            Value = Vector2.zero;
            Knob.anchoredPosition = Vector2.zero;
            Base.anchoredPosition = _home;
        }

        private void OnDisable()
        {
            if (Base != null) Release();
        }

        private void Track(PointerEventData e)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(Base, e.position, e.pressEventCamera, out var local);
            Value = Vector2.ClampMagnitude(local / Radius, 1f);
            Knob.anchoredPosition = Value * Radius;
        }

        private void Update()
        {
            if (Ring != null)
            {
                float a = Held ? 1f : IdleAlpha;
                var c = Ring.color;
                c.a = Mathf.MoveTowards(c.a, a, Time.unscaledDeltaTime * 4f);
                Ring.color = c;
            }
            if (Group != null)
            {
                float g = Held ? 1f : Opacity;
                if (Group.alpha != g) Group.alpha = Mathf.MoveTowards(Group.alpha, g, Time.unscaledDeltaTime * 5f);
            }
        }
    }

    /// <summary>On-screen button that sends one input pulse per press.</summary>
    public sealed class TouchButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        [System.NonSerialized] public InputReader Input;
        public TouchAction Action;
        public bool IsScoreboard;
        /// <summary>Colored glow behind the plate that flashes on every press.</summary>
        public Image Flash;
        private float _scale = 1f, _flash;

        public void OnPointerDown(PointerEventData e)
        {
            _scale = 0.85f;
            _flash = 1f;
            UIKit.Haptics?.Pulse(10);
            if (IsScoreboard) Input.TouchScoreboard = true;
            else Input.PressTouch(Action);
        }

        public void OnPointerUp(PointerEventData e)
        {
            if (IsScoreboard) Input.TouchScoreboard = false;
        }

        private void Update()
        {
            _scale = Mathf.MoveTowards(_scale, 1f, Time.unscaledDeltaTime * 2.5f);
            transform.localScale = new Vector3(_scale, _scale, 1f);
            if (Flash != null)
            {
                _flash = Mathf.MoveTowards(_flash, 0f, Time.unscaledDeltaTime * 3.5f);
                var c = Flash.color;
                c.a = _flash * 0.85f;
                Flash.color = c;
            }
        }
    }

    /// <summary>
    /// Builds the twin-stick layout and painted action buttons (reference resolution 1920×1080).
    /// Every stick and button lives in its own slot (<see cref="Slots"/>) that the player's
    /// <see cref="ControlLayout"/> moves, scales and fades. Preview mode builds a copy that takes no
    /// input, for the layout editor.
    /// </summary>
    public sealed class TouchControls : MonoBehaviour
    {
        public VirtualJoystick Move;
        public VirtualJoystick Aim;
        public GameObject PickupButton;
        public Text PickupLabel;
        public Image PickupIcon;
        public Image DashFill;
        public Text GrenadeCount;
        public Image SwapIcon;
        /// <summary>The movable piece of each control, indexed by <see cref="ControlId"/>.</summary>
        public readonly RectTransform[] Slots = new RectTransform[ControlLayout.Count];
        private readonly CanvasGroup[] _groups = new CanvasGroup[ControlLayout.Count];
        private Sprite _swapArrows;
        private Image _autoPlate, _autoIcon, _autoStateBg;
        private Text _autoState;

        public static TouchControls Build(RectTransform parent, InputReader input, ArtLibrary art, ArtCatalog cat, bool preview = false)
        {
            var root = UIKit.Rect(parent, "TouchControls");
            UIKit.Stretch(root);
            var tc = root.gameObject.AddComponent<TouchControls>();

            // the move stick's floating zone reaches up and right of the stick; the fire stick's sits around it
            tc.Move = tc.Stick(root, art, cat, ControlId.Move, new Vector2(820f, 760f), new Vector2(140f, 130f), false);
            tc.Aim = tc.Stick(root, art, cat, ControlId.Fire, new Vector2(460f, 460f), new Vector2(-30f, 10f), true);
            if (!preview)
            {
                input.TouchMove = tc.Move;
                input.TouchAim = tc.Aim;
            }

            tc.Btn(root, art, input, ControlId.Reload, "RELOAD", HudArt.Get(HudArt.Icon.Reload), TouchAction.Reload, HudArt.Accent.Blue);
            var swap = tc.Btn(root, art, input, ControlId.Swap, "SWAP", HudArt.Get(HudArt.Icon.Swap), TouchAction.Swap, HudArt.Accent.Cyan);
            tc.SwapIcon = swap.transform.Find("Icon").GetComponent<Image>();
            tc._swapArrows = tc.SwapIcon.sprite;
            var nadeBtn = tc.Btn(root, art, input, ControlId.Grenade, "NADE", HudArt.Get(HudArt.Icon.Grenade), TouchAction.Grenade, HudArt.Accent.Green);
            var countBg = UIKit.Image(nadeBtn.transform, "CountBg", art.Circle, new Color(0.04f, 0.06f, 0.1f, 0.98f));
            UIKit.Place(countBg.rectTransform, new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(-16f, -16f), new Vector2(48f, 48f));
            var countRing = UIKit.Image(countBg.transform, "Ring", art.Ring, HudArt.AccentColor(HudArt.Accent.Green));
            UIKit.Stretch(countRing.rectTransform);
            tc.GrenadeCount = UIKit.Label(countBg.transform, "Count", "1", 28, Color.white, TextAnchor.MiddleCenter);
            UIKit.Stretch(tc.GrenadeCount.rectTransform);
            var dash = tc.Btn(root, art, input, ControlId.Dash, "DASH", HudArt.Get(HudArt.Icon.Dash), TouchAction.Dash, HudArt.Accent.Orange);
            tc.DashFill = UIKit.Image(dash.transform, "Cooldown", art.Circle, new Color(0f, 0f, 0f, 0.6f));
            tc.DashFill.type = Image.Type.Filled;
            tc.DashFill.fillMethod = Image.FillMethod.Radial360;
            UIKit.Stretch(tc.DashFill.rectTransform, ControlLayout.BaseSize[(int)ControlId.Dash].x * 0.15f);
            tc.Btn(root, art, input, ControlId.Knife, "KNIFE", HudArt.Get(HudArt.Icon.Knife), TouchAction.Melee, HudArt.Accent.Red);
            // AUTO SHOOT switch: red plate and ON when your gun fires by itself, gray and OFF when it does not
            tc._autoPlate = tc.Btn(root, art, input, ControlId.Auto, "AUTO", HudArt.Get(HudArt.Icon.Target), TouchAction.AutoShoot, HudArt.Accent.Red);
            tc._autoIcon = tc._autoPlate.transform.Find("Icon").GetComponent<Image>();
            tc._autoStateBg = UIKit.Image(tc._autoPlate.transform, "StateBg", art.Pill != null ? art.Pill : art.RoundRect, new Color(0.2f, 0.7f, 0.3f, 0.95f), true);
            UIKit.Place(tc._autoStateBg.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, 2f), new Vector2(66f, 30f));
            tc._autoState = UIKit.Label(tc._autoStateBg.transform, "State", "ON", 20, Color.white, TextAnchor.MiddleCenter);
            UIKit.Stretch(tc._autoState.rectTransform);

            var pickSlot = tc.Slot(root, ControlId.Pickup);
            var pick = UIKit.Panel9(pickSlot, "PickUp", ArtIds.PanelGold, Color.white, 2.6f, true);
            UIKit.Stretch(pick.rectTransform);
            tc.PickupIcon = UIKit.Icon(pick.transform, "Gun", null, new Vector2(170f, 64f));
            UIKit.Place(tc.PickupIcon.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -14f), new Vector2(190f, 66f));
            tc.PickupLabel = UIKit.Label(pick.transform, "Label", "PICK UP", 24, new Color(0.3f, 0.15f, 0.04f), TextAnchor.LowerCenter, false);
            UIKit.Place(tc.PickupLabel.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 14f), new Vector2(250f, 60f));
            var ptb = pick.gameObject.AddComponent<TouchButton>();
            ptb.Input = input;
            ptb.Action = TouchAction.Interact;
            tc.PickupButton = pickSlot.gameObject;

            tc.ApplyLayout(ControlLayout.Defaults());
            if (preview) tc.MakePreview(cat);
            else tc.PickupButton.SetActive(false);
            return tc;
        }

        /// <summary>Moves, scales and fades every control to the player's layout.</summary>
        public void ApplyLayout(ControlLayout layout)
        {
            for (int i = 0; i < ControlLayout.Count; i++) Place(i, layout.Items[i]);
            Move.Floating = !layout.FixedMove;
        }

        public void Place(int i, in ControlPlace p)
        {
            var rt = Slots[i];
            rt.anchorMin = rt.anchorMax = p.Corner;
            rt.anchoredPosition = p.Offset;
            rt.localScale = new Vector3(p.Size, p.Size, 1f);
            _groups[i].alpha = p.Alpha;
            if (i == (int)ControlId.Move) Move.Opacity = p.Alpha;
            else if (i == (int)ControlId.Fire) Aim.Opacity = p.Alpha;
        }

        /// <summary>Layout editor copy: nothing reacts to touches, every control is shown.</summary>
        private void MakePreview(ArtCatalog cat)
        {
            foreach (var g in GetComponentsInChildren<Graphic>(true)) g.raycastTarget = false;
            foreach (var b in GetComponentsInChildren<TouchButton>(true)) b.enabled = false;
            Move.enabled = false;
            Aim.enabled = false;
            DashFill.fillAmount = 0f;
            SetPickup("RIFLE", WeaponArt.Get(cat, WeaponId.Rifle));
            PickupButton.SetActive(true);
        }

        /// <summary>Shows whether auto shoot is on.</summary>
        public void SetAutoShoot(bool on)
        {
            if (_autoPlate == null) return;
            _autoPlate.sprite = HudArt.Plate(on ? HudArt.Accent.Red : HudArt.Accent.Steel);
            _autoIcon.color = on ? Color.white : new Color(1f, 1f, 1f, 0.45f);
            _autoState.text = on ? "ON" : "OFF";
            _autoStateBg.color = on ? new Color(0.2f, 0.7f, 0.3f, 0.95f) : new Color(0.24f, 0.27f, 0.33f, 0.95f);
        }

        /// <summary>Shows the gun you would switch to, or the swap arrows when there is none.</summary>
        public void SetSwapIcon(WeaponArt.Info gun)
        {
            if (SwapIcon == null) return;
            SwapIcon.enabled = true;
            SwapIcon.sprite = gun.Valid ? gun.Sprite : _swapArrows;
            SwapIcon.color = gun.Valid ? gun.Tint : Color.white;
        }

        public void SetPickup(string weaponName, WeaponArt.Info gun)
        {
            PickupLabel.text = "PICK UP\n" + weaponName;
            PickupIcon.enabled = gun.Valid;
            if (gun.Valid)
            {
                PickupIcon.sprite = gun.Sprite;
                PickupIcon.color = gun.Tint;
            }
        }

        /// <summary>An empty, centered slot sized for the control; its CanvasGroup carries the opacity.</summary>
        private RectTransform Slot(RectTransform root, ControlId id)
        {
            int i = (int)id;
            var rt = UIKit.Rect(root, ControlLayout.Names[i]);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = ControlLayout.BaseSize[i];
            Slots[i] = rt;
            _groups[i] = rt.gameObject.AddComponent<CanvasGroup>();
            return rt;
        }

        /// <summary>
        /// A stick in its slot. The touch zone is bigger than the drawn stick and offset by
        /// <paramref name="zoneCenter"/>, so the stick's resting spot is always the slot's center.
        /// </summary>
        private VirtualJoystick Stick(RectTransform root, ArtLibrary art, ArtCatalog cat, ControlId id, Vector2 zoneSize, Vector2 zoneCenter, bool aim)
        {
            var slot = Slot(root, id);
            var zone = UIKit.Image(slot, "Zone", art.Square, new Color(1f, 1f, 1f, 0f), false, true);
            UIKit.Place(zone.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), zoneCenter, zoneSize);
            var js = zone.gameObject.AddComponent<VirtualJoystick>();
            js.Floating = !aim;
            js.Radius = 120f;
            js.Group = _groups[(int)id];

            var b = UIKit.Rect(zone.transform, "Base");
            b.anchorMin = b.anchorMax = Vector2.zero;
            b.pivot = new Vector2(0.5f, 0.5f);
            b.sizeDelta = new Vector2(290f, 290f);
            Sprite baseArt = cat != null ? cat.HudPart(aim ? ArtIds.HudAimRing : ArtIds.HudJoyBase) : null;
            Sprite knobArt = cat != null ? cat.HudPart(aim ? ArtIds.HudFire : ArtIds.HudJoyKnob) : null;
            Image ring;
            if (baseArt != null)
            {
                ring = UIKit.Icon(b, "Ring", baseArt, new Vector2(290f, 290f), new Color(1f, 1f, 1f, 0.55f));
                UIKit.Stretch(ring.rectTransform);
            }
            else
            {
                var bg = UIKit.Image(b, "Bg", art.Circle, new Color(0.05f, 0.07f, 0.12f, 0.35f));
                UIKit.Stretch(bg.rectTransform);
                ring = UIKit.Image(b, "Ring", art.Ring, new Color(1f, 1f, 1f, 0.3f));
                UIKit.Stretch(ring.rectTransform);
            }
            Image knob;
            if (knobArt != null) knob = UIKit.Icon(b, "Knob", knobArt, new Vector2(130f, 130f));
            else knob = UIKit.Image(b, "Knob", art.Circle, aim ? new Color(1f, 0.55f, 0.25f, 0.8f) : new Color(0.92f, 0.94f, 1f, 0.75f));
            UIKit.Place(knob.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(130f, 130f));
            if (aim)
            {
                var label = UIKit.Label(b, "Fire", "FIRE", 24, new Color(1f, 1f, 1f, 0.8f), TextAnchor.MiddleCenter);
                UIKit.Place(label.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(0f, 6f), new Vector2(200f, 30f));
            }

            js.Base = b;
            js.Knob = knob.rectTransform;
            js.Ring = ring;
            js.IdleAlpha = baseArt != null ? 0.55f : 0.3f;
            js.SetHome(zoneSize * 0.5f - zoneCenter); // home is measured from the zone's bottom-left
            return js;
        }

        /// <summary>
        /// A round action button in its slot: gunmetal plate with a colored rim (<see cref="HudArt"/>),
        /// a white icon, a small caption, and a colored glow that flashes when pressed.
        /// </summary>
        private Image Btn(RectTransform root, ArtLibrary art, InputReader input, ControlId id, string label, Sprite icon, TouchAction action, HudArt.Accent accent)
        {
            float size = ControlLayout.BaseSize[(int)id].x;
            Color ac = HudArt.AccentColor(accent);
            var holder = Slot(root, id);
            var flash = UIKit.Image(holder, "Flash", art.Glow, new Color(ac.r, ac.g, ac.b, 0f));
            UIKit.Place(flash.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size * 1.7f, size * 1.7f));
            var img = UIKit.Image(holder, label, HudArt.Plate(accent), new Color(1f, 1f, 1f, 0.94f), false, true);
            img.preserveAspect = true;
            UIKit.Stretch(img.rectTransform);
            var ic = UIKit.Icon(img.transform, "Icon", icon, new Vector2(size * 0.58f, size * 0.58f));
            UIKit.Place(ic.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, size * 0.08f), new Vector2(size * 0.58f, size * 0.58f));
            ic.enabled = icon != null;
            var t = UIKit.Label(img.transform, "Label", label, Mathf.RoundToInt(size * 0.135f), new Color(ac.r * 0.35f + 0.65f, ac.g * 0.35f + 0.65f, ac.b * 0.35f + 0.65f), TextAnchor.MiddleCenter);
            UIKit.Place(t.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), new Vector2(0f, size * 0.2f), new Vector2(size * 0.62f, size * 0.18f));
            var tb = img.gameObject.AddComponent<TouchButton>();
            tb.Input = input;
            tb.Action = action;
            tb.Flash = flash;
            return img;
        }

        /// <summary>Top-right buttons (pause, scoreboard) — added to the HUD in both touch and PC modes.</summary>
        public static void TopButtons(RectTransform parent, InputReader input, ArtLibrary art, ArtCatalog cat)
        {
            var pause = UIKit.Panel9(parent, "PauseBtn", ArtIds.PanelDark, new Color(1f, 1f, 1f, 0.9f), 2.4f, true);
            UIKit.Place(pause.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-20f, -16f), new Vector2(100f, 92f));
            var pi = UIKit.Icon(pause.transform, "Icon", cat != null ? cat.Icon(ArtIds.IconPause) : null, new Vector2(58f, 58f));
            UIKit.Place(pi.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(58f, 58f));
            if (cat == null) { var pl = UIKit.Label(pause.transform, "L", "II", 40, Color.white, TextAnchor.MiddleCenter); UIKit.Stretch(pl.rectTransform); pi.enabled = false; }
            var pb = pause.gameObject.AddComponent<TouchButton>();
            pb.Input = input;
            pb.Action = TouchAction.Pause;

            var board = UIKit.Panel9(parent, "BoardBtn", ArtIds.PanelDark, new Color(1f, 1f, 1f, 0.9f), 2.4f, true);
            UIKit.Place(board.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-132f, -16f), new Vector2(100f, 92f));
            var bi = UIKit.Icon(board.transform, "Icon", cat != null ? cat.Icon(ArtIds.IconTrophy) : null, new Vector2(60f, 60f));
            UIKit.Place(bi.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(60f, 60f));
            if (cat == null) { var bl = UIKit.Label(board.transform, "L", "#", 40, Color.white, TextAnchor.MiddleCenter); UIKit.Stretch(bl.rectTransform); bi.enabled = false; }
            var bb = board.gameObject.AddComponent<TouchButton>();
            bb.Input = input;
            bb.IsScoreboard = true;
        }
    }
}
