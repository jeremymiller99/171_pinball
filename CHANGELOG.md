# Changelog

Version format: `0.MAJOR.MINOR`
- **MAJOR** (second digit) — big updates / new features / systems
- **MINOR** (third digit) — small changes / tweaks / fixes

Version is displayed on the main menu (`Assets/Scenes/Core/MainMenu.unity`).

Entries below 0.4.6 were reconstructed retroactively from git history (commits `ef9ce39`..`ab6744e`).

---

## 0.17.0 — FTUE Phase 2: profile fields, save migration and the narrator's name
_2026-08-08 · Contributor: JJ_

Persists whether a profile has finished the FTUE, and what the player named the AI.
`FTUE_PLAN.md` ticket 6. Save format goes to **version 7**.

- `ProfileSaveData` gains `hasCompletedFtue` and `aiName`. Both ride one version bump; a new
  string field needs no migration logic, since JsonUtility leaves absent fields at their
  initializer.
- **Existing players are grandfathered.** Without this, `hasCompletedFtue` would default false on
  every save on disk and the Phase 5 boot rule would force every existing player through the
  tutorial. The `version < 7` block sets it true unconditionally — on **version alone**, not on a
  "has this player actually played" predicate, which reads as more careful but misses the player
  who made a profile, launched once and quit. Version has zero false positives, the right trade
  when being wrong means dragging an existing player through the FTUE.
- Also closes the `version <= 0` hole the plan flagged: that branch stamps the current version and
  skips every migration below it, so it now grants the grandfather itself. A version-0 profile is
  by definition an existing one.
- New profiles are unaffected — `CreateNewProfile` stamps `currentVersion`, so they never enter the
  migration and keep `hasCompletedFtue` false. `R` in the main menu still re-arms the FTUE, since
  `ResetSlot` goes through `CreateNewProfile`.
- `FtueNarrator` owns the naming rules: trim, a 16-character cap, and the fallback name **`Al`** —
  which is intentional and must not be "corrected" to "AI". `ProfileService` storage stays dumb;
  the FTUE layer decides what a valid name is.
- **Sanitizing strips `<` and `>`.** TMP parses rich text by default and this string is written
  straight into a label, so a name of `<size=500%>` would blow the panel apart and `<sprite=0>`
  would draw something the player never typed. Control characters go too.
- The stored name is player-authored text and is deliberately kept out of analytics, Steam and
  every log line.
- `FtueDialogueView` now reads the character cap from `FtueNarrator` instead of declaring its own,
  so the field limit and the stored limit cannot drift apart.

---

## 0.16.9 — FTUE Phase 2: narrator dialogue view and input-prompt strings
_2026-08-07 · Contributor: JJ_

The presentation half of the FTUE narrator. `FTUE_PLAN.md` ticket 5. Two new files, **no existing
file modified**.

- `FtueDialogueView` sits on the root of a dialogue prefab and fills speaker / body / prompt
  slots, following the `TutorialPanelView` pattern. One component drives both prefabs: an ordinary
  line, and the naming panel, which also shows a `TMP_InputField`.
- **No continue button.** A line types itself out; the first input fills the box, the second
  dismisses it. A button slot remains for panels that want one, but nothing requires it.
- The typewriter runs on **unscaled** time — the director pauses the game for most beats, and a
  scaled typewriter would sit frozen mid-sentence. It reveals via TMP's `maxVisibleCharacters`
  rather than rebuilding the string, so rich-text tags are not counted and layout does not thrash.
- **Escape is excluded** from "any input": it belongs to the pause menu, and swallowing it would
  leave the player unable to pause while a line is on screen.
- **The naming panel does not advance on any input** — the player is about to type, and the first
  keystroke would dismiss it. It types out, then reveals and focuses the field, and waits for
  Enter. The field stays hidden while the line is still typing so it never invites input the panel
  is not ready for.
- Input is ignored on the frame a phase begins, so the press that opened a panel, or the press
  that skipped its typewriter, cannot also be read as the press that dismisses it. Callbacks clear
  the moment they fire, so a click and a key press in the same frame cannot double-advance.
- `FtueBindings.Display(reference)` resolves an action's keyboard binding name, so copy can say
  "Hold {1}" and track the bindings instead of drifting from them — the original brief said "hold
  Enter", which had not been true since Launch moved to Space. Guards a null reference and an
  out-of-range index, both of which otherwise throw, and returns "?" rather than an empty prompt.
- It deliberately always reports the **keyboard** binding rather than switching when a gamepad is
  connected: connected is not in use, and a player with a controller plugged in but hands on the
  keyboard would get the wrong prompt. Doing it properly needs last-used-device tracking, which is
  wider than the tutorial should reach.
- **No new formatting system.** `LocalizedUI.Format` already provides ordered-token substitution
  with a double fallback for broken translator placeholders, which is exactly what the plan called
  for as new work. The FTUE uses it.
- **No `Resources` dependency.** The plan had the prefab loaded from `Resources/FTUE/`, copying
  `BasicTutorialController` — but that controller only needs Resources because it is a
  `DontDestroyOnLoad` singleton with no scene to hold references. The director is in the board
  scene, so it takes direct prefab references instead, removing a runtime load and a
  missing-at-runtime failure mode.

---

## 0.16.8 — FTUE Phase 1: the legacy tutorial panels stand down on the FTUE board
_2026-08-07 · Contributor: JJ_

`BasicTutorialController`'s CONTROLS, LEVEL UP and SHOP panels no longer appear on the FTUE board.
`FTUE_PLAN.md` ticket 4.

- The clash is not hypothetical: that controller gates only on `ProfileService.HasSeen*Tutorial()`,
  so a **fresh profile** — precisely the FTUE case — is exactly when both it and the FTUE narrator
  want to run, and its panels would render on top of the dialogue.
- Guards the three `Show*Panel` sinks rather than their callers, matching the approach in 0.16.7.
  Beyond covering any future caller, this keeps `PauseAndLockInput` from running at all: it zeroes
  `timeScale` and disables `PinballLauncher`/`PinballFlipper` **by type-name string search**, which
  would fight the FTUE's `GameplayInputGate`-based gating.
- One shared `SuppressedByFtue` property carries the reasoning so it is documented once rather
  than three times. 19 lines added, none removed.
- No ordering risk: `RunFlowController` finishes loading the board scene — and with it the
  director's `OnEnable` — before `StartRun` fires `RoundStarted`, so `FtueState.Active` is already
  true the first time this controller could act.
- **Still outstanding (ticket 12/13):** marking `hasSeenFirstPlayTutorial`,
  `hasSeenLevelUpTutorial` and `hasSeenShopTutorial` as seen when the FTUE completes, so these
  panels do not appear on the player's first normal run either.

---

## 0.16.7 — FTUE Phase 1: the tutorial cannot be lost
_2026-08-07 · Contributor: JJ_

The FTUE board now hands the ball back on every drain and never shows the round-failed panel.
`FTUE_PLAN.md` ticket 3. First ticket to touch shared code; both edits are guards on
`FtueState.SuppressRoundFailure`, which is false on every other board.

- `GameRulesManager.ShowRoundFailed` early-outs under the FTUE. Guarding the single sink rather
  than each caller covers **all four** routes into failure — the drain, the empty first spawn in
  `StartRound`, the public `TriggerRoundFailed`, and `PiggyBankBall`. The plan had listed three;
  a full grep found the fourth, which is the argument for guarding the sink.
- The early-out also suppresses the Steam and local leaderboard uploads, `LogRunHighScore` and
  `LogRunLevelReached` that `ShowRoundFailed` performs — none of which should fire for a tutorial.
- `DrainHandler` lets the existing ball-save path always fire in the tutorial instead of only
  inside the 8s window, which a player reading a dialogue box would otherwise fall out of. One
  clause; `TryReturnSavedBallToHand` already leaves the loadout untouched and re-serves the same
  ball at the launcher, VFX included. `eligibleForBallSave` still gates it, so balls that consume
  themselves by design (Molotov, Holoball) are not resurrected.
- If the save ever fails to hold, the guard re-serves via `ActivateNextBall` — which spawns at the
  board spawn point when the hand is empty — and logs a warning, rather than stranding the player
  with no ball. No double-spawn: both callers only reach `ShowRoundFailed` on the branch where
  they did not spawn.
- `DrainHandler.IsBallSaveArmed` gets the same guard, so the board's ball save lamp stays lit for
  the whole ball in the tutorial and the player learns to read it. `BallSaveLight` is unchanged —
  it already drives off this one property. The multiball exclusion deliberately still applies:
  a drain alongside other balls bypasses the save on every board including the FTUE, so a lamp
  that stayed lit there would be promising a save the drain will not honour.

---

## 0.16.6 — FTUE Phase 1: shared state flag and director shell
_2026-08-07 · Contributor: JJ_

Adds the tutorial's shared state contract and the component that owns it. No behaviour changes on
its own — this is the seam every later FTUE ticket guards against. `FTUE_PLAN.md` ticket 2.

- Two new files, `Assets/Scripts/FTUE/FtueState.cs` and `FtueDirector.cs`. **No existing file was
  modified**, so the other boards are unaffected by construction.
- `FtueState.Active` is derived from **ownership of a live `FtueDirector`**, mirroring
  `GameplayInputGate`, rather than being a plain static bool. The director exists only in
  Board_FTUE; unloading that board destroys it, and Unity reports a destroyed object as null, so
  the flag lowers itself.
- This replaces the plan's original "call `FtueState.Reset()` at the start of every run"
  requirement, and is stronger: the dangerous case was FTUE → main menu → a normal run inside one
  session, where a static bool survives. Ownership cannot be *forgotten* on a path nobody wrote
  cleanup for — a Quit button, an exception mid-beat, a play-mode stop — and it needs no edit to
  `GameRulesManager`. `Reset()` remains as an explicit valve for the completion beat.
- `Deactivate` ignores a caller that is not the current owner, so a late teardown from a previous
  director cannot switch off a live tutorial.
- Shop pool overrides are deliberately **not** included yet; they land with the code that reads
  them (ticket 9) rather than sitting unused.

---

## 0.16.5 — FTUE Phase 0: editor-only launcher for Board_FTUE
_2026-08-07 · Contributor: JJ_

