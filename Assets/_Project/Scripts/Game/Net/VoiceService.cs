using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.Vivox;
using UnityEngine;
using UnityEngine.InputSystem;
#if UNITY_ANDROID
using UnityEngine.Android;
#endif

namespace NeonBrawl
{
    /// <summary>
    /// Voice chat in online rooms (Unity Vivox). Everyone in the room shares an ALL channel; in team
    /// battles each team also has its own TEAM channel. You hear both and choose which one your mic
    /// talks into (ALL at the start of every match, so both sides always hear each other).
    /// The mic remembers its last state (on by default); the speaker button silences everyone; muting a
    /// player in the room list silences them on this device only. PC: hold V to talk.
    ///
    /// One-way audio fix: phones open the microphone when they JOIN a voice channel. If the mic
    /// permission arrives later (asked mid-match), the phone could hear others but sent nothing, so the
    /// channels are now rejoined as soon as the permission is granted.
    /// </summary>
    public sealed class VoiceService : MonoBehaviour
    {
        public enum Target { All, Team }

        private GameContext _ctx;
        private bool _busy, _subscribed, _askedThisRun, _rejoin;
        private string _room, _allChannel, _teamChannel;
        private readonly List<VivoxParticipant> _people = new List<VivoxParticipant>(24);
        private VivoxParticipant _self;
        private bool _pushToTalk;

        public bool Available { get; private set; } = true;
        public bool Connected { get; private set; }
        public bool MicOn { get; private set; }
        public bool SpeakerOn { get; private set; } = true;
        public Target TalkTo { get; private set; }
        public bool HasTeamChannel => _teamChannel != null;
        public string Status { get; private set; } = "";
        /// <summary>How loud your own mic is right now (0..1), for the HUD level ring.</summary>
        public float SelfLevel => _self != null && Transmitting ? Mathf.Clamp01((float)_self.AudioEnergy * 1.4f) : 0f;
        /// <summary>Phones: the game may not use the microphone (shown on the mic button).</summary>
        public bool MicBlocked { get; private set; }

        /// <summary>Names of players talking right now (for the HUD).</summary>
        public readonly List<string> Talking = new List<string>(4);

        /// <summary>Connection, mic, speaker, channel or talking list changed.</summary>
        public event Action Changed;

        public void Init(GameContext ctx)
        {
            _ctx = ctx;
            ctx.Net.Changed += OnNetChanged;
            TalkTo = Target.All;
            MicOn = ctx.Prefs.VoiceMic;
        }

        private void OnDestroy()
        {
            if (_ctx != null) _ctx.Net.Changed -= OnNetChanged;
            Unsubscribe();
        }

        /// <summary>
        /// Loads the voice engine while you are still in the menus, so the first online match does not
        /// freeze on it (a long freeze lets Relay drop the connection).
        /// </summary>
        public async void Prewarm()
        {
            if (_ctx == null || !_ctx.Prefs.VoiceChat || !Available) return;
            try
            {
                await UgsBoot.SignInAsync();
                var v = VivoxService.Instance;
                if (v.InitializationState == VivoxInitializationState.Uninitialized) await v.InitializeAsync();
            }
            catch (Exception e) { Debug.LogWarning("[Voice] prewarm: " + e.Message); }
        }

        /// <summary>
        /// Phones: asks for the microphone before you join a room (once per app run). Android pauses the
        /// game while its permission box is up, and a long pause in a match drops you from the room.
        /// </summary>
        public void AskMicEarly()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (_ctx == null || !_ctx.Prefs.VoiceChat || !_ctx.Prefs.VoiceMic) return;
            if (Permission.HasUserAuthorizedPermission(Permission.Microphone)) { MicBlocked = false; return; }
            if (_askedThisRun) return;
            _askedThisRun = true;
            var cb = new PermissionCallbacks();
            cb.PermissionGranted += _ => { MicBlocked = false; if (Connected) _rejoin = true; };
            cb.PermissionDenied += _ => MicBlocked = true;
            cb.PermissionDeniedAndDontAskAgain += _ => MicBlocked = true;
            Permission.RequestUserPermission(Permission.Microphone, cb);
#endif
        }

