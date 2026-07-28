# Fire / Charge / Detonate / Kinetic Refactor Plan

_Drafted with Claude Code (claude-opus-5) for jjmil on 2026-07-27._
_Revised 2026-07-28 with decisions from design review._

**Status: phases 1 and 2 are complete and compiling (0 errors).** Shipped as 0.13.0. §9 has
the editor checklist and what to watch for in the playtest; phases 3–5 (the 14 new items) are
untouched and ready to start.

§1–§6 are settled. §7 holds four per-item tuning values taken as defaults; §8 records what
was verified against the codebase.

---

## 0. Where we already are

A meaningful chunk of the spec is already built, several pieces with the exact numbers in
the design doc.

**Already matches the spec** — only needs re-pointing at the new shared systems

| Item | File | Spec | Current |
| --- | --- | --- | --- |
| Shadow Lamp | `ShadowLampComponent.cs` | Project Holoball 3s while On Fire | identical |
| Capacitor | `CapacitorComponent.cs` | Charge 2, activate 4 nearest | identical |
| Generator | `GeneratorComponent.cs` | 30% to give 1 Charge on collision | identical |
| Transistor | `TransistorBall.cs` | 20% self-Charge on collision | identical |
| D-Battery | `DBatteryBall.cs` | 2 Charge on launch | identical |
| Moore's Launcher | `MooresLauncherFlipper.cs` | Charge 5 → create Transistor | identical |
| Pandora's Box | `PandorasBall.cs` | 20% → Charge or light on Fire | identical |

**Exists, changes meaning**

- `EngineComponent` — loses its bespoke `+25 base points per burning hit` ramp (that becomes
  the global Fire rule) and collapses to "Charge 1 → ignite self."
- `FireballBall` — inverted. Today the ball burns itself and detonates on burnout. Now it
  lights *components* on Fire, 5 times, with a reignite roll. No self-burn, no detonation.

**Being deleted**

- `FireComponent` — an older, unrelated "on fire" system (N hits in T seconds multiplies its
  own score). Two things called fire is the main source of confusion. **Verified safe:** it's
  only referenced by the `FireBumper` / `FireTarget` prefabs and their two
  `BoardComponentDefinition` assets — it is not placed in any board scene. Those two prefabs
  are good candidates to repurpose as Matchbox / Short Circuit so the art isn't wasted.
- All of Fuel / Flammable / stack persistence (see §1.1).
- `GasStationComponent` — cut from this pass, re-spec separately.

**Genuinely new (14 items)** — Balls: Flint, Bomb, MOAB, Cannonball. Components: Matchbox,
Fireworks, Cannon, Short Circuit, Signal Beacon, Tesla Coil, Propane Tank, Rubber Band,
Spring, Big Red Button.

---

## 1. The four keywords — settled behaviour

### 1.1 Fire

**Today:** `FireStatus` (abstract) + `BallFireStatus` + `ComponentFireStatus` +
`FireStatusUtility` + Flammable stacks + Fuel + a burn-seconds countdown + per-slot stack
persistence in `BallLoadoutController` (36 refs) + a `BallSpawner` sync pass. Five concepts
to explain a burning bumper.

**New:** one `FireStatus` component. No ball/component split, no Flammable, no Fuel, no
stacks, no persistence.

```
Ignite()                  -> starts a 4-second burn, resets the ramp
burnSeconds        = 4    (serialized)
activationsPerSecond = 2  (serialized, the "1-4 per second" knob)
scoreRampPerActivation = 0.25, compounding within a burn, resets on extinguish
Extinguished event
```

A 4-second burn at 2/s is 8 activations, ending at `base × 1.25⁷ ≈ 4.77×`. Both the duration
and the rate are per-object serialized fields, so an item or upgrade can push toward the 4/s
end of the range.

**How the ramp is applied.** Nothing in the `ActivateAsBurnTick → ActivateAsIfHit → AddScore
→ scoreManager.AddScore(amountToScore, …)` chain accepts a multiplier today. The old Engine
worked around this by mutating `amountToScore` and subtracting the delta on burnout — exactly
the bookkeeping being deleted. Replacement: **a `ScoreManager` scope**, mirroring the existing
`WithWeakShake(Action)` pattern:

