using System;
using System.Collections.Generic;
using NeonBrawl.Core;
using UnityEngine;

namespace NeonBrawl
{
    /// <summary>
    /// Runs one match. Offline (and on the online host) it is the authority: owns rules, fighters, pickups,
    /// respawns and the timer, and is the only place damage is applied (<see cref="ApplyHit"/>).
    /// On an online client it mirrors the host: its own player's hits become claims, everything else
    /// arrives through <see cref="NetMatch"/>. Destroying this object removes everything the match created.
    /// </summary>
    public sealed class MatchController : MonoBehaviour
    {
        public enum Phase { Countdown, Playing, Ended }

        private static readonly string[] BotNames =
        {
            "Glitch", "Pixel Pam", "Turbo Tom", "Neon Nia", "Zapster", "Captain Chaos", "Lil Laser", "Disco Dan",
            "Mega Maya", "Boomer Bob", "Sparky", "Vortex Vee", "Rocket Rina", "Bananaman", "Nacho", "Kiki Kaboom",
            "Dr. Wobble", "Sniper Sue", "Jelly Jay", "Ravi Rampage"
        };

        private static readonly Color[] Outfits =
        {
            new Color(0.25f, 0.62f, 1f), new Color(0.92f, 0.3f, 0.25f), new Color(0.35f, 0.78f, 0.3f), new Color(0.98f, 0.8f, 0.2f),
            new Color(0.62f, 0.4f, 0.9f), new Color(1f, 0.55f, 0.2f), new Color(0.2f, 0.82f, 0.8f), new Color(0.95f, 0.45f, 0.7f),
            new Color(0.88f, 0.88f, 0.9f), new Color(0.55f, 0.4f, 0.28f), new Color(0.65f, 0.9f, 0.25f), new Color(0.25f, 0.3f, 0.6f)
        };

        private static readonly Color[] Skins =
        {
            new Color(1f, 0.86f, 0.67f), new Color(0.95f, 0.76f, 0.49f), new Color(0.88f, 0.67f, 0.41f),
            new Color(0.78f, 0.53f, 0.26f), new Color(0.55f, 0.33f, 0.14f), new Color(0.36f, 0.23f, 0.13f)
        };

        /// <summary>Multipliers for the painted head art (it is already skin-colored).</summary>
        private static readonly Color[] SkinTints =
        {
            Color.white, new Color(0.96f, 0.88f, 0.8f), new Color(0.86f, 0.72f, 0.6f),
            new Color(0.72f, 0.56f, 0.43f), new Color(0.55f, 0.4f, 0.3f), new Color(0.42f, 0.3f, 0.22f)
        };

        private static readonly Color[] Hairs =
        {
            new Color(0.1f, 0.1f, 0.1f), new Color(0.23f, 0.14f, 0.09f), new Color(0.42f, 0.27f, 0.16f),
            new Color(0.66f, 0.45f, 0.25f), new Color(0.85f, 0.7f, 0.42f), new Color(0.56f, 0.23f, 0.12f), new Color(0.56f, 0.56f, 0.56f)
        };

        public static readonly Color TeamRed = new Color(0.92f, 0.28f, 0.24f);
        public static readonly Color TeamBlue = new Color(0.25f, 0.5f, 0.98f);
        private static readonly Color LocalNameColor = new Color(1f, 0.92f, 0.35f);
        private static readonly Color ShieldPop = new Color(0.6f, 0.85f, 1f);

        private readonly List<Fighter> _fighters = new List<Fighter>(12);
        private readonly List<Fighter> _respawnQueue = new List<Fighter>(12);
        private readonly List<PlayerRecord> _standings = new List<PlayerRecord>(12);
        private readonly List<Destructible> _props = new List<Destructible>(48);
        private readonly List<FighterSpec> _roster = new List<FighterSpec>(12);

        private GameContext _ctx;
        private MapInstance _map;
        private FogOfView _fog;
        private float _countdown;
        private int _lastCount;
        private float _endTimer;
        private float _heartbeatT;
        private float _spectateSwitchT;
        private Transform _fightersRoot;
        private bool _hold;
        private bool _holdShown;

        public MatchSettings Settings { get; private set; }
        public MatchRules Rules { get; private set; }
        public PickupSystem Pickups { get; private set; }
        public PowerSystem PowerUps { get; private set; }
        public MapData Map => _map.Data;
        public List<Fighter> Fighters => _fighters;
        public Fighter Local { get; private set; }
        public bool Spectator { get; private set; }
        public Phase CurrentPhase { get; private set; }
        public float Elapsed { get; private set; }
        public float TimeLeft => Settings.TimeLimit > 0f ? Mathf.Max(0f, Settings.TimeLimit - Elapsed) : 0f;
        public float CountdownLeft => _countdown;

