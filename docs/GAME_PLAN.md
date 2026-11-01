# NEON BRAWL — Top-Down Arena Shooter (Unity 6, Android first)

> Working title. Change it any time; only the logo and the store listing depend on it.

A fast, juicy top-down multiplayer shooter. The camera and feel come from classic top-down PC shooters (line of sight, flashlights, dark maps, raised walls, blood and smoke). The room flow, customization and kill race come from mobile arena shooters. Rules, bots and weapons are ported from **neon-brawl** (the web game).

The game is original: own name, characters, maps, weapons and art. No names, maps or art from other games. This also keeps the Play Store listing safe from takedowns.

---

## 1. Pillars

1. **Feels amazing to shoot.** Every shot has a flash, kick, sound, shell, light and hit reaction. Every kill has a payoff.
2. **Instant fun with friends.** Create a room, share a 5-letter code, play in under 30 seconds. Bots fill the empty slots.
3. **Readable on a phone.** Big clear silhouettes, bold colors, twin-stick controls, 60 fps on mid-range phones.
4. **Make it yours.** Dress your soldier, pick colors, edit the HUD layout, choose maps and rules.
5. **Fair.** Money buys looks and maps, never stronger guns. All weapons are picked up in the match.

## 2. What carries over from neon-brawl, what changes

| neon-brawl (web) | Unity version |
|---|---|
| Side view, gravity, jump, double jump, jetpack | **Top-down 360°** view. Jetpack/jump replaced by **dash roll** (cooldown) and cover behind walls/props |
| 5 weapons with stats | Same 5 with the same stats + 7 new (table below) |
| Room logic: host, roster, 12 max, kill limit 20, 5 min, respawn 3 s, pickups respawn 14 s | **Ported 1:1** |
| Bots easy/normal/hard (aim error, reaction, range, think rate, burst) | **Ported 1:1**, plus path finding on the map grid and cover use |
| P2P rooms via PeerJS, 5-letter codes, quick match | **Unity Relay + Sessions**: same "host creates, friends join with code" model, no server to pay for |
| Server-authoritative damage with per-weapon damage caps | Host-authoritative damage, same caps |
| Kill feed, streaks (DOUBLE KILL!), emotes, chat, scoreboard, minimap | All kept |
| Face photo on the fighter | Later, optional, as a profile picture only (Play Store requires report/block tools for user photos) |
| Synthesized sounds | Real sound effects + music |

## 3. Loops

- **Second to second:** move, aim, shoot, dash, throw, grab pickups, hide behind cover.
- **Match (3–10 min):** most kills when the timer ends, or first to the kill limit, wins. Ties: fewer deaths wins. Respawn after 3 s with 2 s spawn protection.
- **Session:** match → results (coins, XP, MVP) → double coins with a rewarded ad → back to the room → next match.
- **Meta (days/weeks):** level up, rank badges, daily login, 3 daily missions, unlock outfits, headgear, maps.

## 4. Game modes

| Mode | v1.0 | Notes |
|---|---|---|
| Free-for-all deathmatch | ✅ | Online + offline vs bots |
| Team deathmatch (Red vs Blue) | ✅ | Online + offline |
| Quick Match | ✅ | Joins an open public room or creates one; bots fill and get replaced by real players |
| Private room (code) | ✅ | Host sets map, mode, time (3/5/8/10 min), kill limit, bot count, difficulty, fog on/off |
| Training (target range) | ✅ | Teaches controls in 60 s; first-time tutorial |
| Zombie survival waves (offline) | Later (1.1) | Needs a zombie art sheet |
| Capture the flag | Later | |
| Local Wi-Fi (no internet) | Later (1.1) | Direct IP + LAN discovery |

## 5. Controls

