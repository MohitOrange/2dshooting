// FastBufferWriter/Reader keep all state behind one pointer, so the copy that using() disposes is the
// same buffer we write through by ref (Netcode's documented pattern). The compiler cannot know that.
#pragma warning disable CS0728

using System;
using System.Collections.Generic;
using System.Text;
using Unity.Netcode;
using UnityEngine;

namespace NeonBrawl
{
    public enum ChatKind : byte { Text = 0, Phrase = 1, Emote = 2 }

    public struct ChatLine
    {
        public ChatKind Kind;
        public int Index;       // phrase or emote number
        public string Text;     // typed text (Kind == Text)
        public string Name;
        public Color Color;
        public ulong Sender;
        public int FighterId;   // -1 when not in a match
        public bool Mine;
        public bool Quiet;      // bubble only, not in the chat list (bot reactions)
        public float Time;
    }

    /// <summary>
    /// Room and match chat: typed text, quick phrases and emote faces. Online, every line goes through the
    /// host, which checks spam, filters words and relays it to everyone. Offline, lines stay local (bots
    /// still react to kills with emotes). Muting a player hides their lines on this device only.
    /// </summary>
    public sealed class ChatHub : MonoBehaviour
    {
        public const int MaxLines = 40;
        public const int MaxLength = 60;
        public const ulong BotSender = ulong.MaxValue;

        public static readonly string[] Phrases =
            { "HI!", "GG!", "READY!", "START!", "ONE MORE?", "NICE SHOT!", "HELP!", "OOPS!", "THANKS!", "LET'S GO!", "SORRY!", "WELL PLAYED!" };
        public static readonly int[] RoomPhrases = { 0, 2, 3, 4, 1, 8 };
        public static readonly int[] MatchPhrases = { 1, 5, 6, 7, 8, 9 };

        private static readonly int[] WinEmotes = { EmoteArt.Lol, EmoteArt.Cool, EmoteArt.Happy, EmoteArt.Cheeky };
        private static readonly int[] LoseEmotes = { EmoteArt.Sad, EmoteArt.Angry, EmoteArt.Wow };

        private struct BotLine
        {
            public float At;
            public int FighterId;
            public int Emote;
        }

        public readonly List<ChatLine> Lines = new List<ChatLine>(MaxLines);
        public readonly HashSet<ulong> Muted = new HashSet<ulong>();
        public int Unread;

        /// <summary>A line arrived (not called for muted players or quiet bot bubbles).</summary>
        public event Action<ChatLine> Added;

        private GameContext _ctx;
        private readonly Queue<float> _sent = new Queue<float>(8);
        private float _lastSent = -99f;
        private readonly Dictionary<ulong, Vector2> _buckets = new Dictionary<ulong, Vector2>(); // host: (tokens, time)
        private readonly List<BotLine> _botQueue = new List<BotLine>(8);
        private readonly Dictionary<int, float> _botNext = new Dictionary<int, float>();
        private float _botAny;
        private bool _wasOnline;

        public void Init(GameContext ctx)
        {
            _ctx = ctx;
            ctx.Net.Changed += OnNetChanged;
        }

        public static string TextOf(in ChatLine l) =>
            l.Kind == ChatKind.Phrase ? Phrases[Mathf.Clamp(l.Index, 0, Phrases.Length - 1)] : (l.Text ?? "");

        public bool IsMuted(ulong clientId) => Muted.Contains(clientId);

        public bool ToggleMute(ulong clientId)
        {
            if (!Muted.Remove(clientId)) Muted.Add(clientId);
            return Muted.Contains(clientId);
        }

        // ---------------------------------------------------------------- sending