        private static bool HasMicPermission()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            return Permission.HasUserAuthorizedPermission(Permission.Microphone);
#else
            return true;
#endif
        }

        // ---------------------------------------------------------------- joining

        /// <summary>An online match started: join the room's channels (team channel in team battles).</summary>
        public async void EnterMatch(string roomCode, int team, bool teamMode)
        {
            Debug.Log("[Voice] match started: room=" + roomCode + " team=" + team + " teamMode=" + teamMode + " enabled=" + (_ctx != null && _ctx.Prefs.VoiceChat) + " available=" + Available);
            if (_ctx == null || !_ctx.Prefs.VoiceChat || !Available) return;
            string all = "nb-" + Safe(roomCode) + "-all";
            string teamName = teamMode ? "nb-" + Safe(roomCode) + "-t" + team : null;
            // every match starts talking to everyone: a TEAM choice left over from an old match made
            // one side silent to the other
            TalkTo = Target.All;
            if (Connected && all == _allChannel && teamName == _teamChannel)
            {
                await ApplyTransmission();
                ApplyMicState();
                Changed?.Invoke();
                return;
            }
            if (_busy) return;
            _busy = true;
            try
            {
                SetStatus("VOICE: CONNECTING…");
                if (!await LogIn()) return;
                if (Connected) await VivoxService.Instance.LeaveAllChannelsAsync();
                _room = roomCode;
                _allChannel = all;
                _teamChannel = teamName;
                await JoinChannels();
                SetStatus("");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Voice] could not join: " + e.Message);
                Connected = false;
                SetStatus("VOICE UNAVAILABLE");
            }
            finally { _busy = false; }
        }

        private async Task JoinChannels()
        {
            _people.Clear();
            _self = null;
            Talking.Clear();
            MicBlocked = !HasMicPermission();
            await VivoxService.Instance.JoinGroupChannelAsync(_allChannel, ChatCapability.AudioOnly);
            if (_teamChannel != null) await VivoxService.Instance.JoinGroupChannelAsync(_teamChannel, ChatCapability.AudioOnly);
            Connected = true;
            Debug.Log("[Voice] joined " + _allChannel + (_teamChannel != null ? " + " + _teamChannel : "") + " mic=" + MicOn + " permission=" + !MicBlocked);
            await ApplyTransmission();
            ApplyMicState();
            ApplyVolume();
            Changed?.Invoke();
        }

        /// <summary>The mic permission arrived after joining: join again so the phone really sends sound.</summary>
        private async void Rejoin()
        {
            if (!Connected || _busy || _allChannel == null) return;
            _busy = true;
            try
            {
                Debug.Log("[Voice] mic allowed: rejoining so the microphone opens");
                await VivoxService.Instance.LeaveAllChannelsAsync();
                await JoinChannels();
            }
            catch (Exception e) { Debug.LogWarning("[Voice] rejoin: " + e.Message); }
            finally { _busy = false; }
        }

        private static string Safe(string s)
        {
            if (string.IsNullOrEmpty(s)) return "lan";
            var sb = new System.Text.StringBuilder(s.Length);
            foreach (char c in s.ToLowerInvariant()) if (char.IsLetterOrDigit(c)) sb.Append(c);
            return sb.Length > 0 ? sb.ToString() : "lan";
        }

        private async Task<bool> LogIn()
        {
            try
            {
                await UgsBoot.SignInAsync();
                var v = VivoxService.Instance;
                if (v.InitializationState == VivoxInitializationState.Uninitialized) await v.InitializeAsync();
                Subscribe();
                if (!v.IsLoggedIn)
                    await v.LoginAsync(new LoginOptions { DisplayName = NetIO.CleanName(_ctx.Profile.Data.Name) + "#" + _ctx.Net.LocalId, ParticipantUpdateFrequency = ParticipantPropertyUpdateFrequency.FivePerSecond });
                return v.IsLoggedIn;
            }
            catch (Exception e)
            {
                // most often: Vivox is not switched on for this project in the Unity dashboard
                Debug.LogWarning("[Voice] Vivox sign-in failed: " + e.Message);
                Available = false;
                SetStatus("VOICE UNAVAILABLE");
                return false;
            }
        }

        /// <summary>Leaves every voice channel (left the room, or voice switched off).</summary>
        public async void Leave()
        {
            if (!Connected && !_busy) return;
            Connected = false;
            _allChannel = _teamChannel = null;
            _people.Clear();
            _self = null;
            Talking.Clear();
            Changed?.Invoke();
            try
            {
                var v = VivoxService.Instance;
                if (v != null && v.IsLoggedIn)
                {
                    await v.LeaveAllChannelsAsync();
                    await v.LogoutAsync();
                }
            }
            catch (Exception e) { Debug.LogWarning("[Voice] leave: " + e.Message); }
        }

        private void OnNetChanged()
        {
            if (_ctx.Net.State == NetSession.Status.Offline && Connected) Leave();
        }

        // ---------------------------------------------------------------- controls

        /// <summary>Opens or closes your microphone (asks for permission on phones). Remembered for next time.</summary>
        public void SetMic(bool on)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (on && !Permission.HasUserAuthorizedPermission(Permission.Microphone))
            {
                var cb = new PermissionCallbacks();
                cb.PermissionGranted += _ =>
                {
                    MicBlocked = false;
                    MicOn = true;
                    SaveMic();
                    if (Connected) _rejoin = true; // the channel was joined without the mic: join again
                    Changed?.Invoke();
                };
                cb.PermissionDenied += _ => { MicBlocked = true; _ctx.UI.Toast("Allow the microphone to talk"); Changed?.Invoke(); };
                cb.PermissionDeniedAndDontAskAgain += _ => { MicBlocked = true; _ctx.UI.Toast("Microphone is blocked: allow it in the phone settings"); Changed?.Invoke(); };
                Permission.RequestUserPermission(Permission.Microphone, cb);
                return;
            }
