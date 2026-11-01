using System;
using UnityEngine;
using static NeonBrawl.Sdf;

namespace NeonBrawl
{
    /// <summary>
    /// High-resolution redraw of the painted UI kit (U1 panels, U2 icons, U4 rewards) in the same cartoon
    /// style: thick dark outlines, top-lit gradients, gloss and metal corner caps. The collage art was only
    /// 30–60 px per item and looked blurry when stretched; these are painted at 256 px from shapes.
    /// Baked into PNGs by the editor (Neon Brawl ▸ Art ▸ Bake HD UI Kit), so phones pay nothing at runtime.
    /// Slot numbers match <see cref="ArtIds"/>.
    /// </summary>
    public static class UiArt
    {
        public const int Cell = 256;
        public const int PanelCount = 12, IconCount = 20, RewardCount = 12;
        /// <summary>9-slice border of the panel sprites, in pixels (a quarter of the cell).</summary>
        public const int PanelBorder = 64;

        private static readonly Color Line = new Color(0.07f, 0.05f, 0.09f);

        private static Color C(int r, int g, int b) => new Color(r / 255f, g / 255f, b / 255f);

        // ------------------------------------------------------------------ helpers

        /// <summary>Dark outline, vertical gradient fill, optional soft gloss on the upper part.</summary>
        private static void Painted(SdfCanvas g, Func<Vector2, float> sdf, Color top, Color bottom, float outline = 0.07f, bool gloss = true, float y0 = -0.85f, float y1 = 0.85f)
        {
            g.Fill(p => sdf(p) - outline, Line);
            g.Fill(sdf, p => Color.Lerp(bottom, top, Mathf.Clamp01((p.y - y0) / (y1 - y0))));
            if (gloss)
                g.Fill(p => Mathf.Max(sdf(p) + outline * 0.9f, -(p.y - Mathf.Lerp(y0, y1, 0.55f))), new Color(1f, 1f, 1f, 0.2f));
        }

        private static void Shadow(SdfCanvas g, Func<Vector2, float> sdf) => g.Fill(p => sdf(p) - 0.07f, new Color(0f, 0f, 0f, 0.3f), new Vector2(0.02f, -0.06f));

        private static Color Dark(Color c, float k) => new Color(c.r * k, c.g * k, c.b * k, c.a);

        private static Color Light(Color c, float k) => Color.Lerp(c, Color.white, k);

        // ------------------------------------------------------------------ panels (9-sliced)

        private struct PanelStyle
        {
            public Color Top, Bottom, Rim, Cap;
            public bool Gloss, Wood, Rivets;
        }

        private static PanelStyle Style(int id)
        {
            Color steel = C(150, 158, 170), brass = C(214, 160, 70), darkSteel = C(96, 102, 112);
            switch (id)
            {
                case ArtIds.PanelBlue: return new PanelStyle { Top = C(96, 170, 255), Bottom = C(28, 92, 214), Rim = C(16, 38, 92), Cap = steel, Gloss = true };
                case ArtIds.PanelGold: return new PanelStyle { Top = C(255, 222, 92), Bottom = C(240, 150, 22), Rim = C(110, 56, 10), Cap = C(160, 96, 20), Gloss = true };
                case ArtIds.PanelDark: return new PanelStyle { Top = C(54, 57, 63), Bottom = C(27, 28, 32), Rim = C(10, 10, 12), Cap = darkSteel, Rivets = true };
                case ArtIds.PanelTan: return new PanelStyle { Top = C(250, 220, 165), Bottom = C(226, 180, 112), Rim = C(118, 76, 36), Cap = brass };
                case ArtIds.PanelWood: return new PanelStyle { Top = C(218, 128, 58), Bottom = C(160, 78, 30), Rim = C(70, 32, 12), Cap = brass, Wood = true };
                case ArtIds.PanelDark2: return new PanelStyle { Top = C(44, 46, 52), Bottom = C(20, 21, 25), Rim = C(6, 6, 8), Cap = darkSteel, Rivets = true };
                case ArtIds.PanelOrange: return new PanelStyle { Top = C(255, 170, 80), Bottom = C(226, 104, 30), Rim = C(104, 44, 10), Cap = brass, Gloss = true };
                case ArtIds.PanelWood2: return new PanelStyle { Top = C(178, 104, 50), Bottom = C(122, 64, 26), Rim = C(56, 26, 10), Cap = brass, Wood = true };
                case ArtIds.PanelSand: return new PanelStyle { Top = C(252, 212, 120), Bottom = C(232, 160, 62), Rim = C(124, 72, 22), Cap = brass };
                case ArtIds.PanelSlate: return new PanelStyle { Top = C(58, 64, 76), Bottom = C(30, 33, 41), Rim = C(10, 12, 16), Cap = steel, Rivets = true };
                case ArtIds.PanelSteel: return new PanelStyle { Top = C(122, 138, 160), Bottom = C(70, 84, 106), Rim = C(24, 30, 40), Cap = C(190, 198, 210), Rivets = true };
                default: return new PanelStyle { Top = C(236, 224, 200), Bottom = C(200, 186, 160), Rim = C(100, 88, 70), Cap = steel };
            }
        }

