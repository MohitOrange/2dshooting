using System;

namespace NeonBrawl.Core
{
    /// <summary>Pure damage math shared by offline play, the online host and tests.</summary>
    public static class DamageModel
    {
        public readonly struct Split
        {
            public readonly float Health;
            public readonly float Armor;
            public Split(float health, float armor) { Health = health; Armor = armor; }
        }

        /// <summary>Armor soaks <paramref name="absorb"/> of the damage until it runs out.</summary>
        public static Split Apply(float damage, float armor, float absorb)
        {
            if (damage <= 0f) return new Split(0f, 0f);
            float toArmor = Math.Min(Math.Max(armor, 0f), damage * Clamp01(absorb));
            return new Split(damage - toArmor, toArmor);
        }

        /// <summary>Host-side anti-cheat cap per single hit (neon-brawl MAX_DMG).</summary>
        public static float Cap(float damage, float cap) => cap > 0f ? Math.Min(damage, cap) : damage;

        /// <summary>1 at the center, <paramref name="edgeFactor"/> at the edge, 0 outside.</summary>
        public static float SplashFalloff(float distance, float radius, float edgeFactor = 0.35f)
        {
            if (radius <= 0f || distance > radius) return 0f;
            float t = Clamp01(distance / radius);
            return 1f + (edgeFactor - 1f) * t;
        }

        private static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
    }
}
