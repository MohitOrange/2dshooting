using UnityEngine;

namespace NeonBrawl
{
    /// <summary>The ten power-ups. New ids go at the end (saved levels and network messages use the number).</summary>
    public enum PowerId : byte { Turbo, Ghost, Shield, Ammo, Surge, Pick, Frost, Rage, Magnet, Eagle, Count }

    /// <summary>One power's look and numbers. Every level makes it last longer (or reach further).</summary>
    public sealed class PowerDef
    {
        public PowerId Id;
        public string Name, Short, About;
        public Color Color;
        /// <summary>Seconds at level 1 and extra seconds per level (0 = instant power).</summary>
        public float Duration, DurationPerLevel;
        /// <summary>Second number shown on the upgrade card (speed, radius, …) and how it grows.</summary>
        public float Value, ValuePerLevel;
        public string ValueName, ValueUnit;

        public float DurationAt(int level) => Duration + DurationPerLevel * (Mathf.Clamp(level, 1, Powers.MaxLevel) - 1);
        public float ValueAt(int level) => Value + ValuePerLevel * (Mathf.Clamp(level, 1, Powers.MaxLevel) - 1);
    }

    /// <summary>
    /// Power-up rules shared by the match, the HUD and the upgrade screen.
    /// Upgrades cost coins and double every level (200 → 400 → 800 → 1600 → 3200).
    /// </summary>
    public static class Powers
    {
        public const int MaxLevel = 6;
        public const int FirstUpgradeCost = 200;
        public const int Count = (int)PowerId.Count;

        public static readonly PowerDef[] All =
        {
            new PowerDef { Id = PowerId.Turbo, Name = "TURBO BOOTS", Short = "TURBO", About = "Run much faster",
                Color = new Color(1f, 0.6f, 0.16f), Duration = 6f, DurationPerLevel = 1.5f, Value = 55f, ValuePerLevel = 5f, ValueName = "SPEED", ValueUnit = "%" },
            new PowerDef { Id = PowerId.Ghost, Name = "GHOST CLOAK", Short = "GHOST", About = "Enemies cannot see you",
                Color = new Color(0.72f, 0.5f, 1f), Duration = 5f, DurationPerLevel = 1.2f },
            new PowerDef { Id = PowerId.Shield, Name = "IRON SHIELD", Short = "SHIELD", About = "Bullets bounce off: no damage",
                Color = new Color(0.3f, 0.72f, 1f), Duration = 4f, DurationPerLevel = 1f },
            new PowerDef { Id = PowerId.Ammo, Name = "ENDLESS AMMO", Short = "AMMO", About = "Shoot forever, no reloading",
                Color = new Color(1f, 0.84f, 0.2f), Duration = 6f, DurationPerLevel = 1.5f },
            new PowerDef { Id = PowerId.Surge, Name = "LIFE SURGE", Short = "LIFE", About = "Full health that keeps refilling",
                Color = new Color(0.35f, 0.95f, 0.45f), Duration = 5f, DurationPerLevel = 1f, Value = 40f, ValuePerLevel = 8f, ValueName = "HEAL", ValueUnit = "/S" },
            new PowerDef { Id = PowerId.Pick, Name = "POWER PICK", Short = "PICK", About = "Choose the power you need",
                Color = new Color(1f, 0.42f, 0.72f), Value = 2f, ValuePerLevel = 1f, ValueName = "CHOICES", ValueUnit = "" },
            new PowerDef { Id = PowerId.Frost, Name = "FROST NOVA", Short = "FROST", About = "Freeze enemies around you",
                Color = new Color(0.55f, 0.92f, 1f), Duration = 1.2f, DurationPerLevel = 0.35f, Value = 4.5f, ValuePerLevel = 0.4f, ValueName = "RADIUS", ValueUnit = "M" },
            new PowerDef { Id = PowerId.Rage, Name = "RAGE", Short = "RAGE", About = "Double damage",
                Color = new Color(1f, 0.3f, 0.26f), Duration = 6f, DurationPerLevel = 1.2f, Value = 2f, ValuePerLevel = 0f, ValueName = "DAMAGE", ValueUnit = "X" },
            new PowerDef { Id = PowerId.Magnet, Name = "LOOT MAGNET", Short = "MAGNET", About = "Pulls health and ammo to you",
                Color = new Color(0.95f, 0.32f, 0.5f), Duration = 10f, DurationPerLevel = 2f, Value = 4f, ValuePerLevel = 0.5f, ValueName = "RANGE", ValueUnit = "M" },
            new PowerDef { Id = PowerId.Eagle, Name = "EAGLE EYE", Short = "EAGLE", About = "See all enemies through walls",
                Color = new Color(1f, 0.92f, 0.4f), Duration = 8f, DurationPerLevel = 2f },
        };

        public static PowerDef Get(PowerId id) => All[Mathf.Clamp((int)id, 0, Count - 1)];

        /// <summary>Coins to go from <paramref name="level"/> to the next one, or 0 at the top.</summary>
        public static int UpgradeCost(int level) => level >= MaxLevel ? 0 : FirstUpgradeCost << (Mathf.Max(1, level) - 1);

        /// <summary>How many powers POWER PICK offers: 2 at level 1, 3 at level 2, 4 from level 3.</summary>
        public static int PickChoices(int level) => Mathf.Clamp(level + 1, 2, 4);

        /// <summary>From level 4 POWER PICK also makes the chosen power stronger: +1, +2, +3 levels.</summary>
        public static int PickBonus(int level) => Mathf.Max(0, Mathf.Clamp(level, 1, MaxLevel) - 3);

        /// <summary>Turbo speed multiplier at a level (1.55 → 1.80).</summary>
        public static float TurboSpeed(int level) => 1f + Get(PowerId.Turbo).ValueAt(level) / 100f;

        /// <summary>The number on an upgrade card, like "8.5 S" or "+70%".</summary>
        public static string Stat(PowerDef d, int level, bool duration)
        {
            if (duration) return d.DurationAt(level).ToString(d.DurationAt(level) % 1f == 0f ? "0" : "0.0") + " S";
            float v = d.ValueAt(level);
            switch (d.Id)
            {
                case PowerId.Turbo: return "+" + Mathf.RoundToInt(v) + "%";
                case PowerId.Pick: return PickChoices(level) + (PickBonus(level) > 0 ? " +" + PickBonus(level) + "LV" : " PICKS");
                case PowerId.Rage: return "x2";
                default: return (v % 1f == 0f ? v.ToString("0") : v.ToString("0.0")) + " " + d.ValueUnit;
            }
        }
    }
}
