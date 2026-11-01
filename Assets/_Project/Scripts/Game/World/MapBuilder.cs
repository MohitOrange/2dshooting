using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Tilemaps;

namespace NeonBrawl
{
    /// <summary>A built map in the scene.</summary>
    public sealed class MapInstance
    {
        public MapData Data;
        public GameObject Root;
        public Tilemap Water;
        public readonly List<Light2D> Lamps = new List<Light2D>(16);
        public readonly List<Transform> Canopies = new List<Transform>(16);
        public readonly List<SpriteRenderer> CanopyRenderers = new List<SpriteRenderer>(32);
    }

    /// <summary>Small helper for runtime-created 2D lights.</summary>
    public static class Lights
    {
        private static int[] _allLayers;

        public static Light2D Point(Transform parent, Vector2 pos, Color color, float radius, float intensity)
        {
            var go = new GameObject("Light");
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var l = go.AddComponent<Light2D>();
            l.lightType = Light2D.LightType.Point;
            l.color = color;
            l.intensity = intensity;
            l.pointLightOuterRadius = radius;
            l.pointLightInnerRadius = radius * 0.12f;
            l.falloffIntensity = 0.65f;
            l.shadowsEnabled = false;
            ApplyAllLayers(l);
            return l;
        }

        public static void ApplyAllLayers(Light2D l)
        {
            if (_allLayers == null)
            {
                var layers = SortingLayer.layers;
                _allLayers = new int[layers.Length];
                for (int i = 0; i < layers.Length; i++) _allLayers[i] = layers[i].id;
            }
            l.targetSortingLayers = _allLayers;
        }
    }

    /// <summary>
    /// Builds a map from <see cref="MapData"/>: themed ground tiles (random turns hide repeats), walls
    /// with a drop shadow so they read as raised blocks, water that blocks walking but not bullets,
    /// props by role (trees fade when you walk under them, crates break, barrels explode), lamps, decor.
    /// Falls back to the code-drawn look when the theme art is missing.
    /// </summary>
    public static class MapBuilder
    {
        /// <summary>Ground cells drawn outside the map on every side (see MatchController camera bounds).</summary>
        public const int OuterBorder = 12;

        private static readonly Quaternion[] Turns =
        {
            Quaternion.identity, Quaternion.Euler(0f, 0f, 90f), Quaternion.Euler(0f, 0f, 180f), Quaternion.Euler(0f, 0f, 270f)
        };

