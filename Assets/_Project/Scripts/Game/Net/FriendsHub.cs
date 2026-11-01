using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.Friends;
using Unity.Services.Friends.Models;
using Unity.Services.Friends.Notifications;
using UnityEngine;
using UnityEngine.Scripting;

namespace NeonBrawl
{
    /// <summary>What friends see about you (Friends presence "activity"). Kept tiny; read through JSON.</summary>
    [Preserve, Serializable]
    public sealed class NbActivity
    {
        [Preserve] public NbActivity() { }
        /// <summary>"menu", "room" or "match".</summary>
        public string State { get; set; } = "menu";
        /// <summary>Room code friends may join (empty when the room is full or you are offline).</summary>
        public string Room { get; set; } = "";
        public int Players { get; set; }
    }

    /// <summary>A message between friends (only invites so far).</summary>
    [Preserve, Serializable]
    public sealed class NbFriendMessage
    {
        [Preserve] public NbFriendMessage() { }
        public string Kind { get; set; } = "invite";
        public string Room { get; set; } = "";
        public string From { get; set; } = "";
    }

    /// <summary>One person in a list: friend, request or recent player.</summary>
    public sealed class FriendInfo
    {
        public string Id;
        public string Name;          // account name, e.g. "MOHIT#4821"
        public Availability Availability;
        public NbActivity Activity;
        public DateTime LastSeen;

        public bool Online => Availability == Availability.Online || Availability == Availability.Busy || Availability == Availability.Away;
        public bool InRoom => Online && Activity != null && !string.IsNullOrEmpty(Activity.Room);

        public string StatusText
        {
            get
            {
                if (!Online) return "OFFLINE";
                if (Availability == Availability.Away) return "AWAY";
                if (Activity == null) return "ONLINE";
                switch (Activity.State)
                {
                    case "match": return "IN A MATCH";
                    case "room": return "IN A ROOM · " + Activity.Players + "/" + NetSession.MaxPlayers;
                    default: return "ONLINE";
                }
            }
        }
    }

    /// <summary>An invite that arrived from a friend.</summary>
    public struct FriendInvite
    {
        public string FromId, FromName, Room;
        public float At;
    }

    /// <summary>
    /// Friends through Unity Friends: your friend code ("NAME#1234"), friends with live online status,
    /// incoming / sent requests, recent players from rooms, and room invites. A friend in a room shows
    /// its code in their status, so "JOIN" works in one tap; "+" invites them to yours.
    /// Everything from the service is handed to the main thread first.
    /// </summary>
    public sealed class FriendsHub : MonoBehaviour
    {
        public enum Phase { Offline, Connecting, Ready, Failed }

        private const int MaxRecent = 12;
        private const string RecentKey = "nb.recent";

        public Phase State { get; private set; } = Phase.Offline;
        /// <summary>Your friend code, e.g. "MOHIT#4821" (empty until signed in).</summary>
        public string MyCode { get; private set; } = "";
        public readonly List<FriendInfo> Friends = new List<FriendInfo>();
        public readonly List<FriendInfo> Incoming = new List<FriendInfo>();
        public readonly List<FriendInfo> Outgoing = new List<FriendInfo>();
        public readonly List<FriendInfo> Recent = new List<FriendInfo>();
        public int OnlineCount { get; private set; }

        /// <summary>Lists or statuses changed.</summary>
        public event Action Changed;
        /// <summary>A friend invited you to their room.</summary>
        public event Action<FriendInvite> Invited;

        private GameContext _ctx;
        private readonly Queue<Action> _main = new Queue<Action>();
        private readonly List<string> _pendingInvites = new List<string>();
        private bool _subscribed, _presenceDirty, _paused;
        private float _presenceAt, _retryAt = -1f, _refreshAt, _pendingSince;
        private string _lastPresence = "";

        public void Init(GameContext ctx)
        {
            _ctx = ctx;
            LoadRecent();
            _ctx.Net.Changed += () => _presenceDirty = true;
        }

