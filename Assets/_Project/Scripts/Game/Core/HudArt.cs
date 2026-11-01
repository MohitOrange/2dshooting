using UnityEngine;
using static NeonBrawl.Sdf;

namespace NeonBrawl
{
    /// <summary>
    /// Action-button art in the style of the painted HUD sheet: a dark gunmetal disc with a glossy colored
    /// rim, plus clean white icons with a dark outline (no emoji). Painted in code the first time it is used.
    /// </summary>
    public static class HudArt
    {
        public enum Accent { Blue, Cyan, Green, Orange, Red, Steel }
        public enum Icon { Reload, Swap, Grenade, Dash, Knife, Chat, Back, Hand, Mic, Speaker, Person, Target }

        private static readonly Color[] Accents =
        {
            new Color(0.25f, 0.62f, 1f),   // blue
            new Color(0.2f, 0.85f, 0.85f), // cyan
            new Color(0.45f, 0.86f, 0.3f), // green
            new Color(1f, 0.58f, 0.16f),   // orange
            new Color(0.98f, 0.32f, 0.3f), // red
            new Color(0.72f, 0.78f, 0.86f) // steel
        };

        private static readonly Color Line = new Color(0.04f, 0.06f, 0.1f);
        private static readonly Color White = new Color(0.97f, 0.98f, 1f);

        private static Sprite[] _plates, _icons;

        public static Color AccentColor(Accent a) => Accents[(int)a];

        public static Sprite Plate(Accent a)
        {
            if (_plates == null) _plates = Sheet("NB_HudPlates", Accents.Length, 192, PaintPlate);
            return _plates[(int)a];
        }

        public static Sprite Get(Icon i)
        {
            if (_icons == null) _icons = Sheet("NB_HudIcons", (int)Icon.Target + 1, 128, PaintIcon);
            return _icons[(int)i];
        }

        // ------------------------------------------------------------------ plate

        private static void PaintPlate(SdfCanvas g, int accent)
        {
            Color a = Accents[accent];
            Color aDark = new Color(a.r * 0.45f, a.g * 0.45f, a.b * 0.45f);
            g.Fill(p => p.magnitude - 0.95f, new Color(0f, 0f, 0f, 0.35f), new Vector2(0f, -0.04f)); // drop shadow
            g.Fill(p => p.magnitude - 0.93f, new Color(0.03f, 0.05f, 0.08f));                       // outer rim
            // colored rim: bright on top, deep at the bottom
            g.Fill(p => p.magnitude - 0.88f, p => Color.Lerp(aDark, a, Mathf.Clamp01(p.y * 0.6f + 0.6f)));
            g.Fill(p => Ring(p, Vector2.zero, 0.86f, 0.012f), new Color(1f, 1f, 1f, 0.35f));        // rim highlight line
            g.Fill(p => p.magnitude - 0.74f, new Color(0.02f, 0.04f, 0.07f));                       // groove
            // gunmetal face, lit from the top
            g.Fill(p => p.magnitude - 0.70f, p =>
            {
                float t = Mathf.Clamp01(p.y * 0.55f + 0.55f);
                Color top = new Color(0.2f, 0.29f, 0.4f), bottom = new Color(0.07f, 0.11f, 0.17f);
                Color c = Color.Lerp(bottom, top, t);
                float edge = Mathf.Clamp01((p.magnitude - 0.45f) / 0.25f);
                return Color.Lerp(c, c * 0.7f, edge * 0.6f);
            });
            g.Fill(p => Ring(p, Vector2.zero, 0.70f, 0.012f), new Color(a.r, a.g, a.b, 0.55f));     // inner accent line
            g.Fill(p => Ellipse(p - new Vector2(0f, 0.42f), new Vector2(0.5f, 0.2f)), new Color(1f, 1f, 1f, 0.08f)); // gloss
            // four rivets on the rim
            for (int k = 0; k < 4; k++)
            {
                float ang = Mathf.PI * 0.25f + k * Mathf.PI * 0.5f;
                var c = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * 0.81f;
                g.Fill(p => Circle(p, c, 0.035f), new Color(0.05f, 0.07f, 0.1f, 0.9f));
                g.Fill(p => Circle(p, c + new Vector2(-0.008f, 0.008f), 0.02f), new Color(1f, 1f, 1f, 0.55f));
            }
        }

