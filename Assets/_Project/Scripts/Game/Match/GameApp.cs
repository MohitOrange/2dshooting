using NeonBrawl.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

namespace NeonBrawl
{
    /// <summary>
    /// Entry point (the only object in the Game scene besides camera and light). Builds every
    /// service once, then switches between the menus (with a live bot match on your selected map
    /// behind them), the loading screen, matches, pause and results (which pay coins and XP).
    /// </summary>
    public sealed class GameApp : MonoBehaviour
    {
        [SerializeField] private Camera _camera;
        [SerializeField] private Light2D _globalLight;

        private GameContext _ctx;
        private MatchController _match;
        private MatchSettings _lastSettings;
        private bool _lastAutoPlay;
        private int _lastMap;
        private int _demoMap = -1;
        private bool _inMenu;
        private LobbyScene _lobby;
        private string _heroKey;
        private MissionTracker _missions;
        private AdMobAds _admob;
        private IapStore _iap;
        private readonly AdPolicy _adPolicy = new AdPolicy();

        public GameContext Context => _ctx;

        private void Awake()
        {
            Application.targetFrameRate = 60;
            if (Application.isMobilePlatform) QualitySettings.vSyncCount = 0;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            Physics2D.gravity = Vector2.zero;
            Physics2D.queriesHitTriggers = false;
            Physics2D.queriesStartInColliders = true;
            Physics2D.IgnoreLayerCollision(Layers.Corpses, Layers.Fighters, true);
            Physics2D.IgnoreLayerCollision(Layers.Corpses, Layers.Corpses, true);

            if (_camera == null) _camera = Camera.main;
            if (_globalLight == null)
            {
                var lights = FindObjectsByType<Light2D>();
                for (int i = 0; i < lights.Length; i++)
                    if (lights[i].lightType == Light2D.LightType.Global) { _globalLight = lights[i]; break; }
            }

            var prefs = UserPrefs.Load();
            // real ads on phones (and in the editor while the build target is Android); test ads elsewhere
            MockAds mock = null;
            IAdsService ads;
            if (AdMobAds.Supported) ads = _admob = gameObject.AddComponent<AdMobAds>();
            else ads = mock = new MockAds();
            _ctx = new GameContext
            {
                App = this,
                Config = GameConfig.Load(),
                Art = ArtLibrary.Create(),
                Catalog = ArtCatalog.Load(),
                Prefs = prefs,
                Profile = PlayerProfile.Load(),
                Ads = ads,
                Input = new InputReader(),
                Camera = _camera,
                GlobalLight = _globalLight,
                Haptics = new Haptics(prefs),
            };
            _ctx.Store = _iap = new IapStore(_ctx);
            if (!_camera.TryGetComponent(out CameraRig rig)) rig = _camera.gameObject.AddComponent<CameraRig>();
            _ctx.CameraRig = rig;
            _ctx.CameraRig.Init(_ctx, _camera);

            var fx = new GameObject("Fx");
            fx.transform.SetParent(transform, false);
            _ctx.Fx = fx.AddComponent<FxSystem>();
            _ctx.Fx.Init(_ctx);
            _ctx.Projectiles = fx.AddComponent<ProjectileSystem>();
            _ctx.Projectiles.Init(_ctx);

            var audio = new GameObject("Sfx");
            audio.transform.SetParent(transform, false);
            _ctx.Sfx = audio.AddComponent<SfxSystem>();
            _ctx.Sfx.Init(_ctx);

            var online = new GameObject("Online");
            online.transform.SetParent(transform, false);
            _ctx.Net = online.AddComponent<NetSession>();
            _ctx.Net.Init(_ctx);
            _ctx.Net.Lost += OnNetLost;
            _ctx.Chat = online.AddComponent<ChatHub>();
            _ctx.Chat.Init(_ctx);
            _ctx.Voice = online.AddComponent<VoiceService>();
            _ctx.Voice.Init(_ctx);

            var ui = new GameObject("UI");
            ui.transform.SetParent(transform, false);
            var uiRoot = ui.AddComponent<UIRoot>();
            uiRoot.Init(_ctx);
            _ctx.UI = uiRoot;
            if (mock != null) mock.Present = (placement, done) => _ctx.UI.ShowTestAd(done);
            _missions = new MissionTracker(_ctx);
            RefreshTouch();

            _lastSettings = prefs.LastSettings;
            _lastMap = _ctx.Profile.Data.LastMap;
            StartMenu();
            _iap.Init();
            StartAds();
        }