        /// <summary>Online sync for this match, or null offline.</summary>
        public NetMatch Net { get; private set; }
        public bool Online => Net != null;
        /// <summary>Offline, or the online host: this device decides damage, kills and pickups.</summary>
        public bool IsAuthority => Net == null || Net.IsHost;
        public int MapIndex { get; private set; }
        /// <summary>Online: everyone in the match, as the host decided.</summary>
        public List<FighterSpec> Roster => _roster;
        public List<Destructible> Props => _props;

        public event Action<MatchController> Ended;

        // ================================================================== setup

        /// <summary>Offline match (or the menu's demo match when <paramref name="spectator"/>).</summary>
        public void Begin(GameContext ctx, MatchSettings settings, MapData map, bool spectator, bool autoPlay)
        {
            Setup(ctx, settings, map, spectator);
            var rng = new System.Random(Environment.TickCount);
            bool teams = Rules.IsTeamMode;
            var skins = SkinSet(ctx);
            int id = 0;

            if (!spectator)
            {
                var profile = ctx.Profile;
                Color outfit = teams ? TeamRed : (profile != null ? profile.OutfitColor : Outfits[0]);
                IFighterBrain brain = autoPlay ? new BotBrain(this, ctx.Config.Bot(2), rng.Next()) : new PlayerBrain(ctx);
                // Your locker look (outfit, headgear or hair, back item, skin tone).
                var look = profile != null ? profile.Look(outfit) : new FighterLook { Outfit = outfit, Skin = skins[1], Hair = Hairs[0], Body = 1, Head = 1 };
                if (!(ctx.Catalog != null && ctx.Catalog.HasCharacters)) look.Skin = skins[1];
                var spec = new FighterSpec { Id = id, Name = "YOU", Team = 0, Bot = autoPlay, Look = look, NameColor = LocalNameColor };
                Local = AddFighter(spec, true, brain, false);
                id++;
            }

            var names = new List<string>(BotNames);
            for (int b = 0; b < settings.BotCount; b++)
            {
                int team = teams ? Rules.SmallerTeam() : -1;
                var spec = MakeBot(rng, id, team, teams, names, skins);
                AddFighter(spec, false, new BotBrain(this, ctx.Config.Bot(settings.Difficulty), rng.Next()), true);
                id++;
            }

            for (int i = 0; i < _fighters.Count; i++)
            {
                var f = _fighters[i];
                f.Spawn(PickSpawn(f, true));
                f.Frozen = !spectator;
            }
            Finish(spectator);
        }

        /// <summary>Online match, on the host or a client. <paramref name="joinState"/> is set when joining a running match.</summary>
        public void BeginOnline(GameContext ctx, MatchSettings settings, int mapIndex, List<FighterSpec> roster, NetSession net, byte[] joinState)
        {
            MapIndex = mapIndex;
            Setup(ctx, settings, MapLayouts.Get(mapIndex).Build(), false);
            _roster.AddRange(roster);
            Pickups.Authority = net.IsHost;
            PowerUps.Authority = net.IsHost;
            for (int i = 0; i < roster.Count; i++) AddOnlineFighter(roster[i], net.LocalId, net.IsHost);

            if (joinState == null)
            {
                for (int i = 0; i < roster.Count; i++)
                {
                    var f = FighterById(roster[i].Id);
                    f.Spawn(roster[i].Spawn);
                    f.Frozen = true;
                }
            }
            Finish(false);
            Net = gameObject.AddComponent<NetMatch>();
            Net.Init(ctx, net, this, joinState);
        }

        private void Setup(GameContext ctx, MatchSettings settings, MapData map, bool spectator)
        {
            _ctx = ctx;
            ctx.Match = this;
            Settings = settings;
            Spectator = spectator;
            Rules = new MatchRules(settings);
            _map = MapBuilder.Build(ctx, map, settings.Night, transform);
            ctx.Sfx.Ground = SfxSystem.SurfaceForTheme(map.Theme);
            ctx.Sfx.ClearPending();
            var pickupRoot = new GameObject("Pickups").transform;
            pickupRoot.SetParent(transform, false);
            Pickups = new PickupSystem(ctx, pickupRoot);
            for (int i = 0; i < map.Pickups.Count; i++) Pickups.AddSpot(map.Pickups[i]);
            var powerRoot = new GameObject("PowerUps").transform;
            powerRoot.SetParent(transform, false);
            PowerUps = new PowerSystem(ctx, this, powerRoot);
            _fightersRoot = new GameObject("Fighters").transform;
            _fightersRoot.SetParent(transform, false);
        }

