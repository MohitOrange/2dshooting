using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace NeonBrawl
{
    /// <summary>Owns the canvas and switches between home, map select, locker, shop, loading, HUD, pause, settings and results.</summary>
    public sealed class UIRoot : MonoBehaviour
    {
        private GameContext _ctx;
        private HomeView _home;
        private PlayView _play;
        private LockerView _locker;
        private ShopView _shop;
        private LoadingView _loading;
        private HudView _hud;
        private PauseView _pause;
        private SettingsView _settings;
        private ResultsView _results;
        private OverlayView _overlay;
        private OnlineView _online;
        private ProfileView _profile;
        private PowersView _powers;
        private ControlsEditorView _controls;
        private bool _controlsFromPause;

        // menu pages visited before the current one, so BACK returns where you came from
        private readonly System.Collections.Generic.List<Component> _history = new System.Collections.Generic.List<Component>(8);
        private Component _current;

        public void Init(GameContext ctx)
        {
            _ctx = ctx;
            UIKit.Art = ctx.Art;
            UIKit.Cat = ctx.Catalog;
            UIKit.Sfx = ctx.Sfx;
            UIKit.Haptics = ctx.Haptics;

            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;
            gameObject.AddComponent<GraphicRaycaster>();
            gameObject.AddComponent<AdaptiveCanvas>().Scaler = scaler;

            if (FindAnyObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
                es.AddComponent<InputSystemUIInputModule>();
            }

            _hud = MakeScreen<HudView>("Hud", out var s0); _hud.Build(ctx, s0);
            _home = MakeScreen<HomeView>("Home", out var s1); _home.Build(ctx, s1);
            _play = MakeScreen<PlayView>("Play", out var s2); _play.Build(ctx, s2);
            _locker = MakeScreen<LockerView>("Locker", out var s3); _locker.Build(ctx, s3);
            _shop = MakeScreen<ShopView>("Shop", out var s4); _shop.Build(ctx, s4);
            _online = MakeScreen<OnlineView>("Online", out var s10); _online.Build(ctx, s10);
            _profile = MakeScreen<ProfileView>("Profile", out var s11); _profile.Build(ctx, s11);
            _powers = MakeScreen<PowersView>("Powers", out var s13); _powers.Build(ctx, s13);
            _loading = MakeScreen<LoadingView>("Loading", out var s5); _loading.Build(ctx, s5);
            _pause = MakeScreen<PauseView>("Pause", out var s6); _pause.Build(ctx, s6);
            _results = MakeScreen<ResultsView>("Results", out var s7); _results.Build(ctx, s7);
            _settings = MakeScreen<SettingsView>("Settings", out var s8); _settings.Build(ctx, s8);
            _controls = MakeScreen<ControlsEditorView>("ControlsEditor", out var s12); _controls.Build(ctx, s12);
            _overlay = MakeScreen<OverlayView>("Overlay", out var s9); _overlay.Build(ctx, s9);
            HideAll();
            _overlay.gameObject.SetActive(true);
        }

        private T MakeScreen<T>(string name, out RectTransform safe) where T : Component
        {
            var rt = UIKit.Rect(transform, name);
            UIKit.Stretch(rt);
            safe = UIKit.Rect(rt, "SafeArea");
            UIKit.Stretch(safe);
            safe.gameObject.AddComponent<SafeArea>();
            if (typeof(T) != typeof(HudView) && typeof(T) != typeof(OverlayView)) rt.gameObject.AddComponent<ScreenFade>();
            return rt.gameObject.AddComponent<T>();
        }

        private void HideAll()
        {
            _home.gameObject.SetActive(false);
            _play.gameObject.SetActive(false);
            _locker.gameObject.SetActive(false);
            _shop.gameObject.SetActive(false);
            _online.gameObject.SetActive(false);
            _profile.gameObject.SetActive(false);
            _powers.gameObject.SetActive(false);
            _loading.gameObject.SetActive(false);
            _hud.gameObject.SetActive(false);
            _pause.gameObject.SetActive(false);
            _results.gameObject.SetActive(false);
            _settings.gameObject.SetActive(false);
            _controls.gameObject.SetActive(false);
        }

        private bool IsPage(Component c) => c != null && (c == _home || c == _play || c == _locker || c == _shop || c == _online || c == _profile || c == _powers);

        private void Show(Component screen) => Show(screen, true);

        private void Show(Component screen, bool remember)
        {
            if (screen == _home || !IsPage(screen)) _history.Clear();
            else if (remember && screen != _current)
            {
                int seen = _history.IndexOf(screen);
                if (seen >= 0) _history.RemoveRange(seen, _history.Count - seen); // went forward to a page already behind us
                else if (IsPage(_current)) _history.Add(_current);
            }
            _current = screen;
            bool wasMenu = InMenus;
            HideAll();
            screen.gameObject.SetActive(true);
            if (wasMenu && screen != _hud) _ctx.Sfx.PlayUi(SfxId.UiWhoosh, 1f);
            _overlay.transform.SetAsLastSibling();
        }

        /// <summary>The previous menu page (the home camp when there is none).</summary>
        public void Back()
        {
            if (_history.Count == 0) { ShowHome(); return; }
            var prev = _history[_history.Count - 1];
            _history.RemoveAt(_history.Count - 1);
            Show(prev, false);
        }

        /// <summary>Phone back gesture / Esc in the menus: close what is on top, else go back a page.</summary>
        public void BackPressed()
        {
            if (FriendsVisible) { _overlay.Friends.Close(); return; }
            if (ControlsEditorVisible) { _controls.Cancel(); return; }
            if (SettingsVisible) { ShowSettings(false); return; }
            if (OnlineVisible) { _online.GoBack(); return; }
            if (!HomeVisible) Back();
        }

        public void ShowMenu() => ShowHome();
        public void ShowHome() => Show(_home);
        public void ShowPlay() => Show(_play);
        public void ShowLocker() => Show(_locker);
        public void ShowShop() => Show(_shop);
        public void ShowOnline() => Show(_online);
        public void ShowProfile() => Show(_profile);
        public void ShowPowers() => Show(_powers);
        public void RefreshMinimap() => _hud.RefreshMinimap();

        public void ShowLoading(string mapName, float seconds, Action done)
        {
            Show(_loading);
            _loading.Begin(mapName, seconds, done);
        }

        public void ShowHud(MatchController m)
        {
            Show(_hud);
            _hud.Bind(m);
            SetTouch(_ctx.Input.TouchActive);
        }

        public void SetTouch(bool touch) => _hud.SetTouch(touch);

        public void ShowPause(bool show)
        {
            if (show) _pause.SetOnline(_ctx.Match != null && _ctx.Match.Online, _ctx.Net != null && _ctx.Net.IsHost);
            _pause.gameObject.SetActive(show);
            if (show) _pause.transform.SetAsLastSibling();
            else _settings.gameObject.SetActive(false);
            _overlay.transform.SetAsLastSibling();
        }

        public void ShowSettings(bool show)
        {
            _settings.gameObject.SetActive(show);
            _settings.transform.SetAsLastSibling();
            if (show) _ctx.Sfx.PlayUi(SfxId.UiWhoosh, 0.8f);
            _overlay.transform.SetAsLastSibling();
        }

        /// <summary>Opens the touch layout editor over settings (and the pause menu, mid-match).</summary>
        public void ShowControlsEditor()
        {
            _controlsFromPause = _pause.gameObject.activeSelf;
            _settings.gameObject.SetActive(false);
            _pause.gameObject.SetActive(false);
            _hud.SetTouch(false); // the editor shows its own copy
            _controls.gameObject.SetActive(true);
            _controls.transform.SetAsLastSibling();
            _controls.Open();
            _ctx.Sfx.PlayUi(SfxId.UiWhoosh, 0.8f);
            _overlay.transform.SetAsLastSibling();
        }

        /// <summary>Back to the settings page the editor was opened from.</summary>
        public void CloseControlsEditor()
        {
            _controls.gameObject.SetActive(false);
            if (_controlsFromPause) _pause.gameObject.SetActive(true);
            _ctx.App.RefreshTouch();
            ShowSettings(true);
        }

        /// <summary>Moves the match sticks and buttons to the saved layout.</summary>
        public void ApplyControlLayout() => _hud.ApplyLayout();

        public void ShowResults(MatchController m, MatchReward reward, float xpBefore)
        {
            Show(_results);
            _results.Show(m, reward, xpBefore);
        }

        public void Toast(string text) => _overlay.Toast(text);

        public void ShowTestAd(Action<bool> done) => _overlay.ShowAd(done);

        public void AskAdConsent(Action<bool> answer) => _overlay.AskConsent(answer);

        /// <summary>Opens the friends popup on a tab (FriendsPopup.TabFriends / TabRequests / TabAdd).</summary>
        public void ShowFriends(int tab)
        {
            _overlay.Friends.Open(tab);
            _overlay.transform.SetAsLastSibling();
        }

        /// <summary>A friend invited us: the card slides down over whatever is showing.</summary>
        public void ShowInvite(FriendInvite invite)
        {
            _overlay.Invite.Show(invite);
            _overlay.transform.SetAsLastSibling();
        }

        public bool FriendsVisible => _overlay.Friends.IsOpen;

        public bool PauseVisible => _pause.gameObject.activeSelf;
        public bool SettingsVisible => _settings.gameObject.activeSelf;
        public bool ControlsEditorVisible => _controls.gameObject.activeSelf;
        public bool HomeVisible => _home.gameObject.activeSelf;
        public bool InMenus => _home.gameObject.activeSelf || _play.gameObject.activeSelf || _locker.gameObject.activeSelf || _shop.gameObject.activeSelf
                               || _online.gameObject.activeSelf || _profile.gameObject.activeSelf || _powers.gameObject.activeSelf;
        public bool OnlineVisible => _online.gameObject.activeSelf;
    }
}