        /// <summary>
        /// Starts AdMob. Google's own consent form appears first where the law asks for it (EU/UK…);
        /// elsewhere ads are personalized unless the player turned PERSONAL ADS off in Settings.
        /// </summary>
        private void StartAds()
        {
            if (_admob == null) return;
            _admob.Init(_ctx.Prefs.AdConsent != 0);
        }

        /// <summary>Settings changed the personalized-ads answer.</summary>
        public void SetAdConsent(bool yes)
        {
            if (_admob != null) _admob.SetConsent(yes);
        }

        /// <summary>Whether Settings should offer Google's privacy choices (EU/UK players).</summary>
        public bool AdPrivacyOptionsAvailable => _admob != null && _admob.PrivacyOptionsRequired;

        /// <summary>Re-opens Google's consent choices.</summary>
        public void ShowAdPrivacyOptions()
        {
            if (_admob == null) return;
            _admob.ShowPrivacyOptions(err => { if (err != null) _ctx.UI.Toast("Could not open ad choices. Try again later."); });
        }

        /// <summary>
        /// Leaving the results of an offline match is the only place a full-screen ad may appear
        /// (rules in <see cref="AdPolicy"/>). Online play is never interrupted.
        /// </summary>
        private void AfterBreakAd(System.Action next)
        {
            bool fromResults = _match != null && !_match.Online && !_match.Spectator && _match.CurrentPhase == MatchController.Phase.Ended;
            if (fromResults && _adPolicy.InterstitialDue(_ctx.Profile))
            {
                _adPolicy.Shown();
                _ctx.Ads.ShowInterstitial("match_break", next);
            }
            else next();
        }

        private void Start()
        {
            // dev testing of online play from the command line (two copies on one PC):
            //   -nbname NAME  -nbhost | -nbquick | -nbjoin CODE | -nblanhost | -nblanjoin IP   -nbbot  -nbshots SECONDS
            string name = CommandLine.Value("-nbname");
            if (!string.IsNullOrEmpty(name)) { _ctx.Profile.Data.Name = NetIO.CleanName(name); }
            string join = CommandLine.Value("-nbjoin");
            string lan = CommandLine.Value("-nblanjoin");
            if (CommandLine.Has("-nbhost")) { OpenOnline(); _ctx.Net.CreateRoom(); }
            else if (CommandLine.Has("-nbquick")) { OpenOnline(); _ctx.Net.QuickMatch(); }
            else if (!string.IsNullOrEmpty(join)) { OpenOnline(); _ctx.Net.JoinRoom(join); }
            else if (CommandLine.Has("-nblanhost")) { OpenOnline(); _ctx.Net.HostDirect(); }
            else if (!string.IsNullOrEmpty(lan)) { OpenOnline(); _ctx.Net.JoinDirect(lan); }
            if (CommandLine.Has("-nbtlsprobe")) TlsProbe();
            string shots = CommandLine.Value("-nbshots");
            if (!string.IsNullOrEmpty(shots) && float.TryParse(shots, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float every))
                StartCoroutine(DevShots(Mathf.Max(1f, every)));
        }

        /// <summary>Dev: one managed TLS handshake (what the lobby socket does), with the full error if it fails.</summary>
        private static async void TlsProbe()
        {
            try
            {
                using (var tcp = new System.Net.Sockets.TcpClient())
                {
                    await tcp.ConnectAsync("wire.unity3d.com", 443);
                    using (var ssl = new System.Net.Security.SslStream(tcp.GetStream(), false))
                    {
                        await ssl.AuthenticateAsClientAsync("wire.unity3d.com", null, System.Security.Authentication.SslProtocols.Tls12, false);
                        Debug.Log("[TLS] ok " + ssl.SslProtocol);
                    }
                }
            }
            catch (System.Exception e) { Debug.Log("[TLS] fail " + e); }
        }