        public static void PaintPanel(SdfCanvas g, int id)
        {
            var s = Style(id);
            const float half = 0.97f, r = 0.4f, rim = 0.09f;
            Func<Vector2, float> outer = p => Box(p, Vector2.zero, new Vector2(half, half), r);
            Func<Vector2, float> inner = p => Box(p, Vector2.zero, new Vector2(half - rim, half - rim), r - rim);
            g.Fill(outer, s.Rim);
            g.Fill(inner, p =>
            {
                Color c = Color.Lerp(s.Bottom, s.Top, Mathf.Clamp01((p.y + 0.75f) / 1.5f));
                float n = Mathf.PerlinNoise(p.x * 9f + 3.1f, p.y * 9f + 7.7f) - 0.5f; // painted, not flat
                return Dark(c, 1f + n * 0.06f);
            });
            if (s.Wood)
                g.Fill(p => Mathf.Max(Mathf.Abs(Mathf.Sin(p.y * 15f + Mathf.Sin(p.x * 2.3f) * 0.9f)) - 0.12f, inner(p) + 0.04f), new Color(0.25f, 0.1f, 0.02f, 0.22f));
            if (s.Gloss)
                g.Fill(p => Mathf.Max(inner(p) + 0.05f, -(p.y - 0.12f)), new Color(1f, 1f, 1f, 0.16f));
            // bevel: bright lip along the top, shade along the bottom
            g.Fill(p => Mathf.Max(Mathf.Abs(inner(p) + 0.035f) - 0.022f, -(p.y - 0.25f)), new Color(1f, 1f, 1f, 0.42f));
            g.Fill(p => Mathf.Max(Mathf.Abs(inner(p) + 0.035f) - 0.026f, p.y + 0.25f), new Color(0f, 0f, 0f, 0.28f));
            // metal corner caps with a rivet
            for (int k = 0; k < 4; k++)
            {
                float sx = k % 2 == 0 ? -1f : 1f, sy = k < 2 ? 1f : -1f;
                Func<Vector2, float> cap = p => Mathf.Min(Box(p, new Vector2(sx * 0.7f, sy * 0.855f), new Vector2(0.2f, 0.07f), 0.03f),
                                                          Box(p, new Vector2(sx * 0.855f, sy * 0.7f), new Vector2(0.07f, 0.2f), 0.03f));
                g.Fill(p => cap(p) - 0.03f, s.Rim);
                g.Fill(cap, p => Color.Lerp(Dark(s.Cap, 0.75f), Light(s.Cap, 0.25f), Mathf.Clamp01((p.y - sy * 0.7f) * 2f + 0.5f)));
                {
                    var rc = new Vector2(sx * 0.8f, sy * 0.8f);
                    g.Fill(p => Circle(p, rc, 0.045f), Dark(s.Cap, 0.5f));
                    g.Fill(p => Circle(p, rc + new Vector2(-0.012f, 0.012f), 0.025f), Light(s.Cap, 0.6f));
                }
            }
        }

        // ------------------------------------------------------------------ icons