        /// <summary>The local player says something. Returns false when it was not sent (spam, empty).</summary>
        public bool Say(ChatKind kind, int index, string text = null)
        {
            if (kind == ChatKind.Text)
            {
                text = ChatFilter.Clean(text);
                if (text.Length == 0) return false;
                index = 0;
            }
            else text = "";
            if (!Valid(kind, index)) return false;

            float now = Time.unscaledTime;
            while (_sent.Count > 0 && now - _sent.Peek() > 8f) _sent.Dequeue();
            if (_sent.Count >= 5 || now - _lastSent < 0.6f)
            {
                if (_ctx.Match != null && _ctx.Match.Local != null) _ctx.Events.RaiseToast("SLOW DOWN!");
                else _ctx.UI.Toast("Slow down!");
                _ctx.Sfx.PlayUi(SfxId.UiError, 0.6f);
                return false;
            }
            _sent.Enqueue(now);
            _lastSent = now;

            var net = _ctx.Net;
            if (net != null && net.InRoom)
            {
                if (net.IsHost) HostPost(net.LocalId, kind, index, text);
                else
                {
                    var w = NetIO.Writer(Msg.Chat, 16 + text.Length * 4);
                    using (w)
                    {
                        w.WriteValueSafe((byte)kind);
                        w.WriteValueSafe((byte)index);
                        w.WriteValueSafe(text);
                        net.SendToHost(ref w, NetworkDelivery.ReliableSequenced);
                    }
                }
            }
            else
            {
                var local = _ctx.Match != null ? _ctx.Match.Local : null;
                Post(new ChatLine
                {
                    Kind = kind, Index = index, Text = text, Mine = true,
                    Name = local != null ? local.DisplayName : _ctx.Profile.Data.Name,
                    Color = _ctx.Profile.OutfitColor,
                    FighterId = local != null ? local.Id : -1
                });
            }
            return true;
        }

        private static bool Valid(ChatKind kind, int index)
        {
            switch (kind)
            {
                case ChatKind.Text: return true;
                case ChatKind.Phrase: return (uint)index < Phrases.Length;
                case ChatKind.Emote: return (uint)index < EmoteArt.Count;
                default: return false;
            }
        }

        // ---------------------------------------------------------------- host

        private void HostPost(ulong sender, ChatKind kind, int index, string text)
        {
            var net = _ctx.Net;
            bool self = sender == net.LocalId;
            if (!self && !Spend(sender)) return;
            if (kind == ChatKind.Text)
            {
                text = ChatFilter.Clean(text);
                if (text.Length == 0) return;
            }
            else text = "";
            if (!Valid(kind, index)) return;

            int mi = -1;
            for (int i = 0; i < net.Members.Count; i++)
                if (net.Members[i].ClientId == sender) { mi = i; break; }
            if (mi < 0) return;
            var member = net.Members[mi];
            int fid = -1;
            var m = _ctx.Match;
            if (m != null && m.Online)
            {
                var f = m.FighterOwnedBy(sender);
                if (f != null) fid = f.Id;
            }
            var line = new ChatLine
            {
                Kind = kind, Index = index, Text = text, Sender = sender, FighterId = fid,
                Name = member.Name, Color = member.Look.Outfit
            };
            Broadcast(line);
            line.Mine = self;
            Post(line);
        }

        /// <summary>Host spam guard: 6 lines in a burst, then one every 1.2 s.</summary>
        private bool Spend(ulong id)
        {
            float now = Time.unscaledTime;
            if (!_buckets.TryGetValue(id, out var b)) b = new Vector2(6f, now);
            float tokens = Mathf.Min(6f, b.x + (now - b.y) / 1.2f);
            bool ok = tokens >= 1f;
            _buckets[id] = new Vector2(ok ? tokens - 1f : tokens, now);
            return ok;
        }

        private void Broadcast(ChatLine l)
        {
            string text = l.Text ?? "", name = l.Name ?? "";
            var w = NetIO.Writer(Msg.Chat, 48 + (text.Length + name.Length) * 4);
            using (w)
            {
                w.WriteValueSafe((byte)l.Kind);
                w.WriteValueSafe((byte)l.Index);
                w.WriteValueSafe(text);
                w.WriteValueSafe(l.Sender);
                w.WriteValueSafe((short)l.FighterId);
                w.WriteValueSafe(name);
                w.WriteValueSafe((Color32)l.Color);
                w.WriteValueSafe(l.Quiet);
                _ctx.Net.SendToClients(ref w, NetworkDelivery.ReliableSequenced);
            }
        }

