using UnityEngine;

namespace NeonBrawl
{
    /// <summary>
    /// How a map theme uses its art: which ground tile goes where, and which props play which role.
    /// Indices point into <see cref="MapTheme.Tiles"/> (2×2 sheet, reading order) and
    /// <see cref="MapTheme.Props"/> (P sheet reading order; see Builds/art_sheets/P1..P5.png).
    /// </summary>
    public sealed class ThemeLook
    {
        public int Ground, Path, Floor, Wall, Water = -1;
        /// <summary>Theme sheet the floor / wall tile comes from (-1 = this theme). Lets the desert use forest planks indoors.</summary>
        public int FloorTheme = -1, WallTheme = -1;
        /// <summary>Optional different tile for the wall top face (e.g. snow on top of stone walls).</summary>
        public int WallTopTile = -1, WallTopTheme = -1;
        /// <summary>Raised walls: lighter top face, darker side face.</summary>
        public Color WallTop = new Color(0.92f, 0.9f, 0.86f), WallSide = new Color(0.5f, 0.46f, 0.42f);
        public Color GroundTint = Color.white, PathTint = Color.white, FloorTint = Color.white;
        public Color WallTint = new Color(0.6f, 0.6f, 0.62f); // used by the code-drawn fallback only
        /// <summary>Random 90° turns hide repeats on organic ground (sand, grass, snow), not on planks or plates.</summary>
        public bool TurnGround = true, TurnPath = true, TurnFloor;
        public int[] Trees = new int[0], Rocks = new int[0], Crates = new int[0], Covers = new int[0], Decor = new int[0];
        public int Lamp = -1;
        public Color Sun = new Color(1f, 0.96f, 0.88f);
        public float SunIntensity = 1f;
        public Color Background = new Color(0.07f, 0.06f, 0.05f);
        public Color DustColor = new Color(0.86f, 0.78f, 0.62f);

        private static ThemeLook[] _all;

        public static ThemeLook For(int theme)
        {
            if (_all == null) _all = Create();
            return _all[Mathf.Clamp(theme, 0, _all.Length - 1)];
        }

        private static ThemeLook[] Create() => new[]
        {
            // 0 · T1/P1 green outpost: grass, dirt, cobble, metal
            new ThemeLook
            {
                Ground = 0, Path = 1, Floor = 3, Wall = 2, WallTop = new Color(0.95f, 0.95f, 0.9f), WallSide = new Color(0.45f, 0.47f, 0.42f),
                Trees = new[] { 0, 1, 3 }, Rocks = new[] { 2, 6, 8 }, Crates = new[] { 4, 5, 9, 10, 11 }, Covers = new[] { 12 }, Decor = new[] { 7 },
                Sun = new Color(1f, 0.98f, 0.92f), Background = new Color(0.06f, 0.08f, 0.05f), DustColor = new Color(0.6f, 0.55f, 0.4f)
            },
            // 1 · T2/P2 desert: sand, cracked sand, water, dark rock
            new ThemeLook
            {
                Ground = 0, Path = 1, Floor = 3, FloorTheme = 4, FloorTint = new Color(1f, 0.92f, 0.82f), Wall = 1, Water = 2,
                WallTop = new Color(1f, 0.9f, 0.72f), WallSide = new Color(0.6f, 0.45f, 0.3f),
                Trees = new[] { 1, 0 }, Rocks = new[] { 3, 4, 2, 6 }, Crates = new[] { 8, 9 }, Covers = new[] { 7 }, Decor = new[] { 5 },
                Sun = new Color(1f, 0.94f, 0.82f), Background = new Color(0.08f, 0.06f, 0.04f)
            },
            // 2 · T3/P3 frost: snow, snow, ice, dark rock
            new ThemeLook
            {
                Ground = 0, Path = 1, Floor = 2, Wall = 2, WallTheme = 0, Water = 2, TurnFloor = true,
                WallTop = new Color(0.78f, 0.88f, 1f), WallSide = new Color(0.32f, 0.38f, 0.52f),
                Trees = new[] { 2, 0, 1 }, Rocks = new[] { 3, 7 }, Crates = new[] { 8, 9, 10, 11 }, Covers = new[] { 5, 6 }, Decor = new[] { 4 },
                Sun = new Color(0.92f, 0.97f, 1f), SunIntensity = 1.02f, Background = new Color(0.05f, 0.06f, 0.09f), DustColor = new Color(0.9f, 0.95f, 1f)
            },
            // 3 · T4/P4 steel factory: metal plates, hazard plate
            new ThemeLook
            {
                Ground = 0, Path = 2, Floor = 1, Wall = 3, TurnGround = false, TurnPath = false,
                WallTop = new Color(0.5f, 0.53f, 0.6f), WallSide = new Color(0.2f, 0.21f, 0.25f),
                Trees = new[] { 5 }, Rocks = new[] { 2, 5, 3 }, Crates = new[] { 0, 4, 8 }, Covers = new[] { 6, 9 }, Decor = new[] { 1 }, Lamp = 7,
                Sun = new Color(0.86f, 0.9f, 1f), SunIntensity = 0.92f, Background = new Color(0.04f, 0.05f, 0.06f), DustColor = new Color(0.55f, 0.55f, 0.58f)
            },
            // 4 · T5/P5 forest camp: grass, cobble, water, wood planks
            new ThemeLook
            {
                Ground = 0, Path = 1, Floor = 3, Wall = 1, Water = 2, TurnPath = false,
                WallTop = new Color(0.88f, 0.92f, 0.82f), WallSide = new Color(0.4f, 0.42f, 0.36f),
                Trees = new[] { 0, 1 }, Rocks = new[] { 2, 6, 3, 4 }, Crates = new int[0], Covers = new[] { 5, 7, 8, 9 }, Decor = new[] { 3, 4 },
                Sun = new Color(0.95f, 1f, 0.88f), Background = new Color(0.04f, 0.07f, 0.04f), DustColor = new Color(0.5f, 0.45f, 0.32f)
            },
        };
    }
}
