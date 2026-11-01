using NeonBrawl.Core;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace NeonBrawl
{
    /// <summary>
    /// Every message is one named Netcode message ("nb") that starts with this byte.
    /// Clients own their fighter (movement, aim, firing, hit detection); the host owns health, kills,
    /// pickups, props, bots, respawns and the clock.
    /// </summary>
    public enum Msg : byte
    {
        // client -> host
        Hello = 1,       // name, look
        State = 2,       // own fighter pose (unreliable, 20 Hz)
        HitClaim = 3,    // "my shot hit fighter X for N"
        PropHit = 4,     // "my shot hit crate/barrel N"
        TakePickup = 5,  // "I am standing on pickup N"
        Loaded = 6,      // match scene built, ready for the countdown

        // both ways (client -> host, host relays to the others)
        Shot = 10,       // fire / knife / grenade visuals
        Chat = 11,       // typed text, quick phrase or emote (room and match)

        // host -> clients
        Room = 20,
        Start = 21,
        Snapshot = 22,   // unreliable, 20 Hz
        Hit = 23,
        Kill = 24,
        Spawn = 25,
        PickupTaken = 26,
        PickupSpawn = 27,
        PickupDrop = 28,
        Prop = 29,
        End = 30,
        Joined = 31,     // a fighter joined a running match
        Left = 32,       // a fighter left the match
        Closing = 33,    // the host is closing the room

        // both ways
        Ping = 40,       // "still here" every 2 s, so a dead link is noticed (unreliable)
    }

    public enum ShotKind : byte { Fire = 0, Melee = 1, Grenade = 2 }

    /// <summary>One fighter in an online roster, decided by the host.</summary>
    public struct FighterSpec
    {
        public int Id;
        public ulong Owner;
        public bool Bot;
        public string Name;
        public int Team;
        public FighterLook Look;
        public Color NameColor;
        public Vector2 Spawn;
    }

    /// <summary>A player waiting in the room.</summary>
    public struct RoomMember
    {
        public ulong ClientId;
        public string Name;
        public FighterLook Look;
        /// <summary>Unity player id (for friend requests and recent players); empty on LAN.</summary>
        public string PlayerId;
    }

    /// <summary>Read/write helpers so every message uses the same layouts.</summary>
    public static class NetIO
    {
        public static FastBufferWriter Writer(Msg type, int size = 256)
        {
            var w = new FastBufferWriter(size, Allocator.Temp, 64 * 1024);
            w.WriteValueSafe((byte)type);
            return w;
        }

        public static ushort PackAngle(Vector2 dir) =>
            (ushort)Mathf.RoundToInt(Mathf.Repeat(Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg, 360f) / 360f * 65535f);

        public static Vector2 UnpackAngle(ushort a)
        {
            float r = a / 65535f * Mathf.PI * 2f;
            return new Vector2(Mathf.Cos(r), Mathf.Sin(r));
        }

        public static void Write(ref FastBufferWriter w, in MatchSettings s, int map)
        {
            w.WriteValueSafe((byte)s.Mode);
            w.WriteValueSafe((short)s.KillLimit);
            w.WriteValueSafe(s.TimeLimit);
            w.WriteValueSafe((byte)s.BotCount);
            w.WriteValueSafe((byte)s.Difficulty);
            w.WriteValueSafe(s.Night);
            w.WriteValueSafe(s.Fog);
            w.WriteValueSafe((byte)map);
        }

        public static MatchSettings ReadSettings(ref FastBufferReader r, out int map)
        {
            var s = new MatchSettings();
            r.ReadValueSafe(out byte mode); s.Mode = (MatchMode)mode;
            r.ReadValueSafe(out short kills); s.KillLimit = kills;
            r.ReadValueSafe(out s.TimeLimit);
            r.ReadValueSafe(out byte bots); s.BotCount = bots;
            r.ReadValueSafe(out byte diff); s.Difficulty = diff;
            r.ReadValueSafe(out s.Night);
            r.ReadValueSafe(out s.Fog);
            r.ReadValueSafe(out byte m); map = m;
            return s;
        }

        public static void Write(ref FastBufferWriter w, in FighterLook l)
        {
            w.WriteValueSafe((Color32)l.Outfit);
            w.WriteValueSafe((Color32)l.Skin);
            w.WriteValueSafe((Color32)l.Hair);
            w.WriteValueSafe((byte)l.Body);
            w.WriteValueSafe((byte)l.Head);
            w.WriteValueSafe((byte)l.HairStyle);
            w.WriteValueSafe((byte)l.Back);
        }

        public static FighterLook ReadLook(ref FastBufferReader r)
        {
            var l = new FighterLook();
            r.ReadValueSafe(out Color32 c); l.Outfit = c;
            r.ReadValueSafe(out c); l.Skin = c;
            r.ReadValueSafe(out c); l.Hair = c;
            r.ReadValueSafe(out byte b); l.Body = b;
            r.ReadValueSafe(out b); l.Head = b;
            r.ReadValueSafe(out b); l.HairStyle = b;
            r.ReadValueSafe(out b); l.Back = b;
            return l;
        }

        public static void Write(ref FastBufferWriter w, in FighterSpec s)
        {
            w.WriteValueSafe((byte)s.Id);
            w.WriteValueSafe(s.Owner);
            w.WriteValueSafe(s.Bot);
            w.WriteValueSafe(s.Name ?? "");
            w.WriteValueSafe((sbyte)s.Team);
            Write(ref w, s.Look);
            w.WriteValueSafe((Color32)s.NameColor);
            w.WriteValueSafe(s.Spawn);
        }

        public static FighterSpec ReadSpec(ref FastBufferReader r)
        {
            var s = new FighterSpec();
            r.ReadValueSafe(out byte id); s.Id = id;
            r.ReadValueSafe(out s.Owner);
            r.ReadValueSafe(out s.Bot);
            r.ReadValueSafe(out s.Name);
            r.ReadValueSafe(out sbyte team); s.Team = team;
            s.Look = ReadLook(ref r);
            r.ReadValueSafe(out Color32 nc); s.NameColor = nc;
            r.ReadValueSafe(out s.Spawn);
            return s;
        }

        public static string CleanName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "PLAYER";
            name = name.Trim().ToUpperInvariant();
            var sb = new System.Text.StringBuilder(name.Length);
            for (int i = 0; i < name.Length && sb.Length < 14; i++)
            {
                char c = name[i];
                if ((c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == ' ' || c == '_' || c == '-' || c == '.') sb.Append(c);
            }
            return sb.Length == 0 ? "PLAYER" : ChatFilter.Mask(sb.ToString());
        }
    }

    /// <summary>
    /// Turns a sender's clock into ours. Tracks the smallest seen (our time - their time), which is the
    /// fastest packet, and drifts slowly upward so lag spikes do not wreck smoothing.
    /// </summary>
    public sealed class NetClock
    {
        private bool _init;
        private float _offset;

        public float ToLocal(float remoteTime)
        {
            float est = Time.unscaledTime - remoteTime;
            if (!_init || est < _offset - 0.5f || est > _offset + 1.5f) { _offset = est; _init = true; }
            else if (est < _offset) _offset = est;
            else _offset += (est - _offset) * 0.02f;
            return remoteTime + _offset;
        }

        public void Reset() => _init = false;
    }
}
