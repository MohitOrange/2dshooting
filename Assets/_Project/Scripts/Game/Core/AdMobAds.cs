using System;
using System.Collections.Generic;
using UnityEngine;
#if NB_ADMOB && (UNITY_ANDROID || UNITY_IOS)
using GoogleMobileAds.Api;
using GoogleMobileAds.Ump.Api;
#endif

namespace NeonBrawl
{
    /// <summary>
    /// Every ad id in one place. The editor and development builds always use Google's public test ads
    /// (safe to tap); release builds use the real units below. An empty real id switches that format off
    /// in release builds, so a missing unit can never show test ads to players.
    /// Unity Ads joins through AdMob mediation (bidding): AdMob console → Mediation → Unity Ads with
    /// game id 6216551 (Android) / 6216550 (iOS). No code needed for that.
    /// </summary>
    public static class AdIds
    {
        // GitHub Pages serves the repo root of main, so the page lives under /docs/.
        public const string PrivacyPolicyUrl = "https://mohitorange.github.io/2dshooting/docs/privacy-policy.html";

#if UNITY_IOS
        public const string AppId = "";
        public const string Rewarded = "";
        public const string Interstitial = "";
        public const string Banner = "";
        private const string TestRewarded = "ca-app-pub-3940256099942544/1712485313";
        private const string TestInterstitial = "ca-app-pub-3940256099942544/4411468910";
        private const string TestBanner = "ca-app-pub-3940256099942544/2934735716";
#else
        public const string AppId = "ca-app-pub-8048914278660224~6107316062";
        public const string Rewarded = "ca-app-pub-8048914278660224/3481152725";
        // Create a plain Interstitial and a plain Banner unit in AdMob (leave "partner bidding" unticked)
        // and paste their ids here.
        public const string Interstitial = "";
        public const string Banner = "";
        private const string TestRewarded = "ca-app-pub-3940256099942544/5224354917";
        private const string TestInterstitial = "ca-app-pub-3940256099942544/1033173712";
        private const string TestBanner = "ca-app-pub-3940256099942544/6300978111";
#endif

        /// <summary>Phones that always get test ads, even in release builds (AdMob writes the id to logcat).</summary>
        public static readonly List<string> TestDevices = new List<string>();

        /// <summary>Editor, development builds and the "test ads" APK (define NB_TEST_ADS) never show real ads.</summary>
        public static bool TestMode =>
#if NB_TEST_ADS
            true;
#else
            Application.isEditor || Debug.isDebugBuild;
#endif

        public static string RewardedUnit => TestMode ? TestRewarded : Rewarded;
        public static string InterstitialUnit => TestMode ? TestInterstitial : Interstitial;
        public static string BannerUnit => TestMode ? TestBanner : Banner;
    }

    /// <summary>
    /// Ads through Google AdMob (Unity Ads is mixed in by AdMob mediation). Google's consent form (UMP)
    /// shows first where the law needs it (EU/UK…); ads only load once consent allows. Rewarded videos are
    /// always the player's choice, full-screen ads only between offline matches (<see cref="AdPolicy"/>),
    /// and the small banner only on the results screen, in a slot that no button uses.
    /// Every format preloads and reloads itself with a growing back-off.
    /// </summary>
    public sealed class AdMobAds : MonoBehaviour, IAdsService
    {
        public static bool Supported =>
#if NB_ADMOB && (UNITY_ANDROID || UNITY_IOS)
            true;
#else
            false;
#endif

        private readonly Queue<Action> _mainThread = new Queue<Action>();
        private Action<bool> _rewardDone;
        private Action _interDone;
        private bool _earned, _personalized = true, _started;
        private float _rewardRetry = 4f, _interRetry = 4f, _bannerRetry = 8f;
        private bool _bannerWanted;
        private Rect _bannerSlot;
        private bool _audioWasPaused;

        public bool Initialized { get; private set; }

        /// <summary>True when the player's region needs a "privacy options" entry point (UMP).</summary>
        public bool PrivacyOptionsRequired { get; private set; }

