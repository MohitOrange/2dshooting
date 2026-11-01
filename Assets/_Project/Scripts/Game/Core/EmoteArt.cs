using System;
using UnityEngine;
using static NeonBrawl.Sdf;

namespace NeonBrawl
{
    /// <summary>
    /// Eight cartoon emote faces painted in code the first time they are needed (one 512×256 texture).
    /// Every shape is a signed-distance function, so edges stay smooth at any size.
    /// </summary>
    public static class EmoteArt
    {
        public const int Count = 8;
        public const int Happy = 0, Lol = 1, Cool = 2, Angry = 3, Sad = 4, Wow = 5, Love = 6, Cheeky = 7;
        public static readonly string[] Names = { "HAPPY", "LOL", "COOL", "ANGRY", "SAD", "WOW", "LOVE", "CHEEKY" };

        private const int Cell = 128;
        private static Sprite[] _sprites;

        public static Sprite Get(int i)
        {
            if (_sprites == null) Build();
            return (uint)i < Count ? _sprites[i] : null;
        }

        private static readonly Color Ink = new Color(0.28f, 0.15f, 0.04f);
        private static readonly Color MouthIn = new Color(0.45f, 0.08f, 0.1f);
        private static readonly Color Tongue = new Color(1f, 0.45f, 0.52f);
        private static readonly Color Tear = new Color(0.38f, 0.75f, 1f);
        private static readonly Color Cheek = new Color(1f, 0.42f, 0.42f, 0.45f);
        private static readonly Color HeartRed = new Color(0.95f, 0.16f, 0.27f);
        private static readonly Color YellowTop = new Color(1f, 0.9f, 0.36f);
        private static readonly Color YellowBottom = new Color(1f, 0.62f, 0.12f);

        private static void Build()
        {
            _sprites = Sdf.Sheet("NB_Emotes", Count, Cell, Paint);
            for (int i = 0; i < Count; i++) _sprites[i].name = "Emote" + Names[i];
        }

        // ------------------------------------------------------------------ the faces (coordinates -1..1, y up)