        /// <summary>Signs in (shared with online play) and connects to Friends. Safe to call again.</summary>
        public async void Connect()
        {
            if (State == Phase.Connecting || State == Phase.Ready) return;
            State = Phase.Connecting;
            Raise();
            try
            {
                await UgsBoot.SignInAsync();
                var data = _ctx.Profile.Data;
                if (string.IsNullOrEmpty(data.Name) || data.Name == "PLAYER")
                {
                    // a real name before the friend code is made ("PLAYER#1234" would be one of thousands)
                    data.Name = "BRAWLER" + UnityEngine.Random.Range(100, 1000);
                    _ctx.Profile.Save();
                }
                MyCode = await UgsBoot.SyncPlayerNameAsync(data.Name);
                await FriendsService.Instance.InitializeAsync(new Unity.Services.Friends.Options.InitializeOptions()
                    .WithEvents(true).WithMemberPresence(true).WithMemberProfile(true));
                if (!_subscribed)
                {
                    _subscribed = true;
                    var f = FriendsService.Instance;
                    f.RelationshipAdded += e => Post(Rebuild);
                    f.RelationshipDeleted += e => Post(Rebuild);
                    f.PresenceUpdated += e => Post(Rebuild);
                    f.MessageReceived += e => Post(() => OnMessage(e));
                }
                State = Phase.Ready;
                _presenceDirty = true;
                Rebuild();
                Debug.Log("[Friends] ready as " + MyCode + " with " + Friends.Count + " friends");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Friends] connect failed: " + e.Message);
                State = Phase.Failed;
                _retryAt = Time.unscaledTime + 30f;
                Raise();
            }
        }

        /// <summary>The game name changed: your friend code follows it.</summary>
        public async void SyncName()
        {
            if (State != Phase.Ready) return;
            MyCode = await UgsBoot.SyncPlayerNameAsync(_ctx.Profile.Data.Name);
            Raise();
        }

        // ---------------------------------------------------------------- actions

        /// <summary>Sends a friend request by code ("NAME#1234").</summary>
        public async void AddByCode(string code, Action<bool, string> done)
        {
            code = (code ?? "").Trim().Replace(' ', '_').ToUpperInvariant();
            if (code.IndexOf('#') <= 0) { done?.Invoke(false, "Type the full code, like NAME#1234"); return; }
            if (string.Equals(code, MyCode, StringComparison.OrdinalIgnoreCase)) { done?.Invoke(false, "That is your own code"); return; }
            if (!await Ready()) { done?.Invoke(false, "Friends are offline. Check your internet."); return; }
            try
            {
                await FriendsService.Instance.AddFriendByNameAsync(code);
                Rebuild();
                done?.Invoke(true, "Request sent to " + UgsBoot.Display(code));
            }
            catch (Exception e) { done?.Invoke(false, Friendly(e, "No player with that code")); }
        }

        /// <summary>Adds someone you played with (their player id is known from the room).</summary>
        public async void AddById(string id, string name)
        {
            if (string.IsNullOrEmpty(id) || !await Ready()) return;
            try
            {
                await FriendsService.Instance.AddFriendAsync(id);
                Rebuild();
                _ctx.UI.Toast("Friend request sent to " + UgsBoot.ShortName(name));
            }
            catch (Exception e) { _ctx.UI.Toast(Friendly(e, "Could not send the request")); }
        }

        public async void Accept(FriendInfo f)
        {
            if (!await Ready()) return;
            try
            {
                await FriendsService.Instance.AddFriendAsync(f.Id);
                Rebuild();
                _ctx.Sfx.PlayUi(SfxId.UiLevelUp, 0.6f);
                _ctx.UI.Toast(UgsBoot.ShortName(f.Name) + " is now your friend!");
            }
            catch (Exception e) { _ctx.UI.Toast(Friendly(e, "Could not accept")); }
        }

        public async void Decline(FriendInfo f)
        {
            if (!await Ready()) return;
            try { await FriendsService.Instance.DeleteIncomingFriendRequestAsync(f.Id); Rebuild(); }
            catch (Exception e) { _ctx.UI.Toast(Friendly(e, "Could not decline")); }
        }

        public async void CancelRequest(FriendInfo f)
        {
            if (!await Ready()) return;
            try { await FriendsService.Instance.DeleteOutgoingFriendRequestAsync(f.Id); Rebuild(); }
            catch (Exception e) { _ctx.UI.Toast(Friendly(e, "Could not cancel")); }
        }

        public async void Remove(FriendInfo f)
        {
            if (!await Ready()) return;
            try { await FriendsService.Instance.DeleteFriendAsync(f.Id); Rebuild(); }
            catch (Exception e) { _ctx.UI.Toast(Friendly(e, "Could not remove")); }
        }

