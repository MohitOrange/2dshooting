using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace NeonBrawl
{
    public sealed class Pickup
    {
        /// <summary>Same on every device: map spots count up from 0, dropped weapons from 1000.</summary>
        public int Id;
        public float RequestT;
        public PickupType Type;
        public WeaponId Weapon;
        public Vector2 Position;
        public bool Active;
        public bool Dropped;
        public float Timer;
        public int Mag = -1;
        public int Reserve = int.MinValue;
        public float Phase;
        public GameObject Go;
        public Transform Icon;
        public SpriteRenderer Glow;
        public SpriteRenderer Ring;
    }

    /// <summary>
    /// Weapon/health/armor/ammo/grenade pickups that respawn (neon-brawl: 14 s), plus weapons dropped by
    /// dead fighters. Host-authoritative: offline and on the host this decides everything; on an online
    /// client (<see cref="Authority"/> false) it only animates, asks the host for what the local player
    /// stands on, and applies what the host says.
    /// </summary>
    public sealed class PickupSystem
    {
        private readonly GameContext _ctx;
        private readonly Transform _root;
        private readonly List<Pickup> _items = new List<Pickup>(32);
        private readonly Font _font;
        private int _nextSpot;
        private int _nextDrop = 1000;

        /// <summary>False on online clients: the host decides who gets what.</summary>
        public bool Authority = true;
        /// <summary>Host: a pickup was taken (fires before it disappears).</summary>
        public event Action<Pickup, Fighter> Taken;
        public event Action<Pickup> Dropped;
        public event Action<Pickup> Respawned;

        public PickupSystem(GameContext ctx, Transform root)
        {
            _ctx = ctx;
            _root = root;
            _font = UIKit.Font;
        }

        public IReadOnlyList<Pickup> Items => _items;

        public void AddSpot(PickupSpot spot) => Create(spot.Type, spot.Weapon, spot.Position, false, _nextSpot++);

        public void Drop(WeaponSlot slot, Vector2 pos)
        {
            if (slot == null || slot.Dry || !Authority) return;
            var p = Create(PickupType.Weapon, slot.Stats.Id, pos, true, _nextDrop++);
            p.Mag = slot.Mag;
            p.Reserve = slot.Reserve;
            p.Timer = _ctx.Config.Player.DroppedWeaponLife;
            Dropped?.Invoke(p);
        }

        public Pickup Find(int id)
        {
            for (int i = 0; i < _items.Count; i++)
                if (_items[i].Id == id) return _items[i];
            return null;
        }

        public Pickup FindNearest(Vector2 from, PickupType type, float maxDist)
        {
            Pickup best = null;
            float bestD = maxDist * maxDist;
            for (int i = 0; i < _items.Count; i++)
            {
                var p = _items[i];
                if (!p.Active || p.Type != type) continue;
                float d = (p.Position - from).sqrMagnitude;
                if (d < bestD) { bestD = d; best = p; }
            }
            return best;
        }

        public void Tick(List<Fighter> fighters, float dt)
        {
            for (int i = _items.Count - 1; i >= 0; i--)
            {
                var p = _items[i];
                if (!p.Active)
                {
                    if (!Authority) continue; // the host says when it is back
                    p.Timer -= dt;
                    if (p.Timer <= 0f)
                    {
                        SetActive(p, true);
                        _ctx.Fx.PickupSparkle(p.Position, TypeColor(p.Type));
                        Respawned?.Invoke(p);
                    }
                    continue;
                }
                if (p.Dropped)
                {
                    p.Timer -= dt;
                    if (p.Timer <= 0f)
                    {
                        Object.Destroy(p.Go);
                        _items.RemoveAt(i);
                        continue;
                    }
                    if (p.Timer < 3f) p.Go.SetActive(Mathf.Sin(p.Timer * 20f) > -0.3f);
                }
                p.Phase += dt;
                p.Icon.localPosition = new Vector3(0f, Mathf.Sin(p.Phase * 3f) * 0.07f, 0f);
                float pulse = 1f + Mathf.Sin(p.Phase * 4f) * 0.08f;
                p.Glow.transform.localScale = new Vector3(1.3f * pulse, 1.3f * pulse, 1f);
                p.Ring.transform.Rotate(0f, 0f, 60f * dt);
            }

            float r2 = _ctx.Config.Player.PickupRadius * _ctx.Config.Player.PickupRadius;
            if (!Authority)
            {
                AskHost(r2);
                return;
            }
            for (int f = 0; f < fighters.Count; f++)
            {
                var fighter = fighters[f];
                fighter.NearbyWeaponPickup = null;
                if (!fighter.Alive || fighter.IsPuppet) continue; // remote players ask for theirs
                float mr = fighter.Powers.MagnetRadius, m2 = mr * mr;
                for (int i = 0; i < _items.Count; i++)
                {
                    var p = _items[i];
                    if (!p.Active) continue;
                    float d2 = (p.Position - fighter.Position).sqrMagnitude;
                    // LOOT MAGNET pulls health, armor, ammo and grenades from further away (guns still need you there)
                    if (d2 > r2 && (d2 > m2 || p.Type == PickupType.Weapon)) continue;
                    if (TryCollect(fighter, p)) break;
                }
            }
        }

        /// <summary>Player pressed PICK UP while standing on a different weapon.</summary>
        public bool TrySwapWeapon(Fighter f)
        {
            var p = f.NearbyWeaponPickup;
            if (p == null || !p.Active || !f.Alive) return false;
            if (!Authority)
            {
                if (Time.unscaledTime < p.RequestT) return false;
                p.RequestT = Time.unscaledTime + 0.4f;
                _ctx.Match?.Net?.RequestPickup(p, true, f.Weapons.Primary);
                return true;
            }
            Swap(f, p);
            return true;
        }

        // ------------------------------------------------------------------ online

        /// <summary>Client: ask the host for whatever the local player stands on and could use.</summary>
        private void AskHost(float r2)
        {
            var me = _ctx.Match != null ? _ctx.Match.Local : null;
            if (me == null) return;
            me.NearbyWeaponPickup = null;
            if (!me.Alive) return;
            float mr = me.Powers.MagnetRadius, m2 = mr * mr;
            for (int i = 0; i < _items.Count; i++)
            {
                var p = _items[i];
                if (!p.Active) continue;
                float d2 = (p.Position - me.Position).sqrMagnitude;
                if (d2 > r2 && (d2 > m2 || p.Type == PickupType.Weapon)) continue;
                if (!WouldCollect(me, p)) continue;
                if (Time.unscaledTime >= p.RequestT)
                {
                    p.RequestT = Time.unscaledTime + 0.4f;
                    _ctx.Match.Net?.RequestPickup(p, false, null);
                }
                return;
            }
        }

        /// <summary>Same rules as <see cref="TryCollect"/>, without changing anything.</summary>
        private bool WouldCollect(Fighter f, Pickup p)
        {
            var t = _ctx.Config.Player;
            var prim = f.Weapons.Primary;
            switch (p.Type)
            {
                case PickupType.Health: return f.Health < f.MaxHealth;
                case PickupType.Armor: return f.Armor < t.MaxArmor;
                case PickupType.Ammo: return f.Grenades < t.MaxGrenades || (prim != null && !prim.Stats.InfiniteReserve && prim.Reserve < prim.Stats.Reserve);
                case PickupType.Grenade: return f.Grenades < t.MaxGrenades;
                case PickupType.Weapon:
                    if (prim == null) return true;
                    if (prim.Stats.Id == p.Weapon) return !prim.Stats.InfiniteReserve && prim.Reserve < prim.Stats.Reserve;
                    f.NearbyWeaponPickup = p;
                    return false;
            }
            return false;
        }

        /// <summary>Host: a remote player asked for a pickup. Health and armor are the host's numbers;
        /// ammo, grenades and guns are applied by that player's device when the answer arrives.</summary>
        public void HostGrant(Fighter f, int id, bool swap, WeaponId oldWeapon, int oldMag, int oldReserve)
        {
            var p = Find(id);
            if (p == null || !p.Active || f == null || !f.Alive) return;
            float r = Mathf.Max(_ctx.Config.Player.PickupRadius, p.Type == PickupType.Weapon ? 0f : f.Powers.MagnetRadius) + 1.2f;
            if ((p.Position - f.NetLatestPos).sqrMagnitude > r * r) return;
            var t = _ctx.Config.Player;
            switch (p.Type)
            {
                case PickupType.Health:
                    if (!f.Heal(t.HealthPickup)) return;
                    break;
                case PickupType.Armor:
                    if (!f.AddArmor(t.ArmorPickup)) return;
                    break;
                case PickupType.Weapon:
                {
                    var prim = f.Weapons.Primary;
                    if (prim == null || prim.Stats.Id != p.Weapon) f.Weapons.GivePrimary(p.Weapon, p.Mag, p.Reserve);
                    if (swap && oldWeapon != p.Weapon && oldWeapon > WeaponId.Pistol && oldWeapon < WeaponId.Knife)
                    {
                        var old = new WeaponSlot(_ctx.Config.Get(oldWeapon)) { Mag = oldMag, Reserve = oldReserve };
                        Drop(old, f.NetLatestPos - f.AimDir * 0.7f);
                    }
                    break;
                }
            }
            Collected(f, p, null);
        }

        /// <summary>Client: the host gave pickup <paramref name="id"/> to <paramref name="f"/>.</summary>
        public void NetTaken(int id, Fighter f, PickupType type, WeaponId weapon, int mag, int reserve)
        {
            var p = Find(id);
            bool mine = f != null && _ctx.Match != null && f == _ctx.Match.Local;
            string toast = null;
            if (mine)
            {
                var t = _ctx.Config.Player;
                switch (type)
                {
                    case PickupType.Health: toast = "+" + (int)t.HealthPickup + " HP"; break;
                    case PickupType.Armor: toast = "+ARMOR"; break;
                    case PickupType.Ammo:
                        f.Weapons.RefillAmmo();
                        if (f.Grenades < t.MaxGrenades) f.Grenades++;
                        toast = "+AMMO";
                        break;
                    case PickupType.Grenade:
                        f.Grenades = Mathf.Min(t.MaxGrenades, f.Grenades + 2);
                        toast = "+GRENADES";
                        break;
                    case PickupType.Weapon:
                    {
                        var prim = f.Weapons.Primary;
                        if (prim != null && prim.Stats.Id == weapon) { f.Weapons.RefillAmmo(); toast = "+AMMO"; }
                        else { f.Weapons.GivePrimary(weapon, mag, reserve); toast = _ctx.Config.Get(weapon).DisplayName; }
                        break;
                    }
                }
            }
            if (p != null) Collected(f, p, toast);
            else if (toast != null) { _ctx.Events.RaiseToast(toast); _ctx.Sfx.PlayUi(SfxId.Pickup, 0.8f); }
        }

        public void NetRespawn(int id)
        {
            var p = Find(id);
            if (p == null || p.Active) return;
            SetActive(p, true);
            _ctx.Fx.PickupSparkle(p.Position, TypeColor(p.Type));
        }

        /// <summary>Client: a weapon lying on the floor (dropped, or already there when joining late).</summary>
        public void NetDrop(int id, WeaponId weapon, Vector2 pos, int mag, int reserve, float life)
        {
            if (Find(id) != null) return;
            var p = Create(PickupType.Weapon, weapon, pos, true, id);
            p.Mag = mag;
            p.Reserve = reserve;
            p.Timer = life;
        }

        /// <summary>Late join: whether a map spot is up right now.</summary>
        public void NetSetActive(int id, bool active)
        {
            var p = Find(id);
            if (p != null && !p.Dropped && p.Active != active) SetActive(p, active);
        }

        private bool TryCollect(Fighter f, Pickup p)
        {
            var t = _ctx.Config.Player;
            switch (p.Type)
            {
                case PickupType.Health:
                    if (!f.Heal(t.HealthPickup)) return false;
                    Collected(f, p, "+" + (int)t.HealthPickup + " HP");
                    return true;
                case PickupType.Armor:
                    if (!f.AddArmor(t.ArmorPickup)) return false;
                    Collected(f, p, "+ARMOR");
                    return true;
                case PickupType.Ammo:
                {
                    bool ammo = f.Weapons.RefillAmmo();
                    bool nade = false;
                    if (f.Grenades < t.MaxGrenades) { f.Grenades++; nade = true; }
                    if (!ammo && !nade) return false;
                    Collected(f, p, "+AMMO");
                    return true;
                }
                case PickupType.Grenade:
                    if (f.Grenades >= t.MaxGrenades) return false;
                    f.Grenades = Mathf.Min(t.MaxGrenades, f.Grenades + 2);
                    Collected(f, p, "+GRENADES");
                    return true;
                case PickupType.Weapon:
                {
                    var prim = f.Weapons.Primary;
                    if (prim == null)
                    {
                        f.Weapons.GivePrimary(p.Weapon, p.Mag, p.Reserve);
                        Collected(f, p, _ctx.Config.Get(p.Weapon).DisplayName);
                        return true;
                    }
                    if (prim.Stats.Id == p.Weapon)
                    {
                        if (!f.Weapons.RefillAmmo()) return false;
                        Collected(f, p, "+AMMO");
                        return true;
                    }
                    if (f.IsBot)
                    {
                        if (_ctx.Config.Get(p.Weapon).Rank > prim.Stats.Rank || prim.Dry)
                        {
                            Swap(f, p);
                            return true;
                        }
                        return false;
                    }
                    f.NearbyWeaponPickup = p;
                    return false;
                }
            }
            return false;
        }

        private void Swap(Fighter f, Pickup p)
        {
            var old = f.Weapons.GivePrimary(p.Weapon, p.Mag, p.Reserve);
            Collected(f, p, _ctx.Config.Get(p.Weapon).DisplayName);
            if (old != null) Drop(old, f.Position - f.AimDir * 0.7f);
        }

        private void Collected(Fighter f, Pickup p, string toast)
        {
            if (Authority) Taken?.Invoke(p, f);
            _ctx.Fx.PickupSparkle(p.Position, TypeColor(p.Type));
            if (f != null && f.Powers.MagnetRadius > 0f && (p.Position - f.Position).sqrMagnitude > 2f) MagnetPull(p, f);
            bool local = f != null && f.IsLocal;
            _ctx.Sfx.Play(SfxId.Pickup, p.Position, local ? 1f : 0.5f);
            if (local && toast != null)
            {
                _ctx.Events.RaiseToast(toast);
                _ctx.Haptics.Pulse(15);
            }
            if (p.Dropped)
            {
                Object.Destroy(p.Go);
                _items.Remove(p);
            }
            else
            {
                SetActive(p, false);
                p.Timer = _ctx.Config.Player.PickupRespawn;
            }
        }

        /// <summary>LOOT MAGNET: the item streaks across the floor into the player.</summary>
        private void MagnetPull(Pickup p, Fighter f)
        {
            var art = _ctx.Art;
            Color c = Powers.Get(PowerId.Magnet).Color;
            Vector2 to = f.Position - p.Position;
            float ang = Mathf.Atan2(to.y, to.x) * Mathf.Rad2Deg;
            for (int k = 0; k < 5; k++)
                _ctx.Fx.Spawn(art.Glow, art.Additive, p.Position + to * (k * 0.12f), 0f, to * 3.2f, 0.28f, 0.55f - k * 0.06f, 0.15f, TypeColor(p.Type), new Color(c.r, c.g, c.b, 0f), SortOrder.FxTop, 1f);
            _ctx.Fx.Spawn(art.Spark, art.Additive, p.Position + to * 0.5f, ang, Vector2.zero, 0.25f, 1f, 0.3f, Color.white, new Color(c.r, c.g, c.b, 0f), SortOrder.FxTop, 0f, 0f, new Vector2(to.magnitude * 0.9f, 0.35f));
        }

        private static void SetActive(Pickup p, bool active)
        {
            p.Active = active;
            p.Go.SetActive(active);
        }

        public static Color TypeColor(PickupType t)
        {
            switch (t)
            {
                case PickupType.Weapon: return new Color(1f, 0.7f, 0.2f);
                case PickupType.Health: return new Color(0.3f, 1f, 0.45f);
                case PickupType.Armor: return new Color(0.35f, 0.65f, 1f);
                case PickupType.Ammo: return new Color(1f, 0.9f, 0.3f);
                default: return new Color(0.6f, 1f, 0.3f);
            }
        }

        private Pickup Create(PickupType type, WeaponId weapon, Vector2 pos, bool dropped, int id)
        {
            var art = _ctx.Art;
            var go = new GameObject("Pickup_" + type);
            go.transform.SetParent(_root, false);
            go.transform.position = pos;
            Color tc = TypeColor(type);
            var p = new Pickup { Id = id, Type = type, Weapon = weapon, Position = pos, Dropped = dropped, Active = true, Go = go, Phase = Random.value * 6f };
            p.Glow = SR(go.transform, art.Glow, art.Additive, new Color(tc.r, tc.g, tc.b, 0.45f), Vector2.zero, new Vector2(1.3f, 1.3f), 0f, SortOrder.Pickups);
            p.Ring = SR(go.transform, art.ThinRing, art.Additive, new Color(tc.r, tc.g, tc.b, 0.7f), Vector2.zero, new Vector2(0.95f, 0.95f), 0f, SortOrder.Pickups + 1);
            p.Icon = new GameObject("Icon").transform;
            p.Icon.SetParent(go.transform, false);
            int o = SortOrder.Pickups + 2;
            Sprite icon = IconFor(type);
            if (icon != null)
            {
                SR(p.Icon, icon, art.Lit, Color.white, Vector2.zero, Vector2.one, 0f, o);
                _items.Add(p);
                return p;
            }
            switch (type)
            {
                case PickupType.Weapon:
                {
                    var s = _ctx.Config.Get(weapon);
                    var gun = WeaponArt.Get(_ctx.Catalog, weapon);
                    if (gun.Valid)
                    {
                        float k = Mathf.Clamp(s.GunLength * 1.6f, 0.75f, 1.15f) / Mathf.Max(0.01f, gun.Sprite.bounds.size.x);
                        SR(p.Icon, gun.Sprite, art.Lit, gun.Tint, Vector2.zero, new Vector2(k, k), 18f, o);
                    }
                    else SR(p.Icon, art.Square, art.Lit, s.GunColor, Vector2.zero, new Vector2(s.GunLength * 1.5f, s.GunWidth * 1.8f), 30f, o);
                    var label = new GameObject("Label");
                    label.transform.SetParent(go.transform, false);
                    label.transform.localPosition = new Vector3(0f, -0.42f, 0f);
                    var tm = label.AddComponent<TextMesh>();
                    tm.font = _font;
                    tm.text = s.DisplayName;
                    tm.fontSize = 40;
                    tm.characterSize = 0.028f;
                    tm.anchor = TextAnchor.UpperCenter;
                    tm.alignment = TextAlignment.Center;
                    tm.fontStyle = UIKit.Strong;
                    tm.color = new Color(1f, 0.92f, 0.7f);
                    var mr = label.GetComponent<MeshRenderer>();
                    mr.sharedMaterial = _font.material;
                    mr.sortingOrder = o + 1;
                    break;
                }
                case PickupType.Health:
                    SR(p.Icon, art.Circle, art.Lit, Color.white, Vector2.zero, new Vector2(0.55f, 0.55f), 0f, o);
                    SR(p.Icon, art.Cross, art.Lit, new Color(0.9f, 0.18f, 0.2f), Vector2.zero, new Vector2(0.4f, 0.4f), 0f, o + 1);
                    break;
                case PickupType.Armor:
                    SR(p.Icon, art.Circle, art.Lit, new Color(0.25f, 0.5f, 0.95f), Vector2.zero, new Vector2(0.55f, 0.55f), 0f, o);
                    SR(p.Icon, art.Ring, art.Lit, Color.white, Vector2.zero, new Vector2(0.55f, 0.55f), 0f, o + 1);
                    break;
                case PickupType.Ammo:
                    SR(p.Icon, art.Square, art.Lit, new Color(0.42f, 0.42f, 0.22f), Vector2.zero, new Vector2(0.52f, 0.36f), 0f, o);
                    SR(p.Icon, art.Square, art.Lit, new Color(1f, 0.85f, 0.25f), Vector2.zero, new Vector2(0.52f, 0.08f), 0f, o + 1);
                    break;
                case PickupType.Grenade:
                    SR(p.Icon, art.Circle, art.Lit, new Color(0.24f, 0.36f, 0.2f), new Vector2(-0.12f, 0f), new Vector2(0.3f, 0.3f), 0f, o);
                    SR(p.Icon, art.Circle, art.Lit, new Color(0.24f, 0.36f, 0.2f), new Vector2(0.14f, 0.02f), new Vector2(0.3f, 0.3f), 0f, o);
                    break;
            }
            _items.Add(p);
            return p;
        }

        /// <summary>Painted icon from the art catalog, or null to draw the placeholder shape.</summary>
        private Sprite IconFor(PickupType type)
        {
            var cat = _ctx.Catalog;
            if (cat == null) return null;
            switch (type)
            {
                case PickupType.Health: return cat.PickupHealth;
                case PickupType.Armor: return cat.PickupArmor;
                case PickupType.Ammo: return cat.PickupAmmo;
                case PickupType.Grenade: return cat.PickupGrenade;
                default: return null;
            }
        }

        private static SpriteRenderer SR(Transform parent, Sprite s, Material m, Color c, Vector2 pos, Vector2 scale, float rot, int order)
        {
            var go = new GameObject("Sprite");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(0f, 0f, rot);
            go.transform.localScale = new Vector3(scale.x, scale.y, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = s;
            sr.sharedMaterial = m;
            sr.color = c;
            sr.sortingOrder = order;
            return sr;
        }
    }
}