Adds `Pinball/FTUE/Play FTUE Board`, an editor menu item that opens GameplayCore and enters play
mode with a `GameSession` configured for the FTUE mission, board and ship. Without it Board_FTUE
is unreachable — `RunFlowController` finds no board in the session and bounces to the main menu.
First step of `FTUE_PLAN.md` Phase 0.

- One new file, `Assets/Scripts/FTUE/FtueDebugBoot.cs`. **No existing file was modified**, so
  Board_Alpha, Board_NA, Board_NA 1 and Board_Spinners are untouched by construction.
- The whole file is `#if UNITY_EDITOR`-guarded and contributes nothing to a player build.
- It lives under `Assets/Scripts` rather than `Assets/Editor` for two reasons:
  `RuntimeInitializeOnLoadMethod` is not invoked for editor assemblies, and it is the only hook
  that reliably runs before `RunFlowController.Start` reads the session — `playModeStateChanged`
  races it. Keeping it in Assembly-CSharp also means the headless compile check covers it, which
  it would not in an Editor folder.
- The request is held in `SessionState`, not `EditorPrefs`, so it applies to the next play
  session only and leaves nothing on disk. It is cleared before the work runs, so a thrown
  exception cannot strand the flag on.
- `GameSession.GenerateRounds` is deliberately not called: nothing in the shipped flow calls it
  either, and it would mark every fifth round a Devil round for the tutorial.
- Temporary scaffolding. Phase 5 replaces it with the real boot rule; delete the file then.

---

## 0.16.4 — Tooltips use the custom dollar sign
_2026-08-05 · Contributor: JJ_

Tooltip text now renders `$` with the same custom glyph the coins readout on the board canvas
uses, instead of the stock one.

- The board canvas Coins label uses `Bacteria12_Pinballistic SDF`; every text in
  `Tooltip Panel.prefab` and `Header Panel.prefab` was on plain `Bacteria 12 SDF`. Both prefabs
  now point at the Pinballistic asset and its default material — the material has to move with
  the font asset or TMP samples the new glyph rects out of the old atlas.
- The two font assets are the same typeface: all 96 characters have identical em-normalized
  width, height, bearing and advance except `$`, which is wider in Pinballistic (0.8125 em vs
  0.75 em) at the same advance. Their materials match on the shader and all 51 float and colour
  properties; only the atlas texture differs. So the swap changes the dollar sign and nothing
  else.
- Applied to every text in both prefabs rather than just the price fields, because item
  descriptions carry dollar amounts too (`Earn +$1 for every 12 components hit.`), as does the
  header's "Drag to Buy for: $5".
- Nothing overrides these at the instance level — `TooltipManager` and `TooltipHeaderManager`
  instantiate the prefabs at runtime from a prefab reference, and no code assigns `font` or
  `fontSharedMaterial` on tooltip text.

---

## 0.16.3 — Shop items you can't afford are still inspectable
_2026-08-05 · Contributor: JJ_

Clicking a shop offer the player cannot afford now opens its inspect tooltip. Only the purchase
is blocked; reading the item is not.

- `RenderTextureRaycaster.HandleClick` bailed out of the whole method on a failed affordability
  check. That `return` sat above the tooltip resolution block, so the click both played the
  failed-purchase sound and — via the `ClearHover()` at the top of the method — actively hid any
  tooltip already open. The affordability check now only guards the drag that starts a purchase;
  execution falls through to the tooltip as it does for any other clickable object.
- The early return was also skipping hand-ball drag setup, `ShopButton3D.OnClick()`, the outline
  and pulse highlight, and the `onObjectClicked` event whenever the raycast happened to land on
  an unaffordable offer. Those all run again now.
- Hover and controller-navigation tooltips were never gated on price, so this only changes the
  mouse-click inspect path. Purchase remains guarded at the transaction by
  `UnifiedShopController.TryPayForOffer` / `CoinController.TrySpendCoins`.

---

## 0.16.2 — Ball saved VFX pops at the lamp
_2026-08-05 · Contributor: JJ_

Saving a ball now pops a particle effect at the board's ball save lamp, wired the same way as
the Power Surge VFX.

- The prefab list lives on `LevelUpVFXTrigger` as `ballSavePrefabs` / `ballSaveScale` /
  `ballSaveLifetime`, next to `powerSurgePrefabs`, keeping to that script's stated rule that all
  board VFX is configured in one place. New `SpawnBallSaveVFX(Vector3)` mirrors
  `SpawnPowerSurgeVFX` exactly — random pick from the list, uniform scale, auto-destroy.
- `BallSaveLight` now registers in the `ServiceLocator` and exposes `VfxPosition`, so
  `DrainHandler` (a different scene) can ask the board where its lamp is. Optional `vfxPoint`
  child transform nudges the pop off the cylinder; empty uses the lamp's own position.
- The pop fires at the **commit point**, once the ball has actually been returned to the hand —
  not where the save is first decided. `goingToShop` can flip during the score tally (the player
  hitting the shop button mid-drain), and the routine bails out there without saving or
  consuming; popping early would show a save that never landed. The tally is only ~0.5s
  (`moveToRoundTotalDuration` 0.45 + `endHoldDuration` 0.05), so the delay is not perceptible,
  and the effect lands as the ball arrives back at the launcher rather than on top of the score
  fly-up.
- Both halves are board-scene-owned, so a board with no lamp or no prefabs assigned just gets no
  effect rather than an error.
- Gear-menu debug entry `Debug/Spawn Ball Save VFX` on `LevelUpVFXTrigger` pops one at the lamp
  without draining a ball, matching the existing Power Surge debug entry.

---

## 0.16.1 — Ball save board lamp
_2026-08-05 · Contributor: JJ_

New `BallSaveLight` component drives a board lamp on/off with the ball save window, using the
same authored-material swap as the Abductor's progress lights (`sharedMaterial`, not
`material`, so nothing is instanced or leaked) rather than `BoardLight`'s color tinting.

- `DrainHandler.IsBallSaveArmed` is the source of truth: exactly one ball in play **and** that
  ball still inside its window. Multiball reads as unarmed on purpose — a drain with other balls
  still out never reaches the save logic, it just despawns, so lighting the lamp then would be
  lying at the moment the player is most likely to lose a ball.
- The lamp polls that property from `Update` and only writes materials on a state flip, matching
  `Abductor.UpdateProgressLightFlash`. No event was added to `DrainHandler` — it has no `Update`
  of its own and would need one purely to fire the window-expired edge.
- `lightRenderers` left empty falls back to the first renderer on the object or its children, so
  a bare cylinder works with only the two materials assigned.
- Scene wiring is manual: attach `BallSaveLight` to the lamp object in `Board_NA.unity` and set
  on/off to `Blue 1.mat` / `Bluesteel.mat` to match the abductor bank.

---

## 0.16.0 — Ball save: drain within 15s of launch and you keep the ball
_2026-08-05 · Contributor: JJ_

A ball lost **15 seconds or less after it was launched** is now handed straight back to the
launcher, instead of being consumed and replaced by the next ball in the hand.

- The window is timed from the plunger, not from the ball reaching the launcher: `DrainHandler`
  subscribes to the static `PinballLauncher.BallLaunched` event and stamps `Time.time` per ball
  instance. Scaled time, so a pause does not burn the window. A ball that never passed through a
  launcher (multiball splits, board-spawned balls) has no timestamp and is never savable.
- Only a genuine loss can trigger it. `OnBallDrained` gained a third `eligibleForBallSave`
  parameter that defaults to false; `ResetZone` passes true on both its branches (normal drain
  and the out-of-bounds "home run"). Balls that route through the drain flow because they
  consumed themselves by design — Molotov breaking, Holoball expiring, `DuplicatingComponent` —
  keep the default and are still spent.
- The save changes exactly one thing: which ball arrives at the launcher. The drain still runs
  its normal course — score tally, bank into round total, `DecayMultiplier`, level-up
  reconciliation. What is skipped is `ConsumeActiveBallFromLoadout`, so the loadout (and with it
  `BallsRemaining`) is untouched, and a fresh copy of the drained ball's own definition is
  pushed to the front of the hand via the new `BallSpawner.InsertHandBallAtFront`. The existing
  spawn at the end of the drain routine then serves that ball.
- `InsertHandBallAtFront` is deliberately non-animated: `AddBallAnimated` starts a layout
  coroutine that would fight `MoveBallToSpawnPointCoroutine` over the same transform.
- A saved ball re-reads its amped-up flag from `GetAmpedUpForSlot`, which otherwise only gets
  applied during `BuildHandFromPrefabs`, so an AmpUp'd ball does not silently lose it.
- Window is inspector-tunable via `ballSaveSeconds` on the `DrainHandler` GameObject in
  `GameplayCore.unity` (default 15). New serialized field absent from the scene YAML, so the
  existing instance picks up the C# initializer — no scene edit needed.
- Saves are **unlimited** and each re-serve earns a fresh 15s window on its next launch, so an
  unlucky board can in principle save the same ball repeatedly. No per-round cap was added.
- No UI or audio cue fires on a save yet — the only feedback is the same ball returning.

---

## 0.15.4 — Power Surge pays out $1–$3
_2026-08-05 · Contributor: JJ_

Triggering a Power Surge now awards a random **1 to 3 coins** (inclusive) on top of the
multiplier, with the usual gold floating text flying to the coin HUD.

- `PowerSurgeManager.AwardCoins` is called at the top of `ActivatePowerSurge`, so all
  three trigger sites — `PowerSurgePortal`, `PowerSurgeModeDuplicator` and `Abductor` —
  are covered by the one hook.
- The call sits **before** the already-active early-return, so it pays **per portal
  entry**, not per surge: re-entering while a surge is already running extends the timer
  *and* pays again. SFX, VFX, the multiplier bump and the Steam achievement are untouched
  — those still fire only on the state transition into Power Surge.
- Range is inspector-tunable via `coinRewardMin` / `coinRewardMax` on the
  `PowerSurgeManager` GameObject in `GameplayCore.unity` (defaults 1 and 3). Both are new
  serialized fields absent from the scene YAML, so the existing instance picks up the C#
  initializers — no scene edit needed to get 1–3.
- Uses `AddCoinsScaledDeferredUi` + `SpawnGoldText`, matching `CoinAdder`, so round
  modifiers that scale coin gain and Hustle's flat bonus both apply. When no
  `FloatingTextSpawner` is registered the deferred HUD sync is applied directly, since
  nothing would otherwise arrive to trigger it.

