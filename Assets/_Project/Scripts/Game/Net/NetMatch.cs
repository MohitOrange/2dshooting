// FastBufferWriter/Reader keep all state behind one pointer, so the copy that using() disposes is the
// same buffer we write through by ref (Netcode's documented pattern). The compiler cannot know that.
#pragma warning disable CS0728

using System.Collections.Generic;
using NeonBrawl.Core;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace NeonBrawl
{
    /// <summary>
    /// Keeps one online match in sync. Lives on the match object.
    /// Host: sends a snapshot of every fighter 20 times a second, applies (and checks) clients' hit
    /// claims, hands out pickups, and announces hits, kills, spawns, props and the end.
    /// Client: sends its own fighter's pose 20 times a second, claims its hits, and mirrors the host.
    /// Shots are relayed so everyone sees and hears every gun.
    /// Perf: one small message per peer per 50 ms plus events; 0 B GC per frame outside sends.
    /// </summary>
    public sealed class NetMatch : MonoBehaviour
    {
        /// <summary>Remote fighters are drawn this far in the past so there are always two poses to blend.</summary>
        public const float InterpDelay = 0.12f;
        private const float SendInterval = 0.05f;
        private const float LoadTimeout = 10f;
        private const float DamagePerSecond = 700f;
        private const float DamageBurst = 1050f;

        private GameContext _ctx;
        private NetSession _net;
        private MatchController _m;
        private float _sendT;
        private float _loadWait;
        private bool _hold;
        private readonly NetClock _hostClock = new NetClock();
        private readonly Dictionary<ulong, NetClock> _clocks = new Dictionary<ulong, NetClock>();
        private readonly HashSet<ulong> _loaded = new HashSet<ulong>();
        private readonly Dictionary<int, float> _budget = new Dictionary<int, float>();
        private readonly List<int> _keys = new List<int>(12);

        public bool IsHost { get; private set; }
        /// <summary>Host: still waiting for players to finish loading, so the countdown has not started.</summary>
        public bool HoldCountdown => _hold;

        public void Init(GameContext ctx, NetSession net, MatchController m, byte[] joinState)
        {
            _ctx = ctx;
            _net = net;
            _m = m;
            IsHost = net.IsHost;
            if (IsHost)
            {
                _loadWait = LoadTimeout;
                _hold = WaitingFor() > 0;
                m.Pickups.Taken += OnPickupTaken;
                m.Pickups.Dropped += OnPickupDropped;
                m.Pickups.Respawned += OnPickupRespawned;
            }
            else
            {
                if (joinState != null) ApplyJoinState(joinState);
                var w = NetIO.Writer(Msg.Loaded, 8);
                using (w) _net.SendToHost(ref w, NetworkDelivery.ReliableSequenced);
            }
            _net.Match = this; // plays any messages that arrived while loading
        }

        private int WaitingFor()
        {
            int n = 0;
            var roster = _m.Roster;
            for (int i = 0; i < roster.Count; i++)
                if (!roster[i].Bot && roster[i].Owner != _net.LocalId && !_loaded.Contains(roster[i].Owner)) n++;
            return n;
        }

        private void OnDestroy()
        {
            if (_net != null && _net.Match == this) _net.Match = null;
        }

        private void Update()
        {
            if (_m == null) return;
            float dt = Time.unscaledDeltaTime;
            if (IsHost)
            {
                if (_hold)
                {
                    _loadWait -= dt;
                    if (_loadWait <= 0f || WaitingFor() == 0) _hold = false;
                }
                RefillBudgets(dt);
            }
            _sendT -= dt;
            if (_sendT > 0f) return;
            _sendT = Mathf.Max(0f, _sendT + SendInterval);
            if (IsHost) SendSnapshot();
            else SendState();
        }

        private static Vector2 PosOf(Fighter f) => f.IsPuppet ? f.NetLatestPos : f.Position;

        // ================================================================== routing

        public void Handle(Msg type, ulong sender, ref FastBufferReader r)
        {
            if (IsHost)
            {
                switch (type)
                {
                    case Msg.State: HostOnState(sender, ref r); break;
                    case Msg.Shot: HostOnShot(sender, ref r); break;
                    case Msg.HitClaim: HostOnHitClaim(sender, ref r); break;
                    case Msg.PropHit: HostOnPropHit(sender, ref r); break;
                    case Msg.TakePickup: HostOnTakePickup(sender, ref r); break;
                    case Msg.Loaded: HostOnLoaded(sender); break;
                }
                return;
            }
            switch (type)
            {
                case Msg.Snapshot: OnSnapshot(ref r); break;
                case Msg.Shot: OnShotMsg(ref r); break;
                case Msg.Hit: OnHit(ref r); break;
                case Msg.Kill: OnKill(ref r); break;
                case Msg.Spawn: OnSpawn(ref r); break;
                case Msg.PickupTaken: OnPickupTakenMsg(ref r); break;
                case Msg.PickupSpawn:
                    r.ReadValueSafe(out int sid);
                    _m.Pickups.NetRespawn(sid);
                    break;
                case Msg.PickupDrop: OnPickupDropMsg(ref r); break;
                case Msg.Prop:
                {
                    r.ReadValueSafe(out ushort idx);
                    r.ReadValueSafe(out bool alive);
                    r.ReadValueSafe(out Vector2 dir);
                    _m.PropAt(idx)?.NetSetAlive(alive, dir);
                    break;
                }
                case Msg.End: _m.NetEnd(); break;
                case Msg.Joined:
                {
                    var spec = NetIO.ReadSpec(ref r);
                    _m.NetAddFighter(spec, _net.LocalId, false);
                    _ctx.Events.RaiseToast(spec.Name + " joined");
                    break;
                }
                case Msg.Left:
                {
                    r.ReadValueSafe(out byte id);
                    _m.RemoveFighter(id);
                    break;
                }
            }
        }

        // ================================================================== poses

        private void SendSnapshot()
        {
            var fs = _m.Fighters;
            var w = NetIO.Writer(Msg.Snapshot, 32 + fs.Count * 16);
            using (w)
            {
                w.WriteValueSafe(Time.unscaledTime);
                byte phase = (byte)((byte)_m.CurrentPhase | (_hold ? 0x80 : 0));
                w.WriteValueSafe(phase);
                w.WriteValueSafe(_m.CountdownLeft);
                w.WriteValueSafe(_m.Elapsed);
                w.WriteValueSafe((byte)fs.Count);
                for (int i = 0; i < fs.Count; i++)
                {
                    var f = fs[i];
                    byte flags = (byte)((f.Alive ? 1 : 0) | (f.SpawnProtection > 0f ? 2 : 0));
                    w.WriteValueSafe((byte)f.Id);
                    w.WriteValueSafe(flags);
                    w.WriteValueSafe(PosOf(f));
                    w.WriteValueSafe(NetIO.PackAngle(f.AimDir));
                    w.WriteValueSafe((byte)Mathf.CeilToInt(Mathf.Clamp(f.Health, 0f, 255f)));
                    w.WriteValueSafe((byte)Mathf.CeilToInt(Mathf.Clamp(f.Armor, 0f, 255f)));
                    w.WriteValueSafe((byte)f.Weapons.ShownId);
                }
                _net.SendToClients(ref w, NetworkDelivery.UnreliableSequenced);
            }
        }

        private void OnSnapshot(ref FastBufferReader r)
        {
            r.ReadValueSafe(out float hostTime);
            r.ReadValueSafe(out byte phase);
            r.ReadValueSafe(out float countdown);
            r.ReadValueSafe(out float elapsed);
            r.ReadValueSafe(out byte count);
            float t = _hostClock.ToLocal(hostTime);
            _m.NetSyncClock((MatchController.Phase)(phase & 0x7f), (phase & 0x80) != 0, countdown, elapsed);
            for (int i = 0; i < count; i++)
            {
                r.ReadValueSafe(out byte id);
                r.ReadValueSafe(out byte flags);
                r.ReadValueSafe(out Vector2 pos);
                r.ReadValueSafe(out ushort aim);
                r.ReadValueSafe(out byte hp);
                r.ReadValueSafe(out byte armor);
                r.ReadValueSafe(out byte weapon);
                var f = _m.FighterById(id);
                if (f == null) continue;
                if (f.IsPuppet)
                {
                    f.NetPush(t, pos, NetIO.UnpackAngle(aim));
                    f.Weapons.NetShow((WeaponId)weapon);
                }
                f.NetSyncStats(hp, armor, (flags & 2) != 0);
            }
        }

        private void SendState()
        {
            var f = _m.Local;
            if (f == null) return;
            var w = NetIO.Writer(Msg.State, 32);
            using (w)
            {
                w.WriteValueSafe(Time.unscaledTime);
                w.WriteValueSafe(f.Position);
                w.WriteValueSafe(NetIO.PackAngle(f.AimDir));
                w.WriteValueSafe((byte)f.Weapons.ShownId);
                _net.SendToHost(ref w, NetworkDelivery.UnreliableSequenced);
            }
        }

        private void HostOnState(ulong sender, ref FastBufferReader r)
        {
            r.ReadValueSafe(out float remoteTime);
            r.ReadValueSafe(out Vector2 pos);
            r.ReadValueSafe(out ushort aim);
            r.ReadValueSafe(out byte weapon);
            var f = _m.FighterOwnedBy(sender);
            if (f == null || !f.IsPuppet) return;
            if (!_clocks.TryGetValue(sender, out var clock)) _clocks[sender] = clock = new NetClock();
            f.NetPushChecked(clock.ToLocal(remoteTime), pos, NetIO.UnpackAngle(aim));
            f.Weapons.NetShow((WeaponId)weapon);
        }

        // ================================================================== shots

        private NetworkDelivery ShotDelivery(ShotKind kind, WeaponId weapon) =>
            kind == ShotKind.Grenade || _ctx.Config.Get(weapon).Kind == FireKind.Projectile ? NetworkDelivery.ReliableSequenced : NetworkDelivery.Unreliable;

        /// <summary>A fighter this device controls fired: tell the others so they see it.</summary>
        public void OnShot(Fighter f, ShotKind kind, WeaponStats s, Vector2 a, Vector2 b)
        {
            if (f.IsPuppet || (!IsHost && f != _m.Local)) return;
            var w = WriteShot(f.Id, kind, s.Id, a, b);
            using (w)
            {
                if (IsHost) _net.SendToClients(ref w, ShotDelivery(kind, s.Id));
                else _net.SendToHost(ref w, ShotDelivery(kind, s.Id));
            }
        }

        private static FastBufferWriter WriteShot(int id, ShotKind kind, WeaponId weapon, Vector2 a, Vector2 b)
        {
            var w = NetIO.Writer(Msg.Shot, 24);
            w.WriteValueSafe((byte)id);
            w.WriteValueSafe((byte)kind);
            w.WriteValueSafe((byte)weapon);
            w.WriteValueSafe(a);
            w.WriteValueSafe(b);
            return w;
        }

        private static void ReadShot(ref FastBufferReader r, out byte id, out ShotKind kind, out WeaponId weapon, out Vector2 a, out Vector2 b)
        {
            r.ReadValueSafe(out id);
            r.ReadValueSafe(out byte k);
            r.ReadValueSafe(out byte wpn);
            r.ReadValueSafe(out a);
            r.ReadValueSafe(out b);
            kind = (ShotKind)k;
            weapon = (WeaponId)wpn;
        }

        private void HostOnShot(ulong sender, ref FastBufferReader r)
        {
            ReadShot(ref r, out byte id, out var kind, out var weapon, out var a, out var b);
            var f = _m.FighterById(id);
            if (f == null || !f.IsPuppet || f.Owner != sender) return;
            Replay(f, kind, weapon, a, b);
            var w = WriteShot(id, kind, weapon, a, b);
            using (w) _net.SendToClients(ref w, ShotDelivery(kind, weapon), sender);
        }

        private void OnShotMsg(ref FastBufferReader r)
        {
            ReadShot(ref r, out byte id, out var kind, out var weapon, out var a, out var b);
            var f = _m.FighterById(id);
            if (f == null || f == _m.Local) return;
            Replay(f, kind, weapon, a, b);
        }

        private static void Replay(Fighter f, ShotKind kind, WeaponId weapon, Vector2 a, Vector2 b)
        {
            if (!f.Alive) return;
            switch (kind)
            {
                case ShotKind.Fire: f.Weapons.ReplayFire(weapon, b); break;
                case ShotKind.Melee: f.Weapons.ReplayMelee(b); break;
                case ShotKind.Grenade: f.Weapons.ReplayGrenade(a, b); break;
            }
        }

        // ================================================================== claims (client -> host)

        public void ClaimHit(Fighter victim, float damage, Vector2 dir, float knockback, WeaponId weapon, Vector2 point)
        {
            var w = NetIO.Writer(Msg.HitClaim, 40);
            using (w)
            {
                w.WriteValueSafe((byte)victim.Id);
                w.WriteValueSafe(damage);
                w.WriteValueSafe(dir);
                w.WriteValueSafe(knockback);
                w.WriteValueSafe((byte)weapon);
                w.WriteValueSafe(point);
                _net.SendToHost(ref w, NetworkDelivery.ReliableSequenced);
            }
        }

        private void HostOnHitClaim(ulong sender, ref FastBufferReader r)
        {
            r.ReadValueSafe(out byte vid);
            r.ReadValueSafe(out float damage);
            r.ReadValueSafe(out Vector2 dir);
            r.ReadValueSafe(out float knock);
            r.ReadValueSafe(out byte wpn);
            r.ReadValueSafe(out Vector2 point);
            var weapon = (WeaponId)wpn;
            var a = _m.FighterOwnedBy(sender);
            var v = _m.FighterById(vid);
            if (a == null || v == null || weapon >= WeaponId.Barrel || !(damage > 0f)) return;
            if (!a.Alive && Time.time - a.DeathTime > 2f) return; // a rocket may land just after you die
            // could that weapon reach? (generous: the victim is seen a little in the past)
            float reach;
            if (weapon == WeaponId.Grenade) reach = _ctx.Config.Grenade.MaxThrowDistance + _ctx.Config.Grenade.Radius;
            else
            {
                var s = _ctx.Config.Get(weapon);
                reach = s.Range + s.SplashRadius;
            }
            reach += 4f;
            if (v != a && (PosOf(v) - PosOf(a)).sqrMagnitude > reach * reach) return;
            // no more damage per second than any weapon can deal
            if (!_budget.TryGetValue(a.Id, out float left)) left = DamageBurst;
            float dmg = DamageModel.Cap(damage, _ctx.Config.Get(weapon).MaxHitDamage);
            if (dmg > left) return;
            _budget[a.Id] = left - dmg;
            _m.ApplyHit(a, v, dmg, dir.sqrMagnitude > 1.01f ? dir.normalized : dir, Mathf.Min(knock, 30f), weapon, point);
        }

        private void RefillBudgets(float dt)
        {
            if (_budget.Count == 0) return;
            _keys.Clear();
            foreach (var k in _budget.Keys) _keys.Add(k);
            for (int i = 0; i < _keys.Count; i++)
                _budget[_keys[i]] = Mathf.Min(DamageBurst, _budget[_keys[i]] + DamagePerSecond * dt);
        }

        public void ClaimProp(int index, float damage, Vector2 dir)
        {
            if (index < 0) return;
            var w = NetIO.Writer(Msg.PropHit, 20);
            using (w)
            {
                w.WriteValueSafe((ushort)index);
                w.WriteValueSafe(damage);
                w.WriteValueSafe(dir);
                _net.SendToHost(ref w, NetworkDelivery.ReliableSequenced);
            }
        }

        private void HostOnPropHit(ulong sender, ref FastBufferReader r)
        {
            r.ReadValueSafe(out ushort idx);
            r.ReadValueSafe(out float damage);
            r.ReadValueSafe(out Vector2 dir);
            var a = _m.FighterOwnedBy(sender);
            var prop = _m.PropAt(idx);
            if (a == null || prop == null || !(damage > 0f)) return;
            prop.ApplyDamage(Mathf.Min(damage, 200f), a, dir);
        }

        public void RequestPickup(Pickup p, bool swap, WeaponSlot old)
        {
            var w = NetIO.Writer(Msg.TakePickup, 24);
            using (w)
            {
                w.WriteValueSafe(p.Id);
                w.WriteValueSafe(swap);
                w.WriteValueSafe((byte)(old != null ? old.Stats.Id : WeaponId.Pistol));
                w.WriteValueSafe((short)(old != null ? old.Mag : 0));
                w.WriteValueSafe(old != null ? old.Reserve : 0);
                _net.SendToHost(ref w, NetworkDelivery.ReliableSequenced);
            }
        }

        private void HostOnTakePickup(ulong sender, ref FastBufferReader r)
        {
            r.ReadValueSafe(out int id);
            r.ReadValueSafe(out bool swap);
            r.ReadValueSafe(out byte oldWeapon);
            r.ReadValueSafe(out short oldMag);
            r.ReadValueSafe(out int oldReserve);
            var f = _m.FighterOwnedBy(sender);
            if (f == null || _m.CurrentPhase != MatchController.Phase.Playing) return;
            _m.Pickups.HostGrant(f, id, swap, (WeaponId)oldWeapon, oldMag, oldReserve);
        }

        private void HostOnLoaded(ulong sender)
        {
            _loaded.Add(sender);
            var f = _m.FighterOwnedBy(sender);
            // someone who joined a running match appears once their map is built
            if (f != null && !f.Alive && f.DeathTime <= 0f) _m.HostSpawnNow(f);
        }

        // ================================================================== host events

        public void HostHit(Fighter attacker, Fighter victim, float dealt, Vector2 dir, float knockback, WeaponId weapon, Vector2 point)
        {
            var w = NetIO.Writer(Msg.Hit, 48);
            using (w)
            {
                w.WriteValueSafe((sbyte)(attacker != null ? attacker.Id : -1));
                w.WriteValueSafe((byte)victim.Id);
                w.WriteValueSafe(dealt);
                w.WriteValueSafe(dir);
                w.WriteValueSafe(knockback);
                w.WriteValueSafe((byte)weapon);
                w.WriteValueSafe(point);
                w.WriteValueSafe(victim.Health);
                w.WriteValueSafe(victim.Armor);
                _net.SendToClients(ref w, NetworkDelivery.ReliableSequenced);
            }
        }

        private void OnHit(ref FastBufferReader r)
        {
            r.ReadValueSafe(out sbyte aid);
            r.ReadValueSafe(out byte vid);
            r.ReadValueSafe(out float dealt);
            r.ReadValueSafe(out Vector2 dir);
            r.ReadValueSafe(out float knock);
            r.ReadValueSafe(out byte weapon);
            r.ReadValueSafe(out Vector2 point);
            r.ReadValueSafe(out float hp);
            r.ReadValueSafe(out float armor);
            _m.NetApplyHit(aid >= 0 ? _m.FighterById(aid) : null, _m.FighterById(vid), dealt, dir, knock, point, hp, armor);
        }

        public void HostKill(Fighter killer, Fighter victim, WeaponId weapon, Vector2 dir, float force, float time)
        {
            var w = NetIO.Writer(Msg.Kill, 32);
            using (w)
            {
                w.WriteValueSafe((sbyte)(killer != null ? killer.Id : -1));
                w.WriteValueSafe((byte)victim.Id);
                w.WriteValueSafe((byte)weapon);
                w.WriteValueSafe(dir);
                w.WriteValueSafe(force);
                w.WriteValueSafe(time);
                _net.SendToClients(ref w, NetworkDelivery.ReliableSequenced);
            }
        }

        private void OnKill(ref FastBufferReader r)
        {
            r.ReadValueSafe(out sbyte kid);
            r.ReadValueSafe(out byte vid);
            r.ReadValueSafe(out byte weapon);
            r.ReadValueSafe(out Vector2 dir);
            r.ReadValueSafe(out float force);
            r.ReadValueSafe(out float time);
            _m.NetApplyKill(kid >= 0 ? _m.FighterById(kid) : null, _m.FighterById(vid), (WeaponId)weapon, dir, force, time);
        }

        public void HostSpawn(Fighter f, Vector2 pos)
        {
            var w = NetIO.Writer(Msg.Spawn, 16);
            using (w)
            {
                w.WriteValueSafe((byte)f.Id);
                w.WriteValueSafe(pos);
                w.WriteValueSafe(Time.unscaledTime);
                _net.SendToClients(ref w, NetworkDelivery.ReliableSequenced);
            }
        }

        private void OnSpawn(ref FastBufferReader r)
        {
            r.ReadValueSafe(out byte id);
            r.ReadValueSafe(out Vector2 pos);
            r.ReadValueSafe(out float hostTime);
            var f = _m.FighterById(id);
            if (f == null) return;
            _m.NetSpawn(f, pos);
            // poses sent before this spawn must not drag the fighter back to where they died
            if (f.IsPuppet) f.NetSnap(pos, _hostClock.ToLocal(hostTime));
        }

        private void OnPickupTaken(Pickup p, Fighter f)
        {
            var w = NetIO.Writer(Msg.PickupTaken, 24);
            using (w)
            {
                w.WriteValueSafe(p.Id);
                w.WriteValueSafe((byte)(f != null ? f.Id : 255));
                w.WriteValueSafe((byte)p.Type);
                w.WriteValueSafe((byte)p.Weapon);
                w.WriteValueSafe((short)p.Mag);
                w.WriteValueSafe(p.Reserve);
                _net.SendToClients(ref w, NetworkDelivery.ReliableSequenced);
            }
        }

        private void OnPickupTakenMsg(ref FastBufferReader r)
        {
            r.ReadValueSafe(out int id);
            r.ReadValueSafe(out byte fid);
            r.ReadValueSafe(out byte type);
            r.ReadValueSafe(out byte weapon);
            r.ReadValueSafe(out short mag);
            r.ReadValueSafe(out int reserve);
            _m.Pickups.NetTaken(id, _m.FighterById(fid), (PickupType)type, (WeaponId)weapon, mag, reserve);
        }

        private void OnPickupDropped(Pickup p)
        {
            var w = NetIO.Writer(Msg.PickupDrop, 32);
            using (w)
            {
                WriteDrop(ref w, p);
                _net.SendToClients(ref w, NetworkDelivery.ReliableSequenced);
            }
        }

        private static void WriteDrop(ref FastBufferWriter w, Pickup p)
        {
            w.WriteValueSafe(p.Id);
            w.WriteValueSafe((byte)p.Weapon);
            w.WriteValueSafe(p.Position);
            w.WriteValueSafe((short)p.Mag);
            w.WriteValueSafe(p.Reserve);
            w.WriteValueSafe(p.Timer);
        }

        private void OnPickupDropMsg(ref FastBufferReader r)
        {
            r.ReadValueSafe(out int id);
            r.ReadValueSafe(out byte weapon);
            r.ReadValueSafe(out Vector2 pos);
            r.ReadValueSafe(out short mag);
            r.ReadValueSafe(out int reserve);
            r.ReadValueSafe(out float life);
            _m.Pickups.NetDrop(id, (WeaponId)weapon, pos, mag, reserve, life);
        }

        private void OnPickupRespawned(Pickup p)
        {
            var w = NetIO.Writer(Msg.PickupSpawn, 8);
            using (w)
            {
                w.WriteValueSafe(p.Id);
                _net.SendToClients(ref w, NetworkDelivery.ReliableSequenced);
            }
        }

        public void HostProp(int index, bool alive, Vector2 dir)
        {
            if (index < 0) return;
            var w = NetIO.Writer(Msg.Prop, 16);
            using (w)
            {
                w.WriteValueSafe((ushort)index);
                w.WriteValueSafe(alive);
                w.WriteValueSafe(dir);
                _net.SendToClients(ref w, NetworkDelivery.ReliableSequenced);
            }
        }

        public void HostEnd()
        {
            var w = NetIO.Writer(Msg.End, 4);
            using (w) _net.SendToClients(ref w, NetworkDelivery.ReliableSequenced);
        }

        // ================================================================== joining and leaving

        /// <summary>Host: someone joined the room while this match runs. They get a fighter (a bot makes room
        /// if the match is full) and the whole match so far.</summary>
        public void HostLateJoin(RoomMember m)
        {
            if (_m.CurrentPhase == MatchController.Phase.Ended || _m.FighterOwnedBy(m.ClientId) != null) return;
            if (_m.Fighters.Count >= NetSession.MaxPlayers)
            {
                Fighter bot = null;
                for (int i = _m.Fighters.Count - 1; i >= 0 && bot == null; i--)
                    if (_m.Fighters[i].IsBot) bot = _m.Fighters[i];
                if (bot == null) return;
                int botId = bot.Id;
                _m.RemoveFighter(botId);
                SendLeft(botId);
            }
            var spec = _m.MakeLateSpec(m);
            _m.NetAddFighter(spec, _net.LocalId, true);
            var w = NetIO.Writer(Msg.Joined, 96);
            using (w)
            {
                NetIO.Write(ref w, spec);
                _net.SendToClients(ref w, NetworkDelivery.ReliableSequenced, m.ClientId);
            }
            _net.SendStart(_m.Settings, _m.MapIndex, _m.Roster, CaptureJoinState(), m.ClientId);
            _ctx.Events.RaiseToast(m.Name + " joined");
        }

        /// <summary>Host: a player disconnected.</summary>
        public void HostPlayerLeft(ulong clientId)
        {
            _loaded.Remove(clientId);
            _clocks.Remove(clientId);
            var f = _m.FighterOwnedBy(clientId);
            if (f == null) return;
            int id = f.Id;
            _m.RemoveFighter(id);
            SendLeft(id);
        }

        private void SendLeft(int id)
        {
            var w = NetIO.Writer(Msg.Left, 4);
            using (w)
            {
                w.WriteValueSafe((byte)id);
                _net.SendToClients(ref w, NetworkDelivery.ReliableSequenced);
            }
        }

        private byte[] CaptureJoinState()
        {
            var w = new FastBufferWriter(1024, Allocator.Temp, 64 * 1024);
            using (w)
            {
                w.WriteValueSafe((byte)_m.CurrentPhase);
                w.WriteValueSafe(_m.Elapsed);
                var fs = _m.Fighters;
                w.WriteValueSafe((byte)fs.Count);
                for (int i = 0; i < fs.Count; i++)
                {
                    var f = fs[i];
                    var rec = _m.Rules.Get(f.Id);
                    w.WriteValueSafe((byte)f.Id);
                    w.WriteValueSafe(f.Alive);
                    w.WriteValueSafe(PosOf(f));
                    w.WriteValueSafe(f.Health);
                    w.WriteValueSafe(f.Armor);
                    w.WriteValueSafe((short)(rec != null ? rec.Kills : 0));
                    w.WriteValueSafe((short)(rec != null ? rec.Deaths : 0));
                    w.WriteValueSafe((byte)(rec != null ? rec.Streak : 0));
                    w.WriteValueSafe((byte)(rec != null ? rec.BestStreak : 0));
                }
                w.WriteValueSafe((short)_m.Rules.TeamKills(0));
                w.WriteValueSafe((short)_m.Rules.TeamKills(1));
                var items = _m.Pickups.Items;
                w.WriteValueSafe((short)items.Count);
                for (int i = 0; i < items.Count; i++)
                {
                    var p = items[i];
                    w.WriteValueSafe(p.Dropped);
                    w.WriteValueSafe(p.Active);
                    if (p.Dropped) WriteDrop(ref w, p);
                    else w.WriteValueSafe(p.Id);
                }
                var props = _m.Props;
                w.WriteValueSafe((short)props.Count);
                for (int i = 0; i < props.Count; i++) w.WriteValueSafe(props[i].IsAlive);
                return w.ToArray();
            }
        }

        private void ApplyJoinState(byte[] data)
        {
            var r = new FastBufferReader(data, Allocator.Temp);
            using (r)
            {
                r.ReadValueSafe(out byte phase);
                r.ReadValueSafe(out float elapsed);
                r.ReadValueSafe(out byte count);
                for (int i = 0; i < count; i++)
                {
                    r.ReadValueSafe(out byte id);
                    r.ReadValueSafe(out bool alive);
                    r.ReadValueSafe(out Vector2 pos);
                    r.ReadValueSafe(out float hp);
                    r.ReadValueSafe(out float armor);
                    r.ReadValueSafe(out short kills);
                    r.ReadValueSafe(out short deaths);
                    r.ReadValueSafe(out byte streak);
                    r.ReadValueSafe(out byte best);
                    var f = _m.FighterById(id);
                    var rec = _m.Rules.Get(id);
                    if (rec != null)
                    {
                        rec.Kills = kills;
                        rec.Deaths = deaths;
                        rec.Streak = streak;
                        rec.BestStreak = best;
                    }
                    if (f != null && alive) f.NetRevive(pos, hp, armor);
                }
                r.ReadValueSafe(out short red);
                r.ReadValueSafe(out short blue);
                _m.Rules.SetTeamKills(red, blue);
                r.ReadValueSafe(out short items);
                for (int i = 0; i < items; i++)
                {
                    r.ReadValueSafe(out bool dropped);
                    r.ReadValueSafe(out bool active);
                    if (dropped)
                    {
                        r.ReadValueSafe(out int id);
                        r.ReadValueSafe(out byte weapon);
                        r.ReadValueSafe(out Vector2 pos);
                        r.ReadValueSafe(out short mag);
                        r.ReadValueSafe(out int reserve);
                        r.ReadValueSafe(out float life);
                        if (active) _m.Pickups.NetDrop(id, (WeaponId)weapon, pos, mag, reserve, life);
                    }
                    else
                    {
                        r.ReadValueSafe(out int id);
                        _m.Pickups.NetSetActive(id, active);
                    }
                }
                r.ReadValueSafe(out short props);
                for (int i = 0; i < props; i++)
                {
                    r.ReadValueSafe(out bool alive);
                    _m.PropAt(i)?.NetSetQuiet(alive);
                }
                _m.NetJoinRunning((MatchController.Phase)phase, elapsed);
            }
        }
    }
}
