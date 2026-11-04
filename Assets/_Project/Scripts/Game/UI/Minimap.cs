using UnityEngine;
using UnityEngine.UI;

namespace NeonBrawl
{
    /// <summary>
    /// Round minimap in the top-left corner, north up, centred on you. The floor plan is painted once per
    /// match from the map grid (walls, water, cover). On top: your arrow, team mates, enemies you can see,
    /// power orbs, and the two "hearing" cues: FOOTPRINTS where an unseen enemy walks near you and a
    /// BULLET where any enemy fires (pinned to the rim when the shot is off the map window).
    /// EAGLE EYE adds a radar sweep and shows every enemy.
    /// Perf: one texture per match, pooled icons, no allocations per frame.
    /// </summary>
    public sealed class Minimap : MonoBehaviour
    {
        private const float Size = 250f;
        private const float ViewRadius = 15f;   // world units from you to the rim
        private const float HearSteps = 12f;    // footsteps heard this far away
        private const float HearShots = 34f;    // gunfire heard this far away
        private const int Dots = 12, Prints = 28, Shots = 10, Orbs = 6;

        private sealed class Fading
        {
            public Image Img;
            public float Life, MaxLife;
        }

        private GameContext _ctx;
        private MatchController _match;
        private RawImage _map;
        private Texture2D _tex;
        private RectTransform _icons, _sweep;
        private Image _me, _rim;
        private readonly Image[] _dots = new Image[Dots];
        private readonly Image[] _orbs = new Image[Orbs];
        private readonly Fading[] _prints = new Fading[Prints];
        private readonly Fading[] _shots = new Fading[Shots];
        private readonly float[] _stepT = new float[16];
        private readonly float[] _shotT = new float[16];
        private readonly bool[] _leftFoot = new bool[16];
        private int _printNext, _shotNext;
        private float _ppu;

        public static Minimap Build(RectTransform safe, GameContext ctx)
        {
            var art = ctx.Art;
            var root = UIKit.Rect(safe, "Minimap");
            UIKit.Place(root, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(22f, -148f), new Vector2(Size, Size));
            var m = root.gameObject.AddComponent<Minimap>();
            m._ctx = ctx;
            m._ppu = Size / (ViewRadius * 2f);

            var shadow = UIKit.Image(root, "Shadow", art.Glow, new Color(0f, 0f, 0f, 0.55f));
            UIKit.Place(shadow.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -6f), new Vector2(Size * 1.25f, Size * 1.25f));
            var back = UIKit.Image(root, "Back", art.Circle, new Color(0.03f, 0.05f, 0.09f, 0.94f));
            UIKit.Stretch(back.rectTransform, -6f);

            // everything inside is clipped to the circle
            var maskImg = UIKit.Image(root, "Mask", art.Circle, Color.white);
            UIKit.Stretch(maskImg.rectTransform, 4f);
            maskImg.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            m._map = new GameObject("Map", typeof(RectTransform)).AddComponent<RawImage>();
            m._map.transform.SetParent(maskImg.transform, false);
            UIKit.Stretch(m._map.rectTransform);
            m._map.raycastTarget = false;
            m._map.color = new Color(1f, 1f, 1f, 0.92f);

            // range rings
            var ring1 = UIKit.Image(maskImg.transform, "Ring1", art.ThinRing, new Color(1f, 1f, 1f, 0.12f));
            UIKit.Place(ring1.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(HearSteps * 2f * m._ppu, HearSteps * 2f * m._ppu));

            m._sweep = UIKit.Rect(maskImg.transform, "Sweep");
            UIKit.Place(m._sweep, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(Size, Size));
            var sweepImg = UIKit.Image(m._sweep, "Beam", art.Glow, new Color(1f, 0.9f, 0.35f, 0.35f));
            UIKit.Place(sweepImg.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(Size * 0.55f, 60f));
            m._sweep.gameObject.SetActive(false);

            m._icons = UIKit.Rect(maskImg.transform, "Icons");
            UIKit.Stretch(m._icons);
            for (int i = 0; i < Orbs; i++) m._orbs[i] = Icon(m._icons, "Orb", null, 24f);
            for (int i = 0; i < Prints; i++) m._prints[i] = new Fading { Img = Icon(m._icons, "Step", PowerArt.Get(PowerArt.Fx.Foot), 17f) };
            for (int i = 0; i < Dots; i++) m._dots[i] = Icon(m._icons, "Dot", art.Circle, 15f);
            for (int i = 0; i < Shots; i++) m._shots[i] = new Fading { Img = Icon(m._icons, "Shot", PowerArt.Get(PowerArt.Fx.Bullet), 26f) };
            m._me = Icon(m._icons, "Me", PowerArt.Get(PowerArt.Fx.Arrow), 26f);
            m._me.color = new Color(1f, 0.92f, 0.35f);
            m._me.enabled = true;