## 0.15.3 — Frenzy renamed to Power Surge (code only)
_2026-08-05 · Contributor: JJ_

Frenzy mode is now **Power Surge** throughout the C# sources — classes, fields, methods,
events, inspector `[Header]`/`[Tooltip]` text and comments. Behaviour is unchanged.

- Four scripts renamed, each with its `.cs.meta` moved alongside so the GUID is
  untouched and every scene/prefab keeps its script binding:
  `FrenzyManager` → `PowerSurgeManager`, `FrenzyPortal` → `PowerSurgePortal`,
  `FrenzyModeDuplicator` → `PowerSurgeModeDuplicator`,
  `FrenzyBoardLightController` → `PowerSurgeBoardLightController`.
- 20 files touched in total; `ActivateFrenzy` → `ActivatePowerSurge`,
  `isFrenzyActive` → `isPowerSurgeActive`, `OnFrenzyActivated`/`OnFrenzyDeactivated` →
  `OnPowerSurge…`, and so on.
- Every renamed **serialized** field carries `[FormerlySerializedAs("oldName")]` (26 of
  them) so existing scene values survive. Unity has to open **and resave** each affected
  scene/prefab before those attributes can be removed — `GameplayCore`, `Board_NA`,
  `Board_Alpha`, `Board_Spinners`, `MainMenu`, `MainMenu 1`, `Abductor.prefab`.

Deliberately still "Frenzy": the Steam achievement API name `"ACH_FIRST_FRENZY"` and the
FMOD event paths `spec_frenzy_gate` / `value_frenzy_start` (both external contracts); the
`FrenzyModeDuplicator.asset` definition, whose file name *is* its localization key
(`component.FrenzyModeDuplicator.name`) — so the shop still shows **Frenzy Mode
Duplicator** to players; the `FrenzyManager` GameObject name in `GameplayCore.unity`; and
`CFXR3 _FRENZY.prefab`. Entries below this one still say Frenzy, which is what it was
called then.

## 0.15.2 — Flint renamed to Firestarter
_2026-07-28 · Contributor: JJ_

The Entropy fire-starter ball, added as **Flint** in 0.14.0, is now **Firestarter**. Same
item, same 25% per-hit light chance — name only.

- `FlintBall.cs` → `FirestarterBall.cs`, class `FlintBall` → `FirestarterBall`,
  `DefinitionId` `"Flint"` → `"Firestarter"`. The `.cs.meta` was renamed alongside the
  script so the GUID (`2610511d…`) is unchanged and `Firestarter.prefab` keeps its
  script binding.
- `Ball-Descriptions.csv` row and `FIRE_CHARGE_REFACTOR_PLAN.md` updated; the 0.14.0
  entry below still says Flint, which is what it was called then.

Note the definition asset's `displayName` is still `Flint`, so the in-game name has not
changed yet — that field is inspector-only. With the 0.15.1 id fallback the asset's
`Id` now derives from its name and is already `Firestarter`.

## 0.15.1 — Fix: new items never reached the shop shelf
_2026-07-28 · Contributor: JJ_

The shop stopped offering the ship's and mission's allowed pool. With Silverwolf and
`Challenge_NA 1` active the shelf collapsed to **DefaultBumper and Pinball** — the only
two entries in those allow-lists that predated phase 3.

Cause: `BallDefinition.Id` and `BoardComponentDefinition.Id` returned the serialized
`id` field raw, with no fallback, unlike `PlayerShipDefinition.Id` which has always
fallen back to the asset name. Every phase 3 definition was authored with that field
blank. Both `ProgressionService.IsBallUnlocked` / `IsComponentUnlocked` and
`ProgressionConfig.IsStarterBall` / `IsStarterComponent` early-return `false` on an
empty id, so each new item was dropped from the pool **regardless** of being added to
`starterComponents` and to both allow-lists. The wiring was right; the identity was
empty.

- Both `Id` properties now fall back to the asset name, matching the ship precedent.
- No save-data risk: an empty id never matched anything, so no profile can hold one.
  Unlock *writes* go through the same property (`ProgressionTier.RewardBallId =>
  rewardBall.Id`), so reads and writes pick up the fallback together.
- Tradeoff inherited from `PlayerShipDefinition`: a name-derived id means renaming the
  asset later orphans its unlock progress. Setting an explicit `id` in the inspector
  makes the fallback inert and restores rename-safety.
- Also fixes `FrenzyModeDuplicator`, which had the same blank field.

## 0.15.0 — Status badges under every component and ball; Fire stacks additively
_2026-07-28 · Contributor: JJ_

A readout of live statuses under each object, and the stacking change that makes Fire
worth reading. Scripts only — **nothing appears on screen until three assets are built
in the editor**, see `STATUS_BADGES_SETUP.md`. Compile-checked, not playtested.

- **New badge system.** `StatusBadgeDisplay` draws every `IStatusBadgeSource` on an
  object as a row of icon-and-number pairs beneath it. Fire shows its remaining 4s
  stacks, Charge shows a consumer's bank against its requirement (`4/10`), and Cannon
  and Bomb show their fuses (`7/15`, `3/8`). Balls carry the same statuses and so get
  the same badges.
- **Adding a keyword to the readout is one interface.** Signal Beacon's Charge-10 bar
  and phase 5's Bomb fuses will implement `IStatusBadgeSource` and appear; the display
  needs no edits.
- **Charge requirements are always visible, Fire is not.** A Capacitor advertises
  `0/10` sitting idle so the player can read what it wants. Fire has no requirement and
  everything on the board is flammable, so a permanent `0` under all ~20 components
  would bury the board — it appears only while alight.
- **The row is not parented to its object.** `BoardComponent.FixedUpdate` pulses
  `localScale` while a component is selected and flippers rotate under input; a
  parented row would inherit both. It lives at the scene root, is driven to position
  each frame and billboards to the camera, which this game's camera drift
  (`CameraAliveMotion`) and point-to-point pans require anyway.
- **`StatusBadgeLibrary`** holds the icons and prefabs, loaded from `Resources` for the
  same reason `FireVfxLibrary` is: `FireStatus` and `ChargeStatus` are attached at
  runtime and can never have inspector-wired sprites.
- **Fire now stacks additively with no ceiling.** Re-lighting adds a full 4s onto the
  end of the remaining burn instead of resetting the timer to 4s. The ramp still holds
  across a re-light. Flagged for playtest: §10 already noted fire is self-propagating
  after phase 3, and uncapped additive stacking makes a permanently-ablaze board easier
  to reach, not harder. Bounded numerically by the existing 10x `maxScoreMultiplier`.
- **Fixed: a re-lit object could stop ticking entirely.** `Ignite()` zeroed
  `_tickAccumulator` on every call, so anything re-lit faster than one tick interval
  (2/s default) had the accumulator reset before it ever reached the interval and never
  activated. It is now reset only on a fresh light. Latent since 0.13.0 and made
  reachable by phase 3's spreaders.
- **Fire VFX placement.** The VFX itself already worked — it is parented to the
  object's origin, so components whose mesh sits on a child burn in the wrong spot.
  `FireStatus` gains a `vfxAnchor` to fix that per component. Deliberately with **no**
  automatic fallback: the VFX is parented to the anchor and `FireVfxLibrary` applies
  its trim as a local scale, so auto-anchoring to a child with a non-unit scale would
  silently resize the flames on every fire-capable component at once.
- **Status displays are owned by the statuses, not the utilities.** `EnsureOn` runs
  from `FireStatus.Awake` / `ChargeStatus.Awake` rather than from
  `FireStatusUtility` / `ChargeStatusUtility`. `Ignite()` is public and reachable via
  `BoardComponent.FireStatus` — Engine and Matchbox both light themselves that way,
  and prefabs that pre-carry a status never touch the utility — so hanging the display
  off the utility left those paths burning with no readout.
- **Shop merchandise is re-checked every frame, not once.** `ShopOfferShelfController`
  adds `ShopOffer3DEntry` *after* instantiating the offer prefab, and Unity runs
  `Awake` on disabled components, so a one-time check at `Awake` let shelf Cannons
  advertise a live fuse.

## 0.14.1 — Fix duplicate `_fireStatus` serialization warning
_2026-07-28 · Contributor: JJ_

Unity: _"The same field name is serialized multiple times in the class or its parent class.
This is not supported: Base(EngineComponent) _fireStatus"_.

Introduced in 0.13.0, not 0.14.0: phase 1 added a `_fireStatus` field to `BoardComponent`
while `EngineComponent` and `ShadowLampComponent` already declared their own, and phase 3's
Matchbox and Fireworks copied the same pattern. Four subclasses shadowing a base field.

- `BoardComponent` now exposes a `protected FireStatus` accessor that resolves lazily, plus a
  `public bool IsOnFire`, and is the single owner of the backing field.
- Engine, Shadow Lamp, Matchbox and Fireworks drop their own fields and read the base
  accessor. Shadow Lamp and Fireworks also lose their duplicate lazy `IsOnFire()` helpers.
- Swept every `BoardComponent` / `Bumper` subclass in the project for further field-name
  collisions with the base — none remain across all 15.

## 0.14.0 — Phase 3: the new Fire items
_2026-07-28 · Contributor: JJ_

Five new Fire items from the design doc, plus the one dependency they needed. Scripts only —
every item still needs its prefab and definition wired in the editor (see
`FIRE_CHARGE_REFACTOR_PLAN.md` §10). Compile-checked, not playtested.

- **Flint** (ball): 25% on each component hit to light it. Unlimited, which is what the low
  rate pays for — the counterweight to Fireball's five big lights.
- **Matchbox** (sling): 40% per activation to light *itself*, making it the one component that
  gets a fire going with no other source on the board.
- **Fireworks** (bumper): purely a spreader. While it is burning, 50% per activation to light
  2 random components anywhere.
- **Short Circuit** (bumper): 30% per activation to light 1 random component. Unlike Fireworks
  it does not need to be alight itself, so it is the reliable way to start a fire away from
  wherever the ball is.
- **Cannon** (sling): counts a 15-activation fuse, then fires a Cannonball and resets.
- **Cannonball** (ball): the Cannon's payload and the first Kinetic item — everything it hits
  scores on `KineticScoring`'s curve. Speed is sampled from the collision's relative velocity
  at impact, not from the rigidbody afterwards, which the bounce response has already changed.
  Listed under phase 5 in the plan but pulled forward, since a Cannon with nothing to fire is
  not testable.

