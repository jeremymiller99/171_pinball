# Changelog

Version format: `0.MAJOR.MINOR`
- **MAJOR** (second digit) — big updates / new features / systems
- **MINOR** (third digit) — small changes / tweaks / fixes

Version is displayed on the main menu (`Assets/Scenes/Core/MainMenu.unity`).

Entries below 0.4.6 were reconstructed retroactively from git history (commits `ef9ce39`..`ab6744e`).

---

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
