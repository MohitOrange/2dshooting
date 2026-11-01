using System;
using UnityEngine;
using static NeonBrawl.Sdf;

namespace NeonBrawl
{
    /// <summary>
    /// Clean HD shapes for the lobby and the top bars, painted once in code (no blurry stretched collage art):
    /// dark glass cards and pills with a light rim, a soft drop shadow, a light-ray burst and the ink splash
    /// behind the logo. Sprites use 100 pixels per unit, so a 9-slice border shows 1:1 on the 1080p canvas.
    /// </summary>
    public static class GlassArt
    {
        /// <summary>Card corner radius and 9-slice border, in canvas pixels.</summary>
        public const int CardRadius = 26, CardBorder = 40;

        /// <summary>How far the shadow sprite's fade reaches past the card, at <see cref="ShadowScale"/>.</summary>
        public const float ShadowSpread = 26f, ShadowScale = 1.6f;

        private static Sprite _card, _pill, _shadow, _rays, _splat;

        /// <summary>9-sliced dark glass card: navy body lit from the top, a thin dark edge and a light rim.</summary>
        public static Sprite Card
        {
            get
            {
                if (_card == null) _card = Paint("NB_GlassCard", 128, CardBorder, g => PaintGlass(g, CardRadius / 64f));
                return _card;
            }
        }

        /// <summary>Same glass as a capsule. Set the Image's pixelsPerUnitMultiplier to 128 / height.</summary>
        public static Sprite Pill
        {
            get
            {
                if (_pill == null) _pill = Paint("NB_GlassPill", 128, 64, g => PaintGlass(g, 1f));
                return _pill;
            }
        }

        /// <summary>Soft black block that fades out at the edges (tint it, slice it, multiplier <see cref="ShadowScale"/>).</summary>
        public static Sprite Shadow
        {
            get
            {
                if (_shadow == null) _shadow = Paint("NB_SoftShadow", 128, 64, PaintShadow);
                return _shadow;
            }
        }

        /// <summary>White light-ray burst with a soft core (tint it warm and spin it slowly).</summary>
        public static Sprite Rays
        {
            get
            {
                if (_rays == null) _rays = Paint("NB_Rays", 320, 0, PaintRays);
                return _rays;
            }
        }

        /// <summary>Dark ink splash with a neon rim, drawn square; it is shown stretched about 2:1 behind the logo.</summary>
        public static Sprite Splat
        {
            get
            {
                if (_splat == null) _splat = Paint("NB_Splat", 384, 0, PaintSplat);
                return _splat;
            }
        }

        private static Sprite Paint(string name, int n, int border, Action<SdfCanvas> paint)
        {
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = name
            };
            var px = new Color[n * n];
            paint(new SdfCanvas(px, n, 0, 0, n));
            tex.SetPixels(px);
            tex.Apply(false, true);
            var s = Sprite.Create(tex, new Rect(0f, 0f, n, n), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect,
                new Vector4(border, border, border, border));
            s.name = name;
            return s;
        }

        // ------------------------------------------------------------------ painters (cell space -1..1, y up)

        private static void PaintGlass(SdfCanvas g, float radius)
        {
            const float px = 1f / 64f; // one texel in a 128 cell
            float h = 1f - px * 0.5f;
            float r = Mathf.Min(radius, h);
            Func<Vector2, float> outer = p => Box(p, Vector2.zero, new Vector2(h, h), r);
            float edge = 2.5f * px;
            // thin dark edge, so cards read on bright ground too
            g.Fill(p => Mathf.Abs(outer(p) + edge * 0.5f) - edge * 0.5f, new Color(0.01f, 0.02f, 0.05f, 0.95f));
            // see-through navy body, lighter at the top
            Func<Vector2, float> body = p => outer(p) + edge;
            g.Fill(body, p => Color.Lerp(new Color(0.045f, 0.06f, 0.11f, 0.88f), new Color(0.12f, 0.16f, 0.26f, 0.88f), Mathf.Clamp01(p.y * 0.5f + 0.5f)));
            // light rim just inside the edge: bright along the top, faint at the bottom
            g.Fill(p => Mathf.Abs(body(p) + 1.2f * px) - 1.1f * px,
                p => new Color(1f, 1f, 1f, Mathf.Lerp(0.05f, 0.3f, Mathf.Clamp01(p.y * 0.5f + 0.5f))));
            // faint sheen over the upper part
            g.Fill(p => Mathf.Max(body(p) + 3f * px, -(p.y - 0.2f)), new Color(1f, 1f, 1f, 0.035f));
        }