Notes on two judgement calls the spec left open:

- **"On activation" means ball hits and programmatic activations alike** — burn ticks, a
  Capacitor discharge, a detonation — following the Shadow Lamp precedent. This is what makes
  a burning Fireworks a continuous source rather than a one-off, and what makes lighting the
  Cannon worthwhile.
- **Matchbox does not roll while already alight, and Short Circuit excludes itself from its
  own draw.** Both would otherwise re-light themselves on their own twice-a-second burn ticks
  and never go out. Read literally, "light on Fire" has nothing to do to something already lit.

- `StatusTargeting` now also excludes components carrying `ShopOffer3DEntry`. This was latent
  before and became live with these items: `LightRandomComponents` has no distance filter, so
  a burning Fireworks could otherwise have set the shop merchandise alight.
- Term list rewritten: `Flammable`, `Fuel`, `Ignite` and `Shock` removed; `On Fire`, `Charge`
  and `Detonate` rewritten to the new rules; `Kinetic` added; `Reinforce` added as a stub.
  Ball list updated for Fireball, Charcoal and Molotov, with Flint and Cannonball added.

## 0.13.0 — Fire and Charge rebuilt on flat, shared keyword systems
_2026-07-28 · Contributor: JJ_

Phases 1 and 2 of the keyword refactor (see `FIRE_CHARGE_REFACTOR_PLAN.md`). Core systems
plus every existing item migrated onto them; the 14 new items are phases 3-5 and are not in
this entry. **Compile-checked only — not yet playtested.** `Detonation`, `KineticScoring`,
`BoardComponentRegistry.GetRandom` and `FireStatusUtility.LightRandomComponents` are written
and compiling but have no callers until phase 3.

- **Fire is one component again.** `FireStatus` / `BallFireStatus` / `ComponentFireStatus`
  collapse into a single non-abstract `FireStatus`. Flammable ratings, Fuel, burn stacks and
  the whole per-slot stack persistence are gone. Lighting an object now starts a flat
  4-second burn that re-activates it at a serialized rate (default 2/s, design range 1-4)
  and raises its scoring by a compounding 25% per activation, resetting when it goes out.
- **Real ball hits read the burn ramp.** A component six ticks into a burn scores its hits at
  the same step its own activations do, so burning components are worth aiming at. Only fire
  ticks advance the ramp. Replaces the Engine's old trick of mutating `amountToScore` and
  unwinding the delta on burnout.
- **Re-lighting refreshes instead of no-opping.** With Fuel gone this is the only way to
  extend a burn: the timer resets to 4s and the ramp keeps climbing, which is what will make
  Fireworks and Short Circuit worth their odds once they land. Because that means a
  self-lighting item (Molotov, or a ball parked on a Lighter) never burns out, the ramp is
  clamped by a serialized `maxScoreMultiplier`, default 10x — a number chosen during
  implementation, not from the design doc, and flagged for review.
- **Automatic contact spread deleted.** Fire is granted only by things that say they grant
  it, so the per-item odds coming in phase 3 actually mean something.
- **Charge is one component too.** `BallChargeStatus` / `ComponentChargeStatus` merge into
  `ChargeStatus`. A ball holding N Charge rolls 50% on each collision to activate the N
  nearest components without spending it; hitting a component that requires Charge deposits
  instead, and never procs. Only components with a requirement hold Charge. The 2s-grace /
  -2 per second decay is removed — it made Signal Beacon's 10-Charge bar unreachable.
- **New shared systems.** `Detonation` (radius blast that activates rather than only scores,
  guarded by a per-cascade visited set plus a depth backstop), `KineticScoring`
  (`clamp((speed / 8)^2, 0.25, 8)`), `BoardComponentRegistry` (self-registering index, so
  "N nearest" and "a random component" are not per-query scene sweeps), `StatusTargeting`
  (the single Flipper/Portal exclusion, now shared by all three keywords) and
  `StatusTickGate` (hoisted out of `FireStatusUtility`, which `ChargeStatus` had been
  importing).
- `ScoreManager.WithScoreMultiplier(float, Action)` added alongside `WithWeakShake`, so an
  effect can scale a whole activation without touching a component's base score.
- **Migrated:** Engine (now just Charge 1 → light itself), Capacitor, Generator, Shadow Lamp,
  Moore's Launcher, Transistor, D-Battery, Pandora's Box.
- **Re-spec'd:** Fireball is inverted — it lights the components it strikes 5 times, rolls
  10% to reignite when spent, and no longer burns itself or detonates on burnout. Lighter and
  Matchstick Plunger light any ball now that there is nothing to qualify for. Charcoal lights
  what it touches at 50%, Molotov at 60% plus its 5% break chance; both lose their
  fuel-the-queue passives, which had no equivalent.
- **Deleted:** `FireComponent` (an older, unrelated "on fire" system on a Bumper) and
  `GasStationComponent` (built entirely on spraying Fuel board-wide).
- **Three components cut entirely**, each with its prefab, `BoardComponentDefinition` and
  every pool reference: **Fire Bumper** and **Fire Target** (the `FireComponent` pair; also
  removed from `ProgressionConfig.starterComponents` and from `content_localization.csv`) and
  **Gas Station** (removed from `ProgressionConfig.starterComponents`, Silverwolf's
  `componentPoolAllowList`, and Challenge_NA 1's `componentPoolAllowList`). No dangling GUID
  references remain; the affected pools still hold 9, 3 and 3 entries respectively. Four
  orphan `component.FireBumper.*` / `component.FireTarget.*` keys are still in the Unity
  string tables — unused and harmless, removable from the Tables window.
- Prefabs carrying the old status types were repointed at the unified `FireStatus` in YAML,
  so no prefab lost its component: Charcoal, Fireball, Unfinished Molotov, ShadowLampBumper,
  CapacitorBumper, GeneratorBumper, EngineBumper, GasStationBumper.
- `BombComponent` normalised from `new void Awake/OnCollisionEnter` to proper `override`.

## 0.12.0 — Charge item set aligned to the reworked design vault
_2026-07-27 · Contributor: Devin_
- Moore's Launcher replaces the Plasma Launcher: same 5-Charge bank on the flipper, but
  per the updated design it now creates a Transistor ball on the board instead of firing
  a projectile. `PlasmaLauncherFlipper`/`PlasmaBall` are deleted; prefab and definition
  (`MooresLauncher`) added under the new `ChargeComponents` folder.
- New Tech balls with prefabs and definitions: Transistor (on component hit, 20% chance
  to Shock itself) and D-Battery (gains 2 Charge on every plunger launch). New Rare
  Standard ball Pandora's Ball (on component hit, 20% chance to either Shock itself or
  light the struck component on Fire, fueling first so the Ignite takes).
- New Tech components with prefabs and definitions: Generator (bumper standing in for
  the sling, 30% chance per hit to Shock the ball) and Capacitor (banks 2 Charge, then
  consumes it to activate the 4 nearest components through the weak-shake path,
  portals excluded).
- Electric Floorboard cut entirely: the vault kept Electric Grounding in NOT IN
  PRODUCTION through the rework, so the component script, its three Board_NA
  placements, and the `ScoreManager` permanent-points API added for it are all
  removed. The Shock/Charge system itself is unaffected.
- Moore's Launcher gains a creation cooldown (default 2s between Transistors) after a
  playtest avalanche: created Transistors self-charge and re-feed the launcher, and an
  uncapped loop live-locked the editor at low thresholds.
- Playtest findings for design review: Moore's Launcher is extremely difficult to
  trigger as specced — its bank decays like any Charged object (2s grace, then
  -2/sec), and balls rarely return to one flipper that fast, so 5 Charge was never
  reached across several live games (best: 4/5). Verified end-to-end only via a
  lowered test threshold. Relatedly, consumers compete for the same couriers: a
  Capacitor placed upstream drains every ball to 0 before it reaches the flipper,
  which reads as counter-intuitive in play and can starve the launcher entirely.

## 0.11.0 — Shock/Charge system plus Electric Floorboard, Plasma Launcher, Engine rework
_2026-07-26 · Contributor: Devin_
- New Shock/Charge status system mirroring the fire architecture: `ChargeStatus` base with
  `BallChargeStatus`/`ComponentChargeStatus`, `ChargeStatusUtility` helpers, and `[Charge]`
  console tracing via `ChargeDebug`. Shocking an object grants Charge; an object left
  unshocked for 2 seconds bleeds 2 Charge per second (paused outside live play, same
  gating as fire ticks). Balls are the carriers: shock sources charge the ball, and
  consumer components drain the ball's whole Charge on contact.
- Electric Floorboard (roll-over, Tech): slows the ball slightly and Shocks it each pass.
  A ball that arrives already charged discharges into the board; at 1 Charge it triggers,
  consuming its Charge and permanently raising point scoring by 5% for the rest of the
  run (new `ScoreManager.AddPermanentPointsBonus`, survives round resets, cleared on a
  new run).
- Plasma Launcher (flipper upgrade, Tech): banks Charge from charged balls that strike
  the flipper; at 5 it consumes all of it and fires a plasma ball up the board. The
  projectile glides through geometry for 4 seconds activating each component it passes
  (0.5s per-component cooldown, portals skipped, weak-shake scoring path). Falls back to
  a code-built glowing sphere when no prefab is assigned.
- Engine (bumper, Entropy/Tech) reworked to the current design doc: no more seeded
  inspector charge or Flammable-to-score conversion. Charged balls discharge into it;
  once Charged it consumes every stack and Ignites itself, gaining +25 base points per
  activation while it burns and shedding all of the gained points at burn-out. Holds its
  Charge (decay permitting) if it has no Flammable stacks yet, igniting when fueled.
- Editor wiring still needed: board placements/prefabs for the two new components and
  Flammable stacks on Engine's `ComponentFireStatus` — code-only PR, verified against the
  compiler but not yet playtested.

## 0.10.4 — Quit from the pause menu no longer throws during teardown
_2026-07-26 · Contributor: JJ_
- `BoardFireFXController.OnDisable` threw a NullReferenceException every time a board scene
  was torn down. It resolved `FrenzyManager` a second time to unsubscribe, but nothing ever
  registers `FrenzyManager` with the `ServiceLocator`, so the lookup fell through to a scene
  search that returns null once the load has started. The existing guard checked
  `scoringMode`, an unrelated field. It now holds the instance resolved in `OnEnable` and
  unsubscribes from that. `OnEnable` no longer dereferences the lookup unguarded either.