```
scoreManager.WithScoreMultiplier(rampMultiplier, () => component.ActivateAsBurnTick());
```

Zero signature churn on `BoardComponent` and its ~6 overriders, and it's already idiomatic in
this codebase.

**The ramp applies to real ball hits too.** A component 6 ticks into a burn sits at ×3.05, and
a player hit on it scores ×3.05. Burning components are hot targets worth aiming at — which is
what the old Engine's base-score mutation effectively did. Ball hits *read* the ramp but do
not *advance* it; only fire's own activations do.

**Re-ignition refreshes the timer and keeps the ramp.** Fuel was the only way to extend a burn
and it's gone, so `Ignite()` on an already-burning object no longer early-returns: it resets
`burnSeconds` to 4 while the ramp keeps climbing from where it was. Stacking fire sources onto
one component is the payoff, and it's what makes Fireworks' 50%×2 and Short Circuit's 30%
worth anything once the board is already alight.

**Fire is granted only by things that say they grant it.** Automatic contact spread is
deleted — with Flint at 25%, Matchbox 40%, Short Circuit 30% and Fireworks 50%×2, auto-spread
would make every one of those numbers meaningless.

**The five Fuel-based items are re-spec'd, not deleted:**

| Item | New behaviour |
| --- | --- |
| Lighter (Kicker) | On collision: light the ball on Fire. (Already effectively this.) |
| Matchstick Plunger | On launch: light the ball on Fire. |
| Charcoal (Ball) | On collision: light the other object on Fire. — rate open, §7 |
| Unfinished Molotov (Ball) | On collision: light both on Fire; small chance to break. — rate open, §7 |
| Gas Station | **Cut from this pass.** Its whole design was spraying Fuel board-wide. |

### 1.2 Charge

Charge is **a stacking resource**, and the amount held is *how many components the proc
reaches*:

- A ball holding **N** Charge rolls **50% on every collision**; on success it activates the
  **N nearest** components. Charge is not consumed by this proc.
- Charge is spent by **transferring into a component that requires Charge**, on collision.
  Once that component reaches its threshold it fires its ability and the Charge is consumed.
- **Only components that require Charge can hold Charge.** Everything else is a pass-through.
- **The proc is ball-only.** A component banking Charge toward its threshold does *not* roll
  the 50% on its own activations — it's filling a meter, and the meter's payoff is its
  ability. Crisp rule: *carrying* Charge on a ball procs; *storing* Charge in a component pays
  off at the threshold.
- **Hitting a Charge-requiring component transfers only — it does not proc.** The two uses of
  Charge never fire at once, so a hit on a Capacitor is unambiguously a deposit.
- "N nearest" searches within **10 units**, matching Capacitor's existing search radius, and
  skips Flippers and Portals.

Implementation changes from today:

- Merge `BallChargeStatus` / `ComponentChargeStatus` into one `ChargeStatus`.
- **Decay is dropped.** Today Charge bleeds 2/sec after 2 seconds untouched, which makes
  Signal Beacon's 10-Charge requirement nearly unreachable and isn't in the spec.
- Ball-dumps-all-Charge-into-a-consumer transfer is **kept** — it's what makes
  Transistor / D-Battery work.
- `ChargeStatus.Update()` empties out once decay is gone, so its `CanTickNow()` dependency
  disappears with it. `StatusTickGate` (§3) is still worth hoisting for naming, but it ends up
  fire-only rather than shared.

### 1.3 Detonate

New shared helper replacing the ad-hoc `Bomb` prefab + trigger-collection pattern:

```
Detonation.Detonate(origin, radius, scoreMultiplier, depth = 0, visited = new HashSet<int>)
   if depth > maxChainDepth (3): return
   foreach component in OverlapSphere(origin, radius)
      skip Flipper / Portal
      skip if visited already contains component.GetInstanceID()
      visited.Add(component.GetInstanceID())
      component.ActivateAsIfHit()      // may re-enter at depth + 1, same visited set
```

