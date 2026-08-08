# Status Badges — editor setup

Shipped as 0.15.0. The code is written and compile-clean; **nothing shows on screen
until the three assets below exist**, because `StatusBadgeLibrary` is loaded from
`Resources` and carries the row prefab, the badge prefab and the icons.

The display is generic: anything implementing `IStatusBadgeSource` appears under its
object automatically. Fire, Charge and the Cannon's fuse implement it today; Signal
Beacon's Charge-10 bar and phase 5's Bomb fuses will only need the interface, not
edits to the display.

---

## What shows, and when

| Status | Label | Visible when |
| --- | --- | --- |
| Fire | remaining 4s stacks, e.g. `3` | only while alight |
| Charge (consumer) | `4/10` against its requirement | **always**, zero included |
| Charge (carrier / ball) | `4` | only while holding ≥1 |
| Cannon fuse | `7/15` | always |
| Bomb fuse | `3/8` hits to detonation | whenever a threshold is set |

Fire is the deliberate exception to "always visible for capable components": every
object on the board is flammable and has no requirement to advertise, so a permanent
`0` would put an icon under all ~20 components at once.

Balls use the exact same path — they carry `FireStatus` and `ChargeStatus` too, so a
lit ball holding Charge shows both.

---

## 1. `StatusBadge.prefab` — one icon + number

Small UI prefab. Put it anywhere sensible (`Assets/Prefabs/UI/` matches the project).

```
StatusBadge                 RectTransform
                            HorizontalLayoutGroup
                              Child Alignment: Middle Center
                              Spacing: 4
                              Control Child Size: Width ✔ Height ✔
                              Child Force Expand: off
                            ContentSizeFitter
                              Horizontal Fit: Preferred Size
                            StatusBadge (script)
├── Icon                    Image           → assign to the script's "Icon" field
│                           LayoutElement: Preferred Width 32, Preferred Height 32
└── Label                   TextMeshProUGUI → assign to the script's "Label" field
                            Font: Jersey 10 (matches the hit-count popups)
                            Alignment: Midline Left, Auto Size off
```

Leave `Icon`'s sprite empty — the script assigns it per status.

## 2. `StatusBadgeRow.prefab` — the container

One of these is created per object at runtime. **It is instantiated at the scene
root, not parented to the component**, because `BoardComponent.FixedUpdate` pulses
`localScale` while a component is selected and flippers rotate under input — a
parented row would inherit both. Code drives its position and billboards it every
frame.

```
StatusBadgeRow              RectTransform  (Width 200, Height 48 — the fitter resizes it)
                            Canvas
                              Render Mode: World Space
                              Sorting Layer / Order in Layer: above the board
                            CanvasScaler
                              Dynamic Pixels Per Unit: 10   ← without this the text is mush
                            HorizontalLayoutGroup
                              Child Alignment: Middle Center
                              Spacing: 8
                              Control Child Size: Width ✔ Height ✔
                              Child Force Expand: off
                            ContentSizeFitter
                              Horizontal Fit: Preferred Size
                            StatusBadgeRow (script)
                              Badge Parent: leave empty (layout group is on the root)
```

Two things that will bite:

- **Layer.** The row keeps whatever layer the prefab has. It must be inside the
  gameplay camera's culling mask or nothing draws.
- **Sorting.** World-space canvases sort by `Order in Layer` against other
  transparent geometry. If badges vanish behind the playfield, raise it.

## 3. `Assets/Resources/StatusBadgeLibrary.asset`

`Create ▸ Pinball ▸ Status Badge Library`. **Must sit directly in `Resources/`** —
it is loaded by the literal path `"StatusBadgeLibrary"`, same as `FireVfxLibrary`.

| Field | Set to |
| --- | --- |
| Row Prefab | `StatusBadgeRow.prefab` |
| Badge Prefab | `StatusBadge.prefab` |
| Fire Icon | flame sprite |
| Charge Icon | lightning bolt sprite |
| Fuse Icon | fuse / bomb sprite |
| Fire / Charge / Fuse Tint | pre-filled with orange, blue, yellow |
| Offset | `(0, -0.6, 0)` — camera-relative, so −Y reads as "below" on screen |
| Offset In Camera Space | on |
| Row Scale | `0.01` — start here, it is the number you will actually tune |