        /// <summary>
        /// Invites a friend to play: uses your current room, or makes a private one first and sends the
        /// invite as soon as it has a code.
        /// </summary>
        public void Invite(FriendInfo f)
        {
            if (f == null || !f.Online) { _ctx.UI.Toast("Your friend is offline"); return; }
            var net = _ctx.Net;
            if (net.InRoom && !string.IsNullOrEmpty(net.Code) && net.Code != "LAN")
            {
                SendInvite(f.Id, f.Name, net.Code);
                return;
            }
            if (_pendingInvites.Count == 0) _pendingSince = Time.unscaledTime;
            if (!_pendingInvites.Contains(f.Id)) _pendingInvites.Add(f.Id);
            if (net.State == NetSession.Status.Offline)
            {
                _ctx.App.OpenOnline();
                net.CreateRoom();
            }
            _ctx.UI.Toast("Making a room for you and " + UgsBoot.ShortName(f.Name) + "…");
        }

        public bool IsFriend(string id) => Find(Friends, id) != null;
        public bool IsPending(string id) => Find(Outgoing, id) != null || Find(Incoming, id) != null;

        private async void SendInvite(string id, string name, string room)
        {
            try
            {
                await FriendsService.Instance.MessageAsync(id, new NbFriendMessage { Kind = "invite", Room = room, From = MyCode });
                _ctx.Sfx.PlayUi(SfxId.UiOpen, 0.6f);
                _ctx.UI.Toast("Invite sent to " + UgsBoot.ShortName(name));
            }
            catch (Exception e) { _ctx.UI.Toast(Friendly(e, "Could not send the invite")); }
        }

        private void OnMessage(IMessageReceivedEvent e)
        {
            NbFriendMessage m;
            try { m = e.GetAs<NbFriendMessage>(); }
            catch (Exception) { return; }
            if (m == null || m.Kind != "invite" || string.IsNullOrEmpty(m.Room)) return;
            var from = Find(Friends, e.UserId);
            string name = from != null ? from.Name : m.From;
            _ctx.Sfx.PlayUi(SfxId.UiLevelUp, 0.7f);
            _ctx.Haptics?.Pulse(30);
            Invited?.Invoke(new FriendInvite { FromId = e.UserId, FromName = name, Room = m.Room, At = Time.unscaledTime });
        }

        // ---------------------------------------------------------------- recent players

        /// <summary>Someone shared a room with you: remember them for the ADD page.</summary>
        public void AddRecent(string id, string name)
        {
            if (string.IsNullOrEmpty(id) || id == UgsBoot.PlayerId) return;
            for (int i = 0; i < Recent.Count; i++)
                if (Recent[i].Id == id) { Recent.RemoveAt(i); break; }
            Recent.Insert(0, new FriendInfo { Id = id, Name = string.IsNullOrEmpty(name) ? "PLAYER" : name });
            while (Recent.Count > MaxRecent) Recent.RemoveAt(Recent.Count - 1);
            SaveRecent();
            Raise();
        }

        private void LoadRecent()
        {
            Recent.Clear();
            string raw = PlayerPrefs.GetString(RecentKey, "");
            foreach (var part in raw.Split(';'))
            {
                int bar = part.IndexOf('|');
                if (bar > 0) Recent.Add(new FriendInfo { Id = part.Substring(0, bar), Name = part.Substring(bar + 1) });
            }
        }

