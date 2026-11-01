using UnityEngine;
using UnityEngine.UI;

namespace NeonBrawl
{
    /// <summary>
    /// Battle feedback layered on the HUD:
    /// red arcs around your fighter that point at whoever is shooting you,
    /// the "ELIMINATED" banner for your kills (REVENGE on the player who last killed you,
    /// SHUTDOWN when you end a streak), and a dark wash while you wait to respawn.
    /// Everything is pooled and built once; nothing allocates per hit.
    /// </summary>
    public sealed class HudFeedback : MonoBehaviour
    {
        private const int ArcCount = 5;
        private const float ArcFill = 0.12f;                    // share of the ring one arc covers
        private const float ArcCenter = 90f - ArcFill * 180f;   // where the filled arc sits before rotation
        private const float ArcLife = 1.15f;
        private const float BannerLife = 2.3f;

        private sealed class Arc
        {
            public RectTransform Rt;
            public CanvasGroup Group;
            public float T, Angle, Strength;
        }

        private GameContext _ctx;
        private MatchController _match;
        private RectTransform _self, _arcRoot, _banner;
        private Arc[] _arcs;
        private CanvasGroup _bannerGroup;
        private Image _bannerFlash, _bannerGun, _bannerStripe, _deathDim;
        private Text _bannerHead, _bannerName, _bannerXp;
        private Vector2 _bannerHome;
        private float _bannerT = -1f, _deathA;
        private Fighter _nemesis;

        public static HudFeedback Build(RectTransform root, GameContext ctx)
        {
            var rt = UIKit.Rect(root, "Feedback");
            UIKit.Stretch(rt);
            var f = rt.gameObject.AddComponent<HudFeedback>();
            f.Init(ctx, rt);
            return f;
        }

        private void Init(GameContext ctx, RectTransform self)
        {
            _ctx = ctx;
            _self = self;
            var art = ctx.Art;
            var cat = ctx.Catalog;

            _deathDim = UIKit.Image(self, "DeathDim", art.Square, new Color(0.1f, 0.02f, 0.03f, 0f));
            UIKit.Stretch(_deathDim.rectTransform, -20f);
            _deathDim.enabled = false;

            // hit-direction arcs (a ring slice plus a soft glow), centered on the local fighter
            _arcRoot = UIKit.Rect(self, "Arcs");
            UIKit.Place(_arcRoot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            _arcs = new Arc[ArcCount];
            float mid = ArcCenter * Mathf.Deg2Rad;
            for (int i = 0; i < ArcCount; i++)
            {
                var holder = UIKit.Rect(_arcRoot, "Arc" + i);
                UIKit.Place(holder, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(470f, 470f));
                var glow = UIKit.Image(holder, "Glow", art.Glow, new Color(1f, 0.08f, 0.04f, 0.7f));
                UIKit.Place(glow.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(Mathf.Cos(mid), Mathf.Sin(mid)) * 220f, new Vector2(250f, 120f));
                glow.rectTransform.localRotation = Quaternion.Euler(0f, 0f, ArcCenter - 90f);
                ArcRing(holder, art.Ring, new Color(1f, 0.12f, 0.08f, 0.4f), 0f);      // soft body
                ArcRing(holder, art.ThinRing, new Color(1f, 0.55f, 0.45f, 1f), 12f); // bright core
                var group = holder.gameObject.AddComponent<CanvasGroup>();
                group.alpha = 0f;
                group.blocksRaycasts = false;
                group.interactable = false;
                _arcs[i] = new Arc { Rt = holder, Group = group };
            }

            // kill banner, under your fighter
            _banner = UIKit.Rect(self, "KillBanner");
            // sits left of center so the right-hand touch buttons (DASH, NADE) never cover it
            UIKit.Place(_banner, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-80f, -240f), new Vector2(620f, 108f));
            _bannerHome = _banner.anchoredPosition;
            _bannerGroup = _banner.gameObject.AddComponent<CanvasGroup>();
            _bannerGroup.alpha = 0f;
            _bannerGroup.blocksRaycasts = false;
            _bannerGroup.interactable = false;
            var bg = UIKit.Panel9(_banner, "Bg", ArtIds.PanelDark, new Color(1f, 1f, 1f, 0.95f), 2.4f);
            UIKit.Stretch(bg.rectTransform);
            _bannerStripe = UIKit.Image(_banner, "Stripe", art.RoundRect, UIKit.Red, true);
            UIKit.Place(_bannerStripe.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(14f, 0f), new Vector2(12f, 74f));
            _bannerGun = UIKit.Icon(_banner, "Gun", null, new Vector2(150f, 62f));
            UIKit.Place(_bannerGun.rectTransform, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(104f, 0f), new Vector2(150f, 62f));
            _bannerHead = UIKit.Label(_banner, "Head", "ELIMINATED", 24, UIKit.Gold, TextAnchor.LowerLeft);
            UIKit.Place(_bannerHead.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(190f, 20f), new Vector2(290f, 32f));
            _bannerName = UIKit.Title(_banner, "Name", "", 44, Color.white, UIKit.Cream, TextAnchor.UpperLeft);
            UIKit.Place(_bannerName.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(188f, -14f), new Vector2(290f, 54f));
            _bannerName.horizontalOverflow = HorizontalWrapMode.Wrap;
            _bannerName.verticalOverflow = VerticalWrapMode.Truncate;
            _bannerName.resizeTextForBestFit = true;
            _bannerName.resizeTextMinSize = 24;
            _bannerName.resizeTextMaxSize = 44;
            var star = UIKit.Icon(_banner, "Star", cat != null ? cat.Icon(ArtIds.IconStar) : art.Star, new Vector2(44f, 44f),
                cat != null && cat.Icon(ArtIds.IconStar) != null ? (Color?)null : UIKit.Gold);
            UIKit.Place(star.rectTransform, new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-118f, 0f), new Vector2(44f, 44f));
            _bannerXp = UIKit.Label(_banner, "Xp", "+" + MatchReward.XpPerKill + " XP", 30, UIKit.Sky, TextAnchor.MiddleLeft);
            UIKit.Place(_bannerXp.rectTransform, new Vector2(1f, 0.5f), new Vector2(0f, 0.5f), new Vector2(-92f, 0f), new Vector2(90f, 44f));
            _bannerFlash = UIKit.Image(_banner, "Flash", art.Glow, new Color(1f, 1f, 1f, 0f));
            UIKit.Stretch(_bannerFlash.rectTransform, -50f);