        private void Finish(bool spectator)
        {
            var fogGo = new GameObject("LineOfSight");
            fogGo.transform.SetParent(transform, false);
            _fog = fogGo.AddComponent<FogOfView>();
            _fog.Init(this, _ctx.Art, Settings.Fog && Local != null, Settings.Night);
            _fog.SetViewer(Local);

            if (Local != null && !Local.IsBot)
            {
                var ch = new GameObject("Crosshair").AddComponent<Crosshair>();
                ch.transform.SetParent(transform, false);
                ch.Init(_ctx, Local);
            }

            // generous side margins keep your fighter near the middle at map edges, clear of the thumb controls
            // (MapBuilder.OuterBorder draws ground out there)
            var map = _map.Data;
            _ctx.CameraRig.SetBounds(new Rect(-6f, -3.5f, map.Width + 12f, map.Height + 7f));
            if (Local != null) _ctx.CameraRig.Follow(Local, true);
            else if (_fighters.Count > 0) _ctx.CameraRig.Follow(_fighters[0], true);

            CurrentPhase = spectator ? Phase.Playing : Phase.Countdown;
            _countdown = 3.2f;
            _lastCount = 4;
        }

        private Fighter AddFighter(FighterSpec spec, bool local, IFighterBrain brain, bool isBot)
        {
            var f = Fighter.Create(_ctx, _fightersRoot, spec.Id, spec.Name, spec.Team, isBot, local, spec.Look, spec.NameColor, brain);
            Rules.Add(spec.Id, spec.Name, spec.Team, isBot);
            _fighters.Add(f);
            return f;
        }

        /// <summary>Online: the host runs bots, everyone runs their own player, the rest are puppets.</summary>
        private Fighter AddOnlineFighter(FighterSpec spec, ulong me, bool host)
        {
            bool local = !spec.Bot && spec.Owner == me;
            bool puppet = host ? !spec.Bot && spec.Owner != me : !local;
            IFighterBrain brain = null;
            if (local) brain = CommandLine.Has("-nbbot") ? new BotBrain(this, _ctx.Config.Bot(2), spec.Id * 7919 + 17) : new PlayerBrain(_ctx);
            else if (!puppet) brain = new BotBrain(this, _ctx.Config.Bot(Settings.Difficulty), spec.Id * 7919 + Environment.TickCount);
            var s = spec;
            if (local)
            {
                s.Name = "YOU";
                s.NameColor = LocalNameColor;
            }
            var f = AddFighter(s, local, brain, spec.Bot);
            f.SetNetwork(spec.Owner, puppet);
            if (local) Local = f;
            return f;
        }

        private static Color[] SkinSet(GameContext ctx) => ctx.Catalog != null && ctx.Catalog.HasCharacters ? SkinTints : Skins;

        private static FighterSpec MakeBot(System.Random rng, int id, int team, bool teams, List<string> names, Color[] skins)
        {
            int pick = rng.Next(names.Count);
            string n = names[pick];
            names.RemoveAt(pick);
            if (names.Count == 0) names.AddRange(BotNames);
            Color outfit = teams ? (team == 0 ? TeamRed : TeamBlue) : Outfits[(id + 1) % Outfits.Length];
            return new FighterSpec
            {
                Id = id,
                Bot = true,
                Name = n,
                Team = team,
                NameColor = teams ? (team == 0 ? new Color(1f, 0.6f, 0.55f) : new Color(0.6f, 0.75f, 1f)) : Color.white,
                Look = new FighterLook
                {
                    Outfit = outfit,
                    Skin = skins[rng.Next(skins.Length)],
                    Hair = Hairs[rng.Next(Hairs.Length)],
                    Body = rng.Next(8),
                    Head = rng.Next(3) == 0 ? 0 : 1 + rng.Next(15),
                    HairStyle = 1 + rng.Next(8),
                    Back = rng.Next(3) == 0 ? 1 + rng.Next(4) : 0
                }
            };
        }

        /// <summary>
        /// Host: everyone in the room plus bots up to the room's bot count (12 fighters at most),
        /// balanced into teams, each with a starting spot on their side of the map.
        /// </summary>
        public static List<FighterSpec> BuildOnlineRoster(GameContext ctx, MatchSettings s, MapData map, List<RoomMember> members, ulong hostId)
        {
            var rng = new System.Random(Environment.TickCount);
            bool teams = s.Mode == MatchMode.TeamDeathmatch;
            var specs = new List<FighterSpec>(NetSession.MaxPlayers);
            int red = 0, blue = 0;
            for (int i = 0; i < members.Count && specs.Count < NetSession.MaxPlayers; i++)
            {
                var m = members[i];
                int team = teams ? (red <= blue ? 0 : 1) : -1;
                if (team == 0) red++; else if (team == 1) blue++;
                var look = m.Look;
                if (teams) look.Outfit = team == 0 ? TeamRed : TeamBlue;
                specs.Add(new FighterSpec
                {
                    Id = specs.Count, Owner = m.ClientId, Bot = false, Name = m.Name, Team = team, Look = look,
                    NameColor = teams ? (team == 0 ? new Color(1f, 0.6f, 0.55f) : new Color(0.6f, 0.75f, 1f)) : new Color(0.75f, 0.95f, 1f)
                });
            }
            var names = new List<string>(BotNames);
            int bots = Mathf.Min(s.BotCount, NetSession.MaxPlayers - specs.Count);
            var skins = SkinSet(ctx);
            for (int b = 0; b < bots; b++)
            {
                int team = teams ? (red <= blue ? 0 : 1) : -1;
                if (team == 0) red++; else if (team == 1) blue++;
                var spec = MakeBot(rng, specs.Count, team, teams, names, skins);
                spec.Owner = hostId;
                specs.Add(spec);
            }
            AssignSpawns(specs, map, teams, rng);
            return specs;
        }

