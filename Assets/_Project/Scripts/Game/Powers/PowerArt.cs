using System;
using UnityEngine;
using static NeonBrawl.Sdf;

namespace NeonBrawl
{
    /// <summary>
    /// Power-up art, painted in code the first time it is needed (no image files, nothing added to the
    /// download): a glossy colored badge with a white icon for each power, plus white effect shapes
    /// (shield bubble, snowflake, ice block, footprint, bullet, arrow, field arc, twinkle) that are tinted
    /// in the world and on the minimap.
    /// </summary>
    public static class PowerArt
    {
        public enum Fx { Bubble, Flake, Ice, Foot, Bullet, Arrow, Arc, Twinkle, Count }

        private static readonly Color Line = new Color(0.05f, 0.06f, 0.11f);
        private static readonly Color White = new Color(0.98f, 0.98f, 1f);
        private const float O = 0.07f;

        private static Sprite[] _badges, _fx;

        /// <summary>Round badge with the power's color and icon (1 unit wide).</summary>
        public static Sprite Badge(PowerId id)
        {
            if (_badges == null) _badges = Sheet("NB_PowerBadges", Powers.Count, 192, PaintBadge);
            return _badges[Mathf.Clamp((int)id, 0, Powers.Count - 1)];
        }

        public static Sprite Get(Fx f)
        {
            if (_fx == null) _fx = Sheet("NB_PowerFx", (int)Fx.Count, 128, PaintFx);
            return _fx[(int)f];
        }

        // ------------------------------------------------------------------ badges

        private static void PaintBadge(SdfCanvas g, int i)
        {
            var def = Powers.All[i];
            Color c = def.Color;
            Color dark = new Color(c.r * 0.42f, c.g * 0.42f, c.b * 0.42f);
            Color light = Color.Lerp(c, Color.white, 0.25f);
            g.Fill(p => p.magnitude - 0.95f, new Color(0f, 0f, 0f, 0.35f), new Vector2(0f, -0.05f)); // drop shadow
            g.Fill(p => p.magnitude - 0.93f, Line);                                                  // outer rim
            g.Fill(p => p.magnitude - 0.86f, p => Color.Lerp(dark, light, Mathf.Clamp01(p.y * 0.62f + 0.55f)));
            g.Fill(p => Ring(p, Vector2.zero, 0.85f, 0.014f), new Color(1f, 1f, 1f, 0.45f));       // rim highlight
            g.Fill(p => p.magnitude - 0.72f, p =>
            {
                // inner face, a touch deeper, lit from the top
                float t = Mathf.Clamp01(p.y * 0.5f + 0.6f);
                return Color.Lerp(new Color(c.r * 0.55f, c.g * 0.55f, c.b * 0.55f), c, t);
            });
            g.Fill(p => Ring(p, Vector2.zero, 0.72f, 0.012f), new Color(0f, 0f, 0f, 0.35f));
            Glyph(g, (PowerId)i, c);
            g.Fill(p => Ellipse(p - new Vector2(0f, 0.5f), new Vector2(0.52f, 0.2f)), new Color(1f, 1f, 1f, 0.16f)); // gloss
        }

