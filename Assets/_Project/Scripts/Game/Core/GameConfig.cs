using System;
using System.Collections.Generic;
using UnityEngine;

namespace NeonBrawl
{
    /// <summary>Tunable numbers for one weapon. Stats for pistol/SMG/shotgun/sniper/rocket come from neon-brawl.</summary>
    [Serializable]
    public sealed class WeaponStats
    {
        public WeaponId Id;
        public string DisplayName = "";
        public FireKind Kind;
        [Min(0f)] public float Damage = 10f;
        [Min(0.01f), Tooltip("Seconds between shots")] public float Interval = 0.2f;
        [Min(1)] public int MagSize = 10;
        [Tooltip("-1 = infinite")] public int Reserve = 30;
        [Min(0f)] public float ReloadTime = 1.5f;
        [Min(0f)] public float SpreadDeg = 2f;
        [Min(1)] public int Pellets = 1;
        [Min(0.5f)] public float Range = 14f;
        public float ProjectileSpeed;
        public float SplashRadius;
        public int Bounces;
        public int ChainCount;
        public float ChainRange;
        [Tooltip("Push applied to the victim (units/s)")] public float Knockback = 2f;
        [Tooltip("Gun visual kick-back (units)")] public float Recoil = 0.08f;
        [Tooltip("Camera kick for the shooter (units)")] public float CameraKick = 0.05f;
        [Range(0f, 1f)] public float Shake = 0.05f;
        [Range(0.5f, 1.2f)] public float MoveMultiplier = 1f;
        [Tooltip("Host-side damage cap per hit (anti-cheat)")] public float MaxHitDamage = 30f;
        [Tooltip("Camera size while scoped. 0 = no scope")] public float ScopeSize;
        public float BurnDps;
        public float BurnTime;
        [Tooltip("Bots prefer higher ranks")] public int Rank;
        public SfxId Sound = SfxId.ShotLight;
        public bool EjectShell = true;
        public Color TracerColor = new Color(1f, 0.88f, 0.55f);
        public Color GunColor = new Color(0.2f, 0.21f, 0.23f);
        public float GunLength = 0.35f;
        public float GunWidth = 0.1f;

        public bool InfiniteReserve => Reserve < 0;
    }

    [Serializable]
    public sealed class PlayerTuning
    {
        public float Radius = 0.38f;
        public float MoveSpeed = 5.2f;
        public float Acceleration = 60f;
        public float MaxHealth = 100f;
        public float MaxArmor = 100f;
        [Range(0f, 1f)] public float ArmorAbsorb = 0.5f;
        public float DashSpeed = 15f;
        public float DashTime = 0.16f;
        public float DashCooldown = 2.2f;
        public float KnockbackDamping = 9f;
        public float RespawnDelay = 3f;
        public float SpawnProtection = 2f;
        public int StartGrenades = 1;
        public int MaxGrenades = 3;
        public float PickupRadius = 0.8f;
        public float HealthPickup = 50f;
        public float ArmorPickup = 50f;
        public float PickupRespawn = 14f;
        public float DroppedWeaponLife = 15f;
    }

    [Serializable]
    public sealed class GrenadeTuning
    {
        public float Damage = 95f;
        public float Radius = 3f;
        public float Fuse = 2f;
        public float ThrowSpeed = 12f;
        public float MaxThrowDistance = 9f;
        public float Knockback = 14f;
        public float MaxHitDamage = 100f;
        public float BarrelDamage = 80f;
        public float BarrelRadius = 2.8f;
    }

    /// <summary>Bot skill. Easy/normal/hard numbers ported from neon-brawl DIFF (ranges rescaled to top-down units).</summary>
    [Serializable]
    public sealed class BotTuning
    {
        public string Name = "Normal";
        [Tooltip("Aim error in degrees")] public float AimErrorDeg = 8.6f;
        [Tooltip("Seconds before firing at a newly seen enemy")] public float ReactionTime = 0.5f;
        public float ViewRange = 12f;
        public float ThinkInterval = 0.32f;
        public float BurstTime = 0.75f;
        public float BurstPause = 0.5f;
        [Tooltip("Degrees per second")] public float AimTurnSpeed = 420f;
        [Range(0f, 1f)] public float GrenadeChance = 0.15f;
        [Range(0f, 1f)] public float DashChance = 0.25f;