        private static void AssignSpawns(List<FighterSpec> specs, MapData map, bool teams, System.Random rng)
        {
            var free = new List<Vector2>(map.Spawns);
            for (int i = free.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (free[i], free[j]) = (free[j], free[i]);
            }
            for (int i = 0; i < specs.Count; i++)
            {
                var spec = specs[i];
                int pick = -1;
                for (int k = 0; k < free.Count; k++)
                {
                    if (teams && (free[k].x < map.Center.x) != (spec.Team == 0)) continue;
                    pick = k;
                    break;
                }
                if (pick < 0 && free.Count > 0) pick = 0;
                spec.Spawn = pick >= 0 ? free[pick] : map.Spawns[rng.Next(map.Spawns.Count)];
                if (pick >= 0) free.RemoveAt(pick);
                specs[i] = spec;
            }
        }

        /// <summary>Host: a spec for a player joining a running match.</summary>
        public FighterSpec MakeLateSpec(RoomMember m)
        {
            int id = 0;
            for (int i = 0; i < _roster.Count; i++) id = Mathf.Max(id, _roster[i].Id + 1);
            for (int i = 0; i < _fighters.Count; i++) id = Mathf.Max(id, _fighters[i].Id + 1);
            bool teams = Rules.IsTeamMode;
            int team = teams ? Rules.SmallerTeam() : -1;
            var look = m.Look;
            if (teams) look.Outfit = team == 0 ? TeamRed : TeamBlue;
            return new FighterSpec
            {
                Id = id, Owner = m.ClientId, Bot = false, Name = m.Name, Team = team, Look = look,
                NameColor = teams ? (team == 0 ? new Color(1f, 0.6f, 0.55f) : new Color(0.6f, 0.75f, 1f)) : new Color(0.75f, 0.95f, 1f)
            };
        }

        /// <summary>Online: someone joined a running match (not alive until the host spawns them).</summary>
        public Fighter NetAddFighter(FighterSpec spec, ulong me, bool host)
        {
            if (FighterById(spec.Id) != null) return null;
            _roster.Add(spec);
            var f = AddOnlineFighter(spec, me, host);
            f.Frozen = CurrentPhase != Phase.Playing;
            return f;
        }

        /// <summary>Online: a player left (or a bot made room for a player).</summary>
        public void RemoveFighter(int id)
        {
            var f = FighterById(id);
            if (f == null) return;
            _fighters.Remove(f);
            _respawnQueue.Remove(f);
            Rules.Remove(id);
            for (int i = _roster.Count - 1; i >= 0; i--)
                if (_roster[i].Id == id) _roster.RemoveAt(i);
            if (_ctx.CameraRig.Following == f && Local != null) _ctx.CameraRig.Follow(Local, false);
            f.NetRemove();
            Destroy(f.gameObject);
        }

        // ================================================================== queries

        public bool AreEnemies(Fighter a, Fighter b)
        {
            if (a == null || b == null || a == b) return false;
            if (!Rules.IsTeamMode) return true;
            return a.Team != b.Team;
        }

        public void SetCellWalkable(Vector2Int cell, bool walkable)
        {
            if (_map != null) _map.Data.Grid.SetWalkable(cell.x, cell.y, walkable);
        }

        public void GetStandings(List<PlayerRecord> into) => Rules.GetStandings(into);

        public Fighter FighterById(int id)
        {
            for (int i = 0; i < _fighters.Count; i++)
                if (_fighters[i].Id == id) return _fighters[i];
            return null;
        }

        public Fighter FighterOwnedBy(ulong clientId)
        {
            for (int i = 0; i < _fighters.Count; i++)
                if (!_fighters[i].IsBot && _fighters[i].Owner == clientId) return _fighters[i];
            return null;
        }

        public void RegisterProp(Destructible d)
        {
            d.Index = _props.Count;
            _props.Add(d);
        }