**The three icons do not exist in the project.** Nothing under
`Assets/ArtAssets/Sprites/` is a flame, bolt or fuse. They need drawing or sourcing,
imported as Sprite (2D and UI). Until they exist the badges still work — you get
numbers with a blank icon slot.

## 4. Tuning pass

- `Row Scale` and `Offset` are global. Get them right on a bumper first.
- Per-component nudges live on the `StatusBadgeDisplay` that appears on the object at
  runtime: `Anchor` (hang the row off a child transform instead of the origin) and
  `Extra Offset`. Set these on the **prefab** by adding `StatusBadgeDisplay` manually
  — otherwise the runtime-added one uses defaults.
- Moore's Launcher is a flipper that banks Charge, so it gets a badge and it will
  swing with the flipper. Assign an `Anchor` on a non-rotating child if that reads
  badly.
- Billboarding uses `Camera.main`, in line with the other 29 `Camera.main` uses in the
  project. If badges face the wrong way during a shop or nav-map transition, that is
  why — the gameplay camera has to hold the `MainCamera` tag during a round.

---

## Fire VFX — what changed and what to check

Fire VFX was **already working** before this change: `FireStatus` spawns flames from
`FireVfxLibrary` for anything lit through `FireStatusUtility`, components and balls
alike, and the asset in `Resources/` has its prefab wired.

What is wrong is placement — flames are parented to the object's origin at a flat
`0.5` scale, so any component whose mesh sits on a child or whose pivot is off the
visual centre burns in the wrong spot. `FireStatus` now takes a `Vfx Anchor` to fix
that per component.

**There is deliberately no automatic fallback.** An earlier draft anchored to the
first child collider when none was assigned; that is a trap. The VFX is *parented* to
the anchor and `FireVfxLibrary` then applies its scale trim as a **local** scale, so
anchoring to a child carrying a non-unit scale silently resizes the flames — and it
would have done so to every fire-capable component at once (Engine, Shadow Lamp,
Capacitor, Generator, and the Molotov / Charcoal / Fireball balls), not just the ones
that needed it. Default behaviour is unchanged from what ships today.

**Editor work:** light each component in play mode and check where the flames land.
Anywhere it looks wrong, add `FireStatus` to that prefab and set `Vfx Anchor` to the
visual — pick a transform at unit scale, or expect to retune `Fire Vfx Scale`. That is
also the only way to set the per-object `Fire Vfx Prefab` override, since a
runtime-added status can never have inspector-wired fields, which is the whole reason
`FireVfxLibrary` exists.

Also worth clearing while you are in there: `Resources/FireVfxLibrary.asset` still
carries dead `fueledVfxPrefab` / `fueledVfxScale` / `fueledVfxEmissionMultiplier`
keys from the deleted Fuel half. Harmless, just noise.

---

## The balance change you should watch

Fire now **stacks additively with no ceiling** — re-lighting adds a full 4s to
whatever is left rather than resetting to 4s.

`FIRE_CHARGE_REFACTOR_PLAN.md` §10 already flagged that fire is self-propagating
after phase 3: a burning Fireworks lights ~2 components/sec, Short Circuit does not
even need to be alight, and each of those burns and ticks in turn. Additive stacking
with no cap makes an unbounded burn **strictly easier to reach**, not harder. On a
10–20 component board, one lit Fireworks can plausibly leave everything ablaze and
climbing.

This is bounded numerically — `maxScoreMultiplier` still caps each component at 10x —
so it is a readability and intent problem, not a score blowup. Two levers if the
playtest confirms it:

1. A serialized `maxBurnSeconds` clamp on `FireStatus` (12s ≈ 3 stacks).
2. The §10 lever: stop the spread rolls firing on burn ticks, so Fireworks and Short
   Circuit only roll on real ball collisions.

Lever 2 is the more surgical of the two — it turns both from self-sustaining engines
into hit-driven ones without touching stacking at all.

One real bug was fixed on the way through: `Ignite()` used to zero `_tickAccumulator`
on **every** call, including re-lights. Anything re-lit faster than one tick interval
(2/s by default) had its accumulator reset before it ever reached the interval, so it
never ticked at all. It is now only reset on a fresh light.