**Phone (default):** left stick = move. Right stick = aim; push past 70% to fire (auto-fire). **AUTO SHOOT** (on by default; AUTO button above RELOAD, Settings > Controls, X key / right-stick click): with no aim input you lock onto the nearest enemy you can see and fire; while aiming yourself you fire when an enemy is in your sights. Red ticks spin around the locked enemy. Buttons: grenade (hold to aim the arc), reload, swap weapon, dash, melee, scoreboard, emote. Sniper shows a scope zoom button. Vibration on hit, kill and explosion (toggle). **BUTTON LAYOUT** (Settings > Controls > EDIT, also from the pause menu): drag every stick and button anywhere, set its size (60-160%) and opacity (25-100%), choose a floating or fixed move stick; outlines show where the health, timer, menu and ammo panels sit. SAVE / RESET / CANCEL; positions are stored against the nearest screen corner so the layout fits any screen shape (ControlLayout, PlayerPrefs nb.layout).

**HUD editor (Settings → Controls):** drag, resize and fade every button; reset to default. Left-handed preset.

**PC (test build):** WASD move, mouse aim, left click fire, right click scope, Space dash, G grenade, R reload, Q swap, F melee, Tab scoreboard, T chat, 1–6 emotes.

## 6. Weapons

Stats for the first five come straight from neon-brawl. All guns are map pickups (you start with the pistol + knife). Damage caps stay host-checked.

| Weapon | Damage | Shot interval (s) | Mag | Reserve | Reload (s) | Special |
|---|---|---|---|---|---|---|
| Pistol | 18 | 0.24 | 12 | ∞ | 1.0 | Starter |
| Hand cannon | 40 | 0.55 | 7 | 28 | 1.4 | Big knockback |
| SMG | 11 | 0.085 | 32 | 96 | 1.4 | Fast, wide spread |
| Assault rifle | 16 | 0.11 | 30 | 90 | 1.6 | All-rounder |
| Shotgun | 10 × 7 pellets | 0.75 | 6 | 18 | 1.6 | Huge knockback up close |
| Sniper | 76 | 1.1 | 5 | 10 | 2.0 | Scope zoom, laser sight |
| Light machine gun | 14 | 0.09 | 80 | 80 | 3.0 | You move 15% slower |
| Rocket launcher | 88 splash | 1.0 | 1 | 5 | 1.4 | Splash radius, rocket jump knockback |
| Flamethrower | 6 per tick | 0.05 | 100 fuel | – | 2.0 | Burn over time, short range |
| Plasma rifle | 22 | 0.18 | 20 | 60 | 1.5 | Bolts bounce once off walls |
| Tesla gun | 9 | 0.12 | 40 | 80 | 1.8 | Chains to 2 nearby enemies |
| Combat knife | 45 | 0.45 | – | – | – | Melee arc, ×2 from behind |

**Throwables:** frag, smoke, flashbang, toxic gas, sticky plasma, proximity mine, fire bottle. Carry 2.
**Pickups:** medkit (+50), armor vest (absorbs 50% until broken), ammo box, speed can (+30% speed 8 s), shield orb (blocks 60 damage), damage orb (+25% damage 8 s).

## 7. Customization

| Slot | Items v1.0 | Recolor |
|---|---|---|
| Outfit (body) | 8 | Any color (tint) |
| Headgear | 15 + bald | Fixed colors |
| Hair | 8 | Any color |
| Back item | 8 (backpacks, jetpack, cape, katana…) | Fixed |
| Skin tone | 8 presets | — |
| Gloves | 6 colors | — |
| Name tag color, kill emote, victory pose | Later | — |
| Weapon skins (camo tints) | Later | — |

The Wardrobe shows your soldier big on a lit pedestal, rotating and aiming, with item cards (common / rare / epic / legendary).

## 8. Maps (5)

Built (M2), each dressed with one ground sheet (T1–T5) and one prop sheet (P1–P5):