        private static void Glyph(SdfCanvas g, PowerId id, Color c)
        {
            Color deep = new Color(c.r * 0.7f, c.g * 0.7f, c.b * 0.7f);
            switch (id)
            {
                case PowerId.Turbo:
                {
                    // speed lines behind a boot
                    for (int k = 0; k < 3; k++)
                    {
                        float y = 0.26f - k * 0.24f, x0 = -0.74f + k * 0.08f;
                        g.Fill(p => Seg(p, new Vector2(x0, y), new Vector2(-0.3f, y)) - 0.06f, Line);
                        g.Fill(p => Seg(p, new Vector2(x0, y), new Vector2(-0.3f, y)) - 0.028f, White);
                    }
                    Func<Vector2, float> boot = p => Mathf.Min(
                        Box(p, new Vector2(0.04f, 0.12f), new Vector2(0.17f, 0.3f), 0.07f),
                        Box(p, new Vector2(0.14f, -0.27f), new Vector2(0.3f, 0.14f), 0.11f));
                    g.Outlined(boot, White, Line, O);
                    g.Fill(p => Box(p, new Vector2(0.14f, -0.37f), new Vector2(0.28f, 0.04f), 0.03f), deep);
                    // a little wing on the heel
                    Func<Vector2, float> wing = p => Ellipse(Rotate(p - new Vector2(-0.2f, 0.3f), -0.5f), new Vector2(0.2f, 0.09f));
                    g.Outlined(wing, White, Line, O * 0.8f);
                    break;
                }
                case PowerId.Ghost:
                {
                    Func<Vector2, float> ghost = p =>
                    {
                        float d = Mathf.Min(Circle(p, new Vector2(0f, 0.12f), 0.42f), Box(p, new Vector2(0f, -0.14f), new Vector2(0.42f, 0.26f)));
                        d = Mathf.Max(d, -Circle(p, new Vector2(-0.21f, -0.42f), 0.11f));
                        d = Mathf.Max(d, -Circle(p, new Vector2(0.21f, -0.42f), 0.11f));
                        return d;
                    };
                    g.Outlined(ghost, White, Line, O);
                    g.Fill(p => Ellipse(p - new Vector2(-0.15f, 0.14f), new Vector2(0.075f, 0.12f)), Line);
                    g.Fill(p => Ellipse(p - new Vector2(0.15f, 0.14f), new Vector2(0.075f, 0.12f)), Line);
                    g.Fill(p => Ellipse(p - new Vector2(0f, -0.1f), new Vector2(0.07f, 0.06f)), Line);
                    break;
                }
                case PowerId.Shield:
                {
                    Func<Vector2, float> shield = p => Mathf.Min(
                        Box(p, new Vector2(0f, 0.2f), new Vector2(0.42f, 0.28f), 0.08f),
                        Triangle(p, new Vector2(-0.42f, 0.02f), new Vector2(0.42f, 0.02f), new Vector2(0f, -0.6f)));
                    g.Outlined(shield, White, Line, O);
                    g.Fill(p => Mathf.Max(shield(p) + 0.09f, p.x), deep);
                    g.Fill(p => Star5(p, new Vector2(0f, 0.08f), 0.2f, 0.45f) - O * 0.7f, Line);
                    g.Fill(p => Star5(p, new Vector2(0f, 0.08f), 0.2f, 0.45f), White);
                    break;
                }
                case PowerId.Ammo:
                {
                    Func<Vector2, float> bullets = p =>
                    {
                        float d = 99f;
                        for (int k = -1; k <= 1; k++)
                        {
                            float x = k * 0.27f;
                            d = Mathf.Min(d, Box(p, new Vector2(x, -0.16f), new Vector2(0.1f, 0.24f), 0.03f));
                            d = Mathf.Min(d, UnevenCapsule(p - new Vector2(x, 0.08f), 0.1f, 0.025f, 0.28f));
                        }
                        return d;
                    };
                    g.Outlined(bullets, White, Line, O);
                    for (int k = -1; k <= 1; k++)
                    {
                        float x = k * 0.27f;
                        g.Fill(p => Box(p, new Vector2(x, -0.3f), new Vector2(0.1f, 0.035f)), deep);
                    }
                    break;
                }
                case PowerId.Surge:
                {
                    g.Outlined(p => Heart(p, new Vector2(0f, 0.04f), 0.8f), White, Line, O);
                    g.Fill(p => Mathf.Min(Box(p, new Vector2(0f, 0.02f), new Vector2(0.22f, 0.07f), 0.02f), Box(p, new Vector2(0f, 0.02f), new Vector2(0.07f, 0.22f), 0.02f)), deep);
                    break;
                }
                case PowerId.Pick:
                {
                    for (int k = 0; k < 3; k++)
                    {
                        float a = (k == 0 ? 0.42f : k == 1 ? -0.42f : 0f);
                        Func<Vector2, float> card = p =>
                        {
                            Vector2 q = Rotate(p - new Vector2(0f, -0.55f), -a) + new Vector2(0f, -0.55f);
                            return Box(q, new Vector2(0f, 0.04f), new Vector2(0.21f, 0.32f), 0.06f);
                        };
                        g.Outlined(card, k == 2 ? White : new Color(0.86f, 0.88f, 0.94f), Line, O);
                    }
                    g.Fill(p => Star5(p, new Vector2(0f, 0.06f), 0.17f, 0.45f), deep);
                    break;
                }
                case PowerId.Frost:
                {
                    Func<Vector2, float> flake = p => FlakeSdf(p, 0.56f, 0.06f);
                    g.Outlined(flake, White, Line, O * 0.85f);
                    break;
                }
                case PowerId.Rage:
                {
                    Func<Vector2, float> flame = p => Mathf.Min(UnevenCapsule(p - new Vector2(0f, -0.3f), 0.34f, 0.03f, 0.78f),
                        Mathf.Min(UnevenCapsule(Rotate(p - new Vector2(-0.2f, -0.24f), 0.55f), 0.17f, 0.02f, 0.42f),
                                  UnevenCapsule(Rotate(p - new Vector2(0.22f, -0.26f), -0.6f), 0.15f, 0.02f, 0.36f)));
                    g.Outlined(flame, White, Line, O);
                    g.Fill(p => UnevenCapsule(p - new Vector2(0f, -0.36f), 0.19f, 0.02f, 0.46f), new Color(1f, 0.55f, 0.15f));
                    g.Fill(p => UnevenCapsule(p - new Vector2(0f, -0.4f), 0.1f, 0.01f, 0.24f), new Color(1f, 0.9f, 0.4f));
                    break;
                }
                case PowerId.Magnet:
                {
                    Func<Vector2, float> magnet = p =>
                    {
                        Vector2 q = p - new Vector2(0f, -0.02f);
                        float u = Mathf.Max(Ring(q, Vector2.zero, 0.32f, 0.13f), q.y);
                        float legs = Mathf.Min(Box(p, new Vector2(-0.32f, 0.2f), new Vector2(0.13f, 0.22f)), Box(p, new Vector2(0.32f, 0.2f), new Vector2(0.13f, 0.22f)));
                        return Mathf.Min(u, legs);
                    };
                    g.Outlined(magnet, White, Line, O);
                    g.Fill(p => Box(p, new Vector2(-0.32f, 0.33f), new Vector2(0.13f, 0.09f)), new Color(0.3f, 0.34f, 0.44f));
                    g.Fill(p => Box(p, new Vector2(0.32f, 0.33f), new Vector2(0.13f, 0.09f)), new Color(0.3f, 0.34f, 0.44f));
                    g.Fill(p => Mathf.Max(magnet(p) + 0.05f, -(p.y + 0.12f)), deep);
                    // little field sparks above the poles
                    g.Fill(p => Seg(p, new Vector2(-0.48f, 0.58f), new Vector2(-0.4f, 0.7f)) - 0.035f, White);
                    g.Fill(p => Seg(p, new Vector2(0.48f, 0.58f), new Vector2(0.4f, 0.7f)) - 0.035f, White);
                    g.Fill(p => Seg(p, new Vector2(0f, 0.56f), new Vector2(0f, 0.7f)) - 0.035f, White);
                    break;
                }
                case PowerId.Eagle:
                {
                    Func<Vector2, float> lens = p => Mathf.Max(Circle(p, new Vector2(0f, -0.5f), 0.74f), Circle(p, new Vector2(0f, 0.5f), 0.74f));
                    g.Outlined(lens, White, Line, O);
                    g.Fill(p => Circle(p, Vector2.zero, 0.23f), p => Color.Lerp(new Color(0.85f, 0.45f, 0.05f), new Color(1f, 0.82f, 0.25f), Mathf.Clamp01(p.y * 2f + 0.5f)));
                    g.Fill(p => Circle(p, Vector2.zero, 0.11f), Line);
                    g.Fill(p => Circle(p, new Vector2(0.07f, 0.07f), 0.045f), White);
                    break;
                }
            }
        }

