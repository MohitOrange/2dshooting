# Image Prompts — NEON BRAWL (top-down)

28 images in 3 batches. **Batch 1 is all I need for the first playable build with real art.**

## How to generate and share

1. **Tool:** ChatGPT (image generation) or Google Gemini. They follow grid and layout instructions best. If you use Midjourney, tell me and I'll convert the prompts.
2. **One chat for everything.** Generate all sheets in the same chat so the style stays the same. Paste **one prompt at a time**.
3. **Check each image before saving.** Regenerate if:
   - items touch or overlap each other,
   - sprites are drawn at an angle instead of straight from above,
   - any text or numbers appear,
   - the background is not one flat color.

   Quick fix message: *"Regenerate: same items, but none may touch, view straight from above, no text."*
4. **Download the original PNG** (the download button, not a screenshot). Don't paste images into this chat: chat compresses them.
5. **Save into** `D:\unity_games\2dshoot\2dShoot\ArtSource\` named by the ID, e.g. `C1.png`, `W1.png`. Extra tries: `C1_b.png`.
6. Tell me when a batch is in the folder.

**Transparent background (optional):** in ChatGPT you can replace the background sentence with `Background: transparent PNG.` Either way works.

**What I do with them:** remove the background, cut out every item, trim, scale each one to the size it's shown in the game, name it, pack into sprite atlases and compress. Each sprite usually ends up 5–40 KB in the final app, at full clarity for its on-screen size.

**Direction rule:** every character, weapon and projectile **faces RIGHT**. The game rotates them to aim.

## Checklist

| Batch | ID | What | Grid |
|---|---|---|---|
| 1 | C1 | Soldier bodies (8 outfits) | 4×2 |
| 1 | C2 | Base head + 15 headgear | 4×4 |
| 1 | C3 | 8 hairstyles + 8 back items | 4×4 |
| 1 | W1 | 12 weapons, top-down (held) | 4×3 |
| 1 | W2 | 12 weapons, side view (icons) | 4×3 |
| 1 | W3 | Grenades, pickups, power-ups | 4×4 |
| 1 | FX1 | Glowing effects (on black) | 4×4 |
| 1 | FX2 | Smoke and magic effects (on black) | 4×3 |
| 1 | FX3 | Blood, scorch, shells, debris | 4×4 |
| 1 | E0 | Shared props (crates, barrels, sandbags) | 4×3 |
| 1 | T1 | Map 1 ground textures | 2×2 |
| 1 | P1 | Map 1 props | 4×3 |
| 1 | U3 | HUD and touch controls | 5×4 |
| 2 | T2–T5 | Maps 2–5 ground textures | 2×2 each |
| 2 | P2–P5 | Maps 2–5 props | 4×3 each |
| 3 | U1 | UI buttons and panels | 4×4 |
| 3 | U2 | UI icons | 5×4 |
| 3 | U4 | Coins, gems, chests, rank badges, card frames | 5×4 |
| 3 | U5 | Logo | single |
| 3 | U6 | Lobby hub background (campfire camp) | single, landscape |
| 3 | U7 | Loading screen key art | single, landscape |
| 3 | U8 | App icon | single, square |

---

# BATCH 1 — needed for the first playable build

## C1 — Soldier bodies

```text
Create a 2D game sprite sheet of 8 cartoon soldier BODIES for a top-down shooter.

Every body is seen from directly above and faces RIGHT: the shoulders run from the top to the bottom of the cell, and both arms reach forward to the right with both gloved hands close together in front, as if holding a gun with two hands — but do NOT draw any gun. Do NOT draw a head: leave a small, clean, round neck opening in the middle of the shoulders, because the head is a separate layer placed on top. All 8 bodies have exactly the same size, pose and hand position so they can be swapped.

Colors: all cloth is neutral light grey / off-white with soft grey shading (the color is changed in the game). Straps, pouches, belts and buckles are dark charcoal grey. Gloves are black.

The 8 outfits:
1) fitted t-shirt with one chest strap
2) plate-carrier vest with magazine pouches
3) hoodie with drawstrings
4) heavy armor with big round shoulder pads
5) zipped bomber jacket
6) scout suit covered in leafy ghillie scraps
7) medic vest with a heart patch on the back
8) commando with a bullet belt over one shoulder

Style: polished 2D mobile game art, stylized hand-painted cartoon look, clean bold dark-brown outlines, soft cel shading, crisp sharp edges, very readable at small size. View: strictly top-down orthographic — the camera looks straight down at 90 degrees, no perspective, no isometric or 3/4 angle. Light comes from the top-left. No cast shadows and no ground shadows.