- **The detonator survives.** Bomb detonates every 10 seconds, so destruction is out. MOAB
  and Big Red Button can opt into consuming themselves.
- **The blast fully activates** components rather than only calling `AddScore()` like the
  current `Bomb.Explode()`. That's what enables Propane → Propane chains.
- **The visited set is the real safety mechanism**, not the depth guard. Depth alone bounds
  chain length but not breadth, and doesn't stop A → B → A oscillation inside the budget: a
  radius-6 blast on a dense board reaches ~20 components, and three unguarded levels of that
  is up to 8000 `ActivateAsIfHit` calls in a single frame, each firing `PlayBumperHit`. One
  `HashSet<int>` threaded through the whole cascade caps it at "every component at most once
  per detonation"; the depth guard stays as a backstop.
- **Radii: small 3 / medium 6 / large 12** world units, serialized per item. (Capacitor's
  component search radius is 10, for scale.)

### 1.4 Kinetic

```
KineticScoring.Multiplier(speed) =
    clamp(pow(speed / 8, 2), 0.25, 8)
```

| speed | multiplier |
| --- | --- |
| ≤ 4 m/s | 0.25× (floor) |
| 8 m/s | 1.00× (reference) |
| 12 m/s | 2.25× |
| 16 m/s | 4.00× |
| 20 m/s | 6.25× |
| ≥ 22.6 m/s | 8.00× (cap) |

Board speeds run ~0.5–20 m/s (`BallAntiStallAssist` treats 0.5 as stalled and 20 as fast).
Kinetic on a **component** scales by the speed of the ball that hit it; Kinetic on a **ball**
scales everything that ball hits.

---

## 2. Item-by-item

| Item | Kind | Behaviour |
| --- | --- | --- |
| Fireball | Ball | On collision: light component on Fire. 5 uses. When the 5th is spent, roll 10% → refill to 5, else spent for the launch. |
| Flint | Ball | On collision: 25% light component on Fire. |
| Bomb | Ball | Every 10s: Detonate, radius 6. |
| MOAB | Ball | On collision: 2% → Detonate radius 12 at 500% scoring. |
| Cannonball | Ball | Kinetic. Created by Cannon. |
| Matchbox | Bumper | On activation: 40% light self on Fire. |
| Fireworks | Bumper | On activation while on Fire: 50% → light 2 random components on Fire. |
| Shadow Lamp | Bumper | Behaviour unchanged — Project a 3s Holoball on activation while on Fire. Type references updated. |
| Cannon | Bumper | Fuse 15. On activation, if the fuse is finished, create a Cannonball; reset fuse. |
| Short Circuit | Bumper | On activation: 30% light a random component on Fire. |
| Signal Beacon | Bumper | Charge 10 → Reinforce. **Reinforce is a `Debug.Log` stub for now** — behaviour TBD. |
| Capacitor | Bumper | Behaviour unchanged — Charge 2 → activate the 4 nearest. Type references updated. |
| Tesla Coil | Bumper | On collision: 20% → give 2 Charge to the ball. |
| Generator | Bumper | Behaviour unchanged — on collision: 30% → 1 Charge to the ball. Type references updated. |
| Moore's Launcher | Flipper | Behaviour unchanged — Charge 5 → create a Transistor. Type references updated. |
| Propane Tank | Bumper | On collision: 20% → Detonate radius 6. |
| Rubber Band | Bumper | Kinetic. |
| Spring | Bumper | Kinetic. |
| Engine | Bumper | Charge 1 → light self on Fire. (Ramp bookkeeping removed.) |
| Big Red Button | Bumper | Charge 3 → Detonate the ball. Missile is VFX-only unless you say otherwise (§7). |
| Transistor / D-Battery / Pandora's Box | Balls | Behaviour unchanged. Type references updated. |

