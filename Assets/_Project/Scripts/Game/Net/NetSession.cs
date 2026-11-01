// FastBufferWriter/Reader keep all state behind one pointer, so the copy that using() disposes is the
// same buffer we write through by ref (Netcode's documented pattern). The compiler cannot know that.
#pragma warning disable CS0728

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NeonBrawl.Core;
using Unity.Collections;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;

namespace NeonBrawl
{
    /// <summary>
    /// Online rooms. Signs in (anonymous), creates / joins / quick-joins a Unity Multiplayer Services
    /// session (Lobby + Relay: room codes, no server to pay for), and runs the room: who is in it,
    /// the host's match settings, and starting matches. Everything rides on one named Netcode message;
    /// in-match traffic is handed to <see cref="NetMatch"/>.
    /// Dev/LAN: <see cref="HostDirect"/> / <see cref="JoinDirect"/> skip the cloud services.
    /// </summary>
    public sealed class NetSession : MonoBehaviour
    {
        public const string MessageName = "nb";
        public const int MaxPlayers = 12;
        public const float QuickStartDelay = 20f;

        public enum Status { Offline, Busy, InRoom }

        private GameContext _ctx;
        private NetworkManager _nm;
        private UnityTransport _transport;
        private ISession _session;
        private bool _leaving;
        private bool _handlerRegistered;
        private bool _helloSent;
        private bool _matchPending;
        private readonly List<(ulong Sender, byte[] Data)> _pending = new List<(ulong, byte[])>(64);
        private readonly List<ulong> _targets = new List<ulong>(MaxPlayers);
        private NetMatch _match;
        private float _autoStartAt;
        private float _autoStartLeft;
        private float _roomStamp;
        private string _closeReason;
        private readonly List<RoomMember> _lateQueue = new List<RoomMember>(4);

        public Status State { get; private set; }
        public bool IsHost { get; private set; }
        public bool InRoom => State == Status.InRoom;
        public ulong LocalId => _nm != null && _nm.IsListening ? _nm.LocalClientId : 0;
        public string Code { get; private set; } = "";
        public bool QuickRoom { get; private set; }
        public bool MatchRunning { get; private set; }
        public string StatusText { get; private set; } = "";
        public MatchSettings Settings;
        public int MapIndex;
        public readonly List<RoomMember> Members = new List<RoomMember>(MaxPlayers);

        /// <summary>Seconds until a quick room starts on its own (0 = not counting).</summary>
        public float AutoStartLeft => IsHost ? (_autoStartAt > 0f ? Mathf.Max(0f, _autoStartAt - Time.unscaledTime) : 0f)
                                             : Mathf.Max(0f, _autoStartLeft - (Time.unscaledTime - _roomStamp));

        /// <summary>Room changed (members, settings, status). The online screen redraws.</summary>
        public event Action Changed;
        /// <summary>Connection lost or room closed, with a message for the player.</summary>
        public event Action<string> Lost;

        public NetMatch Match
        {
            get => _match;
            set
            {
                _match = value;
                if (_match == null) return;
                if (_matchPending) FlushPending();
                // players who arrived while the host was still loading the match
                if (IsHost && _lateQueue.Count > 0)
                {
                    for (int i = 0; i < _lateQueue.Count; i++)
                        if (IndexOf(_lateQueue[i].ClientId) >= 0) _match.HostLateJoin(_lateQueue[i]);
                    _lateQueue.Clear();
                }
            }
        }

        public void Init(GameContext ctx)
        {
            _ctx = ctx;
            Settings = ctx.Prefs.LastSettings;
            MapIndex = ctx.Profile.Data.LastMap;
        }

        // ---------------------------------------------------------------- connecting

        public async void CreateRoom() => await Connect("CREATING ROOM…", true, false,
            async () => (ISession)await MultiplayerService.Instance.CreateSessionAsync(Options(true)));

        public async void JoinRoom(string code)
        {
            code = (code ?? "").Trim().ToUpperInvariant();
            if (code.Length < 4) { Fail("Type the room code first"); return; }
            await Connect("JOINING " + code + "…", false, false, () => MultiplayerService.Instance.JoinSessionByCodeAsync(code));
        }