Layout: exactly 8 separate objects in an even grid of 4 columns and 2 rows, each centered in its own cell with wide empty space around it. No object touches or overlaps another object or the image edge. Background: one flat, solid, pure magenta color (#FF00FF) everywhere — no gradient, no texture, no floor, no shadow, no glow. Do not use magenta or pink inside the objects. No text, letters, numbers, labels, grid lines, frames or watermark. Square image, highest resolution.
```

## C2 — Base head + headgear

```text
Create a 2D game sprite sheet of 16 HEAD items for a top-down shooter character. All are seen from directly above and face RIGHT (the face side points to the right edge). All 16 are drawn at the same scale so each fits exactly over the same round head.

1) The base head: a round bald head seen from above, warm light skin tone, two small ears on the top and bottom edges, the tip of the nose just visible on the right edge.
Items 2–16 are headgear that sits on that head:
2) green military helmet with chin strap
3) black tactical helmet with night-vision goggles flipped up at the front
4) red beret
5) baseball cap with the visor pointing right
6) bandana with knot tails trailing to the left
7) brown cowboy hat with a wide brim
8) knit beanie with a pom-pom
9) dark hoodie hood
10) black ninja head wrap with tails trailing to the left
11) gas mask with a round filter at the front
12) golden crown with gems
13) viking helmet with two horns
14) samurai helmet with a gold crest
15) white pilot helmet with an orange visor at the front
16) pirate tricorn hat

Style: polished 2D mobile game art, stylized hand-painted cartoon look, clean bold dark-brown outlines, soft cel shading, rich saturated colors, crisp sharp edges, very readable at small size. View: strictly top-down orthographic — the camera looks straight down at 90 degrees, no perspective, no isometric or 3/4 angle. Light comes from the top-left. No cast shadows and no ground shadows.

Layout: exactly 16 separate objects in an even grid of 4 columns and 4 rows, each centered in its own cell with wide empty space around it. No object touches or overlaps another object or the image edge. Background: one flat, solid, pure magenta color (#FF00FF) everywhere — no gradient, no texture, no floor, no shadow, no glow. Do not use magenta or pink inside the objects. No text, letters, numbers, labels, grid lines, frames or watermark. Square image, highest resolution.
```

## C3 — Hairstyles + back items

```text
Create a 2D game sprite sheet of 16 items for a top-down shooter character. All are seen from directly above; the character faces RIGHT. Everything is drawn at the same scale as a round head of a top-down cartoon soldier.

Rows 1–2: eight hairstyles, each shown as hair covering a round head seen from above. All hair is neutral light grey with soft shading (it is recolored in the game):
1) buzz cut
2) spiky hair
3) mohawk
4) ponytail trailing to the left
5) top bun
6) big afro
7) short side part
8) long loose hair flowing to the left

Rows 3–4: eight items worn on the back (the back is the LEFT side, because the character faces right), seen from above:
9) tactical backpack
10) radio pack with a whip antenna
11) parachute pack
12) small jetpack with two thrusters
13) katana in a sheath worn diagonally
14) quiver of arrows
15) red hero cape flowing to the left
16) camo duffel bag

Style: polished 2D mobile game art, stylized hand-painted cartoon look, clean bold dark-brown outlines, soft cel shading, rich saturated colors, crisp sharp edges, very readable at small size. View: strictly top-down orthographic — the camera looks straight down at 90 degrees, no perspective, no isometric or 3/4 angle. Light comes from the top-left. No cast shadows and no ground shadows.

Layout: exactly 16 separate objects in an even grid of 4 columns and 4 rows, each centered in its own cell with wide empty space around it. No object touches or overlaps another object or the image edge. Background: one flat, solid, pure magenta color (#FF00FF) everywhere — no gradient, no texture, no floor, no shadow, no glow. Do not use magenta or pink inside the objects. No text, letters, numbers, labels, grid lines, frames or watermark. Square image, highest resolution.
```

## W1 — Weapons, top-down (held in game)

```text
Create a 2D game sprite sheet of 12 weapons for a top-down shooter, seen from directly above exactly as they look when held by a top-down character: long slim silhouettes, muzzle pointing RIGHT, grip and stock on the left. Original fictional designs with no real brand names or logos, chunky and stylized so each is recognizable at small size, all in correct relative scale (pistol smallest, sniper and machine gun longest).

1) compact pistol
2) heavy hand-cannon pistol
3) submachine gun
4) assault rifle
5) pump shotgun
6) sniper rifle with a long scope
7) light machine gun with a drum magazine
8) rocket launcher tube
9) flamethrower with a small fuel tank
10) sci-fi plasma rifle with glowing green energy cells
11) tesla gun with a copper coil and blue electric glow
12) combat knife

Style: polished 2D mobile game art, stylized hand-painted cartoon look, clean bold dark-brown outlines, soft cel shading, rich saturated colors, crisp sharp edges, very readable at small size. View: strictly top-down orthographic — the camera looks straight down at 90 degrees, no perspective, no isometric or 3/4 angle. Light comes from the top-left. No cast shadows and no ground shadows. Any glow stays inside the weapon, with no halo around it.