        public static BotTuning[] Defaults() => new[]
        {
            new BotTuning { Name = "Easy", AimErrorDeg = 17f, ReactionTime = 0.9f, ViewRange = 9f, ThinkInterval = 0.5f, BurstTime = 0.5f, BurstPause = 0.8f, AimTurnSpeed = 260f, GrenadeChance = 0f, DashChance = 0.05f },
            new BotTuning { Name = "Normal", AimErrorDeg = 8.6f, ReactionTime = 0.5f, ViewRange = 12f, ThinkInterval = 0.32f, BurstTime = 0.75f, BurstPause = 0.5f, AimTurnSpeed = 420f, GrenadeChance = 0.15f, DashChance = 0.25f },
            new BotTuning { Name = "Hard", AimErrorDeg = 3.4f, ReactionTime = 0.22f, ViewRange = 15f, ThinkInterval = 0.18f, BurstTime = 1.3f, BurstPause = 0.3f, AimTurnSpeed = 700f, GrenadeChance = 0.3f, DashChance = 0.45f },
        };
    }

    /// <summary>
    /// All gameplay tuning in one asset (Assets/_Project/Resources/GameConfig.asset). Edit it in the
    /// Inspector; "Reset To Defaults" in its context menu restores the shipped numbers.
    /// </summary>
    [CreateAssetMenu(menuName = "Neon Brawl/Game Config", fileName = "GameConfig")]
    public sealed class GameConfig : ScriptableObject
    {
        public PlayerTuning Player = new PlayerTuning();
        public GrenadeTuning Grenade = new GrenadeTuning();
        public BotTuning[] Bots = BotTuning.Defaults();
        public List<WeaponStats> Weapons = new List<WeaponStats>();

        [NonSerialized] private WeaponStats[] _lookup;

        public static readonly WeaponId[] PickupWeapons =
        {
            WeaponId.HandCannon, WeaponId.Smg, WeaponId.Rifle, WeaponId.Shotgun, WeaponId.Sniper, WeaponId.Lmg,
            WeaponId.Rocket, WeaponId.Flamethrower, WeaponId.Plasma, WeaponId.Tesla
        };

        public static GameConfig Load()
        {
            var cfg = Resources.Load<GameConfig>("GameConfig");
            if (cfg == null)
            {
                cfg = CreateInstance<GameConfig>();
                cfg.ResetToDefaults();
            }
            return cfg;
        }

        public WeaponStats Get(WeaponId id)
        {
            if (_lookup == null) BuildLookup();
            return _lookup[(int)id];
        }

        public BotTuning Bot(int difficulty) => Bots[Mathf.Clamp(difficulty, 0, Bots.Length - 1)];

        [ContextMenu("Reset To Defaults")]
        public void ResetToDefaults()
        {
            Player = new PlayerTuning();
            Grenade = new GrenadeTuning();
            Bots = BotTuning.Defaults();
            Weapons = DefaultWeapons();
            _lookup = null;
        }

        private void OnValidate() => _lookup = null;

        private void BuildLookup()
        {
            int n = Enum.GetValues(typeof(WeaponId)).Length;
            _lookup = new WeaponStats[n];
            if (Weapons != null)
                for (int i = 0; i < Weapons.Count; i++)
                    if (Weapons[i] != null && (int)Weapons[i].Id < n) _lookup[(int)Weapons[i].Id] = Weapons[i];
            List<WeaponStats> defaults = null;
            for (int i = 0; i < n; i++)
            {
                if (_lookup[i] != null) continue;
                defaults ??= DefaultWeapons();
                _lookup[i] = defaults.Find(w => (int)w.Id == i);
            }
        }