        private void Post(Action a) { lock (_mainThread) _mainThread.Enqueue(a); }

        private void Update()
        {
            while (true)
            {
                Action a;
                lock (_mainThread)
                {
                    if (_mainThread.Count == 0) break;
                    a = _mainThread.Dequeue();
                }
                try { a(); } catch (Exception e) { Debug.LogException(e); }
            }
        }

#if NB_ADMOB && (UNITY_ANDROID || UNITY_IOS)
        private RewardedAd _rewarded;
        private InterstitialAd _inter;
        private BannerView _banner;
        private bool _loadingRewarded, _loadingInter, _bannerLoaded, _bannerLoading, _bannerLogged;

        public bool RewardedReady => _rewarded != null && _rewarded.CanShowAd();

        /// <summary>Asks for consent where needed (Google's form), then starts the SDK.</summary>
        public void Init(bool personalized)
        {
            _personalized = personalized;
            ApplyRequestConfiguration();
            ApplyUnityAdsPrivacy();
            RequestConsent();
            // a returning player who already answered can start loading at once
            StartSdk();
        }

        /// <summary>Asks Google whether a consent form is needed here; retries every minute while offline.</summary>
        private void RequestConsent()
        {
            var request = new ConsentRequestParameters { TagForUnderAgeOfConsent = false };
            ConsentInformation.Update(request, error => Post(() =>
            {
                if (error != null) Debug.LogWarning("[Ads] consent update failed: " + error.Message);
                ConsentForm.LoadAndShowConsentFormIfRequired(formError => Post(() =>
                {
                    if (formError != null) Debug.LogWarning("[Ads] consent form: " + formError.Message);
                    PrivacyOptionsRequired = ConsentInformation.PrivacyOptionsRequirementStatus == PrivacyOptionsRequirementStatus.Required;
                    StartSdk();
                    if (!_started) Invoke(nameof(RequestConsent), 60f); // first launch without internet
                }));
            }));
        }

        private void ApplyRequestConfiguration()
        {
            MobileAds.SetRequestConfiguration(new RequestConfiguration
            {
                MaxAdContentRating = MaxAdContentRating.T,                         // the game is rated Teen (cartoon violence)
                PublisherPrivacyPersonalizationState = _personalized ? PublisherPrivacyPersonalizationState.Default : PublisherPrivacyPersonalizationState.Disabled,
                TestDeviceIds = AdIds.TestDevices,
            });
        }

        private void ApplyUnityAdsPrivacy()
        {
#if NB_ADMOB_UNITYADS
            // US state privacy laws: Unity Ads may only personalize with the player's okay (EU consent comes from UMP)
            GoogleMobileAds.Mediation.UnityAds.Api.UnityAds.SetConsentMetaData("privacy.consent", _personalized);
#endif
        }

        private void StartSdk()
        {
            if (_started || !ConsentInformation.CanRequestAds()) return;
            _started = true;
            MobileAds.Initialize(status => Post(() =>
            {
                Initialized = true;
                Debug.Log("[Ads] AdMob ready" + (AdIds.TestMode ? " (test ads)" : ""));
                LoadRewarded();
                LoadInterstitial();
                if (_bannerWanted) LoadBanner();
            }));
        }

        /// <summary>The player changed PERSONAL ADS in Settings.</summary>
        public void SetConsent(bool personalized)
        {
            _personalized = personalized;
            ApplyRequestConfiguration();
            ApplyUnityAdsPrivacy();
        }

        /// <summary>Re-opens Google's consent choices (only offered where the law needs it).</summary>
        public void ShowPrivacyOptions(Action<string> done)
        {
            ConsentForm.ShowPrivacyOptionsForm(error => Post(() => done?.Invoke(error != null ? error.Message : null)));
        }

        private AdRequest NewRequest() => new AdRequest();

        // ------------------------------------------------------------ rewarded