Layout: exactly 12 separate objects in an even grid of 4 columns and 3 rows, each centered in its own cell with wide empty space around it. No object touches or overlaps another object or the image edge. Background: one flat, solid, pure magenta color (#FF00FF) everywhere — no gradient, no texture, no floor, no shadow, no glow. Do not use magenta or pink inside the objects. No text, letters, numbers, labels, grid lines, frames or watermark. Square image, highest resolution.
```

## W2 — Weapons, side view (shop icons and pickups)

Generate this right after W1 in the same chat.

```text
Create a 2D game sprite sheet of the SAME 12 weapons from the previous image, now in a clean full SIDE VIEW (profile), muzzle pointing RIGHT. Match the designs, colors and details of the previous top-down weapons. Original fictional designs, no real brand names or logos, chunky stylized cartoon proportions, bold silhouettes, all in correct relative scale.

1) compact pistol
2) heavy hand-cannon pistol
3) submachine gun
4) assault rifle
5) pump shotgun
6) sniper rifle with a long scope
7) light machine gun with a drum magazine
8) rocket launcher tube
9) flamethrower with a small fuel tank
10) sci-fi plasma rifle with glowing green energy cells
11) tesla gun with a copper coil and blue electric glow
12) combat knife

Style: polished 2D mobile game art, stylized hand-painted cartoon look, clean bold dark-brown outlines, soft cel shading, rich saturated colors, crisp sharp edges, glossy highlights, very readable at small size. View: flat side profile, no perspective. Light comes from the top-left. No cast shadows. Any glow stays inside the weapon, with no halo around it.

Layout: exactly 12 separate objects in an even grid of 4 columns and 3 rows, each centered in its own cell with wide empty space around it. No object touches or overlaps another object or the image edge. Background: one flat, solid, pure magenta color (#FF00FF) everywhere — no gradient, no texture, no floor, no shadow, no glow. Do not use magenta or pink inside the objects. No text, letters, numbers, labels, grid lines, frames or watermark. Square image, highest resolution.
```

## W3 — Grenades, pickups, power-ups

```text
Create a 2D game sprite sheet of 16 items for a top-down shooter, all seen from directly above, anything with a direction pointing RIGHT. Original designs, no brand names or logos.

1) frag grenade
2) smoke grenade (grey canister)
3) flashbang (white canister)
4) toxic gas grenade with green glow
5) sticky plasma grenade with blue glow
6) proximity landmine with a small red light
7) glass fire bottle with a burning cloth wick
8) rocket missile projectile pointing right
9) first-aid kit: white case with a red heart symbol
10) armor vest pickup
11) small olive ammo box
12) energy drink can (speed boost)
13) blue shield power-up orb
14) red damage power-up orb with a lightning bolt symbol
15) gold coin pickup token
16) small bandage roll

Style: polished 2D mobile game art, stylized hand-painted cartoon look, clean bold dark-brown outlines, soft cel shading, rich saturated colors, crisp sharp edges, very readable at small size. View: strictly top-down orthographic — the camera looks straight down at 90 degrees, no perspective, no isometric or 3/4 angle. Light comes from the top-left. No cast shadows and no ground shadows. Any glow stays inside the object, with no halo around it.

Layout: exactly 16 separate objects in an even grid of 4 columns and 4 rows, each centered in its own cell with wide empty space around it. No object touches or overlaps another object or the image edge. Background: one flat, solid, pure magenta color (#FF00FF) everywhere — no gradient, no texture, no floor, no shadow, no glow. Do not use magenta or pink inside the objects. No text, letters, numbers, labels, grid lines, frames or watermark. Square image, highest resolution.
```

## FX1 — Glowing effects (black background)

```text
Create a 2D game VFX sprite sheet of 16 glowing effects for a top-down shooter. Bright, saturated, stylized cartoon effects with smooth glowing gradients (white-hot core, then yellow, then orange edges, unless a different color is given). Crisp shapes, readable at small size.

1) gun muzzle flash seen from above, star-burst shape pointing RIGHT
2) a second, different muzzle flash pointing right
3) a third, small muzzle flash pointing right
4) wide shotgun muzzle blast pointing right
5) green plasma muzzle flash
6) blue electric spark burst
7) explosion stage 1: bright white-yellow flash
8) explosion stage 2: big round orange fireball
9) explosion stage 3: fading red-orange fire with dark smoky edges
10) explosion stage 4: scattered glowing embers
11) flamethrower fire stream pointing right
12) green plasma bolt projectile, elongated, pointing right
13) thin yellow-white bullet tracer streak, horizontal
14) yellow impact spark burst
15) soft round white glow blob
16) jagged blue lightning bolt segment, horizontal

Background: one flat, solid, pure black color (#000000) everywhere — the effects are added with additive blending, so everything that is not effect must be perfectly black.

Layout: exactly 16 separate effects in an even grid of 4 columns and 4 rows, each centered in its own cell with wide empty black space around it. No effect touches another effect or the image edge. No text, letters, numbers, labels, grid lines, frames or watermark. Square image, highest resolution.
```

## FX2 — Smoke and magic effects (black background)

```text
Create a 2D game VFX sprite sheet of 12 soft effects for a top-down shooter. Stylized cartoon effects with soft edges and smooth gradients. On black, the brightness of each effect becomes its transparency in the game.

1) soft round white smoke puff
2) a second white smoke puff, different shape
3) a third, larger light-grey smoke cloud
4) a fourth, small wispy smoke puff
5) beige dust puff
6) green toxic gas cloud
7) thin white shockwave ring (a circle outline)
8) green healing sparkles, plus-shaped
9) golden star burst (level up)
10) golden coin twinkle sparkle
11) blue shield bubble ring
12) white speed streak lines trailing to the left (motion trail)