| # | Name | Art | Layout | Price |
|---|---|---|---|---|
| 1 | **Sandstone Town** | T2/P2 desert | Village, alleys, indoor rooms | Free |
| 2 | **Green Outpost** | T1/P1 grass | Fields, central fort, corner bunkers | Free |
| 3 | **Frostbite Base** | T3/P3 snow | Barracks, ice bunker, pine edges | 1,200 coins |
| 4 | **Steel Factory** | T4/P4 metal | Four halls, barrel yard, close quarters | 2,000 coins |
| 5 | **Forest Camp** | T5/P5 forest | River with three bridges, cabins | 3,000 coins |

Day or night on every map. Water blocks walking but not bullets. Crates break (and come back), barrels explode, trees fade when you walk under them. Layouts are mirrored left/right for fairness and checked for reachability. In a room the host's unlocked maps are playable by everyone.

Each map: 12–14 spawn points, 10–14 pickup spots, 2–3 lanes plus a center fight zone, cover every 4–6 m, a few explosive barrels.

**Lobby hub:** a painted base camp at dusk with a campfire and wooden signposts: PLAY, WARDROBE, SHOP, MAPS, SETTINGS. Fire flickers, sparks rise, your soldier idles by the fire.

**Home screen layout (one grid, 1920×1080 safe area):** 32 px side margin, one top row (88 px tall) with the profile card on the left (rank, name, XP bar with numbers) and coins, gems and settings on the right. The logo sits top-left, drawn from the game font so it stays sharp. The right column is one width (600) and one right edge: map card (map picture, mode and bot count, CHANGE), PLAY, ONLINE ROOMS. Bottom-left dock of four same-size glass buttons: LOCKER, SHOP, DAILY, MISSIONS. Panels are dark glass with soft shadows, painted in code at full resolution.

## 9. Game feel checklist ("the life of the game")

**Shooting:** muzzle flash sprite + short flash of light, recoil kick on the gun and a little camera kick, bullet tracer, ejected shell that bounces and stays on the floor, impact sparks/dust by surface, bullet decals.

**Getting hit:** knockback push in the shot direction (stronger for shotgun/rocket), white hit flash, small shake on the victim, blood splat on the floor, damage number pop, red edge flash on your screen, phone vibration.

**Kills:** 60 ms hit-stop, ragdoll-style body slide, kill feed line, "DOUBLE KILL / RAMPAGE" banner, coin pop-up, killer gets a short camera punch.

**Explosions:** flash light, fireball sprite sequence, shockwave ring, debris, scorch mark, smoke that drifts, camera shake by distance, barrels chain-react.

**Health bar:** smooth slide, delayed "ghost" bar showing the damage just taken, color green → yellow → red, pulse and heartbeat sound under 25%, floating bars over every fighter.

**World:** dynamic 2D lights (torches flicker, alarm lights spin, flashlights on dark maps), line of sight (areas you cannot see are darkened like classic top-down shooters), footstep dust, leaves/sand blowing, idle breathing on characters.

**UI:** buttons squash on press, panels bounce in, coins fly into the counter, chests shake before opening, confetti on level up, every button has a click sound.

**Settings to respect players:** blood on/off (sparks instead), screen shake strength, vibration on/off, graphics Low/Medium/High, 30/60 fps, sound/music volume.

## 10. Economy

**Coins** (soft, earned by playing) and **Gems** (hard, bought or earned slowly). Rewards are small on purpose so unlocks feel earned.

| Coin source | Amount |
|---|---|
| Finish an online match | 8 |
| Each kill | 2 |
| Kill streak 3 / 5 / 10 | +2 / +5 / +10 |
| 1st / 2nd / 3rd place (FFA) | +20 / +12 / +6 |
| Team win | +12 |
| Offline match vs bots | Half of the above |
| Rewarded ad on results screen | ×2 match coins (once per match) |
| Daily login (7-day loop) | 20, 25, 30, 40, 50, 60, day 7 = 5 gems |
| Daily missions (3 per day) | 15–30 each |
| Level up | 50 coins + 2 gems |

A typical online match pays about 20–30 coins (40–60 with the ad).

