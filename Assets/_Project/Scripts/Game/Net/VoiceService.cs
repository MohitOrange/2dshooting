using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.Core;
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
    /// talks into. The mic starts muted; the speaker button silences everyone; muting a player in the
    /// room list silences them on this device only. PC: hold V to talk.
    /// </summary>
    public sealed class VoiceService : MonoBehaviour
    {
        public enum Target { All, Team }

        private GameContext _ctx;
        private bool _busy, _subscribed;
        private string _room, _allChannel, _teamChannel;
        private readonly List<VivoxParticipant> _people = new List<VivoxParticipant>(24);
        private bool _pushToTalk;

        public bool Available { get; private set; } = true;
        public bool Connected { get; private set; }
        public bool MicOn { get; private set; }
        public bool SpeakerOn { get; private set; } = true;
        public Target TalkTo { get; private set; }
        public bool HasTeamChannel => _teamChannel != null;
        public string Status { get; private set; } = "";

        /// <summary>Names of players talking right now (for the HUD).</summary>
        public readonly List<string> Talking = new List<string>(4);

        /// <summary>Connection, mic, speaker, channel or talking list changed.</summary>
        public event Action Changed;

        public void Init(GameContext ctx)
        {
            _ctx = ctx;
            ctx.Net.Changed += OnNetChanged;
            TalkTo = ctx.Prefs.VoiceTeam ? Target.Team : Target.All;
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
        /// Phones: asks for the microphone once, before you join a room. Android pauses the game while its
        /// permission box is up, and a pause of 10 s in a match drops you from the room.
        /// </summary>
        public void AskMicEarly()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (_ctx == null || !_ctx.Prefs.VoiceChat) return;
            if (Permission.HasUserAuthorizedPermission(Permission.Microphone)) return;
            if (PlayerPrefs.GetInt("nb.micasked", 0) == 1) return;
            PlayerPrefs.SetInt("nb.micasked", 1);
            PlayerPrefs.Save();
            Permission.RequestUserPermission(Permission.Microphone);
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
            if (Connected && all == _allChannel && teamName == _teamChannel) return;
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
                _people.Clear();
                Talking.Clear();
                await VivoxService.Instance.JoinGroupChannelAsync(_allChannel, ChatCapability.AudioOnly);
                if (_teamChannel != null) await VivoxService.Instance.JoinGroupChannelAsync(_teamChannel, ChatCapability.AudioOnly);
                Connected = true;
                Debug.Log("[Voice] joined " + _allChannel + (_teamChannel != null ? " + " + _teamChannel : ""));
                if (TalkTo == Target.Team && _teamChannel == null) TalkTo = Target.All;
                await ApplyTransmission();
                SetMic(MicOn);
                ApplyVolume();
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
                    await v.LoginAsync(new LoginOptions { DisplayName = NetIO.CleanName(_ctx.Profile.Data.Name) + "#" + _ctx.Net.LocalId });
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

        /// <summary>Opens or closes your microphone (asks for permission the first time on phones).</summary>
        public void SetMic(bool on)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (on && !Permission.HasUserAuthorizedPermission(Permission.Microphone))
            {
                var cb = new PermissionCallbacks();
                cb.PermissionGranted += _ => SetMic(true);
                cb.PermissionDenied += _ => { _ctx.UI.Toast("Microphone permission is needed to talk"); SetMic(false); };
                Permission.RequestUserPermission(Permission.Microphone, cb);
                return;
            }
#endif
            MicOn = on;
            if (Connected)
            {
                if (on || _pushToTalk) VivoxService.Instance.UnmuteInputDevice();
                else VivoxService.Instance.MuteInputDevice();
            }
            Changed?.Invoke();
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
            _ctx.Prefs.VoiceTeam = t == Target.Team;
            _ctx.Prefs.Save();
            await ApplyTransmission();
            Changed?.Invoke();
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
            // PC: hold V to talk, even while the mic toggle is off
            var kb = Keyboard.current;
            if (kb == null || !Connected || _ctx.Input.Typing) return;
            bool held = kb.vKey.isPressed;
            if (held == _pushToTalk) return;
            _pushToTalk = held;
            if (held) VivoxService.Instance.UnmuteInputDevice();
            else if (!MicOn) VivoxService.Instance.MuteInputDevice();
            Changed?.Invoke();
        }

        public bool Transmitting => Connected && (MicOn || _pushToTalk);

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
            p.ParticipantSpeechDetected += RefreshTalking;
            // someone muted in the room list stays muted
            if (!p.IsSelf && p.DisplayName != null)
            {
                int hash = p.DisplayName.LastIndexOf('#');
                if (hash >= 0 && ulong.TryParse(p.DisplayName.Substring(hash + 1), out ulong id) && _ctx.Chat.IsMuted(id)) p.MutePlayerLocally();
            }
        }

        private void OnRemoved(VivoxParticipant p)
        {
            p.ParticipantSpeechDetected -= RefreshTalking;
            _people.Remove(p);
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