Background: one flat, solid, pure black color (#000000) everywhere — everything that is not effect must be perfectly black.

Layout: exactly 12 separate effects in an even grid of 4 columns and 3 rows, each centered in its own cell with wide empty black space around it. No effect touches another effect or the image edge. No text, letters, numbers, labels, grid lines, frames or watermark. Square image, highest resolution.
```

## FX3 — Blood, scorch, shells, debris (floor decals)

```text
Create a 2D game sprite sheet of 16 floor decals and small debris for a top-down shooter, all seen from directly above.

1) dark red blood splat
2) a second, different blood splat
3) a third, small blood splat
4) a fourth blood splat with droplets
5) blood spray spreading toward the right
6) black scorch burn mark (round)
7) a second, irregular scorch mark
8) small bullet impact crack in the ground
9) brass bullet casing
10) red shotgun shell
11) wooden splinters
12) stone rubble chunks
13) green toxic slime splat
14) black oil puddle
15) broken glass shards
16) small cracked crater

Style: polished 2D mobile game art, stylized hand-painted cartoon look, clean outlines, soft cel shading, crisp sharp edges, readable at small size. View: strictly top-down orthographic — straight down at 90 degrees. No cast shadows.

Layout: exactly 16 separate objects in an even grid of 4 columns and 4 rows, each centered in its own cell with wide empty space around it. No object touches or overlaps another object or the image edge. Background: one flat, solid, pure magenta color (#FF00FF) everywhere — no gradient, no texture, no floor, no shadow, no glow. Do not use magenta or pink inside the objects. No text, letters, numbers, labels, grid lines, frames or watermark. Square image, highest resolution.
```

## E0 — Shared props (used on every map)

```text
Create a 2D game sprite sheet of 12 environment props for a top-down shooter, seen from directly above (you see the top of each object). Objects have bevelled edges and a slightly darker rim so they look raised, solid and chunky.

1) wooden crate with an X brace on top
2) large double wooden crate
3) dark grey steel crate with X-shaped bracing
4) red explosive barrel with a hazard symbol on the lid
5) blue steel drum
6) green toxic waste barrel with glowing goo on the lid
7) straight sandbag wall segment (horizontal)
8) L-shaped sandbag wall corner
9) wooden pallet
10) stack of three old tires
11) concrete road barrier (horizontal)
12) olive military ammo crate with yellow stencil marks (no readable text)

Style: polished 2D mobile game art, stylized hand-painted cartoon look, clean bold dark-brown outlines, soft cel shading, rich saturated colors, crisp sharp edges, very readable at small size. View: strictly top-down orthographic — the camera looks straight down at 90 degrees, no perspective, no isometric or 3/4 angle. Light comes from the top-left. No cast shadows and no ground shadows.

Layout: exactly 12 separate objects in an even grid of 4 columns and 3 rows, each centered in its own cell with wide empty space around it. No object touches or overlaps another object or the image edge. Background: one flat, solid, pure magenta color (#FF00FF) everywhere — no gradient, no texture, no floor, no shadow, no glow. Do not use magenta or pink inside the objects. No text, letters, numbers, labels, grid lines, frames or watermark. Square image, highest resolution.
```

## T1 — Map 1 "Sandstone Town" ground textures

```text
Create 4 seamless tileable ground textures for a top-down 2D shooter map called Sandstone Town, a desert village.

Layout: a 2×2 grid of 4 equal square swatches that fill the whole square image, separated only by thin solid black lines. Each swatch is its own texture and tiles seamlessly on all four sides.

1) fine desert sand with tiny pebbles
2) packed dirt path with scattered cobblestones
3) top of a sandstone brick wall: big flat stone slabs seen from above
4) terracotta clay floor tiles for indoor floors

View: flat, straight down from above. Lighting: perfectly even — no light gradient, no vignette, no shadows, no large unique features that make repetition obvious. Style: stylized hand-painted cartoon texture, clean and readable, slightly muted colors so characters stand out on top. No text, no objects, no characters. Highest resolution.
```

## P1 — Map 1 "Sandstone Town" props

```text
Create a 2D game sprite sheet of 12 environment props for a desert village map in a top-down shooter, all seen from directly above (you see the top of each object). Objects have bevelled edges and a slightly darker rim so they look raised and chunky.

1) palm tree canopy seen from above
2) spiky agave plant
3) small round cactus
4) group of three clay pots
5) market stall with a striped cloth awning (seen from above)
6) hay bale
7) wooden hand cart
8) round stone well
9) patterned woven rug
10) round stone fountain with water
11) stack of wooden planks
12) low sandstone wall segment (horizontal)