        public static MapInstance Build(GameContext ctx, MapData data, bool night, Transform parent)
        {
            var theme = ctx.Catalog != null ? ctx.Catalog.Theme(data.Theme) : null;
            var look = ThemeLook.For(data.Theme);
            bool themed = theme != null && theme.Tiles != null && theme.Tiles.Length >= 4;

            var art = ctx.Art;
            var inst = new MapInstance { Data = data };
            var root = new GameObject("Map_" + data.Name);
            root.transform.SetParent(parent, false);
            inst.Root = root;
            root.AddComponent<Grid>();

            var ground = MakeTilemap(root.transform, "Ground", SortOrder.Ground, art.Lit);
            var wallShadow = MakeTilemap(root.transform, "WallShadow", SortOrder.WallShadow, art.Solid);
            wallShadow.transform.localPosition = new Vector3(0.22f, -0.28f, 0f);
            // raised walls: the collider tilemap draws the darker side face; a lighter copy lifted north is the top face
            var walls = MakeTilemap(root.transform, "Walls", SortOrder.Walls - 1, art.Lit);
            walls.gameObject.layer = Layers.Walls;
            Tilemap wallTops = null;
            Tilemap water = null;

            Tile[] groundTiles, pathTiles, floorTiles;
            Tile wall, wallTop = null, waterTile = null;
            if (themed)
            {
                groundTiles = new[] { MakeTile(theme.Tiles[look.Ground], look.GroundTint, Tile.ColliderType.None) };
                pathTiles = new[] { MakeTile(theme.Tiles[look.Path], look.PathTint, Tile.ColliderType.None) };
                var floorSheet = look.FloorTheme >= 0 && ctx.Catalog.Theme(look.FloorTheme) != null ? ctx.Catalog.Theme(look.FloorTheme) : theme;
                var wallSheet = look.WallTheme >= 0 && ctx.Catalog.Theme(look.WallTheme) != null ? ctx.Catalog.Theme(look.WallTheme) : theme;
                floorTiles = new[] { MakeTile(floorSheet.Tiles[look.Floor], look.FloorTint, Tile.ColliderType.None) };
                wall = MakeTile(wallSheet.Tiles[look.Wall], look.WallSide, Tile.ColliderType.Grid);
                var topSheet = look.WallTopTheme >= 0 && ctx.Catalog.Theme(look.WallTopTheme) != null ? ctx.Catalog.Theme(look.WallTopTheme) : wallSheet;
                wallTop = MakeTile(topSheet.Tiles[look.WallTopTile >= 0 ? look.WallTopTile : look.Wall], look.WallTop, Tile.ColliderType.None);
                wallTops = MakeTilemap(root.transform, "WallTops", SortOrder.Walls, art.Lit);
                wallTops.transform.localPosition = new Vector3(0f, 0.32f, 0f);
                if (look.Water >= 0) waterTile = MakeTile(theme.Tiles[look.Water], Color.white, Tile.ColliderType.Grid);
            }
            else
            {
                groundTiles = new Tile[art.SandTiles.Length];
                for (int i = 0; i < groundTiles.Length; i++) groundTiles[i] = MakeTile(art.SandTiles[i], Color.white, Tile.ColliderType.None);
                pathTiles = new[] { MakeTile(art.DirtTile, Color.white, Tile.ColliderType.None) };
                floorTiles = new[] { MakeTile(art.IndoorTile, Color.white, Tile.ColliderType.None) };
                wall = MakeTile(art.WallTile, Color.white, Tile.ColliderType.Grid);
            }
            if (waterTile == null) waterTile = MakeTile(art.Square, new Color(0.2f, 0.45f, 0.75f), Tile.ColliderType.Grid);
            var shadow = MakeTile(art.Square, new Color(0f, 0f, 0f, 0.36f), Tile.ColliderType.None);

            for (int y = 0; y < data.Height; y++)
                for (int x = 0; x < data.Width; x++)
                {
                    char c = data.At(x, y);
                    var p = new Vector3Int(x, y, 0);
                    int h = Hash(x, y);
                    if (c == '#')
                    {
                        walls.SetTile(p, wall);
                        wallShadow.SetTile(p, shadow);
                        if (wallTops != null) wallTops.SetTile(p, wallTop);
                        continue;
                    }
                    if (MapData.IsWater(c))
                    {
                        if (water == null) water = MakeWater(root.transform, art);
                        water.SetTile(p, waterTile);
                        ground.SetTile(p, floorTiles[0]);
                        continue;
                    }
                    Tile floor;
                    bool turn;
                    char surf = Surface(data, x, y);
                    if (surf == ',') { floor = pathTiles[h % pathTiles.Length]; turn = look.TurnPath; }
                    else if (surf == '_') { floor = floorTiles[h % floorTiles.Length]; turn = look.TurnFloor; }
                    else { floor = groundTiles[h % groundTiles.Length]; turn = look.TurnGround; }
                    ground.SetTile(p, floor);
                    // themed tiles are made seamless when sliced, so they are never turned (turning would break the matching edges)
                    if (!themed && turn) ground.SetTransformMatrix(p, Matrix4x4.Rotate(Turns[(h >> 3) & 3]));
                }

            // Slightly darker ground past the outer walls, so the camera can keep you near the middle of the
            // screen at the map edges (away from the thumb controls) without showing empty black space.
            var outside = themed
                ? MakeTile(theme.Tiles[look.Ground], look.GroundTint * new Color(0.68f, 0.68f, 0.72f, 1f), Tile.ColliderType.None)
                : MakeTile(art.SandTiles[0], new Color(0.68f, 0.68f, 0.72f), Tile.ColliderType.None);
            for (int y = -OuterBorder; y < data.Height + OuterBorder; y++)
                for (int x = -OuterBorder; x < data.Width + OuterBorder; x++)
                    if (x < 0 || y < 0 || x >= data.Width || y >= data.Height) ground.SetTile(new Vector3Int(x, y, 0), outside);

            var wallBody = walls.gameObject.AddComponent<Rigidbody2D>();
            wallBody.bodyType = RigidbodyType2D.Static;
            var tileCol = walls.gameObject.AddComponent<TilemapCollider2D>();
            var composite = walls.gameObject.AddComponent<CompositeCollider2D>();
            composite.geometryType = CompositeCollider2D.GeometryType.Polygons;
            tileCol.compositeOperation = Collider2D.CompositeOperation.Merge;
            if (water != null) FinishWater(water);
            inst.Water = water;

            var props = new GameObject("Props").transform;
            props.SetParent(root.transform, false);
            var rng = new System.Random(data.Name.GetHashCode());
            var shared = ctx.Catalog != null ? ctx.Catalog.Shared : null;
            for (int y = 0; y < data.Height; y++)
                for (int x = 0; x < data.Width; x++)
                {
                    char c = data.At(x, y);
                    Vector2 pos = MapData.CellCenter(x, y);
                    var cell = new Vector2Int(x, y);
                    switch (c)
                    {
                        case 'c':
                            if (themed) ThemedCrate(ctx, props, pos, cell, Pick(theme.Props, look.Crates, shared, ArtIds.SharedCrate, rng), rng);
                            else Crate(ctx, props, pos, cell, art.Crate, (float)(rng.NextDouble() * 8.0 - 4.0));
                            break;
                        case 's':
                        {
                            bool horizontal = data.At(x - 1, y) == 's' || data.At(x + 1, y) == 's';
                            Sprite s = null;
                            if (themed)
                                s = horizontal ? Pick(theme.Props, look.Covers, shared, ArtIds.SharedSandbags, rng) : ArtCatalog.At(shared, ArtIds.SharedSandbags);
                            Cover(art, props, pos, horizontal, s);
                            break;
                        }
                        case 'b': Barrel(ctx, props, pos, cell, ArtCatalog.At(shared, ArtIds.SharedBarrelRed)); break;
                        case 'T':
                            if (themed && look.Trees.Length > 0) Tree(art, props, pos, Pick(theme.Props, look.Trees, null, 0, rng), (float)rng.NextDouble(), inst);
                            else Palm(art, props, pos, (float)rng.NextDouble(), inst);
                            break;
                        case 'o':
                            if (themed) Obstacle(art, props, pos, Pick(theme.Props, look.Rocks, shared, ArtIds.SharedBarrelGrey, rng));
                            else Crate(ctx, props, pos, cell, art.Crate, 0f);
                            break;
                        case 'x':
                            if (themed) DecorProp(art, props, pos, Pick(theme.Props, look.Decor, shared, ArtIds.SharedCone, rng), rng);
                            break;
                        case 'L':
                            Lamp(art, props, pos, night, inst, themed ? ArtCatalog.At(theme.Props, look.Lamp) : null);
                            break;
                    }
                }

            if (themed) ThemedDecor(art, root.transform, data, theme, look, shared, rng);
            else Decor(art, root.transform, data, rng);
            ApplyLighting(ctx, night, data.Theme);
            return inst;
        }