        public async void QuickMatch() => await Connect("FINDING A ROOM…", false, true, () =>
            MultiplayerService.Instance.MatchmakeSessionAsync(
                new QuickJoinOptions { CreateSession = true, Timeout = TimeSpan.FromSeconds(4) }, Options(false)));

        private static SessionOptions Options(bool isPrivate) =>
            new SessionOptions { MaxPlayers = MaxPlayers, IsPrivate = isPrivate }.WithRelayNetwork();

        private async Task Connect(string busy, bool host, bool quick, Func<Task<ISession>> op)
        {
            if (State != Status.Offline) return;
            _leaving = false;
            _closeReason = null;
            SetStatus(Status.Busy, busy);
            try
            {
                await EnsureSignedIn();
                EnsureNetworkManager();
                var task = op();
                if (await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(25))) != task)
                {
                    _ = LeaveWhenDone(task);
                    throw new TimeoutException("Room connection timed out");
                }
                var session = await task;
                if (State != Status.Busy) // cancelled while waiting
                {
                    try { await session.LeaveAsync(); } catch (Exception) { }
                    return;
                }
                _session = session;
                _session.RemovedFromSession += OnSessionGone;
                _session.Deleted += OnSessionGone;
                EnterRoom(_session.IsHost, _session.Code, quick || (!host && !_session.IsPrivate));
            }
            catch (Exception e)
            {
                Debug.LogWarning("[NeonBrawl] online: " + e);
                await ShutdownQuietly();
                ResetRoom();
                Fail(Friendly(e));
            }
        }

        /// <summary>Dev / LAN: host on this machine without Lobby or Relay.</summary>
        public void HostDirect(ushort port = 7777)
        {
            if (State != Status.Offline) return;
            EnsureNetworkManager();
            _transport.SetConnectionData("127.0.0.1", port, "0.0.0.0");
            if (!_nm.StartHost()) { Fail("Could not start a LAN room"); return; }
            EnterRoom(true, "LAN", false);
        }

        /// <summary>Dev / LAN: join a host by IP address.</summary>
        public void JoinDirect(string ip, ushort port = 7777)
        {
            if (State != Status.Offline) return;
            EnsureNetworkManager();
            _transport.SetConnectionData(ip, port);
            if (!_nm.StartClient()) { Fail("Could not reach " + ip); return; }
            EnterRoom(false, "LAN", false);
        }

        private void EnterRoom(bool host, string code, bool quick)
        {
            _leaving = false;
            IsHost = host;
            Code = code ?? "";
            QuickRoom = quick;
            Debug.Log("[NeonBrawl] room " + (string.IsNullOrEmpty(Code) ? "?" : Code) + (host ? " hosted" : " joined") + (quick ? " (quick)" : ""));
            MatchRunning = false;
            Members.Clear();
            RegisterHandler();
            if (host)
            {
                Members.Add(Me(LocalId));
                Settings.BotCount = Mathf.Clamp(Settings.BotCount, 0, MaxPlayers - 1);
                if (!_ctx.Profile.OwnsMap(MapIndex)) MapIndex = 0;
                _autoStartAt = quick ? Time.unscaledTime + QuickStartDelay : 0f;
                SetStatus(Status.InRoom, "");
                BroadcastRoom();
            }
            else
            {
                SetStatus(Status.InRoom, "WAITING FOR THE HOST…");
                if (_nm.IsConnectedClient) SendHello();
            }
        }

        public async void Leave()
        {
            if (State == Status.Offline) return;
            if (IsHost && _nm != null && _nm.IsServer && Members.Count > 1)
            {
                // tell guests why they are being dropped, and give the message a moment to leave
                var w = NetIO.Writer(Msg.Closing, 4);
                using (w) SendToClients(ref w, NetworkDelivery.ReliableSequenced);
                await Task.Delay(250);
            }
            _leaving = true;
            var s = _session;
            _session = null;
            ResetRoom();
            SetStatus(Status.Offline, "");
            try
            {
                if (s != null)
                {
                    if (s.IsHost) await s.AsHost().DeleteAsync();
                    else await s.LeaveAsync();
                }
            }
            catch (Exception e) { Debug.LogWarning("[NeonBrawl] leave: " + e.Message); }
            await ShutdownQuietly();
            _leaving = false;
        }

        private async Task ShutdownQuietly()
        {
            if (_nm != null && _nm.IsListening)
            {
                _leaving = true;
                _nm.Shutdown();
                for (int i = 0; i < 60 && _nm.ShutdownInProgress; i++) await Task.Yield();
            }
        }

        private void ResetRoom()
        {
            IsHost = false;
            Code = "";
            QuickRoom = false;
            MatchRunning = false;
            Members.Clear();
            _helloSent = false;
            _matchPending = false;
            _pending.Clear();
            _lateQueue.Clear();
            _autoStartAt = 0f;
            _handlerRegistered = false;
        }

        private void OnSessionGone()
        {
            if (_leaving || State == Status.Offline) return;
            LoseConnection(IsHost ? "The room was closed" : "The host closed the room");
        }

        private void LoseConnection(string reason)
        {
            if (State == Status.Offline) return;
            if (!string.IsNullOrEmpty(_closeReason)) reason = _closeReason;
            _closeReason = null;
            var s = _session;
            _session = null;
            ResetRoom();
            SetStatus(Status.Offline, "");
            if (s != null) _ = LeaveQuietly(s);
            _ = ShutdownQuietly();
            Lost?.Invoke(reason);
        }

        /// <summary>A join that finished after we gave up: leave it again.</summary>
        private static async Task LeaveWhenDone(Task<ISession> task)
        {
            try
            {
                var s = await task;
                if (s != null) await s.LeaveAsync();
            }
            catch (Exception) { }
        }

        private static async Task LeaveQuietly(ISession s)
        {
            try { await s.LeaveAsync(); } catch (Exception) { }
        }

        private async Task EnsureSignedIn()
        {
            if (UnityServices.State == ServicesInitializationState.Uninitialized)
            {
                var options = new InitializationOptions();
                string profile = CommandLine.Value("-nbprofile");
                if (!string.IsNullOrEmpty(profile)) options.SetProfile(profile);
                await UnityServices.InitializeAsync(options);
            }
            while (UnityServices.State == ServicesInitializationState.Initializing) await Task.Yield();
            if (!AuthenticationService.Instance.IsSignedIn) await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }

        private void EnsureNetworkManager()
        {
            if (_nm != null) return;
            var go = new GameObject("NetworkManager");
            go.SetActive(false);
            _transport = go.AddComponent<UnityTransport>();
            _nm = go.AddComponent<NetworkManager>();
            _nm.NetworkConfig = new NetworkConfig
            {
                NetworkTransport = _transport,
                EnableSceneManagement = false,
                ConnectionApproval = false,
                TickRate = 30,
                ClientConnectionBufferTimeout = 15,
            };
            go.SetActive(true);
            _nm.OnClientStarted += RegisterHandler;
            _nm.OnServerStarted += RegisterHandler;
            _nm.OnClientConnectedCallback += OnClientConnected;
            _nm.OnClientDisconnectCallback += OnClientDisconnected;
            _nm.OnClientStopped += OnStopped;
            _nm.OnServerStopped += OnStopped;
        }

        private void RegisterHandler()
        {
            if (_handlerRegistered || _nm == null || _nm.CustomMessagingManager == null) return;
            _nm.CustomMessagingManager.RegisterNamedMessageHandler(MessageName, OnMessage);
            _handlerRegistered = true;
        }

        private void OnClientConnected(ulong id)
        {
            if (!_nm.IsServer && id == _nm.LocalClientId && State == Status.InRoom) SendHello();
        }

        private void OnClientDisconnected(ulong id)
        {
            if (_nm.IsServer)
            {
                if (id == _nm.LocalClientId) return;
                int i = IndexOf(id);
                if (i < 0) return;
                string who = Members[i].Name;
                Members.RemoveAt(i);
                _match?.HostPlayerLeft(id);
                BroadcastRoom();
                if (MatchRunning) _ctx.Events.RaiseToast(who + " left");
            }
            else if (id == _nm.LocalClientId && !_leaving) LoseConnection("Lost connection to the room");
        }

        private void OnStopped(bool wasHost)
        {
            _handlerRegistered = false;
            if (!_leaving && State != Status.Offline) LoseConnection("Lost connection to the room");
        }

        // ---------------------------------------------------------------- room

        private RoomMember Me(ulong id) => new RoomMember
        {
            ClientId = id,
            Name = NetIO.CleanName(_ctx.Profile.Data.Name),
            Look = _ctx.Profile.Look(_ctx.Profile.OutfitColor)
        };

        private int IndexOf(ulong clientId)
        {
            for (int i = 0; i < Members.Count; i++)
                if (Members[i].ClientId == clientId) return i;
            return -1;
        }

        public string NameOf(ulong clientId)
        {
            int i = IndexOf(clientId);
            return i >= 0 ? Members[i].Name : "PLAYER";
        }

        private void SendHello()
        {
            if (_helloSent) return;
            _helloSent = true;
            var me = Me(LocalId);
            var w = NetIO.Writer(Msg.Hello);
            using (w)
            {
                w.WriteValueSafe(me.Name);
                NetIO.Write(ref w, me.Look);
                SendToHost(ref w, NetworkDelivery.ReliableSequenced);
            }
        }

        /// <summary>Host: change the room's map or rules (everyone sees it at once).</summary>
        public void HostSetSettings(MatchSettings s, int map)
        {
            if (!IsHost) return;
            Settings = s;
            MapIndex = map;
            BroadcastRoom();
        }

        private void BroadcastRoom()
        {
            if (!IsHost) return;
            var w = NetIO.Writer(Msg.Room, 512);
            using (w)
            {
                NetIO.Write(ref w, Settings, MapIndex);
                w.WriteValueSafe(MatchRunning);
                w.WriteValueSafe(QuickRoom);
                w.WriteValueSafe(AutoStartLeft);
                w.WriteValueSafe(Code ?? "");
                w.WriteValueSafe((byte)Members.Count);
                for (int i = 0; i < Members.Count; i++)
                {
                    w.WriteValueSafe(Members[i].ClientId);
                    w.WriteValueSafe(Members[i].Name);
                    NetIO.Write(ref w, Members[i].Look);
                }
                SendToClients(ref w, NetworkDelivery.ReliableSequenced);
            }
            Changed?.Invoke();
        }

        /// <summary>Host: start a match for everyone in the room.</summary>
        public void HostStartMatch()
        {
            if (!IsHost || MatchRunning || State != Status.InRoom) return;
            _autoStartAt = 0f;
            var map = MapLayouts.Get(MapIndex).Build();
            var roster = MatchController.BuildOnlineRoster(_ctx, Settings, map, Members, LocalId);
            MatchRunning = true;
            _lateQueue.Clear();
            // messages for the new match wait until it is built (the old one is still on the results screen)
            _match = null;
            _matchPending = true;
            _pending.Clear();
            SendStart(Settings, MapIndex, roster, null, ulong.MaxValue);
            BroadcastRoom();
            _ctx.Prefs.LastSettings = Settings;
            _ctx.Prefs.Save();
            _ctx.App.StartOnlineMatch(Settings, MapIndex, roster, null);
        }

        /// <summary>Host: sends the roster (and, for someone joining late, the match so far).</summary>
        public void SendStart(MatchSettings s, int map, List<FighterSpec> roster, byte[] joinState, ulong onlyTo)
        {
            var w = NetIO.Writer(Msg.Start, 2048);
            using (w)
            {
                NetIO.Write(ref w, s, map);
                w.WriteValueSafe((byte)roster.Count);
                for (int i = 0; i < roster.Count; i++) NetIO.Write(ref w, roster[i]);
                bool late = joinState != null;
                w.WriteValueSafe(late);
                if (late)
                {
                    w.WriteValueSafe(joinState.Length);
                    w.WriteBytesSafe(joinState);
                }
                if (onlyTo == ulong.MaxValue) SendToClients(ref w, NetworkDelivery.ReliableFragmentedSequenced);
                else SendTo(onlyTo, ref w, NetworkDelivery.ReliableFragmentedSequenced);
            }
        }

        /// <summary>Host: the match is over (results showing); the room is open for the next one.</summary>
        public void HostMatchFinished()
        {
            if (!IsHost) return;
            MatchRunning = false;
            if (QuickRoom) _autoStartAt = Time.unscaledTime + QuickStartDelay + 8f;
            BroadcastRoom();
        }

        private void Update()
        {
            if (!IsHost || State != Status.InRoom || MatchRunning || _autoStartAt <= 0f) return;
            if (Time.unscaledTime >= _autoStartAt) HostStartMatch();
        }

        // ---------------------------------------------------------------- messages

        private void OnMessage(ulong sender, FastBufferReader reader)
        {
            if (State == Status.Offline) return;
            reader.ReadValueSafe(out byte b);
            var type = (Msg)b;
            try
            {
                switch (type)
                {
                    case Msg.Hello:
                        if (IsHost) HandleHello(sender, ref reader);
                        break;
                    case Msg.Room:
                        if (!IsHost) HandleRoom(ref reader);
                        break;
                    case Msg.Start:
                        if (!IsHost) HandleStart(ref reader);
                        break;
                    case Msg.Closing:
                        if (!IsHost) _closeReason = "The host closed the room";
                        break;
                    case Msg.Chat:
                        if (_ctx.Chat != null) _ctx.Chat.OnNet(sender, ref reader);
                        break;
                    default:
                        if (_match != null) _match.Handle(type, sender, ref reader);
                        else if (_matchPending && _pending.Count < 2000) _pending.Add((sender, reader.ToArray()));
                        break;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[NeonBrawl] bad message " + type + " from " + sender + ": " + e.Message);
            }
        }

        private void FlushPending()
        {
            _matchPending = false;
            for (int i = 0; i < _pending.Count && _match != null; i++)
            {
                var r = new FastBufferReader(_pending[i].Data, Allocator.Temp);
                using (r)
                {
                    r.ReadValueSafe(out byte b);
                    try { _match.Handle((Msg)b, _pending[i].Sender, ref r); }
                    catch (Exception e) { Debug.LogWarning("[NeonBrawl] bad queued message " + (Msg)b + ": " + e.Message); }
                }
            }
            _pending.Clear();
        }

        private void HandleHello(ulong sender, ref FastBufferReader r)
        {
            r.ReadValueSafe(out string name);
            var look = NetIO.ReadLook(ref r);
            var m = new RoomMember { ClientId = sender, Name = NetIO.CleanName(name), Look = look };
            int i = IndexOf(sender);
            if (i >= 0) Members[i] = m;
            else
            {
                if (Members.Count >= MaxPlayers) { _nm.DisconnectClient(sender, "Room is full"); return; }
                Members.Add(m);
                if (QuickRoom && !MatchRunning && _autoStartAt > 0f) _autoStartAt = Mathf.Max(_autoStartAt, Time.unscaledTime + 8f);
            }
            BroadcastRoom();
            if (MatchRunning)
            {
                if (_match != null) _match.HostLateJoin(m);
                else if (_matchPending) _lateQueue.Add(m); // the host is still loading this match
            }
            _ctx.Sfx.PlayUi(SfxId.UiOpen, 0.6f);
        }

        private void HandleRoom(ref FastBufferReader r)
        {
            Settings = NetIO.ReadSettings(ref r, out MapIndex);
            r.ReadValueSafe(out bool running);
            r.ReadValueSafe(out bool quick);
            r.ReadValueSafe(out _autoStartLeft);
            r.ReadValueSafe(out string code);
            _roomStamp = Time.unscaledTime;
            MatchRunning = running;
            QuickRoom = quick;
            if (!string.IsNullOrEmpty(code)) Code = code;
            r.ReadValueSafe(out byte count);
            Members.Clear();
            for (int i = 0; i < count; i++)
            {
                var m = new RoomMember();
                r.ReadValueSafe(out m.ClientId);
                r.ReadValueSafe(out m.Name);
                m.Look = NetIO.ReadLook(ref r);
                Members.Add(m);
            }
            StatusText = running ? "MATCH IN PROGRESS…" : "";
            Changed?.Invoke();
        }

        private void HandleStart(ref FastBufferReader r)
        {
            var s = NetIO.ReadSettings(ref r, out int map);
            r.ReadValueSafe(out byte count);
            var roster = new List<FighterSpec>(count);
            for (int i = 0; i < count; i++) roster.Add(NetIO.ReadSpec(ref r));
            r.ReadValueSafe(out bool late);
            byte[] state = null;
            if (late)
            {
                r.ReadValueSafe(out int len);
                state = new byte[len];
                r.ReadBytesSafe(ref state, len);
            }
            Settings = s;
            MapIndex = map;
            MatchRunning = true;
            _match = null;
            _matchPending = true;
            _pending.Clear();
            _ctx.App.StartOnlineMatch(s, map, roster, state);
        }

        // ---------------------------------------------------------------- sending

        public void SendToHost(ref FastBufferWriter w, NetworkDelivery d)
        {
            if (_nm == null || !_nm.IsConnectedClient || _nm.IsServer) return;
            _nm.CustomMessagingManager.SendNamedMessage(MessageName, NetworkManager.ServerClientId, w, d);
        }

        public void SendTo(ulong client, ref FastBufferWriter w, NetworkDelivery d)
        {
            if (_nm == null || !_nm.IsServer || client == _nm.LocalClientId) return;
            if (!_nm.ConnectedClients.ContainsKey(client)) return;
            _nm.CustomMessagingManager.SendNamedMessage(MessageName, client, w, d);
        }

        /// <summary>Host: to every connected client (not to itself), optionally skipping one.</summary>
        public void SendToClients(ref FastBufferWriter w, NetworkDelivery d, ulong except = ulong.MaxValue)
        {
            if (_nm == null || !_nm.IsServer) return;
            _targets.Clear();
            var ids = _nm.ConnectedClientsIds;
            for (int i = 0; i < ids.Count; i++)
                if (ids[i] != _nm.LocalClientId && ids[i] != except) _targets.Add(ids[i]);
            if (_targets.Count > 0) _nm.CustomMessagingManager.SendNamedMessage(MessageName, _targets, w, d);
        }

        public int RttMs()
        {
            if (_nm == null || !_nm.IsListening || _nm.IsServer) return 0;
            return (int)_transport.GetCurrentRtt(NetworkManager.ServerClientId);
        }

        // ---------------------------------------------------------------- helpers

        private void SetStatus(Status s, string text)
        {
            State = s;
            StatusText = text;
            Changed?.Invoke();
        }

        private void Fail(string message)
        {
            if (State == Status.Busy) State = Status.Offline;
            StatusText = message;
            _ctx.Sfx.PlayUi(SfxId.UiError, 0.8f);
            _ctx.UI.Toast(message);
            Changed?.Invoke();
        }

        /// <summary>Stops a join or create that is still waiting.</summary>
        public void Cancel()
        {
            if (State != Status.Busy) return;
            SetStatus(Status.Offline, "");
        }

        private static string Friendly(Exception e)
        {
            string m = e.ToString();
            if (e is AuthenticationException || m.Contains("Authentication")) return "Could not sign in. Check your internet.";
            if (m.Contains("not found") || m.Contains("NotFound") || m.Contains("EntityNotFound") || m.Contains("InvalidJoinCode")) return "No room with that code";
            if (m.Contains("full") || m.Contains("Full")) return "That room is full";
            if (m.Contains("Network") || m.Contains("network") || m.Contains("Unreachable") || m.Contains("timed out")) return "No connection. Check your internet.";
            return "Could not connect. Try again.";
        }

        private void OnDestroy()
        {
            if (_nm != null && _nm.IsListening) _nm.Shutdown();
        }
    }

    /// <summary>Reads "-name value" switches from the command line (dev testing of online play).</summary>
    public static class CommandLine
    {
        /// <summary>Switches added at runtime (editor tests), treated as if typed on the command line.</summary>
        public static readonly HashSet<string> Extra = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public static string Value(string name)
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase)) return args[i + 1];
            return null;
        }

        public static bool Has(string name)
        {
            if (Extra.Contains(name)) return true;
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }
    }
}
