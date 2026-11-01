using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;
using UnityEngine.Tilemaps;

namespace NeonBrawl
{
    /// <summary>
    /// The menu background: a night camp built from the forest art (T5/P5 + shared props) — a crackling
    /// campfire with real light, embers and smoke, your soldier standing by the fire, squad mates on
    /// stumps looking around, fireflies, a river, tents and trees. The camera holds with a slow drift.
    /// Everything answers a tap: the fire flares up, your soldier cocks his rifle and calls out, mates turn
    /// and talk back, props wobble with a thump, the ground puffs dust and your soldier looks there.
    /// </summary>
    public sealed class LobbyScene : MonoBehaviour
    {
        private const int Forest = 4, Green = 0, Desert = 1, Steel = 3;
        public static readonly Vector2 FirePos = Vector2.zero;
        public static readonly Vector2 HeroPos = new Vector2(-2.2f, 0.15f);
        public static readonly Vector2 CameraCenter = new Vector2(2.4f, 0.6f);
        public const float CameraSize = 5.2f;

        private sealed class Mate
        {
            public FighterVisual Visual;
            public Vector2 Pos;
            public float Aim, TargetAim, Next;
        }

        private sealed class Firefly
        {
            public Transform T;
            public SpriteRenderer R;
            public Vector2 Home;
            public float Phase, Speed;
        }

        /// <summary>A prop that wobbles when tapped.</summary>
        private sealed class Poke
        {
            public Transform T;
            public Vector2 Pos;
            public float Radius, Wobble;
            public Vector3 Scale;
        }

        private static readonly string[] HeroLines =
            { "LET'S GO!", "READY WHEN YOU ARE!", "SQUAD UP!", "TO THE ARENA!", "NEW LOOK? TRY CUSTOMIZE!", "I'VE GOT YOUR BACK!", "LOCKED AND LOADED!" };
        private static readonly string[] MateLines =
            { "HEY!", "WATCH IT!", "SHHH...", "NICE NIGHT.", "ONE MORE ROUND?", "ZZZ...", "GG!", "WHO'S HUNGRY?", "LOOKING SHARP!" };

        private GameContext _ctx;
        private Light2D _fireLight;
        private SpriteRenderer[] _flames;
        private SpriteRenderer _groundGlow;
        private FighterVisual _hero;
        private float _heroAim, _heroTarget, _heroNext;
        private readonly List<Mate> _mates = new List<Mate>(3);
        private readonly List<Firefly> _flies = new List<Firefly>(16);
        private Tilemap _water;
        private float _emberT, _smokeT;
        private Transform _heroRoot;
        private bool _introPlayed;
        private readonly List<Poke> _pokes = new List<Poke>(32);
        private float _flare, _heroHop, _crackleT = 1f, _chatterT = 12f;
        private int _lastLine = -1;

        public Vector2 HeroWorld => HeroPos;

        public static LobbyScene Create(GameContext ctx)
        {
            var go = new GameObject("LobbyScene");
            var s = go.AddComponent<LobbyScene>();
            s._ctx = ctx;
            s.BuildWorld();
            return s;
        }

        private MapTheme Theme(int i) => _ctx.Catalog != null ? _ctx.Catalog.Theme(i) : null;

        private Sprite Prop(int theme, int index) => Theme(theme) != null ? ArtCatalog.At(Theme(theme).Props, index) : null;

        private Sprite Shared(int index) => _ctx.Catalog != null ? ArtCatalog.At(_ctx.Catalog.Shared, index) : null;

        // ------------------------------------------------------------------ build