        private static void Paint(SdfCanvas g, int id)
        {
            switch (id)
            {
                case Happy:
                    Face(g, YellowTop, YellowBottom);
                    g.Fill(p => Arc(p - new Vector2(-0.32f, 0.1f), 1.1f, 0.15f, 0.055f), Ink);
                    g.Fill(p => Arc(p - new Vector2(0.32f, 0.1f), 1.1f, 0.15f, 0.055f), Ink);
                    OpenMouth(g, new Vector2(0f, -0.1f), 0.5f);
                    Cheeks(g);
                    break;

                case Lol:
                    Face(g, YellowTop, YellowBottom);
                    g.Fill(p => Mathf.Min(Seg(p, new Vector2(-0.52f, 0.34f), new Vector2(-0.24f, 0.2f)), Seg(p, new Vector2(-0.24f, 0.2f), new Vector2(-0.52f, 0.06f))) - 0.05f, Ink);
                    g.Fill(p => Mathf.Min(Seg(p, new Vector2(0.52f, 0.34f), new Vector2(0.24f, 0.2f)), Seg(p, new Vector2(0.24f, 0.2f), new Vector2(0.52f, 0.06f))) - 0.05f, Ink);
                    OpenMouth(g, new Vector2(0f, -0.04f), 0.56f);
                    TearDrop(g, new Vector2(-0.8f, -0.2f), 0.14f);
                    TearDrop(g, new Vector2(0.8f, -0.2f), 0.14f);
                    break;

                case Cool:
                    Face(g, YellowTop, YellowBottom);
                    var glass = new Color(0.08f, 0.09f, 0.14f);
                    g.Fill(p => Mathf.Min(RoundBox(p - new Vector2(-0.33f, 0.12f), new Vector2(0.27f, 0.16f), 0.11f),
                                          RoundBox(p - new Vector2(0.33f, 0.12f), new Vector2(0.27f, 0.16f), 0.11f)), glass);
                    g.Fill(p => RoundBox(p - new Vector2(0f, 0.25f), new Vector2(0.64f, 0.045f), 0.03f), glass);
                    g.Fill(p => Mathf.Min(Seg(p, new Vector2(-0.6f, 0.24f), new Vector2(-0.86f, 0.32f)), Seg(p, new Vector2(0.6f, 0.24f), new Vector2(0.86f, 0.32f))) - 0.035f, glass);
                    var shine = new Color(1f, 1f, 1f, 0.6f);
                    g.Fill(p => Mathf.Min(Seg(p, new Vector2(-0.48f, 0.04f), new Vector2(-0.36f, 0.2f)), Seg(p, new Vector2(0.18f, 0.04f), new Vector2(0.3f, 0.2f))) - 0.028f, shine);
                    g.Fill(p => Arc(Rotate(new Vector2(p.x - 0.08f, -(p.y + 0.16f)), -0.25f), 0.85f, 0.34f, 0.05f), Ink);
                    break;

                case Angry:
                    Face(g, new Color(1f, 0.45f, 0.22f), new Color(0.86f, 0.16f, 0.12f));
                    g.Fill(p => Seg(p, new Vector2(-0.56f, 0.44f), new Vector2(-0.12f, 0.24f)) - 0.065f, Ink);
                    g.Fill(p => Seg(p, new Vector2(0.56f, 0.44f), new Vector2(0.12f, 0.24f)) - 0.065f, Ink);
                    g.Fill(p => Mathf.Min(Ellipse(p - new Vector2(-0.3f, 0.04f), new Vector2(0.085f, 0.12f)), Ellipse(p - new Vector2(0.3f, 0.04f), new Vector2(0.085f, 0.12f))), Ink);
                    g.Fill(p => Arc(p - new Vector2(0f, -0.66f), 0.95f, 0.32f, 0.065f), Ink);
                    break;

                case Sad:
                    Face(g, YellowTop, YellowBottom);
                    g.Fill(p => Seg(p, new Vector2(-0.54f, 0.3f), new Vector2(-0.18f, 0.44f)) - 0.045f, Ink);
                    g.Fill(p => Seg(p, new Vector2(0.54f, 0.3f), new Vector2(0.18f, 0.44f)) - 0.045f, Ink);
                    g.Fill(p => Mathf.Min(Ellipse(p - new Vector2(-0.3f, 0.08f), new Vector2(0.085f, 0.12f)), Ellipse(p - new Vector2(0.3f, 0.08f), new Vector2(0.085f, 0.12f))), Ink);
                    g.Fill(p => Mathf.Min(Circle(p, new Vector2(-0.27f, 0.13f), 0.032f), Circle(p, new Vector2(0.33f, 0.13f), 0.032f)), Color.white);
                    g.Fill(p => Arc(p - new Vector2(0f, -0.64f), 0.9f, 0.27f, 0.055f), Ink);
                    TearDrop(g, new Vector2(-0.34f, -0.36f), 0.13f);
                    break;

                case Wow:
                    Face(g, YellowTop, YellowBottom);
                    g.Fill(p => Mathf.Min(Ellipse(p - new Vector2(-0.3f, 0.14f), new Vector2(0.21f, 0.25f)), Ellipse(p - new Vector2(0.3f, 0.14f), new Vector2(0.21f, 0.25f))), Ink);
                    g.Fill(p => Mathf.Min(Ellipse(p - new Vector2(-0.3f, 0.14f), new Vector2(0.165f, 0.205f)), Ellipse(p - new Vector2(0.3f, 0.14f), new Vector2(0.165f, 0.205f))), Color.white);
                    g.Fill(p => Mathf.Min(Circle(p, new Vector2(-0.28f, 0.1f), 0.09f), Circle(p, new Vector2(0.28f, 0.1f), 0.09f)), Ink);
                    g.Fill(p => Mathf.Min(Arc(p - new Vector2(-0.3f, 0.36f), 0.9f, 0.2f, 0.04f), Arc(p - new Vector2(0.3f, 0.36f), 0.9f, 0.2f, 0.04f)), Ink);
                    g.Fill(p => Ellipse(p - new Vector2(0f, -0.46f), new Vector2(0.19f, 0.24f)), Ink);
                    g.Fill(p => Ellipse(p - new Vector2(0f, -0.46f), new Vector2(0.14f, 0.19f)), MouthIn);
                    g.Fill(p => Mathf.Max(Ellipse(p - new Vector2(0f, -0.6f), new Vector2(0.12f, 0.08f)), Ellipse(p - new Vector2(0f, -0.46f), new Vector2(0.14f, 0.19f))), Tongue);
                    break;

                case Love:
                    Face(g, YellowTop, YellowBottom);
                    g.Fill(p => Mathf.Min(Heart(p, new Vector2(-0.32f, 0.12f), 0.44f), Heart(p, new Vector2(0.32f, 0.12f), 0.44f)), new Color(0.55f, 0.04f, 0.1f));
                    g.Fill(p => Mathf.Min(Heart(p, new Vector2(-0.32f, 0.13f), 0.36f), Heart(p, new Vector2(0.32f, 0.13f), 0.36f)), HeartRed);
                    g.Fill(p => Mathf.Min(Circle(p, new Vector2(-0.4f, 0.2f), 0.04f), Circle(p, new Vector2(0.24f, 0.2f), 0.04f)), new Color(1f, 1f, 1f, 0.8f));
                    OpenMouth(g, new Vector2(0f, -0.24f), 0.38f);
                    Cheeks(g);
                    break;

                case Cheeky:
                    Face(g, YellowTop, YellowBottom);
                    g.Fill(p => Mathf.Min(Seg(p, new Vector2(-0.48f, 0.26f), new Vector2(-0.2f, 0.14f)), Seg(p, new Vector2(-0.2f, 0.14f), new Vector2(-0.48f, 0.04f))) - 0.05f, Ink);
                    g.Fill(p => Ellipse(p - new Vector2(0.3f, 0.12f), new Vector2(0.095f, 0.135f)), Ink);
                    g.Fill(p => Circle(p, new Vector2(0.33f, 0.17f), 0.035f), Color.white);
                    g.Fill(p => UnevenCapsule(p - new Vector2(0.14f, -0.7f), 0.2f, 0.15f, 0.16f), Ink);
                    g.Fill(p => UnevenCapsule(p - new Vector2(0.14f, -0.7f), 0.16f, 0.11f, 0.16f), Tongue);
                    g.Fill(p => Seg(p, new Vector2(0.14f, -0.52f), new Vector2(0.14f, -0.72f)) - 0.02f, new Color(0.85f, 0.3f, 0.38f));
                    g.Fill(p => Arc(new Vector2(p.x, -(p.y + 0.12f)), 0.95f, 0.42f, 0.055f), Ink);
                    break;
            }
        }