            // rim with a colored accent
            m._rim = UIKit.Image(root, "Rim", art.Ring, new Color(0.62f, 0.78f, 1f, 0.85f));
            UIKit.Stretch(m._rim.rectTransform, -4f);
            var n = UIKit.Label(root, "N", "N", 22, Color.white, TextAnchor.MiddleCenter);
            UIKit.Place(n.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -2f), new Vector2(30f, 26f));
            return m;
        }

        private static Image Icon(Transform parent, string name, Sprite s, float size)
        {
            var img = UIKit.Image(parent, name, s, Color.white);
            UIKit.Place(img.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size, size));
            img.enabled = false;
            img.preserveAspect = true;
            return img;
        }

        public void Bind(MatchController match)
        {
            _match = match;
            bool show = match != null && !match.Spectator && _ctx.Prefs.Minimap;
            gameObject.SetActive(show);
            if (!show) return;
            Paint(match.Map);
            for (int i = 0; i < Prints; i++) { _prints[i].Life = 0f; _prints[i].Img.enabled = false; }
            for (int i = 0; i < Shots; i++) { _shots[i].Life = 0f; _shots[i].Img.enabled = false; }
            for (int i = 0; i < _stepT.Length; i++) _stepT[i] = _shotT[i] = 0f;
        }

        /// <summary>The floor plan: 4 pixels per cell, theme floor, dark walls, blue water, cover in between.</summary>
        private void Paint(MapData map)
        {
            const int k = 4;
            int w = map.Width * k + 2, h = map.Height * k + 2;
            if (_tex == null || _tex.width != w || _tex.height != h)
            {
                if (_tex != null) Destroy(_tex);
                _tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, name = "NB_Minimap" };
            }
            Color floor = FloorColor(map.Theme);
            Color wall = new Color(0.12f, 0.13f, 0.17f);
            Color water = new Color(0.2f, 0.5f, 0.85f);
            Color cover = Color.Lerp(floor, wall, 0.55f);
            Color edge = new Color(0.03f, 0.05f, 0.09f);
            var px = new Color32[w * h];
            for (int i = 0; i < px.Length; i++) px[i] = edge;
            for (int y = 0; y < map.Height; y++)
                for (int x = 0; x < map.Width; x++)
                {
                    char c = map.At(x, y);
                    Color col = c == '#' ? wall : c == '~' ? water : MapData.IsBlocking(c) ? cover : floor;
                    // a little checker so open floor reads as ground, not a flat sheet
                    if (col == floor && ((x + y) & 1) == 0) col = Color.Lerp(floor, Color.white, 0.05f);
                    for (int yy = 0; yy < k; yy++)
                        for (int xx = 0; xx < k; xx++)
                            px[(1 + y * k + yy) * w + 1 + x * k + xx] = col;
                }
            _tex.SetPixels32(px);
            _tex.Apply(false);
            _map.texture = _tex;
        }

        private static Color FloorColor(int theme)
        {
            switch (theme)
            {
                case 0: return new Color(0.3f, 0.48f, 0.26f);   // grass
                case 1: return new Color(0.66f, 0.52f, 0.33f);  // sand
                case 2: return new Color(0.7f, 0.78f, 0.86f);   // snow
                case 3: return new Color(0.38f, 0.4f, 0.44f);   // steel
                default: return new Color(0.27f, 0.42f, 0.24f); // forest
            }
        }

        private void OnDestroy()
        {
            if (_tex != null) Destroy(_tex);
        }

        private void LateUpdate()
        {
            if (_match == null || _tex == null) return;
            var me = _match.Local;
            if (me == null) return;
            var map = _match.Map;
            float dt = Time.deltaTime, t = Time.time;
            Vector2 c = me.Position;

            // the window onto the floor plan (texture has a 1 px border and 4 px per cell)
            float tw = _tex.width, th = _tex.height;
            float u = (1f + (c.x - ViewRadius) * 4f) / tw, v = (1f + (c.y - ViewRadius) * 4f) / th;
            _map.uvRect = new Rect(u, v, ViewRadius * 8f / tw, ViewRadius * 8f / th);

            var aim = me.AimDir;
            _me.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg - 90f);
            _me.color = me.Alive ? new Color(1f, 0.92f, 0.35f) : new Color(1f, 1f, 1f, 0.35f);

            bool eagle = me.Alive && me.Powers.Eagle;
            if (_sweep.gameObject.activeSelf != eagle) _sweep.gameObject.SetActive(eagle);
            if (eagle) _sweep.localRotation = Quaternion.Euler(0f, 0f, -t * 240f);
            _rim.color = eagle ? new Color(1f, 0.85f, 0.3f, 0.95f) : new Color(0.62f, 0.78f, 1f, 0.85f);

            // fighters
            var all = _match.Fighters;
            int dot = 0;
            for (int i = 0; i < all.Count; i++)
            {
                var f = all[i];
                if (f == me || !f.Alive) continue;
                bool enemy = _match.AreEnemies(me, f);
                Vector2 rel = f.Position - c;
                float d = rel.magnitude;
                int slot = f.Id & 15;

                if (enemy && !f.Powers.Ghost)
                {
                    // GUNFIRE: a bullet where they shoot from (on the rim if beyond the window)
                    if (t - f.LastShotTime < 0.2f && d < HearShots && t >= _shotT[slot])
                    {
                        _shotT[slot] = t + 0.45f;
                        var s = _shots[_shotNext];
                        _shotNext = (_shotNext + 1) % Shots;
                        Vector2 p = rel * _ppu;
                        float max = Size * 0.5f - 16f;
                        if (p.magnitude > max) p = p.normalized * max;
                        s.Img.rectTransform.anchoredPosition = p;
                        s.Img.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(f.AimDir.y, f.AimDir.x) * Mathf.Rad2Deg - 90f);
                        s.Life = s.MaxLife = 1.1f;
                        s.Img.enabled = true;
                    }
                    // FOOTSTEPS: unseen enemies walking close by leave prints
                    bool seenNow = eagle || f.Visual.Alpha > 0.5f;
                    if (!seenNow && d < HearSteps && t - f.LastStepTime < 0.1f && t >= _stepT[slot])
                    {
                        _stepT[slot] = t + 0.3f;
                        var pr = _prints[_printNext];
                        _printNext = (_printNext + 1) % Prints;
                        Vector2 vel = f.Velocity;
                        float ang = vel.sqrMagnitude > 0.01f ? Mathf.Atan2(vel.y, vel.x) * Mathf.Rad2Deg - 90f : 0f;
                        _leftFoot[slot] = !_leftFoot[slot];
                        Vector2 side = vel.sqrMagnitude > 0.01f ? new Vector2(-vel.y, vel.x).normalized * (_leftFoot[slot] ? 3f : -3f) : Vector2.zero;
                        pr.Img.rectTransform.anchoredPosition = rel * _ppu + side;
                        pr.Img.rectTransform.localRotation = Quaternion.Euler(0f, 0f, ang);
                        pr.Img.rectTransform.localScale = new Vector3(_leftFoot[slot] ? -1f : 1f, 1f, 1f);
                        pr.Life = pr.MaxLife = 1.6f;
                        pr.Img.enabled = true;
                    }
                }

                // dots: team mates always, enemies only while you can see them (or EAGLE EYE)
                bool show = !enemy || (!f.Powers.Ghost && (eagle || f.Visual.Alpha > 0.5f));
                if (!show || d > ViewRadius + 0.5f || dot >= Dots) continue;
                var img = _dots[dot++];
                img.enabled = true;
                img.rectTransform.anchoredPosition = rel * _ppu;
                img.color = enemy ? new Color(1f, 0.28f, 0.24f) : (f.Team == 0 ? MatchController.TeamRed : MatchController.TeamBlue);
                float pulse = enemy ? 15f + Mathf.Sin(t * 10f) * 2f : 14f;
                img.rectTransform.sizeDelta = new Vector2(pulse, pulse);
            }
            for (int i = dot; i < Dots; i++) if (_dots[i].enabled) _dots[i].enabled = false;

            // power orbs
            var orbs = _match.PowerUps.Orbs;
            int o = 0;
            for (int i = 0; i < orbs.Count && o < Orbs; i++)
            {
                Vector2 rel = orbs[i].Pos - c;
                if (rel.magnitude > ViewRadius) continue;
                var img = _orbs[o++];
                img.enabled = true;
                img.sprite = PowerArt.Badge(orbs[i].Type);
                img.rectTransform.anchoredPosition = rel * _ppu;
                float s = 22f + Mathf.Sin(t * 5f + i) * 3f;
                img.rectTransform.sizeDelta = new Vector2(s, s);
            }
            for (int i = o; i < Orbs; i++) if (_orbs[i].enabled) _orbs[i].enabled = false;

            Fade(_prints, dt, new Color(1f, 0.55f, 0.45f));
            Fade(_shots, dt, new Color(1f, 0.85f, 0.3f));
        }

        private static void Fade(Fading[] items, float dt, Color color)
        {
            for (int i = 0; i < items.Length; i++)
            {
                var it = items[i];
                if (it.Life <= 0f) continue;
                it.Life -= dt;
                if (it.Life <= 0f) { it.Img.enabled = false; continue; }
                float k = it.Life / it.MaxLife;
                color.a = Mathf.Clamp01(k * 1.6f);
                it.Img.color = color;
                // gunfire pops in big, then settles
                if (it.MaxLife < 1.5f)
                {
                    float s = 1f + Mathf.Clamp01((k - 0.75f) * 4f) * 0.8f;
                    var rt = it.Img.rectTransform;
                    rt.localScale = new Vector3(s, s, 1f);
                }
            }
        }
    }
}
