using System;
using NeonBrawl.Core;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace NeonBrawl
{
    /// <summary>
    /// Scene-level service references, passed explicitly to every system (no static singletons).
    /// Created once by <see cref="GameApp"/>.
    /// </summary>
    public sealed class GameContext
    {
        public GameApp App;
        public GameConfig Config;
        public ArtLibrary Art;
        public ArtCatalog Catalog;
        public InputReader Input;
        public FxSystem Fx;
        public SfxSystem Sfx;
        public Haptics Haptics;
        public CameraRig CameraRig;
        public Camera Camera;
        public Light2D GlobalLight;
        public UserPrefs Prefs;
        public PlayerProfile Profile;
        public IAdsService Ads;
        public IStoreService Store;
        public UIRoot UI;
        public ProjectileSystem Projectiles;
        public MatchController Match;
        public NetSession Net;
        public ChatHub Chat;
        public VoiceService Voice;
        public readonly MatchEvents Events = new MatchEvents();
    }

    public readonly struct KillInfo
    {
        public readonly Fighter Killer;
        public readonly Fighter Victim;
        public readonly WeaponId Weapon;
        public readonly KillOutcome Outcome;

        public KillInfo(Fighter killer, Fighter victim, WeaponId weapon, KillOutcome outcome)
        {
            Killer = killer;
            Victim = victim;
            Weapon = weapon;
            Outcome = outcome;
        }
    }

    /// <summary>Match events for the presentation layer (HUD, kill feed, banners).</summary>
    public sealed class MatchEvents
    {
        public event Action<KillInfo> Killed;
        public event Action<string, Color, bool> Announced;
        public event Action<float, Vector2> LocalDamaged;
        public event Action<bool> LocalHitConfirmed;
        public event Action<string> Toast;

        public void RaiseKilled(in KillInfo info) => Killed?.Invoke(info);
        /// <summary>Big centered text. <paramref name="big"/> = countdown/streak size.</summary>
        public void Announce(string text, Color color, bool big) => Announced?.Invoke(text, color, big);
        public void RaiseLocalDamaged(float amount, Vector2 fromDirection) => LocalDamaged?.Invoke(amount, fromDirection);
        public void RaiseLocalHit(bool killed) => LocalHitConfirmed?.Invoke(killed);
        public void RaiseToast(string text) => Toast?.Invoke(text);
    }

    /// <summary>Player options stored in PlayerPrefs.</summary>
    public sealed class UserPrefs
    {
        public float ShakeStrength = 1f;
        public bool Vibration = true;
        public bool Blood = true;
        public float SfxVolume = 1f;
        public float MusicVolume = 0.7f;
        public bool ShowFps;
        public bool ForceTouchControls;
        public bool TextChat = true;
        /// <summary>Personalized ads answer: -1 not asked yet, 0 no, 1 yes.</summary>
        public int AdConsent = -1;
        public bool VoiceChat = true;
        public float VoiceVolume = 0.8f;
        /// <summary>Your mic talks to your team only (team battles).</summary>
        public bool VoiceTeam;
        /// <summary>Fire by itself at enemies you can see (on by default).</summary>
        public bool AutoShoot = true;
        /// <summary>Where the touch sticks and buttons sit, how big and how see-through they are.</summary>
        public ControlLayout Layout = ControlLayout.Defaults();
        public MatchSettings LastSettings = MatchSettings.Default;

        public static UserPrefs Load()
        {
            var p = new UserPrefs
            {
                ShakeStrength = PlayerPrefs.GetFloat("nb.shake", 1f),
                Vibration = PlayerPrefs.GetInt("nb.vibrate", 1) == 1,
                Blood = PlayerPrefs.GetInt("nb.blood", 1) == 1,
                SfxVolume = PlayerPrefs.GetFloat("nb.sfx", 1f),
                MusicVolume = PlayerPrefs.GetFloat("nb.music", 0.7f),
                ShowFps = PlayerPrefs.GetInt("nb.fps", 0) == 1,
                ForceTouchControls = PlayerPrefs.GetInt("nb.touch", 0) == 1,
                TextChat = PlayerPrefs.GetInt("nb.chat", 1) == 1,
                AdConsent = PlayerPrefs.GetInt("nb.adconsent", -1),
                VoiceChat = PlayerPrefs.GetInt("nb.voice", 1) == 1,
                VoiceVolume = PlayerPrefs.GetFloat("nb.voicevol", 0.8f),
                VoiceTeam = PlayerPrefs.GetInt("nb.voiceteam", 0) == 1,
                AutoShoot = PlayerPrefs.GetInt("nb.autoshoot", 1) == 1,
                Layout = ControlLayout.Load(),
            };
            var s = MatchSettings.Default;
            s.Mode = (MatchMode)PlayerPrefs.GetInt("nb.m.mode", (int)s.Mode);
            s.KillLimit = PlayerPrefs.GetInt("nb.m.kills", s.KillLimit);
            s.TimeLimit = PlayerPrefs.GetFloat("nb.m.time", s.TimeLimit);
            s.BotCount = PlayerPrefs.GetInt("nb.m.bots", s.BotCount);
            s.Difficulty = PlayerPrefs.GetInt("nb.m.diff", s.Difficulty);
            s.Night = PlayerPrefs.GetInt("nb.m.night", 0) == 1;
            s.Fog = PlayerPrefs.GetInt("nb.m.fog", 1) == 1;
            p.LastSettings = s;
            return p;
        }

        public void Save()
        {
            PlayerPrefs.SetFloat("nb.shake", ShakeStrength);
            PlayerPrefs.SetInt("nb.vibrate", Vibration ? 1 : 0);
            PlayerPrefs.SetInt("nb.blood", Blood ? 1 : 0);
            PlayerPrefs.SetFloat("nb.sfx", SfxVolume);
            PlayerPrefs.SetFloat("nb.music", MusicVolume);
            PlayerPrefs.SetInt("nb.fps", ShowFps ? 1 : 0);
            PlayerPrefs.SetInt("nb.touch", ForceTouchControls ? 1 : 0);
            PlayerPrefs.SetInt("nb.chat", TextChat ? 1 : 0);
            PlayerPrefs.SetInt("nb.adconsent", AdConsent);
            PlayerPrefs.SetInt("nb.voice", VoiceChat ? 1 : 0);
            PlayerPrefs.SetFloat("nb.voicevol", VoiceVolume);
            PlayerPrefs.SetInt("nb.voiceteam", VoiceTeam ? 1 : 0);
            PlayerPrefs.SetInt("nb.autoshoot", AutoShoot ? 1 : 0);
            Layout.Save();
            var s = LastSettings;
            PlayerPrefs.SetInt("nb.m.mode", (int)s.Mode);
            PlayerPrefs.SetInt("nb.m.kills", s.KillLimit);
            PlayerPrefs.SetFloat("nb.m.time", s.TimeLimit);
            PlayerPrefs.SetInt("nb.m.bots", s.BotCount);
            PlayerPrefs.SetInt("nb.m.diff", s.Difficulty);
            PlayerPrefs.SetInt("nb.m.night", s.Night ? 1 : 0);
            PlayerPrefs.SetInt("nb.m.fog", s.Fog ? 1 : 0);
            PlayerPrefs.Save();
        }
    }

    /// <summary>Owns Time.timeScale: pause, hit-stop and slow motion never fight each other.</summary>
    public static class TimeControl
    {
        private static bool _paused;
        private static float _effectUntil;
        private static float _effectScale = 1f;

        public static bool Paused => _paused;

        public static void SetPaused(bool paused) { _paused = paused; Apply(); }

        /// <summary>Briefly slows time (real seconds). Offline only: online play never changes time.</summary>
        public static void Slow(float realSeconds, float scale)
        {
            _effectUntil = Time.unscaledTime + realSeconds;
            _effectScale = scale;
            Apply();
        }

        public static void ResetAll()
        {
            _paused = false;
            _effectUntil = 0f;
            Apply();
        }

        public static void Tick() => Apply();

        private static void Apply()
        {
            float target = _paused ? 0f : (Time.unscaledTime < _effectUntil ? _effectScale : 1f);
            if (!Mathf.Approximately(Time.timeScale, target)) Time.timeScale = target;
        }
    }
}