            ctx.Events.Killed += OnKilled;
            ctx.Events.LocalDamaged += OnLocalDamaged;
        }

        private static void ArcRing(RectTransform holder, Sprite sprite, Color color, float inset)
        {
            var ring = UIKit.Image(holder, "Ring", sprite, color);
            UIKit.Stretch(ring.rectTransform, inset);
            ring.type = Image.Type.Filled;
            ring.fillMethod = Image.FillMethod.Radial360;
            ring.fillOrigin = (int)Image.Origin360.Top;
            ring.fillClockwise = true;
            ring.fillAmount = ArcFill;
        }

        private void OnDestroy()
        {
            if (_ctx == null) return;
            _ctx.Events.Killed -= OnKilled;
            _ctx.Events.LocalDamaged -= OnLocalDamaged;
        }

        public void Bind(MatchController match)
        {
            _match = match;
            _nemesis = null;
            _bannerT = -1f;
            _bannerGroup.alpha = 0f;
            _deathA = 0f;
            _deathDim.enabled = false;
            for (int i = 0; i < _arcs.Length; i++) { _arcs[i].T = 0f; _arcs[i].Group.alpha = 0f; }
        }

        // ------------------------------------------------------------- events

        private void OnLocalDamaged(float amount, Vector2 from)
        {
            if (_match == null || _match.Spectator || from.sqrMagnitude < 0.0001f) return;
            float ang = Mathf.Atan2(from.y, from.x) * Mathf.Rad2Deg;
            Arc pick = null;
            for (int i = 0; i < _arcs.Length; i++)
            {
                var a = _arcs[i];
                if (a.T > 0f && Mathf.Abs(Mathf.DeltaAngle(a.Angle, ang)) < 28f) { pick = a; break; }
            }
            if (pick == null)
            {
                pick = _arcs[0];
                for (int i = 1; i < _arcs.Length; i++)
                    if (_arcs[i].T < pick.T) pick = _arcs[i];
            }
            pick.Angle = ang;
            pick.T = ArcLife;
            pick.Strength = Mathf.Clamp01(0.6f + amount * 0.02f);
            pick.Rt.localRotation = Quaternion.Euler(0f, 0f, ang - ArcCenter);
        }