        private static float FlakeSdf(Vector2 p, float arm, float w)
        {
            float d = Circle(p, Vector2.zero, w * 1.6f);
            for (int k = 0; k < 3; k++)
            {
                float ang = k * Mathf.PI / 3f + Mathf.PI / 2f;
                var dir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
                d = Mathf.Min(d, Seg(p, -dir * arm, dir * arm) - w);
                for (int s = -1; s <= 1; s += 2)
                {
                    Vector2 b = dir * (arm * 0.55f * s);
                    Vector2 bd = dir * s;
                    d = Mathf.Min(d, Seg(p, b, b + Rotate(bd, 0.8f) * arm * 0.32f) - w * 0.8f);
                    d = Mathf.Min(d, Seg(p, b, b + Rotate(bd, -0.8f) * arm * 0.32f) - w * 0.8f);
                }
            }
            return d;
        }

        // ------------------------------------------------------------------ white effect shapes

        private static void PaintFx(SdfCanvas g, int i)
        {
            switch ((Fx)i)
            {
                case Fx.Bubble:
                {
                    // soft-edged sphere: faint fill, bright rim, a hex web and a gloss spot
                    g.Fill(p => p.magnitude - 0.9f, new Color(1f, 1f, 1f, 0.12f));
                    g.Glow(p => Ring(p, Vector2.zero, 0.88f, 0.03f), new Color(1f, 1f, 1f, 0.6f), 0.08f);
                    g.Fill(p => Ring(p, Vector2.zero, 0.88f, 0.025f), Color.white);
                    for (int k = 0; k < 6; k++)
                    {
                        float a0 = k * Mathf.PI / 3f, a1 = (k + 1) * Mathf.PI / 3f;
                        Vector2 h0 = new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * 0.46f, h1 = new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * 0.46f;
                        g.Fill(p => Seg(p, h0, h1) - 0.016f, new Color(1f, 1f, 1f, 0.45f));
                        g.Fill(p => Seg(p, h0, h0 * 1.88f) - 0.016f, new Color(1f, 1f, 1f, 0.35f));
                    }
                    g.Fill(p => Ellipse(Rotate(p - new Vector2(-0.38f, 0.42f), 0.7f), new Vector2(0.24f, 0.1f)), new Color(1f, 1f, 1f, 0.55f));
                    break;
                }
                case Fx.Flake:
                    g.Fill(p => FlakeSdf(p, 0.82f, 0.08f), Color.white);
                    break;
                case Fx.Ice:
                {
                    // a faceted ice block: light faces, a darker side, white edges
                    Func<Vector2, float> block = p => Box(p, Vector2.zero, new Vector2(0.72f, 0.72f), 0.2f);
                    g.Fill(block, new Color(1f, 1f, 1f, 0.55f));
                    g.Fill(p => Mathf.Max(block(p), -(p.x + p.y) + 0.2f), new Color(1f, 1f, 1f, 0.35f));
                    g.Fill(p => Mathf.Abs(block(p)) - 0.03f, Color.white);
                    g.Fill(p => Seg(p, new Vector2(-0.5f, 0.25f), new Vector2(-0.15f, 0.55f)) - 0.035f, Color.white);
                    g.Fill(p => Seg(p, new Vector2(-0.5f, 0.02f), new Vector2(-0.35f, 0.15f)) - 0.025f, Color.white);
                    g.Fill(p => Seg(p, new Vector2(0.15f, -0.5f), new Vector2(0.5f, -0.2f)) - 0.02f, new Color(1f, 1f, 1f, 0.8f));
                    break;
                }
                case Fx.Foot:
                {
                    // one boot print: the ball of the foot and the heel
                    g.Fill(p => Ellipse(Rotate(p - new Vector2(0.02f, 0.28f), 0.12f), new Vector2(0.3f, 0.42f)), Color.white);
                    g.Fill(p => Ellipse(p - new Vector2(-0.04f, -0.5f), new Vector2(0.24f, 0.24f)), Color.white);
                    break;
                }
                case Fx.Bullet:
                {
                    Func<Vector2, float> b = p => Mathf.Min(Box(p, new Vector2(0f, -0.3f), new Vector2(0.22f, 0.42f), 0.05f),
                        UnevenCapsule(p - new Vector2(0f, 0.1f), 0.22f, 0.05f, 0.62f));
                    g.Fill(p => b(p) - 0.1f, Line);
                    g.Fill(b, Color.white);
                    g.Fill(p => Box(p, new Vector2(0f, -0.56f), new Vector2(0.22f, 0.06f)), new Color(0.75f, 0.75f, 0.78f));
                    break;
                }
                case Fx.Arrow:
                {
                    Func<Vector2, float> arrow = p => Mathf.Max(Triangle(p, new Vector2(0f, 0.85f), new Vector2(-0.6f, -0.7f), new Vector2(0.6f, -0.7f)),
                        -Triangle(p, new Vector2(-0.7f, -0.9f), new Vector2(0.7f, -0.9f), new Vector2(0f, -0.3f)));
                    g.Fill(p => arrow(p) - 0.12f, Line);
                    g.Fill(arrow, Color.white);
                    break;
                }
                case Fx.Arc:
                    g.Glow(p => Arc(p, 0.9f, 0.7f, 0.06f), new Color(1f, 1f, 1f, 0.6f), 0.15f);
                    g.Fill(p => Arc(p, 0.9f, 0.7f, 0.05f), Color.white);
                    break;
                case Fx.Twinkle:
                {
                    Func<Vector2, float> star = p =>
                    {
                        Vector2 q = new Vector2(Mathf.Abs(p.x), Mathf.Abs(p.y));
                        return Mathf.Min(Ellipse(q, new Vector2(0.14f, 0.9f)), Ellipse(q, new Vector2(0.9f, 0.14f)));
                    };
                    g.Glow(p => p.magnitude - 0.15f, new Color(1f, 1f, 1f, 0.7f), 0.45f);
                    g.Fill(star, Color.white);
                    break;
                }
            }
        }
    }
}
