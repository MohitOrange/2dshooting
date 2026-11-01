using System;
using UnityEngine;

namespace NeonBrawl
{
    /// <summary>
    /// Paints anti-aliased shapes into a cell of a pixel buffer. Shapes are signed-distance functions in
    /// cell space (-1..1, y up), so the same art looks clean at any texture size. Used for the emote faces,
    /// HUD buttons and icons that have no painted art.
    /// </summary>
    public sealed class SdfCanvas
    {
        private readonly Color[] _px;
        private readonly int _w, _x0, _y0, _n;
        private readonly float _half;

        public SdfCanvas(Color[] px, int width, int x0, int y0, int n)
        {
            _px = px;
            _w = width;
            _x0 = x0;
            _y0 = y0;
            _n = n;
            _half = n * 0.5f;
        }

        public void Fill(Func<Vector2, float> sdf, Color c, Vector2 offset = default) => Fill(sdf, null, c, offset, 0f);

        public void Fill(Func<Vector2, float> sdf, Func<Vector2, Color> shade) => Fill(sdf, shade, Color.white, default, 0f);

        /// <summary>Soft halo outside a shape (fades over <paramref name="radius"/> units).</summary>
        public void Glow(Func<Vector2, float> sdf, Color c, float radius) => Fill(sdf, null, c, default, radius);

        /// <summary>A shape with a dark outline of width <paramref name="outline"/> (cell units).</summary>
        public void Outlined(Func<Vector2, float> sdf, Color fill, Color line, float outline)
        {
            Fill(p => sdf(p) - outline, line);
            Fill(sdf, fill);
        }

        private void Fill(Func<Vector2, float> sdf, Func<Vector2, Color> shade, Color flat, Vector2 offset, float glow)
        {
            float pixel = 1f / _half;
            for (int y = 0; y < _n; y++)
                for (int x = 0; x < _n; x++)
                {
                    var p = new Vector2((x + 0.5f) / _half - 1f, (y + 0.5f) / _half - 1f);
                    float d = sdf(p - offset);
                    float cover;
                    if (glow > 0f)
                    {
                        if (d >= glow) continue;
                        float t = 1f - Mathf.Clamp01(d / glow);
                        cover = d <= 0f ? 1f : t * t;
                    }
                    else
                    {
                        if (d >= pixel * 0.5f) continue;
                        cover = Mathf.Clamp01(0.5f - d / pixel);
                    }
                    Color c = shade != null ? shade(p) : flat;
                    float a = c.a * cover;
                    if (a <= 0f) continue;
                    int i = (_y0 + y) * _w + _x0 + x;
                    Color dst = _px[i];
                    float outA = a + dst.a * (1f - a);
                    float k = dst.a * (1f - a);
                    _px[i] = new Color((c.r * a + dst.r * k) / outA, (c.g * a + dst.g * k) / outA, (c.b * a + dst.b * k) / outA, outA);
                }
        }
    }

    /// <summary>Signed-distance shapes (negative inside). All in the -1..1 cell space.</summary>
    public static class Sdf
    {
        public static float Circle(Vector2 p, Vector2 c, float r) => (p - c).magnitude - r;

        public static float Ring(Vector2 p, Vector2 c, float r, float half) => Mathf.Abs((p - c).magnitude - r) - half;

        public static float Ellipse(Vector2 p, Vector2 r)
        {
            float k = new Vector2(p.x / r.x, p.y / r.y).magnitude;
            return (k - 1f) * Mathf.Min(r.x, r.y);
        }

        public static float Seg(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 pa = p - a, ba = b - a;
            float h = Mathf.Clamp01(Vector2.Dot(pa, ba) / Vector2.Dot(ba, ba));
            return (pa - ba * h).magnitude;
        }

        public static float Box(Vector2 p, Vector2 c, Vector2 half, float r = 0f)
        {
            p -= c;
            Vector2 q = new Vector2(Mathf.Abs(p.x), Mathf.Abs(p.y)) - half + new Vector2(r, r);
            return Vector2.Max(q, Vector2.zero).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - r;
        }

        /// <summary>Stroke along an arc that bulges up (∩); <paramref name="half"/> = half opening in radians.</summary>
        public static float Arc(Vector2 p, float half, float ra, float rb)
        {
            p.x = Mathf.Abs(p.x);
            Vector2 sc = new Vector2(Mathf.Sin(half), Mathf.Cos(half));
            return (sc.y * p.x > sc.x * p.y ? (p - sc * ra).magnitude : Mathf.Abs(p.magnitude - ra)) - rb;
        }

        public static float Triangle(Vector2 p, Vector2 p0, Vector2 p1, Vector2 p2)
        {
            Vector2 e0 = p1 - p0, e1 = p2 - p1, e2 = p0 - p2;
            Vector2 v0 = p - p0, v1 = p - p1, v2 = p - p2;
            Vector2 pq0 = v0 - e0 * Mathf.Clamp01(Vector2.Dot(v0, e0) / Vector2.Dot(e0, e0));
            Vector2 pq1 = v1 - e1 * Mathf.Clamp01(Vector2.Dot(v1, e1) / Vector2.Dot(e1, e1));
            Vector2 pq2 = v2 - e2 * Mathf.Clamp01(Vector2.Dot(v2, e2) / Vector2.Dot(e2, e2));
            float s = Mathf.Sign(e0.x * e2.y - e0.y * e2.x);
            Vector2 d = Vector2.Min(Vector2.Min(
                new Vector2(Vector2.Dot(pq0, pq0), s * (v0.x * e0.y - v0.y * e0.x)),
                new Vector2(Vector2.Dot(pq1, pq1), s * (v1.x * e1.y - v1.y * e1.x))),
                new Vector2(Vector2.Dot(pq2, pq2), s * (v2.x * e2.y - v2.y * e2.x)));
            return -Mathf.Sqrt(d.x) * Mathf.Sign(d.y);
        }