**Slings are Bumpers.** Matchbox, Cannon, Generator and Rubber Band extend `Bumper` exactly
as `GeneratorComponent` already does; "sling" is a form-factor distinction handled in the
prefab, not in code.

---

## 3. Shared infrastructure

1. **`StatusTickGate`** — hoist `FireStatusUtility.CanTickNow()` out of the fire utility.
   `ChargeStatus` already depends on it today, which is a bad import.
2. **Board component registry** — Fireworks and Short Circuit both need "a random component,"
   and Charge needs "the N nearest." `FindObjectsByType` per activation is too hot at 2 Hz
   across several burning objects. Cached list maintained by `BoardRoot` / `BoardLoader`.
3. **`Detonation`** static helper with the chain-depth guard.
4. **`KineticScoring.Multiplier(speed)`** static.
5. **Preserve the Flipper/Portal exclusion.** `FireStatusUtility.CanCatchFire` excludes them
   because burn ticks would auto-fire flips and teleports. That guard survives and now also
   covers Charge's activate-nearest and Detonate's radius — `Capacitor` already hand-rolls a
   Portal exclusion for exactly this reason.
6. **Keep `ActivateAsIfHit` / `ActivateAsBurnTick`.** All four keywords ride on this and it
   already works. Not rebuilding it.

**Incidental cleanup:** `EngineComponent`, `LighterComponent`, `ShadowLampComponent` and
`BombComponent` declare `new protected void Awake` / `OnCollisionEnter` (method hiding) while
`Bumper` declares them `protected override`. It works under Unity's dispatch but breaks
polymorphic calls. Normalizing to `override` while these files are open.

---

## 4. Content updates

- `Assets/Editor/SOPopulation/Term-Descriptions.csv` — rewrite Charge, Detonate, On Fire,
  Shock; **delete** Flammable, Fuel, Ignite; **add** Kinetic, Reinforce. Includes fixing the
  `Outlink(s)` column on every touched row.
- `Assets/Editor/SOPopulation/Ball-Descriptions.csv` — rewrite Fireball, Bomb, M.O.A.B.,
  Charcoal, Unfinished Molotov; add Flint, Cannonball.
- `CHANGELOG.md` + `m_text: version X.Y.Z` in `Assets/Scenes/Core/MainMenu.unity` — this is a
  **0.11.0**, up from 0.10.4, with a dated `Contributor:` line.

---

## 5. Division of labour

I write all the `.cs`. I **cannot** author prefabs or ScriptableObject assets, so each of the
14 new items needs, from you in the editor:

- a prefab (mesh, collider, VFX) with the new script attached,
- a `BallDefinition` / `BoardComponentDefinition` asset — `BallDefinitionSetupHelper.cs` and
  `BoardComponentDefinitionSetupHelper.cs` are the existing paths,
- placement in the relevant pool / board scene.

I'll list the exact serialized fields each prefab needs as I write each script, and hand you
a single checklist at the end of each phase.

---

## 6. Sequencing

1. **Core systems** — `FireStatus` rewrite, `ChargeStatus` merge, `StatusTickGate`,
   `Detonation`, `KineticScoring`, component registry. Nothing user-visible yet.
2. **Migrate existing items** — Engine, Capacitor, Generator, Shadow Lamp, Moore's Launcher,
   Transistor, D-Battery, Pandora's Box, Fireball, Lighter, Matchstick, Charcoal, Molotov;
   delete Fuel, `FireComponent`, Gas Station. **The build compiles and the game is playable at
   the end of this step** — playtest checkpoint before anything new is added.
3. **New Fire items** — Flint, Matchbox, Fireworks, Short Circuit, Cannon.
4. **New Charge items** — Signal Beacon, Tesla Coil, Big Red Button.
5. **New Detonate / Kinetic items** — Bomb, MOAB, Propane Tank, Cannonball, Rubber Band,
   Spring.
6. **Content pass** — CSVs, changelog, version bump.

---

## 7. Still open — proceeding on these defaults unless corrected

All four are per-item tuning values, not architecture. None block any phase; each is a single
serialized field I'll expose in the inspector so you can change it without a code edit.