| Gem source | Amount |
|---|---|
| Gem packs (in-app purchase) | 80 / 500 / 1,200 / 2,500 |
| Free gems via rewarded ad | 1 gem, max 3 per day |
| Level up | 2 |
| Day 7 login | 5 |
| Achievements | 5–20 |

| Item | Price |
|---|---|
| Map 3 / 4 / 5 | 1,200 / 2,000 / 3,000 coins, or 60 / 100 / 150 gems |
| Hair styles | 200 coins |
| Headgear common / rare | 300 / 800 coins |
| Headgear epic / legendary | 120 / 300 gems |
| Outfits | 500–1,500 coins; premium 80–200 gems |
| Back items | 600–2,000 coins or 60–200 gems |
| Remove Ads (one-time) | Real money; also gives 50 gems |
| Starter pack (one-time) | Real money; gems + 1 legendary item |

**XP:** 10 per match + 3 per kill. **Ranks by level:** Bronze 1–9, Silver 10–19, Gold 20–34, Platinum 35–49, Diamond 50–74, Master 75+.

## 11. Ads, purchases, player ID

Built behind small interfaces now, so the real SDKs plug in later without touching game code.

- **Ads:** Unity LevelPlay mediation (Unity Ads + Google AdMob as ad networks).
  - Rewarded (always optional): double coins, free gem, revive in survival.
  - Interstitial: at most once every 3 matches, never in the first 2 matches of a session, never during gameplay.
  - Banner: menus only, never in a match.
  - Remove Ads purchase removes banners and interstitials; rewarded ads stay optional.
- **Purchases:** Unity IAP (gem packs, Remove Ads, starter pack).
- **Player ID:** Unity Authentication (anonymous, automatic), later linked to Google Play Games sign-in. Cloud save later.

## 12. Technology

| Area | Choice | Why |
|---|---|---|
| Engine | Unity 6000.5.8f1, URP 2D Renderer (already set up) | 2D lights and shadows built in |
| Input | New Input System + custom on-screen twin sticks | Touch, keyboard, gamepad from one setup |
| Camera | Cinemachine 3 | Smooth follow, look-ahead toward aim, impulse screen shake |
| Online | Netcode for GameObjects + Unity Multiplayer Services (Sessions = Lobby + Relay) | Host-based rooms with join codes and quick match. No dedicated server. Free tier to start |
| Offline | Same match code running as a local host with bots | One code path, so offline and online always behave the same |
| Bots | State machine (roam, chase, attack, retreat, grab pickup) + grid path finding | Ported difficulty numbers from neon-brawl |
| Lighting | Light2D: global per map, point lights (torches, lamps), cone lights (flashlights), pooled flash lights | Shadow casters on walls only on High quality |
| Line of sight | Visibility shape from ~180 rays at 20 Hz drawn as a mask; enemies outside it are hidden | Classic top-down shooter look, cheap on phones |
| UI | uGUI + TextMeshPro, 9-sliced sprites | Best fit for image-based, animated mobile UI |
| Tweens | PrimeTween (free, no garbage) | Bouncy UI and juice |
| Data | ScriptableObjects for weapons, maps, cosmetics, economy | Tune without code |
| Save | Versioned JSON file, atomic write, backup | Cloud save later |
| Fonts | Black Ops One + Barlow Condensed (from neon-brawl, free license) | Same brand feel |

