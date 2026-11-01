using System.IO;
using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEngine;

namespace NeonBrawl.EditorTools
{
    /// <summary>
    /// Turns the 160×136 app_icon art into a full set of app icons: a square 1024 master (the art is
    /// widened onto a painted burst so nothing gets squashed), Android legacy/round/adaptive icons, and the
    /// 512 px Play Store icon (docs/store/icon_512.png). Bicubic upscale + a light unsharp mask.
    /// Menu: Neon Brawl ▸ Build ▸ Bake App Icons.
    /// </summary>
    public static class AppIconBaker
    {
        private const string Source = "Assets/_Project/Art/UI/app_icon.png";
        private const string OutDir = "Assets/_Project/Art/Icon";
        private const string StoreDir = "docs/store";

        [MenuItem("Neon Brawl/Build/Bake App Icons")]
        public static void Bake()
        {
            var src = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            src.LoadImage(File.ReadAllBytes(Source));
            // inner art without the 2 px dark rim and rounded corners
            var art = Crop(src, 2, 2, src.width - 4, src.height - 4);

            Directory.CreateDirectory(OutDir);
            Directory.CreateDirectory(StoreDir);

            var master = Compose(art, 1024);
            Save(master, OutDir + "/icon_1024.png");
            Save(Resize(master, 512), StoreDir + "/icon_512.png");

            // adaptive: burst background + the full icon kept inside the 72/108 safe circle
            Save(Burst(432), OutDir + "/icon_adaptive_bg.png");
            var fg = new Texture2D(432, 432, TextureFormat.RGBA32, false);
            var clear = new Color32[432 * 432];
            fg.SetPixels32(clear);
            var small = Resize(master, 300);
            fg.SetPixels(66, 66, 300, 300, RoundedAlpha(small, 46f).GetPixels());
            fg.Apply();
            Save(fg, OutDir + "/icon_adaptive_fg.png");

            AssetDatabase.Refresh();
            foreach (var p in new[] { "icon_1024", "icon_adaptive_bg", "icon_adaptive_fg" })
            {
                var imp = (TextureImporter)AssetImporter.GetAtPath(OutDir + "/" + p + ".png");
                imp.textureType = TextureImporterType.Default;
                imp.mipmapEnabled = false;
                imp.alphaIsTransparency = true;
                imp.textureCompression = TextureImporterCompression.Uncompressed;
                imp.npotScale = TextureImporterNPOTScale.None;
                imp.SaveAndReimport();
            }
            Apply();
            Debug.Log("[Icon] baked app icons");
        }

        /// <summary>Points the player settings at the baked icons (default + every Android kind).</summary>
        public static void Apply()
        {
            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(OutDir + "/icon_1024.png");
            var bg = AssetDatabase.LoadAssetAtPath<Texture2D>(OutDir + "/icon_adaptive_bg.png");
            var fg = AssetDatabase.LoadAssetAtPath<Texture2D>(OutDir + "/icon_adaptive_fg.png");
            if (icon == null) return;
            PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);
            var android = NamedBuildTarget.Android;
            foreach (var kind in new[] { AndroidPlatformIconKind.Legacy, AndroidPlatformIconKind.Round })
            {
                var icons = PlayerSettings.GetPlatformIcons(android, kind);
                for (int i = 0; i < icons.Length; i++) icons[i].SetTexture(icon);
                PlayerSettings.SetPlatformIcons(android, kind, icons);
            }
            var adaptive = PlayerSettings.GetPlatformIcons(android, AndroidPlatformIconKind.Adaptive);
            for (int i = 0; i < adaptive.Length; i++) adaptive[i].SetTextures(bg, fg);
            PlayerSettings.SetPlatformIcons(android, AndroidPlatformIconKind.Adaptive, adaptive);
            AssetDatabase.SaveAssets();
        }

        // ------------------------------------------------------------------ painting

