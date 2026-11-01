using UnityEngine;

namespace NeonBrawl
{
    /// <summary>
    /// Placeholder art generated in code at startup (~1 MB of textures total). Each sprite here is a
    /// slot that the real art (sliced from the AI images) replaces in milestone M2.
    /// </summary>
    public sealed class ArtLibrary
    {
        public Sprite Circle, Ring, ThinRing, Glow, Smoke, Square, Star, Slash, Vignette, Spark;
        public Sprite RoundRect, Pill;
        public Sprite[] Splats;
        public Sprite[] SandTiles;
        public Sprite DirtTile, IndoorTile, WallTile, Crate, Barrel, Sandbag, Palm, Lamp, Cross;

        public Material Lit, Additive, Solid, VertexColor, Unlit;

        private System.Random _rng;

        public static ArtLibrary Create()
        {
            var a = new ArtLibrary { _rng = new System.Random(1337) };
            a.Build();
            return a;
        }

        private void Build()
        {
            Circle = MakeCircle(128, 0f);
            Ring = MakeCircle(128, 12f);
            ThinRing = MakeCircle(128, 5f);
            Glow = MakeGlow(128, 2f, 0f);
            Smoke = MakeGlow(128, 1.4f, 0.35f);
            Square = Sprite.Create(SolidTex(8, 8, Color.white), new Rect(0, 0, 8, 8), new Vector2(0.5f, 0.5f), 8f, 0, SpriteMeshType.FullRect);
            Star = MakeStar(128);
            Slash = MakeSlash(128);
            Vignette = MakeVignette(128);
            Spark = MakeSpark(64);
            RoundRect = MakeRoundRect(64, 64, 20, 100f, new Vector4(24, 24, 24, 24));
            Pill = MakeRoundRect(64, 32, 15, 100f, new Vector4(16, 15, 16, 15));
            Splats = new Sprite[4];
            for (int i = 0; i < Splats.Length; i++) Splats[i] = MakeSplat(64, i);
            SandTiles = new Sprite[3];
            for (int i = 0; i < SandTiles.Length; i++) SandTiles[i] = MakeSand(i);
            DirtTile = MakeDirt();
            IndoorTile = MakeIndoor();
            WallTile = MakeWall();
            Crate = MakeCrate();
            Barrel = MakeBarrel();
            Sandbag = MakeSandbag();
            Palm = MakePalm();
            Lamp = MakeLamp();
            Cross = MakeCross(64);

            Additive = new Material(Resources.Load<Shader>("Shaders/NB_SpriteAdditive")) { name = "NB_Additive" };
            Solid = new Material(Resources.Load<Shader>("Shaders/NB_SpriteSolid")) { name = "NB_Solid" };
            VertexColor = new Material(Resources.Load<Shader>("Shaders/NB_VertexColor")) { name = "NB_VertexColor" };
            Unlit = new Material(Resources.Load<Shader>("Shaders/NB_SpriteUnlit")) { name = "NB_Unlit" };

            // The pipeline's default 2D sprite material (Sprite-Lit-Default) so sprites react to 2D lights.
            var probe = new GameObject("MaterialProbe").AddComponent<SpriteRenderer>();
            Lit = probe.sharedMaterial;
            Object.Destroy(probe.gameObject);
        }

        // ---------------------------------------------------------------- helpers

        private static Texture2D NewTex(int w, int h)
        {
            return new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = "NB_Generated"
            };
        }

        private static Texture2D SolidTex(int w, int h, Color c)
        {
            var t = NewTex(w, h);
            var px = new Color32[w * h];
            Color32 cc = c;
            for (int i = 0; i < px.Length; i++) px[i] = cc;
            t.SetPixels32(px);
            t.Apply(false, true);
            return t;
        }

        private static Sprite Finish(Texture2D t, Color32[] px, float ppu, Vector2 pivot, Vector4 border = default)
        {
            t.SetPixels32(px);
            t.Apply(false, true);
            return Sprite.Create(t, new Rect(0, 0, t.width, t.height), pivot, ppu, 0, SpriteMeshType.FullRect, border);
        }

