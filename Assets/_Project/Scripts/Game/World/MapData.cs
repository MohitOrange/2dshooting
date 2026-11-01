using System.Collections.Generic;
using NeonBrawl.Core;
using UnityEngine;

namespace NeonBrawl
{
    public struct PickupSpot
    {
        public PickupType Type;
        public WeaponId Weapon;
        public Vector2 Position;
    }

    /// <summary>Parsed map: cells, spawns, pickup spots and the bot navigation grid. 1 cell = 1 unit.</summary>
    public sealed class MapData
    {
        public readonly string Name;
        /// <summary>Art theme: index into ArtCatalog.Themes (T1/P1 = 0 … T5/P5 = 4).</summary>
        public readonly int Theme;
        public readonly int Width;
        public readonly int Height;
        public readonly List<Vector2> Spawns = new List<Vector2>(16);
        public readonly List<Vector2> Lamps = new List<Vector2>(16);
        public readonly List<PickupSpot> Pickups = new List<PickupSpot>(24);
        public readonly GridPathfinder Grid;

        private readonly char[] _cells;

        public MapData(string name, string[] rows, WeaponId[] weaponOrder, int theme = 1)
        {
            Name = name;
            Theme = theme;
            Height = rows.Length;
            Width = rows[0].Length;
            _cells = new char[Width * Height];
            Grid = new GridPathfinder(Width, Height);
            int weaponIndex = 0;
            for (int r = 0; r < Height; r++)
            {
                string row = rows[r];
                int y = Height - 1 - r;
                for (int x = 0; x < Width; x++)
                {
                    char c = x < row.Length ? row[x] : '#';
                    _cells[y * Width + x] = c;
                    Grid.SetWalkable(x, y, !IsBlocking(c));
                    Vector2 p = CellCenter(x, y);
                    switch (c)
                    {
                        case 'S': Spawns.Add(p); break;
                        case 'L': Lamps.Add(p); break;
                        case 'W':
                            Pickups.Add(new PickupSpot { Type = PickupType.Weapon, Weapon = weaponOrder[weaponIndex++ % weaponOrder.Length], Position = p });
                            break;
                        case 'H': Pickups.Add(new PickupSpot { Type = PickupType.Health, Position = p }); break;
                        case 'A': Pickups.Add(new PickupSpot { Type = PickupType.Armor, Position = p }); break;
                        case 'M': Pickups.Add(new PickupSpot { Type = PickupType.Ammo, Position = p }); break;
                        case 'G': Pickups.Add(new PickupSpot { Type = PickupType.Grenade, Position = p }); break;
                    }
                }
            }
        }

        public char At(int x, int y) => x < 0 || y < 0 || x >= Width || y >= Height ? '#' : _cells[y * Width + x];

        public static bool IsBlocking(char c) => c == '#' || c == 'c' || c == 'b' || c == 's' || c == 'T' || c == 'o' || c == '~';

        /// <summary>Water blocks walking but not bullets or grenades.</summary>
        public static bool IsWater(char c) => c == '~';

        public static Vector2 CellCenter(int x, int y) => new Vector2(x + 0.5f, y + 0.5f);

        public Vector2Int WorldToCell(Vector2 p) =>
            new Vector2Int(Mathf.Clamp(Mathf.FloorToInt(p.x), 0, Width - 1), Mathf.Clamp(Mathf.FloorToInt(p.y), 0, Height - 1));

        public Vector2 Center => new Vector2(Width * 0.5f, Height * 0.5f);
    }
}
