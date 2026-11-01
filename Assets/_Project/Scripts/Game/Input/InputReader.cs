using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace NeonBrawl
{
    public enum TouchAction { Dash, Reload, Swap, Grenade, Melee, Interact, Pause, AutoShoot }

    /// <summary>
    /// Turns keyboard/mouse, gamepad and on-screen touch controls into one <see cref="FighterInput"/>.
    /// Actions are built in code so no input asset needs editing. Perf: 0 B GC per frame.
    /// </summary>
    public sealed class InputReader : IDisposable
    {
        public const float TouchFireThreshold = 0.6f;

        private readonly InputAction _move, _aimStick, _fire, _dash, _reload, _swap, _grenade, _melee, _interact, _scoreboard, _pause, _toggleTouch, _autoShoot;
        private readonly bool[] _touchPulses = new bool[Enum.GetValues(typeof(TouchAction)).Length];

        public VirtualJoystick TouchMove;
        public VirtualJoystick TouchAim;
        public bool TouchScoreboard;

        /// <summary>Touch: FIRE held with the thumb resting in the middle — shoot the nearest enemy (set by the brain).</summary>
        public bool TouchAutoFire { get; private set; }

        /// <summary>True when the on-screen sticks and buttons are shown.</summary>
        public bool TouchActive { get; set; }
        public bool UsingMouse { get; private set; }
        /// <summary>World point the mouse is over (valid when <see cref="UsingMouse"/>).</summary>
        public Vector2 MouseWorld { get; private set; }

        public InputReader()
        {
            _move = new InputAction("Move", InputActionType.Value);
            _move.AddCompositeBinding("2DVector").With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s").With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            _move.AddCompositeBinding("2DVector").With("Up", "<Keyboard>/upArrow").With("Down", "<Keyboard>/downArrow").With("Left", "<Keyboard>/leftArrow").With("Right", "<Keyboard>/rightArrow");
            _move.AddBinding("<Gamepad>/leftStick");

            _aimStick = new InputAction("AimStick", InputActionType.Value, "<Gamepad>/rightStick");
            _fire = Button("Fire", "<Mouse>/leftButton", "<Gamepad>/rightTrigger");
            _dash = Button("Dash", "<Keyboard>/space", "<Gamepad>/leftTrigger");
            _reload = Button("Reload", "<Keyboard>/r", "<Gamepad>/buttonWest");
            _swap = Button("Swap", "<Keyboard>/q", "<Gamepad>/buttonNorth");
            _grenade = Button("Grenade", "<Keyboard>/g", "<Gamepad>/rightShoulder");
            _melee = Button("Melee", "<Keyboard>/f", "<Gamepad>/buttonEast");
            _interact = Button("Interact", "<Keyboard>/e", "<Gamepad>/buttonSouth");
            _scoreboard = Button("Scoreboard", "<Keyboard>/tab", "<Gamepad>/select");
            _pause = Button("Pause", "<Keyboard>/escape", "<Gamepad>/start");
            _toggleTouch = Button("ToggleTouch", "<Keyboard>/f2", null);
            _autoShoot = Button("AutoShoot", "<Keyboard>/x", "<Gamepad>/rightStickPress");
            _swap.AddBinding("<Mouse>/middleButton");
            _grenade.AddBinding("<Mouse>/rightButton");

            foreach (var a in All()) a.Enable();
        }

        private static InputAction Button(string name, string a, string b)
        {
            var act = new InputAction(name, InputActionType.Button, a);
            if (!string.IsNullOrEmpty(b)) act.AddBinding(b);
            return act;
        }

        private InputAction[] All() => new[] { _move, _aimStick, _fire, _dash, _reload, _swap, _grenade, _melee, _interact, _scoreboard, _pause, _toggleTouch, _autoShoot };

        /// <summary>A chat box has the keyboard: keys type text instead of moving or firing.</summary>
        public bool Typing;

        public bool ScoreboardHeld => (!Typing && _scoreboard.IsPressed()) || TouchScoreboard;
        public bool PausePressed => (!Typing && _pause.WasPressedThisFrame()) | Consume(TouchAction.Pause);
        public bool ToggleTouchPressed => _toggleTouch.WasPressedThisFrame();
        /// <summary>X key, right stick click or the AUTO button: switch auto shoot on or off.</summary>
        public bool AutoShootPressed => (!Typing && _autoShoot.WasPressedThisFrame()) | Consume(TouchAction.AutoShoot);

        public void PressTouch(TouchAction action) => _touchPulses[(int)action] = true;

        private bool Consume(TouchAction a)
        {
            bool v = _touchPulses[(int)a];
            _touchPulses[(int)a] = false;
            return v;
        }

        /// <summary>Fills the local fighter's input for this frame.</summary>
        public void Sample(Camera cam, Vector2 fighterPos, float defaultThrow, ref FighterInput input)
        {
            input.ClearPulses();

            Vector2 mv = Typing ? Vector2.zero : _move.ReadValue<Vector2>();
            if (TouchActive && TouchMove != null && TouchMove.Held) mv = TouchMove.Value;
            input.Move = Vector2.ClampMagnitude(mv, 1f);

            bool fire = false;
            bool hasAim = false;
            Vector2 aim = input.Aim;
            float throwDist = defaultThrow;

            TouchAutoFire = TouchActive && TouchAim != null && TouchAim.Held && TouchAim.Value.sqrMagnitude <= 0.04f;
            if (TouchActive && TouchAim != null && TouchAim.Held && TouchAim.Value.sqrMagnitude > 0.04f)
            {
                aim = TouchAim.Value.normalized;
                hasAim = true;
                fire = TouchAim.Value.magnitude >= TouchFireThreshold;
                UsingMouse = false;
            }
            else
            {
                Vector2 gp = _aimStick.ReadValue<Vector2>();
                if (gp.sqrMagnitude > 0.09f)
                {
                    aim = gp.normalized;
                    hasAim = true;
                    UsingMouse = false;
                }
                else if (!TouchActive && Mouse.current != null && cam != null)
                {
                    Vector2 mp = Mouse.current.position.ReadValue();
                    Vector3 w = cam.ScreenToWorldPoint(new Vector3(mp.x, mp.y, -cam.transform.position.z));
                    MouseWorld = w;
                    Vector2 d = (Vector2)w - fighterPos;
                    if (d.sqrMagnitude > 0.01f)
                    {
                        aim = d.normalized;
                        hasAim = true;
                        throwDist = d.magnitude;
                    }
                    UsingMouse = true;
                }
                fire = !Typing && _fire.IsPressed();
                if (fire && UsingMouse && EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) fire = false;
            }

            input.Aim = aim.sqrMagnitude > 0.0001f ? aim : Vector2.right;
            input.HasAim = hasAim;
            input.Fire = fire;
            input.ThrowDistance = throwDist;
            bool keys = !Typing;
            input.Dash = (keys && _dash.WasPressedThisFrame()) | Consume(TouchAction.Dash);
            input.Reload = (keys && _reload.WasPressedThisFrame()) | Consume(TouchAction.Reload);
            input.Swap = (keys && _swap.WasPressedThisFrame()) | Consume(TouchAction.Swap);
            input.Grenade = (keys && _grenade.WasPressedThisFrame()) | Consume(TouchAction.Grenade);
            input.Melee = (keys && _melee.WasPressedThisFrame()) | Consume(TouchAction.Melee);
            input.Interact = (keys && _interact.WasPressedThisFrame()) | Consume(TouchAction.Interact);
        }

        public void Dispose()
        {
            foreach (var a in All()) { a.Disable(); a.Dispose(); }
        }
    }
}