1. **Cannon's "Fuse length: 15"** — 15 activations, or 15 seconds? Assuming **activations**.
2. **Charcoal and Molotov rates** — both are now "on collision, light on Fire." At 100% they
   badly outclass Flint (25%) and Matchbox (40%). Assuming **Charcoal 50%**, **Molotov 60%**
   plus its existing 5% break chance.
3. **Big Red Button's missile** — VFX only, or a real projectile that travels then detonates?
   Assuming **VFX only** for v1.
4. **Contributor name** for the changelog entry — defaulting to **JJ** (the git author here).
5. **The Fire ramp cap (`maxScoreMultiplier`, default 10x).** Added during implementation, not
   from the design doc: duration-based fire plus refresh-keeps-ramp means a self-lighting item
   compounds without ever burning out. Needs a number you're happy with.

---

## 8. Verified during planning

- **Flammable stacks are not in the save schema.** `Scripts/Profile/` has no reference to
  Flammable, Fuel or Charge, so deleting `BallLoadoutController._extraFlammableStacksBySlot`
  can't break deserialization or orphan data on existing profiles. No migration needed.
- **There is only one `ActiveBalls` list.** `GameRulesManager.ActiveBalls` forwards straight
  to `ballSpawner.ActiveBalls`, so balls registered by `MooresLauncherFlipper` and by
  `Projector` land in the same registry. Cannon's Cannonball can use either path safely.
- **`FireComponent` is not on any live board** — only the `FireBumper` / `FireTarget` prefabs
  and their two `BoardComponentDefinition` shop assets reference it.

---

## 9. Phases 1-2: what shipped, and what you need to do in the editor

Compile-checked with `dotnet build` against the project's own reference set: **0 errors**, and
all 31 warnings are pre-existing (TMP obsolescence, unused fields).

### New files

| File | Role |
| --- | --- |
| `Scripts/StatusEffects/StatusTickGate.cs` | Live-play gate, hoisted out of `FireStatusUtility` |
| `Scripts/StatusEffects/StatusTargeting.cs` | The one Flipper/Portal exclusion, shared by all keywords |
| `Scripts/BoardComponents/BoardComponentRegistry.cs` | Self-registering component index |
| `Scripts/Scoring/Detonation.cs` | Blast radius with visited-set + depth guard |
| `Scripts/Scoring/KineticScoring.cs` | `clamp((speed / 8)^2, 0.25, 8)` |

### Deleted

**Scripts:** `BallFireStatus`, `ComponentFireStatus`, `BallChargeStatus`,
`ComponentChargeStatus`, `FireComponent`, `GasStationComponent`.

**Content — three components cut whole**, prefab + definition + every pool reference:

| Component | Why | Pools cleaned |
| --- | --- | --- |
| Fire Bumper | ran on the deleted legacy `FireComponent` | `starterComponents`, localization CSV |
| Fire Target | same | `starterComponents`, localization CSV |
| Gas Station | its entire design was spraying Fuel board-wide | `starterComponents`, Silverwolf allowlist, Challenge_NA 1 allowlist |

No dangling GUID references remain, and the affected pools still hold 9 / 3 / 3 entries. Four
orphan `component.FireBumper.*` / `component.FireTarget.*` keys remain in the Unity string
tables — unused and harmless; clear them from the Tables window when convenient (the CSV
authoring source is already clean).

Gas Station was cut rather than re-spec'd. If it ever comes back, the translation that
preserved its identity was: pay 1 Credit on collision to light the ball; while the station is
itself on Fire, hits go free and each one lights 3 random components via
`FireStatusUtility.LightRandomComponents`.

### Prefabs already repointed for you

The eight prefabs that carried `BallFireStatus` / `ComponentFireStatus` were rewritten in YAML
to reference the unified `FireStatus` (and the dead Fuel fields stripped), so **none of them
lost a component or shows a missing-script warning**: Charcoal, Fireball, Unfinished Molotov,
ShadowLampBumper, CapacitorBumper, GeneratorBumper, EngineBumper, GasStationBumper.