        private void LoadRewarded()
        {
            string unit = AdIds.RewardedUnit;
            if (!Initialized || _loadingRewarded || _rewarded != null || string.IsNullOrEmpty(unit)) return;
            _loadingRewarded = true;
            RewardedAd.Load(unit, NewRequest(), (ad, error) => Post(() =>
            {
                _loadingRewarded = false;
                if (error != null || ad == null)
                {
                    Debug.Log("[Ads] rewarded load failed: " + (error != null ? error.GetMessage() : "no ad"));
                    Invoke(nameof(LoadRewarded), _rewardRetry);
                    _rewardRetry = Mathf.Min(_rewardRetry * 2f, 120f);
                    return;
                }
                _rewardRetry = 4f;
                _rewarded = ad;
            }));
        }

        public void ShowRewarded(string placement, Action<bool> done)
        {
            if (!RewardedReady)
            {
                done?.Invoke(false);
                LoadRewarded();
                return;
            }
            var ad = _rewarded;
            _rewarded = null;
            _rewardDone = done;
            _earned = false;
            ad.OnAdFullScreenContentOpened += () => Post(PauseGameAudio);
            ad.OnAdFullScreenContentClosed += () => Post(() =>
            {
                ResumeGameAudio();
                ad.Destroy();
                LoadRewarded();
                if (_earned) FinishReward();
                else Invoke(nameof(FinishReward), 1f); // some networks report the reward just after closing
            });
            ad.OnAdFullScreenContentFailed += error => Post(() =>
            {
                Debug.LogWarning("[Ads] rewarded show failed: " + error.GetMessage());
                ResumeGameAudio();
                ad.Destroy();
                FinishReward();
                LoadRewarded();
            });
            ad.Show(reward => Post(() => _earned = true));
        }

        private void FinishReward()
        {
            CancelInvoke(nameof(FinishReward));
            var done = _rewardDone;
            _rewardDone = null;
            done?.Invoke(_earned);
        }

        // ------------------------------------------------------------ interstitial

        private void LoadInterstitial()
        {
            string unit = AdIds.InterstitialUnit;
            if (!Initialized || _loadingInter || _inter != null || string.IsNullOrEmpty(unit)) return;
            _loadingInter = true;
            InterstitialAd.Load(unit, NewRequest(), (ad, error) => Post(() =>
            {
                _loadingInter = false;
                if (error != null || ad == null)
                {
                    Debug.Log("[Ads] interstitial load failed: " + (error != null ? error.GetMessage() : "no ad"));
                    Invoke(nameof(LoadInterstitial), _interRetry);
                    _interRetry = Mathf.Min(_interRetry * 2f, 120f);
                    return;
                }
                _interRetry = 4f;
                _inter = ad;
            }));
        }

        public void ShowInterstitial(string placement, Action done)
        {
            if (_inter == null || !_inter.CanShowAd())
            {
                done?.Invoke();
                LoadInterstitial();
                return;
            }
            var ad = _inter;
            _inter = null;
            _interDone = done;
            ad.OnAdFullScreenContentOpened += () => Post(PauseGameAudio);
            ad.OnAdFullScreenContentClosed += () => Post(() => { ResumeGameAudio(); ad.Destroy(); FinishInter(); LoadInterstitial(); });
            ad.OnAdFullScreenContentFailed += error => Post(() =>
            {
                Debug.LogWarning("[Ads] interstitial show failed: " + error.GetMessage());
                ResumeGameAudio();
                ad.Destroy();
                FinishInter();
                LoadInterstitial();
            });
            ad.Show();
        }

        private void FinishInter()
        {
            var done = _interDone;
            _interDone = null;
            done?.Invoke();
        }

        // ------------------------------------------------------------ banner

        /// <summary>
        /// Shows the small banner inside <paramref name="slotPx"/> (screen pixels, origin bottom-left), centred.
        /// Stays hidden when the banner would not fit, so it can never cover a button.
        /// </summary>
        public void ShowBanner(Rect slotPx)
        {
            _bannerWanted = true;
            _bannerSlot = slotPx;
            if (_banner == null) { LoadBanner(); return; }
            PlaceBanner();
        }