Style: polished 2D mobile game art, stylized hand-painted cartoon look, clean bold dark-brown outlines, soft cel shading, rich warm desert colors, crisp sharp edges, very readable at small size. View: strictly top-down orthographic — the camera looks straight down at 90 degrees, no perspective, no isometric or 3/4 angle. Light comes from the top-left. No cast shadows and no ground shadows.

Layout: exactly 12 separate objects in an even grid of 4 columns and 3 rows, each centered in its own cell with wide empty space around it. No object touches or overlaps another object or the image edge. Background: one flat, solid, pure magenta color (#FF00FF) everywhere — no gradient, no texture, no floor, no shadow, no glow. Do not use magenta or pink inside the objects. No text, letters, numbers, labels, grid lines, frames or watermark. Square image, highest resolution.
```

## U3 — HUD and touch controls

```text
Create a 2D mobile game UI sprite sheet of 20 HUD and touch-control elements for a shooter. Front-facing flat UI elements. Symbols on buttons are white with a dark navy outline.

1) virtual joystick base: dark navy circle with a thin light rim
2) joystick knob: smaller glossy light-grey circle
3) fire button: big round red button with a bullet symbol
4) grenade button: round button with a grenade symbol
5) reload button: round button with circular arrows
6) swap weapon button: round button with two opposite arrows
7) dash button: round button with a running boot and speed lines
8) melee button: round button with a knife
9) red heart health icon
10) blue shield armor icon
11) stopwatch timer icon
12) bullets ammo icon
13) white skull kill icon
14) crosshair reticle: four short lines and a center dot, white with a dark outline
15) crosshair reticle: circle with a center dot, white with a dark outline
16) round minimap frame: metal ring with a small N marker on top, the inside left empty
17) pill-shaped health bar frame, empty dark inside
18) health bar fill strip: plain white with a glossy highlight
19) white hit marker X
20) round emote button with a smiley face

Style: premium mobile game UI, chunky cartoon look: thick dark navy outlines, glossy top highlight, soft bevel, rounded shapes, bright saturated colors, crisp edges, readable at small size. No cast shadows.

Layout: exactly 20 separate objects in an even grid of 5 columns and 4 rows, each centered in its own cell with wide empty space around it. No object touches or overlaps another object or the image edge. Background: one flat, solid, pure magenta color (#FF00FF) everywhere, including inside the minimap ring — no gradient, no texture, no shadow, no glow. Do not use magenta or pink inside the objects. No text, letters, numbers, labels, grid lines, frames or watermark. Square image, highest resolution.
```

---

# BATCH 2 — maps 2 to 5

All texture prompts use the same rules as T1, and all prop prompts use the same rules as P1.

## T2 — Map 2 "Dusty Highway" ground textures

```text
Create 4 seamless tileable ground textures for a top-down 2D shooter map called Dusty Highway, a road through a desert outpost.

Layout: a 2×2 grid of 4 equal square swatches that fill the whole square image, separated only by thin solid black lines. Each swatch is its own texture and tiles seamlessly on all four sides.

1) dark asphalt road surface with fine grain, no painted lines
2) cracked concrete sidewalk slabs
3) dry desert ground with sparse grass tufts
4) corrugated metal roof sheet, seen from above

View: flat, straight down from above. Lighting: perfectly even — no light gradient, no vignette, no shadows, no large unique features that make repetition obvious. Style: stylized hand-painted cartoon texture, clean and readable, slightly muted colors so characters stand out on top. No text, no objects, no characters. Highest resolution.
```

## P2 — Map 2 "Dusty Highway" props

```text
Create a 2D game sprite sheet of 12 environment props for a desert highway outpost map in a top-down shooter, all seen from directly above (you see the top of each object). Objects have bevelled edges and a slightly darker rim so they look raised and chunky.

1) red sedan car seen from above, front pointing right
2) white pickup truck seen from above, front pointing right
3) burned-out car wreck
4) orange traffic cone
5) red-and-white striped road barrier
6) gas station fuel pump
7) street lamp head seen from above
8) green dumpster with a closed lid
9) rooftop air-conditioner unit
10) tumbleweed
11) yellow fire hydrant
12) metal picnic table with benches

Style: polished 2D mobile game art, stylized hand-painted cartoon look, clean bold dark-brown outlines, soft cel shading, rich saturated colors, crisp sharp edges, very readable at small size. View: strictly top-down orthographic — the camera looks straight down at 90 degrees, no perspective, no isometric or 3/4 angle. Light comes from the top-left. No cast shadows and no ground shadows.

Layout: exactly 12 separate objects in an even grid of 4 columns and 3 rows, each centered in its own cell with wide empty space around it. No object touches or overlaps another object or the image edge. Background: one flat, solid, pure magenta color (#FF00FF) everywhere — no gradient, no texture, no floor, no shadow, no glow. Do not use magenta or pink inside the objects. No text, letters, numbers, labels, grid lines, frames or watermark. Square image, highest resolution.
```

## T3 — Map 3 "Blackout Lab" ground textures

```text
Create 4 seamless tileable ground textures for a top-down 2D shooter map called Blackout Lab, a dark secret research facility.

Layout: a 2×2 grid of 4 equal square swatches that fill the whole square image, separated only by thin solid black lines. Each swatch is its own texture and tiles seamlessly on all four sides.

1) dark steel diamond-plate floor
2) slightly dirty white laboratory floor tiles
3) grey concrete wall top with panel seams
4) metal floor grating with pipes visible below