        private static void Face(SdfCanvas g, Color top, Color bottom)
        {
            g.Fill(p => p.magnitude - 0.95f, new Color(0f, 0f, 0f, 0.25f), new Vector2(0.03f, -0.05f));
            g.Fill(p => p.magnitude - 0.93f, Ink);
            g.Fill(p => p.magnitude - 0.85f, p => Color.Lerp(bottom, top, Mathf.Clamp01((p.y + 0.75f) / 1.5f)));
            g.Fill(p => Ellipse(Rotate(p - new Vector2(-0.3f, 0.52f), 0.45f), new Vector2(0.27f, 0.13f)), new Color(1f, 1f, 1f, 0.45f));
        }

        private static void Cheeks(SdfCanvas g)
        {
            g.Fill(p => Mathf.Min(Ellipse(p - new Vector2(-0.58f, -0.2f), new Vector2(0.14f, 0.09f)), Ellipse(p - new Vector2(0.58f, -0.2f), new Vector2(0.14f, 0.09f))), Cheek);
        }

        /// <summary>Open laughing mouth: a half circle with teeth on top and a tongue at the bottom.</summary>
        private static void OpenMouth(SdfCanvas g, Vector2 c, float r)
        {
            Func<Vector2, float> mouth = p => Mathf.Max(Circle(p, c, r), p.y - c.y);
            g.Fill(p => Mathf.Max(Circle(p, c, r + 0.05f), p.y - c.y - 0.05f), Ink);
            g.Fill(mouth, MouthIn);
            g.Fill(p => Mathf.Max(Circle(p, new Vector2(c.x, c.y - r * 1.12f), r * 0.55f), mouth(p)), Tongue);
            g.Fill(p => Mathf.Max(c.y - r * 0.2f - p.y, mouth(p)), Color.white);
        }

        private static void TearDrop(SdfCanvas g, Vector2 bottom, float r)
        {
            g.Fill(p => UnevenCapsule(p - bottom, r + 0.04f, 0.05f, r * 2.1f), Ink);
            g.Fill(p => UnevenCapsule(p - bottom, r, 0.02f, r * 2.1f), Tear);
            g.Fill(p => Circle(p, bottom + new Vector2(-r * 0.35f, r * 0.2f), r * 0.28f), new Color(1f, 1f, 1f, 0.75f));
        }

        private static float RoundBox(Vector2 p, Vector2 half, float r) => Sdf.Box(p, Vector2.zero, half, r);
    }
}