- Quitting to the main menu from the pause menu now stops burning-fire FMOD loops before
  loading the scene. Those loops are attached to board GameObjects the load destroys, and
  `AudioManager` survives the load and only reaped them on its next `Update`.
- Quitting to the main menu from the pause menu now calls `GameSession.ResetSession()`,
  matching the win screen's quit button. The finished run's board plan, seed, challenge and
  ship no longer leak into the menu.
- `SceneFader` clears its pending fade-in request when the caller holds the screen black.
  The flag stayed set for the rest of the session, so the next scene load that bypassed the
  fader opened on a black screen that faded in for no reason.
- Note: a hard crash was reported on this button but has not been reproduced — the recorded
  session survived the NRE and reached the menu. The FMOD cleanup above is preventative.
  If it still crashes, reproduce with fires lit (multiplier at or above `multToIgnite`).

## 0.10.3 — Tooltip keyword panels resolve from the description text
_2026-07-24 · Contributor: JJ_
- Definition Panel 1/2 now populate from any keyword the item's description mentions,
  not just from a ball's authored tags. Board components, modules, ships, shop offers and
  the hub had no tags at all, so their keyword panels never appeared — Fire Bumper now
  pops On Fire, Lighter pops Ignite + On Fire, Frozen Bumper pops Frozen, Gas Station pops
  Fuel + On Fire, Engine Bumper pops Charge + Flammable.
- Matching is whole-word and case-insensitive with the inflections the copy actually uses
  (`Ignite` → "Ignited", `Fuel` → "Fueled", `Detonate` → "detonates"). Longer terms win an
  overlap, so a future `Fire` term can't steal a match from `On Fire`. Panels are ordered
  by where the term appears in the sentence.
- Terms are now loaded from `Resources/TermDefinitions` at runtime instead of relying on
  `Tooltip Panel.prefab`'s hand-wired `necessaryTermDefinitions`. That list was missing
  Bouncy, Frozen and Shock, and `necessaryBallDefinitions` was empty — the same wiring gap
  that already broke keyword panels once in 0.10.0. Ball tags resolve out of
  `Resources/BallDefinitions` the same way. The prefab lists are still honored as overrides.
- Stale-text fix: an unresolved tag used to leave its panel *active* showing the previous
  item's keyword. A panel is now shown only once a definition actually resolved, so
  Confetti (`Holoballs`) and Eye on The Prize (`Craft`, `Pinballs`) hide theirs rather than
  lying. Content gaps behind those: `Holoballs` and `Craft` have no asset at all and need
  term assets authored; `Pinballs` fails only because the tag is pluralized and the asset
  is `Pinball` — tag matching is deliberately exact, so one of the two names has to change.
- Tags are matched against the locale-invariant asset name first, and `DefinitionPanel`
  null-guards its text refs, so the runtime-built fallback tooltip in `TooltipManager`
  (which never assigns the panels) can no longer throw on `Show`.
- Known limitation — English only. `TermDefinition` has no `LocalizedContent` route and
  `content_localization.csv` has no `term.*` keys, so the scan builds its patterns from raw
  English display names while `BallDefinition.Description` is translated. Under a non-English
  locale the tag-driven panels still resolve (asset names are locale-invariant) but the
  description scan matches nothing. Adding `term.*` keys and routing
  `TermDefinition.DisplayName`/`Description` through `LocalizedContent` closes this.

## 0.10.2 — Lighter reworked to two-hit fuse + PR cleanup
_2026-07-23 · Contributor: Devin_
- Lighter no longer self-destructs off its own burn tick half a second after catching
  (playtest read it as the bumper just vanishing). It now explodes only when a ball hits
  it while it burns; an untouched burn refills its innate fuel so it can be lit again.
- Extra fire tracing: Lighter logs which trigger lit or popped it, Engine logs its charge
  state on spawn, AddCharge gains, and stacks it holds while uncharged.
- Playtest verified in-game: Matchstick launch strikes, cross-round stack banking,
  Charcoal/Molotov queue fueling, Molotov break, Gas Station pay/surge/reset (surges
  chain across launches via fires that survive the drain tally - flagged for balance
  review), Lighter two-hit blast, Engine stack-to-score conversion.
- Reverted all test-mode settings: Loric F1 hand and starting coins, Gas Station cost
  back to 10, board scene test placements and the Matchstick test install on the shared
  launcher prefab.

## 0.10.1 — Gas Station credit popups, plunger-ball tooltip, fire console tracing
_2026-07-23 · Contributor: Devin_
- Gas Station now spawns a floating "-10" over itself when it takes credits and a red
  "NEED 10" when the player can't pay, so a refused hit is no longer silent (it previously
  only played the failed-purchase sound).
