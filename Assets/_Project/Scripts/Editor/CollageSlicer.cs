using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace NeonBrawl.EditorTools
{
    /// <summary>
    /// Cuts every sheet of the 28-in-one collage (ArtSource/Collage_preview.png) into sprites: hair and
    /// back items, both gun sheets, the three effect sheets, shared props, the five map themes (ground
    /// tiles + props) and the UI kit (panels, icons, HUD parts, rewards, lobby/loading art, app icon).
    /// Cells sit on magenta, dark navy or black, so each kind gets its own background removal.
    /// Writes one check sheet per cell to Builds/art_sheets/ (items in catalog order, 8 per row).
    /// </summary>
    public static class CollageSlicer
    {
        private const string Source = "Collage_preview.png";
        private const string OutDir = "Assets/_Project/Art";
        private const string SheetDir = "Builds/art_sheets";

        private enum Bg { Magenta, Dark, Glow }

        // Collage grid (top-left image coordinates), inset past the cell frames and the label bars.
        private static readonly int[] ColX0 = { 6, 264, 523, 776, 1033, 1291 };
        private static readonly int[] ColX1 = { 246, 504, 757, 1014, 1271, 1529 };
        private static readonly int[] RowY0 = { 7, 230, 448, 660 };
        private static readonly int[] RowY1 = { 192, 404, 616, 820 };

        /// <summary>One cut-out item: keyed pixels plus its size.</summary>
        private sealed class Piece
        {
            public Color32[] Px;
            public int W, H;
        }

        public static void SliceAll(ArtCatalog catalog, System.Text.StringBuilder log)
        {
            var src = ArtSlicer.Load(Source);
            if (src == null) { log.AppendLine(Source + " missing"); return; }
            int w = src.width, h = src.height;
            var raw = src.GetPixels32();
            Object.DestroyImmediate(src);
            Directory.CreateDirectory(SheetDir);

            // ---- characters: 8 hair styles + 4 back items
            var c3 = Cut(raw, w, h, 2, 0, Bg.Magenta, 250, 2, "C3", log);
            int hairN = Mathf.Min(8, c3.Count);
            float hairPpu = MedianHeight(c3, 0, hairN) / 0.52f;
            catalog.Hair = new Sprite[hairN];
            for (int i = 0; i < hairN; i++)
            {
                var p = c3[i];
                catalog.Hair[i] = ArtSlicer.Save(p.Px, p.W, p.H, $"{OutDir}/Characters/hair_{i:00}.png", hairPpu, ArtSlicer.FindHeadCenter(p.Px, p.W, p.H), 128);
            }
            int backN = Mathf.Max(0, c3.Count - hairN);
            float backPpu = backN > 0 ? MedianHeight(c3, hairN, c3.Count) / 0.62f : 100f;
            catalog.Back = new Sprite[backN];
            for (int i = 0; i < backN; i++)
            {
                var p = c3[hairN + i];
                catalog.Back[i] = ArtSlicer.Save(p.Px, p.W, p.H, $"{OutDir}/Characters/back_{i:00}.png", backPpu, new Vector2(0.5f, 0.5f), 128);
            }

            // ---- guns (both sheets are side views, muzzle to the right)
            catalog.GunsA = SaveAll(Cut(raw, w, h, 3, 0, Bg.Magenta, 250, 2, "W1", log), "Weapons/gun_a", 100f, 256);
            catalog.GunsB = SaveAll(Cut(raw, w, h, 4, 0, Bg.Magenta, 250, 2, "W2", log), "Weapons/gun_b", 100f, 256);

            // ---- effects: each sprite is 1 unit across, code scales it
            catalog.Fx1 = SaveAll(Cut(raw, w, h, 0, 1, Bg.Glow, 300, 5, "FX1", log), "Fx/fx1", 0f, 128);
            catalog.Fx2 = SaveAll(Cut(raw, w, h, 1, 1, Bg.Glow, 300, 5, "FX2", log), "Fx/fx2", 0f, 128);
            catalog.Fx3 = SaveAll(Cut(raw, w, h, 2, 1, Bg.Dark, 120, 3, "FX3", log), "Fx/fx3", 0f, 128);

            // ---- shared props (a crate is about 1 unit)
            var e0 = Cut(raw, w, h, 3, 1, Bg.Dark, 250, 2, "E0", log);
            catalog.Shared = SaveAll(e0, "Props/shared", MedianHeight(e0, 0, e0.Count) / 1.0f, 128);

            // ---- map themes: T1..T5 ground tiles + P1..P5 props
            int[,] tileCells = { { 4, 1 }, { 1, 2 }, { 3, 2 }, { 5, 2 }, { 1, 3 } };
            int[,] propCells = { { 5, 1 }, { 2, 2 }, { 4, 2 }, { 0, 3 }, { 2, 3 } };
            catalog.Themes = new MapTheme[5];
            for (int t = 0; t < 5; t++)
            {
                var theme = new MapTheme();
                var tiles = Cut(raw, w, h, tileCells[t, 0], tileCells[t, 1], Bg.Magenta, 2500, -7, "T" + (t + 1), log);
                theme.Tiles = new Sprite[tiles.Count];
                for (int i = 0; i < tiles.Count; i++)
                {
                    // Square 128 px, fully opaque: a tilemap cell is 1 unit, and padding would show as gaps.
                    var sq = Resample(tiles[i], TileSize, TileSize);
                    bool structured = t == 3 || (t == 0 && i == 3) || (t == 4 && i == 3); // metal plates, planks: keep their lines
                    if (!structured) MakeSeamless(sq, TileSize, TileSize);
                    theme.Tiles[i] = ArtSlicer.Save(sq, TileSize, TileSize, $"{OutDir}/Maps/t{t + 1}_tile_{i}.png", TileSize, new Vector2(0.5f, 0.5f), TileSize);
                }
                var props = Cut(raw, w, h, propCells[t, 0], propCells[t, 1], Bg.Magenta, 250, 2, "P" + (t + 1), log);
                theme.Props = SaveAll(props, $"Maps/p{t + 1}_prop", MedianHeight(props, 0, props.Count) / 1.05f, 128);
                catalog.Themes[t] = theme;
            }

            // ---- UI kit (regular grids are cut slot by slot so multi-part icons like "pause" stay whole)
            var hud = Cut(raw, w, h, 0, 2, Bg.Dark, 150, 3, "U3", log);
            int joy = FindJoystick(hud);
            if (joy >= 0)
            {
                SplitJoystick(hud[joy], out var joyBase, out var joyKnob);
                hud.Add(joyBase);   // index Count-2
                hud.Add(joyKnob);   // index Count-1
                log.AppendLine($"  joystick = U3 item {joy}, split into base {hud.Count - 2} + knob {hud.Count - 1}");
                WriteCheckSheet(hud, "U3_final");
            }
            catalog.Hud = SaveAll(hud, "UI/hud", 100f, 256);
            catalog.Panels = SaveAll(CutGrid(raw, w, h, 3, 3, Bg.Dark, 3, 4, 1, "U1", log), "UI/panel", 100f, 128, 0.3f);
            catalog.Icons = SaveAll(CutGrid(raw, w, h, 4, 3, Bg.Dark, 5, 4, 2, "U2", log), "UI/icon", 100f, 128);
            catalog.Rewards = SaveAll(CutGrid(raw, w, h, 5, 3, Bg.Dark, 4, 3, 2, "U4", log), "UI/reward", 100f, 128);

            catalog.LobbyArt = Picture(raw, w, h, 258, 850, 488, 143, "UI/lobby_art", 512, false, log);
            catalog.LoadingArt = Picture(raw, w, h, 746, 850, 522, 143, "UI/loading_art", 512, false, log);
            catalog.AppIcon = Picture(raw, w, h, 1268, 850, 266, 143, "UI/app_icon", 256, true, log);
        }

        // ------------------------------------------------------------------ cutting

        /// <summary>
        /// Removes the cell background, finds the items and returns them in reading order.
        /// <paramref name="pad"/> &lt; 0 crops inward (ground tiles: drops the anti-aliased rim).
        /// </summary>
        private static List<Piece> Cut(Color32[] raw, int w, int h, int col, int row, Bg bg, int minArea, int pad, string id, System.Text.StringBuilder log)
        {
            int x0 = ColX0[col], x1 = ColX1[col], y0 = RowY0[row], y1 = RowY1[row];
            var region = ArtSlicer.TopLeft(x0, y0, x1 - x0, y1 - y0, h);
            int cw = region.width, ch = region.height;
            var px = ArtSlicer.Crop(raw, w, region);

            byte alphaMin = 128;
            switch (bg)
            {
                case Bg.Magenta: ArtSlicer.KeyMagenta(px); break;
                case Bg.Dark: KeyDark(px, cw, ch); break;
                case Bg.Glow: KeyGlow(px, cw, ch); alphaMin = 40; break;
            }

            var items = ArtSlicer.FindItems(px, cw, ch, new RectInt(0, 0, cw, ch), minArea, alphaMin);
            ArtSlicer.SortGrid(items);
            var pieces = new List<Piece>(items.Count);
            foreach (var it in items)
            {
                RectInt box;
                if (pad >= 0) box = ArtSlicer.Pad(it.Box, pad, cw, ch);
                else box = new RectInt(it.Box.xMin - pad, it.Box.yMin - pad, it.Box.width + pad * 2, it.Box.height + pad * 2);
                if (box.width < 4 || box.height < 4) continue;
                pieces.Add(new Piece { Px = ArtSlicer.Crop(px, cw, box), W = box.width, H = box.height });
            }
            log.AppendLine($"{id}: {pieces.Count} items ({bg})");
            WriteCheckSheet(pieces, id);
            return pieces;
        }

        /// <summary>Regular icon grids: one item per slot (bounding box of everything opaque in the slot).</summary>
        private static List<Piece> CutGrid(Color32[] raw, int w, int h, int col, int row, Bg bg, int cols, int rows, int pad, string id, System.Text.StringBuilder log)
        {
            int x0 = ColX0[col], x1 = ColX1[col], y0 = RowY0[row], y1 = RowY1[row];
            var region = ArtSlicer.TopLeft(x0, y0, x1 - x0, y1 - y0, h);
            int cw = region.width, ch = region.height;
            var px = ArtSlicer.Crop(raw, w, region);
            byte alphaMin = 128;
            if (bg == Bg.Magenta) ArtSlicer.KeyMagenta(px);
            else if (bg == Bg.Dark) KeyDark(px, cw, ch);
            else { KeyGlow(px, cw, ch); alphaMin = 40; }

            var pieces = new List<Piece>(cols * rows);
            for (int r = 0; r < rows; r++)
                for (int c = 0; c < cols; c++)
                {
                    int sx0 = c * cw / cols + 2, sx1 = (c + 1) * cw / cols - 2;
                    int syTop = r * ch / rows + 2, syBot = (r + 1) * ch / rows - 2;
                    int by0 = ch - syBot, by1 = ch - syTop; // texture rows are bottom-up
                    var slot = new RectInt(sx0, by0, sx1 - sx0, by1 - by0);
                    // every part of the icon (pause = two bars), minus slivers of neighbours touching the slot edge
                    var parts = ArtSlicer.FindItems(px, cw, ch, slot, 40, alphaMin);
                    RectInt? box0 = null;
                    foreach (var it in parts)
                    {
                        var b = it.Box;
                        bool touches = b.xMin <= slot.xMin || b.yMin <= slot.yMin || b.xMax >= slot.xMax || b.yMax >= slot.yMax;
                        if (touches && (b.width < 8 || b.height < 8)) continue;
                        box0 = box0.HasValue ? ArtSlicer.Union(box0.Value, b) : b;
                    }
                    if (!box0.HasValue) continue;
                    var box = ArtSlicer.Pad(box0.Value, pad, cw, ch);
                    pieces.Add(new Piece { Px = ArtSlicer.Crop(px, cw, box), W = box.width, H = box.height });
                }
            log.AppendLine($"{id}: {pieces.Count} items (grid {cols}x{rows}, {bg})");
            WriteCheckSheet(pieces, id);
            return pieces;
        }

        /// <summary>The joystick is the most circular large item on the HUD sheet with a grey center.</summary>
        private static int FindJoystick(List<Piece> hud)
        {
            int best = -1, bestSize = 0;
            for (int i = 0; i < hud.Count; i++)
            {
                var p = hud[i];
                float aspect = p.W / (float)p.H;
                if (aspect < 0.85f || aspect > 1.18f) continue;
                var c = p.Px[(p.H / 2) * p.W + p.W / 2];
                bool greyCenter = c.a > 200 && Mathf.Abs(c.r - c.b) < 80 && Mathf.Abs(c.r - c.g) < 60 && c.r > 30 && c.r < 200;
                if (greyCenter && p.W * p.H > bestSize) { best = i; bestSize = p.W * p.H; }
            }
            return best;
        }

        /// <summary>Cuts the painted joystick into a movable knob and a base whose center is filled in.</summary>
        private static void SplitJoystick(Piece p, out Piece basePiece, out Piece knobPiece)
        {
            int w = p.W, h = p.H;
            float cx = (w - 1) * 0.5f, cy = (h - 1) * 0.5f;
            float R = Mathf.Min(w, h) * 0.5f;
            float rk = R * 0.45f;
            // average color of the ring just outside the knob = what fills the hole
            float sr = 0, sg = 0, sb = 0; int n = 0;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                    var c = p.Px[y * w + x];
                    if (d > rk * 1.15f && d < rk * 1.45f && c.a > 200) { sr += c.r; sg += c.g; sb += c.b; n++; }
                }
            var fill = n > 0 ? new Color32((byte)(sr / n), (byte)(sg / n), (byte)(sb / n), 255) : new Color32(40, 50, 60, 255);
            var bp = (Color32[])p.Px.Clone();
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                    if (d < rk * 1.1f) bp[y * w + x] = fill;
                }
            basePiece = new Piece { Px = bp, W = w, H = h };

            int ks = Mathf.CeilToInt(rk * 2.3f);
            var kp = new Color32[ks * ks];
            float kc = (ks - 1) * 0.5f;
            for (int y = 0; y < ks; y++)
                for (int x = 0; x < ks; x++)
                {
                    int sx = Mathf.RoundToInt(cx - kc + x), sy = Mathf.RoundToInt(cy - kc + y);
                    if (sx < 0 || sy < 0 || sx >= w || sy >= h) continue;
                    float d = Mathf.Sqrt((x - kc) * (x - kc) + (y - kc) * (y - kc));
                    var c = p.Px[sy * w + sx];
                    float a = Mathf.Clamp01((rk * 1.08f - d) / 1.5f);
                    c.a = (byte)(c.a * a);
                    kp[y * ks + x] = c;
                }
            knobPiece = new Piece { Px = kp, W = ks, H = ks };
        }

        private static Sprite[] SaveAll(List<Piece> pieces, string pathPrefix, float ppu, int maxSize, float border = 0f)
        {
            var sprites = new Sprite[pieces.Count];
            for (int i = 0; i < pieces.Count; i++)
            {
                var p = pieces[i];
                float useP = ppu > 0f ? ppu : Mathf.Max(p.W, p.H);
                sprites[i] = ArtSlicer.Save(p.Px, p.W, p.H, $"{OutDir}/{pathPrefix}_{i:00}.png", useP, new Vector2(0.5f, 0.5f), maxSize, border);
            }
            return sprites;
        }

        /// <summary>A whole picture (lobby, loading, app icon) inside a region of the bottom row.</summary>
        private static Sprite Picture(Color32[] raw, int w, int h, int x, int yTop, int rw, int rh, string path, int maxSize, bool keyMagenta, System.Text.StringBuilder log)
        {
            var region = ArtSlicer.TopLeft(x, yTop, rw, rh, h);
            var px = ArtSlicer.Crop(raw, w, region);
            var keyed = (Color32[])px.Clone();
            ArtSlicer.KeyMagenta(keyed);
            var items = ArtSlicer.FindItems(keyed, rw, rh, new RectInt(0, 0, rw, rh), 400);
            if (items.Count == 0) { log.AppendLine(path + ": nothing found"); return null; }
            var box = ArtSlicer.Union(items);
            if (!keyMagenta) box = new RectInt(box.xMin + 2, box.yMin + 2, box.width - 4, box.height - 4);
            var crop = ArtSlicer.Crop(keyMagenta ? keyed : px, rw, box);
            log.AppendLine($"{path}: {box.width}x{box.height}px");
            WriteCheckSheet(new List<Piece> { new Piece { Px = crop, W = box.width, H = box.height } }, Path.GetFileName(path));
            return ArtSlicer.Save(crop, box.width, box.height, $"{OutDir}/{path}.png", 100f, new Vector2(0.5f, 0.5f), maxSize);
        }

        private const int TileSize = 128;

        /// <summary>Bilinear resize of an opaque piece (ground tiles).</summary>
        private static Color32[] Resample(Piece p, int nw, int nh)
        {
            var o = new Color32[nw * nh];
            for (int y = 0; y < nh; y++)
            {
                float sy = Mathf.Clamp((y + 0.5f) * p.H / nh - 0.5f, 0f, p.H - 1);
                int y0 = (int)sy, y1 = Mathf.Min(p.H - 1, y0 + 1);
                float fy = sy - y0;
                for (int x = 0; x < nw; x++)
                {
                    float sx = Mathf.Clamp((x + 0.5f) * p.W / nw - 0.5f, 0f, p.W - 1);
                    int x0 = (int)sx, x1 = Mathf.Min(p.W - 1, x0 + 1);
                    float fx = sx - x0;
                    Color a = Color.Lerp(p.Px[y0 * p.W + x0], p.Px[y0 * p.W + x1], fx);
                    Color b = Color.Lerp(p.Px[y1 * p.W + x0], p.Px[y1 * p.W + x1], fx);
                    Color c = Color.Lerp(a, b, fy);
                    c.a = 1f;
                    o[y * nw + x] = c;
                }
            }
            return o;
        }

        /// <summary>
        /// Offset-and-blend: mixes the tile with a copy shifted by half its size, the copy showing at the
        /// edges. Opposite edges then continue into each other, so a tiled floor shows no grid.
        /// </summary>
        private static void MakeSeamless(Color32[] px, int w, int h)
        {
            var src = (Color32[])px.Clone();
            for (int y = 0; y < h; y++)
            {
                float wy = 1f - Mathf.Abs((y + 0.5f) / h * 2f - 1f);
                for (int x = 0; x < w; x++)
                {
                    float wx = 1f - Mathf.Abs((x + 0.5f) / w * 2f - 1f);
                    float m = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(Mathf.Min(wx, wy) * 2.6f));
                    var a = src[y * w + x];
                    var b = src[((y + h / 2) % h) * w + (x + w / 2) % w];
                    px[y * w + x] = Color32.Lerp(b, a, m);
                }
            }
        }

        private static float MedianHeight(List<Piece> pieces, int from, int to)
        {
            if (to <= from) return 100f;
            var hs = new List<int>(to - from);
            for (int i = from; i < to; i++) hs.Add(pieces[i].H);
            hs.Sort();
            return hs[hs.Count / 2];
        }

        // ------------------------------------------------------------------ background removal

        /// <summary>Median of the cell's outer ring: the background color.</summary>
        private static Color32 BorderColor(Color32[] px, int w, int h)
        {
            var r = new List<int>(); var g = new List<int>(); var b = new List<int>();
            void Add(Color32 c) { r.Add(c.r); g.Add(c.g); b.Add(c.b); }
            for (int x = 0; x < w; x++) { Add(px[x]); Add(px[w + x]); Add(px[(h - 1) * w + x]); Add(px[(h - 2) * w + x]); }
            for (int y = 0; y < h; y++) { Add(px[y * w]); Add(px[y * w + 1]); Add(px[y * w + w - 1]); Add(px[y * w + w - 2]); }
            r.Sort(); g.Sort(); b.Sort();
            int m = r.Count / 2;
            return new Color32((byte)r[m], (byte)g[m], (byte)b[m], 255);
        }

        private static float Dist(Color32 a, Color32 b)
        {
            int dr = a.r - b.r, dg = a.g - b.g, db = a.b - b.b;
            return Mathf.Sqrt(dr * dr + dg * dg + db * db);
        }

        /// <summary>
        /// Dark navy background: flood-fill from the border through background-like pixels, so dark
        /// parts inside an item's black outline stay opaque. Edge pixels get a soft alpha.
        /// </summary>
        private static void KeyDark(Color32[] px, int w, int h)
        {
            var bg = BorderColor(px, w, h);
            const float flood = 30f;
            var isBg = new bool[w * h];
            var stack = new Stack<int>();
            for (int x = 0; x < w; x++) { Seed(x, 0); Seed(x, h - 1); }
            for (int y = 0; y < h; y++) { Seed(0, y); Seed(w - 1, y); }
            // leftover magenta (cell frames) is background too
            for (int i = 0; i < px.Length; i++)
                if (!isBg[i] && Mathf.Min(px[i].r, px[i].b) - px[i].g > 70) { isBg[i] = true; stack.Push(i); }
            void Seed(int x, int y)
            {
                int i = y * w + x;
                if (!isBg[i] && Dist(px[i], bg) < flood) { isBg[i] = true; stack.Push(i); }
            }
            while (stack.Count > 0)
            {
                int i = stack.Pop();
                int x = i % w, y = i / w;
                if (x > 0) Seed(x - 1, y);
                if (x < w - 1) Seed(x + 1, y);
                if (y > 0) Seed(x, y - 1);
                if (y < h - 1) Seed(x, y + 1);
            }
            var outPx = new Color32[px.Length];
            for (int i = 0; i < px.Length; i++)
            {
                if (isBg[i]) { outPx[i] = new Color32(0, 0, 0, 0); continue; }
                int x = i % w, y = i / w;
                bool edge = (x > 0 && isBg[i - 1]) || (x < w - 1 && isBg[i + 1]) || (y > 0 && isBg[i - w]) || (y < h - 1 && isBg[i + w]);
                var c = px[i];
                c.a = edge ? (byte)Mathf.Clamp(Mathf.RoundToInt(Dist(c, bg) / 70f * 255f), 90, 255) : (byte)255;
                outPx[i] = c;
            }
            System.Array.Copy(outPx, px, px.Length);
        }

        /// <summary>Glowing effects on black: brightness becomes alpha, the dark background is un-blended.</summary>
        private static void KeyGlow(Color32[] px, int w, int h)
        {
            var bg = BorderColor(px, w, h);
            int bgMax = Mathf.Max(bg.r, Mathf.Max(bg.g, bg.b));
            float range = Mathf.Max(30f, 255f - bgMax);
            for (int i = 0; i < px.Length; i++)
            {
                var c = px[i];
                int m = Mathf.Max(c.r, Mathf.Max(c.g, c.b)) - bgMax;
                float a = Mathf.Clamp01(m / range * 1.3f);
                if (a < 0.04f) { px[i] = new Color32(0, 0, 0, 0); continue; }
                float inv = 1f / a;
                float r = (c.r - bg.r * (1f - a)) * inv, g = (c.g - bg.g * (1f - a)) * inv, b = (c.b - bg.b * (1f - a)) * inv;
                px[i] = new Color32((byte)Mathf.Clamp(r, 0f, 255f), (byte)Mathf.Clamp(g, 0f, 255f), (byte)Mathf.Clamp(b, 0f, 255f), (byte)(a * 255f));
            }
        }

        // ------------------------------------------------------------------ check sheet

        /// <summary>Items in catalog order on a checkerboard, 8 per row, 96 px slots (debug only).</summary>
        private static void WriteCheckSheet(List<Piece> pieces, string id)
        {
            const int slot = 96, perRow = 8;
            int rows = Mathf.Max(1, (pieces.Count + perRow - 1) / perRow);
            int sw = slot * perRow, sh = slot * rows;
            var o = new Color32[sw * sh];
            for (int y = 0; y < sh; y++)
                for (int x = 0; x < sw; x++)
                {
                    bool odd = ((x / 8) + (y / 8)) % 2 == 1;
                    bool slotOdd = ((x / slot) + (y / slot)) % 2 == 1;
                    byte v = (byte)((odd ? 70 : 90) + (slotOdd ? 40 : 0));
                    o[y * sw + x] = new Color32(v, v, v, 255);
                }
            for (int i = 0; i < pieces.Count; i++)
            {
                var p = pieces[i];
                float s = Mathf.Min((slot - 6f) / p.W, (slot - 6f) / p.H);
                int dw = Mathf.Max(1, (int)(p.W * s)), dh = Mathf.Max(1, (int)(p.H * s));
                int ox = (i % perRow) * slot + (slot - dw) / 2;
                int oyTop = (i / perRow) * slot + (slot - dh) / 2;
                for (int y = 0; y < dh; y++)
                    for (int x = 0; x < dw; x++)
                    {
                        var c = p.Px[Mathf.Min(p.H - 1, (int)(y / s)) * p.W + Mathf.Min(p.W - 1, (int)(x / s))];
                        int ty = sh - 1 - (oyTop + (dh - 1 - y));
                        int ti = ty * sw + ox + x;
                        var d = o[ti];
                        float a = c.a / 255f;
                        o[ti] = new Color32((byte)(c.r * a + d.r * (1f - a)), (byte)(c.g * a + d.g * (1f - a)), (byte)(c.b * a + d.b * (1f - a)), 255);
                    }
            }
            var tex = new Texture2D(sw, sh, TextureFormat.RGBA32, false);
            tex.SetPixels32(o);
            tex.Apply();
            File.WriteAllBytes($"{SheetDir}/{id}.png", tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }
    }
}