        private static Texture2D Compose(Texture2D art, int size)
        {
            var bg = Burst(size);
            // the art is wider than tall: fit its width and let the painted burst fill above and below
            int w = size, h = Mathf.RoundToInt(art.height * (float)size / art.width);
            var big = Sharpen(Resize(art, w, h), 0.55f);
            int y0 = (size - h) / 2;
            const float feather = 70f;
            var px = bg.GetPixels();
            var a = big.GetPixels();
            for (int y = 0; y < h; y++)
            {
                float edge = Mathf.Min(y, h - 1 - y);
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(edge / feather));
                for (int x = 0; x < w; x++)
                {
                    var c = a[y * w + x];
                    int i = (y + y0) * size + x;
                    px[i] = Color.Lerp(px[i], new Color(c.r, c.g, c.b, 1f), c.a * k);
                }
            }
            bg.SetPixels(px);
            bg.Apply();
            return bg;
        }

        /// <summary>An explosion burst like the icon art: hot yellow core, orange rays, dark rim.</summary>
        private static Texture2D Burst(int size)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var px = new Color[size * size];
            var core = new Color(1f, 0.86f, 0.2f);
            var mid = new Color(0.98f, 0.62f, 0.08f);
            var rim = new Color(0.62f, 0.24f, 0.04f);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size * 2f - 1f, v = (y + 0.5f) / size * 2f - 1f;
                    float r = Mathf.Sqrt(u * u + v * v) / 1.25f;
                    float ang = Mathf.Atan2(v, u);
                    float rays = Mathf.Pow(Mathf.Abs(Mathf.Cos(ang * 7f)), 6f) * 0.5f + Mathf.Pow(Mathf.Abs(Mathf.Cos(ang * 11f + 0.6f)), 10f) * 0.3f;
                    float g = Mathf.Clamp01(r - rays * 0.25f * (1f - r));
                    var c = g < 0.5f ? Color.Lerp(core, mid, g / 0.5f) : Color.Lerp(mid, rim, (g - 0.5f) / 0.5f);
                    c.a = 1f;
                    px[y * size + x] = c;
                }
            t.SetPixels(px);
            t.Apply();
            return t;
        }

        private static Texture2D RoundedAlpha(Texture2D t, float radius)
        {
            int w = t.width, h = t.height;
            var px = t.GetPixels();
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float dx = Mathf.Max(0f, Mathf.Max(radius - x, x - (w - 1 - radius)));
                    float dy = Mathf.Max(0f, Mathf.Max(radius - y, y - (h - 1 - radius)));
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(radius + 0.5f - d);
                    px[y * w + x].a *= a;
                }
            t.SetPixels(px);
            t.Apply();
            return t;
        }

        // ------------------------------------------------------------------ resampling

        private static Texture2D Crop(Texture2D t, int x, int y, int w, int h)
        {
            var c = new Texture2D(w, h, TextureFormat.RGBA32, false);
            c.SetPixels(t.GetPixels(x, y, w, h));
            c.Apply();
            return c;
        }

        private static Texture2D Resize(Texture2D t, int size) => Resize(t, size, size);

        /// <summary>Catmull-Rom bicubic resample (premultiplied, so edges don't get dark fringes).</summary>
        private static Texture2D Resize(Texture2D t, int w, int h)
        {
            int sw = t.width, sh = t.height;
            var s = t.GetPixels();
            for (int i = 0; i < s.Length; i++) { s[i].r *= s[i].a; s[i].g *= s[i].a; s[i].b *= s[i].a; }
            var o = new Color[w * h];
            float fx = (float)sw / w, fy = (float)sh / h;
            // shrinking: soften the source first so fine detail averages instead of aliasing
            if (fx > 1.5f) s = BoxBlur(s, sw, sh, Mathf.RoundToInt(fx * 0.4f));
            for (int y = 0; y < h; y++)
            {
                float sy = (y + 0.5f) * fy - 0.5f;
                int iy = Mathf.FloorToInt(sy);
                float ty = sy - iy;
                for (int x = 0; x < w; x++)
                {
                    float sx = (x + 0.5f) * fx - 0.5f;
                    int ix = Mathf.FloorToInt(sx);
                    float tx = sx - ix;
                    Color acc = default;
                    for (int m = -1; m <= 2; m++)
                    {
                        float wy = Cubic(m - ty);
                        int yy = Mathf.Clamp(iy + m, 0, sh - 1);
                        for (int n = -1; n <= 2; n++)
                        {
                            float wx = Cubic(n - tx) * wy;
                            int xx = Mathf.Clamp(ix + n, 0, sw - 1);
                            acc += s[yy * sw + xx] * wx;
                        }
                    }
                    acc.a = Mathf.Clamp01(acc.a);
                    if (acc.a > 0.0001f) { acc.r /= acc.a; acc.g /= acc.a; acc.b /= acc.a; }
                    acc.r = Mathf.Clamp01(acc.r); acc.g = Mathf.Clamp01(acc.g); acc.b = Mathf.Clamp01(acc.b);
                    o[y * w + x] = acc;
                }
            }
            var r = new Texture2D(w, h, TextureFormat.RGBA32, false);
            r.SetPixels(o);
            r.Apply();
            return r;
        }

        private static float Cubic(float x)
        {
            x = Mathf.Abs(x);
            if (x < 1f) return 1.5f * x * x * x - 2.5f * x * x + 1f;
            if (x < 2f) return -0.5f * x * x * x + 2.5f * x * x - 4f * x + 2f;
            return 0f;
        }

        private static Texture2D Sharpen(Texture2D t, float amount)
        {
            int w = t.width, h = t.height;
            var px = t.GetPixels();
            var blur = BoxBlur(BoxBlur((Color[])px.Clone(), w, h, 2), w, h, 2);
            for (int i = 0; i < px.Length; i++)
            {
                var c = px[i] + (px[i] - blur[i]) * amount;
                px[i] = new Color(Mathf.Clamp01(c.r), Mathf.Clamp01(c.g), Mathf.Clamp01(c.b), px[i].a);
            }
            t.SetPixels(px);
            t.Apply();
            return t;
        }

        private static Color[] BoxBlur(Color[] px, int w, int h, int r)
        {
            if (r < 1) return px;
            var tmp = new Color[px.Length];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    Color acc = default;
                    for (int k = -r; k <= r; k++) acc += px[y * w + Mathf.Clamp(x + k, 0, w - 1)];
                    tmp[y * w + x] = acc / (2 * r + 1);
                }
            var o = new Color[px.Length];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    Color acc = default;
                    for (int k = -r; k <= r; k++) acc += tmp[Mathf.Clamp(y + k, 0, h - 1) * w + x];
                    o[y * w + x] = acc / (2 * r + 1);
                }
            return o;
        }

        private static void Save(Texture2D t, string path) => File.WriteAllBytes(path, t.EncodeToPNG());
    }
}