        // ------------------------------------------------------------------ icons

        private static void PaintIcon(SdfCanvas g, int icon)
        {
            const float o = 0.07f; // outline width
            switch ((Icon)icon)
            {
                case Icon.Reload:
                {
                    // circular arrow around a bullet
                    System.Func<Vector2, float> arc = p => Arc(Rotate(p, -Mathf.PI * 0.75f), 2.45f, 0.62f, 0.085f);
                    float b = 5f * Mathf.Deg2Rad;
                    Vector2 bc = new Vector2(Mathf.Cos(b), Mathf.Sin(b)) * 0.62f, tan = new Vector2(-Mathf.Sin(b), Mathf.Cos(b)), nrm = new Vector2(Mathf.Cos(b), Mathf.Sin(b));
                    Vector2 t0 = bc + tan * 0.34f, t1 = bc + nrm * 0.22f - tan * 0.02f, t2 = bc - nrm * 0.22f - tan * 0.02f;
                    System.Func<Vector2, float> head = p => Triangle(p, t0, t1, t2);
                    System.Func<Vector2, float> bullet = p => Mathf.Min(Box(p, new Vector2(0f, -0.12f), new Vector2(0.11f, 0.2f), 0.02f),
                        Mathf.Max(Ellipse(p - new Vector2(0f, 0.08f), new Vector2(0.11f, 0.24f)), -p.y + 0.08f));
                    g.Outlined(p => Mathf.Min(Mathf.Min(arc(p), head(p)), bullet(p)), White, Line, o);
                    g.Fill(p => Box(p, new Vector2(0f, -0.22f), new Vector2(0.12f, 0.03f)), new Color(0.55f, 0.6f, 0.7f));
                    break;
                }
                case Icon.Swap:
                {
                    System.Func<Vector2, float> top = p => Mathf.Min(Box(p, new Vector2(-0.12f, 0.27f), new Vector2(0.42f, 0.085f)),
                        Triangle(p, new Vector2(0.68f, 0.27f), new Vector2(0.28f, 0.52f), new Vector2(0.28f, 0.02f)));
                    System.Func<Vector2, float> bottom = p => top(new Vector2(-p.x, -p.y));
                    g.Outlined(p => Mathf.Min(top(p), bottom(p)), White, Line, o);
                    break;
                }
                case Icon.Grenade:
                {
                    System.Func<Vector2, float> body = p => Ellipse(p - new Vector2(0f, -0.18f), new Vector2(0.42f, 0.5f));
                    System.Func<Vector2, float> cap = p => Box(p, new Vector2(0f, 0.38f), new Vector2(0.17f, 0.11f), 0.03f);
                    System.Func<Vector2, float> lever = p => Seg(p, new Vector2(0.12f, 0.46f), new Vector2(0.44f, 0.08f)) - 0.065f;
                    System.Func<Vector2, float> pin = p => Ring(p, new Vector2(-0.3f, 0.5f), 0.13f, 0.045f);
                    g.Outlined(p => Mathf.Min(Mathf.Min(body(p), cap(p)), Mathf.Min(lever(p), pin(p))), White, Line, o);
                    var groove = new Color(0.35f, 0.42f, 0.5f);
                    g.Fill(p => Mathf.Max(Mathf.Abs(p.y + 0.18f) - 0.025f, body(p) + 0.06f), groove);
                    g.Fill(p => Mathf.Max(Mathf.Abs(p.x) - 0.025f, body(p) + 0.06f), groove);
                    break;
                }
                case Icon.Dash:
                {
                    System.Func<Vector2, float> c1 = p => Mathf.Min(Seg(p, new Vector2(-0.3f, 0.45f), new Vector2(0.05f, 0f)), Seg(p, new Vector2(0.05f, 0f), new Vector2(-0.3f, -0.45f))) - 0.1f;
                    System.Func<Vector2, float> c2 = p => c1(p - new Vector2(0.38f, 0f));
                    System.Func<Vector2, float> speed = p => Mathf.Min(Mathf.Min(Seg(p, new Vector2(-0.85f, 0.26f), new Vector2(-0.6f, 0.26f)),
                        Seg(p, new Vector2(-0.9f, 0f), new Vector2(-0.52f, 0f))), Seg(p, new Vector2(-0.85f, -0.26f), new Vector2(-0.6f, -0.26f))) - 0.055f;
                    g.Outlined(p => Mathf.Min(Mathf.Min(c1(p), c2(p)), speed(p)), White, Line, o);
                    break;
                }
                case Icon.Knife:
                {
                    // combat knife pointing up-right
                    System.Func<Vector2, float> shape = q =>
                    {
                        Vector2 p = Rotate(q, -Mathf.PI * 0.25f);
                        float blade = Mathf.Min(Box(p, new Vector2(0.22f, 0.02f), new Vector2(0.3f, 0.13f)),
                            Triangle(p, new Vector2(0.52f, 0.15f), new Vector2(0.52f, -0.11f), new Vector2(0.9f, 0.12f)));
                        float guard = Box(p, new Vector2(-0.12f, 0f), new Vector2(0.05f, 0.22f), 0.02f);
                        float handle = Box(p, new Vector2(-0.45f, 0f), new Vector2(0.28f, 0.1f), 0.07f);
                        return Mathf.Min(blade, Mathf.Min(guard, handle));
                    };
                    g.Outlined(shape, White, Line, o);
                    g.Fill(q =>
                    {
                        Vector2 p = Rotate(q, -Mathf.PI * 0.25f);
                        float grip = Mathf.Abs(Mathf.Repeat(p.x + 0.45f, 0.14f) - 0.07f) - 0.02f;
                        return Mathf.Max(grip, Box(p, new Vector2(-0.45f, 0f), new Vector2(0.22f, 0.06f)));
                    }, new Color(0.4f, 0.46f, 0.55f));
                    g.Fill(q => Seg(Rotate(q, -Mathf.PI * 0.25f), new Vector2(0f, 0.06f), new Vector2(0.62f, 0.08f)) - 0.02f, new Color(0.72f, 0.8f, 0.9f));
                    break;
                }
                case Icon.Chat:
                {
                    System.Func<Vector2, float> bubble = p => Mathf.Min(Box(p, new Vector2(0f, 0.08f), new Vector2(0.72f, 0.45f), 0.26f),
                        Triangle(p, new Vector2(-0.42f, -0.25f), new Vector2(-0.08f, -0.25f), new Vector2(-0.5f, -0.68f)));
                    g.Outlined(bubble, White, Line, o);
                    for (int k = -1; k <= 1; k++)
                    {
                        float x = k * 0.3f;
                        g.Fill(p => Circle(p, new Vector2(x, 0.08f), 0.1f), Line);
                    }
                    break;
                }
                case Icon.Back:
                {
                    System.Func<Vector2, float> arrow = p => Mathf.Min(Box(p, new Vector2(0.12f, 0f), new Vector2(0.48f, 0.12f), 0.04f),
                        Triangle(p, new Vector2(-0.72f, 0f), new Vector2(-0.18f, 0.5f), new Vector2(-0.18f, -0.5f)));
                    g.Outlined(arrow, White, Line, o);
                    break;
                }
                case Icon.Mic:
                {
                    System.Func<Vector2, float> mic = p => Mathf.Min(Mathf.Min(
                        Box(p, new Vector2(0f, 0.28f), new Vector2(0.22f, 0.42f), 0.22f),                       // capsule
                        Mathf.Max(Arc(new Vector2(p.x, -(p.y - 0.18f)), 1.45f, 0.4f, 0.07f), p.y - 0.3f)),       // holder
                        Mathf.Min(Box(p, new Vector2(0f, -0.38f), new Vector2(0.06f, 0.16f)), Box(p, new Vector2(0f, -0.6f), new Vector2(0.3f, 0.07f), 0.04f)));
                    g.Outlined(mic, White, Line, o);
                    g.Fill(p => Mathf.Max(Mathf.Abs(Mathf.Repeat(p.y - 0.06f, 0.14f) - 0.07f) - 0.022f, Box(p, new Vector2(0f, 0.34f), new Vector2(0.13f, 0.28f), 0.12f)), new Color(0.55f, 0.6f, 0.7f));
                    break;
                }
                case Icon.Speaker:
                {
                    System.Func<Vector2, float> spk = p => Mathf.Min(Box(p, new Vector2(-0.48f, 0f), new Vector2(0.16f, 0.2f), 0.04f),
                        Trapezoid(Rotate(p - new Vector2(-0.16f, 0f), Mathf.PI * 0.5f), Vector2.zero, 0.2f, 0.44f, 0.2f));
                    System.Func<Vector2, float> waves = p => Mathf.Min(
                        Arc(Rotate(p - new Vector2(0.02f, 0f), Mathf.PI * 0.5f), 0.7f, 0.36f, 0.055f),
                        Arc(Rotate(p - new Vector2(0.02f, 0f), Mathf.PI * 0.5f), 0.75f, 0.62f, 0.055f));
                    g.Outlined(p => Mathf.Min(spk(p), waves(p)), White, Line, o);
                    break;
                }
                case Icon.Target:
                {
                    // gun sight: ring, four ticks and a center dot (auto shoot)
                    System.Func<Vector2, float> sight = p => Mathf.Min(Mathf.Min(
                        Ring(p, Vector2.zero, 0.46f, 0.075f), Circle(p, Vector2.zero, 0.12f)),
                        Mathf.Min(Mathf.Min(Box(p, new Vector2(0f, 0.66f), new Vector2(0.075f, 0.2f), 0.03f), Box(p, new Vector2(0f, -0.66f), new Vector2(0.075f, 0.2f), 0.03f)),
                                  Mathf.Min(Box(p, new Vector2(0.66f, 0f), new Vector2(0.2f, 0.075f), 0.03f), Box(p, new Vector2(-0.66f, 0f), new Vector2(0.2f, 0.075f), 0.03f))));
                    g.Outlined(sight, White, Line, o);
                    break;
                }
                case Icon.Person:
                {
                    System.Func<Vector2, float> person = p => Mathf.Min(Circle(p, new Vector2(0f, 0.36f), 0.27f),
                        Mathf.Max(Box(p, new Vector2(0f, -0.42f), new Vector2(0.56f, 0.4f), 0.32f), p.y + 0.02f));
                    g.Outlined(person, White, Line, o);
                    break;
                }
                case Icon.Hand:
                {
                    // open hand (pick up)
                    System.Func<Vector2, float> palm = p => Box(p, new Vector2(0f, -0.25f), new Vector2(0.32f, 0.3f), 0.16f);
                    System.Func<Vector2, float> fingers = p =>
                    {
                        float d = 9f;
                        for (int k = 0; k < 4; k++)
                        {
                            float x = -0.24f + k * 0.16f, top = k == 0 || k == 3 ? 0.42f : 0.55f;
                            d = Mathf.Min(d, Seg(p, new Vector2(x, -0.1f), new Vector2(x, top)) - 0.075f);
                        }
                        return Mathf.Min(d, Seg(p, new Vector2(-0.3f, -0.25f), new Vector2(-0.58f, 0.05f)) - 0.08f);
                    };
                    g.Outlined(p => Mathf.Min(palm(p), fingers(p)), White, Line, o);
                    break;
                }
            }
        }
    }
}