        private static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);

        private static Color32 C(float r, float g, float b, float a = 1f) => new Color(r / 255f, g / 255f, b / 255f, a);

        private float Rand() => (float)_rng.NextDouble();

        private static Color32 Shade(Color32 c, float f)
        {
            return new Color32((byte)Mathf.Clamp(c.r * f, 0, 255), (byte)Mathf.Clamp(c.g * f, 0, 255), (byte)Mathf.Clamp(c.b * f, 0, 255), c.a);
        }

        // ---------------------------------------------------------------- shapes

        private static Sprite MakeCircle(int size, float thickness)
        {
            var t = NewTex(size, size);
            var px = new Color32[size * size];
            float c = size * 0.5f, r = c - 1f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - c, dy = y + 0.5f - c;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Clamp01(r - d + 0.5f);
                    if (thickness > 0f) a *= Clamp01(d - (r - thickness) + 0.5f);
                    px[y * size + x] = new Color(1f, 1f, 1f, a);
                }
            return Finish(t, px, size, new Vector2(0.5f, 0.5f));
        }

        private Sprite MakeGlow(int size, float power, float noise)
        {
            var t = NewTex(size, size);
            var px = new Color32[size * size];
            float c = size * 0.5f;
            float seed = Rand() * 100f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - c, dy = y + 0.5f - c;
                    float d = Mathf.Sqrt(dx * dx + dy * dy) / c;
                    float a = Mathf.Pow(Clamp01(1f - d), power);
                    if (noise > 0f) a *= 1f - noise + noise * Mathf.PerlinNoise(seed + x * 0.06f, seed + y * 0.06f) * 1.4f;
                    px[y * size + x] = new Color(1f, 1f, 1f, Clamp01(a));
                }
            return Finish(t, px, size, new Vector2(0.5f, 0.5f));
        }

        /// <summary>Muzzle flash: a hot core with a long forward spike (+x).</summary>
        private static Sprite MakeStar(int size)
        {
            var t = NewTex(size, size);
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size * 2f - 1f;
                    float v = (y + 0.5f) / size * 2f - 1f;
                    float uc = u + 0.5f; // core sits left of center, spike points right
                    float len = Mathf.Sqrt(uc * uc + v * v);
                    float core = Mathf.Pow(Clamp01(1f - len * 2.4f), 1.2f);
                    float forward = uc > 0f ? Clamp01(1f - Mathf.Abs(v) * (5f + uc * 6f) - uc * 0.62f) : 0f;
                    float side = Clamp01(1f - Mathf.Abs(uc) * 7f - Mathf.Abs(v) * 1.9f) * 0.75f;
                    float a = Clamp01(core + forward + side);
                    px[y * size + x] = new Color(1f, 1f, 1f, a);
                }
            return Finish(t, px, size * 0.5f, new Vector2(0.25f, 0.5f));
        }

        /// <summary>Melee swing crescent opening toward +x.</summary>
        private static Sprite MakeSlash(int size)
        {
            var t = NewTex(size, size);
            var px = new Color32[size * size];
            float c = size * 0.5f, r = c - 2f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - c, dy = y + 0.5f - c;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float ang = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
                    float arc = Clamp01(1f - Mathf.Abs(ang) / 70f);
                    float thick = 4f + 14f * arc;
                    float a = Clamp01(r - d + 0.5f) * Clamp01(d - (r - thick) + 0.5f) * arc;
                    px[y * size + x] = new Color(1f, 1f, 1f, a);
                }
            return Finish(t, px, size, new Vector2(0.5f, 0.5f));
        }

        private static Sprite MakeVignette(int size)
        {
            var t = NewTex(size, size);
            var px = new Color32[size * size];
            float c = size * 0.5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f - c) / c, dy = (y + 0.5f - c) / c;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Clamp01((d - 0.55f) / 0.6f);
                    px[y * size + x] = new Color(1f, 1f, 1f, a * a);
                }
            return Finish(t, px, size, new Vector2(0.5f, 0.5f));
        }

        /// <summary>Thin horizontal streak for tracers and sparks (soft ends).</summary>
        private static Sprite MakeSpark(int size)
        {
            int h = 8;
            var t = NewTex(size, h);
            var px = new Color32[size * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size;
                    float v = Mathf.Abs((y + 0.5f) / h * 2f - 1f);
                    float a = Clamp01(1f - v * v) * Mathf.Sin(u * Mathf.PI);
                    px[y * size + x] = new Color(1f, 1f, 1f, Clamp01(a * 1.4f));
                }
            return Finish(t, px, size, new Vector2(0.5f, 0.5f));
        }

        private static Sprite MakeRoundRect(int w, int h, int radius, float ppu, Vector4 border)
        {
            var t = NewTex(w, h);
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float qx = Mathf.Max(Mathf.Abs(x + 0.5f - w * 0.5f) - (w * 0.5f - radius), 0f);
                    float qy = Mathf.Max(Mathf.Abs(y + 0.5f - h * 0.5f) - (h * 0.5f - radius), 0f);
                    float d = Mathf.Sqrt(qx * qx + qy * qy) - radius;
                    float a = Clamp01(0.5f - d);
                    // subtle top highlight so buttons read as raised
                    float shade = 1f - 0.12f * (1f - (float)y / h);
                    px[y * w + x] = new Color(shade, shade, shade, a);
                }
            return Finish(t, px, ppu, new Vector2(0.5f, 0.5f), border);
        }

        private Sprite MakeSplat(int size, int variant)
        {
            var t = NewTex(size, size);
            var px = new Color32[size * size];
            float c = size * 0.5f;
            int blobs = 7 + variant * 2;
            var bx = new float[blobs]; var by = new float[blobs]; var br = new float[blobs];
            bx[0] = c; by[0] = c; br[0] = size * (0.16f + 0.03f * variant);
            for (int i = 1; i < blobs; i++)
            {
                float ang = Rand() * Mathf.PI * 2f;
                float dist = size * (0.08f + Rand() * (i > blobs - 4 ? 0.38f : 0.2f));
                bx[i] = c + Mathf.Cos(ang) * dist;
                by[i] = c + Mathf.Sin(ang) * dist;
                br[i] = size * (i > blobs - 4 ? 0.025f + Rand() * 0.025f : 0.06f + Rand() * 0.08f);
            }
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float a = 0f;
                    for (int i = 0; i < blobs; i++)
                    {
                        float dx = x + 0.5f - bx[i], dy = y + 0.5f - by[i];
                        a = Mathf.Max(a, Clamp01(br[i] - Mathf.Sqrt(dx * dx + dy * dy) + 0.5f));
                    }
                    px[y * size + x] = new Color(1f, 1f, 1f, a);
                }
            return Finish(t, px, size, new Vector2(0.5f, 0.5f));
        }

        private static Sprite MakeCross(int size)
        {
            var t = NewTex(size, size);
            var px = new Color32[size * size];
            float c = size * 0.5f, arm = size * 0.14f, len = size * 0.42f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Abs(x + 0.5f - c), dy = Mathf.Abs(y + 0.5f - c);
                    bool on = (dx < arm && dy < len) || (dy < arm && dx < len);
                    px[y * size + x] = new Color(1f, 1f, 1f, on ? 1f : 0f);
                }
            return Finish(t, px, size, new Vector2(0.5f, 0.5f));
        }

        // ---------------------------------------------------------------- tiles & props (64 px = 1 unit)

        private Sprite MakeSand(int variant)
        {
            const int s = 64;
            var t = NewTex(s, s);
            var px = new Color32[s * s];
            Color32 baseC = C(220, 194, 148);
            for (int i = 0; i < px.Length; i++) px[i] = Shade(baseC, 0.96f + Rand() * 0.07f);
            int pebbles = 3 + variant * 2;
            for (int p = 0; p < pebbles; p++)
                Disc(px, s, 6 + Rand() * 52f, 6 + Rand() * 52f, 1.2f + Rand() * 2f, Shade(baseC, 0.8f + Rand() * 0.08f));
            if (variant == 2)
                for (int p = 0; p < 4; p++) Disc(px, s, 8 + Rand() * 48f, 8 + Rand() * 48f, 3f + Rand() * 2f, Shade(baseC, 1.06f));
            return Finish(t, px, s, new Vector2(0.5f, 0.5f));
        }

        private Sprite MakeDirt()
        {
            const int s = 64;
            var t = NewTex(s, s);
            var px = new Color32[s * s];
            Color32 baseC = C(186, 150, 108);
            for (int i = 0; i < px.Length; i++) px[i] = Shade(baseC, 0.95f + Rand() * 0.08f);
            for (int p = 0; p < 6; p++)
            {
                float x = 8 + Rand() * 48f, y = 8 + Rand() * 48f, r = 4f + Rand() * 3f;
                Disc(px, s, x, y, r + 1f, Shade(baseC, 0.72f));
                Disc(px, s, x, y, r, Shade(baseC, 0.88f + Rand() * 0.1f));
            }
            return Finish(t, px, s, new Vector2(0.5f, 0.5f));
        }

        private Sprite MakeIndoor()
        {
            const int s = 64;
            var t = NewTex(s, s);
            var px = new Color32[s * s];
            Color32 baseC = C(196, 128, 90), grout = C(140, 88, 62);
            float[] cell = { 0.95f + Rand() * 0.1f, 0.95f + Rand() * 0.1f, 0.95f + Rand() * 0.1f, 0.95f + Rand() * 0.1f };
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    bool line = x % 32 < 2 || y % 32 < 2;
                    int ci = (x / 32) + (y / 32) * 2;
                    px[y * s + x] = line ? grout : Shade(baseC, cell[ci] + Rand() * 0.04f);
                }
            return Finish(t, px, s, new Vector2(0.5f, 0.5f));
        }

        private Sprite MakeWall()
        {
            const int s = 64;
            var t = NewTex(s, s);
            var px = new Color32[s * s];
            Color32 top = C(222, 192, 146), light = C(242, 220, 178), dark = C(168, 136, 96), seam = C(190, 158, 114);
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    Color32 c = Shade(top, 0.97f + Rand() * 0.05f);
                    if (y == 31 || y == 32 || (y > 32 ? x == 20 || x == 21 : x == 44 || x == 45)) c = seam;
                    if (y >= s - 5 || x < 4) c = light;
                    if (y < 6 || x >= s - 4) c = dark;
                    px[y * s + x] = c;
                }
            return Finish(t, px, s, new Vector2(0.5f, 0.5f));
        }

        private Sprite MakeCrate()
        {
            const int s = 64;
            var t = NewTex(s, s);
            var px = new Color32[s * s];
            Color32 wood = C(168, 114, 62), edge = C(104, 66, 36), plank = C(146, 96, 50), hi = C(196, 142, 86);
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    Color32 c = Shade(wood, 0.95f + Rand() * 0.08f);
                    bool border = x < 6 || y < 6 || x >= s - 6 || y >= s - 6;
                    bool diag = Mathf.Abs(x - y) < 5 || Mathf.Abs(x + y - s) < 5;
                    if (diag) c = plank;
                    if (border) c = (y >= s - 6 || x < 6) ? hi : edge;
                    if (x < 2 || y < 2 || x >= s - 2 || y >= s - 2) c = edge;
                    px[y * s + x] = c;
                }
            Disc(px, s, 9, 9, 1.5f, C(70, 60, 50)); Disc(px, s, 55, 9, 1.5f, C(70, 60, 50));
            Disc(px, s, 9, 55, 1.5f, C(70, 60, 50)); Disc(px, s, 55, 55, 1.5f, C(70, 60, 50));
            return Finish(t, px, s, new Vector2(0.5f, 0.5f));
        }

        private static Sprite MakeBarrel()
        {
            const int s = 64;
            var t = NewTex(s, s);
            var px = new Color32[s * s];
            Color32 red = C(196, 44, 34), dark = C(112, 22, 16), hi = C(236, 96, 76), yellow = C(244, 204, 48);
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float dx = x + 0.5f - 32f, dy = y + 0.5f - 32f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    Color32 c = red;
                    if (d > 26f) c = dark;
                    else if (d > 17f && d < 19.5f) c = dark;
                    else if (d < 8f) c = yellow;
                    else if (d < 10f) c = dark;
                    if (d < 24f && d > 20f && dx < -4f && dy > 4f) c = hi;
                    float a = Clamp01(29.5f - d + 0.5f);
                    c.a = (byte)(a * 255);
                    px[y * s + x] = c;
                }
            return Finish(t, px, s, new Vector2(0.5f, 0.5f));
        }

        private static Sprite MakeSandbag()
        {
            const int w = 64, h = 32;
            var t = NewTex(w, h);
            var px = new Color32[w * h];
            Color32 bag = C(204, 184, 132), line = C(146, 126, 86), hi = C(228, 212, 168);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float bx = (x % 21) + 0.5f - 10.5f;
                    float by = y + 0.5f - 16f;
                    float d = Mathf.Sqrt(bx * bx / 1.1f + by * by / 1.9f);
                    Color32 c = d < 9.5f ? (by > 4f && bx < 0f ? hi : bag) : (d < 11f ? line : new Color32(0, 0, 0, 0));
                    px[y * w + x] = c;
                }
            return Finish(t, px, 64, new Vector2(0.5f, 0.5f));
        }

        private Sprite MakePalm()
        {
            const int s = 160;
            var t = NewTex(s, s);
            var px = new Color32[s * s];
            const int blades = 9;
            var bladeAng = new float[blades];
            for (int i = 0; i < blades; i++) bladeAng[i] = i * Mathf.PI * 2f / blades + (Rand() - 0.5f) * 0.3f;
            Color32 dark = C(46, 108, 52), mid = C(72, 150, 66), tip = C(130, 190, 84), outline = C(28, 70, 34);
            float c = s * 0.5f, R = c - 3f;
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float dx = x + 0.5f - c, dy = y + 0.5f - c;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float ang = Mathf.Atan2(dy, dx);
                    float best = 9f;
                    for (int i = 0; i < blades; i++)
                    {
                        float da = Mathf.Abs(Mathf.DeltaAngle(ang * Mathf.Rad2Deg, bladeAng[i] * Mathf.Rad2Deg)) * Mathf.Deg2Rad;
                        if (da < best) best = da;
                    }
                    float f = r / R;
                    float width = 0.34f * Mathf.Sin(Mathf.Clamp01(f) * Mathf.PI) + 0.04f;
                    Color32 col = new Color32(0, 0, 0, 0);
                    if (f < 1f && best < width)
                    {
                        col = best > width - 0.06f ? outline : (f < 0.35f ? dark : (f < 0.75f ? mid : tip));
                        if (Mathf.Abs(best) < 0.02f) col = outline; // leaf spine
                    }
                    if (r < 9f) col = C(110, 78, 46);
                    px[y * s + x] = col;
                }
            return Finish(t, px, 64, new Vector2(0.5f, 0.5f));
        }

        private static Sprite MakeLamp()
        {
            const int s = 32;
            var t = NewTex(s, s);
            var px = new Color32[s * s];
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float dx = x + 0.5f - 16f, dy = y + 0.5f - 16f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    Color32 c = d < 6f ? C(255, 236, 170) : (d < 12f ? C(60, 62, 66) : new Color32(0, 0, 0, 0));
                    if (d >= 11f && d < 12.5f) c = C(30, 30, 34);
                    px[y * s + x] = c;
                }
            return Finish(t, px, 64, new Vector2(0.5f, 0.5f));
        }

        private static void Disc(Color32[] px, int size, float cx, float cy, float r, Color32 c)
        {
            int x0 = Mathf.Max(0, (int)(cx - r - 1)), x1 = Mathf.Min(size - 1, (int)(cx + r + 1));
            int y0 = Mathf.Max(0, (int)(cy - r - 1)), y1 = Mathf.Min(size - 1, (int)(cy + r + 1));
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    float dx = x + 0.5f - cx, dy = y + 0.5f - cy;
                    if (dx * dx + dy * dy <= r * r) px[y * size + x] = c;
                }
        }
    }
}