#endif
            MicOn = on;
            SaveMic();
            ApplyMicState();
            Changed?.Invoke();
        }

        private void SaveMic()
        {
            if (_ctx.Prefs.VoiceMic == MicOn) return;
            _ctx.Prefs.VoiceMic = MicOn;
            _ctx.Prefs.Save();
        }

        private void ApplyMicState()
        {
            if (!Connected) return;
            if (MicOn || _pushToTalk) VivoxService.Instance.UnmuteInputDevice();
            else VivoxService.Instance.MuteInputDevice();
        }

        public void SetSpeaker(bool on)
        {
            SpeakerOn = on;
            if (Connected)
            {
                if (on) VivoxService.Instance.UnmuteOutputDevice();
                else VivoxService.Instance.MuteOutputDevice();
            }
            Changed?.Invoke();
        }

        /// <summary>Talk to everyone in the room, or only to your team.</summary>
        public async void SetTalkTo(Target t)
        {
            if (t == Target.Team && _teamChannel == null) t = Target.All;
            TalkTo = t;
            await ApplyTransmission();
            Changed?.Invoke();
            _ctx.UI.Toast(t == Target.Team ? "Your mic: TEAM only" : "Your mic: EVERYONE");
        }

        private async Task ApplyTransmission()
        {
            if (!Connected) return;
            try
            {
                string ch = TalkTo == Target.Team && _teamChannel != null ? _teamChannel : _allChannel;
                await VivoxService.Instance.SetChannelTransmissionModeAsync(TransmissionMode.Single, ch);
            }
            catch (Exception e) { Debug.LogWarning("[Voice] transmission: " + e.Message); }
        }

        /// <summary>Settings: other players' voice volume.</summary>
        public void ApplyVolume()
        {
            if (!Connected) return;
            VivoxService.Instance.SetOutputDeviceVolume(Mathf.RoundToInt(Mathf.Lerp(-50f, 10f, Mathf.Clamp01(_ctx.Prefs.VoiceVolume))));
            if (SpeakerOn) VivoxService.Instance.UnmuteOutputDevice();
        }

        /// <summary>Settings: voice chat switched on or off.</summary>
        public void ApplySettings()
        {
            if (!_ctx.Prefs.VoiceChat) Leave();
            else if (_ctx.Match != null && _ctx.Match.Online && _ctx.Net.InRoom)
                EnterMatch(_ctx.Net.Code, _ctx.Match.Local != null ? _ctx.Match.Local.Team : 0, _ctx.Match.Rules.IsTeamMode);
        }

        /// <summary>Mutes or unmutes one room member's voice on this device.</summary>
        public void MutePlayer(ulong clientId, bool mute)
        {
            string tag = "#" + clientId;
            for (int i = 0; i < _people.Count; i++)
            {
                var p = _people[i];
                if (p.IsSelf || p.DisplayName == null || !p.DisplayName.EndsWith(tag, StringComparison.Ordinal)) continue;
                if (mute) p.MutePlayerLocally();
                else p.UnmutePlayerLocally();
            }
        }

        private void Update()
        {
            if (_rejoin && !_busy)
            {
                _rejoin = false;
                Rejoin();
            }
            // PC: hold V to talk, even while the mic toggle is off
            var kb = Keyboard.current;
            if (kb == null || !Connected || _ctx.Input.Typing) return;
            bool held = kb.vKey.isPressed;
            if (held == _pushToTalk) return;
            _pushToTalk = held;
            ApplyMicState();
            Changed?.Invoke();
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused || !Connected) return;
            // phones can hand the audio device to another app while we are away: set ours up again
            ApplyMicState();
            if (SpeakerOn) VivoxService.Instance.UnmuteOutputDevice();
            if (!MicBlocked || !HasMicPermission()) return;
            MicBlocked = false; // allowed in the phone settings while away
            _rejoin = true;
        }

        public bool Transmitting => Connected && (MicOn || _pushToTalk) && !MicBlocked;

        // ---------------------------------------------------------------- participants

        private void Subscribe()
        {
            if (_subscribed) return;
            _subscribed = true;
            VivoxService.Instance.ParticipantAddedToChannel += OnAdded;
            VivoxService.Instance.ParticipantRemovedFromChannel += OnRemoved;
        }

        private void Unsubscribe()
        {
            if (!_subscribed) return;
            _subscribed = false;
            try
            {
                VivoxService.Instance.ParticipantAddedToChannel -= OnAdded;
                VivoxService.Instance.ParticipantRemovedFromChannel -= OnRemoved;
            }
            catch { /* quitting */ }
        }

        private void OnAdded(VivoxParticipant p)
        {
            _people.Add(p);
            if (p.IsSelf && (_self == null || p.ChannelName == _allChannel)) _self = p;
            p.ParticipantSpeechDetected += RefreshTalking;
            Debug.Log("[Voice] + " + p.DisplayName + " in " + p.ChannelName + (p.IsSelf ? " (you)" : ""));
            // someone muted in the room list stays muted
            if (!p.IsSelf && p.DisplayName != null)
            {
                int hash = p.DisplayName.LastIndexOf('#');
                if (hash >= 0 && ulong.TryParse(p.DisplayName.Substring(hash + 1), out ulong id) && _ctx.Chat.IsMuted(id)) p.MutePlayerLocally();
            }
            Changed?.Invoke();
        }

        private void OnRemoved(VivoxParticipant p)
        {
            p.ParticipantSpeechDetected -= RefreshTalking;
            _people.Remove(p);
            if (p == _self) _self = null;
            RefreshTalking();
        }

        private void RefreshTalking()
        {
            Talking.Clear();
            for (int i = 0; i < _people.Count && Talking.Count < 4; i++)
            {
                var p = _people[i];
                if (!p.SpeechDetected || p.IsMuted) continue;
                string n = p.DisplayName ?? "";
                int hash = n.LastIndexOf('#');
                if (hash > 0) n = n.Substring(0, hash);
                if (p.IsSelf) n = "YOU";
                if (!Talking.Contains(n)) Talking.Add(n);
            }
            Changed?.Invoke();
        }

        private void SetStatus(string s)
        {
            Status = s;
            Changed?.Invoke();
        }
    }
}