### What you need to do

1. **Open the editor and let it reimport** — the five new `.cs` files need `.meta` files
   generated, which only Unity can do.
2. **Set the burn knobs where you want them.** Every object that can catch fire exposes
   `burnSeconds` (4), `activationsPerSecond` (2) and `scoreRampPerActivation` (0.25) on its
   `FireStatus`. Runtime-attached statuses take the code defaults; only prefabs that carry a
   `FireStatus` in advance can be tuned in the inspector.
3. **Fireball's prefab has stale fields.** `explosionPrefab`, `explosionActiveTime` and the
   shake settings are no longer read — the ball does not detonate anymore. Harmless, but
   worth clearing when you next touch it.

### Known limits of what was checked

The build is compile-clean, but nothing has *run* — Unity has not been opened and no scene has
been played. In particular:

- **Four of the new systems have no callers yet.** `Detonation`, `KineticScoring`,
  `BoardComponentRegistry.GetRandom` and `FireStatusUtility.LightRandomComponents` are written
  and compiling, but nothing invokes them until phases 3-5. They are unexercised.
- **`Detonation`'s visited set is not yet wired for real chains.** `ActivateAsIfHit()` takes no
  depth or visited parameters, so a blast triggered from inside another blast currently starts
  a fresh cascade with a fresh set. Needs a static current-cascade on `Detonation` that nested
  calls pick up — resolve in phase 5, alongside the related point that Propane Tank detonates
  on *collision*, so it must override `ActivateAsIfHit` (as `BombComponent` does) for the
  Propane → Propane chain to happen at all.
- **Runtime-attached statuses cannot be tuned in the inspector.** `FireStatus` on components
  and `ChargeStatus` on balls are added via `AddComponent`, so `burnSeconds`,
  `activationsPerSecond`, `chanceToActivateNearest` and `nearestSearchRadius` always take the
  code defaults on those objects. Only prefabs that carry the status in advance (the eight
  above) expose them. Pre-attach the status to anything you want to tune per-object.
- **Capacitor's targeting semantics changed.** It moved from `Physics.OverlapSphere` (needs a
  collider, respects layers) to the registry plus a distance test (any enabled
  `BoardComponent`, collider or not). Behaviourally close, not identical.
- **The registry indexes every enabled `BoardComponent`, including shop-offer displays.**
  Distance filtering hides this today, but `LightRandomComponents` has no distance filter and
  will be able to light a shop offer once Fireworks / Short Circuit land. Worth a filter in
  phase 3.
- **Two ramps can multiply.** A burning ball hitting a burning component applies the ball's
  ramp as a scope *and* the component's own ramp via `AddScore`. Both compound. Left as-is
  because it is a rare and rewarding case, but it was not specified — say if you want it
  capped to one.

### Worth watching in the playtest

- **Burn ramp pacing.** 4s at 2/s is 8 activations ending at ~4.8x. If that reads as too hot
  or too cold, `activationsPerSecond` is the knob — the ramp compounds, so small changes here
  move the ceiling a lot (4/s doubles the activation count to ~28x).
- **The ramp cap is a guess.** Duration-based fire plus refresh-keeps-ramp means anything that
  can re-light itself never burns out and compounds forever — Molotov lights itself on 60% of
  its own collisions, and a ball parked on a Lighter kicker does the same. `maxScoreMultiplier`
  defaults to **10x** to bound that; the natural un-refreshed ceiling is 4.8x, so 10x leaves
  stacking meaningful without a blowup. This number is not from the design doc — it needs your
  call (§7).
- **Charge procs.** A ball with several Charge now activates that many nearby components on a
  coin flip per collision. Feels good in theory; it is the single biggest new source of
  activations and worth watching for noise.
- **Moore's Launcher should finally be reachable.** 0.12.0's playtest note said 5 Charge was
  never hit because the bank decayed at 2/sec. Decay is gone, so it should trigger now — but
  the other note in that entry still stands: an upstream Capacitor drains couriers before they
  reach the flipper.