        public static void PaintIcon(SdfCanvas g, int id)
        {
            switch (id)
            {
                case ArtIds.IconPlay:
                {
                    Func<Vector2, float> disc = p => Circle(p, Vector2.zero, 0.8f);
                    Shadow(g, disc);
                    Painted(g, disc, C(255, 92, 76), C(196, 22, 28));
                    Func<Vector2, float> tri = p => Triangle(p, new Vector2(-0.22f, 0.42f), new Vector2(-0.22f, -0.42f), new Vector2(0.48f, 0f)) - 0.04f;
                    g.Fill(p => tri(p) - 0.05f, Line);
                    g.Fill(tri, p => Color.Lerp(C(226, 230, 240), Color.white, Mathf.Clamp01(p.y + 0.5f)));
                    break;
                }
                case ArtIds.IconPause:
                {
                    Func<Vector2, float> bars = p => Mathf.Min(Box(p, new Vector2(-0.3f, 0f), new Vector2(0.18f, 0.66f), 0.08f), Box(p, new Vector2(0.3f, 0f), new Vector2(0.18f, 0.66f), 0.08f));
                    Shadow(g, bars);
                    Painted(g, bars, C(120, 236, 120), C(28, 156, 52));
                    break;
                }
                case ArtIds.IconGear:
                {
                    Func<Vector2, float> gear = p =>
                    {
                        float d = Circle(p, Vector2.zero, 0.6f);
                        for (int k = 0; k < 4; k++) d = Mathf.Min(d, Box(Rotate(p, k * Mathf.PI / 4f), Vector2.zero, new Vector2(0.16f, 0.84f), 0.05f));
                        return Mathf.Max(d, -Circle(p, Vector2.zero, 0.26f));
                    };
                    Shadow(g, gear);
                    Painted(g, gear, C(246, 248, 252), C(160, 168, 182));
                    g.Fill(p => Ring(p, Vector2.zero, 0.42f, 0.02f), new Color(0f, 0f, 0f, 0.15f));
                    break;
                }
                case ArtIds.IconHome:
                case ArtIds.IconHome2:
                {
                    Func<Vector2, float> house = p => Mathf.Min(Triangle(p, new Vector2(-0.86f, 0.12f), new Vector2(0.86f, 0.12f), new Vector2(0f, 0.86f)) - 0.04f,
                                                                Box(p, new Vector2(0f, -0.36f), new Vector2(0.58f, 0.5f), 0.06f));
                    Shadow(g, house);
                    Painted(g, house, id == ArtIds.IconHome ? C(96, 176, 255) : C(80, 150, 240), C(26, 92, 210));
                    g.Fill(p => Box(p, new Vector2(0f, -0.5f), new Vector2(0.17f, 0.32f), 0.12f), C(18, 46, 110));
                    break;
                }
                case ArtIds.IconCart:
                case ArtIds.IconCartRed:
                {
                    bool red = id == ArtIds.IconCartRed;
                    Func<Vector2, float> basket = p => Trapezoid(p, new Vector2(0.05f, 0.08f), 0.5f, 0.68f, 0.34f);
                    Func<Vector2, float> frame = p => Mathf.Min(Mathf.Min(Seg(p, new Vector2(-0.8f, 0.62f), new Vector2(-0.58f, 0.5f)), Seg(p, new Vector2(-0.58f, 0.5f), new Vector2(-0.42f, -0.36f))),
                        Seg(p, new Vector2(-0.42f, -0.36f), new Vector2(0.56f, -0.36f))) - 0.07f;
                    Func<Vector2, float> wheels = p => Mathf.Min(Circle(p, new Vector2(-0.3f, -0.62f), 0.14f), Circle(p, new Vector2(0.42f, -0.62f), 0.14f));
                    Func<Vector2, float> all = p => Mathf.Min(Mathf.Min(basket(p), frame(p)), wheels(p));
                    Shadow(g, all);
                    Painted(g, all, red ? C(255, 96, 86) : C(96, 180, 255), red ? C(190, 28, 30) : C(24, 96, 214));
                    for (int k = -1; k <= 1; k++)
                    {
                        float x = 0.05f + k * 0.26f;
                        g.Fill(p => Mathf.Max(Mathf.Abs(p.x - x) - 0.035f, basket(p) + 0.1f), new Color(0f, 0f, 0f, 0.25f));
                    }
                    break;
                }
                case ArtIds.IconShirt:
                {
                    Func<Vector2, float> shirt = p =>
                    {
                        float body = Box(p, new Vector2(0f, -0.15f), new Vector2(0.42f, 0.62f), 0.06f);
                        float sleeves = Mathf.Min(Box(Rotate(p - new Vector2(-0.5f, 0.3f), 0.55f), Vector2.zero, new Vector2(0.24f, 0.17f), 0.05f),
                                                  Box(Rotate(p - new Vector2(0.5f, 0.3f), -0.55f), Vector2.zero, new Vector2(0.24f, 0.17f), 0.05f));
                        float shoulders = Box(p, new Vector2(0f, 0.38f), new Vector2(0.55f, 0.12f), 0.08f);
                        return Mathf.Max(Mathf.Min(Mathf.Min(body, sleeves), shoulders), -Circle(p, new Vector2(0f, 0.6f), 0.2f));
                    };
                    Shadow(g, shirt);
                    Painted(g, shirt, C(116, 228, 120), C(26, 150, 60));
                    g.Fill(p => Mathf.Max(Ring(p, new Vector2(0f, 0.6f), 0.22f, 0.03f), -(p.y - 0.36f)), C(16, 90, 36));
                    g.Fill(p => Star5(p, new Vector2(0f, -0.12f), 0.22f, 0.45f), new Color(1f, 1f, 1f, 0.85f));
                    break;
                }
                case ArtIds.IconTrophy:
                {
                    Func<Vector2, float> trophy = p =>
                    {
                        float cup = Mathf.Max(Trapezoid(p, new Vector2(0f, 0.3f), 0.26f, 0.56f, 0.38f), -(p.y + 0.12f) * 1f);
                        float bowl = Mathf.Max(Circle(p, new Vector2(0f, 0.12f), 0.45f), p.y - 0.68f);
                        float handles = Mathf.Min(Ring(p, new Vector2(-0.56f, 0.38f), 0.2f, 0.06f), Ring(p, new Vector2(0.56f, 0.38f), 0.2f, 0.06f));
                        float stem = Box(p, new Vector2(0f, -0.4f), new Vector2(0.1f, 0.2f));
                        float bas = Box(p, new Vector2(0f, -0.68f), new Vector2(0.42f, 0.12f), 0.05f);
                        return Mathf.Min(Mathf.Min(Mathf.Min(cup, bowl), handles), Mathf.Min(stem, bas));
                    };
                    Shadow(g, trophy);
                    Painted(g, trophy, C(255, 120, 76), C(196, 36, 28));
                    g.Fill(p => Star5(p, new Vector2(0f, 0.3f), 0.2f, 0.45f), C(255, 214, 90));
                    break;
                }
                case ArtIds.IconMedal:
                {
                    Func<Vector2, float> ribbon = p => Mathf.Min(Triangle(p, new Vector2(-0.5f, 0.9f), new Vector2(-0.1f, 0.9f), new Vector2(0.05f, 0.1f)),
                                                                 Triangle(p, new Vector2(0.5f, 0.9f), new Vector2(0.1f, 0.9f), new Vector2(-0.05f, 0.1f)));
                    g.Fill(p => ribbon(p) - 0.06f, Line);
                    g.Fill(ribbon, p => p.x < 0f ? C(220, 50, 50) : C(50, 110, 220));
                    Func<Vector2, float> disc = p => Circle(p, new Vector2(0f, -0.22f), 0.58f);
                    Shadow(g, disc);
                    Painted(g, disc, C(255, 224, 100), C(230, 140, 20));
                    g.Fill(p => Ring(p, new Vector2(0f, -0.22f), 0.44f, 0.035f), C(170, 96, 14));
                    g.Fill(p => Star5(p, new Vector2(0f, -0.22f), 0.3f, 0.45f), C(255, 246, 200));
                    break;
                }
                case ArtIds.IconHeart:
                {
                    Func<Vector2, float> heart = p => Heart(p, new Vector2(0f, 0.02f), 1.25f);
                    Shadow(g, heart);
                    Painted(g, heart, C(255, 96, 110), C(200, 22, 50));
                    break;
                }
                case ArtIds.IconStar:
                case ArtIds.IconStar2:
                {
                    Func<Vector2, float> star = p => Star5(p, new Vector2(0f, -0.04f), 0.88f, 0.48f) - 0.04f;
                    Shadow(g, star);
                    Painted(g, star, C(255, 236, 110), C(240, 152, 20));
                    break;
                }
                case ArtIds.IconStar3:
                {
                    Func<Vector2, float> star = p => Star5(p, new Vector2(0f, -0.04f), 0.88f, 0.48f) - 0.04f;
                    Shadow(g, star);
                    Painted(g, star, C(255, 226, 96), C(236, 140, 20));
                    Func<Vector2, float> innerStar = p => Star5(p, new Vector2(0f, -0.04f), 0.46f, 0.48f);
                    g.Fill(p => innerStar(p) - 0.04f, C(170, 90, 14));
                    g.Fill(innerStar, p => Color.Lerp(C(226, 92, 30), C(255, 140, 60), p.y + 0.5f));
                    break;
                }
                case ArtIds.IconShield:
                {
                    Func<Vector2, float> shield = ShieldShape(0.72f);
                    Shadow(g, shield);
                    Painted(g, shield, C(100, 180, 255), C(26, 90, 210));
                    g.Fill(p => Mathf.Max(ShieldShape(0.72f)(p) + 0.16f, -p.x), new Color(1f, 1f, 1f, 0.16f));
                    break;
                }
                case ArtIds.IconPerson:
                {
                    Func<Vector2, float> head = p => Circle(p, new Vector2(0f, 0.36f), 0.3f);
                    Func<Vector2, float> person = p => Mathf.Min(head(p), Box(p, new Vector2(0f, -0.5f), new Vector2(0.62f, 0.38f), 0.3f));
                    Shadow(g, person);
                    Painted(g, p => Box(p, new Vector2(0f, -0.5f), new Vector2(0.62f, 0.38f), 0.3f), C(96, 176, 255), C(30, 96, 214));
                    Painted(g, head, C(255, 210, 160), C(226, 150, 96));
                    break;
                }
                case ArtIds.IconFlagRed:
                case ArtIds.IconFlagBlue:
                {
                    bool red = id == ArtIds.IconFlagRed;
                    Func<Vector2, float> pole = p => Box(p, new Vector2(-0.58f, -0.05f), new Vector2(0.07f, 0.85f), 0.05f);
                    Func<Vector2, float> cloth = p =>
                    {
                        var q = new Vector2(p.x, p.y + Mathf.Sin(p.x * 4.2f) * 0.07f);
                        return Box(q, new Vector2(0.06f, 0.42f), new Vector2(0.58f, 0.34f), 0.05f);
                    };
                    Shadow(g, p => Mathf.Min(pole(p), cloth(p)));
                    Painted(g, cloth, red ? C(255, 112, 70) : C(96, 170, 255), red ? C(214, 50, 26) : C(28, 90, 210));
                    Painted(g, pole, C(255, 222, 120), C(200, 140, 40), 0.06f, false);
                    g.Fill(p => Circle(p, new Vector2(-0.58f, 0.86f), 0.11f), C(255, 214, 90));
                    break;
                }
                case ArtIds.IconBadge:
                case ArtIds.IconRank:
                {
                    bool purple = id == ArtIds.IconBadge;
                    Func<Vector2, float> disc = p => Circle(p, Vector2.zero, 0.8f);
                    Shadow(g, disc);
                    Painted(g, disc, purple ? C(170, 120, 255) : C(255, 110, 150), purple ? C(80, 40, 180) : C(196, 30, 80));
                    g.Fill(p => Ring(p, Vector2.zero, 0.62f, 0.05f), C(255, 214, 90));
                    g.Fill(p => Star5(p, new Vector2(0f, -0.03f), 0.42f, 0.46f) - 0.06f, Line);
                    g.Fill(p => Star5(p, new Vector2(0f, -0.03f), 0.42f, 0.46f), p => Color.Lerp(C(240, 150, 20), C(255, 236, 110), p.y + 0.5f));
                    break;
                }
            }
        }