        public Destructible PropAt(int index) => index >= 0 && index < _props.Count ? _props[index] : null;

        /// <summary>Host: a crate or barrel broke or came back.</summary>
        public void OnPropChanged(Destructible d, bool alive, Vector2 dir)
        {
            if (Net != null && Net.IsHost) Net.HostProp(d.Index, alive, dir);
        }

        // ================================================================== damage

        /// <summary>
        /// Every hit goes through here. Offline it applies. Online, a client turns its own player's hits
        /// into claims for the host and ignores everyone else's (replayed shots are only for looks);
        /// the host applies its bots' and its own hits plus world damage, and takes claims from clients.
        /// Returns true if the hit counts (or was claimed).
        /// </summary>
        public bool ReportHit(Fighter attacker, Fighter victim, float damage, Vector2 dir, float knockback, WeaponId weapon, Vector2 point)
        {
            if (victim == null || !victim.Alive || CurrentPhase == Phase.Ended) return false;
            if (attacker != null && attacker != victim && !AreEnemies(attacker, victim)) return false;
            // RAGE doubles what the attacker deals (decided where the shot is fired)
            if (attacker != null && attacker != victim) damage *= attacker.Powers.DamageMult;
            if (victim.Powers.Shielded)
            {
                ShieldPing(victim, point, dir);
                return false;
            }
            if (Net != null)
            {
                bool world = attacker == null || weapon == WeaponId.Barrel;
                if (!Net.IsHost)
                {
                    if (world || attacker != Local) return false;
                    if (victim.SpawnProtection > 0f)
                    {
                        _ctx.Fx.PlasmaPop(victim.Position, ShieldPop);
                        return false;
                    }
                    Net.ClaimHit(victim, damage, dir, knockback, weapon, point);
                    return true;
                }
                if (!world && attacker.IsPuppet) return false;
            }
            return ApplyHit(attacker, victim, damage, dir, knockback, weapon, point);
        }

        /// <summary>The authority's damage (neon-brawl: server owns health and kills, caps damage per weapon).</summary>
        public bool ApplyHit(Fighter attacker, Fighter victim, float damage, Vector2 dir, float knockback, WeaponId weapon, Vector2 point)
        {
            if (victim == null || !victim.Alive || CurrentPhase == Phase.Ended) return false;
            if (attacker != null && attacker != victim && !AreEnemies(attacker, victim)) return false;
            if (victim.SpawnProtection > 0f)
            {
                _ctx.Fx.PlasmaPop(victim.Position, ShieldPop);
                return false;
            }
            if (victim.Powers.Shielded)
            {
                ShieldPing(victim, point, dir);
                return false;
            }
            float rage = attacker != null && attacker != victim ? attacker.Powers.DamageMult : 1f;
            float dmg = DamageModel.Cap(damage, _ctx.Config.Get(weapon).MaxHitDamage * rage);
            if (dmg <= 0f) return false;

            float dealt = victim.ApplyDamage(dmg, dir, knockback, attacker, point);
            if (victim.Powers.Surging) victim.KeepAlive(); // LIFE SURGE: cannot be finished off
            Net?.HostHit(attacker, victim, dealt, dir, knockback, weapon, point);
            HitFeedback(attacker, victim, dealt, dir, point);
            if (victim.Health <= 0f) HandleKill(attacker, victim, weapon, dir, knockback);
            return true;
        }

        /// <summary>IRON SHIELD: the hit bounces off with a ping.</summary>
        private void ShieldPing(Fighter victim, Vector2 point, Vector2 dir)
        {
            Color c = Powers.Get(PowerId.Shield).Color;
            Vector2 at = (point - victim.Position).sqrMagnitude < 0.01f ? victim.Position - dir.normalized * 0.7f : victim.Position + (point - victim.Position).normalized * 0.75f;
            _ctx.Fx.PlasmaPop(at, c);
            _ctx.Sfx.Play(SfxId.ShieldPing, at, 0.6f);
        }

        /// <summary>Online client: the host applied a hit.</summary>
        public void NetApplyHit(Fighter attacker, Fighter victim, float dealt, Vector2 dir, float knockback, Vector2 point, float hp, float armor)
        {
            if (victim == null) return;
            victim.NetHit(hp, armor, dir, knockback, point, dealt, attacker);
            HitFeedback(attacker, victim, dealt, dir, point);
        }

