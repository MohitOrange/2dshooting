using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace NeonBrawl
{
    /// <summary>How a fighter looks (wardrobe choice). Body/Head index into the art catalog.</summary>
    public struct FighterLook
    {
        public Color Outfit;
        public Color Skin;
        public Color Hair;
        /// <summary>Outfit index (C1 sheet).</summary>
        public int Body;
        /// <summary>0 = bald head, 1+ = headgear (C2 sheet).</summary>
        public int Head;
        /// <summary>0 = none, 1+ = C3 hair style (hidden under headgear).</summary>
        public int HairStyle;
        /// <summary>0 = none, 1+ = C3 back item.</summary>
        public int Back;
    }

    /// <summary>
    /// Top-down body drawn from layered sprites inside a SortingGroup: the sliced art (body tint +
    /// detail layers, head, headgear) when available, otherwise code-generated shapes.
    /// Handles aim rotation, recoil, white hit flash, hit shake, walk sway, spawn blink,
    /// line-of-sight fading, corpse look, and the floating health bar + name.
    /// Perf: 0 B GC/frame; ~12 renderers per fighter.
    /// </summary>
    public sealed class FighterVisual : MonoBehaviour
    {
        private Transform _rig;
        private Transform _body;
        private SortingGroup _group;
        private SpriteRenderer[] _parts;
        private Color[] _partColors;
        private SpriteRenderer[] _flash;
        private SpriteRenderer _shadow, _gun, _barBg, _barGhost, _barFill, _barArmor;
        private Transform _gunT, _handL, _handR, _barRoot;
        private Vector3 _handLBase, _handRBase;
        private TextMesh _name, _nameShadow;
        private MeshRenderer _nameR;
        private bool _artMode;
        private Vector3 _shadowOffset;

        private float _gunCenter = 0.13f;
        private ArtCatalog _catalog;
        private ArtLibrary _art;
        private float _recoil;
        private float _flashT;
        private float _shakeT;
        private float _alpha = 1f;
        private float _appliedAlpha = 1f;
        private float _targetAlpha = 1f;
        private float _phase;
        private float _shownHp = 1f;
        private float _ghostHp = 1f;
        private float _ghostDelay;
        private float _swingT;
        private bool _dead;
        private float _deadTime;
        private bool _barVisible = true;
        private Color _nameColor;

        /// <summary>Distance from the fighter's center to where the gun's grip sits (units).</summary>
        public float GunBase { get; private set; } = 0.3f;

        /// <summary>Current line-of-sight fade (0 = hidden from the local player).</summary>
        public float Alpha => _alpha;

        public void Build(ArtLibrary art, ArtCatalog catalog, FighterLook look, string displayName, Color nameColor)
        {
            _nameColor = nameColor;
            _catalog = catalog;
            _art = art;
            _rig = new GameObject("Rig").transform;
            _rig.SetParent(transform, false);
            _group = _rig.gameObject.AddComponent<SortingGroup>();
            _group.sortingOrder = SortOrder.Fighters;
            _body = new GameObject("Body").transform;
            _body.SetParent(_rig, false);

            var parts = new List<SpriteRenderer>(10);
            _artMode = catalog != null && catalog.HasCharacters;
            if (_artMode)
            {
                int b = Mathf.Abs(look.Body) % catalog.BodyTint.Length;
                GunBase = catalog.HandsForward - 0.12f;
                var back = ArtCatalog.At(catalog.Back, look.Back - 1);
                if (back != null)
                {
                    float bs = 0.6f / Mathf.Max(0.01f, back.bounds.size.y);
                    parts.Add(Part("BackItem", _body, back, Color.white, new Vector2(-0.3f, 0f), new Vector2(bs, bs), -90f, 0, art.Lit));
                }
                parts.Add(Part("BodyCloth", _body, catalog.BodyTint[b], look.Outfit, Vector2.zero, Vector2.one, 0f, 2, art.Lit));
                if (b < catalog.BodyDetail.Length && catalog.BodyDetail[b] != null)
                    parts.Add(Part("BodyDetail", _body, catalog.BodyDetail[b], Color.white, Vector2.zero, Vector2.one, 0f, 3, art.Lit));
                _gun = Part("Gun", _rig, art.Square, new Color(0.22f, 0.23f, 0.25f), new Vector2(GunBase + 0.13f, 0f), new Vector2(0.26f, 0.09f), 0f, 1, art.Lit);
                parts.Add(_gun);
                parts.Add(Part("Head", _rig, catalog.Heads[0], look.Skin, Vector2.zero, Vector2.one, 0f, 5, art.Lit));
                if (look.Head > 0 && look.Head < catalog.Heads.Length)
                    parts.Add(Part("Headgear", _rig, catalog.Heads[look.Head], Color.white, Vector2.zero, Vector2.one, 0f, 6, art.Lit));
                else
                {
                    var hair = ArtCatalog.At(catalog.Hair, look.HairStyle - 1);
                    if (hair != null) parts.Add(Part("Hair", _rig, hair, look.Skin, Vector2.zero, Vector2.one, 0f, 6, art.Lit));
                }
                _shadowOffset = new Vector3(0.1f, -0.13f, 0f);
                _shadow = Part("Shadow", transform, catalog.BodyTint[b], new Color(0f, 0f, 0f, 0.3f), _shadowOffset, Vector2.one, 0f, SortOrder.Shadows, art.Solid);
            }
            else
            {
                GunBase = 0.3f;
                Color sleeve = look.Outfit * 0.82f; sleeve.a = 1f;
                Color pack = look.Outfit * 0.55f; pack.a = 1f;
                Color glove = new Color(0.16f, 0.16f, 0.18f);
                parts.Add(Part("Pack", _body, art.Square, pack, new Vector2(-0.27f, 0f), new Vector2(0.2f, 0.38f), 0f, 1, art.Lit));
                parts.Add(Part("Torso", _body, art.Circle, look.Outfit, new Vector2(-0.03f, 0f), new Vector2(0.52f, 0.76f), 0f, 2, art.Lit));
                parts.Add(Part("ArmL", _body, art.Square, sleeve, new Vector2(0.19f, 0.175f), new Vector2(0.43f, 0.12f), -29f, 3, art.Lit));
                parts.Add(Part("ArmR", _body, art.Square, sleeve, new Vector2(0.19f, -0.175f), new Vector2(0.43f, 0.12f), 29f, 3, art.Lit));
                _gun = Part("Gun", _rig, art.Square, new Color(0.22f, 0.23f, 0.25f), new Vector2(GunBase + 0.13f, 0f), new Vector2(0.26f, 0.09f), 0f, 4, art.Lit);
                parts.Add(_gun);
                var hl = Part("HandL", _rig, art.Circle, glove, new Vector2(0.38f, 0.07f), new Vector2(0.15f, 0.15f), 0f, 5, art.Lit);
                var hr = Part("HandR", _rig, art.Circle, glove, new Vector2(0.38f, -0.07f), new Vector2(0.15f, 0.15f), 0f, 5, art.Lit);
                parts.Add(hl);
                parts.Add(hr);
                _handL = hl.transform;
                _handR = hr.transform;
                _handLBase = _handL.localPosition;
                _handRBase = _handR.localPosition;
                parts.Add(Part("Head", _rig, art.Circle, look.Skin, new Vector2(0.01f, 0f), new Vector2(0.4f, 0.4f), 0f, 6, art.Lit));
                parts.Add(Part("Hair", _rig, art.Circle, look.Hair, new Vector2(-0.04f, 0f), new Vector2(0.34f, 0.38f), 0f, 7, art.Lit));
                _shadowOffset = new Vector3(0.08f, -0.12f, 0f);
                _shadow = Part("Shadow", transform, art.Circle, new Color(0f, 0f, 0f, 0.28f), _shadowOffset, new Vector2(0.86f, 0.86f), 0f, SortOrder.Shadows, art.Solid);
            }

            _parts = parts.ToArray();
            _gunT = _gun.transform;
            _partColors = new Color[_parts.Length];
            _flash = new SpriteRenderer[_parts.Length];
            for (int i = 0; i < _parts.Length; i++)
            {
                _partColors[i] = _parts[i].color;
                var p = _parts[i];
                var f = Part(p.name + "Flash", p.transform, p.sprite, new Color(1f, 1f, 1f, 0f), Vector2.zero, Vector2.one, 0f, p.sortingOrder + 20, art.Solid);
                f.enabled = false;
                _flash[i] = f;
            }

            _barRoot = new GameObject("Bar").transform;
            _barRoot.SetParent(transform, false);
            _barRoot.localPosition = new Vector3(0f, 0.72f, 0f);
            _barBg = Part("BarBg", _barRoot, art.Square, new Color(0f, 0f, 0f, 0.65f), Vector2.zero, new Vector2(0.92f, 0.13f), 0f, SortOrder.WorldUi, art.Solid);
            _barGhost = Part("BarGhost", _barRoot, art.Square, new Color(1f, 0.95f, 0.75f, 0.9f), Vector2.zero, new Vector2(0.86f, 0.08f), 0f, SortOrder.WorldUi + 1, art.Solid);
            _barFill = Part("BarFill", _barRoot, art.Square, new Color(0.35f, 0.9f, 0.35f), Vector2.zero, new Vector2(0.86f, 0.08f), 0f, SortOrder.WorldUi + 2, art.Solid);
            _barArmor = Part("BarArmor", _barRoot, art.Square, new Color(0.35f, 0.7f, 1f), new Vector2(0f, -0.1f), new Vector2(0.86f, 0.04f), 0f, SortOrder.WorldUi + 2, art.Solid);

            var font = UIKit.Font;
            _nameShadow = MakeText(font, "NameShadow", new Vector3(0.015f, 0.2f - 0.015f, 0f), Color.black, SortOrder.WorldUi, out _);
            _name = MakeText(font, "Name", new Vector3(0f, 0.2f, 0f), nameColor, SortOrder.WorldUi + 1, out _nameR);
            _name.text = displayName;
            _nameShadow.text = displayName;
        }

        private TextMesh MakeText(Font font, string n, Vector3 pos, Color c, int order, out MeshRenderer mr)
        {
            var go = new GameObject(n);
            go.transform.SetParent(_barRoot, false);
            go.transform.localPosition = pos;
            var tm = go.AddComponent<TextMesh>();
            tm.font = font;
            tm.fontSize = 48;
            tm.characterSize = 0.035f;
            tm.anchor = TextAnchor.LowerCenter;
            tm.alignment = TextAlignment.Center;
            tm.fontStyle = UIKit.Strong;
            tm.color = c;
            mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = font.material;
            mr.sortingOrder = order;
            return tm;
        }

        private static SpriteRenderer Part(string n, Transform parent, Sprite s, Color c, Vector2 pos, Vector2 scale, float rot, int order, Material mat)
        {
            var go = new GameObject(n);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(0f, 0f, rot);
            go.transform.localScale = new Vector3(scale.x, scale.y, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = s;
            sr.color = c;
            sr.sortingOrder = order;
            if (mat != null) sr.sharedMaterial = mat;
            return sr;
        }

        public void SetWeapon(WeaponStats s)
        {
            Color color;
            var painted = _artMode ? WeaponArt.Get(_catalog, s.Id) : default;
            if (painted.Valid)
            {
                // Grip sits at GunBase; the muzzle ends GunLength further out, so MuzzlePoint still matches.
                float total = s.GunLength / Mathf.Max(0.2f, 1f - painted.Grip);
                float k = total / Mathf.Max(0.01f, painted.Sprite.bounds.size.x);
                _gun.sprite = painted.Sprite;
                _gunT.localScale = new Vector3(k, k * 1.05f, 1f);
                _gunCenter = (0.5f - painted.Grip) * total;
                color = painted.Tint;
            }
            else
            {
                _gun.sprite = _art.Square;
                _gunT.localScale = new Vector3(s.GunLength, s.GunWidth, 1f);
                _gunCenter = s.GunLength * 0.5f;
                color = s.GunColor;
            }
            int gi = System.Array.IndexOf(_parts, _gun);
            if (gi >= 0)
            {
                _partColors[gi] = color;
                _flash[gi].sprite = _gun.sprite;
            }
            _gun.color = WithAlpha(color, _alpha);
        }

        public void SetAim(Vector2 dir)
        {
            float ang = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            var rot = Quaternion.Euler(0f, 0f, ang);
            _rig.localRotation = rot;
            if (_artMode) _shadow.transform.localRotation = rot;
        }

        public void Recoil(float amount) => _recoil = Mathf.Min(_recoil + amount, 0.3f);

        public void OnHit()
        {
            _flashT = 0.09f;
            _shakeT = 0.14f;
        }

        public void Swing() => _swingT = 0.22f;

        public void SetVisible(bool visible) => _targetAlpha = visible ? 1f : 0f;

        /// <summary>Extra fade on top of line of sight (a cloaked fighter seen by its own side).</summary>
        public float AlphaScale = 1f;
        private Color _tint = Color.white;
        private bool _tintDirty;

        /// <summary>Color wash over the soldier (frozen blue, rage red); white = none.</summary>
        public void SetTint(Color t)
        {
            if (t == _tint) return;
            _tint = t;
            _tintDirty = true;
        }

        public void SetBarVisible(bool visible) => _barVisible = visible;

        public void SetDead(bool dead)
        {
            _dead = dead;
            _deadTime = 0f;
            _group.sortingOrder = dead ? SortOrder.Corpses : SortOrder.Fighters;
            _rig.localScale = dead ? new Vector3(1.08f, 1.08f, 1f) : Vector3.one;
            _gun.enabled = !dead;
            _flashT = 0f;
            ApplyColors();
        }

        public void SnapBars(float hp01)
        {
            _shownHp = _ghostHp = hp01;
        }

        /// <summary>Called every frame by the fighter.</summary>
        public void Tick(float dt, float speed01, float hp01, float armor01, bool protectedBlink)
        {
            _recoil *= Mathf.Exp(-16f * dt);
            float swingOffset = 0f;
            if (_swingT > 0f)
            {
                _swingT -= dt;
                swingOffset = Mathf.Sin((1f - _swingT / 0.22f) * Mathf.PI) * 0.28f;
            }

            if (!_dead)
            {
                bool moving = speed01 > 0.1f;
                _phase += dt * (moving ? 13f * speed01 : 3f);
                float bob = moving ? Mathf.Sin(_phase) * 0.03f * speed01 : 0f;
                float breathe = 1f + Mathf.Sin(_phase) * (moving ? 0.012f : 0.018f);
                _body.localScale = new Vector3(breathe, breathe, 1f);
                _body.localRotation = Quaternion.Euler(0f, 0f, moving ? Mathf.Sin(_phase) * 4f * speed01 : 0f);
                _body.localPosition = new Vector3(-_recoil * 0.35f, 0f, 0f);
                _gunT.localPosition = new Vector3(GunBase + _gunCenter - _recoil + swingOffset, bob, 0f);
                if (_handL != null)
                {
                    _handL.localPosition = _handLBase + new Vector3(-_recoil + swingOffset, bob, 0f);
                    _handR.localPosition = _handRBase + new Vector3(-_recoil + swingOffset, -bob, 0f);
                }
            }

            if (_shakeT > 0f)
            {
                _shakeT -= dt;
                float k = _shakeT / 0.14f * 0.07f;
                _rig.localPosition = new Vector3(Random.Range(-k, k), Random.Range(-k, k), 0f);
            }
            else if (_rig.localPosition != Vector3.zero) _rig.localPosition = Vector3.zero;

            if (_flashT > 0f)
            {
                _flashT -= dt;
                float a = Mathf.Clamp01(_flashT / 0.09f) * _alpha;
                for (int i = 0; i < _flash.Length; i++)
                {
                    _flash[i].enabled = a > 0.01f && _parts[i].enabled;
                    _flash[i].color = new Color(1f, 1f, 1f, a);
                }
            }
            else if (_flash[0].enabled)
            {
                for (int i = 0; i < _flash.Length; i++) _flash[i].enabled = false;
            }

            float target = _targetAlpha * AlphaScale;
            if (_dead)
            {
                _deadTime += dt;
                target *= Mathf.Clamp01(1f - (_deadTime - 2.2f) / 0.8f);
            }
            _alpha = Mathf.MoveTowards(_alpha, target, dt * 7f);
            float blink = protectedBlink && Mathf.Sin(Time.time * 30f) > 0f ? 0.45f : 1f;
            if (_tintDirty || !Mathf.Approximately(_alpha * blink, _appliedAlpha)) ApplyColors(blink);

            _shownHp = Mathf.MoveTowards(_shownHp, hp01, dt * 3f);
            if (hp01 < _ghostHp - 0.001f)
            {
                _ghostDelay -= dt;
                if (_ghostDelay <= 0f) _ghostHp = Mathf.MoveTowards(_ghostHp, hp01, dt * 1.2f);
            }
            else
            {
                _ghostHp = hp01;
                _ghostDelay = 0.35f;
            }
            bool showBar = _barVisible && !_dead && _alpha > 0.05f;
            if (_barRoot.gameObject.activeSelf != showBar) _barRoot.gameObject.SetActive(showBar);
            if (showBar)
            {
                SetBar(_barFill.transform, _shownHp, 0.86f, 0f);
                SetBar(_barGhost.transform, _ghostHp, 0.86f, 0f);
                SetBar(_barArmor.transform, armor01, 0.86f, -0.1f);
                _barArmor.enabled = armor01 > 0.001f;
                _barFill.color = WithAlpha(HealthColor(_shownHp), _alpha);
            }
        }

        public static Color HealthColor(float t)
        {
            var green = new Color(0.35f, 0.92f, 0.35f);
            var yellow = new Color(1f, 0.84f, 0.25f);
            var red = new Color(1f, 0.28f, 0.22f);
            return t > 0.5f ? Color.Lerp(yellow, green, (t - 0.5f) * 2f) : Color.Lerp(red, yellow, t * 2f);
        }

        private static void SetBar(Transform t, float v, float width, float y)
        {
            v = Mathf.Clamp01(v);
            t.localScale = new Vector3(Mathf.Max(0.0001f, width * v), t.localScale.y, 1f);
            t.localPosition = new Vector3(-width * 0.5f + width * v * 0.5f, y, 0f);
        }

        private void ApplyColors(float blink = 1f)
        {
            float a = _alpha * blink;
            _appliedAlpha = a;
            _tintDirty = false;
            for (int i = 0; i < _parts.Length; i++)
            {
                Color c = _partColors[i];
                if (_dead) c = new Color(c.r * 0.42f, c.g * 0.42f, c.b * 0.42f, c.a);
                else c = new Color(c.r * _tint.r, c.g * _tint.g, c.b * _tint.b, c.a);
                c.a *= a;
                _parts[i].color = c;
            }
            _shadow.color = new Color(0f, 0f, 0f, (_artMode ? 0.3f : 0.28f) * _alpha);
            if (_nameR != null)
            {
                _name.color = WithAlpha(_nameColor, _alpha);
                _nameShadow.color = new Color(0f, 0f, 0f, 0.8f * _alpha);
            }
        }

        private static Color WithAlpha(Color c, float a) { c.a *= a; return c; }
    }
}
