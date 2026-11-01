using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace NeonBrawl
{
    /// <summary>
    /// All visual juice from one manager: pooled sprite particles, persistent floor decals (blood,
    /// scorch, bullet holes), bouncing shell casings, short light flashes and damage numbers.
    /// Perf: 0 B GC after warm-up; caps: 500 particles, 220 decals, 90 shells, 10 lights.
    /// </summary>
    public sealed class FxSystem : MonoBehaviour
    {
        private sealed class Particle
        {
            public Transform T;
            public SpriteRenderer R;
            public Vector2 Pos, Vel, Stretch;
            public float Life, MaxLife, Drag, Spin, Rot, S0, S1;
            public Color C0, C1;
        }

        private sealed class ShellObj
        {
            public Transform T;
            public SpriteRenderer R;
            public Vector2 Vel;
            public float Spin, Age;
            public bool Moving;
        }

        private sealed class FlashLight
        {
            public Light2D L;
            public float Life, MaxLife, Peak;
        }

        private sealed class Number
        {
            public Transform T;
            public TextMesh Text, Shadow;
            public float Life;
            public Vector2 Pos;
            public Color Color;
        }

        private const int MaxParticles = 500;
        private const int MaxDecals = 220;
        private const int MaxShells = 90;
        private const int MaxLights = 10;
        private const int MaxNumbers = 24;

        private GameContext _ctx;
        private ArtLibrary _art;
        private readonly List<Particle> _active = new List<Particle>(MaxParticles);
        private readonly List<Particle> _free = new List<Particle>(MaxParticles);
        private int _created;
        private SpriteRenderer[] _decals;
        private int _decalNext;
        private ShellObj[] _shells;
        private int _shellNext;
        private FlashLight[] _lights;
        private int _lightNext;
        private Number[] _numbers;
        private int _numberNext;
        private Transform _particleRoot;

        // Painted FX from the art catalog (null = use the code-drawn shape).
        private Sprite _flashBullet, _flashPlasma, _flashTesla, _fireBurst, _sparkBurst, _portal, _star;
        private Sprite[] _smokes, _bloods, _holes, _scorches, _debris;
        private Sprite _coin;

        private static Sprite[] Collect(Sprite[] sheet, int[] ids)
        {
            var list = new List<Sprite>(ids.Length);
            for (int i = 0; i < ids.Length; i++)
            {
                var s = ArtCatalog.At(sheet, ids[i]);
                if (s != null) list.Add(s);
            }
            return list.Count > 0 ? list.ToArray() : null;
        }

        private static Sprite Any(Sprite[] a) => a != null ? a[Random.Range(0, a.Length)] : null;

        private void LoadPainted(ArtCatalog cat)
        {
            if (cat == null) return;
            _flashBullet = ArtCatalog.At(cat.Fx1, ArtIds.FlashOrange);
            _flashPlasma = ArtCatalog.At(cat.Fx1, ArtIds.FlashGreen);
            _flashTesla = ArtCatalog.At(cat.Fx1, ArtIds.FlashBlue);
            _star = ArtCatalog.At(cat.Fx1, ArtIds.StarOrange);
            _fireBurst = ArtCatalog.At(cat.Fx2, ArtIds.FireBurst);
            _sparkBurst = ArtCatalog.At(cat.Fx2, ArtIds.SparkBurst);
            _portal = ArtCatalog.At(cat.Fx2, ArtIds.Portal);
            _coin = cat.Reward(ArtIds.RewardCoin);
            var smoke = new List<Sprite>(4);
            var s0 = ArtCatalog.At(cat.Fx2, ArtIds.SmokeGrey);
            var s1 = ArtCatalog.At(cat.Fx2, ArtIds.SmokeGrey2);
            if (s0 != null) smoke.Add(s0);
            if (s1 != null) smoke.Add(s1);
            var thick = Collect(cat.Fx3, ArtIds.Fx3Smoke);
            if (thick != null) smoke.AddRange(thick);
            _smokes = smoke.Count > 0 ? smoke.ToArray() : null;
            _bloods = Collect(cat.Fx3, ArtIds.Fx3Blood);
            _holes = Collect(cat.Fx3, ArtIds.Fx3Holes);
            _scorches = Collect(cat.Fx3, ArtIds.Fx3Scorch);
            _debris = Collect(cat.Fx3, ArtIds.Fx3Debris);
        }

        /// <summary>A soft smoke sprite (painted when available).</summary>
        private Sprite SmokeSprite => _smokes != null ? _smokes[Random.Range(0, _smokes.Length)] : _art.Smoke;

        public void Init(GameContext ctx)
        {
            _ctx = ctx;
            _art = ctx.Art;
            LoadPainted(ctx.Catalog);
            _particleRoot = new GameObject("Particles").transform;
            _particleRoot.SetParent(transform, false);

            var decalRoot = new GameObject("Decals").transform;
            decalRoot.SetParent(transform, false);
            _decals = new SpriteRenderer[MaxDecals];
            for (int i = 0; i < MaxDecals; i++)
            {
                var sr = NewSprite(decalRoot, "Decal");
                sr.sortingOrder = SortOrder.Decals;
                sr.enabled = false;
                _decals[i] = sr;
            }

            var shellRoot = new GameObject("Shells").transform;
            shellRoot.SetParent(transform, false);
            _shells = new ShellObj[MaxShells];
            for (int i = 0; i < MaxShells; i++)
            {
                var sr = NewSprite(shellRoot, "Shell");
                sr.sprite = _art.Square;
                sr.sharedMaterial = _art.Lit;
                sr.sortingOrder = SortOrder.Shells;
                sr.enabled = false;
                _shells[i] = new ShellObj { T = sr.transform, R = sr };
            }

            var lightRoot = new GameObject("FlashLights").transform;
            lightRoot.SetParent(transform, false);
            _lights = new FlashLight[MaxLights];
            for (int i = 0; i < MaxLights; i++)
            {
                var l = Lights.Point(lightRoot, Vector2.zero, Color.white, 3f, 0f);
                l.enabled = false;
                _lights[i] = new FlashLight { L = l };
            }

            var font = UIKit.Font;
            var numRoot = new GameObject("DamageNumbers").transform;
            numRoot.SetParent(transform, false);
            _numbers = new Number[MaxNumbers];
            for (int i = 0; i < MaxNumbers; i++)
            {
                var go = new GameObject("Number");
                go.transform.SetParent(numRoot, false);
                var shadow = MakeText(go.transform, font, new Vector3(0.025f, -0.025f, 0f), SortOrder.WorldUi + 5);
                var text = MakeText(go.transform, font, Vector3.zero, SortOrder.WorldUi + 6);
                go.SetActive(false);
                _numbers[i] = new Number { T = go.transform, Text = text, Shadow = shadow };
            }
        }

        private static TextMesh MakeText(Transform parent, Font font, Vector3 pos, int order)
        {
            var go = new GameObject("T");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            var tm = go.AddComponent<TextMesh>();
            tm.font = font;
            tm.fontSize = 56;
            tm.characterSize = 0.04f;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.fontStyle = UIKit.Strong;
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = font.material;
            mr.sortingOrder = order;
            return tm;
        }

        private static SpriteRenderer NewSprite(Transform parent, string n)
        {
            var go = new GameObject(n);
            go.transform.SetParent(parent, false);
            return go.AddComponent<SpriteRenderer>();
        }

        // ------------------------------------------------------------------ core spawners

        /// <summary>Spawns one pooled particle. Returns false when the cap is reached.</summary>
        public bool Spawn(Sprite sprite, Material mat, Vector2 pos, float rotDeg, Vector2 vel, float life,
            float scale0, float scale1, Color c0, Color c1, int order, float drag = 0f, float spin = 0f, Vector2 stretch = default)
        {
            Particle p;
            if (_free.Count > 0)
            {
                p = _free[_free.Count - 1];
                _free.RemoveAt(_free.Count - 1);
            }
            else if (_created < MaxParticles)
            {
                var sr = NewSprite(_particleRoot, "P");
                p = new Particle { T = sr.transform, R = sr };
                _created++;
            }
            else return false;

            p.R.sprite = sprite;
            p.R.sharedMaterial = mat;
            p.R.sortingOrder = order;
            p.R.enabled = true;
            p.Pos = pos;
            p.Vel = vel;
            p.Life = p.MaxLife = Mathf.Max(0.01f, life);
            p.S0 = scale0;
            p.S1 = scale1;
            p.C0 = c0;
            p.C1 = c1;
            p.Drag = drag;
            p.Spin = spin;
            p.Rot = rotDeg;
            p.Stretch = stretch == default ? Vector2.one : stretch;
            Apply(p, 0f);
            _active.Add(p);
            return true;
        }

        public void Decal(Sprite s, Material m, Vector2 pos, float rot, float scale, Color c, int order = SortOrder.Decals)
        {
            var sr = _decals[_decalNext];
            _decalNext = (_decalNext + 1) % MaxDecals;
            sr.sprite = s;
            sr.sharedMaterial = m;
            sr.color = c;
            sr.sortingOrder = order;
            sr.enabled = true;
            sr.transform.SetPositionAndRotation(new Vector3(pos.x, pos.y, 0f), Quaternion.Euler(0f, 0f, rot));
            sr.transform.localScale = new Vector3(scale, scale, 1f);
        }

        public void LightFlash(Vector2 pos, Color c, float radius, float intensity, float life)
        {
            var f = _lights[_lightNext];
            _lightNext = (_lightNext + 1) % MaxLights;
            f.L.transform.position = pos;
            f.L.color = c;
            f.L.pointLightOuterRadius = radius;
            f.L.pointLightInnerRadius = radius * 0.1f;
            f.L.intensity = intensity;
            f.L.enabled = true;
            f.Life = f.MaxLife = life;
            f.Peak = intensity;
        }

        // ------------------------------------------------------------------ composite effects

        public void MuzzleFlash(Vector2 muzzle, Vector2 dir, WeaponStats s)
        {
            float ang = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            Color hot, warm;
            switch (s.Id)
            {
                case WeaponId.Plasma: hot = new Color(0.8f, 1f, 0.8f); warm = new Color(0.2f, 1f, 0.4f, 0f); break;
                case WeaponId.Tesla: hot = new Color(0.85f, 0.95f, 1f); warm = new Color(0.3f, 0.6f, 1f, 0f); break;
                case WeaponId.Flamethrower: return;
                default: hot = new Color(1f, 0.92f, 0.65f); warm = new Color(1f, 0.45f, 0.1f, 0f); break;
            }
            float size = Mathf.Clamp(s.GunWidth / 0.1f, 0.8f, 1.6f) * (s.Id == WeaponId.Shotgun ? 1.35f : 1f) * Random.Range(0.85f, 1.1f);
            var flash = s.Id == WeaponId.Plasma ? _flashPlasma : s.Id == WeaponId.Tesla ? _flashTesla : _flashBullet;
            if (flash != null)
                Spawn(flash, _art.Additive, muzzle + dir * (0.16f * size), ang, Vector2.zero, 0.06f, 0.6f * size, 0.85f * size,
                    Color.white, new Color(1f, 1f, 1f, 0f), SortOrder.FxTop, 0f, 0f, new Vector2(1f, Random.Range(0.75f, 1.15f)));
            else Spawn(_art.Star, _art.Additive, muzzle, ang, Vector2.zero, 0.055f, 0.55f * size, 0.75f * size, hot, warm, SortOrder.FxTop);
            Spawn(_art.Glow, _art.Additive, muzzle, 0f, Vector2.zero, 0.07f, 0.9f * size, 1.2f * size, new Color(hot.r, hot.g, hot.b, 0.55f), warm, SortOrder.Fx);
            LightFlash(muzzle, new Color(1f, 0.78f, 0.45f), 3.2f * size, 1.6f, 0.07f);
            if (Random.value < 0.6f)
                Spawn(SmokeSprite, _art.Lit, muzzle, Random.value * 360f, dir * 0.6f + Random.insideUnitCircle * 0.3f, 0.5f, 0.15f, 0.5f,
                    new Color(0.85f, 0.85f, 0.85f, 0.25f), new Color(0.85f, 0.85f, 0.85f, 0f), SortOrder.Fx - 1, 2f, 40f);
        }

        public void Tracer(Vector2 from, Vector2 to, Color c, float width)
        {
            Vector2 d = to - from;
            float dist = d.magnitude;
            if (dist < 0.05f) return;
            Vector2 dir = d / dist;
            float len = Mathf.Min(1.6f, dist);
            const float speed = 95f;
            float ang = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            Spawn(_art.Spark, _art.Additive, from + dir * (len * 0.5f), ang, dir * speed, Mathf.Max(0.03f, (dist - len * 0.5f) / speed), 1f, 1f,
                new Color(c.r, c.g, c.b, 0.95f), new Color(c.r, c.g, c.b, 0.35f), SortOrder.Fx, 0f, 0f, new Vector2(len, 0.5f * width));
        }

        public void Shell(Vector2 pos, Vector2 aim, bool shotgun)
        {
            var s = _shells[_shellNext];
            _shellNext = (_shellNext + 1) % MaxShells;
            Vector2 side = new Vector2(aim.y, -aim.x);
            s.Vel = side * Random.Range(2.2f, 3.4f) - aim * Random.Range(0.2f, 0.8f);
            s.Spin = Random.Range(700f, 1300f) * (Random.value < 0.5f ? -1f : 1f);
            s.Age = 0f;
            s.Moving = true;
            s.R.enabled = true;
            s.R.color = shotgun ? new Color(0.8f, 0.15f, 0.12f) : new Color(0.92f, 0.72f, 0.28f);
            s.T.position = pos;
            s.T.localScale = shotgun ? new Vector3(0.14f, 0.075f, 1f) : new Vector3(0.1f, 0.05f, 1f);
        }

        public void Impact(Vector2 point, Vector2 normal, bool prop)
        {
            int n = Random.Range(3, 6);
            for (int i = 0; i < n; i++)
            {
                Vector2 d = Combat.Rotate(normal, Random.Range(-65f, 65f));
                float sp = Random.Range(4f, 9f);
                Spawn(_art.Spark, _art.Additive, point, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg, d * sp, Random.Range(0.08f, 0.18f), 1f, 0.6f,
                    new Color(1f, 0.85f, 0.45f), new Color(1f, 0.4f, 0.1f, 0f), SortOrder.Fx, 8f, 0f, new Vector2(0.25f, 0.4f));
            }
            Color dust = prop ? new Color(0.62f, 0.45f, 0.3f, 0.5f) : new Color(0.85f, 0.78f, 0.62f, 0.5f);
            Spawn(SmokeSprite, _art.Lit, point + normal * 0.08f, Random.value * 360f, normal * 0.8f, 0.4f, 0.2f, 0.55f, dust, new Color(dust.r, dust.g, dust.b, 0f), SortOrder.Fx - 1, 3f, 60f);
            if (prop)
                for (int i = 0; i < 3; i++)
                {
                    Vector2 d = Combat.Rotate(normal, Random.Range(-70f, 70f));
                    Spawn(_art.Square, _art.Lit, point, Random.value * 360f, d * Random.Range(2f, 4f), 0.45f, 0.08f, 0.06f,
                        new Color(0.55f, 0.38f, 0.22f), new Color(0.55f, 0.38f, 0.22f, 0f), SortOrder.Fx - 2, 5f, 720f, new Vector2(1.6f, 0.6f));
                }
        }

        public void BulletHole(Vector2 point)
        {
            var hole = Any(_holes);
            if (hole != null) Decal(hole, _art.Lit, point, Random.value * 360f, Random.Range(0.16f, 0.22f), new Color(0.8f, 0.78f, 0.75f, 0.85f), SortOrder.Decals + 1);
            else Decal(_art.Circle, _art.Lit, point, 0f, Random.Range(0.07f, 0.1f), new Color(0.12f, 0.1f, 0.08f, 0.6f), SortOrder.Decals + 1);
        }

        /// <summary>Crate burst: painted debris chunks (or wood splinters), dust and a flash.</summary>
        public void Debris(Vector2 pos, Vector2 dir, Color wood, Sprite piece = null)
        {
            dir = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector2.up;
            for (int i = 0; i < 12; i++)
            {
                Vector2 d = Combat.Rotate(dir, Random.Range(-100f, 100f));
                if (piece != null && i % 2 == 0)
                    Spawn(piece, _art.Lit, pos, Random.value * 360f, d * Random.Range(2f, 5.5f), Random.Range(0.5f, 0.85f), Random.Range(0.28f, 0.4f), 0.15f,
                        Color.white, new Color(1f, 1f, 1f, 0f), SortOrder.Fx - 2, 5f, Random.Range(-500f, 500f));
                else
                    Spawn(_art.Square, _art.Lit, pos, Random.value * 360f, d * Random.Range(2.5f, 6.5f), Random.Range(0.4f, 0.7f), 0.22f, 0.1f,
                        wood, new Color(wood.r, wood.g, wood.b, 0f), SortOrder.Fx - 2, 5f, Random.Range(-700f, 700f), new Vector2(1.8f, 0.5f));
            }
            for (int i = 0; i < 3; i++)
                Spawn(SmokeSprite, _art.Lit, pos + Random.insideUnitCircle * 0.3f, Random.value * 360f, Random.insideUnitCircle * 0.7f, Random.Range(0.6f, 0.9f), 0.4f, 1.2f,
                    new Color(0.82f, 0.74f, 0.6f, 0.6f), new Color(0.82f, 0.74f, 0.6f, 0f), SortOrder.Fx - 1, 2f, 40f);
            Decal(_art.Splats[Random.Range(0, _art.Splats.Length)], _art.Lit, pos, Random.value * 360f, 0.7f, new Color(wood.r * 0.6f, wood.g * 0.6f, wood.b * 0.6f, 0.5f));
        }

        public void Blood(Vector2 point, Vector2 dir, float amount)
        {
            dir = dir.sqrMagnitude > 0.0001f ? dir.normalized : Random.insideUnitCircle.normalized;
            if (!_ctx.Prefs.Blood)
            {
                Impact(point, dir, false);
                return;
            }
            var red = new Color(0.62f, 0.04f, 0.05f);
            int drops = Mathf.Clamp((int)(amount / 6f), 2, 8);
            for (int i = 0; i < drops; i++)
            {
                Vector2 d = Combat.Rotate(dir, Random.Range(-35f, 35f));
                Spawn(_art.Circle, _art.Lit, point, 0f, d * Random.Range(2.5f, 6.5f), Random.Range(0.18f, 0.32f), Random.Range(0.08f, 0.14f), 0.05f,
                    red, new Color(red.r, red.g, red.b, 0.6f), SortOrder.Fx - 3, 7f);
            }
            if (amount >= 8f)
            {
                float s = Mathf.Clamp(amount / 60f, 0.25f, 0.7f) * Random.Range(0.8f, 1.2f);
                var painted = Any(_bloods);
                if (painted != null)
                    Decal(painted, _art.Lit, point + dir * Random.Range(0.3f, 0.9f), Random.value * 360f, s * 1.3f, new Color(1f, 0.85f, 0.85f, 0.92f));
                else
                    Decal(_art.Splats[Random.Range(0, _art.Splats.Length)], _art.Lit, point + dir * Random.Range(0.3f, 0.9f), Random.value * 360f, s,
                        new Color(0.45f, 0.03f, 0.04f, 0.85f));
            }
        }

        public void DeathBurst(Vector2 pos, Vector2 dir, Color outfit)
        {
            if (_ctx.Prefs.Blood)
            {
                var painted = Any(_bloods);
                if (painted != null) Decal(painted, _art.Lit, pos + dir * 0.5f, Random.value * 360f, Random.Range(1.2f, 1.5f), new Color(1f, 0.82f, 0.82f, 0.95f));
                else Decal(_art.Splats[Random.Range(0, _art.Splats.Length)], _art.Lit, pos + dir * 0.5f, Random.value * 360f, Random.Range(0.9f, 1.2f), new Color(0.4f, 0.02f, 0.03f, 0.9f));
            }
            Spawn(_art.Ring, _art.Additive, pos, 0f, Vector2.zero, 0.3f, 0.3f, 1.8f, new Color(1f, 1f, 1f, 0.6f), new Color(1f, 1f, 1f, 0f), SortOrder.Fx);
            for (int i = 0; i < 8; i++)
            {
                Vector2 d = Combat.Rotate(dir, Random.Range(-80f, 80f));
                Spawn(_art.Square, _art.Lit, pos, Random.value * 360f, d * Random.Range(2f, 6f), Random.Range(0.4f, 0.7f), 0.12f, 0.06f,
                    outfit, new Color(outfit.r, outfit.g, outfit.b, 0f), SortOrder.Fx - 2, 5f, Random.Range(-600f, 600f));
            }
        }

        public void SpawnPoof(Vector2 pos, Color c)
        {
            if (_portal != null)
                Spawn(_portal, _art.Additive, pos, 0f, Vector2.zero, 0.5f, 0.3f, 1.9f, Color.white, new Color(c.r, c.g, c.b, 0f), SortOrder.Fx, 0f, 220f);
            Spawn(_art.Ring, _art.Additive, pos, 0f, Vector2.zero, 0.35f, 0.2f, 1.6f, new Color(c.r, c.g, c.b, 0.9f), new Color(c.r, c.g, c.b, 0f), SortOrder.Fx);
            Spawn(_art.Glow, _art.Additive, pos, 0f, Vector2.zero, 0.3f, 1.4f, 0.4f, new Color(1f, 1f, 1f, 0.6f), new Color(c.r, c.g, c.b, 0f), SortOrder.Fx);
            for (int i = 0; i < 8; i++)
            {
                Vector2 d = Random.insideUnitCircle.normalized;
                Spawn(_art.Glow, _art.Additive, pos + d * 0.3f, 0f, d * Random.Range(1.5f, 3f), Random.Range(0.3f, 0.5f), 0.2f, 0.05f, c, new Color(c.r, c.g, c.b, 0f), SortOrder.Fx, 3f);
            }
        }

        public void PickupSparkle(Vector2 pos, Color c)
        {
            Spawn(_art.Ring, _art.Additive, pos, 0f, Vector2.zero, 0.3f, 0.3f, 1.3f, c, new Color(c.r, c.g, c.b, 0f), SortOrder.Fx);
            for (int i = 0; i < 6; i++)
            {
                Vector2 d = Random.insideUnitCircle.normalized;
                Spawn(_art.Star, _art.Additive, pos, Random.value * 360f, d * 2f + Vector2.up * 1.2f, 0.45f, 0.2f, 0.05f, Color.white, new Color(c.r, c.g, c.b, 0f), SortOrder.Fx, 3f, 300f);
            }
        }

        public void DashTrail(Vector2 pos, Vector2 dir, Color c)
        {
            for (int i = 0; i < 4; i++)
                Spawn(_art.Circle, _art.Additive, pos - dir * (0.25f * i), 0f, -dir * 1.5f, 0.25f + i * 0.04f, 0.7f, 0.4f,
                    new Color(c.r, c.g, c.b, 0.35f), new Color(c.r, c.g, c.b, 0f), SortOrder.Fx - 4);
            Dust(pos, 1f);
        }

        public void Dust(Vector2 pos, float strength)
        {
            var c = new Color(0.86f, 0.78f, 0.62f, 0.38f * strength);
            Spawn(SmokeSprite, _art.Lit, pos + Random.insideUnitCircle * 0.1f, Random.value * 360f, Random.insideUnitCircle * 0.4f, 0.45f, 0.2f, 0.55f * strength + 0.1f,
                c, new Color(c.r, c.g, c.b, 0f), SortOrder.Decals + 2, 2f, 30f);
        }

        public void Ember(Vector2 pos)
        {
            Spawn(_art.Glow, _art.Additive, pos + Random.insideUnitCircle * 0.3f, 0f, Random.insideUnitCircle * 0.5f + Vector2.up * 0.8f, 0.4f, 0.35f, 0.05f,
                new Color(1f, 0.6f, 0.15f, 0.9f), new Color(1f, 0.2f, 0.05f, 0f), SortOrder.FxTop);
        }

        public void Flame(Vector2 origin, Vector2 dir, float reach, float spread)
        {
            const float speed = 13f;
            for (int i = 0; i < 2; i++)
            {
                Vector2 d = Combat.Rotate(dir, Random.Range(-spread * 0.6f, spread * 0.6f));
                float life = Mathf.Max(0.06f, reach / speed) * Random.Range(0.85f, 1.05f);
                Spawn(_art.Glow, _art.Additive, origin, Random.value * 360f, d * speed, life, 0.3f, 1.1f,
                    new Color(1f, 0.85f, 0.4f, 0.95f), new Color(0.9f, 0.15f, 0.02f, 0f), SortOrder.FxTop, 0.5f, Random.Range(-200f, 200f));
            }
            if (Random.value < 0.35f)
                Spawn(SmokeSprite, _art.Lit, origin + dir * reach * 0.8f, Random.value * 360f, dir * 1.5f, 0.7f, 0.3f, 0.9f,
                    new Color(0.2f, 0.18f, 0.16f, 0.35f), new Color(0.2f, 0.18f, 0.16f, 0f), SortOrder.Fx - 1, 1.5f, 50f);
            LightFlash(origin + dir * reach * 0.5f, new Color(1f, 0.6f, 0.25f), reach * 0.9f, 1.1f, 0.08f);
        }

        public void Lightning(Vector2 a, Vector2 b, Color c)
        {
            Vector2 d = b - a;
            float dist = d.magnitude;
            if (dist < 0.05f) return;
            Vector2 perp = new Vector2(-d.y, d.x) / dist;
            int segs = Mathf.Clamp((int)(dist / 0.6f), 3, 12);
            Vector2 prev = a;
            for (int i = 1; i <= segs; i++)
            {
                Vector2 next = i == segs ? b : a + d * ((float)i / segs) + perp * Random.Range(-0.28f, 0.28f);
                Vector2 s = next - prev;
                float len = s.magnitude;
                Spawn(_art.Spark, _art.Additive, (prev + next) * 0.5f, Mathf.Atan2(s.y, s.x) * Mathf.Rad2Deg, Vector2.zero, 0.09f, 1f, 1f,
                    new Color(0.9f, 0.97f, 1f), new Color(c.r, c.g, c.b, 0f), SortOrder.FxTop, 0f, 0f, new Vector2(len * 1.15f, 0.7f));
                prev = next;
            }
            Spawn(_art.Glow, _art.Additive, b, 0f, Vector2.zero, 0.12f, 0.9f, 0.3f, new Color(c.r, c.g, c.b, 0.9f), new Color(c.r, c.g, c.b, 0f), SortOrder.Fx);
            LightFlash(b, c, 3f, 1.4f, 0.08f);
        }

        public void Slash(Vector2 pos, Vector2 dir)
        {
            float ang = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            Spawn(_art.Slash, _art.Additive, pos, ang, dir * 1.5f, 0.14f, 0.9f, 1.25f, new Color(1f, 1f, 1f, 0.85f), new Color(1f, 1f, 1f, 0f), SortOrder.FxTop);
        }

        public void Explosion(Vector2 pos, float radius)
        {
            LightFlash(pos, new Color(1f, 0.7f, 0.35f), radius * 3.2f, 2.6f, 0.3f);
            Spawn(_art.Glow, _art.Additive, pos, 0f, Vector2.zero, 0.12f, radius * 0.9f, radius * 1.5f, new Color(1f, 0.95f, 0.8f, 0.85f), new Color(1f, 0.7f, 0.3f, 0f), SortOrder.FxTop);
            if (_fireBurst != null)
            {
                // painted fireball, alpha-blended so its orange and red survive on bright ground
                for (int i = 0; i < 4; i++)
                    Spawn(_fireBurst, _art.Lit, pos + Random.insideUnitCircle * radius * 0.3f, Random.value * 360f, Random.insideUnitCircle * 0.8f,
                        Random.Range(0.45f, 0.7f), radius * 0.7f, radius * Random.Range(1.5f, 2f), Color.white, new Color(1f, 0.6f, 0.3f, 0f), SortOrder.FxTop, 1.5f, Random.Range(-50f, 50f));
            }
            else
            {
                for (int i = 0; i < 7; i++)
                {
                    Vector2 off = Random.insideUnitCircle * radius * 0.35f;
                    Spawn(_art.Glow, _art.Additive, pos + off, 0f, off * 1.5f, Random.Range(0.3f, 0.5f), 0.6f, radius * Random.Range(0.8f, 1.1f),
                        new Color(1f, 0.78f, 0.32f), new Color(0.75f, 0.15f, 0.04f, 0f), SortOrder.FxTop, 2f);
                }
            }
            Spawn(_art.Ring, _art.Additive, pos, 0f, Vector2.zero, 0.32f, 0.5f, radius * 2.6f, new Color(1f, 0.9f, 0.7f, 0.6f), new Color(1f, 0.6f, 0.3f, 0f), SortOrder.FxTop);
            var rock = Any(_debris);
            if (rock != null)
                for (int i = 0; i < 5; i++)
                {
                    Vector2 d = Random.insideUnitCircle.normalized;
                    Spawn(rock, _art.Lit, pos, Random.value * 360f, d * Random.Range(4f, 8f), Random.Range(0.5f, 0.8f), Random.Range(0.18f, 0.28f), 0.12f,
                        Color.white, new Color(1f, 1f, 1f, 0f), SortOrder.Fx - 2, 4f, Random.Range(-600f, 600f));
                }
            if (_sparkBurst != null)
                Spawn(_sparkBurst, _art.Additive, pos, Random.value * 360f, Vector2.zero, 0.2f, radius * 1f, radius * 2.2f, new Color(1f, 1f, 1f, 0.7f), new Color(1f, 0.7f, 0.3f, 0f), SortOrder.FxTop);
            for (int i = 0; i < 14; i++)
            {
                Vector2 d = Random.insideUnitCircle.normalized;
                Spawn(_art.Spark, _art.Additive, pos, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg, d * Random.Range(8f, 16f), Random.Range(0.2f, 0.42f), 1f, 0.5f,
                    new Color(1f, 0.85f, 0.4f), new Color(1f, 0.3f, 0.05f, 0f), SortOrder.FxTop, 5f, 0f, new Vector2(0.5f, 0.5f));
            }
            for (int i = 0; i < 7; i++)
            {
                Vector2 off = Random.insideUnitCircle * radius * 0.5f;
                Spawn(SmokeSprite, _art.Lit, pos + off, Random.value * 360f, off * 0.6f + Random.insideUnitCircle * 0.3f, Random.Range(1.2f, 2f), 0.8f, radius * 1.15f,
                    new Color(0.42f, 0.4f, 0.38f, 0.7f), new Color(0.5f, 0.48f, 0.45f, 0f), SortOrder.Fx - 1, 1.2f, Random.Range(-30f, 30f));
            }
            var scorch = Any(_scorches);
            if (scorch != null) Decal(scorch, _art.Lit, pos, Random.value * 360f, radius * 0.95f, new Color(0.55f, 0.5f, 0.45f, 0.9f));
            else Decal(_art.Glow, _art.Solid, pos, 0f, radius * 1.1f, new Color(0.05f, 0.04f, 0.03f, 0.6f));
        }

        /// <summary>Kill reward: a coin pops out of the victim, spins up and fades (bright even at night).</summary>
        public void CoinPop(Vector2 pos)
        {
            if (_coin == null) return;
            Spawn(_coin, _art.Unlit, pos + new Vector2(0f, 0.4f), 0f, new Vector2(0f, 1.6f), 0.9f, 0.55f, 0.8f, Color.white, new Color(1f, 1f, 1f, 0f), SortOrder.WorldUi + 3, 2.2f, 0f);
            Spawn(_art.Glow, _art.Additive, pos + new Vector2(0f, 0.4f), 0f, new Vector2(0f, 1.6f), 0.5f, 0.9f, 0.3f, new Color(1f, 0.85f, 0.3f, 0.7f), new Color(1f, 0.6f, 0.1f, 0f), SortOrder.WorldUi + 2, 2.2f);
            for (int i = 0; i < 5; i++)
            {
                Vector2 d = Random.insideUnitCircle.normalized;
                Spawn(_art.Star, _art.Additive, pos + new Vector2(0f, 0.4f), Random.value * 360f, d * 2.2f + Vector2.up, 0.4f, 0.18f, 0.04f, new Color(1f, 0.95f, 0.6f), new Color(1f, 0.7f, 0.2f, 0f), SortOrder.WorldUi + 2, 3f, 400f);
            }
        }

        /// <summary>Slow soft smoke rising from a campfire.</summary>
        public void CampSmoke(Vector2 pos)
        {
            Spawn(SmokeSprite, _art.Lit, pos + new Vector2(Random.Range(-0.15f, 0.15f), 0.3f), Random.value * 360f,
                new Vector2(Random.Range(-0.15f, 0.25f), Random.Range(0.6f, 0.9f)), Random.Range(2.2f, 3.2f), 0.35f, 1.6f,
                new Color(0.55f, 0.52f, 0.5f, 0.35f), new Color(0.4f, 0.4f, 0.45f, 0f), SortOrder.Canopy - 1, 0.3f, Random.Range(-20f, 20f));
        }

        public void SmokeTrail(Vector2 pos)
        {
            Spawn(SmokeSprite, _art.Lit, pos, Random.value * 360f, Random.insideUnitCircle * 0.3f, 0.55f, 0.15f, 0.5f,
                new Color(0.8f, 0.78f, 0.75f, 0.5f), new Color(0.8f, 0.78f, 0.75f, 0f), SortOrder.Projectiles - 1, 1f, 40f);
            Spawn(_art.Glow, _art.Additive, pos, 0f, Vector2.zero, 0.08f, 0.35f, 0.15f, new Color(1f, 0.6f, 0.2f, 0.8f), new Color(1f, 0.3f, 0.1f, 0f), SortOrder.Projectiles - 1);
        }

        public void PlasmaPop(Vector2 pos, Color c)
        {
            Spawn(_art.Ring, _art.Additive, pos, 0f, Vector2.zero, 0.2f, 0.2f, 0.9f, c, new Color(c.r, c.g, c.b, 0f), SortOrder.FxTop);
            Spawn(_art.Glow, _art.Additive, pos, 0f, Vector2.zero, 0.15f, 0.9f, 0.3f, Color.white, new Color(c.r, c.g, c.b, 0f), SortOrder.FxTop);
            for (int i = 0; i < 5; i++)
            {
                Vector2 d = Random.insideUnitCircle.normalized;
                Spawn(_art.Spark, _art.Additive, pos, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg, d * Random.Range(3f, 6f), 0.18f, 1f, 0.5f, c, new Color(c.r, c.g, c.b, 0f), SortOrder.FxTop, 6f, 0f, new Vector2(0.3f, 0.5f));
            }
            LightFlash(pos, c, 2.4f, 1.3f, 0.1f);
        }

        public void DamageNumber(Vector2 pos, float amount, Color color, bool big)
        {
            int value = Mathf.RoundToInt(amount);
            if (value <= 0) return;
            var n = _numbers[_numberNext];
            _numberNext = (_numberNext + 1) % MaxNumbers;
            n.Text.text = IntToString(value);
            n.Shadow.text = n.Text.text;
            n.Color = color;
            n.Life = 0.75f;
            n.Pos = pos + new Vector2(Random.Range(-0.2f, 0.2f), 0.4f);
            float s = big ? 1.5f : 1f;
            n.T.localScale = new Vector3(s, s, 1f);
            n.T.gameObject.SetActive(true);
        }

        private static readonly string[] SmallInts = BuildInts();

        private static string[] BuildInts()
        {
            var a = new string[301];
            for (int i = 0; i < a.Length; i++) a[i] = i.ToString();
            return a;
        }

        private static string IntToString(int v) => v >= 0 && v < SmallInts.Length ? SmallInts[v] : v.ToString();

        public void Clear()
        {
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                _active[i].R.enabled = false;
                _free.Add(_active[i]);
            }
            _active.Clear();
            for (int i = 0; i < _decals.Length; i++) _decals[i].enabled = false;
            for (int i = 0; i < _shells.Length; i++) { _shells[i].R.enabled = false; _shells[i].Moving = false; }
            for (int i = 0; i < _lights.Length; i++) { _lights[i].L.enabled = false; _lights[i].Life = 0f; }
            for (int i = 0; i < _numbers.Length; i++) _numbers[i].T.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------ update

        private void LateUpdate()
        {
            float dt = Time.deltaTime;
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                var p = _active[i];
                p.Life -= dt;
                if (p.Life <= 0f)
                {
                    p.R.enabled = false;
                    _active[i] = _active[_active.Count - 1];
                    _active.RemoveAt(_active.Count - 1);
                    _free.Add(p);
                    continue;
                }
                if (p.Drag > 0f) p.Vel *= Mathf.Exp(-p.Drag * dt);
                p.Pos += p.Vel * dt;
                p.Rot += p.Spin * dt;
                Apply(p, 1f - p.Life / p.MaxLife);
            }

            for (int i = 0; i < _shells.Length; i++)
            {
                var s = _shells[i];
                if (!s.Moving) continue;
                s.Age += dt;
                s.Vel *= Mathf.Exp(-5.5f * dt);
                s.Spin *= Mathf.Exp(-4f * dt);
                Vector3 pos = s.T.position;
                s.T.position = new Vector3(pos.x + s.Vel.x * dt, pos.y + s.Vel.y * dt, 0f);
                s.T.Rotate(0f, 0f, s.Spin * dt);
                if (s.Age > 0.9f) s.Moving = false;
            }

            for (int i = 0; i < _lights.Length; i++)
            {
                var f = _lights[i];
                if (f.Life <= 0f) continue;
                f.Life -= dt;
                if (f.Life <= 0f) { f.L.enabled = false; continue; }
                f.L.intensity = f.Peak * (f.Life / f.MaxLife);
            }

            for (int i = 0; i < _numbers.Length; i++)
            {
                var n = _numbers[i];
                if (n.Life <= 0f) continue;
                n.Life -= dt;
                if (n.Life <= 0f) { n.T.gameObject.SetActive(false); continue; }
                float t = 1f - n.Life / 0.75f;
                n.Pos += new Vector2(0f, 1.3f * dt);
                n.T.position = n.Pos;
                float pop = t < 0.12f ? 1f + (0.12f - t) * 4f : 1f;
                float a = t > 0.6f ? 1f - (t - 0.6f) / 0.4f : 1f;
                var c = n.Color;
                c.a = a;
                n.Text.color = c;
                n.Shadow.color = new Color(0f, 0f, 0f, a * 0.85f);
                n.Text.transform.localScale = new Vector3(pop, pop, 1f);
                n.Shadow.transform.localScale = n.Text.transform.localScale;
            }
        }

        private static void Apply(Particle p, float t)
        {
            float s = Mathf.LerpUnclamped(p.S0, p.S1, t);
            p.T.SetPositionAndRotation(new Vector3(p.Pos.x, p.Pos.y, 0f), Quaternion.Euler(0f, 0f, p.Rot));
            p.T.localScale = new Vector3(s * p.Stretch.x, s * p.Stretch.y, 1f);
            p.R.color = Color.LerpUnclamped(p.C0, p.C1, t);
        }
    }
}