        private void OnKilled(KillInfo k)
        {
            if (_match == null || _match.Spectator || k.Victim == null) return;
            bool selfKill = k.Killer == null || k.Killer == k.Victim;
            if (k.Victim.IsLocal)
            {
                if (!selfKill) _nemesis = k.Killer;
                return;
            }
            if (selfKill || !k.Killer.IsLocal || k.Outcome.TeamKill) return;

            bool revenge = _nemesis != null && k.Victim == _nemesis;
            bool shutdown = k.Outcome.VictimStreakEnded >= 3;
            if (revenge)
            {
                _nemesis = null;
                _ctx.Events.Announce("REVENGE!", new Color(1f, 0.55f, 0.15f), true);
                _ctx.Sfx.PlayUi(SfxId.UiLevelUp, 0.55f);
            }
            ShowBanner(k, revenge, shutdown);
        }

        private void ShowBanner(KillInfo k, bool revenge, bool shutdown)
        {
            _bannerHead.text = revenge ? "REVENGE!" : shutdown ? "SHUTDOWN!" : "ELIMINATED";
            Color accent = revenge ? UIKit.Orange : shutdown ? new Color(0.75f, 0.45f, 1f) : UIKit.Red;
            _bannerHead.color = revenge || shutdown ? accent : UIKit.Gold;
            _bannerStripe.color = accent;
            _bannerName.text = k.Victim.DisplayName.ToUpperInvariant();
            var gun = WeaponArt.Get(_ctx.Catalog, k.Weapon);
            Sprite icon = gun.Valid ? gun.Sprite
                : k.Weapon == WeaponId.Grenade && _ctx.Catalog != null ? _ctx.Catalog.PickupGrenade
                : k.Weapon == WeaponId.Barrel && _ctx.Catalog != null ? ArtCatalog.At(_ctx.Catalog.Shared, ArtIds.SharedBarrelRed) : null;
            _bannerGun.enabled = icon != null;
            _bannerGun.sprite = icon;
            _bannerGun.color = gun.Valid ? gun.Tint : Color.white;
            _bannerT = 0f;
        }

        // ------------------------------------------------------------- update

        private void LateUpdate()
        {
            if (_match == null) return;
            float udt = Time.unscaledDeltaTime;
            var local = _match.Local;

            // keep the arcs centered on the local fighter (the camera can stop at map edges)
            if (local != null && _ctx.Camera != null)
            {
                Vector2 sp = _ctx.Camera.WorldToScreenPoint(local.Position);
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_self, sp, null, out var lp))
                    _arcRoot.anchoredPosition = lp - _self.rect.center;
            }
            for (int i = 0; i < _arcs.Length; i++)
            {
                var a = _arcs[i];
                if (a.T <= 0f) continue;
                a.T -= udt;
                float age = ArcLife - a.T;
                float s = age < 0.1f ? Mathf.Lerp(1.22f, 1f, age / 0.1f) : 1f;
                a.Rt.localScale = new Vector3(s, s, 1f);
                a.Group.alpha = a.T <= 0f ? 0f : UIKit.Shade(Mathf.Clamp01(a.T / 0.5f) * a.Strength);
            }

            if (_bannerT >= 0f)
            {
                _bannerT += udt;
                float t = _bannerT;
                float s = t < 0.12f ? Mathf.Lerp(1.6f, 0.95f, t / 0.12f) : t < 0.24f ? Mathf.Lerp(0.95f, 1f, (t - 0.12f) / 0.12f) : 1f;
                _banner.localScale = new Vector3(s, s, 1f);
                float outT = BannerLife - 0.35f;
                _bannerGroup.alpha = t < 0.06f ? t / 0.06f : t > outT ? Mathf.Clamp01((BannerLife - t) / 0.35f) : 1f;
                _banner.anchoredPosition = _bannerHome + new Vector2(0f, t > outT ? -(t - outT) * 90f : 0f);
                _bannerFlash.color = new Color(1f, 1f, 1f, UIKit.Shade(Mathf.Clamp01(1f - t / 0.28f)) * 0.85f);
                if (t >= BannerLife)
                {
                    _bannerT = -1f;
                    _bannerGroup.alpha = 0f;
                }
            }

            bool dead = local != null && !local.Alive && _match.CurrentPhase == MatchController.Phase.Playing && !_match.Spectator;
            _deathA = Mathf.MoveTowards(_deathA, dead ? 1f : 0f, udt * (dead ? 1.4f : 3f));
            bool show = _deathA > 0.001f;
            if (_deathDim.enabled != show) _deathDim.enabled = show;
            if (show) _deathDim.color = new Color(0.1f, 0.02f, 0.03f, UIKit.Shade(_deathA * 0.55f));
        }
    }
}