        private void BuildWorld()
        {
            var art = _ctx.Art;
            var forest = Theme(Forest);
            var green = Theme(Green);
            gameObject.AddComponent<Grid>();

            // ground: grass, a cobble trail, a river at the top (the clearing is soft decals below)
            var ground = Tilemap("Ground", SortOrder.Ground, art.Lit);
            Tile grass = Tile(forest != null ? forest.Tiles[0] : art.SandTiles[0], Color.white);
            Tile planks = Tile(forest != null ? forest.Tiles[3] : art.IndoorTile, Color.white);
            Tile water = Tile(forest != null ? forest.Tiles[2] : art.Square, forest != null ? Color.white : new Color(0.2f, 0.45f, 0.75f));
            _water = Tilemap("Water", SortOrder.Ground + 1, art.Lit);
            for (int y = -10; y <= 10; y++)
                for (int x = -16; x <= 20; x++)
                {
                    var p = new Vector3Int(x, y, 0);
                    bool river = y >= 8 + (int)(Mathf.Sin(x * 0.35f) * 1.2f) && y <= 9 + (int)(Mathf.Sin(x * 0.35f) * 1.2f);
                    bool bridge = river && x >= 9 && x <= 10;
                    if (river && !bridge)
                    {
                        _water.SetTile(p, water);
                        continue;
                    }
                    ground.SetTile(p, bridge ? planks : grass);
                }

            var props = new GameObject("Props").transform;
            props.SetParent(transform, false);

            // soft trampled-earth clearing around the fire (blended decals, no hard tile edges)
            var earth = new Color(0.62f, 0.45f, 0.28f, 1f);
            MakeSprite(props, "Clearing", art.Glow, art.Lit, FirePos, new Vector2(7.5f, 6.2f), 0f, earth, SortOrder.GroundDetail - 1);
            for (int i = 0; i < 7; i++)
            {
                float a = i / 7f * Mathf.PI * 2f;
                MakeSprite(props, "ClearingEdge", art.Glow, art.Lit, FirePos + new Vector2(Mathf.Cos(a) * 2.3f, Mathf.Sin(a) * 1.8f), new Vector2(3.2f, 2.6f), i * 40f,
                    new Color(earth.r, earth.g, earth.b, 0.55f), SortOrder.GroundDetail - 1);
            }

            // tents, trees, totem, fences, crates, lamp
            Place(props, Prop(Forest, 2), new Vector2(-6.2f, 2.6f), 2.3f, SortOrder.Props);          // moss tent
            Place(props, Prop(Desert, 2), new Vector2(6.2f, 3.2f), 2.2f, SortOrder.Props);           // canvas tent
            Place(props, Prop(Forest, 6), new Vector2(-9.5f, 0.2f), 1.9f, SortOrder.Props);          // totem
            Place(props, Prop(Steel, 7), new Vector2(4.2f, 2.4f), 1.5f, SortOrder.Props);            // lamp post
            Place(props, Shared(ArtIds.SharedCrate), new Vector2(8.2f, 0.9f), 0.95f, SortOrder.Props);
            Place(props, Shared(ArtIds.SharedCrate2), new Vector2(9.1f, 1.3f), 0.85f, SortOrder.Props - 1);
            Place(props, Shared(ArtIds.SharedBarrelGrey), new Vector2(7.4f, 0.1f), 0.85f, SortOrder.Props);
            Place(props, Shared(ArtIds.SharedSandbags), new Vector2(-4.6f, -3.6f), 1.2f, SortOrder.Props);
            Place(props, Prop(Green, 4), new Vector2(-7.3f, -1.6f), 0.95f, SortOrder.Props);
            // supplies in the open middle of the screen
            Place(props, Prop(Steel, 0), new Vector2(0.8f, -3.3f), 0.9f, SortOrder.Props);
            Place(props, Shared(ArtIds.SharedBarrelBlue), new Vector2(1.75f, -3.6f), 0.75f, SortOrder.Props);
            Place(props, Shared(ArtIds.SharedCone), new Vector2(4.5f, -3.7f), 0.55f, SortOrder.Props);
            Place(props, Shared(ArtIds.SharedTire), new Vector2(3.5f, -3.1f), 0.8f, SortOrder.Props);
            Place(props, Prop(Green, 0), new Vector2(-4.6f, 2.4f), 1.3f, SortOrder.Props);
            for (int i = 0; i < 7; i++)
                Place(props, Prop(Forest, i % 2 == 0 ? 8 : 5), new Vector2(-12f + i * 1.15f, -6.4f), 1.15f, SortOrder.Props);
            Vector2[] trees =
            {
                new Vector2(-10.5f, 4.2f), new Vector2(-3.4f, 5.2f), new Vector2(12.2f, 4.6f), new Vector2(14.6f, 0.4f),
                new Vector2(-12.6f, -3.2f), new Vector2(11.5f, -5.2f), new Vector2(15.5f, -3.8f), new Vector2(-1f, -7.6f), new Vector2(5.5f, -7.4f)
            };
            for (int i = 0; i < trees.Length; i++)
                Place(props, Prop(Forest, i % 3 == 1 ? 1 : 0), trees[i], 2.4f + (i % 3) * 0.3f, SortOrder.Canopy);

            // campfire: stone ring (pebbles), logs, painted flames, ground glow, real light
            for (int i = 0; i < 9; i++)
            {
                float a = i / 9f * Mathf.PI * 2f;
                var stone = MakeSprite(props, "Stone", art.Circle, art.Lit, FirePos + new Vector2(Mathf.Cos(a), Mathf.Sin(a) * 0.8f) * 0.62f,
                    new Vector2(0.24f, 0.2f), 0f, new Color(0.45f, 0.44f, 0.42f), SortOrder.Props);
                stone.transform.localScale *= 0.9f + (i % 3) * 0.12f;
            }
            for (int i = 0; i < 4; i++)
                MakeSprite(props, "Log", art.RoundRect, art.Lit, FirePos, new Vector2(0.95f, 0.18f), i * 45f + 20f, new Color(0.35f, 0.22f, 0.12f), SortOrder.Props + 1);
            _groundGlow = MakeSprite(props, "FireGlow", art.Glow, art.Additive, FirePos, new Vector2(4.5f, 3.8f), 0f, new Color(1f, 0.5f, 0.15f, 0.3f), SortOrder.GroundDetail);
            var flameArt = _ctx.Catalog != null ? ArtCatalog.At(_ctx.Catalog.Fx2, ArtIds.FireBurst) : null;
            var starArt = _ctx.Catalog != null ? ArtCatalog.At(_ctx.Catalog.Fx1, ArtIds.StarOrange) : null;
            _flames = new SpriteRenderer[3];
            _flames[0] = MakeSprite(props, "Flame", flameArt != null ? flameArt : art.Glow, art.Additive, FirePos + new Vector2(0f, 0.35f), new Vector2(1.5f, 1.5f), 90f, Color.white, SortOrder.Fx);
            _flames[1] = MakeSprite(props, "Flame2", flameArt != null ? flameArt : art.Glow, art.Additive, FirePos + new Vector2(0.1f, 0.5f), new Vector2(1.1f, 1.1f), 100f, new Color(1f, 0.9f, 0.6f), SortOrder.Fx + 1);
            _flames[2] = MakeSprite(props, "Core", starArt != null ? starArt : art.Glow, art.Additive, FirePos + new Vector2(0f, 0.2f), new Vector2(0.9f, 0.9f), 0f, new Color(1f, 0.85f, 0.5f, 0.8f), SortOrder.Fx + 2);
            _fireLight = Lights.Point(transform, FirePos + new Vector2(0f, 0.3f), new Color(1f, 0.6f, 0.28f), 6.5f, 1.3f);
            _fireLight.pointLightInnerRadius = 0.6f;
            Lights.Point(transform, new Vector2(4.2f, 3.1f), new Color(1f, 0.85f, 0.6f), 3.2f, 0.9f);
            MakeSprite(props, "LampGlow", art.Glow, art.Additive, new Vector2(4.2f, 3.1f), new Vector2(1.4f, 1.4f), 0f, new Color(1f, 0.85f, 0.5f, 0.45f), SortOrder.Fx);
            Lights.Point(transform, new Vector2(-6.2f, 2.2f), new Color(0.5f, 1f, 0.6f), 3f, 0.6f);

            // squad mates on stumps around the fire
            Vector2[] seats = { new Vector2(-0.4f, 1.95f), new Vector2(2.05f, 0.55f), new Vector2(1.1f, -1.75f) };
            var rng = new System.Random(System.Environment.TickCount);
            for (int i = 0; i < seats.Length; i++)
            {
                Place(props, Prop(Forest, i % 2 == 0 ? 4 : 3), seats[i] + new Vector2(0f, -0.15f), 0.9f, SortOrder.Props);
                var look = new FighterLook
                {
                    Outfit = PlayerProfile.OutfitColors[rng.Next(PlayerProfile.OutfitColors.Length)],
                    Skin = PlayerProfile.SkinTints[rng.Next(PlayerProfile.SkinTints.Length)],
                    Body = rng.Next(8),
                    Head = rng.Next(3) == 0 ? 0 : 1 + rng.Next(15),
                    HairStyle = 1 + rng.Next(8),
                    Back = rng.Next(2) == 0 ? 1 + rng.Next(3) : 0
                };
                var v = MakeFighter("Mate" + i, look, seats[i], i == 1 ? WeaponId.Sniper : i == 2 ? WeaponId.Shotgun : WeaponId.Smg);
                float toFire = Angle(FirePos - seats[i]);
                _mates.Add(new Mate { Visual = v, Pos = seats[i], Aim = toFire, TargetAim = toFire, Next = Random.Range(2f, 5f) });
            }

            // fireflies
            for (int i = 0; i < 16; i++)
            {
                var home = new Vector2(Random.Range(-11f, 14f), Random.Range(-6f, 5f));
                if ((home - FirePos).sqrMagnitude < 9f) home += new Vector2(5f, 2f);
                var r = MakeSprite(props, "Firefly", art.Glow, art.Additive, home, new Vector2(0.28f, 0.28f), 0f, new Color(0.75f, 1f, 0.4f, 0.8f), SortOrder.Canopy + 1);
                _flies.Add(new Firefly { T = r.transform, R = r, Home = home, Phase = Random.value * 10f, Speed = Random.Range(0.4f, 0.9f) });
            }
        }