        /// <summary>Heart of height ≈ <paramref name="size"/> centred on <paramref name="c"/>.</summary>
        public static float Heart(Vector2 p, Vector2 c, float size)
        {
            Vector2 q = (p - c) / size + new Vector2(0f, 0.55f);
            q.x = Mathf.Abs(q.x);
            float d;
            if (q.y + q.x > 1f) d = (q - new Vector2(0.25f, 0.75f)).magnitude - 0.35355f;
            else
            {
                float a = (q - new Vector2(0f, 1f)).sqrMagnitude;
                float m = 0.5f * Mathf.Max(q.x + q.y, 0f);
                float b = (q - new Vector2(m, m)).sqrMagnitude;
                d = Mathf.Sqrt(Mathf.Min(a, b)) * Mathf.Sign(q.x - q.y);
            }
            return d * size;
        }

        /// <summary>Teardrop: round end of radius r1 at the origin, narrow end r2 at height h.</summary>
        public static float UnevenCapsule(Vector2 p, float r1, float r2, float h)
        {
            p.x = Mathf.Abs(p.x);
            float b = (r1 - r2) / h;
            float a = Mathf.Sqrt(1f - b * b);
            float k = Vector2.Dot(p, new Vector2(-b, a));
            if (k < 0f) return p.magnitude - r1;
            if (k > a * h) return (p - new Vector2(0f, h)).magnitude - r2;
            return Vector2.Dot(p, new Vector2(a, b)) - r1;
        }

        /// <summary>Five-point star of outer radius <paramref name="r"/>; <paramref name="rf"/> = inner/outer ratio (≈0.45).</summary>
        public static float Star5(Vector2 p, Vector2 c, float r, float rf)
        {
            p -= c;
            Vector2 k1 = new Vector2(0.809016994f, -0.587785252f), k2 = new Vector2(-k1.x, k1.y);
            p.x = Mathf.Abs(p.x);
            p -= 2f * Mathf.Max(Vector2.Dot(k1, p), 0f) * k1;
            p -= 2f * Mathf.Max(Vector2.Dot(k2, p), 0f) * k2;
            p.x = Mathf.Abs(p.x);
            p.y -= r;
            Vector2 ba = rf * new Vector2(-k1.y, k1.x) - new Vector2(0f, 1f);
            float h = Mathf.Clamp(Vector2.Dot(p, ba) / Vector2.Dot(ba, ba), 0f, r);
            return (p - ba * h).magnitude * Mathf.Sign(p.y * ba.x - p.x * ba.y);
        }

        /// <summary>Trapezoid centred on <paramref name="c"/>: half-width r1 at the bottom, r2 at the top, half-height he.</summary>
        public static float Trapezoid(Vector2 p, Vector2 c, float r1, float r2, float he)
        {
            p -= c;
            Vector2 k1 = new Vector2(r2, he), k2 = new Vector2(r2 - r1, 2f * he);
            p.x = Mathf.Abs(p.x);
            Vector2 ca = new Vector2(p.x - Mathf.Min(p.x, p.y < 0f ? r1 : r2), Mathf.Abs(p.y) - he);
            Vector2 cb = p - k1 + k2 * Mathf.Clamp01(Vector2.Dot(k1 - p, k2) / Vector2.Dot(k2, k2));
            float s = cb.x < 0f && ca.y < 0f ? -1f : 1f;
            return s * Mathf.Sqrt(Mathf.Min(Vector2.Dot(ca, ca), Vector2.Dot(cb, cb)));
        }

        public static Vector2 Rotate(Vector2 p, float rad)
        {
            float c = Mathf.Cos(rad), s = Mathf.Sin(rad);
            return new Vector2(c * p.x - s * p.y, s * p.x + c * p.y);
        }

        /// <summary>Builds a texture of <paramref name="count"/> square cells (4 per row) and slices it into sprites.</summary>
        public static Sprite[] Sheet(string name, int count, int cell, Action<SdfCanvas, int> paint)
        {
            int cols = Mathf.Min(4, count), rows = (count + 3) / 4;
            var tex = new Texture2D(cols * cell, rows * cell, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = name
            };
            var px = new Color[tex.width * tex.height];
            var sprites = new Sprite[count];
            for (int i = 0; i < count; i++)
            {
                int x0 = (i % 4) * cell, y0 = (rows - 1 - i / 4) * cell;
                paint(new SdfCanvas(px, tex.width, x0, y0, cell), i);
                sprites[i] = Sprite.Create(tex, new Rect(x0, y0, cell, cell), new Vector2(0.5f, 0.5f), cell, 0, SpriteMeshType.FullRect);
                sprites[i].name = name + i;
            }
            tex.SetPixels(px);
            tex.Apply(false, true);
            return sprites;
        }
    }
}