        /// <summary>Heater shield: flat top, sides curving in to a point at the bottom.</summary>
        private static Func<Vector2, float> ShieldShape(float w) => p =>
        {
            float off = w * 0.76f, rad = w * 1.76f;
            float sides = Mathf.Max(Circle(p, new Vector2(-off, 0.3f), rad), Circle(p, new Vector2(off, 0.3f), rad));
            return Mathf.Max(sides, p.y - 0.78f) - 0.02f;
        };

        // ------------------------------------------------------------------ rewards

        public static void PaintReward(SdfCanvas g, int id)
        {
            switch (id)
            {
                case ArtIds.RewardCoin:
                case ArtIds.RewardSilver:
                {
                    bool gold = id == ArtIds.RewardCoin;
                    Color top = gold ? C(255, 228, 96) : C(236, 240, 248), bottom = gold ? C(232, 140, 18) : C(140, 150, 166), deep = gold ? C(176, 92, 10) : C(96, 104, 120);
                    Func<Vector2, float> coin = p => Circle(p, Vector2.zero, 0.84f);
                    Shadow(g, coin);
                    Painted(g, coin, top, bottom);
                    g.Fill(p => Circle(p, Vector2.zero, 0.62f), p => Color.Lerp(top, bottom, Mathf.Clamp01(0.55f - p.y * 0.7f)));
                    g.Fill(p => Ring(p, Vector2.zero, 0.62f, 0.03f), deep);
                    g.Fill(p => Star5(p, new Vector2(0f, -0.03f), 0.4f, 0.46f) - 0.05f, deep);
                    g.Fill(p => Star5(p, new Vector2(0f, -0.03f), 0.4f, 0.46f), Light(top, 0.35f));
                    g.Fill(p => Ellipse(Rotate(p - new Vector2(-0.38f, 0.42f), 0.6f), new Vector2(0.2f, 0.08f)), new Color(1f, 1f, 1f, 0.6f));
                    break;
                }
                case ArtIds.RewardGem:
                case ArtIds.RewardRuby:
                {
                    bool blue = id == ArtIds.RewardGem;
                    Color top = blue ? C(150, 220, 255) : C(255, 140, 140), mid = blue ? C(40, 150, 245) : C(230, 40, 50), deep = blue ? C(16, 70, 190) : C(140, 10, 30);
                    Func<Vector2, float> crown = p => Trapezoid(p, new Vector2(0f, 0.32f), 0.86f, 0.5f, 0.24f);
                    Func<Vector2, float> pavilion = p => Triangle(p, new Vector2(-0.86f, 0.09f), new Vector2(0.86f, 0.09f), new Vector2(0f, -0.86f));
                    Func<Vector2, float> gem = p => Mathf.Min(crown(p), pavilion(p)) - 0.02f;
                    Shadow(g, gem);
                    g.Fill(p => gem(p) - 0.07f, Line);
                    g.Fill(crown, p => Color.Lerp(mid, top, Mathf.Clamp01((p.y - 0.08f) / 0.5f)));
                    g.Fill(p => Mathf.Max(pavilion(p), -crown(p) - 0.0f), p => Color.Lerp(deep, mid, Mathf.Clamp01((p.y + 0.86f) / 0.95f)));
                    // facets
                    var facet = new Color(1f, 1f, 1f, 0.35f);
                    g.Fill(p => Mathf.Max(Seg(p, new Vector2(-0.5f, 0.56f), new Vector2(-0.28f, 0.09f)) - 0.018f, gem(p)), facet);
                    g.Fill(p => Mathf.Max(Seg(p, new Vector2(0.5f, 0.56f), new Vector2(0.28f, 0.09f)) - 0.018f, gem(p)), facet);
                    g.Fill(p => Mathf.Max(Seg(p, new Vector2(-0.28f, 0.09f), new Vector2(0f, -0.86f)) - 0.018f, gem(p)), new Color(1f, 1f, 1f, 0.22f));
                    g.Fill(p => Mathf.Max(Seg(p, new Vector2(0.28f, 0.09f), new Vector2(0f, -0.86f)) - 0.018f, gem(p)), new Color(0f, 0f, 0f, 0.18f));
                    g.Fill(p => Mathf.Max(Mathf.Abs(p.y - 0.09f) - 0.018f, gem(p)), new Color(1f, 1f, 1f, 0.3f));
                    g.Fill(p => Triangle(p, new Vector2(-0.5f, 0.5f), new Vector2(-0.3f, 0.5f), new Vector2(-0.45f, 0.2f)), new Color(1f, 1f, 1f, 0.7f));
                    break;
                }
                case ArtIds.RewardChest:
                case ArtIds.RewardChest2:
                case ArtIds.RewardChest3:
                case ArtIds.RewardChestIron:
                    PaintChest(g, id);
                    break;
                case ArtIds.RankStar:
                {
                    Func<Vector2, float> plate = p => Circle(p, new Vector2(0f, -0.06f), 0.72f);
                    Shadow(g, plate);
                    Painted(g, plate, C(150, 110, 230), C(70, 40, 150));
                    Func<Vector2, float> star = p => Star5(p, new Vector2(0f, -0.02f), 0.9f, 0.48f) - 0.03f;
                    Painted(g, star, C(255, 232, 104), C(240, 140, 18));
                    break;
                }
                default:
                {
                    // rank shields: blue / red / gold-on-black, with a star
                    Color top, bottom, rim;
                    if (id == ArtIds.RankBlue) { top = C(110, 170, 255); bottom = C(30, 80, 200); rim = C(200, 210, 230); }
                    else if (id == ArtIds.RankRed) { top = C(255, 110, 80); bottom = C(200, 36, 26); rim = C(255, 210, 110); }
                    else { top = C(70, 70, 80); bottom = C(20, 20, 26); rim = C(255, 206, 80); }
                    Func<Vector2, float> outer = ShieldShape(0.78f);
                    Shadow(g, outer);
                    Painted(g, outer, Light(rim, 0.3f), Dark(rim, 0.7f));
                    Func<Vector2, float> inner = p => outer(p) + 0.14f;
                    g.Fill(inner, p => Color.Lerp(bottom, top, Mathf.Clamp01((p.y + 0.7f) / 1.4f)));
                    Func<Vector2, float> star = p => Star5(p, new Vector2(0f, 0.08f), 0.42f, 0.46f);
                    g.Fill(p => star(p) - 0.05f, Line);
                    g.Fill(star, p => id == ArtIds.RankBlue ? Color.Lerp(C(200, 30, 30), C(255, 110, 90), p.y + 0.3f) : Color.Lerp(C(240, 150, 20), C(255, 236, 110), p.y + 0.3f));
                    break;
                }
            }
        }

