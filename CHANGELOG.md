# Changelog

Version format: `0.MAJOR.MINOR`
- **MAJOR** (second digit) — big updates / new features / systems
- **MINOR** (third digit) — small changes / tweaks / fixes

Version is displayed on the main menu (`Assets/Scenes/Core/MainMenu.unity`).

Entries below 0.4.6 were reconstructed retroactively from git history (commits `ef9ce39`..`ab6744e`).

---

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