        // ---------------------------------------------------------------- receiving

        /// <summary>A chat message from the network (client -> host, or host -> clients).</summary>
        public void OnNet(ulong sender, ref FastBufferReader r)
        {
            var net = _ctx.Net;
            r.ReadValueSafe(out byte kind);
            r.ReadValueSafe(out byte index);
            r.ReadValueSafe(out string text);
            if (net.IsHost)
            {
                HostPost(sender, (ChatKind)kind, index, text);
                return;
            }
            r.ReadValueSafe(out ulong from);
            r.ReadValueSafe(out short fid);
            r.ReadValueSafe(out string name);
            r.ReadValueSafe(out Color32 color);
            r.ReadValueSafe(out bool quiet);
            if (!Valid((ChatKind)kind, index)) return;
            Post(new ChatLine
            {
                Kind = (ChatKind)kind, Index = index, Text = text ?? "", Sender = from, FighterId = fid,
                Name = name ?? "", Color = color, Quiet = quiet, Mine = from == net.LocalId
            });
        }

        private void Post(ChatLine l)
        {
            if (!l.Mine && l.Sender != BotSender && Muted.Contains(l.Sender)) return;
            if (l.Kind == ChatKind.Text && !l.Mine && !_ctx.Prefs.TextChat) return;
            l.Time = Time.unscaledTime;

            var m = _ctx.Match;
            Fighter f = m != null && l.FighterId >= 0 ? m.FighterById(l.FighterId) : null;
            if (f != null)
            {
                var b = ChatBubble.For(f, _ctx.Art);
                if (l.Kind == ChatKind.Emote) b.ShowEmote(l.Index);
                else b.ShowText(TextOf(l));
                if (f.IsLocal || f.Visual.Alpha > 0.1f) _ctx.Sfx.Play(SfxId.UiTap, f.Position, 0.55f, 0.15f);
            }
            else if (!l.Quiet && !l.Mine) _ctx.Sfx.PlayUi(SfxId.UiTap, 0.45f);

            if (l.Quiet) return;
            Lines.Add(l);
            if (Lines.Count > MaxLines) Lines.RemoveAt(0);
            if (!l.Mine) Unread++;
            Added?.Invoke(l);
        }

        // ---------------------------------------------------------------- bots

        /// <summary>Authority only (offline or host): bots sometimes react to a kill with an emote.</summary>
        public void OnKill(Fighter killer, Fighter victim)
        {
            var m = _ctx.Match;
            if (m == null || !m.IsAuthority || victim == null) return;
            if (killer != null && killer != victim && killer.IsBot)
            {
                if (UnityEngine.Random.value < 0.25f) QueueBot(killer, WinEmotes[UnityEngine.Random.Range(0, WinEmotes.Length)]);
            }
            else if (victim.IsBot && UnityEngine.Random.value < 0.2f)
                QueueBot(victim, killer == null || killer == victim ? EmoteArt.Wow : LoseEmotes[UnityEngine.Random.Range(0, LoseEmotes.Length)]);
            if (victim.IsBot && killer != null && killer != victim && !killer.IsBot && UnityEngine.Random.value < 0.1f)
                QueueBot(victim, LoseEmotes[UnityEngine.Random.Range(0, 2)]);
        }

        private void QueueBot(Fighter f, int emote)
        {
            float now = Time.unscaledTime;
            if (now < _botAny) return;
            if (_botNext.TryGetValue(f.Id, out float next) && now < next) return;
            _botAny = now + 2.5f;
            _botNext[f.Id] = now + 12f;
            _botQueue.Add(new BotLine { At = now + UnityEngine.Random.Range(0.4f, 1.1f), FighterId = f.Id, Emote = emote });
        }

        private void Update()
        {
            for (int i = _botQueue.Count - 1; i >= 0; i--)
            {
                if (Time.unscaledTime < _botQueue[i].At) continue;
                var q = _botQueue[i];
                _botQueue.RemoveAt(i);
                var m = _ctx.Match;
                var f = m != null ? m.FighterById(q.FighterId) : null;
                if (f == null || !f.IsBot) continue;
                var line = new ChatLine
                {
                    Kind = ChatKind.Emote, Index = q.Emote, Text = "", Name = f.DisplayName, Color = f.OutfitColor,
                    Sender = BotSender, FighterId = f.Id, Quiet = true
                };
                if (m.Online && _ctx.Net.IsHost) Broadcast(line);
                Post(line);
            }
        }

