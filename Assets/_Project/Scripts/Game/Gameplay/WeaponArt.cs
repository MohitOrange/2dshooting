using UnityEngine;

namespace NeonBrawl
{
    /// <summary>
    /// Which painted gun (W1 = GunsA, W2 = GunsB, side views with the muzzle to the right) each weapon uses,
    /// where its grip is (0 = rear end, 1 = muzzle) and an optional tint for the sci-fi guns.
    /// Used for the gun in hand, weapon pickups, the HUD weapon panel and the kill feed.
    /// </summary>
    public static class WeaponArt
    {
        public struct Info
        {
            public Sprite Sprite;
            public float Grip;
            public Color Tint;
            public bool Valid => Sprite != null;
        }

        public static Info Get(ArtCatalog cat, WeaponId id)
        {
            if (cat == null) return default;
            switch (id)
            {
                case WeaponId.Pistol: return Make(cat.GunsB, 7, 0.24f, Color.white);
                case WeaponId.HandCannon: return Make(cat.GunsB, 8, 0.24f, Color.white);
                case WeaponId.Smg: return Make(cat.GunsB, 1, 0.42f, Color.white);
                case WeaponId.Rifle: return Make(cat.GunsA, 0, 0.42f, Color.white);
                case WeaponId.Shotgun: return Make(cat.GunsB, 0, 0.4f, Color.white);
                case WeaponId.Sniper: return Make(cat.GunsB, 6, 0.36f, Color.white);
                case WeaponId.Lmg: return Make(cat.GunsB, 5, 0.4f, Color.white);
                case WeaponId.Rocket: return Make(cat.GunsA, 6, 0.42f, new Color(0.72f, 0.92f, 0.5f));
                case WeaponId.Flamethrower: return Make(cat.GunsA, 1, 0.42f, new Color(1f, 0.6f, 0.42f));
                case WeaponId.Plasma: return Make(cat.GunsA, 4, 0.42f, new Color(0.55f, 1f, 0.85f));
                case WeaponId.Tesla: return Make(cat.GunsA, 7, 0.42f, new Color(0.62f, 0.82f, 1f));
                default: return default;
            }
        }

        private static Info Make(Sprite[] sheet, int index, float grip, Color tint) =>
            new Info { Sprite = ArtCatalog.At(sheet, index), Grip = grip, Tint = tint };
    }
}