        private void SaveRecent()
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < Recent.Count; i++)
            {
                if (i > 0) sb.Append(';');
                sb.Append(Recent[i].Id).Append('|').Append(Recent[i].Name.Replace(";", "").Replace("|", ""));
            }
            PlayerPrefs.SetString(RecentKey, sb.ToString());
            PlayerPrefs.Save();
        }

        // ---------------------------------------------------------------- state

        private void Rebuild()
        {
            if (State != Phase.Ready) return;
            var f = FriendsService.Instance;
            Fill(Friends, f.Friends);
            Fill(Incoming, f.IncomingFriendRequests);
            Fill(Outgoing, f.OutgoingFriendRequests);
            // online first (rooms you can join on top), then by name
            Friends.Sort((a, b) =>
            {
                int ra = a.InRoom ? 0 : a.Online ? 1 : 2, rb = b.InRoom ? 0 : b.Online ? 1 : 2;
                return ra != rb ? ra.CompareTo(rb) : string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
            });
            int online = 0;
            for (int i = 0; i < Friends.Count; i++) if (Friends[i].Online) online++;
            OnlineCount = online;
            Raise();
        }

        private static void Fill(List<FriendInfo> list, IReadOnlyList<Relationship> source)
        {
            list.Clear();
            if (source == null) return;
            for (int i = 0; i < source.Count; i++)
            {
                var m = source[i].Member;
                if (m == null) continue;
                var info = new FriendInfo
                {
                    Id = m.Id,
                    Name = m.Profile != null && !string.IsNullOrEmpty(m.Profile.Name) ? m.Profile.Name : "PLAYER",
                    Availability = m.Presence != null ? m.Presence.Availability : Availability.Unknown,
                    LastSeen = m.Presence != null ? m.Presence.LastSeen : default,
                };
                if (m.Presence != null)
                {
                    try { info.Activity = m.Presence.GetActivity<NbActivity>(); }
                    catch (Exception) { info.Activity = null; }
                }
                list.Add(info);
            }
        }

        private static FriendInfo Find(List<FriendInfo> list, string id)
        {
            for (int i = 0; i < list.Count; i++) if (list[i].Id == id) return list[i];
            return null;
        }

        private async Task<bool> Ready()
        {
            if (State == Phase.Ready) return true;
            if (State != Phase.Connecting) Connect();
            for (int i = 0; i < 600 && State == Phase.Connecting; i++) await Task.Yield();
            return State == Phase.Ready;
        }

        private void Update()
        {
            while (true)
            {
                Action a;
                lock (_main)
                {
                    if (_main.Count == 0) break;
                    a = _main.Dequeue();
                }
                try { a(); } catch (Exception e) { Debug.LogException(e); }
            }

            float now = Time.unscaledTime;
            if (State == Phase.Failed && _retryAt > 0f && now >= _retryAt) { _retryAt = -1f; Connect(); }
            if (State != Phase.Ready) return;

            // a room we made for an invite now has its code: send the invites
            var net = _ctx.Net;
            if (_pendingInvites.Count > 0 && net.InRoom && !string.IsNullOrEmpty(net.Code))
            {
                for (int i = 0; i < _pendingInvites.Count; i++)
                {
                    var f = Find(Friends, _pendingInvites[i]);
                    if (f != null) SendInvite(f.Id, f.Name, net.Code);
                }
                _pendingInvites.Clear();
            }
            else if (_pendingInvites.Count > 0 && now - _pendingSince > 40f) _pendingInvites.Clear(); // the room never opened

            if (_presenceDirty && now - _presenceAt > 1f) PushPresence();
            // statuses arrive as events; a slow full refresh catches anything missed
            if (now >= _refreshAt)
            {
                _refreshAt = now + 60f;
                _ = RefreshQuietly();
            }
        }

        private async Task RefreshQuietly()
        {
            try { await FriendsService.Instance.ForceRelationshipsRefreshAsync(); Rebuild(); }
            catch (Exception) { }
        }

        private async void PushPresence()
        {
            _presenceDirty = false;
            _presenceAt = Time.unscaledTime;
            var net = _ctx.Net;
            var act = new NbActivity();
            if (net.InRoom)
            {
                act.State = net.MatchRunning || (_ctx.Match != null && _ctx.Match.Online) ? "match" : "room";
                act.Players = net.Members.Count;
                // a private room's code goes only to friends; full rooms are not offered
                act.Room = net.Members.Count < NetSession.MaxPlayers && net.Code != "LAN" ? net.Code : "";
            }
            var availability = _paused ? Availability.Away : Availability.Online;
            string key = availability + "|" + act.State + "|" + act.Room + "|" + act.Players;
            if (key == _lastPresence) return;
            _lastPresence = key;
            try { await FriendsService.Instance.SetPresenceAsync(availability, act); }
            catch (Exception e) { Debug.LogWarning("[Friends] presence: " + e.Message); _lastPresence = ""; }
        }

        private void OnApplicationPause(bool paused)
        {
            _paused = paused;
            _presenceDirty = true;
            if (!paused && State == Phase.Ready) _refreshAt = 0f;
        }

        private void OnApplicationQuit()
        {
            if (State != Phase.Ready) return;
            try { _ = FriendsService.Instance.SetPresenceAvailabilityAsync(Availability.Offline); } catch (Exception) { }
        }

        private void Post(Action a) { lock (_main) _main.Enqueue(a); }

        private void Raise() => Changed?.Invoke();

        private static string Friendly(Exception e, string fallback)
        {
            string m = e.Message ?? "";
            if (m.Contains("not found") || m.Contains("NotFound") || m.Contains("404")) return "No player with that code";
            if (m.Contains("already") || m.Contains("Conflict") || m.Contains("409")) return "Already friends or request already sent";
            if (m.Contains("Network") || m.Contains("network") || m.Contains("timed out")) return "No connection. Check your internet.";
            return fallback;
        }
    }
}