        public void HideBanner()
        {
            _bannerWanted = false;
            if (_banner != null) _banner.Hide();
        }

        private void LoadBanner()
        {
            string unit = AdIds.BannerUnit;
            if (!Initialized || _bannerLoading || string.IsNullOrEmpty(unit)) return;
            if (_banner == null)
            {
                _banner = new BannerView(unit, AdSize.Banner, AdPosition.BottomLeft);
                _banner.OnBannerAdLoaded += () => Post(() => { _bannerLoading = false; _bannerLoaded = true; _bannerRetry = 8f; PlaceBanner(); });
                _banner.OnBannerAdLoadFailed += error => Post(() =>
                {
                    _bannerLoading = false;
                    Debug.Log("[Ads] banner load failed: " + error.GetMessage());
                    Invoke(nameof(LoadBanner), _bannerRetry);
                    _bannerRetry = Mathf.Min(_bannerRetry * 2f, 120f);
                });
                _banner.Hide();
            }
            _bannerLoading = true;
            _banner.LoadAd(NewRequest());
        }

        private void PlaceBanner()
        {
            if (_banner == null || !_bannerLoaded) return;
            if (!_bannerWanted) { _banner.Hide(); return; }
            // The slot starts at the bottom-left corner of the screen, so Google's own BottomLeft anchor
            // (safe-area aware, no pixel/dp maths) puts the banner inside it; we only check that it fits.
            float w = _banner.GetWidthInPixels(), h = _banner.GetHeightInPixels();
            bool fits = w > 1f && h > 1f && w <= _bannerSlot.xMax && h <= _bannerSlot.yMax;
            if (!_bannerLogged)
            {
                _bannerLogged = true;
                Debug.Log("[Ads] banner " + w + "x" + h + " px, free slot up to " + _bannerSlot.xMax + "x" + _bannerSlot.yMax + (fits ? " -> shown" : " -> too big, hidden"));
            }
            if (!fits)
            {
                _banner.Hide();
                return;
            }
            _banner.SetPosition(AdPosition.BottomLeft);
            _banner.Show();
        }

        private void OnDestroy()
        {
            _rewarded?.Destroy();
            _inter?.Destroy();
            _banner?.Destroy();
        }
#else
        // Windows / other builds: no ad SDK. GameApp uses MockAds there instead.
        public bool RewardedReady => false;
        public void Init(bool personalized) { }
        public void SetConsent(bool personalized) { }
        public void ShowPrivacyOptions(Action<string> done) => done?.Invoke(null);
        public void ShowRewarded(string placement, Action<bool> done) => done?.Invoke(false);
        public void ShowInterstitial(string placement, Action done) => done?.Invoke();
        public void ShowBanner(Rect slotPx) { }
        public void HideBanner() { }
#endif

        // full-screen ads bring their own sound: silence the game underneath
        private void PauseGameAudio()
        {
            _audioWasPaused = AudioListener.pause;
            AudioListener.pause = true;
        }

        private void ResumeGameAudio() => AudioListener.pause = _audioWasPaused;
    }

    /// <summary>
    /// When a full-screen ad may show: only between offline matches, never in the first two matches of a
    /// session, at most once every 3 matches and 3 minutes, and never after Remove Ads.
    /// </summary>
    public sealed class AdPolicy
    {
        private int _matches, _sinceLast;
        private float _lastAt = -999f;

        public void MatchFinished() { _matches++; _sinceLast++; }

        public bool InterstitialDue(PlayerProfile profile)
        {
            if (profile.Data.NoAds) return false;
            if (_matches < 3 || _sinceLast < 3) return false;
            return Time.realtimeSinceStartup - _lastAt >= 180f;
        }

        public void Shown()
        {
            _sinceLast = 0;
            _lastAt = Time.realtimeSinceStartup;
        }
    }
}