        public static void ApplyLighting(GameContext ctx, bool night, int theme = 1)
        {
            var look = ThemeLook.For(theme);
            if (ctx.GlobalLight != null)
            {
                ctx.GlobalLight.color = night ? new Color(0.45f, 0.52f, 0.78f) : look.Sun;
                ctx.GlobalLight.intensity = night ? 0.36f : look.SunIntensity;
            }
            if (ctx.Camera != null)
                ctx.Camera.backgroundColor = night ? new Color(0.01f, 0.01f, 0.02f) : look.Background;
        }

        private static int Hash(int x, int y) => ((x * 73856093) ^ (y * 19349663)) & 0x7fffffff;

        /// <summary>
        /// What the floor under a cell is. Cells holding a prop, pickup or spawn take the most common floor
        /// around them, so a crate inside a building stands on the building floor, not on outside ground.
        /// </summary>
        private static char Surface(MapData data, int x, int y)
        {
            char c = data.At(x, y);
            if (c == '.' || c == ',' || c == '_') return c;
            int floor = 0, path = 0, ground = 0;
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0) continue;
                    char n = data.At(x + dx, y + dy);
                    if (n == '_') floor++;
                    else if (n == ',') path++;
                    else if (n == '.') ground++;
                }
            if (floor >= path && floor >= ground && floor > 0) return '_';
            if (path >= ground && path > 0) return ',';
            return '.';
        }

        /// <summary>A random sprite from the theme's role list, else the shared-props fallback.</summary>
        private static Sprite Pick(Sprite[] props, int[] role, Sprite[] shared, int sharedFallback, System.Random rng)
        {
            for (int tries = 0; tries < 4 && role.Length > 0; tries++)
            {
                var s = ArtCatalog.At(props, role[rng.Next(role.Length)]);
                if (s != null) return s;
            }
            return ArtCatalog.At(shared, sharedFallback);
        }

        // ------------------------------------------------------------------ tilemaps

        private static Tilemap MakeTilemap(Transform parent, string name, int order, Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var tm = go.AddComponent<Tilemap>();
            var tr = go.AddComponent<TilemapRenderer>();
            tr.sortingOrder = order;
            tr.sharedMaterial = mat;
            return tm;
        }

        private static Tilemap MakeWater(Transform parent, ArtLibrary art)
        {
            var tm = MakeTilemap(parent, "Water", SortOrder.Ground + 1, art.Lit);
            tm.gameObject.layer = Layers.Water;
            return tm;
        }

        private static void FinishWater(Tilemap water)
        {
            var body = water.gameObject.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Static;
            var col = water.gameObject.AddComponent<TilemapCollider2D>();
            var comp = water.gameObject.AddComponent<CompositeCollider2D>();
            comp.geometryType = CompositeCollider2D.GeometryType.Polygons;
            col.compositeOperation = Collider2D.CompositeOperation.Merge;
        }

        private static Tile MakeTile(Sprite s, Color c, Tile.ColliderType collider)
        {
            var t = ScriptableObject.CreateInstance<Tile>();
            t.sprite = s;
            t.color = c;
            t.colliderType = collider;
            t.flags = TileFlags.None;
            return t;
        }

        // ------------------------------------------------------------------ sprites

        private static SpriteRenderer Sprite(Transform parent, string n, Sprite s, Material m, Vector2 pos, Vector2 scale, float rot, Color c, int order)
        {
            var go = new GameObject(n);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(0f, 0f, rot);
            go.transform.localScale = new Vector3(scale.x, scale.y, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = s;
            sr.sharedMaterial = m;
            sr.color = c;
            sr.sortingOrder = order;
            return sr;
        }

        private static SpriteRenderer DropShadow(ArtLibrary art, SpriteRenderer of, Vector2 offset, float alpha)
        {
            var t = of.transform;
            var sr = Sprite(t, "Shadow", of.sprite, art.Solid, Vector2.zero, Vector2.one, 0f, new Color(0f, 0f, 0f, alpha), SortOrder.Shadows);
            sr.transform.localPosition = Vector3.zero;
            sr.transform.position = (Vector2)t.position + offset;
            return sr;
        }

        /// <summary>Soft oval contact shadow under a painted prop (painted props are drawn at an angle).</summary>
        private static SpriteRenderer FootShadow(ArtLibrary art, Transform parent, Vector2 basePos, float width, float alpha)
        {
            return Sprite(parent, "FootShadow", art.Glow, art.Solid, basePos + new Vector2(0.08f, -0.06f), new Vector2(width * 1.25f, width * 0.55f), 0f,
                new Color(0f, 0f, 0f, alpha), SortOrder.Shadows);
        }

        /// <summary>Uniform scale that makes the sprite <paramref name="size"/> units on its larger side.</summary>
        private static float Fit(Sprite s, float size)
        {
            var b = s.bounds.size;
            return size / Mathf.Max(0.01f, Mathf.Max(b.x, b.y));
        }

        private static void ThemedCrate(GameContext ctx, Transform parent, Vector2 pos, Vector2Int cell, Sprite s, System.Random rng)
        {
            if (s == null) { Crate(ctx, parent, pos, cell, ctx.Art.Crate, 0f); return; }
            Crate(ctx, parent, pos, cell, s, 0f, Fit(s, 1.02f));
        }

        private static void Crate(GameContext ctx, Transform parent, Vector2 pos, Vector2Int cell, Sprite s, float rot, float scale = 1f)
        {
            var art = ctx.Art;
            var sr = Sprite(parent, "Crate", s, art.Lit, pos, new Vector2(scale, scale), rot, Color.white, SortOrder.Props);
            sr.gameObject.layer = Layers.Props;
            var box = sr.gameObject.AddComponent<BoxCollider2D>();
            box.size = new Vector2(0.94f, 0.94f) / scale;
            var sh = FootShadow(art, parent, pos + new Vector2(0f, -0.25f), 0.95f, 0.32f);
            var d = sr.gameObject.AddComponent<Destructible>();
            d.Init(ctx, cell, sr, sh, box, Destructible.Kind.Breakable);
        }

        private static void Cover(ArtLibrary art, Transform parent, Vector2 pos, bool horizontal, Sprite s)
        {
            if (s == null)
            {
                var sb = Sprite(parent, "Sandbags", art.Sandbag, art.Lit, pos, new Vector2(1.04f, 1.2f), horizontal ? 0f : 90f, Color.white, SortOrder.Props);
                sb.gameObject.layer = Layers.Props;
                var b0 = sb.gameObject.AddComponent<BoxCollider2D>();
                b0.size = new Vector2(1f, 0.5f);
                DropShadow(art, sb, new Vector2(0.1f, -0.13f), 0.3f);
                return;
            }
            var b = s.bounds.size;
            float scale = 1.08f / Mathf.Max(0.01f, b.x);
            var sr = Sprite(parent, "Cover", s, art.Lit, pos, new Vector2(scale, scale), horizontal ? 0f : 90f, Color.white, SortOrder.Props);
            sr.gameObject.layer = Layers.Props;
            var box = sr.gameObject.AddComponent<BoxCollider2D>();
            box.size = new Vector2(1f, 0.55f) / scale;
            FootShadow(art, parent, pos + (horizontal ? new Vector2(0f, -0.2f) : Vector2.zero), 1f, 0.26f);
        }

        private static void Barrel(GameContext ctx, Transform parent, Vector2 pos, Vector2Int cell, Sprite painted)
        {
            var art = ctx.Art;
            float scale = painted != null ? Fit(painted, 0.95f) : 0.8f;
            var sr = Sprite(parent, "ExplosiveBarrel", painted != null ? painted : art.Barrel, art.Lit, pos + new Vector2(0f, painted != null ? 0.08f : 0f),
                new Vector2(scale, scale), 0f, Color.white, SortOrder.Props);
            sr.gameObject.layer = Layers.Props;
            var col = sr.gameObject.AddComponent<CircleCollider2D>();
            col.radius = 0.45f / scale;
            col.offset = new Vector2(0f, painted != null ? -0.08f / scale : 0f);
            var sh = painted != null ? FootShadow(art, parent, pos + new Vector2(0f, -0.3f), 0.8f, 0.35f) : DropShadow(art, sr, new Vector2(0.1f, -0.14f), 0.35f);
            var d = sr.gameObject.AddComponent<Destructible>();
            d.Init(ctx, cell, sr, sh, col, Destructible.Kind.Explosive);
        }

        /// <summary>Painted tree: solid trunk, crown drawn above fighters and faded when the local player is under it.</summary>
        private static void Tree(ArtLibrary art, Transform parent, Vector2 pos, Sprite s, float r01, MapInstance inst)
        {
            if (s == null) { Palm(art, parent, pos, r01, inst); return; }
            var trunk = new GameObject("TreeTrunk");
            trunk.transform.SetParent(parent, false);
            trunk.transform.position = pos;
            trunk.layer = Layers.Props;
            var col = trunk.AddComponent<CircleCollider2D>();
            col.radius = 0.36f;
            float size = 1.9f + r01 * 0.5f;
            float scale = Fit(s, size);
            float lift = s.bounds.size.y * scale * 0.5f - 0.35f;
            FootShadow(art, parent, pos + new Vector2(0.25f, -0.2f), size * 0.8f, 0.3f);
            var crown = Sprite(parent, "Tree", s, art.Lit, pos + new Vector2(0f, lift), new Vector2(scale, scale), 0f, Color.white, SortOrder.Canopy);
            inst.Canopies.Add(crown.transform);
            inst.CanopyRenderers.Add(crown);
        }

        private static void Obstacle(ArtLibrary art, Transform parent, Vector2 pos, Sprite s)
        {
            if (s == null) return;
            var b = s.bounds.size;
            float native = Mathf.Max(b.x, b.y);
            float size = Mathf.Clamp(native * 1.1f, 1.05f, 2.1f);
            float scale = size / native;
            float lift = Mathf.Max(0f, b.y * scale * 0.5f - 0.5f);
            var sr = Sprite(parent, "Obstacle", s, art.Lit, pos + new Vector2(0f, lift), new Vector2(scale, scale), 0f, Color.white, SortOrder.Props);
            var go = new GameObject("ObstacleCollider");
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.layer = Layers.Props;
            var box = go.AddComponent<BoxCollider2D>();
            box.size = new Vector2(0.96f, 0.96f);
            FootShadow(art, parent, pos + new Vector2(0f, -0.25f), Mathf.Min(1.6f, b.x * scale), 0.3f);
            sr.gameObject.name = "Obstacle_" + s.name;
        }

        private static void DecorProp(ArtLibrary art, Transform parent, Vector2 pos, Sprite s, System.Random rng)
        {
            if (s == null) return;
            float scale = Fit(s, 0.55f + (float)rng.NextDouble() * 0.25f);
            Sprite(parent, "Decor", s, art.Lit, pos + new Vector2((float)rng.NextDouble() * 0.3f - 0.15f, 0.05f), new Vector2(scale, scale), 0f, Color.white, SortOrder.Props - 2);
        }

        private static void Palm(ArtLibrary art, Transform parent, Vector2 pos, float r01, MapInstance inst)
        {
            var trunk = Sprite(parent, "PalmTrunk", art.Circle, art.Lit, pos, new Vector2(0.34f, 0.34f), 0f, new Color(0.45f, 0.32f, 0.2f), SortOrder.Props);
            trunk.gameObject.layer = Layers.Props;
            var col = trunk.gameObject.AddComponent<CircleCollider2D>();
            col.radius = 0.6f;
            float scale = 0.95f + r01 * 0.25f;
            Sprite(parent, "PalmShadow", art.Palm, art.Solid, pos + new Vector2(0.55f, -0.65f), new Vector2(scale, scale), r01 * 360f, new Color(0f, 0f, 0f, 0.22f), SortOrder.Shadows);
            var canopy = Sprite(parent, "PalmCanopy", art.Palm, art.Lit, pos, new Vector2(scale, scale), r01 * 360f, new Color(1f, 1f, 1f, 0.94f), SortOrder.Canopy);
            inst.Canopies.Add(canopy.transform);
            inst.CanopyRenderers.Add(canopy);
        }

        private static void Lamp(ArtLibrary art, Transform parent, Vector2 pos, bool night, MapInstance inst, Sprite painted)
        {
            if (painted != null)
            {
                float scale = Fit(painted, 1.5f);
                Sprite(parent, "Lamp", painted, art.Lit, pos + new Vector2(0f, 0.5f), new Vector2(scale, scale), 0f, Color.white, SortOrder.Props + 1);
            }
            else Sprite(parent, "Lamp", art.Lamp, art.Lit, pos, new Vector2(0.9f, 0.9f), 0f, Color.white, SortOrder.Props + 1);
            if (!night) return;
            Sprite(parent, "LampGlow", art.Glow, art.Additive, pos, new Vector2(1.2f, 1.2f), 0f, new Color(1f, 0.85f, 0.5f, 0.5f), SortOrder.Fx);
            inst.Lamps.Add(Lights.Point(parent, pos, new Color(1f, 0.84f, 0.6f), 6f, 1.25f));
        }

        private static void Decor(ArtLibrary art, Transform parent, MapData data, System.Random rng)
        {
            var decor = new GameObject("Decor").transform;
            decor.SetParent(parent, false);
            for (int i = 0; i < 160; i++)
            {
                int x = rng.Next(1, data.Width - 1);
                int y = rng.Next(1, data.Height - 1);
                char c = data.At(x, y);
                if (c != '.' && c != ',') continue;
                Vector2 pos = new Vector2(x + (float)rng.NextDouble(), y + (float)rng.NextDouble());
                float s = 0.1f + (float)rng.NextDouble() * 0.22f;
                float shade = 0.55f + (float)rng.NextDouble() * 0.25f;
                Sprite(decor, "Pebble", art.Splats[rng.Next(art.Splats.Length)], art.Lit, pos, new Vector2(s, s), (float)rng.NextDouble() * 360f,
                    new Color(shade, shade * 0.88f, shade * 0.7f, 0.55f), SortOrder.GroundDetail);
            }
        }

        /// <summary>Pebbles in the ground color plus a few painted decor props on open ground.</summary>
        private static void ThemedDecor(ArtLibrary art, Transform parent, MapData data, MapTheme theme, ThemeLook look, Sprite[] shared, System.Random rng)
        {
            var decor = new GameObject("Decor").transform;
            decor.SetParent(parent, false);
            var dust = look.DustColor;
            for (int i = 0; i < 120; i++)
            {
                int x = rng.Next(1, data.Width - 1);
                int y = rng.Next(1, data.Height - 1);
                if (data.At(x, y) != '.') continue;
                Vector2 pos = new Vector2(x + (float)rng.NextDouble(), y + (float)rng.NextDouble());
                float s = 0.08f + (float)rng.NextDouble() * 0.16f;
                float shade = 0.6f + (float)rng.NextDouble() * 0.3f;
                Sprite(decor, "Pebble", art.Splats[rng.Next(art.Splats.Length)], art.Lit, pos, new Vector2(s, s), (float)rng.NextDouble() * 360f,
                    new Color(dust.r * shade, dust.g * shade, dust.b * shade, 0.45f), SortOrder.GroundDetail);
            }
            if (look.Decor.Length == 0) return;
            int placed = 0;
            for (int i = 0; i < 300 && placed < 14; i++)
            {
                int x = rng.Next(2, data.Width - 2);
                int y = rng.Next(2, data.Height - 2);
                if (!OpenAround(data, x, y)) continue;
                DecorProp(art, decor, MapData.CellCenter(x, y), Pick(theme.Props, look.Decor, shared, ArtIds.SharedCone, rng), rng);
                placed++;
            }
        }

        private static bool OpenAround(MapData data, int x, int y)
        {
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                    if (data.At(x + dx, y + dy) != '.') return false;
            return true;
        }
    }
}