        private System.Collections.IEnumerator DevShots(float every)
        {
            string dir = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Application.dataPath) ?? ".", "shots");
            System.IO.Directory.CreateDirectory(dir);
            int n = 0;
            while (true)
            {
                yield return new WaitForSecondsRealtime(every);
                ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "shot_" + (n++).ToString("000") + ".png"));
                string code = _ctx.Net.Code;
                System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "status.txt"),
                    "state=" + _ctx.Net.State + " host=" + _ctx.Net.IsHost + " code=" + code + " members=" + _ctx.Net.Members.Count +
                    " match=" + (_match != null ? _match.CurrentPhase.ToString() : "none") + " status=" + _ctx.Net.StatusText);
            }
        }

        private void OnDestroy()
        {
            _ctx?.Input?.Dispose();
            TimeControl.ResetAll();
        }

        private void Update()
        {
            TimeControl.Tick();
            var input = _ctx.Input;
            if (input.ToggleTouchPressed)
            {
                _ctx.Prefs.ForceTouchControls = !_ctx.Prefs.ForceTouchControls;
                RefreshTouch();
            }
            if (input.PausePressed)
            {
                if (_inMenu) _ctx.UI.BackPressed();
                else if (_ctx.UI.ControlsEditorVisible || _ctx.UI.SettingsVisible) _ctx.UI.BackPressed();
                else if (_match != null && _match.CurrentPhase != MatchController.Phase.Ended) SetPaused(!_ctx.UI.PauseVisible);
            }
        }

        private void OnApplicationPause(bool paused)
        {
            // Phone backgrounded mid-match: pause so nobody dies while away (online the match goes on).
            if (paused && !_inMenu && _match != null && _match.CurrentPhase == MatchController.Phase.Playing) SetPaused(true);
        }

        private bool OnlineMatch => _match != null && _match.Online;

        public void RefreshTouch()
        {
            bool touch = _ctx.Prefs.ForceTouchControls || Application.isMobilePlatform || (Touchscreen.current != null && Mouse.current == null);
            _ctx.Input.TouchActive = touch;
            if (_ctx.UI != null) _ctx.UI.SetTouch(touch && !_inMenu);
        }

        public void StartMenu()
        {
            SetPaused(false);
            _inMenu = true;
            _ctx.Sfx.Duck = 0.25f;
            _ctx.Sfx.PlayMusic(false);
            EnterLobby();
            _ctx.UI.ShowHome();
        }

        /// <summary>Online screen: room list / your room, with the campfire (or the room's map) behind it.</summary>
        public void OpenOnline()
        {
            SetPaused(false);
            bool wasMenu = _inMenu;
            _inMenu = true;
            _ctx.Sfx.Duck = 0.25f;
            if (!wasMenu) _ctx.Sfx.PlayMusic(false);
            EnterLobby();
            _ctx.UI.ShowOnline();
            RefreshTouch();
        }

        /// <summary>Online: the host started a match (or we joined one that is running).</summary>
        public void StartOnlineMatch(MatchSettings settings, int mapIndex, System.Collections.Generic.List<FighterSpec> roster, byte[] joinState)
        {
            _inMenu = false;
            _demoMap = -1;
            SetPaused(false);
            _ctx.UI.ShowPause(false);
            if (_lobby != null) _lobby.Hide();
            _ctx.Sfx.PlayMusic(true);
            var def = MapLayouts.Get(mapIndex);
            _ctx.UI.ShowLoading(def.Name, joinState != null ? 0.6f : 1.3f, () =>
            {
                if (!_ctx.Net.InRoom) { OpenOnline(); return; } // left while loading
                if (_match != null)
                {
                    _match.Ended -= OnMatchEnded;
                    DestroyImmediate(_match.gameObject);
                    _match = null;
                }
                TimeControl.ResetAll();
                var go = new GameObject("OnlineMatch");
                _match = go.AddComponent<MatchController>();
                _match.Ended += OnMatchEnded;
                _match.BeginOnline(_ctx, settings, mapIndex, roster, _ctx.Net, joinState);
                _ctx.Sfx.Duck = 1f;
                _ctx.UI.ShowHud(_match);
                RefreshTouch();
                _ctx.Voice.EnterMatch(_ctx.Net.Code, _match.Local != null ? _match.Local.Team : 0, _match.Rules.IsTeamMode);
            });
        }

        private void OnNetLost(string reason)
        {
            _ctx.UI.Toast(reason);
            _ctx.Sfx.PlayUi(SfxId.UiError, 0.8f);
            if (OnlineMatch || !_inMenu) OpenOnline();
        }

        /// <summary>Menus: show the campfire lobby (removes any map preview) and refresh your soldier if the look changed.</summary>
        public void EnterLobby()
        {
            if (!_inMenu) return;
            if (_match != null)
            {
                _match.Ended -= OnMatchEnded;
                DestroyImmediate(_match.gameObject);
                _match = null;
            }
            _demoMap = -1;
            if (_lobby == null) _lobby = LobbyScene.Create(_ctx);
            _lobby.Show();
            var p = _ctx.Profile;
            string key = p.Data.Body + "/" + p.Data.Head + "/" + p.Data.Hair + "/" + p.Data.Back + "/" + p.Data.Outfit + "/" + p.Data.Skin;
            if (key != _heroKey)
            {
                _heroKey = key;
                _lobby.SetHero(p.Look(p.OutfitColor));
            }
        }

        /// <summary>Menus: run the background bot match on this map (the map you picked shows behind the UI).</summary>
        public void PreviewMap(int mapIndex)
        {
            if (!_inMenu || mapIndex == _demoMap) return;
            _demoMap = mapIndex;
            if (_lobby != null) _lobby.Hide();
            var demo = new MatchSettings
            {
                Mode = MatchMode.FreeForAll, BotCount = 7, Difficulty = 1, TimeLimit = 0f, KillLimit = 0, Night = false, Fog = false
            };
            CreateMatch(demo, true, false, mapIndex);
        }

        public void StartMatch(MatchSettings settings, bool autoPlay, int mapIndex)
        {
            _lastSettings = settings;
            _lastAutoPlay = autoPlay;
            _lastMap = mapIndex;
            _ctx.Prefs.LastSettings = settings;
            _ctx.Prefs.Save();
            _inMenu = false;
            _demoMap = -1;
            SetPaused(false);
            if (_lobby != null) _lobby.Hide();
            _ctx.Sfx.PlayMusic(true);
            var def = MapLayouts.Get(mapIndex);
            _ctx.UI.ShowLoading(def.Name, 1.3f, () =>
            {
                CreateMatch(settings, false, autoPlay, mapIndex);
                _ctx.Sfx.Duck = 1f;
                _ctx.UI.ShowHud(_match);
                RefreshTouch();
            });
        }

        public void Rematch()
        {
            if (OnlineMatch || _ctx.Net.InRoom) { OpenOnline(); return; }
            AfterBreakAd(() => StartMatch(_lastSettings, _lastAutoPlay, _lastMap));
        }

        public void QuitToMenu()
        {
            if (_ctx.Net.InRoom || _ctx.Net.State == NetSession.Status.Busy)
            {
                _ctx.Net.Leave();
                StartMenu();
                return;
            }
            AfterBreakAd(StartMenu);
        }

        public void SetPaused(bool paused)
        {
            // online the match never stops: the menu just opens on top
            TimeControl.SetPaused(paused && !OnlineMatch);
            if (_ctx.UI != null) _ctx.UI.ShowPause(paused);
        }

        private void CreateMatch(MatchSettings settings, bool spectator, bool autoPlay, int mapIndex)
        {
            if (_match != null)
            {
                _match.Ended -= OnMatchEnded;
                DestroyImmediate(_match.gameObject);
                _match = null;
            }
            TimeControl.ResetAll();
            var go = new GameObject(spectator ? "DemoMatch" : "Match");
            _match = go.AddComponent<MatchController>();
            _match.Ended += OnMatchEnded;
            _match.Begin(_ctx, settings, MapLayouts.Get(mapIndex).Build(), spectator, autoPlay);
        }

        private void OnMatchEnded(MatchController m)
        {
            if (m != _match || _inMenu) return;
            TimeControl.ResetAll();
            var profile = _ctx.Profile;
            float xpBefore = profile.Xp01;
            var reward = MatchReward.For(m, m.Online);
            if (m.Online) _ctx.Net.HostMatchFinished();
            if (m.Local != null && !m.Local.IsBot)
            {
                if (!m.Online) _adPolicy.MatchFinished();
                var me = m.Rules.Get(m.Local.Id);
                profile.Data.Matches++;
                if (reward.Won) profile.Data.Wins++;
                if (me != null) profile.Data.Kills += me.Kills;
                profile.Data.Coins += reward.Coins;
                reward.LevelsGained = profile.AddXp(reward.Xp); // saves
            }
            _ctx.UI.ShowResults(m, reward, xpBefore);
            if (m.Local != null && !m.Local.IsBot) _missions.OnMatchEnded(reward.Won, reward.Place);
        }
    }
}