        private void HitFeedback(Fighter attacker, Fighter victim, float dealt, Vector2 dir, Vector2 point)
        {
            bool attackerLocal = attacker != null && attacker.IsLocal && attacker != victim;
            if (attackerLocal)
            {
                _ctx.Fx.DamageNumber(point, dealt, dealt >= 40f ? new Color(1f, 0.85f, 0.25f) : Color.white, dealt >= 40f);
                _ctx.Events.RaiseLocalHit(false);
                _ctx.Sfx.PlayUi(SfxId.HitTick, 0.7f);
            }
            if (victim.IsLocal)
            {
                _ctx.Fx.DamageNumber(victim.Position, dealt, new Color(1f, 0.35f, 0.3f), false);
                _ctx.Events.RaiseLocalDamaged(dealt, -dir);
                _ctx.CameraRig.AddShake(0.12f + dealt * 0.004f);
                _ctx.Haptics.Pulse(25);
            }
            _ctx.Sfx.Play(SfxId.HitFlesh, point, 0.7f);
        }

        private void HandleKill(Fighter killer, Fighter victim, WeaponId weapon, Vector2 dir, float force)
        {
            var dropped = victim.Weapons.Primary;
            Vector2 at = victim.Position;
            var outcome = Rules.RecordKill(killer != null ? killer.Id : -1, victim.Id, Elapsed);
            Net?.HostKill(killer, victim, weapon, dir, force, Elapsed);
            KillPresentation(killer, victim, weapon, dir, force, outcome);
            _ctx.Chat?.OnKill(killer, victim);
            if (dropped != null) Pickups.Drop(dropped, at);
            _respawnQueue.Add(victim);
        }

        /// <summary>Online client: the host recorded a kill.</summary>
        public void NetApplyKill(Fighter killer, Fighter victim, WeaponId weapon, Vector2 dir, float force, float time)
        {
            if (victim == null) return;
            var outcome = Rules.RecordKill(killer != null ? killer.Id : -1, victim.Id, time);
            if (!victim.Alive) return;
            KillPresentation(killer, victim, weapon, dir, force, outcome);
        }

        private void KillPresentation(Fighter killer, Fighter victim, WeaponId weapon, Vector2 dir, float force, KillOutcome outcome)
        {
            victim.Die(dir, force);
            _ctx.Events.RaiseKilled(new KillInfo(killer, victim, weapon, outcome));

            if (killer != null && killer != victim && !outcome.TeamKill)
            {
                string multi = MatchRules.MultiKillName(outcome.MultiKill);
                string streak = MatchRules.StreakName(outcome.KillerStreak);
                if (killer.IsLocal && !Spectator)
                {
                    if (multi != null) _ctx.Events.Announce(multi, new Color(1f, 0.6f, 0.15f), true);
                    else if (streak != null) _ctx.Events.Announce(streak, new Color(1f, 0.35f, 0.25f), true);
                    if (multi != null || streak != null) _ctx.Sfx.PlayUi(SfxId.UiLevelUp, 0.5f);
                    _ctx.Events.RaiseLocalHit(true);
                    _ctx.Sfx.PlayUi(SfxId.KillDing, 1f);
                    _ctx.Sfx.PlayUi(SfxId.UiCoin, 0.7f);
                    _ctx.Fx.CoinPop(victim.Position);
                    _ctx.CameraRig.Punch(multi != null ? 0.11f : 0.07f);
                    _ctx.Haptics.Pulse(45);
                    _ctx.CameraRig.AddShake(0.2f);
                    if (Net == null) TimeControl.Slow(0.06f, 0.05f); // hit-stop; offline only (online never touches time)
                }
                else if (streak != null && !Spectator)
                {
                    _ctx.Events.RaiseToast(killer.DisplayName + " is " + streak + "!");
                }
            }

            if (victim.IsLocal && !Spectator)
            {
                if (killer != null && killer != victim) _ctx.CameraRig.FollowTemporary(killer, 1.6f);
                _ctx.Haptics.Pulse(70);
                _ctx.CameraRig.Punch(0.09f);
                if (Net == null) TimeControl.Slow(0.55f, 0.3f); // death slow-mo; offline only like the hit-stop
            }
            if (Spectator && _ctx.CameraRig.Following == victim && killer != null && killer != victim)
                _ctx.CameraRig.Follow(killer, false);
        }

        // ================================================================== spawning

        private Vector2 PickSpawn(Fighter f, bool initial)
        {
            var spawns = _map.Data.Spawns;
            Vector2 best = spawns[UnityEngine.Random.Range(0, spawns.Count)];
            float bestScore = float.MinValue;
            for (int i = 0; i < spawns.Count; i++)
            {
                Vector2 s = spawns[i];
                if (initial && Rules.IsTeamMode && (s.x < _map.Data.Center.x) != (f.Team == 0)) continue;
                float nearestEnemy = 999f;
                bool occupied = false;
                for (int k = 0; k < _fighters.Count; k++)
                {
                    var o = _fighters[k];
                    if (o == f || !o.Alive) continue;
                    float d = Vector2.Distance(o.Position, s);
                    if (d < 1.2f) occupied = true;
                    if (AreEnemies(f, o)) nearestEnemy = Mathf.Min(nearestEnemy, d);
                }
                if (occupied) continue;
                float score = Mathf.Min(nearestEnemy, 20f) + UnityEngine.Random.Range(0f, 6f);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = s;
                }
            }
            return best;
        }