        private static WeaponStats W(WeaponId id, string name, FireKind kind, float dmg, float interval, int mag, int reserve,
            float reload, float spread, float range, float knock, float cap, int rank, SfxId sound, float len, float width, Color gun)
        {
            return new WeaponStats
            {
                Id = id, DisplayName = name, Kind = kind, Damage = dmg, Interval = interval, MagSize = mag, Reserve = reserve,
                ReloadTime = reload, SpreadDeg = spread, Range = range, Knockback = knock, MaxHitDamage = cap, Rank = rank,
                Sound = sound, GunLength = len, GunWidth = width, GunColor = gun
            };
        }

        public static List<WeaponStats> DefaultWeapons()
        {
            var list = new List<WeaponStats>(16);
            var pistol = W(WeaponId.Pistol, "PISTOL", FireKind.Hitscan, 18, 0.24f, 12, -1, 1.0f, 2f, 14, 2.5f, 30, 0, SfxId.ShotLight, 0.26f, 0.09f, new Color(0.22f, 0.23f, 0.25f));
            pistol.Recoil = 0.08f; pistol.CameraKick = 0.05f; pistol.Shake = 0.05f;
            list.Add(pistol);

            var hc = W(WeaponId.HandCannon, "HAND CANNON", FireKind.Hitscan, 40, 0.55f, 7, 28, 1.4f, 1.2f, 16, 7f, 50, 3, SfxId.ShotHeavy, 0.34f, 0.12f, new Color(0.62f, 0.63f, 0.66f));
            hc.Recoil = 0.18f; hc.CameraKick = 0.16f; hc.Shake = 0.16f;
            list.Add(hc);

            var smg = W(WeaponId.Smg, "SMG", FireKind.Hitscan, 11, 0.085f, 32, 96, 1.4f, 5f, 12, 1.4f, 18, 2, SfxId.ShotLight, 0.42f, 0.11f, new Color(0.25f, 0.27f, 0.3f));
            smg.Recoil = 0.05f; smg.CameraKick = 0.04f; smg.Shake = 0.04f;
            list.Add(smg);

            var rifle = W(WeaponId.Rifle, "ASSAULT RIFLE", FireKind.Hitscan, 16, 0.11f, 30, 90, 1.6f, 2.5f, 18, 2.2f, 24, 4, SfxId.ShotHeavy, 0.62f, 0.12f, new Color(0.36f, 0.3f, 0.2f));
            rifle.Recoil = 0.07f; rifle.CameraKick = 0.06f; rifle.Shake = 0.05f; rifle.MoveMultiplier = 0.95f;
            list.Add(rifle);

            var sg = W(WeaponId.Shotgun, "SHOTGUN", FireKind.Hitscan, 10, 0.75f, 6, 18, 1.6f, 11f, 8, 4.5f, 15, 4, SfxId.ShotShotgun, 0.58f, 0.13f, new Color(0.45f, 0.3f, 0.17f));
            sg.Pellets = 7; sg.Recoil = 0.3f; sg.CameraKick = 0.3f; sg.Shake = 0.32f; sg.MoveMultiplier = 0.95f; sg.TracerColor = new Color(1f, 0.75f, 0.42f);
            list.Add(sg);

            var sn = W(WeaponId.Sniper, "SNIPER", FireKind.Hitscan, 76, 1.1f, 5, 10, 2f, 0.2f, 32, 9f, 115, 5, SfxId.ShotSniper, 0.85f, 0.11f, new Color(0.3f, 0.35f, 0.2f));
            sn.Recoil = 0.3f; sn.CameraKick = 0.3f; sn.Shake = 0.3f; sn.MoveMultiplier = 0.9f; sn.ScopeSize = 9.5f; sn.TracerColor = new Color(1f, 0.97f, 0.85f);
            list.Add(sn);

            var lmg = W(WeaponId.Lmg, "MACHINE GUN", FireKind.Hitscan, 14, 0.09f, 80, 80, 3f, 4.5f, 16, 2f, 22, 4, SfxId.ShotHeavy, 0.72f, 0.15f, new Color(0.2f, 0.22f, 0.2f));
            lmg.Recoil = 0.08f; lmg.CameraKick = 0.07f; lmg.Shake = 0.06f; lmg.MoveMultiplier = 0.85f;
            list.Add(lmg);

            var rk = W(WeaponId.Rocket, "ROCKET LAUNCHER", FireKind.Projectile, 88, 1f, 1, 5, 1.4f, 1f, 30, 14f, 90, 5, SfxId.Rocket, 0.8f, 0.17f, new Color(0.33f, 0.38f, 0.22f));
            rk.ProjectileSpeed = 14f; rk.SplashRadius = 2.6f; rk.Recoil = 0.35f; rk.CameraKick = 0.35f; rk.Shake = 0.25f; rk.MoveMultiplier = 0.9f; rk.EjectShell = false;
            list.Add(rk);

            var fl = W(WeaponId.Flamethrower, "FLAMETHROWER", FireKind.Flame, 6, 0.06f, 100, 100, 2f, 14f, 4.5f, 0.6f, 12, 3, SfxId.Flame, 0.6f, 0.15f, new Color(0.62f, 0.2f, 0.15f));
            fl.BurnDps = 6f; fl.BurnTime = 2f; fl.Recoil = 0.02f; fl.CameraKick = 0.01f; fl.Shake = 0.02f; fl.EjectShell = false; fl.TracerColor = new Color(1f, 0.6f, 0.2f);
            list.Add(fl);

            var pl = W(WeaponId.Plasma, "PLASMA RIFLE", FireKind.Projectile, 22, 0.18f, 20, 60, 1.5f, 2f, 24, 3f, 30, 4, SfxId.Plasma, 0.6f, 0.13f, new Color(0.18f, 0.55f, 0.48f));
            pl.ProjectileSpeed = 22f; pl.Bounces = 1; pl.Recoil = 0.06f; pl.CameraKick = 0.05f; pl.Shake = 0.05f; pl.EjectShell = false; pl.TracerColor = new Color(0.4f, 1f, 0.55f);
            list.Add(pl);

            var ts = W(WeaponId.Tesla, "TESLA GUN", FireKind.Chain, 9, 0.12f, 40, 80, 1.8f, 0f, 10, 1f, 12, 3, SfxId.Tesla, 0.55f, 0.15f, new Color(0.75f, 0.45f, 0.2f));
            ts.ChainCount = 2; ts.ChainRange = 4f; ts.Recoil = 0.03f; ts.CameraKick = 0.02f; ts.Shake = 0.03f; ts.EjectShell = false; ts.TracerColor = new Color(0.55f, 0.85f, 1f);
            list.Add(ts);

            var kn = W(WeaponId.Knife, "KNIFE", FireKind.Melee, 45, 0.45f, 1, -1, 0f, 0f, 1.1f, 5f, 90, 0, SfxId.Swing, 0.3f, 0.07f, new Color(0.78f, 0.8f, 0.83f));
            kn.EjectShell = false;
            list.Add(kn);

            var gr = W(WeaponId.Grenade, "GRENADE", FireKind.Projectile, 95, 1f, 1, 0, 0f, 0f, 9, 14f, 100, 0, SfxId.Explosion, 0.2f, 0.2f, new Color(0.25f, 0.35f, 0.2f));
            gr.EjectShell = false;
            list.Add(gr);

            var br = W(WeaponId.Barrel, "BARREL", FireKind.Projectile, 80, 1f, 1, 0, 0f, 0f, 0, 14f, 90, 0, SfxId.Explosion, 0.2f, 0.2f, new Color(0.7f, 0.15f, 0.1f));
            br.EjectShell = false;
            list.Add(br);
            return list;
        }
    }
}
