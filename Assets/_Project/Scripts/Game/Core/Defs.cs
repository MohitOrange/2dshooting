using UnityEngine;

namespace NeonBrawl
{
    /// <summary>Weapon ids. Grenade and Barrel are kill-feed sources, not pickups.</summary>
    public enum WeaponId { Pistol = 0, HandCannon, Smg, Rifle, Shotgun, Sniper, Lmg, Rocket, Flamethrower, Plasma, Tesla, Knife, Grenade, Barrel }

    public enum FireKind { Hitscan, Projectile, Flame, Chain, Melee }

    public enum PickupType { Weapon, Health, Armor, Ammo, Grenade }

    public enum SfxId
    {
        ShotLight, ShotHeavy, ShotShotgun, ShotSniper, Rocket, Plasma, Tesla, Flame, Explosion,
        HitFlesh, HitWall, Reload, Empty, Pickup, Dash, KillDing, HitTick, GrenadeBounce, Throw,
        Beep, Go, Swing, Death, Respawn, Heartbeat, Click,
        UiTap, UiBack, UiWhoosh, UiCoin, UiError, UiOpen, UiLevelUp,
        Step, Crackle, Thump,
        // recorded-style sounds (Resources/Sfx, made by Tools/Sfx/gen-sfx.js); new ids go at the end
        ShotSmg, ShotCannon, ShotLmg, Pump, Bolt, Shell, ShellPlastic, Ricochet, Whiz,
        StepSand, StepSnow, StepHard, Count
    }

    /// <summary>What the ground is made of, for footsteps.</summary>
    public enum Surface { Grass, Sand, Snow, Hard }

    /// <summary>Physics layers. Set by the project setup tool; numbers work even without names.</summary>
    public static class Layers
    {
        public const int Walls = 6;
        public const int Props = 7;
        public const int Fighters = 8;
        public const int Corpses = 9;
        /// <summary>Rivers and pools: block walking, not bullets or grenades.</summary>
        public const int Water = 10;

        public const int WallMask = 1 << Walls;
        public const int PropMask = 1 << Props;
        public const int FighterMask = 1 << Fighters;
        public const int SolidMask = WallMask | PropMask;
        public const int ShotMask = WallMask | PropMask | FighterMask;
    }

    /// <summary>Sprite sorting orders (all on the Default sorting layer).</summary>
    public static class SortOrder
    {
        public const int Ground = -100;
        public const int GroundDetail = -97;
        public const int Decals = -94;
        public const int Shells = -92;
        public const int WallShadow = -88;
        public const int Shadows = -86;
        public const int Pickups = -70;
        public const int Corpses = -40;
        public const int Props = -20;
        public const int Fighters = 0;
        public const int Projectiles = 10;
        public const int Walls = 20;
        public const int Fx = 30;
        public const int FxTop = 34;
        public const int Canopy = 40;
        public const int Fog = 60;
        public const int WorldUi = 70;
        public const int Crosshair = 75;
    }

    /// <summary>One frame of fighter intent, produced by a player, a bot, or (later) the network.</summary>
    public struct FighterInput
    {
        public Vector2 Move;
        public Vector2 Aim;
        public bool HasAim;
        public bool Fire;
        public bool Dash;
        public bool Reload;
        public bool Swap;
        public bool Grenade;
        public bool Melee;
        public bool Interact;
        public float ThrowDistance;

        public void ClearPulses()
        {
            Dash = Reload = Swap = Grenade = Melee = Interact = false;
        }
    }

    /// <summary>Decides a fighter's input each frame (player, bot, network proxy).</summary>
    public interface IFighterBrain
    {
        void Think(Fighter self, float dt, ref FighterInput input);
    }

    /// <summary>Anything that bullets and explosions can damage besides fighters (barrels, crates).</summary>
    public interface IDamageable
    {
        bool IsAlive { get; }
        void TakeDamage(float amount, Fighter attacker, Vector2 direction);
    }
}