View: flat, straight down from above. Lighting: perfectly even — no light gradient, no vignette, no shadows, no large unique features that make repetition obvious. Style: stylized hand-painted cartoon texture, clean and readable, medium brightness (the map is darkened by in-game lighting). No text, no objects, no characters. Highest resolution.
```

## P3 — Map 3 "Blackout Lab" props

```text
Create a 2D game sprite sheet of 12 environment props for a secret research lab map in a top-down shooter, all seen from directly above (you see the top of each object). Objects have bevelled edges and a slightly darker rim so they look raised and chunky.

1) computer desk with two monitors and a keyboard
2) tall server rack with small blinking lights on top
3) lab table with flasks and test tubes
4) office chair
5) glass stasis capsule with glowing blue liquid
6) red rotating alarm lamp
7) power generator
8) pipe junction with valves
9) yellow wet-floor warning stand
10) vending machine
11) metal filing cabinet
12) broken robotic arm machine

Style: polished 2D mobile game art, stylized hand-painted cartoon look, clean bold dark-brown outlines, soft cel shading, cool steel and teal colors with bright accent lights, crisp sharp edges, very readable at small size. View: strictly top-down orthographic — the camera looks straight down at 90 degrees, no perspective, no isometric or 3/4 angle. Light comes from the top-left. No cast shadows and no ground shadows. Any glow stays inside the object, with no halo around it.

Layout: exactly 12 separate objects in an even grid of 4 columns and 3 rows, each centered in its own cell with wide empty space around it. No object touches or overlaps another object or the image edge. Background: one flat, solid, pure magenta color (#FF00FF) everywhere — no gradient, no texture, no floor, no shadow, no glow. Do not use magenta or pink inside the objects. No text, letters, numbers, labels, grid lines, frames or watermark. Square image, highest resolution.
```

## T4 — Map 4 "Toxic Yard" ground textures

```text
Create 4 seamless tileable ground textures for a top-down 2D shooter map called Toxic Yard, an industrial chemical yard.

Layout: a 2×2 grid of 4 equal square swatches that fill the whole square image, separated only by thin solid black lines. Each swatch is its own texture and tiles seamlessly on all four sides.

1) worn grey concrete with oil stains
2) bright green lawn grass
3) yellow-and-black diagonal hazard-stripe metal floor
4) bubbling toxic green slime surface

View: flat, straight down from above. Lighting: perfectly even — no light gradient, no vignette, no shadows, no large unique features that make repetition obvious. Style: stylized hand-painted cartoon texture, clean and readable, slightly muted colors so characters stand out on top. No text, no objects, no characters. Highest resolution.
```

## P4 — Map 4 "Toxic Yard" props

```text
Create a 2D game sprite sheet of 12 environment props for an industrial chemical yard map in a top-down shooter, all seen from directly above (you see the top of each object). Objects have bevelled edges and a slightly darker rim so they look raised and chunky.

1) long red shipping container with a corrugated roof
2) yellow forklift, front pointing right
3) round chemical storage tank
4) toxic green waste puddle
5) large industrial floor vent fan
6) yellow-and-black hazard barrier
7) round leafy tree canopy
8) green bush
9) rusty scrap metal pile
10) electrical transformer box with warning stripes
11) pipe valve wheel station
12) stack of steel beams

Style: polished 2D mobile game art, stylized hand-painted cartoon look, clean bold dark-brown outlines, soft cel shading, rich saturated colors, crisp sharp edges, very readable at small size. View: strictly top-down orthographic — the camera looks straight down at 90 degrees, no perspective, no isometric or 3/4 angle. Light comes from the top-left. No cast shadows and no ground shadows. Any glow stays inside the object, with no halo around it.

Layout: exactly 12 separate objects in an even grid of 4 columns and 3 rows, each centered in its own cell with wide empty space around it. No object touches or overlaps another object or the image edge. Background: one flat, solid, pure magenta color (#FF00FF) everywhere — no gradient, no texture, no floor, no shadow, no glow. Do not use magenta or pink inside the objects. No text, letters, numbers, labels, grid lines, frames or watermark. Square image, highest resolution.
```

## T5 — Map 5 "Jungle Temple" ground textures

```text
Create 4 seamless tileable ground textures for a top-down 2D shooter map called Jungle Temple, ancient stone ruins in a jungle.

Layout: a 2×2 grid of 4 equal square swatches that fill the whole square image, separated only by thin solid black lines. Each swatch is its own texture and tiles seamlessly on all four sides.

1) mossy ancient stone floor slabs
2) jungle dirt ground with fallen leaves
3) top of a carved stone block wall
4) shallow clear river water over pebbles

