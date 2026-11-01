using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace NeonBrawl.EditorTools
{
    /// <summary>
    /// Turns the AI art sheets in ArtSource/ into game sprites:
    /// removes the magenta background (un-blending the edges so there is no pink fringe), finds each
    /// item, sorts them in grid order, crops, sets pivots/scale, imports them compressed, and fills
    /// Resources/ArtCatalog.asset. Run: Neon Brawl ▸ Art ▸ Slice Art Sources.
    /// </summary>
    public static class ArtSlicer
    {
        private const string SourceDir = "ArtSource";
        private const string OutDir = "Assets/_Project/Art";
        /// <summary>Art up to this many pixels on its longest side is stored uncompressed.</summary>
        public const int SmallArt = 160;

        /// <summary>Applies the compression rule to every sprite already cut (run after changing it).</summary>
        [MenuItem("Neon Brawl/Art/Fix Art Compression")]
        public static void FixCompression()
        {
            int changed = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { OutDir }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!(AssetImporter.GetAtPath(path) is TextureImporter ti) || ti.textureType != TextureImporterType.Sprite) continue;
                ti.GetSourceTextureWidthAndHeight(out int w, out int h);
                var want = Mathf.Max(w, h) <= SmallArt ? TextureImporterCompression.Uncompressed : TextureImporterCompression.CompressedHQ;
                bool overridden = false;
                foreach (var platform in new[] { "Android", "Standalone", "iPhone" })
                {
                    var ps = ti.GetPlatformTextureSettings(platform);
                    if (ps.overridden) { ps.overridden = false; ti.SetPlatformTextureSettings(ps); overridden = true; }
                }
                if (ti.textureCompression == want && !overridden && ti.alphaIsTransparency) continue;
                ti.textureCompression = want;
                ti.alphaIsTransparency = true;
                ti.SaveAndReimport();
                changed++;
            }
            Debug.Log("[NeonBrawl] art compression fixed on " + changed + " sprites");
        }
        private const string CatalogPath = "Assets/_Project/Resources/ArtCatalog.asset";

        // In-game sizes (units)
        private const float BodyHeight = 0.92f;   // shoulder span of a soldier body
        private const float HeadHeight = 0.5f;    // bald head incl. ears

        public sealed class Item
        {
            public RectInt Box;
            public int Area;
            public Vector2 Center;
        }

        [MenuItem("Neon Brawl/Art/Slice Art Sources")]
        public static void SliceAll()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<ArtCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<ArtCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }
            var log = new System.Text.StringBuilder();
            SliceBodies(catalog, log);
            SliceHeads(catalog, log);
            SliceCollage(catalog, log);
            CollageSlicer.SliceAll(catalog, log);
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            UiArtBaker.Bake(); // the HD UI kit replaces the small panels, icons and rewards cut above
            File.WriteAllText("Builds/art_slice_log.txt", log.ToString());
            Debug.Log("[NeonBrawl] Art sliced:\n" + log);
        }

        // ------------------------------------------------------------------ sheets

        private static void SliceBodies(ArtCatalog catalog, System.Text.StringBuilder log)
        {
            var src = Load("C1.png");
            if (src == null) { log.AppendLine("C1.png missing"); return; }
            var px = src.GetPixels32();
            int w = src.width, h = src.height;
            KeyMagenta(px);
            var items = FindItems(px, w, h, new RectInt(0, 0, w, h), 2000);
            SortGrid(items);
            log.AppendLine("C1 bodies found: " + items.Count);
            if (items.Count == 0) return;

            var heights = new List<int>();
            foreach (var it in items) heights.Add(it.Box.height);
            heights.Sort();
            float ppu = heights[heights.Count / 2] / BodyHeight;

            int n = Mathf.Min(items.Count, 8);
            catalog.BodyTint = new Sprite[n];
            catalog.BodyDetail = new Sprite[n];
            float handsSum = 0f;
            for (int i = 0; i < n; i++)
            {
                var box = Pad(items[i].Box, 4, w, h);
                var crop = Crop(px, w, box);
                Vector2 pivot = FindNeck(crop, box.width, box.height);
                SplitBySaturation(crop, out var tint, out var detail);
                string baseName = $"{OutDir}/Characters/body_{i + 1:00}";
                catalog.BodyTint[i] = Save(tint, box.width, box.height, baseName + "_tint.png", ppu, pivot, 256);
                catalog.BodyDetail[i] = Save(detail, box.width, box.height, baseName + "_detail.png", ppu, pivot, 256);
                float hands = (box.width * (1f - pivot.x) - 18f) / ppu;
                handsSum += hands;
                log.AppendLine($"  body {i + 1}: {box.width}x{box.height}px pivot {pivot.x:0.00},{pivot.y:0.00} handsForward {hands:0.00}u");
            }
            catalog.HandsForward = handsSum / n;
            log.AppendLine($"  ppu {ppu:0} handsForward avg {catalog.HandsForward:0.00}");
        }

        private static void SliceHeads(ArtCatalog catalog, System.Text.StringBuilder log)
        {
            var src = Load("C2.png");
            if (src == null) { log.AppendLine("C2.png missing"); return; }
            var px = src.GetPixels32();
            int w = src.width, h = src.height;
            KeyMagenta(px);
            var items = FindItems(px, w, h, new RectInt(0, 0, w, h), 1500);
            SortGrid(items);
            log.AppendLine("C2 heads found: " + items.Count);
            if (items.Count == 0) return;

            float ppu = items[0].Box.height / HeadHeight;
            int n = Mathf.Min(items.Count, 16);
            catalog.Heads = new Sprite[n];
            for (int i = 0; i < n; i++)
            {
                var box = Pad(items[i].Box, 4, w, h);
                var crop = Crop(px, w, box);
                Vector2 pivot = FindHeadCenter(crop, box.width, box.height);
                catalog.Heads[i] = Save(crop, box.width, box.height, $"{OutDir}/Characters/head_{i:00}.png", ppu, pivot, 256);
                log.AppendLine($"  head {i}: {box.width}x{box.height}px pivot {pivot.x:0.00},{pivot.y:0.00}");
            }
            log.AppendLine($"  ppu {ppu:0}");
        }

        /// <summary>The 28-sheet preview collage: only the logo and pickup icons are big enough to use.</summary>
        private static void SliceCollage(ArtCatalog catalog, System.Text.StringBuilder log)
        {
            var src = Load("Collage_preview.png");
            if (src == null) { log.AppendLine("Collage_preview.png missing"); return; }
            var px = src.GetPixels32();
            int w = src.width, h = src.height;
            KeyMagenta(px);

            // Regions are in top-left image coordinates (as seen in an image viewer).
            var logoRegion = TopLeft(4, 860, 246, 132, h);
            var logoItems = FindItems(px, w, h, logoRegion, 300);
            if (logoItems.Count > 0)
            {
                var box = Union(logoItems);
                box = Pad(box, 3, w, h);
                catalog.Logo = Save(Crop(px, w, box), box.width, box.height, $"{OutDir}/UI/logo_preview.png", 100f, new Vector2(0.5f, 0.5f), 512);
                log.AppendLine($"Logo: {box.width}x{box.height}px from {logoItems.Count} parts");
            }

            var pickRegion = TopLeft(1284, 4, 248, 184, h);
            var icons = FindItems(px, w, h, pickRegion, 250);
            SortGrid(icons);
            log.AppendLine("W3 icons found: " + icons.Count);
            var names = new[] { "grenade_green", "grenade_red", "grenade_blue", "bottle", "medkit", "ammo_box", "coin", "gem", "heart", "shield", "magnet", "bolt" };
            var sprites = new Sprite[Mathf.Min(icons.Count, names.Length)];
            for (int i = 0; i < sprites.Length; i++)
            {
                var box = Pad(icons[i].Box, 2, w, h);
                float ppu = Mathf.Max(box.width, box.height) / 0.62f;
                sprites[i] = Save(Crop(px, w, box), box.width, box.height, $"{OutDir}/Pickups/{names[i]}.png", ppu, new Vector2(0.5f, 0.5f), 128);
                log.AppendLine($"  icon {i} {names[i]}: {box.width}x{box.height}px");
            }
            catalog.PickupIcons = sprites;
            Sprite Pick(int i) => i < sprites.Length ? sprites[i] : null;
            catalog.PickupGrenade = Pick(0);
            catalog.PickupHealth = Pick(4);
            catalog.PickupAmmo = Pick(5);
            catalog.Coin = Pick(6);
            catalog.Gem = Pick(7);
            catalog.PickupArmor = Pick(9);
        }

        // ------------------------------------------------------------------ image ops

        internal static Texture2D Load(string file)
        {
            string path = Path.Combine(SourceDir, file);
            if (!File.Exists(path)) return null;
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.LoadImage(File.ReadAllBytes(path));
            return tex;
        }

        /// <summary>
        /// Magenta key with edge un-blending: the amount of magenta in a pixel (min(r,b) - g) gives its
        /// transparency, then the magenta is subtracted back out so edges keep their real color.
        /// </summary>
        public static void KeyMagenta(Color32[] px)
        {
            for (int i = 0; i < px.Length; i++)
            {
                var c = px[i];
                float m = (Mathf.Min(c.r, c.b) - c.g) / 255f;
                float a = Mathf.Clamp01(1f - m * 1.12f);
                if (a < 0.06f) { px[i] = new Color32(0, 0, 0, 0); continue; }
                if (a < 0.999f)
                {
                    float inv = 1f / a;
                    float r = (c.r - (1f - a) * 255f) * inv;
                    float g = c.g * inv;
                    float b = (c.b - (1f - a) * 255f) * inv;
                    c = new Color32((byte)Mathf.Clamp(r, 0f, 255f), (byte)Mathf.Clamp(g, 0f, 255f), (byte)Mathf.Clamp(b, 0f, 255f), (byte)(a * 255f));
                }
                px[i] = c;
            }
        }

        /// <summary>Connected opaque regions inside <paramref name="region"/>; small specks merge into nearby items.</summary>
        public static List<Item> FindItems(Color32[] px, int w, int h, RectInt region, int minArea, byte alphaMin = 128)
        {
            var label = new int[w * h];
            var found = new List<Item>();
            var small = new List<Item>();
            var stack = new Stack<int>();
            for (int y = region.yMin; y < region.yMax; y++)
                for (int x = region.xMin; x < region.xMax; x++)
                {
                    int idx = y * w + x;
                    if (label[idx] != 0 || px[idx].a < alphaMin) continue;
                    int id = found.Count + small.Count + 1;
                    int minX = x, maxX = x, minY = y, maxY = y, area = 0;
                    long sx = 0, sy = 0;
                    stack.Push(idx);
                    label[idx] = id;
                    while (stack.Count > 0)
                    {
                        int p = stack.Pop();
                        int px0 = p % w, py0 = p / w;
                        area++; sx += px0; sy += py0;
                        if (px0 < minX) minX = px0; if (px0 > maxX) maxX = px0;
                        if (py0 < minY) minY = py0; if (py0 > maxY) maxY = py0;
                        for (int dy = -1; dy <= 1; dy++)
                            for (int dx = -1; dx <= 1; dx++)
                            {
                                int nx = px0 + dx, ny = py0 + dy;
                                if (nx < region.xMin || ny < region.yMin || nx >= region.xMax || ny >= region.yMax) continue;
                                int ni = ny * w + nx;
                                if (label[ni] != 0 || px[ni].a < alphaMin) continue;
                                label[ni] = id;
                                stack.Push(ni);
                            }
                    }
                    var item = new Item
                    {
                        Box = new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1),
                        Area = area,
                        Center = new Vector2(sx / (float)area, sy / (float)area)
                    };
                    (area >= minArea ? found : small).Add(item);
                }
            foreach (var s in small)
            {
                if (s.Area < 12) continue;
                foreach (var f in found)
                {
                    var grown = new RectInt(f.Box.xMin - 14, f.Box.yMin - 14, f.Box.width + 28, f.Box.height + 28);
                    if (grown.Contains(new Vector2Int((int)s.Center.x, (int)s.Center.y)))
                    {
                        f.Box = Union(f.Box, s.Box);
                        break;
                    }
                }
            }
            return found;
        }

        /// <summary>Reading order: rows top to bottom (texture y is bottom-up), then left to right.</summary>
        public static void SortGrid(List<Item> items)
        {
            if (items.Count == 0) return;
            var heights = new List<int>();
            foreach (var it in items) heights.Add(it.Box.height);
            heights.Sort();
            float tol = heights[heights.Count / 2] * 0.5f;
            items.Sort((a, b) =>
            {
                float ay = a.Box.center.y, by = b.Box.center.y;
                if (Mathf.Abs(ay - by) > tol) return by.CompareTo(ay);
                return a.Box.center.x.CompareTo(b.Box.center.x);
            });
        }

        internal static RectInt TopLeft(int x, int yTop, int width, int height, int texHeight) =>
            new RectInt(x, texHeight - yTop - height, width, height);

        internal static RectInt Pad(RectInt r, int pad, int w, int h)
        {
            int x0 = Mathf.Max(0, r.xMin - pad), y0 = Mathf.Max(0, r.yMin - pad);
            int x1 = Mathf.Min(w, r.xMax + pad), y1 = Mathf.Min(h, r.yMax + pad);
            return new RectInt(x0, y0, x1 - x0, y1 - y0);
        }

        internal static RectInt Union(RectInt a, RectInt b)
        {
            int x0 = Mathf.Min(a.xMin, b.xMin), y0 = Mathf.Min(a.yMin, b.yMin);
            int x1 = Mathf.Max(a.xMax, b.xMax), y1 = Mathf.Max(a.yMax, b.yMax);
            return new RectInt(x0, y0, x1 - x0, y1 - y0);
        }

        internal static RectInt Union(List<Item> items)
        {
            var r = items[0].Box;
            for (int i = 1; i < items.Count; i++) r = Union(r, items[i].Box);
            return r;
        }

        internal static Color32[] Crop(Color32[] px, int w, RectInt box)
        {
            var o = new Color32[box.width * box.height];
            for (int y = 0; y < box.height; y++)
                for (int x = 0; x < box.width; x++)
                    o[y * box.width + x] = px[(box.yMin + y) * w + box.xMin + x];
            return o;
        }

        /// <summary>Center of the dark-brown neck opening in a body (falls back to a fixed spot).</summary>
        private static Vector2 FindNeck(Color32[] px, int w, int h)
        {
            double sx = 0, sy = 0;
            int n = 0;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < (int)(w * 0.6f); x++)
                {
                    var c = px[y * w + x];
                    if (c.a < 200) continue;
                    int lum = (c.r * 3 + c.g * 6 + c.b) / 10;
                    if (lum < 100 && c.r > c.b + 10 && c.r >= c.g && c.r > 30)
                    {
                        sx += x; sy += y; n++;
                    }
                }
            if (n < 150) return new Vector2(0.32f, 0.5f);
            return new Vector2((float)(sx / n) / w, (float)(sy / n) / h);
        }

        /// <summary>Head center from the skin-colored ears at the top and bottom (falls back to the box center).</summary>
        internal static Vector2 FindHeadCenter(Color32[] px, int w, int h)
        {
            int band = Mathf.Max(4, (int)(h * 0.22f));
            Vector2 top = SkinCentroid(px, w, h, h - band, h, out int nt);
            Vector2 bottom = SkinCentroid(px, w, h, 0, band, out int nb);
            if (nt > 30 && nb > 30) return new Vector2((top.x + bottom.x) * 0.5f / w, (top.y + bottom.y) * 0.5f / h);
            return new Vector2(0.5f, 0.5f);
        }

        private static Vector2 SkinCentroid(Color32[] px, int w, int h, int y0, int y1, out int count)
        {
            double sx = 0, sy = 0;
            count = 0;
            for (int y = Mathf.Max(0, y0); y < Mathf.Min(h, y1); y++)
                for (int x = 0; x < w; x++)
                {
                    var c = px[y * w + x];
                    if (c.a < 200) continue;
                    if (c.r > 185 && c.g > 130 && c.g < 220 && c.b > 90 && c.b < 190 && c.r - c.b > 35 && c.r > c.g)
                    {
                        sx += x; sy += y; count++;
                    }
                }
            return count > 0 ? new Vector2((float)(sx / count), (float)(sy / count)) : Vector2.zero;
        }

        /// <summary>Greys (cloth, straps, gloves) go to the tint layer; colored pixels (skin, patches, brass) stay as detail.</summary>
        private static void SplitBySaturation(Color32[] px, out Color32[] tint, out Color32[] detail)
        {
            tint = new Color32[px.Length];
            detail = new Color32[px.Length];
            for (int i = 0; i < px.Length; i++)
            {
                var c = px[i];
                if (c.a == 0) continue;
                int mx = Mathf.Max(c.r, Mathf.Max(c.g, c.b)), mn = Mathf.Min(c.r, Mathf.Min(c.g, c.b));
                float sat = mx > 0 ? (mx - mn) / (float)mx : 0f;
                if (sat > 0.2f && mx > 70) detail[i] = c;
                else tint[i] = c;
            }
        }

        internal static Sprite Save(Color32[] px, int w, int h, string assetPath, float ppu, Vector2 pivot, int maxSize, float borderFrac = 0f)
        {
            // Resize here (not via maxTextureSize) and pad to a multiple of 4: ETC2/DXT only compress
            // textures whose sides are multiples of 4, otherwise Unity stores them raw (4x bigger).
            float scale = Mathf.Min(1f, maxSize / (float)Mathf.Max(w, h));
            if (scale < 1f)
            {
                int nw = Mathf.Max(1, Mathf.RoundToInt(w * scale)), nh = Mathf.Max(1, Mathf.RoundToInt(h * scale));
                px = Downscale(px, w, h, nw, nh);
                ppu *= scale;
                w = nw;
                h = nh;
            }
            if (borderFrac > 0f && ((w & 3) != 0 || (h & 3) != 0) && w >= 8 && h >= 8)
            {
                // 9-sliced panels can't have a transparent pad on one side: shrink to a multiple of 4 instead.
                int nw = w & ~3, nh = h & ~3;
                px = Downscale(px, w, h, nw, nh);
                ppu *= nw / (float)w;
                w = nw;
                h = nh;
            }
            int pw = (w + 3) & ~3, ph = (h + 3) & ~3;
            if (pw != w || ph != h)
            {
                var padded = new Color32[pw * ph];
                for (int y = 0; y < h; y++) System.Array.Copy(px, y * w, padded, y * pw, w);
                pivot = new Vector2(pivot.x * w / pw, pivot.y * h / ph);
                px = padded;
                w = pw;
                h = ph;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(assetPath));
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.SetPixels32(px);
            tex.Apply();
            File.WriteAllBytes(assetPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);

            var ti = (TextureImporter)AssetImporter.GetAtPath(assetPath);
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.spritePixelsPerUnit = ppu;
            ti.mipmapEnabled = false;
            ti.alphaIsTransparency = true;
            ti.filterMode = FilterMode.Bilinear;
            ti.maxTextureSize = Mathf.Max(32, Mathf.NextPowerOfTwo(Mathf.Max(w, h)));
            // Block compression (ASTC/DXT) smears small painted art into blocky "broken" pixels.
            // Small pieces stay uncompressed (they are tiny); big ones use the high-quality format.
            ti.textureCompression = Mathf.Max(w, h) <= SmallArt ? TextureImporterCompression.Uncompressed : TextureImporterCompression.CompressedHQ;
            var s = new TextureImporterSettings();
            ti.ReadTextureSettings(s);
            s.spriteAlignment = (int)SpriteAlignment.Custom;
            s.spritePivot = pivot;
            s.spriteMeshType = SpriteMeshType.FullRect;
            ti.SetTextureSettings(s);
            if (borderFrac > 0f)
            {
                float b = Mathf.Round(Mathf.Min(w, h) * borderFrac);
                ti.spriteBorder = new Vector4(b, b, b, b);
            }
            ti.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        }

        /// <summary>Area-weighted, alpha-weighted downscale (no dark fringes, keeps thin lines).</summary>
        private static Color32[] Downscale(Color32[] src, int w, int h, int nw, int nh)
        {
            var dst = new Color32[nw * nh];
            float sx = w / (float)nw, sy = h / (float)nh;
            for (int y = 0; y < nh; y++)
            {
                float fy0 = y * sy, fy1 = (y + 1) * sy;
                int y0 = (int)fy0, y1 = Mathf.Min(h, Mathf.CeilToInt(fy1));
                for (int x = 0; x < nw; x++)
                {
                    float fx0 = x * sx, fx1 = (x + 1) * sx;
                    int x0 = (int)fx0, x1 = Mathf.Min(w, Mathf.CeilToInt(fx1));
                    float r = 0f, g = 0f, b = 0f, a = 0f, area = 0f;
                    for (int yy = y0; yy < y1; yy++)
                    {
                        float wy = Mathf.Min(yy + 1, fy1) - Mathf.Max(yy, fy0);
                        for (int xx = x0; xx < x1; xx++)
                        {
                            float wgt = wy * (Mathf.Min(xx + 1, fx1) - Mathf.Max(xx, fx0));
                            var c = src[yy * w + xx];
                            float ca = c.a * wgt;
                            r += c.r * ca;
                            g += c.g * ca;
                            b += c.b * ca;
                            a += ca;
                            area += wgt;
                        }
                    }
                    dst[y * nw + x] = a > 0f
                        ? new Color32((byte)Mathf.Min(255f, r / a), (byte)Mathf.Min(255f, g / a), (byte)Mathf.Min(255f, b / a),
                            (byte)Mathf.Clamp(Mathf.RoundToInt(a / area), 0, 255))
                        : new Color32(0, 0, 0, 0);
                }
            }
            return dst;
        }
    }
}