        /// <summary>Host: bring a fighter in now (a late joiner who finished loading).</summary>
        public void HostSpawnNow(Fighter f)
        {
            if (f == null || f.Alive || CurrentPhase == Phase.Ended) return;
            _respawnQueue.Remove(f);
            Vector2 pos = PickSpawn(f, false);
            f.Spawn(pos);
            f.Frozen = CurrentPhase != Phase.Playing;
            Net?.HostSpawn(f, pos);
        }

        /// <summary>Online client: the host respawned someone.</summary>
        public void NetSpawn(Fighter f, Vector2 pos)
        {
            if (f == null) return;
            f.Spawn(pos);
            f.Frozen = CurrentPhase != Phase.Playing;
            if (f.IsLocal) _ctx.CameraRig.Follow(f, false);
        }

        private void TickRespawns()
        {
            float delay = _ctx.Config.Player.RespawnDelay;
            for (int i = _respawnQueue.Count - 1; i >= 0; i--)
            {
                var f = _respawnQueue[i];
                if (Time.time - f.DeathTime < delay) continue;
                _respawnQueue.RemoveAt(i);
                Vector2 pos = PickSpawn(f, false);
                f.Spawn(pos);
                Net?.HostSpawn(f, pos);
                if (f.IsLocal) _ctx.CameraRig.Follow(f, false);
            }
        }

        // ================================================================== clock

        /// <summary>Online client: the host's phase and clock from a snapshot.</summary>
        public void NetSyncClock(Phase hostPhase, bool hold, float countdown, float elapsed)
        {
            if (CurrentPhase == Phase.Countdown)
            {
                if (hostPhase == Phase.Playing) StartFight(elapsed);
                else
                {
                    _hold = hold;
                    if (hold) { _countdown = 3.2f; _lastCount = 4; }
                    else if (Mathf.Abs(_countdown - countdown) > 0.15f) _countdown = countdown;
                }
            }
            else if (CurrentPhase == Phase.Playing && hostPhase == Phase.Playing && Mathf.Abs(Elapsed - elapsed) > 0.25f)
            {
                Elapsed = elapsed;
            }
        }

        /// <summary>Online client: the host ended the match.</summary>
        public void NetEnd()
        {
            if (CurrentPhase != Phase.Ended) EndMatch();
        }

        private void StartFight(float elapsed = 0f)
        {
            CurrentPhase = Phase.Playing;
            Elapsed = elapsed;
            _ctx.Events.Announce("FIGHT!", new Color(1f, 0.85f, 0.25f), true);
            _ctx.Sfx.PlayUi(SfxId.Go, 1f);
            for (int i = 0; i < _fighters.Count; i++) _fighters[i].Frozen = false;
        }

        /// <summary>Late join: the match is already running.</summary>
        public void NetJoinRunning(Phase phase, float elapsed)
        {
            if (phase == Phase.Playing)
            {
                CurrentPhase = Phase.Playing;
                Elapsed = elapsed;
                for (int i = 0; i < _fighters.Count; i++) _fighters[i].Frozen = false;
            }
        }

        private void Update()
        {
            if (_ctx == null) return;
            float dt = Time.deltaTime;
            AnimateWorld(dt);

            switch (CurrentPhase)
            {
                case Phase.Countdown:
                    if (Net != null && Net.IsHost) _hold = Net.HoldCountdown;
                    if (_hold)
                    {
                        _countdown = 3.2f;
                        _lastCount = 4;
                        if (!_holdShown)
                        {
                            _holdShown = true;
                            _ctx.Events.Announce("WAITING FOR PLAYERS", new Color(0.62f, 0.85f, 1f), false);
                        }
                        break;
                    }
                    _countdown -= dt;
                    int n = Mathf.CeilToInt(_countdown);
                    if (n != _lastCount && n >= 1 && n <= 3)
                    {
                        _lastCount = n;
                        _ctx.Events.Announce(n.ToString(), Color.white, true);
                        _ctx.Sfx.PlayUi(SfxId.Beep, 0.8f);
                    }
                    if (_countdown <= 0f && IsAuthority) StartFight();
                    break;

                case Phase.Playing:
                    Elapsed += dt;
                    Pickups.Tick(_fighters, dt);
                    PowerUps.Tick(_fighters, dt);
                    if (IsAuthority) TickRespawns();
                    TickLocalFeedback(dt);
                    if (Spectator) TickSpectator(dt);
                    else if (IsAuthority && (Rules.IsTimeUp(Elapsed) || Rules.IsKillLimitReached())) EndMatch();
                    break;

                case Phase.Ended:
                    _endTimer -= Time.unscaledDeltaTime;
                    if (_endTimer <= 0f && _endTimer > -100f)
                    {
                        _endTimer = -1000f;
                        Ended?.Invoke(this);
                    }
                    break;
            }
        }