- Hover tooltips now work on the promoted ball waiting at the plunger. Active balls are no
  longer parked on hand-slot cubes, so the slot-based lookup missed them;
  `RenderTextureRaycaster` gained a proximity fallback against `BallSpawner.ActiveBalls`
  (skipping fast-moving balls so tooltips don't flicker mid-play).
- New `FireDebug` console tracing for the whole fire system - filter the Console on
  "[Fire]". Logs every Fuel (+amount and new total), Ignite (burn duration), burn-out,
  stack banking to loadout slots, Charcoal/Molotov queue fueling, Matchstick strikes
  (including "no stacks, no light"), Lighter explosions, Gas Station payments/refusals/
  surges/resets, and Engine stack-to-score conversions. Flip `FireDebug.enabled` to
  silence.
- Loric F1 test loadout: hand reordered to Charcoal, Molotov, Fireball, Fireball, Pinball
  (fuel carriers first, igniters after) and `startingCoins` set to 100 so the Gas Station
  has credits to take. Reverted in 0.10.2.

## 0.10.0 — Remaining fire items: Matchstick, Lighter, Gas Station, Engine, Unfinished Molotov
_2026-07-23 · Contributor: Devin_
- `MatchstickPlunger` (attach to the launcher): Ignites every ball as it launches, so
  Flammable loadouts no longer depend on Fireball or board fire to get going.
- `LighterComponent` + `LighterBumper` prefab/definition: hits Ignite it (innate
  Flammable 5), and any activation while On Fire — a second hit or its own burn tick —
  destroys it, Fueling everything in `blastRadius` twice and Igniting it. The burn tick
  makes it a half-second fuse once lit, and blast-lit lighters chain.
- `GasStationComponent` + `GasStationBumper` prefab/definition: ball hits cost 10 Credits
  (via `CoinController.TrySpendCoins`) and Fuel the ball once. When five objects burn at
  once it surges — Fuels the whole board 3x and stops charging — and resets on launch.
- `EngineComponent` + `EngineBumper` prefab/definition: while Charged, Flammable stacks it
  collects convert straight to score (5 per stack) off `StacksChanged` instead of burning.
  No Shock system exists yet, so charge is inspector-seeded with `AddCharge` as the hook.
- `MolotovBall` + `Unfinished Molotov` prefab/definition/CSV row, added as a shop starter:
  contact with a component or ball Fuels both sides (other side via `fuelOtherOnContact`),
  each pour has a 1-in-20 chance to break the bottle and retire the ball through the drain
  flow, and while queued it Fuels every launched ball once (same pattern as Charcoal).
- New `FireStatusUtility` helpers `CountObjectsOnFire` / `FuelAllObjectsOnBoard` back the
  Gas Station surge; the fuel-all path routes through `CanCatchFire`, so flippers and
  portals stay fireproof.
- Tooltip fix: `Tooltip Panel.prefab`'s `necessaryTermDefinitions` only contained Odds, so
  Fireball's and Charcoal's keyword panels (Flammable / Ignite / Fuel) rendered empty.
  Wired in the Flammable, Ignite, Fuel, On Fire, Detonate, and Charge term definitions.

## 0.9.6 — Fire VFX trim + flippers and portals are fireproof
_2026-07-20 · Contributor: JJ_
- `FireVfxLibrary` now owns spawning of its own prefabs and applies a per-prefab scale and
  emission-rate trim, so the shared smoke and flames can be toned down without editing the
  CFXR assets. Smoke defaults to 0.3 scale / 0.35 emission — it was eating far too much
  screen space at full size. Per-object `fireVfxPrefab` / `fueledVfxPrefab` overrides spawn
  untrimmed, so Charcoal and Fireball are unaffected.
- Swapped the shared On Fire prefab from `CFXR Fire` to `CFXR2 Firewall A`.
- New `FireStatusUtility.CanCatchFire`: components whose `componentType` is `Flipper` or
  `Portal` never get a `ComponentFireStatus`, so they can no longer be Fueled or lit. Also
  guarded in `BallFireStatus`'s contact-ignite loop so an editor-placed status on one of
  those can't light either, and in its burn tick so a ball that is itself On Fire stops
  re-triggering a flipper or portal it happens to be resting against.

## 0.9.5 — Flame VFX for burning board components
_2026-07-20 · Contributor: JJ_
- `FireVfxLibrary` gained an `onFireVfxPrefab` slot, wired to `CFXR Fire`, and
  `FireStatus.StartFireFeedback` now falls back to it when the object has no
  `fireVfxPrefab` of its own. Board components — which are always given their
  `ComponentFireStatus` at runtime and so can never be wired in the inspector — finally
  show flames while On Fire. Charcoal and Fireball keep their own `OnFireVFX` prefab.

## 0.9.4 — Smoke VFX for Fueled objects
_2026-07-20 · Contributor: JJ_
- `Assets/Scripts/StatusEffects/FireStatus.cs` now spawns a looping smoke effect on any
  object carrying Fuel beyond its innate Flammable rating (new `IsFueled` property), so a
  board component that Charcoal has fueled visibly smolders before it ever catches. The
  smoke is parented to the object, refreshes off `StacksChanged`, and is torn down when the
  object ignites (the fire VFX takes over), burns out, or is destroyed.
- New `Assets/Scripts/StatusEffects/FireVfxLibrary.cs` + `Assets/Resources/FireVfxLibrary.asset`
  — a Resources-loaded prefab library, needed because `ComponentFireStatus` is always added
  at runtime and so can never have inspector-wired prefab fields. Points at
  `CFXR Smoke Source 3D` by default; a per-object `fueledVfxPrefab` field on `FireStatus`
  overrides it.

## 0.9.3 — Breaking-news chyron crawl (Monitor 1b political-decay screen)
_2026-07-19 · Contributor: JJ_
- New `Assets/Scripts/UI/BreakingNewsCrawl.cs` — a MonoBehaviour that procedurally builds a
  horizontal breaking-news chyron inside any RectTransform under a Canvas. A red "BREAKING"
  tag pins to the left (cycling through configurable labels like BREAKING / LIVE / ALERT /
  URGENT and pulsing between two colors on a flash timer), while a seamless two-copy
  marquee scrolls political-decay headlines right-to-left across the remainder of the
  container. Headlines, colors, scroll speed, tag width, cycle intervals, and font are all
  inspector-tunable; the crawl area auto-adds a `RectMask2D` so text is hard-clipped to its
  bounds. Meant to sit as a third slab on the Monitor 1b canvas alongside
  `StockTickerDisplay` and `StockChartDisplay`.

## 0.9.2 — Crashing single-stock line-chart display (Monitor 1b companion)
_2026-07-19 · Contributor: JJ_
- New `Assets/Scripts/UI/StockChartDisplay.cs` + `Assets/Scripts/UI/StockChartLineGraphic.cs`
  — a MonoBehaviour that procedurally builds an animated single-stock line chart inside any
  RectTransform, paired with a custom `MaskableGraphic` that draws the polyline, optional
  area fill, and optional faint grid. A sliding sample window advances on a jittered timer
  using a downward-biased random walk (with configurable plunge spikes and rare small
  bounces), and the header label continuously updates with the current price and cumulative
  window % change. All colors, thickness, grid divisions, background, and clipping are
  inspector-tunable; a `RectMask2D` is auto-added by default so the chart stays inside its
  container. Meant to sit next to `StockTickerDisplay` on the Monitor 1b canvas.

## 0.9.1 — Crashing stock-ticker display (Monitor 1b main-menu ambient screen)
_2026-07-19 · Contributor: JJ_
- New `Assets/Scripts/UI/StockTickerDisplay.cs` — a self-contained MonoBehaviour that
  procedurally builds a scrolling red stock ticker inside any RectTransform under a Canvas.
  Rows animate upward, prices tick down on a jittered timer, and a configurable "big drop"
  chance hammers a random row with a large percentage loss and pulses both the row and an
  optional background image bright red. Rows recycle as they scroll off the top; symbols and
  starting prices are randomized from serialized lists. Intended for the Monitor 1b canvas
  on `Assets/Scenes/Core/MainMenu 1.unity` (attach in the editor — no prefab required; add
  an optional `TMP_Text` header and background `Image` and assign in the inspector).

## 0.9.0 — Fire status system (Flammable / Ignite / On Fire / Fuel) + Fireball & Charcoal
_2026-07-14 · Contributor: Devin_
- New keyword-driven fire status system in `Assets/Scripts/StatusEffects/`: `FireStatus`
  (shared stacks/burn/tick core), `BallFireStatus` (contact spread both directions,
  fuel-on-contact, 0.5s re-activation of the last component hit, loadout write-back),
  `ComponentFireStatus` (added at runtime when a component is first Fueled; ticks its hit
  effect while burning), `FireStatusUtility` (get-or-add helpers + tick gating during
  shop/drain/no-run). Flammable X = X stacks = X seconds of burn once Ignited; Fuel adds a
  stack and extends an active burn; burning consumes ~1 stack/second.
- `BoardComponent.ActivateAsIfHit()`: new programmatic activation path (no ball, so no ball
  multipliers) with overrides on `Bumper` (audio, no bounce/shake), `BombComponent`
  (extracted `TryExplode()`, ticks count toward explosions), `CasinoComponent` (payout only
  every Nth activation), `FrozenComponent` (extracted `HandleHitProgression()`, ticks chip
  the freeze). `DuplicatingComponent` intentionally keeps base behavior (can't clone without
  a source ball).
- New balls: **Fireball** (`FireballBall`, Striker — launches On Fire via new
  `PinballLauncher.BallLaunched` static event, cannot be Fueled, detonates like a bomb when
  its burn ends, then retires through the drain flow) and **Charcoal** (`CharcoalBall`,
  Catalyst — fuels everything it touches; while queued, every launched ball is Fueled twice).
  CSV rows for both already existed in `Ball-Descriptions.csv`.
- Fuel persists between launches: new `_extraFlammableStacksBySlot` parallel list in
  `BallLoadoutController` (synced through all loadout mutators) +
  `BallSpawner.SyncFireStacksFromLoadout()` on hand rebuild.
- Fixed `TooltipUI` bug where the second definition panel was never populated (both tags
  rendered into the first panel).
- Known limitations: board-component definitions still have no tooltip term tags (only balls
  surface keyword panels); legacy `FireComponent` heat-up bumper is untouched and unrelated
  to the new system; prefabs/definitions for the two balls are set up in-editor (see below).
- Note: the "bump version text in MainMenu.unity" step from AGENTS.md appears stale — the
  menu label is CI-driven via `BuildVersionLabel`/`Application.version`; no `v0.x.y` scene
  text exists to update.

## 0.8.7 — Spanish localization pass on MainMenu (continued)
_2026-06-01 · Contributor: Devin_
- Cleaned up 4 typo keys in `Menu Labels` that had leading whitespace (`␣mainMenu.settings.displayMode`, `␣mainMenu.settings.resolution`, `␣mainMenu.highscore`, `␣mainMenu.rank`). Stripped the leading space in-place via direct YAML edit so the key IDs and existing translations were preserved. Also fixed two wrong English source values in the same pass: `displayMode` was set to `Display`, now `Display Mode`; `highscore` was set to `Highscore` (no colon), now `Highscore:` to match the ChallengeCard source string.
- Wired up the remaining MainMenu scene buttons (Mission Select, Progression, Close, Choose Your Ship:, Ship Name, Ship Description, Win condition, Name) plus the Play/Quit buttons in `Main Menu.prefab`, the Settings Panel labels (Left Flipper:, Right Flipper:, and others), `ChallengeCard.prefab` (Rank:/Highscore:), and `Quick Run (1).prefab` (Quick run). Added one new key `mainMenu.quit` → Quit / Salir for the Quit button.
- Renamed `mainMenu.start` back to `mainMenu.play` with English source `Play` — the Main Menu Play button was historically named that way and the rename produced a dead reference + warning. Switched the Spanish translation to `Jugar` so it fits the button width (was overflowing as `Comenzar`).
- Updated `LOCALIZATION_HANDOFF.md` with the current key inventory, what's confirmed wired, the three outstanding issues (two unwired `LocalizeStringEvent` components in MainMenu.unity, an orphan reference to the deleted old `mainMenu.play` keyId, the collection rename still not committed to disk), and a `Script-level localization needed` section covering the Progression screen (`ProgressionScreenController.cs` sets all text in code), save slot labels (`Save N` is script-concatenated), and Settings keybind labels (Input System driven, recommend leaving as-is).
- Menu-scene version text bumped to `v0.8.7`.

## 0.8.6 — Spanish localization pass on MainMenu (work in progress)
_2026-06-01 · Contributor: Devin_
- Added ~35 string keys to the existing `Menu Labels` collection (Profile screen, Credits screen, ChallengeCard, Quick Run, and full Settings Panel labels) with English source values and Spanish translations. Existing 5 keys (`mainMenu.play`/`.settings`/`.collection`/`.profile`/`.team22`) were preserved and Spanish-translated; `mainMenu.play` was deleted in favour of `mainMenu.start`.
- Wired `LocalizeStringEvent` components in `Slot.prefab` (Active / All-Time Score: / Total Wins:) and overrode the nested Button1 instance's String Reference to `mainMenu.delete`. All three save slots now switch correctly via the prefab.
- Wired the Credits screen title (`mainMenu.credits`) and role-labels body (`mainMenu.creditsBody`) on MainMenu.unity; bumped the names column's RectTransform Pos X so the longer Spanish role labels don't overlap.
- Reverted the unintended scene-level prefab overrides on the Profile instance in MainMenu.unity that were blocking Slot.prefab's wiring from propagating, and clearing the "No translation found" warning that came from an unwired LSE on the Credits title.
- Work is partial — handoff details (key list, what's not yet wired, known typo keys to clean up, the pending `MainMenu` collection rename) are in `LOCALIZATION_HANDOFF.md`. Pick up from there next session.
- Menu-scene version text bumped to `v0.8.6`.

## 0.8.5 — basic three-panel tutorial (first play, first level-up, first shop visit)
_2026-05-14 · Contributor: JJ_
- Added `Assets/Scripts/UI/BasicTutorialController.cs`: self-bootstrapping `DontDestroyOnLoad` singleton (RuntimeInitializeOnLoadMethod / BeforeSceneLoad) that owns its own ScreenSpaceOverlay canvas (sort order 9990) and builds three programmatic panels in code — no prefab wiring required. Subscribes to `GameRulesManager.RoundStarted` / `ShopAvailabilityChanged` / `ShopOpened` whenever GRM appears via `ServiceLocator` (rechecked on `SceneManager.sceneLoaded` and per-frame in Update so additive board scenes pick it up).
- Panel 1 (first play): fires on the first `RoundStarted` of the player's first run. Pauses the game (`Time.timeScale = 0`), disables `PinballLauncher` / `PinballFlipper`, shows cursor. Closes via START button → restores time/input/cursor and records `hasSeenFirstPlayTutorial`.
- Panel 2 (first level-up): fires on the first `ShopAvailabilityChanged(true)`. Same pause/lock as panel 1. **No close button** — the panel auto-closes when `ShopOpened` fires (i.e. when the player presses the lit-up shop button). The fullscreen background does NOT block the shop button because `RenderTextureRaycaster` reads `Mouse.current.position` directly and runs its own `Physics.Raycast` against the 3D `ShopButton3D` — Unity's UI raycast blocking doesn't apply. Time/input/cursor are restored before panel 3 builds so `ShopTransitionController`'s camera-pan and UI-slide coroutines (which read `Time.deltaTime`) run normally. Records `hasSeenLevelUpTutorial`.
- Panel 3 (first shop visit): fires on the first `ShopOpened`. Does NOT pause (the shop is already its own modal state). Closes via GOT IT button. Records `hasSeenShopTutorial` (existing flag, previously unused).
- Added `hasSeenFirstPlayTutorial` and `hasSeenLevelUpTutorial` bool fields to `ProfileSaveData` plus matching `HasSeen*Tutorial` / `Record*TutorialSeen` static methods on `ProfileService`. Default-false bools — no profile version bump needed; existing saves see all three tutorials on next play. The pre-existing `hasAnsweredFirstTimePlayingPrompt` flag (used by `ModifierCardPopupController` to suppress the first-round modifier card) is intentionally untouched — different semantics.
- Panel labels and buttons use the project's **Manticore** TMP font (`Manticore 14 SDF`), resolved at runtime via `Resources.FindObjectsOfTypeAll<TMP_FontAsset>()` + name-hint match (mirrors the `FpsCounterHUD` pattern). Result is cached after first lookup. Falls back to TMP default if Manticore isn't loaded by any scene reference. Panel text strings are grouped at the top of `BasicTutorialController.cs` under a clearly-marked `PANEL CONTENT` block for easy editing.
- Menu-scene version text bumped to `v0.8.5`.

## 0.8.4 — run-fail highscore + level-reached analytics events
_2026-05-13 · Contributor: JJ_
- `PinballAnalytics` now exposes `LogRunHighScore(score, boardId)` and `LogRunLevelReached(levelReached, boardId)` which record two new custom events: `runHighScore` (params: `score(long)`, `boardId(string)`) and `runLevelReached` (params: `levelReached(int, 1-based)`, `boardId(string)`). Both must be registered in the Unity Cloud Dashboard with those exact param types — note `score` is a Long, not Int, because pinball scores routinely exceed `int.MaxValue`.
- Both events fire from `GameRulesManager.ShowRoundFailed`, immediately after the existing `SteamLeaderboards.UploadScore` call, using the already-computed `capturedScore`, `boardName`, and current `roundIndex + 1`. They do **not** fire on full-run completion (`CompleteRunAndShowWinScreen`) or on quit-to-menu — only on the explicit out-of-balls fail path, since the ask was "where the player ended" when their run was over.
- Highscore per player is derived dashboard-side via `MAX(score) GROUP BY user`; max level reached via `MAX(levelReached) GROUP BY user`. The event names are descriptive of the moment (one row per failed run), not of "personal best at the time of fire" — every run-fail emits, and the dashboard does the aggregation.
- Menu-scene version text bumped to `v0.8.4`.

## 0.8.3 — shop session time tracking (subtracted from level durations)
_2026-05-13 · Contributor: JJ_
- `PinballAnalytics` now exposes `LogShopSession(levelIndex, durationSeconds, boardId)` which records a new `shopSessionCompleted` custom event. Params: `levelIndex(int, 1-based — the level the player just completed before entering this shop)`, `durationSeconds(float)`, `boardId(string)`. Must be registered in the Unity Cloud Dashboard with those exact param types before data flows.
- `GameRulesManager` stamps `_shopOpenedAt = Time.unscaledTime` in `OpenShop()` and, on `CloseShopAndAdvanceIndexOnly`, computes the elapsed, adds it to a per-level `_shopElapsedThisLevel` accumulator, and fires the `shopSessionCompleted` event. The close path is gated on the prior `_shopOpen` value so idempotent / defensive close calls don't emit phantom zero-duration sessions. Run-end paths (`CompleteRunAndShowWinScreen`, `ShowRoundFailed`, retry via `StartRun`) bypass this method, so quitting mid-shop produces no event.
- `TryProcessLevelUps` now subtracts `_shopElapsedThisLevel` from the `levelCompleted` `durationSeconds` and resets the accumulator. Level durations are now active-play time only — the shop-time caveat from 0.8.2 no longer applies. `Mathf.Max(0f, ...)` guards against negative durations from any timer skew.
- Menu-scene version text bumped to `v0.8.3`.

## 0.8.2 — level completion time analytics event
_2026-05-13 · Contributor: JJ_
- `PinballAnalytics` now exposes `LogLevelCompleted(levelIndex, durationSeconds, boardId)` which records a new `levelCompleted` custom event. Params: `levelIndex(int, 1-based)`, `durationSeconds(float)`, `boardId(string, board scene name)`. The event must be registered in the Unity Cloud Dashboard with those exact param types before data flows.
- `GameRulesManager` tracks `_currentLevelStartTime` (set in `StartRun` alongside `_runStartTime`, reset on every goal-cross inside `TryProcessLevelUps`). When the player crosses `CurrentGoal`, the duration since the previous goal-cross (or run start, for level 1) is logged and the timer is reset for the next level. Timer uses `Time.unscaledTime` to match `RunElapsedTime`.
- Only completed levels are logged — round failures and quit-mid-level produce no event. Batched level-ups inside a single `TryProcessLevelUps` call attribute the elapsed time to the first level in the batch and ~0s to subsequent ones (sum-of-durations still equals total run time).
- Caveat: level time includes any shop visit or board-load gap that fell between the previous goal-cross and this one — the clock keeps running through shop sessions. For a pure active-play metric, a future change would subtract a shop-elapsed accumulator.
- Menu-scene version text bumped to `v0.8.2`.

## 0.8.1 — shop item analytics events
_2026-05-13 · Contributor: JJ_
- Added `Assets/Scripts/Analytics/PinballAnalytics.cs`: static wrapper that initializes Unity Services + `AnalyticsService` once at startup (`RuntimeInitializeOnLoadMethod` BeforeSceneLoad) and exposes `LogShopItemShown` / `LogShopItemPurchased`. All calls are no-ops until the service is ready and never throw into gameplay.
- `ShopOfferShelfController.SpawnOfferDisplay` now logs `shopItemShown` for each offer spawned on the shelf.
- `UnifiedShopController` now logs `shopItemPurchased` from `ConfirmComponentPlacement` (covers click-confirm + drag-drop board), `AutoBuyBallOffer` (drag ball to empty slot), and `ConfirmDragDropBallReplace` (drag ball over existing slot). Logged after `_shelf.ConsumeOffer` so refunded/failed paths don't count. Mystery balls log the placeholder id on `shown` and the resolved concrete ball id on `purchased`.
- Each event sends `itemId`, `itemName`, `itemType` (Ball/BoardComponent), and `price` -- both events and their parameter schemas must be registered in the Unity Cloud Dashboard before data flows; per-item counters come from grouping by `itemId` in dashboard queries.
- Menu-scene version text bumped to `v0.8.1`.

## 0.8.0 — local playtest leaderboard
_2026-05-06 · Contributor: Devin Lopez_
- Added `Assets/Scripts/Leaderboard/{LeaderboardEntry,LeaderboardData,LocalLeaderboard}.cs`: file-backed top-N (cap 100) leaderboard at `Application.persistentDataPath/leaderboard.json`. One entry per run, sorted by score desc; persists last entered name in PlayerPrefs (`LocalLeaderboard_LastName`) for fast turn-taking on a shared dev machine.
- Added `Assets/Scripts/UI/LeaderboardPanelController.cs`: self-contained programmatic overlay panel. Shows score, a name input prefilled with the last name, then the top 10 with the new entry highlighted and a Continue button. Manages its own pause + gameplay-input lock (mirrors `WinScreenController`'s `PinballLauncher`/`PinballFlipper` disable). No prefab wiring required — built fully in code.
- `RunCompletionHelper.RecordProgressAndShowWinScreen` and `GameRulesManager.ShowRoundFailed` now show the leaderboard before the existing win/fail UI, with the original UI as the continuation callback. Both completed and drained runs submit. Existing `SteamLeaderboards.UploadScore` calls are unchanged — the local board runs in parallel and does not depend on Steam.
- Menu-scene version text bumped to `v0.8.0`.

## 0.7.6 — main-menu → gameplay fade transition
_2026-04-22 · Contributor: JJ_
- Added `SceneFader` (`Assets/Scripts/UI/SceneFader.cs`): self-bootstrapping `DontDestroyOnLoad` singleton that builds its own top-most (sort order 32000) screen-space overlay canvas + black `Image` and drives an unscaled-time fade `CanvasGroup`. No scene/prefab wiring required — spawns via `RuntimeInitializeOnLoadMethod(BeforeSceneLoad)`.
- `MainMenuUI.StartQuickRun` / `StartChallengeBoards` / `LoadMenuScene` now go through `SceneFader.FadeAndLoadScene` instead of `SceneManager.LoadScene`. Both play paths pass `holdBlackUntilReady: true` so the screen stays fully black across `GameplayCore` load + the additive board-scene load (no flash of empty core).
- `RunFlowController.StartRunFromSession` now calls `SceneFader.Instance.FadeIn()` one frame after `LoadBoard` + `StartRun` + `ResumeGameplayInput` complete, so the fade-in reveals the finished board rather than racing the additive load.
- Menu-scene version text bumped to `v0.7.6`.

## 0.7.5 — firework, drop-target, and frenzy-portal SFX hooks
_2026-04-21 · Contributor: JJ_
- `LevelUpVFXTrigger` now calls `AudioManager.PlayFireworks` at each firework spawn point during the stagger coroutine, so the level-up visual is matched by a firework SFX per burst.
- `DropTarget` plays `PlayDropTargetDown` on every ball hit (fresh hit in `OnCollisionEnter`/`OnTriggerEnter` and re-hit during rise via `BeginFallFromCurrentPosition`) and `PlayDropTargetUp` when the reset timer expires and the rise animation begins.
- `FrenzyPortal` plays `PlayFrenzyActivated` only on a successful frenzy state transition (compares `IsFrenzyActive` before and after `ActivateFrenzy`), so repeat portal entries during an active frenzy don't re-trigger the sound.

## 0.7.4 — pixelation setting, pause-menu settings access, main-menu polish, art-asset reorg
_2026-04-21 · Contributor: JJ_ (commit `fee2eda`)
**Pixelation + pause-menu settings**
- Added `PixelationSettingsManager` (auto-created singleton) that resizes the shared pixel render texture at runtime based on a saved level. Five presets: Crisp (1280x720), Smooth (960x540), Normal (640x360), Retro (400x225, default), Pixel Art (320x180). Setting persists via `PlayerPrefs` and is re-applied on each scene load.
- Added `PixelationSettingsUI` dropdown (mirrors the existing `DisplaySettingsUI`/`VolumeSettingsUI` pattern). Drops into the Settings Panel prefab with a single `TMP_Dropdown` reference.
- `PauseMenuController` now supports opening the Settings Panel from the pause menu: auto-wires a `Settings Button` under the Pause Menu panel by name, instantiates a serialized Settings Panel prefab under the pause canvas, and closes back to the pause menu when the pause action is pressed again.
- New shared `Assets/Prefabs/Settings.prefab` plus a large rework of the Main Menu `Settings Panel.prefab` (~1900-line diff) to host the new pixelation dropdown alongside the existing display/volume sections.

**Main menu polish**
- Added `SpaceshipSilverwolf` prefab instance to the main menu scene as a hero visual.
- `UIHoverBob` attached to the title so it floats (8px amplitude, 1.5 Hz bob, 1.5° rotation wobble).
- New "Team 22" label on the menu.
- Credits panel: swapped the title font credit from "Jersey 10 (Google Fonts)" to "Bacteria (somepx)" to match the new main-menu font.
- `version` text bumped to `0.7.4`.

**Art-asset reorganization**
- Moved `Fonts/`, `Materials/`, `Meshes/` (and loose sprites) under a new `Assets/ArtAssets/{Fonts,Materials,Meshes,Sprites}/` root so art is centralized instead of scattered at `Assets/` root. ~440 file moves; all references updated via GUID — no behavior change, but every prefab/scene touching those assets shows up in this commit.
- Added new fonts: **Bacteria 12**, **Desert 6**, **Manticore 14** (each with SDF assets) and a Pinballistic Steam store sprite.
- Misc ball prefab tweaks on `Gear.prefab` and `PiggyBank.prefab` that rode along with the asset move.

## 0.7.3 — Amp Up rework + tooltip runtime effects
_2026-04-20 · Contributor: JJ_
- `AmpUpBall` now destroys itself on first component hit (like the Egg) and permanently amps up the ball queued behind it in the loadout. An amped ball has a 25% chance per component hit to award +0.1 mult.
- Amped-up status persists per-loadout-slot via `BallLoadoutController._ampedUpBySlot`, survives hand rebuilds, and is re-synced to live `Ball` instances after `BallSpawner.BuildHandFromPrefabs`.
- Hover tooltips on hand balls now show the ball's active amped-up status and any pending egg multipliers (chain-multiplied across consecutive eggs queued in front).
- Removed the old AmpUp-on-drain flat-mult path (`_flatMultBonusByLoadoutSlot`, `TryApplyAmpUpBonusBehindDrainedSlot`, `ConsumePendingFlatMultBonusForLoadoutSlot`, `DrainHandler` AmpUp hook).

## 0.7.2 — retry-breaks-dragging fix
_2026-04-13 · Contributor: JJ_
- Fixed a bug where ball/offer/board-component dragging broke after dying and retrying a round. Root cause: `ServiceLocator.Get<T>()` cached a reference via its `FindAnyObjectByType` fallback and kept returning it after the Unity object was destroyed, so `RenderTextureRaycaster` saw a fake-null `UnifiedShopController` and silently blocked every drag gate.
- `ServiceLocator.Get`/`TryGet` now detect Unity-destroyed cached references, purge them, and re-resolve via the fallback.
- `UnifiedShopController` now self-registers with `ServiceLocator` in `Awake` / `OnDestroy` so the fallback path is never exercised.

### Bundled into the 0.7.3 release — contributions merged 0.7.2 → 0.7.3
_2026-04-13 to 2026-04-20 · Contributors: Devin Alvarez, DrewWhitmer, JJ_
These commits landed on `main` between the 0.7.2 and 0.7.3 changelog entries but were never given their own version bumps. Reconstructed retroactively from git (commits `e0be397`..`10220c5`).
- **Devin Alvarez — 9 new balls** (`30b9ca0`, 2026-04-13): added `Pitball`, `Snowball`, `Gear`, `Confetti` (+ `ConfettiShard` shard), `AmpUp`, `PiggyBank`, `Matryoshka`, and `CrossEyed` ball prefabs / definitions (Recall split off to a separate branch).
- **DrewWhitmer — component upgrade lights + bounce** (`e0be397`, 2026-04-13): upgrade-tier lights and bounce behavior on components.
- **DrewWhitmer — bomb component update** (`a06880d`, 2026-04-13): bomb component polish.
- **DrewWhitmer — controller support for components** (`1714324`, 2026-04-16, PR #26): gamepad navigation through the component shop/board interactions.
- **JJ — main menu tests + camera movement** (`f11843b`, `f6c9ce2`, 2026-04-16): menu-screen test scaffolding and menu camera motion pass.
- **DrewWhitmer — updated flippers** (`7619157`, 2026-04-19): flipper behavior tuning.
- **DrewWhitmer — better kickers, bumpers** (`be4bdfa`, 2026-04-19): kicker/bumper behavior + feel improvements.
- **DrewWhitmer — bouncing component upgrades** (`3d3ec82`, 2026-04-19): bouncing upgrade tier for components.
- **DrewWhitmer — focused shop** (`dc0728c`, 2026-04-20, PR #29): focused-shop layout/flow revision.
- **DrewWhitmer — new shop controller support** (`16678f6` merge / `4e19932` + `9ab57f6`, 2026-04-20): gamepad navigation through the shop + ball controller interactions.

## 0.7.1 — debug unlock-all button
_2026-04-13 · Contributor: DrewWhitmer_
- Added an "unlock everything" button to the debug menu that unlocks all balls and shop components.

## 0.7.0 — Eye On The Prize, Board_NA overhaul, ShopHub
_2026-04-12 · Contributor: JJ_
- Renamed `ChaosBall` → `EyeOnThePrizeBall` and reworked the ball prefab/behavior.
- Large `Board_NA` scene overhaul (lighting, layout, new props).
- New `ShopHub` system consolidating shop entry points.
- New `BallHandSlot` and `DropTargetResetTimerLights` board components.
- New `RenderTextureRaycaster` for UI-on-render-texture input.
- Fixed ball hand issue, removed dead code in `UnifiedShopController` and `BallSpawner`.
- Moved `ModifierPools` into `Resources/` so they load at runtime.

## 0.6.3 — frenzy mode + fireworks
_2026-04-09 to 2026-04-10 · Contributor: JJ_
- Updated drop target frenzy mode.
- Added fireworks FX.

## 0.6.2 — scoring fix
_2026-04-09 · Contributor: JJ_
- Fixed a game-breaking scoring glitch.

## 0.6.1 — FMOD restructure
_2026-04-09 · Contributors: jjanzen93, JJ_
- Restructured FMOD event naming and audio banks.
- Removed unused modifier code exposed by the audio refactor.

## 0.6.0 — modifier rework
_2026-04-09 · Contributor: JJ_
- Devil modifiers now trigger every 5 rounds.
- Omitted angel rounds and pruned unused modifier code paths.

## 0.5.2 — shop & ball particle polish
_2026-04-08 to 2026-04-09 · Contributors: JJ, DrewWhitmer_
- Added shop button and fixed several shop/gameplay bugs.
- Dynamic ball particles tied to component type and speed.
- Shop fixes and board updates.

## 0.5.1 — shop models, board UI, tooling
_2026-04-07 · Contributor: JJ_
- New shop model; ball and component type system.
- New in-game board UI.
- Added an unreferenced asset detector editor tool.

## 0.5.0 — flipper upgrades, component shop, particle refactor
_2026-04-05 · Contributors: DrewWhitmer, JJ_
- Flipper upgrades system + component shop fix.
- Ball particle script refactor; brighter particles.

## 0.4.8 — coins text fix
_2026-04-01 · Contributor: JJ_
- Fixed coins text display.

## 0.4.7 — lighting + build config
_2026-04-01 · Contributors: JJ, DrewWhitmer_
- New lighting pass across scenes.
- Removed stale tests and fixed the build name.

## 0.4.6 — game rules refactor
_2026-03-31 · Contributor: JJ_
- Split `GameRulesManager` into `DrainHandler` and `GoalScaler` for clearer separation of concerns.
- Reworked `Ball`, `ResetZone`, `DuplicatingComponent`, `AlienShip`, `ScoreManager`, and `RoundModifierController` to use the new handlers.
- Cleaned up `BasicTutorialPanelController`.

## 0.4.5 — component prefab refactor
_2026-03-31 · Contributor: JJ_
- Normalized all board component prefabs (Bumpers, Targets, Drop Target, Locked Target, Roll Over, and themed variants: Bomb, Casino, Duplicating, Fire, Frozen) to a shared structure.

## 0.4.4 — manager refactor + coin system
_2026-03-31 · Contributor: JJ_
- Extracted coin handling out of `GameRulesManager`/`ScoreManager` into a new `CoinController`.
- Updated `CoinAdder`, `ScoreJuiceFeedback`, `FloatingText`, `ComponentUIController`, and `RoundTypeIconStripUI` to use it.
- Misc tweaks to `PinballFlipper`, `BallHideController`, `BoardBackgroundMaterialSwitcher`.

## 0.4.3 — service locator standardization
_2026-03-31 · Contributor: JJ_
- Converted many singleton/`Instance` calls across the codebase to go through the service locator.
- Updated ball scripts, board components (Bumper, LockedTarget, Flipper, Launcher, Portal, ResetZone, AlienShip), FX (`AudioManager`, `CameraShake`, `HapticManager`, `ScoreJuiceFeedback`, `GoalCinematicController`), input bindings, and display settings.

## 0.4.2 — dead code cleanup
_2026-03-31 · Contributor: JJ_
- Removed unused scenes (`Game.unity`, `RolloverTest.unity`, `Test.unity`).
- Deleted unused scripts: `PointAdder`, `MultAdder`, `BoardRoundResetter`, old `DropTarget` bits.

## 0.4.1 — ships, board GUIDs, portal polish
_2026-03-31 · Contributor: JJ_
- Added `BoardComponentGuidAssigner` editor tool.
- New player ship definitions: `LoricF1`, `Silverwolf` (prefabs + assets).
- Added `Board_NA` scene and `Challenge_NA` challenge mode.
- Introduced `IBoardComponentSelectionListener`; major updates to `BoardComponent` and `MainMenu`.
- Portal Entrance prefab polish.

## 0.4.0 — baseline for automated tracking
- Starting point. Everything prior to 0.4.1 is pre-changelog history.