**Networking details**
- Host is the authority for health, damage, kills, score, pickups, timer and bots (same as neon-brawl's server rules).
- Your own movement is predicted locally (instant feel); others are smoothed (interpolated). 30 Hz network tick.
- Shots appear instantly for the shooter; the host checks the hit and applies damage with the per-weapon cap.
- If the host leaves, the match ends and players return to the menu (host migration later).
- Max 12 players per room.

**Project layout**
```
Assets/_Project/
  Art/        Characters, Weapons, FX, Maps/Map1..Map5, UI   (sliced from your images)
  Audio/      Music, SFX, Mixer
  Data/       Weapons, Maps, Cosmetics, Economy (ScriptableObjects)
  Prefabs/    Player, Bot, Weapons, Pickups, FX, UI
  Scenes/     Bootstrap, Hub, Map_Sandstone, Map_Highway, Map_Lab, Map_ToxicYard, Map_Jungle, Training
  Scripts/    Core, Gameplay, Net, UI, Meta, Platform, Editor   (one assembly each)
  Settings/   URP, input actions, quality tiers
  Tests/      EditMode, PlayMode
ArtSource/    Your raw AI images (outside Assets, never shipped)
Docs/         This plan and the image prompts
```

## 13. Size and performance budget

- Download size target: **under ~80 MB** on Play (AAB).
- 60 fps on mid-range Android, 30 fps option for low-end. No garbage per frame during matches.
- Under ~100 draw calls in a match: sprite atlases per group (characters, weapons, FX, each map, UI).
- Textures: your images are cut, trimmed, scaled to the size they are shown at, packed into atlases and compressed (ASTC on Android). A 2 MB AI image usually ends up as a few KB per sprite.
- Pools for bullets, shells, decals, FX, lights, damage numbers, audio. Floor decals capped at 200 (oldest fade out).
- Quality tiers: Low (no shadows, no bloom, fewer particles), Medium, High.

## 14. Milestones

| # | Milestone | Result you can test |
|---|---|---|
| M0 | Plan + image prompts | This doc + `IMAGE_PROMPTS.md` |
| M1 ✅ | Core gameplay with placeholder shapes | Move, aim, shoot, knockback, dash, pickups, bots, one blockout map, full offline match. Windows + Android test build |
| M2 ✅ | Your art imported + game feel pass | All 28 collage sheets cut (Neon Brawl ▸ Art ▸ Slice Art Sources, check sheets in Builds/art_sheets). Painted guns, props, ground tiles, FX, UI kit. 5 themed maps. New home, map select, locker, shop, loading, HUD, results |
| M3 ✅ | Online rooms | Create/join with code, quick match, sync, scoreboard. Room chat (text, quick phrases, 8 emote faces, word filter, tap a player to mute), in-match emote panel with bubbles over fighters, bots emote after kills |
| M4 | Meta (partly done in M2) | Done: wardrobe, coins/gems/XP/levels, save, shop with chests, map unlocks, daily rewards, missions. Left: achievements |
| M5 | Monetization + platform | Done: LevelPlay rewarded + interstitial ads (test app key until yours), Unity IAP gem packs + Remove Ads, ad consent question. Left: your LevelPlay/Play Console IDs, receipt validation key, banner (optional), tutorial |
| M6 | Release | Optimization on real phones, signed AAB + APK + Windows build, store listing assets |

## 15. What you will need to provide (later, I'll tell you when)

1. The images from `IMAGE_PROMPTS.md`, saved into `ArtSource/`.
2. A Unity Cloud project linked to this Unity project (for online rooms). Free.
3. Google Play Console account (one-time fee) and the package name, e.g. `com.yourname.neonbrawl`.
4. LevelPlay + AdMob app IDs (for real ads; test IDs are used until then).
5. A privacy policy URL (needed for ads and online play; can be a GitHub Pages page).
6. An Android signing keystore (I'll set up the build; you keep the password).

## 16. Risks

| Risk | Mitigation |
|---|---|
| AI images come out in mixed styles | Same style text in every prompt; generate all sheets in one chat; regenerate any outlier |
| Host-based online can be cheated by the host | Damage caps and speed checks now; server validation with Cloud Code later |
| Coins/gems saved on the phone can be edited | Fine for launch; move to Cloud Save + server checks before big spending features |
| Scope is large | Milestones each end with a playable build; Zombie mode, CTF and LAN come after launch |
| Realtime 2D shadows are expensive on phones | Shadows only on High; line-of-sight mask does the heavy lifting |
| Store rating (blood, guns) | Blood toggle; rating questionnaire answered honestly; not aimed at children |