        private static void PaintChest(SdfCanvas g, int id)
        {
            bool iron = id == ArtIds.RewardChestIron;
            Color woodTop = iron ? C(130, 140, 156) : id == ArtIds.RewardChest3 ? C(214, 124, 52) : C(196, 112, 50);
            Color woodBottom = iron ? C(70, 78, 92) : C(120, 60, 24);
            Color trimTop = iron ? C(210, 216, 226) : C(255, 222, 100), trimBottom = iron ? C(120, 128, 140) : C(214, 136, 20);
            Func<Vector2, float> body = p => Box(p, new Vector2(0f, -0.3f), new Vector2(0.82f, 0.42f), 0.08f);
            Func<Vector2, float> lid = p => Mathf.Max(Box(p, new Vector2(0f, 0.3f), new Vector2(0.82f, 0.36f), 0.3f), -(p.y - 0.08f));
            Func<Vector2, float> chest = p => Mathf.Min(body(p), lid(p));
            Shadow(g, chest);
            Painted(g, chest, woodTop, woodBottom, 0.07f, false, -0.75f, 0.66f);
            // planks
            g.Fill(p => Mathf.Max(Mathf.Abs(Mathf.Repeat(p.y + 0.72f, 0.2f) - 0.1f) - 0.012f, body(p) + 0.05f), new Color(0f, 0f, 0f, 0.22f));
            // seam between lid and body
            g.Fill(p => Box(p, new Vector2(0f, 0.08f), new Vector2(0.86f, 0.05f), 0.02f), Line);
            // metal bands + rim
            Func<Vector2, float> bands = p => Mathf.Min(Mathf.Max(Mathf.Min(Mathf.Abs(p.x + 0.5f), Mathf.Abs(p.x - 0.5f)) - 0.09f, chest(p) + 0.0f),
                                                        Box(p, new Vector2(0f, 0.08f), new Vector2(0.84f, 0.035f)));
            g.Fill(p => bands(p) - 0.04f, Line);
            g.Fill(bands, p => Color.Lerp(trimBottom, trimTop, Mathf.Clamp01((p.y + 0.7f) / 1.4f)));
            // lock
            Func<Vector2, float> lockPlate = p => Box(p, new Vector2(0f, 0.02f), new Vector2(0.17f, 0.2f), 0.05f);
            g.Fill(p => lockPlate(p) - 0.045f, Line);
            g.Fill(lockPlate, p => Color.Lerp(trimBottom, trimTop, Mathf.Clamp01(p.y * 2f + 0.6f)));
            g.Fill(p => Mathf.Min(Circle(p, new Vector2(0f, 0.06f), 0.05f), Box(p, new Vector2(0f, -0.04f), new Vector2(0.022f, 0.07f))), Line);
            // shine on the lid
            g.Fill(p => Mathf.Max(lid(p) + 0.1f, -(p.y - 0.45f)), new Color(1f, 1f, 1f, 0.18f));
        }
    }
}