        private void TickLocalFeedback(float dt)
        {
            if (Local == null || Local.IsBot) return;
            var cur = Local.Weapons.Current.Stats;
            _ctx.CameraRig.SetZoom(Local.Alive && cur.ScopeSize > 0f && Local.Input.HasAim ? cur.ScopeSize : 0f);
            if (Local.Alive && Local.Health01 < 0.3f)
            {
                _heartbeatT -= dt;
                if (_heartbeatT <= 0f)
                {
                    _heartbeatT = 0.95f;
                    _ctx.Sfx.PlayUi(SfxId.Heartbeat, 0.8f);
                }
            }
        }

        private void TickSpectator(float dt)
        {
            _spectateSwitchT -= dt;
            var cur = _ctx.CameraRig.Following;
            if (_spectateSwitchT <= 0f || cur == null || !cur.Alive)
            {
                _spectateSwitchT = 9f;
                for (int tries = 0; tries < 8; tries++)
                {
                    var f = _fighters[UnityEngine.Random.Range(0, _fighters.Count)];
                    if (f.Alive) { _ctx.CameraRig.Follow(f, false); break; }
                }
            }
        }

        private void AnimateWorld(float dt)
        {
            float t = Time.time;
            for (int i = 0; i < _map.Canopies.Count; i++)
            {
                var c = _map.Canopies[i];
                c.localRotation = Quaternion.Euler(0f, 0f, c.localEulerAngles.z + Mathf.Sin(t * 0.9f + i * 1.7f) * 4f * dt);
            }
            for (int i = 0; i < _map.Lamps.Count; i++)
                _map.Lamps[i].intensity = 1.15f + Mathf.PerlinNoise(t * 3f, i * 3.1f) * 0.25f;

            // Tree crowns fade while the local player stands under them, so you never lose yourself.
            Vector2 me = Local != null && Local.Alive ? Local.Position : new Vector2(-999f, -999f);
            for (int i = 0; i < _map.CanopyRenderers.Count; i++)
            {
                var r = _map.CanopyRenderers[i];
                var b = r.bounds;
                bool under = me.x > b.min.x + 0.15f && me.x < b.max.x - 0.15f && me.y > b.min.y + 0.1f && me.y < b.max.y;
                var c = r.color;
                float target = under ? 0.38f : 1f;
                if (!Mathf.Approximately(c.a, target))
                {
                    c.a = Mathf.MoveTowards(c.a, target, dt * 4f);
                    r.color = c;
                }
            }
            if (_map.Water != null)
            {
                float w = 0.92f + Mathf.Sin(t * 1.6f) * 0.06f;
                _map.Water.color = new Color(w, w + 0.03f, 1f);
            }
        }

        private void EndMatch()
        {
            CurrentPhase = Phase.Ended;
            _endTimer = 1.8f;
            for (int i = 0; i < _fighters.Count; i++) _fighters[i].Frozen = true;
            if (Net == null) TimeControl.Slow(1.4f, 0.3f);
            else if (Net.IsHost) Net.HostEnd();
            _ctx.CameraRig.SetZoom(0f);
            _ctx.Events.Announce("MATCH OVER", new Color(1f, 0.85f, 0.3f), true);
            _ctx.Sfx.PlayUi(SfxId.Go, 1f);
        }

        /// <summary>Winner line for the results screen.</summary>
        public string WinnerText(out bool localWon)
        {
            localWon = false;
            if (Rules.IsTeamMode)
            {
                int w = Rules.WinningTeam();
                if (w < 0) return "DRAW!";
                localWon = Local != null && Local.Team == w;
                return w == 0 ? "RED TEAM WINS!" : "BLUE TEAM WINS!";
            }
            Rules.GetStandings(_standings);
            if (_standings.Count == 0) return "";
            var top = _standings[0];
            localWon = Local != null && top.Id == Local.Id;
            return localWon ? "YOU WIN!" : top.Name + " WINS!";
        }

        private void OnDestroy()
        {
            if (_ctx == null) return;
            if (_ctx.Match == this) _ctx.Match = null;
            if (_ctx.Projectiles) _ctx.Projectiles.Clear();
            if (_ctx.Fx) _ctx.Fx.Clear();
            if (_ctx.Sfx) { _ctx.Sfx.ClearPending(); _ctx.Sfx.Ground = Surface.Grass; }
            if (_ctx.CameraRig) _ctx.CameraRig.SetZoom(0f);
        }
    }
}