View: flat, straight down from above. Lighting: perfectly even — no light gradient, no vignette, no shadows, no large unique features that make repetition obvious. Style: stylized hand-painted cartoon texture, clean and readable, slightly muted colors so characters stand out on top. No text, no objects, no characters. Highest resolution.
```

## P5 — Map 5 "Jungle Temple" props

```text
Create a 2D game sprite sheet of 12 environment props for a jungle temple ruins map in a top-down shooter, all seen from directly above (you see the top of each object). Objects have bevelled edges and a slightly darker rim so they look raised and chunky.

1) huge jungle tree canopy
2) fern bush
3) giant tropical leaf plant
4) broken round stone pillar
5) mossy stone head of a fictional ancient guardian statue lying on the ground
6) standing fire brazier with flames on top
7) vine-covered boulder
8) wooden plank bridge section (horizontal)
9) lily pads with a white flower
10) fallen hollow log
11) ancient wooden chest with gold trim
12) wall-mounted fire torch

Style: polished 2D mobile game art, stylized hand-painted cartoon look, clean bold dark-brown outlines, soft cel shading, rich jungle greens and warm stone colors, crisp sharp edges, very readable at small size. View: strictly top-down orthographic — the camera looks straight down at 90 degrees, no perspective, no isometric or 3/4 angle. Light comes from the top-left. No cast shadows and no ground shadows. Any glow stays inside the object, with no halo around it.

Layout: exactly 12 separate objects in an even grid of 4 columns and 3 rows, each centered in its own cell with wide empty space around it. No object touches or overlaps another object or the image edge. Background: one flat, solid, pure magenta color (#FF00FF) everywhere — no gradient, no texture, no floor, no shadow, no glow. Do not use magenta or pink inside the objects. No text, letters, numbers, labels, grid lines, frames or watermark. Square image, highest resolution.
```

---

# BATCH 3 — menus, shop, store

## U1 — UI buttons and panels

```text
Create a 2D mobile game UI kit sprite sheet of 16 elements. Front-facing flat UI elements. All buttons, banners and panels are EMPTY — no text and no icons on them.

1) large green rounded button
2) large orange rounded button
3) large blue rounded button
4) large red rounded button
5) large grey disabled button
6) small round yellow button
7) small square blue button
8) wide wooden plank title banner
9) large window panel: dark navy steel plate with riveted corners and a plain flat center (so it can be stretched)
10) small dark rounded info panel
11) tab button, selected (bright gold)
12) tab button, not selected (dark)
13) toggle switch ON (green)
14) toggle switch OFF (grey)
15) horizontal slider track
16) round slider knob

Style: premium mobile game UI, chunky cartoon look: thick dark navy outlines, glossy top highlight, soft bevel and inner shading, rounded corners, bright saturated colors (green, orange, blue, red, gold), dark navy-steel panels with small rivets, and warm wooden plank signs. Crisp edges. No cast shadows.

Layout: exactly 16 separate objects in an even grid of 4 columns and 4 rows, each centered in its own cell with wide empty space around it. No object touches or overlaps another object or the image edge. Background: one flat, solid, pure magenta color (#FF00FF) everywhere — no gradient, no texture, no shadow, no glow. Do not use magenta or pink inside the objects. No text, letters, numbers, labels, grid lines, frames or watermark. Square image, highest resolution.
```

## U2 — UI icons

```text
Create a 2D mobile game icon sprite sheet of 20 icons. Each icon stands alone (no button background behind it). Glossy colorful cartoon icons with thick dark navy outlines, soft bevel, bright saturated colors, consistent style like a premium mobile game.

1) settings gear
2) shopping cart
3) play triangle
4) back arrow
5) close X
6) padlock
7) TV screen with a play symbol (watch ad)
8) two people (friends)
9) trophy cup
10) gift box with a ribbon
11) folded map
12) shirt on a hanger (wardrobe)
13) crosshair (armory)
14) three-step winners podium (leaderboard)
15) speaker (sound)
16) music note
17) vibrating phone
18) chat bubble
19) smiley face
20) check mark

Layout: exactly 20 separate icons in an even grid of 5 columns and 4 rows, each centered in its own cell with wide empty space around it. No icon touches or overlaps another icon or the image edge. Background: one flat, solid, pure magenta color (#FF00FF) everywhere — no gradient, no texture, no shadow, no glow. Do not use magenta or pink inside the icons. No text, letters, numbers, labels, grid lines, frames or watermark. Square image, highest resolution.
```

## U4 — Coins, gems, chests, rank badges, card frames

```text
Create a 2D mobile game sprite sheet of 20 reward and shop elements. Glossy colorful cartoon style with thick dark navy outlines, soft bevel, bright highlights, crisp edges, consistent with a premium mobile game. Any glow stays inside the object, with no halo around it.

1) single gold coin
2) small pile of gold coins
3) sack full of gold coins
4) single blue diamond gem
5) handful of blue gems
6) open chest overflowing with blue gems
7) closed wooden treasure chest with iron bands
8) open treasure chest glowing gold inside
9) gift box with a ribbon
10) yellow XP star
11) bronze rank badge (shield emblem with one chevron)
12) silver rank badge (two chevrons)
13) gold rank badge (three chevrons)
14) platinum rank badge with small wings
15) diamond rank badge with a gem in the center
16) master rank badge with a crown and laurel
17) item card frame, common: grey rounded rectangle with a simple border and plain flat center
18) item card frame, rare: blue border
19) item card frame, epic: purple border
20) item card frame, legendary: ornate gold border