        private static void PaintShadow(SdfCanvas g)
        {
            g.Fill(p => -1f, p =>
            {
                float d = Box(p, Vector2.zero, new Vector2(0.36f, 0.36f), 0.3f);
                if (d <= 0f) return Color.black;
                float t = Mathf.Clamp01(1f - d / 0.62f);
                return new Color(0f, 0f, 0f, t * t * (3f - 2f * t));
            });
        }

        private static void PaintRays(SdfCanvas g)
        {
            g.Fill(p => -1f, p =>
            {
                float r = p.magnitude;
                if (r >= 1f) return Color.clear;
                float a = Mathf.Atan2(p.y, p.x);
                // 14 rays, every other one a little weaker
                float ray = Mathf.Pow(Mathf.Cos(a * 7f) * 0.5f + 0.5f, 5f) * (0.75f + 0.25f * Mathf.Cos(a * 14f));
                ray += Mathf.Pow(Mathf.Cos(a * 7f + Mathf.PI) * 0.5f + 0.5f, 7f) * 0.45f;
                float fade = Mathf.Pow(1f - r, 1.5f);
                float core = Mathf.Pow(Mathf.Clamp01(1f - r * 1.7f), 2f) * 0.45f;
                return new Color(1f, 1f, 1f, Mathf.Clamp01(ray * fade * Mathf.Clamp01(r * 6f) + core));
            });
        }

        // ink drops flung past the streak tips (x, y, radius); drawn half as wide because the splash is shown ~2:1
        private static readonly Vector3[] Drops =
        {
            new Vector3(-0.93f, 0.12f, 0.06f), new Vector3(0.94f, -0.1f, 0.055f), new Vector3(0.62f, 0.86f, 0.045f),
            new Vector3(-0.55f, -0.88f, 0.05f), new Vector3(0.2f, -0.93f, 0.035f),
        };

        /// <summary>Splash body: a lumpy blob with a few long thin streaks.</summary>
        private static float SplatBlob(Vector2 p)
        {
            float r = p.magnitude, a = Mathf.Atan2(p.y, p.x);
            float edge = 0.6f + 0.05f * Mathf.Sin(a * 3f + 0.7f) + 0.04f * Mathf.Sin(a * 7f + 2.1f) + 0.02f * Mathf.Sin(a * 17f + 0.3f)
                         + 0.3f * Mathf.Pow(Mathf.Max(0f, Mathf.Sin(a * 4f + 0.5f)), 30f)
                         + 0.18f * Mathf.Pow(Mathf.Max(0f, Mathf.Sin(a * 6f + 2f)), 26f)
                         + 0.1f * Mathf.Pow(Mathf.Max(0f, Mathf.Sin(a * 11f + 1f)), 20f);
            return (r - edge) * 0.8f;
        }

        private static float SplatDrops(Vector2 p)
        {
            float d = 9f;
            for (int i = 0; i < Drops.Length; i++)
                d = Mathf.Min(d, Ellipse(p - new Vector2(Drops[i].x, Drops[i].y), new Vector2(Drops[i].z * 0.52f, Drops[i].z)));
            return d;
        }

        private static void PaintSplat(SdfCanvas g)
        {
            var inkTop = new Color(0.08f, 0.06f, 0.15f, 0.97f);
            var inkBottom = new Color(0.02f, 0.02f, 0.05f, 0.97f);
            g.Glow(SplatBlob, new Color(0.5f, 0.28f, 1f, 0.3f), 0.09f);
            g.Fill(p => SplatBlob(p) - 0.01f, new Color(0.62f, 0.4f, 1f, 0.55f));
            g.Fill(p => Mathf.Min(SplatBlob(p), SplatDrops(p)), p => Color.Lerp(inkBottom, inkTop, Mathf.Clamp01(p.y * 0.5f + 0.5f)));
        }
    }
}