        private static float Angle(Vector2 d) => Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;

        private Tilemap Tilemap(string n, int order, Material m)
        {
            var go = new GameObject(n);
            go.transform.SetParent(transform, false);
            var tm = go.AddComponent<Tilemap>();
            var r = go.AddComponent<TilemapRenderer>();
            r.sortingOrder = order;
            r.sharedMaterial = m;
            return tm;
        }

        private static Tile Tile(Sprite s, Color c)
        {
            var t = ScriptableObject.CreateInstance<Tile>();
            t.sprite = s;
            t.color = c;
            t.flags = TileFlags.None;
            return t;
        }

        private static SpriteRenderer MakeSprite(Transform parent, string n, Sprite s, Material m, Vector2 pos, Vector2 scale, float rot, Color c, int order)
        {
            var go = new GameObject(n);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(0f, 0f, rot);
            go.transform.localScale = new Vector3(scale.x, scale.y, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = s;
            sr.sharedMaterial = m;
            sr.color = c;
            sr.sortingOrder = order;
            return sr;
        }

        /// <summary>Painted prop scaled to <paramref name="size"/> units (largest side), standing on <paramref name="basePos"/>, with a soft shadow.</summary>
        private void Place(Transform parent, Sprite s, Vector2 basePos, float size, int order)
        {
            if (s == null) return;
            var art = _ctx.Art;
            var b = s.bounds.size;
            float k = size / Mathf.Max(0.01f, Mathf.Max(b.x, b.y));
            float lift = b.y * k * 0.5f - 0.2f;
            MakeSprite(parent, "Shadow", art.Glow, art.Solid, basePos + new Vector2(0.1f, -0.05f), new Vector2(b.x * k * 1.1f, b.x * k * 0.45f), 0f, new Color(0f, 0f, 0f, 0.35f), SortOrder.Shadows);
            var r = MakeSprite(parent, s.name, s, art.Lit, basePos + new Vector2(0f, lift), new Vector2(k, k), 0f, Color.white, order);
            _pokes.Add(new Poke { T = r.transform, Pos = basePos + new Vector2(0f, lift), Radius = Mathf.Max(0.45f, size * 0.5f), Scale = r.transform.localScale });
        }

        private FighterVisual MakeFighter(string n, FighterLook look, Vector2 pos, WeaponId weapon)
        {
            var go = new GameObject(n);
            go.transform.SetParent(transform, false);
            go.transform.position = pos;
            var v = go.AddComponent<FighterVisual>();
            v.Build(_ctx.Art, _ctx.Catalog, look, "", Color.white);
            v.SetWeapon(_ctx.Config.Get(weapon));
            v.SetBarVisible(false);
            return v;
        }

        // ------------------------------------------------------------------ hero

        /// <summary>(Re)builds your soldier by the fire with the current locker look.</summary>
        public void SetHero(FighterLook look)
        {
            if (_heroRoot != null) Destroy(_heroRoot.gameObject);
            _hero = MakeFighter("Hero", look, HeroPos, WeaponId.Rifle);
            _heroRoot = _hero.transform;
            _heroRoot.localScale = new Vector3(1.25f, 1.25f, 1f);
            _heroAim = _heroTarget = 25f;
            _heroNext = 2f;
            _ctx.Fx.SpawnPoof(HeroPos, look.Outfit);
        }

        // ------------------------------------------------------------------ show / animate

        public void Show()
        {
            gameObject.SetActive(true);
            var g = _ctx.GlobalLight;
            if (g != null)
            {
                // bright enough moonlight that the camp reads on every phone, even where 2D point lights are weak
                g.color = new Color(0.55f, 0.62f, 0.9f);
                g.intensity = 0.55f;
            }
            if (_ctx.Camera != null) _ctx.Camera.backgroundColor = new Color(0.02f, 0.03f, 0.05f);
            // wide enough to frame the camp on every screen shape (tablets get a taller view)
            float aspect = _ctx.Camera != null ? Mathf.Max(0.5f, _ctx.Camera.aspect) : 2f;
            float size = Mathf.Max(CameraSize, 9.6f / aspect);
            _ctx.CameraRig.Hold(CameraCenter, size, !_introPlayed, FirePos);
            _introPlayed = true;
        }

        public void Hide() => gameObject.SetActive(false);

        // ------------------------------------------------------------------ taps

        private bool Interactive => _ctx.UI != null && _ctx.UI.HomeVisible && _ctx.Camera != null;

        private void HandleTap()
        {
            var ptr = Pointer.current;
            if (ptr == null || !ptr.press.wasPressedThisFrame || !Interactive) return;
            var es = EventSystem.current;
            if (es != null)
            {
                if (es.IsPointerOverGameObject()) return;
                var touch = Touchscreen.current;
                if (touch != null && es.IsPointerOverGameObject(touch.primaryTouch.touchId.ReadValue())) return;
            }
            Vector2 sp = ptr.position.ReadValue();
            TapAt(_ctx.Camera.ScreenToWorldPoint(new Vector3(sp.x, sp.y, -_ctx.Camera.transform.position.z)));
        }

        /// <summary>What a tap on the camp at world point <paramref name="wp"/> does.</summary>
        public void TapAt(Vector2 wp)
        {
            // your soldier glances at whatever you touch
            _heroTarget = Angle(wp - HeroPos);
            _heroNext = 3.5f;

            if (_hero != null && (wp - HeroPos).sqrMagnitude < 0.9f * 0.9f) { HeroReact(); return; }
            if ((wp - FirePos).sqrMagnitude < 1.05f * 1.05f) { FlareFire(); return; }
            for (int i = 0; i < _mates.Count; i++)
                if ((wp - _mates[i].Pos).sqrMagnitude < 0.8f * 0.8f) { MateReact(_mates[i], true); return; }
            Poke best = null;
            float bestD = float.MaxValue;
            for (int i = 0; i < _pokes.Count; i++)
            {
                float d = (wp - _pokes[i].Pos).sqrMagnitude;
                if (d < _pokes[i].Radius * _pokes[i].Radius && d < bestD) { bestD = d; best = _pokes[i]; }
            }
            if (best != null)
            {
                best.Wobble = 1f;
                _ctx.Sfx.Play(SfxId.Thump, best.Pos, 0.9f, 0.15f);
                _ctx.Fx.Dust(best.Pos + new Vector2(0f, -0.3f), 0.7f);
                _ctx.Haptics?.Pulse(12);
                return;
            }
            if (_water != null && _water.HasTile(_water.WorldToCell(wp)))
            {
                _ctx.Fx.SpawnPoof(wp, new Color(0.55f, 0.8f, 1f));
                _ctx.Sfx.Play(SfxId.Thump, wp, 0.5f, 0.25f);
                return;
            }
            _ctx.Fx.Dust(wp, 0.9f);
            _ctx.Sfx.Play(SfxId.Step, wp, 0.9f, 0.15f);
        }

        private void FlareFire()
        {
            _flare = 1f;
            for (int i = 0; i < 16; i++) _ctx.Fx.Ember(FirePos + new Vector2(Random.Range(-0.35f, 0.35f), Random.Range(0.1f, 0.6f)));
            _ctx.Fx.CampSmoke(FirePos);
            _ctx.Sfx.Play(SfxId.Flame, FirePos, 1f, 0.1f);
            _ctx.Sfx.Play(SfxId.Crackle, FirePos, 1f, 0.1f);
            _ctx.Haptics?.Pulse(20);
        }

        private void HeroReact()
        {
            _heroHop = 1f;
            _hero.Recoil(0.25f);
            _ctx.Sfx.Play(SfxId.Reload, HeroPos, 0.9f, 0.05f);
            var bubble = ChatBubble.For(_hero, _ctx.Art);
            bubble.Shift = new Vector2(1.15f, -0.55f); // to his right: the logo covers the space above him
            bubble.ShowText(PickLine(HeroLines));
            _ctx.Haptics?.Pulse(15);
        }

        private void MateReact(Mate m, bool tapped)
        {
            if (tapped) m.TargetAim = Angle(HeroPos - m.Pos) + Random.Range(-20f, 20f);
            m.Next = 3f;
            ChatBubble.For(m.Visual, _ctx.Art).ShowText(PickLine(MateLines));
            _ctx.Sfx.Play(SfxId.UiTap, m.Pos, 0.6f, 0.2f);
        }

        private string PickLine(string[] lines)
        {
            int i = Random.Range(0, lines.Length);
            if (i == _lastLine) i = (i + 1) % lines.Length;
            _lastLine = i;
            return lines[i];
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            float t = Time.time;
            HandleTap();

            // fire flicker (a tap makes it flare up)
            _flare = Mathf.MoveTowards(_flare, 0f, dt * 0.9f);
            float boost = 1f + _flare * _flare * 0.7f;
            float flick = Mathf.PerlinNoise(t * 6f, 0.3f);
            _fireLight.intensity = (1.05f + flick * 0.55f) * (1f + _flare * 0.9f);
            _fireLight.pointLightOuterRadius = (6f + flick * 1f) * (1f + _flare * 0.25f);
            _groundGlow.color = new Color(1f, 0.5f, 0.15f, (0.28f + flick * 0.15f) * (1f + _flare));
            for (int i = 0; i < _flames.Length; i++)
            {
                float n = Mathf.PerlinNoise(t * (4f + i), i * 3.7f);
                var tr = _flames[i].transform;
                float s = (i == 2 ? 0.8f + n * 0.35f : (i == 0 ? 1.4f : 1.05f) * (0.88f + n * 0.3f)) * boost;
                tr.localScale = new Vector3(s, s * (i == 2 ? 1f : 1.1f), 1f);
                if (i < 2) tr.rotation = Quaternion.Euler(0f, 0f, 90f + (n - 0.5f) * 18f + i * 8f);
            }
            _emberT -= dt;
            if (_emberT <= 0f) { _emberT = Random.Range(0.04f, 0.12f) / (1f + _flare * 3f); _ctx.Fx.Ember(FirePos + new Vector2(0f, 0.3f)); }
            _smokeT -= dt;
            if (_smokeT <= 0f) { _smokeT = Random.Range(0.35f, 0.6f); _ctx.Fx.CampSmoke(FirePos); }
            if (Interactive)
            {
                // the fire pops now and then; a mate says something every so often
                _crackleT -= dt;
                if (_crackleT <= 0f) { _crackleT = Random.Range(1.5f, 3.5f); _ctx.Sfx.Play(SfxId.Crackle, FirePos, 0.55f, 0.2f); }
                _chatterT -= dt;
                if (_chatterT <= 0f && _mates.Count > 0)
                {
                    _chatterT = Random.Range(14f, 26f);
                    MateReact(_mates[Random.Range(0, _mates.Count)], false);
                }
            }

            // tapped props wobble like jelly and settle
            for (int i = 0; i < _pokes.Count; i++)
            {
                var p = _pokes[i];
                if (p.Wobble <= 0f) continue;
                p.Wobble = Mathf.MoveTowards(p.Wobble, 0f, dt * 2.2f);
                float w = Mathf.Sin((1f - p.Wobble) * 28f) * p.Wobble * 0.14f;
                p.T.localScale = new Vector3(p.Scale.x * (1f + w), p.Scale.y * (1f - w), 1f);
            }

            // hero: glances at the fire, then back toward you
            if (_hero != null)
            {
                _heroNext -= dt;
                if (_heroNext <= 0f)
                {
                    _heroNext = Random.Range(2.5f, 5f);
                    _heroTarget = Random.value < 0.5f ? Angle(FirePos - HeroPos) + Random.Range(-15f, 15f) : Random.Range(-60f, 60f);
                }
                _heroAim = Mathf.MoveTowardsAngle(_heroAim, _heroTarget, dt * 260f);
                _hero.SetAim(Dir(_heroAim));
                _hero.Tick(dt, 0f, 1f, 0f, false);
                // little hop when tapped
                _heroHop = Mathf.MoveTowards(_heroHop, 0f, dt * 3f);
                float hop = Mathf.Sin(_heroHop * Mathf.PI) * 0.18f;
                _heroRoot.localScale = new Vector3(1.25f + hop, 1.25f + hop, 1f);
            }
            for (int i = 0; i < _mates.Count; i++)
            {
                var m = _mates[i];
                m.Next -= dt;
                if (m.Next <= 0f)
                {
                    m.Next = Random.Range(2f, 6f);
                    float toFire = Angle(FirePos - m.Pos);
                    m.TargetAim = Random.value < 0.6f ? toFire + Random.Range(-25f, 25f) : toFire + Random.Range(-140f, 140f);
                }
                m.Aim = Mathf.MoveTowardsAngle(m.Aim, m.TargetAim, dt * 90f);
                m.Visual.SetAim(Dir(m.Aim));
                m.Visual.Tick(dt, 0f, 1f, 0f, false);
            }

            // fireflies wander and blink
            for (int i = 0; i < _flies.Count; i++)
            {
                var f = _flies[i];
                f.Phase += dt * f.Speed;
                var p = f.Home + new Vector2(Mathf.Sin(f.Phase * 1.3f) * 1.4f + Mathf.Sin(f.Phase * 3.1f) * 0.3f, Mathf.Sin(f.Phase * 1.7f + 1f) * 0.9f);
                f.T.position = p;
                float blink = Mathf.Clamp01(Mathf.Sin(f.Phase * 4f + i) * 1.6f);
                f.R.color = new Color(0.75f, 1f, 0.4f, 0.15f + 0.75f * blink);
            }

            if (_water != null)
            {
                float w = 0.9f + Mathf.Sin(t * 1.4f) * 0.07f;
                _water.color = new Color(w, w + 0.04f, 1f);
            }
        }

        private static Vector2 Dir(float deg) => new Vector2(Mathf.Cos(deg * Mathf.Deg2Rad), Mathf.Sin(deg * Mathf.Deg2Rad));
    }
}