Layout: exactly 20 separate objects in an even grid of 5 columns and 4 rows, each centered in its own cell with wide empty space around it. No object touches or overlaps another object or the image edge. Background: one flat, solid, pure magenta color (#FF00FF) everywhere — no gradient, no texture, no shadow, no glow. Do not use magenta or pink inside the objects. No text, letters, numbers, labels, grid lines, frames or watermark. Square image, highest resolution.
```

## U5 — Logo

Change the title first if you want a different game name.

```text
Create a game logo for a mobile top-down shooter titled "NEON BRAWL". The text must be spelled exactly N-E-O-N B-R-A-W-L, on two lines or one line.

Bold chunky 3D cartoon letters with thick dark navy outlines and glossy highlights, letter fill in a hot orange-to-yellow gradient with a glowing cyan neon edge, slightly tilted for energy. Behind the text: two crossed cartoon pistols and a small explosion burst with flying sparks.

Centered with a wide empty margin. Background: one flat, solid, pure magenta color (#FF00FF) — no gradient, no shadow. No other text. Landscape 3:2, highest resolution.
```

## U6 — Lobby hub background (campfire camp)

```text
Create a painted 2D mobile game menu background, landscape 16:9.

Scene: a cozy hidden military base camp at dusk in a desert canyon, seen from the side at eye level like a stage (not top-down). In the center foreground, a crackling campfire inside a ring of stones with a warm orange glow lighting the ground. Around it, wooden signposts with BLANK boards (no writing): one large signpost on the left, two medium ones on the right, one small one near the fire. Behind: canvas army tents lit by warm lanterns, stacked supply crates, an old jeep, string lights between poles, palm trees and canyon rock walls. Sky: purple-blue to orange gradient with the first stars.

Keep the center-bottom area in front of the fire clear (a character will stand there) and keep the top-center sky clear (the game logo goes there).

Style: rich painterly cartoon illustration, warm firelight against cool dusk light, inviting and magical, high detail. No people, no text, no letters, no logos.
```

## U7 — Loading screen key art

```text
Create a landscape 16:9 key art illustration for a mobile shooter loading screen.

Three original cartoon soldier heroes in dynamic front-facing action poses, charging toward the viewer down a desert town street:
- center: confident soldier in a green combat helmet firing an assault rifle
- left: soldier with a red bandana and a pump shotgun
- right: soldier in a white pilot helmet with an orange visor, holding a glowing green plasma rifle

Chunky stylized proportions (big heads, expressive determined faces, chunky gloves and boots), like a premium animated film. Behind them: a big orange explosion, flying crates, sparks and smoke, sandstone buildings, palm trees, golden sunset sky.

Style: bold dark outlines, saturated colors, strong rim light, high detail. No text, no logos, no real brand names.
```

## U8 — App icon

```text
Create a square 1:1 mobile app icon artwork: close-up of an original cartoon soldier wearing a green combat helmet, with a determined grin, pointing a chunky pistol toward the viewer with a bright muzzle flash. Bold dark outlines, glossy cel shading, bright orange-yellow radial burst background.

Fill the whole square edge to edge: no rounded corners, no border, no frame, no text. High contrast, readable when very small. Highest resolution.
```

---

## Later (after launch): Zombie mode

```text
Create a 2D game sprite sheet of 8 cartoon zombies for a top-down shooter, seen from directly above, facing RIGHT, arms reaching forward to the right. Funny-spooky (not gory): green-grey skin, torn clothes, varied sizes: 1) walker, 2) runner (thin), 3) brute (huge), 4) exploder (swollen, glowing green), 5) spitter, 6) zombie in a hard hat, 7) zombie in a lab coat, 8) boss zombie with a spiked collar.

Style: polished 2D mobile game art, stylized hand-painted cartoon look, clean bold dark-brown outlines, soft cel shading, rich saturated colors, crisp sharp edges, very readable at small size. View: strictly top-down orthographic — the camera looks straight down at 90 degrees, no perspective, no isometric or 3/4 angle. Light comes from the top-left. No cast shadows and no ground shadows.

Layout: exactly 8 separate objects in an even grid of 4 columns and 2 rows, each centered in its own cell with wide empty space around it. No object touches or overlaps another object or the image edge. Background: one flat, solid, pure magenta color (#FF00FF) everywhere — no gradient, no texture, no floor, no shadow, no glow. Do not use magenta or pink inside the objects. No text, letters, numbers, labels, grid lines, frames or watermark. Square image, highest resolution.
```