        private void OnNetChanged()
        {
            bool online = _ctx.Net.State != NetSession.Status.Offline;
            if (_wasOnline && !online)
            {
                Lines.Clear();
                Muted.Clear();
                _buckets.Clear();
                Unread = 0;
            }
            _wasOnline = online;
        }
    }

    /// <summary>Cleans typed chat: no rich-text tags or emoji (the font has none), single spaces, bad words masked.</summary>
    public static class ChatFilter
    {
        // matched anywhere inside a word
        private static readonly string[] Inside =
        {
            "fuck", "shit", "bitch", "cunt", "pussy", "whore", "slut", "nigg", "faggot", "retard", "bastard", "asshole", "motherf", "dickhead",
            "chutiya", "chutia", "madarchod", "maderchod", "behenchod", "bhenchod", "benchod", "bhosd", "gandu", "randi", "bsdk", "lauda", "lavda"
        };

        // matched only as a whole word
        private static readonly string[] Whole =
            { "ass", "dick", "fk", "fck", "fuk", "stfu", "kys", "cock", "porn", "sex", "nazi", "rape", "bc", "mc", "lund", "loda", "chod", "fag" };

        public static string Clean(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var sb = new StringBuilder(s.Length);
            bool space = false;
            for (int i = 0; i < s.Length && sb.Length < ChatHub.MaxLength; i++)
            {
                char c = s[i];
                if (char.IsControl(c) || char.IsSurrogate(c) || c == '<' || c == '>' || c == '​' || c == '﻿') continue;
                if (char.IsWhiteSpace(c))
                {
                    if (!space && sb.Length > 0) sb.Append(' ');
                    space = true;
                    continue;
                }
                space = false;
                sb.Append(c);
            }
            return Mask(sb.ToString().Trim());
        }

        /// <summary>Replaces the letters of bad words with '*' (also catches 0/1/3/4/5/@/$ swaps).</summary>
        public static string Mask(string t)
        {
            if (string.IsNullOrEmpty(t)) return t ?? "";
            var n = t.ToLowerInvariant().ToCharArray();
            if (n.Length != t.Length) return t;
            for (int i = 0; i < n.Length; i++)
            {
                switch (n[i])
                {
                    case '0': n[i] = 'o'; break;
                    case '1': case '!': n[i] = 'i'; break;
                    case '3': n[i] = 'e'; break;
                    case '4': case '@': n[i] = 'a'; break;
                    case '5': case '$': n[i] = 's'; break;
                    case '7': n[i] = 't'; break;
                }
            }
            string norm = new string(n);
            bool[] hide = null;
            foreach (var w in Inside)
            {
                int at = 0;
                while ((at = norm.IndexOf(w, at, StringComparison.Ordinal)) >= 0)
                {
                    hide = hide ?? new bool[t.Length];
                    for (int k = 0; k < w.Length; k++) hide[at + k] = true;
                    at += w.Length;
                }
            }
            foreach (var w in Whole)
            {
                int at = 0;
                while ((at = norm.IndexOf(w, at, StringComparison.Ordinal)) >= 0)
                {
                    bool startOk = at == 0 || !char.IsLetter(norm[at - 1]);
                    bool endOk = at + w.Length >= norm.Length || !char.IsLetter(norm[at + w.Length]);
                    if (startOk && endOk)
                    {
                        hide = hide ?? new bool[t.Length];
                        for (int k = 0; k < w.Length; k++) hide[at + k] = true;
                    }
                    at += w.Length;
                }
            }
            if (hide == null) return t;
            var o = t.ToCharArray();
            for (int i = 0; i < o.Length; i++) if (hide[i] && !char.IsWhiteSpace(o[i])) o[i] = '*';
            return new string(o);
        }
    }
}
